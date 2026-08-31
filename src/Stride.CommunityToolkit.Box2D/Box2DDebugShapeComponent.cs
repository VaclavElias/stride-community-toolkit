using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Draws a testbed-style 2D shape at this entity's world transform through <see cref="Box2DDebugDraw"/>,
/// so shapes participate in Stride's component system - scripts, hierarchy, enable/disable - without
/// needing a model. Requires <c>game.AddBox2DDebugDraw()</c> to have been called.
/// </summary>
/// <remarks>
/// The entity's world position and Z rotation place the shape; scale is ignored. Shapes submitted
/// this way draw before manual <see cref="Box2DDebugDraw.DrawSolidPolygon"/> calls made later the
/// same frame.
/// </remarks>
[DefaultEntityComponentProcessor(typeof(Box2DDebugShapeProcessor))]
public sealed class Box2DDebugShapeComponent : ActivableEntityComponent
{
    /// <summary>
    /// The shape outline in local space, counter-clockwise, at most 8 corners. May be swapped at
    /// runtime; the next frame draws the new outline. A single vertex with <see cref="Radius"/> set
    /// draws a circle; two vertices with a radius draw a capsule.
    /// </summary>
    public Vector2[] Vertices { get; set; } = [];

    /// <summary>The border colour; the fill derives from it like every testbed shape.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Optional rounding radius added around the polygon, in world units.</summary>
    public float Radius { get; set; }
}

