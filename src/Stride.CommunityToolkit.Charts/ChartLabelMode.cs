namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// How tick labels are drawn.
/// </summary>
public enum ChartLabelMode
{
    /// <summary>
    /// Labels are world-space text that scales with the chart, so it grows and shrinks with distance and zoom.
    /// The right choice when the chart sits in a 3D scene and is looked at from various distances.
    /// </summary>
    World,

    /// <summary>
    /// Labels are screen-space text of a fixed pixel size that follows its tick. The right choice for a flat 2D
    /// chart with an orthographic camera, where labels should stay readable at every zoom level.
    /// </summary>
    Screen,
}