# 00002 — LayeredEditors becomes two package repositories

> Status: **approved 2026-09-22**. Supersedes nothing. Applies [`00001`](00001-package-template.md).

The shared libraries leave `JanusMael/ClaudeForge` as **seven ids across two new repositories**
generated from `bbpkg`, and publish to nuget.org through trusted publishing. `AgentForge.*` and
`JsonC` stay on GitHub Packages, untouched.

⭐ **This is the end state ClaudeForge's own `plans/00001` named**, not a reversal of it. That plan
put *"Publishing anything publicly"* under **Not in scope**, gated public publication on three
projects exercising the libraries, and called splitting them into their own repository "the natural
end state of this direction". This is that split.

⚠ **The gate is met for the CODE and not the PACKAGING.** The third project is DiffView, which
consumes `LayeredEditors.Avalonia.Diagnostics` `1.0.1` — the *unprefixed* id — hand-packed into a
folder feed. No `Bennewitz.Ninja.LayeredEditors.*` id at CalVer has ever been resolved by anything.
Packaging is the half this plan makes permanent, so the gate must not be read the stronger way.

## Decisions

The package boundaries and names are derived in
[`docs/layered-editors-package-split.md`](../docs/layered-editors-package-split.md), from measured
imports rather than from the folder names. This plan records what follows from it.

| # | Decision | Why |
|---|---|---|
| 1 | **Two repositories, seven ids** — `Bennewitz.Ninja.AppServices` (4) and `Bennewitz.Ninja.ScopedEditors` (3) | A package's identity is its dependency closure. Every split here is one the code already made internally and the packaging failed to follow |
| 2 | **`LayeredEditors` → `ScopedEditors`** | "Editors" is accurate and kept — the family is a model, view-models and views for editing. "Scoped" replaces the ambiguous "Layered", naming the override model precisely. ⛔ Deliberately not "Settings", which reads as flat key-value pairs when this is schema-driven editing of structured data |
| 3 | **Staged move.** The new repositories publish and are verified from the feed **before** anything is deleted from ClaudeForge | A published version cannot be corrected, so a deletion must never rest on an unverified release |
| 4 | **Only these seven.** `AgentForge.*` and `JsonC` keep publishing to GitHub Packages exactly as today | Smallest irreversible step. Scope can widen later and can never narrow |
| 5 | **The service APIs change shape before first publish** — async, richer returns, no logger dependency (below) | Every one of these is breaking after publication and free before it |
| 6 | **`NUGET_USER` is a repo variable** in both repositories, from the first commit | Masking turns the 401 into `owned by user '***'` and hides the only value that diagnoses it |

⛔ **The generated skeleton is scaffolding, not a shipping id.** `dotnet new bbpkg -n AppServices`
produces a library whose id is the bare stem. For `AppServices` that id **is** wanted; for
`ScopedEditors` it is **not** — there is no bare `Bennewitz.Ninja.ScopedEditors` package — so
that one is deleted rather than kept. Shipping it would publish an eighth id nobody asked for,
permanently.

⛔ **One policy per repository, never one per id.** nuget.org mints one API key per token exchange
scoped to one matching policy, so a second policy matching the same claims is never consulted and
its package is rejected `403` — after the first is already permanent.

| Repository | Policy pattern | Covers |
|---|---|---|
| `Bennewitz.Ninja.AppServices` | `Bennewitz.Ninja.AppServices*` | the bare stem **and** its suffixed siblings — note no dot before `*` |
| `Bennewitz.Ninja.ScopedEditors` | `Bennewitz.Ninja.ScopedEditors.*` | the three suffixed ids, and deliberately **not** the bare stem |

## Service API shape

`ShellLauncher` has **eight `catch` blocks and eight bare `bool` returns** — eight causes arriving
as one bit. All three changes below are free now and breaking later.

- **Asynchronous**, returning `ValueTask<T>` and taking a `CancellationToken`. An async signature
  wraps a synchronous implementation for free; a synchronous one can only wrap an async
  implementation by blocking, which deadlocks a UI thread. iOS's
  `openURL:options:completionHandler:` has no synchronous form to offer.
- **A result, not a bool.** ⛔ The decisive case is not failure: `RevealInFileManager` on a phone is
  *unsupported*, and a caller must hide the affordance rather than show an error. A bool cannot say
  which. `LaunchResult` is a `readonly record struct` over a `LaunchStatus` enum
  (`Succeeded`, `Unsupported`, `NotFound`, `Denied`, `Cancelled`, `Failed`) with a `Succeeded`
  property so terse call sites stay terse.
- **No logger dependency.** Widening the result removes most of the need — the caller already owns a
  logger. For narrative a result cannot carry, a package-defined `DiagnosticSink` delegate
  (level, message, exception) supplied once via the constructor. ⓘ **Standing rule for this lane:
  contracts-only logging, or none.** If the stance ever breaks, `Microsoft.Extensions.Logging.Abstractions`
  — never Serilog, which would force one logging implementation on every consumer.

## Scope

