using System.Xml.Linq;

namespace Templates.Tests.Packaging;

/// <summary>
/// Guards what this repository publishes, and — uniquely here — what the workflow it SHIPS would
/// publish in somebody else's repository.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A published NuGet version can never be replaced</b> — only unlisted. Everything else here
/// is recoverable; this is not. So the guard runs on every push rather than at release time, when
/// finding out is still free.
/// </para>
/// <para>
/// ⚠ <b>Two release workflows, and only one of them ever runs here.</b> Actions reads workflows at
/// the repository root only, so <c>templates/bbpkg/.github/workflows/release.yml</c> is inert
/// content until some generated repository cuts its first tag. That makes it the file most likely
/// to rot unnoticed, so the shape tests below run against <b>both</b> by path. This is the primary
/// proof the shipped workflow has; <c>verify-release</c> exercising a generated repository is the
/// other, and neither is a real publish.
/// </para>
/// </remarks>
public sealed class PackagingTests
{
    private const string RootWorkflow = ".github/workflows/release.yml";
    private const string ShippedWorkflow = "templates/bbpkg/.github/workflows/release.yml";

    /// <summary>
    /// Every packable project appears in <c>packages.push</c> or <c>packages.local</c>. Adding one
    /// and classifying it nowhere fails here rather than at the push.
    /// </summary>
    [Fact]
    public void Every_packable_project_is_classified()
    {
        HashSet<string> packable = PackableIds();
        HashSet<string> classified = [.. Ids("packages.push"), .. Ids("packages.local")];

        Assert.NotEmpty(packable);

        string[] unclassified = [.. packable.Except(classified).Order()];
        Assert.True(
            unclassified.Length == 0,
            "Packable but in neither packages.push nor packages.local: " + string.Join(", ", unclassified) +
            ". Classify it — publishing it by accident cannot be undone.");

        string[] phantom = [.. classified.Except(packable).Order()];
        Assert.True(
            phantom.Length == 0,
            "Listed but nothing packs it: " + string.Join(", ", phantom) +
            ". A stale entry means the push names a package that will not exist.");
    }

    /// <summary>
    /// No id is in both lists, which would say "publish this" and "never publish this" at once.
    /// </summary>
    [Fact]
    public void No_id_is_both_published_and_private()
    {
        string[] both = [.. Ids("packages.push").Intersect(Ids("packages.local")).Order()];

        Assert.True(both.Length == 0, "In both packages.push and packages.local: " + string.Join(", ", both));
    }

    /// <summary>
    /// Neither release workflow globs, and in both, every publishing step takes its list from
    /// <c>packages.push</c> rather than repeating it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠ <b>The ids are deliberately NOT asserted literally.</b> A workflow that repeats the list
    /// has two homes for it, and the second one silently rots when a package is added — which is
    /// the failure this whole file exists to prevent, reintroduced by the guard meant to stop it.
    /// </para>
    /// <para>
    /// ⭐ <c>gh release create</c> is covered as well as <c>dotnet nuget push</c>, and it is the one
    /// more likely to rot. Its failure is quieter: a release asset can be deleted, so a glob there
    /// never announces itself the way a permanent nuget.org id does — it just ships something
    /// nobody chose.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RootWorkflow)]
    [InlineData(ShippedWorkflow)]
    public void The_release_workflow_globs_nothing_and_publishes_what_is_declared(string relativePath)
    {
        string workflow = Workflow(relativePath);

        Assert.DoesNotContain("*.nupkg", workflow, StringComparison.Ordinal);

        int declarationReads = workflow.Split("packages.push").Length - 1;
        Assert.True(
            declarationReads >= 3,
            $"{relativePath}: the push, the GitHub release and the preflight summary should each read " +
            $"packages.push; found {declarationReads} reference(s). A step that stops reading it stops " +
            "publishing what is declared.");
    }

