using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// The two extension points that replace the in-package log windows: <c>ConfigureLogger</c> for the
/// application log and <c>EventListener</c> for host-fed events.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ They exist because severing the held-back windows from <c>AvaloniaDiagnostics</c> left a host
/// with no way to feed a viewer of its own — the first consumer's viewer would have shipped empty.
/// Neither names a window or a key; how a viewer is shown is the host's business.
/// </para>
/// <para>
/// ⚠ <c>AvaloniaDiagnostics</c> is a static, configure-once bootstrap, so every test starts from
/// <c>ResetForTests</c> and puts the global <see cref="Log.Logger"/> back afterwards. This assembly
/// runs serially (see Parallelization.cs), which is what makes that safe.
/// </para>
/// </remarks>
public sealed class AvaloniaDiagnosticsHookTests : IDisposable
{
    private readonly ILogger _originalLogger = Log.Logger;
    private readonly string _logs =
        Path.Combine(Path.GetTempPath(), "bb-appservices-hooks-" + Guid.NewGuid().ToString("N"));

    public AvaloniaDiagnosticsHookTests() => AvaloniaDiagnostics.ResetForTests();

    public void Dispose()
    {
        Log.Logger = _originalLogger;
        AvaloniaDiagnostics.ResetForTests();
        try
        {
            Directory.Delete(_logs, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void A_sink_attached_through_ConfigureLogger_receives_the_application_log()
    {
        CollectingSink sink = new();
        AvaloniaDiagnostics.ConfigureLogging(Options(configure: c => c.WriteTo.Sink(sink)));

        Log.Information("hello {Who}", "hook");

        LogEvent logged = Assert.Single(sink.Events);
        Assert.Equal("hello {Who}", logged.MessageTemplate.Text);
    }

    [Fact]
    public void ConfigureLogger_extends_the_pipeline_rather_than_replacing_it()
    {
        // The built-in file sink must still receive events when the host adds its own; a hook that
        // silently displaced the file would lose the one log that survives a crash.
        CollectingSink sink = new();
        AvaloniaDiagnostics.ConfigureLogging(Options(configure: c => c.WriteTo.Sink(sink)));

        Log.Information("to both");

        Assert.Single(sink.Events);
        Assert.NotNull(AvaloniaDiagnostics.CurrentLogFilePath);
    }

    [Fact]
    public void ConfigureLogger_can_lower_one_source_below_MinimumLevel()
    {
        // A minimum per source needs no option of this package's own: Serilog's override, applied
        // here, lets one category through at Debug while the rest of the log keeps Information, and
        // the file records each line at the level it was logged at. A library's interaction lines at
        // Debug, in an app whose log keeps Information, are the case that asked.
        AvaloniaDiagnostics.ConfigureLogging(Options(configure: c =>
            c.MinimumLevel.Override("Host.Interaction", LogEventLevel.Debug)));

        Log.Information("the app's own line");
        Log.ForContext(Constants.SourceContextPropertyName, "Host.Interaction").Debug("the user clicked");
        Log.ForContext(Constants.SourceContextPropertyName, "Host.Other").Debug("another source");
        Log.Debug("the app at debug");

        string? path = AvaloniaDiagnostics.CurrentLogFilePath;
        AvaloniaDiagnostics.ResetForTests(); // disposes the file sink, which flushes it
        Assert.NotNull(path);
        string written = File.ReadAllText(path);

        Assert.Contains("[INF] the app's own line", written);
        Assert.Contains("[DBG] the user clicked", written);
        Assert.DoesNotContain("another source", written);
        Assert.DoesNotContain("the app at debug", written);
    }

    [Fact]
    public void The_listener_receives_every_enqueued_event_even_without_the_event_file()
    {
        List<string> seen = [];
        AvaloniaDiagnostics.ConfigureLogging(Options(listener: seen.Add));

        AvaloniaDiagnostics.EnqueueEvent("one");
        AvaloniaDiagnostics.EnqueueEvent("two");

        Assert.Equal(["one", "two"], seen);
    }

    [Fact]
    public void A_throwing_listener_does_not_escape_EnqueueEvent()
    {
        AvaloniaDiagnostics.ConfigureLogging(
            Options(listener: _ => throw new InvalidOperationException("the listener's own bug")));

        Assert.Null(Record.Exception(() => AvaloniaDiagnostics.EnqueueEvent("still fine")));
    }

    [Fact]
    public void Without_a_listener_EnqueueEvent_is_a_quiet_no_op()
    {
        AvaloniaDiagnostics.ConfigureLogging(Options());

        Assert.Null(Record.Exception(() => AvaloniaDiagnostics.EnqueueEvent("nobody is listening")));
    }

    private AvaloniaDiagnosticsOptions Options(
        Action<LoggerConfiguration>? configure = null,
        Action<string>? listener = null) =>
        new()
        {
            AppName = "hook-tests",
            LogsDirectory = _logs,
            EnableTraceSink = false,
            // Avalonia's logger sink is process-wide and not the subject here.
            BridgeAvaloniaLogger = false,
            ConfigureLogger = configure,
            EventListener = listener,
        };

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
