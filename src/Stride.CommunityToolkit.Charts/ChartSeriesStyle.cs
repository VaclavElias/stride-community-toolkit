using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// How one series is drawn, where it should differ from the chart's defaults.
/// </summary>
/// <remarks>
/// <para>
/// Every property is optional: what you leave unset comes from <see cref="ChartOptions.Series"/>, so a style
/// says only what is different rather than repeating the defaults.
/// </para>
/// <code>
/// chart.Plot(MathF.Sin, style: new ChartSeriesStyle { Width = 5f });     // default colour and glow, twice the width
/// chart.Plot(MathF.Cos, color: Color.Orange);                            // the common case needs no style at all
/// </code>
/// <para>
/// A <c>color</c> argument on the plotting methods wins over <see cref="Color"/> here, so one style can be
/// shared by several series that differ only in colour.
/// </para>
/// </remarks>
public sealed class ChartSeriesStyle
{
    /// <summary>Stroke width in pixels. Unset takes <see cref="ChartSeriesOptions.CurveWidth"/>, live.</summary>
    public float? Width { get; set; }

    /// <summary>The line colour. Unset takes the next colour from <see cref="ChartSeriesOptions.Palette"/>.</summary>
    public Color? Color { get; set; }

    /// <summary>
    /// The glow halo in pixels, <c>0</c> for none. Unset takes
    /// <see cref="ChartSeriesOptions.Glow"/>, live.
    /// </summary>
    public float? Glow { get; set; }
}