using Bennewitz.Ninja.AppServices.EntryPoint;

// The web probe the entry-point tests start through WebApplicationFactory, and stop on HostBuilt the
// way a design-time tool stops an app (plans/00001, decision 10). It is shaped as bbweb and bbapi
// will be: the body in RunAsync, the app disposed on the way out. OnFatal writes to stderr, so a test
// that finds stderr empty also knows OnFatal never ran.
return await AppMain.RunHostAsync(typeof(Program).Assembly, args, RunAsync, new AppMainOptions
{
    OnFatal = _ => Console.Error.WriteLine("probe: OnFatal"),
});

static async Task RunAsync(string[] args)
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    await using WebApplication app = builder.Build();
    app.MapGet("/", () => "ok");
    await app.RunAsync();
}

/// <summary>Public so the tests' <c>WebApplicationFactory&lt;Program&gt;</c> can start this app.</summary>
public partial class Program;
