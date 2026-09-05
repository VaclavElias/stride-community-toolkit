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
| [`engine-example-opportunities.md`](engine-example-opportunities.md), [`samples-example-opportunities.md`](samples-example-opportunities.md), [`starbreach-example-opportunities.md`](starbreach-example-opportunities.md) | Research: example and toolkit opportunities mined from the Stride sources, the bundled samples, and the Starbreach demo (all cross-checked 2026-09-02) | Graduate items into `example-backlog.md` / `TODO.md`; keep the docs as the rejected-and-why record |
| [`box2d-example-opportunities.md`](box2d-example-opportunities.md), [`bepu-demos-opportunities.md`](bepu-demos-opportunities.md) | Research: the same harvest over the Box2D.NET samples and the bepuphysics2 demos, docs and harness (2026-09-05, agent-produced, line numbers unverified) | Verify the Box2D items against the pinned NuGet before porting; graduate into `plans/box2d-library.md` and `plans/bepu-examples.md` |

## Reading order for the research docs

About 2,400 lines across the three documents. The order below front-loads the parts that change
decisions and leaves the reference tables for lookup. Read for *what is possible* first; decide
what to build afterwards, against `example-backlog.md`.

**Part 1 — the example opportunities (read in full, in this order)**

1. `engine-example-opportunities.md` — the header, **"What the cross-check changed"** and
   **"Facts established"** (≈ 140 lines). The mental model of what Stride 4.4 can and cannot do
   code-only, and the eleven things the first version got wrong. Everything else assumes these.
2. `engine-example-opportunities.md` — **"Toolkit-side findings"** (≈ 40 lines). Short, and two
   items are bugs in shipped toolkit code (`AddCleanUIStage`, `GetHeightAt`) that matter whether
   or not any example is built.
3. `engine-example-opportunities.md` — **Full specs 1–37** (≈ 770 lines, the core of the whole
   exercise). They are ordered by category gap × payoff, so sequential reading is the right
   reading. Specs 1–25 are the original set with corrections applied in place; 26–37 are new and
   marked "(new)". Each spec ends with a verdict and a toolkit piece; note the ones that pull you.
4. `samples-example-opportunities.md` — **§1.4 Facts**, **§2 Candidates (#60–#77)** and
   **§3 Patterns** (≈ 300 lines). This is the gameplay-shaped complement to the engine doc's
   rendering-heavy specs: character controllers, cameras, pathfinding, streaming, game-state
   flow, blend trees — and the "good vs wart" list from Stride's own templates. Skip §1.2/§1.3
   (per-template inventory tables) and §4 (worked-usage table) on the first pass; they are lookup
   material for whoever builds an example.
5. `starbreach-example-opportunities.md` — the **candidates** and **patterns** sections, if not
   already read. Same shape as the samples doc; overlaps it on third-person cameras and event
   buses (the two docs cross-reference each other where they claim the same backlog row).
6. `engine-example-opportunities.md` — **"Engine re-sweep additions" (compact specs 80–93)**
   (≈ 120 lines). Rendering-heavy and spike-ish (temporal AA and subsurface scattering exist but
   are wired to nothing); read after the main specs so they land as depth, not as competition.

**Part 2 — reference material (skim once, return by lookup)**

7. `engine-example-opportunities.md` — the four **Inventory** tables. ~130 one-line rows grouped
   by area; skim the "Verdict · Level · Category" column for categories you care about (the
   toolkit's empty ones are Interaction, Audio, Gameplay, Integration).
8. `engine-example-opportunities.md` — **"Considered and rejected"**, and the rejected tables at
   the end of the other two docs. Worth one pass so you know what *not* to go looking for; then
   only consult when an idea comes up.

**Part 3 — everything that is not an example (in this order)**

9. `engine-example-opportunities.md` — **"Upstream findings"** (23 items). Decide which become
   `notes/upstream/` drafts. The first six are the ones with real user impact (shader compilation
   mode, `Channel<T>`, navigation enable flag, HRTF docs, `LightProbeRenderer`, `Material.New`).
10. `engine-example-opportunities.md` — **"Toolkit infrastructure"**. The headless/CI
    recommendation for screenshot-verifying the ~57 examples; it is a plan-doc decision, not an
    example decision.
11. The `engine-patterns.md` correction (transparency mechanism, item 6 of "What the cross-check
    changed"). Reproduce the original failure before rewording the manual page.
12. `engine-example-opportunities.md` — **"Coverage impact"** — the suggested first five, to
    compare against your own list after all of the above.

**Part 4 — the physics libraries (2026-09-05, read after Part 1)**

13. `bepu-demos-opportunities.md` — **Top 10** table first, then **§3 Documentation** (the
    stability ladder and CCD pages are manual material as much as example material), then §1/§2.
    Every "Access" line says whether a Stride component covers it or raw Bepu is needed.
14. `box2d-example-opportunities.md` — **Top 10** table, then gem 12 (the joint façade) since it
    blocks most of the rest, then gems 3, 15 and 21, which are pure API with no example needed.

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
