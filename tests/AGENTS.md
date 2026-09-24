# AGENTS.md — `tests/`

The test project `AppServices.Tests`, referencing every shipped project. `tests/Directory.Build.props`
makes it an xUnit v3 test executable that is never packed.

| Folder | Classes | What they cover |
|---|---|---|
| `Architecture/` | `LayeringTests`, `AssemblyQualityTests`, `PackageMetadataTests` | What each project may reference, the family's AssemblyQuality rules over every shipped assembly, and the metadata and trimmable mark every package carries |
| `Packaging/` | `PackagingTests` | Every packable project is classified in `packages.push` or `packages.local`, and the release workflow names what it publishes |
| `Shell/`, `Share/`, root | `ShellLauncherWindowsTerminalTests`, `DefaultShareServiceTests`, `LaunchResultTests`, `MovedSourceSmokeTests` | The launcher, the share service's reported `ShareOutcome`, `LaunchResult`, and the environment provider and dialog vocabulary |
| `Logging/`, `AvaloniaUI/` | `BucketedRollingFileSinkTests`, `SerilogAvaloniaSinkTests`, `AvaloniaDiagnosticsHookTests` | Bucket arithmetic and retention, the Avalonia-to-Serilog level mapping, and the `ConfigureLogger` and `EventListener` hooks |
| `Dialogs/`, `Headless/` | `DialogAccessibilityTests`, `NativeErrorDialogTests`, `HeadlessSessionTests`, `HeadlessTestApp` | Every interactive control in the C#-built dialogs has an automation name, built on a headless Avalonia platform; the native dialog never throws |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| Tests run on Microsoft.Testing.Platform | `dotnet test` takes `--solution` and rejects VSTest-only switches | `global.json`, `test.runner` |
| `tests/Directory.Build.props` imports the root props explicitly | Without it the test project silently loses the root's target framework and nullable settings | the import line itself |
| The assembly runs serially | Some tests change process-wide static state (`AvaloniaDiagnostics`, Serilog's `Log.Logger`, `NativeErrorDialog.SuppressForTests`), and the headless tests share one session and dispatcher | `Parallelization.cs`; `HeadlessSessionTests` guards the one-session premise |
| A test that changes static state restores it in `Dispose` | The next test inherits it | `AvaloniaDiagnosticsHookTests` (`AvaloniaDiagnostics.ResetForTests`), `NativeErrorDialogTests` (`NativeErrorDialog.Reset`) |
| Headless isolation is per test, and there is no warm-up | Each dispatch builds a fresh application, so a warm-up changes nothing. Switching to `AvaloniaTestIsolation` per assembly changes what every test shares and needs its own evidence first | `HeadlessSessionTests` remarks |
| A share test never passes text without a URI on Windows or macOS | That path runs the real `clip.exe` or `pbcopy` and overwrites the developer's clipboard; every other path goes through the launcher injected into `DefaultShareService` | `DefaultShareServiceTests` remarks |
| A platform-bound test skips with `Assert.Skip` or `Assert.SkipUnless`, never passes silently | A skip is visible in the report; an early return reads as a pass | `ShellLauncherWindowsTerminalTests`, whose launching tests carry `[Trait("Category", "Integration.WT")]` or `"Diagnostic.WT"`; `LaunchResultTests` |
| `LayeringTests.Tiers` and `LayeringTests.ForeignFamilies` are the one table of what may reference what | `AssemblyQualityTests` reads them for AQ1003; a copy is the list that silently rots | `AssemblyQualityTests.AQ1003_no_assembly_references_what_its_tier_forbids` |
| The layering guards keep both the csproj check and the reflection check | The compiler omits an unused reference from the assembly, so a declared-but-unused bad reference is invisible to reflection | `LayeringTests` remarks |
| The vacuity guards stay | Every other assertion iterates a list, and an empty list passes all of them | `LayeringTests.Every_tier_named_here_actually_exists`, `PackageMetadataTests.There_is_something_to_check`, `AssemblyQualityTests.Every_shipped_assembly_is_in_the_scan`, each rule's `Inspected > 0` |
| `DialogAccessibilityTests` pins the number of interactive controls each dialog has | A new control must bump it; a lower count means the walker lost something | `DialogAccessibilityTests` remarks |
| The project stays named `AppServices.Tests` | Each `src` project's `AssemblyInfo.cs` grants internals to that name; a rename sends the grant nowhere | `InternalsVisibleTo` in each `AssemblyInfo.cs` |

⛔ **Never weaken an architecture or packaging test to make it pass.** When one fails, the project,
the package list or the workflow is wrong, not the test.
