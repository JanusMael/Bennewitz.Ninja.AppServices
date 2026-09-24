using System.Reflection;
using Avalonia.Headless;

namespace AppServices.Tests.Headless;

/// <summary>
/// Guards the premise <c>Parallelization.cs</c> rests on: one headless session, and so one
/// dispatcher, per assembly.
/// </summary>
/// <remarks>
/// <para>
/// Every headless fixture reaches the session through
/// <c>HeadlessUnitTestSession.GetOrStartForAssembly(...)</c>. If that ever returned a fresh session
/// per call, each fixture would run on a dispatcher thread of its own, and running the tests one at
/// a time would no longer mean driving one dispatcher one test at a time.
/// </para>
/// <para>
/// ⛔ <b>There is deliberately no warm-up.</b> An assembly fixture used to dispatch once before the
/// first test, on the belief that it built the Avalonia application for every test after it. It
/// did not. With no <c>[AvaloniaTestIsolation]</c> on the assembly the isolation is
/// <c>PerTest</c>, and <c>DispatchCore</c> calls <c>EnsureIsolatedApplication()</c>, which resets
/// the dispatcher and runs <c>SetupUnsafe()</c>, on every dispatch (Avalonia 12.1.3,
/// <c>HeadlessUnitTestSession.cs</c>). The warm-up's application was discarded at once, and the
/// tests that guarded it passed by construction. Sharing one application across tests would take
/// <c>[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]</c>, which changes
/// what every test shares and needs its own evidence first.
/// </para>
/// </remarks>
public sealed class HeadlessSessionTests
{
    [Fact]
    public void GetOrStartForAssembly_ReturnsTheSameSessionEveryTime()
    {
        HeadlessUnitTestSession a =
            HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        HeadlessUnitTestSession b =
            HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        // Cached per assembly. If it were not, every fixture would run on a dispatcher of its own,
        // and running the tests one at a time would no longer protect a single dispatcher.
        Assert.Same(a, b);
    }
}
