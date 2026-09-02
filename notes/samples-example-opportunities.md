# Stride Templates & Samples — Example Opportunities

A harvest of the project templates and samples that ship with the Stride engine, for toolkit
example ideas and game-dev patterns. Source tree: `D:\Projects\GitHub\stride` at commit
`d87510abb` (2026-08-29, "Merge branch 'master' into bepu-2d"). All paths below are relative to
`D:\Projects\GitHub\stride\samples\` unless marked `sources\`.

**Status: research, nothing agreed.** Companion to [engine-example-opportunities.md](engine-example-opportunities.md)
(the "engine sweep") and [starbreach-example-opportunities.md](starbreach-example-opportunities.md).
Same graduation path: pick → backlog row / TODO entry → build. Example numbers start at **60**
(27–47 are the engine sweep's; 48–59 are claimed by the engine sweep's cross-check additions).

**The iron rule, as for Starbreach: pattern, not code.** Everything except `Physics\BepuSample` is
on **Bullet** (`Stride.Physics`), and most of it is asset-bound (prefabs, sprite sheets, FBX,
`.sduipage`). Every candidate below says what survives a code-only, Bepu, procedural-primitive
rebuild.

---

## 1. Ground truth

### 1.1 What "templates" actually are

`sources\templates\` holds **no template content** — just four packaging csprojs plus
`Stride.Templates.Common.targets` and `README.md`:

| Package | `dotnet new` short names | Source folder |
|---|---|---|
| `Stride.Templates.Games` | `stride-game` | `samples\NewGame\NewGame` |
| `Stride.Templates.Games.Starters` | `stride-fps`, `stride-platformer2d`, `stride-topdownrpg`, `stride-thirdpersonplatformer`, `stride-vrsandbox` | `samples\Templates\*` |
| `Stride.Templates.Samples` | 18 feature demos (`stride-jumpyjet`, `stride-bepuphysics`, `stride-ui-menu`, …) | `samples\{Audio,Games,Graphics,Input,Particles,Physics,Tutorials,UI}` |
| `Stride.Templates.AssetPacks` | `stride-pack-buildingblocks`, `-animatedmodels`, `-materials`, `-particles` (item templates) | `samples\Templates\Packs\*` |

The mapping is in `sources\templates\Stride.Templates.Samples\Stride.Templates.Samples.csproj:15-70`
and `...Games.Starters.csproj:14-`. `sources\editor\Stride.Assets.Presentation\Templates\` is WPF
wizard code (asset-from-file generators, `DotNetNewTemplateBridge`), not template content — nothing
to harvest there. `samples\Library\Library` is a blank class-library template (csproj only) and
`samples\Others\NativeLinking` contains **only two screenshots and no project** — both rejected.

Versioning: every sample csproj targets **`net10.0`** and references **Stride 4.4.0** from
nuget.org (`samples\nuget.config`); `NewGame` uses `$EngineVersion$`. `SharedAssemblyInfo.cs:59`
pins `SamplesVersion = "4.4.0"`. Platform exe projects are `net10.0-windows`.

**Physics engine:** every starter, game, tutorial and UI sample references `Stride.Physics`
(Bullet). Only `Physics\BepuSample` references `Stride.BepuPhysics`. The `stride-game` "New Game"
template still defaults to Bullet (`NewGame\NewGame\MyTemplate.Game\MyTemplate.Game.csproj`
references `Stride.Physics`, `Stride.Navigation`, `Stride.Video`, `Stride.Particles`, `Stride.UI`).
The `bepu-2d` branch does not touch any template.

### 1.2 Per-template inventory (starters + NewGame)

Line counts exclude platform `Main` stubs. "Wired" means the class name appears as a component in
an `.sdscene`/`.sdprefab` (verified by grepping every YAML tag in the tree).

| Template | .cs / lines | Scripts (wired ✓ / dead ✗) | Physics | Assets | Code-only? |
|---|---|---|---|---|---|
| **FirstPersonShooter** (`stride-fps`) | 12 / 992 | `PlayerInput`✓ `PlayerController`✓ `FpsCamera`✓ `WeaponScript`✓ `AnimationController`✓ `EffectController`✓ (`TriggerScript` abstract base, `TriggerEvent`/`TriggerGroup` `[DataContract]` POCOs, `Utils`, `InputManagerExtensions`) | Bullet; `CharacterComponent` (Gravity −20, `JumpSpeed: 0` → no jump, MaxSlope 45°) at `Assets\MainScene.sdscene:4851-4863`; 143 `StaticColliderComponent` (PrototypingBlocks) | Heavy: mannequin + 4 clips (idle/walk/shoot/reload), gun FBX with `ModelNodeLinkComponent` to `MachinegunBone`/`MagazineBone` (`:5686-5689`, `:7356-7359`), VFXPackage prefabs, bullet LED sprite sheet | Logic yes (capsule + camera + raycast); animation state machine no (needs clips) |
| **ThirdPersonPlatformer** (`stride-thirdpersonplatformer`) | 7 / 1068 | `PlayerInput`✓ `PlayerController`✓ `ThirdPersonCamera`✓ `AnimationController`✓; **`BasicCameraController.cs` (188 lines) ✗ dead** — wired to nothing in `MainScene.sdscene` | Bullet; `CharacterComponent` Gravity −40, `JumpSpeed 13`, `FallSpeed 10`, MaxSlope 45° (`MainScene.sdscene:2848-2851`); 144 static colliders | mannequin + 6 clips (idle/walk/run/jump-start/airborne/landing) | Camera + controller yes; blend tree needs clips |
| **TopDownRPG** (`stride-topdownrpg`) | 14 / 1536 | `PlayerInput`✓ `PlayerController`✓ `AnimationController`✓ `Trigger`✓(×10 + prefabs) `CrateScript`✓ `CoinScript`✓ `LootCoinScript`✓ `MusicScript`✓ `SceneStreaming`✓(×5, `DynamicScene` only) `EnableDynamicNavigation`✓(`DynamicScene` only); **✗ dead:** `InputManagerExtensions.cs` (no caller, and buggy — see warts), `TaskExtension.InterruptedBy`, `PlayerInput.JumpEventKey` (declared, never broadcast) | Bullet + `Stride.Navigation`; `NavigationComponent` with baked `NavigationMesh.sdnavmesh` in `MainScene` (`:2927-2930`), `NavigationMesh: null` + dynamic system in `DynamicScene` (`:197-203`); `NavigationBoundingBoxComponent` in every chunk scene | mannequin + 4 clips, VFX prefabs, crate/coin models, music + 2 sfx | Movement/pathing/triggers yes; needs a navmesh (4.4 managed DotRecast makes that possible) |
| **Platformer2D** (`stride-platformer2d`) | 3 / 241 | `PlayerController`✓ `CollectCoin`✓(×13 + prefab) `CoinRotation`✓ | Bullet in 2D: `CharacterComponent` capsule, `StepHeight 0.05`, `MaxSlope 0`, `JumpSpeed 7`, Gravity −10 (`MainScene.sdscene:558-572`); 67 static colliders; **orthographic camera `OrthographicSize: 16`** (`:1910-1915`) | 4 PNGs + 1 wav; sprite sheets as assets | **Yes** — the most code-only-friendly starter. Uses modern C# (`required`/`init`, `Nullable enable`) |
| **VRSandbox** (`stride-vrsandbox`) | 6 / 491 | `HandController`✓(×2) `TeleportController`✓ `PlayerInput`✓; `VRGame : Game` used by `VRSandboxApp.cs` | Bullet; 30 rigidbodies; Mars gravity set in code (`HandController.cs:45`) | mannequin hands + "Grab" clip, VFX arc particles; VR enabled via compositor `VRSettings: Enabled: true` (`GraphicsCompositor.sdgfxcomp:151-159`, Oculus/OpenVR) | Needs a headset — pattern only |
| **NewGame** (`stride-game`) | 1 / 236 | `BasicCameraController` (not wired — `MainScene.sdscene` has only camera/lights/background/ground/sphere; user adds it) | Bullet referenced, unused | Skybox, ground, sphere, HDR + LDR variants of every asset | Already the ancestor of the toolkit's `Basic3DCameraController` |

**Asset packs** (`Templates\Packs\`): `PrototypingBlocks` (31 grey-box prefabs, each a model +
static collider), `mannequinModel` (**the only rigged, animated model in the tree** — the sanctioned
source if any toolkit animation example ever needs a skinned mesh; same MIT tree), `VFXPackage` (20
particle prefabs — same package Starbreach uses), `MaterialPackage`, `SamplesAssetPackage` (stands
and plates). No C# in any pack.

### 1.3 Per-sample inventory (feature demos)

| Sample | .cs / lines | Wired scripts | Physics / notable | Code-only? |
|---|---|---|---|---|
| `Games\JumpyJet` | 7 / 790 | `CharacterScript`✓ `PipesScript`✓ `UIScript`✓ `BackgroundScript`✓; `JumpyJetRenderer` wired as compositor child (`GraphicsCompositor.sdgfxcomp:101-108`) | Bullet only for triggers (bird integrates its own velocity); ortho camera `OrthographicSize 11.36` | **Yes** (3 textures + font) |
| `Games\SpaceEscape` | 12 / 1467 | `CharacterScript`✓ `GameScript`✓ `BackgroundScript`✓ `UIScript`✓ `LevelGenerator`✓ `BackgroundInfo`✓(×6) `ObstacleInfo`✓ `PlayAnimationScript`✓; `BendFogRenderFeature` wired as `MeshRenderFeature` sub-feature (`:54`), effect `SpaceEscapeEffectMain` (`:32`) | **No physics** — hand-rolled AABB tests; custom `.sdfx` + 3 `.sdsl` | Level gen + collision yes; models/animations no |
| `Physics\PhysicsSample` | 10 / 899 | all 10 wired across 3 scenes + prefabs | Bullet (already reviewed by backlog 2026-08-11) | Ideas only |
| `Physics\BepuSample` | 22 / 1557 | 16 components wired across 8 scenes | **Bepu** (already reviewed by backlog) | Yes |
| `Tutorials\CSharpBeginner` | 17 / 1055 | all 16 demos + `TutorialUI` wired (one scene each) | Bullet (colliders only) | Mostly yes |
| `Tutorials\CSharpIntermediate` | 23 / 1446 | all wired; each topic has Start/Completed scenes | Bullet + Navigation; `NavigationBoundingBoxComponent`×2 + `NavigationComponent` in `11_Navigation` | Mostly yes |
| `UI\GameMenu` | 3 / 534 | `MainScript`✓ `SplashScript`✓ | UI from `Main.sduipage` + `MainLibrary.sduilib` (asset-built, code-driven) | Half |
| `UI\UIElementLink` | 3 / 213 | `SplashScript`✓ `AnimationStart`✓; `UIElementLinkComponent` in 3 scenes | Code-built UI | Yes |
| `UI\UIParticles` | 2 / 444 | `SplashScript`✓ | Code-built UI; second ortho camera + `RenderGroup` | Yes |
| `Graphics\AnimatedModel` | 4 / 209 | `UIScript`✓ `RotateEntity`✓; **`AnimationScript.cs` and `RenderTextureSceneRenderer.cs` are NOT wired** — the compositor uses the engine's `Stride.Rendering.Compositing.RenderTextureSceneRenderer` (`GraphicsCompositor.sdgfxcomp:138`), and no scene references `AnimatedModel.AnimationScript` (medium-high confidence: grep of all YAML) | knight FBX + 2 clips, `.sdrendertex` | The two dead files are the valuable ones — see candidates |
| `Graphics\CustomEffect` | 1 / 69 | `CustomEffectRenderer` as compositor `Game` renderer (`:101`); `Effect.sdsl : SpriteBase` | SpriteBatch + `EffectInstance` | **Yes** |
| `Graphics\MaterialShader` | 0 / 0 | none; `ComputeColorWave*.sdsl` generics wired into `Material.sdmat` | — | Same recipe as Starbreach #12 |
| `Graphics\SpriteFonts` | 1 / 435 | `FontRenderer` as compositor renderer | SpriteBatch text | Fonts are assets |
| `Graphics\SpriteStudioDemo` | 5 / 456 | all wired | Bullet; `Stride.SpriteStudio.Runtime` | Niche runtime — rejected |
| `Particles\ParticlesSample` | 16 / 1461 | `NextSceneScript`✓ `CameraOrbitScript`✓ `RotationScript`✓(×34) `LaserOrientationScript`✓ `RotateEntity`✓ `AnimationStart`✓ `PrefabInstance`✓ `GameProfiler`✓; custom modules `CustomParticleSpawner`✓(×3), `CustomParticleInitializer`/`Updater`/`Shape` via `CustomParticles.sdscene`; `ParticleCustomMaterial` via `CustomMaterials.sdscene` | 7 scenes, `.sdfx` + 4 `.sdsl` | Modules yes |
| `Input\TouchInputs` | 2 / 320 | `TouchInputsScript`✓ + `TouchInputsRenderer` compositor | All 5 gesture configs | Yes |
| `Input\GravitySensor` | 5 / 171 | all wired | `Input.Gravity` sensor + Bullet gravity | Desktop has no sensor |
| `Audio\SimpleAudio` | 1 / 95 | `SoundScript`✓ | `Sound.CreateInstance` | Asset-bound |

### 1.4 Facts established (and corrections to the engine sweep)

1. **`DynamicNavigationMeshSystem` IS registered by the engine.** The engine sweep's first
   version said "nothing in the engine registers it" and that TopDownRPG "only finds it". Wrong on
   both counts: `sources\engine\Stride.Navigation\Processors\BoundingBoxProcessor.cs:16-27`
   creates and adds the system in `OnSystemAdd` as soon as a `NavigationBoundingBoxComponent`
   enters the scene, and `NavigationProcessor.cs:37-45,186-197` hooks it. The system just starts
   **`Enabled = false`** (`DynamicNavigationMeshSystem.cs:62-66`) and only wakes if
   `NavigationSettings.EnableDynamicNavigationMesh` is true in game settings (TopDownRPG ships it
   `false`, `Assets\GameSettings.sdgamesettings:26`) — which is exactly why
   `Gameplay\EnableDynamicNavigation.cs:14-23` exists: it flips `Enabled = true` and, if the
   processor hasn't run yet, waits on `Game.GameSystems.CollectionChanged` (`:19-20,30-43`). The
   correct doc line is: *"add a `NavigationBoundingBoxComponent`, then set `Enabled = true` (or
   the game-settings flag) — it is registered for you but sleeps."* Code-only games without a
   `GameSettings` asset hit the `gameSettings == null` branch and get default build settings
   (`DynamicNavigationMeshSystem.cs:87-96`).
2. **`IBlendTreeBuilder` has three real worked usages** — all three 3D starters implement it
   (`FirstPersonShooter…\Player\AnimationController.cs:13,76,172-175`,
   `ThirdPersonPlatformer…\Player\AnimationController.cs:15,95,234-270`,
   `TopDownRPG…\Player\AnimationController.cs:15,81,184-206`). The engine sweep listed it as "doc
   snippet, needs animation assets" and the Starbreach doc noted Starbreach hand-rolled instead.
   The API surface used: `AnimationComponent.BlendTreeBuilder = this`,
   `Blender.CreateEvaluator(clip)` / `ReleaseEvaluator` (in `Cancel()`),
   `AnimationOperation.NewPush(evaluator, time)` and `NewBlend(CoreAnimationOperation.Blend, factor)`
   pushed onto a stack (verified public in `sources\engine\Stride.Engine\Animations\AnimationOperation.cs:28-61`).
3. **Virtual buttons have one worked usage**: `Tutorials\CSharpBeginner…\Code\VirtualButtonsDemo.cs:20-50`
   (`Input.VirtualButtonConfigSet`, `VirtualButtonBinding("Forward", VirtualButton.Keyboard.W)`,
   `VirtualButtonConfig` collection initializer, `Input.GetVirtualButton(0, "Forward")` — note
   that method is `[Obsolete]` in 4.4; use `GetVirtualButtonValue`). The starters do **not** use
   them — they hand-roll `List<Keys>` + `Any(IsKeyDown)`.
4. **Gestures are used in four places**: all five configs in `Input\TouchInputs…\TouchInputsScript.cs:72-76,152-176`;
   swipe classification in `Games\SpaceEscape…\CharacterScript.cs:89,211-281`; `Drag`+`Composite`
   in the NewGame/TPP/Bepu `BasicCameraController` (`NewGame…\BasicCameraController.cs:47-51,193-211`)
   — **but only when `!Platform.IsWindowsDesktop`**, so the toolkit's derived controller never
   exercises them on desktop even though the engine sweep verified mouse pointer events feed them.
5. **Render-to-texture has an in-engine class now**: `Stride.Rendering.Compositing.RenderTextureSceneRenderer`
   (the AnimatedModel compositor uses it); the sample's own copy documents the Aug-2026 fix
   (commit `c9f5541fc` "settle RenderTextureSceneRenderer output for sampling") — the
   `ResourceBarrierTransition(RenderTexture, BarrierLayout.ShaderResource)` line
   (`Graphics\AnimatedModel…\RenderTextureSceneRenderer.cs:43`). Feed to engine sweep spec 20.
6. **Procedural `AnimationClip` property paths, with the type-qualification syntax**:
   `Graphics\AnimatedModel…\AnimationScript.cs:21-25` —
   `"[LightComponent.Key].Type.(ColorLightBase-AQN)Color.(ColorRgbProvider-AQN)Value"` — animates
   a light's colour through a polymorphic property chain. Feed to engine sweep spec 9.
7. **Copy-pasted helpers**: `Core\InputManagerExtensions.cs` and `Core\Utils.LogicDirectionToWorldDirection`
   exist byte-identically (bar namespace) in FPS, TPP, VRSandbox and BepuSample (`Extensions\`).
   TopDownRPG carries a *different*, buggy, unused variant (§3). Third independent vote for the
   backlog's "Gamepad input helpers" row; the FPS/TPP/Bepu version is the canonical one.
8. **The engine's own examples reach for `Game.DrawTime` inside `Update()`** for animation clocks
   ("Use DrawTime rather than UpdateTime", `ThirdPersonPlatformer…\AnimationController.cs:142-143`;
   FPS `:126`). Every gameplay timer elsewhere uses `Game.UpdateTime`. Worth a sentence in any
   animation example; no rationale was found in the sources.
9. **No template switches cameras at runtime, none uses `Crossfade`-based state machines on the
   character (they use blend trees), none uses `Task.Delay` except Platformer2D** — see warts.
10. **`GameSettings`-driven physics in 2D**: Platformer2D is a 3D Bullet world viewed
    orthographically; there is no 2D physics mode. The toolkit's Bepu `Body2DComponent`/Box2D path
    is the code-only equivalent.

---

## 2. Example candidates

Ordered by value against the toolkit's empty categories (Interaction, Audio, Gameplay and
Integration are at zero; Input has one). Verdicts: **example** / **toolkit** / **both**.

### 60. `Example60_InputPipeline_Events` — one input script, many consumers

- **Level:** Intermediate · **Category:** Input · **Complexity:** 5 · **Verdict:** both
- **Source:** `Templates\FirstPersonShooter…\Player\PlayerInput.cs:18-24,45-49,51-143`;
  `ThirdPersonPlatformer…\Player\PlayerInput.cs:62-88`; consumers `PlayerController.cs:22,48`,
  `FpsCamera.cs:47,84`, `WeaponScript.cs:26-28,75-78`; `Core\Utils.cs:10-22`.
- **The mechanic:** a single `PlayerInput : SyncScript` reads keyboard + every gamepad and
  broadcasts *intent* on static `EventKey`s every frame (`MoveDirectionEventKey`
  as a **world-space** `Vector3`, `CameraDirectionEventKey` as a screen-space `Vector2`, `Shoot`,
  `Reload`, `Jump` as `bool`). Controller, camera, weapon and animation each hold an
  `EventReceiver<T>` and `TryReceive` in their own `Update`. Four details worth teaching, each
  with a comment in the source: (1) `Priority = -1000` in the constructor "to fix single frame
  input lag" (FPS `:47-48`); (2) movement is converted to world space *through the camera*
  (`Utils.LogicDirectionToWorldDirection` — forward = `cross(up, invView.Right)`) so an AI could
  drive the same controller (`:54-56`); (3) stick rotation is scaled by `dt`, mouse delta is not,
  with the reason spelled out (`:95-98`); (4) the TPP variant rescales the stick magnitude after
  the dead zone so speed starts at 0 just outside it and clamps to 1 (`TPP PlayerInput.cs:67-86`)
  — the same lesson as Starbreach's `SoldierPlayerInput`, minus the response curve. Click-to-lock /
  Esc-to-unlock mouse (`:100-115`).
- **Rebuild:** capsule + Bepu `CharacterComponent` (`Move(Vector3)`, `TryJump()`, `IsGrounded` —
  `sources\engine\Stride.BepuPhysics\Stride.BepuPhysics\CharacterComponent.cs:93,148,159`), the
  toolkit's `Example20_BepuFirstPersonCharacter` as the base. Zero assets.
- **Toolkit piece:** the `InputManagerExtensions` `GetLeftThumbAny(deadZone)` family (FPS
  `Core\InputManagerExtensions.cs:38-53`) — averages every pad past the dead zone; fills the
  backlog's gamepad-helpers row. **Wart to fix in the example:** the keys are `static` ("TODO
  Should not be static, but allow binding between player and controller", TPP `:18`) —
  single-player only; make them instance members and bind explicitly, and show why.
- **Cross-link:** engine sweep spec 4 (virtual buttons) is the *other* answer to the same problem;
  Starbreach's `SoldierPlayerInput` is the same idea with priorities.

### 61. `Example61_ThirdPersonCamera_ConeSweep` — the platformer camera, with coyote-time jumping

- **Level:** Intermediate · **Category:** Input (Gameplay for the jump half) · **Complexity:** 6 ·
  **Verdict:** example — **claims the backlog's "Third-person camera following a physics body"
  row together with Starbreach #1**; the two use different occlusion techniques and should be one
  example with two modes.
- **Source:** `Templates\ThirdPersonPlatformer…\Camera\ThirdPersonCamera.cs:66-117` (occlusion),
  `:119-139` (orientation), `:148-156` (setup); `Player\PlayerController.cs:31-41,70-121` (jump),
  `:123-143` (move + facing).
- **The mechanic (camera):** the camera is a *child of a pivot on the character* and only ever
  moves along local +Z (`Entity.Transform.Position.Z = maxLength`, `:116`). Occlusion is a
  **cone sweep**, not a ray: `ShapeSweepPenetrating(new ConeColliderShape(distance, ConeRadius,
  UpZ), from, to, results, DefaultFilter, DefaultFilter)` between pivot−½d and pivot+½d
  (`:85-97`), keeping only hits whose signed distance along the camera vector is positive
  (`:101-108`); `ConeRadius <= 0` degrades to a plain raycast (`:75-84`). The comment at `:95`
  records a real design decision: *"Intentionally ignoring StaticFilter, to avoid collision with
  poles"* — thin level props deliberately don't pull the camera in. Pitch clamped in degrees,
  yaw unbounded (`:129-134`).
- **The mechanic (jump):** "Jump Time Limit" `JumpReactionThreshold = 0.3 s` — the character may
  still jump for 0.3 s after leaving the ground (**coyote time**, `:74-121`), timed with
  `Simulation.FixedTimeStep` rather than frame dt (`:76`); jumping zeroes the budget (`:116`).
  Movement inertia is `move = move*0.85 + new*0.15` (`:130`) and the *visual* child is yawed
  from `Atan2(-z, x) + π/2` while the capsule never rotates (`:138-142`).
- **Rebuild:** Bepu `SweepCast` with a capsule/sphere (Bepu has no cone) — say so in the example.
  Coyote time and inertia are pure C#.
- **Warts to fix on the way:** `cameraRotationXYZ = Vector3.Lerp(cur, target, 0.25f)` per frame
  (`:137`, framerate-dependent — the comment even invites replacement) → `MathUtil.ExpDecay`;
  the 0.85/0.15 inertia has the same problem. The dead `BasicCameraController.cs` in the same
  project is a one-line "templates carry dead files too" aside.
- **Cross-link:** `Tutorials\CSharpIntermediate…\10_ThirdPersonCamera\ThirdPersonCamera.cs:66-92`
  is the minimal raycast-only version (first-person pivot + third-person pivot, snap to
  `hitDistance − 0.1`) — the "level 1".

### 62. `Example62_ClickToMove_Pathfinding` — click → navmesh path → follow

- **Level:** Intermediate → Advanced · **Category:** Gameplay · **Complexity:** 7 · **Verdict:**
  example — the concrete scenario for engine sweep spec 11, and the corrected dynamic-navmesh story.
- **Source:** `Templates\TopDownRPG…\Player\PlayerController.cs:164-194` (re-path),
  `:196-269` (follow), `:124-154` (attack), `Player\PlayerInput.cs:37-97` (click + highlight),
  `Core\Utils.cs:144-198` (click classification), `Core\ClickResult.cs`,
  `Gameplay\EnableDynamicNavigation.cs`.
- **The mechanic:** pointer press → `RaycastPenetrating(near, far, hitTriggers: true)` →
  classify each hit by **collision group** (`CustomFilter1` = ground, `CustomFilter2` = loot
  crate, `Utils.cs:173-180`), keep the nearest → broadcast a `ClickResult` struct. Holding the
  button re-broadcasts so the character follows the cursor (`PlayerInput.cs:44-53`).
  Pathing: `navigation.TryFindPath(destination, pathToDestination)` **only when the target moved
  > 1 cm** (`:166-171`); skip leading waypoints within 0.25 m (`:174-178`); advance an
  intermediate waypoint by **projecting the position onto the segment** (`:209-220`) and the final
  one by distance; speed = `min(1, distToGoal·DestinationSlowdown)` × corner factor
  `max(0, dot(dir, moveDir))·CornerSlowdown + (1−CornerSlowdown)` (`:241-247`).
  Attack: walk to the crate until within `AttackDistance`, halt, enable a child
  `RigidbodyComponent` "PunchCollision" for `AttackCooldown` seconds (`:124-154`).
  Hover highlight: a ghost entity that **borrows the hovered model** and swaps every material
  slot for `HighlightMaterial`, copying the world matrix with `UseTRS = false` (`PlayerInput.cs:55-72`);
  hidden by `LocalMatrix = Matrix.Scaling(0)` (`:77`).
- **Rebuild:** flat ground + boxes; build the navmesh at runtime with `NavigationMeshBuilder`
  (Bullet colliders) or the in-engine `Stride.BepuPhysics.Navigation` stack (see the engine
  sweep's corrected spec 11); for the dynamic variant, add a `NavigationBoundingBoxComponent` and
  set the auto-registered system `Enabled = true`. Bullet groups → Bepu `CollisionLayer`/`CollisionMask`.
- **Cross-link:** Starbreach #4/#5; `Tutorials\CSharpIntermediate…\11_Navigation\NavigateCharacter.cs:79-107`
  (same `TryFindPath` + waypoint spheres, transform-only movement — the "level 1").

### 63. `Example63_SceneStreaming_ByTriggerDepth` — chunk loading that pre-loads on the way in

- **Level:** Advanced · **Category:** Gameplay · **Complexity:** 7 · **Verdict:** example
- **Source:** `Templates\TopDownRPG…\Gameplay\SceneStreaming.cs` (162 lines); wiring
  `Assets\DynamicScene.sdscene:85-88,123-126,292-295,377-380` (five chunks, one trigger each);
  chunks carry their own `NavigationBoundingBoxComponent`.
- **The mechanic:** each frame, walk `Trigger.Collisions` (filtered both ways by
  `CanCollideWith & CollisionGroup`, `:64-66`) and inspect **contact depth**:
  `contact.Distance < -LoadDepth` → synchronous `Content.Load(Url)`;
  `< -PreLoadDepth` → `Content.LoadAsync(Url)` with a cancellation token and a follow-up task
  that unloads the result if the trigger was left meanwhile (`:106-130`); any contact → keep;
  none → cancel/unload, detaching only if `!Content.IsLoaded(Url)` because another trigger might
  still own it (`:151-155`, "Ideally scripts should cooperate differently"). Attach is
  `Instance.Parent = Entity.Scene` (`:136`).
- **Rebuild:** build chunk `Scene`s procedurally with `Scene.Offset`, keep the trigger-depth state
  machine verbatim. Bepu: contact depth from `IContactEventHandler` manifolds.
- **Cross-link:** engine sweep child-scenes row; Starbreach `Streaming.cs` (adds the resync fix);
  `CSharpIntermediate…\06_Scenes\LoadChildScene.cs:26-40` and `LoadScene.cs:21-22` as primers.

### 64. `Example64_TimedSpawner_TriggerGroup` — named effects: prefab + local matrix + lifetime

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 4 · **Verdict:** both
- **Source:** `Templates\FirstPersonShooter…\Trigger\TriggerScript.cs:21-83`, `TriggerEvent.cs`,
  `TriggerGroup.cs`, consumer `EffectController.cs:19-38`; extension-method twin
  `TopDownRPG…\Core\Utils.cs:16-101` (`SpawnPrefabModel` adds an impulse);
  `Particles\ParticlesSample…\PrefabInstance.cs:84-137` (the `Following` variant).
- **The mechanic:** a `[DataContract] TriggerGroup { Name, List<TriggerEvent> }` where each event
  is `{ Name, SourcePrefab, Duration, Position/Rotation/Scale → cached LocalMatrix }` — an
  editor-editable *effect table* (`MainScene.sdscene:2106-2115`). `SpawnInstance(prefab,
  attachEntity, timeout, localMatrix)`: instantiate, compose `prefabLocal * localMatrix` and
  **`Decompose` back into the transform** (`:43-45`), add as child or to the root, count down in
  frame-dt, remove (`:57-79`). The consumer spawns `"BulletImpact"` oriented with
  `Quaternion.BetweenDirections(UnitY, hit.Normal)` (`EffectController.cs:29`) and attaches
  `"DamagedTrail"` to the hit rigidbody so it follows (`:31-36`).
- **Rebuild:** template entity + `Entity.Clone()` instead of `Prefab`; toolkit primitives/particles.
- **Toolkit piece:** `SpawnTimed(entity, parent, seconds, matrix)` — four identical copies exist.

### 65. `Example65_Collectibles` — coins, crates and loot bursts

- **Level:** Beginners · **Category:** Gameplay · **Complexity:** 4 · **Verdict:** example
- **Source:** `Templates\TopDownRPG…\Gameplay\CoinScript.cs:32-91`, `CrateScript.cs:69-99`,
  `LootCoinScript.cs`, `Gameplay\Trigger.cs`; 2D twin `Templates\Platformer2D…\Gameplay\CollectCoin.cs`,
  `CoinRotation.cs`.
- **The mechanic:** a `Trigger : AsyncScript` awaits `NewCollision()`, filters by collision
  groups, broadcasts `EventKey<bool>` (for `StartAndEnd`, a task loops `CollisionEnded()` **until
  it is the same `Collision` object** — `Trigger.cs:64-76`; identical in `PhysicsSample…\Trigger.cs:34-46`).
  Pickup animation is procedural: spin ramps to 10 rad/s, height = `1 + max(0, sin(t))`, then
  `Scale = 0` and removal after `Game.WaitTime(3 s)` (`CoinScript.cs:44-60,83-90`). The crate
  shrinks over π seconds and spawns 3–6 coins with random offset/scale/impulse (`CrateScript.cs:89-98`).
- **Teach the contrast:** Platformer2D does the same with `await Task.Delay(3 s)` then
  `Entity.Scene = null` (`CollectCoin.cs:37-38`) — wall-clock — versus TopDownRPG's
  `Game.WaitTime` (`Core\Utils.cs:120-128`). Right and wrong ship in sibling templates.

### 66. `Example66_GameStateFlow_MenuPlayGameOver` — the whole loop in one small game

- **Level:** Beginners · **Category:** UI (Gameplay secondary) · **Complexity:** 5 · **Verdict:** example
- **Source:** `Games\JumpyJet…\GameGlobals.cs:10-13` (four payload-less `EventKey`s),
  `UIScript.cs:57-72,77-87,89-220`, `CharacterScript.cs:110-143`, `PipesScript.cs:55-67`,
  `BackgroundScript.cs:17-31`; callback twin `Games\SpaceEscape…\GameScript.cs:32-46,84-129`.
- **The mechanic:** every system owns `EventReceiver`s for the global state events; the UI swaps
  `UIComponent.Page = new UIPage { RootElement = … }` between three roots — two `ModalElement`s
  (menu, game-over) so clicks can't fall through — and buttons broadcast `Reset`/`Started`.
  SpaceEscape shows explicit orchestration instead (`GameScript` wires `Click +=` and calls
  `Reset()` directly, unsubscribing in `Cancel()` `:70-77`).
- **Code-built UI cookbook inside:** `SetCanvasPinOrigin/RelativePosition/RelativeSize`,
  `ContentDecorator` with `MinimumWidth` "so the box doesn't resize when the score changes"
  (`UIScript.cs:145-151`), three-state `Button` images.
- **Cross-link:** engine sweep spec 13 and spec 25; the second natural vehicle for `Example36_EventBus`.

### 67. `Example67_FlappyLoop` — JumpyJet's bird, pipes and pass-trigger

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 5 · **Verdict:** example
- **Source:** `Games\JumpyJet…\CharacterScript.cs:24-32,127-142,156-170`, `:71-105`, `PipesScript.cs:37-53,69-102`.
- **The mechanic:** kinematic bird (`v += g·dt; p += v·dt`; tap sets `v.y = 6.5` or `2` above a
  top limit; pitch clamped ±18°, sprite frame flips by sign of `v.y`). Physics only detects: two
  `AsyncScript` tasks on the same `PhysicsComponent` — one counts collisions where either side is
  `CustomFilter1` (invisible "pipe passed" trigger), one treats `DefaultFilter`/`DefaultFilter`
  as death. Pipes: N = `ceil((sceneWidth + 2·pipeWidth)/gap)` instances; an off-screen pipe is
  re-placed at `previousPipe.X + gap`, ring-indexed `(i − 1 + n) % n` (`:71-90`).
- **Rebuild:** toolkit 2D scene, Box2D/Bepu-2D sensors, `SpriteFromTexture`.

### 68. `Example68_ParallaxBackgroundRenderer` — scrolling layers inside the compositor

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 5 · **Verdict:** both
- **Source:** `Games\JumpyJet…\JumpyJetRenderer.cs:55-117` (`SceneRendererBase` owning a
  `SpriteBatch`; registers opaque/transparent stages in `CollectCore` under
  `SaveRenderOutputAndRestore`; `DrawCore` clears depth, draws parallax, then
  `renderSystem.Draw(...)` for both stages); `BackgroundSection.cs:55-115` (two-quad wrap);
  `GraphicsCompositor.sdgfxcomp:101-108`; script reaches it via
  `((SceneCameraRenderer)compositor.Game).Child` (`BackgroundScript.cs:20`).
- **Toolkit piece:** `ParallaxLayer`; the cleanest small worked example of the collect-phase
  contract spec 20 flags as "the step everyone forgets".

### 69. `Example69_EndlessRunner_Sections` — level generator and physics-free collisions

- **Level:** Intermediate · **Category:** Gameplay · **Complexity:** 6 · **Verdict:** example
- **Source:** `Games\SpaceEscape…\Background\BackgroundScript.cs:76-114,210-241`,
  `LevelGenerator.cs:26-124`, `Section.cs:53-115` (sub-mesh bounding boxes walked up the skeleton
  `:84-105`), `BackgroundScript.cs:132-164,182-208` (`CollisionHelper.BoxContainsBox`),
  `CharacterScript.cs:25-83,290-401` (lane state machine; lane change driven by **animation
  time** `t = CurrentTime / Clip.Duration`, `:390-401`).
- **The mechanic:** sections are `Entity.Clone()`s chained end-to-end by half-lengths, removed at
  Z < −16, appended when the last crosses Z < 280; obstacles placed on a random lane inside the
  i-th of `nbObst` equal Z slots. Collisions are pure math (AABB swapped for a shorter one while
  sliding). Input unifies swipe and keys.
- **Cross-link:** engine sweep spec 6 (`CollisionHelper`).

### 70. `Example70_WorldBend_SubRenderFeature` — per-mesh effect permutations and constant uploads

- **Level:** Advanced · **Category:** Rendering · **Complexity:** 8 · **Verdict:** example — successor to `Example13_RootRendererShader`
- **Source:** `Games\SpaceEscape…\Rendering\BendFogRenderFeature.cs` (126 lines),
  `Effects\SpaceEscapeEffectMain.sdfx` (params `EnableFog/EnableBend/EnableOnflyTextureUVChange`,
  conditional `mixin`s over `StrideForwardShadingEffect`), `TransformationBendWorld.sdsl`
  (`PreTransformPosition` adds `k·z²`), `CustomFogEffect.sdsl`, `TransformationTextureUV.sdsl`;
  compositor `:32,54`.
- **The mechanic:** `InitializeCore` asks the root feature for **draw-CB offset slots** by key name
  (`CreateDrawCBufferOffsetSlot(CustomFogEffectKeys.FogColor.Name)`, `:44-48`);
  `PrepareEffectPermutations` validates *per-mesh* `Mesh.Parameters` into the permutation
  (`:52-77`) so the skyplane opts out of bending (`BackgroundScript.cs:62-63`); `Prepare` writes
  structs into the mapped per-draw buffer (`:80-124`). The only in-tree example of
  `Mesh.Parameters`-driven permutations outside the engine.

### 71. `Example71_Platformer2D_SpriteCharacter`

- **Level:** Beginners · **Category:** Gameplay (Physics) · **Complexity:** 5 · **Verdict:** example
- **Source:** `Templates\Platformer2D…\PlayerController.cs:14-37,48-92,94-168`, `CoinRotation.cs:20-29`; `MainScene.sdscene:558-572,1910-1915`.
- **The mechanic:** 3D Bullet `CharacterComponent` seen orthographically; facing via
  `CharacterComponent.Orientation = RotationY(180°)` (`:64,78`); jump on press when `IsGrounded`
  (`:84-87`); sprite-sheet animation by accumulator with per-state frame counters (`:117-158`).
- **Rebuild:** toolkit `Body2DComponent`/Box2D + `SpriteFromTexture`; teach the engine's
  `SpriteAnimation.Play(sprite, from, to, LoopInfinite, fps)` (`PhysicsSample…\CharacterScript.cs:47,53`,
  `GravitySensor…\BallScript.cs:11`) instead of the hand-rolled timer.
- **Warts to fix (real bugs):** `HandleAnimation` runs **twice per frame** (`:42` and `:89`);
  `SetVelocity(Zero)` then `SetVelocity(move)` (`:55,88`); stray `using System.Windows.Input;` (`:7`);
  `CollectCoin` uses `Task.Delay`.

### 72. `Example72_BlendTree_ProceduralClips` — `IBlendTreeBuilder` without an FBX

- **Level:** Advanced · **Category:** Gameplay · **Complexity:** 7 · **Verdict:** both — merges spec 9 with the starters' blend trees.
- **Source:** TPP `AnimationController.cs:93-110` (setup), `:122-157` (idle→walk→run lerp with
  `sqrt` skew "because idle-walk blend looks weird" `:127`, and a **blended duration**
  `lerp(dur1, dur2, factor)` `:145-156`), `:207-227`, `:234-270` (`BuildBlendTree`: push A,
  push B, `NewBlend(Blend, factor)` — "laid out as a stack and has to be flattened" `:240`);
  FPS `:112-165` (one-shot shoot/reload returning to default); RPG `:141-158`. Procedural clips:
  `AnimatedModel…\AnimationScript.cs:14-40,42-89`, `ParticlesSample…\RotationScript.cs`.
- **The lesson:** two layers — the easy API (`Play`, `Crossfade`, `IsPlaying`, `RepeatMode`,
  `TimeFactor`, `await Ended(...)`: `CSharpIntermediate…\AnimationBasics.cs:20-61`,
  `SpriteStudioDemo…\PlayerScript.cs:158-159`) and the blend tree underneath. Build two clips in
  code, blend by a speed slider, add a pre-empting one-shot — the starters' FSM, zero assets.
  Also `Cancel()` → `ReleaseEvaluator` (`:112-120`).
- **Caveat:** the skinned version needs the `stride-pack-animatedmodels` mannequin; note as sequel.

### 73. `Example73_SpriteBatchCustomEffect` — a full-screen ripple in 69 lines

- **Level:** Intermediate · **Category:** Rendering · **Complexity:** 5 · **Verdict:** example
- **Source:** `Graphics\CustomEffect…\CustomEffectRenderer.cs:24-65`, `Effects\Effect.sdsl` (`shader Effect : SpriteBase`, overrides `Shading()`).
- **The mechanic:** `EffectSystem.LoadEffect("Effect").WaitForResult()` → `EffectInstance`,
  `Parameters.Set(EffectKeys.Frequency, 40)`, `spriteBatch.Begin(ctx, blendState:
  NonPremultiplied, depthStencilState: None, effect: instance)` in a `VirtualResolution = (1,1,1)`
  batch — the simplest custom-shader entry point, one level below spec 19. Letterbox source-rect
  math (`:56-60`) reused by `SpriteFonts`/`TouchInputs`.
- **Wart:** `samplerState` is set on the effect (`:34`) **before** it is created (`:42`).

### 74. `Example74_ParticlesOverUI_RenderGroups`

- **Level:** Intermediate · **Category:** Rendering (UI) · **Complexity:** 6 · **Verdict:** example — worked usage for the render-groups row
- **Source:** `UI\UIParticles…\SplashScript.cs:172-178` (UI px → ortho world by dividing by
  `(virtualWidth, −virtualHeight, 1)`), `:180-228` (`EntityCloner.Clone`, `ResetSimulation()`,
  countdown, `Dispose()`), `:231-256` (overlay camera `AspectRatio`/`OrthographicSize` synced each
  frame), `:280-297` (gauge via two `Star` columns whose `SizeValue`s are re-split; a fire particle
  parked at the gauge end via `lifeBarGrid.WorldMatrix` + `ActualSize`). Second camera renders only `Group01`.

### 75. `Example75_UIElementLink`

- **Level:** Intermediate · **Category:** UI · **Complexity:** 5 · **Verdict:** example
- **Source:** `UI\UIElementLink…\SplashScript.cs:44-58,83-88` (a `Button` named `"ElementName"` — link matches by **element name**), scenes `FullScreen`/`TiltedScreen`/`BillboardedScreen.sdscene` (`UIElementLinkComponent` + knight + particles; vary `IsFullScreen`/`IsBillboard`). Only usage in the tree.

### 76. `Example76_AsyncWebApi`

- **Level:** Beginners · **Category:** Integration · **Complexity:** 3 · **Verdict:** example (Integration has zero examples)
- **Source:** `Tutorials\CSharpIntermediate…\05_Async\AsyncWebApi.cs:14-54` — `HttpClient.GetAsync` awaited in `Execute()`, `System.Text.Json`; comment at `:35` is the one every AsyncScript beginner needs. **Wart:** prompt says "Press A", code checks `Keys.G` (`:21,23`).

### 77. `Example77_DayNightCycle` — scenario for the procedural-clip example

- **Verdict:** fold into spec 9. `Graphics\AnimatedModel…\AnimationScript.cs:42-68`: 14-key cubic `AnimationCurve<Vector3>` sun colour + linear `Quaternion` rotation on a 1-s clip at `TimeFactor = 0.1`. Zero assets; **dead code in the sample**.

### Snippet-tier

- **Exponential smoothing done right** — `Physics\BepuSample…\Components\Camera\FindAndAttachCameraComponent.cs:23`: `lerpSpeed = 1 − exp(−100·dt)` then `Lerp`/`Slerp`; `:33-48` finds the first `CameraComponent` in children.
- **Beam between two points** — `ParticlesSample…\LaserOrientationScript.cs:42-75`: hand-built look-at basis, scale Z to distance.
- **Spawn on animation time** — `PrefabInstance.cs:65-79`: fire when `CurrentTime` crosses `TimeDelay`, re-armed on wrap (Stride has no animation events).
- **Additive layering** — `AnimationStart.cs:48-57`: `animComponent.Add(clip, startTime, blendOp)`; self-removing script `Entity.Remove(this)` (`:45`).
- **Grab-and-reparent under a bone** — `VRSandbox…\Player\HandController.cs:104-200`: kinematic + `CanSleep=false`, relative pose by decomposing both world matrices, `ModelNodeLinkComponent`, `UseTRS`; release: `LocalMatrix = WorldMatrix`, remove link, `TransformLink = null`, throw with controller velocity. Negative `TimeFactor` plays the grab clip backwards (`:47-49,86,93`).
- **Uncapped update while unfocused** — `VRSandbox…\VRGame.cs:9-18`.
- **Hover ghost highlight** — `TopDownRPG…\Player\PlayerInput.cs:55-78`.
- **Autopilot + stop on side contact** — `PhysicsSample…\CharacterScript.cs:66-85,116-160` (`|normal.x| > 0.5` cancels).
- **Kinematic reset on trigger exit** — `PhysicsSample…\AutoResetRigidBody.cs:27-50` (the backlog's reset-out-of-bounds idea, in code).
- **`stackalloc` query buffers** — `BepuSample…\OverlapTesterComponent.cs:15,30`, `RayCastComponent.cs:33-35` — for `Example14_ShapeQueries`.
- **Scene selector, three ways** — `BepuSample…\SceneSelectorComponent.cs:44-60` (child swap + `Content.Unload` + `Dispose`), `PhysicsSample…\NextSceneScript.cs:25-32` (root swap deferred to next `Update`, not inside the click callback), `CSharpIntermediate…\LoadScene.cs:19-23` (`Content.Unload(RootScene)` first).

---

## 3. Game-dev patterns worth documenting

### Good — worth teaching as-is

- **Input isolated behind events, with a priority** (FPS/TPP/RPG; `Priority = -1000`, FPS `:47-48`; design reason at FPS `:54-56`).
- **Camera-relative movement as a 10-line utility** (`Core\Utils.LogicDirectionToWorldDirection` ×4; `BepuSample…\Extensions\CameraExtensions.cs:7-18` as a `CameraComponent` extension). Toolkit-shaped.
- **Dead zone, then rescale, then clamp** (TPP `PlayerInput.cs:67-86`, VR `:39-55`).
- **Stick × dt, mouse delta as-is** — FPS `:95-98`; `BepuSample…\BasicCameraControllerComponent.cs:80-88,151-156,189-192` is the best-commented input code in the tree.
- **Validate wiring in `Start()` with exceptions** — every `AnimationController`, `PlayerController` (FPS `:33`), TPP camera (`:155`).
- **Release in `Cancel()`**: evaluators; `Children.Clear()` (`PipesScript.cs:104-108`); `ClientSizeChanged -=` (`UISceneBase.cs:38-44`); events (`GameScript.cs:70-77`); `Content.Unload` (`AsyncCollisionTriggerDemo.cs:44-47`); constraints (`DemoScript.cs:258-261`).
- **`[DataContract]` POCOs as designer tables** — `TriggerEvent.cs:29-51`; `[DataMemberIgnore] EventKey` (`Trigger.cs:38-39`); `CSharpBeginner…\PropertiesDemo.cs` is the attribute cheat-sheet.
- **Physics-clock spawning** (`BepuSample…\SpawnerComponent.cs:34-55`, already claimed).
- **Coyote time on a fixed clock** (#61); **waypoint advance by projection** (#62); **pre-load depth vs load depth** (#63); **ring-buffer reuse** (#67, #69); **matched `CollisionEnded`** (`Trigger.cs:64-76`).
- **`ModalElement` for menus**; `UISceneBase` re-fitting `UIComponent.Resolution` on `ClientSizeChanged` (`UI\GameMenu…\UISceneBase.cs:22-24,46-50`).
- **`[DataMember(Mask = LiveScriptingMask)]` / `IsLiveReloading`** — real live-scripting support (`PhysicsSample…\CharacterScript.cs:32-42,67`, `SimpleAudio…\SoundScript.cs:29-33,43`). Docs note only — the engine's own live reloader is dead code in 4.4 (see the engine sweep).
- **`[ComponentCategory("…")]`** on every BepuSample component — belongs in the toolkit's DataContract note.

### Cautionary tales — teach the wart and the fix

- **Per-frame `Lerp(a, b, 0.25f)` / `0.85·old + 0.15·new`** — TPP `:137`, TPP/RPG `:130`/`:250`, `RotateEntity.cs:24`. Correct form in the same tree: `FindAndAttachCameraComponent.cs:23`. `CameraOrbitScript.cs:62-70` does fixed-step friction integration — right idea, heavy.
- **Wall-clock timers**: `Platformer2D…\CollectCoin.cs:37` (`Task.Delay`), `SpriteStudioDemo…\EnemyScript.cs:74-83` (`Stopwatch`). Fix: `TopDownRPG…\Core\Utils.WaitTime` (`:120-128`) or the frame-dt countdown (`TriggerScript.cs:57-63`).
- **Dead code shipped in templates**: TPP `BasicCameraController.cs`; TopDownRPG `Core\InputManagerExtensions.cs` (unused **and** wrong — `GamePadCount >= index` lets `index == count` through to `GetGamePadByIndex(index)` → null → NRE; the other four copies null-check), `TaskExtension.InterruptedBy`, `PlayerInput.JumpEventKey`; AnimatedModel `AnimationScript.cs` + `RenderTextureSceneRenderer.cs`. Lesson: *grep the YAML before trusting a sample file*.
- **Platformer2D's double `HandleAnimation`** (`:42`+`:89`), stray WPF `using` (`:7`), `Task.Delay` — the newest template is the least careful.
- **`CustomEffectRenderer` sets the sampler before creating it** (`:34` vs `:42`).
- **`MusicScript : AsyncScript`** loops `NextFrame` forever doing nothing (`:29-33`) — should be a `StartupScript`; `NextSceneScript.Update() {}` empty override (`ParticlesSample…:23`).
- **`Quaternion.Lerp` for camera rotation** (`CSharpBeginner…\TutorialUI.cs:131`) — `Slerp`.
- **`new Random()` per shot / per crate** (`EffectController.cs:34`, `CrateScript.cs:91`).
- **Static event keys** (all three starters) — single player, cross-scene leakage; TODO in source.
- **Recreating `UIPage` on every state switch** (JumpyJet/SpaceEscape) — swap `RootElement` instead (`ParticlesSample…\UIScript.cs:21`).
- **Tutorial prompt/key mismatch** (`AsyncWebApi.cs:21,23`); **GameMenu "FIXME: UI asset should support multiline text"** (`MainScript.cs:298,363`).
- **`GameProfiler` hotkey drift**: ParticlesSample F1–F3 filter types, F4 sort (`:75-92`), and F3/F4 bound twice (`:83-86`, `:125-132`); BepuSample F1 cycle, F2 sort, F3/F4 page (`GameProfilerComponent.cs:81-99`). Spec 15 should pick one.

---

## 4. Worked-usage references for engine-sweep topics

| Sweep topic | Template / sample usage | What it shows |
|---|---|---|
| `EventKey`/`EventReceiver` (spec 13) | FPS `PlayerInput.cs:18-24`, `WeaponScript.cs:22-28`, `EffectController.cs:17,23` (`ReceiveAsync`); JumpyJet `GameGlobals.cs` (named payload-less keys); RPG `Trigger.cs:39`, `CoinScript.cs:66`; PhysicsSample `AutoResetRigidBody.cs:24-29` | Broadcast-per-frame vs edge events; `TryReceive` vs `ReceiveAsync`; receiver created lazily from another script's key |
| Virtual buttons (spec 4) | `CSharpBeginner…\VirtualButtonsDemo.cs:20-50` | Exact config/binding/`GetVirtualButton(index, name)` API |
| Gestures (spec 5) | `TouchInputsScript.cs:72-76,152-201`; SpaceEscape `CharacterScript.cs:89,211-281` (`GestureConfigDrag(GestureShape.Free) { MinimumDragDistance = 0.02f, RequiredNumberOfFingers = 1 }`, `GestureState.Began`, quadrant classification); `BasicCameraController.cs:47-51,193-211` (`DeltaScale` → `MathF.Log(scale+1)`) | Configs added once under `IsLiveReloading` |
| Navigation (spec 11) | RPG `PlayerController.cs:81-89,164-194`; `MainScene.sdscene:2927-2930` (baked); `DynamicScene.sdscene:197-203`; `NavigateCharacter.cs:93-107`; engine `BoundingBoxProcessor.cs:16-27` | **Corrects the sweep**: auto-registered, sleeps until enabled; needs a bounding-box component |
| `IBlendTreeBuilder` | FPS/TPP/RPG `AnimationController.cs` | Three complete state machines |
| Procedural `AnimationClip` (spec 9) | `AnimatedModel…\AnimationScript.cs:14-89` (**dead**), `RotationScript.cs:14-34` (wired ×34) | Type-qualified paths; `Optimize()`; `KeyFrameData<T>((CompressedTimeSpan)ts, v)` |
| Animation easy API | `AnimationBasics.cs:20-61`; `PlayerScript.cs:158-159` (`await Ended`); `AnimationStart.cs:53` (`Add`); VR `HandController.cs:47-49,86,93` (negative `TimeFactor`) | |
| Render-to-texture (spec 20) | `AnimatedModel…\RenderTextureSceneRenderer.cs` + engine class (compositor `:138`), `RenderTexture.sdrendertex` | Collect/draw contract; temp depth via `Allocator`; Aug-2026 barrier fix |
| Custom `SceneRendererBase` | `JumpyJetRenderer.cs`, `CustomEffectRenderer.cs`, `FontRenderer.cs`, `TouchInputsRenderer.cs:44-45` (`SceneInstance.GetCurrent(context)`) | Four small compositor renderers |
| Custom SDSL in materials (Starbreach #12) | `MaterialShader…\ComputeColorWave*.sdsl`; `ParticlesSample…\ComputeColorTextureScroll.sdsl` (identical to Starbreach's), `ComputeColorRadial.sdsl` | Generics with `Global.Time`; premultiplied-alpha note |
| Sub render feature / permutations | SpaceEscape `BendFogRenderFeature.cs`, `SpaceEscapeEffectMain.sdfx` | #70 |
| Particle module authoring (spec 23) | `CustomParticleInitializer.cs:27-33,35-75,91-111`; `CustomParticleUpdater.cs:29-34,42-50,57-129` (`IsPostUpdater`, custom `ParticleFieldDescription<Vector2>`); `CustomParticleSpawner.cs:66-98` (`GetMaxParticlesPerSecond`, `MarkAsDirty`, carry-over); `CustomParticleShape.cs:29-137`; `ParticleCustomMaterial.cs` + `ParticleCustomEffect.sdfx` (`compose` slots) | Complete live reference for every module type |
| Particles from scripts | VR `TeleportController.cs:73-82` (`Emitters[0].Initializers[2] as InitialPositionArc`); UIParticles `:263-275,305-317` (`Play/Stop/ResetSimulation/Dispose`) | |
| UI in code (spec 25) | JumpyJet/SpaceEscape `UIScript.cs`; `UIByCode.cs`; `NextSceneScript.cs:41-59` (`StripDefinition`, `SetGridColumn`); UIParticles `:142-169,335-372` (`SetPanelZIndex`, `UniformGrid`, `StretchType.Fill`); GameMenu `:191-208` (gauge), `:240-274,333-355` (`UILibrary.InstantiateElement<T>`, `FindVisualChildOfType<T>`), `:373-380` (`EditText`); `UIByEditor.cs:26-29` | Also `GetCanvasRelativePosition` in `SoundScript.cs:50,78-79` |
| `UIElementLinkComponent` | #75 | Name-matched link |
| Render groups | #74 | Second camera + `Group01` |
| Prefabs / cloning (spec 12) | `CloneEntityDemo.cs:26-54`; `InstantiatingPrefabsDemo.cs:18-36`; `RemoveEntitiesDemo.cs:56-66`; `LevelGenerator.cs:40,63`; `ObjectSpawner.cs:24-38` (reuse one instance) | |
| Content loading | `LoadingContentDemo.cs:29-37`; `PipesScript.cs:35,39` (`UrlReference<Prefab>`) | |
| Scene loading | `LoadScene.cs`, `LoadChildScene.cs`, `SceneStreaming.cs`, `SceneSelectorComponent.cs`, `NextSceneScript.cs`, `TutorialUI.cs:101-120` | #63 + snippet |
| Collision triggers | `CollisionTriggerDemo.cs:17-42` (`Collisions.CollectionChanged`); `AsyncCollisionTriggerDemo.cs:20-41`; `Teleport.cs:18-27` (move a rigidbody: `UpdateWorldMatrix` + `UpdatePhysicsTransformation`) | |
| Raycast / project / unproject | `RaycastDemo.cs:37`; `RaycastPenetratingDemo.cs:36`; `ProjectDemo.cs:21-27`; `UnprojectDemo.cs:23-29`; RPG `Utils.cs:144-158`; `RaycastingScript.cs:29-59` | Three unproject styles |
| Audio | `AudioDemo.cs:20-41` (`AudioEmitterComponent["Gun"]` controller); `LoadMusic.cs:21-62` (`ReadyToPlay`, `PlayState`, `Pause`, `Volume` 0–2, `Pan`); `CoinScript.cs:68-69,77`; `SoundScript.cs:40-47,71-72` (`IsLooping`; `Stop(); Play()` retrigger) | All `Sound`-asset-bound |
| Fonts / text | `FontRenderer.cs:135-154,189-228,300-338` (`MeasureString`, `TextAlignment`, rotation/scale/origin overload) | |
| Sprite-sheet animation | `SpriteAnimation.Play(...)` in `PhysicsSample…\CharacterScript.cs:47,53`, `EnemyScript.cs:16`, `BallScript.cs:11`; `SpriteFromSheet.CurrentFrame` as ammo LED (FPS `WeaponScript.cs:42-47`) | |
| `GameProfilingSystem` (spec 15) | `ParticlesSample…\GameProfiler.cs`, `BepuSample…\GameProfilerComponent.cs` | Two hotkey schemes |
| Mouse lock | FPS/TPP `PlayerInput.cs:100-115`; `FirstPersonCamera.cs:31-32,43-52` (`Input.MousePosition = (0.5,0.5)` before locking) | |
| Gamepad | `InputManagerExtensions.cs` ×4; `BasicCameraController.cs:73-86`; `TouchInputsScript.cs:178-185` | |
| Sensors | `GravityScript.cs:20-34` (`Input.Gravity`, axis remap) | Desktop null |
| Character tuning values | FPS `:4851-4863`, TPP `:2848-2851`, Platformer2D `:558-572`, PhysicsSample `CharacterScript.cs:61-65` ("Step Height is extremely important…"), VR Mars gravity | Known-good feel numbers |

**Not used anywhere in templates/samples**: runtime camera switching, `DynamicSoundSource`, GPU picking, `CloneContext`, `TransformComponent.PostOperations`, `IInputEventListener`, `VirtualButtonTwoWay`/groups, post effects or shadows from code (all compositors are assets), `Dispatcher`, `Profiler.Subscribe`, save games, `GameTime.Factor`.

---

## 5. Considered and rejected

| Thing | Where | Reason |
|---|---|---|
| `sources\templates\*`, `TemplatePreprocessor` | | Packaging plumbing |
| `sources\editor\Stride.Assets.Presentation\Templates\*` | 30 files | WPF wizards / asset-from-file generators; the C# script templates are trivial skeletons |
| `samples\Library`, `samples\Others\NativeLinking` | | Blank csproj; two screenshots, no project |
| `NewGame\BasicCameraController.cs` | | Already the toolkit's ancestor; only new note is the desktop-gated gestures |
| `SpriteStudioDemo` | | Niche runtime; 2D mechanics covered by #67/#71 (note bullet built in code with `LinearFactor (1,0,0)`, `AngularFactor 0`; enemy kinematic+trigger reset `EnemyScript.cs:31-57`) |
| `GravitySensor` | | Device sensor; desktop null |
| `SimpleAudio`, `MusicScript`, `AudioDemo`/`LoadMusic` | | `Sound`-asset-bound; API recorded in §4 |
| `GameMenu` as an example | | `.sduipage`/`.sduilib`-built; idioms captured in #66/#74/spec 25 |
| `MaterialShader` | | Duplicate of Starbreach #12 |
| `SpriteFonts` as an example | | Fonts are compiled assets; toolkit has its own font story |
| PhysicsSample `DemoScript` | | Bullet constraint tour; `Example15_*` covers Bepu |
| BepuSample `Components\Utils\*` | | Already claimed/declined by the backlog; only `stackalloc` and `FindAndAttachCamera` are new |
| `CSharpBeginner` basics (10 files) | | Below Getting Started bar; `Example01_*` covers them (`LerpDemo` is the *good* lerp) |
| `CSharpIntermediate` FP/TP cameras standalone | | Subsumed by #61 and `Example20`; note FP camera drives `CharacterComponent.Orientation` for yaw + child pivot for pitch (`FirstPersonCamera.cs:60-65`) |
| VRSandbox as an example | | Headset-bound; API recorded, snippets kept |
| RPG `TaskExtension.InterruptedBy` | | Unused; leaky by design |
| SpaceEscape data holders | | One-liners |
| FPS `AnimationController` standalone | | Asset-bound; folded into #72 |
| Copying `InputManagerExtensions` verbatim | | Adopt into the toolkit rather than as an example |

---

## 6. Suggested backlog additions, condensed

| Proposed | Level · Category | Note |
|---|---|---|
| Input pipeline with events (#60) | Intermediate · Input | Fills thin Input; gamepad helpers ride along |
| Third-person camera: cone sweep + coyote jump (#61) | Intermediate · Input | **Joins Starbreach #1 on the open row** |
| Click-to-move pathfinding (#62) | Intermediate→Advanced · Gameplay | Scenario for spec 11; carries the dynamic-navmesh correction |
| Scene streaming by trigger depth (#63) | Advanced · Gameplay | Pairs with Starbreach `Streaming.cs` |
| Timed spawner / effect table (#64) | Beginners · Gameplay | Toolkit `SpawnTimed` |
| Collectibles (#65) | Beginners · Gameplay | `Task.Delay` vs `WaitTime` contrast |
| Game-state flow + code UI (#66) | Beginners · UI | Second vehicle for `Example36_EventBus` |
| Flappy loop (#67) | Intermediate · Gameplay | |
| Parallax scene renderer (#68) | Intermediate · Rendering | Smallest "own compositor renderer" |
| Endless runner sections (#69) | Intermediate · Gameplay | Physics-free AABB; pairs with spec 6 |
| World-bend sub render feature (#70) | Advanced · Rendering | `Example13` sequel |
| 2D platformer character (#71) | Beginners · Gameplay | Fixes three real starter bugs |
| Blend tree over procedural clips (#72) | Advanced · Gameplay | Merges spec 9 + starters; zero assets |
| SpriteBatch custom effect (#73) | Intermediate · Rendering | |
| Particles over UI via render groups (#74) | Intermediate · Rendering | |
| UI element link (#75) | Intermediate · UI | |
| Async web API (#76) | Beginners · Integration | Smallest honest Integration example |
| Day/night procedural clip (#77) | — | Demo scene for spec 9 |

**Corrections applied to `engine-example-opportunities.md`** in the 2026-09-02 cross-check (kept
here for traceability): the dynamic-navmesh registration story; `IBlendTreeBuilder` has three real
usages and a code-only route (#72); spec 4 cites `VirtualButtonsDemo.cs`; spec 5 cites
`TouchInputsScript.cs`, the SpaceEscape swipe, and the `!Platform.IsWindowsDesktop` gate; spec 9
cites `AnimationScript.cs` for the path syntax; spec 20 cites the engine `RenderTextureSceneRenderer`
and the barrier fix; spec 23 points at the ParticlesSample modules as the live reference. The
Starbreach doc's "not used" list is also stale on `IBlendTreeBuilder` and virtual buttons: templates
and tutorials *do* use them.

Add `stride/samples` (templates, tutorials, games, UI, graphics, particles, input, audio) to the
backlog's **Sources reviewed** table when this graduates; the BepuSample and PhysicsSample rows stand.
