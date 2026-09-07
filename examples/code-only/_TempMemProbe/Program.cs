// TEMPORARY diagnostic harness - delete when the investigation is done.
// SHAPE=sphere|cube|prism  BODY=2d|3d  MODEL=perbody|shared  LAYOUT=grid|random  COUNT=n  SECONDS=n
using System.Diagnostics;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Components;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Bepu.Colliders;
using Stride.CommunityToolkit.Bepu.Extensions;
using Stride.CommunityToolkit.Engine;
using Stride.Graphics.GeometricPrimitives;
using Stride.CommunityToolkit.Helpers;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Rendering.Instancing;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;

var shape = (Environment.GetEnvironmentVariable("SHAPE") ?? "sphere").ToLowerInvariant();
var body = (Environment.GetEnvironmentVariable("BODY") ?? "2d").ToLowerInvariant();
var modelMode = (Environment.GetEnvironmentVariable("MODEL") ?? "perbody").ToLowerInvariant();
var layout = (Environment.GetEnvironmentVariable("LAYOUT") ?? "random").ToLowerInvariant();
var count = int.Parse(Environment.GetEnvironmentVariable("COUNT") ?? "10000");
var seconds = double.Parse(Environment.GetEnvironmentVariable("SECONDS") ?? "40");

var modelType = shape switch
{
    "cube" or "cubehull" => PrimitiveModelType.Cube,
    "prism" => PrimitiveModelType.TriangularPrism,
    "cone" => PrimitiveModelType.Cone,
    "teapot" => PrimitiveModelType.Teapot,
    "torus" => PrimitiveModelType.Torus,
    _ => PrimitiveModelType.Sphere,
};

// A cube driven through the ConvexHullCollider path instead of the analytic BoxCollider.
// Separates "is it hulls?" from "is it flat faces?" - the cube is the only shape that can be
// both, so it is the only one that can tell the two apart.
ConvexHullCollider? CubeHull() => SharedHullCache.CreateCollider(
    "ProbeCubeHull", 1, 1, 1,
    () => GeometricPrimitive.Cube.New(1f).ToDecomposedHulls());

float wallWidth = 100;
Vector3 wallSize = new(1, 50, 1);

BufferedEntityInstancing? bufferedInstancing = null;
Model? sharedModel = null;
var elapsed = 0.0;
var nextReport = 0.0;
var process = Process.GetCurrentProcess();
var inertiaScale = float.Parse(Environment.GetEnvironmentVariable("SCALE") ?? "0");
var angularClamp = Environment.GetEnvironmentVariable("ANGCLAMP") ?? "zero";

// Bepu's guidance for planar simulation is that the constrained axis should not be thin - thin
// shapes give poorly conditioned contact manifolds. The prism defaults to a depth of 1; this makes
// that adjustable. Prism only, because Size means something different for every other primitive.
var probeDepth = float.Parse(Environment.GetEnvironmentVariable("DEPTH") ?? "1");
Vector3? probeSize = shape == "prism" && probeDepth != 1f ? new Vector3(1, 1, probeDepth) : null;

// Every spawned body, so ejections can be counted rather than eyeballed
var bodies = new List<BodyComponent>();

Console.Error.WriteLine($"[probe] shape={shape} body={body} model={modelMode} layout={layout} count={count} seconds={seconds}");

using var game = new Game();

game.Run(start: Start, update: Update);

bufferedInstancing?.Dispose();
Report("final");
Console.Error.WriteLine("[probe] COMPLETED");

