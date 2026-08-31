namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The legend: a colour swatch and name per series, stacked in the chart's top left corner.
/// </summary>
public sealed class ChartLegendOptions
{
    /// <summary>
    /// Whether a legend is built and kept in step with the series. Defaults to <see langword="true"/>; hide
    /// and show it at runtime with <see cref="Chart.LegendVisible"/>.
    /// </summary>
    public bool Visible { get; set; } = true;
}