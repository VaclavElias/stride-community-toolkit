# bepuphysics2 demos - example and helper opportunities

Harvested 2026-09-05 from `D:\Projects\GitHub\bepuphysics2` (HEAD `768838f95`, 2026-08-21, version
`2.5.0-beta.24`), scanning Demos, DemoUtilities, DemoRenderer, DemoBenchmarks, DemoTests and
Documentation - not the physics internals. Deduped against [plans/bepu-examples.md](plans/bepu-examples.md),
[example-backlog.md](example-backlog.md), the shipped Bepu examples' metadata, `src/Stride.CommunityToolkit.Bepu`
and the engine integration at `stride/sources/engine/Stride.BepuPhysics`.

Produced by a read-only research agent; line numbers are as read that day and should be re-checked
before porting. Sibling docs: [engine-example-opportunities.md](engine-example-opportunities.md),
[samples-example-opportunities.md](samples-example-opportunities.md),
[box2d-example-opportunities.md](box2d-example-opportunities.md).

**Licence for porting.** Apache 2.0 (`LICENSE.md`). Compatible with the toolkit's MIT, but §4
requires the attribution notice on any *derived* file. Rule: idea-level ports need nothing; a file
that is a recognisable translation of a demo carries a one-line header "adapted from bepuphysics2
`<file>`, Apache-2.0, (c) Ross Nordby".

## 1. Demo scenes worth porting

### G1 - Cloth from a constraint lattice - **built 2026-09-06** as `E05_3D_Cloth`
- **Source:** `Demos/Demos/ClothDemo.cs:114-281` (`CreateBodyGrid` :121, `CreateDistanceConstraints` :164, `CreateAreaConstraints` :143); variant `Demos/SpecializedTests/ClothLatticeDemo.cs`.
- **What it does:** four 10x30 sheets differing only in stiffness and whether area constraints are present, then a 96x96 sheet draped over two static capsules. Render text says it: "the library has no special case for cloth; standard bodies and constraints work well." Distance constraints use a limit with minimum = 15 % of rest length so the cloth bunches but never stretches (:176); area constraints (3-body) stop the shear that distance-only lattices show.
- **Toolkit form:** `Example27_Cloth` (or `E05_3D_Constraints_Cloth`), 4-way comparison + one big sheet.
- **Covered?** No. **Access:** components - `AreaConstraintComponent` and `CenterDistanceLimitConstraintComponent` exist in `Stride.BepuPhysics/Constraints/`. Caveat: the demo suppresses self-collision between nodes within 3 lattice steps via a custom narrow-phase filter; Stride's `CollisionGroup` rule is hard-wired to "absolute index difference < 2" (`Definitions/CollisionGroup.cs:9-23`), so only immediate neighbours can be excluded. Accept that or make the nodes non-colliding.
- **Effort:** M. **Why:** cloth is a top-5 "can Stride do X" question and the answer is undiscoverable.

### G2 - Soft bodies: what Stride's `SoftBodyComponent` actually is
- **Source:** `Demos/Demos/NewtDemo.cs:660-764` (`CreateDeformable`), voxeliser at :275+.
- **What it does:** voxelises a mesh, puts a sphere body at each lattice vertex, `Weld`s every unique edge, adds `VolumeConstraint` per tetrahedron. Interior vertices get no collidable at all (:697-699); volume constraints are subtle and `PlumpDancerDemo` omits them for scale.
- **Stride reality:** `Stride.BepuPhysics.Soft/SoftBodyComponent.cs:52` calls `Newt.Create(...)` - Stride ships this demo's code as its soft-body feature (`Soft/Definitions/BepuThings.cs`, `DumbTetrahedralizer` :275, `Newt` :484). **The volume-constraint loop is commented out in Stride's copy** (`BepuThings.cs:607`), so Stride soft bodies are welds only.
- **Toolkit form:** `Example28_SoftBody`; a manual note that volume constraints are disabled upstream.
- **Covered?** Partial (backlog "Soft bodies | Advanced | Idea"). **Access:** component (needs a `Model`). **Effort:** S-M. **Why:** a shipped Stride feature with zero examples anywhere.

