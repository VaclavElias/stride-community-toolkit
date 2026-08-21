# Bepu: hull-vs-hull contact generation returns `depth = NaN` for overlapping extruded polygons

Draft of an upstream issue for [bepu/bepuphysics2](https://github.com/bepu/bepuphysics2).
Found while investigating a freeze in the Stride Community Toolkit's 2D polygon example.
A paste-ready title and body distilled from this file:
[bepu-hull-contact-nan-issue.md](bepu-hull-contact-nan-issue.md).

**Version:** BepuPhysics 2.5.0-beta.28 (as resolved by Stride 4.4.0-beta5), .NET 10, Windows x64.
Also reproduced unchanged (fixed-offset table and the incidence sweep both) against bepuphysics2
**master at `16ecf9cf`** (20 Aug 2026) via project reference, in a standalone project at
`D:\Projects\GitHub\bepuphysics2\BepuHullContact`.

**Tracker check (20 Aug 2026):** no existing issue covers this. Searches for hull/NaN/contact turn
up #311 (user error, zero-length quaternion), #81 (2018, no repro), #262 (tree-cost assert from a
user's own bad state) and #325 (`ConvexHullHelper.CreateShape` input handling, not contact
generation). This would be a new report.

Companion to [bepu-rank1-inertia-corruption.md](bepu-rank1-inertia-corruption.md). Both end in a
non-finite contact out of hull collision and may share a root cause; the triggers differ.

## Summary

Two identical `ConvexHull` shapes - a regular polygon extruded along Z - overlapping with an offset
in **both** X and Y produce a contact manifold whose `depth` is `NaN`, on the very first
`Timestep`. The contact normal and offsets are finite and correct; only the depth is `NaN`.

The `NaN` propagates to the body poses in the same step. From then on the broad phase tree holds
`NaN` bounds, and the next `BroadPhase.Add` never returns - `Tree.Add` spins indefinitely on one
core. That secondary hang is what a user sees: the application freezes rather than crashes.

