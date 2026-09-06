using Stride.Core.Mathematics;
using Stride.Rendering;
using System.Runtime.InteropServices;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Immediate-mode drawing of filled convex shapes whose outline stays a constant number of pixels
/// wide at any zoom, distance or window size, because the shader measures it per fragment from a
/// signed distance function instead of building it as geometry. "Pixels" here means pixels on a
/// 100% display: the widths follow the display's scale by default (see <see cref="AutoScale"/>),
/// so they are the same size to the eye everywhere.
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
/// <see cref="BorderWidth"/>, <see cref="Fill"/>, <see cref="Glow"/>, <see cref="Dash"/>,
/// <see cref="Gradient"/> and <see cref="Opacity"/> are current state, captured by each draw call
/// as it is made, so you can change them between calls the way you would with a sprite batch.
/// </para>
/// </remarks>
public sealed class ShapeBatch : RenderObject
{
    internal readonly List<ShapeInstance> Instances = [];

    // Every submitted shape's points, one run after another, each already in its shape's
    // normalized space; an instance says where its run starts
    internal readonly List<Vector2> Points = [];

    // Where this batch's records and points start in the frame's shared buffers; the render
    // feature sets both when it gathers every batch for upload
    internal int InstanceBase;
    internal int PointBase;

    // A polyline longer than this is split into runs that share an end point. The pixel stage
    // tests every segment of a run for every fragment of its quad, so the cap bounds the cost of a
    // very long run; where two runs meet, the shared round cap is drawn twice, which shows only
    // under an opacity below one.
    private const int PolylineRunLength = 64;

    // Scratch for closing a polyline, so a long run costs no allocation per frame
    private readonly List<Vector2> _run = [];

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
    /// How the interior is painted: the fill's own colour, or <c>null</c> for the outline colour the
    /// testbed way, and its intensity. See <see cref="ShapeFill"/>. Captured by each draw call as it
    /// is made.
    /// </summary>
    public ShapeFill Fill { get; } = new();

    /// <summary>
    /// A soft glow outside the outline - width in pixels, and a colour or <c>null</c> for the
    /// outline's. See <see cref="ShapeGlow"/>. Captured by each draw call as it is made.
    /// </summary>
    public ShapeGlow Glow { get; } = new();

    /// <summary>
    /// The dash pattern along outlines - length, gap and phase in on-screen pixels. A length of 0,
    /// the default, draws solid. Circles, arcs and lines dash; polygons stay solid. See
    /// <see cref="DashPattern"/>. Captured by each draw call as it is made.
    /// </summary>
    public DashPattern Dash { get; } = new();

    /// <summary>
    /// A gradient across the fill: the colour it runs to and the direction. <c>Gradient.Color</c>
    /// left <c>null</c>, the default, is a flat fill. See <see cref="FillGradient"/>. Captured by
    /// each draw call as it is made.
    /// </summary>
    public FillGradient Gradient { get; } = new();

    /// <summary>
    /// A multiplier on everything a shape draws - border, fill and glow alike - from 0 to 1. The
    /// default 1 changes nothing. Captured by each draw call as it is made.
    /// </summary>
    /// <remarks>
    /// This is how a widget goes disabled or fades in: one assignment, rather than an alpha edit on
    /// each of its colours. It multiplies the alpha the colours already carry, so a fill at half
    /// alpha under an opacity of a half draws at a quarter.
    /// </remarks>
    public float Opacity { get; set; } = 1f;

    /// <summary>
    /// Whether the pixel-measured widths - border, glow, dashes, pixel lines - follow the display's
    /// scale, so a 2-pixel border is the same width to the eye on a 150% laptop as on a 100%
    /// monitor. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The figure comes from <see cref="Rendering.DisplayScale"/>, shared with everything else in the
    /// toolkit that draws in pixels, and is re-read when the window moves to another monitor. Turn
    /// it off to get exactly the pixels asked for - a screenshot at a known size, or a game applying
    /// its own UI-scale setting through <see cref="Rendering.DisplayScale.Override"/> and nothing
    /// else should compound it. World-unit sizes are never affected either way.
    /// </remarks>
    public bool AutoScale { get; set; } = true;

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
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
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

