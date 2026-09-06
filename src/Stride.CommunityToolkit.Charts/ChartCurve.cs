using Stride.CommunityToolkit.Charts.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A curve plotted from a function, <c>y = f(x)</c>. It knows the function it came from, which is what lets a
/// view-driven chart re-sample it when the visible range changes, and what makes <see cref="SetFunction"/>
/// possible.
/// </summary>
/// <remarks>
/// Returned by <see cref="Chart.Plot"/>. Samples outside the <c>y</c> range are clipped to the chart edge,
/// samples that are not finite (a function outside its domain) break the curve, and so does a zero-crossing
/// jump larger than a quarter of the chart's height between two samples - the asymptotes of <c>tan(x)</c>
/// or <c>1/x</c> - where the branches are instead extended to the chart edge. Curves plotted from points
/// rather than a function - <see cref="Chart.PlotParametric"/>, <see cref="Chart.AddLine"/> - are
/// <see cref="ChartLineSeries"/>, because there is no function to re-evaluate.
/// </remarks>
public sealed class ChartCurve : ChartSeries
{
    // The most samples a re-sample may use, so a deep zoom-out cannot make a rebuild arbitrarily expensive
    private const int MaxSamples = 8000;

    private readonly Chart _chart;
    private readonly int _sampleCount;
    private readonly float _sampleDensity;
    private Func<float, float> _function;

    internal ChartCurve(Chart chart, string name, Entity entity, PolylineOptions options, Func<float, float> function, int samples)
        : base(name, entity, options)
    {
        _chart = chart;
        _function = function;
        _sampleCount = samples;

        // Samples per chart unit at creation, so a wider range later is sampled at the same detail per
        // unit instead of stretching a fixed count across it
        _sampleDensity = samples / (chart.Options.Range.XMax - chart.Options.Range.XMin);
    }

    /// <summary>
    /// Swaps the function and rebuilds just this curve, in place: it keeps its name, colour and legend row,
    /// and no entity is created or destroyed. This is how a curve is animated - a parameter changing every
    /// frame - without the churn of removing and plotting it again.
    /// </summary>
    /// <param name="f">The new function, sampled with the density this curve was created with.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">If the curve has been removed from its chart.</exception>
    public void SetFunction(Func<float, float> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        _function = f;
        Rebuild(_chart);
    }

    /// <inheritdoc />
    internal override void Rebuild(Chart chart)
    {
        var r = chart.Options.Range;

        // Never fewer samples than asked for; the cap bounds the cost at deep zoom-out
        var samples = Math.Clamp((int)(_sampleDensity * (r.XMax - r.XMin)), _sampleCount, MaxSamples);
        var points = PolylineSampling.Function(_function, r.XMin, r.XMax, samples);
        var branches = PolylineClipping.SplitAtJumps(points, (r.YMax - r.YMin) * 0.25f, extendEnds: true);

        var runs = new List<IReadOnlyList<Vector3>>();

        foreach (var branch in branches)
        {
            runs.AddRange(chart.Clip(branch));
        }

        ReplaceRibbons(chart, runs, closed: false);
    }
}