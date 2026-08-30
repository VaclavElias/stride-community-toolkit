using Stride.CommunityToolkit.Rendering.Lines;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The chart's legend: one colour swatch and name per series, stacked in the top left corner, rebuilt
/// whenever the series change so it always matches what is drawn. The layout scales with the chart's view
/// so the legend keeps a constant on-screen size while a view-driven chart zooms.
/// </summary>
internal sealed class ChartLegend : IDisposable
{
    private const float RowStep = 0.5f;

    private readonly Game _game;
    private readonly Chart _chart;
    private Entity? _root;
    private bool _visible = true;

    internal ChartLegend(Game game, Chart chart)
    {
        _game = game;
        _chart = chart;
    }

    /// <summary>Shows or hides the legend without rebuilding it.</summary>
    internal bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            Apply();
        }
    }

    /// <summary>
    /// Tears the legend down and builds it again from the chart's current series list.
    /// </summary>
    internal void Rebuild()
    {
        Teardown();

        var o = _chart.Options;

        if (!o.ShowLegend || _chart.Series.Count == 0)
            return;

        ChartScaffold.EnsureTextRenderer(_game, o.LabelMode);

        // The whole legend hangs off one entity in the chart's top left corner; the view scale keeps its
        // apparent size constant while a view-driven chart zooms
        var scale = _chart.ViewScale;

        _root = new Entity("Legend");
        _root.Transform.Position = new Vector3(o.XMin + 0.4f * scale, o.YMax - 0.5f * scale, 3f * Chart.LayerStep);

        for (var i = 0; i < _chart.Series.Count; i++)
        {
            var series = _chart.Series[i];
            var y = -i * RowStep * scale;

            // A short ribbon in the series colour, followed by its name in the chart's label style
            var swatch = _game.CreatePolyline(
                [new Vector3(0f, y, 0f), new Vector3(0.45f * scale, y, 0f)],
                new PolylineOptions { Width = o.CurveWidth * scale, Color = series.Color, EmissiveIntensity = series.Options.EmissiveIntensity },
                $"Legend swatch {series.Name}");
            _root.AddChild(swatch);

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

            label.Transform.Position = new Vector3(0.6f * scale, y, 0f);
            _root.AddChild(label);
        }

        _chart.Root.AddChild(_root);
        Apply();
    }

    /// <summary>Removes the legend and frees the swatch ribbon buffers nothing else tracks.</summary>
    private void Teardown()
    {
        if (_root is null)
            return;

        foreach (var child in _root.GetChildren().ToArray())
        {
            if (child.Get<ModelComponent>()?.Model is { } model)
            {
                foreach (var mesh in model.Meshes)
                {
                    PolylineMeshBuilder.Release(mesh);
                }
            }
        }

        _chart.Root.RemoveChild(_root);
        _root = null;
    }

    /// <inheritdoc cref="Teardown" />
    public void Dispose() => Teardown();

    private void Apply()
    {
        if (_root is null)
            return;

        foreach (var child in _root.GetChildren())
        {
            if (child.Get<ModelComponent>() is { } model)
                model.Enabled = _visible;

            if (child.Get<EntityTextComponent>() is { } screenText)
                screenText.IsVisible = _visible;

            if (child.Get<WorldTextComponent>() is { } worldText)
                worldText.IsVisible = _visible;
        }
    }
}