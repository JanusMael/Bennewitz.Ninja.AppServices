# AGENTS.md — Bennewitz.Ninja.AppServices

> For anyone changing this repository, human or agent. The invariants a change must not break, the
> commands, and the checklists for recurring work. Each top-level directory has an `AGENTS.md` of
> its own for what only its files show. Work state is [`PROGRESS.md`](PROGRESS.md). What every
> repository in this family carries, and how it is checked, is prescribed in
> [`docs/repository-conventions.md`](https://github.com/JanusMael/Bennewitz.Ninja.Templates/blob/main/docs/repository-conventions.md)
> in Bennewitz.Ninja.Templates.

## What this repository is

Application services for desktop apps — shell launching, environment probing, sharing, dialogs and
logging — behind contracts a test can fake without referencing a UI toolkit. It ships packages to
nuget.org, layered so a consumer takes only what it needs: `Bennewitz.Ninja.AppServices.Abstractions`
(the contracts and the dialog vocabulary, referencing nothing), `Bennewitz.Ninja.AppServices` (default
implementations that need an operating system but no UI framework), `Bennewitz.Ninja.AppServices.Logging`
(Serilog sinks) and `Bennewitz.Ninja.AppServices.Avalonia` (Avalonia dialogs, pickers and the bridge
from Avalonia's logger into Serilog). The code moved here out of OpenForge2k, whose Avalonia
applications are the consumers: they reference the Avalonia package, which brings its siblings
through its project references. A test or a non-UI host references `.Abstractions` or
`Bennewitz.Ninja.AppServices` alone. `Bennewitz.Ninja.AppServices.EntryPoint`, referencing nothing, is
the entry point every app's `Main` calls, console, web and desktop alike (`plans/00001`).
`README.md` is packed into every package and is the nuget.org description.

## Layout

| Directory | What it holds |
|---|---|
| `src/` | The shipped projects and `src/Directory.Build.props`, which marks them trimmable |
| `tests/` | `AppServices.Tests`: the ported suites, the headless dialog tests, the entry-point tests, and the architecture and packaging guards. `EntryPointProbe` and `EntryPointWebProbe`: the apps the entry-point tests run |
| `scripts/` | File-based apps: `assert-packages.cs` and `repo-conventions.cs` |
| `docs/` | `publishing.md`, the release runbook |
| `plans/` | Numbered plans, `NNNNN-slug.md`: a draft is edited in place, an approved one never again |
| `.github/` | The workflows, `repository.json`, and the pointer for tools that read `.github/` |

## Invariants

| Invariant | Failure if broken | Guarded by |
|---|---|---|
| Every packable project's id is in exactly one of `packages.push` and `packages.local` | A package nobody chose is published, permanently | `PackagingTests.Every_packable_project_is_classified`, `PackagingTests.No_id_is_both_published_and_private`; `scripts/assert-packages.cs`; the release step `Assert packed matches declared` |
| The release pushes the ids `packages.push` names and never globs `*.nupkg` | A new packable project is published by the next tag | `.github/workflows/release.yml`, steps `Push to NuGet.org` and `Create GitHub Release`; `PackagingTests.The_release_workflow_globs_nothing_and_publishes_what_is_declared` |
| `AppServices.Abstractions` references nothing but the framework | A test or non-UI host inherits a UI toolkit or a logger, and the split stops paying for itself | `LayeringTests.Abstractions_compiles_against_nothing_but_the_framework`, `LayeringTests.No_project_reaches_outside_its_tier` |
| `AppServices.EntryPoint` references nothing but the framework | A native AOT app that takes it for its `Main` carries whatever it references | `LayeringTests.EntryPoint_compiles_against_nothing_but_the_framework`, `LayeringTests.No_project_reaches_outside_its_tier` |
| `AppServices.EntryPoint` is native AOT compatible, measured | A native AOT app, such as the family's `bbapi`, fails to publish once it calls `AppMain` | `IsAotCompatible` in its csproj; CI's `aot-linux` and `aot-windows` publish `tests/EntryPointProbe` with native AOT and run `EntryPointProbeTests` against the binary |
| An entry point exits 0 on success, 1 when an exception escapes, 2 on a console usage error and 130 after Ctrl+C, with the fatal report on stderr before any callback | A calling script cannot tell a crash from a bad invocation or an interruption, and a failing logger takes the report with it | `AppMainTests`, `EntryPointProbeTests` |
| The app's log is flushed on every exit, SIGTERM included: a console or desktop run flushes from its SIGTERM registration, and a web run's host ends the run | A stopped service or a closed session loses its log's last events, since on Linux the runtime raises no `ProcessExit` for SIGTERM | `AppMainTests.The_log_is_flushed_once_per_run_however_the_run_ends`, `EntryPointProbeTests.SIGTERM_flushes_the_log_and_the_signal_still_ends_the_process` |
| `HostAbortedException` passes through `AppMain` unreported | A design-time tool's normal stop is reported as a crash, and shows a desktop app's fatal dialog | `AppMainTests.A_HostAbortedException_passes_through_unreported`, `EntryPointWebProbeTests.A_stop_on_HostBuilt_passes_its_HostAbortedException_back_with_nothing_on_stderr` |
| Each project reaches only what its tier allows: only `AppServices.AvaloniaUI` sees Avalonia, and `AppServices.Logging` sees Serilog and nothing else | A lower package drags a UI framework into every consumer | `LayeringTests.Tiers`; `LayeringTests.No_project_declares_a_package_its_tier_forbids`; `AssemblyQualityTests.BNAQ1003_no_assembly_references_what_its_tier_forbids` |
| Nothing here references another family (`LayeringTests.ForeignFamilies`) | The families can no longer be versioned or abandoned independently | `LayeringTests.Nothing_here_reaches_another_family`, `LayeringTests.No_compiled_assembly_reaches_another_family` |
| Every shipped assembly passes the family's AssemblyQuality rules `BNAQ1001`–`BNAQ1005` | A package ships a violation nothing ran | `AssemblyQualityTests`, one test per rule and `Every_shipped_assembly_is_in_the_scan` |
| Every project grants `InternalsVisibleTo` to every assembly this repository builds, from the generated `AssemblyInfo.InternalsVisibleTo.cs`, and to other repositories only from `AssemblyInfo.InternalsVisibleTo.External.cs`; no project declares a grant of its own | A seam between two projects becomes public API for want of a grant, and a grant from anywhere else goes unseen, or nowhere when its name is wrong | `scripts/repo-conventions.cs check`; `AssemblyQualityTests.BNAQ1005_every_friend_grant_names_an_allowed_assembly` |
| No public method takes a defaulted `CancellationToken`, or is a token-less overload of one that takes it | A caller silently opts out of cancellation | `AssemblyQualityTests.BNAQ1001_no_public_method_takes_a_defaulted_cancellation_token` |
| No public signature names a `Microsoft.Win32` or `System.Runtime.InteropServices` type | A contract becomes Windows-shaped, and every consumer binds against platform plumbing it never chose | `AssemblyQualityTests.BNAQ1002_no_platform_or_leak_prone_type_appears_in_the_public_surface` |
| No namespace segment shadows a referenced root, so the Avalonia assembly and namespaces are `AppServices.AvaloniaUI` while its package id stays `Bennewitz.Ninja.AppServices.Avalonia` | Under a `….Avalonia` namespace, `Avalonia.X` resolves against it instead of Avalonia's root; a changed package id strands every consumer on the old one | `AssemblyQualityTests.BNAQ1004_no_namespace_segment_shadows_a_referenced_root`; `PackageId` in `src/AppServices.AvaloniaUI/AppServices.AvaloniaUI.csproj` |
| Every shipped assembly declares itself trimmable | A consumer's trimmed publish keeps it whole and outside its trim analysis, and says nothing | `src/Directory.Build.props`; `PackageMetadataTests.Every_shipped_assembly_is_marked_trimmable` |
| Every packable project states `IsPackable`, a `Description` and `PackageTags`, with no `TODO`, and the packed `README.md` has no placeholder | Placeholder metadata is published and can never be replaced | `PackageMetadataTests` |
| Avalonia is pinned at 12.1.3 or later | UI Automation selection stops reaching the client on Windows; the headless tests cannot see it | `Directory.Packages.props`, `UI` group comment; `Avalonia.Headless` pinned to the same version |
| Packages resolve from nuget.org only | A second source added later silently starts supplying packages | `NuGet.config`, `packageSourceMapping` |
| Versions are pinned centrally, transitive ones included | Two projects drift to different versions of one dependency | `Directory.Packages.props`, `CentralPackageTransitivePinningEnabled` |
| Warnings are errors | A warning ships | `Directory.Build.props`, `TreatWarningsAsErrors`; `ci.yml` builds with `-warnaserror` |
| The version is the tag, `vYYYY.Q.MMDD` | The package and the tag disagree. A published version can never be replaced | `release.yml`, step `Resolve version and tag` |
| `NUGET_USER` is a repository **variable**, not a secret | A masked value hides why a trusted-publishing login fails | `release.yml`, step `Refuse to release without NUGET_USER` |
| The repository meets the family conventions | Documentation or settings go missing unnoticed | `scripts/repo-conventions.cs`, run by CI's `conventions` job |

## Commands

```bash
dotnet build AppServices.slnx -c Release --nologo -warnaserror
dotnet test --solution AppServices.slnx --no-build -c Release
dotnet pack AppServices.slnx -c Release --nologo --output ./packages/Release
dotnet run scripts/assert-packages.cs -- ./packages/Release
dotnet run --file scripts/repo-conventions.cs -- check
dotnet run --file scripts/repo-conventions.cs -- grants
dotnet publish tests/EntryPointProbe/EntryPointProbe.csproj -c Release -r <rid> -p:PublishAot=true -warnaserror
```

- Tests run on Microsoft.Testing.Platform (`global.json`), so `dotnet test` takes `--solution` and
  rejects VSTest-only switches such as `--nologo`. `--no-build` needs the Release build first.
- Write `-p:` rather than `/p:`: Git Bash on Windows rewrites a leading-slash argument into a path.
- `grants` rewrites `AssemblyInfo.InternalsVisibleTo.cs` from every project's assembly name, and
  writes an empty External file when there is none. Run it after adding, removing or renaming a
  project; `check` fails until the list matches.
- The native AOT publish links with the platform's C toolchain: clang and zlib on Linux, the Visual
  Studio C++ tools on Windows. Visual Studio 18 Build Tools' `vcvarsall.bat` calls `vswhere.exe` by
  name, so put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on PATH first, or the link
  fails with its "is not recognized" message where the linker's path should be.

## Checklists

**Adding a package**
1. Add the project under `src/`, with `IsPackable`, a `PackageId` of `Bennewitz.Ninja.AppServices.<Name>`,
   an `AssemblyName` equal to the project file name, a `Description`, `PackageTags` and
   `GenerateDocumentationFile`.
2. Run `dotnet run --file scripts/repo-conventions.cs -- grants`, so the generated grants name the new
   assembly; `check` fails until they do.
3. Add its row to `LayeringTests.Tiers`: what it may reference and which packages it must not.
4. Add its id to `packages.push`, or to `packages.local` with the reason as a comment above it. The
   trusted-publishing pattern `Bennewitz.Ninja.AppServices*` already covers a `Bennewitz.Ninja.AppServices.` id;
   an id outside it needs the policy widened first, see `docs/publishing.md`.
5. Add it to the package table in `README.md`, and record it in `PROGRESS.md`.

**Adding a top-level directory:** give it an `AGENTS.md` and a `CLAUDE.md` containing `@AGENTS.md`,
or exempt it in `.github/repository.json` under `undocumented`, with the reason. CI fails until
one of the two is done.

**Releasing:** `docs/publishing.md`. One version per calendar day; verify against the nuget.org
flat-container, then move the release into `PROGRESS.md` under `Published`.

**Every change:** update `PROGRESS.md` in the same commit. Commits are Conventional Commits, with
`!` on a change that breaks a consumer, and a breaking change gets a line in `README.md`, which is
what nuget.org shows.
