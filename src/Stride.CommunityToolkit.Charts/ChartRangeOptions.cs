namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// What the chart shows: the bounds of each axis, and how often it is ticked.
/// </summary>
public sealed class ChartRangeOptions
{
    /// <summary>The smallest <c>x</c> shown. Defaults to <c>-5</c>.</summary>
    public float XMin { get; set; } = -5f;

    /// <summary>The largest <c>x</c> shown. Defaults to <c>5</c>.</summary>
    public float XMax { get; set; } = 5f;

    /// <summary>The smallest <c>y</c> shown. Defaults to <c>-5</c>.</summary>
    public float YMin { get; set; } = -5f;

    /// <summary>The largest <c>y</c> shown. Defaults to <c>5</c>.</summary>
    public float YMax { get; set; } = 5f;

    /// <summary>
    /// The smallest <c>z</c> shown. Defaults to <c>0</c>; leave both Z bounds equal for a flat chart, or
    /// spread them apart to get a 3D chart with a Z axis and box clipping.
    /// </summary>
    public float ZMin { get; set; }

    /// <summary>The largest <c>z</c> shown. Defaults to <c>0</c> - see <see cref="ZMin"/>.</summary>
    public float ZMax { get; set; }

    /// <summary>Spacing between tick marks, major grid lines and labels on all axes. Defaults to <c>1</c>.</summary>
    public float TickStep { get; set; } = 1f;

    /// <summary>
    /// How many minor grid cells fit in one <see cref="ChartRangeOptions.TickStep"/>; <c>0</c> or <c>1</c> means no minor grid.
    /// Defaults to <c>0</c>.
    /// </summary>
    public int MinorDivisions { get; set; }
}