using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// How the tick labels are drawn.
/// </summary>
public sealed class ChartLabelOptions
{
    /// <summary>Whether tick labels are created. Defaults to <see langword="true"/>.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether labels scale with the chart or keep a pixel size. Defaults to <see cref="ChartLabelMode.World"/>.</summary>
    public ChartLabelMode Mode { get; set; } = ChartLabelMode.World;

    /// <summary>Height of the tick labels in chart units, used in <see cref="ChartLabelMode.World"/>. Defaults to <c>0.3</c>.</summary>
    public float Height { get; set; } = 0.3f;

    /// <summary>Font size of the tick labels in pixels, used in <see cref="ChartLabelMode.Screen"/>. Defaults to <c>16</c>.</summary>
    public float FontSize { get; set; } = 16f;

    /// <summary>Colour of the tick labels. Defaults to white.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Numeric format for the tick labels. Defaults to <c>"0.##"</c>.</summary>
    public string Format { get; set; } = "0.##";
}