# Progress

Work state for `Bennewitz.Ninja.Templates`. Plan: [plans/00001-package-template.md](plans/00001-package-template.md),
approved 2026-09-20 and frozen — drift from it is recorded here, never edited into it.

## Status

| Step | State | Notes |
|---|---|---|
| 1 · Repo + approved plan | **done** | `ca2552f`, the plan alone |
| 2 · Packaging project + skeleton | **done** | Packs clean; layout verified inside the `.nupkg` |
| 3 · `template.json` | **done** | Install-from-nupkg → generate → names verified |
| 4 · Packaging tests | **done** | Both halves. Shipped: `PackagingTests` + `scripts/assert-packages.cs`. Root: `tests/Templates.Tests`, 13 tests, asserting **both** release workflows by path. Mutation-tested |
| 5 · Both CI and release workflows | **done** | Both halves. The root pair was missing until step 8 and this row wrongly read done — see the drift note |
| 6 · `package-release` skill, in `bb-skills` | **done** | Approved as `*.proposed`, then installed alone — see below |
| 7 · `verify-release` | **done** | `scripts/verify-release.cs`; pack → packed-content → install-from-nupkg → generate → tree → build/test/pack. Feed poll and published install are step 8's, per the plan |
| 8 · First release | **done** | `2026.3.921` published 2026-09-21 and verified from the feed: installed from nuget.org, generated, tree asserted, generated repo builds/tests/packs. `isTemplate` flipped |

Generating a repo from the template currently yields: **build clean with `-warnaserror`, 11 tests
passing, pack succeeding, and `assert-packages` green** — on the first run, with no edits.

## Drift from the approved plan

**Renamed to `Bennewitz.Ninja.Templates`** — repo, package id and directory. The plan's Decisions
table names `Bennewitz.Ninja.PackageTemplate`, decided before the ecosystem convention was checked.

Every `PackageType=Template` package with meaningful download share is **plural**:
`Microsoft.Android.Templates`, `Microsoft.iOS.Templates`, `Microsoft.MacCatalyst.Templates`,
`Aspire.ProjectTemplates`, `Uno.ProjectTemplates.Dotnet`, `HotChocolate.Templates.Server`. Singular
survives only in `Furion.Template.*`, where it is a prefix across a dozen per-flavour packages.

The structural reason matters more than the convention: a template package **bundles one or more
templates**, so the id names a container. `bbpkg` is the first; a second ships inside the same
package instead of claiming a new id, which a singular name fights.

Done while nothing was published and no remote existed, which is the only cheap moment for it. The
repo name still follows the package stem, so that decision is unchanged.

**Steps 4 and 5 were done together.** The plan orders the packaging tests before the workflows, but
the tests assert against those workflows, so step 4 alone could only have been written blind. No
scope changed; only the order.

**The release workflow is driven by `packages.push` rather than repeating the ids.** The plan's
step 4 verification expects each id named literally in the workflow. A template cannot know how many
ids a repository will have — and more importantly, a workflow that repeats the list has two homes
for it, and the second silently rots when a package is added. That is the failure this whole
mechanism exists to prevent, reintroduced by the guard meant to stop it. So the workflow reads the
file, the test asserts it reads the file and globs nothing, and `assert-packages` closes the loop
against the packed output.

**Package path has no `templates/` segment.** The plan's step 2 verification names
`content/templates/bbpkg/.template.config/template.json`. The explicit
`PackagePath="content\%(RecursiveDir)…"` produces `content/bbpkg/.template.config/template.json`,
because `%(RecursiveDir)` is relative to the wildcard root and so excludes `templates/` itself.

The shorter path is correct and is what the engine expects; the plan's line described the behaviour
of `ContentTargetFolders`, which that step replaced. No action — recorded so the next reader is not
surprised by the mismatch.

## Verified, not assumed

Each of these was run, not reasoned about. They are the findings that four rounds of plan review did
not surface.

- **`dotnet pack` fails without the NU5128 suppression.** `Directory.Build.props` sets
  `TreatWarningsAsErrors`, and a template package ships no `lib/` by design. Reproduced twice
  independently before the suppression existed.
- **`IncludeContentInPack=false` is not an alternative fix** — it drops the packed README and pack
  dies with NU5017 instead.
- **Content outside the packaging project's cone flattens**, dropping `.template.config/`, and the
  resulting package still installs, still lists, and still "generates" — emitting the extracted
  nupkg. Any check written against an exit code passes on it. This is why step 3 installs from the
  packed `.nupkg` and asserts the generated tree.
- **One `sourceName` cannot produce both names.** Verified end to end:
  `dotnet new bbpkg -n Widget` yields `AssemblyName` `Widget` and `PackageId`
  `Bennewitz.Ninja.Widget`, the latter from a `join` generator over `name`.
