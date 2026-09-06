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
/// chart.Plot(MathF.Sin, style: new ChartSeriesStyle { Width = 0.1f });   // default colour and glow, twice the width
/// chart.Plot(MathF.Cos, color: Color.Orange);                            // the common case needs no style at all
/// </code>
/// <para>
/// A <c>color</c> argument on the plotting methods wins over <see cref="Color"/> here, so one style can be
/// shared by several series that differ only in colour.
/// </para>
/// </remarks>
public sealed class ChartSeriesStyle
{
    /// <summary>Ribbon width in chart units. Unset takes <see cref="ChartSeriesOptions.CurveWidth"/>.</summary>
    public float? Width { get; set; }

    /// <summary>The line colour. Unset takes the next colour from <see cref="ChartSeriesOptions.Palette"/>.</summary>
    public Color? Color { get; set; }

    /// <summary>
    /// Emissive intensity; above <c>1</c> glows when bloom is on. Unset takes
    /// <see cref="ChartSeriesOptions.Glow"/>.
    /// </summary>
    public float? Glow { get; set; }
}