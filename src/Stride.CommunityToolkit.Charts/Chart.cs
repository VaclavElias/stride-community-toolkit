using Stride.CommunityToolkit.Charts.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A chart in the XY plane (optionally with a Z extent): axes, tick marks, tick labels, titles, an optional
/// major and minor grid, a legend, and any number of plotted curves - all children of one <see cref="Root"/>
/// entity, so the whole chart can be placed, rotated and scaled in the world as a single object.
/// </summary>
/// <remarks>
/// The chart itself is a façade over three parts, each with one responsibility: <see cref="ChartScaffold"/>
/// (axes, ticks, labels, titles), <see cref="ChartGrid"/> (the textured grid planes) and
/// <see cref="ChartLegend"/>. Every line is a ribbon mesh from <see cref="PolylineMeshBuilder"/>, so it has
/// real thickness. Construct a chart, add its <see cref="Root"/> to a scene, then
/// call <see cref="Plot"/> for each curve. <see cref="Dispose"/> frees everything the chart owns.
/// </remarks>
public sealed class Chart : IDisposable
{
    // Every ribbon lies in the chart plane, so coplanar ones z-fight where they cross and flicker dark
    // fringes. Each layer is nudged along Z by this much: grids behind the axes, ticks and curves in front.
    internal const float LayerStep = 0.005f;

    private readonly List<ChartSeries> _series = [];
    private readonly List<ChartCursor> _cursors = [];
    private readonly ChartScaffold _scaffold;
    private readonly ChartLegend _legend;
    private readonly ChartGrid _grid;
    private ChartViewFollower? _follower;

    // The view height the chart was created with; ViewScale compares the current height against it
    private readonly float _referenceHeight;
    private readonly float _referencePixelHeight;
    private bool _isDisposed;

    /// <summary>The entity every part of the chart is parented to. Add it to a scene and move it to place the chart.</summary>
    public Entity Root { get; }

    /// <summary>The settings the chart was created with.</summary>
    public ChartOptions Options { get; }

    /// <summary>Whether the chart has a real Z extent - axes, clipping and grids gain the third dimension.</summary>
    public bool Is3D => Options.Range.ZMax > Options.Range.ZMin;

    /// <summary>The curves on the chart, in the order they were added.</summary>
    public IReadOnlyList<ChartSeries> Series => _series;

    /// <summary>Shows or hides the major and minor grid. Cheap to toggle every frame; nothing is rebuilt.</summary>
    public bool GridVisible
    {
        get => _grid.Visible;
        set => _grid.Visible = value;
    }

    /// <summary>
    /// Shows or hides the legend without rebuilding it. The legend itself appears only while
    /// <see cref="ChartLegendOptions.Visible"/> is on and the chart has at least one series.
    /// </summary>
    public bool LegendVisible
    {
        get => _legend.Visible;
        set => _legend.Visible = value;
    }

    /// <summary>
    /// The emissive intensity every series is drawn with. Above <c>1</c> the curves glow through the
    /// compositor's bloom; the higher it goes the more they bleed into the scene around them.
    /// </summary>
    /// <remarks>
    /// Setting it pushes the new value straight into the material of every series already on the chart -
    /// no mesh is rebuilt, so it is cheap enough to animate - and updates
    /// <see cref="ChartSeriesOptions.EmissiveIntensity"/> so series added afterwards match. Turning the
    /// grid off at the same time is what makes a chart read as a glowing figure rather than a diagram.
    /// </remarks>
    public float Glow
    {
        get => Options.Series.EmissiveIntensity;
        set
        {
            Options.Series.EmissiveIntensity = value;

            foreach (var series in _series)
            {
                series.SetEmissiveIntensity(value);
            }
        }
    }

    /// <summary>The game the chart draws in - meshes are built on its device, on the game thread.</summary>
    internal Game Game { get; }

    /// <summary>
    /// How much taller the current view is than the one the chart was created with, corrected for the
    /// window's pixel height. Ribbon widths, tick lengths and the legend layout are multiplied by this
    /// whenever they are rebuilt, so a view-driven chart keeps its lines the same thickness on screen at
    /// every zoom level. Static geometry that is never rebuilt keeps its world-unit width.
    /// </summary>
    internal float ViewScale => (Options.Range.YMax - Options.Range.YMin) / _referenceHeight
        * (_referencePixelHeight / MathF.Max(1f, Game.GraphicsDevice.Presenter.BackBuffer.Height));

