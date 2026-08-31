using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Rendering.Lines;

/// <summary>
/// How a band between two polylines is turned into a filled mesh by <see cref="AreaMeshBuilder"/>.
/// </summary>
/// <remarks>
/// The fill is flat geometry in the plane given by <see cref="Normal"/> - no thickness, unlike a ribbon -
/// so the colour usually carries alpha: a shaded region reads as "under this curve" while the curve and the
/// grid stay visible through it.
/// </remarks>
public sealed class AreaOptions
{
    /// <summary>The fill colour. Its alpha is what makes the region translucent; defaults to a quarter-opaque white.</summary>
    public Color Color { get; set; } = new(255, 255, 255, 64);

    /// <summary>
    /// Emissive strength. Values above <c>1</c> exceed the bloom threshold, so with post effects enabled the
    /// fill glows; <c>1</c> is a flat, unlit-looking region.
    /// </summary>
    public float EmissiveIntensity { get; set; } = 1f;

    /// <summary>The normal of the plane the fill lies in. Defaults to <see cref="Vector3.UnitZ"/>, the chart plane.</summary>
    public Vector3 Normal { get; set; } = Vector3.UnitZ;
}
