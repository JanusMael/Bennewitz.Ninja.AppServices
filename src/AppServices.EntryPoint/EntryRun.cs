using System.Reflection;
using System.Runtime.InteropServices;

namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>The kinds of app <see cref="AppMain"/> runs, which differ only in who owns what.</summary>
internal enum AppKind
{
    /// <summary>Owns its command line and its Ctrl+C.</summary>
    Console,

    /// <summary>A web host: its arguments are configuration, and its host owns shutdown.</summary>
    Host,

    /// <summary>No console: its window's close is its cancellation.</summary>
    Desktop,
}

/// <summary>
/// One run of an entry point: its hooks, its Ctrl+C, its reports, its flush and the exit code it ends
/// with.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Everything is per run, nothing is shared between runs.</b> A test host runs one entry point
/// many times in a process, sometimes two at once, and each run owns its own callbacks. The cost is a
/// duplicate report when two overlapping runs both see one process-wide crash.
/// </para>
/// <para>
/// ⛔ <b>First match wins</b> when a run ends: a <c>HostAbortedException</c> passes through
/// untouched (the caller's exception filter); after Ctrl+C a console run exits 130 however its work
/// ended; a console <see cref="UsageException"/> exits 2; any other exception is a failure and exits 1;
/// otherwise the work's own code stands.
/// </para>
/// </remarks>
internal sealed class EntryRun : IDisposable
{
    internal const int Failure = 1;
    internal const int UsageError = 2;
    internal const int Cancelled = 130;
    internal const string VersionArgument = "--version";

    private static readonly AsyncLocal<EntryRun?> s_current = new();

    private readonly AppKind _kind;
    private readonly AppMainOptions _options;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly EntryRun? _previous;
    private readonly PosixSignalRegistration? _sigterm;
    private int _flushed;
    private int _ctrlCPresses;

    private EntryRun(Assembly program, AppKind kind, AppMainOptions options)
    {
        Program = program;
        AppName = program.GetName().Name ?? "The application";
        _kind = kind;
        _options = options;

        // ⛔ First, before the version check or the work: a failure in either must be reported.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        if (_kind == AppKind.Console)
        {
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        // A web host handles SIGTERM itself and lets its run end, which flushes; the other two kinds
        // would otherwise be killed by it with their log unflushed.
        if (_kind != AppKind.Host)
        {
            _sigterm = RegisterSigterm();
        }

        _previous = s_current.Value;
        s_current.Value = this;
    }

    /// <summary>
    /// The run the calling code is inside, if any. Test-only: it is how a test reaches
    /// <see cref="PressCtrlC"/> from inside the work it hands a console run.
    /// </summary>
    internal static EntryRun? Current => s_current.Value;

    internal Assembly Program { get; }

    internal string AppName { get; }

    internal CancellationToken Token => _cancellation.Token;

    private bool CancelledByCtrlC => _kind == AppKind.Console && Volatile.Read(ref _ctrlCPresses) > 0;

    internal static EntryRun Start(Assembly program, AppKind kind, AppMainOptions? options)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new EntryRun(program, kind, options ?? new AppMainOptions());
    }

    /// <summary><c>--version</c>, and only when it is the only argument.</summary>
    internal static bool AsksForVersion(string[] args) => args is [VersionArgument];

    /// <summary>
    /// Whether <paramref name="exception"/> is a design-time tool stopping the entry point. By name,
    /// as <c>HostFactoryResolver</c> checks it: it throws the public
    /// <c>Microsoft.Extensions.Hosting.HostAbortedException</c> when that loads, and otherwise a
    /// private type of the same name.
    /// </summary>
    internal static bool IsHostAborted(Exception exception) =>
        exception.GetType().Name == "HostAbortedException";

