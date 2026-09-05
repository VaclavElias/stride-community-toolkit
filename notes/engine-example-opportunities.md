# Engine Example Opportunities

A research sweep of the Stride sources (`D:\Projects\GitHub\stride\sources`, Stride 4.4, master as of
2026-08-29, commit `d87510abb`) looking for the next wave of [engine patterns](../docs/manual/engine-patterns.md):
public-but-undocumented capabilities, and internal-but-copyable (MIT) code, that could become
toolkit examples, toolkit helpers, or both. Six areas were surveyed in parallel on 2026-08-29 —
Graphics+Games, Rendering, Engine+Input+UI, the remaining engine subsystems (Audio, Particles,
Navigation, Video, VR), the editor sources, and core+tools — each briefed with what
[engine-patterns.md](../docs/manual/engine-patterns.md) and the
[example backlog](example-backlog.md) had already mined.

**Cross-checked 2026-09-02.** A second pass re-verified every claim below against the same commit
(three agents re-read every cited file) and re-swept the tree for misses with dedicated briefs for
Game Studio visualisation code, the test projects (including headless mode), core/asset
compilers/tools/MSBuild targets, the engine runtime, and the bundled templates and samples. The
result: ~30 corrections applied in place (the largest are listed in the next section), 12 new
full specs (`Example48`–`Example59`), 14 compact specs from the engine re-sweep
(`Example80`–`Example93`), ~80 new inventory rows, a new
[toolkit-side findings](#toolkit-side-findings) section, and two sibling documents:
[starbreach-example-opportunities.md](starbreach-example-opportunities.md) and
[samples-example-opportunities.md](samples-example-opportunities.md) (the `stride/samples` tree,
~16k lines nobody had looked at, which turned out to be the real content behind
`sources/templates`).

**Status: research, nothing agreed.** The intended flow is: pick items from here → add a row to
[example-backlog.md](example-backlog.md) (Source: "in-engine, this doc") or an entry to
[TODO.md](TODO.md) for toolkit work → build. Example numbers are provisional: 27–47 are the
original specs, 48–59 the cross-check additions, 60–77 belong to the samples doc, and 80–93 are
the engine re-sweep's compact specs. Family names (`Example27_Audio_*`)
follow the existing convention of one number per topic family.

Verdicts: **example** (teach it), **toolkit** (wrap or port it into a toolkit library), **both**.
Levels and categories follow [examples/code-only/README.md](../examples/code-only/README.md)
(note: the README has no "Scripts" category; rows below that say Scripts should land in
Gameplay or Performance when they graduate). Every claim below was verified by an agent reading
the actual source (public vs internal checked); line numbers are as of the surveyed commit and
will drift.

## What the cross-check changed

The headline corrections, so a reader of the first version knows what to un-learn:

1. **HRTF works on Windows.** The native audio DLL is built with `-DWINDOWS_DESKTOP`, which
   compiles the XAudio2 backend (with `HrtfApo.dll` spatialisation) and excludes OpenAL
   (`XAudio2.cpp:6`, `OpenAL.cpp:6`, `native/Stride.Native.targets:176-182`). Only Linux/macOS
   ignore the HRTF parameters. It is off by default because the gate is
   `AudioEngineSettings.HrtfSupport` read from `IGameSettingsService` (`AudioSystem.cs:59-60`).
2. **`DynamicNavigationMeshSystem` is registered by the engine** — `BoundingBoxProcessor.cs:16-27`
   adds it when the first `NavigationBoundingBoxComponent` appears. The real gotcha is that it is
   constructed `Enabled = false` (`DynamicNavigationMeshSystem.cs:64`) and only a GameSettings
   asset flips it on. And `Stride.Navigation` references **Bullet** (`Stride.Navigation.csproj:16`);
   its raw-geometry input is internal, so there is no Bepu route through it — but the tree ships
   `Stride.BepuPhysics.Navigation` (Recast on Bepu), which spec 11 now targets.
3. **"Every post effect ships disabled" is only true via `GraphicsCompositorHelper.CreateDefault`.**
   A bare `new PostProcessingEffects()` has bloom, SSAO, SSR, DoF, light streaks, lens flare and
   FXAA **enabled** (`RendererCoreBase.cs:44`). The toolkit's own `AddCleanUIStage()` builds
   exactly that — see [toolkit-side findings](#toolkit-side-findings).
4. **Code-only games never get a shader compilation mode — but it matters less than it looks.**
   `Game.cs:383-386` calls `EffectSystem.SetCompilationMode` only when a GameSettings asset exists,
   and `EffectCompilerParameters.Default` is `Debug = true, OptimizationLevel = 0`. *Corrected
   2026-09-03 by building it:* the D3D11 compiler applies the optimisation level only when `Debug`
   is false (`Direct3D/ShaderCompiler.cs:84-96`; `D3DCOMPILE_OPTIMIZATION_LEVEL1` is `0`, FXC's
   default), so `Debug` and `Release` both compile at FXC level 1 with debug info and produce
   byte-identical stripped bytecode — proven by identical cache hashes. Only `AppStore`
   (`Debug = false`, level 2, no symbols) changes the output, and only on D3D11: Vulkan and D3D12
   consume the SPIR-V directly and the level is never read. The real value of the settings gap is
   elsewhere (correction 11 in the toolkit findings: HRTF, physics, navigation, Bepu, rendering
   settings all read `IGameSettingsService`).
5. **Desktop `/roaming`, `/local`, `/cache` resolve to folders next to the executable**
   (`PlatformFolders.cs:80-134`, with a `// TODO`), not to the user profile. Spec 8 is reframed.
6. **The transparency explanation in the published manual page is wrong about mechanism.**
   `engine-patterns.md` says setting `MaterialPass.HasTransparency` by hand means "the generated
   shader never learns to blend". Blending is pipeline state: `MeshTransparentRenderStageSelector.cs:16`
   routes on `HasTransparency` and `MeshPipelineProcessor.cs:21-24` forces `AlphaBlend` +
   `DepthRead` for the transparent stage. Three editor services rely on this without the blend
   feature. Points 2–3 of that section stay valid; point 1 needs rewording (and the original
   failure should be reproduced first — a custom compositor with no transparent stage on its
   pipeline processor would explain it).
7. **`Stride.Debugger` live reload is dead code** (`LiveAssemblyReloader.cs:33` throws
   `NotImplementedException`); the Game Studio "live scripting" path ends there.
8. **Nothing registers `LightProbeRenderer`.** `GraphicsCompositorHelper.CreateDefault` lists every
   light renderer except it (`GraphicsCompositorHelper.cs:63-103`), so runtime-baked probes never
   light a code-only scene until you add one.
9. **`UIPage`/`UILibrary`/`SpriteSheet`/`Sprite` are constructible in code** — the first version
   listed them as asset-bound. Toolkit examples already build `UIPage`s.
10. **The "editor TFM" excuse was wrong.** `StrideXplatEditorTargetFramework` = `net10.0`
    (`Stride.Editor.Frameworks.props:17`); only `StrideEditorTargetFramework` is `net10.0-windows`.
    Yaml, Design, Translation, Importer.3D and TextureConverter are blocked by dependencies and
    natives, not by the framework. Stride 4.4 and the toolkit both target **net10.0**.
11. **The toolkit already has** an orbit camera (`Basic3DOrbitCameraController`), a three-point
    lighting helper (`AddStudioLighting`), a GPU-stats overlay (`PerfMonitor`), a live
    `Profiler.Subscribe()` aggregator, `Texture.Load` usages, and UI attached-property setters
    (`UIElementExtensions` exist in the engine). Specs 15, 18, 24, 25 and three inventory rows
    were downgraded accordingly.

## Facts established

Worth recording even where no example follows — several are "stop looking for it" answers:

- **The gizmo contract lives in the engine, not the editor.** `IGizmo`, `IEntityGizmo`,
  `GizmoComponentAttribute` are in `engine/Stride.Engine/Engine/Gizmos/`, and every shader the
  editor's picking/wireframe/highlight machinery needs (`PickingShader.sdsl`,
  `HighlightShader.sdsl`, `MaterialFrontBackBlendShader.sdsl`, `CameraOrientationGizmoShader.sdsl`,
  plus `EffectCompiling.sdsl`, `CompilationErrorShader.sdsl`, `LightConstantWhite.sdsl`,
  `SharedTextureCoordinate.sdsl`, `Sprite3DBase.sdsl` under `Rendering/Editor/`) is engine-side.
  So is `EditorTopLevelCompositor` (`engine/Stride.Engine/Rendering/Compositing/`). Most editor
  techniques port into a game without touching editor assemblies.
- **Post effects are disabled only by the default compositor.** `GraphicsCompositorHelper.CreateDefault`
  builds `PostProcessingEffects`, calls `DisableAll()` and re-enables only the colour-transform
  group (`GraphicsCompositorHelper.cs:45-49`). Constructed directly, `PostProcessingEffects` has
  Bloom, AmbientOcclusion, SSR, DoF, LightStreak, LensFlare and FXAA on (`RendererCoreBase.cs:44`);
  `AmbientOcclusion.cs:31` even has `//Enabled = false;` commented out. `Fog`, `Outline`,
  `Vignetting`, `FilmGrain`, `Dither` exist but are *not added* by default — `CreateDefault` puts
  only `ToneMap` in `ColorTransforms.Transforms`. `GraphicsCompositorHelper2D.CreateDefault` does
  call `DisableAll()`.
- **Code-only shader compilation runs with the engine's default parameters** (`Debug = true`,
  level 0 as a *parameter*), which on D3D11 means FXC's default level 1 with debug info — the same
  bytecode `CompilationMode.Release` gives; only `AppStore` changes the output, and Vulkan/D3D12
  ignore the level entirely (see correction 4). Runtime-compiled effects are cached under
  `<exe>/cache/effects/<Effect>/<hash>.sdfxbc` and, on desktop, the compiler also dumps the
  generated `_vs.hlsl`/`_ps.hlsl`, `.spv`/`.spvdis`, and a `_meta.txt` containing the parameters
  and a reproducible C# `ShaderSource` (`EffectCompiler.cs:343-375, 644-652`;
  `EffectCompilerCache.cs:51, 123-160, 248-262, 389-396`). Verified on disk: Example01's `bin`
  holds 513 such files. `dotnet clean` keeps them; `publish` copies only `data/**`.
- **Stride 4.4 has no**: spline components (grep-verified; the community `Stride.Splines` package
  is external), model LOD (`Model.cs:22-24` is a comment), UI data binding, GPU particles,
  occlusion/pipeline-statistics GPU queries (`QueryType` has exactly one member, `Timestamp`),
  decals (only Assimp's `MappingMode.Decal`), deferred *shading* (a normals-only
  `GBufferRenderStage` exists for light-probe baking, `ForwardRenderer.cs:79,236,287,495`), audio
  capture (`Microphone` is internal and throws `NotImplementedException`), heightfield colliders in
  Bepu (Bullet has them — spec 27), a navmesh crowd, terrain mesh generation, working hot reload,
  `.sdeffectlog` recording without Game Studio (`EffectSystem.EffectUsed` is internal), variance
  shadow maps (`LightShadowMapFilterTypeVariance.cs` is an empty namespace; the VSM shaders are
  orphans; only PCF 3/5/7 exists).
- **Two big features are implemented and wired to nothing.** Temporal anti-aliasing
  (`TemporalAntiAliasEffect` + `MeshVelocityRenderFeature` + `VelocityTargetSemantic`) and
  subsurface scattering (`MaterialSubsurfaceScatteringFeature` + `SubsurfaceScatteringRenderFeature`
  + `ForwardRenderer.SubsurfaceScatteringBlurEffect`) exist in `Stride.Rendering`; a tree-wide grep
  finds no compositor, test or editor code that uses either. Spike material — specs 80/81.
