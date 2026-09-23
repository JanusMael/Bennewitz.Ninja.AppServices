using Bennewitz.Ninja.AppServices.AvaloniaUI.Binding;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Bennewitz.Ninja.AppServices.Dialogs;
using Bennewitz.Ninja.AppServices.Logging;
using Serilog;
using AvaloniaLogger = Avalonia.Logging.Logger;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI;

/// <summary>
/// One-stop bootstrap for the <c>Bennewitz.Ninja.AppServices.AvaloniaUI</c>
/// diagnostics pipeline.
/// <para>
/// Intended usage is three calls in total from a host Avalonia app:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <see cref="ConfigureLogging"/> — called from <c>Program.Main</c>
///       <em>before</em> <c>BuildAvaloniaApp()</c>. Creates
///       <see cref="Log.Logger"/>, wires the rolling file sink + Trace sink,
///       and (if requested) redirects
///       <c>Avalonia.Logging.Logger.Sink</c> into Serilog.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="InstallAvaloniaHooks"/> — called from
///       <c>App.OnFrameworkInitializationCompleted</c> <em>after</em> the
///       framework has finished initialising (dispatchers, binding plugin list,
///       etc. are all live). Installs <see cref="BindingValidationErrorLogger"/> to capture
///       binding-coercion errors via the <c>DataValidationErrors.ErrorsProperty</c>
///       attached-property pipeline.
///     </description>
///   </item>
/// </list>
/// <para>
/// Every step is individually toggleable via
/// <see cref="AvaloniaDiagnosticsOptions"/>, so a host that wants only, say,
/// the file sink + Trace (no binding plugin swap) can
/// disable the pieces it doesn't need.
/// </para>
/// </summary>
public static class AvaloniaDiagnostics
{
    private static BucketedRollingFileSink? _fileSink;
    private static AvaloniaDiagnosticsOptions? _options;

    // The host-event file, and the private logger that feeds it. Deliberately NOT Log.Logger:
    // these events are outside the Serilog pipeline by design, and routing them through the
    // shared logger would put them in the app log too - the separation this exists to keep.
    private static BucketedRollingFileSink? _eventFileSink;
    private static global::Serilog.Core.Logger? _eventLogger;

    /// <summary>
    /// Application name supplied via
    /// <see cref="AvaloniaDiagnosticsOptions.AppName"/>. Exposed so host code
    /// (e.g. a crash handler) can build user-facing strings like
    /// <c>"Application Error — {AppName}"</c> without re-threading the options
    /// instance through the call stack.
    /// <para>
    /// Returns <c>null</c> before <see cref="ConfigureLogging"/> has been
    /// called.
    /// </para>
    /// </summary>
    public static string? AppName => _options?.AppName;

    /// <summary>
    /// Logs directory supplied via
    /// <see cref="AvaloniaDiagnosticsOptions.LogsDirectory"/>. Returns
    /// <c>null</c> before <see cref="ConfigureLogging"/> has been called.
    /// </summary>
    public static string? LogsDirectory => _options?.LogsDirectory;

    /// <summary>
    /// Full path of the file the rolling sink is currently writing to, or
    /// <c>null</c> if <see cref="ConfigureLogging"/> has not been called or no
    /// event has been emitted yet. Convenient for "Open current log" menu
    /// items outside of the F12 window.
    /// </summary>
    public static string? CurrentLogFilePath => _fileSink?.CurrentFilePath;

    /// <summary>
    /// Full path of the host-event file, or <see langword="null"/> when
    /// <see cref="AvaloniaDiagnosticsOptions.EnableEventLogFile"/> was not set.
    /// </summary>
    public static string? CurrentEventLogFilePath => _eventFileSink?.CurrentFilePath;

    /// <summary>
    /// Configures <see cref="Log.Logger"/> and (optionally) redirects
    /// Avalonia's internal logger into it. Safe to call only once per process;
    /// subsequent calls are ignored.
    /// <para>
    /// <strong>Must be called before <c>BuildAvaloniaApp()</c>.</strong> The
    /// Serilog pipeline needs to exist before any Avalonia type touches the
    /// logger, and <c>AvaloniaLogger.Sink</c> is a static that any later
    /// framework code may read once and cache.
    /// </para>
    /// </summary>
    /// <param name="options">Configuration. At minimum
    /// <see cref="AvaloniaDiagnosticsOptions.AppName"/> and
    /// <see cref="AvaloniaDiagnosticsOptions.LogsDirectory"/> must be
    /// supplied; everything else has a safe default.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is <c>null</c>.
    /// </exception>
    public static void ConfigureLogging(AvaloniaDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_options is not null)
        {
            return; // idempotent
        }

        _options = options;

        Directory.CreateDirectory(options.LogsDirectory);

        _fileSink = new BucketedRollingFileSink(
            options.LogsDirectory,
            options.BucketSize,
            options.Retention,
            options.FileNamePrefix);

