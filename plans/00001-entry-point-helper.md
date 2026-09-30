# 00001 — An entry point every app template calls

> Status: **approved 2026-09-30**. Supersedes nothing. Answers step 1 of
> [`00007`](https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/plans/00007-console-template-and-entry-points.md)
> in Bennewitz.Ninja.Templates.

Templates' plan `00007` gives every app template the same entry-point behaviour and puts that
behaviour in AppServices, so that a fix reaches every generated app through a package bump rather
than only the next one generated. Its decisions 1, 3, 4 and 5 are the requirement. The helper's
name, its home and its API are this plan's.

## The requirement

| # | Every entry point |
|---|---|
| a | `int Main`. An unhandled exception is reported as fatal and returns 1, and the app's log is flushed on every exit |
| b | `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` are reported before anything else runs |
| c | Diagnostics and the fatal report go to stderr, results to stdout |
| d | Ctrl+C cancels a `CancellationToken` handed to the work and exits 130. `--version` prints `[AssemblyMetadata("PublicVersion")]`, never `AssemblyInformationalVersion` |

Exit codes: 0 success, 1 unhandled failure, 2 a usage error with the usage on stderr, 130 cancelled by
Ctrl+C; 2 only where the app owns its command line. The fatal report is written to stderr directly and
needs no logger. Nothing beyond the BCL: the app's log is flushed through a callback it passes. It is
`IsAotCompatible` and trim-safe, measured by a native AOT publish with warnings as errors. It re-throws
`HostAbortedException`, never calls `Environment.Exit`, and runs many times in one process. A web host
keeps its own shutdown, answers `--version` before its host is built and never rejects an argument.
`bbavalonia` keeps its native fatal-error dialog and its rolling-file log, and its window's close is
its cancellation.

## What exists

