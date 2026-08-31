using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Charts.Lines;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.Core.Mathematics;
using Stride.Engine;
using System.Globalization;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The chart's rebuildable furniture: axes, tick marks, tick labels and titles. Everything built here is
/// tracked so a range change can tear it down - freeing the ribbon buffers - and build it again for the new
/// ranges, which is how a view-driven chart follows the camera.
/// </summary>
internal sealed class ChartScaffold : IDisposable
{
    private readonly Game _game;
    private readonly Chart _chart;
    private readonly List<Entity> _entities = [];

    internal ChartScaffold(Game game, Chart chart)
    {
        _game = game;
        _chart = chart;
    }

    /// <summary>
    /// Registers the renderer the chart's label mode needs; harmless when already registered, and needed
    /// beyond tick labels because the legend, titles and cursor draw text even when labels are off.
    /// </summary>
    internal static void EnsureTextRenderer(Game game, ChartLabelMode mode)
    {
        if (mode == ChartLabelMode.Screen)
            game.AddEntityTextRenderer();
        else
            game.AddWorldTextRenderer();
    }

    /// <summary>Builds axes, ticks, labels (when enabled) and titles for the current ranges.</summary>
    internal void Build()
    {
        BuildAxes();
        BuildTicks();

        if (_chart.Options.Labels.Visible)
        {
            BuildLabels();
        }

        BuildTitles();
    }

    /// <summary>Removes everything built by <see cref="Build"/> and frees the ribbon buffers behind it.</summary>
    internal void Teardown()
    {
        foreach (var entity in _entities)
        {
            if (entity.Get<ModelComponent>()?.Model is { } model)
            {
                foreach (var mesh in model.Meshes)
                {
                    PolylineMeshBuilder.Release(mesh);
                }
            }

            _chart.Root.RemoveChild(entity);
        }

        _entities.Clear();
    }

    /// <inheritdoc cref="Teardown" />
    public void Dispose() => Teardown();

    private void BuildAxes()
    {
        var o = _chart.Options;
        var scale = _chart.ViewScale;

        // Each axis sits on the other coordinate's zero, or on the nearest edge when zero is out of range
        var axisY = Math.Clamp(0f, o.Range.YMin, o.Range.YMax);
        var axisX = Math.Clamp(0f, o.Range.XMin, o.Range.XMax);

        Add(_game.CreatePolyline(
            [new Vector3(o.Range.XMin, axisY, 0f), new Vector3(o.Range.XMax, axisY, 0f)],
            new PolylineOptions { Width = o.Axes.Width * scale, Color = o.Axes.XColor },
            "X axis"));

        Add(_game.CreatePolyline(
            [new Vector3(axisX, o.Range.YMin, 0f), new Vector3(axisX, o.Range.YMax, 0f)],
            new PolylineOptions { Width = o.Axes.Width * scale, Color = o.Axes.YColor },
            "Y axis"));

        if (_chart.Is3D)
        {
            // The Z axis ribbon cannot lie in the chart plane (its direction is that plane's normal),
            // so its ribbon lies in the XZ plane instead
            Add(_game.CreatePolyline(
                [new Vector3(axisX, axisY, o.Range.ZMin), new Vector3(axisX, axisY, o.Range.ZMax)],
                new PolylineOptions { Width = o.Axes.Width * scale, Color = o.Axes.ZColor, Normal = Vector3.UnitY },
                "Z axis"));
        }
    }

