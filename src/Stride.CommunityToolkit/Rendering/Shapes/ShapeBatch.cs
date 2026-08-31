using Stride.Rendering;

namespace Stride.CommunityToolkit.Rendering.Shapes;

/// <summary>
/// Immediate-mode drawing of filled convex shapes whose outline stays a constant number of pixels
/// wide at any zoom, distance or window size, because the shader measures it per fragment from a
/// signed distance function instead of building it as geometry.
/// </summary>
/// <remarks>
/// <para>
/// Shapes are flat, but they can sit anywhere in 3D: on a plane you choose, facing the camera, or
/// swung about an axis so a capsule reads as a thick 3D line. Every shape submitted in a frame goes
/// out in a single instanced draw call, however many there are.
/// </para>
/// <para>
/// Submit shapes every frame from your update logic; they are drawn once, blended in submission
/// order, and the batch resets itself after rendering. Register with <c>game.AddShapeBatch()</c>.
/// </para>
/// <para>
/// <see cref="BorderWidth"/> and <see cref="FillAlpha"/> are current state, captured by each draw
/// call as it is made, so you can change them between calls the way you would with a sprite batch.
/// </para>
/// </remarks>
public sealed class ShapeBatch : RenderObject
{
    internal readonly List<ShapeInstance> Instances = [];

    /// <summary>
    /// How many shapes have been submitted so far this frame. Resets to zero once the batch is
    /// drawn, so read it after your own submissions and before the frame ends.
    /// </summary>
    public int Count => Instances.Count;

    /// <summary>
    /// Outline width in on-screen pixels, constant at any zoom or distance. The Box2D testbed uses
    /// 3; set 0 for a borderless fill. Captured by each draw call as it is made.
    /// </summary>
    public float BorderWidth { get; set; } = 3f;

    /// <summary>
    /// Fill intensity relative to the outline colour, 0 to 1; 0 leaves an unfilled outline. The
    /// testbed value is 0.6, but its GL pipeline blends in sRGB space while Stride blends in linear
    /// space, which reads lighter for the same value - around 0.5 tends to match it side by side.
    /// Captured by each draw call as it is made.
    /// </summary>
    public float FillAlpha { get; set; } = 0.6f;

    /// <summary>
    /// Whether shapes are tested against the depth buffer, so scene geometry can occlude them. They
    /// never write depth. The default is <c>false</c>, which draws them as an overlay on top of
    /// everything - what you want for gizmos and 2D scenes, but not for decals on the ground.
    /// </summary>
    /// <remarks>
    /// This applies to the whole batch. Call <c>game.AddShapeBatch()</c> a second time for a batch
    /// with the other setting when you need both in one scene.
    /// </remarks>
    public bool DepthTest { get; set; }

    /// <summary>
    /// Submits a convex polygon lying in the XY plane, the 2D case.
    /// </summary>
    /// <param name="vertices">The corners in local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's local origin.</param>
    /// <param name="rotation">Rotation in radians about the Z axis.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawSolidPolygon(ReadOnlySpan<Vector2> vertices, Vector2 position, float rotation, Color color, float radius = 0f)
    {
        var (sin, cos) = MathF.SinCos(rotation);

        var plane = new ShapePlane(
            new Vector3(position.X, position.Y, 0f),
            new Vector3(cos, sin, 0f),
            new Vector3(-sin, cos, 0f),
            PlaneMode.Fixed);

        Add(vertices, plane, CurrentStyle(color), radius, 1f);
    }

    /// <summary>
    /// Submits a circle in the XY plane, the 2D case.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    public void DrawSolidCircle(Vector2 center, float radius, Color color)
        => DrawSolidPolygon([Vector2.Zero], center, 0f, color, radius);

    /// <summary>
    /// Submits a convex polygon lying in an arbitrary plane in 3D.
    /// </summary>
    /// <param name="vertices">The corners in the plane's local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's local origin.</param>
    /// <param name="axisX">The plane's X axis. Normalized for you.</param>
    /// <param name="axisY">The plane's Y axis. Normalized for you.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <param name="scale">Uniform scale applied to the whole shape, radius included.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawSolidPolygon(ReadOnlySpan<Vector2> vertices, Vector3 position, Vector3 axisX, Vector3 axisY, Color color, float radius = 0f, float scale = 1f)
        => Add(vertices,
            new ShapePlane(position, Vector3.Normalize(axisX), Vector3.Normalize(axisY), PlaneMode.Fixed),
            CurrentStyle(color), radius, scale);