| Source | What it shows |
|---|---|
| `bbavalonia`'s `Program.cs` | `AppDomain.UnhandledException` shows `AvaloniaDiagnostics.ShowNativeFatalError`; a failure logs `Log.Fatal` and returns 1; `Log.CloseAndFlush` in `finally`. No `TaskScheduler.UnobservedTaskException` hook, and a start-up failure shows no dialog |
| `bbweb`, `bbapi` | Top-level `app.Run()`, never disposed. Both read `PublicVersion` through `GetCustomAttributes<AssemblyMetadataAttribute>()` for `/version`, which `bbapi` compiles under native AOT with warnings as errors |
| `src/Directory.Build.props` | Every shipped assembly trimmable, with the trim analyser on. `IsAotCompatible` deliberately off: nothing has measured it. It names how a library is measured past the analyser: a publish of a consumer that roots it with `TrimmerRootAssembly` and sets `TrimmerSingleWarn=false` |
| The root `Directory.Build.props` | `PublicVersion` follows `$(Version)`: the release tag's version, 1.0.0 on a local build |
| `HostFactoryResolver` (dotnet/runtime, release/10.0) | Runs an app's entry point to capture its host, listening on `DiagnosticListener.AllListeners` for the `Microsoft.Extensions.Hosting` listener's `HostBuilt` event. With `stopApplication`, its default and what design-time tools such as `dotnet ef` use, it throws `HostAbortedException` there: the public `Microsoft.Extensions.Hosting.HostAbortedException` when that type loads, otherwise a private type of the same name, and it recognises either by `GetType().Name`. `WebApplicationFactory` passes `stopApplication: false`, so under it nothing is thrown and the entry point runs until the factory is disposed |

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **A fifth package, `Bennewitz.Ninja.AppServices.EntryPoint`**: project and assembly `AppServices.EntryPoint`, namespace `Bennewitz.Ninja.AppServices.EntryPoint`, referencing nothing but the framework. Its `LayeringTests.Tiers` row forbids `Avalonia`, `Serilog`, `CommunityToolkit` and `Microsoft.Extensions` | The constraints rule out the Logging and Avalonia packages, and the other two are the wrong home (see Dismissed). A leaf with no references is what a native AOT web API can take without carrying anything else |
| 2 | **It alone sets `IsAotCompatible`**, in its own csproj, with the AOT, trim and single-file analysers that implies; the other four keep `src/Directory.Build.props` as it is | `00007` requires the claim, and step 5 measures it. The other four still have nothing measuring theirs |
| 3 | **One static class, `AppMain`, with a method per app kind**, each taking the app's own assembly, the arguments, the work and optional `AppMainOptions` (the API below). **`RunConsoleAsync` takes no caller token**: an entry point has none to give, and Ctrl+C is the console's cancellation | A call site is one line. The kinds differ in exactly what `00007` decision 4 says they differ in, so each method carries its kind's rules rather than switches a caller has to set right. A token added later would need a new method name, since `BNAQ1001` forbids a token-less overload beside one that takes a token; the tests reach Ctrl+C through an internal seam under the existing `AppServices.Tests` grant |
| 4 | **The version is read from the assembly the app passes**, `typeof(Program).Assembly`, and only when `--version` is the only argument. A missing or empty `PublicVersion` is reported on stderr and exits 1 | `GetEntryAssembly()` is the test runner under `WebApplicationFactory`. A fallback would print a version nobody released, and `00007` forbids the informational one |
| 5 | **The exit code comes from how the work ended, first match wins:** (1) `HostAbortedException`: re-thrown, not reported. (2) Console, after Ctrl+C: 130 however the work ended; an exception other than `OperationCanceledException` is still reported on stderr. (3) Console, `UsageException`: its message and `Usage` on stderr, 2. (4) Any other exception: the fatal report, then `OnFatal`, 1. (5) Otherwise the code the work returned | One order for every kind, so a test of a mapping holds for all of them. `00007` says Ctrl+C exits 130, so nothing after one turns into a 1 |
| 6 | **The fatal report is plain text on `Console.Error`**, written before any callback: a first line `fatal: <assembly> stopped on an unhandled exception`, then `exception.ToString()`. An unobserved task exception is reported the same way with `warning:`. A callback that throws is reported on stderr, and a write to stderr that fails is dropped; neither changes the exit code | Plain text needs no escaping; ObexNet built its JSON error line from an unescaped exception message. Writing first means a failing logger or dialog cannot take the report with it, and a closed stderr cannot take the exit code |
| 7 | **`HostAbortedException` is recognised by its type's name**, as `HostFactoryResolver` recognises it | Referencing its package would break the BCL-only constraint, and the full name would miss the resolver's private type of the same name |
| 8 | **Each run installs its own hooks and removes them in `finally`**: the two exception events and `AppDomain.ProcessExit` for every kind, `Console.CancelKeyPress` for the console. The first Ctrl+C cancels the token; a second is not intercepted, so a hung app can still be killed. **`FlushLog` runs at most once per run**: in `finally`, from `AppDomain.UnhandledException` when the runtime is terminating, and from `ProcessExit` for an exit no `finally` sees, such as SIGTERM | Runs can repeat and overlap in one test host, and each needs its own callbacks; the cost is a duplicate report when two overlapping runs see one process-wide crash. An exception on another thread still ends the process from the hook, with the runtime's exit code rather than 1, because changing it would take `Environment.Exit` |
| 9 | **`AvaloniaDiagnostics.EntryPointOptions()`** in the Avalonia package (the maintainer's choice, 2026-09-30) returns the options `bbavalonia` needs: `OnFatal` logs `Log.Fatal` and shows the native fatal dialog, `OnUnobservedTaskException` logs a warning, `FlushLog` is `Log.CloseAndFlush`. The Avalonia tier may reference `AppServices.EntryPoint` | The dialog and log wiring is exactly what a package bump should be able to fix in every app; left as template text, it never would be. A start-up failure also gets the dialog it lacks today |
| 10 | **Two probe apps.** `tests/EntryPointProbe`, a console app, calls `RunConsoleAsync` with one mode per exit code and one that throws on a background thread; the tests run it as a child process, and CI publishes it with native AOT and runs the binary. `tests/EntryPointWebProbe`, a minimal web app, calls `RunHostAsync`; the tests start it through `WebApplicationFactory<Program>`, and stop it the way a design-time tool does, with a `DiagnosticListener` subscription that throws the public `HostAbortedException` on the `HostBuilt` event | An exit code or a stream can only be seen from outside the process (`00007` decision 9), and an AOT claim only by an AOT publish. `HostAbortedException` is thrown only by a stop on `HostBuilt`, never by `WebApplicationFactory`, and `Microsoft.Extensions.HostFactoryResolver.Sources` is not on nuget.org, from which alone this repository restores. Each probe has its own `Directory.Build.props`, importing the root one and setting `IsPackable` to false, since `tests/Directory.Build.props` makes every project there an xunit test executable. The tests reference the web probe for its `Program`; the console probe is built and run, not referenced |
| 11 | **The native AOT jobs are required checks** (the maintainer's choice, 2026-09-30), named in `.github/repository.json`'s `requiredChecks` and applied with `repo-conventions apply` | A change that breaks the AOT claim cannot merge, so the claim `bbapi` relies on holds between releases, and a pull request counts as green only with it measured |

### The API

```csharp
namespace Bennewitz.Ninja.AppServices.EntryPoint;

public static class AppMain
{
    // Owns its command line: Ctrl+C exits 130, a UsageException exits 2.
    public static Task<int> RunConsoleAsync(Assembly program, string[] args,
        Func<string[], CancellationToken, Task<int>> run, AppMainOptions? options = null);

    // A web host: its arguments are configuration, and its host owns shutdown.
    public static Task<int> RunHostAsync(Assembly program, string[] args,
        Func<string[], Task> run, AppMainOptions? options = null);

    // No console: the window's close is its cancellation. Synchronous, for [STAThread].
    public static int RunDesktop(Assembly program, string[] args,
        Func<string[], int> run, AppMainOptions? options = null);
}

public sealed class AppMainOptions
{
    public string? Usage { get; init; }
    public Action? FlushLog { get; init; }
    public Action<Exception>? OnFatal { get; init; }
    public Action<Exception>? OnUnobservedTaskException { get; init; }
}

public sealed class UsageException : Exception { /* message only */ }
```

The calls the templates make:

```csharp
// bbconsole
return await AppMain.RunConsoleAsync(typeof(Program).Assembly, args, RunAsync, new() { Usage = Usage });

// bbweb, bbapi: the existing body moves into RunAsync, with `await using var app` and `await app.RunAsync()`
return await AppMain.RunHostAsync(typeof(Program).Assembly, args, RunAsync);

// bbavalonia
[STAThread]
public static int Main(string[] args) =>
    AppMain.RunDesktop(typeof(Program).Assembly, args, Run, AvaloniaDiagnostics.EntryPointOptions());
```

### Dismissed

- **The helper in `Bennewitz.Ninja.AppServices.Abstractions`.** It is the contracts package; it would
  gain runtime behaviour and an AOT claim that every contract consumer inherits.
- **The helper in `Bennewitz.Ninja.AppServices`.** Its AOT claim would cover shell launching, the
  registry probe and the native dialog, which nothing has measured, and `bbapi` would carry all of it.
- **A generic `RunConsoleAsync<TProgram>`.** A static `Program`, as `bbavalonia` has, cannot be a type
  argument (CS0718).
- **One process-wide, reference-counted set of hooks.** Overlapping runs would share one `OnFatal` and
  one `FlushLog`, and which run's they were would depend on timing.
- **An argument parser.** `00007` decision 8 leaves parsing to the app; the helper answers `--version`
  and maps `UsageException`, nothing else.
- **A graceful SIGTERM for the console.** Not in the requirement; the runtime's default stays, and
  `ProcessExit` still flushes the log.

## Scope

**In:** the `Bennewitz.Ninja.AppServices.EntryPoint` package; `AvaloniaDiagnostics.EntryPointOptions()`;
their tests, the two probes and CI's native AOT jobs; the documents that name the packages; a release
of all five ids; telling the Templates session.

**Out:** the templates' own changes (`00007` steps 2 and 3); moving OpenForge2k's apps onto the helper;
`BNAQ1005` and `BNAQ1006`.

## Steps

| # | Step | Verified by |
|---|---|---|
| 1 | This plan, with `plans/AGENTS.md`, `plans/CLAUDE.md` and a `plans/` row in the root `AGENTS.md` layout, so the new directory meets the family conventions | `repo-conventions check` conforms, and CI is green |
| 2 | The package: `AppMain`, `AppMainOptions` and `UsageException`; `AppServices.Tests` referencing it; its `LayeringTests.Tiers` row and a check, like `Abstractions_compiles_against_nothing_but_the_framework`, that its compiled references are the framework's alone; `packages.push`, the README's package table, `repository.json`'s description, every `AGENTS.md` passage that names the packages, including `src/AGENTS.md`'s rule on `IsAotCompatible`, and root `AGENTS.md` invariant rows for the helper's contract, each naming the test that guards it | `dotnet build -warnaserror` with its AOT, trim and single-file analysers on. `PackagingTests`, `PackageMetadataTests`, `LayeringTests` and `AssemblyQualityTests` pick the fifth assembly up from `src/` and pass. `assert-packages` sees five ids |
| 3 | In-process tests: every rule of decision 5 in its order, Ctrl+C through the internal seam; the streams each writes; `--version`; a missing `PublicVersion`; `UsageException`; `HostAbortedException`, the public type and a private one of the same name, propagating out of `RunHostAsync`; a throwing callback and a failing stderr; `FlushLog` once per run; hooks removed, shown by an unobserved task exception raised after a run that nothing reports; runs repeated and overlapping | Each seen red when broken on purpose, then green |
| 4 | The probes and their tests. The console probe run as a child process: exit 0; 1 with the fatal report on stderr; 2 with the usage on stderr; `--version` on stdout; a throw on a background thread, with the fatal report on stderr, the probe's flush marker and a non-zero exit; 130 by SIGINT on Linux in CI, skipped visibly on Windows. The web probe started twice by `WebApplicationFactory<Program>`, with `Microsoft.AspNetCore.Mvc.Testing` a test-only reference, and stopped once on `HostBuilt` as decision 10 describes | Those tests pass locally and in CI. Under the factory, each start serves a request and nothing reaches stderr. Under the stop on `HostBuilt`, the `HostAbortedException` leaves the entry point, nothing reaches stderr and `OnFatal` is never called |
| 5 | The native AOT measurement, in CI jobs of its own for linux-x64 and win-x64, added to `requiredChecks` and applied: the console probe published with `PublishAot=true`, `AppServices.EntryPoint` rooted with `TrimmerRootAssembly` and `TrimmerSingleWarn=false`, and warnings as errors, then the native binary run | No warnings, with every method of the package compiled. The native binary prints the same `PublicVersion` the framework-dependent one does, and exits 1 on a failure with the fatal report on stderr. `repo-conventions check` sees the new required checks |
| 6 | `AvaloniaDiagnostics.EntryPointOptions()` | A test with the native dialog suppressed sees the fatal event in the log, and `LayeringTests` accepts the new edge |
| 7 | Release all five ids at the day's version through `docs/publishing.md`, tagged on the maintainer's go | Every id in the nuget.org flat-container, then `PROGRESS.md`'s `Published` row |
| 8 | Tell the Templates session the version, the API and the AOT measurement | Its step 2 can start |
