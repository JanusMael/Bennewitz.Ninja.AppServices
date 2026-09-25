using System.Reflection;
using System.Xml.Linq;

namespace AppServices.Tests.Architecture;

/// <summary>
/// Enforces what each package in this family is allowed to reach. Carried across from
/// ClaudeForge's <c>AssemblyLayeringTests</c> in step 5 of plan 00002, because moving the code out
/// of that repository left its guards behind.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Two checks, because either alone has a blind spot.</b> The csproj check reads the project
/// files and is the leading indicator: a bad reference is an architectural decision the moment it
/// is written. The reflection check reads compiled reference tables and catches what arrives
/// without a direct <c>ProjectReference</c> — a transitive leak, or one injected by a props file.
/// </para>
/// <para>
/// ⛔ <b>The csproj check is not redundant, and this is the part that is easy to drop.</b> The
/// compiler <b>omits unused references from the assembly reference table entirely</b>, so a
/// declared-but-not-yet-used bad reference is invisible to reflection. That is precisely the state
/// a violation is in immediately before someone starts depending on it — when it is cheapest to
/// fix. ClaudeForge learned this by having only the reflection check and watching a live bad
/// reference sail through it.
/// </para>
/// <para>
/// ⭐ The invariant worth protecting most is that <c>AppServices.Abstractions</c> reaches
/// <b>nothing</b>. It is the package a test or a non-UI host consumes; the day it grows a reference
/// is the day the split stops paying for itself.
/// </para>
/// </remarks>
public sealed class LayeringTests
{
    /// <summary>What one project in this family is allowed to reach.</summary>
    /// <param name="Project">Project name, which is also its directory and file name.</param>
    /// <param name="MayReferenceProjects">Every project reference it is allowed to declare.</param>
    /// <param name="ForbiddenPackages">
    /// Package-id prefixes it must never declare, with the tier reason.
    /// </param>
    // Internal, not private: AssemblyQualityTests reads this same table for BNAQ1003, so the tiers
    // have ONE home. A second copy would be the list that silently rots.
    internal sealed record Tier(string Project, string[] MayReferenceProjects, string[] ForbiddenPackages);

    internal static readonly Tier[] Tiers =
    [
        // Zero outgoing edges, deliberately. Not "few" — none.
        new("AppServices.Abstractions", [], ["Avalonia", "Serilog", "CommunityToolkit"]),

        // Needs an operating system, not a UI framework.
        new("AppServices", ["AppServices.Abstractions"], ["Avalonia", "Serilog", "CommunityToolkit"]),

        // A sink is inherently a Serilog thing and inherently not a UI thing.
        new("AppServices.Logging", [], ["Avalonia", "CommunityToolkit"]),

        // The only tier allowed to see Avalonia.
        new("AppServices.AvaloniaUI",
            ["AppServices.Abstractions", "AppServices", "AppServices.Logging"],
            ["CommunityToolkit"]),
    ];

    /// <summary>
    /// Name fragments belonging to another repository. ⚠ The two families share no edge — measured,
    /// not assumed — so either could be versioned or abandoned without touching the other. A
    /// reference here would quietly end that.
    /// </summary>
    internal static readonly string[] ForeignFamilies = ["ScopedEditors", "LayeredEditors", "ClaudeForge", "AgentForge"];

    [Fact]
    public void Every_tier_named_here_actually_exists()
    {
        // ⛔ Without this, renaming a project turns every assertion below into a no-op pass — the
        // classic way an architecture test stops testing anything while staying green.
        foreach (Tier tier in Tiers)
        {
            Assert.True(
                File.Exists(ProjectFile(tier.Project)),
                $"{tier.Project} is named by these tests but does not exist. Either it was renamed "
                + "(update this file) or it is gone, in which case its layering is unguarded.");
        }
    }

