# Engine Example Opportunities

A research sweep of the Stride sources (`D:\Projects\GitHub\stride\sources`, Stride 4.4, master as of
2026-08-29) looking for the next wave of [engine patterns](../docs/manual/engine-patterns.md):
public-but-undocumented capabilities, and internal-but-copyable (MIT) code, that could become
toolkit examples, toolkit helpers, or both. Six areas were surveyed in parallel — Graphics+Games,
Rendering, Engine+Input+UI, the remaining engine subsystems (Audio, Particles, Navigation, Video,
VR), the editor sources, and core+tools — each briefed with what
[engine-patterns.md](../docs/manual/engine-patterns.md) and the
[example backlog](example-backlog.md) had already mined, so everything below is *new* relative to
those.

**Status: research, nothing agreed.** The intended flow is: pick items from here → add a row to
[example-backlog.md](example-backlog.md) (Source: "in-engine, this doc") or an entry to
[TODO.md](TODO.md) for toolkit work → build. Example numbers below are provisional: they start at
27 because 24–26 are claimed by the [Bepu plan](plans/bepu-examples.md), and family names
(`Example27_Audio_*`) follow the existing convention of one number per topic family.

Verdicts: **example** (teach it), **toolkit** (wrap or port it into a toolkit library), **both**.
Levels and categories follow [examples/code-only/README.md](../examples/code-only/README.md).
Every claim below was verified by an agent reading the actual source (public vs internal checked);
line numbers are as of the surveyed commit and will drift.

## Facts established by the sweep

Worth recording even where no example follows — several are "stop looking for it" answers:

- **The gizmo contract lives in the engine, not the editor.** `IGizmo`, `IEntityGizmo`,
  `GizmoComponentAttribute` are in `engine/Stride.Engine/Engine/Gizmos/`, and every shader the
  editor's picking/wireframe/highlight machinery needs (`PickingShader.sdsl`,
  `HighlightShader.sdsl`, `MaterialFrontBackBlendShader.sdsl`, `CameraOrientationGizmoShader.sdsl`)
  is engine-side. Most editor techniques port into a game without touching editor assemblies.
- **Every post effect ships disabled.** `GraphicsCompositorHelper.CreateDefault` builds
  `PostProcessingEffects`, calls `DisableAll()` and re-enables only the tonemap. Bloom, DoF, SSAO,
  SSR, lens flare, FXAA, fog, outline, vignette, film grain are all sitting there behind
  `Enabled = true`.
- **Stride 4.4 has no**: spline components (grep-verified; the community `Stride.Splines` package
  is external), model LOD, UI data binding, GPU particles, occlusion/pipeline-statistics GPU
  queries (`QueryType` has exactly one member, `Timestamp`), decals, deferred rendering, audio
  capture (`Microphone` is internal and throws `NotImplementedException`).
- **HRTF is a no-op on desktop.** The `useHrtf`/`HrtfEnvironment` parameters are threaded all the
  way to `OpenAL.cpp` and ignored there; only the old UWP XAudio2 path used them. Don't teach it.
- **Runtime audio is possible after all** — not via `Sound` (Celt/ffmpeg pipeline, internals), but
  via a public `DynamicSoundSource` subclass plus the public `SoundInstance` constructor. See
  the Audio specs.
- **Navigation went fully managed.** 4.4 replaced native Recast with the DotRecast NuGet packages;
  `NavigationMeshBuilder` is public, no native dependency, works code-only. Also:
  `DynamicNavigationMeshSystem` is registered by *nothing* in the engine — a game must add it
  itself, which nobody documents.
- **Light probes CAN be baked at runtime.** `LightProbeGenerator.GenerateCoefficients(game)` in
  `Stride.Engine` renders and prefilters probe cubemaps from game code (`Game` implements
  `ISceneRendererContext`). One survey initially rejected light probes as editor-only; this
  finding supersedes that.
- **The toolkit already owns runtime skybox IBL.** `Stride.CommunityToolkit.Skyboxes` is a port of
  the engine's `SkyboxGenerator` (SH + GGX prefiltering). The un-mined remainder is *live-scene*
  cubemap capture (`CubemapSceneRenderer.GenerateCubemap`) — a runtime reflection probe.
- **`Stride.Games.AutoTesting` is new in 4.4** and is a screenshot-regression harness (simulated
  input, software rendering, LPIPS image comparison) that looks purpose-built for CI-verifying
  the toolkit's ~57 examples. See Infrastructure below.

---

## Full specs — the strongest candidates

Ordered roughly by (category gap × payoff ÷ effort). The current
[coverage snapshot](example-backlog.md#coverage-snapshot) has **zero** examples in Input,
Interaction, Audio, Gameplay, Performance and Integration — specs are grouped to attack those
first.

### 1. `Example27_Audio_ProceduralSound` — a sound with no sound file

- **Level:** Beginners (with helper) · **Category:** Audio · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Audio/DynamicSoundSource.cs`, `SoundInstance.cs:43-60` (the public
  constructor), `Native/AudioLayer.cs`; copyable sine generator in
  `engine/Stride.Audio.Tests/SoundGenerator.cs` (MIT, ~50 lines).
- **What it shows:** subclass `DynamicSoundSource`, fill PCM buffers in `ExtractAndFillData()` via
  `FillBuffer(...)`, construct `SoundInstance(engine, listener, source, 44100, mono: true, ...)`.
  The centrepiece is the circular-constructor trick: pass `null` to the base ctor, assign the
  protected `soundInstance` field afterwards, add to `NewSources` last (safe because the base ctor
  only stores the reference). Play a synthesized tone; change pitch/waveform live.
- **Toolkit piece:** a new `Stride.CommunityToolkit.Audio` library with `ProceduralSoundSource`
  taking a fill callback, hiding the ctor dance. Must null-check `game.Audio.AudioEngine`
  (silently null when OpenAL is missing — `AudioSystem.Initialize` swallows the exception).
- **Why it matters:** Audio has zero examples, this is the *only* way to get audio into a
  code-only Stride game, and it is completely undocumented.

### 2. `Example27_Audio_WavFile` — play a .wav from disk, no compiled asset

- **Level:** Beginners · **Category:** Audio · **Complexity:** 3 · **Verdict:** both
- **Sources:** as above, contrasted against `engine/Stride.Assets/Media/SoundAssetCompiler.cs`
  (the ffmpeg/Celt pipeline this sidesteps).
- **What it shows:** a ~80-line `WavSoundSource : DynamicSoundSource` parsing a RIFF/WAVE header
  (fmt + data, 16-bit PCM) from a `FileStream` and streaming it. This is the toolkit's founding
  pattern — "load files at runtime instead of the asset pipeline" — applied to the one subsystem
  where it currently has no answer.
- **Toolkit piece:** `game.LoadSound(path)` in the same Audio library.

### 3. `Example27_Audio_Spatial` — 3D positional audio, honestly

- **Level:** Intermediate · **Category:** Audio · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Audio/AudioEmitter.cs`, `AudioListener.cs`,
  `engine/Stride.Engine/Audio/AudioListenerProcessor.cs`, `Native/OpenAL.cpp:613-681`.
- **What it shows:** `SoundInstance(..., spatialized: true)` + `Apply3D(AudioEmitter)` as a sound
  orbits the camera. The teaching core is the gotcha chain: `AudioListenerComponent.Listener` is
  internal, so runtime sounds must use `AudioEngine.DefaultListener` — which nothing ever moves —
  so the fix is transforming the emitter's world position into camera space each frame (a nice
  coordinate-space lesson). Spatialization requires mono; `Pan` and 3D are mutually exclusive.
