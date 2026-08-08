# Contribute Code

If you are a developer and want to help build the Stride Community Toolkit, then you can do so in various ways.

## Check our issue tracker

If you're just getting started, issues marked as ['good first issue'](https://github.com/stride3d/stride-community-toolkit/labels/good%20first%20issue) are a great entry point. You can also browse our full [issue tracker](https://github.com/stride3d/stride-community-toolkit/issues).

## Coordinate work

Before you start working on an issue:
- Comment on the relevant issue (or create one) to signal interest.
- Check on GitHub or Discord if you need direction or context.
- Ensure no one else is actively working on the same item.
- Outline your approach and gather feedback to ensure the design fits the project.

## Building the toolkit

See [Building the Toolkit](building.md) for how the solutions and solution filters are laid out, why
the examples build is configured the way it is, how to debug a running example, and how to produce
local NuGet packages for testing.

## Coding style

Use Stride's `.editorconfig` when making changes.

The full list of repository conventions - naming, nullability, XML documentation, terminology,
performance and threading rules, and the pattern to follow when adding an example - lives in
[`.github/copilot-instructions.md`](https://github.com/stride3d/stride-community-toolkit/blob/main/.github/copilot-instructions.md).
It is written for AI assistants but applies equally to people, and it is deliberately the single
source of truth so the two do not drift apart. Keep it up to date when conventions change.

## Engine behaviour worth knowing

Bepu owns the transform of any entity with a body attached, and the failures that follow from
misunderstanding that are all silent. See
[Bepu: Who Owns the Transform?](../../manual/physics-extensions/bepu-transform-ownership.md).

## Submitting changes

- Push changes to a topic branch in your fork.
- Open a pull request from that branch to the official repository.

## Contributing to Stride Engine

Interested in contributing to the Stride Engine? See the [Stride Engine Contribution](https://doc.stride3d.net/latest/en/contributors/index.html) page.
