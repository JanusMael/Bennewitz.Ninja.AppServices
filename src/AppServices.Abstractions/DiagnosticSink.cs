namespace Bennewitz.Ninja.AppServices.Abstractions;

/// <summary>Severity of a diagnostic message, for a consumer routing it into its own logger.</summary>
/// <remarks>
/// ⚠ Deliberately coarse. This exists so a consumer can filter and route; it is not a logging
/// framework's level model and should never grow toward one.
/// </remarks>
public enum DiagnosticLevel
{
    /// <summary>Detail useful only when diagnosing this library's behaviour.</summary>
    Debug = 0,

    /// <summary>Something a consumer might want in a normal log.</summary>
    Information,

    /// <summary>Something recoverable that a consumer should probably know about.</summary>
    Warning,

    /// <summary>Something went wrong.</summary>
    Error,
}

/// <summary>
/// Receives narrative a <see cref="LaunchResult"/> cannot carry. Supplied once, at construction.
/// </summary>
/// <param name="level">How much the consumer should care.</param>
/// <param name="message">One line, already formatted. Not localised.</param>
/// <param name="exception">The exception behind it, when there was one.</param>
/// <remarks>
/// <para>
/// ⛔ <b>This is a delegate, and that is the whole point — it is the alternative to a logger
/// dependency.</b> Widening the return to <see cref="LaunchResult"/> removed most of the need,
/// because the caller already owns a logger and now gets an answer worth logging. What is left is
/// narrative, and narrative does not justify imposing a logging model on every consumer.
/// </para>
/// <para>
/// ⛔ <b>Standing rule for this lane: contracts-only logging, or none.</b> An
/// <c>Action&lt;string&gt;</c> was considered and rejected — it loses the level and the exception,
/// so a consumer cannot route or filter. If the stance ever breaks, the replacement is
/// <c>Microsoft.Extensions.Logging.Abstractions</c> and never Serilog, which would force one
/// logging implementation on everybody.
/// </para>
/// <para>
/// ⚠ Implementations must treat a sink as untrusted: it is consumer code on the caller's thread, so
/// it can throw, and a diagnostic must never be the reason a launch fails.
/// </para>
/// </remarks>
public delegate void DiagnosticSink(DiagnosticLevel level, string message, Exception? exception);
