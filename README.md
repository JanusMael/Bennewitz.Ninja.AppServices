# Bennewitz.Ninja.AppServices

Application services for desktop apps: shell launching, environment probing, sharing, dialogs and
logging, behind contracts a test can fake without referencing a UI toolkit. The launcher and the
share service report what actually happened, as a `LaunchResult` or a `ShareOutcome`, rather than
completing the same way whether or not anything did.

| Package | What it is |
|---|---|
| `Bennewitz.Ninja.AppServices.Abstractions` | The contracts and the dialog vocabulary: shell launching, environment, sharing, dialogs and pickers. Framework-free. |
| `Bennewitz.Ninja.AppServices` | Default implementations: shell launching, environment probing, sharing and a native error dialog. Needs an operating system, not a UI framework, so it works from a CLI, a service or a test. |
| `Bennewitz.Ninja.AppServices.Logging` | Serilog sinks for desktop applications, starting with a bucketed rolling file sink that keeps a bounded number of files per bucket. |
| `Bennewitz.Ninja.AppServices.Avalonia` | Avalonia implementations — dialog service, file pickers, fatal and non-fatal dialogs — and the bridge that routes Avalonia's logger and its binding errors into Serilog. Its `EntryPointOptions` gives an Avalonia app's entry point the fatal dialog and the log. |
| `Bennewitz.Ninja.AppServices.EntryPoint` | One entry point for console, web and desktop apps: exit codes a script can read, a fatal report on stderr, Ctrl+C, `--version`, and the app's log flushed on every exit. References nothing but the framework, and is native AOT compatible. |

## Install

In an Avalonia application, one package brings the others with it:

```bash
dotnet add package Bennewitz.Ninja.AppServices.Avalonia
```

Without Avalonia, take `Bennewitz.Ninja.AppServices`, which brings the contracts, and add
`Bennewitz.Ninja.AppServices.Logging` if you want the sinks. Code that should only see the contracts
references `Bennewitz.Ninja.AppServices.Abstractions` alone.

Any app, console, web or desktop, takes `Bennewitz.Ninja.AppServices.EntryPoint` for its `Main`. It
references nothing else, so a native AOT app can take it too.

## Entry point

`AppMain` is the whole of an app's `Main`, with one method per kind of app:

```csharp
// A console app, which owns its command line
return await AppMain.RunConsoleAsync(typeof(Program).Assembly, args, RunAsync, new() { Usage = Usage });

// A web app: its arguments are configuration, and its host owns shutdown
return await AppMain.RunHostAsync(typeof(Program).Assembly, args, RunAsync);

// An Avalonia desktop app, with its fatal dialog and its log
return AppMain.RunDesktop(typeof(Program).Assembly, args, Run, AvaloniaDiagnostics.EntryPointOptions());
```

| Exit code | When |
|---|---|
| 0 | The work succeeded, or `--version` printed the release version |
| 1 | An exception escaped the work: it is reported on stderr, then passed to `OnFatal` |
| 2 | A console app's `UsageException`: its message and the usage go to stderr |
| 130 | Ctrl+C in a console app, which cancels the token its work was handed |

`--version`, as the only argument, prints the app's `[AssemblyMetadata("PublicVersion")]` before the
work runs. The fatal report is written to stderr before any callback, so it needs no logger, and
`FlushLog` flushes the app's own log on every exit. On SIGTERM, a console or desktop app flushes as
the signal arrives and then ends; a web app's host, once it is running, shuts down and ends the run,
which flushes. A run flushes only once, so an app that handles SIGTERM itself, with a generic host
for one, belongs in `RunHostAsync`. An exception on another thread is reported and flushed too, but
the process then ends with the runtime's own exit code. A `HostAbortedException`, which design-time
tools such as `dotnet ef` throw to stop an app, passes through untouched.

## Upgrading from 2026.3.923

- `Bennewitz.Ninja.AppServices.Avalonia` keeps its id, but the assembly inside it is now
  `AppServices.AvaloniaUI` and its namespaces are `Bennewitz.Ninja.AppServices.AvaloniaUI.*`, because a
  namespace segment named `Avalonia` shadows Avalonia's own root namespace.
- No `CancellationToken` parameter has a default any more; pass one. `IShareService.ShareTextAsync`'s
  `uri` is required as well (pass `null` for none), since a required token cannot follow an optional
  parameter.
- `ShareOutcome` gains `Cancelled`, returned when the token is already cancelled and nothing was
  attempted. A `switch` over the outcome needs the case.
- `AvaloniaDiagnosticsOptions` gains `ConfigureLogger` and `EventListener`, so a host can extend the
  log pipeline and receive diagnostic events without reaching into the library.

`2026.3.924` is also the first release whose assemblies are marked trimmable, with the trim analyser
running on every build.

`Bennewitz.Ninja.AppServices.Avalonia` `2026.3.924` requires **Avalonia 12.1.3 or later**. That
release fixes UI Automation selection never reaching the client on Windows
([AvaloniaUI/Avalonia#22151](https://github.com/AvaloniaUI/Avalonia/pull/22151)). A host that
references Avalonia directly at an earlier version fails restore with `NU1605`, a package downgrade,
until it raises that reference.

## Releasing

See [docs/publishing.md](docs/publishing.md). The short version:

1. Set the `NUGET_USER` repository **variable** to your nuget.org **profile name**, not an email. A
   variable, not a secret: GitHub masks a secret in the log, which hides the one value that explains
   a 401.
2. Create **one** trusted-publishing policy whose glob patterns cover every id in
   [`packages.push`](packages.push) and match nothing in [`packages.local`](packages.local).
3. Run **Release** → *Run workflow* with the version **blank**. That logs in and stops, proving the
   credentials without publishing.
4. Tag `vYYYY.Q.MMDD` and push.

⛔ **One policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to one matching policy — so a second policy is never consulted and its package is rejected `403`
after the first has already published permanently.

## Licence

MIT. See [LICENSE](LICENSE).
