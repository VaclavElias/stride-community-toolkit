using Stride.CommunityToolkit.Charts.Lines;
using Stride.CommunityToolkit.Rendering;
using Stride.CommunityToolkit.Shapes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Rendering;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A chart in the XY plane (optionally with a Z extent): axes, tick marks, tick labels, titles, an optional
/// major and minor grid, a legend, and any number of plotted curves - all children of one <see cref="Root"/>
/// entity, so the whole chart can be placed, rotated and scaled in the world as a single object.
/// </summary>
/// <remarks>
/// <para>
/// A chart is three things: <see cref="Options"/>, which stay live after it is built; series, added with
/// <see cref="Plot"/> and its siblings and taken off with <see cref="Remove"/>; and <see cref="Update(CameraComponent)"/>,
/// called once a frame with the camera, which applies whatever changed in the options and drives the
/// parts that depend on the view. Construct a chart, add its <see cref="Root"/> to a scene, plot, and call
/// <see cref="Update(CameraComponent)"/> from your update loop. <see cref="Dispose"/> frees everything the chart owns.
/// </para>
/// <para>
/// Inside, the chart is a façade over three parts, each with one responsibility: <see cref="ChartScaffold"/>
/// (axes, ticks, labels, titles), <see cref="ChartGrid"/> (the textured grid planes) and
/// <see cref="ChartLegend"/>. Curves are ribbon meshes from <see cref="PolylineMeshBuilder"/>, with real
/// thickness in chart units; the furniture around them - axes, ticks, legend swatches, markers, the cursor -
/// is drawn every frame by <see cref="Update(CameraComponent)"/> as pixel-measured shapes, so it keeps its size at any zoom
/// or distance.
/// </para>
/// </remarks>
public sealed class Chart : IDisposable
{
    // Every ribbon lies in the chart plane, so coplanar ones z-fight where they cross and flicker dark
    // fringes. Each layer is nudged along Z by this much: grids behind the axes, ticks and curves in front.
    internal const float LayerStep = 0.005f;

    // Where the curves sit: above the axes at 0 and the area fills at half a step
    private const float CurveLayer = 2f * LayerStep;

    private readonly List<ChartSeries> _series = [];
    private readonly ChartScaffold _scaffold;
    private readonly ChartLegend _legend;
    private readonly ChartGrid _grid;
    private ChartViewFollower? _follower;
    private ChartCursor? _cursor;
    private ShapeBatch? _ownedBatch;

    // What Update last applied from Options, so a frame with nothing changed costs a few compares
    private RangeSnapshot _appliedRange;
    private bool _appliedFollow;
    private bool _appliedGridVisible;
    private bool _appliedLegendVisible;
    private float _appliedGlow;

    // The view height the chart was created with; ViewScale compares the current height against it
    private readonly float _referenceHeight;
    private readonly float _referencePixelHeight;
    private bool _isDisposed;

    /// <summary>The entity every part of the chart is parented to. Add it to a scene and move it to place the chart.</summary>
    public Entity Root { get; }

    /// <summary>
    /// The chart's settings, live: change one and the next <see cref="Update(CameraComponent)"/> applies it. See
    /// <see cref="ChartOptions"/> for the one exception, the series defaults.
    /// </summary>
    public ChartOptions Options { get; }

    /// <summary>
    /// Whether the chart has a real Z extent - axes, clipping and grids gain the third dimension. Decided
    /// once, when the chart is built, from whether <see cref="ChartRangeOptions.ZMax"/> was above
    /// <see cref="ChartRangeOptions.ZMin"/>.
    /// </summary>
    public bool Is3D { get; }

    /// <summary>The curves on the chart, in the order they were added.</summary>
    public IReadOnlyList<ChartSeries> Series => _series;

    /// <summary>
    /// The point of the chart plane under the mouse, in chart units, while <see cref="ChartCursorOptions.Visible"/>
    /// is on and the mouse is over the chart; otherwise <see langword="null"/>. The value to read a curve
    /// against, or to snap something to. Refreshed by <see cref="Update(CameraComponent)"/>.
    /// </summary>
    public Vector3? CursorPosition { get; private set; }

