using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Lines;
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

        if (_chart.Options.ShowLabels)
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
        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);

        Add(_game.CreatePolyline(
            [new Vector3(o.XMin, axisY, 0f), new Vector3(o.XMax, axisY, 0f)],
            new PolylineOptions { Width = o.AxisWidth * scale, Color = o.XAxisColor },
            "X axis"));

        Add(_game.CreatePolyline(
            [new Vector3(axisX, o.YMin, 0f), new Vector3(axisX, o.YMax, 0f)],
            new PolylineOptions { Width = o.AxisWidth * scale, Color = o.YAxisColor },
            "Y axis"));

        if (_chart.Is3D)
        {
            // The Z axis ribbon cannot lie in the chart plane (its direction is that plane's normal),
            // so its ribbon lies in the XZ plane instead
            Add(_game.CreatePolyline(
                [new Vector3(axisX, axisY, o.ZMin), new Vector3(axisX, axisY, o.ZMax)],
                new PolylineOptions { Width = o.AxisWidth * scale, Color = o.ZAxisColor, Normal = Vector3.UnitY },
                "Z axis"));
        }
    }

    private void BuildTicks()
    {
        var o = _chart.Options;
        var scale = _chart.ViewScale;

        // Ticks sit on the same clamped axis lines the axes use, centred across them
        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);
        var half = o.TickLength * scale * 0.5f;

        // One segment per tick value, all batched into a single mesh per axis
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

        // The slight Z lift keeps ticks in front of the axis ribbon instead of z-fighting with it
        if (xTicks.Count > 0)
        {
            var ticks = _game.CreateSegments(xTicks, new PolylineOptions { Width = o.TickWidth * scale, Color = o.XAxisColor }, "X ticks");
            ticks.Transform.Position = new Vector3(0f, 0f, Chart.LayerStep);
            Add(ticks);
        }

        if (yTicks.Count > 0)
        {
            var ticks = _game.CreateSegments(yTicks, new PolylineOptions { Width = o.TickWidth * scale, Color = o.YAxisColor }, "Y ticks");
            ticks.Transform.Position = new Vector3(0f, 0f, Chart.LayerStep);
            Add(ticks);
        }

        // Z ticks are little X-direction dashes along the Z axis; like the axis they lie in the XZ plane
        if (_chart.Is3D)
        {
            var zTicks = new List<(Vector3, Vector3)>();

            foreach (var z in TickValues(o.ZMin, o.ZMax, o.TickStep))
            {
                zTicks.Add((new Vector3(axisX - half, axisY, z), new Vector3(axisX + half, axisY, z)));
            }

            if (zTicks.Count > 0)
            {
                Add(_game.CreateSegments(zTicks, new PolylineOptions { Width = o.TickWidth * scale, Color = o.ZAxisColor, Normal = Vector3.UnitY }, "Z ticks"));
            }
        }
    }

    private void BuildLabels()
    {
        var o = _chart.Options;
        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);
        var gap = o.TickLength * _chart.ViewScale * 0.5f + (o.LabelMode == ChartLabelMode.World ? o.LabelHeight * 0.25f : 0f);

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

        if (_chart.Is3D)
        {
            foreach (var z in TickValues(o.ZMin, o.ZMax, o.TickStep))
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

        if (string.IsNullOrEmpty(o.Title) && string.IsNullOrEmpty(o.XTitle) && string.IsNullOrEmpty(o.YTitle) && string.IsNullOrEmpty(o.ZTitle))
            return;

        EnsureTextRenderer(_game, o.LabelMode);

        var axisY = Math.Clamp(0f, o.YMin, o.YMax);
        var axisX = Math.Clamp(0f, o.XMin, o.XMax);

        if (o.Title is { Length: > 0 } title)
        {
            // Anchored inside the top edge: on a view-driven chart YMax is the window top, so anything
            // above it would be off screen
            AddTitleLabel(title, new Vector3((o.XMin + o.XMax) * 0.5f, o.YMax, 0f), TextAnchor.TopCenter, new Vector2(0f, 10f), o.TitleFontSize, o.TitleHeight);
        }

        if (o.XTitle is { Length: > 0 } xTitle)
        {
            AddTitleLabel(xTitle, new Vector3(o.XMax, axisY, 0f), TextAnchor.TopRight, new Vector2(-4f, 10f), o.LabelFontSize, o.LabelHeight);
        }

        if (o.YTitle is { Length: > 0 } yTitle)
        {
            AddTitleLabel(yTitle, new Vector3(axisX, o.YMax, 0f), TextAnchor.TopLeft, new Vector2(10f, 4f), o.LabelFontSize, o.LabelHeight);
        }

        if (_chart.Is3D && o.ZTitle is { Length: > 0 } zTitle)
        {
            AddTitleLabel(zTitle, new Vector3(axisX, axisY, o.ZMax), TextAnchor.TopLeft, new Vector2(10f, 4f), o.LabelFontSize, o.LabelHeight);
        }
    }

    private void AddLabel(float value, Vector3 position, TextAnchor anchor)
    {
        var o = _chart.Options;
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
        Add(label);
    }

    private void AddTitleLabel(string text, Vector3 position, TextAnchor anchor, Vector2 screenOffset, float fontSize, float worldHeight)
    {
        var o = _chart.Options;
        var label = new Entity($"Title {text}");

        if (o.LabelMode == ChartLabelMode.Screen)
        {
            label.Add(new EntityTextComponent
            {
                Text = text,
                FontSize = fontSize,
                TextColor = o.LabelColor,
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
                TextColor = o.LabelColor,
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