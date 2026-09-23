namespace Bennewitz.Ninja.AppServices.Abstractions;

/// <summary>How a launch attempt ended.</summary>
/// <remarks>
/// <para>
/// ⛔ <b>The decisive case is not failure.</b> Revealing a file in a file manager on a phone is
/// <see cref="Unsupported"/>, and a caller should hide the affordance rather than show an error. A
/// <see cref="bool"/> cannot say which, so every caller that wanted to distinguish them either
/// guessed or showed the wrong thing.
/// </para>
/// <para>
/// ⚠ Members are ordered by how a caller should treat them, not alphabetically, and
/// <see cref="Succeeded"/> is deliberately <c>0</c> so <c>default</c> is never a silent failure —
/// a <see cref="LaunchResult"/> that was never assigned reads as success, which is wrong loudly
/// rather than wrong quietly.
/// </para>
/// </remarks>
public enum LaunchStatus
{
    /// <summary>The target was handed to the platform successfully.</summary>
    Succeeded = 0,

    /// <summary>
    /// The platform offers no way to do this. Hide the affordance; do not report an error.
    /// ⚠ Distinct from <see cref="Failed"/>: nothing went wrong, the capability is absent.
    /// </summary>
    Unsupported,

    /// <summary>The target, or the program that would open it, does not exist.</summary>
    NotFound,

    /// <summary>The platform refused on permission grounds.</summary>
    Denied,

    /// <summary>The caller cancelled before the launch completed.</summary>
    Cancelled,

    /// <summary>Something went wrong that none of the others describes.</summary>
    Failed,
}

/// <summary>The outcome of a launch, with optional detail for a log or a diagnostic surface.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Detail">
/// One line of context for a human, or <see langword="null"/>. ⚠ Not for display in UI chrome and
/// not localised — it names the mechanism, so it belongs in a log or a diagnostics pane.
/// </param>
/// <remarks>
/// A <c>readonly record struct</c> so returning one allocates nothing, and so equality works for
/// tests without anyone writing it.
/// </remarks>
public readonly record struct LaunchResult(LaunchStatus Status, string? Detail = null)
{
    /// <summary>
    /// True only for <see cref="LaunchStatus.Succeeded"/>, so a call site that does not care why
    /// stays as terse as the <see cref="bool"/> it replaced.
    /// </summary>
    public bool Succeeded => Status == LaunchStatus.Succeeded;

    /// <summary>The target was handed to the platform.</summary>
    public static LaunchResult Ok() => new(LaunchStatus.Succeeded);

    /// <summary>The platform offers no way to do this. Hide the affordance.</summary>
    public static LaunchResult Unsupported(string? detail = null) => new(LaunchStatus.Unsupported, detail);

    /// <summary>The target, or the program that would open it, does not exist.</summary>
    public static LaunchResult NotFound(string? detail = null) => new(LaunchStatus.NotFound, detail);

    /// <summary>The platform refused on permission grounds.</summary>
    public static LaunchResult Denied(string? detail = null) => new(LaunchStatus.Denied, detail);

    /// <summary>The caller cancelled before the launch completed.</summary>
    public static LaunchResult Cancelled(string? detail = null) => new(LaunchStatus.Cancelled, detail);

    /// <summary>Something went wrong that no other status describes.</summary>
    public static LaunchResult Failed(string? detail = null) => new(LaunchStatus.Failed, detail);
}
