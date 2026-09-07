using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// How the axes and their tick marks are drawn, and what each axis is called. Sizes are in on-screen pixels
/// and hold at any zoom or distance; live, like the rest of the options.
/// </summary>
public sealed class ChartAxesOptions
{
    /// <summary>Colour of the <c>x</c> axis. Defaults to red.</summary>
    public Color XColor { get; set; } = Color.Red;

    /// <summary>Colour of the <c>y</c> axis. Defaults to lime green.</summary>
    public Color YColor { get; set; } = Color.LimeGreen;

    /// <summary>Colour of the <c>z</c> axis of a 3D chart. Defaults to the editor's axis blue.</summary>
    public Color ZColor { get; set; } = new(0x2F, 0x6A, 0xE1);

    /// <summary>Width of the axes in pixels. Defaults to <c>2</c>.</summary>
    public float Width { get; set; } = 2f;

    /// <summary>Length of each tick mark in pixels, centred on its axis. Defaults to <c>8</c>.</summary>
    public float TickLength { get; set; } = 8f;

    /// <summary>Width of the tick marks in pixels. Defaults to <c>1.5</c>.</summary>
    public float TickWidth { get; set; } = 1.5f;

    /// <summary>The x axis title, drawn at the axis's right end. <see langword="null"/> or empty for none.</summary>
    public string? XTitle { get; set; }

    /// <summary>The y axis title, drawn at the axis's top end. <see langword="null"/> or empty for none.</summary>
    public string? YTitle { get; set; }

    /// <summary>The z axis title of a 3D chart, drawn at the axis's far end. <see langword="null"/> or empty for none.</summary>
    public string? ZTitle { get; set; }
}