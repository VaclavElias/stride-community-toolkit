// TEMPORARY - a Stride-free reproduction, so the finding can be reported against Bepu alone.
// Two identical convex hulls (a regular polygon extruded along Z) placed almost on top of each
// other, stepped a few times. Nothing here touches Stride, the toolkit, or Body2DComponent.
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

namespace Temp2DProbe;

internal static class PureBepuRepro
{
    /// <summary>Prints every contact the narrow phase produces, so a NaN can be traced to it.</summary>
    public static bool LogManifolds
    {
        get => Callbacks.LogManifoldsFlag;
        set => Callbacks.LogManifoldsFlag = value;
    }

    /// <summary>
    /// Monte Carlo version of <see cref="Run"/>: many random overlapping placements of the same
    /// hull pair, counting how many go non-finite. A single fixed offset only ever samples one
    /// point of an extremely coordinate-sensitive failure surface; a rate per side count is what
    /// says whether a shape is safe to spawn on top of itself.
    /// </summary>
    /// <returns>Per-trial failure step: -1 for a clean trial, otherwise the first step whose pose
    /// was non-finite. Step 0 means the very first contact produced the NaN, which is the
    /// signature of the depth bug rather than of a gradual energy blow-up.</returns>
    public static int[] Sweep(int sides, float radius, float depth, int trials, int steps, bool rotate, int seed)
    {
        var random = new Random(seed);
        var failedAtStep = new int[trials];

        for (var trial = 0; trial < trials; trial++)
        {
            // Overlaps up to one circumradius in each axis, the situation repeated spawning at the
            // same point produces. Rotation about Z is what bodies falling in the plane pick up.
            var offsetX = (random.NextSingle() * 2f - 1f) * radius;
            var offsetY = (random.NextSingle() * 2f - 1f) * radius;
            var rotationA = rotate ? random.NextSingle() * MathF.Tau : 0f;
            var rotationB = rotate ? random.NextSingle() * MathF.Tau : 0f;

            failedAtStep[trial] = RunOnce(sides, radius, depth, offsetX, offsetY, rotationA, rotationB, steps);

            // The first failing placement, replayed with manifold logging on, is the receipt that
            // the failure is a NaN depth out of the narrow phase and not a blow-up.
            if (failedAtStep[trial] == 0 && LogFirstFailure)
            {
                LogFirstFailure = false;
                LogManifolds = true;

                Console.Error.WriteLine(
                    $"[sweep] first failing placement: sides={sides} radius={radius} depth={depth} " +
                    $"offset=({offsetX:F6},{offsetY:F6}) rotZ=({rotationA:F6},{rotationB:F6})");

                RunOnce(sides, radius, depth, offsetX, offsetY, rotationA, rotationB, 1);

                LogManifolds = false;
            }
        }

        return failedAtStep;
    }

    /// <summary>When set, the first failing placement the next sweep finds is replayed once with
    /// manifold logging enabled, then the flag clears itself.</summary>
    public static bool LogFirstFailure;

    /// <summary>
    /// The control for <see cref="Sweep"/>: the same random placements, but with an analytic
    /// <see cref="Box"/> instead of a hull. Separates "deep overlap breaks contact generation in
    /// general" from "the hull path specifically does".
    /// </summary>
    /// <returns>Per-trial failure step, as in <see cref="Sweep"/>.</returns>
    public static int[] SweepBoxControl(float radius, float depth, int trials, int steps, bool rotate, int seed)
    {
        var random = new Random(seed);
        var failedAtStep = new int[trials];

        for (var trial = 0; trial < trials; trial++)
        {
            var offsetX = (random.NextSingle() * 2f - 1f) * radius;
            var offsetY = (random.NextSingle() * 2f - 1f) * radius;
            var rotationA = rotate ? random.NextSingle() * MathF.Tau : 0f;
            var rotationB = rotate ? random.NextSingle() * MathF.Tau : 0f;

            failedAtStep[trial] = RunOnce(0, radius, depth, offsetX, offsetY, rotationA, rotationB, steps);
        }

        return failedAtStep;
    }

    /// <summary>One silent trial. Returns -1 if every pose stayed finite, otherwise the first
    /// step at which one went non-finite. <paramref name="sides"/> = 0 uses an analytic
    /// <see cref="Box"/> the size of the square the hull path would build, as a control.</summary>
    private static int RunOnce(int sides, float radius, float depth, float offsetX, float offsetY, float rotationA, float rotationB, int steps)
    {
        var pool = new BufferPool();
        using var dispatcher = new ThreadDispatcher(1);

        var simulation = Simulation.Create(
            pool,
            new Callbacks(),
            new PoseCallbacks(),
            new SolveDescription(8, 1));

        TypedIndex index;
        BodyInertia inertia;

        if (sides == 0)
        {
            // The box a 4-sided hull at this circumradius circumscribes, as an analytic shape
            var side = radius * MathF.Sqrt(2f);
            var box = new Box(side, side, depth);

            index = simulation.Shapes.Add(box);
            inertia = box.ComputeInertia(1f);
        }
        else
        {
            var hull = BuildHull(sides, radius, depth, pool);

            index = simulation.Shapes.Add(hull);
            inertia = hull.ComputeInertia(1f);
        }

        var poseA = new RigidPose(new Vector3(0, 0, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotationA));
        var poseB = new RigidPose(new Vector3(offsetX, offsetY, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotationB));

        var a = simulation.Bodies.Add(BodyDescription.CreateDynamic(poseA, inertia, index, 0.01f));
        var b = simulation.Bodies.Add(BodyDescription.CreateDynamic(poseB, inertia, index, 0.01f));

        var failedAt = -1;

        for (var step = 0; step < steps && failedAt < 0; step++)
        {
            simulation.Timestep(1 / 60f, dispatcher);

            var pa = simulation.Bodies[a].Pose.Position;
            var pb = simulation.Bodies[b].Pose.Position;

            var clean = float.IsFinite(pa.X) && float.IsFinite(pa.Y) && float.IsFinite(pa.Z) &&
                        float.IsFinite(pb.X) && float.IsFinite(pb.Y) && float.IsFinite(pb.Z);

            if (!clean) failedAt = step;
        }

        simulation.Dispose();
        pool.Clear();

        return failedAt;
    }

