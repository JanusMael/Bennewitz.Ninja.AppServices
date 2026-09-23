using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace AppServices.Tests.Headless;

/// <summary>
/// Guards that <see cref="HeadlessSessionBootstrap"/> still does what it exists to do: build the
/// Avalonia application before any test runs, not in whichever test dispatches first.
/// </summary>
/// <remarks>
/// ⛔ <b>The regression this catches is INVISIBLE without it.</b> Deleting the warm-up dispatch
/// leaves every test passing on most runs: set-up simply moves into the first test to dispatch, and
/// fails only when a non-headless test has already bound <c>Dispatcher.UIThread</c> to another
/// thread. By the time any test can look, set-up has happened either way, so the bootstrap records
/// what it saw and this asserts the record.
/// </remarks>
public sealed class HeadlessSessionBootstrapTests
{
    [Fact]
    public void TheApplicationIsBuiltBeforeAnyTest_NotByTheFirstTestToDispatch()
    {
        Assert.True(HeadlessSessionBootstrap.ApplicationBuiltBeforeAnyTest,
            "HeadlessSessionBootstrap did not observe a built Avalonia application before the first "
            + "test. Its warm-up Dispatch has been removed or reordered, so AppBuilder.SetupUnsafe() is "
            + "once again the first-scheduled test's responsibility, which reintroduces the intermittent "
            + "'The calling thread cannot access this object because a different thread owns it'.");
    }

    /// <summary>
    /// The premise of the fix: one session per assembly, so the warm-up and every fixture are
    /// talking about the same thing.
    /// </summary>
    [Fact]
    public void GetOrStartForAssembly_ReturnsTheSameSessionEveryTime()
    {
        HeadlessUnitTestSession a =
            HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        HeadlessUnitTestSession b =
            HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        // Cached per assembly. If it were not, the bootstrap would warm up an instance the
        // fixtures never use.
        Assert.Same(a, b);
    }

    /// <summary>
    /// And that the session's thread really owns the dispatcher: the property whose violation
    /// produces the cross-thread throw in the first place.
    /// </summary>
    [Fact]
    public Task TheSessionThreadOwnsTheDispatcher()
    {
        HeadlessUnitTestSession session =
            HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        return session.Dispatch(() =>
        {
            Assert.NotNull(Application.Current);
            Assert.True(Dispatcher.UIThread.CheckAccess(),
                "Dispatcher.UIThread must be owned by the session thread. If it is not, some earlier "
                + "toucher on another thread bound it, which is what makes compositor construction throw.");
        }, CancellationToken.None);
    }
}
