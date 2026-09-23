# Bennewitz.Ninja.AppServices

TODO: one paragraph saying what this package does, in the words someone searching nuget.org would
use. This file is packed into the package, so it is also the description shown on nuget.org.

## Install

```bash
dotnet add package Bennewitz.Ninja.AppServices
```

## Releasing

See [docs/publishing.md](docs/publishing.md). The short version:

1. Add the `NUGET_USER` secret — your nuget.org **profile name**, not an email.
2. Create **one** trusted-publishing policy whose glob patterns cover every id in
   [`packages.push`](packages.push) and match nothing in [`packages.local`](packages.local).
3. Run **Release** → *Run workflow* with the version **blank**. That logs in and stops, proving the
   credentials without publishing.
4. Tag `vYYYY.Q.MMDD` and push.

⛔ **One policy, never one per package id.** nuget.org mints one API key per token exchange, scoped
to one matching policy — so a second policy is never consulted and its package is rejected `403`
after the first has already published permanently.

## Licence

MIT. See [LICENSE](LICENSE).
