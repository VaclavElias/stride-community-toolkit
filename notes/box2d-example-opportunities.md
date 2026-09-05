# Box2D.NET - example and API opportunities

Harvested 2026-09-05 from `D:\Projects\GitHub\Box2D.NET` (ikpil's C# port of Box2D v3; local checkout
`3.1.1.557-131-g8f5e816`, toolkit pins NuGet `3.1.654`), scanning `Box2D.NET.Samples`, `Box2D.NET.Shared`,
the test project and docs. Deduped against `src/Stride.CommunityToolkit.Box2D` and the four shipped
Box2D examples. Produced by a read-only research agent; line numbers are as read that day, and the
sample sources may be ahead of the pinned package - see the version note at the end.

Sibling docs: [engine-example-opportunities.md](engine-example-opportunities.md),
[samples-example-opportunities.md](samples-example-opportunities.md),
[bepu-demos-opportunities.md](bepu-demos-opportunities.md); library history in
[plans/box2d-library.md](plans/box2d-library.md).

## Context checked first

Toolkit wrapper (src/Stride.CommunityToolkit.Box2D) surfaces today:
- Box2DSimulation - body creation, fixed-step Update, IBox2DSimulationUpdate hooks, contact/sensor handler registration, raycast/overlap forwarding.
- PhysicsWorld2D - world creation, TargetHz/MaxStepsPerFrame/SubStepCount/TimeScale/WorkerCount, SetGravity, accumulator Step.
- PhysicsQueries2D - RaycastClosest, RaycastAll, OverlapPoint, OverlapAABB, OverlapCircle only.
- ShapeFixtureBuilder - box/circle/triangle/capsule/convex polygon. No segment, no chain, no compound helper.
- Events/PhysicsEventRouter2D - contact begin/end/hit + sensor begin/end only (55-114). No joint events, no body-move events, no pre-solve, no custom filter.
- Box2DStrideBridge.SyncTransformsFromPhysics (96-115) already uses b2World_GetBodyEvents move events.
- BodyForces - impulse, impulse-at-point, get/set velocity. Box2DBodyComponent - force/impulse/torque, damping, velocities.
- No joint API anywhere in the library. Joints only hand-rolled in Example18_Box2DPhysics/Helpers/ShapeSpawner.cs:149 (one distance joint).
- No b2World_Explode, b2World_CastShape, b2World_CastMover/b2SolvePlanes, b2CreateChain, B2DebugDraw adapter anywhere.

Existing Box2D examples: Example18 (integration basics + distance joints), Example02_Junkyard (testbed replica, ShapeBatch, kinematic SetTargetTransform), Example02_Junkyard_Playground (entity-per-shape, sensor gate, OverlapPoint picking, camera follow), Example01_Basic2DScene_StressPile_Box2D (instancing, sleep split). Covered: contact/sensor events, kinematic driving, picking, impulses, instancing, stress. Untouched: everything joint-, character-, cast- and callback-related.

## Gems

### 1. Character Mover - capsule controller with no rigid body
- Source: Box2D.NET.Samples/Samples/Characters/Mover.cs (647 lines; core 280-460, plane callback 496-518)
- Quake-style pmove. Character is a bare B2Capsule + transform. Per step: b2World_CollideMover (contact planes), b2SolvePlanes (translation), b2World_CastMover (sweep fraction), 5 iterations, b2ClipVector for velocity. Pogo spring (b2World_CastShape down + b2SpringDamper) floats it above ground and pushes back with b2Body_ApplyForce. Per-shape ShapeUserData { maxPush, clipVelocity } makes elevators rigid and NPCs soft.
- APIs: b2World_CollideMover, b2World_CastMover (B2Worlds.cs:2460, 2532), b2SolvePlanes/b2ClipVector (B2Movers.cs:17, 65), b2World_CastShape (B2Worlds.cs:2391), B2CollisionPlane, B2PlaneResult, b2CreateChain.
- Toolkit: Example18_Box2D_CharacterMover + CharacterMover2D (Move(Vector2 desiredVelocity, float dt), IsOnGround, PogoHertz), backed by PhysicsQueries2D.CollideMover/CastMover/CastShape.
- Covered: No. Effort: L (~180 lines controller; replace SVG level with hand-built chains).
- Why: the thing people ask 2D engines for; v3 mover API barely known, no C#/Stride example anywhere.

