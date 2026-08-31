using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Draws a flat shape at this entity's world transform through the game's <see cref="ShapeBatch"/>,
/// so shapes take part in Stride's component system - scripts, hierarchy, enable and disable -
/// without needing a model or a material. Requires <c>game.AddShapeBatch()</c> to have been called.
/// </summary>
/// <remarks>
/// <para>
/// The entity's world matrix places the shape: its X and Y axes become the plane the shape lies in,
/// so a rotated entity carries the shape into 3D, and the length of the X axis is taken as a uniform
/// scale. In a 2D scene, where entities only ever rotate about Z, this is just position and rotation.
/// </para>
/// <para>
/// Shapes submitted this way draw before manual <see cref="ShapeBatch"/> calls made later in the
/// same frame.
/// </para>
/// </remarks>
[DefaultEntityComponentProcessor(typeof(ShapeProcessor))]
public sealed class ShapeComponent : ActivableEntityComponent
{
    /// <summary>
    /// The shape outline in local space, counter-clockwise, at most 8 corners. May be swapped at
    /// runtime; the next frame draws the new outline. A single vertex with <see cref="Radius"/> set
    /// draws a circle; two vertices with a radius draw a capsule.
    /// </summary>
    public Vector2[] Vertices { get; set; } = [];

    /// <summary>The outline colour; the fill derives from it.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Optional rounding radius added around the outline, in world units.</summary>
    public float Radius { get; set; }

    /// <summary>
    /// Whether the shape faces the camera instead of lying in the entity's own plane. The entity's
    /// rotation and scale are ignored when this is set; only its position is used.
    /// </summary>
    public bool Billboard { get; set; }

    /// <summary>
    /// Outline width in on-screen pixels for this shape, or <c>null</c> to use the batch's
    /// <see cref="ShapeBatch.BorderWidth"/>.
    /// </summary>
    public float? BorderWidth { get; set; }

    /// <summary>
    /// Fill intensity for this shape, 0 to 1, or <c>null</c> to use the batch's
    /// <see cref="ShapeBatch.FillAlpha"/>. Set 0 for an unfilled outline.
    /// </summary>
    public float? FillAlpha { get; set; }
}