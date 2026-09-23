namespace Bennewitz.Ninja.AppServices.Abstractions;

/// <summary>
/// Platform-agnostic shell / file-manager launcher. Encapsulates all OS-specific logic for opening
/// terminals, revealing files in the platform file manager, and handing a path or URL to whatever
/// the desktop has registered for it.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Every member is asynchronous, including the ones whose implementations are not.</b> An
/// async signature wraps a synchronous implementation for free; a synchronous one can only wrap an
/// async implementation by blocking, which deadlocks a UI thread. iOS's
/// <c>openURL:options:completionHandler:</c> has no synchronous form to offer, so a synchronous
/// contract would have to be broken to reach that platform at all.
/// </para>
/// <para>
/// ⚠ <see cref="System.Threading.Tasks.ValueTask{TResult}"/> rather than
/// <see cref="System.Threading.Tasks.Task{TResult}"/> because the common path completes
/// synchronously and allocating a task per launch is pure waste. The usual caveat applies: await it
/// once, and never concurrently.
/// </para>
/// </remarks>
public interface IShellLauncher
{
    /// <summary>
    /// Opens the platform terminal with <paramref name="command"/> pre-filled in the command line.
    /// The user still has to press Enter — the command is never executed automatically.
    /// </summary>
    /// <param name="command">Text to pre-fill. Never executed by this call.</param>
    /// <param name="cancellationToken">Cancels before the launch is handed to the platform.</param>
    /// <returns>
    /// <see cref="LaunchStatus.Succeeded"/> when a terminal was launched, and
    /// <see cref="LaunchStatus.Unsupported"/> when no suitable terminal emulator exists — a
    /// headless box, or a desktop with none of the known emulators installed.
    /// ⚠ Those two used to be <c>true</c> and <c>false</c>, and the second is not an error: surface
    /// a Copy fallback rather than a failure message.
    /// </returns>
    /// <remarks>
    /// Platform behaviour:
    /// <list type="bullet">
    ///   <item><b>Windows</b> — Prefers PowerShell 7+ (pwsh.exe). If the user has configured
    ///     Windows Terminal as their default terminal host it opens a new Windows Terminal tab;
    ///     otherwise a standalone PowerShell window.</item>
    ///   <item><b>macOS</b> — Opens Terminal.app via osascript.</item>
    ///   <item><b>Linux / WSL</b> — In WSL, tries wt.exe (Windows Terminal) first, which opens a
    ///     new WSL tab. Otherwise gnome-terminal → cosmic-term → xfce4-terminal → mate-terminal →
    ///     tilix → konsole → lxterminal → xterm.</item>
    /// </list>
    /// </remarks>
    ValueTask<LaunchResult> LaunchTerminalWithCommandAsync(string command, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the platform file manager showing the folder that contains
    /// <paramref name="filePath"/>, with the file pre-selected where the platform supports it.
    /// </summary>
    /// <param name="filePath">The file to reveal.</param>
    /// <param name="cancellationToken">Cancels before the launch is handed to the platform.</param>
    /// <returns>
    /// ⭐ <see cref="LaunchStatus.Unsupported"/> on a platform with no file manager to open — a
    /// phone, most obviously. <b>This is the case that motivated the whole result type:</b> a
    /// caller must hide the affordance rather than offer an action that can only fail.
    /// </returns>
    /// <remarks>
    /// Platform behaviour:
    /// <list type="bullet">
    ///   <item><b>Windows</b> — <c>explorer.exe /select,"&lt;path&gt;"</c></item>
    ///   <item><b>macOS</b> — <c>open -R "&lt;path&gt;"</c> (Finder, selects the file)</item>
    ///   <item><b>Linux</b> — nautilus → dolphin → nemo → thunar → xdg-open, falling back to
    ///     opening the parent directory when the file manager cannot select a single file.</item>
    /// </list>
    /// </remarks>
    ValueTask<LaunchResult> RevealInFileManagerAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Opens <paramref name="filePath"/> in the platform default text editor.</summary>
    /// <param name="filePath">Absolute path to open. A relative path is rejected rather than resolved.</param>
    /// <param name="cancellationToken">Cancels before the launch is handed to the platform.</param>
    /// <returns>
    /// <see cref="LaunchStatus.Failed"/> for a relative path, which is a caller error rather than a
    /// platform one, and <see cref="LaunchStatus.NotFound"/> when the file is not there.
    /// </returns>
    /// <remarks>
    /// Platform behaviour:
    /// <list type="bullet">
    ///   <item><b>Windows</b> — <c>Process.Start</c> with <c>UseShellExecute=true</c> lets the OS
    ///     dispatch the file via its registered handler.</item>
    ///   <item><b>macOS</b> — <c>open -t "&lt;path&gt;"</c> opens the user's default text editor.</item>
    ///   <item><b>Linux</b> — <c>xdg-open "&lt;path&gt;"</c> defers to the desktop environment.</item>
    /// </list>
    /// </remarks>
    ValueTask<LaunchResult> OpenInDefaultEditorAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Opens <paramref name="url"/> in the platform default browser.</summary>
    /// <param name="url">The URL to open. Empty or whitespace is rejected.</param>
    /// <param name="cancellationToken">Cancels before the launch is handed to the platform.</param>
    /// <returns>
    /// A status rather than nothing. ⚠ Opening a URL is cosmetic, so a caller is still expected to
    /// offer a copy-link fallback — but it can now tell whether one is needed.
    /// </returns>
    /// <remarks>
    /// Platform behaviour:
    /// <list type="bullet">
    ///   <item><b>Windows</b> — <c>Process.Start</c> with <c>UseShellExecute=true</c> dispatches
    ///     via the registered <c>http://</c> / <c>https://</c> handler.</item>
    ///   <item><b>macOS</b> — <c>open "&lt;url&gt;"</c> (Launch Services routes to the default browser).</item>
    ///   <item><b>Linux</b> — <c>xdg-open "&lt;url&gt;"</c> defers to the desktop environment.</item>
    /// </list>
    /// </remarks>
    ValueTask<LaunchResult> LaunchUrlAsync(string url, CancellationToken cancellationToken);
}