### G3 - Planet / per-body gravity
- **Source:** `Demos/Demos/PlanetDemo.cs:20-48`, `Demos/Demos/PerBodyGravityDemo.cs:20-89`.
- **What it does:** replaces `IPoseIntegratorCallbacks.IntegrateVelocity` - 1/r² pull toward a centre; per-body gravity via a `CollidableProperty<float>` lookup, with a long comment on why per-body gathers are awkward in an AOSOA callback.
- **Access:** needs raw Bepu and Stride blocks it: `StridePoseIntegratorCallbacks` is an `internal struct`, non-replaceable (`Definitions/StridePoseIntegratorCallbacks.cs:12`); per-body gravity in Stride is a bool (`MaterialProperties.Gravity` / `BodyComponent.Gravity`). Port: `BodyComponent.Gravity = false` + `ISimulationUpdate.SimulationUpdate` applying `ApplyLinearImpulse(dir * G * mass * dt / d²)` per body. Say so in the example.
- **Toolkit form:** `Example29_PlanetGravity`. **Covered?** Partial (backlog "Custom gravity" assumes `PoseGravity` + toggle suffices - not for orbits). **Effort:** M. **Why:** the most screenshot-friendly physics demo there is.

### G4 - Continuous collision detection, three columns
- **Source:** `Demos/Demos/ContinuousCollisionDetectionDemo.cs` (layout :67-117, spinner rig :22-48).
- **What it does:** 100 boxes in three configurations dropped at -150 m/s - tiny margin (tunnels), unlimited margin (default, works), tiny margin + sweep - plus four motorised blades, two pairs with and without sweeps, showing ghost collisions (blades slowing each other without touching).
- **Access:** components - `BodyComponent.ContinuousDetectionMode`, `.SpeculativeMargin`, `.ContinuousDetection` (`BodyComponent.cs:92/305/319`). Spinner rig = `AngularHingeConstraintComponent` + `AngularAxisMotorConstraintComponent` + `OneBodyLinearServoConstraintComponent`.
- **Toolkit form:** `Example30_ContinuousCollision`, anchored by a manual page (D3). **Covered?** Partial. **Effort:** M. **Why:** "my bullet went through the wall" is the most-asked physics question.

### G5 - Substepping / solver-iteration live tuning
- **Source:** `Demos/Demos/SubsteppingDemo.cs` - three impossible rigs (10 000:1 wrecking ball, 20-box stack under a 10 000:1 block, motorised high-stiffness chain), Z/X/C/V change substeps and iterations at runtime (:128-153).
- **The gem inside:** `AwakenAllBodies()` at :111-126 - changing solver settings does nothing visible until you wake everything.
- **Access:** partly blocked. `BepuSimulation.SolverIteration` and `.SolverSubStep` are `get; init;` (`BepuSimulation.cs:218,227`), so runtime tuning goes through `bepuSimulation.Simulation.Solver.SubstepCount/VelocityIterationCount` (raw Bepu, reachable - `Simulation` is public at :70). Waking: `BodyComponent.Awake` (:180). **Belongs in ARCHITECTURE.md.**
- **Toolkit form:** `Example31_SolverTuning` + manual page. **Covered?** Partial. **Effort:** M. **Why:** the fix for every "my stack jitters" report, and it looks unreachable today.

### G6 - Bounciness / friction parameter grids
- **Source:** `Demos/Demos/BouncinessDemo.cs:90-137`, `Demos/Demos/FrictionDemo.cs`.
- **Pattern to steal:** a 100x100 grid where X varies spring frequency and Z varies damping ratio - the whole parameter space in one screenshot. Counterintuitive finding (:131-135): raising spring frequency *reduces* bounce unless the substep count also rises.
- **Toolkit form:** feeds plan item #4 `Example24_PhysicsMaterials`. **Covered?** Yes in the plan, but the grid layout and the frequency/substep interaction are not in the spec. **Access:** `CollidableComponent.SpringFrequency/SpringDampingRatio/FrictionCoefficient/MaximumRecoveryVelocity` (:107-164). Stride's blend rule is fixed inside `StrideNarrowPhaseCallbacks`. **Effort:** S.

