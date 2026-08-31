using Box2D.NET;
using Stride.CommunityToolkit.Box2D;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Instancing;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Games;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Shapes;

// A faithful replica of the Box2D.NET sample Benchmarks/BenchmarkJunkyard: a walled yard whose
// floor and walls are rows of overlapping static squares, 8,000 small five-sided "rocks" raining
// into it, and a kinematic pusher plowing back and forth through the pile at x = 60*sin(0.2t).
//
// The visuals copy the Box2D testbed's debug draw: every shape is a full-colour border around a
// fill at 0.6x the colour (its solid_polygon.fs shader does exactly that), static shapes are pale
// green, the kinematic pusher royal blue, awake dynamic bodies pink, sleeping ones gray, on the
// testbed's dark gray background. The border is baked into each Model as a slightly larger polygon
// behind the fill, so the rocks still render as a single instanced draw per sleep state.

// --- the sample's numbers, verbatim
const float GridSize = 1.0f;
const int ColumnCount = 200;
const int RowCount = 40;
const float Radius = 0.25f;
const float YStart = 15.0f;

// --- the testbed palette (b2HexColor values used by b2World_Draw, and the samples' GL clear colour)
var paleGreen = new Color(0x98, 0xFB, 0x98);
var pink = new Color(0xFF, 0xC0, 0xCB);
var gray = new Color(0x80, 0x80, 0x80);
var royalBlue = new Color(0x41, 0x69, 0xE1);
var background = new Color(0.2f, 0.2f, 0.2f);

Box2DSimulation? simulation = null;
Scene? scene = null;

BufferedEntityInstancing? awakeInstancing = null;
BufferedEntityInstancing? sleepingInstancing = null;
Entity? awakeMaster = null;
Entity? sleepingMaster = null;

// Sleep-state tracking, aligned across the three lists: which master each rock is drawn by right now
List<Entity> rocks = [];
List<B2BodyId> rockIds = [];
List<bool> awakeStates = [];

// The five-sided rock outline: the sample places five points on a circle by the Fibonacci sphere
// algorithm and takes their convex hull. The hull sorts them; the mesh needs them sorted too.
var pentagon = FibonacciPentagon(Radius);

using var game = new Game();

game.Run(start: Start, update: Update);

// The buffered instancings own their GPU buffers, and the engine never releases user-owned buffers.
// The simulation owns the native Box2D world - neither is tied to the scene's lifetime.
awakeInstancing?.Dispose();
sleepingInstancing?.Dispose();
simulation?.Dispose();

void Start(Scene rootScene)
{
    game.Window.AllowUserResizing = true;
    game.Window.Title = "Junkyard Box2D Example - Stride Community Toolkit";

    scene = rootScene;

    game.SetupBase2D(clearColor: background);
    game.Add2DCameraController();
    game.AddProfiler();

    // The sample's viewport: camera centered on (8, 25), zoom 60 - which in the testbed means the
    // visible world is 60 units tall
    var camera = rootScene.GetCamera() ?? throw new InvalidOperationException("Camera not found in scene");
    camera.Entity.Transform.Position = new Vector3(1.84f, 42.88f, 50);
    camera.OrthographicSize = 100;

    simulation = new Box2DSimulation();

    // The testbed steps the world exactly once per rendered frame - no catch-up. This scene is the
    // heaviest benchmark in the sample suite, and letting the accumulator run three catch-up steps
    // per frame would only deepen the slow motion it plays in on a loaded machine.
    simulation.MaxStepsPerFrame = 1;

    CreateGround();
    SetupRockInstancing();
    SpawnRocks();
    CreatePusher();
}