    /// <summary>
    /// Builds a chart. The <see cref="Root"/> is not added to a scene; do that where you want it.
    /// </summary>
    /// <param name="game">The game the chart is drawn in.</param>
    /// <param name="options">What the chart shows and how it is drawn; <see langword="null"/> for <see cref="ChartOptions.Glow3D"/>.</param>
    /// <param name="name">The root entity's name, or <c>"Chart"</c>.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="game"/> is <see langword="null"/>.</exception>
    public Chart(Game game, ChartOptions? options = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        options ??= ChartOptions.Glow3D();

        if (options.Labels.Visible)
        {
            ChartScaffold.EnsureTextRenderer(game, options.Labels.Mode);
        }

        Game = game;
        Options = options;
        _referenceHeight = options.Range.YMax - options.Range.YMin;
        _referencePixelHeight = game.GraphicsDevice.Presenter.BackBuffer.Height;
        Root = new Entity(name ?? "Chart");

        _scaffold = new ChartScaffold(game, this);
        _legend = new ChartLegend(game, this);
        _grid = new ChartGrid(game, this);

        _grid.Create();
        _scaffold.Build();
        _grid.Update();
    }

    /// <summary>
    /// Plots <c>y = f(x)</c> across the chart's <c>x</c> range. Samples outside the <c>y</c> range are clipped
    /// to the chart edge, samples that are not finite (a function outside its domain) break the curve, and so
    /// does a zero-crossing jump larger than a quarter of the chart's height between two samples - the
    /// asymptotes of <c>tan(x)</c> or <c>1/x</c> - where the branches are instead extended to the chart edge.
    /// </summary>
    /// <param name="f">The function to plot.</param>
    /// <param name="color">The curve's colour; <see langword="null"/> takes the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="samples">How many points to sample; more is smoother.</param>
    /// <param name="style">Width and glow where they should differ from the chart's defaults.</param>
    /// <returns>The curve, already on the chart; keep it to animate it with <see cref="ChartCurve.SetFunction"/> or to <see cref="Remove"/> it later.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="f"/> is <see langword="null"/>.</exception>
    public ChartCurve Plot(Func<float, float> f, Color? color = null, string? name = null, int samples = 200, ChartSeriesStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(f);

        var points = PolylineSampling.Function(f, Options.Range.XMin, Options.Range.XMax, samples);
        var branches = PolylineClipping.SplitAtJumps(points, (Options.Range.YMax - Options.Range.YMin) * 0.25f, extendEnds: true);

        var runs = new List<IReadOnlyList<Vector3>>();
        foreach (var branch in branches)
        {
            runs.AddRange(Clip(branch));
        }

        var seriesName = name ?? $"Plot {_series.Count + 1}";
        var options = ResolveStyle(style, color, closed: false);
        var entity = BuildSeriesEntity(runs, options, seriesName, closed: false);

        // The sample density is remembered so a view-driven chart can re-sample at the same detail per unit
        var spec = new CurveSpec(f, samples, samples / (Options.Range.XMax - Options.Range.XMin));

        return Register(new ChartCurve(this, seriesName, entity, options, runs.Count == 0, spec));
    }

    /// <summary>
    /// Plots a parametric curve <c>p(t)</c>, clipped to the chart's ranges.
    /// </summary>
    /// <param name="p">The curve; its <c>z</c> is kept, so the curve may leave the chart plane.</param>
    /// <param name="from">The first <c>t</c>.</param>
    /// <param name="to">The last <c>t</c>.</param>
    /// <param name="color">The curve's colour; <see langword="null"/> takes the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="samples">How many points to sample; more is smoother.</param>
    /// <param name="closed">Whether to join the last point back to the first - a circle, an ellipse, any loop.</param>
    /// <param name="style">Width and glow where they should differ from the chart's defaults.</param>
    /// <returns>The series, already on the chart; keep it to <see cref="Remove"/> the curve later.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="p"/> is <see langword="null"/>.</exception>
    public ChartSeries PlotParametric(Func<float, Vector3> p, float from, float to, Color? color = null, string? name = null, int samples = 200, bool closed = false, ChartSeriesStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(p);

        var points = PolylineSampling.Parametric(p, from, to, samples);

        return AddLine(points, color, name ?? $"Plot {_series.Count + 1}", closed, clip: true, style);
    }

