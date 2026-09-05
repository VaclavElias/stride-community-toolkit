using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// The GPU-facing layout of one shape, mirrored field for field by the shader's ShapeData. The
/// field order is the wire format, so do not reorder it without changing ShapeShader.sdsl to match.
/// </summary>
/// <remarks>
/// Laid out in 16-byte groups, each a <see cref="Vector4"/> or four 4-byte values, which is the
/// alignment structured buffers want and what keeps the shader's reads simple. Where a group has a
/// spare slot it carries a related scalar - the plane axes carry the border width and fill alpha,
/// the slice carries the glow width - rather than padding.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct ShapeInstance
{
    /// <summary>Bit 2 of the GPU flags: the fill runs from <see cref="FillColor"/> to <see cref="GradientColor"/> along <see cref="Gradient"/>.</summary>
    internal const int GradientFlag = 4;

    // --- Placement: the plane the shape lies in --------------------------------------------------

    /// <summary>xyz: world position of the local origin; w: plane mode.</summary>
    public readonly Vector4 Position;

    /// <summary>xyz: the plane's X axis; w: border width in pixels.</summary>
    public readonly Vector4 AxisX;

    /// <summary>xyz: the plane's Y axis; w: fill alpha.</summary>
    public readonly Vector4 AxisY;

    // --- Geometry: up to eight corners, and the rounding around them ----------------------------

    public readonly Vector4 Points12;
    public readonly Vector4 Points34;
    public readonly Vector4 Points56;
    public readonly Vector4 Points78;

    public readonly int Count;
    public readonly float Radius;
    public readonly float Scale;
    public readonly int Flags;

    // --- Colours ---------------------------------------------------------------------------------

    public readonly Color Color;
    public readonly Color FillColor;
    public readonly Color GlowColor;
    public readonly Color GradientColor;

    // --- Slice: x band depth, y cut start, z cut sweep, w glow width in pixels -------------------

    public readonly Vector4 Slice;

    // --- Dash: x length, y gap, z phase, all in pixels; w spare ----------------------------------

    public readonly Vector4 Dash;

    // --- Gradient: xy direction in the plane's local axes; zw spare ------------------------------

    public readonly Vector4 Gradient;

    internal ShapeInstance(in ShapePlane plane, in ShapeStyle style, in ShapeSlice slice, ReadOnlySpan<Vector4> packedPoints, int count, float radius, float scale)
    {
        Position = new Vector4(plane.Origin, (float)plane.Mode);
        AxisX = new Vector4(plane.AxisX, style.BorderWidth);
        AxisY = new Vector4(plane.AxisY, style.FillAlpha);
        Points12 = packedPoints[0];
        Points34 = packedPoints[1];
        Points56 = packedPoints[2];
        Points78 = packedPoints[3];
        Count = count;
        Radius = radius;
        Scale = scale;
        Flags = slice.Flags | (style.HasGradient ? GradientFlag : 0);
        Color = style.Color;
        FillColor = style.FillColor;
        GlowColor = style.GlowColor;
        GradientColor = style.GradientColor;
        Slice = new Vector4(slice.RingWidth, slice.StartAngle, slice.SweepAngle, style.GlowWidth);
        Dash = new Vector4(style.DashLength, style.DashGap, style.DashPhase, 0f);
        Gradient = new Vector4(style.GradientDirection, 0f, 0f);
    }
}