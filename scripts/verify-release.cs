#!/usr/bin/env dotnet
// Pre-publication gate for the template package itself: pack it, install it FROM THE PACKED
// .nupkg, generate a repository, and assert the generated tree — all before anything is published.
//
// ⛔ Every check here asserts CONTENT, never an exit code. A template package whose content was
// flattened still installs, still lists, and still "generates" — emitting the extracted nupkg
// instead of a project. Each `dotnet` call below would return 0 on that package. The three
// structural checks exist because all three failure modes were reproduced, not imagined:
//
//     .template.config/ dropped by flattening      → content check 1
//     dotfiles excluded by NuGet's default rules   → content check 2 (NU5119)
//     LICENSE packed as a FOLDER, not a file       → content check 3
//
// ⭐ Two modes, and they differ in ONE thing: where the template comes from. Everything after the
// install — generate, assert the tree, build, test, pack, run the packaging guard — is the same
// code running the same assertions. A published check that re-implemented those assertions could
// drift from the local one and start proving something subtly different, and a release is exactly
// when you cannot afford a check that has quietly become a different check.
//
//   default       pack this working tree, assert the packed content, install from that .nupkg.
//                 Run it before tagging.
//   --published   install the id from nuget.org at the given version. Run it AFTER releasing,
//                 because it is the only mode that tests the artifact people will actually get.
//                 Packing locally and calling it "verified" proves the working tree instead.
//
// Usage:  dotnet run --file scripts/verify-release.cs
//         dotnet run --file scripts/verify-release.cs -- --published 2026.3.921
//         dotnet run --file scripts/verify-release.cs -- --keep   (keep the scratch dirs)
//
// ⚠ `--file` is required HERE and not in a generated repository. `dotnet run <file.cs>` binds to
// the project in the working directory when there is one, and this repository root holds
// Bennewitz.Ninja.Templates.csproj — so the bare form tries to run the packaging project and dies
// with "The current OutputType is 'Library'". A generated repository keeps its projects under
// src/ and tests/, so `dotnet run scripts/assert-packages.cs` resolves to the file there, which is
// why its CI says it that way and why this script invokes it that way below.

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

const string TemplateShortName = "bbpkg";
const string TemplateFolder = "bbpkg";
const string GeneratedStem = "Widget";
const string PackageId = "Bennewitz.Ninja.Templates";

bool keep = args.Contains("--keep", StringComparer.OrdinalIgnoreCase);

// ⭐ --published <version> swaps ONLY where the template comes from: nuget.org instead of a local
// pack. Everything after the install is the same code on the same assertions, which is the point.
// A published check that re-implemented the tree assertions could drift from the local one and
// start proving something slightly different — and the release is exactly when you cannot afford
// a check that has quietly become a different check.
int publishedFlag = Array.FindIndex(args, argument => string.Equals(argument, "--published", StringComparison.OrdinalIgnoreCase));
string? publishedVersion = publishedFlag >= 0 && publishedFlag + 1 < args.Length ? args[publishedFlag + 1] : null;

if (publishedFlag >= 0 && publishedVersion is null)
{
    return Fail("--published needs a version, e.g. --published 2026.3.921.");
}

string repoRoot = Directory.GetCurrentDirectory();
string packagingProject = Path.Combine(repoRoot, "Bennewitz.Ninja.Templates.csproj");

if (!File.Exists(packagingProject))
{
    return Fail($"No packaging project at '{packagingProject}'. Run this from the repository root.");
}

// The expected owner and repository come from git, never from a constant here: a check that
// hardcodes the answer it is looking for cannot notice when the repository is renamed.
(string? originOwner, string? originRepo) = OriginSlug(repoRoot);

if (originOwner is null || originRepo is null)
{
    return Fail("Could not read an origin remote of the form <owner>/<repo>. This check compares the packed metadata against it.");
}

string scratch = Path.Combine(Path.GetTempPath(), "bbpkg-verify-" + Guid.NewGuid().ToString("n")[..8]);
string packOutput = Path.Combine(scratch, "packages");
string generateRoot = Path.Combine(scratch, "generated");

Directory.CreateDirectory(packOutput);
Directory.CreateDirectory(generateRoot);

