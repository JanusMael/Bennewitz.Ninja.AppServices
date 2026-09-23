namespace Bennewitz.Ninja.AppServices;

/// <summary>
/// A placeholder so the package has something to compile and something to test. Replace it — the
/// point of this file is that the scaffold builds, packs and passes on the first run, so a red
/// build after generating means you broke something rather than that setup is unfinished.
/// </summary>
public static class Greeting
{
    /// <summary>
    /// Returns a greeting for <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Who to greet. Whitespace-only is treated as absent.</param>
    public static string For(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Hello." : $"Hello, {name.Trim()}.";
}