void Report(string tag)
{
    process.Refresh();

    var managed = GC.GetTotalMemory(false) / 1048576.0;
    var priv = process.PrivateMemorySize64 / 1048576.0;

    var pool = 0.0;
    var pairs = 0;
    var awake = 0;

    var configuration = game.Services.GetService<BepuConfiguration>();

    if (configuration?.BepuSimulations.Count > 0)
    {
        var sim = configuration.BepuSimulations[0].Simulation;

        pool = sim.BufferPool.GetTotalAllocatedByteCount() / 1048576.0;
        pairs = sim.NarrowPhase.PairCache.Mapping.Count;
        awake = sim.Bodies.ActiveSet.Count;
    }

    // Ejections only. Counting anything above the pile would just count bodies still falling from
    // the spawn column, and taking the max speed over everything would report terminal velocity
    // rather than anything the solver did - both of which this got wrong the first time round.
    var throughFloor = 0;
    var pastWalls = 0;
    var offPlane = 0;
    var settledMaxSpeed = 0f;
    var maxZ = 0f;

    foreach (var b in bodies)
    {
        var p = b.Position;

        if (p.Y < -30f) throughFloor++;
        if (MathF.Abs(p.X) > 55f) pastWalls++;
        if (MathF.Abs(p.Z) > 0.1f) offPlane++;

        maxZ = MathF.Max(maxZ, MathF.Abs(p.Z));

        // Only bodies still inside the box: excludes both the spawn column above and anything
        // already ejected, which would otherwise just report how long it has been free-falling
        if (p.Y is > -30f and < 30f && MathF.Abs(p.X) < 55f)
        {
            settledMaxSpeed = MathF.Max(settledMaxSpeed, b.LinearVelocity.Length());
        }
    }

    Console.Error.WriteLine(
        $"[mem] {tag,-8} t={elapsed,5:0.0}s  managed {managed,6:0} MB  private {priv,7:0} MB  " +
        $"bepupool {pool,6:0} MB  pairs {pairs,7}  awake {awake,6}  floor {throughFloor,5}  " +
        $"walls {pastWalls,5}  offplane {offPlane,6}  maxZ {maxZ,7:0.000}  pileSpeed {settledMaxSpeed,7:0.0}");
}

void Start(Scene rootScene)
{
    game.AddGraphicsCompositor().AddCleanUIStage();
    game.Add3DCamera(initialPosition: new Vector3(0, 0, 80), initialRotation: Vector3.Zero);

    var light = game.AddDirectionalLight();
    light.Transform.Rotation = Quaternion.RotationX(MathUtil.DegreesToRadians(-30)) *
                               Quaternion.RotationY(MathUtil.DegreesToRadians(-30));

    // Confines ordinary 3D bodies to a slab, so they pile as densely as Body2DComponent makes
    // them pile - without the component. Separates "hull under sustained contact" from "2D body".
    // The default floor is only one unit deep in Z, which is fine for planar bodies but lets 3D
    // ones fall straight past it, so the slab gets a floor of its own.
    var slab = Environment.GetEnvironmentVariable("ZWALLS") == "on";
    var depth = slab ? 6f : 1f;

    CreateWall(rootScene, new Vector3(-wallWidth / 2, 0, 0), new Vector3(wallSize.X, wallSize.Y, depth));
    CreateWall(rootScene, new Vector3(wallWidth / 2, 0, 0), new Vector3(wallSize.X, wallSize.Y, depth));
    CreateWall(rootScene, new Vector3(0, -25, 0), new Vector3(wallWidth, 1, depth));

    if (slab)
    {
        CreateWall(rootScene, new Vector3(0, 0, -3f), new Vector3(wallWidth, 50, 1));
        CreateWall(rootScene, new Vector3(0, 0, 3f), new Vector3(wallWidth, 50, 1));
    }

    // A pile that holds while shallow and gives way once it is deep is the signature of a solver
    // that cannot converge within its iteration budget, so make the budget adjustable.
    var config = game.Services.GetService<BepuConfiguration>();
    var solver = config.BepuSimulations[0].Simulation.Solver;

    Console.Error.WriteLine($"[probe] solver defaults: substeps={solver.SubstepCount} velocityIterations={solver.VelocityIterationCount}");

    if (Environment.GetEnvironmentVariable("SUBSTEP") is { Length: > 0 } s) solver.SubstepCount = int.Parse(s);
    if (Environment.GetEnvironmentVariable("ITER") is { Length: > 0 } it) solver.VelocityIterationCount = int.Parse(it);

    Console.Error.WriteLine($"[probe] solver in use:   substeps={solver.SubstepCount} velocityIterations={solver.VelocityIterationCount}");

    game.AddInstancingSupport();

    var prototype = game.Create3DPrimitive(modelType, new Primitive3DEntityOptions { Size = probeSize });
    prototype.Transform.Position = new Vector3(0, -100, 0);
    prototype.Scene = rootScene;
    sharedModel = prototype.Get<ModelComponent>().Model;

    bufferedInstancing = new BufferedEntityInstancing(new BepuEntityInstancing());

    var master = new Entity("Master")
    {
        new ModelComponent(sharedModel),
        new InstancingComponent { Type = bufferedInstancing }
    };
    master.Scene = rootScene;

    game.AddInstancingBufferUpload(bufferedInstancing);

    Report("prespawn");

    if (layout == "spread")
    {
        // Nothing overlaps at spawn, and the jitter keeps it off an exact lattice (which
        // degenerates the broad-phase tree). The pile builds gradually, as it does on screen.
        var random = new Random(1);
        var perRow = (int)(wallWidth / 1.6f);
        var rows = count / perRow;
        var rowGap = float.Parse(Environment.GetEnvironmentVariable("ROWGAP") ?? "3");

        for (var i = 0; i < rows; i++)
        for (var j = 0; j < perRow; j++)
            Spawn(rootScene, new Vector3(
                (j - perRow / 2f) * 1.6f + (random.NextSingle() - 0.5f) * 0.05f,
                20 + i * rowGap + (random.NextSingle() - 0.5f) * 0.05f,
                0));
    }
    else if (layout == "grid")
    {
        var rows = (int)(count / wallWidth);
        for (var i = 0; i < rows; i++)
        for (var j = 0; j < wallWidth; j++)
            Spawn(rootScene, new Vector3(j - wallWidth / 2, 20 + i * 5, 0));
    }
    else
    {
        for (var i = 0; i < count; i++)
            Spawn(rootScene, VectorHelper.RandomVector3(xRange: [-40, 40], yRange: [20, 400], zRange: [0, 0]));
    }

    Report("spawned");
}

