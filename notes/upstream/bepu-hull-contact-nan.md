# Bepu: hull-vs-hull contact generation returns `depth = NaN` for some extruded polygons

Draft of an upstream issue for [bepu/bepuphysics2](https://github.com/bepu/bepuphysics2).
Found while investigating a freeze in the Stride Community Toolkit's 2D polygon example.

**Version:** BepuPhysics 2.5.0-beta.28 (as resolved by Stride 4.4.0-beta5), .NET 10, Windows x64.

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

## Repro

Self-contained: one file plus a `PackageReference`, no engine involved. Both bodies use the same
shape and the same inertia. `sides` is the loop variable, so one run shows which counts fail.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BepuPhysics" Version="2.5.0-beta.28" />
  </ItemGroup>
</Project>
```

```csharp
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

const float Radius = 0.5f;      // circumradius of the polygon
const float Depth = 1f;         // extrusion along Z
const float OffsetX = 0.3445f;  // second body's offset from the first
const float OffsetY = 0.076f;   // set this to 0 and every side count below is clean

foreach (var sides in (int[])[3, 4, 5, 6, 7, 8, 10, 16, 32])
{
    Run(sides);
}

static void Run(int sides)
{
    var pool = new BufferPool();
    using var dispatcher = new ThreadDispatcher(1);

    var simulation = Simulation.Create(pool, new Callbacks(), new PoseCallbacks(), new SolveDescription(8, 1));

    // The polygon, extruded: `sides` points at z = +Depth/2 and the same `sides` at -Depth/2.
    pool.Take<Vector3>(sides * 2, out var points);

    var angleStep = MathF.Tau / sides;

    for (var i = 0; i < sides; i++)
    {
        var angle = i * angleStep - MathF.PI / 2;
        var x = MathF.Cos(angle) * Radius;
        var y = MathF.Sin(angle) * Radius;

        points[i] = new Vector3(x, y, Depth / 2);
        points[i + sides] = new Vector3(x, y, -Depth / 2);
    }

    ConvexHullHelper.CreateShape(points.Slice(0, sides * 2), pool, out _, out var hull);
    pool.Return(ref points);

    var index = simulation.Shapes.Add(hull);
    var inertia = hull.ComputeInertia(1f);

    var a = simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(0, 0, 0), inertia, index, 0.01f));
    var b = simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(OffsetX, OffsetY, 0), inertia, index, 0.01f));

    Callbacks.LogContacts = true;
    simulation.Timestep(1 / 60f, dispatcher);
    Callbacks.LogContacts = false;

    var pa = simulation.Bodies[a].Pose.Position;
    var pb = simulation.Bodies[b].Pose.Position;
    var finite = float.IsFinite(pa.X) && float.IsFinite(pa.Y) && float.IsFinite(pa.Z)
              && float.IsFinite(pb.X) && float.IsFinite(pb.Y) && float.IsFinite(pb.Z);

    Console.WriteLine($"sides={sides,-3} {(finite ? "ok" : "POSE IS NaN AFTER ONE STEP")}");

    simulation.Dispose();
    pool.Clear();
}

struct Callbacks : INarrowPhaseCallbacks
{
    public static bool LogContacts;

    public void Initialize(Simulation simulation) { }

    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) => true;

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties material)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        material = new PairMaterialProperties(1f, 2f, new SpringSettings(30, 1));

        if (LogContacts)
        {
            for (var i = 0; i < manifold.Count; i++)
            {
                manifold.GetContact(i, out var offset, out var normal, out var depth, out _);

                Console.WriteLine($"    contact {i}/{manifold.Count} depth={depth} normal=({normal.X}, {normal.Y}, {normal.Z})");
            }
        }

        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

    public void Dispose() { }
}

struct PoseCallbacks : IPoseIntegratorCallbacks
{
    private Vector3Wide _gravityDt;

    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    public readonly bool AllowSubstepsForUnconstrainedBodies => false;

    public readonly bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation) { }

    public void PrepareForIntegration(float dt) => _gravityDt = Vector3Wide.Broadcast(new Vector3(0, -10, 0) * dt);

    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        => velocity.Linear += _gravityDt;
}
```

Output, identical in Debug and Release:

```
sides=3   ok
sides=4   ok
sides=5   ok
sides=6   POSE IS NaN AFTER ONE STEP
sides=7   ok
sides=8   ok
sides=10  ok
sides=16  ok
sides=32  POSE IS NaN AFTER ONE STEP
```

## What the narrow phase produces

From the `ConfigureContactManifold` logging above. Same shape family, same overlap magnitude, only
the side count differs:

```
sides=5   4 contacts, depth 0.5533842, normal (-0.9510566, -0.30901703, 0)
sides=6   2 contacts, depth NaN,       normal (-0.9999121, -0.013258234, 0)
sides=7   4 contacts, depth 0.5977102, normal (-0.97492796, -0.22252081, 0)
```

And the same hexagon with the Y offset removed, which is fine:

```
sides=6, offsetY=0   4 contacts, depth 0.5215254, normal (-1, 0, 0)
```

Two things stand out in the failing case: the depth is `NaN`, and the manifold has only 2 contacts
where every working case has 4. Both surviving contacts sit on the same `z = +0.5` cap, so the
manifold has also lost the opposite face.

## Sensitivity

The set of failing side counts moves if the vertex coordinates change in their last bits. Computing
the vertex angle as `i * (Tau / sides)` (as above) gives failures at **6 and 32**. Computing the
mathematically identical `(i * Tau) / sides` instead gives failures at **6, 10 and 32** - sides=10
flips from clean to `NaN` on that one change alone.

So the specific side counts are not the interesting part; a nearly-degenerate face configuration is.
Reproduced consistently across Debug and Release, and across repeated runs.

## Scope

Swept with the offset fixed at `(0.3445, 0.076, 0)`, 240 steps, both as a bare `ConvexHull` and
wrapped in a single-child `Compound` - identical results either way.

With `offsetY = 0` every side count is clean, including 6 and 32, so an offset along both axes is
part of the trigger. Circumradius matters too: at 0.3, 0.4, 0.6, 0.7 and 1.0 the hexagon is clean at
the offsets tested.

How it surfaces in practice, for severity: in the engine, spawning hexagons back to back at roughly
the same point freezes the application within a handful of bodies, every time. Spawning the same
hexagon interleaved with six other shapes - about 57 hexagons among 400 bodies falling into one
column - was clean over two runs. So the trigger needs two of these hulls meeting at a particular
relative pose, which is common when bodies are spawned on top of each other and rare otherwise.

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