- **Toolkit piece:** a `SpatialSoundEmitter` script component doing the listener-space transform.
- **Caveat to document, not demo:** HRTF is a desktop no-op (see facts above).

### 4. `Example28_Input_VirtualButtons` — rebindable actions, chords and synthetic axes

- **Level:** Beginners · **Category:** Input · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Input/VirtualButton/` (`VirtualButton.cs` + `.Keyboard/.Mouse/
  .GamePad`, `VirtualButtonBinding.cs`, `VirtualButtonConfig(Set).cs`, `VirtualButtonGroup.cs`,
  `VirtualButtonTwoWay.cs`); consumed at `InputManager.cs:495-565`.
- **What it shows:** Stride ships a full action-mapping layer nobody uses. `"Jump"` bound to
  Space; `VirtualButtonTwoWay(A, D)` producing the *same* analog float as a gamepad stick;
  `VirtualButtonGroup` chords (Ctrl+S); a runtime rebind screen. The config-set index is the
  player number — local multiplayer for free.
- **Toolkit piece:** an `InputActions` wrapper; the toolkit camera controllers could be
  re-expressed on top of it (they currently hand-roll `Input.IsKeyDown` and gamepad state — see
  also the gamepad-helpers row already in the [backlog](example-backlog.md)).

### 5. `Example28_Input_Gestures` — tap, drag, flick, long-press (with a mouse)

- **Level:** Intermediate · **Category:** Input · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Input/Gestures/` (configs, events, recognizers); hookup at
  `InputManager.cs:87-103,154`.
- **What it shows:** add a `GestureConfig` to `Input.Gestures`, read `Input.GestureEvents`. The
  key verified fact: `MouseDeviceState` emits pointer events with `Id = 0`, so tap/drag/flick/
  long-press **work on desktop with a mouse** — only the two-finger composite (pinch/rotate)
  needs touch. Demo: drag to pan, flick to throw, long-press to delete, on the existing 2D scene.
  Note configs freeze once added, and coordinates are normalised to `[0,1] × [0,1/aspect]`.

### 6. `Example29_PickingNoPhysics` — raycasts without a physics engine

- **Level:** Intermediate (Beginners variant possible) · **Category:** Interaction ·
  **Complexity:** 5 · **Verdict:** both
- **Sources:** `core/Stride.Core.Mathematics/CollisionHelper.cs` (~1580 lines, ~45 public static
  methods), `Ray.cs`, `BoundingBox/Sphere.cs`, `IIntersectableWithRay.cs`.
- **What it shows:** every toolkit picking example today requires Bepu. `CollisionHelper` has the
  whole classical matrix — `RayIntersectsTriangle` (Möller–Trumbore, with barycentric point
  overload), `RayIntersectsBox/Sphere/Plane`, `RayIntersectsRectangle` (oriented quad — click a
  world-space panel), closest-point and distance families, and a generic
  `GetNearestHit<T>(objects, ref ray, out hit, out distance, out point)`. Demo: hover-highlight
  over bounding boxes, then refine to exact triangles read from the mesh.
- **Toolkit piece:** a `RayPicking` helper in `Stride.CommunityToolkit` pairing the existing
  `ScreenToWorldRay` camera extensions with `GetNearestHit` over entity bounds; companion to the
  existing `RaySegment` type.
- **Cross-link:** frame as the deliberate opposite of `Example14_Raycast`.

### 7. `Example30_TransformGizmos` — finish the gizmo family, interactively

- **Level:** Advanced · **Category:** Interaction · **Complexity:** 9 · **Verdict:** toolkit
  first, then example
- **Sources:** `editor/Stride.Assets.Presentation/AssetEditors/Gizmos/TransformationGizmo.cs`
  (503 lines — the drag machinery), `RotationGizmo.cs` (254), `ScaleGizmo.cs` (325),
  `EditorGameEntityTransformService.cs` (orchestration + snapping).
- **What it shows:** the toolkit's ported `TranslationGizmo` is display-only. The editor base
  class holds everything people get wrong: screen-constant sizing
  (`SizeFactor · (defaultSize/backBufferHeight) · 2·tan(fov/2) · distance`, rows renormalised so
  non-uniform parent scale survives), drag-plane construction per axis mode, an 8-pixel drag
  threshold, a 2.5° grazing-ray guard, and *absolute* deltas (returning the mouse to its origin
  restores the transform exactly). The rotation gizmo hit-tests its torus as 20 oriented boxes
  around the circle; the scale gizmo maps drag distance through `exp(t)` so scale can never go
  negative. Snapping everywhere is just `MathUtil.Snap` — there is no snapping subsystem.
- **Toolkit piece:** extend the ported gizmos into an interactive T/R/S set; the editor's one-line
  mouse arbitration (`IsMouseAvailable => services.All(x => x == this || !x.IsControllingMouse)`)
  should come along as an `InputArbiter` helper — it retrofits into Example07/08/14 and both
  camera controllers, all of which currently fight over the mouse ad hoc.

### 8. `Example31_SaveGame` — save/load with the engine's own machinery

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 5 · **Verdict:** both
- **Sources:** `core/Stride.Core.IO/VirtualFileSystem.cs` (+ `ZipFileSystemProvider.cs`,
  `DirectoryWatcher.cs`), `core/Stride.Core.Serialization/IO/DictionaryStore.cs` / `Store.cs`.
- **What it shows:** `/roaming`, `/local`, `/cache` mount points answer "where does my save file
  go on Windows/Linux/Android"; `DictionaryStore<K,V>` over a VFS stream gives a transactional,
  append-only save store where any `[DataContract]` type (including `Vector3`, `Quaternion`)
  round-trips through Stride's own binary serializer. Second act: mount a folder or .zip as
  `/mods` and hot-reload with `DirectoryWatcher`. Side-notes: `SerializerExtensions.Clone<T>` as
  a one-line deep clone; `ObjectId.FromObject` as a cheap content hash.
- **Overlap:** `Example07_CubeClicker` saves clicks with its own code — cross-link, don't merge.

