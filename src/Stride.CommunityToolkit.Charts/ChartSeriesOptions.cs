using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The defaults a series takes when it is added without a style of its own.
/// </summary>
public sealed class ChartSeriesOptions
{
    /// <summary>Ribbon width used for curves added without an explicit style. Defaults to <c>0.06</c>.</summary>
    public float CurveWidth { get; set; } = 0.06f;

    /// <summary>
    /// How strongly the series glow: the emissive intensity of their material, which above <c>1</c> bleeds
    /// into the scene through the compositor's bloom. Defaults to <c>2.5</c>. Live: changing it pushes the
    /// new value into every series already on the chart on the next <see cref="Chart.Update(Stride.Engine.CameraComponent)"/>, as a
    /// parameter write rather than a rebuild, so it is cheap enough to animate.
    /// </summary>
    public float Glow { get; set; } = 2.5f;

    /// <summary>Size of a scatter marker glyph in pixels, the same at any zoom or distance. Defaults to <c>8</c>.</summary>
    public float MarkerSize { get; set; } = 8f;

    /// <summary>Stroke width of a scatter marker glyph in pixels. Defaults to <c>1.5</c>.</summary>
    public float MarkerWidth { get; set; } = 1.5f;

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