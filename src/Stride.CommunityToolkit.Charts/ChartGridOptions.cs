using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// How the grid is drawn, and which coordinate planes carry one.
/// </summary>
public sealed class ChartGridOptions
{
    /// <summary>Whether the grid is shown when the chart is created. Defaults to <see langword="false"/>; toggle later with <see cref="Chart.GridVisible"/>.</summary>
    public bool Visible { get; set; }

    /// <summary>Which coordinate planes carry a grid. Defaults to <see cref="ChartGridPlanes.XY"/>; a flat chart ignores the other planes.</summary>
    public ChartGridPlanes Planes { get; set; } = ChartGridPlanes.XY;

    /// <summary>Colour of the major grid lines. Defaults to a dim grey so curves stand out against it.</summary>
    public Color Color { get; set; } = new(90, 90, 110);

    /// <summary>Ribbon width of the major grid lines. Defaults to <c>0.012</c>.</summary>
    public float Width { get; set; } = 0.012f;

    /// <summary>Colour of the minor grid lines. Defaults to a fainter grey than <see cref="Color"/>.</summary>
    public Color MinorColor { get; set; } = new(60, 60, 75);

    /// <summary>Ribbon width of the minor grid lines. Defaults to <c>0.008</c>.</summary>
    public float MinorWidth { get; set; } = 0.008f;
}