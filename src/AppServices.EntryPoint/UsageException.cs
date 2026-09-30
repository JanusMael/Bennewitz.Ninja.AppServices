namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>
/// A console app's usage error: an unknown option, or a missing argument. Thrown from the work that
/// <see cref="AppMain.RunConsoleAsync"/> runs, it is reported on stderr with its message and
/// <see cref="AppMainOptions.Usage"/>, and exits 2.
/// </summary>
/// <remarks>
/// ⚠ Only the console kind owns its command line. From <see cref="AppMain.RunHostAsync"/> or
/// <see cref="AppMain.RunDesktop"/> it is an unhandled failure like any other, and exits 1.
/// </remarks>
public sealed class UsageException : Exception
{
    /// <summary>A usage error, described by <paramref name="message"/>.</summary>
    /// <param name="message">What was wrong with the command line, such as the unknown option.</param>
    public UsageException(string message)
        : base(message)
    {
    }
}
