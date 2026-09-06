using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A plane a stroke is drawn in, the way the batch's polyline calls take one: a world-space origin and
/// the two axes its points are measured along.
/// </summary>
/// <param name="Position">The world position of the plane's origin.</param>
/// <param name="AxisX">The direction of the plane's <c>x</c>; any length.</param>
/// <param name="AxisY">The direction of the plane's <c>y</c>; any length.</param>
internal readonly record struct ChartPlane(Vector3 Position, Vector3 AxisX, Vector3 AxisY);