    /// <summary>
    /// Submits a convex polygon in the plane a rotation puts the XY plane in.
    /// </summary>
    /// <param name="vertices">The corners in local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's local origin.</param>
    /// <param name="rotation">Orientation of the shape's plane.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawSolidPolygon(ReadOnlySpan<Vector2> vertices, Vector3 position, Quaternion rotation, Color color, float radius = 0f)
        => Add(vertices,
            new ShapePlane(position, Vector3.Transform(Vector3.UnitX, rotation), Vector3.Transform(Vector3.UnitY, rotation), PlaneMode.Fixed),
            CurrentStyle(color), radius, 1f);

    /// <summary>
    /// Submits a convex polygon that always faces the camera, screen-aligned - a marker that keeps
    /// its shape and orientation from any viewpoint.
    /// </summary>
    /// <param name="vertices">The corners in local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's centre.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawBillboard(ReadOnlySpan<Vector2> vertices, Vector3 position, Color color, float radius = 0f)
        => Add(vertices,
            new ShapePlane(position, Vector3.UnitX, Vector3.UnitY, PlaneMode.Screen),
            CurrentStyle(color), radius, 1f);

    /// <summary>
    /// Submits a camera-facing circle: a point marker that stays perfectly round from any angle.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    public void DrawBillboardCircle(Vector3 center, float radius, Color color)
        => DrawBillboard([Vector2.Zero], center, color, radius);

    /// <summary>
    /// Submits a filled disc lying flat in the plane a normal defines - a ground marker, an
    /// area-of-effect indicator, a decal.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="normal">Normal of the plane the disc lies in.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    public void DrawDisc(Vector3 center, Vector3 normal, float radius, Color color)
    {
        BuildBasis(normal, out var axisX, out var axisY);

        Add([Vector2.Zero], new ShapePlane(center, axisX, axisY, PlaneMode.Fixed), CurrentStyle(color), radius, 1f);
    }

    /// <summary>
    /// Submits an unfilled circle lying flat in the plane a normal defines - a selection ring or a
    /// range indicator that does not tint what it encircles.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="normal">Normal of the plane the ring lies in.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The ring colour.</param>
    public void DrawRing(Vector3 center, Vector3 normal, float radius, Color color)
    {
        BuildBasis(normal, out var axisX, out var axisY);

        Add([Vector2.Zero], new ShapePlane(center, axisX, axisY, PlaneMode.Fixed), new ShapeStyle(color, BorderWidth, 0f), radius, 1f);
    }

    /// <summary>
    /// Submits a rectangle lying in an arbitrary plane - a panel on a wall, a floor tile, a decal.
    /// </summary>
    /// <param name="center">World position of the rectangle's centre.</param>
    /// <param name="axisX">The plane's X axis. Normalized for you.</param>
    /// <param name="axisY">The plane's Y axis. Normalized for you.</param>
    /// <param name="size">Width along X and height along Y, in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="FillAlpha"/>.</param>
    /// <param name="cornerRadius">Optional corner rounding, in world units.</param>
    public void DrawRectangle(Vector3 center, Vector3 axisX, Vector3 axisY, Vector2 size, Color color, float cornerRadius = 0f)
    {
        // The rounding radius grows the shape, so shrink the corners to keep the size as asked
        var halfWidth = MathF.Max(size.X * 0.5f - cornerRadius, 0.0001f);
        var halfHeight = MathF.Max(size.Y * 0.5f - cornerRadius, 0.0001f);

        ReadOnlySpan<Vector2> corners =
        [
            new(-halfWidth, -halfHeight),
            new(halfWidth, -halfHeight),
            new(halfWidth, halfHeight),
            new(-halfWidth, halfHeight),
        ];

        Add(corners,
            new ShapePlane(center, Vector3.Normalize(axisX), Vector3.Normalize(axisY), PlaneMode.Fixed),
            CurrentStyle(color), cornerRadius, 1f);
    }