void Spawn(Scene rootScene, Vector3 position)
{
    CollidableComponent component = body switch
    {
        "3d" => new BodyComponent { Collider = new CompoundCollider() },
        // The two halves of Body2DComponent, separately
        "lockonly" => new SplitBody2D { Lock = true, ZFix = false, Collider = new CompoundCollider() },
        "zfixonly" => new SplitBody2D { Lock = false, ZFix = true, Collider = new CompoundCollider() },
        "both" => new SplitBody2D { Lock = true, ZFix = true, Collider = new CompoundCollider() },
        // Body2DComponent with the out-of-plane inertia scale under our control, so it can be swept
        // without rebuilding the library. SCALE=0 reproduces the original zeroing.
        "scaled" => new SplitBody2D { Lock = true, ZFix = true, Scale = inertiaScale, AngularClamp = angularClamp, Collider = new CompoundCollider() },
        // Bepu's own idiom: the WHOLE inverse inertia tensor zeroed, as its character demo does.
        // Loses in-plane rolling, so it is a diagnostic rather than a candidate fix.
        "lockall" => new SplitBody2D { Lock = true, ZFix = true, Scale = 0f, LockZToo = true, AngularClamp = angularClamp, Collider = new CompoundCollider() },
        _ => new Body2DComponent { Collider = new CompoundCollider() }
    };

    Entity entity;

    if (shape == "cubehull")
    {
        component.Collider = new CompoundCollider { Colliders = { CubeHull()! } };
        entity = new Entity("Item") { new ModelComponent(sharedModel), component };
    }
    else if (modelMode == "shared")
    {
        // One mesh for every body: AddBepu3DPhysics only needs a ModelComponent to be present,
        // it derives the collider from the primitive type and reads nothing out of the mesh
        entity = new Entity("Item") { new ModelComponent(sharedModel) };
        entity.AddBepu3DPhysics(modelType, new Bepu3DPhysicsOptions { Component = component, Size = probeSize });
    }
    else
    {
        entity = game.Create3DPrimitive(modelType, new Bepu3DPhysicsOptions { Component = component });
    }

    // Body2DComponent caps MaximumRecoveryVelocity at 1.5 for hull colliders, down from Bepu's
    // default of 1000. This puts it back, to see whether that cap is what runs away.
    if (Environment.GetEnvironmentVariable("RECOVERY") is { Length: > 0 } recovery)
    {
        component.MaximumRecoveryVelocity = float.Parse(recovery);
    }

    entity.Remove<ModelComponent>();
    entity.Transform.Position = position;

    if (component is BodyComponent tracked) bodies.Add(tracked);
    bufferedInstancing?.AddInstance(entity);
    entity.Scene = rootScene;
}