### 2. Platformer - one-way platforms via pre-solve callback
- Source: Samples/Events/Platform.cs (261; pre-solve 118-155, ground check 167-200)
- b2World_SetPreSolveCallback + shapeDef.enablePreSolveEvents; callback returns false when contact normal points down relative to player. Jump ground-check via b2Body_GetContactData; bodyDef.motionLocks.angularZ = true.
- APIs: b2World_SetPreSolveCallback (B2Worlds.cs:2633), enablePreSolveEvents (B2ShapeDef.cs:52), b2Body_GetContactCapacity/GetContactData, B2MotionLocks.
- Toolkit: Example18_Box2D_Platformer + Box2DSimulation.SetPreSolveHandler(IPreSolveHandler2D) + Box2DBodyComponent.GetContacts().
- Covered: No. Effort: M. Caveat: callback runs on solver worker threads, must be thread-safe/read-only (Platform.cs:125-127) - toolkit runs 8 workers by default.
- Why: one-way platforms are the most-requested 2D feature, impossible with layers/filters.

### 3. Explosion - b2World_Explode
- Source: Samples/Shapes/Explosion.cs (135); B2ExplosionDef.cs; also Samples/Events/BodyMove.cs
- b2DefaultExplosionDef() -> position/radius/falloff/impulsePerLength/maskBits -> b2World_Explode. Impulse proportional to projected perimeter. Sample also animates weld joints via b2Joint_SetLocalFrameA.
- APIs: b2World_Explode (B2Worlds.cs:2734), B2ExplosionDef, b2CreateWeldJoint, b2Joint_Get/SetLocalFrameA.
- Toolkit: Box2DSimulation.Explode(Vector2 position, float radius, float falloff, float impulsePerLength, ulong maskBits = ~0ul) + Example18_Box2D_Explosion (click to detonate, ShapeBatch rings).
- Covered: No. Effort: S API, S/M example. Why: highest value per line in the scan.

### 4. Ragdolls - Human factory (11 bones, joint springs, friction torque)
- Source: Box2D.NET.Shared/Humans.cs (711), Human.cs, Bone.cs, BoneId.cs; used by Samples/Joints/Ragdoll.cs, ScaleRagdoll.cs, Continuous/BounceHumans.cs, Events/SensorFunnel.cs
- Capsules + revolute joints with limits, spring hertz/damping, friction torque. Runtime setters Human_SetJointFrictionTorque/SpringHertz/DampingRatio/SetVelocity/ApplyRandomAngularImpulse/SetScale/EnableSensorEvents. Ragdoll.cs:55 stability tuning: b2World_SetContactTuning(worldId, 240, 0, 2).
- Toolkit: Example18_Box2D_Ragdoll (spawn, sliders, mouse joint drag). Keep Ragdoll2D example-local.
- Covered: No; needs joint API (gem 12). Effort: L (port ~400 lines) or M (6-bone).

### 5. Soft body / donut
- Source: Samples/Joints/SoftBody.cs (40) + Samples/Donut.cs (105)
- 7 capsules in a ring, revolute joints with enableSpring/hertz/dampingRatio, filter.groupIndex = -groupIndex to avoid self-collision.
- Toolkit: Example18_Box2D_SoftBody (donuts of different stiffness into a bowl, ShapeBatch capsules).
- Covered: No. Effort: S once joints exist. Why: best visual per line; teaches negative group index (Box2DCollisionMatrix does not model it).

### 6. Car / Driving - wheel joints
- Source: Samples/Car.cs (154, reusable Car struct) + Samples/Joints/Driving.cs (285); also DoohickeyFarm.cs, Doohickey.cs
- Car.Spawn: rounded hull chassis + two circle wheels, b2CreateWheelJoint (localFrameA.q = b2MakeRot(0.5*PI), enableMotor, maxMotorTorque, hertz/dampingRatio, lower/upperTranslation, allowFastRotation = true on wheels, rollingResistance = 0.1). Runtime b2WheelJoint_* setters.
- APIs: b2CreateWheelJoint, B2WheelJoints.*, b2ComputeHull/b2MakePolygon with radius, bodyDef.allowFastRotation, material.rollingResistance.
- Toolkit: Example18_Box2D_Car (A/D drive, camera FollowTarget, suspension sliders). allowFastRotation and rollingResistance not exposed by ShapeFixtureBuilder.CreateCustomShapeDef.
- Covered: No. Effort: M.

