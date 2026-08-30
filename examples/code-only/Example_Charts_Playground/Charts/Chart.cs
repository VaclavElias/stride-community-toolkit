using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Lines;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.Core.Mathematics;
using Stride.Engine;
using System.Globalization;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A 2D chart in the XY plane: axes, tick marks, tick labels, an optional major and minor grid, and any
/// number of plotted curves - all children of one <see cref="Root"/> entity, so the whole chart can be
/// placed, rotated and scaled in the world as a single object.
/// </summary>
/// <remarks>
/// Every line is a ribbon mesh from <see cref="PolylineMeshBuilder"/>, so it has real thickness. Labels are
/// either world text that scales with the chart or screen text of a fixed pixel size, chosen by
/// <see cref="ChartOptions.LabelMode"/>. Create a chart with <see cref="Create"/>, add its <see cref="Root"/>
/// to a scene, then call <see cref="Plot"/> for each curve.
/// </remarks>
public sealed class Chart
{
    // Every ribbon lies in the chart plane, so coplanar ones z-fight where they cross and flicker dark
    // fringes. Each layer is nudged along Z by this much: grids behind the axes, ticks and curves in front.
    internal const float LayerStep = 0.005f;

    private readonly Game _game;
    private readonly List<ModelComponent> _gridModels = [];
    private readonly List<ChartSeries> _series = [];

    /// <summary>The entity every part of the chart is parented to. Add it to a scene and move it to place the chart.</summary>
    public Entity Root { get; }

    /// <summary>The settings the chart was created with.</summary>
    public ChartOptions Options { get; }

    /// <summary>The curves on the chart, in the order they were added.</summary>
    public IReadOnlyList<ChartSeries> Series => _series;

    /// <summary>Shows or hides the major and minor grid. Cheap to toggle every frame; the meshes are built once.</summary>
    public bool GridVisible
    {
        get => _gridModels.Count > 0 && _gridModels[0].Enabled;
        set
        {
            foreach (var model in _gridModels)
                model.Enabled = value;
        }
    }

    private Chart(Game game, ChartOptions options, string name)
    {
        _game = game;
        Options = options;
        Root = new Entity(name);

        BuildAxes();
        BuildTicks();
        BuildGrids();

        if (options.ShowLabels)
        {
            BuildLabels();
        }
    }

    /// <summary>
    /// Builds a chart. The <see cref="Root"/> is not added to a scene; do that where you want it.
    /// </summary>
    /// <param name="game">The game the chart is drawn in.</param>
    /// <param name="options">Ranges, ticks, grid, labels and curve defaults; <see langword="null"/> for <see cref="ChartOptions.Glow3D"/>.</param>
    /// <param name="name">The root entity's name, or <c>"Chart"</c>.</param>
    /// <returns>The chart, ready for <see cref="Plot"/> calls.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="game"/> is <see langword="null"/>.</exception>
    public static Chart Create(Game game, ChartOptions? options = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        options ??= ChartOptions.Glow3D();

        if (options.ShowLabels)
        {
            // Text never appears without its renderer; registering twice is harmless
            if (options.LabelMode == ChartLabelMode.Screen)
                game.AddEntityTextRenderer();
            else
                game.AddWorldTextRenderer();
        }

        return new Chart(game, options, name ?? "Chart");
    }

    /// <summary>
    /// Plots <c>y = f(x)</c> across the chart's <c>x</c> range. Samples outside the <c>y</c> range are clipped
    /// to the chart edge, samples that are not finite (a function outside its domain) break the curve, and so
    /// does a jump larger than the chart's height between two samples - the asymptotes of <c>tan(x)</c> or
    /// <c>1/x</c> - which would otherwise draw a false vertical line.
    /// </summary>
    /// <param name="f">The function to plot.</param>
    /// <param name="options">Width, colour and glow; <see langword="null"/> for the chart's curve defaults and the next palette colour.</param>
    /// <param name="samples">How many points to sample; more is smoother.</param>
    /// <param name="name">The series and entity name.</param>
    /// <returns>The series, already on the chart; keep it to <see cref="Remove"/> the curve later.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="f"/> is <see langword="null"/>.</exception>
    public ChartSeries Plot(Func<float, float> f, PolylineOptions? options = null, int samples = 200, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(f);

        var points = PolylineSampling.Function(f, Options.XMin, Options.XMax, samples);
        var branches = PolylineClipping.SplitAtJumps(points, Options.YMax - Options.YMin);

        var runs = new List<IReadOnlyList<Vector3>>();
        foreach (var branch in branches)
        {
            runs.AddRange(Clip(branch));
        }

        return AddSeries(runs, options, name ?? $"Plot {_series.Count + 1}", closed: false);
    }

