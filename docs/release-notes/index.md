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

- `GameProfiler` lists its keys on the text overlay behind F1 (`HelpToggleKey`), with the current filter, sort, page and refresh rate; the list's title names the chord just used, for three seconds, since Stride's profiler text has no line of ours to say it on.
- `InputManager.IsKeyPressedUnmodified(key)`: a key pressed with neither Ctrl nor Alt held. The text overlay, its collapsible sections and `KeyMenu` read their keys through it, so the profiler's Ctrl+F2 no longer also collapses the camera help, and Ctrl+1 to 5 do not pick a menu item.
- `Capsule2DProceduralModel.StraightLength(height, radius)`: the straight part the mesh draws for a capsule, with its minimum sliver. The Bepu and Bullet 2D capsule colliders take their length from it, so a collider is exactly as tall as its mesh; the Bepu one used to be half the mesh's sliver for a wide capsule under one unit.
- Every `SceneSetup` adds the `GameProfiler` script, off unless `Profiler = true`, so Ctrl+Shift+P brings the profiler up in any scene. `AddProfiler(enabled)` takes the starting state, and on a root scene that already has the script it turns it on instead of adding a second.
- `Basic3DFollowCameraController` and `Basic3DOrbitCameraController` have `ShowHelp`, as the 2D and 3D controllers do.
- `InputManager.IsShiftDown()`, `IsCtrlDown()` and `IsAltDown()` in `Stride.CommunityToolkit.Input`; the camera controllers and the profiler read their modifiers through them. `IsGamePadButtonPressed(button, index)` completes the gamepad button family.
- `RenderStageNames`: the `Opaque`, `Transparent` and `Ui` stage names the toolkit's compositors create, where the helpers re-spelled the strings.
- `SpriteTextRendererBase`: the frame `EntityTextRenderer`, `WorldTextRenderer` and `EntityInfoRenderer` share, the sprite batch, the default font, the background texture, the display scale and the per-frame camera and viewport checks. The three derive from it, and `EntityInfoRenderer` now loads its default font through the same fallback as the other two, so it draws in Game Studio's viewport.
- `TextureCanvas` and `CreateTextureCanvas` default `size` to 1024 by 1024, so it can be left out.
- `MeshBuilder` 16-bit indices take the whole unsigned range the GPU reads, 0 to 65,535; the limit was 32,767.
- The four camera controllers and `GameProfiler` carry `[DataContract]`, so Game Studio lists them; the follow controller's `ObstacleTest` delegate is `[DataMemberIgnore]`.

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
- `E02_3D_EasingInGame` pops the crystals in staggered, as its comment says; `E04_ImGuiNet` keeps the orbiting kinematic body awake; `E02_2D_EasingInGame`'s Space collects a coin that is still popping in, which used to need a second press.
- `Delay`, `DelayWarped`, `ExecuteInTime`, `ExecuteInWarpedTime` and `WaitFor` throw at the call for a null script or action and a non-positive delay, instead of returning a faulted task; `ExecuteInTime` with a null action is refused rather than becoming a delay.
- `SetMaxFPS` and `SetFocusLostFPS` throw `ArgumentException` for a game that is not a `GameBase`, as `Exit` does, instead of `InvalidCastException`.
- `Add3DCamera`, `AddRootRenderFeature` and `AddParticleRenderer` throw `InvalidOperationException` when the game has no compositor, like the other compositor helpers, instead of a null reference; `AddGroundGizmo(showAxisName: true)` does too, where it used to leave the axis letters out silently.
- The compositor, entity-info, render-texture (a zero size), texture-loader, system-font, camera-controller and gizmo helpers validate their arguments first, so a null fails at the call with its name; `TextOverlay.AddCollapsibleSection` refuses an empty title, which left the toggle key doing nothing; `MeshBuilder.AddIndex` on a builder without indices says so instead of "between 0 and -1"; `TextMesh` names `options` as the parameter in every range exception and the property in the message.
- The camera controllers read the frame's `MouseDelta` for their drags instead of tracking the last cursor position, so the first frame of a drag counts; the follow controller ignores the wheel while the middle button orbits, as the other two do.
- Per-frame work cut: `TryWorldToScreenPoint` transforms the position once, not three times; `TextureCanvas.ToTexture` copies on the GPU instead of reading the pixels back and uploading them; `ListExtensions.EnsureSize` and `SetCount` resize in one step; the text overlay builds no key string per line and measures each prefix once, its font resolver compares a struct key, `KeyNames` caches, `EntityInfoRenderer` builds its style once a frame and `PcmSoundSource` reuses its reclaim list.
- `TextureExtensions.FindAssetSourceDir` returns an empty string when the executing assembly has no solution folder above it, where it used to search the working directory; `PolygonProceduralModel.New` validates the outline once, on the cache miss.
- `RandomExtensions.NextSingle(Random)` is gone: `Random.NextSingle()` is the BCL's since .NET 6 and every call already bound to it.
- Housekeeping: `GameExtensions` is split into `Run`, `Cameras`, `Lights` and `Materials` partials beside the existing two; `Camera2DMath` is `CameraMath` with the controllers' shared zoom step; the editor's axis colours, the instancing growth rule, the display-scale threshold and the bevel halving limit are each one constant; `SkyboxGenerator` spells Lambert right.

### 📄 Docs

- Physics extensions manual: a paragraph on the loading stall, why a scene's first fall differs from a restart and how to hold the simulation until the first short frame.