### 7. Conveyor belt & tangent speed
- Source: Samples/Shapes/ConveyorBelt.cs (69), Samples/Shapes/TangentSpeed.cs (146)
- shapeDef.material.tangentSpeed = 2 on a static platform moves boxes. TangentSpeed.cs:58-75: chain with per-segment B2SurfaceMaterial[] (tangent speed + customColor).
- APIs: B2SurfaceMaterial.tangentSpeed/customColor/rollingResistance/userMaterialId, chainDef.materials + materialCount.
- Toolkit: Example18_Box2D_ConveyorBelt; add tangentSpeed/rollingResistance/userMaterialId to CreateCustomShapeDef (ShapeFixtureBuilder.cs:112 only takes density/friction/restitution/isSensor) or a SurfaceMaterial2D record.
- Covered: No. Effort: S / M.

### 8. Sensor showcase
- Source: Samples/Events/SensorBookend.cs (352), SensorTypes.cs (238), SensorHits.cs (254), SensorFunnel.cs (350), FootSensor.cs (157), Benchmarks/BenchmarkSensor.cs (246)
- Bookend: destroying a sensor or visitor mid-overlap still delivers matched end-touch. Types: static/kinematic/dynamic sensors, filtered by category/mask bits. Hits: sensors vs bullets. FootSensor: grounded state (pairs with gem 2). BenchmarkSensor: 40x40 grid + b2World_SetCustomFilterCallback, polling b2Shape_GetSensorData.
- APIs: b2Shape_GetSensorCapacity/GetSensorData (poll current overlaps), b2Shape_EnableSensorEvents at runtime.
- Toolkit: Example18_Box2D_Sensors (bookend + types + foot sensor); PhysicsQueries2D.GetSensorOverlaps(B2ShapeId).
- Covered: Partial (Events/SensorEventData.cs + Playground gate: begin/end only). Effort: M.

### 9. Joint events & breakable joints
- Source: Samples/Events/JointEvent.cs (241), Samples/Joints/BreakableJoint.cs (258)
- forceThreshold/torqueThreshold per joint + b2World_GetJointEvents; BreakableJoint polls b2Joint_GetConstraintForce/Torque and b2DestroyJoint across all six joint types.
- APIs: b2World_GetJointEvents (B2Worlds.cs:1506), B2JointEvent, b2Joint_SetForceThreshold/SetTorqueThreshold, b2Joint_GetConstraintForce/Torque, b2DestroyJoint.
- Toolkit: IJointEventHandler + PhysicsEventRouter2D.ProcessJoints mirroring contact/sensor pumps; Example18_Box2D_BreakableJoints (rope bridge snaps).
- Covered: No. Effort: M (pump S; needs joint API).

### 10. Tumbler / Spinner / Joint Grid / Washer benchmarks
- Source: Box2D.NET.Shared/Benchmarks.cs - CreateJointGrid (31, 100x100 revolute grid), CreateTumbler (490, motorised box + 2025 boxes), CreateWasher (558), CreateManyPyramids (167), CreateSmash (447), CreateCompounds (797); wrappers Samples/Benchmarks/BenchmarkTumbler.cs (27), BenchmarkSpinner.cs, BenchmarkJointGrid.cs, BenchmarkManyTumblers.cs (194)
- Toolkit: Example02_Tumbler_Box2D and Example02_JointGrid_Box2D in the Junkyard house style.
- Covered: Partial (many bodies yes; many joints no - different solver path, graph colouring). Effort: S each.

### 11. Mouse drag joint
- Source: Samples/Sample.cs:724-800 (MouseDown/Up/Move, QueryCallback at 697)
- b2World_OverlapAABB on 2mm box -> b2Shape_TestPoint -> kinematic anchor body -> b2CreateMotorJoint (linearHertz 7.5, linearDampingRatio 1, maxSpringForce scaled by mass*gravity, maxVelocityTorque = 0.25*sqrt(I/m)*mg). No b2MouseJoint in v3; this is the idiom. b2Joint_IsValid must be re-checked each frame (Sample.cs:791).
- Toolkit: MouseDragJoint2D helper (Begin/Update/End) in the library.
- Covered: Partial (Playground picks + impulse, no drag). Effort: S.

