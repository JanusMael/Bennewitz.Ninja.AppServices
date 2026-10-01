# Progress

Work state for Bennewitz.Ninja.AppServices, updated in the same change as the work it describes.
What has stopped changing moves out rather than piling up.

## Published

All four ids — `Bennewitz.Ninja.AppServices`, `.Abstractions`, `.Logging` and `.Avalonia` — ship
together at every version.

| Version | Tag | What it carried | Verified |
|---|---|---|---|
| `2026.3.924` | `v2026.3.924` at `d1c5c9c`, 2026-09-24 | **Every assembly marked trimmable**, with the trim analyser on every build (`5600c1c`), fixing `.923`. **Breaking:** the Avalonia package keeps its id but its assembly and namespaces are `AppServices.AvaloniaUI` (`74b867c`, `a853070`); no `CancellationToken` has a default, `IShareService.ShareTextAsync`'s `uri` is required and `ShareOutcome` gains `Cancelled` (`746a856`); Avalonia 12.1.3 is the floor (`d1c5c9c`). `AvaloniaDiagnosticsOptions` gains `ConfigureLogger` and `EventListener` (`4815c74`). In the repository: the original MSTest suites ported to xunit v3 (`8b2caf2`, `cfb5b14`), and the AssemblyQuality rules run as tests (`44d9ba4`) | Every id lists `2026.3.924` in the nuget.org flat-container. A host referencing Avalonia below 12.1.3 fails restore with `NU1605`, measured against the packed packages |
| `2026.3.923` | `v2026.3.923` at `5626145`, 2026-09-23 | The first release: the four ids moved out of OpenForge2k under `plans/00002` in Bennewitz.Ninja.Templates (`0928c61`). Launches return `ValueTask<LaunchResult>` and diagnostics go through a `DiagnosticSink` (`043b545`); the layering guards (`5626145`). ⚠ It shipped without `IsTrimmable`, with a defaulted `CancellationToken` (AQ1001) and `….Avalonia` namespaces shadowing Avalonia's root (AQ1004), and with the template's `TODO` paragraph as the packed README. All four are fixed in `.924` and guarded by `PackageMetadataTests` and `AssemblyQualityTests` | Every id listed in the nuget.org flat-container, and consumed from a project whose `nuget.config` names only nuget.org |

## On `main`, not yet released

