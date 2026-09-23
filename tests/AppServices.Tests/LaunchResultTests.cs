using Bennewitz.Ninja.AppServices;
using Bennewitz.Ninja.AppServices.Abstractions;

namespace AppServices.Tests;

/// <summary>
/// Step 4 of plan 00002 replaced eight bare <see cref="bool"/> returns and three <c>void</c>s with
/// <see cref="LaunchResult"/>. These assert the distinctions that change actually bought, because a
/// richer type nobody can tell apart at the call site would be the same defect in more code.
/// </summary>
public sealed class LaunchResultTests
{
    [Fact]
    public void Unsupported_is_distinguishable_from_failed()
    {
        // ⭐ The distinction the whole type exists for. RevealInFileManager on a phone is
        // Unsupported and the caller must HIDE the affordance; a launch that threw is Failed and
        // the caller should report it. A bool collapses both to false.
        LaunchResult unsupported = LaunchResult.Unsupported("no file manager");
        LaunchResult failed = LaunchResult.Failed("the process would not start");

        Assert.NotEqual(unsupported.Status, failed.Status);
        Assert.NotEqual(unsupported, failed);
        Assert.False(unsupported.Succeeded);
        Assert.False(failed.Succeeded);
    }

    [Fact]
    public void Every_status_is_distinct_and_only_one_succeeds()
    {
        LaunchStatus[] all = Enum.GetValues<LaunchStatus>();

        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.Single(all, s => new LaunchResult(s).Succeeded);
    }

    [Fact]
    public void Default_reads_as_success_rather_than_a_silent_failure()
    {
        // Succeeded is deliberately 0. An unassigned result is then wrong LOUDLY -- a caller acts
        // on a success that did not happen -- rather than quietly swallowing a real failure.
        Assert.True(default(LaunchResult).Succeeded);
        Assert.Equal(LaunchStatus.Succeeded, default(LaunchResult).Status);
    }

    [Fact]
    public async Task A_cancelled_token_yields_Cancelled_and_never_launches()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        ShellLauncher launcher = new();

        LaunchResult url = await launcher.LaunchUrlAsync("https://example.invalid", cts.Token);
        LaunchResult terminal = await launcher.LaunchTerminalWithCommandAsync("echo hi", cts.Token);

        Assert.Equal(LaunchStatus.Cancelled, url.Status);
        Assert.Equal(LaunchStatus.Cancelled, terminal.Status);
    }

    [Fact]
    public async Task An_empty_url_is_Failed_and_says_why()
    {
        LaunchResult result = await new ShellLauncher().LaunchUrlAsync("   ", TestContext.Current.CancellationToken);

        Assert.Equal(LaunchStatus.Failed, result.Status);
        Assert.Equal("url is empty", result.Detail);
    }

    [Fact]
    public async Task A_relative_path_is_rejected_as_a_caller_error()
    {
        // Failed rather than NotFound: the path was never resolved, so nothing was looked for.
        LaunchResult result = await new ShellLauncher().OpenInDefaultEditorAsync("relative/file.txt", TestContext.Current.CancellationToken);

        Assert.Equal(LaunchStatus.Failed, result.Status);
        Assert.Equal("path is not absolute", result.Detail);
    }

    [Fact]
    public async Task A_throwing_diagnostic_sink_does_not_break_the_launch()
    {
        // A sink is consumer code on this thread. A diagnostic must never become the cause of the
        // failure it was describing.
        //
        // ⚠ Asserts only that nothing ESCAPES. Whether the launch itself succeeds is platform
        // business -- Windows throws Win32Exception 2 for a missing file under UseShellExecute,
        // while `xdg-open` starts happily and fails inside itself -- so asserting on the status
        // here would pass on one CI leg and fail on another.
        ShellLauncher launcher = new((_, _, _) => throw new InvalidOperationException("sink is broken"));

        LaunchResult result = await launcher.OpenInDefaultEditorAsync(
            Path.Combine(Path.GetTempPath(), "bb-appservices-does-not-exist.txt"),
            TestContext.Current.CancellationToken);

        Assert.IsType<LaunchResult>(result);
    }

    [Fact]
    public async Task The_sink_is_told_which_operation_failed()
    {
        // ⛔ Windows only, and SKIPPED rather than vacuously passed elsewhere. Reaching the sink
        // needs a deterministic throw, and only Windows gives one here: UseShellExecute against a
        // missing file raises Win32Exception 2. An Assert.All over a list that can be empty would
        // go green on every platform while proving nothing, which is the shape this repository
        // argues against everywhere else.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "only Windows fails deterministically here");

        List<(DiagnosticLevel Level, string Message, Exception? Error)> seen = [];
        ShellLauncher launcher = new((level, message, error) => seen.Add((level, message, error)));

        LaunchResult result = await launcher.OpenInDefaultEditorAsync(
            Path.Combine(Path.GetTempPath(), "bb-appservices-does-not-exist.txt"),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchStatus.NotFound, result.Status);

        (DiagnosticLevel Level, string Message, Exception? Error) entry = Assert.Single(seen);
        Assert.Equal(DiagnosticLevel.Warning, entry.Level);
        Assert.Contains("opening a file in the default editor", entry.Message);
        Assert.Contains("NotFound", entry.Message);
        Assert.NotNull(entry.Error);
    }
}