### 12. Joint facade - the biggest hole
- Source: Box2D.NET/B2Joints.cs:402-758 - b2CreateDistanceJoint, MotorJoint, FilterJoint, PrismaticJoint, RevoluteJoint, WeldJoint, WheelJoint; b2DestroyJoint; B2JointType.cs (7 types)
- v3 API awkward: bodies inside def.@base.bodyIdA/bodyIdB; anchors as localFrameA/localFrameB computed with b2Body_GetLocalPoint (Car.cs:84-88, Mover.cs:204-207). @base keyword clash.
- Toolkit: Joints2D static class / JointBuilder2D, e.g. Joints2D.CreateRevolute(worldId, bodyA, bodyB, worldPivot, motorSpeed, maxMotorTorque, enableSpring, hertz, limits) taking a world pivot; Destroy; typed reaction force/torque getters.
- Covered: No. Effort: M. Unblocks gems 4, 5, 6, 9, 10, 11, 14, 16.

### 13. Shape casts, overlap-shape, query showcase
- Source: Samples/Collisions/ShapeCast.cs (430), CastWorld.cs (703), OverlapWorld.cs (402), RayCast.cs, Benchmarks/BenchmarkCast.cs (414); B2Worlds.cs:2185 (b2World_OverlapShape), 2260 (b2World_CastRay generic context), 2391 (b2World_CastShape)
- CastWorld: ray/circle/capsule/polygon casts, closest/any/multiple via fraction return protocol (return fraction = closest, 0 = stop, 1 = all). OverlapWorld: b2World_OverlapShape with arbitrary B2ShapeProxy.
- Toolkit: PhysicsQueries2D.CastCircle/CastCapsule/CastBox(origin, translation, ...) and OverlapShape, returning QueryRaycastHit lists; Example18_Box2D_Queries.
- Covered: Partial (rays, AABB, circle overlap; no shape cast). Effort: M.

### 14. Chain shapes + SVG-path terrain
- Source: Samples/Shapes/ChainShape.cs (219), ChainSegmentShape.cs, ChainLink.cs, Continuous/ChainDrop.cs/ChainSlide.cs/GhostBumps.cs; Samples/Helpers/SvgParser.cs:16 (ParsePath); usage Mover.cs:126-145, TangentSpeed.cs:44-56
- b2CreateChain with points/isLoop/materials: ghost-vertex-free terrain. SvgParser.ParsePath turns Inkscape path strings into chain points.
- APIs: b2CreateChain, B2ChainDef, b2Chain_SetFriction/SetRestitution, b2Shape_GetParentChain.
- Toolkit: ShapeFixtureBuilder.AttachChain(Vector2[] points, B2BodyId body, bool isLoop = false, B2ChainDef? def = null) and AttachSegment; Example18_Box2D_Terrain (rolling hill vs boxes-row that snags).
- Covered: No (Junkyard floor built from overlapping static squares because chains missing). Effort: S API, M example (SVG parser ~120 lines MIT).

### 15. Debug draw adapter - b2World_Draw into ShapeBatch
- Source: Box2D.NET/B2DebugDraw.cs; b2World_Draw at B2Worlds.cs:1175; reference Samples/Graphics/Draw.cs + Draws.cs; option UI Samples/Sample.cs:1067-1098
- 9 delegates (DrawSolidPolygonFcn, DrawSolidCircleFcn, DrawSolidCapsuleFcn, DrawLineFcn, DrawPointFcn, DrawTransformFcn, DrawStringFcn, ...) + ~15 toggles (drawShapes, drawJoints, drawJointExtras, drawContacts, drawContactNormals, drawContactForces, drawFrictionForces, drawBounds, drawMass, drawBodyNames, drawGraphColors, drawIslands, drawChainNormals, drawingBounds, forceScale, jointScale).
- Toolkit: Box2DDebugRenderer wiring callbacks to ShapeBatch.DrawSolidPolygon/DrawSolidCircle/DrawPixelLine (ShapeBatch.cs:104,123,374 near signature match incl. capsule radius). Toggles as properties.
- Covered: No. Effort: M. Why: every future Box2D example becomes ~5 lines of rendering; free diagnostic overlays.

### 16. Machines - scissor lift, gear lift, doohickey
- Source: Samples/Joints/ScissorLift.cs (234; line 39 notes 8 sub-steps needed), GearLift.cs (343), Samples/Doohickey.cs (108), DoohickeyFarm.cs
- Toolkit: Example18_Box2D_Machines with a sub-step slider that visibly stabilises them (PhysicsStepSettings.SubStepCount).
- Covered: No. Effort: M.

