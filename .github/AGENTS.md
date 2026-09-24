# AGENTS.md — `.github/`

The workflows and the repository's GitHub settings.

| File | What it is |
|---|---|
| `workflows/ci.yml` | On every push and pull request: `build` (build and test), `pack` (pack, then `scripts/assert-packages.cs`) and `conventions` (`scripts/repo-conventions.cs check`) |
| `workflows/release.yml` | Publishes to nuget.org through trusted publishing, on a `v*.*.*` tag. Dispatched with the version blank, it only proves the credentials |
| `repository.json` | What this repository's GitHub settings vary by: description, topics, required checks, exemptions. `scripts/repo-conventions.cs` applies and checks it |
| `copilot-instructions.md` | A pointer to the root `AGENTS.md` for tools that look here |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **The CI job names `build`, `pack` and `conventions` are required checks**, listed under `requiredChecks` in `repository.json`. Renaming or removing one means updating the list and running `apply` in the same change | A required check that no job reports blocks every pull request, and GitHub never says why | `scripts/repo-conventions.cs check` fails on the mismatch |
| **`release.yml` keeps its file name** | The trusted-publishing policy on nuget.org names this file, and the OIDC token is bound to the name. Renamed, every login fails | `docs/publishing.md`, policy field `Workflow File` |
| The release names each package it pushes and attaches, from `packages.push`, and never globs `*.nupkg` | A glob publishes whatever is in the folder, permanently | `PackagingTests.The_release_workflow_globs_nothing_and_publishes_what_is_declared`, `PackagingTests.The_release_workflow_never_names_a_private_package` |
| `release.yml` keeps its credential preflight: every build, push and release step is gated on `RELEASING` | A blank-version dispatch must prove the login without publishing | `PackagingTests.The_release_workflow_has_a_credential_preflight` |
| Every gate runs before `NuGet Login (Trusted Publishing)`, and the login stays immediately before `Push to NuGet.org` | Past the push nothing can be undone, and the login's token is short-lived | `release.yml`, step order and comments |
| `release.yml` holds `id-token: write` and `contents: write`, nothing broader | `id-token: write` is what replaces a stored API key; `contents: write` creates the GitHub Release | `release.yml`, `permissions` |
| The `conventions` job holds `contents: read` only | `check` reads settings through the workflow's token and must not be able to change them | `ci.yml`, `permissions` |

⚠ `PackagingTests` drops comment lines before searching `release.yml`, so a comment may explain the
glob it forbids; a command line may not contain one.