    /// <summary>The game the chart draws in - meshes are built on its device, on the game thread.</summary>
    internal Game Game { get; }

    /// <summary>
    /// How much taller the current view is than the one the chart was created with, corrected for the
    /// window's pixel height. Curves, trajectories and areas are still ribbon meshes with a width in chart
    /// units, so a view-driven chart multiplies their width by this whenever it re-plots them, keeping them
    /// the same thickness on screen at every zoom level. Nothing else reads it: the furniture is drawn in
    /// pixels and needs no correction.
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
            ChartText.EnsureRenderer(game, options.Labels.Mode);
        }

        Game = game;
        Options = options;
        Is3D = options.Range.ZMax > options.Range.ZMin;
        _referenceHeight = options.Range.YMax - options.Range.YMin;
        _referencePixelHeight = game.GraphicsDevice.Presenter.BackBuffer.Height;
        Root = new Entity(name ?? "Chart");

        _scaffold = new ChartScaffold(game, this);
        _legend = new ChartLegend(game, this);
        _grid = new ChartGrid(game, this);

        // Built for the options as given; Update then only has to apply what changes afterwards
        _appliedRange = RangeSnapshot.Of(options.Range);
        _appliedGridVisible = options.Grid.Visible;
        _appliedLegendVisible = options.Legend.Visible;
        _appliedGlow = options.Series.Glow;

        _grid.Create();
        _scaffold.Build();
        _grid.Update();
        _legend.SetVisible(options.Legend.Visible);
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

        var seriesName = name ?? $"Plot {_series.Count + 1}";
        var options = ResolveStyle(style, color, closed: false);

        return Register(new ChartCurve(this, seriesName, CreateSeriesEntity(seriesName, CurveLayer), options, f, samples));
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
    public ChartLineSeries PlotParametric(Func<float, Vector3> p, float from, float to, Color? color = null, string? name = null, int samples = 200, bool closed = false, ChartSeriesStyle? style = null)
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
    public ChartLineSeries AddLine(IReadOnlyList<Vector3> points, Color? color = null, string? name = null, bool closed = false, bool clip = true, ChartSeriesStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        var seriesName = name ?? $"Line {_series.Count + 1}";
        var options = ResolveStyle(style, color, closed);

        return Register(new ChartLineSeries(seriesName, CreateSeriesEntity(seriesName, CurveLayer), options, points, closed, clip));
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
        entity.Transform.Position = new Vector3(0f, 0f, CurveLayer);
        Root.AddChild(entity);

        return Register(new ChartTrajectory(seriesName, entity, options, line, Options));
    }

    /// <summary>
    /// Adds scatter markers: one small × per point, drawn every frame in pixels, so they keep their size at
    /// any zoom or distance. Points outside the chart's ranges are not drawn until the range brings them
    /// back.
    /// </summary>
    /// <param name="points">The data points, in chart units.</param>
    /// <param name="color">The markers' colour; <see langword="null"/> takes the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="size">The glyph's size in pixels; <see langword="null"/> takes <see cref="ChartSeriesOptions.MarkerSize"/>.</param>
    /// <param name="width">The glyph's stroke width in pixels; <see langword="null"/> takes <see cref="ChartSeriesOptions.MarkerWidth"/>.</param>
    /// <returns>The series, already on the chart; remove it like any other.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="points"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="size"/> or <paramref name="width"/> is not positive.</exception>
    public ChartMarkerSeries AddMarkers(IReadOnlyList<Vector3> points, Color? color = null, string? name = null, float? size = null, float? width = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        var markerSize = size ?? Options.Series.MarkerSize;
        var markerWidth = width ?? Options.Series.MarkerWidth;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(markerSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(markerWidth);

        // The stroke width rides in the options' Width, in pixels here rather than chart units
        var options = ResolveStyle(new ChartSeriesStyle { Width = markerWidth }, color, closed: false);
        var seriesName = name ?? $"Markers {_series.Count + 1}";

        // Nothing is drawn through the entity; it keeps the series shaped like every other
        return Register(new ChartMarkerSeries(seriesName, CreateSeriesEntity(seriesName, 0f), options, points, markerSize));
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

        var areaOptions = new AreaOptions { Color = fill, EmissiveIntensity = Options.Series.Glow };
        var legendOptions = ResolveStyle(null, solid, closed: false);

        var seriesName = name ?? $"Area {_series.Count + 1}";

        // Behind the curves and the axes, in front of the grid
        var entity = CreateSeriesEntity(seriesName, LayerStep * 0.5f);

        return Register(new ChartAreaSeries(seriesName, entity, legendOptions, areaOptions, new AreaSpec(upper, lower, from, to, samples)));
    }

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
    /// The chart's frame: applies whatever changed in <see cref="Options"/> since the last call, and drives
    /// the parts that depend on the camera - re-targeting the range to the view when
    /// <see cref="ChartRangeOptions.FollowCamera"/> is on, and the mouse readout when
    /// <see cref="ChartCursorOptions.Visible"/> is. Call it once a frame from your update loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A changed range rebuilds the axes, ticks, labels and legend and re-plots every series; a changed
    /// visibility or glow is a flag or a parameter write. Nothing happens on a frame where nothing changed,
    /// so the call is cheap to make unconditionally.
    /// </para>
    /// <para>
    /// The camera is a parameter rather than something the chart finds for itself, because a scene can hold
    /// several: pass the one that looks at this chart, and two charts can follow two different cameras.
    /// </para>
    /// </remarks>
    /// <param name="camera">The camera looking at this chart.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="camera"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If <see cref="ChartRangeOptions"/> has no positive width or height.</exception>
    /// <exception cref="ObjectDisposedException">If the chart has been disposed.</exception>
    public void Update(CameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        UpdateCore(camera, shapes: null);
    }

    /// <summary>
    /// <see cref="Update(CameraComponent)"/>, drawing the furniture into a batch of your own instead of the
    /// chart's - one draw call for a chart and the HUD around it, and your choice of depth behaviour. The
    /// chart never creates a batch of its own when it is updated this way.
    /// </summary>
    /// <param name="camera">The camera looking at this chart.</param>
    /// <param name="shapes">The batch to submit the axes, ticks, legend swatches, markers and cursor into.</param>
    /// <exception cref="ArgumentNullException">If either argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If <see cref="ChartRangeOptions"/> has no positive width or height.</exception>
    /// <exception cref="ObjectDisposedException">If the chart has been disposed.</exception>
    public void Update(CameraComponent camera, ShapeBatch shapes)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(shapes);

        UpdateCore(camera, shapes);
    }

    private void UpdateCore(CameraComponent camera, ShapeBatch? shapes)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var o = Options;

        if (o.Range.FollowCamera != _appliedFollow)
        {
            _appliedFollow = o.Range.FollowCamera;

            // A view-driven chart needs the endless grid; a figure keeps the bounded one
            _grid.SetInfinite(_appliedFollow);
            _follower = _appliedFollow ? new ChartViewFollower(Game, this) : null;
        }

        // Writes the range when the view moved; the range diff below is what acts on it
        _follower?.Update(camera);

        ApplyRange();

        if (o.Grid.Visible != _appliedGridVisible)
        {
            _appliedGridVisible = o.Grid.Visible;
            _grid.ApplyVisibility();
        }

        if (o.Legend.Visible != _appliedLegendVisible)
        {
            _appliedLegendVisible = o.Legend.Visible;
            _legend.SetVisible(_appliedLegendVisible);
        }

        if (o.Series.Glow != _appliedGlow)
        {
            _appliedGlow = o.Series.Glow;

            foreach (var series in _series)
            {
                series.SetEmissiveIntensity(_appliedGlow);
            }
        }

        // The furniture: everything measured in pixels is submitted afresh each frame, in the chart's own
        // depth-tested batch unless the caller brought one
        var batch = shapes ?? (_ownedBatch ??= Game.AddShapeBatch(depthTest: true));
        var pixelScale = batch.AutoScale ? DisplayScale.GetOrCreate(Game).Value : 1f;
        var view = new ChartView(Root, camera, Game.GraphicsDevice.Presenter.BackBuffer.Height, pixelScale);

        UpdateCursor(camera, in view);

        _scaffold.Draw(batch, in view);

        foreach (var series in _series)
        {
            if (series is ChartMarkerSeries markers)
            {
                markers.Draw(batch, in view, o, Is3D);
            }
        }

        _legend.Draw(batch, in view);
        _cursor?.Draw(batch, in view);
    }

    /// <summary>
    /// Re-targets the chart when the range differs from the one last applied: axes, ticks, labels, titles
    /// and the legend are torn down and rebuilt with their ribbon buffers freed, the grid planes are
    /// re-aimed, and every series is re-plotted for the new ranges and view scale.
    /// </summary>
    private void ApplyRange()
    {
        var r = Options.Range;
        var snapshot = RangeSnapshot.Of(r);

        if (snapshot == _appliedRange)
            return;

        if (!(r.XMax > r.XMin) || !(r.YMax > r.YMin))
        {
            throw new InvalidOperationException("Options.Range must have positive width and height.");
        }

        _appliedRange = snapshot;

        _scaffold.Build();
        _grid.Update();
        _legend.Rebuild();

        foreach (var series in _series)
        {
            series.Rebuild(this);
        }
    }

    /// <summary>
    /// Moves the readout under the mouse while it is wanted, building it on first use; hides it otherwise.
    /// </summary>
    private void UpdateCursor(CameraComponent camera, in ChartView view)
    {
        if (!Options.Cursor.Visible)
        {
            _cursor?.Hide();
            CursorPosition = null;
            return;
        }

        _cursor ??= new ChartCursor(Game, this);
        _cursor.Update(camera, Game.Input.MousePosition, in view);
        CursorPosition = _cursor.Position;
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

        _cursor?.Dispose();
        _cursor = null;
        _scaffold.Dispose();
        _legend.Dispose();
        _grid.Dispose();

        // The owned batch was registered with the renderer by AddShapeBatch; taken out again so an empty
        // batch is not left in the visibility group for the rest of the game
        if (_ownedBatch is not null)
        {
            RenderContext.GetShared(Game.Services).VisibilityGroup?.RenderObjects.Remove(_ownedBatch);
            _ownedBatch = null;
        }
    }

    /// <summary>The range fields <see cref="Update(CameraComponent)"/> compares to know whether a rebuild is due.</summary>
    private readonly record struct RangeSnapshot(float XMin, float XMax, float YMin, float YMax, float ZMin, float ZMax, float TickStep, int MinorDivisions)
    {
        internal static RangeSnapshot Of(ChartRangeOptions r)
            => new(r.XMin, r.XMax, r.YMin, r.YMax, r.ZMin, r.ZMax, r.TickStep, r.MinorDivisions);
    }

    /// <summary>Clips a run of points to the chart's ranges - a box for a 3D chart, a rectangle otherwise.</summary>
    internal List<Vector3[]> Clip(IReadOnlyList<Vector3> points)
        => Is3D
            ? PolylineClipping.Clip(points, Options.Range.XMin, Options.Range.XMax, Options.Range.YMin, Options.Range.YMax, Options.Range.ZMin, Options.Range.ZMax)
            : PolylineClipping.Clip(points, Options.Range.XMin, Options.Range.XMax, Options.Range.YMin, Options.Range.YMax);

    /// <summary>
    /// The entity a series draws through, parented to the chart and lifted off the plane by
    /// <paramref name="layer"/> so coplanar ribbons do not z-fight.
    /// </summary>
    private Entity CreateSeriesEntity(string name, float layer)
    {
        var entity = new Entity(name);
        entity.Transform.Position = new Vector3(0f, 0f, layer);
        Root.AddChild(entity);

        return entity;
    }

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
            EmissiveIntensity = style?.Glow ?? Options.Series.Glow,
            Color = color ?? style?.Color ?? next,
            Closed = closed,
        };
    }

    /// <summary>Adds a series to the chart, builds it for the current ranges and refreshes the legend.</summary>
    private T Register<T>(T series) where T : ChartSeries
    {
        _series.Add(series);
        series.Rebuild(this);
        _legend.Rebuild();

        return series;
    }
}