    /// <summary>
    /// Plots a parametric curve <c>p(t)</c>, clipped to the chart's ranges.
    /// </summary>
    /// <param name="p">The curve; its <c>z</c> is kept, so the curve may leave the chart plane.</param>
    /// <param name="from">The first <c>t</c>.</param>
    /// <param name="to">The last <c>t</c>.</param>
    /// <param name="options">Width, colour and glow; <see langword="null"/> for the chart's curve defaults and the next palette colour.</param>
    /// <param name="samples">How many points to sample; more is smoother.</param>
    /// <param name="name">The series and entity name.</param>
    /// <returns>The series, already on the chart; keep it to <see cref="Remove"/> the curve later.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="p"/> is <see langword="null"/>.</exception>
    public ChartSeries PlotParametric(Func<float, Vector3> p, float from, float to, PolylineOptions? options = null, int samples = 200, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(p);

        var points = PolylineSampling.Parametric(p, from, to, samples);

        return AddLine(points, options, name ?? $"Plot {_series.Count + 1}");
    }

    /// <summary>
    /// Adds a line through arbitrary points - measured data, a trajectory, a hand-drawn shape.
    /// </summary>
    /// <param name="points">The points, in chart units.</param>
    /// <param name="options">Width, colour and glow; <see langword="null"/> for the chart's curve defaults and the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="clip">
    /// Whether to cut the line to the chart's ranges. <see langword="true"/> (the default) also breaks the line at
    /// points that are not finite; <see langword="false"/> only does the latter and lets the line leave the chart.
    /// </param>
    /// <returns>The series, already on the chart; keep it to <see cref="Remove"/> the line later.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="points"/> is <see langword="null"/>.</exception>
    public ChartSeries AddLine(IReadOnlyList<Vector3> points, PolylineOptions? options = null, string? name = null, bool clip = true)
    {
        ArgumentNullException.ThrowIfNull(points);

        var closed = options?.Closed == true && points.Count > 1;

        // A closed shape is clipped as an open one that returns to its start; if nothing was cut it is drawn
        // closed after all, so the seam gets a proper mitred join
        IReadOnlyList<Vector3> source = closed ? [.. points, points[0]] : points;

        var runs = clip ? Clip(source) : PolylineClipping.SplitAtNonFinite(source);

        var keepClosed = closed && runs.Count == 1 && runs[0].Length == source.Count
            && runs[0][0] == source[0] && runs[0][^1] == source[^1];

        return keepClosed
            ? AddSeries([points], options, name ?? $"Line {_series.Count + 1}", closed: true)
            : AddSeries(runs, options, name ?? $"Line {_series.Count + 1}", closed: false);
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
        RebuildLegend();

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
        RebuildLegend();
    }

    /// <summary>
    /// Adds an empty trajectory: a curve that grows one point at a time - the path of a moving body, drawn
    /// while it moves. Feed it from your update loop with <see cref="ChartTrajectory.Add"/>; points are
    /// clipped to the chart's ranges the same way <see cref="Plot"/> clips a function.
    /// </summary>
    /// <param name="capacity">The most points the trail can hold; the GPU buffers are allocated once, for this many.</param>
    /// <param name="options">Width, colour and glow; <see langword="null"/> for the chart's curve defaults and the next palette colour.</param>
    /// <param name="name">The series and entity name.</param>
    /// <param name="rollOver">What a full trail does with the next point: <see langword="false"/> ignores it, <see langword="true"/> drops the oldest - an oscilloscope trace.</param>
    /// <returns>The trajectory, already on the chart and empty; it is also in <see cref="Series"/> and removed like any other curve.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="capacity"/> is less than two.</exception>
    public ChartTrajectory AddTrajectory(int capacity = 1000, PolylineOptions? options = null, string? name = null, bool rollOver = false)
    {
        options ??= DefaultCurveOptions();

        // The trail is clipped to the ranges, so the mesh bounds can be pinned once instead of growing
        var margin = options.Width;
        var bounds = new BoundingBox(
            new Vector3(Options.XMin - margin, Options.YMin - margin, -1f),
            new Vector3(Options.XMax + margin, Options.YMax + margin, 1f));

        var line = new GrowingPolyline(_game, capacity, options, bounds) { RollOver = rollOver };

        var seriesName = name ?? $"Trajectory {_series.Count + 1}";
        var entity = _game.CreatePolylineEntity(line.Mesh, options, seriesName);
        entity.Transform.Position = new Vector3(0f, 0f, 2f * LayerStep);
        Root.AddChild(entity);

        var series = new ChartTrajectory(seriesName, entity, options, line, Options);
        _series.Add(series);
        RebuildLegend();

        return series;
    }

