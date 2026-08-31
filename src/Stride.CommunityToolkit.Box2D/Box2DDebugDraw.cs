using Stride.Core.Mathematics;
using Stride.Rendering;
using System.Runtime.InteropServices;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Immediate-mode 2D shape drawing in the Box2D testbed's style: convex polygons with a fill at 60%
/// alpha and a border that stays a constant few pixels wide at any zoom, both computed per fragment
/// by <c>Box2DDebugShader</c> from the shapes submitted this frame.
/// </summary>
/// <remarks>
/// <para>
/// Submit shapes every frame from your update logic; they are drawn once, blended in submission
/// order in the transparent render stage, and the batch resets itself after rendering. One GPU draw
/// call covers every polygon submitted, however many.
/// </para>
/// <para>
/// Register with <c>game.AddBox2DDebugDraw()</c>, which wires <see cref="Box2DDebugDrawFeature"/>
/// into the graphics compositor and returns the instance to submit shapes to.
/// </para>
/// </remarks>
public sealed class Box2DDebugDraw : RenderObject
{
    /// <summary>The GPU-facing layout of one polygon, mirrored by the shader's PolygonData.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct PolygonInstance
    {
        public readonly Vector4 Transform;
        public readonly Vector4 Points12;
        public readonly Vector4 Points34;
        public readonly Vector4 Points56;
        public readonly Vector4 Points78;
        public readonly int Count;
        public readonly float Radius;
        public readonly Color Color;
        public readonly float Pad;

        internal PolygonInstance(Vector4 transform, ReadOnlySpan<Vector4> packedPoints, int count, float radius, Color color)
        {
            Transform = transform;
            Points12 = packedPoints[0];
            Points34 = packedPoints[1];
            Points56 = packedPoints[2];
            Points78 = packedPoints[3];
            Count = count;
            Radius = radius;
            Color = color;
            Pad = 0;
        }
    }

    internal readonly List<PolygonInstance> Instances = [];

    /// <summary>
    /// Border width in on-screen pixels, constant at any zoom. The testbed default is 3; set 0 for
    /// borderless fills. Takes effect the next frame.
    /// </summary>
    public float BorderWidth { get; set; } = 3f;

    /// <summary>
    /// Fill intensity relative to the border colour, 0 to 1. The testbed value is 0.6, but its GL
    /// pipeline blends in sRGB space while Stride blends in linear space, which reads lighter for
    /// the same value - around 0.5 tends to match the testbed side by side. Takes effect the next frame.
    /// </summary>
    public float FillAlpha { get; set; } = 0.6f;

    /// <summary>
    /// Submits a solid convex polygon for this frame.
    /// </summary>
    /// <param name="vertices">The polygon corners in local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the polygon's local origin.</param>
    /// <param name="rotation">Rotation in radians about the Z axis.</param>
    /// <param name="color">The border colour; the fill is drawn at 60% of its alpha.</param>
    /// <param name="radius">Optional rounding radius added around the polygon, in world units.</param>
    /// <exception cref="ArgumentException">Thrown when fewer than 2 or more than 8 vertices are provided.</exception>
    public void DrawSolidPolygon(ReadOnlySpan<Vector2> vertices, Vector2 position, float rotation, Color color, float radius = 0f)
    {
        if (vertices.Length < 2 || vertices.Length > 8)
            throw new ArgumentException("A polygon needs between 2 and 8 vertices.", nameof(vertices));

        var (sin, cos) = MathF.SinCos(rotation);

        Span<Vector4> packed = stackalloc Vector4[4];

        for (var i = 0; i < vertices.Length; i++)
        {
            ref var slot = ref packed[i / 2];

            if (i % 2 == 0)
            {
                slot.X = vertices[i].X;
                slot.Y = vertices[i].Y;
            }
            else
            {
                slot.Z = vertices[i].X;
                slot.W = vertices[i].Y;
            }
        }

        Instances.Add(new PolygonInstance(new Vector4(position.X, position.Y, cos, sin), packed, vertices.Length, radius, color));
    }

    /// <summary>Called by the render feature once the batch is drawn; the next frame starts empty.</summary>
    internal void Reset() => Instances.Clear();
}