    /// <summary>
    /// Adds a line through arbitrary points - measured data, a trajectory, a hand-drawn shape.
    /// </summary>
    /// <param name="points">The points, in chart units.</param>
    /// <param name="color">The line's colour; <see langword="null"/> takes the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="closed">Whether to join the last point back to the first.</param>
    /// <param name="clip">
    /// Whether to cut the line to the chart's ranges. <see langword="true"/> (the default) also breaks the line at
    /// points that are not finite; <see langword="false"/> only does the latter and lets the line leave the chart.
    /// </param>
    /// <param name="style">Width and glow where they should differ from the chart's defaults.</param>
    /// <returns>The series, already on the chart; keep it to <see cref="Remove"/> the line later.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="points"/> is <see langword="null"/>.</exception>
    public ChartSeries AddLine(IReadOnlyList<Vector3> points, Color? color = null, string? name = null, bool closed = false, bool clip = true, ChartSeriesStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        var (runs, keepClosed) = ComputeLineRuns(points, closed, clip);

        var seriesName = name ?? $"Line {_series.Count + 1}";
        var options = ResolveStyle(style, color, keepClosed);
        var entity = BuildSeriesEntity(runs, options, seriesName, keepClosed);

        // Remembered so a view-driven chart can re-clip the line when the visible range changes
        return Register(new ChartSeries(seriesName, entity, options, runs.Count == 0)
        {
            SourcePoints = points,
            ClipSource = clip,
        });
    }

    /// <summary>
    /// Adds an empty trajectory: a curve that grows one point at a time - the path of a moving body, drawn
    /// while it moves. Feed it from your update loop with <see cref="ChartTrajectory.Add"/>; points are
    /// clipped to the chart's ranges the same way <see cref="Plot"/> clips a function.
    /// </summary>
    /// <param name="capacity">The most points the trail can hold; the GPU buffers are allocated once, for this many.</param>
    /// <param name="color">The trail's colour; <see langword="null"/> takes the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="rollOver">What a full trail does with the next point: <see langword="false"/> ignores it, <see langword="true"/> drops the oldest - an oscilloscope trace.</param>
    /// <param name="style">Width and glow where they should differ from the chart's defaults.</param>
    /// <returns>The trajectory, already on the chart and empty; it is also in <see cref="Series"/> and removed like any other curve.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="capacity"/> is less than two.</exception>
    public ChartTrajectory AddTrajectory(int capacity = 1000, Color? color = null, string? name = null, bool rollOver = false, ChartSeriesStyle? style = null)
    {
        var options = ResolveStyle(style, color, closed: false);

        // Bounds grow with the points: a view-driven chart can widen its ranges after the trail starts,
        // so pinning them to the creation-time ranges could get the mesh wrongly culled
        var line = new GrowingPolyline(Game, capacity, options) { RollOver = rollOver };

        var seriesName = name ?? $"Trajectory {_series.Count + 1}";
        var entity = Game.CreatePolylineEntity(line.Mesh, options, seriesName);
        entity.Transform.Position = new Vector3(0f, 0f, 2f * LayerStep);
        Root.AddChild(entity);

        return Register(new ChartTrajectory(seriesName, entity, options, line, Options));
    }

    /// <summary>
    /// Adds scatter markers: one small × per point, all batched into a single mesh. Points outside the
    /// chart's ranges are dropped, and on a view-driven chart the markers keep their on-screen size and are
    /// re-filtered when the range changes.
    /// </summary>
    /// <param name="points">The data points, in chart units.</param>
    /// <param name="color">The markers' colour; <see langword="null"/> takes the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="size">The marker's diagonal extent in chart units; <see langword="null"/> takes <see cref="ChartSeriesOptions.MarkerSize"/>.</param>
    /// <param name="style">Ribbon width and glow where they should differ from the chart's defaults.</param>
    /// <returns>The series, already on the chart; remove it like any other.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="points"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="size"/> is not positive.</exception>
    public ChartSeries AddMarkers(IReadOnlyList<Vector3> points, Color? color = null, string? name = null, float? size = null, ChartSeriesStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        var markerSize = size ?? Options.Series.MarkerSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(markerSize);

        var options = ResolveStyle(style, color, closed: false);
        var seriesName = name ?? $"Markers {_series.Count + 1}";
        var entity = new Entity(seriesName);
        entity.Transform.Position = new Vector3(0f, 0f, 2f * LayerStep);
        Root.AddChild(entity);

        var series = new ChartSeries(seriesName, entity, options, isEmpty: false)
        {
            MarkerPoints = points,
            MarkerSize = markerSize,
        };

        _series.Add(series);
        series.RebuildMarkerModel(this);
        _legend.Rebuild();

        return series;
    }

