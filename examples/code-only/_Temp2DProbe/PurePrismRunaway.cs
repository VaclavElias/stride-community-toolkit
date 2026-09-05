// TEMPORARY - Stride-free reproduction of the narrow-phase memory runaway seen with a partly
// zeroed inverse inertia tensor. Nothing here touches Stride, the toolkit, or Body2DComponent;
// only the inertia handed to Bepu differs between runs.
using System.Diagnostics;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

namespace Temp2DProbe;

internal static class PurePrismRunaway
{
    /// <summary>How the inverse inertia tensor is modified before the bodies are created.</summary>
    public enum Lock
    {
        /// <summary>Untouched - a normal 3D body.</summary>
        None,

        /// <summary>Every term zeroed, which is the idiom Bepu's own character demo uses.</summary>
        AllZeroed,

        /// <summary>XX and YY zeroed, ZZ kept - "infinite inertia about X and Y", rank 1.</summary>
        PartlyZeroed,

        /// <summary>XX and YY scaled down instead of zeroed, so the tensor stays full rank.</summary>
        Scaled,

        /// <summary>Only XX zeroed - rank 2 - to fill in the rank table between 1 and 3.</summary>
        OneAxisZeroed,
    }

    /// <summary>
    /// The factor <see cref="Lock.Scaled"/> applies. Sweeping it tests the theory that scaling works
    /// by letting bodies drift slightly off-plane, which breaks the exact face alignment that
    /// degenerates hull collision - if so, a small enough scale should behave like zero and fail.
    /// </summary>
    public static float OutOfPlaneInertiaScale = 1e-4f;

    /// <summary>Which collidable the pile is built from.</summary>
    public enum Shape
    {
        /// <summary>A triangular prism as a <see cref="ConvexHull"/>.</summary>
        PrismHull,

        /// <summary>A cube as a <see cref="ConvexHull"/> - separates "hull" from "prism".</summary>
        CubeHull,

        /// <summary>An analytic <see cref="Box"/> of the same size - separates "hull" from "shape".</summary>
        Box,

        /// <summary>An analytic <see cref="Sphere"/>.</summary>
        Sphere,
    }