### G7 - Colosseum + bullets
- **Source:** `Demos/Demos/ColosseumDemo.cs` (ring builders :19-60, shooting :100-118), `Demos/Demos/PyramidDemo.cs`.
- **What it does:** a stack of box rings; Z fires a small fast sphere, X a 100 kg block with a 0.1 speculative margin. Reusable `CreateRingWall`/`CreateRingPlatform`/`CreateRing` maths.
- **Toolkit form:** `Example32_Colosseum`, or fold the ring builder into `Example23_CubeFountain`'s throw. **Covered?** Partial. **Effort:** M. **Why:** the demo people record and post.

### G8 - Rope twist: adjacent-link collision filtering
- **Source:** `Demos/Demos/RopeTwistDemo.cs:15-79` (`RopeFilter`, `RopeNarrowPhaseCallbacks`), scene :80-170.
- **What it does:** four 130-link ropes with a 10 000:1 wrecking ball spun at 20 rad/s; adjacent links within N indices never collide. `SolveDescription(1, 60)` - 60 substeps, 1 iteration.
- **Access:** components, and the mapping is clean: Stride's `CollisionGroup` docs use this exact chain example (`Definitions/CollisionGroup.cs:19-23`), `Id` = rope index, `IndexA` = link index.
- **Toolkit form:** a Z-toggle for link self-collision in the shipped `E05_3D_Constraints_Rope`, or a cross-link from `E05_3D_CollisionGroup`. **Covered?** No. **Effort:** S. **Why:** "my chain links collide with themselves and jitter" is the next bug after building a rope.

### G9 - Chain fountain (Mould effect)
- **Source:** `Demos/Demos/ChainFountainDemo.cs` - 4096 capsule beads coiled in a bin, `BallSocket` + `SwingLimit` per link, the tip kicked at 20 m/s.
- **Access:** components (`BallSocketConstraintComponent`, `SwingLimitConstraintComponent`), but 4096 entities x 2 constraint components is a lot for Stride's per-entity model - scale down to a few hundred beads.
- **Toolkit form:** `Example33_ChainFountain`. **Covered?** No. **Effort:** M.