    private PolylineOptions DefaultCurveOptions()
    {
        var palette = Options.CurvePalette;

        return new PolylineOptions
        {
            Width = Options.CurveWidth,
            EmissiveIntensity = Options.CurveEmissiveIntensity,
            Color = palette.Length > 0 ? palette[_series.Count % palette.Length] : Color.White,
        };
    }

    // Legend: rebuilt whenever the series change, so it always matches what is drawn
    private const float LegendRowStep = 0.5f;
    private Entity? _legendRoot;
    private bool _legendVisible = true;

    /// <summary>
    /// Shows or hides the legend without rebuilding it. The legend itself appears only while
    /// <see cref="ChartOptions.ShowLegend"/> is on and the chart has at least one series.
    /// </summary>
    public bool LegendVisible
    {
        get => _legendVisible;
        set
        {
            _legendVisible = value;
            ApplyLegendVisibility();
        }
    }

    /// <summary>
    /// Adds a coordinate readout that follows the mouse: a ring marker on the chart under the cursor and a
    /// label with the chart-space coordinates, in the chart's label style. Call
    /// <see cref="ChartCursor.Update"/> from your update loop with the camera and the mouse position.
    /// </summary>
    /// <returns>The cursor, already parented to the chart and hidden until its first update.</returns>
    public ChartCursor AddCursor()
    {
        EnsureTextRenderer();

        return new ChartCursor(_game, this);
    }

    /// <summary>
    /// Tears down and rebuilds the legend - a colour swatch and name per series, stacked in the chart's top
    /// left corner. Called automatically when series are added or removed.
    /// </summary>
    private void RebuildLegend()
    {
        if (_legendRoot is not null)
        {
            // The swatches own ribbon buffers nothing else tracks
            foreach (var child in _legendRoot.GetChildren().ToArray())
            {
                if (child.Get<ModelComponent>()?.Model is { } model)
                {
                    foreach (var mesh in model.Meshes)
                    {
                        PolylineMeshBuilder.Release(mesh);
                    }
                }
            }

            Root.RemoveChild(_legendRoot);
            _legendRoot = null;
        }

        if (!Options.ShowLegend || _series.Count == 0)
            return;

        EnsureTextRenderer();

        var o = Options;

        _legendRoot = new Entity("Legend");
        _legendRoot.Transform.Position = new Vector3(o.XMin + 0.4f, o.YMax - 0.5f, 3f * LayerStep);

        for (var i = 0; i < _series.Count; i++)
        {
            var series = _series[i];
            var y = -i * LegendRowStep;

            var swatch = _game.CreatePolyline(
                [new Vector3(0f, y, 0f), new Vector3(0.45f, y, 0f)],
                new PolylineOptions { Width = o.CurveWidth, Color = series.Color, EmissiveIntensity = series.Options.EmissiveIntensity },
                $"Legend swatch {series.Name}");
            _legendRoot.AddChild(swatch);

            var label = new Entity($"Legend label {series.Name}");

            if (o.LabelMode == ChartLabelMode.Screen)
            {
                label.Add(new EntityTextComponent
                {
                    Text = series.Name,
                    FontSize = o.LabelFontSize,
                    TextColor = o.LabelColor,
                    Anchor = TextAnchor.MiddleLeft,
                    Offset = new Vector2(6f, 0f),
                });
            }
            else
            {
                label.Add(new WorldTextComponent
                {
                    Text = series.Name,
                    Height = o.LabelHeight,
                    TextColor = o.LabelColor,
                    Anchor = TextAnchor.MiddleLeft,
                    Billboard = true,
                    KeepUpright = true,
                });
            }

            label.Transform.Position = new Vector3(0.6f, y, 0f);
            _legendRoot.AddChild(label);
        }

        Root.AddChild(_legendRoot);
        ApplyLegendVisibility();
    }

