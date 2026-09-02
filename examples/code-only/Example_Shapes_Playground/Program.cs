using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.CommunityToolkit.Shapes;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

// A tour of ShapeBatch in 3D. Every shape here is flat - a polygon evaluated per fragment as a
// signed distance function - but flat shapes turn out to cover a lot of ground once they can sit on
// any plane, face the camera, or swing about an axis:
//
//   discs and rings on the floor      area-of-effect and selection markers
//   decals                            flat art laid onto the ground
//   panels                            rectangles standing in the world
//   lines and wire boxes              a capsule swung to face the camera is a thick 3D line
//   billboards                        markers that stay the same shape from any angle
//
// The point of all of it is the outline. It is a fixed number of PIXELS wide no matter how far away
// the shape is, because the shader measures it per fragment against the fragment's own clip w
// rather than building it as geometry. Press 7 and fly down the corridor of rings: they shrink with
// distance, their outlines do not.

const int PillarCount = 6;
const float PillarRing = 12f;
const float GroundLift = 0.02f;
const int DemoCount = 7;

var demoNames = new[]
{
    "Ground discs (area of effect)",
    "Selection rings",
    "Decals",
    "Panels on a plane",
    "Thick 3D lines and wire boxes",
    "Camera-facing billboards",
    "Distance proof (a corridor of rings)",
};

// Two batches, so the depth toggle can show the same shapes as scene geometry or as an overlay
ShapeBatch? sceneShapes = null;
ShapeBatch? overlayShapes = null;

var enabled = new bool[DemoCount];
Array.Fill(enabled, true);

var pillars = new Entity[PillarCount];
var pillarHeights = new float[PillarCount];

var depthTested = true;
var borderWidth = 3f;
var fillAlpha = 0.45f;
var submitted = 0;

using var game = new Game();

game.Run(start: Start, update: Update);

void Start(Scene rootScene)
{
    game.Window.AllowUserResizing = true;
    game.Window.Title = "Shapes Playground - Stride Community Toolkit";

    game.SetupBase3D();
    game.Add3DCameraController();
    game.AddSkybox();
    game.AddProfiler();

    // Yaw, pitch, roll in degrees: looking down at the arena from behind it
    game.SetCameraPosition(new Vector3(0, 20, 34));
    game.SetCameraRotation(new Vector3(0, -26, 0));

    // Depth-tested: scene geometry occludes these, so a disc on the floor goes behind a pillar.
    // Overlay: drawn on top of everything, which is what you want for gizmos and debug marks.
    sceneShapes = game.AddShapeBatch(depthTest: true);
    overlayShapes = game.AddShapeBatch(depthTest: false);

    BuildScene(rootScene);

    DebugOverlay.GetOrCreate(game).AddSection("Shapes", BuildOverlayLines);
}

void BuildScene(Scene scene)
{
    // Dark and matte, so the shapes read against it instead of fighting a specular hotspot
    var groundMaterial = game.CreateMaterial(new Color(38, 41, 47), specular: 0.04f, microSurface: 0.25f);

    var ground = game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions
    {
        EntityName = "Ground",
        Material = groundMaterial,
        Size = new Vector3(70, 0.5f, 200),
        Position = new Vector3(0, -0.25f, -60),
    });

    ground.Scene = scene;

    var pillarMaterial = game.CreateMaterial(new Color(96, 103, 116), specular: 0.1f, microSurface: 0.35f);

    // A ring of pillars: something for the markers to sit under and the lines to reach for, and
    // something solid for the depth toggle to hide shapes behind
    for (var i = 0; i < PillarCount; i++)
    {
        var angle = i * MathF.Tau / PillarCount;
        var height = 3f + i * 0.9f;

        pillarHeights[i] = height;

        var pillar = game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions
        {
            EntityName = $"Pillar {i}",
            Material = pillarMaterial,
            Size = new Vector3(1.8f, height, 1.8f),
            Position = new Vector3(MathF.Cos(angle) * PillarRing, height * 0.5f, MathF.Sin(angle) * PillarRing),
        });

        pillar.Scene = scene;
        pillars[i] = pillar;
    }
}