### G10 - Ragdoll joint recipe
- **Source:** `Demos/Demos/RagdollDemo.cs` - `AddArm` :203-306, `AddLeg` :308+, `BuildAngularMotor` :195-201, filter type :18-45.
- **The recipe:** shoulder = `BallSocket` + `SwingLimit` + `TwistLimit` + `AngularMotor`; elbow = `SwivelHinge` + `SwingLimit` + `TwistLimit`; knee = `Hinge` + `SwingLimit`; wrist/ankle = `BallSocket` + `SwingLimit` + `TwistServo`. Comment at :197-200: angular motors with damping 0 and max force ~75 make ragdolls behave like action figures; swap for `AngularServo` and you have physics-driven animation.
- **Access:** every constraint type has a Stride component. `SubgroupCollisionFilter` does not - use `CollisionGroup` (chest -> two arms -> two legs branching doesn't fit the linear index rule; disable per-limb-chain with distinct `Id`s).
- **Toolkit form:** `Example34_Ragdoll`. **Covered?** Partial (backlog idea). **Effort:** L.

### G11 - Ragdoll tumble dryer
- **Source:** `Demos/Demos/RagdollTubeDemo.cs` (100 lines) - ragdolls in a rotating kinematic tube. Second scene of G10 or a stress example. **Effort:** S once G10 exists.

### G12 - Car: the four-constraint wheel - **built 2026-09-06** as `E05_3D_Car`, see [plans/cars.md](plans/cars.md)
- **Source:** `Demos/Demos/Cars/SimpleCar.cs:38-104` (`CreateWheel`), `SimpleCarController.cs` (Ackermann :58-104), `CarDemo.cs:37-120`, `RaceTrack.cs`, `WheelHandles.cs`.
- **The recipe:** per wheel - `LinearAxisServo` with `TargetOffset = suspensionLength` (suspension spring); `PointOnLineServo` along the suspension direction (keeps the wheel on its strut); `AngularAxisMotor` (drive and brake in one); `AngularHinge` whose `LocalHingeAxisA` is rotated about the suspension axis (steering, `SimpleCar.Steer` :20-26). Body is a two-box `Compound`. Controller adds Ackermann geometry and only re-applies constraint descriptions when they change, to avoid waking the car every frame (:100-101).
- **Access:** all four exist as Stride components - `LinearAxisServoConstraintComponent`, `PointOnLineServoConstraintComponent`, `AngularAxisMotorConstraintComponent`, `AngularHingeConstraintComponent`.
- **Toolkit form:** `Example35_Vehicle`, possibly a `CarBuilder.cs` beside it. **Covered?** Partial - backlog "Vehicle" points at the old Stride BepuSample car, not this. **Effort:** L. **Why:** "how do I make a car" is the largest unanswered request in the backlog; this is a complete minimal recipe.

### G13 - Tank
- **Source:** `Demos/Demos/Tanks/Tank.cs:16-60+`, `TankController.cs`, `AITank.cs`, `TankDemo.cs` (explosion :27).
- Turret swivel servo + barrel pitch servo aimed from a target point; treads as rows of wheels with left/right motor banks; shells and explosion impulse. Components exist. Stretch goal after G12. **Effort:** L.

### G14 - Character obstacle course
- **Source:** `Demos/Demos/Characters/CharacterDemo.cs` - moving platforms :125-187, motorised fans :61-86, seesaw :109-121, "legos" :32-59.
- **Three bits:** (a) kinematic moving platform driven by `velocity = (target - current) / satisfactionTime` for linear and angular - exactly what `BodyComponent.SetTargetPose` does (:390-428), the canonical use; (b) the lego field creates the same boxes as dynamic, kinematic and static side by side - the clearest mobility lesson; (c) fans and seesaw.
- **Toolkit form:** `Example20_BepuCharacter_ObstacleCourse` (sequel to the shipped first-person example). **Covered?** No. **Effort:** M. **Why:** moving platforms are where every homemade character controller breaks.

### G15 - Physics-driven animation (Dancers)
- **Source:** `Demos/Demos/Dancers/DemoDancers.cs` (`DancerControl` :45-60, per-dancer simulations :85-90, :236-250), `DancerDemo.cs`, `PlumpDancerDemo.cs`.
- **What it does:** a ragdoll whose limbs are pulled toward animated control points by `OneBodyLinearServo`, so it dances while fully simulated; a ball-lattice dress draped on it. Each background dancer gets its own single-threaded `Simulation` run in parallel - comment at :87 names it: "cosmetic physics - simulations that don't interact with the main simulation".
- **Access:** `OneBodyLinearServoConstraintComponent` exists; multiple simulations shown by `E05_3D_MultipleSimulations`. **Toolkit form:** `Example36_ActiveRagdoll` and a manual note "use a second simulation for cosmetic physics". **Covered?** No. **Effort:** L.

### G16 - Contact events: the caveats block
- **Source:** `Demos/Demos/ContactEventsDemo.cs:21-39` (eight-point comment), particle-spawn handler :668-705.
- **Why:** the best explanation anywhere of: handlers run on worker threads; a contact existing != touching (speculative contacts have negative depth); pairs of sleeping bodies fire nothing; impact force must be pulled from the solver, not the event.
- **Access:** Stride's `IContactHandler` (`Definitions/Contacts/IContactHandler.cs`) mirrors `OnStartedTouching`/`OnTouching`/`OnStoppedTouching`; the older `IContactEventHandler` is `[Obsolete]` - **`E13_SignalR` uses the obsolete one.**
- **Toolkit form:** feeds plan item #7 `Example16_TriggerZones` and a manual page. **Effort:** S.

### G17 - Impact force visualisation / impact sounds
- **Source:** `Demos/Demos/SolverContactEnumerationDemo.cs:19-70`; the commented force-drawing block in `ChainFountainDemo.cs:82-116` (cylinder per contact scaled by `PenetrationImpulse * substepCount / dt`).
- **Access:** available - Stride wraps it as `Contacts<TManifold>.ComputeImpactForce(contact)` (`Definitions/Contacts/Contacts.cs:52`).
- **Toolkit form:** `Example37_ImpactForces` and the backlog's impact-sounds row (pairs with the Audio family). **Effort:** S-M.

### G18 - Sleeping and waking
- **Source:** `Demos/SpecializedTests/PyramidAwakenerTestDemo.cs`, `SubsteppingDemo.cs:111-126`.
- **Access:** `BodyComponent.Awake`, `.SleepThreshold`, `.MinimumTimestepCountUnderThreshold` (`BodyComponent.cs:132-180`).
- **Toolkit form:** `Example38_Sleeping` - a pile falls asleep, body count drops in the HUD, a key wakes it. **Covered?** No, absent from plan and backlog. **Effort:** S. **Why:** "why did my body stop reacting" and "why did my change do nothing" have the same answer.

### G19 - Spawn/despawn churn
- **Source:** `Demos/SpecializedTests/FountainStressTestDemo.cs` - `QuickQueue<BodyHandle>` removing the oldest body at the cap, statics removed and re-added mid-run, `Deterministic = true`.
- **Toolkit form:** hardening notes for plan item #3 `Example23_CubeFountain`. **Effort:** S.

### G20 - Newton's cradle (honest negative result)
- **Source:** `Demos/SpecializedTests/NewtonsCradleDemo.cs:12-16` - the solver does not conserve momentum across the constraint graph the classical way; "the bounce gets distributed fuzzily". A manual paragraph, not an example.

### G21 - Compound shapes
- **Source:** `Demos/Demos/CompoundDemo.cs:24-212`.
- v2 does no recentering - build far from the origin and `BuildDynamicCompound` hands back the centre (:27-41); `Compound` vs `BigCompound` by child count (:186, :217-220). Stride surfaces the result as `CollidableComponent.CenterOfMass` (:252). **Covered?** Partial (backlog "One body, many shapes"). **Effort:** S-M.

### G22 - Mesh boundary smoothing
- **Source:** `Demos/SpecializedTests/CustomMeshSmoothingTestDemo.cs`, `MeshReduction`.
- Why a body catches on internal triangle edges of a flat mesh and how `MeshReduction` smooths them. A paragraph in plan item #5 `Example25_MeshColliders`.

### G23 - Reference-only
`CustomVoxelCollidableDemo.cs` (custom `IShape`), `SimpleSelfContainedDemo.cs` (best reading for "what Stride does for you"), `GyroscopeTestDemo.cs` (needs `AngularIntegrationMode.ConserveMomentum`; **Stride hard-codes `Nonconserving`** at `StridePoseIntegratorCallbacks.cs:41`, so the Dzhanibekov effect is unavailable - engine gap), `HullContactNaNDemo.cs` (the convention of shipping a minimal repro scene is worth copying - see [upstream/bepu-hull-contact-nan-issue.md](upstream/bepu-hull-contact-nan-issue.md)), `MinkowskiVisualizer.cs`, `DepthRefinerTestDemo.cs`.

## 2. Infrastructure gems

### I1 - `Grabber` (mouse pick-and-drag by servo constraint) - **built 2026-09-05** as `GrabberScript` + `E05_3D_Grabber`
- **Source:** `Demos/Grabber.cs` (135 lines); wired in `DemoHarness.cs:231-271` and :407.
- **What it does:** raycast from the camera through the cursor, keep the hit distance, add `OneBodyLinearServo` (anchored at the local grab point) + `OneBodyAngularServo`, re-`ApplyDescription` each frame at the new target, remove on release. Servo max force scaled by 1 / inverseMass so heavy things stay draggable (:59,65), spring (5, 2), angular servo skipped for locked-inertia bodies (:82,104), grab dropped if the body turns kinematic or is removed (:73-86), `TimestepsUnderThresholdCount = 0` keeps it awake (:118). Q + mouse rotates the held object (`DemoHarness.cs:251-265`).
- **Toolkit form:** `GrabberScript` (SyncScript) or `TryGrab/UpdateGrab/ReleaseGrab` in `Stride.CommunityToolkit.Bepu`, on top of `CameraComponentExtensions.RaycastMouse`.
- **Covered?** Partial and worth fixing - plan item #8 `E05_3D_Constraints_GravityGun` is the same technique as an example only, and the shipped `E05_3D_Constraints` drags by `Teleport` (`Program.cs:141`), which fights the solver.
- **Access:** components (`OneBodyLinearServoConstraintComponent`, `OneBodyAngularServoConstraintComponent`); `TimestepsUnderThresholdCount` -> `BodyComponent.Awake = true`. **Effort:** M. **Why:** every physics demo becomes playable in two lines.

### I2 - `RolloverInfo` (world-anchored labels that expand on hover)
- **Source:** `Demos/RolloverInfo.cs` (70 lines); used by `SubsteppingDemo`, `ContinuousCollisionDetectionDemo` (:99-114), `ClothDemo` (:230-266).
- **What it does:** register (worldPosition, description, previewText); each projects to screen as a small stub, and only the one nearest the mouse expands. Side-by-side comparisons get labelled without a wall of text.
- **Toolkit form:** a `WorldLabelOverlay` script in core (not Bepu-specific), on `DebugOverlay`/`DebugTextPrinter` plus world-to-screen projection. **Covered?** No - `E03_3D_EntityText`/`E03_3D_WorldText` have no proximity reveal; plan Decision 15 wants live values. **Effort:** S-M. **Why:** every "naive vs stabilised" example needs exactly this.

### I3 - Physics timing HUD: `SimulationTimeSamples` + `TimingsRingBuffer` + `Graph`
- **Source:** `Demos/SimulationTimeSamples.cs` (:41-52), `Demos/TimingsRingBuffer.cs` (`ComputeStats` :53-73), `Demos/UI/Graph.cs`, wiring `DemoHarness.cs:69-104`, display modes :112-140 / :395-406.
- **Stages:** Simulation total, PoseIntegrator, Sleeper, BroadPhaseUpdate, CollisionTesting, NarrowPhaseFlush, Solver, BatchCompressor.
- **Access:** `simulation.Profiler[simulation.Solver]` etc. `BepuPhysics.csproj:18,23` defines `PROFILE` in Debug and Release, so the NuGet should expose it - verify against the package Stride references (`Stride.BepuPhysics.csproj:22`). Reach it via public `BepuSimulation.Simulation`.
- **Toolkit form:** a `DebugOverlay` section "Bepu" with per-stage ms + body/constraint counts, optional sparkline. **Covered?** No - `AddProfiler` shows Stride's profiler, not Bepu's stages. **Effort:** M. **Why:** "physics is slow" becomes a two-line diagnosis.

### I4 - Debug view toggles + single-step timestepping
- **Source:** `DemoHarness.cs:280-298` (J/K/L -> `ShowConstraints/ShowContacts/ShowBoundingBoxes` at :409-411), :306-309 (holding middle mouse steps the simulation once every 60 frames).
- **Toolkit form:** extend `CollidableGizmoScript` / `Stride.BepuPhysics.Debug`'s `DebugRenderComponent` to draw constraints, contact points and AABBs; add a "step one physics frame" key to the `Example26_TimeControl` plan item. **Covered?** Partial (colliders only; plan #9 has no single-step). **Access:** contacts/constraints need raw Bepu enumeration (`SolverContactEnumerationDemo`), AABBs are cheap. **Effort:** M.

### I5 - `Controls`: self-describing key bindings
- **Source:** `Demos/Controls.cs` - `HoldableBind`/`InstantBind` with primary and alternative binding, `AppendString` (:380-440), binding table :445-505, controls screen generated from it in `DemoHarness.cs:342-383`.
- **Toolkit form:** a `KeyBinding` record + a `DebugOverlay` section built from the list, in core. `KeyNames.cs` already exists. **Covered?** No - and it fixes plan Decision 18 (a binding lives in three places and drifts). **Effort:** S. **Why:** on-screen help can never go stale again.

### I6 - `DemoMeshHelper.CreateDeformedPlane` (procedural heightfield)
- **Source:** `Demos/DemoMeshHelper.cs:42-79`; used by `CompoundDemo:205`, `SweepDemo:105`, `CarDemo:115`, `FountainStressTestDemo`. Large-mesh fast paths :126-186 (`CreateGiantMeshFastWithoutBounds` -> dummy tree topology + `Refit`).
- **Toolkit form:** `game.Create3DHeightmap(width, height, Func<int,int,float> height, ...)` returning an entity with a `Model` and a `MeshCollider`. Feeds plan item #5. **Covered?** No. **Effort:** M.

### I7 - Headless / benchmark harnesses
- **Source:** `Demos/SpecializedTests/HeadlessDemo.cs` (N warm-up + N measured frames, ms/frame + `BufferPool.GetTotalAllocatedByteCount()`), `DemoBenchmarks/ShapePileBenchmark.cs`, `RagdollTubeBenchmark.cs`, `DemoBenchmarks/README.md`, `BenchmarkHelper.cs`.
- **Toolkit form:** a physics case in `src/Stride.CommunityToolkit.Benchmarks`, and a headless "N bodies for M steps" harness for CI. **Covered?** No. **Effort:** M.

### I8 - Test patterns worth copying
- **Source:** `DemoTests/ConstraintDescriptionMappingTests.cs` (fuzz every constraint description with random bytes, add, read back, assert field-by-field), `DemoTests/PairDeterminismTests.cs`, `Demos/SpecializedTests/DeterminismTest.cs`.
- **Toolkit form:** a round-trip test over Stride's `*ConstraintComponent` setters - would have caught the `MotorDamping` reciprocal finding. **Effort:** M.

### I9 - `Demo.cs` timestep strategies
- **Source:** `Demos/Demo.cs:56-90` - one step per frame; N fixed steps per frame; accumulator with interpolation weight. Thread-count heuristic :44-46 (`ProcessorCount - 2`).
- **Toolkit form:** a manual page mapping onto `BepuSimulation.FixedTimeStep`, `.MaxStepPerFrame`, `.TimeScale`, `.ThreadCount`, `InterpolationMode` - Stride already implements the accumulator and nothing says so. **Effort:** S.

### I10 - Lower value / skip
`DemoSet.cs` + `UI/DemoSwapper.cs` (Launcher covers it; `DemoHarness.cs:44-47` "dispose then force a blocking GC to expose leaks" is a nice trick), `DemoContentBuilder/*`, `DemoUtilities/TextBuilder.cs` (allocation-free appending - mildly interesting for the overlay), `DemoUtilities/Input.cs`, `DemoRenderer`.

## 3. Documentation folder (`Documentation/`)

- **D1 `StabilityTips.md` (57 lines) - the most valuable file.** Two failure modes (incomplete force propagation -> bouncing; excessive stiffness -> explosions), the mass-ratio pathology, and a four-step debugging ladder (raise the rate to 600 Hz to prove it's convergence -> raise iterations -> raise substeps and drop iterations -> if only a higher full rate helps, it's collision detection). Rules: keep constraint frequency below `0.5 / timeStepDuration`; with aggressive substepping, `substepCount ~ 6 x constraintFrequency x timeStepDuration`; "avoid making heavy objects depend on light objects"; ">4 velocity iterations with 4+ substeps is often pointless." -> manual page `docs/manual/physics-extensions/bepu-stability.md`; it explains every tuning finding recorded in `E05_3D_Constraints_Rope`.
- **D2 `Substepping.md` (78 lines).** How substepping sits inside `Timestep` (only solver/integrator repeat, not collision detection), per-substep iteration scheduling (`new SolveDescription(new[]{2,1,1})`), why changing substep counts at runtime is risky (`ScaleAccumulatedImpulses`), and the limitation: contacts are only incrementally updated between substeps and can inject energy - five mitigations. Note Stride's `SolverSubStep`/`SolverIteration` are init-only.
- **D3 `ContinuousCollisionDetection.md` (93 lines).** Speculative contacts and margins; ghost collisions with diagrams (`Documentation/images/ContinuousCollisionDetection/*.png`); `Discrete`/`Passive`/`Continuous` precisely ("if max margin is `float.MaxValue`, Discrete and Passive are identical"; a Discrete body with a small margin can make other bodies' CCD miss it); `minimumSweepTimestep` and `sweepConvergenceThreshold`; speculative contacts are why there is no coefficient of restitution. Anchors G4 and plan item #4.
- **D4 `QuestionsAndAnswers.md` (100 lines) - symptom-first, the toolkit's manual style.** Offset a shape's rotation centre (a `Compound` with one child at a local pose); kinematics are "both unstoppable forces and immovable objects" and a dynamic caught between a kinematic and a static gets pushed out of the level; two kinematics never generate a constraint; zero inverse mass + nonzero inverse inertia opens "a portal to NaNland"; determinism is single-machine and needs `Deterministic = true` when multithreaded (`BepuSimulation.Deterministic`); where restitution went; swept tests hit mesh backfaces while collisions don't.
- **D5 `PerformanceTips.md` (38 lines).** Shape cost ordering (sphere/capsule, box, triangle, cylinder, convex hull; charts `images/collisionPairRelativePerformance.png`, `images/hullComplexityCost.png`), minimise hull vertices, never use a mobile mesh, reuse shapes, leave a core free (`BepuSimulation.ThreadCount`). Relevant to `SharedHullCache` and `ConvexHullColliderExtensions`.
- **D6 `GettingStarted.md` (117 lines)** + `SimpleSelfContainedDemo.cs` - the raw-API walkthrough; a "what Stride's components do for you" sidebar.
- **D7 Low value:** `Building.md`, `PackagingAndVersioning.md`, `UpgradingFromV1.md`, `changelog.md`, `roadmap.md`.
- **Reusable assets:** diagrams under `Documentation/images/` are Apache-2.0 - embed with attribution rather than redraw.

## Top 10, ranked

| # | Gem | Form | Effort | Covered? |
|---|---|---|---|---|
| 1 | I1 Grabber - servo-constraint mouse drag | helper in Bepu library + fixes `E05_3D_Constraints`'s `Teleport` drag | M | partial |
| 2 | G12 Car - the four-constraint wheel recipe | `Example35_Vehicle` | L | partial |
| 3 | G1 Cloth - distance-limit + area-constraint lattice | `Example27_Cloth` | M | no |
| 4 | I3 Physics timing HUD - per-stage Bepu profiler | `DebugOverlay` section | M | no |
| 5 | I2 RolloverInfo - hover-reveal world labels | `WorldLabelOverlay` script | S-M | no |
| 6 | G15 Dancers - servo-driven active ragdoll + cosmetic sub-simulations | `Example36_ActiveRagdoll` | L | no |
| 7 | D1+D2+G5 Stability & substepping - debugging ladder, live tuning, the init-only finding | 2 manual pages + `Example31_SolverTuning` | M | partial |
| 8 | G4+D3 CCD - three-column tunnelling/ghost-collision scene | `Example30_ContinuousCollision` + manual page | M | partial |
| 9 | G10 Ragdoll - the joint recipe + action-figure damping trick | `Example34_Ragdoll` | L | partial |
| 10 | I4 Debug toggles + single-step | extend `CollidableGizmoScript` / `Example26_TimeControl` | M | partial |

**Just outside:** G18 sleeping/waking (S, genuinely missing), G17 impact forces via `ComputeImpactForce` (unblocks impact sounds), I5 self-describing key bindings (smallest effort here, fixes a known authoring pain), G2 soft bodies (shipped feature, no example, volume constraints commented out upstream), G14 character obstacle course.

## Engine gaps surfaced (for the upstream list)

- `StridePoseIntegratorCallbacks` is internal and non-replaceable: no custom gravity fields, no `ConserveMomentum` (Dzhanibekov effect unavailable).
- `BepuSimulation.SolverIteration`/`SolverSubStep` are init-only; runtime tuning needs the raw `Simulation`.
- `SoftBodyComponent`'s volume constraints are commented out (`BepuThings.cs:607`).
- `CollisionGroup` filtering is hard-wired to index difference < 2.
- `E13_SignalR` uses the obsolete `IContactEventHandler` (toolkit-side fix).
