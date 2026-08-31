using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// How the axes and their tick marks are drawn, and what each axis is called.
/// </summary>
public sealed class ChartAxesOptions
{
    /// <summary>Colour of the <c>x</c> axis. Defaults to red.</summary>
    public Color XColor { get; set; } = Color.Red;

    /// <summary>Colour of the <c>y</c> axis. Defaults to lime green.</summary>
    public Color YColor { get; set; } = Color.LimeGreen;

    /// <summary>Colour of the <c>z</c> axis of a 3D chart. Defaults to the editor's axis blue.</summary>
    public Color ZColor { get; set; } = new(0x2F, 0x6A, 0xE1);

    /// <summary>Ribbon width of the axes. Defaults to <c>0.03</c>.</summary>
    public float Width { get; set; } = 0.03f;

    /// <summary>Length of each tick mark, centred on its axis. Defaults to <c>0.15</c>.</summary>
    public float TickLength { get; set; } = 0.15f;

    /// <summary>Ribbon width of the tick marks. Defaults to <c>0.02</c>.</summary>
    public float TickWidth { get; set; } = 0.02f;

    /// <summary>The x axis title, drawn at the axis's right end. <see langword="null"/> or empty for none.</summary>
    public string? XTitle { get; set; }

    /// <summary>The y axis title, drawn at the axis's top end. <see langword="null"/> or empty for none.</summary>
    public string? YTitle { get; set; }

    /// <summary>The z axis title of a 3D chart, drawn at the axis's far end. <see langword="null"/> or empty for none.</summary>
    public string? ZTitle { get; set; }
}