    private void BuildTicks()
    {
        var o = _chart.Options;
        var scale = _chart.ViewScale;

        // Ticks sit on the same clamped axis lines the axes use, centred across them
        var axisY = Math.Clamp(0f, o.Range.YMin, o.Range.YMax);
        var axisX = Math.Clamp(0f, o.Range.XMin, o.Range.XMax);
        var half = o.Axes.TickLength * scale * 0.5f;

        // One segment per tick value, all batched into a single mesh per axis
        var xTicks = new List<(Vector3, Vector3)>();
        foreach (var x in TickValues(o.Range.XMin, o.Range.XMax, o.Range.TickStep))
        {
            xTicks.Add((new Vector3(x, axisY - half, 0f), new Vector3(x, axisY + half, 0f)));
        }

        var yTicks = new List<(Vector3, Vector3)>();
        foreach (var y in TickValues(o.Range.YMin, o.Range.YMax, o.Range.TickStep))
        {
            yTicks.Add((new Vector3(axisX - half, y, 0f), new Vector3(axisX + half, y, 0f)));
        }

        // The slight Z lift keeps ticks in front of the axis ribbon instead of z-fighting with it
        if (xTicks.Count > 0)
        {
            var ticks = _game.CreateSegments(xTicks, new PolylineOptions { Width = o.Axes.TickWidth * scale, Color = o.Axes.XColor }, "X ticks");
            ticks.Transform.Position = new Vector3(0f, 0f, Chart.LayerStep);
            Add(ticks);
        }

        if (yTicks.Count > 0)
        {
            var ticks = _game.CreateSegments(yTicks, new PolylineOptions { Width = o.Axes.TickWidth * scale, Color = o.Axes.YColor }, "Y ticks");
            ticks.Transform.Position = new Vector3(0f, 0f, Chart.LayerStep);
            Add(ticks);
        }

        // Z ticks are little X-direction dashes along the Z axis; like the axis they lie in the XZ plane
        if (_chart.Is3D)
        {
            var zTicks = new List<(Vector3, Vector3)>();

            foreach (var z in TickValues(o.Range.ZMin, o.Range.ZMax, o.Range.TickStep))
            {
                zTicks.Add((new Vector3(axisX - half, axisY, z), new Vector3(axisX + half, axisY, z)));
            }

            if (zTicks.Count > 0)
            {
                Add(_game.CreateSegments(zTicks, new PolylineOptions { Width = o.Axes.TickWidth * scale, Color = o.Axes.ZColor, Normal = Vector3.UnitY }, "Z ticks"));
            }
        }
    }

    private void BuildLabels()
    {
        var o = _chart.Options;
        var axisY = Math.Clamp(0f, o.Range.YMin, o.Range.YMax);
        var axisX = Math.Clamp(0f, o.Range.XMin, o.Range.XMax);
        var gap = o.Axes.TickLength * _chart.ViewScale * 0.5f + (o.Labels.Mode == ChartLabelMode.World ? o.Labels.Height * 0.25f : 0f);

        foreach (var x in TickValues(o.Range.XMin, o.Range.XMax, o.Range.TickStep))
        {
            // The origin is labelled once, by the y axis, so the two "0"s do not overlap
            if (IsZero(x) && IsZero(axisY))
                continue;

            AddLabel(x, new Vector3(x, axisY - gap, 0f), TextAnchor.TopCenter);
        }

        foreach (var y in TickValues(o.Range.YMin, o.Range.YMax, o.Range.TickStep))
        {
            AddLabel(y, new Vector3(axisX - gap, y, 0f), TextAnchor.MiddleRight);
        }

        if (_chart.Is3D)
        {
            foreach (var z in TickValues(o.Range.ZMin, o.Range.ZMax, o.Range.TickStep))
            {
                // The origin is already labelled by the y axis
                if (IsZero(z) && IsZero(axisY))
                    continue;

                AddLabel(z, new Vector3(axisX - gap, axisY, z), TextAnchor.MiddleRight);
            }
        }
    }

