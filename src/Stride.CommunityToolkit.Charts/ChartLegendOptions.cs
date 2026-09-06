namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The legend: a colour swatch and name per series, stacked in the chart's top left corner.
/// </summary>
public sealed class ChartLegendOptions
{
    /// <summary>
    /// Whether the legend is shown. Defaults to <see langword="true"/>. Live: hiding it does not tear it
    /// down, and it only appears at all while the chart has at least one series.
    /// </summary>
    public bool Visible { get; set; } = true;
}