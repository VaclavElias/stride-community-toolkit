# Release Notes

The changes, new features and fixes in each release of the **Stride Community Toolkit**.

## What to Expect

The toolkit moves faster than the Stride Game Engine, so expect frequent releases, and breaking changes between them while it is in preview.

## 1.0.0.0-preview.70

## What's Changed

### 💥 Breaking Changes

- `GameProfiler`'s keys are chorded with Ctrl: Ctrl+F1 filter, Ctrl+F2 sort, Ctrl+F3 and Ctrl+F4 page, Ctrl+1 to Ctrl+5 jump to a page, Ctrl+minus and Ctrl+plus refresh rate. The plain F2, F3 and F4 belong to the camera help and the text overlay, which the profiler used to fight in every scene with `Profiler = true`.
- `CollidableGizmoScript.Color` is a `Color4`, not a `Color4?`; the fully transparent default keeps the gizmo material's own colour. Game Studio's property grid cannot edit a nullable value type. `AddPhysicsDebug(gizmoColor)` is unchanged.

### 🎉 New Features

- `Capsule2DProceduralModel.StraightLength(height, radius)`: the straight part the mesh draws for a capsule, with its minimum sliver. The Bepu and Bullet 2D capsule colliders take their length from it, so a collider is exactly as tall as its mesh; the Bepu one used to be half the mesh's sliver for a wide capsule under one unit.

### 🐞 Bug Fixes

- `ScreenFade` draws through a batch of its own. It used to register the game's default `ShapeBatch`, so every shape component in a scene with a fade drew after the post effects and without depth. It also takes the batch out of rendering when removed, and `AddScreenFade` before the scene exists says what to do.
- `AddUIStage` and `AddCleanUIStage` add the UI stage once; a second call stacked another stage, UI render feature and renderer.
- `AddOnEventAction` and `AddOnEventTask` handle every event that arrived since the last frame, not one per frame.
- `Basic3DCameraController` locks and hides the cursor when the right-button drag starts and restores the visibility and lock it found when the drag ends, instead of forcing both every frame, so a game's own cursor setting stays.
- Non-billboard `WorldTextComponent` on a scaled entity is no longer skewed or sized by the entity's scale; on an entity with a zero scale axis it stays world-aligned.
- `SystemFonts.LoadFirst` tries the next family when one fails to load.
- `TextureCanvas.Load` and `SetData` discard the filter buffer sized for the old texture; `Store` resolves the format before creating the file, so an unsupported extension leaves no empty file.
- Polygon hole bridging compares its ray and distance tests against a length tolerance, not the area tolerance the cross products use; the two agree only at an extent of one unit.
- `CircleProceduralModel` refuses fewer than three segments with an `ArgumentOutOfRangeException` instead of failing in the mesh draw.
- Bullet: `AddBulletPhysics` with a 2D circle no longer lays the entity flat (the rotation dated from a cylinder model); a wide 2D capsule's collider takes its length from the mesh, where it used to go negative; the heightmap index buffer is sized to its quads rather than padded with degenerate triangles.
- Bepu: `CollidableGizmoScript` removes its gizmo entities when it leaves the scene.
- Box2D: a stall capped by `MaxStepsPerFrame` is dropped, not replayed at up to three times speed; simulation-update, contact and sensor handlers may unregister from inside their callback; a second `Dispose` of a multi-threaded world is allowed; a capsule shorter than two radii is attached as the circle its mesh draws, where it used to get a collider taller than the mesh, or none at all once Box2D.NET refused the length.
- Charts: `Plot` and `PlotParametric` validate `samples` (2 to 8000) instead of throwing from inside a clamp; the constructor rejects a range with no width or height, which `Update` documented but never raised; X and Y axis titles sit at the axes' depth on a 3D chart whose Z range excludes zero.
- DebugShapes: `GenerateCone` with `uvSplits: 0` no longer lifts a rim vertex to the apex; `MaxPrimitives = 0` drops shapes rather than throwing.
- ImGui: the initial vertex and index buffers are `Default` usage, so a frame under 128 vertices with two windows no longer throws on the offset upload; key and mouse-button events carry the event's own state, so a press and release inside one frame reaches ImGui; a non-RGBA font atlas is refused with a message instead of drawing red boxes.
- ImGui `PerfMonitor`: per-thread samples live in a concurrent dictionary; the thread cache is per monitor, so two monitors do not cross-wire; Stop Profiling no longer ends the process through an unobserved cancellation; closing the window cancels the marker loop and removes the auto sampler from the game systems; the caller-info label prints the line number as an integer; the GPU and Stride Systems strips each show one whole frame, held back until the next frame's start event proves it complete, where they used to draw whatever markers had arrived, children before their parents and two GPU frames at once, and flashed; events Stride stamps at the same instant nest the same way every frame; the marker loop follows the profiler's state, so a scene that starts with `Profiler = true` shows the strips at once instead of after pressing the button twice; every sample strip holds a frame for `StripRefreshTime` (500 ms) so it can be read, where redrawing each frame shook the bars, and a frame that trips the large-delta pause shows at once; two samples of one name no longer trip ImGui's conflicting-ID warning; the window can be moved.
- ImGuiNet: `FontPath` and `FontSize` set after `AddImGuiNet()` apply on the next frame; the display-scale subscription and the pipeline state are released on `Destroy`, and the context is destroyed even when initialisation failed part way; the dead legacy modifier writes are gone and the package description says what input it forwards.
- Tools: the examples launcher no longer kills the next example, or logs an error, after Stop; the metadata generator's YAML inspector treats an apostrophe inside a value as text, so an unquoted `#` after a contraction is reported.
- Tests: the overlay key tests build the overlay on a bare registry instead of a `Game` outside the serialised collection; the lock-rotation and run-on-game-thread tests pace on time, not frames.
- `E02_3D_EasingInGame` pops the crystals in staggered, as its comment says; `E04_ImGuiNet` keeps the orbiting kinematic body awake.

### 📄 Docs

- Physics extensions manual: a paragraph on the loading stall, why a scene's first fall differs from a restart and how to hold the simulation until the first short frame.