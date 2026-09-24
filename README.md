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
| `Bennewitz.Ninja.AppServices.Avalonia` | Avalonia implementations — dialog service, file pickers, fatal and non-fatal dialogs — and the bridge that routes Avalonia's logger and its binding errors into Serilog. |

## Install

In an Avalonia application, one package brings the other three with it:

```bash
dotnet add package Bennewitz.Ninja.AppServices.Avalonia
```

Without Avalonia, take `Bennewitz.Ninja.AppServices`, which brings the contracts, and add
`Bennewitz.Ninja.AppServices.Logging` if you want the sinks. Code that should only see the contracts
references `Bennewitz.Ninja.AppServices.Abstractions` alone.

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
