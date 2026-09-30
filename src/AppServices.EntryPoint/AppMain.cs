using System.Reflection;

namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>
/// The entry point every app calls from <c>Main</c>, one method per kind of app, so that what an entry
/// point does on failure, on Ctrl+C and on <c>--version</c> is fixed once, here, rather than in each app.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>What every kind does.</b> The two process-wide exception events and process exit are hooked
/// before anything else runs, and unhooked when the run ends. <c>--version</c>, as the only argument,
/// prints the app's <c>[AssemblyMetadata("PublicVersion")]</c> and exits 0, before the work runs; a
/// missing one is reported on stderr and exits 1. An exception the work lets escape is reported on
/// stderr, then passed to <see cref="AppMainOptions.OnFatal"/>, and exits 1. The app's log is flushed
/// on every exit.
/// </para>
/// <para>
/// ⛔ <b>A <c>HostAbortedException</c> is never caught.</b> A design-time tool, such as <c>dotnet ef</c>,
/// throws it inside the entry point to stop it once the host is built, and expects it back. It is
/// recognised by its type's name, as the tool recognises it, since the tool may throw a private type
/// of that name.
/// </para>
/// <para>
/// ⚠ <b>An exception on another thread ends the process from the runtime's own handler.</b> It is
/// reported and the log is flushed, but the exit code is the runtime's, not 1: changing it would take
/// <see cref="Environment.Exit"/>, which a test host running this entry point cannot survive.
/// </para>
/// </remarks>
public static class AppMain
{
    /// <summary>
    /// Runs a console app, which owns its command line: Ctrl+C cancels the token handed to
    /// <paramref name="run"/> and exits 130, and a <see cref="UsageException"/> exits 2.
    /// </summary>
    /// <param name="program">The app's own assembly, <c>typeof(Program).Assembly</c>, whose
    /// <c>PublicVersion</c> <c>--version</c> prints.</param>
    /// <param name="args">The command line.</param>
    /// <param name="run">The work: its return value is the exit code, unless Ctrl+C was pressed.</param>
    /// <param name="options">The usage text and the app's callbacks.</param>
    /// <returns>0 or the work's own code, 1 on an unhandled failure, 2 on a usage error, 130 after Ctrl+C.</returns>
    public static async Task<int> RunConsoleAsync(Assembly program, string[] args,
        Func<string[], CancellationToken, Task<int>> run, AppMainOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(run);
        using EntryRun entry = EntryRun.Start(program, AppKind.Console, options);

        int returned;
        try
        {
            if (EntryRun.AsksForVersion(args))
            {
                return entry.AnswerVersion();
            }

            returned = await run(args, entry.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (!EntryRun.IsHostAborted(exception))
        {
            return entry.Failed(exception);
        }
        finally
        {
            entry.Flush();
        }

        return entry.Completed(returned);
    }

    /// <summary>
    /// Runs a web host. Its arguments are configuration, so none is rejected, and its host owns
    /// SIGTERM and Ctrl+C, so neither is handled here. <c>--version</c> is answered before
    /// <paramref name="run"/> builds the host.
    /// </summary>
    /// <param name="program">The app's own assembly, <c>typeof(Program).Assembly</c>, whose
    /// <c>PublicVersion</c> <c>--version</c> prints.</param>
    /// <param name="args">The command line, passed to <paramref name="run"/> untouched.</param>
    /// <param name="run">The work: builds the host and runs it until it stops.</param>
    /// <param name="options">The app's callbacks.</param>
    /// <returns>0 when the host stops, 1 on an unhandled failure.</returns>
    public static async Task<int> RunHostAsync(Assembly program, string[] args,
        Func<string[], Task> run, AppMainOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(run);
        using EntryRun entry = EntryRun.Start(program, AppKind.Host, options);

        try
        {
            if (EntryRun.AsksForVersion(args))
            {
                return entry.AnswerVersion();
            }

            await run(args).ConfigureAwait(false);
        }
        catch (Exception exception) when (!EntryRun.IsHostAborted(exception))
        {
            return entry.Failed(exception);
        }
        finally
        {
            entry.Flush();
        }

        return entry.Completed(0);
    }

    /// <summary>
    /// Runs a desktop app with no console, whose window's close is its cancellation. Synchronous, so
    /// that a <c>[STAThread]</c> <c>Main</c> runs the UI on its own thread.
    /// </summary>
    /// <param name="program">The app's own assembly, <c>typeof(Program).Assembly</c>, whose
    /// <c>PublicVersion</c> <c>--version</c> prints.</param>
    /// <param name="args">The command line.</param>
    /// <param name="run">The work: runs the UI until it closes, and returns its exit code.</param>
    /// <param name="options">The app's callbacks: for a desktop app, its dialog and its log.</param>
    /// <returns>The work's own code, or 1 on an unhandled failure.</returns>
    public static int RunDesktop(Assembly program, string[] args,
        Func<string[], int> run, AppMainOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(run);
        using EntryRun entry = EntryRun.Start(program, AppKind.Desktop, options);

        int returned;
        try
        {
            if (EntryRun.AsksForVersion(args))
            {
                return entry.AnswerVersion();
            }

            returned = run(args);
        }
        catch (Exception exception) when (!EntryRun.IsHostAborted(exception))
        {
            return entry.Failed(exception);
        }
        finally
        {
            entry.Flush();
        }

        return entry.Completed(returned);
    }
}