void CreateWall(Scene rootScene, Vector3 position, Vector3 size)
{
    var wall = game.Create3DPrimitive(PrimitiveModelType.Cube, new Bepu3DPhysicsOptions
    {
        Size = size,
        Material = game.CreateMaterial(Color.LightGray),
        Component = new StaticComponent { Collider = new CompoundCollider() }
    });
    wall.Transform.Position = position;
    wall.Scene = rootScene;
}

void Update(Scene rootScene, GameTime time)
{
    elapsed += time.Elapsed.TotalSeconds;

    if (elapsed >= nextReport)
    {
        Report("running");
        nextReport = elapsed + 5;
    }

    if (elapsed >= seconds) game.Exit();
}

/// <summary>
/// Body2DComponent split into its two independent halves, so each can be tested on its own:
/// the inverse-inertia rotation lock, and the per-step out-of-plane velocity correction.
/// </summary>
class SplitBody2D : BodyComponent, ISimulationUpdate
{
    public bool Lock { get; init; }
    public bool ZFix { get; init; }

    /// <summary>What the out-of-plane inverse inertia is multiplied by. Zero is the original lock.</summary>
    public float Scale { get; init; }

    /// <summary>Also zero ZZ, giving the fully zeroed tensor Bepu's own character demo uses.</summary>
    public bool LockZToo { get; init; }

    /// <summary>
    /// How out-of-plane angular velocity is handled each step: "zero" hard-clears it (what
    /// Body2DComponent does), "damp" scales it down, "off" leaves it to the inertia lock alone.
    /// </summary>
    public string AngularClamp { get; init; } = "zero";

    protected override void AttachInner(BepuPhysics.RigidPose pose, BepuPhysics.BodyInertia shapeInertia, BepuPhysics.Collidables.TypedIndex shapeIndex)
    {
        base.AttachInner(pose, shapeInertia, shapeIndex);

        if (!Lock) return;

        var inertia = BodyInertia;
        var inverseInertia = inertia.InverseInertiaTensor;

        inverseInertia.XX *= Scale;
        inverseInertia.YY *= Scale;
        inverseInertia.YX = 0f;
        inverseInertia.ZX = 0f;
        inverseInertia.ZY = 0f;

        if (LockZToo) inverseInertia.ZZ = 0f;

        inertia.InverseInertiaTensor = inverseInertia;
        BodyInertia = inertia;
    }

    public void SimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        if (!ZFix || !Awake) return;

        var velocity = LinearVelocity;
        var zError = Position.Z;
        var targetVelocityZ = MathF.Abs(zError) > 0.001f ? Math.Clamp(-zError, -1f, 1f) : 0f;

        if (velocity.Z != targetVelocityZ)
        {
            velocity.Z = targetVelocityZ;
            LinearVelocity = velocity;
        }

        if (AngularClamp == "off") return;

        var angularVelocity = AngularVelocity;

        if (angularVelocity.X != 0f || angularVelocity.Y != 0f)
        {
            if (AngularClamp == "damp")
            {
                angularVelocity.X *= 0.5f;
                angularVelocity.Y *= 0.5f;
            }
            else
            {
                angularVelocity.X = 0f;
                angularVelocity.Y = 0f;
            }

            AngularVelocity = angularVelocity;
        }
    }

    public void AfterSimulationUpdate(BepuSimulation sim, float simTimeStep) { }
}