    /// <summary>
    /// Builds the chart and axis titles, in the chart's label style: the chart title above the top edge,
    /// axis titles at the ends of their axes the way maths textbooks letter them.
    /// </summary>
    private void BuildTitles()
    {
        var o = _chart.Options;

        if (string.IsNullOrEmpty(o.Title.Text) && string.IsNullOrEmpty(o.Axes.XTitle) && string.IsNullOrEmpty(o.Axes.YTitle) && string.IsNullOrEmpty(o.Axes.ZTitle))
            return;

        EnsureTextRenderer(_game, o.Labels.Mode);

        var axisY = Math.Clamp(0f, o.Range.YMin, o.Range.YMax);
        var axisX = Math.Clamp(0f, o.Range.XMin, o.Range.XMax);

        if (o.Title.Text is { Length: > 0 } title)
        {
            // Anchored inside the top edge: on a view-driven chart YMax is the window top, so anything
            // above it would be off screen
            AddTitleLabel(title, new Vector3((o.Range.XMin + o.Range.XMax) * 0.5f, o.Range.YMax, 0f), TextAnchor.TopCenter, new Vector2(0f, 10f), o.Title.FontSize, o.Title.Height);
        }

        if (o.Axes.XTitle is { Length: > 0 } xTitle)
        {
            AddTitleLabel(xTitle, new Vector3(o.Range.XMax, axisY, 0f), TextAnchor.TopRight, new Vector2(-4f, 10f), o.Labels.FontSize, o.Labels.Height);
        }

        if (o.Axes.YTitle is { Length: > 0 } yTitle)
        {
            AddTitleLabel(yTitle, new Vector3(axisX, o.Range.YMax, 0f), TextAnchor.TopLeft, new Vector2(10f, 4f), o.Labels.FontSize, o.Labels.Height);
        }

        if (_chart.Is3D && o.Axes.ZTitle is { Length: > 0 } zTitle)
        {
            AddTitleLabel(zTitle, new Vector3(axisX, axisY, o.Range.ZMax), TextAnchor.TopLeft, new Vector2(10f, 4f), o.Labels.FontSize, o.Labels.Height);
        }
    }

    private void AddLabel(float value, Vector3 position, TextAnchor anchor)
    {
        var o = _chart.Options;
        var text = value.ToString(o.Labels.Format, CultureInfo.InvariantCulture);
        var label = new Entity($"Label {text}");

        if (o.Labels.Mode == ChartLabelMode.Screen)
        {
            // A few pixels of breathing room beyond the tick, in the direction the anchor pushes the text
            var offset = anchor == TextAnchor.TopCenter ? new Vector2(0f, 4f) : new Vector2(-4f, 0f);

            label.Add(new EntityTextComponent
            {
                Text = text,
                FontSize = o.Labels.FontSize,
                TextColor = o.Labels.Color,
                Anchor = anchor,
                Offset = offset,
            });
        }
        else
        {
            label.Add(new WorldTextComponent
            {
                Text = text,
                Height = o.Labels.Height,
                TextColor = o.Labels.Color,
                Anchor = anchor,
                Billboard = true,
                KeepUpright = true,
            });
        }

        label.Transform.Position = position;
        Add(label);
    }

    private void AddTitleLabel(string text, Vector3 position, TextAnchor anchor, Vector2 screenOffset, float fontSize, float worldHeight)
    {
        var o = _chart.Options;
        var label = new Entity($"Title {text}");

        if (o.Labels.Mode == ChartLabelMode.Screen)
        {
            label.Add(new EntityTextComponent
            {
                Text = text,
                FontSize = fontSize,
                TextColor = o.Labels.Color,
                Anchor = anchor,
                Offset = screenOffset,
            });
        }
        else
        {
            label.Add(new WorldTextComponent
            {
                Text = text,
                Height = worldHeight,
                TextColor = o.Labels.Color,
                Anchor = anchor,
                Billboard = true,
                KeepUpright = true,
            });
        }

        label.Transform.Position = position;
        Add(label);
    }

    private void Add(Entity entity)
    {
        _chart.Root.AddChild(entity);
        _entities.Add(entity);
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

    private static bool IsZero(float value) => MathF.Abs(value) < 1e-5f;
}