- The headless test bootstrap is gone (`76ddfaf`, #2): with per-test isolation it built an
  application the next dispatch discarded, and its guards passed by construction.
  `HeadlessSessionTests` keeps the one test that can fail. Tests only; nothing a package carries.
- The family's repository conventions (`53485ad`, `plans/00003` step 6 in Bennewitz.Ninja.Templates):
  `scripts/repo-conventions.cs`, `.github/repository.json`, and the `conventions` CI job.
- The AI-facing and work-state documents: `AGENTS.md` at the root and in every top-level directory,
  their `CLAUDE.md` pointers, `.github/copilot-instructions.md`, and this file (`plans/00003` step 7).
  In the same change, `conventions` became a required check, `release.yml` gained the
  `check --release` preflight, and its two comments that called `NUGET_USER` a secret were
  corrected.

- **The family's standard build properties** (`plans/00004` in Bennewitz.Ninja.Templates):
  `IsContinuousIntegration` is gone and AutoVersioning is `2026.3.916` (`a50294e`);
  `scripts/repo-conventions.cs` evaluates every project against the family's build properties
  (`ef0f573`, `cf30a9b`); and `.github/repository.json` requires trimming, so the check fails if a
  library loses `IsTrimmable` or turns `EnableTrimAnalyzer` off (`8e07ab7`). Build and CI only;
  nothing a package carries changes.

- **The `nuget` topic is required only where `packages.push` names an id** (Templates `fb6961a`):
  `scripts/repo-conventions.cs` is the template's current copy. CI only.

- **AssemblyQuality 2026.3.925**, a test dependency; nothing a package carries changes. Rule IDs
  are now `BNAQ1001`–`BNAQ1004`, and test names, messages and `AGENTS.md` rows follow; the
  `2026.3.923` row keeps the old ones. The bump failed one test, correctly: `BNAQ1002`'s default
  JSON set inspects nothing here, because no shipped assembly references a JSON library.
  **Decided 2026-09-25:** the test adds `Microsoft.Win32` and `System.Runtime.InteropServices`,
  which every shipped assembly reaches, so a registry key, safe handle or marshalling type in a
  public signature fails it; seen red on a deliberate leak. Rejected: Serilog outside
  `AppServices.Logging` (its six findings are `AvaloniaDiagnosticsOptions`' Serilog-typed
  `MinimumLevel` and `ConfigureLogger`, by design), Avalonia outside `AppServices.AvaloniaUI`
  (inspects nothing, and `BNAQ1003` already forbids the reference), accepting the zero, and
  dropping the assertion. Every rule now also asserts an empty `Skipped`, and `BNAQ1004` reads
  internal types; none of 925's new checks finds anything here. Tests only.

- **`PackagingTests` fails closed** (Templates `74feb10`), the `bbpkg` template's fix for a gap
  FileServer's audit found: a project under `src/` now counts as packable unless its last
  `IsPackable` says `false`, as the SDK does, rather than only when one says `true`. With a probe
  project that never mentions `IsPackable`, `PackageMetadataTests.Every_project_states_IsPackable_explicitly`
  already failed and `Every_packable_project_is_classified` passed; now both fail. Tests only.

- **AssemblyQuality 2026.3.929**, the pin alone: a type that will not load, or one nested in a type
  that will not load, is named in `Skipped` instead of crashing the scan. Measured with `Serilog.dll`
  removed from a copy of the test output: at 2026.3.925 `BNAQ1001`, `BNAQ1002` and `BNAQ1004` threw
  `FileNotFoundException`; at 2026.3.928 `BNAQ1004` still did, reading the namespace of
  `BucketedRollingFileSink`'s compiler-generated `<>c`, which was reported from here; now all three
  fail naming what they could not examine. `BNAQ1006` is not adopted: it is a release-time check
  against published consumers. `BNAQ1005` came with the friend grants below. Tests only.

- **`plans/`**, the family's numbered plans, and `00001`, an entry point every app template calls
  (approved 2026-09-30), which answers step 1 of Bennewitz.Ninja.Templates' `00007`. Documents only.

- **`PublicVersion` defaults in `Directory.Build.targets`**, the `bbpkg` template's file verbatim,
  where `Version` already has its value. Taken in `Directory.Build.props`, the default was empty on
  every build but a release's, so nothing built here carried `[AssemblyMetadata("PublicVersion")]`.
  Build only: a release's packages are unchanged, since its `-p:Version=` wins either way.

- **`Bennewitz.Ninja.AppServices.EntryPoint`**, `plans/00001` steps 2–5: `AppMain`'s
  `RunConsoleAsync`, `RunHostAsync` and `RunDesktop`, with `AppMainOptions` and `UsageException`.
  It references nothing, and is the one assembly here that claims `IsAotCompatible`. `AppMainTests`
  covers every exit code and stream in process, `EntryPointProbeTests` runs `tests/EntryPointProbe`
  as a child process, and `EntryPointWebProbeTests` starts `tests/EntryPointWebProbe` under
  `WebApplicationFactory` and stops it on `HostBuilt`; each rule was seen red when broken on
  purpose. The native AOT publish is warning-free on win-x64, and the probe's contract passes
  against the native binary; CI's new required `aot-linux` and `aot-windows` jobs repeat both on
  every change. A console or desktop run also flushes the log on SIGTERM, from a
  `PosixSignalRegistration` that leaves the signal to end the process (see the drift below) and
  whose failure to register never stops an app from starting. The probe's SIGINT and SIGTERM tests
  assert its flush marker, not the exit code alone, since .NET reports a child killed by a signal as
  128 plus its number; `AppMainTests` holds a usage error after Ctrl+C to 130, the order decision 5
  sets; and packing the solution no longer warns about the web probe. A new package; the other four
  are unchanged.

- **`AvaloniaDiagnostics.EntryPointOptions()`**, `plans/00001` step 6: the options
  `AppMain.RunDesktop` takes in an Avalonia app. A failure is logged as fatal, then shown in the
  native fatal-error dialog, which a start-up failure now gets too; a task exception nobody observed
  is logged as a warning; and `Log.CloseAndFlush` runs on every exit. The Avalonia package now
  depends on `.EntryPoint`; `AvaloniaDiagnosticsEntryPointTests` saw both halves red when each was
  broken on purpose.

- **Solution-wide friend grants**, Templates' `plans/00006`: `scripts/repo-conventions.cs` is the
  template's current copy (Templates `dc46e70`), and the root `Directory.Build.targets` is `bbpkg`'s,
  which links two root files into every project: the generated `AssemblyInfo.InternalsVisibleTo.cs`,
  granting all eight assemblies built here, and the hand-written
  `AssemblyInfo.InternalsVisibleTo.External.cs`, with no grants yet. The four per-project grants to
  `AppServices.Tests` are gone, and `BNAQ1005` checks every compiled grant against the two files.
  The packages now carry grants to their siblings, the probes and the tests; nothing else a package
  carries changes.

- **`DefaultShareService`'s `Process` seam is internal** (breaking): the class has one public
  constructor, which takes nothing, and the `Func<ProcessStartInfo, Process?>` test seam is
  internal. This repository's tests reach it through the generated grants, and ClaudeForge's tests,
  which build the service through the seam at ten call sites, through a grant to `ClaudeForge.Tests`
  in the External file; ClaudeForge's app writes `new DefaultShareService()` and compiles unchanged.
  `BNAQ1002` now covers `System.Diagnostics` too, and finds nothing else. An assembly built against
  `2026.3.924` that calls the old constructor must be rebuilt, as the README's upgrading section says.

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

1. **`plans/00001` steps 7–8**: a release of all five ids, then telling the Templates session the
   version, the API and the AOT measurement. Templates' `00007` steps 2 and 3 wait for that release.
   **Go given 2026-10-01** (maintainer): once #8, the friend grants and the `DefaultShareService`
   change are merged with `main` green.
2. **Correct the comments the move left stale:** `AppServices.Abstractions.csproj` names
   `AppServicesLayeringTests` (the class is `LayeringTests`); `MovedSourceSmokeTests` describes
   the layering guards as still to come; `ShellLauncherWindowsTerminalTests` gives a VSTest
   `TestCategory=` filter for traits named `Category`; `Parallelization.cs` says
   `SerilogAvaloniaSinkTests` changes static state, which it does not.
3. **Guard that every project under `src/` has a row in `LayeringTests.Tiers`.** Today a new
   project with no row is outside the tier checks and BNAQ1003, and nothing fails.
4. **Headless isolation per assembly** is unverified and waits for evidence before
   `[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]` is considered.
5. `LiveLogWindowSink` and its windows stay in OpenForge2k until they can ship together; nothing
   here is scheduled for them.
