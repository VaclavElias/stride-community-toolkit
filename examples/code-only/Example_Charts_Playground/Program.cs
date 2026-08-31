using Stride.CommunityToolkit.Charts;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Rendering.Lines;
using Stride.CommunityToolkit.Scripts;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.CommunityToolkit.Windows;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using System.Globalization;

// Playground for the chart helpers that are being grown here before moving into the toolkit.
// Lines/ and Charts/ are already in their final namespaces (Stride.CommunityToolkit.Rendering.Lines
// and Stride.CommunityToolkit.Charts), so extracting them later is a move, not a rewrite.
//
// Two looks, one chart API:
//   flat 2D  - orthographic camera, light background, no lighting or glow, major and minor grid,
//              labels that keep their pixel size while zooming. The compositor is created by hand
//              rather than through SetupBase2DScene so it can enable MSAA (thin lines flicker without
//              it) and a paper-like clear colour, and skip the physics ground a chart does not need.
//   glow 3D  - the default 3D scene with skybox and bloom; emissive intensity above 1 makes lines glow.
//
// Controls: G toggles the grid, T removes or restores the tan curve, L toggles the legend, Space
// throws the ball whose flight the trajectory records live; the mouse hovers a coordinate readout
// over the chart. The keys are listed in the DebugOverlay section so they share one screen block with
// the camera help (F2 collapses it, F3 moves it, F4 hides it).

// Without this a scaled-up 4K desktop hands the game a scaled, blurred window. A no-op off Windows.
WindowsDpiManager.EnablePerMonitorV2();

// Run with "--3d" for the glowing 3D look; a runtime switch rather than a const so neither branch is
// dead code to the compiler
var use3DScene = true; // args.Any(a => a.Equals("--3d", StringComparison.OrdinalIgnoreCase));