    public static void Run(Lock mode, int count, float seconds, bool zLock, Shape shapeKind = Shape.PrismHull, int threads = 0, float spacing = 1.25f, double abortAtMegabytes = 4096)
    {
        if (threads <= 0) threads = Math.Max(1, Environment.ProcessorCount - 2);

        // Written and flushed before anything else, because the failure mode under investigation
        // kills the process outright and would take buffered output with it.
        Console.WriteLine($"--- start {shapeKind} {mode} zLock={zLock} count={count} threads={threads} spacing={spacing} scale={OutOfPlaneInertiaScale:E0}");
        Console.Out.Flush();

        var pool = new BufferPool();
        using var dispatcher = new ThreadDispatcher(threads);

        PoseCallbacks.ZeroZVelocity = zLock;

        var simulation = Simulation.Create(pool, new Callbacks(), new PoseCallbacks(), new SolveDescription(8, 1));

        var (shape, shapeInertia) = BuildShape(shapeKind, simulation, pool);
        var inertia = ApplyLock(shapeInertia, mode);

        // A floor wide enough that the pile does not simply walk off the end.
        simulation.Statics.Add(new StaticDescription(
            new Vector3(0, -0.5f, 0),
            simulation.Shapes.Add(new Box(120, 1, 20))));

        // A planar grid, which is what the toolkit's stress pile drops. Spacing is wider than the
        // shape so nothing overlaps at spawn - the pile only comes together as it falls.
        const int PerRow = 40;

        for (var i = 0; i < count; i++)
        {
            var column = i % PerRow;
            var row = i / PerRow;

            simulation.Bodies.Add(BodyDescription.CreateDynamic(
                new Vector3((column - PerRow / 2) * spacing, 1f + row * spacing, 0),
                inertia,
                shape,
                0.01f));
        }

        var process = Process.GetCurrentProcess();
        var reportedBadState = false;
        var steps = (int)(seconds * 60);
        var peakPool = 0.0;
        var peakWorking = 0.0;

        for (var step = 0; step < steps; step++)
        {
            // Checked BEFORE the step. Collision detection runs at the start of a Timestep, before
            // the solver writes poses, so "state was already bad on entry to step N" and "a contact
            // went bad during step N" can be ordered against each other.
            if (!reportedBadState)
            {
                for (var i = 0; i < simulation.Bodies.ActiveSet.Count; i++)
                {
                    ref var body = ref simulation.Bodies.ActiveSet.DynamicsState[i];

                    if (IsFinite(body.Motion.Pose.Position) && IsFinite(body.Motion.Velocity.Linear)
                        && IsFinite(body.Motion.Velocity.Angular) && IsFinite(body.Motion.Pose.Orientation))
                    {
                        continue;
                    }

                    Console.WriteLine(
                        $"{shapeKind,-10} {mode,-13} BAD BODY STATE entering step {step} (t={step / 60f:F3}s) " +
                        $"pos={body.Motion.Pose.Position} lin={body.Motion.Velocity.Linear} " +
                        $"ang={body.Motion.Velocity.Angular} orientation={body.Motion.Pose.Orientation}");
                    Console.Out.Flush();

                    reportedBadState = true;

                    break;
                }
            }

            simulation.Timestep(1 / 60f, dispatcher);

            if (Callbacks.NonFiniteContacts > 0)
            {
                Console.WriteLine(
                    $"{shapeKind,-10} {mode,-13} BAD CONTACT during step {step} (t={step / 60f:F3}s) " +
                    $"({Callbacks.NonFiniteContacts} in this step)");
                Console.Out.Flush();

                Callbacks.NonFiniteContacts = 0;
            }

            if (step % 30 != 0) continue;

            process.Refresh();

            var poolMb = simulation.BufferPool.GetTotalAllocatedByteCount() / 1048576.0;
            var workingMb = process.WorkingSet64 / 1048576.0;

            peakPool = Math.Max(peakPool, poolMb);
            peakWorking = Math.Max(peakWorking, workingMb);

            if (poolMb <= abortAtMegabytes) continue;

            Console.WriteLine(
                $"{shapeKind,-10} {mode,-13} zLock={zLock,-5} count={count} threads={threads}RUNAWAY at t={step / 60f:F1}s " +
                $"pool={poolMb:F0}MB working={workingMb:F0}MB");

            simulation.Dispose();
            pool.Clear();

            return;
        }

        Console.WriteLine(
            $"{shapeKind,-10} {mode,-13} zLock={zLock,-5} count={count} threads={threads}stable through {seconds:F0}s " +
            $"peakPool={peakPool:F0}MB peakWorking={peakWorking:F0}MB");

        Console.Out.Flush();

        simulation.Dispose();
        pool.Clear();
    }

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static bool IsFinite(Quaternion q) => float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W);

    private static BodyInertia ApplyLock(BodyInertia inertia, Lock mode)
    {
        ref var t = ref inertia.InverseInertiaTensor;

        switch (mode)
        {
            case Lock.AllZeroed:
                t = default;
                break;

            case Lock.PartlyZeroed:
                t.XX = 0f;
                t.YY = 0f;
                t.YX = 0f;
                t.ZX = 0f;
                t.ZY = 0f;
                break;

            case Lock.OneAxisZeroed:
                t.XX = 0f;
                t.YX = 0f;
                t.ZX = 0f;
                break;

            case Lock.Scaled:
                t.XX *= OutOfPlaneInertiaScale;
                t.YY *= OutOfPlaneInertiaScale;
                t.YX = 0f;
                t.ZX = 0f;
                t.ZY = 0f;
                break;
        }

        return inertia;
    }

    private static (TypedIndex Shape, BodyInertia Inertia) BuildShape(Shape kind, Simulation simulation, BufferPool pool)
    {
        switch (kind)
        {
            case Shape.Box:
            {
                var box = new Box(1, 1, 1);

                return (simulation.Shapes.Add(box), box.ComputeInertia(1f));
            }

            case Shape.Sphere:
            {
                var sphere = new Sphere(0.5f);

                return (simulation.Shapes.Add(sphere), sphere.ComputeInertia(1f));
            }

            default:
            {
                var hull = BuildHull(kind, pool);

                return (simulation.Shapes.Add(hull), hull.ComputeInertia(1f));
            }
        }
    }

    /// <summary>
    /// PrismHull: base width 1 across X, height 1 across Y, depth 1 along Z, centred on the origin -
    /// the shape the toolkit generates for <c>PrimitiveModelType.TriangularPrism</c>.
    /// CubeHull: the same unit cube as <see cref="Shape.Box"/>, but driven through the hull path.
    /// </summary>
    private static ConvexHull BuildHull(Shape kind, BufferPool pool)
    {
        if (kind == Shape.CubeHull)
        {
            pool.Take<Vector3>(8, out var cube);

            var next = 0;

            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                        cube[next++] = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f);

            ConvexHullHelper.CreateShape(cube.Slice(0, 8), pool, out _, out var cubeHull);

            pool.Return(ref cube);

            return cubeHull;
        }

        pool.Take<Vector3>(6, out var points);

        points[0] = new Vector3(-0.5f, -0.5f, 0.5f);
        points[1] = new Vector3(0f, 0.5f, 0.5f);
        points[2] = new Vector3(0.5f, -0.5f, 0.5f);
        points[3] = new Vector3(-0.5f, -0.5f, -0.5f);
        points[4] = new Vector3(0f, 0.5f, -0.5f);
        points[5] = new Vector3(0.5f, -0.5f, -0.5f);

        ConvexHullHelper.CreateShape(points.Slice(0, 6), pool, out _, out var hull);

        pool.Return(ref points);

        return hull;
    }

    private struct Callbacks : INarrowPhaseCallbacks
    {
        /// <summary>
        /// Counts contacts whose depth is not finite. If the pile corrupts memory because the
        /// narrow phase produced a bad manifold first, this is where it shows up - and it is the
        /// same symptom as the extruded-polygon NaN issue.
        /// </summary>
        public static int NonFiniteContacts;

        public void Initialize(Simulation simulation) { }

        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) => true;

        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties material)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            material = new PairMaterialProperties(1f, 2f, new SpringSettings(30, 1));

            for (var i = 0; i < manifold.Count; i++)
            {
                manifold.GetContact(i, out _, out var normal, out var depth, out _);

                if (float.IsFinite(depth) && float.IsFinite(normal.X) && float.IsFinite(normal.Y) && float.IsFinite(normal.Z))
                {
                    continue;
                }

                Interlocked.Increment(ref NonFiniteContacts);
            }

            return true;
        }

        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

        public void Dispose() { }
    }

    private struct PoseCallbacks : IPoseIntegratorCallbacks
    {
        /// <summary>Mirrors what a 2D body component does on top of the inertia lock.</summary>
        public static bool ZeroZVelocity;

        private Vector3Wide _gravityDt;
        private bool _zeroZ;

        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        public readonly bool AllowSubstepsForUnconstrainedBodies => false;

        public readonly bool IntegrateVelocityForKinematics => false;

        public void Initialize(Simulation simulation) => _zeroZ = ZeroZVelocity;

        public void PrepareForIntegration(float dt)
            => _gravityDt = Vector3Wide.Broadcast(new Vector3(0, -10, 0) * dt);

        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            velocity.Linear += _gravityDt;

            if (_zeroZ)
            {
                velocity.Linear.Z = Vector<float>.Zero;
            }
        }
    }
}
