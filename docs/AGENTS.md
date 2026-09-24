# AGENTS.md — `docs/`

Documents for humans. What an agent needs lives in the `AGENTS.md` files, not here. Nothing here is
packed; `README.md` at the root is what nuget.org shows.

| Document | What it is | Kept in step with |
|---|---|---|
| `publishing.md` | The release runbook: one-time trusted-publishing setup, the `YYYY.Q.MMDD` version rule, releasing, verifying against the nuget.org flat-container, and the failure table | `.github/workflows/release.yml`. A step renamed or reordered there is renamed or reordered here in the same change |

## Rules

| Rule | Why |
|---|---|
| The trusted-publishing policy in `publishing.md` stays one policy whose glob covers every id in `packages.push` | nuget.org scopes one token exchange to one policy, so a second policy's package is rejected `403` after the first has published permanently |
| `publishing.md` names `NUGET_USER` as a repository variable holding the nuget.org profile name | That is what `release.yml` reads (`vars.NUGET_USER`); a masked secret hides the value that explains a `401` |