    private void ApplyLegendVisibility()
    {
        if (_legendRoot is null)
            return;

        foreach (var child in _legendRoot.GetChildren())
        {
            if (child.Get<ModelComponent>() is { } model)
                model.Enabled = _legendVisible;

            if (child.Get<EntityTextComponent>() is { } screenText)
                screenText.IsVisible = _legendVisible;

            if (child.Get<WorldTextComponent>() is { } worldText)
                worldText.IsVisible = _legendVisible;
        }
    }

    /// <summary>
    /// Registers the renderer the chart's label mode needs; harmless when already registered, and needed
    /// here because the legend and cursor draw text even when tick labels are off.
    /// </summary>
    private void EnsureTextRenderer()
    {
        if (Options.LabelMode == ChartLabelMode.Screen)
            _game.AddEntityTextRenderer();
        else
            _game.AddWorldTextRenderer();
    }

    private List<Vector3[]> Clip(IReadOnlyList<Vector3> points)
        => PolylineClipping.Clip(points, Options.XMin, Options.XMax, Options.YMin, Options.YMax);

    private ChartSeries AddSeries(IReadOnlyList<IReadOnlyList<Vector3>> runs, PolylineOptions? options, string name, bool closed)
    {
        var palette = Options.CurvePalette;

        options ??= new PolylineOptions
        {
            Width = Options.CurveWidth,
            EmissiveIntensity = Options.CurveEmissiveIntensity,
            Color = palette.Length > 0 ? palette[_series.Count % palette.Length] : Color.White,
        };

        // Nothing inside the chart: an empty entity keeps the series usable and the palette in step
        var entity = runs.Count switch
        {
            0 => new Entity(name),
            1 when closed => _game.CreatePolyline(runs[0], options, name),
            _ => _game.CreatePolylines(runs, options, name),
        };

        entity.Transform.Position = new Vector3(0f, 0f, 2f * LayerStep);
        Root.AddChild(entity);

        var series = new ChartSeries(name, entity, options, isEmpty: runs.Count == 0);
        _series.Add(series);
        RebuildLegend();

        return series;
    }

    private void BuildAxes()
    {
        var o = Options;

        // Each axis sits on the other coordinate's zero, or on the nearest edge when zero is out of range
        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);

        Root.AddChild(_game.CreatePolyline(
            [new Vector3(o.XMin, axisY, 0f), new Vector3(o.XMax, axisY, 0f)],
            new PolylineOptions { Width = o.AxisWidth, Color = o.XAxisColor },
            "X axis"));