### 9. `Example32_ProceduralAnimation` — an AnimationClip built in code

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Animations/AnimationClip.cs`, `AnimationCurve.cs`,
  `CompressedTimeSpan.cs`; path resolution via `Updater/UpdateEngine.cs`
  (see `AnimationUpdater.cs:51`); real property paths in
  `Stride.Assets.Models/ImportModelCommand.Animation.cs:83,90,253`.
- **What it shows:** the animation system needs no FBX. Build `AnimationCurve<Vector3>` keyframes
  with `CompressedTimeSpan`, `clip.AddCurve("[TransformComponent.Key].Position", curve)`, add to
  `AnimationComponent.Animations`, `Play`/`Crossfade`, `await animComponent.Ended(playing)`. The
  property-path strings (`"[TransformComponent.Key].Position"`,
  `"[ModelComponent.Key].Skeleton.NodeTransformations[3].Transform.Position"` — you can even
  keyframe `.Model` itself) are the undocumented core. No animation example exists anywhere in
  the toolkit today.
- **Toolkit piece:** an `AnimationClipBuilder` hiding `CompressedTimeSpan` and the path strings.

### 10. `Example33_SplinePath` — Catmull-Rom waypoint paths

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 4 · **Verdict:** both
- **Sources:** `core/Stride.Core.Mathematics/Vector3.cs:756-821` (`CatmullRom`, `Hermite`),
  `Quaternion.cs:1183-1230` (`Slerp`, `Squad`), `MathUtil.cs` (`ExpDecay`, `SmootherStep`).
- **What it shows:** Stride has no spline *component* (verified), but the spline *math* is all
  there. Move an entity through six waypoints, orient along the tangent (the toolkit's
  `MathUtilEx.LookRotation` closes the loop), draw the curve with the MeshLine technique.
  Deserves a sidebar: `MathUtil.ExpDecay` as the correct, framerate-independent replacement for
  the `Lerp(a, b, 0.1f)`-per-frame bug everyone writes.
- **Toolkit piece:** `CatmullRomPath` / `SplineFollower` (arc-length sampling) in
  `Stride.CommunityToolkit.Mathematics`.

### 11. `Example34_NavigationPathfinding` — a navmesh with zero native code

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 6 · **Verdict:** both
- **Sources:** `engine/Stride.Navigation/NavigationMeshBuilder.cs`, `NavigationComponent.cs`,
  `NavigationQuerySettings.cs`; `DynamicNavigationMeshSystem.cs` for the auto-rebuild variant.
- **What it shows:** 4.4's navigation is pure managed DotRecast. The simple route:
  `NavigationMeshBuilder.Add(new StaticColliderData { Component = collider })` per (Bullet)
  collider, `Build(...)`, assign the result to `NavigationComponent.NavigationMesh` + `GroupId`,
  then `TryFindPath(start, end, waypoints)` and walk it — path drawn with DebugShapes over
  procedurally placed obstacles. The advanced variant registers `DynamicNavigationMeshSystem`
  (which *nothing in the engine registers* — a one-line toolkit helper and a documentation coup)
  and rebuilds as obstacles move. Requires at least one `NavigationBoundingBoxComponent` or the
  rebuild silently returns empty.
- **Cross-link:** distinct from the backlog's `Stride.BepuPhysics.Navigation` row (Bepu path);
  note both in whichever ships second.

### 12. `Example35_CodeOnlyPrefabs` — clone a template entity

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 3 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Engine/Design/EntityCloner.cs`, `Prefab.cs`,
  `EntityExtensions.cs:19` (`entity.Clone()`).
- **What it shows:** build one entity in code, `Clone()` it a hundred times — a deep,
  serializer-based clone that duplicates children and components while *sharing* models and
  materials; `CloneContext.MappedObjects` substitutes references during cloning. Teach the
  contrast explicitly: clone = many entities, instancing (Example21) = one draw call. Caveat:
  non-serializable script state resets.

### 13. `Example36_EventBus` — EventKey/EventReceiver pub-sub

- **Level:** Beginners · **Category:** Scripts · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Engine/Events/EventKey.cs`, `EventReceiver.cs`,
  `EventReceiverOptions.cs`.
- **What it shows:** Stride's best-kept secret — decoupled script communication. Broadcast from
  one script; `await receiver.ReceiveAsync()` in an AsyncScript, `TryReceive`/`TryReceiveAll` in
  a SyncScript, `EventReceiver.ReceiveOne(...)` as a select over several streams. Teach the two
  gotchas: default mode keeps only the latest event (`Buffered` makes it a queue), and the
  receiver constructor drains one stale event on connect.

### 14. `Example37_EntityProcessors` — write a system, not a hundred scripts

- **Level:** Intermediate · **Category:** Performance (or Scripts) · **Complexity:** 6 ·
  **Verdict:** example
- **Sources:** new API: `engine/Stride.Engine/Engine/FlexibleProcessing/IComponent.cs`,
  `ProcessorManager.cs` (usage: `Stride.Engine.Tests/TestEntityManager.cs:527-620`); classic API:
  `Engine/EntityProcessor.cs`, `Design/DefaultEntityComponentProcessorAttribute.cs`.
- **What it shows:** both processor APIs, old vs new. FlexibleProcessing
  (`IComponent<TProcessor, TSelf>` with a nested processor, lazily created on first component) is
  the pattern Bepu's `ISimulationUpdate` is built on — the toolkit ships an example *consuming*
  it but nothing teaching *authoring*. Measure N components in one batched `Update` against N
  `SyncScript`s — the direct sequel to `Example23_SyncScriptStress`. Cover `ExecutionMode`,
  `Order`, and required-component declarations on the classic API.

### 15. `Example38_ProfilingTrace` — see your frame in Perfetto

- **Level:** Intermediate · **Category:** Performance · **Complexity:** 5 · **Verdict:** both
- **Sources:** `core/Stride.Core/Diagnostics/Profiler.cs` (`Subscribe()` returns a
  `ChannelReader<ProfilingEvent>`), `ProfilingKey.cs`, `ChromeTracingProfileWriter.cs`;
  `engine/Stride.Engine/Profiling/GameProfilingSystem.cs` for the on-screen half.
- **What it shows:** declare your own `ProfilingKey`, wrap a hot loop with
  `using (Profiler.Begin(key))`, watch it appear in the built-in overlay
  (`GameProfilingSystem.EnableProfiling` filtered to your keys) — then press a key, capture five
  seconds with `ChromeTracingProfileWriter`, and drop the JSON into Perfetto to see engine phases
  and your keys interleaved on a flame chart. Nothing in the toolkit or engine docs mentions the
  trace writer at all.
- **Toolkit piece:** `ProfilerScope` + a trace-capture toggle; a live-stats aggregator over
  `Profiler.Subscribe()` feeding the ImGui overlay is a natural follow-on.

### 16. `Example39_ParallelDispatcher` — the engine's parallel-for

- **Level:** Advanced · **Category:** Performance · **Complexity:** 6 · **Verdict:** example
- **Sources:** `core/Stride.Core/Threading/Dispatcher.cs`, `ThreadPool.cs`,
  `ConcurrentCollector.cs`.
- **What it shows:** `Dispatcher.For`/`ForEach`/`ForBatched` share the engine's worker pool, so —
  unlike `Parallel.For` — game code doesn't oversubscribe against render and physics jobs. Update
  50k transforms serially, with `Parallel.For`, and with `Dispatcher.For`, measured with the
  `ProfilingKey` from spec 15. Explain `[Pooled]` delegate pooling and `ConcurrentCollector<T>`.
  The toolkit already calls `Dispatcher.For` in DebugShapes without a word of explanation.

### 17. `Example40_PostEffects` — bloom, fog, vignette and friends, six lines each

- **Level:** Beginners → Intermediate · **Category:** Rendering · **Complexity:** 4 ·
  **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/Images/PostProcessingEffects.cs`;
  `Images/Outline/Outline.cs` and `Images/Fog/Fog.cs` (~95 lines each, constructed with
  `Enabled = false`); `Images/ColorTransforms/Vignetting/`, `ColorTransforms/Noise/FilmGrain.cs`,
  `Images/Dither/`; `engine/Stride.Engine/Rendering/Compositing/ForwardRenderer.cs`.
