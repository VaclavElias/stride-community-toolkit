# Starbreach Example Opportunities

A harvest of [Starbreach](https://github.com/stride3d/Starbreach) — Stride's official demo
third-person shooter — for toolkit example ideas and game-dev patterns worth documenting. Local
clone at `D:\Projects\GitHub\Starbreach` (cloned 2026-08-30 with `GIT_LFS_SKIP_SMUDGE=1`; the git
tree is 12 MB, the LFS assets it points at total ~8.4 GB and were **not** downloaded — pull a
specific folder with `git lfs pull --include="path/**"` if an example ever needs a real asset).

**Status: research, nothing agreed.** Companion to
[engine-example-opportunities.md](engine-example-opportunities.md) (the engine-source sweep, which
this cross-references throughout). Same graduation path: pick → backlog row / TODO entry → build.

**The iron rule for this source: pattern, never code.** Starbreach targets `net461` and Stride
`4.0.0.1-beta04-1207` (2016-era Silicon Studio demo code, last touched Feb 2022) on **Bullet**
physics. Everything ports as an *idea* rebuilt code-only on current APIs (usually Bepu), the same
way the backlog treats the pre-port Bepu repo.

## Ground truth about the repo

- 6,045 lines across 63 `.cs` files; packages: `Stride.Navigation`, `Stride.Particles`,
  `Stride.Physics` (Bullet), `Stride.UI`, `Stride.Video`.
- Two projects: `Starbreach` (code + assets + the three custom `.sdsl` under `VFX\`) and
  `VFXPackage` — a **code-free, asset-only package** (23 textures, 19 particle prefabs, one FBX)
  pulled in by `ProjectReference`. That structure is itself a documentable pattern.
- What is actually *wired into scenes* (grepped from `.sdprefab`/`.sdscene`): `PressurePlate` ×27,
  `PressurePlateTrigger` ×12, `PatrollingDroneController` ×12, `Drone` ×12, `LaserFence` ×10,
  `Fan` ×9, `Streaming` ×7, drone weapons, soldier scripts ×2. **`InitialLaserAttributes` and
  `BeamPostUpdate` are referenced by no asset** — compiling but never shipped; treat with extra
  care.
- Mixed vintage: most is 2016 code, but `SoldierController.cs:162-397` and
  `CameraController.UnOccludeCamera` are a visibly newer rewrite (`MathF`, tuples, `??=`) — the
  best code in the repo. The older parts supply the cautionary tales, which are teachable too.
- The post-fx stack is configuration only (`Assets/Shared/GraphicsCompositor.sdgfxcomp:172-201`:
  AO, SSR, DoF, Bloom, LightStreak, LensFlare, Hejl2 tonemap, Vignetting, FXAA) — useful as
  "a shipped game's settings" defaults for the engine sweep's `Example40_PostEffects`.

---

## Example candidates

Ordered by value against the toolkit's empty categories (Gameplay, Interaction, Input, Audio).
File references are into the local clone.

### 1. Third-person camera with occlusion sweep ("whiskers")

- **Level:** Intermediate · **Category:** Input · **Verdict:** claims the backlog's open
  **"Third-person camera following a physics body"** row, with a concretely better angle than the
  Bepu repo's `ThirdPersonCameraComponent` (which has no occlusion handling)
- **Source:** `Starbreach\Camera\CameraController.cs:154-191` (parameters `:35-90`; tuned values
  in `MainScene.sdscene:579-600`)
- **The mechanic:** update the pivot's world matrix *explicitly* first (so the sweep uses this
  frame's pivot), sphere-sweep (radius 0.35) from pivot to the ideal camera position, filtering
  out the player's own capsule. The two non-obvious bits every naive implementation misses:
  convert `HitFraction` to camera distance **adding the sweep radius back** so the camera sits on
  the far side of the sphere, not at its centre (`:178-183` — the comment there is the whole
  lesson); and asymmetric response — snap **in** instantly (`min`), ease **out** exponentially and
  framerate-scaled (`:172`). Clamp to `[0.1, 1]` so it never enters the character.
- **Rebuild:** fully procedural (capsule + boxes as occluders); Bepu `SweepCast` with a sphere —
  already confirmed exposed by the backlog.
- **Add as stage two:** the aim/hip stance system (`Camera\CameraParameterBase.cs:89-93` and
  siblings) — every stance-dependent property (`Distance` 3.5→1.5, `Fov` 45→40, `Pan`) is a
  `CameraParameter<T>` interpolated at a constant rate derived from gap ÷ duration, so all
  parameters land together and it's framerate-independent — unlike the `Lerp(cur, target, dt*k)`
  everyone writes.

### 2. Switches, gates and doors — the Activator system

- **Level:** Intermediate · **Category:** Interaction (currently zero examples) · **Verdict:**
  example — and the recommended vehicle for the engine sweep's `Example36_EventBus`: **don't
  build an abstract pub-sub demo; build this**
- **Source:** `Gameplay\Activator.cs`, `PressurePlateTrigger.cs`, `PressurePlate.cs:164-243`,
  `ActivatorCollection.cs:39-80`, consumer `LaserFence.cs:155-178`. Used 27× across the levels —
  this is the game's real puzzle system.
- **The mechanic:** three layers. (1) A trigger script blocks on
  `Task.WhenAny(NewCollision(), CollisionEnded())`, recomputes occupancy against a collision-group
  mask, and broadcasts a `bool` on an `EventKey<bool>` *only on change*. (2) The plate receives it
  on an `EventReceiver<bool>` created **`Buffered`** (so a fast press/release pair isn't lost —
  the distinction the API docs never explain), animates the physical press, and only then
  broadcasts its own event — with momentary, `Toggle` and latching `SingleActivation` modes.
  (3) `ActivatorCollection` combines activators with AND/OR + `Inverted`; the fence polls it.
- **Rebuild:** entirely procedural — sinking plates, sliding-box doors, two plates wired as an
  AND gate. The best worked usage of `EventKey`/`EventReceiver` in any Stride codebase surveyed.

### 3. Enemy perception: trigger volume + line-of-sight raycast

- **Level:** Intermediate · **Category:** Gameplay · **Verdict:** example
- **Source:** `Drones\PatrollingDroneController.cs:191-224` (perception), `:333-344` (LOS),
  `:351-363` (alert radius)
- **The mechanic:** two-stage perception — a kinematic trigger sphere supplies *candidates*
  (iterating `Collisions`, filtered by collision group), each confirmed by a raycast whose hit
  must be *that exact collider* (`raycast.Collider == targetCollider`) — a clean encoding of
  "unobstructed". Endpoints lifted to fixed heights instead of eye bones. On alert, the sense
  radius grows 15→20 (hysteresis: harder to escape than to be spotted). Rebuild notes: Bepu wants
  the sensor shape swapped, not scaled — say so explicitly in the example.

### 4. Patrol → chase state machine

- **Level:** Advanced · **Category:** Gameplay · **Verdict:** example (natural follow-on from #3)
- **Source:** `PatrollingDroneController.cs` whole; `DroneControllerBase.cs:42-92`;
  FSM in `Core\FiniteStateMachine.cs` (see the patterns section for its warts)
- **The mechanic:** chase re-paths only when the target has moved >1 m from the last path target
  (`:325-330`), stops advancing inside 6 m if LOS holds. Pathing consumes
  `NavigationComponent.TryFindPath` as an `IEnumerator<Vector3>` coroutine — and **gracefully
  degrades to a straight two-point path when there's no navmesh** (`DroneControllerBase.cs:47-50`),
  which is exactly the fallback that makes a nav-dependent AI testable code-only.

### 5. Waypoint patrol path with rejoin-from-anywhere

- **Level:** Beginners · **Category:** Gameplay · **Verdict:** example
- **Source:** `Core\Path.cs:73-99` (`SelectWaypoint`), `Drones\DronePath.cs:26-36`
- **The mechanic:** the path is *authored as scene hierarchy* — a `StartupScript` turns its child
  entities into waypoints. `SelectWaypoint` picks the nearest, then skips one ahead if the query
  position already projects past it along the segment — so an AI that wandered off rejoins the
  loop correctly instead of walking backwards. Distinct lesson from the sweep's Catmull-Rom
  `Example33_SplinePath` (smooth interpolation); they'd pair well but shouldn't merge.

### 6. Smooth character turning with banking, over averaged velocity

- **Level:** Intermediate · **Category:** Input (or Gameplay) · **Verdict:** example — the best
  code in the repo
- **Source:** `Soldier\SoldierController.cs:162-197` (rolling window), `:327-397`
  (`SmoothRotate`, `RotateTowardsEased`, `MoveToZeroExp`)
- **The mechanic:** rotate toward *measured movement*, not input — a 0.25 s queue of per-frame
  deltas, re-aggregated from scratch each frame to avoid float drift, yielding `AverageVelocity`
  plus a `DistanceTravelledAverage` that later drives animation playback speed (a real foot-slide
  fix). Easing uses `MoveToZeroExp` — a correct framerate-independent exponential approach
  ("100 units takes 10 s, 200 takes 14.1 s") — the proper fix for the `Lerp(a,b,dt*k)` bug, and
  the same lesson as `MathUtil.ExpDecay` from the engine sweep. Banking lean is derived from
  quaternion dot/cross between successive orientations, decayed by the same primitive, clamped.
- **Rebuild:** capsule + nose cone so the lean is visible; WASD; zero assets.

### 7. Hitscan weapon: cone spread, penetrating raycast, hit filtering

- **Level:** Intermediate · **Category:** Gameplay (Physics cross-link) · **Verdict:** example —
  or contribute the spread/sorting sections to `Example14_Raycast`, which the Bepu plan already
  extends with `RayCastPenetrating`
- **Source:** `Soldier\SoldierWeapon.cs:189-237`
- **The mechanic:** spread sampled as a polar disc (`radius = tan(cone/2)·rand`, `angle = 2π·rand`)
  — not per-axis rand, which biases into the corners; penetrating hits **sorted by
  `Dot(point, rayDir)`** because the result list is unordered; then a filter walk skipping the
  drones' sense-sphere group and disabled colliders — the in-code comment calls that a "hack",
  and the lesson is real: disabled colliders still return hits in Bullet. Damage lands via
  `Utils.GetDestructible(entity)?.Damage(25)` (see patterns, interfaces).

### 8. Missile salvo — grid launcher with randomized order and stagger

- **Level:** Intermediate · **Category:** Gameplay · **Verdict:** example; zero assets needed
- **Source:** `Drones\MissileDroneWeapon.cs:65-136`
- **The mechanic:** build a grid of 2D offsets across a rectangle, pop them **in random order**
  with a few ms of jitter between launches; each launch **recomputes the spawn basis from the
  current transform** because the drone is still turning (a bug people ship when they cache it).
  Spread is a proper cone (random pitch within angle, random roll about the aim axis). The
  takeaway: a satisfying spawn pattern is mostly ordering and jitter, not geometry. Replace its
  `Task.Delay` with a game-clock wait (see the patterns section — that's the repo's biggest wart).

### 9. Projectile lifecycle: collision-or-timeout race, owner exclusion, AoE

- **Level:** Intermediate · **Category:** Physics/Gameplay · **Verdict:** example
- **Source:** `Drones\Projectile.cs:32-76`, `RocketProjectile.cs:61-91`, `LaserProjectile.cs:36-60`
- **The mechanic:** trigger-body projectile (`AngularFactor = 0`, `IsTrigger`, impulse), lifetime
  as `Task.WhenAny(NewCollision(), WaitTime(lifespan))` in a loop, ignoring collisions with
  `Owner` so it can't kill its launcher. AoE damage read from a separate child sensor body and
  scaled by contact distance — where the shipped formula
  `(0.25 + 0.75·|d|/AoE)·Damage` gives *more* damage further out. A genuine bug to point at and
  fix. Companion snippet: the homing projectile (`ProjectileHoming.cs:19-38` — bend velocity
  toward target, renormalize, `Quaternion.BetweenDirections` for the mesh; note it wastefully
  zeroes and re-impulses every frame — show the tidy version).

### 10. Explode into physics fragments, then clean up in stages

- **Level:** Intermediate · **Category:** Physics/Gameplay · **Verdict:** example
- **Source:** `Drones\Drone.cs:394-449`, `VFX\DroneExplosion.cs:19-51`
- **The mechanic:** on death: stop the body, disable the collider, hide the intact model, swap in
  a prefab of fragment rigidbodies, flip each `IsKinematic = false`, radial impulse + randomized
  torque. Cleanup is **staged**: physics disabled after 4 s, entities removed after 10 more — stop
  simulating long before you delete, a real performance habit nobody writes down. The
  `entity.IsDisposed` guards are themselves a teaching point.
- **Rebuild:** N procedural cubes assembled into the original's silhouette — arguably clearer than
  a fractured mesh.

### 11. Sound variation — don't play the same clip twice

- **Level:** Beginners · **Category:** Audio · **Verdict:** merge with the engine sweep's
  `Example27_Audio_ProceduralSound` — procedural tones + variation selection + stop-before-retrigger
  is a better example than either half alone
- **Source:** `Core\RandomSoundSelector.cs`; `Drone.cs:253,381-382`
- **The mechanic:** `AudioEmitterComponent` is a name→sound dictionary, so `"Hit0".."Hit2"` live on
  one emitter; the selector collects controllers *by name prefix*, so a fourth variation is an
  asset-only change. `StopAll()` before `PlayAndForget()` so rapid hits retrigger instead of
  layering — the answer to "why does my hit sound like a machine gun".
- **⚠ Bug to teach from:** `random.Next(0, Sounds.Length - 1)` — exclusive upper bound, so the
  last sound never plays. The same off-by-one is copy-pasted **four times** in the codebase
  (`RandomSoundSelector.cs:34`, `SoldierController.cs:420`, `SoldierWeaponFireFeedback.cs:137,141`).
  Show it as-is, fix it in the example.

### 12. Custom SDSL in a standard material: shader generics + runtime parameters

- **Level:** Advanced · **Category:** Rendering · **Verdict:** example — merges two engine-sweep
  inventory rows (*material node graph in code* + *live material parameters*) with a shipped-game
  worked usage
- **Source:** `Starbreach\VFX\ComputeColorTextureScrollV.sdsl` / `...Param.sdsl`,
  driver `VFX\Fan.cs:34-46`, materials `Assets\Shared\VFX\BlueLockdown.sdmat:8-27`
- **The mechanic — two techniques:** (1) **shader generics**:
  `shader ComputeColorTextureScrollV<float UvSpeed, float colorIntensity>` scrolls UVs by
  `Global.Time`, fades the cylinder caps from object-space Y, and is wired into a material as
  `ComputeShaderClassColor` with compile-time `Generics`, multiplied by an HDR tint `(25,10,25)`
  under additive transparency + `CullMode: None` — the complete force-field/laser-wall recipe,
  and it blooms for free. (2) **runtime parameters**: the `Param` variant declares
  `rgroup PerMaterial { Texture2D MyTexture; float2 Offset; }` and `Fan.cs` sets `Offset` (and
  even the texture) per frame via `pass.Parameters.Set(...Keys.Offset, ...)`. Together they
  demonstrate the permutation-vs-value distinction concretely: generics recompile, `rgroup`
  parameters are free.
- **Rebuild:** procedural cylinder + procedurally generated stripe texture + material built in
  code; the shader is 15 lines, shippable inline.

### 13. Write a particle initializer / updater (laser beam)

- **Level:** Advanced · **Category:** Rendering · **Verdict:** better hook for the sweep's
  "particles, part two" than force fields — users want beams
- **Source:** `Particles\InitialLaserAttributes.cs`, `VFX\BeamPostUpdate.cs` — **dead code**
  (referenced by no asset), so pattern only, never validated in the shipped demo
- **The mechanic:** both declare `RequiredFields` in the constructor and use `unsafe` pool access.
  The gem: given a `TransformComponent Target`, the initializer sets each particle's velocity
  *and lifetime* as `life = distance / speed`, so particles of random speeds all arrive at the
  target simultaneously — a real "beam connecting two points" technique. `[DataContract]` +
  `[Display]` is how a module appears in Game Studio; the `Target` field is the "aim this effect
  at that entity" idiom. See also the readable `vfx-LaserBeam.sdprefab` in VFXPackage: two
  `ShapeBuilderRibbon` emitters + one billboard, empty `Updaters` — the beam look is ribbon shape
  plus initializers, not per-frame updates.

### 14. Respawn loop + runtime camera switching

- **Level:** Beginners–Intermediate · **Category:** Gameplay (+ a small Rendering example) ·
  **Verdict:** example; the camera-slot half is on no list anywhere and is frequently asked
- **Source:** `Core\PlayerSpawner.cs`, `Soldier\SoldierSpawner.cs:24-104`
- **The mechanic:** template-method spawner — instantiate, position, run a `PreSpawnPlayer` hook
  **before adding to the scene** (scripts haven't started, so they can still be configured), then
  add. And the undocumented bit: switch the active camera at runtime via
  `CameraComponent.Slot = SceneSystem.GraphicsCompositor.Cameras[0].ToSlotId()` (deactivate with
  `new SceneCameraSlotId()`) — spectator cam, death cam, cutscene cam. Rebuild the prefab as a
  template-entity clone (sweep's `Example35_CodeOnlyPrefabs`).

### 15. Turret head tracking (decoupled yaw)

- **Level:** Beginners (yaw) / Advanced (bone override) · **Category:** Gameplay · **Verdict:**
  example
- **Source:** `Drones\Drone.cs:298-354`, `Core\Utils.cs:43-54` (`UpdateYaw`), `Drone.cs:21-29,241`
- **The mechanic:** body yaw (world) and head yaw (local, relative to body) converge separately at
  constant angular rates, with the arc-shortening fix (`if |Δ| > π, wrap by 2π`) that cures "my
  turret spins the long way round"; firing gated on the head being within 1° of target. The yaw
  half rebuilds procedurally (cylinder body + child barrel, no skeleton). The bone half is the
  advanced lesson — see the worked-usage table: it's the only real-world
  `TransformComponent.PostOperations` use found anywhere.

### Snippet-tier (sections inside other examples, not standalone)

- **Instance-varied hover** — `VFX\FloatingRock.cs:23-41` seeds `Random(Entity.GetHashCode())` and
  randomizes frequency/amplitude/phase so a field of rocks doesn't pulse in unison. Contrast in
  the same repo: `Drone.cs:50` uses `new Random()` in a field initializer — on .NET Framework,
  drones constructed the same frame get **identical** phases. Right and wrong way, two files apart.
- **Chase-light guide path** — `VFX\GuidePath.cs`: reassign `ModelComponent.Materials[0]` between
  on/off materials down a chain of children, with a 25% overlap so it reads as motion. Teaches
  that `Materials[i]` is a live override.
- **Rumble as an envelope** — `Core\Utils.cs:124-141`: lerp the two motor speeds over a duration
  as a script task; hit vs death get different envelopes (`SoldierController.cs:413,430`). Feeds
  the sweep's gamepad row with a better angle than a bare `SetVibration`.

---

## Game-dev patterns worth documenting

Material for a docs/manual page (in the confirmed teaching style — real repo code, honest
verdicts, scar tissue). Starbreach supplies both the patterns and the anti-patterns, often within
one file of each other.

### Good — worth teaching as-is

- **Interfaces as the contract between systems** (`Core\IDestructible.cs`, `IStunnable`,
  `IUsable`): the weapon never knows what a drone is — it finds `IDestructible` on the hit entity
  and calls `Damage(25)`. Lookup is a LINQ scan of components per hit
  (`Utils.cs:99-112` — `Entity` is `IEnumerable<EntityComponent>`); document both the idiom and
  its allocation cost (cache or marker component as the fix).
- **Input as its own component with an explicit priority** (`Soldier\SoldierPlayerInput.cs`):
  reads devices only, exposes state + delegates, and **asserts its own `Priority < 0` in
  `Start()`**; scene sets input at −5, gameplay default, drones at 4000 — a real per-frame
  ordering scheme. The gamepad handling is the part worth lifting: dead zone, then **rescale the
  remaining range so aim starts at 0 just outside it**, clamp, then a `pow(x, 1.6)` response
  curve. Second independent vote for the backlog's "Gamepad input helpers" row (marked *Worth
  adopting* from BepuSample) — and this version has the dead-zone rescale + curve BepuSample
  lacks.
- **Validate inspector-wired references in `Start()`, loudly**: `SoldierController.cs:125-130`
  throws six `ArgumentException`s up front, including a *structural* invariant
  (`Transform.Parent != null` → throw). Unfashionable, correct: an exception at startup beats a
  null-ref twelve frames later.
- **Manual animation layering** (`Soldier\SoldierAnimation.cs`) — the honest state of the art:
  two FSMs (lower/upper body) over one `AnimationComponent`; linear-blend clips kept *before*
  additive ones in `PlayingAnimations` by an explicit boundary index; four-way directional walk
  blended by dot-products against cosine thresholds; `TimeFactor` tied to measured distance
  travelled (foot-slide fix); stance swaps that preserve `CurrentTime`/weights so the pose doesn't
  pop. **Notably: Starbreach does *not* use `IBlendTreeBuilder`** — the shipped demo hand-rolled
  it. Asset-bound, so a docs note rather than an example.
- **Service interface instead of singleton** (`IStarbreach` via
  `Services.AddService<IStarbreach>(this)`, `StarbreachGame.cs:36`) — the good counter-example to
  the singleton wart below; pairs with the sweep's `ServiceRegistry` row.
- **Debug-print with an auto-resetting line counter** (`Utils.cs:56-92`): a one-time task at
  `int.MinValue` priority zeroes the line counter each frame so callers never manage positions;
  drawn twice for a 1 px shadow. The toolkit's debug-text helpers could adopt the counter reset.
- **Asset-only shared package** (`VFXPackage`): a project with no C# — just textures + VFX prefabs
  + an `.sdpkg` — shared by `ProjectReference`. The idiomatic way to share content between Stride
  projects; worth a short docs note.
- **Level organisation**: main scene holds player/lights/camera plus per-platform `Streaming`
  entities pointing at child-scene URLs; navmeshes are per-platform assets bound to one scene
  each. Realistic structure worth describing (along with the honest note that the repo also
  carries `PropsOld/`, a dead `Effects` folder reference, and a leftover Xenko-era asset — real
  projects accumulate this).

### Cautionary tales — teach the wart and the fix

- **`Task.Delay` vs the game clock — the most important wart in the repo.** `Utils.WaitTime`
  (`Utils.cs:114-122`) exists and is correct (loops on `NextFrame`, compares `UpdateTime.Total`,
  respects pause and `GameTime.Factor`) — and yet wall-clock `Task.Delay` is used for game timing
  in at least a dozen sites (`Drone.cs:409-445`, `PatrollingDroneController.cs:116,120`, both
  drone weapons, both projectiles, `SplashScreen.cs`). Consequences: slow motion and pause don't
  affect them, and the tasks outlive their entities — which is exactly why `Drone.cs:437,439`
  needs `IsDisposed` guards. This is the concrete failure mode the time-control example (Bepu
  plan `Example26_TimeControl` + the sweep's `GameTime.Factor` row) should demonstrate: *the
  reason your slow motion doesn't slow the explosions is `Task.Delay`.*
- **The coroutine FSM** (`Core\FiniteStateMachine.cs` + `Core\State.cs`): the *idea* is good —
  states with `Task`-returning `Enter`/`Exit` so a transition can await an animation
  (`SoldierAnimation.cs:319-338` awaits the draw-weapon clip), the machine running as one
  `AddTask` micro-thread. The implementation has four teachable flaws: `SwitchTo` only queues, so
  `CurrentStateName` lags a frame (hence defensive re-checks at every call site); `Exit()` never
  runs the current state's `ExitMethod` (leaks "on" state); `(PatrolState)from` hard-casts the
  previous state and throws if entered from anywhere else (the comment admits it); and singleton
  state objects carry per-run mutable data. A toolkit FSM example should fix all four.
- **Weapon timer done twice** — `SoldierWeapon.cs:96-124` has no accumulator (effective cap: one
  shot per frame), overwrites the inspector's fire rate in `Start()`, and encodes reload by
  writing a *future* timestamp into the last-shot field (one field, two meanings).
  `DroneWeapon.cs:28-51` does the same job properly (`CanShoot`/`TryShoot`/`StartReloading`).
  Show both; the drone version is the pattern.
- **`SyncActivator`** (`Activator.cs:28-46`): re-implements the `SyncScript` loop on top of
  `AsyncScript` because the contract was put in a base class instead of an interface. One-line
  docs moral: *if you find yourself re-emulating `SyncScript`, your abstraction wanted to be an
  interface.*
- **Singletons and magic strings — the fragility catalogue**: `SoldierController.Instance` read by
  an *enemy* (`Drone.cs:427`); bone and entity names as string literals everywhere
  (`"Bone_turret_ring"`, `Name.StartsWith("DroneFr")`, `FindChild("AoESensor")`, ...) — rename in
  the editor, break the game silently. Pair with the `IStarbreach` service as the fix.
- **Demo-code honesty**: the player's damage line is commented out (godmode shipped,
  `SoldierController.cs:409-411`); the HUD is commented out wholesale; `IStunnable` is implemented
  and read but `Stun()` is never called. Useful for setting expectations about demo repos.

---

## Worked-usage references for engine-sweep topics

Where Starbreach exercises an API the [engine sweep](engine-example-opportunities.md) flagged as
undocumented — evidence and tuned values for those examples.

| Sweep topic | Starbreach usage | What it shows |
|---|---|---|
| `EventKey`/`EventReceiver` (spec 13) | `Gameplay\Activator.cs:20`, `PressurePlateTrigger.cs:22,50`, `PressurePlate.cs:144-168`, `Utils.cs:72-83` | `EventKey<bool>` as `[DataMemberIgnore]` on an abstract base; deliberate **`Buffered`** receivers; `receiver.Dispose()` in `Cancel()`; drain-two-receivers idiom. Build `Example36_EventBus` on this. |
| `TransformComponent.PostOperations` (inventory) | `Drone.cs:21-29,241,468-485` | The only real-world use found: override skeleton `NodeTransformations` for turret aim *after* the transform system runs. |
| `ModelNodeLinkComponent` (inventory) | `BasicDrone.sdprefab:345-348`, `MainScene.sdscene:687-701` | Muzzle + `AudioEmitterComponent` linked to a gun-barrel bone (fire sound emits from the barrel). Confirms the "needs a skinned asset" caveat. |
| Navigation (spec 11) | `DroneControllerBase.cs:42-92` | `TryFindPath` consumed as a coroutine; **straight-line fallback when no mesh** (`:47-50`) — the pattern that makes nav AI testable code-only. Classic `Stride.Navigation`, editor-baked meshes. |
| Script priorities (inventory) | `SoldierPlayerInput.cs:96-97`, `MainScene.sdscene:653`, `Drone.cs:63` | A real whole-game ordering scheme: input −5, gameplay 0, late systems 4000; `AddTask` at `int.MinValue`. |
| Mouse lock (inventory) | `SoldierPlayerInput.cs:155-172` | Click-to-lock/Esc-to-unlock, `IsMousePositionLocked` gating, unlock in `Cancel()` so the cursor isn't left captured. |
| Gamepad vibration (inventory) | `Utils.cs:124-141` + callers | Rumble as a lerped envelope, not a value. |
| `GameTime.Factor` / time control | Negative evidence: `Utils.WaitTime` vs ~12 `Task.Delay` sites | The failure mode the slow-motion example should fix. |
| Child scenes + streaming (inventory) | `Core\Streaming.cs:42-75` | Trigger-driven `LoadAsync<Scene>` → add to `RootScene.Children` → **wait a frame, then `UpdateWorldMatrix()` + `UpdatePhysicsTransformation()` on every physics entity** — without it, every streamed collider sits at the origin. Fold the load-on-trigger + resync fix into the sweep's child-scenes row. |
| Live material params + `ComputeShaderClassColor` (two inventory rows) | `Fan.cs:40-45`, `ComputeColorTextureScroll*.sdsl`, `BlueLockdown.sdmat` | Candidate 12 above — merge the two rows into one example. |
| `GameProfilingSystem` (spec 15) | `GameProfiler.cs:45-138` | A complete hotkey scheme: Ctrl+Shift+P toggle, F1–F3 result types, F4 sorting, ± refresh, paging. |
| Runtime camera switching (**new — on no list**) | `SoldierSpawner.cs:24-42` | `CameraComponent.Slot = compositor.Cameras[0].ToSlotId()`; deactivate with `new SceneCameraSlotId()`. |
| Particle module authoring (spec 23) | `InitialLaserAttributes.cs`, `BeamPostUpdate.cs` | Candidate 13 above (dead code — pattern only). |
| Post effects (spec 17) | `GraphicsCompositor.sdgfxcomp:172-201` | A shipped game's settings, as defaults for the example. |

**Not used by Starbreach**, despite being on the sweep's list — don't expect this repo to help:
`IBlendTreeBuilder` (hand-rolled instead), virtual buttons (raw `IsKeyDown` + `List<Keys>`),
gestures, code-built `AnimationClip`, save games, `DynamicSoundSource`/spatial audio beyond the
emitter component, GPU picking, orbit camera, `IInputEventListener`. (The bundled templates and
tutorials *do* use the first four — see [samples-example-opportunities.md](samples-example-opportunities.md):
three `IBlendTreeBuilder` state machines, `VirtualButtonsDemo.cs`, `TouchInputsScript.cs`, and a
dead-but-complete procedural `AnimationClip` in the AnimatedModel sample.)

---

## Considered and rejected

| Thing | File | Reason |
|---|---|---|
| Screenshot capture | `VFX\SaveRenderFrame.cs` | Empty — body is `// TODO Take a screenshot`. |
| Startup level merge | `Core\StreamingScript.cs` | Self-described placeholder; superseded by `Streaming.cs`. |
| Comet spawner | `Particles\CometSpawner.cs` | Hand-rolled gravity on a transform; toolkit spawning/instancing examples supersede it. |
| Manual drone driving | `Drones\TestDroneController.cs` | Dev test rig; mouse-lock half covered elsewhere. |
| Constant rotators | `Environment\SpinModel.cs`, `VFX\RotateAxisY.cs` | Trivial; only note = same job as `SyncScript` vs `AsyncScript`, a two-line aside. |
| Weapon fire feedback | `Soldier\SoldierWeaponFireFeedback.cs` | Needs 4 particle systems + 8 sounds + a light; half dead (`displayBulletHole` computed then hard-overwritten to `false`). Keep only the `ParticleSystem.Play()`/`Timeout(n)` idiom. |
| HUD code | `SoldierController.cs`, `SoldierWeapon.cs:130-176` | Commented out wholesale; the live `Vector3.Project` crosshair fragment sits under a disabled `if` with a TODO about lag. Cautionary only. |
| `IUsable` interaction | `SoldierController.cs:235-268` | The sphere-sweep runs every frame and its result is discarded — the use block is commented out. The sweep-query snippet itself belongs to the Bepu plan's `Example14_ShapeQueries`. |
| `IStunnable` | `IDestructible.cs:7-16`, `Drone.cs:356-364` | Implemented and read, but `Stun()` is never called. Half a feature. |
| `TaskExtension.InterruptedBy` | `Core\TaskExtension.cs` | Unused; the interrupted path awaits a task that never completes — clever, leaky, bad to teach. |
| Splash-screen flow | `Core\SplashScreen.cs` | Asset-bound; the outer loop never exits. Only worth "an `AsyncScript` can own a whole game-flow sequence". |
| A dedicated force-field shader | — | Doesn't exist: the force-field look is scroll shader + additive + `CullMode: None` + HDR tint under bloom (candidate 12). That recipe *is* the takeaway. |

---

## Suggested backlog additions, condensed

For when items graduate — rows in the format [example-backlog.md](example-backlog.md) expects,
Source = "Starbreach" + file. Also add Starbreach to the backlog's **Sources reviewed** table
pointing here.

| Proposed | Level · Category | Note |
|---|---|---|
| Third-person camera with occlusion sweep | Intermediate · Input | **Claims the existing open row**, better angle than the Bepu repo's component |
| Switches, gates and doors (Activator) | Intermediate · Interaction | Vehicle for `Example36_EventBus`; empty category |
| Enemy perception (trigger + LOS) | Intermediate · Gameplay | Empty category |
| Patrol → chase FSM | Advanced · Gameplay | Follow-on; FSM rebuilt without the four flaws |
| Smooth turning + banking | Intermediate · Input | Best code in the repo |
| Missile salvo (grid, jitter, stagger) | Intermediate · Gameplay | Zero assets |
| Projectile lifetime + AoE | Intermediate · Physics | Includes the backwards-falloff bug to fix |
| Explode into fragments, staged cleanup | Intermediate · Physics | |
| Sound variation + procedural tones | Beginners · Audio | Merge with sweep `Example27_Audio_ProceduralSound` |
| Custom SDSL in a standard material | Advanced · Rendering | Merges two sweep inventory rows |
| Particle initializer/updater (beam) | Advanced · Rendering | Better hook for "particles, part two" |
| Runtime camera switching | Beginners · Rendering | New — on no prior list |
| Waypoint path + rejoin | Beginners · Gameplay | Distinct from the spline example |
| Homing projectile | Beginners · Physics | Snippet-tier |
| Instance-varied idle motion | Getting Started · Shapes | Snippet, with the `new Random()` seeding trap |

Plus one docs-page candidate outside the example system: a **game-dev patterns** manual page
built from the patterns section above (FSMs, interfaces-as-contracts, input priority, the
`Task.Delay` wart, validate-in-`Start`) — Starbreach provides the real code and the scar tissue
in the house style.
