# Plan: Stride.CommunityToolkit.Box2D library

This began as `Example18_Box2DPhysics/IMPROVEMENTS.md`, a wish list for `Box2DSimulation.cs` back
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

**Transform interpolation** (originally item 9). The simulation steps at a fixed 60 Hz with up to
three steps per frame, so whenever the render rate and step rate diverge the bodies visibly step
rather than glide. Bepu has the same open question, so whatever shape this takes should probably
match on both sides.

**Joint helpers** (originally item 12). Nothing in the library; `Example18`'s `SceneManager` calls
`B2Joints` directly. Distance, revolute and motor joints would be the obvious three, and they would
give the Bepu constraint examples (`Example15_Constraint_*`) natural Box2D twins.

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

**Debug drawing via `B2DebugDraw` / `B2DrawContext`** (originally item 7). Not the route taken.
Rather than wrapping Box2D's own debug draw, the toolkit ports the testbed's `solid_polygon` SDF
shader, which gives an outline that stays a constant few *pixels* wide at any zoom — something mesh
geometry structurally cannot do. It outgrew Box2D entirely and was promoted into the core toolkit on
2026-08-31 as `ShapeBatch` in
[`src/Stride.CommunityToolkit/Rendering/Shapes`](../../src/Stride.CommunityToolkit/Rendering/Shapes),
so it is no longer a Box2D concern at all. See
[`Example_Shapes_Playground`](../../examples/code-only/Example_Shapes_Playground).

**Exposing `EnableContactEvents` / `EnableHitEvents` / `EnableSensorEvents` as properties.**
Misconceived: Box2D enables these per fixture on the shape def, not per world, so there is no
world-level property to expose. Captured as a gotcha above instead.

**"Add comprehensive examples in `Example18_Box2DPhysics2/Program.cs`."** That project never
existed. The Box2D examples that do:
[`Example18_Box2DPhysics`](../../examples/code-only/Example18_Box2DPhysics),
[`Example01_Basic2DScene_StressPile_Box2D`](../../examples/code-only/Example01_Basic2DScene_StressPile_Box2D),
[`Example02_Junkyard_Box2D`](../../examples/code-only/Example02_Junkyard_Box2D) and
[`Example02_Junkyard_Playground_Box2D`](../../examples/code-only/Example02_Junkyard_Playground_Box2D).

**The trailing "Status" checklist.** It declared contact events, sensor events, kinematic and static
body creation, raycasting, filtering and debug draw all unimplemented, contradicting the checkboxes
directly above it in the same document. Every line of it was wrong by the time it was read.