- **What it shows:** reach `((ForwardRenderer)compositor.SingleView).PostEffects` and flip
  switches: bloom, ambient occlusion, depth of field, screen-space reflections, the depth-based
  `Fog` and `Outline` nobody knows exist, and the hidden `ColorTransform`s (vignette, film grain,
  dither) that fuse into the tonemap pass at zero extra cost. A keyboard-cycled tour with the
  same scene. `Example13_MeshOutline` (per-object) vs the full-screen `Outline` makes a good
  contrast pair.
- **Toolkit piece:** a fluent post-fx configurator on the existing `AddGraphicsCompositor` path.

### 18. `Example41_Shadows` — why your code-only scene has no shadows

- **Level:** Beginners · **Category:** Rendering · **Complexity:** 3 · **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/Lights/LightShadowMap.cs` and siblings
  (`LightDirectionalShadowMap.cs`, filter types, cascade counts), renderers in
  `Rendering/Shadows/`.
- **What it shows:** `LightShadowMap.Enabled` defaults to **false** — the single most common
  code-only lighting gotcha. Enable it, then tour the tuning surface as plain settable POCOs:
  cascade count and partitioning, PCF filter size, `BiasParameters` vs peter-panning, the
  `Debug` flag that draws the shadow map. Point/spot variants (`CubeMap` vs `DualParaboloid`).
- **Toolkit piece:** `light.EnableShadows(...)` extension with sensible defaults.

### 19. `Example42_ScreenEffectShader` — your own full-screen shader, the easy way

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 6 · **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/Images/ImageEffectShader.cs`, `ImageEffect.cs`;
  `Outline.cs`/`Fog.cs` as reference wrappers; simplest blit path:
  `engine/Stride.Graphics/PrimitiveQuad.cs` + `GraphicsDeviceExtensions.DrawTexture/DrawQuad`.
- **What it shows:** `new ImageEffectShader("MyShader")` — one `.sdsl`, `SetInput`/`SetOutput`/
  `Draw`, inputs auto-bound to `Texture0..9` with texel sizes, scoped temp render targets via
  `NewScopedRenderTarget2D`. Open with the even smaller `GraphicsContext.DrawTexture` /
  `DrawQuad` one-liners (the built-in fullscreen triangle) before introducing the class. The
  screen-space sibling of `Example13_RootRootRendererShader`'s mesh effect.
- **Toolkit piece:** a `ScreenEffect` helper slotting an `ImageEffectShader` into the compositor.

### 20. `Example43_RenderToTexture` — cameras on monitors, minimaps, PiP

- **Level:** Advanced · **Category:** Rendering · **Complexity:** 7 · **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/Compositing/RenderTextureSceneRenderer.cs`,
  `DelegateSceneRenderer.cs`, `SceneRendererCollection.cs`; the production-quality worked example
  is the editor's camera preview,
  `editor/.../EntityHierarchyEditor/Game/EditorGameCameraPreviewService.cs` (336 lines, two
  cooperating renderers, temp textures from `GraphicsContext.Allocator`); screenshot half:
  `Texture.ToStaging`/`GetDataAsImage`/`Save` (`engine/Stride.Graphics/Texture.cs:1070-1760`).
- **What it shows:** render a second camera into a texture, show it on an in-world quad and as a
  screen-corner inset; save a PNG screenshot on demand. Teach the collect-phase contract
  (`SetRenderOutputAndRestore` — the step everyone forgets) and `DelegateSceneRenderer` as the
  20-line way to inject draw code into the compositor. The editor's thumbnail system
  (`editor/Stride.Editor/Thumbnails/ThumbnailGenerator.cs`) supplies known-good framing and
  lighting constants for an item-icon variant.
- **Extends** `Example09_Renderer` rather than replacing it.

### 21. `Example44_GpuPicking` — pixel-perfect picking, the Game Studio way

- **Level:** Advanced · **Category:** Rendering (secondary Interaction) · **Complexity:** 8 ·
  **Verdict:** both
- **Sources:** `editor/Stride.Assets.Presentation/SceneEditor/PickingSceneRenderer.cs`,
  `PickingRenderFeature.cs`; shader `engine/Stride.Rendering/Rendering/Utils/PickingShader.sdsl`
  (engine-side, so fully portable).
- **What it shows:** how the editor *actually* selects objects — no raycasts. A dedicated render
  stage writes `(componentId, meshIndex.materialIndex)` into an `R32G32_Float` target, with the
  instance ID packed into the fraction; a **1×1 scissor rectangle** at the cursor rasterises a
  single pixel, `CopyRegion` + `GetData` on a persistent 1×1 staging texture reads it back.
  Works on skinned meshes, alpha-cut sprites and GPU instances where collider raycasts can't.
  Completes the picking triptych: Bepu raycast (Example14) / math ray (spec 6) / GPU ID buffer.
- **Toolkit piece:** a `GpuPicker` helper.

### 22. `Example45_MeshVertexReadback` — read your mesh back at runtime

- **Level:** Intermediate · **Category:** Shapes · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Graphics/VertexBufferHelper.cs`, `IndexBufferHelper.cs`,
  `MeshExtension.cs` (`AsReadable`), `Semantics/`; companions
  `engine/Stride.Rendering/Extensions/` (the `Stride.Extensions` namespace:
  `GenerateTangentBinormal`, `MergeDrawData`, `ReverseWindingOrder`, `ComputeBounds`, ...).
- **What it shows:** the most-asked Stride question — "how do I read my mesh's vertices?"
  `binding.AsReadable(Services, out var helper, out var count)` then
  `helper.Copy<PositionSemantic, Vector3>(span)` with automatic format conversion (Half4, Byte4,
  Color → your type). The API even carries runnable `<example>` blocks in its XML docs and is
  effectively invisible. Demo: deform or explode a procedural mesh, or derive a picking mesh for
  spec 6. Close with the `Stride.Extensions` mesh-surgery namespace (mis-named, hence
  undiscovered): generate tangents so normal maps work on procedural geometry, merge draws,
  reverse winding.