        if (options.EnableEventLogFile)
        {
            _eventFileSink = new BucketedRollingFileSink(
                options.LogsDirectory,
                options.BucketSize,
                options.Retention,
                options.EventLogFileNamePrefix ?? "events");

            // Verbose minimum: the host decides what is worth enqueuing, not a level filter.
            _eventLogger = new LoggerConfiguration()
                           .MinimumLevel.Verbose()
                           .WriteTo.Sink(_eventFileSink)
                           .CreateLogger();
        }

        LoggerConfiguration config = new LoggerConfiguration()
                                     .MinimumLevel.Is(options.MinimumLevel)
                                     .Enrich.FromLogContext()
                                     .WriteTo.Sink(_fileSink);

        if (options.EnableTraceSink)
        {
            config = config.WriteTo.Trace();
        }

        Log.Logger = config.CreateLogger();

        if (options.BridgeAvaloniaLogger)
        {
            AvaloniaLogger.Sink = new SerilogAvaloniaSink(options.MutedAvaloniaAreas);
        }
    }

    /// <summary>
    /// Performs the post-framework-init half of the bootstrap:
    /// <list type="bullet">
    ///   <item>Installs <see cref="BindingValidationErrorLogger"/> (when
    ///         <see cref="AvaloniaDiagnosticsOptions.EnableBindingValidationLogger"/>
    ///         is set) to catch binding-coercion errors via the
    ///         <c>DataValidationErrors.ErrorsProperty</c> attached-property
    ///         pipeline.</item>
    /// </list>
    /// <para>
    /// <strong>Must be called from <c>App.OnFrameworkInitializationCompleted</c></strong>
    /// (or equivalent) <em>after</em> the framework has initialised, so the
    /// binding plugin list is in place before it is modified.
    /// </para>
    /// <para>
    /// Must be preceded by a call to <see cref="ConfigureLogging"/>. When the
    /// options instance from that call is not available, this method is a
    /// no-op.
    /// </para>
    /// </summary>
    public static void InstallAvaloniaHooks()
    {
        if (_options is null)
        {
            return;
        }

        AvaloniaDiagnosticsOptions options = _options;

        if (options.EnableBindingValidationLogger)
        {
            BindingValidationErrorLogger.Install();
        }
    }

    /// <summary>
    /// Appends one line to the host-fed event log (enabled via
    /// <see cref="AvaloniaDiagnosticsOptions.EnableEventLogFile"/>). Thread-safe,
    /// non-blocking, and a no-op when the file was not enabled. Independent of the
    /// Serilog pipeline — use it for host-domain events (e.g. file-watcher hits) that
    /// should be recorded without polluting the log.
    /// </summary>
    public static void EnqueueEvent(string line)
    {
        // The file is what survives the session and can be handed to someone else. The live
        // window this once also fed is held back from the first release.
        _eventLogger?.Information("{EventLine}", line);
    }

    /// <summary>
    /// Shows a <see cref="FatalErrorDialog"/> using <see cref="AppName"/> for
    /// the window title. Convenience wrapper for host crash handlers;
    /// identical to calling <see cref="FatalErrorDialog.ShowSafe"/> with a
    /// caller-built title. Safe to call at any time — if
    /// <see cref="ConfigureLogging"/> has not run the title falls back to a
    /// neutral <c>"Application Error"</c>.
    /// </summary>
    /// <param name="message">User-facing header shown above the exception
    /// details. <c>null</c> uses
    /// <see cref="FatalErrorDialog.DefaultMessage"/>.</param>
    /// <param name="exception">The exception whose
    /// <see cref="Exception.ToString"/> output is displayed.</param>
    public static void ShowFatalErrorDialog(string? message, Exception exception)
    {
        string title = string.IsNullOrWhiteSpace(AppName)
            ? "Application Error"
            : $"Application Error — {AppName}";
        FatalErrorDialog.ShowSafe(title, message ?? FatalErrorDialog.DefaultMessage, exception);
    }

    /// <summary>
    /// Shows a modeless, NON-fatal <see cref="NonFatalNoticeDialog"/> with a
    /// copyable body. Convenience wrapper for host informational notices (e.g.
    /// "these settings have no structured editor"); identical to calling
    /// <see cref="NonFatalNoticeDialog.ShowSafe"/>. Safe to call at any time and
    /// from any thread.
    /// </summary>
    /// <param name="title">Window title.</param>
    /// <param name="message">User-facing header shown above the body.</param>
    /// <param name="bodyText">Copyable body (e.g. a newline-separated list).</param>
    public static void ShowNonFatalNotice(string title, string message, string bodyText)
    {
        NonFatalNoticeDialog.ShowSafe(title, message, bodyText);
    }

    /// <summary>
    /// Shows a native OS error dialog (last-resort crash surface for the case
    /// where the Avalonia runtime is dead). Convenience wrapper for host
    /// crash handlers; identical to calling
    /// <see cref="NativeErrorDialog.ShowFatalError"/> with a caller-built title.
    /// </summary>
    /// <param name="message">Text to display in the dialog body.</param>
    public static void ShowNativeFatalError(string message)
    {
        string title = string.IsNullOrWhiteSpace(AppName)
            ? "Application Error"
            : $"{AppName} — Critical Error";
        NativeErrorDialog.ShowFatalError(title, message);
    }
}