namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The mouse readout: a marker on the chart plane under the cursor and a label with the chart-space
/// coordinates, hidden while the mouse is off the chart. The point it reads is
/// <see cref="Chart.CursorPosition"/>.
/// </summary>
public sealed class ChartCursorOptions
{
    /// <summary>Whether the readout follows the mouse. Defaults to <see langword="false"/>.</summary>
    public bool Visible { get; set; }

    /// <summary>Numeric format of the readout. Defaults to <c>"0.00"</c>.</summary>
    public string Format { get; set; } = "0.00";

    /// <summary>Radius of the ring marker in pixels. Defaults to <c>6</c>.</summary>
    public float Radius { get; set; } = 6f;

    /// <summary>
    /// Width of the soft halo around the ring, in pixels; <c>0</c> for none. Defaults to <c>8</c>, the
    /// glowing look of <see cref="ChartOptions.Glow3D"/>; <see cref="ChartOptions.Light2D"/> turns it off.
    /// </summary>
    public float Glow { get; set; } = 8f;
}