    /// <summary>
    /// Submits a thick line between two points in 3D: a capsule swung about its own axis to face
    /// the camera, so it reads as a round-capped line of the width you ask for from any angle.
    /// </summary>
    /// <param name="start">World-space start point.</param>
    /// <param name="end">World-space end point.</param>
    /// <param name="width">Line width in world units.</param>
    /// <param name="color">The line colour.</param>
    /// <remarks>
    /// Unlike hardware line rendering, which clamps to one pixel on most drivers, this is a real
    /// world-space width. The line is drawn solid, ignoring <see cref="FillAlpha"/>.
    /// </remarks>
    public void DrawLine(Vector3 start, Vector3 end, float width, Color color)
    {
        var direction = end - start;
        var length = direction.Length();
        var lineRadius = MathF.Max(width, 0.0001f) * 0.5f;
        var center = (start + end) * 0.5f;

        // Shorter than it is wide: the round caps have swallowed the segment, so it is just a dot
        if (length <= lineRadius * 2f)
        {
            DrawBillboardCircle(center, lineRadius, color);

            return;
        }

        // The caps add the radius back at each end, so the segment stops short of the endpoints
        var halfSegment = length * 0.5f - lineRadius;

        ReadOnlySpan<Vector2> segment = [new(-halfSegment, 0f), new(halfSegment, 0f)];

        // Solid: an outline-only line would be two thin rails rather than a line
        Add(segment,
            new ShapePlane(center, direction / length, Vector3.UnitY, PlaneMode.Axial),
            new ShapeStyle(color, BorderWidth, 1f), lineRadius, 1f);
    }

    /// <summary>
    /// Submits the twelve edges of an axis-aligned box as thick lines - a bounds or selection
    /// volume whose edges keep their width at any distance.
    /// </summary>
    /// <param name="center">World-space centre of the box.</param>
    /// <param name="size">Full extent along each axis, in world units.</param>
    /// <param name="width">Edge width in world units.</param>
    /// <param name="color">The edge colour.</param>
    public void DrawWireBox(Vector3 center, Vector3 size, float width, Color color)
    {
        var half = size * 0.5f;

        Span<Vector3> corners =
        [
            center + new Vector3(-half.X, -half.Y, -half.Z),
            center + new Vector3(half.X, -half.Y, -half.Z),
            center + new Vector3(half.X, -half.Y, half.Z),
            center + new Vector3(-half.X, -half.Y, half.Z),
            center + new Vector3(-half.X, half.Y, -half.Z),
            center + new Vector3(half.X, half.Y, -half.Z),
            center + new Vector3(half.X, half.Y, half.Z),
            center + new Vector3(-half.X, half.Y, half.Z),
        ];

        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;

            DrawLine(corners[i], corners[next], width, color);
            DrawLine(corners[i + 4], corners[next + 4], width, color);
            DrawLine(corners[i], corners[i + 4], width, color);
        }
    }

    /// <summary>Called by the render feature once the batch is drawn; the next frame starts empty.</summary>
    internal void Reset() => Instances.Clear();

    /// <summary>The border and fill as they stand right now, which is what a draw call captures.</summary>
    private ShapeStyle CurrentStyle(Color color) => new(color, BorderWidth, FillAlpha);

    /// <summary>
    /// Any two perpendicular unit axes spanning the plane a normal defines. Which two does not
    /// matter for a disc, and for anything else the caller supplies its own axes.
    /// </summary>
    private static void BuildBasis(Vector3 normal, out Vector3 axisX, out Vector3 axisY)
    {
        var n = Vector3.Normalize(normal);

        // Cross with whichever world axis is least aligned, so the result is never degenerate
        var reference = MathF.Abs(n.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;

        axisX = Vector3.Normalize(Vector3.Cross(reference, n));
        axisY = Vector3.Cross(n, axisX);
    }

    private void Add(ReadOnlySpan<Vector2> vertices, in ShapePlane plane, in ShapeStyle style, float radius, float scale)
    {
        if (vertices.Length < 1 || vertices.Length > 8)
            throw new ArgumentException("A shape needs between 1 and 8 vertices.", nameof(vertices));

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

        Instances.Add(new ShapeInstance(plane, style, packed, vertices.Length, radius, scale));
    }
}
