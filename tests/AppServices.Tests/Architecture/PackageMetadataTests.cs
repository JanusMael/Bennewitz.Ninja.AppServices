using System.Reflection;
using System.Xml.Linq;

namespace AppServices.Tests.Architecture;

/// <summary>
/// Guards the metadata every published package carries. Carried across from ClaudeForge's
/// <c>PackageMetadataTests</c> in step 5 of plan 00002.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A published version can never be replaced</b>, so a nuspec that says <c>TODO</c> says it
/// forever. The template ships those placeholders deliberately — they are a prompt, not a default —
/// and the only thing standing between a prompt and a permanent one is a check that reads them.
/// </para>
/// <para>
/// ⚠ Distinct from the template's <c>PackagingTests</c>, which asks whether every packable project
/// is <i>classified</i> for publishing. This asks whether what gets published is <i>describable</i>.
/// </para>
/// </remarks>
public sealed class PackageMetadataTests
{
    [Fact]
    public void There_is_something_to_check()
    {
        // ⛔ Every assertion below iterates the project list. An empty list passes all of them, so
        // a restructure that moves src/ elsewhere would silently retire this whole class.
        Assert.NotEmpty(ProjectFiles());
    }

    [Fact]
    public void Every_project_states_IsPackable_explicitly()
    {
        List<string> silent =
        [
            .. ProjectFiles()
                .Where(p => Property(p, "IsPackable") is null)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(n => n!),
        ];

        Assert.True(
            silent.Count == 0,
            "These projects do not say whether they are packable:\n  " + string.Join("\n  ", silent)
            + "\n\nInheriting the default means the answer changes when the default does, and the "
            + "direction that costs something is publishing an id nobody meant to publish.");
    }

    [Fact]
    public void Every_assembly_name_matches_its_project_file()
    {
        List<string> mismatched = [];

        foreach (string path in ProjectFiles())
        {
            string expected = Path.GetFileNameWithoutExtension(path);
            string? actual = Property(path, "AssemblyName");

            if (actual is not null && !string.Equals(actual, expected, StringComparison.Ordinal))
            {
                mismatched.Add($"{expected}.csproj declares AssemblyName {actual}");
            }
        }

        Assert.True(
            mismatched.Count == 0,
            "An assembly is not named after its project:\n  " + string.Join("\n  ", mismatched)
            + "\n\nThe layering guard matches assemblies by name, so a mismatch takes a project out "
            + "of its own scan without failing anything.");
    }

    [Fact]
    public void Every_packable_project_describes_itself()
    {
        List<string> undescribed = [];

        foreach (string path in ProjectFiles())
        {
            if (!string.Equals(Property(path, "IsPackable"), "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path);

            foreach (string element in (string[])["Description", "PackageTags"])
            {
                string? value = Property(path, element);

                if (string.IsNullOrWhiteSpace(value))
                {
                    undescribed.Add($"{name}: {element} is missing");
                }
                else if (value.Contains("TODO", StringComparison.OrdinalIgnoreCase))
                {
                    // ⭐ The one that actually fires. `dotnet new bbpkg` emits TODO here on purpose,
                    // and nothing else would notice it until the package was on nuget.org.
                    undescribed.Add($"{name}: {element} still says TODO");
                }
            }
        }

        Assert.True(
            undescribed.Count == 0,
            "A package would publish without usable metadata:\n  " + string.Join("\n  ", undescribed)
            + "\n\nDescription is what a consumer reads on nuget.org before deciding, and PackageTags "
            + "is how they arrive there at all.");
    }

    [Fact]
    public void Every_shipped_assembly_is_marked_trimmable()
    {
        // ⛔ Read off the COMPILED assembly, never the project file. IsTrimmable is inherited from
        // src/Directory.Build.props, so no csproj mentions it; and what a consumer's TrimMode=partial
        // publish obeys is the mark inside the DLL and nothing else. The first releases of this
        // family shipped without it, and no build or test noticed -- which is why this exists.
        string output = Path.GetDirectoryName(typeof(PackageMetadataTests).Assembly.Location)!;
        List<string> unmarked = [];

        foreach (string project in ProjectFiles().Select(Path.GetFileNameWithoutExtension).Select(n => n!))
        {
            string path = Path.Combine(output, project + ".dll");

            // Without this, a project missing from the output would be skipped rather than
            // checked, and the guard would pass on whatever it happened to find.
            Assert.True(File.Exists(path), $"{project}.dll is not in {output}, so its mark cannot be checked.");

            bool marked = Assembly.LoadFrom(path)
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(a => a.Key == "IsTrimmable"
                          && string.Equals(a.Value, "True", StringComparison.OrdinalIgnoreCase));

            if (!marked)
            {
                unmarked.Add(project);
            }
        }

        Assert.True(
            unmarked.Count == 0,
            "These shipped assemblies are not marked trimmable:\n  " + string.Join("\n  ", unmarked)
            + "\n\nAn app publishing with TrimMode=partial trims ONLY marked assemblies, so these "
            + "would ship whole and outside its trim analysis. Check src/Directory.Build.props.");
    }

    private static string? Property(string projectFile, string name) =>
        XDocument.Load(projectFile)
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == name)
            ?.Value
            .Trim();

    private static IReadOnlyList<string> ProjectFiles() =>
        [.. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories)];

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