### 17. Custom filter callback
- Source: Samples/Shapes/CustomFilter.cs (112), Benchmarks/BenchmarkSensor.cs:50
- b2World_SetCustomFilterCallback + shapeDef.enableCustomFiltering; per-pair logic beyond bit masks.
- APIs: b2World_SetCustomFilterCallback (B2Worlds.cs:2626), B2ShapeDef.enableCustomFiltering, B2UserData.Signed/Ref.
- Toolkit: Box2DSimulation.SetCustomFilter(Func<B2ShapeId, B2ShapeId, bool>) or ICustomFilter2D with entity pairs; escape hatch in Box2DCollisionMatrix.
- Covered: Partial (group matrix only). Effort: S.

### 18. Wind - b2Shape_ApplyWind
- Source: Samples/Shapes/Wind.cs (194; step 160-185)
- b2Shape_ApplyWind(shapeId, wind, drag, lift, wake): drag and lift by projected area and orientation. Noise lerped for gusts.
- Toolkit: BodyForces.ApplyWind(B2BodyId body, Vector2 wind, float drag, float lift) + Example18_Box2D_Wind (flag/kelp).
- Covered: No. Effort: S.

### 19. Runtime geometry modification
- Source: Samples/Shapes/ModifyGeometry.cs (166)
- b2Shape_SetPolygon/SetCircle/SetCapsule/SetSegment + b2Body_ApplyMassFromShapes. Trap (16-17): only dynamic and kinematic shapes.
- Toolkit: Box2DBodyComponent.SetShape / ShapeFixtureBuilder.ReplaceShape. Covered: No. Effort: S.

### 20. Friction & restitution callbacks
- Source: Samples/Bodies/Weeble.cs:40-42; B2Worlds.cs:1958, 1976; B2WorldDef.frictionCallback/restitutionCallback
- Override mixing rules incl. by userMaterialId. Weeble also: b2Body_SetMassData with parallel-axis inertia (71), b2Body_GetLocalPointVelocity (128).
- Toolkit: PhysicsStepSettings/PhysicsWorld2D FrictionMixer/RestitutionMixer; Example18_Box2D_Materials (ice/rubber/wood grid). Covered: No. Effort: S/M.

### 21. World tuning knobs not exposed
- Source: B2Worlds.cs:1677-1994, B2WorldDef.cs
- Missing: b2World_SetRestitutionThreshold (1763), SetHitEventThreshold (1783 - toolkit has EnableHitEvents but no threshold), SetContactTuning (1806), EnableSleeping (1677), EnableContinuous (1743), EnableSpeculative (2778), EnableWarmStarting (1715), SetMaximumLinearSpeed (1841), GetAwakeBodyCount (1733), GetProfile (1861), GetCounters (1898), RebuildStaticTree (2765), worldDef.enableContactSoftening, worldDef.capacity (BenchmarkCapacity.cs).
- Toolkit: extend PhysicsStepSettings (def-time) + settable PhysicsWorld2D properties (runtime); PhysicsDiagnostics2D readout (Profile/Counters/AwakeBodyCount). Covered: No. Effort: S.

### 22. Continuous collision & bullets
- Source: Samples/Continuous/ - Pinball.cs (186, isBullet ball vs flippers), BounceHouse.cs (194; line 99 body-centred circles), SkinnyBox.cs, Drop.cs, SpeculativeFallback.cs, SpeculativeGhost.cs, RestitutionThreshold.cs, GhostBumps.cs, Wedge.cs, ChainSlide.cs, SegmentSlide.cs
- Toolkit: Example18_Box2D_Pinball (playable) and/or Example18_Box2D_Bullets (thin wall, continuous on/off, count pass-throughs). isBullet not exposed on Box2DBodyComponent.
- Covered: Partial. Effort: M / S.

### 23. Determinism - hash the world
- Source: Box2D.NET.Shared/Determinism.cs (156), Samples/Determinisms/FallingHinges.cs
- Hinged lattice, step, hash transforms, compare across runs and worker counts. PhysicsWorld2D doc already claims determinism across worker counts.
- Toolkit: Example18_Box2D_Determinism (1 vs 8 workers side by side, matching hash). Covered: No. Effort: S/M.

