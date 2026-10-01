# AGENTS.md — `src/`

The shipped projects. Everything here becomes a package, so everything here is permanent once
released.

| Project | Package id | May reference | Ships |
|---|---|---|---|
| `AppServices.Abstractions` | `Bennewitz.Ninja.AppServices.Abstractions` | nothing | `IShellLauncher`, `IShareService`, `IDialogService`, `IEnvironmentProvider`, `IPermissionPathPicker`, `ISaveChangesPrompt`, `LaunchResult`, `ShareOutcome`, `DiagnosticSink`, and `DialogMessage` under `Dialogs/` |
| `AppServices` | `Bennewitz.Ninja.AppServices` | `AppServices.Abstractions` | `ShellLauncher`, `DefaultShareService`, `DefaultEnvironmentProvider`, `NativeErrorDialog` |
| `AppServices.Logging` | `Bennewitz.Ninja.AppServices.Logging` | Serilog only | `BucketedRollingFileSink` |
| `AppServices.AvaloniaUI` | `Bennewitz.Ninja.AppServices.Avalonia` | its sibling projects, Avalonia, Serilog | `AvaloniaDialogService`, `FatalErrorDialog`, `NonFatalNoticeDialog`, `AvaloniaDiagnostics` with `AvaloniaDiagnosticsOptions` and `EntryPointOptions`, `SerilogAvaloniaSink`, `BindingValidationErrorLogger` |
| `AppServices.EntryPoint` | `Bennewitz.Ninja.AppServices.EntryPoint` | nothing | `AppMain` with `AppMainOptions`, and `UsageException` |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **`AppServices.Abstractions` has no references at all** | It is the package a test or a non-UI host consumes; a reference here quietly undoes the split | `LayeringTests.Abstractions_compiles_against_nothing_but_the_framework`; the comment in its csproj |
| **`AppServices.EntryPoint` has no references at all** | Every app's `Main` takes it, a native AOT web API's included, so whatever it references travels into every app. The app's logger is flushed through a callback the app passes | `LayeringTests.EntryPoint_compiles_against_nothing_but_the_framework`; the comment in its csproj |
| `AppMain` never catches a `HostAbortedException`, and recognises it by its type's name | A design-time tool such as `dotnet ef` throws it inside the entry point to stop it, and may throw a private type of that name | `AppMainTests.A_HostAbortedException_passes_through_unreported`; `EntryPointWebProbeTests` |
| A console or desktop run flushes on SIGTERM through a `PosixSignalRegistration` that never sets `Cancel` | On Linux the runtime ends the process on SIGTERM without raising `ProcessExit`, so without it the log is lost; a cancelled SIGTERM would leave the app running | `EntryPointProbeTests.SIGTERM_flushes_the_log_and_the_signal_still_ends_the_process` |
| A doc comment in `AppServices.Abstractions` names an implementation with `<c>`, never `cref` | The implementation is in a package it cannot reference, so the `cref` fails the build | `GenerateDocumentationFile` with warnings as errors |
| `AppServices` and `AppServices.Abstractions` take no logger; diagnostics go through a `DiagnosticSink` passed in | Neither package may reference Serilog | `LayeringTests.Tiers`; `LayeringTests.No_project_declares_a_package_its_tier_forbids` |
| `AppServices.Logging` holds sinks that need no window | A sink that calls into a window is a UI type; `LiveLogWindowSink` stayed in OpenForge2k for that reason | `LayeringTests.Tiers`; the comment in its csproj |
| `AppServices.AvaloniaUI` → `AppServices` is an allowed edge | `AvaloniaDiagnostics.ShowNativeFatalError` wraps `NativeErrorDialog` | `LayeringTests.Tiers`; the comment in its csproj |
| The Avalonia project's package id keeps `.Avalonia`; its assembly and namespaces stay `.AvaloniaUI` | A package id is permanent; a namespace segment `Avalonia` shadows Avalonia's root | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root` |
| `AssemblyName` equals the project file name; namespaces are `Bennewitz.Ninja.<AssemblyName>` | The layering guards match assemblies by name, so a mismatch drops a project from its own scan | `PackageMetadataTests.Every_assembly_name_matches_its_project_file`; `RootNamespace` in the root `Directory.Build.props` |
| Every `CancellationToken` parameter is required, and no public overload leaves it out | A default, or an overload that passes `CancellationToken.None`, lets a caller skip cancellation without noticing | `AssemblyQualityTests.BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token` |
| No public signature names a `Microsoft.Win32` or `System.Runtime.InteropServices` type | The contracts stay platform-neutral and fakeable; a registry key or safe handle in one binds every consumer to Windows plumbing | `AssemblyQualityTests.BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface` |
| No project declares a grant of its own: every one compiles the generated `AssemblyInfo.InternalsVisibleTo.cs`, which grants every assembly this repository builds, and `AssemblyInfo.InternalsVisibleTo.External.cs`, the hand-written grants to the maintainer's other repositories | `internal` means solution-internal, so a seam between two projects stays internal, and a member no other assembly may reach is `private`. None of it is a security boundary: nothing here is strong-named | `scripts/repo-conventions.cs check`; `AssemblyQualityTests.BNAQ1005_every_friend_grant_names_an_allowed_assembly` |
| `src/Directory.Build.props` imports the root props explicitly | MSBuild applies only the closest `Directory.Build.props`; without the import every project here silently loses the target framework, nullable settings, package metadata and AutoVersioning | the import line; `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` fails if the file stops applying |
| `IsTrimmable` and `EnableTrimAnalyzer` stay on; `IsAotCompatible` stays off everywhere but `AppServices.EntryPoint` | The trimmable mark travels in the package; the analyser runs here or nowhere. AOT compatibility is claimed only where something measures it, and only `AppServices.EntryPoint` is measured: CI's `aot-linux` and `aot-windows` jobs publish it with native AOT | `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable`; `src/Directory.Build.props`, comment; `AppServices.EntryPoint.csproj`, comment |

⚠ **The trim analyser cannot see compiled XAML.** Every dialog here is built in C#, so it sees all of
them today. A project that adds `.axaml` files needs an ILLink pass as well; `src/Directory.Build.props`
explains how.
