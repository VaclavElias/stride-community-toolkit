using Stride.CommunityToolkit.Charts.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A line through given points - measured data, a hand-drawn shape, or a parametric curve already sampled.
/// Created by <see cref="Chart.AddLine"/> and <see cref="Chart.PlotParametric"/>. It remembers its points,
/// so a view-driven chart re-clips it when the visible range changes.
/// </summary>
public sealed class ChartLineSeries : ChartSeries
{
    private readonly bool _closed;
    private readonly bool _clip;

    /// <summary>The points, in chart units, as given.</summary>
    public IReadOnlyList<Vector3> Points { get; }

    internal ChartLineSeries(string name, Entity entity, PolylineOptions options, IReadOnlyList<Vector3> points, bool closed, bool clip)
        : base(name, entity, options)
    {
        Points = points;
        _closed = closed && points.Count > 1;
        _clip = clip;
    }

    /// <inheritdoc />
    internal override void Rebuild(Chart chart)
    {
        // A closed shape is clipped as an open one that returns to its start, and drawn closed after all
        // only when nothing was cut, so the seam gets a proper mitred join
        IReadOnlyList<Vector3> source = _closed ? [.. Points, Points[0]] : Points;

        var clipped = _clip ? chart.Clip(source) : PolylineClipping.SplitAtNonFinite(source);

        var keepClosed = _closed && clipped.Count == 1 && clipped[0].Length == source.Count
            && clipped[0][0] == source[0] && clipped[0][^1] == source[^1];

        var runs = new List<IReadOnlyList<Vector3>>();

        if (keepClosed)
        {
            runs.Add(Points);
        }
        else
        {
            foreach (var run in clipped)
            {
                runs.Add(run);
            }
        }

        ReplaceRibbons(chart, runs, keepClosed);
    }
}