// TEMPORARY diagnostic harness for the 2D shapes - delete when the investigation is done.
// SHAPE=circle|capsule|rectangle|square|polygon|triangle|parallelogram
// COUNT=n  PERFRAME=n  SPREAD=f  SECONDS=n  CACHE=0|1
using System.Diagnostics;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Mathematics;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

var shape = (Environment.GetEnvironmentVariable("SHAPE") ?? "polygon").ToLowerInvariant();
var count = int.Parse(Environment.GetEnvironmentVariable("COUNT") ?? "400");
var perFrame = int.Parse(Environment.GetEnvironmentVariable("PERFRAME") ?? "1");
var spread = float.Parse(Environment.GetEnvironmentVariable("SPREAD") ?? "6");
var seconds = double.Parse(Environment.GetEnvironmentVariable("SECONDS") ?? "40");
var sides = int.Parse(Environment.GetEnvironmentVariable("SIDES") ?? "0");
var radius = float.Parse(Environment.GetEnvironmentVariable("RADIUS") ?? "0.5");
var verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
var bodyMode = (Environment.GetEnvironmentVariable("BODY") ?? "2d").ToLowerInvariant();
var depth = float.Parse(Environment.GetEnvironmentVariable("DEPTH") ?? "1");
var trace = Environment.GetEnvironmentVariable("TRACE") == "1";

var parallelogram = new Vector2[]
{
    new(-0.5f, -0.25f),
    new(0.5f, -0.25f),
    new(0.75f, 0.25f),
    new(-0.25f, 0.25f),
};

var (type, vertices) = shape switch
{
    "circle" => (Primitive2DModelType.Circle, null),
    "capsule" => (Primitive2DModelType.Capsule, null),
    "rectangle" => (Primitive2DModelType.Rectangle, null),
    "square" => (Primitive2DModelType.Square, null),
    "triangle" => (Primitive2DModelType.Triangle, null),
    "parallelogram" => (Primitive2DModelType.Polygon, parallelogram),
    _ => (Primitive2DModelType.Polygon, (Vector2[]?)null),
};

// "mix" is the spawn menu example's own shape list, in its own order. "mixnohex" is the same list
// with the default Polygon dropped, which answers what the example does once the hexagon is safe.
(Primitive2DModelType Type, Vector2[]? Vertices)[] mixed =
[
    (Primitive2DModelType.Circle, null),
    (Primitive2DModelType.Capsule, null),
    (Primitive2DModelType.Rectangle, null),
    (Primitive2DModelType.Square, null),
    (Primitive2DModelType.Polygon, null),
    (Primitive2DModelType.Triangle, null),
    (Primitive2DModelType.Polygon, parallelogram),
];

if (shape == "mixnohex")
{
    mixed = [.. mixed.Where(m => m.Type != Primitive2DModelType.Polygon || m.Vertices is not null)];
}

var isMixed = shape is "mix" or "mixnohex";

var random = new Random(1);
var process = Process.GetCurrentProcess();
Scene? scene = null;
var spawned = 0;
var tracked = new List<Entity>();
var elapsed = 0.0;
var nextReport = 0.0;

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Console.Error.WriteLine($"[probe] UNHANDLED {e.ExceptionObject}");

Console.Error.WriteLine($"[probe] shape={shape} body={bodyMode} count={count} perframe={perFrame} spread={spread} seconds={seconds}");

// PRISM=1 runs the Stride-free reproduction of the inertia-lock memory runaway.
if (Environment.GetEnvironmentVariable("PRISM") == "1")
{
    var prismCount = int.Parse(Environment.GetEnvironmentVariable("PRISMCOUNT") ?? "8000");
    var prismSeconds = float.Parse(Environment.GetEnvironmentVariable("PRISMSECONDS") ?? "10");
    var zLock = Environment.GetEnvironmentVariable("ZLOCK") == "1";

    // MODE names a single lock so a run that kills the process can still be attributed.
    var modeName = Environment.GetEnvironmentVariable("MODE");
    var modes = modeName is null
        ? Enum.GetValues<Temp2DProbe.PurePrismRunaway.Lock>()
        : [Enum.Parse<Temp2DProbe.PurePrismRunaway.Lock>(modeName, ignoreCase: true)];

    var shapeKind = Enum.Parse<Temp2DProbe.PurePrismRunaway.Shape>(
        Environment.GetEnvironmentVariable("PRISMSHAPE") ?? "PrismHull", ignoreCase: true);
    var threads = int.Parse(Environment.GetEnvironmentVariable("THREADS") ?? "0");
    var spacing = float.Parse(Environment.GetEnvironmentVariable("SPACING") ?? "1.25");

    if (Environment.GetEnvironmentVariable("SCALE") is { } scaleText)
    {
        Temp2DProbe.PurePrismRunaway.OutOfPlaneInertiaScale = float.Parse(scaleText);
    }

    foreach (var lockMode in modes)
    {
        Temp2DProbe.PurePrismRunaway.Run(lockMode, prismCount, prismSeconds, zLock, shapeKind, threads, spacing);
    }

    return 0;
}