- **Reads** what the Example05 family writes.

### 23. `Example12_Particles_ForceFields` — particles, part two

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 5 · **Verdict:** example
- **Sources:** `engine/Stride.Particles/Updaters/UpdaterForceField.cs`, `UpdaterCollider.cs`,
  `Updaters/FieldShapes/`, `ParticleModule.TryGetDebugDrawShape`.
- **What it shows:** a vortex force field (directed/vortex/repulsive force decomposition over a
  shaped falloff) plus a particle collider (restitution, friction, `IsHollow`, `KillParticles`),
  with the field shapes drawn via the toolkit's DebugShapes — the modules implement
  `TryGetDebugDrawShape` themselves. Everything is public and CPU-side; there are no GPU
  particles to look for.
- **Siblings worth their own rows later:** ribbons/trails (with the undocumented hard requirement
  `SortingPolicy = ByOrder` + `InitialSpawnOrder`, or you get garbage geometry), fireworks via
  child emitters (`SpawnerFromParent` + death trigger), burst-on-click (`EmitParticles(n)`,
  `SpawnerBurst`), soft particles (`ParticleMaterialSimple.SoftEdgeDistance`), and custom
  `ParticleUpdater` subclasses (the deepest teaching value — unsafe SoA pool access).

### 24. `Example46_OrbitCamera` — the Game Studio camera in your game

- **Level:** Intermediate · **Category:** Input · **Complexity:** 5 · **Verdict:** both
- **Sources:** `editor/.../GameEditor/Game/EditorGameCameraService.cs` (334),
  `EntityHierarchyEditor/Game/EditorGameEntityCameraService.cs` (349);
  `editor/Stride.Editor/Engine/EntityExtensions.cs` (`CalculateBoundSphere`).
- **What it shows:** orbit/pan/dolly/zoom with the editor's exact feel, in ~200 lines of
  portable math. The elegant core: when *not* orbiting, the orbit pivot is continuously
  re-derived from the current view, so orbiting "just works" from wherever you stopped; pan speed
  scales with orbit radius so it feels constant at any zoom. F-to-focus frames any entity via
  `CalculateBoundSphere` (which handles skinned models, sprites and particles — worth porting
  verbatim). Six axis-aligned views + ortho toggle complete it.
- **Toolkit piece:** `OrbitCameraController` beside the existing free-fly controller — the
  third-person-camera backlog row's closest cousin, and the most-requested missing camera mode
  for editors, model viewers and RTS-likes.

### 25. `Example47_UI_CodeGallery` — Stride UI without Game Studio, properly