/// <summary>
/// One static body carrying every floor and wall shape: 161 slightly overlapping squares across the
/// bottom and 50 up each side wall at x = -80 and x = +80, exactly as the sample builds them.
/// </summary>
void CreateGround()
{
    var groundId = simulation!.CreateStaticBody(Vector3.Zero);
    var shapeDef = ShapeFixtureBuilder.CreateDefaultShapeDef();

    // The floor squares are 1.1 wide x 1.0 tall and the wall squares 1.0 x 1.1, so neighbours
    // overlap - the sample uses the overlap to avoid gaps between the individual boxes
    var floorModel = CreateOutlinedModel(RectangleVertices(0.55f * GridSize, 0.5f * GridSize), paleGreen, 0.08f);
    var wallModel = CreateOutlinedModel(RectangleVertices(0.5f * GridSize, 0.55f * GridSize), paleGreen, 0.08f);

    var floorInstancing = new EntityInstancing();
    var floorMaster = new Entity("FloorMaster")
    {
        new ModelComponent(floorModel),
        new InstancingComponent { Type = floorInstancing }
    };
    floorMaster.Scene = scene;

    var wallInstancing = new EntityInstancing();
    var wallMaster = new Entity("WallMaster")
    {
        new ModelComponent(wallModel),
        new InstancingComponent { Type = wallInstancing }
    };
    wallMaster.Scene = scene;

    var y = 0.0f;
    var x = -80.0f * GridSize;

    for (var i = 0; i < 161; ++i)
    {
        var box = b2MakeOffsetBox(0.55f * GridSize, 0.5f * GridSize, new B2Vec2(x, y), b2Rot_identity);
        b2CreatePolygonShape(groundId, in shapeDef, in box);
        AddStaticVisual(floorInstancing, x, y);
        x += GridSize;
    }

    y = GridSize;
    x = -80.0f * GridSize;

    for (var i = 0; i < 50; ++i)
    {
        var box = b2MakeOffsetBox(0.5f * GridSize, 0.55f * GridSize, new B2Vec2(x, y), b2Rot_identity);
        b2CreatePolygonShape(groundId, in shapeDef, in box);
        AddStaticVisual(wallInstancing, x, y);
        y += GridSize;
    }

    y = GridSize;
    x = 80.0f * GridSize;

    for (var i = 0; i < 50; ++i)
    {
        var box = b2MakeOffsetBox(0.5f * GridSize, 0.55f * GridSize, new B2Vec2(x, y), b2Rot_identity);
        b2CreatePolygonShape(groundId, in shapeDef, in box);
        AddStaticVisual(wallInstancing, x, y);
        y += GridSize;
    }
}

void AddStaticVisual(EntityInstancing instancing, float x, float y)
{
    // Static visuals carry no model of their own - the master draws them all in one call
    var entity = new Entity("StaticSquare");
    entity.Transform.Position = new Vector3(x, y, 0);
    entity.Scene = scene;
    instancing.AddInstance(entity);
}

void SetupRockInstancing()
{
    // Without this nothing instanced is drawn, and nothing warns you: the code-built compositor
    // wires up transform, skinning, material and lighting, but not instancing
    game.AddInstancingSupport();

    awakeInstancing = new BufferedEntityInstancing(new Box2DEntityInstancing());
    sleepingInstancing = new BufferedEntityInstancing(new Box2DEntityInstancing());

    awakeMaster = new Entity("AwakeMaster")
    {
        new ModelComponent(CreateOutlinedModel(pentagon, pink, 0.05f)),
        new InstancingComponent { Type = awakeInstancing }
    };

    sleepingMaster = new Entity("SleepingMaster")
    {
        new ModelComponent(CreateOutlinedModel(pentagon, gray, 0.05f)),
        new InstancingComponent { Type = sleepingInstancing }
    };

    awakeMaster.Scene = scene;
    sleepingMaster.Scene = scene;

    // Registers with the graphics compositor, not the scene, so it outlives anything in the scene
    game.AddInstancingBufferUpload(awakeInstancing);
    game.AddInstancingBufferUpload(sleepingInstancing);
}

/// <summary>
/// The junk: 200 columns x 40 rows of five-sided rocks stacked from y = 15 upward, each column
/// zig-zagged sideways by the alternating 0.1 offset, exactly as the sample spawns them.
/// </summary>
void SpawnRocks()
{
    var side = -0.1f;

    for (var i = 0; i < ColumnCount; ++i)
    {
        var x = 1.5f * (2.0f * i - ColumnCount) * Radius;

        for (var j = 0; j < RowCount; ++j)
        {
            var y = 4.0f * j * Radius + YStart;
            var position = new Vector3(x + side, y, 0);
            side = -side;

            var entity = new Entity("Rock");
            var bodyId = simulation!.CreateDynamicBody(entity, position);

            ShapeFixtureBuilder.AttachPolygon(pentagon, bodyId);

            // The instancing reads this component to skip all its work while every body is asleep
            entity.Add(new Box2DBodyComponent { BodyId = bodyId });

            entity.Transform.Position = position;

            awakeInstancing?.AddInstance(entity);
            entity.Scene = scene;

            rocks.Add(entity);
            rockIds.Add(bodyId);
            awakeStates.Add(true);
        }
    }
}