void Update(Scene scene, GameTime gameTime)
{
    HandleInput();

    var shapes = depthTested ? sceneShapes : overlayShapes;

    if (shapes is null) return;

    // Current state, captured by each draw call as it is made
    shapes.BorderWidth = borderWidth;
    shapes.FillAlpha = fillAlpha;

    var before = shapes.Count;
    var seconds = (float)gameTime.Total.TotalSeconds;

    if (enabled[0]) DrawGroundDiscs(shapes, seconds);
    if (enabled[1]) DrawSelectionRings(shapes);
    if (enabled[2]) DrawDecals(shapes, seconds);
    if (enabled[3]) DrawPanels(shapes);
    if (enabled[4]) DrawLines(shapes, seconds);
    if (enabled[5]) DrawBillboards(shapes, seconds);
    if (enabled[6]) DrawDistanceProof(shapes);

    submitted = shapes.Count - before;
}

/// <summary>
/// Pulsing filled discs lying on the floor: the shape every game needs for an area of effect, a
/// spawn point or a capture zone. A disc is one vertex plus a radius, so it is analytically round -
/// no tessellation to give it away up close.
/// </summary>
void DrawGroundDiscs(ShapeBatch shapes, float seconds)
{
    for (var i = 0; i < PillarCount; i++)
    {
        var pillar = pillars[i].Transform.Position;
        var pulse = 2.6f + MathF.Sin(seconds * 1.6f + i * 0.9f) * 0.7f;

        shapes.DrawDisc(new Vector3(pillar.X, GroundLift, pillar.Z), Vector3.UnitY, pulse, Color.OrangeRed);
    }
}

/// <summary>
/// Unfilled rings, which is the same shape with the fill turned off - a selection marker that does
/// not tint what it encircles. The arena boundary shows the width holding at a large radius.
/// </summary>
void DrawSelectionRings(ShapeBatch shapes)
{
    foreach (var pillar in pillars)
    {
        var position = pillar.Transform.Position;

        shapes.DrawRing(new Vector3(position.X, GroundLift, position.Z), Vector3.UnitY, 1.9f, Color.Cyan);
    }

    shapes.DrawRing(new Vector3(0, GroundLift, 0), Vector3.UnitY, PillarRing + 6f, Color.DeepSkyBlue);
}

/// <summary>
/// Flat art laid onto the ground. A hexagon landing pad with four tiles turning slowly around it -
/// arbitrary polygons on an arbitrary plane, which is all a decal really is.
/// </summary>
void DrawDecals(ShapeBatch shapes, float seconds)
{
    ReadOnlySpan<Vector2> hexagon =
    [
        new(5f, 0f), new(2.5f, 4.33f), new(-2.5f, 4.33f),
        new(-5f, 0f), new(-2.5f, -4.33f), new(2.5f, -4.33f),
    ];

    // Lying flat means the polygon's own X and Y axes map to the world's X and Z
    shapes.DrawSolidPolygon(hexagon, new Vector3(0, GroundLift, 0), Vector3.UnitX, Vector3.UnitZ, Color.MediumPurple);

    for (var i = 0; i < 4; i++)
    {
        var angle = seconds * 0.4f + i * MathF.PI * 0.5f;
        var (sin, cos) = MathF.SinCos(angle);
        var position = new Vector3(cos * 7.5f, GroundLift, sin * 7.5f);

        // The tile's axes turn with it, so the square rolls around the pad rather than sliding
        shapes.DrawRectangle(position, new Vector3(cos, 0, sin), new Vector3(-sin, 0, cos), new Vector2(2.2f, 2.2f), Color.Violet, cornerRadius: 0.35f);
    }
}