    [Fact]
    public void No_project_reaches_outside_its_tier()
    {
        List<string> violations = [];

        foreach (Tier tier in Tiers)
        {
            foreach (string reference in ProjectReferences(tier.Project))
            {
                // Every path segment, not just the file name: a project file need not be named
                // after its directory, so "../AppServices.AvaloniaUI/Renamed.csproj" is a real
                // violation a file-name-only check waves through.
                string[] segments = reference.Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                string referenced = Path.GetFileNameWithoutExtension(segments[^1]);

                if (!tier.MayReferenceProjects.Contains(referenced, StringComparer.Ordinal))
                {
                    violations.Add($"{tier.Project} -> {referenced}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A project declares a reference its tier does not allow:\n  "
            + string.Join("\n  ", violations)
            + "\n\nMove the shared type down a tier rather than pointing a lower tier at a higher "
            + "one. AppServices.Abstractions in particular must reach nothing at all.");
    }

    [Fact]
    public void No_project_declares_a_package_its_tier_forbids()
    {
        List<string> violations = [];

        foreach (Tier tier in Tiers)
        {
            foreach (string package in PackageReferences(tier.Project))
            {
                foreach (string forbidden in tier.ForbiddenPackages)
                {
                    if (package.StartsWith(forbidden, StringComparison.Ordinal))
                    {
                        violations.Add($"{tier.Project} -> {package}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A project declares a package reference its tier forbids:\n  "
            + string.Join("\n  ", violations)
            + "\n\nAppServices.Logging touching Avalonia is the one that matters most: a sink that "
            + "needs a window is a UI type wearing a sink's name, which is exactly how "
            + "LiveLogWindowSink ended up unable to ship.");
    }

    [Fact]
    public void Nothing_here_reaches_another_family()
    {
        List<string> violations = [];

        foreach (Tier tier in Tiers)
        {
            IEnumerable<string> all = ProjectReferences(tier.Project).Concat(PackageReferences(tier.Project));

            foreach (string reference in all)
            {
                foreach (string foreign in ForeignFamilies)
                {
                    if (reference.Contains(foreign, StringComparison.Ordinal))
                    {
                        violations.Add($"{tier.Project} -> {reference}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A project in this family references another one:\n  " + string.Join("\n  ", violations)
            + "\n\nThe two families share no edge, which is what lets either be versioned or "
            + "abandoned without touching the other.");
    }

    [Fact]
    public void No_compiled_assembly_reaches_another_family()
    {
        // Catches what a csproj cannot: a transitive leak, or a reference injected by a props or
        // targets file rather than written in the project.
        string output = Path.GetDirectoryName(typeof(LayeringTests).Assembly.Location)!;
        string[] assemblies = [.. Directory.GetFiles(output, "AppServices*.dll")];

        Assert.True(
            assemblies.Length > 0,
            $"No AppServices assembly in {output} — this test cannot see what it is guarding.");

        List<string> violations = [];

        foreach (string path in assemblies)
        {
            string name;
            AssemblyName[] referenced;
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                name = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(path);
                referenced = assembly.GetReferencedAssemblies();
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
            {
                continue; // not a managed assembly we can inspect
            }

            foreach (AssemblyName reference in referenced)
            {
                string referencedName = reference.Name ?? string.Empty;

                if (ForeignFamilies.Any(f => referencedName.Contains(f, StringComparison.Ordinal)))
                {
                    violations.Add($"{name} -> {referencedName}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "A compiled assembly references another family:\n  " + string.Join("\n  ", violations)
            + "\n\nThis one fires without a direct ProjectReference, so check props/targets files "
            + "and transitive references too.");
    }

    [Fact]
    public void Abstractions_compiles_against_nothing_but_the_framework()
    {
        // The strongest statement this family makes, asserted against the compiled output rather
        // than the project file, so a props-injected reference cannot slip past it.
        string output = Path.GetDirectoryName(typeof(LayeringTests).Assembly.Location)!;
        string path = Path.Combine(output, "AppServices.Abstractions.dll");

        Assert.True(File.Exists(path), $"AppServices.Abstractions.dll is not in {output}.");

        string[] nonFramework =
        [
            .. Assembly.LoadFrom(path)
                .GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                            && !n.Equals("netstandard", StringComparison.Ordinal)
                            && !n.Equals("mscorlib", StringComparison.Ordinal)),
        ];

        Assert.True(
            nonFramework.Length == 0,
            "AppServices.Abstractions has grown a reference:\n  " + string.Join("\n  ", nonFramework)
            + "\n\nIt is the package a test or a non-UI host consumes. Reaching anything is the "
            + "change that quietly undoes the split.");
    }

    // ── reading the project files ────────────────────────────────────────────

    private static string ProjectFile(string project) =>
        Path.Combine(RepoRoot(), "src", project, project + ".csproj");

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        Attributes(project, "ProjectReference");

    private static IReadOnlyList<string> PackageReferences(string project) =>
        Attributes(project, "PackageReference");

    private static IReadOnlyList<string> Attributes(string project, string element) =>
    [
        .. XDocument.Load(ProjectFile(project))
            .Descendants()
            .Where(e => e.Name.LocalName == element)
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!),
    ];

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