    /// <summary>
    /// Both publishing steps still exist in both workflows. Without this the glob test above
    /// passes vacuously against a workflow that has stopped publishing at all.
    /// </summary>
    [Theory]
    [InlineData(RootWorkflow)]
    [InlineData(ShippedWorkflow)]
    public void The_release_workflow_still_has_both_publishing_steps(string relativePath)
    {
        string workflow = Workflow(relativePath);

        Assert.Contains("dotnet nuget push", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both workflows keep the credential preflight, so credentials are exercisable before a tag
    /// makes anything permanent.
    /// </summary>
    [Theory]
    [InlineData(RootWorkflow)]
    [InlineData(ShippedWorkflow)]
    public void The_release_workflow_has_a_credential_preflight(string relativePath)
    {
        string workflow = Workflow(relativePath);

        Assert.Contains("RELEASING", workflow, StringComparison.Ordinal);
        Assert.Contains("NuGet/login", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both workflows refuse to release when <c>NUGET_USER</c> is unset, rather than skipping the
    /// push and still creating a GitHub Release for a package that never shipped.
    /// </summary>
    [Theory]
    [InlineData(RootWorkflow)]
    [InlineData(ShippedWorkflow)]
    public void The_release_workflow_refuses_to_release_without_a_nuget_user(string relativePath)
    {
        string workflow = Workflow(relativePath);

        Assert.Contains("NUGET_USER", workflow, StringComparison.Ordinal);
        Assert.Contains("exit 1", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing in <c>packages.local</c> is named anywhere in this repository's release workflow —
    /// a private package should have no path to a publishing step at all.
    /// </summary>
    [Fact]
    public void The_release_workflow_never_names_a_private_package()
    {
        string workflow = Workflow(RootWorkflow);

        foreach (string id in Ids("packages.local"))
        {
            Assert.DoesNotContain(id, workflow, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// This repository's own workflows must pass <c>--file</c> when running a C# script, and the
    /// shipped one must not need to.
    /// </summary>
    /// <remarks>
    /// ⛔ <c>dotnet run &lt;file.cs&gt;</c> binds to the PROJECT in the working directory when there
    /// is one. This repository root holds <c>Bennewitz.Ninja.Templates.csproj</c>, a Library, so
    /// the bare form dies with <i>"The current OutputType is 'Library'"</i>. A generated repository
    /// keeps its projects under <c>src/</c> and <c>tests/</c>, so nothing at its root captures the
    /// bare form — which is why the two workflows legitimately differ here, and why a reader
    /// "fixing" the inconsistency would break this one.
    /// </remarks>
    [Theory]
    [InlineData(RootWorkflow)]
    [InlineData(".github/workflows/ci.yml")]
    public void A_script_run_from_this_repository_root_passes_the_file_flag(string relativePath)
    {
        string workflow = Workflow(relativePath);

        foreach (string line in workflow.Split('\n').Where(candidate => candidate.Contains("dotnet run", StringComparison.Ordinal)))
        {
            Assert.Contains("--file", line, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Comment lines are dropped before searching, because the comments above the publishing steps
    /// explain the very glob these tests forbid — a whole-file search would fail on the explanation.
    /// </summary>
    private static string Workflow(string relativePath)
    {
        string path = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), relativePath + " does not exist, so nothing publishes — and nothing gates what would.");

        return string.Join('\n', File.ReadLines(path).Where(line => !line.TrimStart().StartsWith('#')));
    }

    /// <summary>
    /// Reads one of the two package lists, dropping blank lines and <c>#</c> comments.
    /// </summary>
    private static HashSet<string> Ids(string fileName)
    {
        string path = Path.Combine(RepoRoot(), fileName);
        Assert.True(File.Exists(path), fileName + " is missing, so nothing declares what may be published.");

        return
        [
            .. File.ReadLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
        ];
    }

    /// <summary>
    /// Package ids of every packable project belonging to THIS repository, read from the project
    /// files rather than from packed output, so the guard runs before anything has been packed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <c>templates/</c> is excluded, and that exclusion is load-bearing rather than tidiness:
    /// the skeleton's <c>src/PkgStem/PkgStem.csproj</c> is packable and declares the literal
    /// <c>PKG_ID</c>, which is a placeholder substituted at generation. Counting it would demand
    /// that this repository classify a package id that never exists.
    /// </para>
    /// <para>
    /// ⚠ <c>PackageId</c> falls back to <c>AssemblyName</c> and then the project file name when it
    /// is not set, which is how a project acquires an id nobody chose. Mirrored here so an
    /// unclassified project cannot hide behind an unset property.
    /// </para>
    /// </remarks>
    private static HashSet<string> PackableIds()
    {
        string root = RepoRoot();
        HashSet<string> ids = [];

        foreach (string project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            string[] segments = Path.GetRelativePath(root, project)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (segments.Any(segment =>
                    segment.Equals("templates", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            XDocument document = XDocument.Load(project);

            bool packable = document.Descendants("IsPackable")
                .Any(element => string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

            if (!packable)
            {
                continue;
            }

            string? declared = document.Descendants("PackageId").LastOrDefault()?.Value.Trim();
            string? assembly = document.Descendants("AssemblyName").LastOrDefault()?.Value.Trim();

            ids.Add(Pick(declared, assembly, Path.GetFileNameWithoutExtension(project)));
        }

        return ids;

        static string Pick(string? declared, string? assembly, string fallback) =>
            !string.IsNullOrWhiteSpace(declared) ? declared
            : !string.IsNullOrWhiteSpace(assembly) ? assembly
            : fallback;
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding the packaging project, so the
    /// tests do not depend on the working directory a runner happens to choose.
    /// </summary>
    /// <remarks>
    /// ⚠ Anchored on the packaging project rather than on a solution file, because this repository
    /// deliberately has none: a <c>.slnx</c> beside a <c>.csproj</c> of the same stem makes a bare
    /// <c>dotnet build</c> ambiguous.
    /// </remarks>
    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bennewitz.Ninja.Templates.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
