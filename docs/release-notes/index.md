# Release Notes

Welcome to the Release Notes for the **Stride Community Toolkit**. This section aims to provide you with an organized, high-level summary of changes, enhancements, and fixes made in each version release. If you're looking to understand what has changed from one version to the next, you're in the right place.

## What to Expect

The Stride Community Toolkit is developed with rapid iteration in mind. It moves at a faster development pace compared to the Stride Game Engine. As a result, you should expect frequent updates that may introduce breaking changes. This fast-paced approach allows us to incorporate community feedback quickly and continue improving the toolkit.


## 1.0.0.0-preview.64

## What's Changed

### 💥 Breaking Changes

- The text renderers moved next to their components: `WorldTextRenderer`, `EntityTextRenderer`, `ScreenTextDrawer` and `ScreenTextStyle` are now in `Stride.CommunityToolkit.Rendering.Text` instead of `Stride.CommunityToolkit.Renderers`. Change the `using`; `Renderers` keeps `EntityDebugSceneRenderer`.
- `ShapeComponent.Vertices` is a `List<Vector2>` instead of an array, so Game Studio's property grid can edit it and the asset serializer can load it. Assigning an existing array needs a spread: `Vertices = [.. outline]`.
- `WorldTextComponent` and `EntityTextComponent`: `FadeStartDistance` and `MaxDistance` are `float` with 0 meaning off, instead of `float?`, which Game Studio could not edit.
- Box2D: the abstract base of the joint option records is `JointOptionsBase` (was `JointOptions2D`); the joint sources moved to the package root with no namespace change. `CharacterMover2D`'s category bits are `static readonly` instead of `const`; source compatible, recompile against them.
- `ShapeBatchExtensions.EffectName` is gone; the shape effect is fixed. `ShapeBatch` uploads through a new wire format - a record buffer plus a point buffer - and composites premultiplied in linear light on an sRGB target, so every shape colour now matches its sRGB value and scenes tuned against the old, darker output look brighter. Border colour alpha applies, and the glow's alpha is its strength.
- The `Display` names of `ShapeComponent`, `WorldTextComponent` and `EntityTextComponent` lost their "(call Add…)" suffixes: the calls are no longer required.

### 🎉 New Features

- **Space polylines**: `ShapeBatch.DrawPolyline` and `DrawPixelPolyline` take `ReadOnlySpan<Vector3>` - a run of 3D points stroked on screen with round joins, in a world width that narrows with distance or a constant pixel width, with glow, dashes and opacity, and per-fragment depth so a stroke threads behind and in front of geometry. Pieces of 64 points.
- **Soft depth fade**: `ShapeBatch.DepthFade` fades a shape out over a distance in world units as it approaches scene geometry, using the depth the forward renderer resolves before the transparent stage - a marker sunk in the floor melts into it, a ring in front of a wall dims over the wall. 0 keeps the hard depth cut. `ShapeStyle` gained the `DepthFade` parameter.
- **Textured batch**: `ShapeBatch.FillSource` takes any of Stride's material `IComputeColor` nodes - a texture with scale, offset and address modes, a blend, a custom shader class - and every shape drawn with `Textured` on multiplies its fill by the sample, over the shape's bounding box with the top-left origin; `FillWith(texture)` installs the common case and `game.AddShapeBatch(fill: ...)` creates a batch with one. One source per batch, its own effect; a plain batch keeps its shader. New `ShapeEffect` and `ShapeShaderTextured` in the Shapes package.
- **Components in Game Studio**: `ShapeComponent`, `WorldTextComponent` and `EntityTextComponent` draw in the scene editor's viewport, before Play. Their processors run in the editor and register the render feature or scene renderer themselves, on the compositor of the scene they belong to, and again whenever the editor swaps compositors. No `AddShapeBatch`, `AddWorldTextRenderer` or `AddEntityTextRenderer` call is required for a component to draw; the calls remain for control - the batch handle, the depth-test choice, the renderer's place in the chain.
- `GrabberScript` and `Grabber2DScript` appear in Game Studio under Physics: the Bepu and Box2D packages now register their assemblies for scanning. `game.AddGrabber()` puts the Bepu grabber on the camera entity from code.
- `Box2DBodyComponent` can be added in Game Studio without throwing; the body is still created from code.
- New example **Compute Boids** (`E10_3D_ComputeBoids`): a flock on the GPU - a compute shader steers thousands of boids and writes the instance matrices an instanced mesh draws from.

### 🐞 Bug Fixes

- `UseGameSettings` can now raise the graphics profile of a code-only game: the engine's `PrepareContext` wrote its level-10 default over the device manager after the toolkit had applied the caller's settings; they are applied again from `WindowCreated`, before the device is created.
- ImGui, ImGui.NET and DebugShapes decoded their colours for a gamma target on Stride's sRGB backbuffer; they now decode to linear like ShapeBatch. DebugShapes' primitive shader read its colour as float bits, which turned some colours into NaN.
- Text and debug renderers no longer throw when the compositor has no camera slot, and text renderers fall back to a system font when the built-in font is not in the content database - both the case in Game Studio's scene editor.
- `E04_ImGuiNet`'s overlay margins follow the display scale.

### 🎨 Rendering

- ShapeBatch: the pixel stage reads each shape's record from a structured buffer instead of fifteen interpolated registers, points live in their own buffer so a shape has as many as it needs, streams are integer-exact, the border is a flat band with an anti-aliasing ramp measured from screen derivatives, and a 4x4 Bayer dither hides banding in glows.
- `ShapeGlow.Additive`: a glow that adds light rather than covering what is behind it.
- The render feature gathers every batch of the frame into shared buffers once, draws each batch under a lock, and flushes after the last view; batches can be removed with `RemoveShapeBatch`.

### ✨ Enhancement

- `ShapeBatchExtensions.AddShapeBatch` registers the batch synchronously, before the first frame, instead of one frame later.
- `SceneRendererRegistration` and `CompositorCameras` in `Rendering.Compositing`, for any component library that wants to work in Game Studio the way the toolkit's do.
- Every toolkit script has a display name and a category in the Add-component list: the camera controllers under Camera, the profiler under Debug, the Bepu gizmo and debug scripts under Physics.

### 📄 Docs

- [Using toolkit components in Game Studio](../manual/game-studio.md) rewritten around components that draw by themselves; [Making components work in Game Studio](../contributing/toolkit/game-studio-components.md) is the recipe for library authors, with the editor traps named.
- ShapeBatch manual: colour space and blend state, glow alpha, space strokes, limits.
- API docs now include the Box2D package.

### 🎓 Examples

- **2D Panels** grew to twenty-four stations; the **HUD** and the **SignalR** deck use strokes and additive glows; **ShapeBatch** shows a glowing helix and a trefoil knot as space strokes, three dashed rings turning at their own gap ratios, and a numbered label on every demo that L widens to name the method it is made of.
- Five Bepu examples use `game.AddGrabber()`.

### 🔧 Engineering

- Gold-image regression: `build/gold-images.cs` photographs five purpose-built scenes in `tests/Stride.CommunityToolkit.GoldScenes` - 2D shapes, 3D shapes, text, DebugShapes, ImGui - on the WARP software renderer and compares them with `tests/gold` under Stride's per-pixel rule; reproducible frame for frame across machines. The new `gold-images.yml` workflow runs it on pull requests that touch a renderer and uploads the contact sheet.
- NDepend at zero issues across the solution.

### 💪 Other Changes

- Examples' manifest and doc pages regenerated.