# Notes

Working documents for maintainers: what is agreed and waiting, what is merely observed, what is
planned, and what is being written up for another project. Everything here is repository-internal.

**Nothing in this folder is published.** The GitHub Pages workflow runs docfx over `docs/` only
(`.github/workflows/github-pages.yml`), so anything meant for the public site belongs in `docs/`,
not here. That is the reason this folder exists at the repository root rather than under `docs/`.

## What is where

| Path | Holds | Lifetime |
|---|---|---|
| [`TODO.md`](TODO.md) | Agreed work, ordered by what to do first | Ongoing |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | API-design observations that are *not* yet agreed | Ongoing |
| [`example-backlog.md`](example-backlog.md) | Every example idea across the repository, with status and provenance | Ongoing |
| [`plans/`](plans) | One document per piece of committed work, settled before building | Retire into the docs when the work lands |
| [`upstream/`](upstream) | Drafts of issues and PRs aimed at Stride, Bepu or elsewhere | Delete once filed |

The distinction between `TODO.md` and `ARCHITECTURE.md` is agreement, not size: an item moves from
`ARCHITECTURE.md` to `TODO.md` once it has been decided that it should be done.

## Naming

Kebab-case, topic first, `.md`. No `PLAN_` or `TODO_` prefix and no `SCREAMING_SNAKE` — the folder
already says what kind of document it is, so `plans/bepu-examples.md` beats `PLAN_Bepu_Examples.md`.

## What does not live here

- Anything user-facing → `docs/`, which is what gets published.
- A README for a project, example or tool → next to that project.
- Runnable measurement rigs and reproductions → next to the code they exercise. The two Bepu
  write-ups in `upstream/` are paired with rigs in `examples/code-only/_Temp2DProbe`, which stay
  where they are so they can be run.