    /// <summary>
    /// Shades the region between <c>y = f(x)</c> and a horizontal baseline over a stretch of <c>x</c> - the
    /// picture of a definite integral. The fill is translucent and drawn behind the curves.
    /// </summary>
    /// <param name="f">The function bounding the region.</param>
    /// <param name="from">The first <c>x</c> of the stretch.</param>
    /// <param name="to">The last <c>x</c> of the stretch.</param>
    /// <param name="baseline">The <c>y</c> the region is measured from. Defaults to <c>0</c>, the x axis.</param>
    /// <param name="color">The fill colour; <see langword="null"/> takes the next palette colour at <see cref="ChartSeriesOptions.AreaOpacity"/>.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="samples">How many columns the region is built from; more follows a wiggly curve more closely.</param>
    /// <returns>The region, already on the chart; remove it like any other series.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="to"/> is not greater than <paramref name="from"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="samples"/> is less than two.</exception>
    public ChartAreaSeries AddArea(Func<float, float> f, float from, float to, float baseline = 0f, Color? color = null, string? name = null, int samples = 200)
    {
        ArgumentNullException.ThrowIfNull(f);

        return AddArea(f, _ => baseline, from, to, color, name, samples);
    }

    /// <summary>
    /// Shades the region between two functions over a stretch of <c>x</c> - the gap between a measurement
    /// and a model, or between two bounds.
    /// </summary>
    /// <param name="upper">One function bounding the region.</param>
    /// <param name="lower">The other; the two may cross, and the region simply narrows to nothing there.</param>
    /// <param name="from">The first <c>x</c> of the stretch.</param>
    /// <param name="to">The last <c>x</c> of the stretch.</param>
    /// <param name="color">The fill colour; <see langword="null"/> takes the next palette colour at <see cref="ChartSeriesOptions.AreaOpacity"/>.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="samples">How many columns the region is built from.</param>
    /// <returns>The region, already on the chart; remove it like any other series.</returns>
    /// <exception cref="ArgumentNullException">If either function is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="to"/> is not greater than <paramref name="from"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="samples"/> is less than two.</exception>
    public ChartAreaSeries AddArea(Func<float, float> upper, Func<float, float> lower, float from, float to, Color? color = null, string? name = null, int samples = 200)
    {
        ArgumentNullException.ThrowIfNull(upper);
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 2);

        if (to <= from)
        {
            throw new ArgumentException("The stretch must have a positive width.", nameof(to));
        }

        // The swatch keeps the solid colour so the legend stays legible; only the fill is translucent
        var solid = ResolveStyle(null, color, closed: false).Color;
        var fill = new Color(solid.R, solid.G, solid.B, (byte)Math.Clamp((int)(Options.Series.AreaOpacity * 255f), 0, 255));

        var areaOptions = new AreaOptions { Color = fill, EmissiveIntensity = Options.Series.EmissiveIntensity };
        var legendOptions = ResolveStyle(null, solid, closed: false);

        var seriesName = name ?? $"Area {_series.Count + 1}";
        var entity = new Entity(seriesName);

        // Behind the curves and the axes, in front of the grid
        entity.Transform.Position = new Vector3(0f, 0f, LayerStep * 0.5f);
        Root.AddChild(entity);

        var series = new ChartAreaSeries(seriesName, entity, legendOptions, areaOptions, new AreaSpec(upper, lower, from, to, samples));

        _series.Add(series);
        series.Rebuild(this);
        _legend.Rebuild();