    /// <summary>
    /// Prints the release version on stdout and returns 0, or reports that there is none and returns 1.
    /// </summary>
    /// <remarks>
    /// ⛔ <c>PublicVersion</c>, never <c>AssemblyInformationalVersion</c>, which AutoVersioning sets to
    /// "Built with ♥ &lt;commit&gt;". There is no fallback: a missing one is a build defect, and any
    /// other number would be a version nobody released.
    /// </remarks>
    internal int AnswerVersion()
    {
        string? version = null;
        foreach (AssemblyMetadataAttribute metadata in Program.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (metadata.Key == "PublicVersion")
            {
                version = metadata.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            Error($"error: {AppName} carries no [AssemblyMetadata(\"PublicVersion\")], so it has no release version to print");
            return Failure;
        }

        Console.Out.WriteLine(version);
        return 0;
    }

    /// <summary>The exit code for work that returned <paramref name="returned"/>.</summary>
    internal int Completed(int returned) => CancelledByCtrlC ? Cancelled : returned;

    /// <summary>The exit code for work that threw <paramref name="exception"/>, after reporting it.</summary>
    internal int Failed(Exception exception)
    {
        if (CancelledByCtrlC)
        {
            // Ctrl+C was pressed, so the run is cancelled however its work ended. A cancellation is
            // the expected way to end; anything else is still worth seeing.
            if (exception is not OperationCanceledException)
            {
                Error($"fatal: {AppName} stopped on an unhandled exception after Ctrl+C", exception);
            }

            return Cancelled;
        }

        if (_kind == AppKind.Console && exception is UsageException usage)
        {
            Error(string.IsNullOrWhiteSpace(_options.Usage)
                ? $"error: {usage.Message}"
                : $"error: {usage.Message}{Environment.NewLine}{Environment.NewLine}{_options.Usage}");
            return UsageError;
        }

        ReportFatal(exception);
        return Failure;
    }

    /// <summary>Flushes the app's log, once per run whichever of its callers comes first.</summary>
    internal void Flush()
    {
        if (Interlocked.Exchange(ref _flushed, 1) == 0 && _options.FlushLog is { } flush)
        {
            Invoke(nameof(AppMainOptions.FlushLog), flush);
        }
    }

    /// <summary>
    /// What pressing Ctrl+C does: the first press cancels the token and is intercepted; any later one
    /// is not, so the runtime ends a run that did not stop.
    /// </summary>
    /// <returns>Whether this press is intercepted.</returns>
    internal bool PressCtrlC()
    {
        if (Interlocked.Increment(ref _ctrlCPresses) != 1)
        {
            return false;
        }

        try
        {
            _cancellation.Cancel();
        }
        catch (Exception exception)
        {
            // A callback registered on the token threw, or the run has just ended. Either way this
            // runs on the runtime's signal thread, where an escaping exception would end the process.
            Error($"warning: {AppName}: cancellation did not complete cleanly", exception);
        }

        return true;
    }

    public void Dispose()
    {
        _sigterm?.Dispose();
        if (_kind == AppKind.Console)
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        s_current.Value = _previous;
        _cancellation.Dispose();
    }

    private void ReportFatal(Exception exception)
    {
        Error($"fatal: {AppName} stopped on an unhandled exception", exception);
        if (_options.OnFatal is { } onFatal)
        {
            Invoke(nameof(AppMainOptions.OnFatal), () => onFatal(exception));
        }
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Exception exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"A non-exception object was thrown: {e.ExceptionObject}");

        ReportFatal(exception);

        // The runtime ends the process when this returns, and no finally will run.
        if (e.IsTerminating)
        {
            Flush();
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Error($"warning: {AppName} had a task exception nobody observed", e.Exception);
        if (_options.OnUnobservedTaskException is { } onUnobserved)
        {
            Invoke(nameof(AppMainOptions.OnUnobservedTaskException), () => onUnobserved(e.Exception));
        }
    }

    // An exit no finally sees still flushes, Environment.Exit called from the work for one. On a normal
    // exit the run has already ended and unhooked this, so it does not flush twice.
    // ⚠ SIGTERM is not such an exit: on Linux it ends the process without raising ProcessExit, so
    // RegisterSigterm flushes for it instead.
    private void OnProcessExit(object? sender, EventArgs e) => Flush();

    // Flushes on SIGTERM and leaves the signal to do what it does by default, so the process still ends
    // as SIGTERM ends it, its log flushed first.
    // ⛔ Never set the context's Cancel: a cancelled SIGTERM would leave the app running.
    private PosixSignalRegistration? RegisterSigterm()
    {
        try
        {
            return PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => Flush());
        }
        catch (Exception)
        {
            // ⛔ Nothing that goes wrong here may stop the app from starting, and Start runs outside
            // AppMain's try: a platform without SIGTERM, a handler it cannot install, access refused.
            // The run goes on without this flush.
            return null;
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (PressCtrlC())
        {
            e.Cancel = true;
        }
    }

    private void Invoke(string callback, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Error($"warning: {AppName}: the {callback} callback threw", exception);
        }
    }

    private static void Error(string headline, Exception exception) =>
        Error(headline + Environment.NewLine + exception);

    private static void Error(string text)
    {
        try
        {
            Console.Error.WriteLine(text);
        }
        catch (Exception)
        {
            // ⛔ A closed or broken stderr must not replace the exit code with an exception.
        }
    }
}