// "--zoom 61" starts zoomed out to that view height - handy for checking how the view-driven chart
// adapts its tick step, sampling density and line widths at a given zoom level
var zoomArg = 0f;
var zoomIndex = Array.FindIndex(args, a => a.Equals("--zoom", StringComparison.OrdinalIgnoreCase));
if (zoomIndex >= 0 && zoomIndex + 1 < args.Length) _ = float.TryParse(args[zoomIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out zoomArg);

using var game = new Game();

Chart? chart = null;
ChartSeries? tangent = null;
ChartTrajectory? trail = null;
ChartCursor? cursor = null;
ChartViewFollower? follower = null;
CameraComponent? camera = null;
Entity? ball = null;

// The thrown ball: launched from the lower left corner, integrated by hand each frame - the point of
// the demo is watching the trajectory series record a moving body, not the integrator
var launchPosition = new Vector2(-4.5f, -3.5f);
var launchVelocity = new Vector2(4f, 7f);
const float Gravity = 9.81f;
var ballPosition = Vector2.Zero;
var ballVelocity = Vector2.Zero;
var ballFlying = false;

game.Run(start: Start, update: Update);

void Start(Scene rootScene)
{
    game.Window.AllowUserResizing = true;
    game.Window.Title = "Charts Playground";

    if (use3DScene)
    {
        game.SetupBase3D();
        game.Add3DCameraController();
        //game.AddSkybox();
    }
    else
    {
        // What SetupBase2DScene does, minus the ground, plus MSAA and a light background
        game.Add2DGraphicsCompositor(clearColor: new Color(250, 250, 250), msaa: MultisampleCount.X4).AddUIStage();
        game.Add2DCamera();
        var cameraEntity = game.Add2DCameraController();
        var controller = cameraEntity.Get<Basic2DCameraController>()!;

        // Left-drag pans like a canvas app; wheel zoom is cursor-anchored, and rolling the wheel while
        // middle-dragging no longer zooms (both new controller behaviours)
        controller.MouseDragButton = MouseButton.Left;

        // The view-driven chart invites zooming far out
        controller.MaxOrthographicSize = 500f;

        if (zoomArg > 0f)
        {
            var camera2D = rootScene.Entities.Select(e => e.Get<CameraComponent>()).FirstOrDefault(c => c != null);
            if (camera2D is not null) camera2D.OrthographicSize = zoomArg;
        }
    }

    var options = use3DScene ? ChartOptions.Glow3D() : ChartOptions.Light2D();
    options.XMin = -5f;
    options.XMax = 5f;
    options.YMin = -4f;
    options.YMax = 4f;

    // Titles in the chart's label style: the chart title above the top edge, axis letters at the ends
    options.Title = "Charts Playground";
    options.XTitle = "x";
    options.YTitle = "y";
    options.ZTitle = "z";

    if (use3DScene)
    {
        // The 3D chart gets a real Z extent and a floor grid; curves that stay at z = 0 draw exactly
        // as they do on the flat chart
        options.ZMin = -3f;
        options.ZMax = 3f;
        options.GridPlanes = ChartGridPlanes.XY | ChartGridPlanes.XZ;
    }

    chart = Chart.Create(game, options);

    // In 3D the chart stands in the XY plane, lifted so most of it is above the ground; in 2D the
    // orthographic camera already looks at the origin
    chart.Root.Transform.Position = use3DScene ? new Vector3(0, 3f, 0) : Vector3.Zero;
    chart.Root.Scene = rootScene;

    // Curves added without options take the preset's width, glow and next palette colour
    chart.Plot(x => 2f * MathF.Sin(x), name: "sin");
    chart.Plot(x => 0.15f * x * x - 3f, name: "parabola");

    // The awkward ones: ln(x) is NaN left of zero, so the curve simply starts at the y axis; tan(x)
    // shoots past the chart's top and bottom and jumps across each asymptote, and is clipped to the
    // edges and cut into its branches rather than joined by a false vertical line
    chart.Plot(x => MathF.Log(x), name: "ln");
    tangent = chart.Plot(MathF.Tan, samples: 600, name: "tan");

    chart.PlotParametric(
        t => new Vector3(1.5f * MathF.Cos(t), 1.5f * MathF.Sin(t), 0f), 0f, MathUtil.TwoPi,
        new PolylineOptions { Width = options.CurveWidth, Color = options.CurvePalette[4], EmissiveIntensity = options.CurveEmissiveIntensity, Closed = true },
        samples: 96,
        name: "circle");

    if (use3DScene)
    {
        // A helix - a curve only a 3D chart can hold: x and y trace a circle while z advances
        chart.PlotParametric(
            t => new Vector3(2.5f * MathF.Cos(t), 2.5f * MathF.Sin(t), t / MathUtil.TwoPi - 2f),
            0f, 5f * MathUtil.TwoPi,
            samples: 400,
            name: "helix");
    }

    // The analytic flight path, thin and faint, so the recorded trail can be seen landing exactly on
    // the curve the equations predict: y = y0 + (vy/vx)(x - x0) - g (x - x0)^2 / (2 vx^2)
    chart.Plot(
        x => launchPosition.Y + launchVelocity.Y / launchVelocity.X * (x - launchPosition.X)
            - Gravity * (x - launchPosition.X) * (x - launchPosition.X) / (2f * launchVelocity.X * launchVelocity.X),
        new PolylineOptions
        {
            Width = options.CurveWidth * 0.5f,
            Color = use3DScene ? new Color(140, 140, 140) : new Color(180, 180, 180),
            EmissiveIntensity = 1f,
        },
        name: "ballistic");

    // The live trail the flying ball leaves behind; one point is appended per frame in Update
    trail = chart.AddTrajectory(capacity: 900, name: "throw");

    // Scatter: noisy measurements around the sin curve, drawn as one batched mesh of x markers - the
    // classic "data points versus fitted curve" picture. The jitter is a deterministic hash of x (the
    // classic shader one-liner), so the picture is identical every run without involving Random, and
    // the explicit colour keeps the palette rotation of the earlier series unchanged.
    var samples = new List<Vector3>();

    for (var x = -4.5f; x <= 4.5f; x += 0.45f)
    {
        var hash = MathF.Sin(x * 12.9898f) * 43758.5453f;
        var jitter = (hash - MathF.Floor(hash) - 0.5f) * 0.7f;
        samples.Add(new Vector3(x, 2f * MathF.Sin(x) + jitter, 0f));
    }

    chart.AddMarkers(samples, options: new PolylineOptions { Width = options.CurveWidth, Color = new Color(96, 66, 166), EmissiveIntensity = options.CurveEmissiveIntensity }, name: "samples");

    // The ball itself is a small closed ribbon circle moved along the flight path
    ball = game.CreatePolyline(
        PolylineSampling.Parametric(t => new Vector3(0.12f * MathF.Cos(t), 0.12f * MathF.Sin(t), 0f), 0f, MathUtil.TwoPi, 20),
        new PolylineOptions { Width = 0.05f, Color = use3DScene ? Color.White : new Color(40, 40, 40), Closed = true, EmissiveIntensity = use3DScene ? 2.5f : 1f },
        "ball");
    chart.Root.AddChild(ball);

    // The readout that follows the mouse; fed with the camera and cursor position every frame
    cursor = chart.AddCursor();

    // 2D only: the chart follows the camera, so the grid always fills the window - pan and zoom and
    // the axes, ticks, labels and curves re-target to whatever is visible (Desmos-style)
    if (!use3DScene)
    {
        follower = chart.FollowCamera();
    }

    // 3D: swap the fly controller for an orbit around the chart and frame the camera to fit the
    // window - the natural way to inspect a 3D figure
    if (use3DScene)
    {
        var cameraEntity3D = rootScene.Entities.FirstOrDefault(e => e.Get<CameraComponent>() != null);

        if (cameraEntity3D?.Get<CameraComponent>() is { } camera3D)
        {
            if (cameraEntity3D.Get<Basic3DCameraController>() is { } flyController)
            {
                cameraEntity3D.Remove(flyController);
            }

            cameraEntity3D.Add(new Basic3DOrbitCameraController { Target = chart.Root.Transform.Position });

            // A gently angled start, mostly facing the chart plane. The framing distance is dictated by
            // the box corner the frustum touches - the helix gives the box a real Z depth - so the
            // flatter the angle, the fuller the window; these angles keep it clearly 3D while filling
            // most of the frame. The orbit picks its initial angles up from this pose.
            cameraEntity3D.Transform.Rotation = Quaternion.RotationYawPitchRoll(
                MathUtil.DegreesToRadians(18f), MathUtil.DegreesToRadians(-12f), 0f);
            chart.FrameCamera(camera3D, padding: 0.02f);
        }
    }

    ThrowBall();

    // The overlay draws itself and is shared with the camera controller's help; the lambda is read
    // every frame, so the grid, series and trail state it shows is always current
    var overlay = DebugOverlay.GetOrCreate(game);

    // Debug text is 16 pixels tall at scale 1, which is tiny on a high-DPI display. Scale the whole
    // overlay by the monitor's DPI factor (2 on a 4K screen at 200%); the font is rasterised at the
    // resulting size, so any factor stays sharp
    overlay.Scale = MathF.Max(1f, WindowsDpiManager.GetPrimaryScale() ?? 1f);

    // The default box is Stride's 49% black, tuned for dark scenes; on paper white it needs to be darker
    if (!use3DScene)
    {
        overlay.BackgroundColor = new Color(0, 0, 0, 200);
        overlay.FontSize = 16;
        overlay.LineSpacing = 1;
    }

    overlay.AddSection("Chart", () =>
    [
        new("CHART"),
        new($"Press G to toggle the grid ({(chart.GridVisible ? "on" : "off")})", Color.Yellow),
        new($"Press T to {(tangent is null ? "restore" : "remove")} the tan curve", Color.Yellow),
        new($"Press L to toggle the legend ({(chart.LegendVisible ? "on" : "off")})", Color.Yellow),
        new($"Press Space to throw the ball (trail: {trail.Count}/{trail.Capacity} points)", Color.Yellow),
        new($"{chart.Series.Count} series: {string.Join(", ", chart.Series.Select(s => s.Name))}"),
    ]);
}

void Update(Scene scene, GameTime time)
{
    if (chart is null) return;

    if (game.Input.IsKeyPressed(Keys.G))
    {
        chart.GridVisible = !chart.GridVisible;
    }

    if (game.Input.IsKeyPressed(Keys.L))
    {
        chart.LegendVisible = !chart.LegendVisible;
    }

    // Remove frees the ribbon's GPU buffers; plotting again builds new ones. Cheap enough to do on a
    // key press, and the pattern a live re-plot (a parameter slider, say) would use every change
    if (game.Input.IsKeyPressed(Keys.T))
    {
        if (tangent is null)
        {
            tangent = chart.Plot(MathF.Tan, samples: 600, name: "tan");
        }
        else
        {
            chart.Remove(tangent);
            tangent = null;
        }
    }

    if (game.Input.IsKeyPressed(Keys.Space))
    {
        ThrowBall();
    }

    // The readout follows the mouse over the chart plane; hidden while the cursor is off the chart
    camera ??= scene.Entities.Select(e => e.Get<CameraComponent>()).FirstOrDefault(c => c != null);

    if (camera is not null)
    {
        follower?.Update(camera);
        cursor?.Update(camera, game.Input.MousePosition);
    }

    if (!ballFlying || trail is null || ball is null) return;

    // Semi-implicit Euler; capped so a stall (a dragged window) cannot teleport the ball
    var dt = MathF.Min((float)time.Elapsed.TotalSeconds, 0.1f);

    ballVelocity.Y -= Gravity * dt;
    ballPosition += ballVelocity * dt;

    ball.Transform.Position = new Vector3(ballPosition.X, ballPosition.Y, 0.05f);

    // The trajectory clips to the chart's ranges by itself; the trail simply ends at the edge
    trail.Add(new Vector3(ballPosition.X, ballPosition.Y, 0f));

    if (ballPosition.Y < chart.Options.YMin - 1f || ballPosition.X > chart.Options.XMax + 1f)
    {
        ballFlying = false;
    }
}

void ThrowBall()
{
    trail?.Clear();
    ballPosition = launchPosition;
    ballVelocity = launchVelocity;
    ballFlying = true;
}

/*
---example-metadata
slug: charts-playground
title:
  en: Charts Playground
level: Intermediate
category: Geometry
complexity: 3
order: 35
description:
  en: |-
    A sandbox for chart and plotting helpers: a chart with axes, tick marks, labels, a major and minor
    grid, a legend, a mouse coordinate readout, and function curves drawn as lines with real thickness.
    Two presets share one API - a flat, paper-like 2D chart under an orthographic camera with MSAA and
    pixel-sized labels, and a glowing 3D chart in a lit scene with bloom. Hardware lines are one pixel
    wide, so each line is a ribbon mesh built by PolylineMeshBuilder from sampled points. Curves are
    clipped to the chart, broken where a function is undefined or jumps across an asymptote, and can
    be removed again with their GPU buffers freed. A thrown ball records its flight into a growing
    trajectory that lands exactly on the plotted analytic curve. The helpers live in their final
    toolkit namespaces and will move into the library once their shape settles.
concepts:
  - Building a ribbon mesh from a list of points, and one mesh from many segments or runs
  - Sampling y = f(x) and parametric curves into points
  - "Clipping a polyline to a rectangle (Liang-Barsky) and splitting it at NaN and at asymptotes"
  - "A growing trajectory: pre-allocated Default-usage buffers updated in place, no per-frame allocations"
  - "A view-driven chart: ranges follow the camera, with 1-2-5 tick steps picked per zoom level"
  - "3D charts: a Z axis, box clipping and grid planes on the floor and walls, opt-in via ZMin/ZMax"
  - Chart and axis titles, and scatter markers batched into one mesh
  - Comparing a simulated flight path with the analytic ballistic curve on the same chart
  - "A mouse readout: intersecting the pick ray with the chart plane, no Stride UI needed"
  - A legend rebuilt from the live series list, with its ribbon buffers freed on every rebuild
  - "Emissive intensity above 1 plus bloom: glowing lines"
  - Why thin geometry flickers without MSAA, and enabling it on the 2D compositor
  - Screen-sized tick labels with EntityTextComponent versus world-sized ones with WorldTextComponent
  - Composing a 2D scene by hand instead of SetupBase2DScene
  - Removing a curve and disposing the vertex and index buffers nothing else tracks
  - Toggling a ModelComponent with a key listed in a DebugOverlay section
  - "Using helpers: Add2DGraphicsCompositor, Add2DCamera, Add2DCameraController, SetupBase3DScene, AddSkybox, DebugOverlay, GetPickRay"
tags:
  - 2D
  - 3D
  - Geometry
  - Mesh
  - Line
  - Chart
  - Physics
  - MSAA
  - Emissive
  - Bloom
*/