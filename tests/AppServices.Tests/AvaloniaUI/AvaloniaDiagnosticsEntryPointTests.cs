using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.Dialogs;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="AvaloniaDiagnostics.EntryPointOptions"/>: what an Avalonia app's entry point does with a
/// failure, with a task exception nobody observed, and with its log.
/// </summary>
/// <remarks>
/// ⚠ The native dialog is suppressed and recorded (<c>NativeErrorDialog.SuppressForTests</c>), and the
/// static bootstrap is reset around every test, as <see cref="AvaloniaDiagnosticsHookTests"/> does.
/// This assembly runs serially (Parallelization.cs), which is what makes that safe.
/// </remarks>
public sealed class AvaloniaDiagnosticsEntryPointTests : IDisposable
{
    private readonly ILogger _originalLogger = Log.Logger;
    private readonly TextWriter _originalError = Console.Error;
    private readonly string _logs =
        Path.Combine(Path.GetTempPath(), "bb-appservices-entry-" + Guid.NewGuid().ToString("N"));
    private readonly CollectingSink _sink = new();

    public AvaloniaDiagnosticsEntryPointTests()
    {
        AvaloniaDiagnostics.ResetForTests();
        NativeErrorDialog.SuppressForTests = true;

        // The entry point's own report goes to stderr; it is not the subject here.
        Console.SetError(new StringWriter());

        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = "entry-tests",
            LogsDirectory = _logs,
            EnableTraceSink = false,
            // Avalonia's logger sink is process-wide and not the subject here.
            BridgeAvaloniaLogger = false,
            ConfigureLogger = configuration => configuration.WriteTo.Sink(_sink),
        });
    }

    public void Dispose()
    {
        Console.SetError(_originalError);
        NativeErrorDialog.Reset();
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
    public void A_failure_is_logged_as_fatal_and_shown_in_the_native_dialog()
    {
        InvalidOperationException failure = new("the window failed to open");

        AvaloniaDiagnostics.EntryPointOptions().OnFatal!(failure);

        LogEvent logged = Assert.Single(_sink.Events, e => e.Level == LogEventLevel.Fatal);
        Assert.Same(failure, logged.Exception);
        Assert.Equal("entry-tests — Critical Error", NativeErrorDialog.LastSuppressedCall.Title);
        Assert.Contains("the window failed to open", NativeErrorDialog.LastSuppressedCall.Message);
    }

    [Fact]
    public void A_task_exception_nobody_observed_is_logged_as_a_warning()
    {
        InvalidOperationException unobserved = new("nobody awaited this");

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogEvent logged = Assert.Single(_sink.Events);
        Assert.Equal(LogEventLevel.Warning, logged.Level);
        Assert.Same(unobserved, logged.Exception);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void A_desktop_run_that_fails_at_start_up_logs_it_shows_the_dialog_and_exits_1()
    {
        // The case bbavalonia's own Main never showed a dialog for: a failure escaping start-up.
        int code = AppMain.RunDesktop(typeof(AvaloniaDiagnosticsEntryPointTests).Assembly, [],
            _ => throw new InvalidOperationException("start-up failed"),
            AvaloniaDiagnostics.EntryPointOptions());

        Assert.Equal(1, code);
        Assert.Contains(_sink.Events, e => e.Level == LogEventLevel.Fatal);
        Assert.Contains("start-up failed", NativeErrorDialog.LastSuppressedCall.Message);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
