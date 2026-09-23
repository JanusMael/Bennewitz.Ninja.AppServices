using System.Reflection;
using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;

namespace AppServices.Tests.Architecture;

/// <summary>
/// The family's own assembly rules, <c>Bennewitz.Ninja.AssemblyQuality</c>, run over every assembly
/// this repository ships.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Added after 2026.3.923 shipped two violations that nobody here knew to look for:</b>
/// AQ1001 (every new cancellation token had a default) and AQ1004 (both <c>.Avalonia</c> namespaces
/// shadowed Avalonia's root). The rules had been published the day before; nothing ran them. A rule
/// that exists but is not run is indistinguishable from no rule.
/// </para>
/// <para>
/// ⚠ <b>Zero findings means nothing unless something was inspected.</b> The vacuity guard here is
/// that the scan contains EVERY shipped assembly, checked against the project list. After that, an
/// inspected count of zero is accepted only where zero is true, and each such case says why.
/// </para>
/// <para>
/// AQ1003's forbidden references come from <see cref="LayeringTests.Tiers"/> and
/// <see cref="LayeringTests.ForeignFamilies"/>, not from a copy: one table, two checks.
/// </para>
/// </remarks>
public sealed class AssemblyQualityTests
{
    private static readonly string[] ProjectNames = LoadProjectNames();
    private static readonly Assembly[] Shipped = [.. ProjectNames.Select(LoadFromOutput)];

    [Fact]
    public void Every_shipped_assembly_is_in_the_scan()
    {
        Assert.NotEmpty(ProjectNames);
        Assert.Equal(ProjectNames.Length, Shipped.Length);
    }

    [Fact]
    public void AQ1001_no_public_method_takes_a_defaulted_cancellation_token()
    {
        AssemblyRuleResult result = new CancellationTokenRule().Analyze(AssemblyScanContext.Of(Shipped));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "AQ1001 inspected no cancellation tokens, but this "
            + "repository's launch and share contracts take them, so the scan missed them.");
    }

    [Fact]
    public void AQ1002_no_leak_prone_type_appears_in_the_public_surface()
    {
        AssemblyRuleResult result = new SurfaceLeakRule().Analyze(AssemblyScanContext.Of(Shipped));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "AQ1002 inspected no public members, so it proved nothing.");
    }

    [Fact]
    public void AQ1003_no_assembly_references_what_its_tier_forbids()
    {
        List<string> findings = [];

        foreach (LayeringTests.Tier tier in LayeringTests.Tiers)
        {
            Assembly assembly = Shipped.Single(a => a.GetName().Name == tier.Project);
            string[] forbidden = [.. tier.ForbiddenPackages.Concat(LayeringTests.ForeignFamilies)];

            AssemblyRuleResult result =
                new ForbiddenReferenceRule(forbidden).Analyze(AssemblyScanContext.Of(assembly));

            Assert.True(result.Inspected > 0, $"AQ1003 inspected no references of {tier.Project}.");
            findings.AddRange(result.Findings.Select(f => f.ToString()));
        }

        Assert.Empty(findings);
    }

    [Fact]
    public void AQ1004_no_namespace_segment_shadows_a_referenced_root()
    {
        AssemblyRuleResult result = new NamespaceShadowRule().Analyze(AssemblyScanContext.Of(Shipped));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "AQ1004 inspected no namespaces, so it proved nothing.");
    }

    private static Assembly LoadFromOutput(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
        Assert.True(File.Exists(path), $"{name}.dll is not in the test output, so it cannot be scanned.");
        return Assembly.LoadFrom(path);
    }

    private static string[] LoadProjectNames() =>
    [
        .. Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Select(n => n!)
            .Order(StringComparer.Ordinal),
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