Console.WriteLine($"Scratch: {scratch}");
Console.WriteLine();

bool installed = false;

try
{
    // What `dotnet new install` is pointed at: a freshly packed file, or the published id.
    string installSource;

    if (publishedVersion is not null)
    {
        // ── 1p · The published package, straight off the feed ───────────────────────────────
        // ⚠ Nothing is packed here on purpose. Packing locally and then "verifying the release"
        // proves the working tree, not the artifact somebody will actually install — and those
        // differ exactly when it matters, as the Linux flattening showed.
        Step($"Use the published {PackageId} {publishedVersion}");
        installSource = $"{PackageId}::{publishedVersion}";
        Console.WriteLine($"  {installSource}");
    }
    else
    {

    // ── 1 · Pack the template package ───────────────────────────────────────────────────────
    Step("Pack the template package");

    if (!Dotnet(["pack", packagingProject, "-c", "Release", "--nologo", "--output", packOutput], repoRoot, out string packLog))
    {
        return Fail("dotnet pack failed:" + Environment.NewLine + packLog);
    }

    string[] packed = [.. Directory.EnumerateFiles(packOutput, "*.nupkg", SearchOption.TopDirectoryOnly)
        .Where(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))];

    if (packed.Length != 1)
    {
        return Fail($"Expected exactly one .nupkg, got {packed.Length}: {string.Join(", ", packed.Select(Path.GetFileName))}.");
    }

    string nupkg = packed[0];
    Console.WriteLine($"  packed {Path.GetFileName(nupkg)}");

    // ── 2 · Assert the packed CONTENT, not that pack exited 0 ───────────────────────────────
    Step("Assert the packed content");

    using (ZipArchive archive = ZipFile.OpenRead(nupkg))
    {
        HashSet<string> entries = [.. archive.Entries.Select(entry => entry.FullName)];

        string contentRoot = $"content/{TemplateFolder}/";

        // Check 1 — the flattening failure. Without this the package still installs and still
        // "generates", emitting the extracted nupkg rather than a project.
        string templateJson = contentRoot + ".template.config/template.json";

        if (!entries.Contains(templateJson))
        {
            return Fail(
                $"'{templateJson}' is not in the package. The template content was flattened, which drops " +
                ".template.config/ — and the resulting package still installs and still 'generates'.");
        }

        // Check 2 — NU5119. NuGet excludes dotfiles by default, so a generated repository would
        // arrive with no .gitignore and no .gitattributes: the files nobody opens.
        foreach (string dotfile in new[] { ".gitignore", ".gitattributes" })
        {
            if (!entries.Contains(contentRoot + dotfile))
            {
                return Fail($"'{contentRoot + dotfile}' is not in the package. NoDefaultExcludes is what keeps dotfiles in.");
            }
        }

        // Check 3 — the PackagePath failure. A final segment with no extension reads as a FOLDER,
        // so LICENSE packed to content/bbpkg/LICENSE/bbpkg/LICENSE and the real file vanished.
        if (!entries.Contains(contentRoot + "LICENSE"))
        {
            return Fail($"'{contentRoot}LICENSE' is not in the package as a FILE. An extensionless PackagePath segment reads as a folder.");
        }

        string[] licenseAsFolder = [.. entries.Where(entry => entry.StartsWith(contentRoot + "LICENSE/", StringComparison.Ordinal))];

        if (licenseAsFolder.Length > 0)
        {
            return Fail("LICENSE was packed as a FOLDER: " + string.Join(", ", licenseAsFolder));
        }

        Console.WriteLine($"  {entries.Count} entries; .template.config, dotfiles and LICENSE all present");

        // The nuspec has to say Template, or `dotnet new install` treats it as an ordinary package.
        XDocument nuspec = ReadNuspec(archive) ?? throw new InvalidOperationException("no .nuspec");

        string? packageType = nuspec.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "packageType")
            ?.Attribute("name")?.Value;

        if (!string.Equals(packageType, "Template", StringComparison.Ordinal))
        {
            return Fail($"Package type is '{packageType ?? "(none)"}', not 'Template'. `dotnet new install` would not register it.");
        }

        // ⛔ Metadata is permanent too. A package whose repository URL points at a DIFFERENT
        // repository is not a packaging warning — it is a wrong nuget.org listing that cannot be
        // corrected afterwards, only superseded. Compared against git, so a rename cannot rot it.
        string?[] urls =
        [
            nuspec.Descendants().FirstOrDefault(element => element.Name.LocalName == "projectUrl")?.Value,
            nuspec.Descendants().FirstOrDefault(element => element.Name.LocalName == "repository")?.Attribute("url")?.Value,
        ];

        string expected = $"{originOwner}/{originRepo}";
        string[] wrong = [.. urls.Where(url => !string.IsNullOrWhiteSpace(url) && !url!.Contains(expected, StringComparison.OrdinalIgnoreCase))!];

        if (wrong.Length > 0)
        {
            return Fail(
                $"Packed metadata points somewhere other than '{expected}': {string.Join(", ", wrong)}. " +
                "Fix Directory.Build.props before releasing — a published nuspec cannot be corrected.");
        }

        Console.WriteLine($"  package type Template; metadata points at {expected}");
    }

    installSource = nupkg;

    } // end of the local-pack path

    // ── 3 · Clear any earlier registration of THIS template ─────────────────────────────────
    // ⚠ Two registrations of one identity — say a folder install and a nupkg install — make
    // generation fail with "Sequence contains more than one matching element".
    Step("Clear earlier registrations");

    string[] registrations =
    [
        "Bennewitz.Ninja.Templates",
        Path.Combine(repoRoot, "templates", TemplateFolder),
        Path.Combine(repoRoot, "templates"),
    ];

    foreach (string candidate in registrations)
    {
        // A miss is the normal case, so its failure is not an error here.
        Dotnet(["new", "uninstall", candidate], repoRoot, out _);
    }

    // Only files belonging to THIS package are removed. Clearing the whole folder would
    // uninstall every unrelated template the developer has.
    string enginePackages = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".templateengine", "packages");

    if (Directory.Exists(enginePackages))
    {
        foreach (string stale in Directory.EnumerateFiles(enginePackages, "Bennewitz.Ninja.Templates*.nupkg"))
        {
            File.Delete(stale);
            Console.WriteLine($"  removed stale {Path.GetFileName(stale)}");
        }
    }

    // ── 4 · Install FROM THE PACKAGE, never from the folder ─────────────────────────────────
    // Installing from '.' reads the source tree and so cannot see a packaging mistake at all.
    Step("Install the template");

    // ⚠ Retried, and only in the published case. The CLI's own index lags the flat container by
    // minutes after a push, so an immediate miss means "not yet indexed", never "not published".
    // A local .nupkg has no such excuse, so it gets one attempt and a real failure.
    int attempts = publishedVersion is null ? 1 : 20;
    string installLog = string.Empty;
    bool ok = false;

    for (int attempt = 1; attempt <= attempts && !ok; attempt++)
    {
        ok = Dotnet(["new", "install", installSource], repoRoot, out installLog);

        if (!ok && attempt < attempts)
        {
            Thread.Sleep(TimeSpan.FromSeconds(30));
        }
        else if (ok && attempt > 1)
        {
            Console.WriteLine($"  installed on attempt {attempt}");
        }
    }

    if (!ok)
    {
        return Fail($"dotnet new install failed for '{installSource}':" + Environment.NewLine + installLog);
    }

    installed = true;

    // ── 5 · Generate ────────────────────────────────────────────────────────────────────────
    Step("Generate a repository");

    if (!Dotnet(["new", TemplateShortName, "-n", GeneratedStem, "--RepoOwner", originOwner], generateRoot, out string generateLog))
    {
        return Fail("dotnet new failed:" + Environment.NewLine + generateLog);
    }

    string generated = Path.Combine(generateRoot, GeneratedStem);

    if (!Directory.Exists(generated))
    {
        return Fail($"Nothing generated at '{generated}'.");
    }

    // ── 6 · Assert the generated TREE ───────────────────────────────────────────────────────
    Step("Assert the generated tree");

    string[] required =
    [
        ".gitignore",
        ".gitattributes",
        "global.json",
        "NuGet.config",
        "LICENSE",
        "README.md",
        "Directory.Build.props",
        "Directory.Packages.props",
        "packages.push",
        "packages.local",
        $"{GeneratedStem}.slnx",
        Path.Combine("scripts", "assert-packages.cs"),
        Path.Combine("docs", "publishing.md"),
        Path.Combine(".github", "workflows", "ci.yml"),
        Path.Combine(".github", "workflows", "release.yml"),
        Path.Combine("src", GeneratedStem, $"{GeneratedStem}.csproj"),
        Path.Combine("tests", $"{GeneratedStem}.Tests", $"{GeneratedStem}.Tests.csproj"),
    ];

    string[] absent = [.. required.Where(relative => !File.Exists(Path.Combine(generated, relative)))];

    if (absent.Length > 0)
    {
        return Fail("Generated tree is missing: " + string.Join(", ", absent));
    }

    // The extracted-nupkg tell. These appear when the "template" was really the package itself.
    //
    // ⚠ Matched as whole PATH SEGMENTS. A prefix test on "package" also matches packages.push and
    // packages.local, which are legitimate files this very template ships — the check would have
    // failed on a correct tree.
    string[] debrisSegments = ["_rels", "package"];

    string[] packageDebris =
    [
        .. Directory.EnumerateFileSystemEntries(generated, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(generated, path))
            .Where(relative =>
            {
                string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return segments.Any(segment => debrisSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)) ||
                    segments.Any(segment => segment.Contains("[Content_Types]", StringComparison.OrdinalIgnoreCase)) ||
                    relative.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase);
            }),
    ];

    if (packageDebris.Length > 0)
    {
        return Fail("The generated tree contains package debris, so the nupkg was extracted rather than a template applied: " + string.Join(", ", packageDebris));
    }

    // .template.config is the template's own metadata and must not travel into the generated repo.
    if (Directory.Exists(Path.Combine(generated, ".template.config")))
    {
        return Fail(".template.config/ survived into the generated tree.");
    }

    // No placeholder may survive, in a path or in a file. This is what proves the substitution
    // actually ran, rather than the engine having copied the skeleton verbatim.
    string[] tokens = ["PkgStem", "PKG_ID", "REPO_OWNER"];
    List<string> leaks = [];

    foreach (string path in Directory.EnumerateFiles(generated, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(generated, path);

        if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => tokens.Any(segment.Contains)))
        {
            leaks.Add($"{relative} (path)");
            continue;
        }

        string text = File.ReadAllText(path);

        foreach (string token in tokens.Where(token => text.Contains(token, StringComparison.Ordinal)))
        {
            leaks.Add($"{relative} (contains '{token}')");
        }
    }

    if (leaks.Count > 0)
    {
        return Fail("Unsubstituted template placeholders survived: " + string.Join(", ", leaks));
    }

    // The two derived names the template exists to get right, asserted rather than assumed.
    string csproj = File.ReadAllText(Path.Combine(generated, "src", GeneratedStem, $"{GeneratedStem}.csproj"));
    string expectedId = $"Bennewitz.Ninja.{GeneratedStem}";

    if (!csproj.Contains($"<PackageId>{expectedId}</PackageId>", StringComparison.Ordinal))
    {
        return Fail($"Generated csproj does not declare <PackageId>{expectedId}</PackageId>.");
    }

    if (!csproj.Contains($"<AssemblyName>{GeneratedStem}</AssemblyName>", StringComparison.Ordinal))
    {
        return Fail($"Generated csproj does not declare <AssemblyName>{GeneratedStem}</AssemblyName>.");
    }

    string push = File.ReadAllText(Path.Combine(generated, "packages.push"));

    if (!push.Contains(expectedId, StringComparison.Ordinal))
    {
        return Fail($"packages.push does not list '{expectedId}', so the generated release would push nothing.");
    }

    Console.WriteLine($"  tree complete, no debris, no placeholders, id {expectedId}");

    // ── 7 · The generated repository has to actually work ───────────────────────────────────
    Step("Build, test and pack the generated repository");

    if (!Dotnet(["build", $"{GeneratedStem}.slnx", "-c", "Release", "--nologo", "-warnaserror"], generated, out string buildLog))
    {
        return Fail("The generated repository does not build:" + Environment.NewLine + buildLog);
    }

    if (!Dotnet(["test", "--solution", $"{GeneratedStem}.slnx", "--no-build", "-c", "Release"], generated, out string testLog))
    {
        return Fail("The generated repository's tests fail:" + Environment.NewLine + testLog);
    }

    Console.WriteLine("  " + LastNonEmptyLines(testLog, 1));

    string generatedPackages = Path.Combine("packages", "Release");

    if (!Dotnet(["pack", $"{GeneratedStem}.slnx", "-c", "Release", "--no-build", "--nologo", "--output", generatedPackages], generated, out string generatedPackLog))
    {
        return Fail("The generated repository does not pack:" + Environment.NewLine + generatedPackLog);
    }

    // ── 8 · Its own guard, run the way its CI runs it ───────────────────────────────────────
    Step("Run the generated repository's packaging guard");

    if (!Dotnet(["run", Path.Combine("scripts", "assert-packages.cs"), "--", generatedPackages], generated, out string assertLog))
    {
        return Fail("assert-packages failed in the generated repository:" + Environment.NewLine + assertLog);
    }

    Console.WriteLine("  " + LastNonEmptyLines(assertLog, 1));

    Console.WriteLine();
    Console.WriteLine(publishedVersion is null
        ? "verify-release: PASS — packed, installed from the .nupkg, generated, and the generated repository builds, tests and packs."
        : $"verify-release: PASS — {PackageId} {publishedVersion} installed FROM NUGET.ORG, generated, and the generated repository builds, tests and packs.");

    return 0;
}
finally
{
    if (installed)
    {
        Dotnet(["new", "uninstall", "Bennewitz.Ninja.Templates"], repoRoot, out _);
    }

    if (keep)
    {
        Console.WriteLine($"Left in place: {scratch}");
    }
    else
    {
        TryDelete(scratch);
    }
}

