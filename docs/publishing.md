# Publishing to nuget.org

This repository publishes through **NuGet.org Trusted Publishing (OIDC)**. There is no API key
stored anywhere: the release workflow mints a short-lived token per run, so nothing in this
repository would still be valid if it leaked.

What publishes is declared in [`packages.push`](../packages.push), one id per line.
[`packages.local`](../packages.local) declares anything that packs but must **never** be published.
The release workflow reads both, so the declaration has exactly one home.

---

## Before the first release — one-time setup

Steps 1 and 2 are account-level and have to be done by the nuget.org account owner.

### 1 · Add the `NUGET_USER` variable

```bash
gh variable set NUGET_USER --body <your-nuget.org-profile-name> --repo JanusMael/Bennewitz.Ninja.AppServices
```

⭐ **A variable, not a secret.** Trusted publishing carries nothing to leak — this is the
nuget.org profile name the OIDC token is exchanged against, and a profile name is public. Making
it a secret buys no protection and costs the one thing you need when it is wrong: GitHub masks a
secret in logs, so the 401 below reads `owned by user '***'` and never tells you what was sent.

⛔ **The value is the nuget.org PROFILE NAME, not an email address.** An email is accepted and
then fails at login, where the error points at the policy rather than at the value.

⛔ **It is NOT an API key.** The easiest mistake to make, because the push step below takes
`--api-key` — but that key is an **output** of `NuGet/login`, minted per run and expiring. The
action's documented input is *"Your NuGet account username."* A key here produces the same 401 as
every other wrong value; if you generated one to try it, revoke it, because this setup exists so
that no long-lived key is needed at all.

⛔ **It is the profile name of whoever CREATED the policy**, which is not always whoever owns the
package. They differ whenever a policy is created under an organization.

### 2 · Create the trusted-publishing policy

At <https://www.nuget.org/account/trustedpublishing> → **Add policy**. **One policy**, however many
ids you publish:

| Field | Value |
|---|---|
| Policy owner | your individual account, **not** an organization |
| Repository Owner | `JanusMael` |
| Repository | `Bennewitz.Ninja.AppServices` |
| Workflow File | `release.yml` |
| Environment | **leave blank** |
| Scopes | **Publish new packages** *and* **Publish new versions of existing packages** |
| Glob Patterns | patterns covering every id in `packages.push` — and matching nothing in `packages.local` |

⛔ **ONE policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to **one** matching policy. Two policies matching the same repository and workflow means one is
chosen and the other's package is rejected `403` — *after* the first has published permanently. The
field is *Glob Patterns and Packages*, plural: a single policy carries as many patterns as you need.

⛔ **The scope must permit publishing NEW packages.** A scope limited to new versions of existing
packages matches nothing until an id exists, and every id is new until the first release lands.

⛔ **Environment must be BLANK.** It is filled in only when the publishing job declares
`environment:`, and this one deliberately does not. A value here matches nothing, and the failure
reads as *"no matching policy"* — which sends you looking at the repository name instead.

⛔ **Workflow File is the exact FILENAME**, no path and no `.github/workflows/` prefix, and it is
**not** the workflow's `name:` field. Matching is case-insensitive.

⚠ **A policy for a PRIVATE repository starts temporarily active for 7 days** and goes inactive if
nothing publishes in that window. The window can be restarted at any time.

### 3 · Preflight the credentials

**Release** → *Run workflow* → leave **version BLANK** → run.

A blank version logs in to NuGet.org and stops. Nothing is built and nothing is pushed, so run it as
often as you like. The run summary prints the ids from `packages.push` — check that your policy's
patterns cover every one of them.

⚠ **A green preflight proves the login works, not that the policy covers every id.** The exchange
succeeds before any package id is considered, so nothing in a preflight can detect a coverage gap.
Only the push can, and by then part of the release is permanent. The summary prints the list
precisely so the comparison is against something generated rather than remembered.

---

## Choosing the version

The standard across these packages is **`YYYY.Q.MMDD`** — year, quarter, then month-day with the
leading zero dropped. `2026.3.920` is 20 September 2026, in Q3.