// SWEEP=1 answers "which side counts are safe to spawn on top of each other" with a failure rate
// rather than a single offset: TRIALS random placements per side count, at the RADIUS/DEPTH given.
// ROTATE=0 keeps both bodies axis-aligned; the default also randomises their rotation about Z,
// which is what falling 2D bodies actually have when they meet.
if (Environment.GetEnvironmentVariable("SWEEP") == "1")
{
    var trials = int.Parse(Environment.GetEnvironmentVariable("TRIALS") ?? "200");
    var rotate = Environment.GetEnvironmentVariable("ROTATE") != "0";
    var steps = int.Parse(Environment.GetEnvironmentVariable("STEPS") ?? "8");

    Console.Error.WriteLine($"--- sweep: radius={radius} depth={depth} trials={trials} steps={steps} rotate={rotate} ---");

    Temp2DProbe.PureBepuRepro.LogFirstFailure = Environment.GetEnvironmentVariable("LOGFIRSTFAIL") == "1";

    foreach (var n in int.TryParse(Environment.GetEnvironmentVariable("SIDESONLY"), out var only) ? [only] : new[] { 3, 4, 5, 6, 7, 8, 10 })
    {
        var failedAtStep = Temp2DProbe.PureBepuRepro.Sweep(n, radius, depth, trials, steps, rotate, seed: 1);

        // Step 0 is the depth-NaN signature: the very first manifold poisoned the pose. Anything
        // later would point at a gradual blow-up instead.
        var failures = failedAtStep.Count(s => s >= 0);
        var atStepZero = failedAtStep.Count(s => s == 0);

        Console.Error.WriteLine(
            $"[sweep] sides={n,-3} failures={failures}/{trials} ({100.0 * failures / trials:F1}%), " +
            $"{atStepZero} of them on the first step");
    }

    // The analytic control: same placements, a Box instead of a hull. If deep overlap broke
    // contact generation in general this would fail too; if it is clean, the hull path owns it.
    var boxFailedAtStep = Temp2DProbe.PureBepuRepro.SweepBoxControl(radius, depth, trials, steps, rotate, seed: 1);
    var boxFailures = boxFailedAtStep.Count(s => s >= 0);
    var boxAtStepZero = boxFailedAtStep.Count(s => s == 0);

    Console.Error.WriteLine(
        $"[sweep] box control: failures={boxFailures}/{trials} ({100.0 * boxFailures / trials:F1}%), " +
        $"{boxAtStepZero} of them on the first step");

    return 0;
}

// PURE=1 runs the Stride-free reproduction instead of the game.
if (Environment.GetEnvironmentVariable("PURE") == "1")
{
    // The offsets Stride actually produced when the pile went NaN: centres 0.3445 apart in X and
    // 0.0760 in Y. Swept, because a hull pair can be fine at one overlap and degenerate at another.
    Console.Error.WriteLine("--- sweep: side count against offset direction ---");

    foreach (var isCompound in new[] { false, true })
    {
        foreach (var n in new[] { 3, 4, 5, 6, 7, 8, 10, 16, 32 })
        {
            foreach (var dy in new[] { 0f, 0.076f })
            {
                Temp2DProbe.PureBepuRepro.Run(n, radius, depth, 0.3445f, 240, isCompound, offsetY: dy);
            }
        }
    }

    Temp2DProbe.PureBepuRepro.LogManifolds = true;

    Console.Error.WriteLine("--- pentagon, same offset, for comparison ---");
    Temp2DProbe.PureBepuRepro.Run(5, radius, depth, 0.3445f, 1, offsetY: 0.076f);

    Console.Error.WriteLine("--- hexagon, offset in X only ---");
    Temp2DProbe.PureBepuRepro.Run(6, radius, depth, 0.3445f, 1, offsetY: 0f);

    Console.Error.WriteLine("--- hexagon, offset in X and Y ---");
    Temp2DProbe.PureBepuRepro.Run(6, radius, depth, 0.3445f, 1, offsetY: 0.076f);

    return 0;
}

// CHECK=1 builds the hull Bepu would build for each regular polygon and prints what came out,
// without ever starting the game. Separates "the hull is wrong" from "the simulation goes wrong".
if (Environment.GetEnvironmentVariable("CHECK") == "1")
{
    for (var n = 3; n <= 10; n++)
    {
        CheckHull(n);
    }

    return 0;
}

using var game = new Game();

