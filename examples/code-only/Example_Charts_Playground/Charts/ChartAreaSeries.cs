using Stride.CommunityToolkit.Rendering.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A shaded region on a chart: the area between two functions over a stretch of <c>x</c> - the picture of a
/// definite integral, of the gap between a measurement and a model, or simply a curve's footprint. Created
/// with <see cref="Chart.AddArea(Func{float, float}, float, float, float, Color?, string?, int)"/>.
/// </summary>
/// <remarks>
/// The region remembers its two functions and its <c>x</c> stretch, so a view-driven chart re-samples and
/// re-clips it when the visible range changes, exactly as it re-plots a curve. The fill is flat geometry
/// with a translucent colour, drawn behind the curves.
/// </remarks>
public sealed class ChartAreaSeries : ChartSeries
{
    private readonly Func<float, float> _upper;
    private readonly Func<float, float> _lower;
    private readonly float _from;
    private readonly float _to;
    private readonly int _samples;
    private readonly AreaOptions _areaOptions;

    internal ChartAreaSeries(string name, Entity entity, PolylineOptions legendOptions, AreaOptions areaOptions,
        Func<float, float> upper, Func<float, float> lower, float from, float to, int samples)
        : base(name, entity, legendOptions, isEmpty: false)
    {
        _areaOptions = areaOptions;
        _upper = upper;
        _lower = lower;
        _from = from;
        _to = to;
        _samples = samples;
    }

    /// <summary>The first <c>x</c> of the shaded stretch, as asked for.</summary>
    public float From => _from;

    /// <summary>The last <c>x</c> of the shaded stretch, as asked for.</summary>
    public float To => _to;

    /// <summary>
    /// Re-samples the region for the chart's current ranges and rebuilds its mesh; the stretch is trimmed
    /// to whatever part of it is visible.
    /// </summary>
    internal void Rebuild(Chart chart)
    {
        ReleaseModel();

        var o = chart.Options;
        var from = MathF.Max(_from, o.XMin);
        var to = MathF.Min(_to, o.XMax);

        if (to <= from)
            return;

        var upper = PolylineSampling.Function(_upper, from, to, _samples);
        var lower = PolylineSampling.Function(_lower, from, to, _samples);
        var runs = AreaMeshBuilder.Columns(upper, lower, o.YMin, o.YMax);

        if (runs.Count == 0)
            return;

        var mesh = AreaMeshBuilder.Build(chart.Game.GraphicsDevice, [.. runs], _areaOptions);
        Entity.Add(AreaExtensions.CreateModel(chart.Game, mesh, _areaOptions));
    }
}
