using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>How a shape is painted: its colours, and the border, fill, glow and dash pattern in force when it was submitted.</summary>
/// <param name="Color">The outline colour.</param>
/// <param name="FillColor">The fill colour, alpha included.</param>
/// <param name="BorderWidth">Outline width in pixels.</param>
/// <param name="FillAlpha">Fill intensity, 0 to 1.</param>
/// <param name="GlowWidth">Glow width in pixels; 0 for none.</param>
/// <param name="GlowColor">The glow colour.</param>
/// <param name="DashLength">Length of each dash along the outline in pixels; 0 draws solid.</param>
/// <param name="DashGap">Gap between dashes in pixels, already resolved - never 0 when dashing.</param>
/// <param name="DashPhase">Where along the outline the pattern starts, in pixels.</param>
internal readonly record struct ShapeStyle(Color Color, Color FillColor, float BorderWidth, float FillAlpha, float GlowWidth, Color GlowColor, float DashLength, float DashGap, float DashPhase);