using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace AppServices.Tests.EntryPoint;

/// <summary>
/// The entry point seen from outside: <c>tests/EntryPointProbe</c> run as a child process, its exit
/// code and its two streams read the way a calling script reads them.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Every test runs against the probe's framework-dependent build, and again against its native AOT
/// binary when <c>ENTRYPOINT_PROBE_NATIVE</c> names one, as CI's aot jobs do. Without it the native
/// cases skip, visibly.
/// </para>
/// <para>
/// ⚠ Signals are sent on Linux and macOS only. A console Ctrl+C cannot be sent to a child process
/// reliably on Windows, so there the 130 exit is covered in process, by <see cref="AppMainTests"/>, and
/// SIGTERM is not a Windows signal at all.
/// </para>
/// <para>
/// ⛔ A signal test asserts the probe's flush marker, never the exit code alone. .NET reports a child
/// killed by a signal as 128 plus the signal's number, so a probe that SIGINT killed outright reads 130,
/// exactly as a handled Ctrl+C does.
/// </para>
/// </remarks>
public sealed class EntryPointProbeTests
{
    private const string NativeVariable = "ENTRYPOINT_PROBE_NATIVE";

    public static TheoryData<string> Builds => ["framework-dependent", "native"];

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task Success_exits_0_with_the_result_on_stdout(string build)
    {
        Result result = await RunAsync(Probe(build), "ok");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["ok"], Lines(result.Stdout));
        Assert.Equal(["probe: flushed"], Lines(result.Stderr));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task A_failure_exits_1_with_the_fatal_report_then_OnFatal_then_the_flush_on_stderr(string build)
    {
        Result result = await RunAsync(Probe(build), "fail");

        Assert.Equal(1, result.ExitCode);
        string[] stderr = Lines(result.Stderr);
        Assert.Equal("fatal: EntryPointProbe stopped on an unhandled exception", stderr[0]);
        Assert.Contains("the probe failed on purpose", result.Stderr);
        Assert.True(Array.IndexOf(stderr, "probe: OnFatal") > 0, "OnFatal ran before the report was written.");
        Assert.Equal("probe: flushed", stderr[^1]);
        Assert.Equal("", result.Stdout);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task A_usage_error_exits_2_with_the_usage_on_stderr(string build)
    {
        Result result = await RunAsync(Probe(build), "bogus");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(
            ["error: unknown argument: bogus", "", "usage: EntryPointProbe [host|desktop] [ok|fail|wait|background|--version]", "probe: flushed"],
            Lines(result.Stderr));
        Assert.Equal("", result.Stdout);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task Version_prints_the_probe_s_PublicVersion_from_every_method(string build)
    {
        string probe = Probe(build);
        string expected = FrameworkDependentPublicVersion();

        foreach (string[] args in (string[][])[["--version"], ["host", "--version"], ["desktop", "--version"]])
        {
            Result result = await RunAsync(probe, args);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal([expected], Lines(result.Stdout));
        }
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task The_host_and_desktop_methods_exit_1_on_a_failure(string build)
    {
        string probe = Probe(build);

        foreach (string kind in (string[])["host", "desktop"])
        {
            Result result = await RunAsync(probe, kind, "fail");

            Assert.Equal(1, result.ExitCode);
            Assert.StartsWith("fatal: EntryPointProbe stopped on an unhandled exception", result.Stderr);
        }
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task A_web_host_never_rejects_an_argument(string build)
    {
        Result result = await RunAsync(Probe(build), "host", "--urls", "http://localhost:0");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["ok"], Lines(result.Stdout));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task A_crash_on_another_thread_is_reported_and_flushed_and_exits_non_zero(string build)
    {
        Result result = await RunAsync(Probe(build), "background");

        // The runtime's own exit code, not 1: changing it would take Environment.Exit.
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("fatal: EntryPointProbe stopped on an unhandled exception", result.Stderr);
        Assert.Contains("the probe failed on a background thread", result.Stderr);
        Assert.Contains("probe: OnFatal", Lines(result.Stderr));
        Assert.Contains("probe: flushed", Lines(result.Stderr));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task SIGINT_cancels_the_work_and_exits_130(string build)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "A console Ctrl+C cannot be sent to a child process reliably on Windows; AppMainTests covers 130 in process.");

        using Process process = Start(Probe(build), "wait");
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        // The probe prints this once its work is waiting on the token.
        Assert.Equal("waiting", await process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken));
        await SignalAsync(process, "-INT");

        await WaitAsync(process);
        Assert.Equal(130, process.ExitCode);

        // ⛔ The flush is the proof, not the 130: a probe that SIGINT killed outright reads 130 as well.
        Assert.Equal(["probe: flushed"], Lines(await stderr));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public async Task SIGTERM_flushes_the_log_and_the_signal_still_ends_the_process(string build)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "SIGTERM is not a Windows signal.");

        string probe = Probe(build);

        // A console run and a desktop run flush on SIGTERM themselves; a web run's host handles it.
        foreach (string[] args in (string[][])[["wait"], ["desktop", "wait"]])
        {
            using Process process = Start(probe, args);
            Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

            Assert.Equal("waiting", await process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken));
            await SignalAsync(process, "-TERM");

            await WaitAsync(process);

            // 128 + 15: SIGTERM still ended the process, as it would have without the flush.
            Assert.Equal(143, process.ExitCode);
            Assert.Equal(["probe: flushed"], Lines(await stderr));
        }
    }