- **Level:** Beginners → Intermediate · **Category:** UI · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.UI/Panels/` (Canvas/Grid/StackPanel + `StripDefinition`),
  `Controls/` (incl. `ModalElement`, `ToggleButton`, `ScrollViewer`), `UIElement.cs`;
  world-space: `Engine/UIComponent.cs` (`IsBillboard`, `IsFixedSize`, `ResolutionStretch`).
- **What it shows:** a controls-and-layout cookbook for code-first UI — above all the
  attached-property idiom (`element.DependencyProperties.Set(Canvas.RelativePositionPropertyKey,
  ...)`), which is the single most confusing thing about Stride UI in code, plus star-sized
  grids, `ModalElement` dialogs, and a world-space billboard health bar (`IsFullScreen = false`,
  `IsFixedSize` for constant screen size at any distance). Document the verified absence of data
  binding so nobody hunts for it.
- **Toolkit piece:** fluent `UIBuilder` extensions (`.WithGridRow(1)`, `.WithCanvasRelative(...)`).

---

## Inventory — everything else worth keeping

One line each; verdicts as above. These are real candidates that didn't make the top cut, grouped
by area. (Tier-2 material for the backlog when a category needs feeding.)

### Graphics + Games

| Item | Source (under `sources\`) | Verdict · Level · Category | Note |
|---|---|---|---|
| `GameTime.Factor` slow motion | `engine\Stride.Games\GameTime.cs:106-139` | example · Beginners · Gameplay | One assignment; Bullet, **Bepu**, animation and particles all honour it. Best folded into the Bepu plan's `Example26_TimeControl` (which currently covers only `BepuSimulation.TimeScale`) rather than a new example. |
| Custom `GameSystemBase` | `Stride.Games\GameSystemBase.cs`, `GameSystemCollection.cs` | example · Intermediate · Scripts | The pattern `ImGuiSystem` already uses but nothing teaches; late-added systems initialize immediately. |
| Graphics settings menu | `Stride.Games\GraphicsDeviceManager.cs` (+`GraphicsAdapterFactory`, `GraphicsOutput`) | example · Intermediate · UI | Resolution/vsync/MSAA/monitor/GPU pickers + `ApplyChanges()`; pairs with a `DeviceCapabilitiesReport` helper. |
| Fixed timestep / FPS cap / background throttle | `Stride.Games\GameBase.cs:163-344,529-615`, `core\Stride.Core\ThreadThrottler.cs` | both · Intermediate · Performance | Three FAQs in one; `DrawInterpolationFactor` is unknown. |
| `RawTick` manual loop | `GameBase.cs:529,635` | example · Advanced · Gameplay | Deterministic replay, headless benchmarking, lockstep (pairs with SignalR). |
| WinForms embedding + headless | `Stride.Games\GameContext*.cs`, `IMessageLoop.cs` | example · Advanced · Integration | `isUserManagingRun: true` + `game.Tick()`; `AppContextType.Headless`. |
| Second OS window | `Stride.Games\GameWindowRenderer.cs` | example · Advanced · Integration | Presenter swapping; detached inspector/spectator window. |
| `GameWindow` tricks | `Stride.Games\GameWindow.cs` | toolkit · Beginners · Interaction | Borderless toggle, `Opacity`, `Closing`/`Deactivated` events; `ToggleBorderlessFullscreen()` helper. |
| GPU stats HUD | `Stride.Graphics\GraphicsDevice.cs:69-100` | toolkit · Beginners · Performance | `FrameDrawCalls`, `FrameTriangleCount`, VRAM counters — feed the debug overlay; great beside instancing examples. |
| `GpuTimer` (QueryPool) | `Stride.Graphics\QueryPool.cs`, `QueryManager.cs` (Rendering) | both · Advanced · Performance | Real GPU timing; poll-next-frame pattern. Only `Timestamp` queries exist. |
| GPU debug markers | `CommandList` `BeginProfile/EndProfile` | example section · Intermediate · Performance | Named RenderDoc/PIX regions; 3 lines inside Example09. |
| `Sprite3DBatch` billboards | `Stride.Graphics\Sprite3DBatch.cs`, `Sprite.cs` | both · Intermediate · Rendering | World-space sprites/damage numbers with no sprite-sheet asset; also `SpriteFromTexture` provider (`Stride.Rendering\Sprites\`). |
| `Texture.Load` from stream + texture views | `Texture.Extensions.cs`, `Texture.cs:1592` | both · Beginners · Rendering | "Load a PNG at runtime" one-liner; `LoadTextureFromFile` helper. |
| `UIBatch` / `BatchBase<T>` | `Stride.Graphics\UIBatch.cs`, `BatchBase.cs` | example · Advanced · Rendering | Nine-slice + depth-biased drawing; `BatchBase` is the canonical dynamic-buffer streaming reference. |
| Compute + indirect draw | `Buffer.Structured/Raw/Argument.cs`, `CommandList` dispatch/indirect | example · Advanced · Performance | With `ComputeEffectShader` (Rendering) for GPU particles / GPU culling. |
| Split-screen viewports | `CommandList.cs:68-181`, `Viewport.Project/Unproject` | example · Intermediate · Rendering | Up to 16 viewports/scissors; `Project` for world→screen labels. |
| Render-state cheat sheet | `BlendStates.cs`, `DepthStencilStates.cs`, `RasterizerStates.cs`, `SamplerStateFactory.cs` | example · Beginners · Rendering | Named presets (`Wireframe`, `Additive`...) nobody can discover. |
| `MutablePipelineState`, `GraphicsResourceAllocator`, `GetOrCreateSharedData` | various | doc sections · Advanced · Rendering | The three idioms every custom renderer needs; document once. |
| `LaunchParameters` | `Stride.Games\LaunchParameters.cs` | note · Getting Started · Scripts | Built-in command-line dict; the example launcher could use it. |

### Rendering

| Item | Source | Verdict · Level · Category | Note |
|---|---|---|---|
| Light shafts (god rays) | `Rendering\Images\LightShafts\` + `LightShaftComponent` | example · Advanced · Rendering | Spectacular; requires shadows (spec 18 first). Min/max volume trick is a real GPU lesson. |
| Cel/toon, hair, clear-coat materials | `Rendering\Materials\CelShading\`, `Hair\`, `MaterialClearCoatFeature.cs` | example · Intermediate · Rendering | Five-line stylised material descriptors invisible outside the GS dropdown. |
| Live material parameters | `MaterialRenderFeature.cs:249-455`, `MaterialKeys` | both · Beginners · Rendering | `Passes[0].Parameters.Set(...)` animates free; value vs permutation keys explains the hitches. `material.SetColor()` helper. |
| Material node graph in code | `ComputeColors\ComputeShaderClassColor.cs` etc. | both · Advanced · Rendering | Arbitrary `.sdsl` ComputeColor into a standard lit material; editor's `GizmoShaderMaterial.Create` is a 20-line worked example. |
| Multi-pass materials | `MaterialFeature.MultipassGeneration`, `MaterialGeneratorContext.cs:89-160` | example · Advanced · Rendering | Inverted-hull outlines / fur shells; only in-tree user is clear coat. Third outline technique → comparison page. |
| Grab-pass refraction | `ForwardRenderer.cs:112-127`, `OpaqueBase.sdsl` | example · Advanced · Rendering | `BindOpaqueAsResourceDuringTransparentRendering` — off by default, zero docs, real refraction. |
| Render groups & cull masks | `RenderGroup(Mask).cs`, `SceneCameraRenderer.RenderMask` | both · Intermediate · Rendering | First-person weapon layer, minimap-only objects; 32 groups. |
| Wireframe/x-ray via stage + processor | `WireframePipelineProcessor.cs` (18 lines), editor `PhysicsDebugShapeService.cs` (the 40-line recipe) | both · Advanced · Rendering | Cleanest "what is a render stage" intro. |
| Runtime reflection probe | `Stride.Engine\Rendering\Skyboxes\CubemapSceneRenderer.cs`; editor `EditorGameCubemapService.cs` | both · Advanced · Rendering | Live-scene cubemap → feed the *existing* toolkit `SkyboxGenerator`. The un-mined half of the skybox story. |
| Runtime light-probe bake | `Stride.Engine\Rendering\LightProbes\LightProbeGenerator.cs` | example · Advanced · Rendering | GS "bake" button callable from a script (≥4 probes); for procedural levels. |
| Tessellation + displacement | `MaterialTessellationPNFeature.cs`, `MaterialDisplacementMapFeature.cs` | example · Advanced · Rendering | Needs `GenerateIndexBufferAEN`; obscure but marquee. |
| Spot-light gobo | `LightSpot.cs:80-130` (`ProjectiveTexture`) | example · Intermediate · Rendering | Projector/stained glass in a handful of property sets. |
| HDR auto-exposure + tonemap operators | `LuminanceEffect.cs`, `ColorTransforms\ToneMap\` (9 operators incl. ACES) | example · Intermediate · Rendering | Small; also `GaussianBlur`/`ImageScaler` as general texture utilities. |
| `ImageReadback<T>` | `Images\ImageReadback\` | toolkit · Advanced · Performance | Non-blocking GPU→CPU with staging pool; the "without stalling" sequel to picking. |
| `BackgroundComponent` | `Rendering\Background\` | example · Beginners · Rendering | One-component backdrop/skybox; simplest readable `RootRenderFeature`. |
| `ProceduralModelDescriptor` | `Rendering\ProceduralModels\` | example · Beginners · Shapes | `UvScale`/`LocalOffset` answered; the layer under the toolkit's own primitives. |
| Material-channel debug view | editor `MaterialFilterRenderFeature.cs` (56 lines) + engine `MaterialStreamDescriptor` statics | both · Advanced · Rendering | Key-cycled "show normals/roughness/AO" — 100% engine-public. |
| Selection wireframe + tint highlight | editor `WireframeRenderFeature.cs`, `HighlightRenderFeature.cs` | example/toolkit · Advanced · Rendering | How GS really draws selection (filtered stage, depth off, front/back colour); per-material-slot tinting. |
| Overlay scene (2nd SceneSystem) | editor `EntityHierarchyEditorGame.cs` + `EditorTopLevelCompositor` | toolkit · Advanced · Rendering | Own lighting, no clear, gizmo group mask — would simplify every gizmo/debug example. Prerequisite reading for much of the editor column. |
| VR stereo via `VRApi.Dummy` | `Stride.VirtualReality\DummyDevice.cs`, `VRRendererSettings` | both · Advanced · Rendering | Full stereo pipeline with no headset; `VRDeviceSystem` already registered in every Game. |

### Engine, Input, UI

| Item | Source | Verdict · Level · Category | Note |
|---|---|---|---|
| Script priorities & micro-threads | `ScriptSystem.cs`, `ScriptComponent.Priority`, `Scheduler.cs` | example · Intermediate · Scripts | Sync scripts batch per priority; async runs after sync; `AddTask`, `NextFrame`. |
| `AsyncSignal` / `AsyncAutoResetEvent` | `core\Stride.Core.MicroThreading\` | example section · Advanced · Scripts | Producer/consumer between AsyncScripts. **Avoid `Channel<T>`** (see upstream findings). |
| Scene-graph events / live inspector | `EntityManager.cs:54-115`, `SceneInstance.cs` | toolkit · Intermediate · Interaction | Event-driven ImGui scene tree; `SceneInstance.GetCurrent(RenderContext)` from render features. |
| Child scenes + `Scene.Offset` | `Engine\Scene.cs` | example · Intermediate · Gameplay | Chunk streaming / floating origin, fully code-only. |
| Custom `TransformLink` + `PostOperations` | `TransformLink.cs`, `TransformComponent.cs:36-42` | both · Advanced · Gameplay | Replace "multiply by parent" with anything; the hook under `ModelNodeLinkComponent`; also the engine-blessed spline-follow mechanism. |
| `ModelNodeLinkComponent` | `Engine\ModelNodeLinkComponent.cs` | example · Intermediate · Gameplay | Sword-in-hand; needs a skinned asset, so half code-only. |
| `SpriteAnimationSystem` | `Rendering\Sprites\SpriteAnimationSystem.cs` | example · Beginners · Rendering | Frame animation + `Queue` chaining over a code-built sprite sheet; feeds the 2D story. |
| Event-driven input | `IInputEventListener.cs`, `InputManager.Events` | example · Intermediate · Input | No lost inputs between frames; pooled events. |
| Text input + IME | `ITextInputDevice.cs`, `TextInputEvent.cs` | both · Intermediate · Input | The only correct typed-text path; in-game console/chat, pairs with SignalR. |
| Gamepad vibration + indices | `IGamePadDevice.cs` | example · Beginners · Input | Four-motor rumble, hot-plug, local-multiplayer index model. |
| Mouse lock + raw input | `InputManager.cs:143,251-378` | toolkit · Beginners · Input | Pointer lock for the free-fly controller; normalised-vs-absolute is the #1 mouse-look bug. |
| `InputSourceSimulated` | `Stride.Input\Simulated\` | toolkit · Advanced · Integration | Scripted input for replays/attract mode/tests; used by AutoTesting (below). |
| Custom `UIElement` + `ElementRenderer` | `Stride.UI\Renderers\`, `UIRenderFeature.cs:336-348` | example · Advanced · UI | Radial gauge/minimap element; includes `DependencyPropertyFactory`. |
| Routed events | `Stride.UI\Events\EventManager.cs` | example · Intermediate · UI | Tunnel/bubble/`Handled`; `RegisterClassHandler` = "all buttons click-sound" in one line. |
| UI hit-testing | `UIRenderFeature.Picking.cs`, `UISystem.UIElementUnderMouseCursor` | toolkit · Intermediate · Interaction | One-liner most people reimplement. |
| `UIElementLinkComponent` | `Stride.UI\Engine\UIElementLinkComponent.cs` | example · Advanced · UI | 3D entity attached to a UI element (item preview in an inventory slot). |
| UI adorners + magnet snapping | editor `UIEditor\Adorners\`, `UILayoutHelper.cs` | example · Advanced · UI | Drag handles/resize/snap in pure Stride.UI; sequel to Example10. |
| `IBlendTreeBuilder` | `AnimationComponent.cs:44,212` | doc snippet · Advanced · Gameplay | Locomotion blend spaces; needs animation assets. |

### Editor, core, misc

| Item | Source | Verdict · Level · Category | Note |
|---|---|---|---|
| ViewCube + corner axes | editor `CameraOrientationGizmo.cs`, `SpaceMarker.cs`, `GizmoViewportRenderer.cs` | both · Advanced · Rendering | Highest "I want that" widget; sub-viewport + second camera + math picking + 3D SpriteBatch text in one. |
| Billboard icon markers | editor `BillboardingGizmo.cs`, `EntityGizmo.cs` | toolkit · Beginners · Rendering | Constant-screen-size world icons; `PixelsPerUnit = texture.Width` is the knob. |
| Frustum & light-shape wireframes | editor `CameraGizmo.cs`, `LightSpotGizmo.cs`, `LightPointGizmo.cs` | example · Intermediate · Shapes | Dynamic line-mesh recipe + frustum corner math. |
| Constraint visualiser | editor `PhysicsConstraintGizmo.cs` (569) | example · Advanced · Physics | Adapt to Bepu; would upgrade the whole Example15 family; also the clearest `GeometricPrimitive` lifetime warning. |
| Gizmo registry (`GizmoManager`) | editor `EditorGameComponentGizmoService.cs` + engine `Engine\Gizmos\` | toolkit · Intermediate · Scripts | Attribute-driven auto-gizmos on component add; generalises `Example08_CollidableGizmo`. |
| Three-point lighting preset | editor `PrefabEditorLightService.cs` (44) | toolkit · Getting Started · Rendering | Known-good numbers for "why is my scene flat"; `AddThreePointLighting()`. |
| `CalculateBoundSphere` | `editor\Stride.Editor\Engine\EntityExtensions.cs` | toolkit · Beginners · Scripts | Needed by spec 24; handles skinned/sprite/particle bounds correctly. |
| Frustum culling visualiser | `core\...\BoundingFrustum.cs`, `BoundingBoxExt.cs` | both · Intermediate · Rendering | Cull against a second camera, green/red debug boxes; `IsVisible(entity)` helper. |
| In-game log console | `core\...\Diagnostics\Logger.cs`, `GlobalLogger.GlobalMessageLogged`, `LogListener` | both · Beginners · UI | Route engine+game logs to overlay/ImGui + rotating file via VFS. |
| `GuillotinePacker` | `core\Stride.Core.Mathematics\GuillotinePacker.cs` | both · Intermediate · Rendering | Runtime atlas packing (the engine's shadow-atlas packer); pairs with `TextureCanvas`. |
| `ServiceRegistry` DI | `core\Stride.Core\ServiceRegistry.cs` | example · Intermediate · Scripts | Share state without singletons; `GetOrCreate<T>`, `GetServiceLate`. |
| `RandomSeed` | `core\...\RandomSeed.cs` | toolkit · Intermediate · Gameplay | Stateless deterministic (seed, index) randomness for procedural gen/replays. |
| `ThreadThrottler` | `core\Stride.Core\ThreadThrottler.cs` | example · Intermediate · Performance | Cap 60 FPS three ways, measure jitter. |
| Colour helpers | `ColorHSV.cs`, `Color.Palette.cs` (143 colours), sRGB↔linear | toolkit · Beginners · Rendering | `FromHsv`, palette generation, and the linear-vs-gamma "why is my red wrong" note. |
| `TrackingCollection<T>` | `core\Stride.Core\Collections\` | supporting · Beginners · UI | Observable collections behind the scene graph; inventory-updates-UI demo material. |
| RenderDoc capture key | `tools\Stride.Graphics.RenderDocPlugin\RenderDocManager.cs` | example · Advanced · Rendering | Press F12 → `.rdc`; publishes as its own NuGet; Windows/D3D only. |
| Gettext localization | `core\Stride.Core.Translation\` | example · Intermediate · UI | Works but targets the editor TFM — flag the caveat if used. |
| Video → texture | `engine\Stride.Video\` | spike · Advanced · Rendering | Blocked on `ObjectDatabase` URL resolution; untested `ObjectDatabase.Write` + `Content.Save/Load` route. Don't promise it. |

---

## Considered and rejected

Recorded so the same paths aren't mined twice. (Each survey's full rejection list, with reasons,
is preserved in its report; this is the merged short form.)

**Already mined / already covered:** `GeometricPrimitives`, `FastTextRenderer`, `Font\*` runtime
fonts, `ViewportGridGizmo` + grid services (only the plane-switch/colour-space details are new —
a docs addendum to engine-patterns.md, not an example), `AxialGizmo` (ported verbatim),
`EditorGameHelper` picking, gizmo colour materials, `InstancingRenderFeature`,
`DebugTextSystem`, editor `DebugShapes\` (overlaps `Example08_DebugShapes`).

**Asset-pipeline-bound, unreachable code-only:** `Sound`/`CompressedSoundSource` construction
(internal + ffmpeg), `AudioEmitterComponent` with runtime audio (`SoundBase` internals),
`StreamingManager`/`StreamedBufferSound` (desktop path is a stub), `SpriteSheet` assets,
`UILibrary`/`UIPage`, `SkinningRenderFeature` (needs a rigged import), texture-content
serializers, `Stride.Importer.3D` and `Stride.TextureConverter` (editor TFM + native deps),
`ContentStreamingService`.

**Verified absent — document, don't hunt:** splines, model LOD, UI data binding, GPU particles,
occlusion queries, decals, deferred renderer, `Microphone` capture, HRTF on desktop.

**Too deep / internal plumbing:** descriptor sets and root signatures below `EffectInstance`,
constant-buffer suballocation (`ResourceGroup*`, `BufferPool`), explicit barrier APIs, deferred
command lists, `RootRenderFeature` beyond what Example13 covers, MRT semantics
(`ResourceResolver`), the reflection stack, `Stride.Core.AssemblyProcessor` (blog post, not
example), Yaml (editor TFM), `Stride.Core.Design` transactions/settings (editor packages — teach
the undo pattern hand-rolled instead).

**Editor-only glue with no runtime story:** controllers/dispatchers, Quantum change watchers,
content loaders, recovery services, preview system (superseded by the thumbnail pipeline for
teaching), GameStudio shell, gizmo icon `.resx` assets (mechanism portable, PNGs not ours).

**Niche / platform-bound:** mobile sensors, UWP/WMR, `SpriteStudio` runtime, `Stride.Voxels`
(needs `ForwardRendererVoxels` + non-default package — possible far-future stretch),
`VROverlay` (hardware only), VTune hooks, `Stride.Debugger`, `Stride.Engine.NextGen` (one dead
file).

**Small-but-noted:** `AngleSingle`, vector swizzles, `Half*`/`Int2/3` (mention inside existing
examples), `LaunchParameters`, `ContentManagerStats`, `LightShaftsVolumeGizmo`'s
`LocalMatrix = WorldMatrix` trick, `GameSettings` injection into code-only games (the
`IGameSettingsService` slot is free — remember as a general trick).

---

## Upstream findings

Bugs and doc gaps found while verifying — candidates for `notes/upstream/` drafts:

1. **`Channel<T>` throws `NotImplementedException` on its default path.**
   `core\Stride.Core.MicroThreading\Channel.cs`: with the default
   `ChannelPreference.PreferReceiver`, `Send` with a waiting receiver hits a
   `throw new NotImplementedException()` (mirrored for `Receive` under `PreferSender`). The
   engine itself sidesteps it (`Scheduler.cs:53` constructs with `PreferSender`). Issue-worthy.
2. **`SoundInstance.Position` NREs for dynamic sources.** `SoundInstance.cs:402` dereferences
   `sound`, which is only set by the internal static-sound constructor; instances created via the
   public `DynamicSoundSource` constructor throw when `Position` is read while playing.
3. **`ShaderClassString` is an orphaned type.** Nothing in the whole tree consumes
   `ShaderClassCode`/`ShaderClassString`; only `ShaderClassSource` is handled by the compilers.
4. **`DynamicNavigationMeshSystem` is never registered by the engine** and no documentation says a
   game must add it; the TopDownRPG template only *finds* it. Doc gap (or engine-side fix).
5. **HRTF parameters are dead on desktop** — threaded to `OpenAL.cpp` and ignored. Doc gap at
   minimum.

## Toolkit infrastructure (not examples)

- **`Stride.Games.AutoTesting`** (`engine/Stride.Games.AutoTesting/`) — new 4.4
  screenshot-regression harness: registers via `[ModuleInitializer]`, swaps in simulated
  input, forces software rendering (`STRIDE_TESTS_GPU=1` opts out), exposes
  `WaitFrames`/`Screenshot(name, threshold)`/`PressKey`, compares with LPIPS and a per-shot
  threshold (explicitly built for nondeterministic content like particles). The toolkit's ~57
  examples currently ship hand-made `.webp` previews — this could generate *and CI-verify* them.
  Deserves its own evaluation/plan doc.
- **`FrameGameSystem`** (`Stride.Graphics.Regression`) — schedule work on specific frame numbers;
  handy for deterministic example screenshots.
- **`DelegateSceneRenderer`** — could simplify the toolkit's own renderer boilerplate.
- Editor-derived helpers that are toolkit-shaped regardless of examples: `AddThreePointLighting()`,
  `CalculateBoundSphere`, the `InputArbiter`, `OverlayScene`, `GizmoManager`, `GpuStatsOverlay`,
  `InGameLogListener`, `LoadTextureFromFile`, `EnableShadows`, `material.SetColor`.

## Coverage impact

If only the full specs above were built, the empty categories fill as: **Audio** +3, **Input** +3
(virtual buttons, gestures, orbit camera), **Interaction** +2, **Gameplay** +5, **Performance**
+3, **Scripts** +1, **UI** +1, plus Rendering/Shapes depth. Integration stays thin — its best
candidates are the WinForms-embed and second-window items in the inventory.

Suggested first five, balancing quick wins against gap-filling:
`Example27_Audio_ProceduralSound` + `Example27_Audio_WavFile` (one PR, new category, new library),
`Example40_PostEffects` (biggest visible payoff per line), `Example41_Shadows` (the FAQ),
`Example29_PickingNoPhysics` (Interaction unlocked, zero dependencies), and the
`GameTime.Factor` fold-in to the Bepu plan's time-control example (near-free).
