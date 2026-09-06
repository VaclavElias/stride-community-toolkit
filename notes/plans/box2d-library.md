# Plan: Stride.CommunityToolkit.Box2D library

This began as `E06_Box2D/IMPROVEMENTS.md`, a wish list for `Box2DSimulation.cs` back
when it lived inside that example. The library was extracted into
[`src/Stride.CommunityToolkit.Box2D`](../../src/Stride.CommunityToolkit.Box2D) on 2026-08-31 and
most of the list has since been built, so the document has been rewritten against the code as it
actually stands and moved here.

**Status:** of the thirteen original items, **nine are built**, one is partly built, and three were
dropped as no longer relevant. The library is not published yet — `IsPackable=false` while the API
settles.

### Where this sits among the other planning documents

| Document | Owns | Lifetime |
|---|---|---|
| **This plan** | The Box2D library's API surface: what exists, what is missing, what was deliberately dropped | Temporary — **retire into the docs once the API settles and the package ships** |
| [`notes/example-backlog.md`](../example-backlog.md) | Every example idea across the repository, including the Box2D ones | Outlives this plan |
| [`ARCHITECTURE.md`](../ARCHITECTURE.md) | API-design friction noticed while building | Ongoing |

---

## Built

| # | Item | API today |
|---|---|---|
| 1 | Contact events | `Box2DSimulation.RegisterContactEventHandler` / `Unregister`, `IContactEventHandler`, `ContactEventData`, routed by `PhysicsEventRouter2D.ProcessContacts` |
| 2 | Sensor events | `RegisterSensorEventHandler` / `Unregister`, `ISensorEventHandler`, `SensorEventData`, `ProcessSensors` |
| 3 | Body creation | `CreateDynamicBody`, `CreateKinematicBody`, `CreateStaticBody` — each with an `Entity` overload and an entity-less one, all taking an optional rotation in radians |
| 4 | Raycasting | `Box2DSimulation.Raycast` / `RaycastAll`; `PhysicsQueries2D.RaycastAll` when you hold a raw `B2WorldId` |
| 5 | Collision filtering | `Box2DCollisionMatrix.SetCollision` / `CanCollide` |
| 8 | Simulation update hook | `IBox2DSimulationUpdate` with `RegisterSimulationUpdate` / `Unregister`; `PhysicsWorld2D.Step` takes `beforeFixedStep` and `perFixedStep` |
| 10 | Body component | `Box2DBodyComponent.ApplyForce` / `ApplyImpulse` / `ApplyTorque`; `BodyForces.SetVelocity` / `GetVelocity` / `ApplyImpulse` / `ApplyImpulseAtPoint` |
| 11 | World queries | `OverlapPoint`, `OverlapAABB`, `OverlapCircle` |
| 12 | Fixtures | `ShapeFixtureBuilder.AttachShape` (toolkit 2D primitives), `AttachPolygon` (hull, ≤ 8 vertices), `CreateDefaultShapeDef`, `CreateCustomShapeDef` |
| 13 | Joints (2026-09-06) | `Joints2D.CreateRevolute` / `Prismatic` / `Wheel` / `Distance` / `Weld` / `Motor` / `Filter`, `Destroy`, `IsValid`, `GetAnchors` - world-space pivots and axes, per-type `*JointOptions` records; `Box2DSimulation.Joints` adds `Entity` overloads. See [box2d-joints.md](box2d-joints.md) |
| 14 | Explosion (2026-09-06) | `Box2DSimulation.Explode(position, radius, impulsePerLength, falloff, maskBits)` |
| 15 | Mouse grab (2026-09-06) | `Grabber2DScript` on the camera entity - kinematic anchor + motor joint, the samples' idiom (v3 has no mouse joint); in every Box2D example |
| 16 | Chains (2026-09-06) | `ShapeFixtureBuilder.AttachChain(points, body, isLoop, friction)` - adds the ghost end points itself; documents the right-hand collision side |
| 17 | Debug draw (2026-09-06) | `Box2DDebugDraw(shapeBatch)` - `b2World_Draw` through `ShapeBatch`, testbed toggles as properties, `DrawString` hook for text; in `E06_Box2D_Joints` |
| 18 | Shape casts (2026-09-06) | `PhysicsQueries2D.CastCircleClosest` / `CastSegmentClosest` / `CastShapeClosest` returning `ShapeCastHit`, and `OverlapCircle` with a `B2QueryFilter`; the same on `Box2DSimulation` |
| 19 | SVG paths (2026-09-06) | `SvgPath2D.Parse(path, offset, scale, reverse)` - the samples' parser: straight-line commands only, y flipped |
| 20 | Character mover (2026-09-06) | `CharacterMover2D` on the v3 mover API (`CollideMover` / `SolvePlanes` / `CastMover`, pogo by shape cast), `IBox2DSimulationUpdate`, per-shape `MoverShapeResponse` in shape user data via `SetResponse`; `E06_Box2D_CharacterMover`. See [box2d-character-mover.md](box2d-character-mover.md) |