**In:** both repositories and their first releases; moving the projects and their tests; the rename;
the API reshaping above; `packages.push`; one trusted-publishing policy per repository;
`NUGET_USER`; the credential preflight; verification against nuget.org rather than a green run.

**Out:** `LiveLogWindow`, `LiveTailWindow` and `HeaderLink` — held back from the first release, because the tail window is being replaced by a general control built elsewhere and the live log window is due a rework. A published id's public types are a promise, and shipping a shape already decided against buys a breaking change in weeks. ⭐ This is why `LiveLogWindowSink` must be renamed: it ships without the window it is named after, and never referenced one.

**Also out:** `AgentForge.*` and `JsonC`. Removing the source from ClaudeForge — that is stage two, its
own change there, after this is verified. Repointing DiffView. Unlisting anything already on GitHub
Packages. Any change to ClaudeForge's `IsPackable` split, which is already correct at 11/3.

## Steps

Steps 1–8 are free to repeat. Nothing past step 9 is recoverable.

1. **Generate both repositories.** `dotnet new install Bennewitz.Ninja.Templates`, then
   `dotnet new bbpkg -n AppServices --RepoOwner JanusMael` and the same for `ScopedEditors`.
   *Proves it:* each generated tree builds, tests pass, `dotnet pack` produces the skeleton id.
2. **Move the projects**, preserving the dependency graph and deleting the `ScopedEditors`
   skeleton. *Proves it:* `dotnet pack` produces exactly four ids in one repository and three in the
   other, all prefixed, and no bare `ScopedEditors`.
3. **Apply the rename** across namespaces, `PackageId`s and every consuming reference.
   *Proves it:* nothing anywhere still says `LayeredEditors`; a grep is the check.
4. **Reshape the service APIs** per the section above. *Proves it:* the interfaces return
   `ValueTask<LaunchResult>`; a test asserts `Unsupported` is distinguishable from `Failed`.
5. **Carry the guards across.** ClaudeForge's `AssemblyLayeringTests` and `PackageMetadataTests`
   enforce that these libraries reference nothing product-specific, and moving the code leaves them
   behind. *Proves it:* an equivalent test in each new repository fails when a reference outside the
   family is added. ⚠ It must scan **csproj XML as well as by reflection** — the compiler omits
   unused references from the assembly reference table, so a declared-but-unused bad reference is
   invisible to reflection alone.
6. **`packages.push` lists the shipping ids; `packages.local` is empty with a comment saying so.**
   *Proves it:* the template's test asserting `packed == push ∪ local`, reading ids from the
   `.nuspec` **inside** each `.nupkg` — never from the filename, since `<id>.<version>.nupkg` is not
   decidable lexically.
7. **Create one policy per repository** at <https://www.nuget.org/account/trustedpublishing>: owner
   `JanusMael` (the individual account), the repository, the release workflow's **filename**,
   Environment **blank**, scopes allowing **new packages** and new versions, and the pattern from
   the table above. *Proves it:* each policy lists with no pending or inactive warning.
8. **Set `NUGET_USER`** in both, then **preflight from a branch**.
   *Proves it:* login succeeds, the log names the user in plain text, build/pack/push all skipped.
   ⓘ The policy is not branch-scoped, so this needs no tag and no merge. ⚠ A green preflight proves
   *a* policy matched, never that it covers every id — check them by eye, because nothing else will.
9. **Tag and publish** both. `YYYY.Q.MMDD`, one release per calendar day per repository.
   *Proves it:* all seven ids indexed via the nuget.org flat-container API — **not** a green run —
   and a throwaway project referencing `Bennewitz.Ninja.ScopedEditors.Avalonia` restores and
   builds with **no PAT configured anywhere**.

## After this plan

- **Stage two, in ClaudeForge:** delete the moved projects, consume the published packages, reshape
  its `nuget.config`. Its own change, only once step 9 is verified.
- **DiffView, owned by its session:** repoint from the unprefixed `LayeredEditors.Avalonia.Diagnostics`
  `1.0.1` on a folder feed to the prefixed id at CalVer. ⭐ **This is the only thing that proves the
  loop actually shortened** — until it happens the benefit is prospective, and the step 9
  throwaway-project check would pass while the one real consumer never moved.

## Verified before drafting

All five original ids are **free** on nuget.org and the `Bennewitz.Ninja.*` prefix reservation is
live, so policies can exist before anything is published. `JanusMael/ClaudeForge` is **public**, so
these were never private — GitHub Packages inherits repository visibility, and what changes is which
feed and whether reads need a token. `Avalonia.Diagnostics` and `Avalonia.Desktop` are **real
published packages**, which is why no id here is named for either.

## Open

- Whether the generic control replacing `LiveTailWindow` is genuinely general. If it is, it is not a
  logging type and wants a controls home rather than `AppServices.Avalonia`.

ⓘ Two earlier open items are now **settled by measurement** and recorded in the split document:
`ScopedEditors.ViewModels` uses no dialog types, so the two families share no edge; and the
severity model belongs with the editors rather than with app services.
