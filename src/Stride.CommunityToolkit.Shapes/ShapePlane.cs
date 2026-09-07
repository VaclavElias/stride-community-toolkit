using Stride.Core.Mathematics;
namespace Stride.CommunityToolkit.Shapes;

/// <summary>Where a shape's flat plane sits in the world.</summary>
internal readonly record struct ShapePlane(Vector3 Origin, Vector3 AxisX, Vector3 AxisY, PlaneMode Mode);