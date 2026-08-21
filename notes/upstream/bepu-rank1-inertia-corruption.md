# Bepu: a rank-1 inverse inertia tensor corrupts the narrow phase for triangular-prism hulls

Draft of an upstream issue for [bepu/bepuphysics2](https://github.com/bepu/bepuphysics2).
Found while building 2D physics on top of Bepu in the Stride Community Toolkit.

**Version:** BepuPhysics 2.5.0-beta.28 (as resolved by Stride 4.4.0-beta5), .NET 10, Windows x64.

Companion to [bepu-hull-contact-nan.md](bepu-hull-contact-nan.md). Both end in a non-finite contact out of
hull collision, so they may share a root cause, but the trigger and the damage differ enough to
report separately.

**Related upstream issue:** [#109 "Consider inertia tensor locking safety"](https://github.com/bepu/bepuphysics2/issues/109),
open, filed by Ross himself: "It's not uncommon to set rows of the inertia tensor to zero to
restrict local rotation. This can make some constraints unsolvable and cause them to spew NaNs."
That is the *constraint solver* face of partial zeroing; this report is the *narrow phase* face
(non-finite contacts and memory corruption, no user constraints involved). Reference #109 when
filing - it shows the configuration is common enough that Ross considered guarding it.

## Summary

Locking a body to a plane by zeroing *some* terms of `BodyInertia.InverseInertiaTensor` - `XX` and
`YY` set to 0 while `ZZ` is kept, i.e. infinite moment of inertia about X and Y - makes the narrow
phase produce non-finite contacts and then corrupt memory, when the bodies are triangular-prism
convex hulls.

It needs **both** factors. Neither alone does anything:

| shape | full tensor | all terms zeroed | **XX,YY zeroed, ZZ kept** | XX,YY scaled by 1e-4 |
|---|---|---|---|---|
| triangular prism hull | stable | stable | **fails 3/3** | stable |
| cube hull | stable | stable | stable | stable |
| box (analytic) | stable | stable | stable | stable |
| sphere (analytic) | stable | stable | stable | stable |

8000 bodies, single-threaded, 15 s of simulated time, 3 runs per cell for the prism row and 2 for
the others. Every failing run failed; every stable run was stable.

Single-threaded matters: this is not a data race.

## It is the rank, not the magnitude

Filling in the other ranks, prism hull throughout, 2 runs each:

| `InverseInertiaTensor` | rank | result |
|---|---|---|
| untouched | 3 | stable |
| every term zeroed | 0 | stable |
| `XX` zeroed only | 2 | stable |
| **`XX` and `YY` zeroed, `ZZ` kept** | **1** | **fails** |
| `XX`, `YY` multiplied by 1e-1 … 1e-12 | 3 | stable |

The last row is the informative one. A factor of 1e-12 is physically indistinguishable from zero -
the body resists out-of-plane rotation just as absolutely - yet it is stable, while an exact zero is
not. So this is not about how much inertia the body has out of plane. It is specifically a
rank-1 tensor: rank-deficient, but not entirely zero.

Every scale from 1e-1 down to 1e-12 was stable, so there is no threshold to find; the failure needs
the exact zeros.

## The bad contact comes first

Checking body state on entry to each step, and contacts as they are generated, orders the two:

```
PrismHull  PartlyZeroed  BAD CONTACT during step 238 (t=3.967s) (4 in this step)
PrismHull  PartlyZeroed  BAD BODY STATE entering step 239 (t=3.983s) pos=<NaN, NaN, NaN> ...
```

Collision detection runs at the start of a `Timestep`, before the solver writes poses, so the
non-finite contact in step 238 precedes any non-finite body state. Every body pose, orientation and
velocity was finite going into the step that produced the bad manifold.

Since contact generation never reads inertia, the tensor cannot be corrupting the manifold directly.
It appears to be a trajectory effect: the rank-1 tensor puts prisms into configurations that
hull-vs-hull collision then mishandles. That is the same end state as the extruded-polygon issue in
the companion document, which needs no inertia trickery at all - which is why the two may be one
bug in hull collision robustness, with the tensor merely being a reliable way to reach it.

## Why anyone would write this

[bepu/bepuphysics2#2495](https://github.com/bepu/bepuphysics2/issues/2495) recommends exactly this
technique for constraining a body to a plane - an infinite moment of inertia about a specific axis.
That is what led us here, and it is why the configuration is worth making safe or rejecting loudly
rather than leaving it to corrupt memory.

The all-zeroed variant in the table is the idiom Bepu's own character controller uses, and it is
fine. It is specifically the *partly* zeroed, rank-1 tensor that breaks.

## What happens

The first symptom is a contact whose depth is not finite, produced by hull-vs-hull collision:

```
--- start PrismHull PartlyZeroed zLock=False count=8000 threads=1 spacing=1.25
PrismHull  PartlyZeroed  NON-FINITE CONTACT at t=3.97s (4 so far)
```

Shortly after, the process dies inside `NarrowPhase.UpdateConstraint`:

```
System.IndexOutOfRangeException: Index was outside the bounds of the array.
   at BepuPhysics.CollisionDetection.NarrowPhase`1.UpdateConstraint[TBodyHandles,TDescription,TContactImpulses](...)
   at BepuPhysics.CollisionDetection.ConvexTwoBodyAccessor`4.UpdateConstraintForManifold[...](...)
   at BepuPhysics.CollisionDetection.CollisionBatcher`1.ProcessConvexResult(ConvexContactManifold&, PairContinuation&)
   at BepuPhysics.CollisionDetection.CollisionBatcher`1.Add(...)
   at BepuPhysics.CollisionDetection.NarrowPhase`1.HandleOverlap(Int32, CollidableReference, CollidableReference)
   at BepuPhysics.Trees.Tree.TestLeafAgainstNode[TOverlapHandler](...)
   at BepuPhysics.Trees.Tree.GetOverlapsBetweenDifferentNodes[TOverlapHandler](...)
   at BepuPhysics.Trees.Tree.GetSelfOverlaps[TOverlapHandler](TOverlapHandler&)
   at BepuPhysics.CollisionDetection.CollidableOverlapFinder`1.DispatchOverlaps(Single, IThreadDispatcher)
   at BepuPhysics.Simulation.CollisionDetection(Single, IThreadDispatcher)
   at BepuPhysics.Simulation.Timestep(Single, IThreadDispatcher)
```

Multithreaded, the same configuration usually dies as an `AccessViolationException` in
`NarrowPhase.ExecutePreflushJob` -> `PendingConstraintAddCache.FlushWithSpeculativeBatches` ->
`Contact3.ApplyDescription` instead. Under other counts we have also seen `Stack overflow`, an
`Internal CLR error (0x80131506)`, and silent process death. Those all look like the same corruption
surfacing wherever it happens to be noticed, so the single-threaded `IndexOutOfRangeException` above
is the most useful form.

## Repro

Full harness: `PurePrismRunaway.cs` in `examples/code-only/_Temp2DProbe`, driven by `PRISM=1`. The parts that matter:

```csharp
// The prism: base width 1 across X, height 1 across Y, depth 1 along Z, centred on the origin.
pool.Take<Vector3>(6, out var points);

points[0] = new Vector3(-0.5f, -0.5f,  0.5f);
points[1] = new Vector3( 0f,    0.5f,  0.5f);
points[2] = new Vector3( 0.5f, -0.5f,  0.5f);
points[3] = new Vector3(-0.5f, -0.5f, -0.5f);
points[4] = new Vector3( 0f,    0.5f, -0.5f);
points[5] = new Vector3( 0.5f, -0.5f, -0.5f);

ConvexHullHelper.CreateShape(points.Slice(0, 6), pool, out _, out var hull);

var shape = simulation.Shapes.Add(hull);
var inertia = hull.ComputeInertia(1f);

// The lock. Comment this block out and the same run is stable.
ref var t = ref inertia.InverseInertiaTensor;
t.XX = 0f;
t.YY = 0f;
t.YX = 0f;
t.ZX = 0f;
t.ZY = 0f;   // ZZ deliberately left alone, so the body can still roll in the plane

// A static floor, then 8000 dynamic prisms on a grid in the XY plane at z = 0, spaced 1.25 apart
// so nothing overlaps at spawn. Step at 1/60 for 15 s with a single-threaded dispatcher.
```

Environment variables on the harness: `PRISMSHAPE=PrismHull|CubeHull|Box|Sphere`,
`MODE=None|AllZeroed|OneAxisZeroed|PartlyZeroed|Scaled`, `SCALE`, `PRISMCOUNT`, `THREADS`,
`SPACING`, `PRISMSECONDS`.

Note on spawn spacing: with bodies spawned already interpenetrating (`SPACING` below the shape
size), *every* configuration produces non-finite contacts, including an untouched inertia tensor.
That is a separate and much less interesting effect, and it is why the numbers above use a spacing
wider than the shape.

## What we did about it

Scaling instead of zeroing - `XX *= 1e-4f`, `YY *= 1e-4f`, keeping the tensor full rank - behaves
identically for our purposes and is stable. Verified in the tables above and, before that, over 20
runs at 8000 and 20000 bodies in the engine, where the partly-zeroed version failed 3/3 and reached
13 GB of resident memory.

We are not asking for that to be adopted; it is a workaround. The report is that a rank-1 inverse
inertia tensor - which the project's own issue tracker recommends as the way to constrain a body to
a plane - can corrupt the narrow phase rather than being rejected or handled.

If the right resolution is that a rank-1 tensor is simply not supported, a validation throw would be
worth far more than silence: the current failure surfaces as an `AccessViolationException` in an
unrelated part of the engine, minutes into a run, and took a long investigation to attribute.