⚠ **Day resolution means ONE release per calendar day.** A second tag on the same date collides with
a version that can never be replaced, and the recovery is to wait for tomorrow.

⛔ **A published version is permanent.** It cannot be replaced, re-pushed, or corrected — only
superseded by a higher one, or unlisted.

---

## Releasing

### 4 · Tag and push

```bash
git tag -a v2026.3.920 -m "Bennewitz.Ninja.AppServices 2026.3.920"
git push origin v2026.3.920
```

The version comes from the tag (`v` stripped), so the tag and the package agree by construction
rather than by remembering to bump a file.

ⓘ A release can also be started from the Actions tab — **Release** → *Run workflow* → **enter the
version** — which is the path to use when a tag already exists. Filling the version in is what
separates a release from the preflight; leaving it blank never publishes.

### 5 · Watch the run

```bash
gh run watch --repo JanusMael/Bennewitz.Ninja.AppServices --exit-status
```

Every gate runs **before** the push, because the push is the irreversible part: build, the full
suite, pack, and `assert-packages`, which compares what pack actually produced against what the two
lists declare.

### 6 · Verify against the feed, not against the workflow

A green workflow says the steps exited zero. Ask nuget.org, lower-casing the id:

```bash
curl -s https://api.nuget.org/v3-flatcontainer/<lowercase-id>/index.json
```

Indexing takes a few minutes, so a miss immediately after the run means "not yet", not "failed". The
registration index the CLI resolves through lags further behind than flatcontainer, so
`dotnet add package` may need another minute after the version appears in that listing.

---

## When something goes wrong

⛔ **Never re-tag to fix a failed run.** `gh run rerun` replays the YAML from the commit the tag
points at, so moving the tag does not change what runs — and deleting and re-pushing a tag to a new
commit makes the release history disagree with itself. Fix forward: land the fix on `main`, then
either dispatch the workflow by hand or cut the next day's version.

| Symptom | Cause | Fix |
|---|---|---|
| `NuGet/login` returns **403** | Job is missing `id-token: write` | It is present in `release.yml`; if it was edited, restore it |
| Token exchange **401**, *"No matching trust policy owned by user"* | The name in `NUGET_USER`, the policy's owner, or the policy's scope | Work the list below — the message is identical for all three |
| **Push unauthorized** | The policy is owned by a different account than the package | Confirm the policy owner on nuget.org |
| **Token expired** | More than an hour between login and push | The two steps are adjacent here; suspect a stalled build |
| `already_exists` | Re-running a completed release | Expected — `--skip-duplicate` makes it a no-op |
| GitHub Release **422** | A release already exists for that tag | Delete the conflicting release, then re-run |
| One package pushed, another **403** | More than one policy, so one exchange reached only one of them | Collapse to a single policy whose patterns cover every id, then `gh run rerun` — `--skip-duplicate` no-ops the one already live |

⚠ **The `403` row is the one worth expecting**, because it is the only failure that leaves your
packages at different versions. It is survivable only because the tag can be replayed: fix the
policy first, since a rerun against an unchanged policy fails identically.

### Working the 401

nuget.org returns the same message whichever field is wrong and never says which, so change one
thing at a time and re-run the preflight, which costs nothing:

1. **Read `NUGET_USER` out loud before anything else.** `gh variable get NUGET_USER`. It is a
   variable precisely so you can: the value is what nuget.org is failing to find, and every other
   step below is guesswork until you have looked at it.
2. **It is the profile name** — not an email, and **not an API key** (see step 1 of the setup).
   It is the account that **created** the policy, which is not always the one that owns it.
3. **The policy owner is the individual account**, not an organization.
4. **The scope allows publishing new packages.**
5. **A single policy's patterns cover every id** in `packages.push`.
6. **The policy shows no pending or inactive warning** in the UI.

⚠ **Order matters, and this list used to start at what is now step 2.** On 2026-09-21 the
`Bennewitz.Ninja.Templates` first release burned six preflight runs working down the policy
fields — Environment, scopes, creator-versus-owner, delete-and-recreate — while the actual fault
was the value itself, unreadable because it was stored as a secret. The policy was correct the
whole time. Look at the value first.
