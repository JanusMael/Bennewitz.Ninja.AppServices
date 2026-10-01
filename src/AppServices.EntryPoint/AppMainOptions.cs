namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>
/// What an app hands <see cref="AppMain"/> beside its work: its usage text and the callbacks that
/// connect the entry point to the app's own logging and dialogs.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Every callback is optional, and none is needed for the report.</b> The fatal report is
/// written to stderr before any callback runs, so a failure before logging is configured, or a
/// logger that fails itself, still leaves the report behind.
/// </para>
/// <para>
/// ⚠ <b>A callback that throws is reported on stderr and changes nothing else</b>: not the exit
/// code, and not the callbacks after it.
/// </para>
/// </remarks>
public sealed class AppMainOptions
{
    /// <summary>
    /// The usage text a console app prints on stderr, after a <see cref="UsageException"/>'s message,
    /// before exiting 2. Ignored by the web and desktop kinds, which own no command line.
    /// </summary>
    public string? Usage { get; init; }

    /// <summary>
    /// Flushes the app's log. Runs once per run: when the run ends however it ends, when the runtime
    /// is terminating on an exception from another thread, or when the process exits without the run
    /// ending, as on SIGTERM.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>A run flushes once, and a console or desktop run does it the moment SIGTERM arrives</b>,
    /// then lets the signal end the process. An app that handles SIGTERM itself and goes on running,
    /// a generic host for one, logs its shutdown after that flush, and nothing flushes it again; after
    /// <c>Log.CloseAndFlush</c> those events are dropped. Such an app belongs in
    /// <see cref="AppMain.RunHostAsync"/>, whose host owns SIGTERM.
    /// </remarks>
    public Action? FlushLog { get; init; }

    /// <summary>
    /// Called with an exception that ends the run as a failure, after its report is on stderr: to log
    /// it, or to show a dialog. Also called for an exception on another thread that is terminating the
    /// process.
    /// </summary>
    public Action<Exception>? OnFatal { get; init; }

    /// <summary>
    /// Called with a task exception nobody observed, after its report is on stderr. It does not end
    /// the run.
    /// </summary>
    public Action<Exception>? OnUnobservedTaskException { get; init; }
}
