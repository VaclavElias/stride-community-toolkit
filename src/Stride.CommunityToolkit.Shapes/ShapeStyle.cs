using Stride.Core.Mathematics;
namespace Stride.CommunityToolkit.Shapes;

/// <summary>How a shape is painted: its colour, and the border and fill in force when it was submitted.</summary>
internal readonly record struct ShapeStyle(Color Color, float BorderWidth, float FillAlpha);