static void Step(string title) => Console.WriteLine($"→ {title}");

static string LastNonEmptyLines(string log, int count) =>
    string.Join(
        Environment.NewLine + "  ",
        log.Split('\n').Select(line => line.TrimEnd('\r', ' ')).Where(line => line.Length > 0).TakeLast(count));

static XDocument? ReadNuspec(ZipArchive archive)
{
    ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(candidate =>
        candidate.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
        !candidate.FullName.Contains('/'));

    if (entry is null)
    {
        return null;
    }

    using Stream stream = entry.Open();
    return XDocument.Load(stream);
}

// ArgumentList, never a joined string: a path with a space is the normal case on Windows, and a
// hand-quoted command line is where that becomes a bug nobody reproduces on their own machine.
static bool Dotnet(string[] arguments, string workingDirectory, out string log)
{
    ProcessStartInfo startInfo = new()
    {
        FileName = "dotnet",
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    foreach (string argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    StringBuilder captured = new();

    using Process process = new() { StartInfo = startInfo };

    process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (captured) { captured.AppendLine(e.Data); } } };
    process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (captured) { captured.AppendLine(e.Data); } } };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    log = captured.ToString();

    return process.ExitCode == 0;
}

static (string? Owner, string? Repo) OriginSlug(string repoRoot)
{
    if (!Git(["remote", "get-url", "origin"], repoRoot, out string url))
    {
        return (null, null);
    }

    // Both forms reach the same place: git@github.com:Owner/Repo.git and https://…/Owner/Repo.git
    string trimmed = url.Trim();

    if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
    {
        trimmed = trimmed[..^4];
    }

    string[] segments = trimmed.Split(['/', ':'], StringSplitOptions.RemoveEmptyEntries);

    return segments.Length >= 2
        ? (segments[^2], segments[^1])
        : (null, null);
}

static bool Git(string[] arguments, string workingDirectory, out string log)
{
    ProcessStartInfo startInfo = new()
    {
        FileName = "git",
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    foreach (string argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using Process process = new() { StartInfo = startInfo };
    process.Start();

    log = process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();

    return process.ExitCode == 0;
}

static void TryDelete(string directory)
{
    try
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    catch (IOException)
    {
        Console.WriteLine($"Could not delete {directory}; it is only scratch.");
    }
}

static int Fail(string message)
{
    Console.Error.WriteLine("::error::" + message);
    return 1;
}
