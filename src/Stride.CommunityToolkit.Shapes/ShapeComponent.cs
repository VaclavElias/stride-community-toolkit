using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Draws a flat shape at this entity's world transform through a <see cref="ShapeBatch"/>, so shapes
/// take part in Stride's component system - scripts, hierarchy, enable and disable - without needing
/// a model or a material. Requires <c>game.AddShapeBatch()</c> to have been called.
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
// Runtime only, matching the toolkit's text components: the processor draws through a ShapeBatch
// that the running game registers, and the editor never has one, so running it there is wasted work.
[DefaultEntityComponentProcessor(typeof(ShapeProcessor), ExecutionMode = ExecutionMode.Runtime)]
// DataContract is what makes the component usable from Game Studio at all: without it the editor
// cannot clone the component to the game side and reports "No serializer available for type".
[DataContract("ShapeComponent")]
[Display("Shape (call AddShapeBatch)", Expand = ExpandRule.Once)]
[ComponentCategory("Rendering")]
public sealed class ShapeComponent : ActivableEntityComponent
{
    /// <summary>
    /// The shape outline in local space, counter-clockwise, at most 8 corners. May be swapped at
    /// runtime; the next frame draws the new outline. A single vertex with <see cref="Radius"/> set
    /// draws a circle; two vertices with a radius draw a capsule.
    /// </summary>
    public Vector2[] Vertices { get; set; } = [];

    /// <summary>The outline colour; the fill derives from it unless <see cref="FillColor"/> is set.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Optional rounding radius added around the outline, in world units.</summary>
    public float Radius { get; set; }

    /// <summary>
    /// Whether the shape faces the camera instead of lying in the entity's own plane. The entity's
    /// rotation and scale are ignored when this is set; only its position is used.
    /// </summary>
    public bool Billboard { get; set; }

    /// <summary>
    /// The batch this shape draws through, or <c>null</c> to use the game's default - the first one
    /// registered with <c>AddShapeBatch()</c>.
    /// </summary>
    /// <remarks>
    /// Set this whenever a scene has more than one batch and the shape must land in a particular
    /// one. Library code especially cannot assume anything about the default: it is whichever batch
    /// the host game happened to register first, which may well be depth-tested, and a marker that
    /// must never be occluded would then silently disappear behind scene geometry.
    /// </remarks>
    /// <remarks>
    /// Not serialized: a batch is a live render object owned by the running game, so there is
    /// nothing for Game Studio to author here. Assign it from code.
    /// </remarks>
    [DataMemberIgnore]
    public ShapeBatch? Batch { get; set; }

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

    /// <summary>
    /// The fill's own colour for this shape, or <c>null</c> to use the batch's
    /// <see cref="ShapeBatch.FillColor"/> - which itself defaults to filling with the outline colour.
    /// </summary>
    public Color? FillColor { get; set; }
}
