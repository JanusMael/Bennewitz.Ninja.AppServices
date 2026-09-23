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