try
{
    game.Run(start: Start, update: Update);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[probe] THREW {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Report("crash");

    return 1;
}

Report("final");
Console.Error.WriteLine("[probe] COMPLETED");

return 0;

void CheckHull(int n)
{
    var poly = PolygonProceduralModel.GenerateRegularPolygonVertices(radius, n);
    var points = new System.Numerics.Vector3[poly.Length * 2];

    for (var i = 0; i < poly.Length; i++)
    {
        points[i] = new System.Numerics.Vector3(poly[i].X, poly[i].Y, 0.5f);
        points[i + poly.Length] = new System.Numerics.Vector3(poly[i].X, poly[i].Y, -0.5f);
    }

    var pool = new BepuUtilities.Memory.BufferPool();
    pool.Take<System.Numerics.Vector3>(points.Length, out var buffer);
    points.CopyTo(buffer.As<System.Numerics.Vector3>().Slice(0, points.Length));

    BepuPhysics.Collidables.ConvexHullHelper.CreateShape(
        buffer.Slice(0, points.Length), pool, out var center, out var hull);

    hull.ComputeBounds(System.Numerics.Quaternion.Identity, out var min, out var max);

    var inertia = hull.ComputeInertia(1f);
    var t = inertia.InverseInertiaTensor;

    Console.Error.WriteLine(
        $"[hull] sides={n} bundles={hull.Points.Length} faces={hull.FaceToVertexIndicesStart.Length} " +
        $"center=({center.X:F4},{center.Y:F4},{center.Z:F4}) " +
        $"min=({min.X:F4},{min.Y:F4},{min.Z:F4}) max=({max.X:F4},{max.Y:F4},{max.Z:F4}) " +
        $"invMass={inertia.InverseMass:F4} " +
        $"tensor=[{t.XX:E3} {t.YX:E3} {t.YY:E3} {t.ZX:E3} {t.ZY:E3} {t.ZZ:E3}]");

    pool.Clear();
}

void Report(string tag)
{
    process.Refresh();

    var managed = GC.GetTotalMemory(false) / 1048576.0;
    var working = process.WorkingSet64 / 1048576.0;

    Console.Error.WriteLine(
        $"[probe] {tag} t={elapsed:F1}s spawned={spawned} managed={managed:F0}MB working={working:F0}MB");
}

void Start(Scene rootScene)
{
    scene = rootScene;

    game.SetupBase2DScene();

    // MUTATE=1 checks whether Create2DPrimitive writes back into the caller's options object, and
    // what the next shape built from the same instance ends up with.
    if (Environment.GetEnvironmentVariable("MUTATE") == "1")
    {
        var shared = new Bepu2DPhysicsOptions { Material = game.CreateFlatMaterial(Color.White) };

        Console.Error.WriteLine($"[mutate] before        Size={shared.Size?.ToString() ?? "null"}");

        game.Create2DPrimitive(Primitive2DModelType.Capsule, shared);
        Console.Error.WriteLine($"[mutate] after capsule Size={shared.Size?.ToString() ?? "null"}");

        game.Create2DPrimitive(Primitive2DModelType.Circle, shared);
        Console.Error.WriteLine($"[mutate] after circle  Size={shared.Size?.ToString() ?? "null"} (a default circle should be radius 0.5)");

        Console.Error.Flush();

        game.Exit();
    }
}

void Update(Scene rootScene, GameTime time)
{
    elapsed += time.Elapsed.TotalSeconds;

    // The hang happens inside the broad phase while adding a body, which is what a NaN bounding box
    // would do. Catching the first non-finite pose says whether the tree was already poisoned before
    // the add, or whether the add itself is at fault.
    if (trace)
    {
        foreach (var (index, tracedEntity) in tracked.Index())
        {
            var b = tracedEntity.Get<BodyComponent>();

            Console.Error.WriteLine(
                $"[trace] t={elapsed:F3} #{index} pos={tracedEntity.Transform.Position} " +
                $"lin={b?.LinearVelocity} ang={b?.AngularVelocity}");
        }

        Console.Error.Flush();
    }

    foreach (var spawnedEntity in tracked)
    {
        var p = spawnedEntity.Transform.Position;

        if (float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)) continue;

        Console.Error.WriteLine($"[probe] NON-FINITE pose at t={elapsed:F2}s spawned={spawned}: {p}");
        Console.Error.Flush();

        game.Exit();

        return;
    }

    for (var i = 0; i < perFrame && spawned < count; i++)
    {
        if (verbose)
        {
            Console.Error.WriteLine($"[probe] spawning #{spawned}");
            Console.Error.Flush();
        }

        var (spawnType, spawnVertices) = isMixed ? mixed[spawned % mixed.Length] : (type, vertices);

        var entity = game.Create2DPrimitive(spawnType, new()
        {
            Material = game.CreateFlatMaterial(random.NextColor()),
            Vertices = spawnVertices,
            Size = sides > 0 ? new Vector2(radius, sides) : null,
            Depth = depth,
            Component = bodyMode switch
            {
                "3d" => new BodyComponent { Collider = new CompoundCollider() },
                "probe" => new Temp2DProbe.ProbeBody { Collider = new CompoundCollider() },
                _ => null,
            },
        });

        entity.Transform.Position = new Vector3((random.NextSingle() - 0.5f) * spread, 14, 0);
        entity.Scene = scene;

        tracked.Add(entity);
        spawned++;
    }

    if (elapsed >= nextReport)
    {
        Report("tick");
        nextReport += 2.0;
    }

    if (elapsed >= seconds)
    {
        game.Exit();
    }
}