    /// <summary>The probe to run: its framework-dependent build beside this one, or the native AOT binary CI names.</summary>
    private static string Probe(string build)
    {
        if (build == "native")
        {
            string? native = Environment.GetEnvironmentVariable(NativeVariable);
            Assert.SkipWhen(string.IsNullOrEmpty(native),
                $"{NativeVariable} names no native AOT build of the probe; CI's aot jobs set it.");
            Assert.True(File.Exists(native), $"{NativeVariable} names {native}, which does not exist.");
            return native!;
        }

        string probe = Path.Combine(FrameworkDependentDirectory(),
            OperatingSystem.IsWindows() ? "EntryPointProbe.exe" : "EntryPointProbe");
        Assert.True(File.Exists(probe), $"{probe} is missing; the solution build builds it.");
        return probe;
    }

    /// <summary>
    /// The PublicVersion AutoVersioning stamped on the probe, read from its framework-dependent build.
    /// The native binary must print the same one.
    /// </summary>
    private static string FrameworkDependentPublicVersion()
    {
        string assembly = Path.Combine(FrameworkDependentDirectory(), "EntryPointProbe.dll");
        return Assembly.LoadFrom(assembly).GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "PublicVersion").Value!;
    }

    /// <summary>
    /// The probe's output for this test run's configuration and framework, read off this assembly's
    /// own output directory: tests/AppServices.Tests/bin/&lt;configuration&gt;/&lt;framework&gt;/.
    /// </summary>
    private static string FrameworkDependentDirectory()
    {
        DirectoryInfo output = new(AppContext.BaseDirectory);
        return Path.Combine(RepoRoot(), "tests", "EntryPointProbe", "bin", output.Parent!.Name, output.Name);
    }

    private static async Task<Result> RunAsync(string probe, params string[] args)
    {
        using Process process = Start(probe, args);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await WaitAsync(process);
        return new Result(process.ExitCode, await stdout, await stderr);
    }

    private static Process Start(string probe, params string[] args)
    {
        ProcessStartInfo info = new(probe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        return Process.Start(info) ?? throw new InvalidOperationException($"{probe} did not start.");
    }

    /// <summary>Sends <paramref name="signal"/>, such as <c>-INT</c>, to <paramref name="process"/> through <c>kill</c>.</summary>
    private static async Task SignalAsync(Process process, string signal)
    {
        using Process kill = Process.Start("kill", [signal, process.Id.ToString(CultureInfo.InvariantCulture)])
            ?? throw new InvalidOperationException("kill did not start.");
        await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitAsync(Process process)
    {
        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("The probe did not exit within a minute; it was killed.");
        }
    }

    /// <summary>A stream's lines, whatever its line endings, with no empty last line.</summary>
    private static string[] Lines(string text) =>
        text.Length == 0 ? [] : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

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

    private sealed record Result(int ExitCode, string Stdout, string Stderr);
}
