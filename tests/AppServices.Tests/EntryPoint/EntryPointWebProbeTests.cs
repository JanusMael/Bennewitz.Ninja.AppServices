using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AppServices.Tests.EntryPoint;

/// <summary>
/// <c>tests/EntryPointWebProbe</c>, a web app whose entry point is <c>AppMain.RunHostAsync</c>, under
/// the two things that run a web app's entry point in process.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b><c>WebApplicationFactory</c> never throws <c>HostAbortedException</c>.</b> It resolves the
/// host with <c>stopApplication: false</c>, so the entry point simply runs until the factory is
/// disposed. What throws it is a design-time tool, such as <c>dotnet ef</c>, which stops the entry
/// point on the host's <c>HostBuilt</c> diagnostic event. The second test does exactly that, because
/// the package <c>HostFactoryResolver</c> ships in is not on nuget.org.
/// </para>
/// <para>
/// ⚠ The probe's <c>OnFatal</c> writes to stderr, so an empty stderr also shows it never ran.
/// </para>
/// </remarks>
public sealed class EntryPointWebProbeTests : IDisposable
{
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _stderr = new();

    public EntryPointWebProbeTests() => Console.SetError(_stderr);

    public void Dispose() => Console.SetError(_originalError);

    [Fact]
    public async Task WebApplicationFactory_starts_and_stops_the_app_twice_with_nothing_on_stderr()
    {
        for (int run = 0; run < 2; run++)
        {
            await using WebApplicationFactory<Program> factory = new();
            using HttpClient client = factory.CreateClient();

            Assert.Equal("ok", await client.GetStringAsync("/", TestContext.Current.CancellationToken));
        }

        Assert.Equal("", _stderr.ToString());
    }

    [Fact]
    public void A_stop_on_HostBuilt_passes_its_HostAbortedException_back_with_nothing_on_stderr()
    {
        using StopOnHostBuilt stop = new();
        using IDisposable subscription = DiagnosticListener.AllListeners.Subscribe(stop);

        MethodInfo entryPoint = typeof(Program).Assembly.EntryPoint
            ?? throw new InvalidOperationException("The web probe has no entry point.");
        Exception? escaped = null;

        // On a thread of its own, as HostFactoryResolver runs an entry point.
        Thread thread = new(() =>
        {
            try
            {
                entryPoint.Invoke(null, [Array.Empty<string>()]);
            }
            catch (TargetInvocationException exception)
            {
                escaped = exception.InnerException;
            }
        });
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromMinutes(1)), "The entry point did not stop at HostBuilt.");
        Assert.IsType<Microsoft.Extensions.Hosting.HostAbortedException>(escaped);
        Assert.Equal("", _stderr.ToString());
    }

    /// <summary>
    /// What <c>HostFactoryResolver</c> does with <c>stopApplication</c>, reduced to the stop: on the
    /// <c>Microsoft.Extensions.Hosting</c> listener's <c>HostBuilt</c> event, throw the public
    /// <c>HostAbortedException</c> inside the entry point that is building the host.
    /// </summary>
    private sealed class StopOnHostBuilt
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.Extensions.Hosting")
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "HostBuilt")
            {
                throw new Microsoft.Extensions.Hosting.HostAbortedException();
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
