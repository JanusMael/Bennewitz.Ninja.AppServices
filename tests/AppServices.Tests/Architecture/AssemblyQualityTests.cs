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
/// BNAQ1001 (every new cancellation token had a default) and BNAQ1004 (both <c>.Avalonia</c> namespaces
/// shadowed Avalonia's root). The rules had been published the day before; nothing ran them. A rule
/// that exists but is not run is indistinguishable from no rule. Until AssemblyQuality 2026.3.925
/// the IDs were <c>AQ1001</c>–<c>AQ1004</c>; the numbers did not change.
/// </para>
/// <para>
/// ⚠ <b>Zero findings means nothing unless something was inspected, and nothing was skipped.</b>
/// The vacuity guard here is that the scan contains EVERY shipped assembly, checked against the
/// project list. After that, each rule reports an inspected count above zero, meaning it met
/// something that could have produced a finding, and an empty <c>Skipped</c>, meaning nothing it
/// needed failed to load, so its answer is complete. An inspected count of zero is accepted only
/// where zero is true, and each such case says why.
/// </para>
/// <para>
/// BNAQ1003's forbidden references come from <see cref="LayeringTests.Tiers"/> and
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
    public void BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token()
    {
        AssemblyRuleResult result = new CancellationTokenRule().Analyze(AssemblyScanContext.Of(Shipped));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "BNAQ1001 inspected no cancellation tokens, but this "
            + "repository's launch and share contracts take them, so the scan missed them.");
        AssertNothingSkipped("BNAQ1001", result);
    }

    /// <summary>
    /// No Win32 or interop type appears in a public signature, and neither does a type from the
    /// rule's own leak-prone set.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The rule's default set alone inspects nothing here.</b> It names JSON DOM namespaces, and
    /// no shipped assembly references a JSON library, so on its own the rule reports that it had
    /// nothing to check (measured on 2026.3.925). The two namespaces added here are ones every
    /// shipped assembly really reaches: <c>AppServices</c> references <c>Microsoft.Win32.Registry</c>,
    /// and <c>System.Runtime</c> brings the rest to all four. A registry key, safe handle or
    /// marshalling type in a public signature makes a contract Windows-shaped and binds every
    /// consumer to platform plumbing it never chose.
    /// </remarks>
    [Fact]
    public void BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface()
    {
        SurfaceLeakRule rule = new(["Microsoft.Win32", "System.Runtime.InteropServices"]);
        AssemblyRuleResult result = rule.Analyze(AssemblyScanContext.Of(Shipped));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "BNAQ1002 inspected no public members, but every shipped "
            + "assembly reaches Microsoft.Win32 and System.Runtime.InteropServices, so the scan missed them.");
        AssertNothingSkipped("BNAQ1002", result);
    }

    [Fact]
    public void BNAQ1003_no_assembly_references_what_its_tier_forbids()
    {
        List<string> findings = [];

        foreach (LayeringTests.Tier tier in LayeringTests.Tiers)
        {
            Assembly assembly = Shipped.Single(a => a.GetName().Name == tier.Project);
            string[] forbidden = [.. tier.ForbiddenPackages.Concat(LayeringTests.ForeignFamilies)];

            AssemblyRuleResult result =
                new ForbiddenReferenceRule(forbidden).Analyze(AssemblyScanContext.Of(assembly));

            Assert.True(result.Inspected > 0, $"BNAQ1003 inspected no references of {tier.Project}.");
            AssertNothingSkipped($"BNAQ1003 on {tier.Project}", result);
            findings.AddRange(result.Findings.Select(f => f.ToString()));
        }

        Assert.Empty(findings);
    }

    [Fact]
    public void BNAQ1004_no_namespace_segment_shadows_a_referenced_root()
    {
        // Internal types too: the shadow is a compile error inside the declaring assembly, so it
        // bites internal code exactly as hard as public code.
        AssemblyRuleResult result =
            NamespaceShadowRule.IncludingInternalTypes().Analyze(AssemblyScanContext.Of(Shipped));

        Assert.Empty(result.Findings);
        Assert.True(result.Inspected > 0, "BNAQ1004 inspected no namespaces, so it proved nothing.");
        AssertNothingSkipped("BNAQ1004", result);
    }

    /// <summary>
    /// Fails when a rule could not load part of what it was given, naming each part in full: a clean
    /// result says nothing about what the rule never saw.
    /// </summary>
    private static void AssertNothingSkipped(string rule, AssemblyRuleResult result) =>
        Assert.True(
            result.Skipped.Count == 0,
            $"{rule} could not examine everything it was given, so its clean result is partial:\n  "
            + string.Join("\n  ", result.Skipped));

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
