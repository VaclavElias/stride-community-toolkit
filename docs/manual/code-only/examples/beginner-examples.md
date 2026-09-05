---
generated: true
---

# C# Beginner Examples

One new idea at a time, on top of the base scene. Toolkit helpers only, with no engine extension points to understand first.

## Examples Overview

- [Basic3D Scene (Every Primitive)](primitives-3d.md): Every 3D primitive the toolkit can build - cube, cone, capsule, sphere, cylinder, teapot, torus and triangular prism - dropped into one scene so the shapes, their default sizes and their generated colliders can be compared side by side.
- [Material](material.md): A row of cubes that differ only in their material, so the effect of each property is visible in isolation.
- [Post Effects](post-effects.md): Every post effect Stride ships, one key each: bloom, ambient occlusion, screen-space reflections, depth of field, light streaks, lens flare, fog, outline, FXAA, and the vignette, film-grain and dither colour transforms.
- [Mesh Line](mesh-line.md): A line drawn between two spheres, built as a real mesh rather than a debug primitive.
- [Wav File](wav-file.md): Play a .wav read from disk at runtime, with no compiled asset: LoadWav decodes the file into memory and each CreateInstance is an independent playback.
- [Procedural Sound](procedural-sound.md): A tone with no sound file: a callback computes the samples as they play.
- [Give Me a Cube](give-me-cube-body.md): Add behaviour to an entity with a SyncScript component instead of the update callback of game.Run.
- [SyncScript - moving a body every frame](sync-script.md): A cube driven in a circle by a SyncScript, which is the ordinary way to run code every frame.
- [Entity Text (Screen-Space)](entity-text.md): A gallery of everything EntityTextComponent can do, one feature per pole: anchoring, shadows, backgrounds, scaling, rotation, opacity, distance fading, several texts on one entity, and HUD text pinned to window corners that survives resizing.
- [World Text (In-Scene)](world-text.md): A gallery of everything WorldTextComponent can do, one setting per station: billboarding that stays upright, free billboarding, text fixed in place, text lying flat on the ground, world-unit sizing, depth-tested text hidden behind a wall next to text drawn through it, distance fading, and a glow behind the letters in a few colour combinations - a HUD blue, neon, a readability halo and a soft bloom.
- [2D Panels and Text](2d-scene-panels.md): Sixteen HUD panel recipes side by side, each one property away from the last: fill only, border only, transparent fill over a stripe that proves it, a fill colour of its own, rounded corners, heavy borders, glows of three strengths and colours, glass, a ship-console panel with corner ticks and a gauge, dashed rings and lines that turn and march, a gradient to the text colour, a gradient to nothing, and a panel at a third opacity.
- [Basic2D Scene (Multiple Primitives)](primitives-2d.md): Create a minimal 2D scene using toolkit helpers and place multiple different primitive shapes.
- [Basic2D Scene (Falling Shapes)](falling-shapes-2d.md): Create a minimal 2D scene using toolkit helpers and place multiple capsule primitives with flat materials.
- [Basic2D Scene (Debug Rendering)](debug-render-2d.md): A pile of falling 2D shapes with the physics debug overlays turned on, so what the simulation is actually solving can be seen rather than inferred.
- [DPI-Aware Window](dpi-aware.md): The capsule scene again, with two differences.

> [!NOTE]
> Each example references a handful of toolkit packages. The `using` directives at the top of
> every listing name them, and the linked project file on GitHub is authoritative. A few examples
> also need a third-party package - Box2D.NET, Jitter2, Myra or ImGui - which their page calls out.

[!INCLUDE [basic-examples-outro](../../../includes/manual/examples/basic-examples-outro.md)]