Two gotchas worth keeping, both of which cost time:

- **Events are enabled per fixture, not per world.** `enableSensorEvents = true` has to be set
  explicitly on the sensor's shape def *and* on every visitor's, or nothing fires. Both bodies must
  also have entities registered with the bridge — the router drops a pair where either side is
  entity-less, which is easy to hit now that entity-less bodies exist.
- `Box2DCollisionMatrix` and `IBox2DSimulationUpdate` carry the `Box2D` prefix to avoid ND2012
  clashes with the equivalents in `Stride.BepuPhysics`. Keep the prefix on anything new that has a
  Bepu namesake.

### Built but never on the original list

Multithreading (`Box2DTaskScheduler`, `PhysicsStepSettings.WorkerCount`), entity-less body
creation, `Box2DEntityInstancing` with sleep-skipping, and a rewrite of transform sync onto
`b2World_GetBodyEvents` so only moved bodies are touched.

---

## Open

**Physics property coverage** (originally item 6, partly built). `PhysicsWorld2D.SetGravity` exists
and `PhysicsStepSettings` covers `TargetHz`, `MaxStepsPerFrame`, `SubStepCount`, `TimeScale` and
`WorkerCount`. Still missing, and currently called raw from example code, which is the signal that
they belong in the library:

- `b2World_SetMaximumLinearSpeed` — the stress pile needs it to stop bodies being ejected through
  the geometry at high impact speeds.
- `b2Body_SetSleepThreshold` — the default of `0.05` means a large connected pile never fully goes
  to sleep, so the sleep-skipping in `Box2DEntityInstancing` never gets to engage. Tuning this is an
  open experiment, not just a missing wrapper.
- Linear and angular damping.
- Body-definition flags at creation: `allowFastRotation` (the Box2D car sample sets it on its
  wheels; at 35 rad/s and 60 Hz it is not needed) and `isBullet`. `CreateDynamicBody` takes only a
  position and a rotation today.

**Transform interpolation** (originally item 9). The simulation steps at a fixed 60 Hz with up to
three steps per frame, so whenever the render rate and step rate diverge the bodies visibly step
rather than glide. Bepu has the same open question, so whatever shape this takes should probably
match on both sides.

**Joint helpers** (originally item 12) - **done 2026-09-06**, all seven types, as row 13 above;
`E06_Box2D_Joints` is the showcase. Still calling `B2Joints` directly: `E06_Box2D`'s
`ShapeSpawner` for its one distance joint - worth switching when that example is next touched.

**Performance stats** (originally item 13). No wrapper over `b2World_GetProfile` or the world
counters. Worth having — all of the multithreading tuning so far was done with a throwaway
scratchpad rig, and in-game counters are what actually found the ejected-bodies bug.

**Box2D.NET version bump.** Pinned to NuGet **3.1.654**. When a later release ships: delete
`Box2DTaskScheduler` (upstream has a built-in scheduler, where setting `workerCount` alone
activates it) and expose `B2Capacity` presizing through `PhysicsStepSettings`. The unreleased clone
measured roughly 15–25% faster on a 20k-box pile with both of those.

**Publishing.** `IsPackable=false` today. Flip it once the API stops moving.

---

## Dropped as no longer relevant

**Debug drawing via `B2DebugDraw` / `B2DrawContext`** (originally item 7) - dropped on
2026-08-31, **revived 2026-09-06** as row 17. The 2026-08-31 reasoning was that the testbed's
`solid_polygon` SDF shader, now [`Stride.CommunityToolkit.Shapes`](../../src/Stride.CommunityToolkit.Shapes),
made the wrapper unnecessary: it draws the *bodies*. It does not draw joints, contacts, forces,
bounds or islands, and those are what `b2World_Draw` knows about - so `Box2DDebugDraw` wraps it
after all, rendering through that same batch. The Box2D library now references the Shapes package.

**Exposing `EnableContactEvents` / `EnableHitEvents` / `EnableSensorEvents` as properties.**
Misconceived: Box2D enables these per fixture on the shape def, not per world, so there is no
world-level property to expose. Captured as a gotcha above instead.

**"Add comprehensive examples in `E06_Box2D2/Program.cs`."** That project never
existed. The Box2D examples that do:
[`E06_Box2D`](../../examples/code-only/E06_Box2D),
[`E10_2D_StressPile_Box2D`](../../examples/code-only/E10_2D_StressPile_Box2D),
[`E06_Box2D_Junkyard`](../../examples/code-only/E06_Box2D_Junkyard) and
[`E06_Box2D_JunkyardInteractive`](../../examples/code-only/E06_Box2D_JunkyardInteractive).

**The trailing "Status" checklist.** It declared contact events, sensor events, kinematic and static
body creation, raycasting, filtering and debug draw all unimplemented, contradicting the checkboxes
directly above it in the same document. Every line of it was wrong by the time it was read.