### 24. Stacking & robustness
- Source: Samples/Stackings/CardHouse.cs (89), Arch.cs (119), CapsuleStack.cs, Cliff.cs, DoubleDomino.cs, TiltedStack.cs, Confined.cs; Samples/Robustness/Cart.cs (184; header 18-20), HighMassRatio1-3.cs, OverlapRecovery.cs, TinyPyramid.cs
- Toolkit: Example18_Box2D_Stacking with dropdown (DebugTextDropdown pattern from StressPile). Covered: No. Effort: S each.

### 25. Small but sharp
- b2Body_SetTargetTransform - Samples/Bodies/Kinematic.cs (Lissajous). Partial: Junkyard uses it, no Box2DSimulation method.
- b2Body_WakeTouching - Samples/Bodies/WakeTouching.cs. No.
- B2MotionLocks (linearX/linearY/angularZ) - Samples/Joints/MotionLocks.cs, Bodies/MixedLocks.cs. Essential for platformers. No.
- bodyDef.name / b2Body_SetName + drawBodyNames. No.
- B2UserData.Ref(object) on shapes - shape-level game data. Toolkit resolves only body->Entity (Box2DStrideBridge.cs:83) and drops events whose body has no entity (PhysicsEventRouter2D.cs:128,144) - limitation for the entity-less Junkyard style. No.
- b2Body_GetContactData - per-body contact list. No.
- b2Body_ApplyMassFromShapes, b2Body_SetMassData, b2Body_ComputeAABB, b2Shape_GetClosestPoint, b2Body_GetLocalPointVelocity. No.
- Samples/Geometries/ConvexHull.cs (233) - b2ComputeHull with degenerate-input handling; ShapeFixtureBuilder.AttachPolygon calls b2ComputeHull blind (ShapeFixtureBuilder.cs:84) - latent bug.
- Samples/Shapes/EllipseShape.cs, RoundedShapes.cs, OffsetShapes.cs, CompoundShapes.cs - b2MakeRoundedBox, multi-fixture bodies; ShapeFixtureBuilder supports neither compound nor corner radius.
- Samples/Helpers/SvgParser.cs, Box2D.NET.Shared/RandomSupports.cs (seeded RNG) - small, MIT, portable.

## Package version & licensing
- Toolkit pins Box2D.NET 3.1.654 (Stride.CommunityToolkit.Box2D.csproj:22).
- Local checkout: 3.1.1.557-131-g8f5e816; CHANGELOG last titled release [3.1.0.500] 2025-04-26. Action: verify b2World_CollideMover, b2SolvePlanes, b2Shape_ApplyWind, b2World_GetJointEvents, b2Joint_SetLocalFrameA, b2Body_SetTargetTransform and the def.@base.localFrameA joint layout against 3.1.654 before porting (b2Body_SetTargetTransform confirmed present - Junkyard uses it).
- Licence: MIT, (c) Erin Catto 2022, (c) Choi Ikpil 2025; SPDX headers on every sample. Keep an origin header in ported Program.cs naming the file and copyright; Example02_Junkyard_Box2D header is the precedent but lacks the copyright line - tighten.
- Not portable: sample harness (ImGuiNET, Silk.NET/GLFW, Serilog). Port scene-building and step logic only.

## Top 10, ranked
| # | Gem | Form | Effort | Blocked by |
|---|-----|------|--------|-----------|
| 1 | Joint facade (Joints2D, 7 types, world-space anchors) | API | M | - |
| 2 | b2World_Explode wrapper + example | API + Example18_Box2D_Explosion | S | - |
| 3 | Character Mover | API + Example18_Box2D_CharacterMover | L | shape casts |
| 4 | Debug draw adapter (B2DebugDraw -> ShapeBatch) | Box2DDebugRenderer | M | - |
| 5 | Platformer / one-way platforms | API + Example18_Box2D_Platformer | M | motion locks |
| 6 | Car / Driving | Example18_Box2D_Car | M | #1 |
| 7 | Shape casts + OverlapShape | API | M | - |
| 8 | Soft body / donut | Example18_Box2D_SoftBody | S | #1 |
| 9 | Tumbler + Joint Grid replicas | 2 examples | S | #1 for grid |
| 10 | Chain shapes + SVG terrain | API + Example18_Box2D_Terrain | S/M | - |

Runners-up: joint events + breakable joints (9), conveyor/tangent speed (7), sensor showcase (8), mouse drag joint (11), world-tuning knobs (21 - half a day of API, no example needed).