- **NuGet excludes dotfiles from a package by default** (NU5119), so a template ships generated
  repos with no `.gitignore` and no `.gitattributes`. `NoDefaultExcludes` fixes it. ⚠ This is a
  *warning*: the family's `TreatWarningsAsErrors` is the only reason it surfaced here rather than in
  somebody's generated repo.
- **`PackagePath` must be the bare root `content/`.** Both more explicit forms are wrong, and both
  were tried: `content/%(RecursiveDir)%(Filename)%(Extension)` is correct for every file *with* an
  extension and packs `LICENSE` to `content/bbpkg/LICENSE/bbpkg/LICENSE`, because NuGet reads a
  final segment with no extension as a folder. `content/%(RecursiveDir)` is a folder too, so NuGet
  appends the recursive path a second time.
- **⛔ The trailing separator must be a FORWARD slash, and this one shipped broken.** NuGet decides
  "folder or file?" by whether `PackagePath` ends in a directory separator, and a backslash is
  only a separator on Windows. `content\` was correct on Windows and **flattened on Linux**,
  dropping `.template.config/` — precisely the failure the bare-root form exists to prevent,
  reintroduced by the separator rather than the path. Every earlier observation in this section
  was a Windows pack, which is why nothing caught it; the release runs on `ubuntu-latest`, so the
  first real release would have published a template that installs, lists and "generates" while
  emitting the extracted nupkg.
  ⭐ Found by `verify-release` on this repository's **first CI run**, in the 26 seconds after the
  branch merged. That is the whole argument for running the gate on the platform that publishes
  rather than trusting a green local check — and the bug predates all of step 7, having been in
  the packaging project since step 2.
- **Both guards fail when they should.** Removing the id from `packages.push` fails
  `Every_packable_project_is_classified` *and* makes `assert-packages` exit `1` — checked without a
  pipe, since `$?` after one reports the last command's status rather than the script's.
- **Duplicate template installs break generation.** Installing the same identity from a folder and
  from a `.nupkg` leaves two registrations and `dotnet new` fails with "Sequence contains more than
  one matching element". Uninstall until `dotnet new list` is clean, and clear
  `~/.templateengine/packages/`, before reinstalling.
- **`dotnet run <file.cs>` binds to the PROJECT when the working directory has one.** From this
  repository root it tries to run `Bennewitz.Ninja.Templates.csproj` and fails with *"The current
  OutputType is 'Library'"*. `verify-release` therefore needs `dotnet run --file`, while
  `assert-packages` does not: a generated repository keeps its projects under `src/` and `tests/`,
  so nothing at its root captures the bare form. Both invocations are correct where they appear,
  which is why they differ.
- **This repository's own `Directory.Build.props` still carried XamlQuality's identity** — both
  `PackageProjectUrl` and `RepositoryUrl` pointed at `Bennewitz.Ninja.XamlQuality`, and
  `AssemblyProduct` read `XamlQuality`. The shipped props under `templates/bbpkg/` were always
  correct, tokenised as `REPO_OWNER`/`PKG_ID`; this was the template repo's own copy-paste
  leftover. Found by `verify-release` on its first run, before step 8 could make it permanent.
  Fixed, and the check compares the packed nuspec against `git remote get-url origin` rather than
  a constant, so a rename cannot rot it.
- **The same leftover was in `Directory.Packages.props`, and grep did not find it.** A
  `System.CommandLine` `PackageVersion` nothing here references, under a comment about "the rules
  LIBRARY", beside an xunit comment explaining the ThemeAudit port. Both describe XamlQuality
  without ever writing the word, so a search for `XamlQuality` — which is what confirmed the
  `Directory.Build.props` fix had no twins — came back clean. It was found by reading the file.
  ⚠ **The lesson is about the search, not the file:** copy-paste residue is identifiable by what
  it *describes*, and a string search only finds residue that names its origin. Removed; the two
  genuinely-used entries and the TrxReport version-pin rationale stayed. Verified by deleting
  every `bin/` and `obj/` outside `templates/` and restoring from scratch, because an existing
  `obj/` caches the dependency graph and would have hidden a pin that was doing work.
- **`grep -qv '^[[:space:]]*#'` is not "has a real id" — a blank line is not a comment either.**
  The preflight's `packages.local` guard used the negated form, so a single blank line made it
  print the `⛔ That pattern must NOT match anything in packages.local:` header with nothing
  beneath it — while `packages.local`'s own header promises blank lines are ignored. Code
  contradicting its file's documented contract. Replaced in **both** workflows with the positive
  form `grep -qE '^[[:space:]]*[^#[:space:]]'`, checked against six shapes: comments only,
  comments plus a blank line, blanks only, a bare id, an indented id, and an empty file. Only the
  two blank-line cases changed behaviour. Cosmetic — it never affected what was pushed — but it
  shipped inside the template, so every generated repository would have inherited it.
- **`dotnet test --nologo` is rejected under Microsoft.Testing.Platform.** `global.json` selects
  MTP, which forwards unrecognised switches to the test app; xunit's runner answers
  *"Unknown option '--nologo'"* and the run ends **"Zero tests ran"**. It does exit non-zero
  (5), so CI fails rather than passing vacuously — but the message names the option, not the
  platform, so it reads like a typo rather than a runner difference. Neither workflow passes it.
- **The root packaging tests fail when they should**, checked rather than assumed, one mutation at
  a time with everything else restored in between:
  - id removed from `packages.push` → `Every_packable_project_is_classified` fails, naming
    `Bennewitz.Ninja.Templates`; 12 others still pass.
  - a `*.nupkg` glob put into the **shipped** `gh release create` →
    `The_release_workflow_globs_nothing_and_publishes_what_is_declared` fails for the shipped
    path. That is the mutation that matters: the shipped workflow never runs in this repository,
    so this test is the only thing standing between a glob and somebody's first generated release.
- **⛔ `NUGET_USER` was a secret, and the masking is what made the first release cost six runs.**
  The value was not a nuget.org username. Every preflight therefore returned nuget.org's
  deliberately vague 401 — `No matching trust policy owned by user '***' was found` — with the
  one diagnostic fact redacted by GitHub's secret masking. Six runs went into the policy fields
  instead: Environment, scopes, creator-versus-owner, delete-and-recreate. **The policy was
  correct from the first attempt.** Setting the value to the profile name `JanusMael` turned it
  green immediately.
  ⭐ It is now a repo **variable**, in both workflows and in the shipped runbook, so the error
  names the value. A profile name is public; the secret bought no protection and cost the
  diagnosis. The runbook's "Working the 401" list now *starts* with "read the value out loud".
  ⚠ The trap worth naming: the push step takes `--api-key`, so "this variable is the API key" is
  the natural reading. It is not — the key is an **output** of `NuGet/login`, minted per run. The
  action's documented input is *"Your NuGet account username."*

## The skill, as built (step 6)

It lives at `C:\c\cl\bb-skills\package-release\SKILL.md` and is installed to `~/.claude/skills/`.

⚠ **Its gate is not `packages.push`.** That was the first design, and testing it against the real
repositories showed it would decline in `Bennewitz.Ninja.XamlQuality` — the repository that taught it
every rule it states — because none of the five package repos has that file yet. It greps for
`NuGet/login` instead, engages in all five, and declines in a repository that does not publish to
nuget.org at all. That matters because it is installed globally and fires everywhere: FileServer,
AutoVersioning and chisel all glob their pushes by deliberate design.

⚠ **`Install-Skills.ps1` was NOT run.** It replaces every skill, and four of the eight differ from
their installed copies in both directions. `package-release` was installed on its own by copying its
folder. Resolving that drift is deferred.

## Next

All eight steps are done. `Bennewitz.Ninja.Templates 2026.3.921` is published and verified from
the feed, and the repository is marked as a GitHub template. What remains is follow-on work, none
of it blocking:

1. ✅ **`NUGET_USER` carried to `Bennewitz.Ninja.XamlQuality`** on 2026-09-21, and verified by a
   real credential preflight rather than by reading the diff: the run logged the profile name in
   plain text, which as a secret had printed `***`. ⓘ That run also proved a trusted-publishing
   policy is **not branch-scoped** — it matched from a feature branch — so credentials can be
   proven before anything is tagged.
2. **[`plans/00002`](plans/00002-layerededitors-becomes-two-package-repositories.md) is approved**
   — the first application of this template, to the shared libraries in `JanusMael/ClaudeForge`.
   Seven ids across two new repositories, `Bennewitz.Ninja.AppServices` and
   `Bennewitz.Ninja.ScopedEditors`. Its evidence is
   [`docs/layered-editors-package-split.md`](docs/layered-editors-package-split.md).

⛔ **This entry used to reserve `plans/00002` for `Bennewitz.Ninja.DiffView`, and that was wrong
twice over.** DiffView stopped being the interesting case when its ThemeAudit tool moved to
XamlQuality: it now packs two ordinary ids and names both explicitly, so it would never exercise
the `packages.local` half the template exists for. ClaudeForge does — eleven packable projects
across three families, one of which must not reach nuget.org. ⚠ DiffView is handled by a session on
another machine and is **not** tracked here; if it ever needs a plan it takes the next free number,
not this one.

ⓘ The `NUGET_USER` **secret** is still present on this repository alongside the variable. Harmless
and unread — the workflows take `vars.` — but delete it when convenient so there is one home for
the value.
