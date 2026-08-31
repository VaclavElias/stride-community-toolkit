namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The mouse readout added by <see cref="Chart.AddCursor"/>.
/// </summary>
public sealed class ChartCursorOptions
{
    /// <summary>Numeric format of the readout. Defaults to <c>"0.00"</c>.</summary>
    public string Format { get; set; } = "0.00";
}