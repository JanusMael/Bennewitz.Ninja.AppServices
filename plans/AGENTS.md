# AGENTS.md — `plans/`

Numbered plans: `NNNNN-slug.md`, zero-padded to five digits, sequential. List the directory before
taking a number; two sessions picking "the next one" is how a number gets used twice.

## Lifecycle

| Status line | What may happen to the file |
|---|---|
| `draft, awaiting approval` | Edited in place, as many rounds as it takes |
| `approved YYYY-MM-DD` | **Nothing, ever.** Committed on its own, before any implementation |

## Rules

| Rule | Why |
|---|---|
| An approved plan is never edited: not a typo, not a step that turned out differently | It is the record of what was agreed. Edited, it becomes a description of what was built |
| What actually happened goes in `PROGRESS.md`, under the plan's drift section | The plan stays the promise; the work state says how the promise was kept |
| A materially different approach is a new number whose status line says `Supersedes NNNNN` | The old plan stays as it was approved |
| A link inside an approved plan that stops resolving is left alone | Its links are specification. A dead one records drift, which a fix would hide |