Whether a given polygon triggers it is extremely sensitive to the exact vertex coordinates - see
[Sensitivity](#sensitivity) below - so this reads as a numerical robustness problem in the depth
refinement rather than anything specific to hexagons.

The fixed offset below is the minimal repro, but it understates the bug: a Monte Carlo sweep over
random placements - see [Incidence](#incidence) - fails for **every** side count from 3 up, at 15-40%
of random overlapping poses once the bodies carry a rotation about Z. Triangles and squares built as
hulls fail at the same rates as hexagons. An analytic `Box` given the identical placements never
fails, so the hull-vs-hull pair handler owns this outright.

## Repro

Two forms, strongest first.

**As a `DemoTests` xunit test** - `DemoTests/HullPairContactTests.cs` in the local clone, written in
the style of `PairDeterminismTests`: it drives `CollisionBatcher` directly, so there is no
`Simulation`, no gravity, no inertia, no solver and no integration - just `batcher.Add` +
`batcher.Flush` and an assertion that every contact depth in the returned manifold is finite. Three
facts, 47 ms total:

- `ExtrudedHexagonPairAtKnownOffsetHasFiniteContactDepths` - the minimal deterministic case;
  **fails** with `Count=2, normal=<-0.9999121, -0.013258234, -0>, depth0=NaN`.
- `ExtrudedPolygonPairsAtRandomOverlapsHaveFiniteContactDepths` - the incidence sweep; **fails**
  with 16-44% of placements per side count, every count from 3 up.
- `BoxPairsAtTheSameRandomOverlapsHaveFiniteContactDepths` - the analytic control on the identical
  placements; **passes**.

The test fails identically in Debug and Release. Debug is notable: no `CHECKMATH` validation fires
on this path - `ProcessConvexResult` only validates the manifold *normal* - so the tester emits the
NaN depth unvalidated, and the first internal checkpoint that catches it is downstream in the
simulation's constraint creation (the assert in the section below). Being expected-to-fail, the
test is issue material or fix-PR material, not a standalone PR.

**As a demo in the harness** - `Demos/Demos/HullContactNaNDemo.cs`, registered at the end of
`DemoSet`, using the standard demo bootstrap (`DemoNarrowPhaseCallbacks`,
`DemoPoseIntegratorCallbacks`, `SolveDescription(8, 1)`). Three pairs of extruded polygon hulls
spawn overlapping at the identical relative pose, differing only in side count: 5, 6, 7. In
Release the harness keeps running and the demo's UI text reports `sides=6: POSES ARE NaN` in red
while 5 and 7 settle normally beside it; in Debug, `CHECKMATH` fires the `Invalid value` assert on
the demo's very first timestep - verified: identical stack to the standalone run, now reached
through the maintainers' own harness, so F5 under a debugger breaks with the failing manifold in
view. Same expected-to-fail caveat as the test.

**As a standalone program** - the same thing shrunk to one file plus a `PackageReference`: one
narrow phase call through `CollisionBatcher`, no `Simulation`. This is the form to inline in the
issue body. Extend the `sides` list to reproduce the sensitivity results further down; the sweep
lives in the test branch.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BepuPhysics" Version="2.5.0-beta.28" />
  </ItemGroup>
</Project>
```

```csharp
// Two identical convex hulls (a regular polygon extruded along Z), one narrow phase call, no
// Simulation. At this offset, sides=6 produces a manifold whose depths are NaN under a finite
// normal, with 2 contacts on the same z-cap where the working cases have 4; sides=5 and 7 are
// clean.
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities.Memory;
using System.Numerics;

const float Radius = 0.5f;                     // circumradius of the polygon
const float Depth = 1f;                        // extrusion along Z
var offset = new Vector3(0.3445f, 0.076f, 0);  // pose B relative to pose A; with Y = 0 every side count is clean

var pool = new BufferPool();
var registry = DefaultTypes.CreateDefaultCollisionTaskRegistry();

foreach (var sides in (int[])[5, 6, 7])
{
    var shapes = new Shapes(pool, 8);

    pool.Take<Vector3>(sides * 2, out var points);

    for (var i = 0; i < sides; i++)
    {
        var angle = i * (MathF.Tau / sides) - MathF.PI / 2;
        var x = MathF.Cos(angle) * Radius;
        var y = MathF.Sin(angle) * Radius;

        points[i] = new Vector3(x, y, Depth / 2);
        points[i + sides] = new Vector3(x, y, -Depth / 2);
    }

    ConvexHullHelper.CreateShape(points.Slice(0, sides * 2), pool, out _, out var hull);
    pool.Return(ref points);

    var shape = shapes.Add(hull);

    Console.WriteLine($"sides={sides}:");

    var batcher = new CollisionBatcher<PrintCallbacks>(pool, shapes, registry, 1 / 60f, default);
    batcher.Add(shape, shape, offset, Quaternion.Identity, Quaternion.Identity, 0.1f, new PairContinuation(0));
    batcher.Flush();

    shapes.Dispose();
}

struct PrintCallbacks : ICollisionCallbacks
{
    public bool AllowCollisionTesting(int pairId, int childA, int childB) => true;

    public void OnChildPairCompleted(int pairId, int childA, int childB, ref ConvexContactManifold manifold) { }

    public void OnPairCompleted<TManifold>(int pairId, ref TManifold manifold) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        for (var i = 0; i < manifold.Count; i++)
        {
            manifold.GetContact(i, out _, out var normal, out var depth, out _);
            Console.WriteLine($"  contact {i}/{manifold.Count} depth={depth} normal={normal}");
        }
    }
}
```

Output, verified against the 2.5.0-beta.28 package and against master `16ecf9cf` via project
reference, identical in Debug and Release:

```
sides=5:
  contact 0/4 depth=0.5533842 normal=<-0.9510566, -0.30901703, 0>
  contact 1/4 depth=0.36468905 normal=<-0.9510566, -0.30901703, 0>
  contact 2/4 depth=0.36468905 normal=<-0.9510566, -0.30901703, 0>
  contact 3/4 depth=0.5533842 normal=<-0.9510566, -0.30901703, 0>
sides=6:
  contact 0/2 depth=NaN normal=<-0.9999121, -0.013258234, -0>
  contact 1/2 depth=NaN normal=<-0.9999121, -0.013258234, -0>
sides=7:
  contact 0/4 depth=0.5977102 normal=<-0.97492796, -0.22252081, -0>
  contact 1/4 depth=0.49200162 normal=<-0.97492796, -0.22252081, -0>
  contact 2/4 depth=0.5977101 normal=<-0.97492796, -0.22252081, -0>
  contact 3/4 depth=0.4920017 normal=<-0.97492796, -0.22252081, -0>
```

## What the narrow phase produces

Same shape family, same overlap magnitude, only the side count differs - the output above shows it.
The same hexagon with the Y offset removed, for comparison, is fine:

```
sides=6, offsetY=0   4 contacts, depth 0.5215254, normal (-1, 0, 0)
```

Two things stand out in the failing case: the depth is `NaN`, and the manifold has only 2 contacts
where every working case has 4. Both surviving contacts sit on the same `z = +0.5` cap, so the
manifold has also lost the opposite face.

### Bepu's own debug validation catches it at the source

Running the same failing pair through a full `Simulation.Timestep` against the bepuphysics2
*source* in Debug - where `BepuPhysics.csproj` defines `CHECKMATH`, turning `MathChecker.Validate`
into a hard `Debug.Fail` - the process terminates at sides=6 the moment the manifold is delivered,
before the solver or pose integration ever see it. (The bare `CollisionBatcher` repro above does
not trip this even in Debug, since the manifold never reaches constraint creation - which is where
the first depth check lives.)

```
Process terminated.
Assertion failed.
Invalid value.
   at BepuUtilities.MathChecker.Validate(Single f) in BepuUtilities\MathChecker.cs:line 33
   at BepuPhysics.CollisionDetection.CollisionBatcher`1.ProcessConvexResult(ConvexContactManifold& manifold, PairContinuation& continuation) in BepuPhysics\CollisionDetection\CollisionBatcher.cs:line 345
   at BepuPhysics.CollisionDetection.CollidableOverlapFinder`1.DispatchOverlaps(Single dt, IThreadDispatcher threadDispatcher) in ...
   at BepuPhysics.Simulation.CollisionDetection(Single dt, IThreadDispatcher threadDispatcher) in ...
   at BepuPhysics.Simulation.Timestep(Single dt, IThreadDispatcher threadDispatcher) in ...
```

The top frame is the single-`float` overload, inlined from the manifold delivery chain inside
`ProcessConvexResult` (line 339's explicit check is on the *normal*, which is finite in this
manifold). So Bepu's own validation agrees the value is already invalid in the hull-vs-hull
tester's output, upstream of everything else - it is not the solver, speculative contacts, or
integration manufacturing the `NaN` later. It also means anyone running the source in Debug gets an
instant, breakpointable repro rather than the silent Release-mode pose corruption.

Practical note for running the repro: use Release to see the full table and the incidence sweep;
in Debug the first failing side count terminates the process at the assert.

## Root cause

Pinned on 21 Aug 2026 by temporarily instrumenting `ConvexHullPairTester` on the `sides=6` case
(instrumentation reverted afterwards; the `bepuphysics2` tree is clean). The chain, in order:

1. `DepthRefiner.FindMinimumDepth` **succeeds**. It returns `depth=0.5271011` and
   `localNormal=<-0.9999121, -0.013258234, -0>`, both finite and correct. The iterative normal
   search is not the problem, which is worth stating because it is the part that looks most
   suspicious from outside.
2. `ConvexHullTestHelper.PickRepresentativeFace` then picks the **wrong face for hull A**: face 4,
   the `+Z` extrusion cap, normal `<0, 0, 1>`. The correct choice is face 6, normal `<1, 0, 0>`,
   whose alignment dot with the contact normal is 0.9999121 against the cap's 0.

   It happens because the helper primarily minimises *plane error*, and only falls back to normal
   alignment when two faces are within `boundingPlaneEpsilon` of each other. With
   `closestOnAInA = <0.4385421, 0.04149428, 0.5>` and `boundingPlaneEpsilon = 0.00033333336`, the
   cap's plane error is exactly 0 (the Z is exactly on the cap plane) while face 6's is 0.005529 -
   about 17x the epsilon. So the first branch of `useCandidate` fires, the cap is taken on plane
   error alone, and alignment is never consulted.
3. `ConvexHullPairTester` passes `1f / Vector3.Dot(slotFaceNormalA, slotLocalNormal)` into
   `ManifoldCandidateHelper.Reduce`. That dot is **exactly** zero - an in-plane contact normal
   against a `(0, 0, 1)` cap normal - so the argument is `+Inf`.
4. In `Reduce`, `dotAxis = faceNormalA * inverseFaceNormalADotLocalNormal` evaluates to
   `<NaN, NaN, Inf>` (`0 * Inf`), so `baseDot`, `xDot` and `yDot` are all NaN and every
   `candidateDepth = baseDot + candidate.X * xDot + candidate.Y * yDot` is NaN.
5. The manifold normal is written separately at the end of the tester and never touches any of
   this - hence the signature symptom, a finite normal over NaN depths. The 2-contacts-on-one-cap
   symptom is the same root: the clip ran against the cap's 6-vertex face rather than the side face.

Why extruded 2D shapes hit it so much harder than 3D hulls:

- A coplanar pair's contact normal is exactly in-plane and a cap normal is exactly `(0, 0, ±1)`, so
  the dot is exactly zero, not merely small. There is no degraded-but-finite regime to land in.
- `closestOnA` is reconstructed as `closestOnB - localNormal * depth`, so its float error is
  concentrated along the contact normal, i.e. in-plane, while its Z stays exact. The cap therefore
  wins the plane-error comparison by construction whenever the closest point sits on a cap boundary.
  That is a systematic bias, which is the better explanation for the 15-40% incidence than bad luck.

Two candidate fixes, for whenever Ross picks one up: guard the reciprocal (stops the NaN, still
produces a bad manifold), or fix the weighting so a face perpendicular to the contact normal can
never be selected as the representative regardless of plane error. The second looks like the real
one.

Files: `BepuPhysics/CollisionDetection/CollisionTasks/ConvexHullPairTester.cs:73-77,195,219`,
`ConvexHullTestHelper.cs:38-51`, `ManifoldCandidateHelper.cs:125,331-337`.

## Sensitivity

The set of failing side counts moves if the vertex coordinates change in their last bits. Computing
the vertex angle as `i * (Tau / sides)` (as above) gives failures at **6 and 32**. Computing the
mathematically identical `(i * Tau) / sides` instead gives failures at **6, 10 and 32** - sides=10
flips from clean to `NaN` on that one change alone.

So the specific side counts are not the interesting part; a nearly-degenerate face configuration is.
Reproduced consistently across Debug and Release, and across repeated runs.

## Incidence

The fixed offset above samples one point of an extremely coordinate-sensitive failure surface. To
measure how big the surface actually is: 200 random placements per side count, offsets uniform in
±(one circumradius) on both axes, 8 steps of 1/60 s each, single-threaded, fixed seed. Run twice -
once with both bodies axis-aligned (what freshly spawned bodies are), once with each given a random
rotation about Z (what bodies that have fallen and tumbled in a 2D scene have). The failure counted
is any body pose going non-finite.

Circumradius 1, extrusion depth 4 (a shape from the game example this was found in):

| sides | axis-aligned | rotated about Z |
|------:|-------------:|----------------:|
| 3     | 0 / 200      | 35 / 200 (17.5%) |
| 4     | 0 / 200      | 34 / 200 (17.0%) |
| 5     | 79 / 200 (39.5%) | 81 / 200 (40.5%) |
| 6     | 0 / 200      | 31 / 200 (15.5%) |
| 7     | 74 / 200 (37.0%) | 48 / 200 (24.0%) |
| 8     | 87 / 200 (43.5%) | 33 / 200 (16.5%) |
| 10    | 0 / 200      | 45 / 200 (22.5%) |

The original geometry (circumradius 0.5, depth 1), rotated: sides 3 through 10 fail at 15-24%,
except the hexagon at **80.5%** (161/200).

Three observations from the sweep:

- **Every failure, in all configurations, happens on the very first step.** Not one trial survived
  step 0 and diverged later, so this is never a gradual energy blow-up - it is always the first
  manifold poisoning the poses.
- **The analytic control never fails.** The identical 200 placements (same seed, same offsets, same
  rotations) with an analytic `Box` in place of the hull: 0 failures, axis-aligned and rotated both.
- **Shallow overlaps fail too.** Restricting placements to a center distance of [1.6, 1.95] for
  circumradius 1 shapes - where the hulls only graze - still fails 3-14% of poses for every side
  count except the triangle, the shallowest at a distance of 1.846 for the decagon. Its faces touch
  at 1.902, so that is an overlap of about 0.05: shallower than the 0.1 speculative margin, and
  inside the penetration an ordinary resting stack settles at. This is what rules out "the bodies
  were spawned inside each other, which is unsupported" as an explanation.
- **No side count is safe.** The axis-aligned column is what made the bug look shape-specific -
  which side counts fail there genuinely does vary with radius and coordinates - but a rotation
  about Z, which any body in a physics scene picks up within seconds, exposes every count including
  triangles and squares.

A failing rotated square, replayed with manifold logging - the same signature as the hexagon above,
NaN depth under a finite normal, and only the 2 contacts on one z-cap where a working manifold has 4:

```
sides=4 radius=1 depth=4 offset=(-0.502663,-0.778512) rotZ=(2.934315,4.848132)
  contact 0/2 depth=NaN normal=(0.6227854, 0.7823928, 0) offset=(0.4881382, -0.6431858, 2)
  contact 1/2 depth=NaN normal=(0.6227854, 0.7823928, 0) offset=(-0.63798887, 0.21228904, 2)
```

And a failing rotated triangle:

```
sides=3 radius=1 depth=4 offset=(0.315038,-0.134435) rotZ=(2.224774,5.930462)
  contact 0/2 depth=NaN normal=(-0.9218825, 0.38746938, 0) offset=(-0.030417204, -1.0728703, 2)
  contact 1/2 depth=NaN normal=(-0.9218825, 0.38746938, 0) offset=(-0.3249439, 0.6339555, 2)
```

The sweep is the repro program above with the fixed offset replaced by seeded random offsets and
rotations per trial, one fresh `Simulation` per trial.

## Scope

Swept with the offset fixed at `(0.3445, 0.076, 0)`, 240 steps, both as a bare `ConvexHull` and
wrapped in a single-child `Compound` - identical results either way.

With `offsetY = 0` every side count is clean, including 6 and 32, so an offset along both axes is
part of the trigger. Which side counts fail at a *fixed* offset also moves with the circumradius -
at 0.3, 0.4, 0.6, 0.7 and 1.0 the hexagon is clean at the offsets tested - but the incidence sweep
above shows that is sampling noise, not safety: at circumradius 1 the axis-aligned hexagon is clean
across 200 random offsets while 5, 7 and 8 sides fail at ~40%, and with rotation every count fails.

How it surfaces in practice, for severity: in the engine, spawning these polygon hulls back to back
at roughly the same point kills the application within a handful of bodies. Observed with hexagons
(freeze via the `Tree.Add` hang below), and later with 5- and 7-sided polygons at circumradius 1,
matching the axis-aligned column of the sweep - including the user-facing pattern that 3- and
4-sided spawns seemed safe while 5+ crashed, which is exactly that column. Spawning the same hexagon
interleaved with six other shapes - about 57 hexagons among 400 bodies falling into one column - was
clean over two runs. So the practical trigger is two of these hulls meeting while overlapped, which
is common when bodies are spawned on top of each other and rare otherwise; but the rotated column
says any long-running scene that lets hull bodies interpenetrate (a deep pile, an explosion, fast
bodies tunnelling into each other) is exposed regardless of side count.

Not involved, each ruled out by measurement:

- **The hull itself.** `ComputeBounds` gives `(-0.4330, -0.5, -0.5)`..`(0.4330, 0.5, 0.5)`, 8 faces,
  and `ComputeInertia(1)` gives a finite, well-conditioned tensor (`XX = YY = 7.3846`, `ZZ = 9.6`).
- **`Compound` wrapping.** A bare `ConvexHull` reproduces it identically.
- **Timestep size.** A first step of 0.1 s, 0.2 s or 0.5 s changes nothing either way.
- **Substepping and iteration count.** Reproduces at `SolveDescription(8, 1)`.

## The secondary hang

Worth fixing independently of the contact bug, because it turns a recoverable `NaN` into a hard
freeze with no diagnostic. Once a body pose is `NaN`, the broad phase stores `NaN` bounds, and the
next insertion never terminates:

```
BepuPhysics.Trees.Tree.Add(BoundingBox, BufferPool)
BepuPhysics.Trees.Tree.Add(BoundingBox, BufferPool)
BepuPhysics.CollisionDetection.BroadPhase.Add(...)
BepuPhysics.CollisionDetection.BroadPhase.AddActive(...)
BepuPhysics.Bodies.AddCollidableToBroadPhase(...)
BepuPhysics.Bodies.Add(BodyDescription&)
```

Sampled twice, 100 seconds of CPU apart, with about ten bodies in the tree - so this is a loop, not
slow work.
