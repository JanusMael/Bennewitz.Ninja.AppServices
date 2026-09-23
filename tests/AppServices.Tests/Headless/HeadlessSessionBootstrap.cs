using Avalonia;
using Avalonia.Headless;

[assembly: AssemblyFixture(typeof(AppServices.Tests.Headless.HeadlessSessionBootstrap))]

namespace AppServices.Tests.Headless;

/// <summary>
/// Starts the assembly's shared <see cref="HeadlessUnitTestSession"/> <em>and forces the Avalonia
/// application to be built</em>, once, before any test in this assembly runs.
/// </summary>
/// <remarks>
/// <para>
/// ⓘ The same bootstrap as ScopedEditors.Tests', for the same reason, and ported from the same
/// OpenForge2k original. xunit v3's <b>assembly fixture</b> is initialised before the first test in
/// the assembly and disposed after the last. <c>HeadlessSessionBootstrapTests</c> comes with it and
/// proves that ordering holds rather than trusting it.
/// </para>
/// <para>
/// ⛔⛔ <b><c>GetOrStartForAssembly</c> ALONE DOES NOT BUILD THE APPLICATION.</b> It starts the
/// session and its dispatcher <em>thread</em>. <c>AppBuilder.SetupUnsafe()</c>, which constructs
/// the compositor, runs lazily on the <b>first <c>Dispatch</c></b>, so the warm-up dispatch below
/// is the load-bearing line: it makes the session thread the first toucher of
/// <c>Dispatcher.UIThread</c>, deterministically, before any test.
/// </para>
/// <para>
/// ⚠ <b>Why a wrong thread at all:</b> <c>Dispatcher.UIThread</c> is a lazily resolved
/// process-global that binds to whichever thread touches it first. A non-headless test that touches
/// Avalonia can bind it to a runner thread, and the session thread then fails
/// <c>VerifyAccess</c> building the compositor: "The calling thread cannot access this object
/// because a different thread owns it", reported against an arbitrary unrelated assertion. Classes
/// that are green alone and flaky together are the signature.
/// </para>
/// <para>
/// This complements the assembly's disabled parallelisation: that keeps the single headless
/// dispatcher from being driven concurrently once it is up; this decides when it comes up.
/// </para>
/// </remarks>
public sealed class HeadlessSessionBootstrap : IAsyncLifetime
{
    /// <summary>
    /// Whether the warm-up dispatch observed a built Avalonia application. Read by
    /// <c>HeadlessSessionBootstrapTests</c>; it is the only way to prove from inside the suite that
    /// set-up happened before the first test rather than in it.
    /// </summary>
    internal static bool ApplicationBuiltBeforeAnyTest { get; private set; }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        // The session is cached per assembly, so this is the one start; every GetOrStartForAssembly
        // call in a fixture then returns the same instance.
        HeadlessUnitTestSession session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessSessionBootstrap).Assembly);

        // ⛔ DO NOT DELETE. This dispatch is what builds the application; see the type remarks.
        ApplicationBuiltBeforeAnyTest =
            await session.Dispatch(() => Application.Current is not null, CancellationToken.None);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