/// <summary>
/// Rectangles standing upright in the world, facing outward from the centre - a sign, a screen, a
/// portal. Rounded corners come free: the rounding radius is the same term that makes a capsule.
/// </summary>
void DrawPanels(ShapeBatch shapes)
{
    // Fill and outline are independent colours: a dark panel with a light edge, which deriving the
    // fill from the outline colour cannot produce
    shapes.FillColor = new Color(16, 28, 52);

    for (var i = 0; i < 4; i++)
    {
        var angle = i * MathF.PI * 0.5f + MathF.PI * 0.25f;
        var (sin, cos) = MathF.SinCos(angle);

        // Upright: the panel's Y is the world's up, its X the tangent around the circle
        var tangent = new Vector3(-sin, 0, cos);
        var position = new Vector3(cos * 17f, 3.4f, sin * 17f);

        shapes.DrawRectangle(position, tangent, Vector3.UnitY, new Vector2(6f, 3.6f), Color.LightSkyBlue, cornerRadius: 0.5f);
    }

    shapes.FillColor = null;
}

/// <summary>
/// Thick 3D lines and a wire box. Hardware line rendering clamps to one pixel on most drivers; these
/// are capsules swung about their own axis to face the camera, so the width is real and holds up
/// close. The box is the twelve edges drawn as twelve lines.
/// </summary>
void DrawLines(ShapeBatch shapes, float seconds)
{
    var hub = new Vector3(0, 9.5f, 0);
    var width = 0.14f + MathF.Sin(seconds * 2f) * 0.05f;

    for (var i = 0; i < PillarCount; i++)
    {
        var pillar = pillars[i].Transform.Position;
        var top = new Vector3(pillar.X, pillarHeights[i], pillar.Z);

        shapes.DrawLine(hub, top, width, Color.Gold);
    }

    // Pixel-width rails running to the horizon: the same thickness near and far, where the thick
    // world-space lines above visibly taper with distance
    shapes.DrawPixelLine(new Vector3(-22, 0.5f, 5f), new Vector3(-22, 0.5f, -150f), 2f, Color.White);
    shapes.DrawPixelLine(new Vector3(22, 0.5f, 5f), new Vector3(22, 0.5f, -150f), 2f, Color.White);

    // A selection volume around the tallest pillar
    var tallest = pillars[PillarCount - 1].Transform.Position;
    var tallestHeight = pillarHeights[PillarCount - 1];

    shapes.DrawWireBox(new Vector3(tallest.X, tallestHeight * 0.5f, tallest.Z), new Vector3(2.6f, tallestHeight + 0.8f, 2.6f), 0.08f, Color.Yellow);
}

/// <summary>
/// Camera-facing markers. A billboard keeps its shape and its screen orientation from every angle,
/// which is what you want for a waypoint or a unit marker - fly around and they never foreshorten.
/// </summary>
void DrawBillboards(ShapeBatch shapes, float seconds)
{
    var bob = MathF.Sin(seconds * 2f) * 0.25f;

    // A coloured fill inside a neutral outline: the chart-marker case, readable against any
    // background because the ring never takes the series colour
    shapes.FillColor = Color.LimeGreen;

    for (var i = 0; i < PillarCount; i++)
    {
        var pillar = pillars[i].Transform.Position;
        var above = new Vector3(pillar.X, pillarHeights[i] + 1.6f + bob, pillar.Z);

        shapes.DrawBillboardCircle(above, 0.45f, Color.White);
    }

    shapes.FillColor = null;

    // A diamond over the hub: any polygon can be billboarded, not just circles
    ReadOnlySpan<Vector2> diamond = [new(0.9f, 0f), new(0f, 0.9f), new(-0.9f, 0f), new(0f, -0.9f)];

    shapes.DrawBillboard(diamond, new Vector3(0, 11.5f + bob, 0), Color.GreenYellow);
}