/// <summary>
/// The pusher: a kinematic 4 x 8 plow whose shape sits 4 units above its body origin. Its sweep is
/// driven per fixed step by <see cref="PusherDriver"/> through the simulation update hook, exactly
/// like the sample's StepJunkyard.
/// </summary>
void CreatePusher()
{
    var pusherEntity = new Entity("Pusher");
    var pusherId = simulation!.CreateKinematicBody(pusherEntity, Vector3.Zero);

    var shapeDef = ShapeFixtureBuilder.CreateDefaultShapeDef();
    var pusherBox = b2MakeOffsetBox(2.0f, 4.0f, new B2Vec2(0.0f, 4.0f), b2Rot_identity);
    b2CreatePolygonShape(pusherId, in shapeDef, in pusherBox);

    // The visual is a child at the shape's offset, so the synced body transform carries it along
    var visual = new Entity("PusherVisual")
    {
        new ModelComponent(CreateOutlinedModel(RectangleVertices(2.0f, 4.0f), royalBlue, 0.12f))
    };
    visual.Transform.Position = new Vector3(0, 4, 0);
    pusherEntity.AddChild(visual);

    pusherEntity.Scene = scene;

    simulation.RegisterSimulationUpdate(new PusherDriver(pusherId));
}

void Update(Scene rootScene, GameTime time)
{
    // Box2D is stepped by hand: fixed-timestep accumulation, the pusher drive and entity transform
    // sync all happen inside
    simulation?.Update(time.Elapsed);

    UpdateSleepTint();
}

/// <summary>
/// Moves rocks between the awake and sleeping masters as their sleep state changes, so the pile
/// shows where the engine has stopped simulating - the testbed's pink/gray colour convention.
/// </summary>
void UpdateSleepTint()
{
    for (var i = 0; i < rocks.Count; i++)
    {
        var awake = b2Body_IsAwake(rockIds[i]);

        if (awake == awakeStates[i]) continue;

        awakeStates[i] = awake;

        if (awake)
        {
            sleepingInstancing?.RemoveInstance(rocks[i]);
            awakeInstancing?.AddInstance(rocks[i]);
        }
        else
        {
            awakeInstancing?.RemoveInstance(rocks[i]);
            sleepingInstancing?.AddInstance(rocks[i]);
        }
    }

    // An instancing with zero instances falls back to drawing the master's model once, un-instanced,
    // at the master's own transform - a lone shape floating at the origin. Hide the master instead.
    if (awakeMaster != null && sleepingMaster != null)
    {
        awakeMaster.Get<ModelComponent>().Enabled = awakeInstancing!.RegisteredInstanceCount > 0;
        sleepingMaster.Get<ModelComponent>().Enabled = sleepingInstancing!.RegisteredInstanceCount > 0;
    }
}

/// <summary>
/// Builds a flat two-mesh model matching the testbed's solid-shape shader: a full-colour border
/// polygon behind a fill polygon at 0.6x the colour, the fill nudged forward so they never z-fight.
/// </summary>
Model CreateOutlinedModel(Vector2[] vertices, Color color, float borderThickness)
{
    var centroid = Vector2.Zero;

    foreach (var vertex in vertices) centroid += vertex;

    centroid /= vertices.Length;

    // Push every corner outward from the centroid by the border thickness
    var borderVertices = new Vector2[vertices.Length];

    for (var i = 0; i < vertices.Length; i++)
    {
        var direction = vertices[i] - centroid;
        var length = direction.Length();
        borderVertices[i] = centroid + direction * ((length + borderThickness) / length);
    }

    var fillData = PolygonProceduralModel.New(vertices);
    var borderData = PolygonProceduralModel.New(borderVertices);

    for (var i = 0; i < fillData.Vertices.Length; i++)
    {
        fillData.Vertices[i].Position.Z += 0.1f;
    }

    var fillColor = new Color((byte)(color.R * 0.6f), (byte)(color.G * 0.6f), (byte)(color.B * 0.6f));

    var model = new Model();
    model.Meshes.Add(new Mesh { Draw = new GeometricPrimitive(game.GraphicsDevice, borderData).ToMeshDraw(), MaterialIndex = 0 });
    model.Meshes.Add(new Mesh { Draw = new GeometricPrimitive(game.GraphicsDevice, fillData).ToMeshDraw(), MaterialIndex = 1 });
    model.Materials.Add(game.CreateFlatMaterial(color));
    model.Materials.Add(game.CreateFlatMaterial(fillColor));

    return model;
}