    public static void Run(int sides, float radius, float depth, float offsetX, int steps, bool compound = false, float firstStepDt = 1 / 60f, float offsetY = 0f)
    {
        var pool = new BufferPool();
        using var dispatcher = new ThreadDispatcher(1);

        var simulation = Simulation.Create(
            pool,
            new Callbacks(),
            new PoseCallbacks(),
            new SolveDescription(8, 1));

        var hull = BuildHull(sides, radius, depth, pool);
        TypedIndex index;
        BodyInertia inertia;

        if (compound)
        {
            // What the toolkit actually builds: one collider, but wrapped in a CompoundCollider,
            // so Bepu sees a compound with a single convex hull child rather than a bare hull.
            var builder = new CompoundBuilder(pool, simulation.Shapes, 1);

            builder.Add(hull, RigidPose.Identity, 1f);
            builder.BuildDynamicCompound(out var children, out inertia);
            builder.Dispose();

            index = simulation.Shapes.Add(new Compound(children));
        }
        else
        {
            index = simulation.Shapes.Add(hull);
            inertia = hull.ComputeInertia(1f);
        }

        var a = simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new Vector3(0, 0, 0), inertia, index, 0.01f));
        var b = simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new Vector3(offsetX, offsetY, 0), inertia, index, 0.01f));

        for (var step = 0; step < steps; step++)
        {
            // Stride spends ~200 ms on the frame that first uses a new material, and hands the
            // whole of it to Bepu. firstStepDt reproduces that one long frame.
            simulation.Timestep(step == 0 ? firstStepDt : 1 / 60f, dispatcher);

            var pa = simulation.Bodies[a].Pose.Position;
            var pb = simulation.Bodies[b].Pose.Position;

            if (float.IsFinite(pa.X) && float.IsFinite(pa.Y) && float.IsFinite(pa.Z) &&
                float.IsFinite(pb.X) && float.IsFinite(pb.Y) && float.IsFinite(pb.Z))
            {
                continue;
            }

            Console.Error.WriteLine(
                $"[pure] compound={compound} dt0={firstStepDt:F3} sides={sides} radius={radius} depth={depth} offset=({offsetX:F4},{offsetY:F4}) NaN at step {step}");

            simulation.Dispose();
            pool.Clear();

            return;
        }

        Console.Error.WriteLine(
            $"[pure] compound={compound} dt0={firstStepDt:F3} sides={sides} radius={radius} depth={depth} offset=({offsetX:F4},{offsetY:F4}) clean after {steps} steps");

        simulation.Dispose();
        pool.Clear();
    }

    private static ConvexHull BuildHull(int sides, float radius, float depth, BufferPool pool)
    {
        pool.Take<Vector3>(sides * 2, out var points);

        var half = depth / 2f;
        var step = MathF.Tau / sides;

        for (var i = 0; i < sides; i++)
        {
            // The same expression the toolkit's PolygonProceduralModel uses, including the -PI/2
            // start, so the point set is bit-for-bit what the toolkit feeds Bepu.
            var angle = i * step - MathF.PI / 2;
            var x = MathF.Cos(angle) * radius;
            var y = MathF.Sin(angle) * radius;

            points[i] = new Vector3(x, y, half);
            points[i + sides] = new Vector3(x, y, -half);
        }

        ConvexHullHelper.CreateShape(points.Slice(0, sides * 2), pool, out _, out var hull);

        pool.Return(ref points);

        return hull;
    }

    private struct Callbacks : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation) { }

        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) => true;

        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties material)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            material = new PairMaterialProperties(1f, 2f, new SpringSettings(30, 1));

            if (LogManifoldsFlag)
            {
                for (var i = 0; i < manifold.Count; i++)
                {
                    manifold.GetContact(i, out var offset, out var normal, out var depth, out _);

                    Console.Error.WriteLine(
                        $"  [contact] {i}/{manifold.Count} depth={depth} " +
                        $"normal=({normal.X},{normal.Y},{normal.Z}) " +
                        $"offset=({offset.X},{offset.Y},{offset.Z})");
                }
            }

            return true;
        }

        public static bool LogManifoldsFlag;

        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

        public void Dispose() { }
    }

    private struct PoseCallbacks : IPoseIntegratorCallbacks
    {
        private Vector3Wide _gravityDt;

        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        public readonly bool AllowSubstepsForUnconstrainedBodies => false;

        public readonly bool IntegrateVelocityForKinematics => false;

        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt)
            => _gravityDt = Vector3Wide.Broadcast(new Vector3(0, -10, 0) * dt);

        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
            => velocity.Linear += _gravityDt;
    }
}