/// <summary>
/// The headline: identical rings marching away from the camera. They shrink, their outlines do not.
/// Geometry-based outlines cannot do this - a ring of triangles thins to nothing with distance.
/// </summary>
void DrawDistanceProof(ShapeBatch shapes)
{
    for (var i = 0; i < 12; i++)
    {
        shapes.DrawRing(new Vector3(0, 2.4f, -10f - i * 13f), Vector3.UnitZ, 2f, Color.HotPink);
    }
}

void HandleInput()
{
    for (var i = 0; i < DemoCount; i++)
    {
        if (game.Input.IsKeyPressed(Keys.D1 + i)) enabled[i] = !enabled[i];
    }

    if (game.Input.IsKeyPressed(Keys.T)) depthTested = !depthTested;

    if (game.Input.IsKeyPressed(Keys.F))
    {
        fillAlpha = fillAlpha switch
        {
            < 0.1f => 0.25f,
            < 0.3f => 0.45f,
            < 0.5f => 0.7f,
            _ => 0f,
        };
    }

    if (game.Input.IsKeyPressed(Keys.OemPlus) || game.Input.IsKeyPressed(Keys.Add))
        borderWidth = MathF.Min(borderWidth + 1f, 16f);

    if (game.Input.IsKeyPressed(Keys.OemMinus) || game.Input.IsKeyPressed(Keys.Subtract))
        borderWidth = MathF.Max(borderWidth - 1f, 0f);
}

IReadOnlyList<TextElement> BuildOverlayLines()
{
    List<TextElement> lines =
    [
        new($"{submitted} shapes, one instanced draw call", Color.LightGreen),
        new($"Border {borderWidth:0} px (+/-)   Fill {fillAlpha:0.00} (F)", Color.MediumSeaGreen),
        new(depthTested ? "T - depth tested: the scene occludes shapes" : "T - overlay: shapes draw on top", Color.Gold),
        new(""),
    ];

    for (var i = 0; i < DemoCount; i++)
    {
        lines.Add(new($"{i + 1} {(enabled[i] ? "[x]" : "[ ]")} {demoNames[i]}", enabled[i] ? Color.White : Color.Gray));
    }

    return lines;
}

/*
---example-metadata
slug: shapes-playground
title:
  en: Shapes Playground
  cs: Hřiště s tvary
level: Intermediate
category: Rendering
complexity: 3
order: 160
description:
  en: |-
    The full tour of ShapeBatch in 3D: ground discs and selection rings, decals, panels standing on a
    plane, genuinely thick 3D lines and wire boxes, and camera-facing billboards. Every shape is flat
    and evaluated per fragment as a signed distance function, so its outline stays a constant number
    of pixels wide however far away it is - press 7 and fly down the corridor of rings to see it.
  cs: |-
    Kompletní ukázka ShapeBatch ve 3D: kotouče a výběrové kroužky na zemi, dekaly, panely stojící v
    rovině, opravdu silné 3D čáry a drátěné kvádry a billboardy natočené ke kameře. Každý tvar je
    plochý a počítaný per fragment jako signed distance function, takže jeho obrys má stále stejnou
    šířku v pixelech bez ohledu na vzdálenost.
concepts:
  - Registering a shape renderer with AddShapeBatch
  - Depth-tested shapes versus overlay shapes from two batches
  - Discs, rings and polygons lying on an arbitrary plane in 3D
  - Thick 3D lines and wire boxes from camera-facing capsules
  - Billboards that keep their shape from any viewpoint
  - Why a signed distance function keeps an outline a constant pixel width
tags:
  - 3D
  - Rendering
  - Shapes
  - ShapeBatch
  - Gizmos
  - Decals
  - Billboard
  - SDF
  - Shader
related:
  - Example08_DebugShapes
  - Example02_Junkyard_Playground_Box2D
  - Example18_Box2DPhysics
tocName: Shapes playground
enabled: true
created: 2026-08-31
---
*/