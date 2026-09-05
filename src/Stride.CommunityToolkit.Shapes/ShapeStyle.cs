using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>How a shape is painted: its colours, and the border, fill, glow, dash pattern, gradient and opacity in force when it was submitted.</summary>
/// <param name="Color">The outline colour.</param>
/// <param name="FillColor">The fill colour, alpha included.</param>
/// <param name="BorderWidth">Outline width in pixels.</param>
/// <param name="FillAlpha">Fill intensity, 0 to 1.</param>
/// <param name="GlowWidth">Glow width in pixels; 0 for none.</param>
/// <param name="GlowColor">The glow colour.</param>
/// <param name="Dash">The dash pattern, gap resolved; a length of 0 draws solid.</param>
/// <param name="Gradient">The fill gradient, if any.</param>
/// <param name="Opacity">A multiplier on every alpha the shape produces, 0 to 1.</param>
internal readonly record struct ShapeStyle(
    Color Color,
    Color FillColor,
    float BorderWidth,
    float FillAlpha,
    float GlowWidth,
    Color GlowColor,
    DashStyle Dash,
    GradientStyle Gradient,
    float Opacity);