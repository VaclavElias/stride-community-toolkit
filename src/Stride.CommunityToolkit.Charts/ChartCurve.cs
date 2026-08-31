using Stride.CommunityToolkit.Charts.Lines;
using Stride.Engine;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A curve plotted from a function, <c>y = f(x)</c>. It knows the function it came from, which is what lets a
/// view-driven chart re-sample it when the visible range changes, and what makes <see cref="SetFunction"/>
/// possible.
/// </summary>
/// <remarks>
/// Returned by <see cref="Chart.Plot"/>. Curves plotted from points rather than a function -
/// <see cref="Chart.PlotParametric"/>, <see cref="Chart.AddLine"/> - are plain <see cref="ChartSeries"/>,
/// because there is no function to re-evaluate.
/// </remarks>
public sealed class ChartCurve : ChartSeries
{
    private readonly Chart _chart;

    /// <summary>The function being plotted; a view-driven chart re-samples it when the range changes.</summary>
    internal Func<float, float> Function { get; private set; }

    /// <summary>The sample count the plot was created with; re-sampling never goes below it.</summary>
    internal int SampleCount { get; }

    /// <summary>Samples per world unit at creation time, so re-sampling keeps the same detail per unit.</summary>
    internal float SampleDensity { get; }

    internal ChartCurve(Chart chart, string name, Entity entity, PolylineOptions options, bool isEmpty, in CurveSpec spec)
        : base(name, entity, options, isEmpty)
    {
        _chart = chart;
        Function = spec.Function;
        SampleCount = spec.SampleCount;
        SampleDensity = spec.SampleDensity;
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

        Function = f;
        _chart.Rebuild(this);
    }
}