# AGENTS.md — `scripts/`

File-based C# apps, run with `dotnet run`. Each is compiled with this repository's
`Directory.Build.props`, so warnings are errors here too.

| Script | What it does | Run by |
|---|---|---|
| `assert-packages.cs` | Checks that the packages `dotnet pack` actually produced are exactly the ones `packages.push` and `packages.local` declare | CI's `pack` job, and the release step `Assert packed matches declared` before it publishes |
| `repo-conventions.cs` | Checks, and applies, the family's repository conventions: documentation, GitHub settings and rulesets | CI's `conventions` job, and the maintainer |

## Rules

| Rule | Why |
|---|---|
| **`repo-conventions.cs` is never edited here** | It is a copy of `templates/bbpkg/scripts/repo-conventions.cs` in Bennewitz.Ninja.Templates, identical in every family repository. Change it there and copy it back; run from that repository, `check --repo` reports every copy that differs |
| `assert-packages.cs` reads each id from the `.nuspec` inside the package, never from the file name | `<id>.<version>.nupkg` cannot be split reliably: nothing separates an id ending in `.Widget` from one ending in `.Widget.2026` |
| `assert-packages.cs` fails on an empty or missing packages directory | A release that publishes nothing is a failure, not a no-op |
| A file-based app is compiled trimmed | Reflection-based serialisation fails the build with `IL2026`; `repo-conventions.cs` builds JSON with `System.Text.Json.Nodes` for that reason |
| Run `repo-conventions.cs` with `dotnet run --file` | Beside a `.csproj`, `dotnet run <file>` binds to the project instead |