static Vector2[] RectangleVertices(float halfWidth, float halfHeight) =>
[
    new(-halfWidth, -halfHeight),
    new(halfWidth, -halfHeight),
    new(halfWidth, halfHeight),
    new(-halfWidth, halfHeight),
];

/// <summary>
/// The sample's rock outline: five points on a circle of the given radius by the Fibonacci sphere
/// algorithm, sorted by angle so both the convex hull and the fan-triangulated mesh see them in order.
/// </summary>
static Vector2[] FibonacciPentagon(float radius)
{
    var phi = MathF.PI * (MathF.Sqrt(5.0f) - 1.0f);
    var points = new Vector2[5];

    for (var i = 0; i < 5; ++i)
    {
        var theta = phi * i;
        points[i] = new Vector2(radius * MathF.Cos(theta), radius * MathF.Sin(theta));
    }

    return [.. points.OrderBy(p => MathF.Atan2(p.Y, p.X))];
}

/// <summary>
/// Drives the kinematic pusher along the sample's sweep, x = 60*sin(0.2t), by setting its target
/// transform once per fixed step - Box2D derives the kinematic velocity from target and time step.
/// </summary>
public sealed class PusherDriver : IBox2DSimulationUpdate
{
    private readonly B2BodyId _pusherId;
    private int _stepCount;

    public PusherDriver(B2BodyId pusherId) => _pusherId = pusherId;

    public void SimulationUpdate(Box2DSimulation simulation, float deltaTime)
    {
        var time = deltaTime * _stepCount;
        _stepCount++;

        var cosSin = b2ComputeCosSin(0.2f * time);
        var target = new B2Transform(new B2Vec2(60.0f * cosSin.sine, 0.0f), b2Rot_identity);

        b2Body_SetTargetTransform(_pusherId, in target, deltaTime, true);
    }

    public void AfterSimulationUpdate(Box2DSimulation simulation, float deltaTime)
    {
    }
}

/*
---example-metadata
slug: junkyard-box2d
title:
  en: Junkyard (Box2D)
  cs: Vrakoviště (Box2D)
level: Advanced
category: Performance
complexity: 4
order: 62
description:
  en: |-
    A faithful replica of the Box2D.NET BenchmarkJunkyard sample: 8,000 small five-sided rocks rain
    into a walled yard and a kinematic plow sweeps back and forth through the pile, driven by a
    target transform once per fixed step. The visuals copy the Box2D testbed's debug draw - pale
    green statics, pink awake bodies that turn gray when they sleep, a royal blue pusher - with the
    border-and-fill shape style baked into two-mesh models so the rocks still render as one
    instanced draw per sleep state.
  cs: |-
    Věrná replika ukázky BenchmarkJunkyard z Box2D.NET: 8 000 malých pětiúhelníkových kamenů prší
    do ohrazeného dvora a kinematická radlice se prohrnuje hromadou tam a zpět. Vizuál kopíruje
    debug draw z Box2D testbedu - světle zelená statika, růžová bdící tělesa šednoucí ve spánku,
    královsky modrá radlice.
concepts:
  - Replicating a Box2D testbed benchmark scene in Stride
  - Driving a kinematic body with SetTargetTransform once per fixed step
  - Hooking per-fixed-step logic through IBox2DSimulationUpdate
  - One static body carrying hundreds of fixtures, drawn instanced
  - Custom convex polygon fixtures from the same vertices as the rendered mesh
  - Baking the testbed border-and-fill look into a two-mesh Model, instancing-friendly
  - Tinting sleeping bodies by moving instances between an awake and a sleeping master
tags:
  - 2D
  - Box2D
  - Physics
  - Instancing
  - Performance
  - Kinematic
  - Third Party
related:
  - Example01_Basic2DScene_StressPile_Box2D
  - Example18_Box2DPhysics
enabled: true
screenshotFrame: 600
created: 2026-08-31
---
*/
