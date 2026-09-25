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
5. **`DefaultShareService`'s public constructor names `System.Diagnostics.Process`:** its test seam
   `Func<ProcessStartInfo, Process?>?` is public API of `Bennewitz.Ninja.AppServices`, which a
   `BNAQ1002` over `System.Diagnostics` reports. The tests already have `InternalsVisibleTo`, but
   making it internal breaks binary compatibility, so it waits for a decision and a release that
   can carry one.
