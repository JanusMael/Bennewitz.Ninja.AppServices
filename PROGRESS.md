# Progress

Work state for Bennewitz.Ninja.AppServices, updated in the same change as the work it describes.
What has stopped changing moves out rather than piling up.

## Published

All five ids — `Bennewitz.Ninja.AppServices`, `.Abstractions`, `.Logging`, `.Avalonia` and, from
`2026.4.1001`, `.EntryPoint` — ship together at every version.

| Version | Tag | What it carried | Verified |
|---|---|---|---|
| `2026.4.1001` | `v2026.4.1001` at `03ebb64`, 2026-10-01 | **`Bennewitz.Ninja.AppServices.EntryPoint`, a new id** (`plans/00001`, #8): `AppMain`'s `RunConsoleAsync`, `RunHostAsync` and `RunDesktop`, with `AppMainOptions` and `UsageException`. It references nothing and is native AOT compatible, measured by CI's required `aot-linux` and `aot-windows` jobs; a console or desktop run flushes the log on SIGTERM as well (see the drift below). `.Avalonia` gains `AvaloniaDiagnostics.EntryPointOptions()` and depends on `.EntryPoint`. **Breaking:** `DefaultShareService`'s `Process` seam is internal, granted to `ClaudeForge.Tests` (#10). Every package carries the solution-wide friend grants of Templates' `plans/00006` (#9). In the repository: the family's conventions, AI-facing documents and standard build properties, `PublicVersion` in `Directory.Build.targets`, AssemblyQuality 2026.3.929 with `BNAQ1005`, the headless bootstrap removed, `PackagingTests` failing closed, the numbered plans, and `packages.push` in dependency order | Every id lists `2026.4.1001` in the nuget.org flat-container, pushed in dependency order and each `Created`. A console app whose `nuget.config` names only nuget.org restored all five at `2026.4.1001`, built, and ran through `AppMain`. Before the tag, the last judge pass (its caveats fixed), the blank-version preflight's login and `check --release`, and `main`'s CI were green. The Templates session was told the version, the API and the AOT measurement the same day, `plans/00001` step 8 |
| `2026.3.924` | `v2026.3.924` at `d1c5c9c`, 2026-09-24 | **Every assembly marked trimmable**, with the trim analyser on every build (`5600c1c`), fixing `.923`. **Breaking:** the Avalonia package keeps its id but its assembly and namespaces are `AppServices.AvaloniaUI` (`74b867c`, `a853070`); no `CancellationToken` has a default, `IShareService.ShareTextAsync`'s `uri` is required and `ShareOutcome` gains `Cancelled` (`746a856`); Avalonia 12.1.3 is the floor (`d1c5c9c`). `AvaloniaDiagnosticsOptions` gains `ConfigureLogger` and `EventListener` (`4815c74`). In the repository: the original MSTest suites ported to xunit v3 (`8b2caf2`, `cfb5b14`), and the AssemblyQuality rules run as tests (`44d9ba4`) | Every id lists `2026.3.924` in the nuget.org flat-container. A host referencing Avalonia below 12.1.3 fails restore with `NU1605`, measured against the packed packages |
| `2026.3.923` | `v2026.3.923` at `5626145`, 2026-09-23 | The first release: the four ids moved out of OpenForge2k under `plans/00002` in Bennewitz.Ninja.Templates (`0928c61`). Launches return `ValueTask<LaunchResult>` and diagnostics go through a `DiagnosticSink` (`043b545`); the layering guards (`5626145`). ⚠ It shipped without `IsTrimmable`, with a defaulted `CancellationToken` (AQ1001) and `….Avalonia` namespaces shadowing Avalonia's root (AQ1004), and with the template's `TODO` paragraph as the packed README. All four are fixed in `.924` and guarded by `PackageMetadataTests` and `AssemblyQualityTests` | Every id listed in the nuget.org flat-container, and consumed from a project whose `nuget.config` names only nuget.org |

## On `main`, not yet released

- **`BucketedRollingFileSink`'s inner `.MinimumLevel.Information()` says it is not a filter.** It
  reads like one, and DiffView nearly reported it on 2026-10-08 as dropping a host's `Debug` lines.
  `Emit` formats each event itself and passes every finished line on at Information, so it never
  drops one; `BucketedRollingFileSinkTests.Emit_WritesDebugAndVerbose_UnderTheirOwnLevel` measures
  that.
- **A minimum per source is documented, not added.** DiffView found none the same day, in the copy
  of this pipeline it consumes from ClaudeForge, which has no `ConfigureLogger`. Here
  `ConfigureLogger` already takes Serilog's `MinimumLevel.Override`, which lowers one source below
  `MinimumLevel` as readily as it raises one, so no option of this package's own was added.
  `MinimumLevel` and `ConfigureLogger` now say so, and
  `AvaloniaDiagnosticsHookTests.ConfigureLogger_can_lower_one_source_below_MinimumLevel` measures it.

## Drift from `plans/00001`

- **Its "What exists" row on the root `Directory.Build.props` was wrong.** It repeated that file's
  comment, "1.0.0 on a local build", but the default was taken before the SDK sets `Version`, so
  `PublicVersion` was empty and no assembly carried it. Found by the entry point's first `--version`
  test, and fixed by adopting the template's `Directory.Build.targets`.
- **Decision 8's SIGTERM premise was wrong, and so is the reason the Dismissed list gives for
  leaving SIGTERM alone.** Both have `ProcessExit` flush the log on SIGTERM, but on Linux the
  runtime ends the process without raising it: a console run and a desktop run were each killed with
  nothing flushed, three times of three on .NET 10.0.12. Found by the fable-judge pass on #8, which
  also found the SIGINT test passing with no handler registered, since a child killed by SIGINT
  reads 130 too. **Decided 2026-10-01** (maintainer): a console or desktop run registers a SIGTERM
  `PosixSignalRegistration` that flushes and never cancels, so SIGTERM still ends the process; a web
  run's host handles SIGTERM itself, and its run ends and flushes. `EntryPointProbeTests` sends
  SIGTERM to both kinds and asserts the flush. A run flushes once, so one that survives SIGTERM, a
  generic host inside `RunConsoleAsync` for one, has spent its flush before it logs its shutdown;
  `FlushLog`'s doc and the README send such an app to `RunHostAsync`. A web app SIGTERMed before its
  host registers dies unflushed, as it did before.

## Next

1. **Correct the comments the move left stale:** `AppServices.Abstractions.csproj` names
   `AppServicesLayeringTests` (the class is `LayeringTests`); `MovedSourceSmokeTests` describes
   the layering guards as still to come; `ShellLauncherWindowsTerminalTests` gives a VSTest
   `TestCategory=` filter for traits named `Category`; `Parallelization.cs` says
   `SerilogAvaloniaSinkTests` changes static state, which it does not.
2. **Guard that every project under `src/` has a row in `LayeringTests.Tiers`.** Today a new
   project with no row is outside the tier checks and BNAQ1003, and nothing fails.
3. **Headless isolation per assembly** is unverified and waits for evidence before
   `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]` is considered.
4. `LiveLogWindowSink` and its windows stay in OpenForge2k until they can ship together; nothing
   here is scheduled for them.