        Root.AddChild(_game.CreatePolyline(
            [new Vector3(axisX, o.YMin, 0f), new Vector3(axisX, o.YMax, 0f)],
            new PolylineOptions { Width = o.AxisWidth, Color = o.YAxisColor },
            "Y axis"));
    }

    private void BuildTicks()
    {
        var o = Options;
        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);
        var half = o.TickLength * 0.5f;

        var xTicks = new List<(Vector3, Vector3)>();
        foreach (var x in TickValues(o.XMin, o.XMax, o.TickStep))
        {
            xTicks.Add((new Vector3(x, axisY - half, 0f), new Vector3(x, axisY + half, 0f)));
        }

        var yTicks = new List<(Vector3, Vector3)>();
        foreach (var y in TickValues(o.YMin, o.YMax, o.TickStep))
        {
            yTicks.Add((new Vector3(axisX - half, y, 0f), new Vector3(axisX + half, y, 0f)));
        }

        if (xTicks.Count > 0)
        {
            var ticks = _game.CreateSegments(xTicks, new PolylineOptions { Width = o.TickWidth, Color = o.XAxisColor }, "X ticks");
            ticks.Transform.Position = new Vector3(0f, 0f, LayerStep);
            Root.AddChild(ticks);
        }

        if (yTicks.Count > 0)
        {
            var ticks = _game.CreateSegments(yTicks, new PolylineOptions { Width = o.TickWidth, Color = o.YAxisColor }, "Y ticks");
            ticks.Transform.Position = new Vector3(0f, 0f, LayerStep);
            Root.AddChild(ticks);
        }
    }

    private void BuildGrids()
    {
        var o = Options;

        // Minor grid sits behind the major grid, and skips the lines the major grid already draws
        if (o.MinorDivisions > 1)
        {
            var minorStep = o.TickStep / o.MinorDivisions;
            AddGrid(GridLines(minorStep, skipMultiplesOf: o.TickStep), o.MinorGridWidth, o.MinorGridColor, "Minor grid", -2f * LayerStep);
        }

        AddGrid(GridLines(o.TickStep, skipMultiplesOf: null), o.GridWidth, o.GridColor, "Grid", -LayerStep);
    }

    private List<(Vector3, Vector3)> GridLines(float step, float? skipMultiplesOf)
    {
        var o = Options;
        var lines = new List<(Vector3, Vector3)>();

        foreach (var x in TickValues(o.XMin, o.XMax, step))
        {
            if (skipMultiplesOf is { } major && IsMultiple(x, major))
                continue;

            lines.Add((new Vector3(x, o.YMin, 0f), new Vector3(x, o.YMax, 0f)));
        }

        foreach (var y in TickValues(o.YMin, o.YMax, step))
        {
            if (skipMultiplesOf is { } major && IsMultiple(y, major))
                continue;

            lines.Add((new Vector3(o.XMin, y, 0f), new Vector3(o.XMax, y, 0f)));
        }

        return lines;
    }

    private void AddGrid(List<(Vector3, Vector3)> lines, float width, Color color, string name, float z)
    {
        if (lines.Count == 0)
            return;

        var grid = _game.CreateSegments(lines, new PolylineOptions { Width = width, Color = color }, name);
        grid.Transform.Position = new Vector3(0f, 0f, z);
        Root.AddChild(grid);

        var model = grid.Get<ModelComponent>()!;
        model.Enabled = Options.GridVisible;
        _gridModels.Add(model);
    }

    private void BuildLabels()
    {
        var o = Options;
        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);
        var gap = o.TickLength * 0.5f + (o.LabelMode == ChartLabelMode.World ? o.LabelHeight * 0.25f : 0f);

        foreach (var x in TickValues(o.XMin, o.XMax, o.TickStep))
        {
            // The origin is labelled once, by the y axis, so the two "0"s do not overlap
            if (IsZero(x) && IsZero(axisY))
                continue;

            AddLabel(x, new Vector3(x, axisY - gap, 0f), TextAnchor.TopCenter);
        }

        foreach (var y in TickValues(o.YMin, o.YMax, o.TickStep))
        {
            AddLabel(y, new Vector3(axisX - gap, y, 0f), TextAnchor.MiddleRight);
        }
    }

    private void AddLabel(float value, Vector3 position, TextAnchor anchor)
    {
        var o = Options;
        var text = value.ToString(o.LabelFormat, CultureInfo.InvariantCulture);
        var label = new Entity($"Label {text}");

        if (o.LabelMode == ChartLabelMode.Screen)
        {
            // A few pixels of breathing room beyond the tick, in the direction the anchor pushes the text
            var offset = anchor == TextAnchor.TopCenter ? new Vector2(0f, 4f) : new Vector2(-4f, 0f);

            label.Add(new EntityTextComponent
            {
                Text = text,
                FontSize = o.LabelFontSize,
                TextColor = o.LabelColor,
                Anchor = anchor,
                Offset = offset,
            });
        }
        else
        {
            label.Add(new WorldTextComponent
            {
                Text = text,
                Height = o.LabelHeight,
                TextColor = o.LabelColor,
                Anchor = anchor,
                Billboard = true,
                KeepUpright = true,
            });
        }

        label.Transform.Position = position;
        Root.AddChild(label);
    }

    /// <summary>
    /// Every multiple of <paramref name="step"/> within [<paramref name="min"/>, <paramref name="max"/>],
    /// computed from integer multiples so accumulated float error cannot drop the last one.
    /// </summary>
    private static IEnumerable<float> TickValues(float min, float max, float step)
    {
        if (step <= 0f)
            yield break;

        var first = (int)MathF.Ceiling(min / step - 1e-4f);
        var last = (int)MathF.Floor(max / step + 1e-4f);

        for (var i = first; i <= last; i++)
        {
            yield return i * step;
        }
    }

    private static bool IsMultiple(float value, float step)
    {
        var ratio = value / step;
        return MathF.Abs(ratio - MathF.Round(ratio)) < 1e-4f;
    }

    private static bool IsZero(float value) => MathF.Abs(value) < 1e-5f;
}