        Add(vertices, plane, CurrentStyle(color), ShapeSlice.Whole, radius, 1f);
    }

    /// <summary>
    /// Submits a circle in the XY plane, the 2D case.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    public void DrawSolidCircle(Vector2 center, float radius, Color color)
        => DrawSolidPolygon([Vector2.Zero], center, 0f, color, radius);

    /// <summary>
    /// Submits a convex polygon lying in an arbitrary plane in 3D.
    /// </summary>
    /// <param name="vertices">The corners in the plane's local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's local origin.</param>
    /// <param name="axisX">The plane's X axis. Normalized for you.</param>
    /// <param name="axisY">The plane's Y axis. Normalized for you.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <param name="scale">Uniform scale applied to the whole shape, radius included.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawSolidPolygon(ReadOnlySpan<Vector2> vertices, Vector3 position, Vector3 axisX, Vector3 axisY, Color color, float radius = 0f, float scale = 1f)
        => Add(vertices,
            new ShapePlane(position, Vector3.Normalize(axisX), Vector3.Normalize(axisY), PlaneMode.Fixed),
            CurrentStyle(color), ShapeSlice.Whole, radius, scale);

    /// <summary>
    /// Submits a convex polygon in the plane a rotation puts the XY plane in.
    /// </summary>
    /// <param name="vertices">The corners in local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's local origin.</param>
    /// <param name="rotation">Orientation of the shape's plane.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawSolidPolygon(ReadOnlySpan<Vector2> vertices, Vector3 position, Quaternion rotation, Color color, float radius = 0f)
        => Add(vertices,
            new ShapePlane(position, Vector3.Transform(Vector3.UnitX, rotation), Vector3.Transform(Vector3.UnitY, rotation), PlaneMode.Fixed),
            CurrentStyle(color), ShapeSlice.Whole, radius, 1f);

    /// <summary>
    /// Submits a convex polygon that always faces the camera, screen-aligned - a marker that keeps
    /// its shape and orientation from any viewpoint.
    /// </summary>
    /// <param name="vertices">The corners in local space, counter-clockwise, at most 8.</param>
    /// <param name="position">World position of the shape's centre.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="radius">Optional rounding radius around the polygon, in world units.</param>
    /// <exception cref="ArgumentException">Fewer than 1 or more than 8 vertices were given.</exception>
    public void DrawBillboard(ReadOnlySpan<Vector2> vertices, Vector3 position, Color color, float radius = 0f)
        => Add(vertices,
            new ShapePlane(position, Vector3.UnitX, Vector3.UnitY, PlaneMode.Screen),
            CurrentStyle(color), ShapeSlice.Whole, radius, 1f);

    /// <summary>
    /// Submits a camera-facing circle: a point marker that stays perfectly round from any angle.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    public void DrawBillboardCircle(Vector3 center, float radius, Color color)
        => DrawBillboard([Vector2.Zero], center, color, radius);

    /// <summary>
    /// Submits a camera-facing disc whose radius is measured in pixels on screen, so it is the same
    /// size at any distance - a marker or a scatter point that never shrinks as the camera pulls back.
    /// </summary>
    /// <param name="center">World position of the centre.</param>
    /// <param name="pixelRadius">Radius in pixels on a 100% display; follows the display scale like the border width.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill"/>.</param>
    /// <remarks>Pixel-measured shapes are always billboards: the conversion from pixels to world units is exact only in a screen-aligned plane.</remarks>
    public void DrawPixelDisc(Vector3 center, float pixelRadius, Color color)
        => Add([Vector2.Zero], new ShapePlane(center, Vector3.UnitX, Vector3.UnitY, PlaneMode.Screen), CurrentStyle(color), ShapeSlice.Whole with { PixelRadius = true }, pixelRadius, 1f);

    /// <summary>
    /// Submits a camera-facing ring whose radius is measured in pixels on screen, stroked
    /// <see cref="BorderWidth"/> pixels wide - a cursor marker or a selection halo that keeps its size
    /// at any distance.
    /// </summary>
    /// <param name="center">World position of the centre.</param>
    /// <param name="pixelRadius">Radius of the stroke's centreline in pixels on a 100% display.</param>
    /// <param name="color">The stroke colour.</param>
    public void DrawPixelRing(Vector3 center, float pixelRadius, Color color)
        => Add([Vector2.Zero], new ShapePlane(center, Vector3.UnitX, Vector3.UnitY, PlaneMode.Screen), OutlineStyle(color), Stroke with { PixelRadius = true }, pixelRadius, 1f);

    /// <summary>
    /// Submits a filled disc lying flat in the plane a normal defines - a ground marker, an
    /// area-of-effect indicator, a decal.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="normal">Normal of the plane the disc lies in.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    public void DrawDisc(Vector3 center, Vector3 normal, float radius, Color color)
        => Add([Vector2.Zero], PlaneFromNormal(center, normal), CurrentStyle(color), ShapeSlice.Whole, radius, 1f);

    /// <summary>
    /// Submits an unfilled circle lying flat in the plane a normal defines - a selection ring or a
    /// range indicator that does not tint what it encircles.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="normal">Normal of the plane the ring lies in.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="color">The ring colour.</param>
    /// <remarks>
    /// The ring is the shape, not the disc it encloses, so a <see cref="ShapeGlow.Width"/> glows on both
    /// sides of it. <see cref="ShapeFill.Alpha"/> does not apply.
    /// </remarks>
    public void DrawRing(Vector3 center, Vector3 normal, float radius, Color color)
        => Add([Vector2.Zero], PlaneFromNormal(center, normal), OutlineStyle(color), Stroke, radius, 1f);

    /// <summary>
    /// Submits a filled ring - a disc with a hole - lying flat in the plane a normal defines, with
    /// the outline drawn around both edges. A donut, a range band, a thick unit ring.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="normal">Normal of the plane the annulus lies in.</param>
    /// <param name="outerRadius">Outer radius in world units.</param>
    /// <param name="innerRadius">Radius of the hole in world units, smaller than the outer one.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    public void DrawAnnulus(Vector3 center, Vector3 normal, float outerRadius, float innerRadius, Color color)
        => AddSector(PlaneFromNormal(center, normal), outerRadius, innerRadius, 0f, MathF.Tau, color);

    /// <summary>
    /// Submits a filled ring in the XY plane, the 2D case of <see cref="DrawAnnulus(Vector3, Vector3, float, float, Color)"/>.
    /// </summary>
    /// <param name="center">World-space centre.</param>
    /// <param name="outerRadius">Outer radius in world units.</param>
    /// <param name="innerRadius">Radius of the hole in world units, smaller than the outer one.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    public void DrawAnnulus(Vector2 center, float outerRadius, float innerRadius, Color color)
        => AddSector(PlaneXY(center), outerRadius, innerRadius, 0f, MathF.Tau, color);

    /// <summary>
    /// Submits a filled slice of a disc, cut by two radial edges, lying flat in the plane a normal
    /// defines: a pie wedge, a field-of-view cone, a cooldown sweep. With an inner radius it is a
    /// slice of a ring instead - a donut chart segment, a radial progress bar with square ends.
    /// </summary>
    /// <param name="center">World-space centre the slice is cut from.</param>
    /// <param name="normal">Normal of the plane the slice lies in.</param>
    /// <param name="radius">Outer radius in world units.</param>
    /// <param name="startAngle">Where the slice starts, in radians. See the remarks for where 0 is.</param>
    /// <param name="sweepAngle">How far it extends, in radians. Positive is counter-clockwise, negative clockwise; a full turn or more is the whole ring or disc.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="innerRadius">Radius of the hole, in world units; 0 (the default) cuts from the centre.</param>
    /// <remarks>
    /// Angles increase counter-clockwise as seen from the side the normal points to. Zero lies
    /// along the plane's X axis, which is world X for a slice lying on the ground (normal up) and
    /// for one standing in the XY plane (normal +Z); add an offset to the start angle to turn it.
    /// </remarks>
    public void DrawSector(Vector3 center, Vector3 normal, float radius, float startAngle, float sweepAngle, Color color, float innerRadius = 0f)
        => AddSector(PlaneFromNormal(center, normal), radius, innerRadius, startAngle, sweepAngle, color);

    /// <summary>
    /// Submits a filled slice of a disc or ring in the XY plane, the 2D case of
    /// <see cref="DrawSector(Vector3, Vector3, float, float, float, Color, float)"/>. Angles are
    /// counter-clockwise from the X axis.
    /// </summary>
    /// <param name="center">World-space centre the slice is cut from.</param>
    /// <param name="radius">Outer radius in world units.</param>
    /// <param name="startAngle">Where the slice starts, in radians from the X axis.</param>
    /// <param name="sweepAngle">How far it extends, in radians. Positive is counter-clockwise, negative clockwise; a full turn or more is the whole ring or disc.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="innerRadius">Radius of the hole, in world units; 0 (the default) cuts from the centre.</param>
    public void DrawSector(Vector2 center, float radius, float startAngle, float sweepAngle, Color color, float innerRadius = 0f)
        => AddSector(PlaneXY(center), radius, innerRadius, startAngle, sweepAngle, color);

    /// <summary>
    /// Submits an arc of a circle with round ends, lying flat in the plane a normal defines. With no
    /// width it is a stroke the border's pixel width - a partial <see cref="DrawRing"/>; with one it
    /// is a filled, outlined band of that world width centred on the radius - a radial progress bar.
    /// </summary>
    /// <param name="center">World-space centre of the circle.</param>
    /// <param name="normal">Normal of the plane the arc lies in.</param>
    /// <param name="radius">Radius of the arc's centreline in world units.</param>
    /// <param name="startAngle">Where the arc starts, in radians. Zero is along the plane's X axis; see <see cref="DrawSector(Vector3, Vector3, float, float, float, Color, float)"/>.</param>
    /// <param name="sweepAngle">How far it extends, in radians. Positive is counter-clockwise, negative clockwise; a full turn or more closes the ring.</param>
    /// <param name="color">The outline colour; with a width the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="width">Width of the band in world units, or 0 (the default) for a stroke.</param>
    /// <remarks>
    /// The ends are semicircles, which is what a progress ring wants. For square, radial ends use
    /// <see cref="DrawSector(Vector3, Vector3, float, float, float, Color, float)"/> with an inner radius.
    /// </remarks>
    public void DrawArc(Vector3 center, Vector3 normal, float radius, float startAngle, float sweepAngle, Color color, float width = 0f)
        => AddArc(PlaneFromNormal(center, normal), radius, startAngle, sweepAngle, color, width);

    /// <summary>
    /// Submits an arc of a circle with round ends in the XY plane, the 2D case of
    /// <see cref="DrawArc(Vector3, Vector3, float, float, float, Color, float)"/>. Angles are
    /// counter-clockwise from the X axis.
    /// </summary>
    /// <param name="center">World-space centre of the circle.</param>
    /// <param name="radius">Radius of the arc's centreline in world units.</param>
    /// <param name="startAngle">Where the arc starts, in radians from the X axis.</param>
    /// <param name="sweepAngle">How far it extends, in radians. Positive is counter-clockwise, negative clockwise; a full turn or more closes the ring.</param>
    /// <param name="color">The outline colour; with a width the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="width">Width of the band in world units, or 0 (the default) for a stroke.</param>
    public void DrawArc(Vector2 center, float radius, float startAngle, float sweepAngle, Color color, float width = 0f)
        => AddArc(PlaneXY(center), radius, startAngle, sweepAngle, color, width);

    /// <summary>
    /// Submits a rectangle lying in an arbitrary plane - a panel on a wall, a floor tile, a decal.
    /// </summary>
    /// <param name="center">World position of the rectangle's centre.</param>
    /// <param name="axisX">The plane's X axis. Normalized for you.</param>
    /// <param name="axisY">The plane's Y axis. Normalized for you.</param>
    /// <param name="size">Width along X and height along Y, in world units.</param>
    /// <param name="color">The outline colour; the fill derives from it and <see cref="ShapeFill.Alpha"/>.</param>
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
            CurrentStyle(color), ShapeSlice.Whole, cornerRadius, 1f);
    }

    /// <summary>
    /// Submits a run of points as one stroke of a world-space width, with round joins and caps - a
    /// plotted curve, a path, the outline of any shape including a concave one.
    /// </summary>
    /// <param name="points">The run, in the plane's own coordinates, of any length.</param>
    /// <param name="position">World position of the plane's origin.</param>
    /// <param name="axisX">The plane's X axis. Normalized for you.</param>
    /// <param name="axisY">The plane's Y axis. Normalized for you.</param>
    /// <param name="width">Stroke width in world units.</param>
    /// <param name="color">The stroke colour. Drawn solid, ignoring <see cref="ShapeFill.Alpha"/>.</param>
    /// <param name="closed">Whether the last point joins back to the first.</param>
    /// <remarks>
    /// Joins are round: the stroke is everything within half the width of the run itself, drawn as
    /// one shape. Only a run of more than 64 points is split, into pieces that share a point; where
    /// two pieces meet the round cap is drawn twice, which shows only under an <see cref="Opacity"/>
    /// below one, as a slightly brighter dot. <see cref="Dash"/> runs along the whole run.
    /// </remarks>
    public void DrawPolyline(ReadOnlySpan<Vector2> points, Vector3 position, Vector3 axisX, Vector3 axisY, float width, Color color, bool closed = false)
        => AddPolyline(points, new ShapePlane(position, Vector3.Normalize(axisX), Vector3.Normalize(axisY), PlaneMode.Fixed), SolidStyle(color), MathF.Max(width, 0.0001f) * 0.5f, closed);

    /// <summary>
    /// The 2D case of <see cref="DrawPolyline(ReadOnlySpan{Vector2}, Vector3, Vector3, Vector3, float, Color, bool)"/>: a stroke in the XY plane.
    /// </summary>
    public void DrawPolyline(ReadOnlySpan<Vector2> points, float width, Color color, bool closed = false)
        => DrawPolyline(points, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, width, color, closed);

    /// <summary>
    /// Submits a run of points as one stroke a constant number of pixels wide at any distance, with
    /// round joins and caps - the <see cref="DrawPixelLine"/> of curves and frames.
    /// </summary>
    /// <param name="points">The run, in the plane's own coordinates, of any length.</param>
    /// <param name="position">World position of the plane's origin.</param>
    /// <param name="axisX">The plane's X axis. Normalized for you.</param>
    /// <param name="axisY">The plane's Y axis. Normalized for you.</param>
    /// <param name="pixelWidth">Stroke width in pixels on a 100% display.</param>
    /// <param name="color">The stroke colour.</param>
    /// <param name="closed">Whether the last point joins back to the first.</param>
    public void DrawPixelPolyline(ReadOnlySpan<Vector2> points, Vector3 position, Vector3 axisX, Vector3 axisY, float pixelWidth, Color color, bool closed = false)
        => AddPolyline(points, new ShapePlane(position, Vector3.Normalize(axisX), Vector3.Normalize(axisY), PlaneMode.Fixed), OutlineStyle(color, pixelWidth), 0f, closed);

    /// <summary>
    /// The 2D case of <see cref="DrawPixelPolyline(ReadOnlySpan{Vector2}, Vector3, Vector3, Vector3, float, Color, bool)"/>: a stroke in the XY plane.
    /// </summary>
    public void DrawPixelPolyline(ReadOnlySpan<Vector2> points, float pixelWidth, Color color, bool closed = false)
        => DrawPixelPolyline(points, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, pixelWidth, color, closed);

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
    /// world-space width. The line is drawn solid, ignoring <see cref="ShapeFill.Alpha"/>.
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
            SolidStyle(color), ShapeSlice.Whole, lineRadius, 1f);
    }

    /// <summary>
    /// Submits a line whose width is measured in on-screen pixels rather than world units, so it
    /// keeps exactly the same thickness however far away it is - grid lines, axis rules, leader
    /// lines, anything that should read as drawn on the screen rather than placed in the scene.
    /// </summary>
    /// <param name="start">World-space start point.</param>
    /// <param name="end">World-space end point.</param>
    /// <param name="pixelWidth">Line width in on-screen pixels.</param>
    /// <param name="color">The line colour.</param>
    /// <remarks>
    /// This is <see cref="DrawLine"/> with its world width collapsed to nothing, which leaves the
    /// outline - already measured in pixels - drawing the whole line. <see cref="BorderWidth"/> and
    /// <see cref="ShapeFill.Alpha"/> do not apply; <paramref name="pixelWidth"/> is the width.
    /// </remarks>
    public void DrawPixelLine(Vector3 start, Vector3 end, float pixelWidth, Color color)
    {
        var direction = end - start;
        var length = direction.Length();

        if (length <= float.Epsilon) return;

        var halfSegment = length * 0.5f;

        ReadOnlySpan<Vector2> segment = [new(-halfSegment, 0f), new(halfSegment, 0f)];

        Add(segment,
            new ShapePlane((start + end) * 0.5f, direction / length, Vector3.UnitY, PlaneMode.Axial),
            OutlineStyle(color, pixelWidth), ShapeSlice.Whole, 0f, 1f);
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
    internal void Reset()
    {
        Instances.Clear();
        Points.Clear();
    }

    /// <summary>A stroke with no area: a hollow band of zero depth, which is what a ring or an arc is.</summary>
    private static readonly ShapeSlice Stroke = new(Hollow: true, RingWidth: 0f, StartAngle: 0f, SweepAngle: 0f, RoundCaps: false);

    private void AddSector(in ShapePlane plane, float radius, float innerRadius, float startAngle, float sweepAngle, Color color)
    {
        if (radius <= 0f || innerRadius >= radius || !TryNormalizeSweep(ref startAngle, ref sweepAngle)) return;

        var slice = new ShapeSlice(Hollow: innerRadius > 0f, RingWidth: radius - innerRadius, startAngle, sweepAngle, RoundCaps: false);

        Add([Vector2.Zero], plane, CurrentStyle(color), slice, radius, 1f);
    }

    private void AddArc(in ShapePlane plane, float radius, float startAngle, float sweepAngle, Color color, float width)
    {
        if (radius <= 0f || !TryNormalizeSweep(ref startAngle, ref sweepAngle)) return;

        // The band straddles the radius, so its outer edge is half a width beyond it
        var halfWidth = MathF.Max(width, 0f) * 0.5f;
        var slice = new ShapeSlice(Hollow: true, RingWidth: width, startAngle, sweepAngle, RoundCaps: true);

        // A stroke has no area to fill; a band takes the current fill like any other shape
        Add([Vector2.Zero], plane, halfWidth > 0f ? CurrentStyle(color) : OutlineStyle(color), slice, radius + halfWidth, 1f);
    }

    /// <summary>
    /// Puts a sweep into the form the shader reads: counter-clockwise, and 0 for a full turn.
    /// Returns <c>false</c> for a sweep of nothing, which draws nothing.
    /// </summary>
    private static bool TryNormalizeSweep(ref float startAngle, ref float sweepAngle)
    {
        if (sweepAngle == 0f) return false;

        // Clockwise is the same range walked from its other end
        if (sweepAngle < 0f)
        {
            startAngle += sweepAngle;
            sweepAngle = -sweepAngle;
        }

        if (sweepAngle >= MathF.Tau) sweepAngle = 0f;

        return true;
    }

    /// <summary>The colours, border, fill and glow as they stand right now, which is what a draw call captures.</summary>
    private ShapeStyle CurrentStyle(Color color)
    {
        // No explicit fill colour: the testbed's own formula, where FillAlpha scales the outline
        // colour's brightness as well as its opacity. Keeping it verbatim is what makes the Box2D
        // examples match the testbed side by side.
        if (Fill.Color is not { } fill)
        {
            // The gradient's far end gets the same treatment as the near one: scaled by the fill
            // alpha in the shader, so the two ends dim together
            return new(color, color, BorderWidth, Fill.Alpha, Glow.Width, Glow.Color ?? color, Glow.Additive, Dash.Capture(), CaptureGradient(color), Opacity);
        }

        // An explicit fill colour is used as given. Dimming its brightness the testbed way would
        // turn a chosen colour into a muddy version of itself, so the fill alpha scales opacity only.
        var near = WithFillAlpha(fill);
        var gradient = Gradient.Color is { } to ? new GradientStyle(true, WithFillAlpha(to), Gradient.Direction) : new GradientStyle(false, near, Gradient.Direction);

        return new(color, near, BorderWidth, 1f, Glow.Width, Glow.Color ?? color, Glow.Additive, Dash.Capture(), gradient, Opacity);
    }

    /// <summary>The current style with the fill turned off, for shapes that are all outline.</summary>
    private ShapeStyle OutlineStyle(Color color) => new(color, color, BorderWidth, 0f, Glow.Width, Glow.Color ?? color, Glow.Additive, Dash.Capture(), new GradientStyle(false, color, Gradient.Direction), Opacity);

    /// <summary>The current style with the fill turned off and its own outline width.</summary>
    private ShapeStyle OutlineStyle(Color color, float borderWidth) => new(color, color, borderWidth, 0f, Glow.Width, Glow.Color ?? color, Glow.Additive, Dash.Capture(), new GradientStyle(false, color, Gradient.Direction), Opacity);

    /// <summary>
    /// The current style with the fill turned all the way up, for shapes that are drawn solid. A
    /// gradient still applies: a line that fades out along its length is a leader line.
    /// </summary>
    private ShapeStyle SolidStyle(Color color) => new(color, color, BorderWidth, 1f, Glow.Width, Glow.Color ?? color, Glow.Additive, Dash.Capture(), CaptureGradient(color), Opacity);

    /// <summary>The gradient as a draw call captures it, its far colour taken as given - the shader scales it by the fill alpha.</summary>
    private GradientStyle CaptureGradient(Color fallback) => new(Gradient.Color is not null, Gradient.Color ?? fallback, Gradient.Direction);

    /// <summary>An explicit colour with the fill alpha applied to its opacity only.</summary>
    private Color WithFillAlpha(Color colour) => new(colour.R, colour.G, colour.B, (byte)Math.Clamp(colour.A * Fill.Alpha, 0f, 255f));

    /// <summary>The XY plane at a 2D position, the plane every 2D call draws in.</summary>
    private static ShapePlane PlaneXY(Vector2 center)
        => new(new Vector3(center.X, center.Y, 0f), Vector3.UnitX, Vector3.UnitY, PlaneMode.Fixed);

    /// <summary>
    /// The plane a normal defines, with any two perpendicular unit axes spanning it. Which two
    /// only shows for shapes with an angular cut, so they are chosen so that angle 0 is world X
    /// both for a shape lying on the ground and for one standing in the XY plane, and the pair is
    /// right-handed about the normal so angles run counter-clockwise seen from its side.
    /// </summary>
    private static ShapePlane PlaneFromNormal(Vector3 center, Vector3 normal)
    {
        var n = Vector3.Normalize(normal);

        // Cross with whichever world axis is least aligned, so the result is never degenerate
        var axisX = MathF.Abs(n.Y) < 0.9f
            ? Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n))
            : Vector3.Normalize(Vector3.Cross(n, Vector3.UnitZ));

        var axisY = Vector3.Cross(n, axisX);

        return new ShapePlane(center, axisX, axisY, PlaneMode.Fixed);
    }

    /// <summary>
    /// Submits a run as one stroke, or as a few runs sharing their end points when it is very
    /// long, each carrying the arc length at which it starts so a dash pattern continues across them.
    /// </summary>
    private void AddPolyline(ReadOnlySpan<Vector2> points, in ShapePlane plane, in ShapeStyle style, float radius, bool closed)
    {
        if (points.Length < 2) return;

        _run.Clear();

        foreach (var point in points) _run.Add(point);

        if (closed) _run.Add(points[0]);

        var run = CollectionsMarshal.AsSpan(_run);
        var offset = 0f;

        for (var start = 0; start + 1 < run.Length; start += PolylineRunLength - 1)
        {
            var piece = run.Slice(start, Math.Min(PolylineRunLength, run.Length - start));

            Add(piece, plane, style, ShapeSlice.Whole with { Polyline = true, RunOffset = offset }, radius, 1f);

            for (var i = 0; i + 1 < piece.Length; i++) offset += Vector2.Distance(piece[i], piece[i + 1]);
        }
    }

    /// <summary>
    /// Records one shape: its points shifted and scaled into the 2x2 quad the shader draws, so
    /// the pixel stage reads them ready to use, and the record that says where they are.
    /// </summary>
    private void Add(ReadOnlySpan<Vector2> vertices, in ShapePlane plane, in ShapeStyle style, in ShapeSlice slice, float radius, float scale)
    {
        if (vertices.Length < 1)
            throw new ArgumentException("A shape needs at least one vertex.", nameof(vertices));

        // A pixel-measured radius is converted to world units on the GPU, at the shape's own
        // depth, and the scale with it - which only works out when there is nothing else to scale
        if (slice.PixelRadius && vertices.Length != 1)
            throw new ArgumentException("A shape with a pixel-measured radius is a single point.", nameof(vertices));

        var lower = vertices[0];
        var upper = vertices[0];

        for (var i = 1; i < vertices.Length; i++)
        {
            lower = Vector2.Min(lower, vertices[i]);
            upper = Vector2.Max(upper, vertices[i]);
        }

        var center = (lower + upper) * 0.5f;
        var extent = upper - lower;

        // The radius reaches beyond the furthest point either way, so radius plus half the widest
        // extent is the half-size of the square that holds the outline; the border and glow
        // get their room from the vertex stage's margin
        var localScale = radius + 0.5f * MathF.Max(extent.X, extent.Y);
        var invScale = 1f / MathF.Max(localScale, 0.000001f);

        var offset = Points.Count;

        for (var i = 0; i < vertices.Length; i++)
        {
            Points.Add(invScale * (vertices[i] - center));
        }

        Instances.Add(new ShapeInstance(plane, style, slice, center, localScale, offset, vertices.Length, radius, scale));
    }
}