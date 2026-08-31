using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The defaults a series takes when it is added without a style of its own.
/// </summary>
public sealed class ChartSeriesOptions
{
    /// <summary>Ribbon width used for curves added without an explicit style. Defaults to <c>0.06</c>.</summary>
    public float CurveWidth { get; set; } = 0.06f;

    /// <summary>Emissive intensity used for curves added without an explicit style; above <c>1</c> glows when bloom is on. Defaults to <c>2.5</c>.</summary>
    public float EmissiveIntensity { get; set; } = 2.5f;

    /// <summary>Size of a scatter marker glyph in chart units. Defaults to <c>0.14</c>.</summary>
    public float MarkerSize { get; set; } = 0.14f;

    /// <summary>
    /// How opaque a shaded region is when no colour is given, from <c>0</c> to <c>1</c>. Defaults to
    /// <c>0.25</c> - enough to read as a region, faint enough to see the grid and curves through it.
    /// </summary>
    public float AreaOpacity { get; set; } = 0.25f;

    /// <summary>Colours handed out in turn to series added without an explicit colour.</summary>
    public IReadOnlyList<Color> Palette { get; set; } =
    [
        Color.Cyan, Color.Orange, Color.Magenta, Color.Yellow, Color.LightGreen, Color.HotPink, Color.DeepSkyBlue,
    ];
}