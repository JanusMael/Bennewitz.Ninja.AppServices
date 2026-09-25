# AGENTS.md — `src/`

The shipped projects. Everything here becomes a package, so everything here is permanent once
released.

| Project | Package id | May reference | Ships |
|---|---|---|---|
| `AppServices.Abstractions` | `Bennewitz.Ninja.AppServices.Abstractions` | nothing | `IShellLauncher`, `IShareService`, `IDialogService`, `IEnvironmentProvider`, `IPermissionPathPicker`, `ISaveChangesPrompt`, `LaunchResult`, `ShareOutcome`, `DiagnosticSink`, and `DialogMessage` under `Dialogs/` |
| `AppServices` | `Bennewitz.Ninja.AppServices` | `AppServices.Abstractions` | `ShellLauncher`, `DefaultShareService`, `DefaultEnvironmentProvider`, `NativeErrorDialog` |
| `AppServices.Logging` | `Bennewitz.Ninja.AppServices.Logging` | Serilog only | `BucketedRollingFileSink` |
| `AppServices.AvaloniaUI` | `Bennewitz.Ninja.AppServices.Avalonia` | its sibling projects, Avalonia, Serilog | `AvaloniaDialogService`, `FatalErrorDialog`, `NonFatalNoticeDialog`, `AvaloniaDiagnostics` with `AvaloniaDiagnosticsOptions`, `SerilogAvaloniaSink`, `BindingValidationErrorLogger` |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **`AppServices.Abstractions` has no references at all** | It is the package a test or a non-UI host consumes; a reference here quietly undoes the split | `LayeringTests.Abstractions_compiles_against_nothing_but_the_framework`; the comment in its csproj |
| A doc comment in `AppServices.Abstractions` names an implementation with `<c>`, never `cref` | The implementation is in a package it cannot reference, so the `cref` fails the build | `GenerateDocumentationFile` with warnings as errors |
| `AppServices` and `AppServices.Abstractions` take no logger; diagnostics go through a `DiagnosticSink` passed in | Neither package may reference Serilog | `LayeringTests.Tiers`; `LayeringTests.No_project_declares_a_package_its_tier_forbids` |
| `AppServices.Logging` holds sinks that need no window | A sink that calls into a window is a UI type; `LiveLogWindowSink` stayed in OpenForge2k for that reason | `LayeringTests.Tiers`; the comment in its csproj |
| `AppServices.AvaloniaUI` → `AppServices` is an allowed edge | `AvaloniaDiagnostics.ShowNativeFatalError` wraps `NativeErrorDialog` | `LayeringTests.Tiers`; the comment in its csproj |
| The Avalonia project's package id keeps `.Avalonia`; its assembly and namespaces stay `.AvaloniaUI` | A package id is permanent; a namespace segment `Avalonia` shadows Avalonia's root | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root` |
| `AssemblyName` equals the project file name; namespaces are `Bennewitz.Ninja.<AssemblyName>` | The layering guards match assemblies by name, so a mismatch drops a project from its own scan | `PackageMetadataTests.Every_assembly_name_matches_its_project_file`; `RootNamespace` in the root `Directory.Build.props` |
| Every `CancellationToken` parameter is required, and no public overload leaves it out | A default, or an overload that passes `CancellationToken.None`, lets a caller skip cancellation without noticing | `AssemblyQualityTests.BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token` |
| No public signature names a `Microsoft.Win32` or `System.Runtime.InteropServices` type | The contracts stay platform-neutral and fakeable; a registry key or safe handle in one binds every consumer to Windows plumbing | `AssemblyQualityTests.BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface` |
| `InternalsVisibleTo` grants go to `AppServices.Tests` only, and only where a test needs an internal | A broad grant makes internals part of what siblings can depend on | each project's `AssemblyInfo.cs` |
| `src/Directory.Build.props` imports the root props explicitly | MSBuild applies only the closest `Directory.Build.props`; without the import every project here silently loses the target framework, nullable settings, package metadata and AutoVersioning | the import line; `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` fails if the file stops applying |
| `IsTrimmable` and `EnableTrimAnalyzer` stay on; `IsAotCompatible` stays off | The trimmable mark travels in the package; the analyser runs here or nowhere. AOT compatibility is a claim nothing has measured | `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable`; `src/Directory.Build.props`, comment |

⚠ **The trim analyser cannot see compiled XAML.** Every dialog here is built in C#, so it sees all of
them today. A project that adds `.axaml` files needs an ILLink pass as well; `src/Directory.Build.props`
explains how.
