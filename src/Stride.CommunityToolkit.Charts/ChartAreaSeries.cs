using Stride.CommunityToolkit.Charts.Lines;
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
    private readonly AreaSpec _spec;
    private readonly AreaOptions _areaOptions;

    internal ChartAreaSeries(string name, Entity entity, PolylineOptions legendOptions, AreaOptions areaOptions, in AreaSpec spec)
        : base(name, entity, legendOptions, isEmpty: false)
    {
        _areaOptions = areaOptions;
        _spec = spec;
    }

    /// <summary>The first <c>x</c> of the shaded stretch, as asked for.</summary>
    public float From => _spec.From;

    /// <summary>The last <c>x</c> of the shaded stretch, as asked for.</summary>
    public float To => _spec.To;

    /// <summary>
    /// Re-samples the region for the chart's current ranges and rebuilds its mesh; the stretch is trimmed
    /// to whatever part of it is visible.
    /// </summary>
    internal void Rebuild(Chart chart)
    {
        ReleaseModel();

        var o = chart.Options;
        var from = MathF.Max(_spec.From, o.XMin);
        var to = MathF.Min(_spec.To, o.XMax);

        if (to <= from)
            return;

        var upper = PolylineSampling.Function(_spec.Upper, from, to, _spec.Samples);
        var lower = PolylineSampling.Function(_spec.Lower, from, to, _spec.Samples);
        var runs = AreaMeshBuilder.Columns(upper, lower, o.YMin, o.YMax);

        if (runs.Count == 0)
            return;

        var mesh = AreaMeshBuilder.Build(chart.Game.GraphicsDevice, [.. runs], _areaOptions);
        Entity.Add(AreaModel.Create(chart.Game, mesh, _areaOptions));
    }
}