- **`Material.New` never stores the descriptor** (`Material.cs:36-52`, "this field is null at
  runtime"). Anything that resolves a `Material` back to its descriptor — layered materials'
  `MaterialBlendLayer` — fails with "Unable to find material" unless you assign `Descriptor` yourself.
- **`Global.Time`/`TimeStep` are uploaded every frame** by `TransformRenderFeature.cs:83-96`, so any
  `ComputeShaderClassColor` `.sdsl` in a material animates with zero C#.
- **HRTF works on Windows (XAudio2), not on Linux/macOS (OpenAL)** — correction 1. It needs
  `AudioEngineSettings.HrtfSupport` through an injected GameSettings; `useHrtf` is then a
  per-instance flag on `SoundInstance`/`CreateInstance`.
- **Runtime audio is possible after all** — not via `Sound` (Celt/ffmpeg pipeline, internals), but
  via a public `DynamicSoundSource` subclass plus the public `SoundInstance` constructor. See
  the Audio specs. `AudioLayer` is a fully public P/Invoke surface; only `AudioEngine.AudioDevice`
  and `AudioListener.Listener` are internal.
- **Navigation is managed DotRecast on Bullet colliders**, auto-registers its dynamic system but
  leaves it disabled — correction 2. The Bepu-side twin is `engine/Stride.BepuPhysics/Stride.BepuPhysics.Navigation/`
  (`RecastMeshSystem.RebuildNavMesh()`/`TryFindPath`, `RecastNavigationComponent.TryFindPath(target)`,
  `BepuNavigationBoundingBoxComponent`; its `RecastMeshSystem.cs:54` throws via
  `GetSafeServiceAs<IGameSettingsService>()` without injected settings).
- **Light probes CAN be baked at runtime.** `LightProbeGenerator.GenerateCoefficients(ISceneRendererContext)`
  (`LightProbeGenerator.cs:29`, ≥4 probes at `:119`) renders and prefilters probe cubemaps from
  game code (`Game : ISceneRendererContext`, `Game.cs:33`). But nothing registers
  `LightProbeRenderer` (correction 8), and `ComputeSphericalHarmonics.sdsl` is engine-side.
- **The toolkit already owns runtime skybox IBL.** `Stride.CommunityToolkit.Skyboxes` is a port of
  the engine's `SkyboxGenerator` (SH + GGX prefiltering). The un-mined remainder is *live-scene*
  cubemap capture (`CubemapSceneRenderer.GenerateCubemap(ISceneRendererContext, Vector3, int)`,
  `CubemapSceneRenderer.cs:35`) — a runtime reflection probe.
- **Headless means "no window", not "no GPU".** `GameContextHeadless` (public) selects the internal
  `GameWindowHeadless` (a bare `while (!Exiting) RunCallback()` loop) and `HeadlessGraphicsPresenter`
  (offscreen target, no-op `Present`). A `GraphicsDevice` is always created; on a runner with no
  GPU use `STRIDE_GRAPHICS_SOFTWARE_RENDERING=1` (WARP; Lavapipe on Vulkan). Scenes, scripts,
  rendering, UI, simulated input, Bullet, navigation and Bepu all run this way in Stride's own
  test gate. The toolkit's `GameExtensionsRunTests.cs` already does it. See spec 26 for the traps.
- **`Content.Save`/`Content.Load` work at runtime on desktop.** `/data/db` is writable
  (`ObjectDatabase.cs:40-47`); engine tests save and reload an `Entity` from a running game
  (`EntitySerializerTest.cs:19-25`). The Video spike now has a founded route.
- **No-GameSettings fallbacks** (`Game.cs:56` — `Settings` is private-set, so injection goes
  through `IGameSettingsService`): rendering 1280×720/`Level_10_0`/Linear, compilation mode not
  applied, streaming settings skipped, HRTF null, Bullet `new PhysicsSettings()`, Bepu blank
  config + warning, Bepu navigation throws. A `game.UseGameSettings(...)` helper registered before
  `Run()` would fix all of them at once.
- **`Stride.Games.AutoTesting` is new in 4.4** and is published on nuget.org; its LPIPS
  comparison lives in a separate, unpublished `Stride.ScreenshotComparator`. See Infrastructure.

---

## Full specs — the strongest candidates

Ordered roughly by (category gap × payoff ÷ effort). The current
[coverage snapshot](example-backlog.md#coverage-snapshot) has **zero** examples in Interaction,
Audio, Gameplay and Integration, one in Input (`Example01_Basic2DScene_SpawnMenu`) and five in
Performance — specs are grouped to attack the empty ones first.

### 1. `Example27_Audio_ProceduralSound` — a sound with no sound file

- **Built 2026-09-05** as `Example27_Audio_ProceduralSound` on `AudioSystemExtensions.CreateProceduralSound`; see [plans/audio-examples.md](plans/audio-examples.md).
- **Level:** Beginners (with helper) · **Category:** Audio · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Audio/DynamicSoundSource.cs` (protected ctor `:109-124`,
  `soundInstance` `:80`, `NewSources` `:22`, `FillBuffer` `:300/:324/:338`, `ExtractAndFillData`
  `:347`, `MaxNumberOfBuffers` `:156`), `SoundInstance.cs:43-60` (the public constructor),
  `Native/AudioLayer.cs`; copyable sine generator in `engine/Stride.Audio.Tests/SoundGenerator.cs`
  (MIT, 52 lines — note it uses `Sin(freq·t)` without 2π).
- **What it shows:** subclass `DynamicSoundSource`, fill PCM buffers in `ExtractAndFillData()` via
  `FillBuffer(...)`, construct `SoundInstance(engine, listener, source, 44100, mono: true, ...)`.
  The centrepiece is the circular-constructor trick: pass `null` to the base ctor, assign the
  protected `soundInstance` field afterwards, add to `NewSources` last (safe because the base ctor
  only stores the reference and the worker only touches `soundInstance` after the add — the
  engine's own `CompressedSoundSource.cs:54-68` uses the same order). No cleaner path exists:
  `SoundInstance.Source` and the parameterless ctor are internal. Play a synthesized tone; change
  pitch/waveform live. Alternative for one-shots: `AudioLayer` is public, so with `[UnsafeAccessor]`
  on the two internal handles a static PCM buffer needs no streaming worker.
- **Toolkit piece:** a new `Stride.CommunityToolkit.Audio` library with `ProceduralSoundSource`
  taking a fill callback, hiding the ctor dance. Must null-check `game.Audio.AudioEngine`:
  `AudioSystem.Initialize` swallows the native init exception (`AudioSystem.cs:53-77`) — on
  Windows the NuGet ships `libstrideaudio.dll` (XAudio2), so this bites mainly on Linux without
  OpenAL. Also `AudioSystem.OnActivated/OnDeactivated` NRE when init failed (`:139,145`, upstream).
- **Why it matters:** Audio has zero examples, this is the *only* way to get audio into a
  code-only Stride game, and it is completely undocumented.

### 2. `Example27_Audio_WavFile` — play a .wav from disk, no compiled asset

- **Built 2026-09-05** as `Example27_Audio_WavFile` on `game.Audio.LoadWav` / `WavSound.CreateInstance`, in-memory PCM only.
- **Level:** Beginners · **Category:** Audio · **Complexity:** 3 · **Verdict:** both
- **Sources:** as above (`FillBuffer(byte[], int, BufferType)` `:338`; `BufferType.EndOfStream/EndOfLoop`
  in `AudioLayer.cs:40-46`), contrasted against `engine/Stride.Assets/Media/SoundAssetCompiler.cs`
  (the ffmpeg/Celt pipeline this sidesteps).
- **What it shows:** a ~80-line `WavSoundSource : DynamicSoundSource` parsing a RIFF/WAVE header
  (fmt + data, 16-bit PCM) from a `FileStream` and streaming it. This is the toolkit's founding
  pattern — "load files at runtime instead of the asset pipeline" — applied to the one subsystem
  where it currently has no answer. Note XAudio2 applies `Pan` only to mono sources
  (`XAudio2.cpp:1514-1516`).
- **Toolkit piece:** `game.LoadSound(path)` in the same Audio library.

### 3. `Example27_Audio_Spatial` — 3D positional audio, honestly

- **Built 2026-09-05** as `Example27_Audio_Spatial`. The listener fix went the other way from the spec: `AttachListener` puts an `AudioListenerComponent` on the camera and reads its internal listener with `[UnsafeAccessor]`, so the engine moves it; `SoundEmitterScript` does the emitter side. HRTF is a runtime toggle (instance recreated).
- **Level:** Intermediate · **Category:** Audio · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Audio/AudioEmitter.cs`, `AudioListener.cs` (public
  `AudioListener(AudioEngine)` ctor `:19` leaves Forward/Up zero; internal `Update()` `:135`),
  `engine/Stride.Engine/Audio/AudioListenerProcessor.cs:62-78`, `Native/XAudio2.cpp:1320,1368,1768`
  (HRTF APO per source), `OpenAL.cpp:378-410` (Linux, ignores HRTF).
- **What it shows:** `SoundInstance(..., spatialized: true)` + `Apply3D(AudioEmitter)`
  (`SoundInstance.cs:194-205`) as a sound orbits the camera. The teaching core is the gotcha
  chain: `AudioListenerComponent.Listener` is internal (`:27`), so runtime sounds must use
  `AudioEngine.DefaultListener` (public field, `AudioEngine.cs:21`) — which nothing ever moves —
  so the fix is transforming the emitter's world position into camera space each frame (a nice
  coordinate-space lesson). Spatialization requires mono; `Pan` and 3D are mutually exclusive
  (OpenAL writes both to `AL_POSITION`; XAudio2 emitter is `ChannelCount = 1`).
- **Toolkit piece:** a `SpatialSoundEmitter` script component doing the listener-space transform.
- **HRTF, correctly stated:** works on Windows once `AudioEngineSettings.HrtfSupport` is injected
  via GameSettings and the instance is created with `useHrtf: true`; a no-op on Linux/macOS. Demo
  it behind a toggle, with the platform caveat.
- **Ready-made scenarios** in the dead `Stride.Audio.Tests/TestAudioSystem.cs` (Doppler sweep
  `:537`, attenuation `:573`, orbit `:611`, split-ear `:220-222`).

### 4. `Example28_Input_VirtualButtons` — rebindable actions, chords and synthetic axes

- **Level:** Beginners · **Category:** Input · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Input/VirtualButton/` (`VirtualButton.cs` + `.Keyboard/.Mouse/
  .GamePad`, `VirtualButtonBinding.cs`, `VirtualButtonConfig(Set).cs`, `VirtualButtonGroup.cs`,
  `VirtualButtonTwoWay.cs`); consumed at `InputManager.cs:98,494-566,994-1029`. Worked usage:
  `samples/Tutorials/CSharpBeginner…/VirtualButtonsDemo.cs:20-50`; `VirtualButton.Find("Keyboard.a")`
  string form pinned in `Stride.Input.Tests/TestInput.cs:347-364`.
- **What it shows:** Stride ships a full action-mapping layer nobody uses. `"Jump"` bound to
  Space; `VirtualButtonTwoWay(A, D)` producing the *same* analog float as a gamepad stick;
  `VirtualButtonGroup` chords (Ctrl+S); a runtime rebind screen. The config-set index is the
  player number — local multiplayer for free. Use `GetVirtualButtonValue` —
  `GetVirtualButton` is `[Obsolete]` (`InputManager.cs:494`).
- **Gotchas found:** `VirtualButton.Pointer` exists but is not registered in `Find`, and its
  Pressed/Released read `DownPointers` (`VirtualButton.Pointer.cs:142-150`); `VirtualButtonTwoWay.Is*`
  are always false (`:53-66`). Upstream-worthy.
- **Toolkit piece:** an `InputActions` wrapper; the toolkit camera controllers could be
  re-expressed on top of it (they currently hand-roll `Input.IsKeyDown` and gamepad state — see
  also the gamepad-helpers row already in the [backlog](example-backlog.md), which the samples doc
  seconds with four identical copies of `InputManagerExtensions`).

### 5. `Example28_Input_Gestures` — tap, drag, flick, long-press (with a mouse)

- **Level:** Intermediate · **Category:** Input · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Input/Gestures/` (configs, events, `GestureRecognizer` is internal);
  hookup at `InputManager.cs:87-88,103,154,736-759`. Worked usage: `samples/Input/TouchInputs…/TouchInputsScript.cs:72-76,152-201`
  (all five configs) and the SpaceEscape swipe classifier.
- **What it shows:** add a `GestureConfig` to `Input.Gestures`, read `Input.GestureEvents`. The
  key verified fact: `MouseDeviceState` emits pointer events with `Id = 0` from the **left
  button** (`MouseDeviceState.cs:121,139`), so tap/drag/flick/long-press **work on desktop with a
  mouse** — only the two-finger composite (pinch/rotate) needs touch (or two `PointerSimulated`
  ids). Demo: drag to pan, flick to throw, long-press to delete, on the existing 2D scene. Note
  configs freeze once added, and coordinates are normalised to `[0,1] × [0, SurfaceSize.Y/SurfaceSize.X]`.
  Curious fact: the stock `BasicCameraController` gates its gestures behind
  `!Platform.IsWindowsDesktop`, so the toolkit's derived controller never exercises them.

### 6. `Example29_PickingNoPhysics` — raycasts without a physics engine

- **Level:** Intermediate (Beginners variant possible) · **Category:** Interaction ·
  **Complexity:** 5 · **Verdict:** both
- **Sources:** `core/Stride.Core.Mathematics/CollisionHelper.cs` (1575 lines, 43 public static
  methods — the file header lists Segment/Capsule/Cone/Torus tests that were never implemented),
  `Ray.cs:69-238` and `Plane.cs:216-305` (instance wrappers), `BoundingBox/Sphere.cs`,
  `IIntersectableWithRay.cs` (implemented by `BoundingBox`, `BoundingSphere`, `Plane`).
- **What it shows:** every toolkit picking example today requires Bepu. `CollisionHelper` has the
  whole classical matrix — `RayIntersectsTriangle` (Möller–Trumbore, `:593/:689`),
  `RayIntersectsBox/Sphere/Plane`, `RayIntersectsRectangle(ref Ray, ref Matrix, ref Vector3, int normalAxis, out Vector3)`
  (`:710`, oriented quad — click a world-space panel), closest-point and distance families, and
  `GetNearestHit<T>(IEnumerable<T>, ref readonly Ray, out T, out float, out Vector3) where T : struct, IIntersectableWithRay`
  (`:1551`). The hit result is the struct, so entity identity needs a wrapper. Demo:
  hover-highlight over bounding boxes, then refine to exact triangles read from the mesh
  (`VertexBufferHelper.CopyAsTriangleList` de-indexes for you — spec 22).
- **Toolkit piece:** a `RayPicking` helper in `Stride.CommunityToolkit` pairing the existing
  `ScreenToWorldRay` camera extensions with `GetNearestHit` over entity bounds; companion to the
  existing `RaySegment` type.
- **Cross-link:** frame as the deliberate opposite of `Example14_Raycast`. The SpaceEscape sample's
  physics-free AABB runner (samples doc #69) is the gameplay-scale demo.

### 7. `Example30_TransformGizmos` — finish the gizmo family, interactively

- **Level:** Advanced · **Category:** Interaction · **Complexity:** 9 · **Verdict:** toolkit
  first, then example
- **Sources:** `editor/Stride.Assets.Presentation/AssetEditors/Gizmos/TransformationGizmo.cs`
  (503 lines — the drag machinery), `AxisTransformationGizmo.cs`, `RotationGizmo.cs` (254),
  `ScaleGizmo.cs` (325), `EditorGameEntityTransformService.cs` (orchestration + snapping),
  `EditorGameMouseServiceBase.cs:27` (mouse arbitration).
- **What it shows:** the toolkit's ported `TranslationGizmo` is display-only. The editor base
  class holds everything people get wrong: screen-constant sizing
  (`SizeFactor · (defaultSize/backBufferHeight) · 2·tan(fov/2) · distance`, `:230`, rows
  renormalised at `:250-260` so non-uniform parent scale survives), drag-plane construction per
  axis mode, an 8-pixel drag threshold (`:24`), a 2.5° grazing-ray guard (`:26`), and *absolute*
  deltas (returning the mouse to its origin restores the transform exactly). The rotation gizmo
  hit-tests its torus as 20 oriented boxes around the circle (`RotationGizmo.cs:65`); the scale
  gizmo maps drag distance through `exp(t)` so scale can never go negative (`ScaleGizmo.cs:282`).
  Snapping everywhere is just `MathUtil.Snap` (`:586-643`) — there is no snapping subsystem.
- **Toolkit piece:** extend the ported gizmos into an interactive T/R/S set; the editor's one-line
  mouse arbitration (`IsMouseAvailable => services.All(x => x == this || !x.IsControllingMouse)`,
  in `EditorGameMouseServiceBase`, not the transform service) should come along as an
  `InputArbiter` helper — it retrofits into Example07/08/14 and both camera controllers, all of
  which currently fight over the mouse ad hoc.

### 8. `Example31_SaveGame` — save/load with the engine's own machinery

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 5 · **Verdict:** both
- **Sources:** `core/Stride.Core.IO/VirtualFileSystem.cs:85-94,141,151` (+ public
  `ZipFileSystemProvider.cs`, `DirectoryWatcher.Desktop.cs`), `core/Stride.Core.Serialization/IO/DictionaryStore.cs`
  / `Store.cs:13,47,52,152-261`; worked example `Stride.Core.Tests/TestStore.cs:12-81` (two
  stores on one file merging concurrent writes with `UseTransaction`/`Save()`/`LoadNewValues()`).
- **What it shows (reframed):** the built-in `/roaming`, `/local`, `/cache` mounts are **folders
  next to the executable on desktop** (`PlatformFolders.cs:80-134`; only Android/iOS/UWP get
  per-user folders) — so the example mounts its own:
  `VirtualFileSystem.MountFileSystem("/save", Path.Combine(LocalApplicationData, ...))`. Then
  `DictionaryStore<K,V>` over a VFS stream gives a transactional, append-only save store where
  any `[DataContract]` type (including `Vector3`, `Quaternion`) round-trips through Stride's own
  binary serializer — provided the assembly processor runs (`StrideAssemblyProcessor` is on by
  default for NuGet consumers, `Stride.Core.targets:61`; verify once for file-based apps).
  Second act: mount a folder or .zip as `/mods` and hot-reload with `DirectoryWatcher` (~1 s
  latency; paths lower-cased). Side-notes: `SerializerExtensions.Clone<T>` as a one-line deep
  clone; `ObjectId.FromObject` as a cheap content hash; `ListStore<T>` as the sibling store.
- **Alternative route:** spec 28 (`Example49_SceneSaveLoad`) uses `Content.Save` for whole
  entity graphs; this spec is the small-POCO version.
- **Overlap:** `Example07_CubeClicker` saves clicks with its own code (third-party `NexVYaml` —
  `Stride.Core.Yaml` is runtime-usable, depends only on `Stride.Core.Reflection`) — cross-link,
  don't merge.

### 9. `Example32_ProceduralAnimation` — an AnimationClip built in code

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Animations/AnimationClip.cs:22-98`, `AnimationCurve.cs:80,88`,
  `KeyFrameData.cs:16`, `CompressedTimeSpan.cs:12,16`; path resolution `Updater/UpdateEngine.cs:87-92,183-209`
  and `EntityChildPropertyResolver.cs:31-44` (the text before the last `.` inside `[...]` is
  looked up as a DataContract alias, so `.Key` is decorative; bare segments resolve child entities
  by name); real paths in `Stride.Assets.Models/ImportModelCommand.Animation.cs:83,90,253`.
  Worked usages: `samples/Graphics/AnimatedModel…/AnimationScript.cs:14-89` (dead code in the
  sample — a 14-key sun colour + rotation day/night clip, with the type-qualified path syntax
  `"[LightComponent.Key].Type.(ColorLightBase-AQN)Color.(ColorRgbProvider-AQN)Value"`),
  `Stride.Engine.Tests/AnimationChannelTest.cs:43-102` (discontinuities via duplicate key times,
  `Optimize()`), `AnimatedModelTests.cs:71-79` (an `AnimationCurve<object>` on
  `"[ModelComponent.Key].Model"` swaps the model itself).
- **What it shows:** the animation system needs no FBX. Build `AnimationCurve<Vector3>` keyframes
  with `CompressedTimeSpan`, `clip.AddCurve("[TransformComponent.Key].Position", curve)`, add to
  `AnimationComponent.Animations`, `Play`/`Crossfade`, `await animComponent.Ended(playing)`. No
  animation example exists anywhere in the toolkit today. Sequel: the samples doc's #72 puts
  `IBlendTreeBuilder` (three real usages in the starters) over procedural clips, zero assets.
  Sidebar: `IComputeCurve<T>` (spec 32) for parameter curves that aren't clips.
- **Toolkit piece:** an `AnimationClipBuilder` hiding `CompressedTimeSpan` and the path strings.

### 10. `Example33_SplinePath` — Catmull-Rom waypoint paths

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 4 · **Verdict:** both
- **Sources:** `core/Stride.Core.Mathematics/Vector3.cs:756-821` (`CatmullRom`, `Hermite`),
  `Quaternion.cs:1183-1230` (`Slerp`, `Squad`), `:695` (`LookRotation` — engine version does not
  normalise and NaNs on parallel/zero input; toolkit `MathUtilEx.LookRotation` handles it),
  `:1168` (`RotateTowards`), `MathUtil.cs:392,406` (`ExpDecay`, `SmootherStep`).
- **What it shows:** Stride has no spline *component* (verified), but the spline *math* is all
  there. Move an entity through six waypoints, orient along the tangent, draw the curve with the
  MeshLine technique. Deserves a sidebar: `MathUtil.ExpDecay` as the correct, framerate-independent
  replacement for the `Lerp(a, b, 0.1f)`-per-frame bug everyone writes — the samples tree ships
  both the bug (TPP camera) and the fix (`FindAndAttachCameraComponent.cs:23`) three folders apart.
- **Toolkit piece:** `CatmullRomPath` / `SplineFollower` (arc-length sampling) in
  `Stride.CommunityToolkit.Mathematics`. The engine-blessed follow mechanism is a custom
  `TransformLink` (inventory).

### 11. `Example34_NavigationPathfinding` — a navmesh with zero native code (re-targeted)

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 6 · **Verdict:** both
- **Sources:** Bepu route (preferred): `engine/Stride.BepuPhysics/Stride.BepuPhysics.Navigation/`
  (`Processors/RecastMeshSystem.cs:24,69,282,306`, `Components/RecastNavigationComponent.cs:11,66`,
  `RecastNavigationProcessor.cs:37`, `BepuNavigationBoundingBoxComponent`; NuGet packaging
  unverified). Bullet route: `engine/Stride.Navigation/NavigationMeshBuilder.cs` (`Add(new StaticColliderData { Component = bulletCollider })`,
  `Build`; shape walk `:461-606`; runs on `Dispatcher.ForEach`), `NavigationComponent.cs`,
  `Processors/RecastNavigationMesh.cs:14-75` (public `TryFindPath`/`Raycast` without a component).
  Defaults: build settings `0.2/0.3/32/2/20/12/1.3/6/1`, agent `Height 1, MaxClimb 0.25, MaxSlope 45°, Radius 0.5`.
  Worked usages: `samples/Templates/TopDownRPG…/PlayerController.cs:164-269` (re-path only when
  the target moved, waypoint advance by projection, corner slow-down),
  `Stride.Navigation.Tests/PlayerController.cs:86-199`, `DynamicBarrierTest.cs:84-123`.
- **What it shows:** 4.4's navigation is pure managed DotRecast — but `Stride.Navigation`'s only
  input is Bullet `StaticColliderComponent`s (its raw-mesh builder is internal; upstream ask), so
  in a Bepu-first toolkit the example builds on `Stride.BepuPhysics.Navigation`. The dynamic
  variant: add a `NavigationBoundingBoxComponent`, then set the **auto-registered but sleeping**
  `DynamicNavigationMeshSystem.Enabled = true` (or inject `NavigationSettings.EnableDynamicNavigationMesh`)
  — the TopDownRPG template ships `EnableDynamicNavigation.cs` for exactly this. Path drawn with
  DebugShapes over procedurally placed obstacles; the click-to-move scenario is the samples doc's #62.
- **Cross-link:** the backlog's `Stride.BepuPhysics.Navigation` row is now the *same* item.

### 12. `Example35_CodeOnlyPrefabs` — clone a template entity

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 3 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Engine/Design/EntityCloner.cs:20-33,57,83,102-173`, `Prefab.cs:30`,
  `EntityExtensions.cs:19` (`entity.Clone()`), `CloneSerializer.cs:14-70`; semantics pinned in
  `Stride.Engine.Tests/TestEntity.cs:131-203`; usages `samples/Tutorials/CSharpBeginner…/CloneEntityDemo.cs:26-54`.
- **What it shows:** build one entity in code, `Clone()` it a hundred times — a deep,
  serializer-based clone that duplicates children and components while *sharing* the `Model`
  (and therefore its materials; `Material` has no `CloneSerializer`, `Material.cs:16-18`, so
  `ModelComponent.Materials` overrides are deep-cloned). `CloneContext.MappedObjects` is set only
  by the private `Clone<T>` (`EntityCloner.cs:132`) — both public entry points pass null, so
  reference substitution is not available from outside; say so. Teach the contrast explicitly:
  clone = many entities, instancing (Example21) = one draw call, static batching (spec 30) = one
  mesh. Caveat: non-serializable script state resets.

### 13. `Example36_EventBus` — EventKey/EventReceiver pub-sub

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Engine/Events/EventKey.cs:24-49`, `EventReceiver.cs:55-162`
  (`GetAwaiter`, `Count`, `Reset` too), `EventReceiverBase.cs:36-54` (capacity 1 at `:38`,
  drain-on-connect at `:53-54`), `EventReceiverOptions.cs:17-23`. Worked usages: every 3D starter's
  `PlayerInput` (samples doc #60), JumpyJet's four global keys (#66), Starbreach's Activator system.
- **What it shows:** Stride's best-kept secret — decoupled script communication. Broadcast from
  one script; `await receiver.ReceiveAsync()` in an AsyncScript, `TryReceive`/`TryReceiveAll` in
  a SyncScript, `EventReceiver.ReceiveOne(...)` as a select over several streams. Teach the two
  gotchas: default mode keeps only the latest event (`Buffered` makes it a queue), and the
  receiver constructor drains one stale event on connect. And the wart every starter carries:
  `static` keys ("TODO should not be static") — make them instance members.

### 14. `Example37_EntityProcessors` — write a system, not a hundred scripts

- **Level:** Intermediate · **Category:** Performance · **Complexity:** 6 · **Verdict:** example
- **Sources:** new API: `engine/Stride.Engine/Engine/FlexibleProcessing/IComponent.cs:12`
  (`IComponent<TProcessor, TThis>` with nested `IProcessor`), `ProcessorManager.cs:87-157` (lazy
  create on first component, teardown at zero; usage `Stride.Engine.Tests/TestEntityManager.cs:527-620`);
  classic API: `Engine/EntityProcessor.cs:51,95,105,225`, `Design/DefaultEntityComponentProcessorAttribute.cs:24`,
  `IProcessorBase.cs:12` (`ExecutionMode`), `IUpdateProcessor.cs:14`/`IDrawProcessor.cs:14`
  (`Order`, evaluated once); bare-`EntityManager` usage `TestEntityManager.cs:27-128,629-754`.
- **What it shows:** both processor APIs, old vs new. FlexibleProcessing is the pattern Bepu's
  `ISimulationUpdate` is built on as an *interface marker*
  (`ISimulationUpdate.cs:15`) — the toolkit ships an example *consuming* it but nothing teaching
  *authoring*. Measure N components in one batched `Update` against N `SyncScript`s — the direct
  sequel to `Example23_SyncScriptStress`. Cover `ExecutionMode` (also why scripts are inert in
  `Preview`/`Editor` modes), `Order`, and required-component declarations on the classic API.
- **Gotcha to use as a teaching moment:** `ProcessorManager.cs:95,97` builds its profiling keys
  from `GetType().Name` — so every flexible processor shows up as "ProcessorManager" in the
  profiler (upstream). Use your own `ProfilingKey` for the measurement.

### 15. `Example38_ProfilingTrace` — see your frame in Perfetto

- **Level:** Intermediate · **Category:** Performance · **Complexity:** 5 · **Verdict:** example
  (the toolkit already has the aggregator)
- **Sources:** `core/Stride.Core/Diagnostics/Profiler.cs:148,164,213` (`Subscribe()` returns a
  `ChannelReader<ProfilingEvent>`; events flow only for *enabled* keys; `MinimumProfileDuration`
  1 µs at `:123`), `ProfilingKey.cs`, `ChromeTracingProfileWriter.cs:11-86` (`Start(path, indent)`/`Stop()`;
  GPU events on a synthetic "GPU" thread from `ProfilingState.BeginGpu`);
  `engine/Stride.Engine/Profiling/GameProfilingSystem.cs:393` (`EnableProfiling(bool excludeKeys, params ProfilingKey[])`
  — it also forces vsync off, `:404-408`); worked example `Stride.Core.Tests/TestProfiler.cs:117-188,289-327`.
- **What it shows:** declare your own `ProfilingKey`, wrap a hot loop with
  `using (Profiler.Begin(key))`, watch it appear in the built-in overlay filtered to your keys —
  then press a key, capture five seconds with `ChromeTracingProfileWriter`, and drop the JSON into
  Perfetto to see engine phases, **GPU timings** (enable the renderer keys) and your keys
  interleaved on a flame chart. Nothing in the engine docs mentions the trace writer.
- **Toolkit piece:** `ProfilerScope` + a trace-capture toggle. The live-stats aggregator already
  exists: `src/Stride.CommunityToolkit.ImGui/DebugTools/PerfMonitorHelpers.cs:22` subscribes to
  `Profiler`. `PerformanceReport` is `[Conditional("DEBUG")]`. Two samples carry conflicting
  `GameProfiler` hotkey schemes — pick one.

### 16. `Example39_ParallelDispatcher` — the engine's parallel-for

- **Level:** Advanced · **Category:** Performance · **Complexity:** 6 · **Verdict:** example
- **Sources:** `core/Stride.Core/Threading/Dispatcher.cs` (`ForBatched<TJob>` `:54`, `For` `:179`,
  `For<TLocal>` `:193`, `ForEach` over arrays/lists/collectors/dictionaries `:220-355`, `Sort` `:357`;
  `MaxDegreeOfParallelism` settable, seeded from `STRIDE_MAX_PARALLELISM` `:23-33`; worker
  exceptions rethrown on the caller `:94-96`), `ThreadPool.cs`, `ConcurrentCollector.cs:62`,
  `PooledAttribute.cs:10` (rewritten by the assembly processor's `DispatcherProcessor`, which runs
  for NuGet consumers).
- **What it shows:** `Dispatcher.For`/`ForEach`/`ForBatched` share the engine's worker pool, so —
  unlike `Parallel.For` — game code doesn't oversubscribe against render and physics jobs. Update
  50k transforms serially, with `Parallel.For`, and with `Dispatcher.For`, measured with the
  `ProfilingKey` from spec 15; flip `MaxDegreeOfParallelism` live for the "1 vs N threads" knob.
  Explain `[Pooled]` delegate pooling and `ConcurrentCollector<T>`. The toolkit already calls
  `Dispatcher.For` in DebugShapes (`DebugPrimitiveRenderer.cs:129`) without a word of explanation.

### 17. `Example40_PostEffects` — bloom, fog, vignette and friends, six lines each

- **Level:** Beginners → Intermediate · **Category:** Rendering · **Complexity:** 4 ·
  **Verdict:** both — **and it starts with a toolkit bug fix**
- **Sources:** `engine/Stride.Rendering/Rendering/Images/PostProcessingEffects.cs:42-50,181-195`;
  `Images/Outline/Outline.cs` and `Images/Fog/Fog.cs` (~97 lines each, both need depth);
  `ColorTransforms/Vignetting/Vignetting.cs:12`, `ColorTransforms/Noise/FilmGrain.cs:10`,
  `Dither/Dither.cs:9` (all `: ColorTransform`, fused into one pass by `ColorTransformGroup.cs:62-69`);
  `engine/Stride.Engine/Rendering/Compositing/ForwardRenderer.cs:84`; driving the effects
  *outside* a compositor: `Stride.Graphics.Tests.10_0/TestImageEffect.cs:43-95`.
- **What it shows:** `((PostProcessingEffects)((ForwardRenderer)compositor.SingleView).PostEffects)`
  — two casts, because `ForwardRenderer.PostEffects` is typed `IPostProcessingEffects` — then flip
  switches: bloom, ambient occlusion, depth of field, screen-space reflections, the depth-based
  `Fog` and `Outline` nobody knows exist, and the `ColorTransform`s (vignette, film grain, dither)
  that must be **added** to `ColorTransforms.Transforms` (only `ToneMap` is there by default) and
  then cost nothing extra. A keyboard-cycled tour with the same scene. `Example13_MeshOutline`
  (per-object) vs the full-screen `Outline` makes a good contrast pair. Also: HDR auto-exposure via
  `LuminanceEffect` and the nine tonemap operators (ACES, Drago, Exponential, Hejl2, HejlDawson,
  Logarithmic, MikeDay, Reinhard, U2Filmic); `LightStreak` (`StreakCount`, `IsAnamorphic`),
  `LensFlare`, `Bloom.Afterimage` (off by default), `BrightFilter.Threshold`, the three DoF bokeh
  techniques (`CircularGaussian`/`HexagonalMcIntosh`/`HexagonalTripleRhombi`, `AutoFocus`),
  `LocalReflections.DebugMode`, `ToneMap.AutoExposure/TemporalAdaptation`, `FXAAEffect.Quality`, and
  `MSAAResolver.FilterType` (ten filters; the one effect whose `Enabled` is hard-wired true).
- **Toolkit piece:** a fluent post-fx configurator on the existing `AddGraphicsCompositor` path —
  which must also fix `AddCleanUIStage()` (see toolkit-side findings: it replaces `PostEffects`
  with a bare `new PostProcessingEffects { ... }` and therefore silently runs SSAO, SSR, bloom,
  light streaks, lens flare and FXAA in every example that calls it).
- **Built 2026-09-03** as `Example40_PostEffects` (`ConfigurePostEffects`/`GetPostEffects` on both
  `GraphicsCompositor` and `Game`; twelve keys, first set on at start, `DebugOverlay` state list).
  Two scene lessons worth reusing: the default directional light backlights a camera on the +Z
  side, so put the camera at −Z looking along +Z (then +X is screen-left); and a floor at
  `specular: 1` turns metallic and mirrors the skybox's dark underside as a hard black band —
  `specular: 0.5, microSurface: 0.85` is glossy enough for SSR without that.

### 18. `Example41_Shadows` — tuning the shadows you already have (downgraded)

- **Level:** Beginners · **Category:** Rendering · **Complexity:** 3 · **Verdict:** example
  section or doc page
- **Sources:** `engine/Stride.Rendering/Rendering/Lights/LightShadowMap.cs:22,33,70,88-89`
  (`Enabled` default **false**, `Size = Medium`, bias 0.01/10), `LightDirectionalShadowMap.cs:36-127`
  (cascade count, `PartitionManual`/`PartitionLogarithmic` with `PSSMFactor`, `StabilizationMode`
  None/ProjectionSnapping/ViewSnapping, `DepthRange.IsAutomatic/IsBlendingCascades`),
  `LightShadowMapFilterTypePCF.cs:38` (the only filter — the variance type is an empty namespace),
  `MaterialInstance.IsShadowCaster`/`ModelComponent.IsShadowCaster` (per-slot and per-entity
  opt-out, checked by `ShadowMapRenderStageSelector.cs:22-29`), `DitheredShadows` on the
  transparency features (transparent objects cast Bayer-dithered shadows), renderers in
  `Rendering/Shadows/`. Game Studio's
  default-scene numbers: `Stride.Assets/Entities/SceneAssetFactories.cs:44-48` (sun intensity 1.0,
  X −30°/Y −180°, `Large`, PCF 5×5).
- **What it shows:** `LightShadowMap.Enabled` defaults to false — but the toolkit's
  `AddDirectionalLight(enableShadows: true)` already sets `Enabled`, `Large`, PCF 5×5,
  `PartitionLogarithmic` (`GameExtensions.cs:435-455`), so the FAQ is answered. What remains is
  the tuning surface as plain settable POCOs: cascade count and partitioning, PCF filter size,
  `BiasParameters` vs peter-panning, and `Debug` — which **tints receivers one colour per cascade**
  (`TCascadeDebug` shader generic), it does not draw the shadow map. Point/spot variants
  (`CubeMap` vs `DualParaboloid`). Light shafts (inventory) are the spectacular sequel.
- **Toolkit piece:** `ConfigureShadows(...)` on the light, not `EnableShadows()`.

### 19. `Example42_ScreenEffectShader` — your own full-screen shader, the easy way

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 6 · **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/Images/ImageEffectShader.cs:51,119-139`
  (`ImageEffectShader(effectName, delaySetRenderTargets)`; inputs auto-bound to `Texture0..9`
  with texel sizes, throws past 10, cube inputs → `TextureCubes`), `ImageEffect.cs:77-119,332,423-461`
  (`NewScopedRenderTarget2D` is **protected** — subclass only; from a scene renderer use
  `PushScopedResource(context.Allocator.GetTemporaryTexture2D(...))`); `Outline.cs`/`Fog.cs` as
  reference wrappers; simplest blit path: `engine/Stride.Graphics/GraphicsDeviceExtensions.cs:28-110`
  (`DrawTexture`/`DrawQuad`, a full-screen *triangle*; `GetSharedWhiteTexture()`); one level lower
  still: `samples/Graphics/CustomEffect…/CustomEffectRenderer.cs:24-65` (a `SpriteBatch` with its
  own `EffectInstance`, 69 lines) and `Stride.Graphics.Tests/TestCustomEffect.cs:37-60`
  (`DynamicEffectInstance` + `[Link]`-bound custom keys + `DrawQuad(effectInstance)`).
- **What it shows:** `new ImageEffectShader("MyShader")` — one `.sdsl`, `SetInput`/`SetOutput`/
  `Draw`. Open with the `DrawTexture`/`DrawQuad` one-liners before introducing the class. The
  screen-space sibling of `Example13_RootRendererShader`'s mesh effect. Sidebar: where the
  compiled shader and its dumped HLSL end up (`cache/effects/`), and what the compilation modes
  really do on each graphics API (correction 4).
- **Toolkit piece:** the toolkit already has `TextureCanvas.Apply(ImageEffect, params Texture?[]?)`
  (`Rendering/Utilities/TextureCanvas.cs:474`) — start the `ScreenEffect` compositor helper there.

### 20. `Example43_RenderToTexture` — cameras on monitors, minimaps, PiP

- **Level:** Advanced · **Category:** Rendering · **Complexity:** 7 · **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/Compositing/RenderTextureSceneRenderer.cs:7,42,48`
  (temp depth from `context.Allocator`; the Aug-2026 fix
  `ResourceBarrierTransition(RenderTexture, BarrierLayout.ShaderResource)` before sampling — the
  4.4 requirement, also at `Stride.Graphics.Tests/TestRenderToTexture.cs:100,110,115`),
  `DelegateSceneRenderer.cs:10-23`, `SceneRendererCollection.cs`; the contract:
  `RenderContext.SaveRenderOutputAndRestore()` / `SaveViewportAndRestore()` (`RenderContext.cs:145,153`),
  `RenderDrawContext.PushRenderTargetsAndRestore()`, `PushRenderViewAndRestore(RenderView)` (`:162`)
  for a second camera; the production-quality worked example is the editor's camera preview,
  `editor/.../EntityHierarchyEditor/Game/EditorGameCameraPreviewService.cs` (336 lines, two
  cooperating renderers, temp textures from `GraphicsContext.Allocator`, `ReleaseReference`);
  all-code compositor composition: `Stride.Graphics.Tests/TestSharedStageMultipleOutputs.cs:39-86`;
  screenshot half: `Texture.ToStaging`/`GetDataAsImage`/`Save` (`Texture.cs:1561,1681,1694-1752`).
- **What it shows:** render a second camera into a texture, show it on an in-world quad and as a
  screen-corner inset; save a PNG screenshot on demand. Teach the collect-phase contract
  (`SaveRenderOutputAndRestore` — the step everyone forgets; JumpyJet's `JumpyJetRenderer.cs:55-117`
  is the smallest worked example) and `DelegateSceneRenderer` as the 20-line way to inject draw
  code into the compositor. Screenshot gotcha from AutoTesting's `ForceAlphaOpaque`
  (`ScreenshotTestRunner.cs:281-295`): DXGI ignores back-buffer alpha, PNG viewers don't — the
  toolkit's `capture-screenshots.cs` already applies it. The editor's thumbnail system
  (`editor/Stride.Editor/Thumbnails/ThumbnailGenerator.cs`) supplies known-good framing and
  lighting constants for an item-icon variant.
- **Extends** `Example09_Renderer` rather than replacing it.

### 21. `Example44_GpuPicking` — pixel-perfect picking, the Game Studio way

- **Level:** Advanced · **Category:** Rendering (secondary Interaction) · **Complexity:** 8 ·
  **Verdict:** both
- **Sources:** `editor/Stride.Assets.Presentation/SceneEditor/PickingSceneRenderer.cs:55-118`
  (245 lines), `PickingRenderFeature.cs:12-41`, `EditorGameEntitySelectionService.cs:526-561`
  (the hookup recipe); shader `engine/Stride.Rendering/Rendering/Utils/PickingShader.sdsl` +
  `Picking.sdfx` (engine-side, so fully portable); the 3-MRT alternative `ModelComponentPickingShader.sdsl`.
- **What it shows:** how the editor *actually* selects objects — no raycasts. A render stage
  named — literally — `"Picking"` writes `(componentId, meshIndex.materialIndex)` into an
  `R32G32_Float` target, with the instance ID packed into the fraction
  (`PickingData.x + min(InstanceID,1023)/1024`); a **1×1 scissor rectangle** at the cursor
  rasterises a single pixel, `CopyRegion` + `GetData` on a persistent 1×1 staging texture reads it
  back (synchronous stall — `ImageReadback<T>` is the non-blocking sequel). Works on skinned
  meshes and GPU instances (instance→entity is your own map; `InstancingEntityTransform.GetInstanceAt`
  is internal) where collider raycasts can't. Sprites are picked by a *separate* path: `SpriteRenderFeature.cs:84`
  string-compares the stage name `"Picking"` and writes the runtime id (`:180-184`) — they do not
  go through `PickingRenderFeature`. Completes the picking triptych: Bepu raycast (Example14) /
  math ray (spec 6) / GPU ID buffer.
- **Porting recipe:** `RenderStage("Picking", "Picking")` + filter,
  `meshRenderFeature.RenderFeatures.Add(new PickingRenderFeature())`, a stage selector with
  `EffectName = "<ForwardEffect>.Picking"` (verify the child-effect composition against
  `StrideForwardShadingEffect` at build), renderer appended after the main one. Public:
  `RuntimeIdHelper.ToRuntimeId`, `CreateDrawCBufferOffsetSlot`, `CreateObjectKey<T>`. Copy:
  `PickingObjectInfo` (internal) and `EntityPickingResult` (editor).
- **Toolkit piece:** a `GpuPicker` helper.

### 22. `Example45_MeshVertexReadback` — read your mesh back at runtime

- **Level:** Intermediate · **Category:** Shapes · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Graphics/VertexBufferHelper.cs:14-23,83-119,232,314,376`
  (`Copy<PositionSemantic, Vector3>`, converters for `Half2/4`, `UShort4`, `Byte4`, `Color`;
  `CopyAsTriangleList` de-indexes), `IndexBufferHelper.cs`, `MeshExtension.cs:27,43,92-133`
  (`AsReadable`; **`TryFetchBufferContent` falls back to a GPU readback the code itself flags
  "will most likely break on non-dx11 APIs"** — attach `BufferData` when building runtime meshes
  so the readback never runs), `Semantics/ConcreteSemantics.cs:59`; companions
  `engine/Stride.Rendering/Extensions/` (the `Stride.Extensions` namespace, 11 files:
  `GenerateTangentBinormal`, `MergeDrawData`, `ReverseWindingOrder`, `ComputeBounds`,
  `TransformBuffer`, `SplitMeshes`, `CompactIndexBuffer`, `GenerateIndexBufferAEN`).
- **What it shows:** the most-asked Stride question — "how do I read my mesh's vertices?"
  `binding.AsReadable(Services, out var helper, out var count)` then
  `helper.Copy<PositionSemantic, Vector3>(span)` with automatic format conversion. The API even
  carries runnable `<example>` blocks in its XML docs and is effectively invisible. Demo: deform
  or explode a procedural mesh, or derive a picking mesh for spec 6. Close with the
  `Stride.Extensions` mesh-surgery namespace (mis-named, hence undiscovered): generate tangents so
  normal maps work on procedural geometry, merge draws, reverse winding — and static batching
  (spec 30) as the big consumer.
- **Reads** what the Example05 family writes.

### 23. `Example12_Particles_ForceFields` — particles, part two

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 5 · **Verdict:** example
- **Sources:** `engine/Stride.Particles/Updaters/UpdaterForceField.cs:48-153,180-186`,
  `UpdaterCollider.cs:40-95,171-177` (namespace `Stride.Particles.Modules`),
  `Updaters/FieldShapes/`, `ParticleModule.TryGetDebugDrawShape` (`:79` — the overrides return
  false unless `DebugDraw = true`; only the first debug-drawing module is drawn; shape unit
  conventions: sphere r1, cube side 2, cone +Y h1 r1, cylinder h1 r1, torus R1 r0.5). Live
  reference for every module type: `samples/Particles/ParticlesSample…/CustomParticle{Initializer,Updater,Spawner,Shape}.cs`
  + `ParticleCustomMaterial.cs` (Starbreach's are dead code). Known-good presets:
  `Stride.Assets.Presentation/.../ParticleSystemEntityFactory.cs:38-188` (Simple/Fountain/Ribbon).
- **What it shows:** a vortex force field (directed/vortex/repulsive force decomposition over a
  shaped falloff) plus a particle collider (restitution, friction, `IsHollow`, `KillParticles`),
  with the field shapes drawn via the toolkit's DebugShapes. Everything is public and CPU-side;
  there are no GPU particles to look for.
- **Siblings worth their own rows later:** ribbons/trails (with the undocumented hard requirement
  `SortingPolicy = ByOrder` + `InitialSpawnOrder` / `InitialSpawnOrderGroup`, or you get garbage
  geometry — canonical config in the factory above), flipbooks and velocity-stretched quads
  (`UVBuilderFlipbook.cs:37-95`, `ShapeBuilderOrientedQuad.cs:27-40`, `ShapeBuilderHexagon`,
  any `IComputeColor` via `ParticleMaterialComputeColor.cs:44-64`), fireworks via child emitters
  (`SpawnerFromParent` + death trigger), burst-on-click (`EmitParticles(n)`, `SpawnerBurst`),
  soft particles (`ParticleMaterialSimple.SoftEdgeDistance`), custom `ParticleUpdater` subclasses
  (unsafe SoA pool access — `Stride.Particles.Tests/ParticlePoolTest.cs` runs the pool with no
  device), and `ParticleSystemControl.Update(dt·Speed, system)` as a scrub API.

### 24. `Example46_OrbitCamera` — the Game Studio camera in your game (downgraded to "extend")

- **Level:** Intermediate · **Category:** Input · **Complexity:** 5 · **Verdict:** both
- **Sources:** `editor/.../GameEditor/Game/EditorGameCameraService.cs` (334; `ResetCamera(CameraOrientation)`
  `:169-198`, ortho `:84-88`), `EntityHierarchyEditor/Game/EditorGameEntityCameraService.cs` (349;
  pivot re-derivation `:329-336`, pan scaling `:264`, `SetTarget` at 3×radius `:68-91`);
  `editor/Stride.Editor/Engine/EntityExtensions.cs:67` (`CalculateBoundSphere` — skinned `:108-141`,
  sprite `:148-161`, particles `:195-201`, navigation bbox `:203-209`; drop the SpriteStudio block
  `:163-193`); engine-side twin `Stride.Graphics.Regression/TestCamera.cs:359-423`; preview-camera
  fit math `Preview/PreviewFromEntity.cs:171-294` (`r + r/tan(fov/2)`, `Far = 2.5·max(distance, r)`).
- **What it shows:** the toolkit **already ships** `Basic3DOrbitCameraController`
  (`src/Stride.CommunityToolkit/Scripts/Basic3DOrbitCameraController.cs:31`: orbit,
  distance-scaled pan, multiplicative zoom, pitch clamp). Missing vs the editor: the pivot that is
  continuously re-derived from the current view when *not* orbiting (so orbiting "just works"
  from wherever you stopped), dolly/WASD, six axis-aligned views + ortho toggle, and F-to-focus
  via `CalculateBoundSphere`. The example shows the upgraded controller; the third-person camera
  (backlog row, Starbreach #1 + samples #61) is the gameplay cousin.
- **Toolkit piece:** extend the existing controller; port `CalculateBoundSphere` (the toolkit's
  `GetMeshHWL` is the mesh-only cousin).

### 25. `Example47_UI_CodeGallery` — Stride UI without Game Studio, properly

- **Level:** Beginners → Intermediate · **Category:** UI · **Complexity:** 5 · **Verdict:** example
- **Sources:** `engine/Stride.UI/Panels/` (`Canvas.cs:23-52`, `GridBase.cs:28-70`,
  `StripDefinition.cs:23,96` — default `Star`), `Controls/` (`ModalElement.cs:20-70`,
  `ToggleButton`, `ScrollViewer`, `Slider.cs:61-369`, `ScrollingText.cs`, `UniformGrid`),
  `UIElement.cs:98,174,185,213,801`, `UIElementExtensions.cs:20-257` (**`SetGridRow`,
  `SetCanvasRelativePosition`, … already exist** — the first version proposed writing them),
  `VisualTreeHelper.cs:22-61`; world-space: `Engine/UIComponent.cs:57-122` (`IsBillboard` default
  true, `IsFixedSize` default false, `ResolutionStretch`, `SnapText`, `Sampler`);
  `UIPage.cs:17-24`/`UILibrary.cs` + `UILibraryExtensions.InstantiateElement<T>` (all public, all
  constructible in code). Test-derived material: `Stride.UI.Tests/Layering/GridTests.cs` (star
  min/max, 3D layers, 4.4 `ColumnGap/RowGap/LayerGap`), `CanvasTests.cs` (per-axis absolute/relative
  via `NaN`), `Regression/UITestGameBase.cs:108-231` (code-only 9-slice skins from `Sprite.Borders`),
  `EditTextTest.cs`, `ScrollViewerTest.cs` (`ScrollTo` deferred until Arrange), `TestFixedSizeUI.cs`.
  Samples: JumpyJet/SpaceEscape `UIScript.cs`, GameMenu (samples doc #66).
- **What it shows:** a controls-and-layout cookbook for code-first UI — above all the
  attached-property idiom and its **real** trap: `Canvas.UseAbsolutePositionPropertyKey` defaults
  to **true**, so `RelativePosition` is ignored until you flip it. Star-sized grids with
  min/max, `ModalElement` dialogs, code-only skins (`new Sprite(name, texture) { Borders = ... }`
  into `Button.PressedImage` etc.), and a world-space billboard health bar (`IsFullScreen = false`,
  `IsFixedSize` for constant screen size; the `Resolution == Transform.Scale` pairing rule).
  Document the verified absence of data binding so nobody hunts for it, and the routed-event
  model (inventory).
- **Toolkit piece:** none needed for setters — reuse `UIElementExtensions`. A `UIBuilder` only if
  it adds real value over them.

### 26. `Example48_Headless` — a Stride game with no window (new)

- **Level:** Intermediate · **Category:** Integration (Performance for the ECS variant) ·
  **Complexity:** 6 · **Verdict:** both
- **Sources:** `engine/Stride.Games/GameContextHeadless.cs:10-18`, `GameWindowHeadless.cs:13-73`
  (internal; ignores `IsUserManagingRun`), `engine/Stride.Graphics/HeadlessGraphicsPresenter.cs:10-53`,
  `GamePlatform.cs:363-371,439-441`, `GraphicsAdapterFactory.Direct3D.cs:61-67`
  (`STRIDE_GRAPHICS_SOFTWARE_RENDERING`), `GameBase.cs:83-84,219,282,529,600-611,639`; the toolkit's
  own `tests/Stride.CommunityToolkit.Tests/Engine/GameExtensionsRunTests.cs:34-74`; deterministic
  loop as Stride's Bepu tests do it (`Stride.BepuPhysics.Tests/GameTest.cs:14-21`); bare-engine
  variants `Stride.Engine.Tests/TestEntityManager.cs`, `Stride.UI.Tests/Layering/*`,
  `Stride.Shaders.Tests/RenderingTests.cs:78-95` (`ShaderMixer.MergeSDSL` + `Spv.ValidateFile`).
- **What it shows (family):**
  - `_BepuServer`: `new GameContextHeadless()`, software adapter env var, `IsFixedTimeStep` +
    `TargetElapsedTime` + `IsDrawDesynchronized = false`, **`WindowMinimumUpdateRate.SetMaxFrequency(60)`**
    (the headless loop never sleeps: default throttle 0 → 100 % of a core; a hidden-window game
    throttles to 15 Hz instead), no compositor (nothing draws, no shaders, no `data/db`), exit on
    a time budget or `Console.CancelKeyPress`. Overriding `RawTickProducer` gives a wall-clock-free
    replay loop. Bepu is device-free, so this is a real dedicated-server skeleton for Example17's
    SignalR.
  - `_EntityWorld`: `new EntityManager(new ServiceRegistry())` + processors + `Update(new GameTime())`
    — no `Game` at all; the cleanest ECS lesson in the engine, pairs with spec 14.
  - `_UILayout`: build a `Grid`/`Canvas` tree, `Measure`/`Arrange` by hand, print sizes — a console app.
  - `_ShaderCheck`: compile every toolkit `.sdsl` with `ShaderMixer`, no GPU — the seed of a CI test.
- **Traps to document:** headless ≠ no GPU (device always created); Linux Vulkan still demands
  `VK_KHR_swapchain` (`GraphicsDevice.Vulkan.cs:450,629-636`, hence Xvfb in Stride's CI); Debug
  builds request a D3D debug device (`Game.cs:234-238`); no input without `InputSourceSimulated`;
  one game per process; `game.Exit()` is the only way out.
- **Toolkit piece:** `game.RunHeadless(start, update, hz: 60)` beside `Run`, and a
  `STRIDE_TOOLKIT_HEADLESS` switch in `RunCore` for CI (see Infrastructure).

### 27. `Example49_HeightmapTerrain` — a terrain you can deform (new)

- **Level:** Intermediate · **Category:** Physics (Shapes) · **Complexity:** 6 · **Verdict:** both
- **Sources:** Bullet: `engine/Stride.Physics/Engine/Heightmap.cs:16-74` (`Create<T>`),
  `HeightmapUtils.cs:11-77`, `Shapes/HeightfieldColliderShape.cs:22-34` (three public ctors over
  `UnmanagedArray<short|byte|float>` — a type that is `[Obsolete]` yet required), `:99-104`
  (diamond/zigzag subdivision), `:122-127,216` (updatable debug primitive), `:156-163`
  (`LockToReadAndWriteHeights()` — live deformation), `Data/HeightfieldColliderShapeDesc.cs:15,110`;
  compiler-side remap worth copying `Stride.Assets/Physics/HeightmapAssetCompiler.cs:290-344`.
  Toolkit: `src/Stride.CommunityToolkit/Physics/HeightmapExtensions.cs` (`IntersectsRay` `:45`,
  `GetHeightAt` `:78`, `GetNormal` `:129`, `ToTexture` `:167`, `ToMesh` `:198`, `ToWorldPoints` `:281`)
  — **used by no example**.
- **What it shows:** noise or PNG heights → toolkit mesh → `StaticColliderComponent` with a
  heightfield shape → drop bodies → deform live under `LockToReadAndWriteHeights()` and rebuild the
  mesh; CPU brush picking via `IntersectsRay`; navmesh bonus (the navigation compiler feeds
  heightfields). Bullet only — Bepu has no heightfield (grep-verified), so this joins the
  `_BulletPhysics` example family.
- **Blockers:** the toolkit's `GetHeightAt` divides `Shorts` by a magic 255 and ignores
  `HeightScale` (mesh and collider disagree vertically until fixed — toolkit-side findings);
  `Float` heightmaps require `HeightScale == 1`, `Byte` cannot span negative and positive.

### 28. `Example50_SceneSaveLoad` — save an entity graph like an asset (new)

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 6 · **Verdict:** both
- **Sources:** `core/Stride.Core.Serialization/ObjectDatabase.cs:40-47,123-126`,
  `ContentManager.cs:67-77,541-619` (`Save`), `:157-179` (`Reload`), `FileOdbBackend.cs:97-126`,
  `ContentSerializerContext.cs:118-143` (deterministic URLs for sub-objects); proofs
  `Stride.Core.Tests/TestContentManager.cs:20-33,68-95` (a database + provider + `ContentManager`
  built from scratch, `[ContentSerializer(typeof(DataContentSerializer<T>))]`, shared references,
  `LoadContentReferences = false`), `Stride.Engine.Tests/EntitySerializerTest.cs:19-25`
  (`Content.Save("EntityAssets/Entity", entity)` → `Load<Entity>` in a running game). Every asset
  compiler uses the same `ContentManager.Save`. `tools/Stride.StorageTool` inspects the result.
- **What it shows:** the engine's content database is a working runtime store: save a whole
  entity subtree (transforms, components, procedural materials) and load it back as if it were an
  asset. Use a *second* `ObjectDatabase("/save/db", "index")` + `DatabaseFileProviderService` so
  saves don't land in `/data/db` next to the exe. This is also the founded route for the Video
  spike (a runtime-written `Video` object with a resolvable URL).
- **Blockers:** code-created `Texture` round-trip unverified; no versioning; your types need the
  assembly processor. Spec 8 is the small-POCO sibling.

### 29. `Example51_LightProbeNetwork` — bake probes at runtime and *see* them (new)

- **Level:** Advanced · **Category:** Rendering · **Complexity:** 8 · **Verdict:** both
- **Sources:** `engine/Stride.Rendering/Rendering/LightProbes/LightProbeGenerator.cs:29,116-206`,
  `BowyerWatsonTetrahedralization.cs:18-147` (public general 3D Delaunay tetrahedraliser;
  extrapolation vertices appended at 100 m), `LightProbeRuntimeData.cs:23-41` (public fields),
  `LightProbeRenderer.cs:24` (`CurrentLightProbes` tag), `LightProbeProcessor.cs:22-66`
  (reacts only to add/remove — call `UpdateLightProbePositions()`/`UpdateLightProbeCoefficients()`),
  `ComputeSphericalHarmonics.sdsl` (engine); editor wireframe
  `EditorGameLightProbeGizmoService.cs:216-380` (`ConvertToMesh` at `:327-380`: six line indices
  per face, faces touching extrapolation vertices skipped, `Yellow` α 0x9F, `Group29` +
  `AntiAliasLinePipelineProcessor`).
- **What it shows:** place ≥4 probes in a procedural level, `GenerateCoefficients(game)`, add the
  **missing** `new LightProbeRenderer()` to `ForwardLightingRenderFeature.LightRenderers`
  (correction 8), then draw the tetrahedral network exactly as Game Studio does by reading the
  processor's runtime data. Gotcha: the editor gizmo instantiates the SH shader at order 5 while
  the data is order 3 (upstream).
- **Toolkit piece:** `AddLightProbeRenderer()` on the compositor path; the tetrahedraliser is a
  general-purpose maths gift.

### 30. `Example52_StaticBatching` — many entities, one mesh (new)

- **Level:** Intermediate · **Category:** Performance · **Complexity:** 6 · **Verdict:** both
- **Sources:** `engine/Stride.Assets.Models/PrefabModelAssetCompiler.cs:117-281,328-408` (the
  recipe, build-time but written entirely against runtime-public APIs), over
  `Rendering/Extensions/TransformExtensions.cs:21` (`TransformBuffer`), `SplitExtensions.cs:13`
  (`SplitMeshes`), `IndexExtensions.cs:109,503` (`CompactIndexBuffer`, `GetReversedWindingOrder`),
  `BoundingExtensions.cs:15`, `ImportModelCommand.Model.cs:200-245` (`GroupDrawData`); vertices
  read via `AsReadable` (spec 22).
- **What it shows:** collect N entities sharing a material, transform their vertex buffers into
  world space, merge into one `Mesh` per material, watch `GraphicsDevice.FrameDrawCalls` drop —
  then compare with instancing (Example21/22) and cloning (spec 12): three answers to "many
  copies", each with its trade-off.
- **Toolkit piece:** `entities.BatchInto(model)`.

### 31. `Example53_TextureInspector` — mips, cube faces, 3D slices, swizzles (new)

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 5 · **Verdict:** both
- **Sources:** `editor/Stride.Assets.Presentation/Preview/TexturePreview.cs:56-250`,
  `TextureCubePreviewMode.cs:14-44`, `PreviewFromSpriteBatch.cs:99,176,196-207`,
  `Shaders/PreviewTexture.sdfx` (10 editor lines), engine `Rendering/Editor/Sprite3DBase.sdsl`,
  `Stride.Graphics/Shaders/SpriteBatchShader.sdsl:30-48`, `BatchBase.cs:208-209`;
  `Stride.Core.Presentation/Core/Utils.cs:18` (the 21-entry zoom table); readback/views tour in
  `Stride.Graphics.Tests/TestTexture.cs:97-164,342-416,628-710`.
- **What it shows:** all engine-public: view mip N with a sampler pinned to
  `MinMipLevel = MaxMipLevel = N` (load with `StreamingDisabled`), unfold a cubemap into the 4×3
  cross via `ToTextureView(new TextureViewDescription { ArraySlice = i, Type = ArrayBand })`,
  scrub a 3D texture's slices through `SpriteBatch` with a `Sprite3DBase` effect, `RRR1`/`NormalMap`
  swizzles, flip the device colour space per texture, zoom-about-cursor
  (`offset += (cursor01 − 0.5)·size·(1/newScale − 1/oldScale)`). Gotcha the editor itself gets
  wrong: it samples colour with `TextureFilter.ComparisonPoint` (a depth-comparison filter).
- **Toolkit piece:** a debug `TextureViewer` for the ImGui overlay; the cubemap-cross view is
  also the missing debug tool for the Skyboxes library and spec 29.

### 32. `Example54_ComputeCurves` — parameter curves without an AnimationClip (new)

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 4 · **Verdict:** both
- **Sources:** `engine/Stride.Engine/Animations/IComputeCurve.cs`, `ComputeAnimationCurve.cs:49-75`,
  `AnimationKeyFrame.cs`, `ComputeFunctionCurve.cs:58-62` (sine), `ComputeBinaryCurve.cs`
  (add/subtract/multiply), `ComputeCurveSampler.cs` (32 baked samples), `ComputeSeparateCurveVector3.cs`,
  `Interpolator.cs`; consumers: particles (`UpdaterSizeOverTime` + `ComputeAnimationCurveFloat`
  in the factory presets), the editor curve editor.
- **What it shows:** a public, `[DataContract]`, composable `IComputeCurve<T>` graph on a
  normalised [0,1] parameter — keyframes, sine, constants, binary ops, per-channel vectors, a
  baked sampler — usable for anything (tween a colour, shape a recoil, drive a spline speed), not
  just particles. Gotchas: `AnimationKeyTangentType` has only `Linear` so `Cubic` is unreachable;
  `Interpolator.Quaternion.Cubic` throws; the sampler bakes on construction — set `Curve` then
  `UpdateChanges()`.
- **Toolkit piece:** `Tween`-style helpers over it; the Charts library could plot them.

### 33. `Example55_RuntimeSpriteAtlas` — pack sprites at runtime (new)

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Assets/Textures/Packing/MaxRectanglesBinPack.cs` (604 lines, five
  heuristics, rotation), `TexturePacker.cs:122-157` (`Best`, `AllowMultipack`,
  `AllowNonPowerOfTwo`, growth), `AtlasTextureFactory.cs:80-168` (`CreateTextureAtlas`, border
  modes, formats); tests `Stride.Assets.Tests/TexturePackerTests.cs:38-393`. Usings: Mathematics +
  `Image` only (~1,200 lines to copy — the assembly is editor-side by packaging, not by TFM).
  Editor auto-slicing to pair with it: `tools/Stride.TextureConverter/Frontend/TextureTool.cs:869-964`
  (`FindSpriteRegion`, pure managed contour tracing).
- **What it shows:** load loose PNGs at runtime, pack them into one atlas, build a code
  `SpriteSheet` (`Sprite.cs:55` public ctors; `SpriteSheet.Sprites`), animate with
  `SpriteAnimation.Play` (`Stride.Engine.Tests/SpriteAnimationTest.cs:23-224`) — the whole 2D
  asset pipeline in code. Distinct from `GuillotinePacker` (the shadow-atlas packer, inventory).
- **Toolkit piece:** `SpriteAtlasBuilder`.

### 34. `Example56_Bepu_TriggersAndContacts` — the Bepu features the tests know about (new)

- **Level:** Intermediate · **Category:** Physics · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.BepuPhysics/Stride.BepuPhysics.Tests/BepuTests.cs:481-526,572,599-645`
  (`IContactHandler` with `NoContactResponse = true` — a trigger volume; contact handler on
  statics; removing an entity inside the callback; moving a static trigger;
  `contacts.ComputeImpactForce(contact)` under `ContinuousDetectionMode.Continuous`), `:817-837`
  (a `ContactEvents` adapter turning the interface into C# events), `:707-717` +
  `BepuSimulation.cs:467-578` (`RayCastPenetrating` with `Span<HitInfoStack>`; `SweepCast`/`Overlap`
  exist, untested), `:305-350` (`IndexBasedSimulationSelector` — two simulations, constraints
  detach), `:353-400,721-806` (`ISimulationUpdate` ordering; `AfterUpdate()`/`NextUpdate()`
  awaiters — grep: unused by the toolkit), `:117-302` (`Body2D` `ZTolerance`/`Awake`/`Kinematic`
  inertia lock).
- **What it shows:** the toolkit has 15 physics examples and none covers trigger volumes, impact
  force, sweeps/overlaps, multiple simulations or the await-a-tick idioms. One example with four
  scenes, the adapter promoted to the Bepu library. Also the samples' `stackalloc` query buffers
  (`BepuSample…/OverlapTesterComponent.cs:15,30`).
- **Cross-link:** the [Bepu plan](plans/bepu-examples.md) — fold in rather than duplicate.

### 35. `Example57_UI_DesignTimeEditor` — drag handles, magnet snapping, hit proxies (new)

- **Level:** Advanced · **Category:** UI (Interaction) · **Complexity:** 8 · **Verdict:** both
- **Sources:** `editor/.../UIEditor/Game/UIEditorController.cs:41-47,112-186`,
  `UIEditorGameAdornerService.cs:25-236,527-547`, `.Events.cs:74-310`, `UILayoutHelper.cs` (527
  lines, depends only on `Stride.UI` + maths), `Adorners/*.cs` (swap the WinForms cursors and WPF
  `SystemParameters`); engine `UIRenderFeature.Picking.cs:356-378` (**public static**
  `GetElementsAtPosition(root, ref ray, ref wvp)`), `UIRenderFeature.cs:414-418` (the undocumented
  world→UI transform: scale by `Size/Resolution`, negate rows 2 and 3).
- **What it shows:** the exact mechanism behind Game Studio's UI editor — a second `UIComponent`
  at 2× resolution holding opacity-0 hit proxies over every element, selection/sizing/margin
  handles as `Border`s, per-axis magnet snapping parent → container → siblings, alignment
  re-derived from the drop position. Reveals two runtime facts: overlapping `UIComponent`s all
  receive pointer events (`Handled` doesn't cross components), and `RenderSize`/`WorldMatrix` are
  one frame stale inside `Update`. Sequel to `Example10_StrideUI_DragAndDrop`.
- **Toolkit piece:** `UILayoutHelper` port; hit-testing one-liner.

### 36. `Example58_SDSL_FeatureTour` — the shader language, by example (new)

- **Level:** Advanced · **Category:** Rendering · **Complexity:** 8 · **Verdict:** both (example
  + a docs page + the headless compile test from spec 26)
- **Sources:** `sources/shaders/assets/SDSL/{RenderTests,ComputeTests,StreamOutTests,CompilerTests}`
  (~70 self-contained `.sdsl` files, each with a `// PSMain(ExpectedResult=#RRGGBBAA, cbuffer.X=(...))`
  header parsed by `Stride.Shaders.Tests/TestHeaderParser.cs:21-46`), driven by the public
  pipeline `new ShaderMixer(new ShaderLoader(dir))` → `MergeSDSL(...)` → `Spv.ValidateFile`
  (`RenderingTests.cs:78-95`; `ShaderLoaderBase` is public abstract); `StrideShaderTests.cs:164-498`
  (`ShaderSource.ToCode()` dumps the tree the material system generates — the single best way to
  *see* it). Catalogue: inheritance/`override`/`abstract` (`SimpleInheritanceAbstract.sdsl`),
  `stream`s (`StreamVSToPS.sdsl`), `stage` (`CompositionStageMethod1.sdsl`,
  `CompositionInheritanceStageIndirectAndDirect.sdsl`), `compose` + arrays + `foreach`
  (`CompositionArray1.sdsl`, `CompositionArrayForeachNested.sdsl`), generics incl.
  `<Semantic S>`, `<LinkType L>` + `[Link]`, `<MemberName M>`, array sizes from generics,
  `cbuffer` merging by name (`CBuffer.sdsl`; the rename pitfall at `RenderingTests.cs:130-159`),
  `rgroup`, structured/byte-address buffers, compute `[numthreads]`, geometry shaders
  (`StreamGS.sdsl`), tessellation (`StreamTessellation.sdsl:7-64`), composite constructors
  (`CompositeCtor.sdsl`), `.sdfx` `params`/`using params`/conditional `mixin`.
- **What it shows:** one quad per feature, a C# `ShaderMixinSource` flipping compositions and
  generics live, and the compile-error path (spec 19's sidebar + the editor's `EffectCompiling`/
  `CompilationErrorShader` fallback, inventory). Half the toolkit's shader questions are SDSL
  questions; nothing documents the language by runnable example.

### 37. `Example59_Input_SimulatedReplay` — scripted input for demos, attract mode and CI (new)

- **Level:** Intermediate · **Category:** Input · **Complexity:** 5 · **Verdict:** both
- **Sources:** `engine/Stride.Input/Simulated/` (all public: `InputSourceSimulated.AddMouse/AddKeyboard/AddGamePad/AddPointer`,
  `MouseSimulated.SimulateMouseDown/Up/Wheel/SetPosition/SimulatePointer`, `KeyboardSimulated`,
  `GamePadSimulated.SetButton/SetAxis`, `PointerSimulated` for multi-touch); wiring
  `Stride.Graphics.Regression/GameTestBase.cs:302-323` (`Input.Sources.Clear(); Add(new InputSourceSimulated())`);
  semantics pinned in `Stride.Input.Tests/TestInput.cs` (press+release in one frame ⇒ both
  `IsKeyPressed` and `IsKeyReleased`; `RepeatCount`; `LockPosition` centring); the driver API in
  `Stride.Games.AutoTesting/ScreenshotTestRunner.cs:356-393` (`WaitFrames`/`PressKey(duration)`/`Tap`);
  event logger `Stride.Input.Tests/TestInputEvents.cs:22-83` + the `SpriteBatch` text console in
  `InputTestBase.cs:11-95`.
- **What it shows:** replace the input sources with a simulated one, record a session as
  `(frame, action)` pairs, replay it — the attract-mode/tutorial-ghost/CI-driver trick in one
  mechanism. Plus the "what is my controller sending" logger, the best diagnostic the toolkit
  doesn't have.
- **Toolkit piece:** `InputScript` builder (`Press(Keys.A).Wait(3).Release()`), reused by the
  screenshot CI for interactive examples (Infrastructure).

---

## Engine re-sweep additions — compact specs (`Example80`–`Example93`)

From the second pass over `Stride.Rendering`, `Stride.Graphics`, `Stride.Games`, `Stride.Engine`
and `Stride.BepuPhysics`. Shorter than the specs above; promote to full specs when they graduate.

- **`Example80_TemporalAA`** — Advanced (9) · Rendering · spike. `Images/AntiAliasing/TemporalAntiAliasEffect.cs`
  (public; `JitteringMagnitude`, `BlendWeightMin/Max`, `HistoryBlurAmp`, `VelocityDecay`) +
  `Rendering/MeshVelocityRenderFeature.cs` (public `SubRenderFeature`; writes
  `PreviousWorldViewProjection` per draw, `:154-177`) + `VelocityTargetSemantic` →
  `VelocityOutput.sdsl` (`ForwardRenderer.cs:260-263` allocates the R16G16 target when
  `PostEffects.RequiresVelocityBuffer`). Wire: `meshRenderFeature.RenderFeatures.Add(new MeshVelocityRenderFeature())`,
  `postFx.Antialiasing = new TemporalAntiAliasEffect()`. Blockers: depth/velocity are bound only on
  the `aaFirst` path, i.e. `Bloom.StableConvolution == true` (default); there is **no camera
  projection jitter** (`:88-156` only perturbs filter weights), so expect reprojection blur rather
  than true TAA; untested upstream. What it teaches regardless: how a render-target semantic adds
  an MRT output to the opaque stage and how a sub-feature adds a per-draw cbuffer slot; honest A/B
  against FXAA.
- **`Example81_SubsurfaceScattering`** — Advanced (9) · Rendering · spike. `Materials/SubsurfaceScattering/MaterialSubsurfaceScatteringFeature.cs`
  (`ScatteringWidth`, `Translucency`, `TranslucencyMap`, `ProfileFunction` Skin/Custom),
  `Images/SubsurfaceScattering/SubsurfaceScatteringRenderFeature.cs`, `SubsurfaceScatteringBlur.cs`
  (`FollowSurface`, `NumberOfPasses`, `ActiveRenderMode` incl. "show scattering objects"),
  `ForwardRenderer.cs:99,265-270,528-544`, `LightDirectionalShadowMap.ComputeTransmittance`. Wire
  the sub-feature, set `forwardRenderer.SubsurfaceScatteringBlurEffect = new SubsurfaceScatteringBlur()`,
  keep `PostEffects` non-null (the material-index target is requested only inside that branch,
  `:246-271`). Never exercised in-tree.
- **`Example82_LayeredMaterials`** — Intermediate (6) · Rendering · both. `MaterialDescriptor.Layers`,
  `MaterialBlendLayer.cs:40-79` (`Material`, `BlendMap : IComputeScalar`, `Overrides`),
  `MaterialOverrides.cs:40-106` (`UVScale`, per-channel contributions), `MaterialStreamLinear/Normal/AdditiveBlend.sdsl`.
  One compiled shader, no extra draw; base + `Layers = { new MaterialBlendLayer { Material = m, BlendMap = new ComputeTextureScalar(mask) } }`.
  The trap is the `Material.New`/`Descriptor` fact above. Toolkit: `MaterialBuilder.WithLayer(material, mask, overrides)`.
- **`Example83_MaterialFeatures`** — Intermediate (6) · Rendering · both. A key-cycled gallery of one
  sphere per descriptor feature nobody sees: alpha-test cutoff (`MaterialTransparencyCutoffFeature.Alpha`),
  additive transparency, `DitheredShadows` (`ShadowMapCasterAlphaDithered.sdsl`, 4×4 Bayer — the
  visual hook), occlusion/cavity maps (`MaterialOcclusionMapFeature`), normal-map `ScaleAndBias`/`IsXYNormal`,
  glossiness `Invert`, energy-conserving specular, swappable BRDF terms (`MaterialSpecularMicrofacetModelFeature.Fresnel/Visibility/NormalDistribution/Environment`
  — 2/9/3/3 implementations), thin glass (`RefractiveIndex`, multipass), `MaterialAttributes.CullMode/DepthFunction`,
  stochastic tiling (`ComputeTextureBase.UseRandomTextureCoordinates`), `ComputeTextureColor.Swizzle/FallbackValue`,
  31 `BinaryOperator` blend modes, `MaterialCelShadingLightRamp.RampTexture`. First slice belongs in `Example01_Material`.
- **`Example84_Letterbox`** — Beginners (3) · Rendering · both. `Compositing/ForceAspectRatioSceneRenderer.cs`
  (`Child`, `FixedAspectRatio`, `ForceAspectRatio`; viewport-only maths `:40-60`, so the bars are
  whatever `ClearRenderer` painted). Wrap the forward renderer; pair with `CameraComponent.UseCustomAspectRatio`.
  Pixel-art and board-game projects ask for this constantly.
- **`Example85_OverlayStage`** — Intermediate (7) · Rendering · both. `SingleStageRenderer`,
  `SceneRendererCollection`, `SimpleGroupToRenderStageSelector`, `RenderStage.cs:40-52`
  (`SortMode`, `Filter`, `DepthAccess`), `ClearRenderer` (`ClearFlags` DepthOnly). A new
  `RenderStage("Overlay", "Main")` fed by `Group31`, drawn *after* the post-effects with depth
  cleared: gizmos, first-person weapons and debug shapes escape bloom/DoF/fog and depth clashes.
  `RenderStageFilter` is an abstract extension point with zero subclasses in the tree — a five-line
  subclass filters by anything, where the render-groups row only knows masks.
- **`Example86_ManyLights`** — Intermediate (6) · Performance · example. Clustered forward lighting
  is already in `CreateDefault` (`GraphicsCompositorHelper.cs:60-71`): non-shadowed, non-projective
  point/spot lights fall through to `LightClusteredPointSpotGroupRenderer` (`CanRenderLight`
  `:79-133`; 64-px clusters, 8 slices). 500 animated point lights with `Shadow.Enabled = false` at
  full frame rate, then enable shadows on ten and watch the per-light path take over; needs
  `Level_10_0`.
- **`Example87_SpriteComponentTour`** — Beginners (4) · Rendering · example. `Engine/SpriteComponent.cs:34-129`:
  `SpriteType.Billboard`, `Intensity > 1` (feeds bloom), `PremultipliedAlpha`, `IgnoreDepth`,
  `IsAlphaCutoff`, `BlendMode` None/Auto/Alpha/Additive/NoColor, point `Sampler` (pixel art),
  `Swizzle`, `RenderGroup`; billboard maths `SpriteRenderFeature.cs:190-215`. Complements the
  `Sprite3DBatch` row (the manual way). Bonus: `BackgroundComponent` renders an equirectangular
  2D texture as a 360° panorama without a cubemap (`BackgroundRenderFeature.cs:121-127`).
- **`Example88_BreakableJoints`** — Intermediate (6) · Physics · both. `Constraints/_ConstraintComponentBase.cs:55-71`
  (`GetAccumulatedImpulseMagnitude()`, `GetAccumulatedForceMagnitude()` — "compare with a motor's
  MaximumForce"), `Enabled`, `Attached`; `WeldConstraintComponent` planks, a dropped weight, planks
  over a threshold get `Enabled = false`. `AngularAxisGearMotorConstraintComponent.VelocityScale`
  (gear ratios) as a second visual. Not in the Bepu plan or backlog.
- **`Example89_PhysicsInterpolation`** — Beginners (4) · Physics · example. `BodyComponent.cs:106-131`
  (`InterpolationMode` None/Interpolated/Extrapolated), `BepuSimulation.FixedTimeStep/MaxStepPerFrame`
  (`:166,252`), `SleepThreshold`, `Deterministic`. Three identical spinning bodies, one per mode,
  at a 20 Hz fixed step so the stutter is obvious; `MaxStepPerFrame` as the spiral-of-death cap.
  The toolkit's `Body2DComponent` sets `Interpolated` in its constructor without a word.
- **`Example90_BitmapFont`** — Intermediate (6) · UI · both. `Font/FontSystem.cs:97-124`
  (`NewStatic`/`NewScalable(size, IList<Glyph>, IList<Texture>, baseOffset, lineSpacing, kernings)`),
  public `Glyph`/`Kerning`, `SpriteFont.IndexInString` (`:300`, caret-from-click), `PreGenerateGlyphs`.
  A retro 8×8 glyph sheet or an msdf-atlas output → `SpriteFont` → `DrawString`/`TextBlock`; then a
  text box with click-to-caret. Distinct from the runtime-TTF row (that is `NewDynamic`).
- **`Example91_AnimatedMaterial_GlobalTime`** — Beginners (4) · Rendering · example. A six-line
  `.sdsl` deriving `ComputeColor` that uses `Global.Time` for a pulse/scroll/dissolve, dropped into
  `MaterialEmissiveMapFeature`; sidebar cataloguing the portable mixins nobody lists —
  `Utils/Math.sdsl` (`RayIntersectsPlane/Sphere`, `Luminance`), `HSVUtils.sdsl`, `BlendUtils.sdsl`,
  `ColorUtility.sdsl` (`ToLinear`/`LinearToSRgb`), `NormalPack.sdsl`, `ComputeColorTextureRepeat<>`,
  `ComputeColorFromStream<TStream, TRgba>` (read any vertex semantic as a colour).
- **`Example92_LanSockets`** — Advanced (8) · Integration · example. `Engine/Network/SimpleSocket.cs:58-106`
  (`StartServer(port, singleConnection)`, `StartClient(address, port)`), `SocketMessageLayer.cs:36-110`
  (`AddPacketHandler<T>`, `Send(object)` through Stride's binary serializer, `SendReceiveAsync`
  request/response). The engine's own TCP layer (built for the connection router — no
  reconnect/backoff; payload types need generated serializers). The no-ASP.NET alternative to
  `Example17_SignalR`; pairs with spec 26's headless server.
- **`Example93_CustomProjection`** — Advanced (8) · Rendering · example. `CameraComponent.cs:161-200`
  (`UseCustomViewMatrix`, `UseCustomProjectionMatrix`, `UseCustomAspectRatio`, `Frustum`), consumed
  at `SceneCameraRenderer.cs:112-150`; `ForwardRenderer.cs:369-373` (VR eyes) is the in-tree
  worked example. A planar mirror/portal via a second camera with an oblique near plane rendered
  through spec 20's texture renderer; an off-axis "window" projection. Pairs with `Matrix.Reflection`
  (planar-shadows row).

Smaller items from the same pass (inventory-grade):

| Item | Source | Note |
|---|---|---|
| Per-entity material override | `ModelComponent.Materials` (`:97-106`), `Model.Instantiate()` (`Model.cs:125`) | Non-null entries override `Model.Materials`; the toolkit uses it internally (`ModelComponentExtensions.cs:308`) but never teaches it. |
| Rigid node hierarchies in one model | `Model.Skeleton` + `Mesh.NodeIndex` + `ModelComponent.Skeleton` (`SkeletonUpdater.NodeTransformations`) | A turret with a rotating head as one `ModelComponent`; spec 9 already cites the animation path for it. |
| Missing primitives | `GeometricPrimitive.Disc.New(radius, sectorAngle)` (pie wedge), `GeoSphere`, `Plane.New(generateBackFace, NormalDirection)` | `PrimitiveModelType` has neither Disc nor GeoSphere; the wedge is what the Charts library needs. |
| Animation extras | `AnimationBlendOperation.Add`, `PlayingAnimation.TimeFactor/Weight/WeightTarget`, `AnimationCurveInterpolationType.Cubic`, `Interpolator` public | Fold into spec 9. `AddCurve(..., isUserCustomProperty: true)` is skipped by `AnimationUpdater.cs:45` — unverified whether readable. |
| Diagnostics HUD | `GraphicsAdapter.DriverInfo` (`GpuName`, `VendorName`, `DriverVersion`, `ApiName/Version`), `GraphicsDevice.RendererName/IsDebugMode/Features`, `GraphicsDeviceFeatures[format].MultisampleCountMax` | Extends the `DeviceCapabilitiesReport` idea. |
| Debug-scope validation tree (4.4) | `GraphicsDevice.DebugScope.cs:48-110`, `CommandList.DebugScope.cs`; enabled by `GameContext.DeviceCreationFlags = Debug` | Draw/dispatch/clear/copy counts per `BeginProfile` scope, dumped when a validation error fires. |
| Lifecycle events | `Game.GameStarted/GameDestroyed` (static), `GameBase.UnhandledException`, `Activated/Deactivated/Exiting/WindowCreated`, `GameWindow.Closing/FullscreenChanged/ClientSizeChanged`, `Game.ConsoleLogMode/ConsoleLogLevel` | Crash-screen and autosave hooks; doc section. |
| `TimerTick` | `Stride.Games/Time/TimerTick.cs` (pausable, `SpeedFactor`) | Fits the Bepu plan's time-control example. |
| Splash screen | `SceneSystem.cs:48-78,102-140` (`SplashScreenUrl/Color`, `DoubleViewSplashScreen`) | Needs `content.Exists(url)` — reachable through spec 28's runtime database. |
| Texture sharing | `TextureOptions.Shared/SharedNtHandle`, `Texture.SharedHandle` (D3D11) | WPF `D3DImage`, OBS/Spout, cross-process; Windows only. |
| HDR output / vsync at runtime | `GraphicsPresenter.SetOutputColorSpace(ColorSpaceType, format)`, `PresentInterval` | HDR10 pipeline completeness unverified. |
| `SphericalHarmonicsRendererEffect.InputSH` | `Images/SphericalHarmonics/` | Visualise an SH probe as a sphere — debugging aid for the Skyboxes library and spec 29. |
| `GcProfiling` | `Profiling/GCProfiling.cs` (public) | GC count/memory into `Profiler`; feed `PerfMonitor`. |
| Entity helpers | `EntityExtensions.Enable<T>(applyOnChildren)`/`EnableAll`, `Entity.GetOrCreate<T>`, `EntityTransformExtensions.SetWorld/WorldToLocal/LocalToWorld`, `TransformComponent.UseTRS = false` | Partial overlap with toolkit `TransformExtensions`. |
| Bepu small items | `EmptyCollider`, `TriangleCollider`, `CollidableComponent.RayCast` (`:414-472`), `BepuSimulation.Simulation` (raw Bepu), `PoseLinearDamping/AngularDamping`, `MeshCollider.Closed`, `Stride.BepuPhysics.Debug.DebugRenderComponent` (F11 wireframe) | Snippets for the Bepu family. |
| `RenderView.CullingMode = None`, `CullingMask`, custom `SortMode` | `RenderView.cs:99-106`, `SortMode.cs:12-14` | 2D layer sorting via a custom sort key. |
| Portable shader oddments | `SpriteSuperSampler.sdsl`, `MaterialSurfaceDiffuseMetalFlakes.sdsl`, `ProceduralModels/CameraCube.sdsl` (orphaned geometry-shader single-pass cubemap via `SV_RenderTargetArrayIndex`) | For the mixin catalogue in spec 36. |
| Gobo extras | `LightSpot.cs:84-128` (`MipMapScale`, `AspectRatio`, `TransitionArea`, `FlipMode`) | Fold into the gobo row. |
| Image loaders | `Image.cs:754-760` registers Stride, DDS, GIF, TIFF, BMP, JPG, PNG (WIC on desktop); TGA is in the enum but no `Register` was seen | Say "PNG/JPG/DDS/BMP/GIF/TIFF" in docs; treat TGA as unverified. |

---

## Toolkit-side findings

Things the cross-check found in the toolkit itself while checking "does the toolkit already have
this". Bugs first.

- **`AddCleanUIStage()` runs every post effect.** `src/Stride.CommunityToolkit/Rendering/Compositing/GraphicsCompositorExtensions.cs:191-200`
  replaces `PostEffects` with `new PostProcessingEffects { DepthOfField = { Enabled = false }, ColorTransforms = { Transforms = { new ToneMap() } } }`
  — no `DisableAll()`, so SSAO, SSR (which also forces normal + specular-roughness MRTs), bloom,
  light streaks, lens flare and FXAA are on in every example that calls it. `AddGraphicsCompositor()`
  itself is fine (goes through `CreateDefault`). Fix: `DisableAll()` first, then enable what the
  UI stage needs — and measure the frame-time difference for the changelog. *Fixed 2026-09-03:*
  `Example01_Basic3DScene_Primitives`, vsync off, warm cache: 8.6 ms → 4.8 ms per frame
  (116 → ~205 FPS). The same change also stops `AddCleanUIStage`/`AddUIStage` discarding
  renderers attached before them.
- **`HeightmapExtensions.GetHeightAt` contract mismatch** (`src/Stride.CommunityToolkit/Physics/HeightmapExtensions.cs:78`):
  divides `Shorts[index]` by a magic 255 and ignores `HeightScale`; NRE for `Float`/`Byte`
  heightmaps. The whole extension file is unused by any example (spec 27 would be the first).
- **Already exists — do not rebuild:** `Basic3DOrbitCameraController` (spec 24),
  `AddStudioLighting(Key, Fill, Rim)` / `AddAllDirectionLighting` (`GameExtensions.cs:553,484`)
  for the three-point-lighting row, `PerfMonitor` GPU counters (`DebugTools/PerfMonitor.cs`,
  missing only `FrameTriangleCount`), `PerfMonitorHelpers` `Profiler.Subscribe()` aggregator,
  `Texture.Load` usages (`Skyboxes/GameExtensions.cs:40`, `TextureCanvas.cs:261`),
  `TextureCanvas.Apply(ImageEffect)`, `ScreenshotCapture` + `build/capture-screenshots.cs`
  (frame-scheduled fixed-timestep capture, the same technique as Stride's own harnesses),
  `GameExtensionsRunTests.cs` (a working headless xunit fixture), `WorldTextComponent` billboards
  (cousin of `Sprite3DBatch`), `GetMeshHWL` (mesh-only cousin of `CalculateBoundSphere`),
  `VectorHelper.SeedRandom` (stateful cousin of `RandomSeed`).
- **The engine has it — do not write it:** `UIElementExtensions.SetGridRow/SetCanvasRelativePosition/…`
  and `VisualTreeHelper` (spec 25; Example06/07/10 already use them), `Quaternion.LookRotation`
  (toolkit `MathUtilEx.LookRotation` is the NaN-safe version — keep it, but say why),
  `Vector3.RotateAround` (toolkit twin commented out at `Engine/TransformExtensions.cs:213-226`).
- **Cross-platform system fonts:** `Stride.Assets.Tests/TestSystemFontProvider.cs:11-78` carries
  the metric-compatible fallback table (Arial ↔ Liberation Sans, Times ↔ Liberation Serif,
  Courier ↔ Liberation Mono) — copy it into `DebugOverlayFontResolver`.
- **`engine-patterns.md` transparency wording** — correction 6 above; reproduce, then reword.
- **Example metadata:** levels say "Beginner" in metadata blocks and "Beginners" in the README;
  nine inventory rows here use a "Scripts" category the README doesn't have. Separate audit.
- **Physics-clock waits:** the samples tree ships `Task.Delay` (Platformer2D), `Stopwatch`
  (SpriteStudioDemo) and the correct `Game.WaitTime` (TopDownRPG) side by side — the same
  wall-clock-vs-game-clock lesson Starbreach carries in ~12 sites. The Bepu plan's
  `Example26_TimeControl` is the place to teach it once.

---

## Inventory — everything else worth keeping

One line each; verdicts as above. These are real candidates that didn't make the top cut, grouped
by area. Rows marked ✱ were added or re-verdicted by the cross-check.

### Graphics + Games

| Item | Source (under `sources\`) | Verdict · Level · Category | Note |
|---|---|---|---|
| `GameTime.Factor` slow motion | `engine\Stride.Games\GameTime.cs:106-156` | example · Beginners · Gameplay | One assignment via `WarpElapsed`; Bullet (`Bullet2PhysicsSystem.cs:95`), **Bepu** (`PhysicsGameSystem.cs:29`; `BepuSimulation.cs:156` says `TimeScale` stacks with it), animation and particles all honour it. Fold into the Bepu plan's `Example26_TimeControl` rather than a new example. |
| Custom `GameSystemBase` | `Stride.Games\GameSystemBase.cs`, `GameSystemCollection.cs:254-260` | example · Intermediate · Gameplay | The pattern `ImGuiSystem` and `ScreenshotSystem` already use but nothing teaches; late-added systems initialize immediately. |
| Graphics settings menu | `Stride.Games\GraphicsDeviceManager.cs:169,240,295,399,422` (+`Stride.Graphics\GraphicsAdapterFactory.cs:67,85`, `GraphicsOutput.cs`) | example · Intermediate · UI | Resolution/vsync/MSAA/monitor/GPU pickers + `ApplyChanges()`; pairs with a `DeviceCapabilitiesReport` helper. |
| Fixed timestep / FPS cap / background throttle | `Stride.Games\GameBase.cs:163-344,529-615`, `core\Stride.Core\ThreadThrottler.cs:22,85` | both · Intermediate · Performance | Three FAQs in one; `DrawInterpolationFactor` is unknown; `WindowMinimumUpdateRate`/`MinimizedMinimumUpdateRate` defaults (0 / 15 Hz) explain the headless spin (spec 26). |
| `RawTick` manual loop | `GameBase.cs:529,639` (both **protected**) | example · Advanced · Gameplay | Needs a `Game` subclass. Deterministic replay, headless benchmarking, lockstep (pairs with SignalR). |
| WinForms embedding + user-managed loop | `Stride.Games\GameContext*.cs`, `GameContextFactory.cs:35-50`, `GameWindowWinforms.cs:182-186`, `GameWindowSDL.cs:153` | example · Advanced · Integration | `isUserManagingRun: true` + `RunCallback()`; worked example `Stride.Engine.NoAssets.Tests\GameWindowTest.cs:29-46`. **Not** available with the headless context (upstream). |
| Second OS window | `Stride.Games\GameWindowRenderer.cs:38,72,77`; `NoAssets.Tests\GameWindowTest.cs`, `GameWindowMinimizeTest.cs:120-214,288-302` ✱ | example · Advanced · Integration | `Initialize()` + `LoadContent()` + `BeginDraw/Clear/EndDraw`; minimize ⇒ `BeginDraw()` false; `PumpUntil` helper; window tests can't run in parallel. Detached inspector/spectator window. |
| `GameWindow` tricks | `Stride.Games\GameWindow.cs:53-217` | toolkit · Beginners · Interaction | Borderless toggle, `Opacity`, `Closing`/`Deactivated` events; `ToggleBorderlessFullscreen()` helper. |
| GPU stats HUD ✱ | `Stride.Graphics\GraphicsDevice.cs:69-86` | toolkit · Beginners · Performance | Toolkit `PerfMonitor` already shows draw calls and memory — add `FrameTriangleCount`, cross-link from Example21/22. |
| `GpuTimer` (QueryPool) | `Stride.Graphics\QueryPool.cs`, `Stride.Rendering\QueryManager.cs:12-109` | both · Advanced · Performance | Real GPU timing; poll-next-frame pattern. Only `Timestamp` queries exist. |
| GPU debug markers | `CommandList` `BeginProfile(Color4, string)/EndProfile()` (D3D11 `:864/883`, D3D12, Vulkan) | example section · Intermediate · Performance | Named RenderDoc/PIX regions; 3 lines inside Example09. |
| `Sprite3DBatch` billboards | `Stride.Graphics\Sprite3DBatch.cs:15,66`, `Sprite.cs`, `Rendering\Sprites\SpriteFromTexture.cs:19,79` | both · Intermediate · Rendering | World-space sprites/damage numbers with no sprite-sheet asset; toolkit `WorldTextComponent` is the text-only cousin. |
| `Texture.Load` from stream + texture views ✱ | `Texture.cs:582,1592`, `Texture.Extensions.cs:28,92` (`FromFileData`) | toolkit wrapper · Beginners · Rendering | Toolkit already uses it in two places; a public `LoadTextureFromFile` is a trivial wrapper. Straight-alpha PNGs need `BlendStates.NonPremultiplied`. |
| `UIBatch` / `BatchBase<T>` | `Stride.Graphics\UIBatch.cs:215,328`, `BatchBase.cs:38` | example · Advanced · Rendering | Nine-slice + depth-biased drawing; `BatchBase` is the canonical dynamic-buffer streaming reference; `UIBatch.SDFSpriteFontEffect` is the public route to render SDF fonts correctly. |
| Compute + indirect draw ✱ | `Buffer.Structured/Raw/Argument.cs`, `CommandList` `Dispatch(x,y,z)`/`Dispatch(Buffer)`/`DrawInstanced(Buffer)` (D3D11 `:689-824`); `Rendering\ComputeEffect\ComputeEffectShader.cs`; tests `Stride.Graphics.Tests.11_0\TestUnorderedAccessOnlyTexture.cs:29-66`, `TestHammersley.cs:36-78`, engine `ComputeShaderBase.sdsl:20-69` | example · Advanced · Performance | `ComputeEffectShader { ShaderSourceName, ThreadNumbers, ThreadGroupCounts }` + UAV barrier + typed buffers is fully demonstrated by the tests; indirect dispatch/draw is demonstrated by nothing. |
| Split-screen viewports | `CommandList.cs:22,68-181`, `Viewport.cs:183-217` | example · Intermediate · Rendering | Up to 16 viewports/scissors; `Project` for world→screen labels. |
| Render-state cheat sheet | `BlendStates.cs`, `DepthStencilStates.cs`, `RasterizerStates.cs`, `SamplerStateFactory.cs` | example · Beginners · Rendering | Named presets (`Wireframe`, `Additive`, `NonPremultiplied`, `DepthRead`...) nobody can discover. |
| Raw vertex/index buffers + `MutablePipelineState` ✱ | `Stride.Graphics.Tests\TestTextureSampling.cs:54-135` | example · Intermediate · Rendering | Hand-built quad: `Buffer.Vertex.New`, `VertexDeclaration`, `MutablePipelineState` (`SetDefaults`/`RootSignature`/`EffectBytecode`/`Output.CaptureState`/`Update`), sampler `Mirror`. The level below Example09. |
| Texture readback, views, `DataBox` uploads ✱ | `Stride.Graphics.Tests\TestTexture.cs:97-164,190-310,536-614,628-710` | both · Advanced · Rendering | `GetData`/`SetData` round trips, per-slice views, UAV clears, 3D textures, depth readback, multi-mip/array upload — the only readable example of it; the deterministic debug-pattern generator (`:769-788`) is a toolkit utility. |
| CPU `Image` API ✱ | `Stride.Graphics.Tests\TestImage.cs:44-154,218-257` | section · Beginners · Rendering | `Image.New2D`, `PixelBuffer.GetPixel/SetPixel`, `Save(stream, Png)`; runtime `Image.Load` lacks TGA/PSD. Section of Example06. |
| Cubemap prefiltering viewer ✱ | `Stride.Graphics.Tests.11_0\TestLambertPrefilteringSH.cs:50-166`, `TestRadiancePrefilteringGgx.cs:72-139` | example · Advanced · Rendering | The engine's SH/GGX filters shown face-by-face, mip-by-mip — the debug tool the Skyboxes library lacks; merges into spec 31. |
| Compositor multi-output + letterbox ✱ | `Stride.Graphics.Tests\TestSharedStageMultipleOutputs.cs:39-86`, `FixedAspectRatioTests.cs:41-58` | example · Advanced · Rendering | Second `ForwardRenderer` sharing stages; `ForceAspectRatioSceneRenderer { FixedAspectRatio = 3 }`. Section of spec 20. |
| SpriteBatch atlas/flip/text tour ✱ | `Stride.Graphics.Tests\TestSpriteBatch.cs:60-162`, `TestSpriteBatchResolution.cs`, `TestSpriteFont.cs:77-151` | example · Beginners · Rendering | Every `Draw` overload, `VirtualResolution`, `MeasureString`, drop shadows, `TextAlignment`. |
| `MutablePipelineState`, `GraphicsResourceAllocator`, `GetOrCreateSharedData` | `GraphicsDevice.cs:438`, `MutablePipelineState.cs:55` | doc sections · Advanced · Rendering | The three idioms every custom renderer needs; document once. |
| `LaunchParameters` | `Stride.Games\LaunchParameters.cs:32` | note · Getting Started · Gameplay | Built-in command-line dict; the example launcher could use it. |
| Native crash handler ✱ | `shared\NativeCrashHandler.cs:24-107` | toolkit · Advanced · Integration | Crash-dialog suppression + minidump; copyable. |
| MSBuild switches for code-only apps ✱ | `Stride.Core\build\Stride.Core.targets`, `Stride.Graphics\build\*.targets`, `assets\Stride.AssetCompiler\build\Stride.AssetCompiler.targets`, `Stride.Engine\buildTransitive\Stride.Engine.targets:13-20` | doc page · Intermediate · Integration | `StrideGraphicsApi` (one line → D3D12/Vulkan), `StrideAssemblyProcessor` (IL-rewrites your assembly; off ⇒ serialization/clone break), `StrideCompilerSkipBuild`, `StrideSkipAssetsClean`, `StrideProjectAssetExtensions` (the "one folder per app" rule), AOT switches `Stride.Engine.RemoteEffectCompilerEnabled`/`Stride.Games.WinFormsBackendEnabled`/`SDLBackendEnabled`, and the VC++ redist check that runs only under Visual Studio. |

### Rendering

| Item | Source | Verdict · Level · Category | Note |
|---|---|---|---|
| Light shafts (god rays) | `Rendering\Images\LightShafts\` (`LightShafts.cs`, 491) + `engine\Stride.Engine\Engine\LightShaftComponent.cs:21` | example · Advanced · Rendering | Spectacular; requires shadows (spec 18 first). Min/max volume trick is a real GPU lesson; `LightShaftBoundingVolumeComponent.Model` is public. |
| Cel/toon, hair, clear-coat, thin glass materials | `Rendering\Materials\CelShading\`, `Hair\`, `MaterialClearCoatFeature.cs:20`, `MaterialSpecularThinGlassModelFeature.cs` | example · Intermediate · Rendering | Five-line stylised material descriptors invisible outside the GS dropdown. |
| Live material parameters | `MaterialRenderFeature.cs:138,257-267`, `MaterialPass.cs:39`, `MaterialKeys` | both · Beginners · Rendering | `Passes[0].Parameters.Set(...)` animates free; value vs permutation keys explains the hitches. `material.SetColor()` helper. |
| Material node graph in code | `ComputeColors\ComputeShaderClassColor.cs`, `MaterialGenerator.Generate` (`Stride.Assets.Tests\TestMaterialGenerator.cs:35-92` shows the expected mixin tree) | both · Advanced · Rendering | Arbitrary `.sdsl` ComputeColor into a standard lit material; editor's `GizmoShaderMaterial.Create` is a 27-line worked example; `MaterialBlendLayer` at `Stride.Graphics.Tests.10_0\MaterialTests.cs:236-308`. |
| Multi-pass materials | `MaterialFeature.cs:43` (`MultipassGeneration`), `MaterialGeneratorContext.cs:89-160` | example · Advanced · Rendering | Inverted-hull outlines / fur shells; in-tree users: clear coat, hair (diffuse + specular), thin glass. Third outline technique → comparison page. |
| Grab-pass refraction | `engine\Stride.Engine\Rendering\Compositing\ForwardRenderer.cs:127,835`, `Rendering\Utils\OpaqueBase.sdsl` | example · Advanced · Rendering | `BindOpaqueAsResourceDuringTransparentRendering` — off by default, zero docs, real refraction. |
| Render groups & cull masks | `RenderGroup(Mask).cs`, `SceneCameraRenderer.cs:33,102` | example · Intermediate · Rendering | First-person weapon layer, minimap-only objects; the toolkit uses groups pervasively already. Worked usage: particles over UI via a second camera (samples doc #74). |
| Wireframe/x-ray via stage + processor | `WireframePipelineProcessor.cs` (19 lines), editor `PhysicsDebugShapeService.cs` (52); `Stride.Engine.Tests\TesselationTest.cs:209-215` (`RendererInitialized` → `PipelineProcessors.Add`) | both · Advanced · Rendering | Cleanest "what is a render stage" intro. |
| Runtime reflection probe | `Stride.Engine\Rendering\Skyboxes\CubemapSceneRenderer.cs:35`; editor `EditorGameCubemapService.cs` | both · Advanced · Rendering | Live-scene cubemap → feed the *existing* toolkit `SkyboxGenerator`. The un-mined half of the skybox story. |
| Tessellation + displacement | `MaterialTessellationPNFeature.cs`, `MaterialDisplacementMapFeature.cs` | example · Advanced · Rendering | AEN index buffer is generated automatically by `MaterialRenderFeature.cs:363`; obscure but marquee. |
| Spot-light gobo | `LightSpot.cs:84-124` (`ProjectiveTexture`, `UVScale`, `UVOffset`, `ProjectionPlaneDistance`) | example · Intermediate · Rendering | Projector/stained glass in a handful of property sets. |
| HDR auto-exposure + tonemap operators | `Images\LuminanceEffect\LuminanceEffect.cs`, `ColorTransforms\ToneMap\` (9 operators) | example · Intermediate · Rendering | Folded into spec 17; also `GaussianBlur`/`ImageScaler` as general texture utilities. |
| `ImageReadback<T>` | `Images\ImageReadback\` (`:17,47,68,99`) | toolkit · Advanced · Performance | Non-blocking GPU→CPU with staging pool; the "without stalling" sequel to picking. |
| `BackgroundComponent` | `engine\Stride.Engine\Engine\BackgroundComponent.cs:23`, `Rendering\Background\BackgroundRenderFeature.cs:13` | example · Beginners · Rendering | One-component backdrop/skybox; simplest readable `RootRenderFeature`. |
| `ProceduralModelDescriptor` | `Rendering\ProceduralModels\` (`:19,46`; `PrimitiveProceduralModelBase.cs:45,51`) | example · Beginners · Shapes | `UvScale`/`LocalOffset` answered; the layer under the toolkit's own primitives. |
| Material-channel debug view | editor `MaterialFilterRenderFeature.cs` (55 lines) + engine `MaterialStreamDescriptor` (diffuse streams public; emissive/occlusion/cavity descriptors private but re-declarable) | both · Advanced · Rendering | Key-cycled "show normals/roughness/AO". |
| Selection wireframe + tint highlight | editor `WireframeRenderFeature.cs` (111), `HighlightRenderFeature.cs` (99); `AssetHighlighter` colours `(1,0.35,0.25,0.8)`, micro-thread fade | example/toolkit · Advanced · Rendering | How GS really draws selection (filtered stage, depth off, front/back colour); per-material-slot tinting; `HighlightFlash` helper. |
| Overlay scene (2nd SceneSystem) | `engine\Stride.Engine\Rendering\Compositing\EditorTopLevelCompositor.cs` (engine-side!) + editor `EntityHierarchyEditorGame.cs:363-367`; `ExecutionMode.Preview` makes scripts inert | toolkit · Advanced · Rendering | Own lighting (ambient 0.1 + two directionals at 0.45), no clear, gizmo group mask — would simplify every gizmo/debug example. |
| Shader-compile feedback ✱ | `RootEffectRenderFeature.cs:75` (`ComputeFallbackEffect`, public), `RenderEffect.cs:28,68`; engine shaders `Rendering\Editor\EffectCompiling.sdsl`, `CompilationErrorShader.sdsl`, `LightConstantWhite.sdsl`; editor `EntityHierarchyEditorGame.cs:151-213` | example · Advanced · Rendering | Game Studio's pulsing green "compiling" / red "error" placeholder: a fallback effect with lighting forced to `LightConstantWhite`, retried every 5 s. Code-only apps compile at runtime and draw nothing for the first frames — this is the honest fix. Only a 15-line `.sdfx` is editor-side. |
| Skybox material ball ✱ | editor `Preview\SkyboxPreview.cs:39-127`, `MaterialPreview.cs`; engine `SharedTextureCoordinate.sdsl` | example · Intermediate · Rendering | A sphere lit only by `LightSkybox`, metalness/glossiness sliders on a code-built material; `SharedTextureCoordinate` lets multi-UV materials preview on one-UV primitives. Companion to Example01_Material and the Skyboxes library. |
| Planar shadows / mirrors ✱ | `core\Stride.Core.Mathematics\Matrix.cs:2362-2444` (`Shadow`, `Reflection`; zero engine consumers) | example · Intermediate · Rendering | The classic projected-shadow and mirror-world tricks; needs `UseTRS = false` and `CullMode.Front` for the mirror. |
| Preview/thumbnail camera + light rigs ✱ | editor `Preview\PreviewFromEntity.cs:70-130,171-294` (fit `r + r/tan(fov/2)`, `Far = 2.5·max(distance,r)`, headlight rig children of the camera, HDR ×2.22, clear colours), `PrefabEditorLightService.cs` (ambient 0.3 + two directionals 2.5), `ThumbnailGenerator.cs` | toolkit · Beginners · Rendering | Known-good numbers for "why is my scene flat" — but the toolkit already has `AddStudioLighting`; add the *preview orbit camera* and the headlight rig only. |
| Sprite sheet animation in code ✱ | `Rendering\Sprites\SpriteAnimationSystem.cs:109-214`; `Stride.Engine.Tests\SpriteAnimationTest.cs:23-224`, `SpriteRenderer2DTests.cs:56-114` | example · Beginners · Rendering | `new SpriteSheet { Sprites = { new Sprite(name, texture, region) } }` + `SpriteFromSheet`, `SpriteAnimation.Play/Queue/Pause`; the toolkit has zero `SpriteAnimation` uses; pairs with spec 33 and the samples' 2D platformer (#71). |
| Particles over UI / second ortho camera ✱ | `samples\UI\UIParticles…\SplashScript.cs:172-297` | example · Intermediate · Rendering | UI px → ortho world, overlay camera synced each frame, particle parked on a gauge via `WorldMatrix` + `ActualSize`. |
| VR stereo via `VRApi.Dummy` | `Stride.VirtualReality\DummyDevice.cs:15-210` (public; head rotation from phone sensors), `VRApi.cs:11`, `engine\Stride.Engine\Rendering\Compositing\VRRendererSettings.cs` | both · Advanced · Rendering | Full stereo pipeline with no headset; `VRDeviceSystem` already registered in every Game and falls back to the dummy. No test covers it — read `ForwardRenderer.VRSettings` first. |

### Engine, Input, UI

| Item | Source | Verdict · Level · Category | Note |
|---|---|---|---|
| Script priorities & micro-threads | `ScriptSystem.cs:30,94,156,167`, `ScriptComponent.cs:270`, `Scheduler.cs`; bare-scheduler demo `Stride.Core.Tests\TestMicroThread.cs:252-286`, `Stride.Engine.Tests\EventSystemTests.cs:15-46` | example · Intermediate · Gameplay | Sync scripts batch per priority; `AddTask`, `NextFrame`; a `new Scheduler()` run by hand proves continuations resume inside `Run()`. `Priority = -1000` on input scripts is the starters' "single frame input lag" fix. |
| `AsyncSignal` / `AsyncAutoResetEvent` | `core\Stride.Core.MicroThreading\` | example section · Advanced · Gameplay | Producer/consumer between AsyncScripts. **Avoid `Channel<T>`** (see upstream findings). |
| Scene-graph events / live inspector | `EntityManager.cs:54-74`, `SceneInstance.cs:130` | toolkit · Intermediate · Interaction | Event-driven ImGui scene tree; `SceneInstance.GetCurrent(RenderContext)` from render features. |
| Child scenes + `Scene.Offset` | `Engine\Scene.cs:67,72` | example · Intermediate · Gameplay | Chunk streaming / floating origin, fully code-only; the TopDownRPG's contact-depth streaming (samples doc #63) is the gameplay version. |
| Custom `TransformLink` + `PostOperations` | `TransformLink.cs:10,17`, `TransformComponent.cs:41` | both · Advanced · Gameplay | Replace "multiply by parent" with anything; the hook under `ModelNodeLinkComponent`; also the engine-blessed spline-follow mechanism. |
| `ModelNodeLinkComponent` | `Engine\ModelNodeLinkComponent.cs:16,33,55` | example · Intermediate · Gameplay | Sword-in-hand; needs a skinned asset — the `stride-pack-animatedmodels` mannequin is the sanctioned MIT source. |
| `IBlendTreeBuilder` ✱ | `AnimationComponent.cs:45,212`, `AnimationOperation.cs:28-61`; three worked state machines in the FPS/TPP/RPG starters' `AnimationController.cs` | example · Advanced · Gameplay | Not "doc snippet" any more: the samples doc's #72 puts it over procedural clips with zero assets. |
| Additive animation ✱ | `Stride.Assets.Models\AnimationAssetCompiler.cs:197-265` (`AnimationBlender` subtract — runtime-public), `AnimationClip.Optimize()` | section · Advanced · Gameplay | Build an additive clip at runtime; section of spec 9. |
| Event-driven input | `IInputEventListener.cs:9,17`, `InputManager.cs:148,468` | example · Intermediate · Input | No lost inputs between frames; pooled events. |
| Text input + IME | `ITextInputDevice.cs`, `TextInputEvent.cs` | both · Intermediate · Input | The only correct typed-text path; in-game console/chat, pairs with SignalR. |
| Gamepad vibration, indices, custom layouts ✱ | `IGamePadDevice.cs:30-66` (four-motor `SetVibration`), `GamePadLayouts\GamePadLayout.cs:13-198`, `GamePadLayouts.AddLayout`, `GamePadLayoutDS4.cs`, `InputManager.cs:226,231,647` | example · Beginners → Advanced · Input | Rumble, hot-plug, local-multiplayer index model; mapping a raw `IGameControllerDevice` to a `GamePadState` (needs hardware). Samples ship four identical `GetLeftThumbAny` helpers — the backlog's gamepad-helpers row. |
| Mouse lock + raw input | `InputManager.cs:143,357,370` | toolkit · Beginners · Input | Pointer lock for the free-fly controller; normalised-vs-absolute is the #1 mouse-look bug; `Input.MousePosition = (0.5,0.5)` before locking (samples). |
| Input event logger ✱ | `Stride.Input.Tests\TestInputEvents.cs:22-83`, `InputTestBase.cs:11-95` | both · Beginners · Input | "What is my controller sending" — folded into spec 37. |
| 2D camera zoom-to-cursor ✱ | editor `SpriteEditor\ViewModels\ViewportViewModel.cs:158-161` | both · Beginners · Input | `Offset = newScale/oldScale·(Offset + p) − p`; ortho `OrthographicSize = 1/scale`. The toolkit's 2D camera lacks it. |
| Custom `UIElement` + `ElementRenderer` | `Stride.UI\Renderers\ElementRenderer.cs:15`, `UIRenderFeature.cs:336-346`, `DependencyPropertyFactory.cs:11`; `MeasureOverride`/`ArrangeOverride` validators in `Stride.UI.Tests\Layering\` | example · Advanced · UI | Radial gauge/minimap element. |
| Routed events | `Stride.UI\Events\EventManager.cs:75,129`; `Stride.UI.Tests\Events\EventManagerTests.cs`, `UIElementEventTests.cs:167-379` | example · Intermediate · UI | Tunnel/bubble/`Handled`/`handledEventsToo`; `RegisterClassHandler` = "all buttons click-sound" in one line. |
| UI hit-testing | `UIRenderFeature.Picking.cs:356-378` (public static `GetElementsAtPosition`), `UISystem.cs:43` | toolkit · Intermediate · Interaction | One-liner most people reimplement; `RaiseTouchDownEvent` fakes a press with no input at all. |
| UI layout engine, headless ✱ | `Stride.UI.Tests\Layering\UIElementLayeringTests.cs:73-122,1028-1204`, `GridTests.cs`, `CanvasTests.cs`, `ScrollViewerTests.cs` | example · Intermediate · UI | Measure/arrange precedence, what invalidates what, star min/max, 3D layers, 4.4 `Gap`s, `ScrollTo` deferred until Arrange; runs with no device (spec 26 `_UILayout`). |
| `EditText`, `ScrollViewer` anchors, virtualized `StackPanel` ✱ | `Stride.UI.Tests\Regression\EditTextTest.cs:38-352`, `ScrollViewerAnchorTest.cs`, `StackPanelTest.cs:112-176` | example · Intermediate · UI | `MaxLength`, `CharacterFilterPredicate`, password mode, selection API; `SnapToAnchors`; 1000-button virtualization. |
| `UIElementLinkComponent` | `Stride.UI\Engine\UIElementLinkComponent.cs`; `samples\UI\UIElementLink` (name-matched) | example · Advanced · UI | 3D entity attached to a UI element (item preview in an inventory slot). |
| UI adorners + magnet snapping | editor `UIEditor\Adorners\`, `UILayoutHelper.cs` | example · Advanced · UI | Promoted to spec 35. |
| `TextBlock` ellipsis trimming ✱ | `Stride.Core.Presentation.Wpf\Controls\Trimming.cs` (`ProcessTrimming`, Begin/Middle/End × Character/Word) | toolkit · Beginners · UI | `Stride.UI.TextBlock` has only `WrapText`. |
| Colour picker maths ✱ | `Stride.Core.Presentation.Wpf\Controls\ColorPicker.cs:274-386` | example · Beginners · UI | SV square per hue via `ColorHSV(...).ToColor()`; low priority. |
| Video → your texture ✱ | `Stride.Video.Tests\VideoSmokeTest.cs:124-236` | spike · Advanced · Rendering | `VideoComponent { Source, Target = yourTexture }`, `VideoBackendRegistry.PreferredBackendName`, `ForceSoftwareDecode`; still asset-bound (`Video` has no public provider) — spec 28's database route is the way in. |

### Editor, core, misc

| Item | Source | Verdict · Level · Category | Note |
|---|---|---|---|
| ViewCube + corner axes | editor `CameraOrientationGizmo.cs` (364), `SpaceMarker.cs` (215), `GizmoViewportRenderer.cs` (83, internal) | both · Advanced · Rendering | Highest "I want that" widget; sub-viewport + second camera + math picking + 3D SpriteBatch text in one. |
| Billboard icon markers | editor `BillboardingGizmo.cs:35-39`, `EntityGizmo.cs` | toolkit · Beginners · Rendering | Constant-screen-size world icons; `PixelsPerUnit = texture.Width` is the knob. |
| Frustum & light-shape wireframes | editor `CameraGizmo.cs:126-162`, `LightSpotGizmo.cs`, `LightPointGizmo.cs` | example · Intermediate · Shapes | Dynamic line-mesh recipe + frustum corner math. |
| Bounding-box gizmo ✱ | editor `ModelGizmo.cs:52-61`, `NavigationBoundingBoxGizmo.cs:53-69` | toolkit · Beginners · Shapes | Unit line box at `ModelComponent.BoundingBox.Center × Extent`; shown only while selected. |
| Constraint visualiser | editor `PhysicsConstraintGizmo.cs` (569) | example · Advanced · Physics | Adapt to Bepu; would upgrade the whole Example15 family; also the clearest `GeometricPrimitive` lifetime warning. |
| Gizmo registry (`GizmoManager`) | editor `EditorGameComponentGizmoService.cs` (397) + engine `Engine\Gizmos\` | toolkit · Intermediate · Gameplay | Attribute-driven auto-gizmos on component add; generalises `Example08_CollidableGizmo`. |
| Navmesh debug visual ✱ | editor `EditorGameNavigationMeshService.cs:224-366` | toolkit · Intermediate · Gameplay | Two materials per group (updated tiles highlighted), `offset.Y = 0.05·group`, alpha 0.33; `NavigationMeshTile.GetTileVertices` is internal — fan-triangulate public `Data` yourself. Pairs with spec 11. |
| `CalculateBoundSphere` | `editor\Stride.Editor\Engine\EntityExtensions.cs:67`; engine-side `Stride.Graphics.Regression\TestCamera.cs:359-423` | toolkit · Beginners · Gameplay | Needed by spec 24; handles skinned/sprite/particle bounds; drop the SpriteStudio block. |
| Frustum culling visualiser | `core\...\BoundingFrustum.cs:11,47,104`, `BoundingBoxExt.cs` | both · Intermediate · Rendering | Cull against a second camera, green/red debug boxes; `IsVisible(entity)` helper. |
| In-game log console | `core\...\Diagnostics\Logger.cs`, `GlobalLogger.cs:37`, `LogListener.cs`; `IAppSettingsProvider` via `[AssemblyScan]` (`AppSettingsManager.cs:41-72`) is the only way to set `LoggerConfig.Level` early | both · Beginners · UI | Route engine+game logs to overlay/ImGui + rotating file via VFS. |
| `GuillotinePacker` | `core\Stride.Core.Mathematics\GuillotinePacker.cs` (users: shadow atlas, font cache) | both · Intermediate · Rendering | Runtime atlas packing; pairs with `TextureCanvas`. Spec 33 is the MaxRects big brother. |
| `ServiceRegistry` DI | `core\Stride.Core\ServiceRegistry.cs:115` (`GetOrCreate<T>() where T : class, IService` + static `NewInstance(registry)` convention), `ServiceRegistryExtensions.cs:29` (`GetServiceLate<T>`) | example · Intermediate · Gameplay | Share state without singletons. |
| `RandomSeed` | `core\...\RandomSeed.cs:18-59` | toolkit · Intermediate · Gameplay | Stateless deterministic (seed, index) randomness for procedural gen/replays; toolkit `VectorHelper.SeedRandom` is the stateful cousin. |
| Colour helpers | `ColorHSV.cs`, `Color.Palette.cs` (143 colours), sRGB↔linear (`Color4.cs:340,349`, `MathUtil.cs:452,463`), `ColorExtensions.StringToRgba/RgbaToString` (not a round trip — `#AARRGGBB` out) | toolkit · Beginners · Rendering | `FromHsv`, palette generation, and the linear-vs-gamma "why is my red wrong" note. |
| Maths gotchas page ✱ | `Quaternion.cs:432-447` (`a*b` = apply a then b), `Matrix.cs:457-470` (`System.Numerics` conversions transpose — Bepu bridge uses `Unsafe.As`), `Int3.Round` vs truncating cast, `Matrix.DecomposeXYZ` near gimbal lock (editor `RotationCurveViewModel.cs:207-238` has the precise variant), `TestRotationsData.cs` singularity sets | doc page · Beginners · Gameplay | The five conventions everyone trips over. |
| Turret tracking / look-at ✱ | `Quaternion.cs:576,695,779,1168,1308`, `Vector3.cs:1438` | example · Beginners · Gameplay | `RotateTowards` with a max angle per frame; `AngleBetween`; the NaN trap in engine `LookRotation`. |
| `TrackingCollection<T>` | `core\Stride.Core\Collections\` | supporting · Beginners · UI | Observable collections behind the scene graph; inventory-updates-UI demo material. |
| Custom VFS provider ✱ | `core\Stride.Core.IO\VirtualFileProviderBase.cs:9-110`, `Stride.Core.Tests\MemoryFileProvider.cs` (99 lines) | toolkit · Intermediate · Gameplay | Mount `/mods` from memory or network; pairs with spec 8. |
| Mod loading ✱ | `core\Stride.Core\Reflection\AssemblyRegistry.cs:75-95,178-224`, `DataSerializerFactory.cs:183-247` (unload has a TODO), `VirtualFileSystem.RemountFileSystem` | example · Advanced · Integration | Register a plugin assembly so its `[DataContract]` types serialize and its scripts run. |
| `Stride.Core.Design` runtime-safe pieces ✱ | `MicroThreadLock.cs`, `Threading\AsyncLock.cs`, `ObjectCache.cs`, `Windows\GlobalMutex.cs`, `TransactionStack` | copy · Advanced · Gameplay | Not editor-TFM after all; the package's dependencies are the blocker — copy files. Undo/redo via `TransactionStack`. |
| RenderDoc capture key | `tools\Stride.Graphics.RenderDocPlugin\RenderDocManager.cs:79,129-135` | example · Advanced · Rendering | Press F12 → `.rdc`; D3D11/D3D12 **and Vulkan**; NuGet publication unverified. |
| Gettext localization | `core\Stride.Core.Translation\` | example · Intermediate · UI | Runtime TFM (`net10.0`), works; `ResxTranslationProvider` alternative. |
| Content database ✱ | promoted to spec 28 | | |
| `Stride.Core.Yaml` ✱ | `Serializer.cs:57-391`; depends only on `Stride.Core.Reflection` | note · Intermediate · Gameplay | Runtime-usable; Example07 uses third-party YAML instead — either is fine, say so. |
| Game Studio default-scene numbers ✱ | `Stride.Assets\Entities\SceneAssetFactories.cs:23-90`; editor camera `SceneEditorSettings.cs:19-106` (position (4,2,4), pitch −π/12, yaw π/4, speed 3, wheel factor 12); `CameraComponent.cs:28-36` (FOV 45°, near 0.1, far 1000) | doc · Getting Started · Rendering | The numbers a code-only scene should copy to "look like a new GS project". |

---

## Considered and rejected

Recorded so the same paths aren't mined a third time. (Each survey's full rejection list, with
reasons, is preserved in its report; this is the merged short form.)

**Already mined / already covered:** `GeometricPrimitives`, `FastTextRenderer`, `Font\*` runtime
fonts, `ViewportGridGizmo` + grid services (only the plane-switch/colour-space details are new —
a docs addendum to engine-patterns.md, not an example), `AxialGizmo` (ported verbatim),
`EditorGameHelper` picking, gizmo colour materials, `InstancingRenderFeature`,
`DebugTextSystem`, editor `DebugShapes\` (overlaps `Example08_DebugShapes`), three-point lighting
(`AddStudioLighting` exists), orbit camera (exists — extend), GPU-stats overlay (exists).

**Asset-pipeline-bound, unreachable code-only:** `Sound`/`CompressedSoundSource` construction
(internal + ffmpeg) and therefore `AudioEmitterComponent` (its `Sounds` dictionary holds `Sound`),
`StreamingManager`/`StreamedBufferSound` (desktop path is a stub), `.sdsheet` atlas packing (but
`Sprite`/`SpriteSheet` themselves are code-constructible — spec 33), `SkinningRenderFeature`
(needs a rigged import — the mannequin pack is the MIT source), texture-content serializers,
`Video` (no public provider — spec 28 is the way in), `ContentStreamingService`, `.sdeffectlog`
recording (`EffectSystem.EffectUsed` internal), V-HACD decomposition (internal native; runtime
`ConvexHull` from points is fine).

**Verified absent — document, don't hunt:** splines, model LOD, UI data binding, GPU particles,
occlusion queries, decals, deferred shading, `Microphone` capture, HRTF on **Linux/macOS**,
Bepu heightfields, navmesh crowd, terrain mesh generator, Bullet vehicle/soft bodies in the
wrapper, variance shadow maps, runtime SDF font generation, working hot reload (`LiveAssemblyReloader` throws),
rubber-band/hover selection in the editor, sound waveform preview, zoom-to-cursor in any
game-side editor camera, manual ticking under the headless context.

**Too deep / internal plumbing:** descriptor sets and root signatures below `EffectInstance`,
constant-buffer suballocation (`ResourceGroup*`, `BufferPool`), explicit barrier APIs, deferred
command lists, `RootRenderFeature` beyond what Example13 and the samples' `BendFogRenderFeature`
cover, MRT semantics (`ResourceResolver`) — though the orphaned
`Stride.Graphics.Tests.10_0\Assets\MultipleRenderTargetsEffect*.sdsl` is a seed —, the reflection
stack, `Stride.Core.AssemblyProcessor` (blog post, not example; its analyzers `STRDIAG000-011`
are the user-facing part), `Stride.Core.Assets`/Quantum (editor asset model), `TestUpdateEngine`
(raw-pointer property poking).

**Editor-only glue with no runtime story:** controllers/dispatchers, Quantum change watchers,
content loaders, recovery services, preview/thumbnail *infrastructure* (the previewers themselves
were mined — specs 31, inventory), GameStudio shell, `AssemblyReloading\*`, `GameStudio\Debugging\*`
(Roslyn SCC condensation), `GraphicsCaptureClient` (Windows.Graphics.Capture of a foreign
window), WPF/Avalonia controls beyond the harvested maths, `GameEngineHost` (WS_CHILD embedding —
`GameContext` covers it), gizmo icon `.resx` assets (mechanism portable, PNGs not ours).

**Niche / platform-bound:** mobile sensors, UWP/WMR, `SpriteStudio` runtime, `Stride.Voxels`
(needs `ForwardRendererVoxels` + non-default package — possible far-future stretch), `VROverlay`
(hardware only), VTune hooks, `Stride.Engine.NextGen` (one dead file), `Stride.FontCompiler`
(an empty project — the real compiler is `Stride.Assets/SpriteFont/Compiler`, FreeType + native
msdfgen), `Stride.TextureConverter`/`Importer.3D` (native deps, not TFM).

**Test harnesses as dependencies:** `Stride.Graphics.Regression.GameTestBase` (hard-codes the
Stride repo layout), `xunit.runner.stride` (mobile/interactive concerns), `Stride.Tests.Combined`,
`Stride.GameStudio.AutoTesting`, `nunitlite`, `Stride.Games.AutoTesting` as-is (module-initializer
side effects; see Infrastructure).

**Small-but-noted:** `AngleSingle`, vector swizzles, `Half*`/`Int2/3` (mention inside existing
examples), `LaunchParameters`, `ContentManagerStats`, `LightShaftsVolumeGizmo`'s
`LocalMatrix = WorldMatrix` trick, `GameSettings` injection into code-only games (the
`IGameSettingsService` slot is free — now a proper toolkit-helper candidate, see facts),
`[ComponentCategory]` on components (belongs in the DataContract note), `SoundInstance` odds
(`PlayExclusive`, `SetRange`-based seeking — `Position` is range-relative), `SystemFontProvider`
fallback table (toolkit-side findings), `TemporaryFile/Directory`, `LZ4Stream`, `ObjectCollector`.

---

## Upstream findings

Bugs and doc gaps found while verifying — candidates for `notes/upstream/` drafts. Items 1–2 and
5 stand; 3–4 are corrected; 6+ are new from the cross-check.

1. **`Channel<T>` throws `NotImplementedException` on its default path.**
   `core\Stride.Core.MicroThreading\Channel.cs`: with the default
   `ChannelPreference.PreferReceiver`, `Send` with a waiting receiver hits `throw new NotImplementedException()`
   with a commented-out `//await Scheduler.Yield();` (`:70-75`); mirrored for `Receive` under
   `PreferSender` (`:100-105`). The engine sidesteps it (`Scheduler.cs:53` constructs with
   `PreferSender`, and `NextFrame()` always takes the queuing branch). Also `// TODO: Thread-safety`.
2. **`SoundInstance.Position` NREs for dynamic sources.** `SoundInstance.cs:402` dereferences
   `sound`, set only by the internal static-sound constructor (`:64-68`); the guard at `:398` means
   it fires only while playing.
3. **`ShaderClassString.ShaderSourceCode` is dead** — corrected: `ShaderClassCode` *is* consumed
   (`EffectCompilerBase.cs:50-56`, `ShaderMixinContext.cs:295`), but nothing reads the inline
   `ShaderSourceCode` string; the compiler resolves by `ClassName` and silently ignores inline
   source. Doc gap or API cleanup.
4. **`DynamicNavigationMeshSystem` starts disabled and only GameSettings can enable it** —
   corrected from "never registered": `BoundingBoxProcessor.cs:16-27` registers it; `Enabled = false`
   at `DynamicNavigationMeshSystem.cs:64`; `NavigationSettings.EnableDynamicNavigationMesh`
   defaults false and code-only games have no settings. Doc gap; a public toggle would help.
   Related ask: a public `NavigationMeshBuilder.Add(Vector3[] vertices, int[] indices, Matrix)` so
   non-Bullet users can feed geometry (`NavigationMeshInputBuilder`/`NavigationBuilder` are internal).
5. **HRTF parameters are dead on OpenAL** (`OpenAL.cpp:378-410`) — Linux/macOS only. The docs
   should say HRTF is Windows-only, and that `AudioEngineSettings.HrtfSupport` is the gate.
6. **`CompilationMode.Debug` and `Release` produce identical D3D11 bytecode.** The D3D compiler
   applies `OptimizationLevel` only when `Debug` is false (`Direct3D/ShaderCompiler.cs:84-96`), and
   both modes set `Debug = true`; `D3DCOMPILE_SKIP_OPTIMIZATION` is commented out. So the
   parameter's own documentation ("level 0 with debug information" vs "level 1 with debug
   information") is wrong for D3D11, Vulkan/D3D12 never read the level, and code-only games
   (which never get `SetCompilationMode`, `Game.cs:383-386`) lose nothing except the option of
   `AppStore`. Doc fix at minimum; the `Debug` branch should probably set
   `D3DCOMPILE_OPTIMIZATION_LEVEL0` (or skip optimisation) so the mode means what it says.
7. **`ProcessorManager` labels every flexible processor with its own type name** in profiling keys
   (`ProcessorManager.cs:95,97` uses `GetType().Name`).
8. **`AudioSystem.OnActivated/OnDeactivated` NRE** when native audio init failed (`AudioSystem.cs:139,145`).
9. **`VirtualButton.Pointer`** is unregistered in `Find` and its Pressed/Released read `DownPointers`
   (`VirtualButton.Pointer.cs:142-150`); `VirtualButtonTwoWay.Is*` always false (`:53-66`).
10. **`GameWindowHeadless` ignores `IsUserManagingRun`** (`GameWindowHeadless.cs:62-72`) and
    `GameContextHeadless` cannot set it — no manual ticking headless.
11. **Vulkan device creation demands `VK_KHR_swapchain` even headless** (`GraphicsDevice.Vulkan.cs:450,629-636`)
    while the comment at `:453` says it is presentation-only; forces Xvfb on Linux CI.
12. **Nothing registers `LightProbeRenderer`** — `GraphicsCompositorHelper.CreateDefault` omits it,
    so baked probes are silent in code-only compositors.
13. **Sprite picking depends on the render stage being *named* `"Picking"`** (`SpriteRenderFeature.cs:84`,
    `//TODO string comparison`).
14. **`LiveAssemblyReloader` is dead code** (`Stride.Debugger\...\LiveAssemblyReloader.cs:33`).
15. **Desktop `/local`/`/roaming`/`/cache` point next to the exe** with `// TODO` comments
    (`PlatformFolders.cs:80-134`).
16. **`UnmanagedArray<T>` is `[Obsolete]` yet required by `HeightfieldColliderShape` ctors.**
17. **`Quaternion.LookRotation` NaNs on degenerate input** (`Quaternion.cs:695`, no normalisation).
18. **`ColorExtensions.RgbaToString`/`StringToRgba` are not a round trip** (`#AARRGGBB` out).
19. **`Stride.Graphics.Regression.GameTestBase` hard-codes the repo layout** (`build/Stride.slnx`,
    `gh workflow` names) — unusable for third-party gold tests.
20. **Temporal AA and subsurface scattering are implemented and wired to nothing** (no compositor,
    test or editor reference; the TAA effect has no projection jitter). Either finish and expose
    them or document them as experimental.
21. **`Material.New` leaves `Descriptor` null** (`Material.cs:36-52`), which breaks
    `MaterialBlendLayer` resolution (`MaterialGeneratorContext.cs:62`) for runtime-built layer
    materials with an unhelpful "Unable to find material".
22. **`MSAAResolver.Enabled` is hard-wired true** (`MSAAResolver.cs:80-82`) — the one exception to
    "every post effect can be disabled".
23. **Orphans:** `SelectedSpriteShader.sdsl`, `FlattenLayers.sdsl`, `SwapUV.sdsl`,
    `CameraCube.sdsl`, `ComputeColorCave.sdsl`/`ComputeColorOutdoor.sdsl` (no C# node),
    `ShadowMapFilterVsm.sdsl`/`ShadowMapCasterVsm.sdsl` (VSM filter type is an empty namespace),
    `RenderStageFilter` (abstract, zero subclasses),
    `ModelComponentPickingShader/Effect`, `StrideEditorHighlightingEffect.sdfx`,
    `MultipleRenderTargetsEffect*` test shaders, `CustomEffect.sdfx:21` references a non-existent
    `CustomShader2`; `AnimationKeyTangentType` has a single member so `Cubic` curves are
    unreachable and `Interpolator.Quaternion.Cubic` throws; `TexturePreview` samples colour with
    `TextureFilter.ComparisonPoint`; `LightProbeGizmo` instantiates SH order 5 for order-3 data;
    `EditorGameNavigationMeshService.ToggleVisiblity` overwrite (`:505`); `Video` has no public
    provider/factory; templates ship dead files (TPP `BasicCameraController.cs`, TopDownRPG's
    buggy unused `InputManagerExtensions.cs`) and Platformer2D calls `HandleAnimation` twice per frame.

## Toolkit infrastructure (not examples)

- **`Stride.Games.AutoTesting`** (`engine/Stride.Games.AutoTesting/`, **published on nuget.org**,
  4.4.0-beta3…beta5) — 4.4 screenshot-regression harness: registers via `[ModuleInitializer]`
  (which also forces `STRIDE_GRAPHICS_SOFTWARE_RENDERING=1` unless `STRIDE_TESTS_GPU=1` — a side
  effect on every consumer), swaps in simulated input, exposes `WaitFrames`/`WaitTime`/
  `Screenshot(name, threshold, claudeFallback)`/`PressKey`/`Tap`/`Exit`, captures at end of Draw
  with alpha forced opaque, writes `screenshot-test/screenshots/*.png` + `done.json`. It is **not
  headless** (hidden window + WARP; Linux needs Xvfb). The **LPIPS comparison is not in the
  package**: it lives in `sources/tests/Stride.ScreenshotComparator` (unpublished; ~260 lines +
  a 10 MB `lpips_alex.onnx` checked in, `Microsoft.ML.OnnxRuntime` + ImageSharp; optional Claude
  vision fallback needing an API key; `VisionGate` re-judges deferred frames). `Stride.Graphics.Regression`
  is unpublished, xunit-bound and repo-bound. `FrameGameSystem` — drop; `WaitFrames` covers it.
- **Recommended toolkit CI path** (from the tests sweep): keep the toolkit's own `ScreenshotCapture`
  + `capture-screenshots.cs`; add a `STRIDE_TOOLKIT_HEADLESS=1` switch to `RunCore`
  (`GameContextHeadless`), let the orchestrator — never the library — set the software-rendering
  env var; build the examples once and run the exes in Release (Debug requests a D3D debug
  device); vendor the LPIPS comparator (threshold 0.05, per-example overrides, no Claude fallback
  initially); one baseline bucket (Windows/D3D11/WARP pinned to `Microsoft.Direct3D.WARP` 1.0.20 as
  Stride does); nightly/dispatch with sharding, since ~60 examples at 1280×720 under WARP is
  30–60 min; skip SignalR, MyraUI, ImGui-under-headless (unverified), DPI-aware and `_Temp*`; drive
  interactive examples with spec 37's input script. Cheap wins first: an examples build job (none
  exists), the headless `.sdsl` compile test, and smoke-running each example's `Start` for N
  frames without a compositor. Deserves its own plan doc.
- **`DelegateSceneRenderer`** — slots into the toolkit's `AddSceneRenderer`/`EnsureSceneRenderer<T>`
  unchanged; docs only.
- **`game.UseGameSettings(...)`** — one helper that fixes the no-settings fallback table (compilation
  mode, HRTF, Bepu navigation throwing, rendering defaults) before `Run()`.
- Editor/test-derived helpers that are toolkit-shaped regardless of examples: `CalculateBoundSphere`,
  the `InputArbiter`, `OverlayScene`, `GizmoManager`, `InGameLogListener`, `ConfigureShadows`,
  `material.SetColor`, `AddLightProbeRenderer`, `RunHeadless`, `InputScript`, `SpriteAtlasBuilder`,
  `PreviewOrbitCamera`, `HighlightFlash`, `BoundingBoxGizmo`, `NavigationMeshDebugVisual`,
  `ParticlePresets`, `DrawStringFitted`, `TextureViewer`, `ContactEvents` adapter, `SpawnTimed`
  (samples), `GetLeftThumbAny` gamepad helpers (samples), `UILayoutHelper` port.

## Coverage impact

If only the full specs above were built, the categories fill as: **Audio** +3, **Input** +4
(virtual buttons, gestures, orbit camera, simulated replay), **Interaction** +2, **Gameplay** +7,
**Performance** +5 (incl. many-lights), **Integration** +2 (headless, LAN sockets) — plus the
samples doc's #76 (async web API) and the mod-loading row —, **Physics** +4 (Bepu triggers,
heightmap, breakable joints, interpolation), **UI** +3, plus a lot of Rendering/Shapes depth
(the compact specs are mostly Rendering).

Suggested first five, balancing quick wins against gap-filling:
`Example27_Audio_ProceduralSound` + `Example27_Audio_WavFile` (one PR, new category, new library),
`Example40_PostEffects` (biggest visible payoff per line — and it fixes `AddCleanUIStage`),
`Example48_Headless` (Integration unlocked, the CI story starts here, and the toolkit's own test
fixture is already half of it), `Example29_PickingNoPhysics` (Interaction unlocked, zero
dependencies), and the `GameTime.Factor` + `UseGameSettings` pair of one-line additions folded
into existing examples (near-free; `UseGameSettings` is what gives a code-only game HRTF, physics
and navigation settings — the shader-mode half turned out to be moot, see correction 4).