        return series;
    }

    /// <summary>Rebuilds one series in place for the current ranges - what <see cref="ChartCurve.SetFunction"/> calls.</summary>
    internal void Rebuild(ChartSeries series) => ReplotSeries(series);

    /// <summary>
    /// Takes a curve off the chart: detaches its entity and frees its GPU buffers. Does nothing if the series
    /// is not on this chart.
    /// </summary>
    /// <param name="series">The series returned by <see cref="Plot"/>, <see cref="PlotParametric"/> or <see cref="AddLine"/>.</param>
    /// <returns><see langword="true"/> if the series was on the chart and has been removed.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="series"/> is <see langword="null"/>.</exception>
    public bool Remove(ChartSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (!_series.Remove(series))
            return false;

        Root.RemoveChild(series.Entity);
        series.Dispose();
        _legend.Rebuild();

        return true;
    }

    /// <summary>
    /// Takes every curve off the chart, freeing their GPU buffers. Axes, ticks, grid and labels stay.
    /// </summary>
    public void Clear()
    {
        foreach (var series in _series)
        {
            Root.RemoveChild(series.Entity);
            series.Dispose();
        }

        _series.Clear();
        _legend.Rebuild();
    }

    /// <summary>
    /// Adds a coordinate readout that follows the mouse: a ring marker on the chart under the cursor and a
    /// label with the chart-space coordinates, in the chart's label style. Call
    /// <see cref="ChartCursor.Update"/> from your update loop with the camera and the mouse position.
    /// </summary>
    /// <returns>The cursor, already parented to the chart and hidden until its first update.</returns>
    public ChartCursor AddCursor()
    {
        ChartScaffold.EnsureTextRenderer(Game, Options.Labels.Mode);

        var cursor = new ChartCursor(Game, this);
        _cursors.Add(cursor);

        return cursor;
    }

    /// <summary>
    /// Turns the chart into a view-driven, Desmos-style one: feed the returned follower with the camera
    /// every frame and the chart re-targets its ranges to whatever the camera sees, so the grid always
    /// covers the whole screen and the tick step adapts to the zoom.
    /// </summary>
    /// <returns>The follower, which <see cref="Update"/> then pumps for you.</returns>
    public ChartViewFollower FollowCamera()
    {
        // A view-driven chart needs the endless grid; a figure keeps the bounded one
        _grid.SetInfinite();

        return _follower = new ChartViewFollower(Game, this);
    }

    /// <summary>
    /// Drives everything on the chart that depends on the camera: the view-driven follower from
    /// <see cref="FollowCamera"/>, and every cursor from <see cref="AddCursor"/>, which is fed the current
    /// mouse position. Call it once a frame from your update loop; a chart with neither does nothing here.
    /// </summary>
    /// <remarks>
    /// The camera is a parameter rather than something the chart finds for itself, because a scene can hold
    /// several: pass the one that looks at this chart, and two charts can follow two different cameras.
    /// </remarks>
    /// <param name="camera">The camera looking at this chart.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="camera"/> is <see langword="null"/>.</exception>
    public void Update(CameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        _follower?.Update(camera);

        foreach (var cursor in _cursors)
        {
            cursor.Update(camera, Game.Input.MousePosition);
        }
    }

    /// <summary>
    /// Re-targets the chart to a new visible range. Axes, ticks, labels, titles and the legend are torn down
    /// and rebuilt with their ribbon buffers freed, the grid planes are re-aimed, and every series is
    /// re-plotted for the new ranges and view scale.
    /// </summary>
    /// <param name="xMin">The new left edge.</param>
    /// <param name="xMax">The new right edge.</param>
    /// <param name="yMin">The new bottom edge.</param>
    /// <param name="yMax">The new top edge.</param>
    /// <exception cref="ArgumentException">If the range has no positive width or height.</exception>
    public void SetVisibleRange(float xMin, float xMax, float yMin, float yMax)
    {
        if (!(xMax > xMin) || !(yMax > yMin))
        {
            throw new ArgumentException("The range must have positive width and height.");
        }

        var o = Options;
        o.Range.XMin = xMin;
        o.Range.XMax = xMax;
        o.Range.YMin = yMin;
        o.Range.YMax = yMax;

        // The grid's on/off state survives the rebuild, and Options stays in sync for anyone reading it
        o.Grid.Visible = _grid.Visible;

        _scaffold.Teardown();
        _scaffold.Build();
        _grid.Update();
        _legend.Rebuild();

        foreach (var series in _series)
        {
            ReplotSeries(series);
        }
    }

    /// <summary>
    /// Moves <paramref name="camera"/> so the chart's ranges exactly fill the window, with a little
    /// breathing room. An orthographic camera is centred and gets its size set; a perspective camera keeps
    /// its viewing direction and backs away until the chart's box fits the frustum. One-shot: the chart
    /// never steers the camera afterwards.
    /// </summary>
    /// <param name="camera">The camera to frame.</param>
    /// <param name="padding">Extra room as a fraction of the chart per side. Defaults to <c>0.05</c>.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="camera"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="padding"/> is negative.</exception>
    public void FrameCamera(CameraComponent camera, float padding = 0.05f)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);

        var backBuffer = Game.GraphicsDevice.Presenter.BackBuffer;
        var aspect = (float)backBuffer.Width / backBuffer.Height;

        // The chart's box in world space: the local ranges through the root's world matrix
        Root.Transform.UpdateWorldMatrix();
        var localMin = new Vector3(Options.Range.XMin, Options.Range.YMin, Is3D ? Options.Range.ZMin : 0f);
        var localMax = new Vector3(Options.Range.XMax, Options.Range.YMax, Is3D ? Options.Range.ZMax : 0f);
        var box = ChartFraming.TransformBox(localMin, localMax, in Root.Transform.WorldMatrix);
        var centre = (box.Minimum + box.Maximum) * 0.5f;
        var size = Vector3.Max(box.Maximum - box.Minimum, new Vector3(1e-3f));

        var transform = camera.Entity.Transform;

        if (camera.Projection == CameraProjectionMode.Orthographic)
        {
            // Centre the view and size it; the camera keeps its depth along Z
            transform.Position = new Vector3(centre.X, centre.Y, transform.Position.Z);
            camera.OrthographicSize = ChartFraming.OrthographicSize(size.X, size.Y, aspect, padding);
            return;
        }

        // Keep the viewing direction and back away along it until every corner of the box fits the frustum
        transform.UpdateWorldMatrix();
        var forward = transform.WorldMatrix.Forward;

        if (forward.LengthSquared() < MathUtil.ZeroTolerance)
        {
            forward = -Vector3.UnitZ;
        }

        forward.Normalize();

        var right = transform.WorldMatrix.Right;
        right.Normalize();
        var up = transform.WorldMatrix.Up;
        up.Normalize();

        var fov = MathUtil.DegreesToRadians(camera.VerticalFieldOfView);
        var distance = ChartFraming.PerspectiveDistance(in box, right, up, forward, aspect, fov, padding);
        transform.Position = centre - forward * distance;
    }

    /// <summary>
    /// Frees everything the chart owns: series buffers, scaffolding, legend, cursors, and the grid planes
    /// with their texture. Remove <see cref="Root"/> from its scene first.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        Clear();

        foreach (var cursor in _cursors)
        {
            cursor.Dispose();
        }

        _cursors.Clear();
        _scaffold.Dispose();
        _legend.Dispose();
        _grid.Dispose();
    }

    /// <summary>
    /// A copy of <paramref name="options"/> with the width scaled for the current view; the original is
    /// untouched so re-plots always scale from the intended base width.
    /// </summary>
    internal PolylineOptions ScaledOptions(PolylineOptions options) => ViewScale == 1f ? options : new PolylineOptions
    {
        Width = options.Width * ViewScale,
        Color = options.Color,
        EmissiveIntensity = options.EmissiveIntensity,
        Normal = options.Normal,
        Closed = options.Closed,
    };

    /// <summary>
    /// Turns a style and an optional colour into the ribbon options a series is drawn with: anything the
    /// caller left unset comes from <see cref="ChartOptions.Series"/>, and an explicit colour wins over the
    /// style's. The plane normal is the chart's own, which is why a series style has no say in it.
    /// </summary>
    private PolylineOptions ResolveStyle(ChartSeriesStyle? style, Color? color, bool closed)
    {
        var palette = Options.Series.Palette;
        var next = palette.Count > 0 ? palette[_series.Count % palette.Count] : Color.White;

        return new PolylineOptions
        {
            Width = style?.Width ?? Options.Series.CurveWidth,
            EmissiveIntensity = style?.EmissiveIntensity ?? Options.Series.EmissiveIntensity,
            Color = color ?? style?.Color ?? next,
            Closed = closed,
        };
    }

    /// <summary>Adds a finished series to the chart and refreshes the legend.</summary>
    private T Register<T>(T series) where T : ChartSeries
    {
        _series.Add(series);
        _legend.Rebuild();

        return series;
    }

    /// <summary>
    /// Rebuilds one series for the current ranges and view scale: function plots are re-sampled, point
    /// lines and parametric curves are re-clipped from their remembered points, markers are re-filtered,
    /// and a trajectory keeps its recorded geometry and only rescales its ribbon width.
    /// </summary>
    private void ReplotSeries(ChartSeries series)
    {
        if (series.IsDisposed)
            return;

        if (series is ChartTrajectory trajectory)
        {
            trajectory.RescaleWidth(ViewScale);
            return;
        }

        if (series is ChartAreaSeries area)
        {
            area.Rebuild(this);
            return;
        }

        if (series.MarkerPoints is not null)
        {
            series.RebuildMarkerModel(this);
            return;
        }

        List<IReadOnlyList<Vector3>> runs;
        var keepClosed = false;

        if (series is ChartCurve curve)
        {
            // The sample density per world unit is what the plot was created with, so zooming out keeps
            // the same detail per unit instead of stretching a fixed count across a wider range; the cap
            // bounds the rebuild cost at deep zoom-out
            var width = Options.Range.XMax - Options.Range.XMin;
            var samples = Math.Clamp((int)(curve.SampleDensity * width), curve.SampleCount, 8000);
            var points = PolylineSampling.Function(curve.Function, Options.Range.XMin, Options.Range.XMax, samples);
            var branches = PolylineClipping.SplitAtJumps(points, (Options.Range.YMax - Options.Range.YMin) * 0.25f, extendEnds: true);

            runs = [];
            foreach (var branch in branches)
            {
                runs.AddRange(Clip(branch));
            }
        }
        else if (series.SourcePoints is not null)
        {
            (runs, keepClosed) = ComputeLineRuns(series.SourcePoints, series.Options.Closed, series.ClipSource);
        }
        else
        {
            return;
        }

        series.ReleaseModel();

        if (runs.Count == 0)
            return;

        var effective = ScaledOptions(series.Options);
        var newMesh = keepClosed && runs.Count == 1
            ? PolylineMeshBuilder.Build(Game.GraphicsDevice, runs[0], effective)
            : PolylineMeshBuilder.BuildMany(Game.GraphicsDevice, runs, effective);
        series.Entity.Add(PolylineExtensions.CreateModel(Game, newMesh, effective));
    }

    /// <summary>
    /// Turns raw points into drawable runs: a closed shape is clipped as an open one that returns to its
    /// start, and drawn closed after all when nothing was cut, so the seam gets a proper mitred join.
    /// </summary>
    private (List<IReadOnlyList<Vector3>> Runs, bool KeepClosed) ComputeLineRuns(IReadOnlyList<Vector3> points, bool closed, bool clip)
    {
        closed = closed && points.Count > 1;

        IReadOnlyList<Vector3> source = closed ? [.. points, points[0]] : points;

        var clipped = clip ? Clip(source) : PolylineClipping.SplitAtNonFinite(source);

        var keepClosed = closed && clipped.Count == 1 && clipped[0].Length == source.Count
            && clipped[0][0] == source[0] && clipped[0][^1] == source[^1];

        var runs = new List<IReadOnlyList<Vector3>>();

        if (keepClosed)
        {
            runs.Add(points);
        }
        else
        {
            foreach (var run in clipped)
            {
                runs.Add(run);
            }
        }

        return (runs, keepClosed);
    }

    private List<Vector3[]> Clip(IReadOnlyList<Vector3> points)
        => Is3D
            ? PolylineClipping.Clip(points, Options.Range.XMin, Options.Range.XMax, Options.Range.YMin, Options.Range.YMax, Options.Range.ZMin, Options.Range.ZMax)
            : PolylineClipping.Clip(points, Options.Range.XMin, Options.Range.XMax, Options.Range.YMin, Options.Range.YMax);

    /// <summary>
    /// Builds the entity that draws one series' runs, parented to the chart. Nothing inside the chart gives
    /// an empty entity, which keeps the series usable and the palette in step.
    /// </summary>
    private Entity BuildSeriesEntity(List<IReadOnlyList<Vector3>> runs, PolylineOptions options, string name, bool closed)
    {
        var effective = ScaledOptions(options);

        var entity = runs.Count switch
        {
            0 => new Entity(name),
            1 when closed => Game.CreatePolyline(runs[0], effective, name),
            _ => Game.CreatePolylines(runs, effective, name),
        };

        entity.Transform.Position = new Vector3(0f, 0f, 2f * LayerStep);
        Root.AddChild(entity);

        return entity;
    }
}