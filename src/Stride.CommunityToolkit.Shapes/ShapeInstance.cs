using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// The GPU-facing layout of one shape, mirrored field for field by the shader's ShapeData. The
/// field order is the wire format, so do not reorder it without changing ShapeShader.sdsl to match.
/// </summary>
/// <remarks>
/// Laid out in 16-byte groups, each a <see cref="Vector4"/>, four 4-byte values or one of the named
/// groups below, which is the alignment structured buffers want and what keeps the shader's reads
/// simple. Where a group has a spare slot it carries a related scalar - the plane axes carry the
/// border width and fill alpha, the slice the glow width, the gradient the opacity - rather than
/// padding.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct ShapeInstance
{
    /// <summary>Bit 2 of the GPU flags: the fill runs from <see cref="FillColor"/> to <see cref="GradientColor"/> along the gradient direction.</summary>
    internal const int GradientFlag = 4;

    // --- Placement: the plane the shape lies in --------------------------------------------------

    /// <summary>xyz: world position of the local origin; w: plane mode.</summary>
    public readonly Vector4 Position;

    /// <summary>xyz: the plane's X axis; w: border width in pixels.</summary>
    public readonly Vector4 AxisX;

    /// <summary>xyz: the plane's Y axis; w: fill alpha.</summary>
    public readonly Vector4 AxisY;

    public readonly Vector4 Points12;
    public readonly Vector4 Points34;
    public readonly Vector4 Points56;
    public readonly Vector4 Points78;
    public readonly int Count;
    public readonly float Radius;
    public readonly float Scale;
    public readonly int Flags;
    public readonly Color Color;
    public readonly Color FillColor;
    public readonly Color GlowColor;
    public readonly Color GradientColor;
    public readonly SliceData Slice;
    public readonly DashData Dash;
    public readonly GradientData Gradient;

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
        Flags = slice.Flags | (style.Gradient.Enabled ? GradientFlag : 0);
        Color = style.Color;
        FillColor = style.FillColor;
        GlowColor = style.GlowColor;
        GradientColor = style.Gradient.Color;
        Slice = new SliceData(slice.RingWidth, slice.StartAngle, slice.SweepAngle, style.GlowWidth);
        Dash = new DashData(style.Dash.Length, style.Dash.Gap, style.Dash.Phase);
        Gradient = new GradientData(style.Gradient.Direction, style.Opacity);
    }

    /// <summary>Which part of the shape is kept, plus the glow width in the spare slot.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SliceData(float ringWidth, float angleStart, float angleSweep, float glowWidth)
    {
        /// <summary>How deep the band reaches inward from the outline, in world units, when hollow.</summary>
        public readonly float RingWidth = ringWidth;

        /// <summary>Where the angular cut starts, radians from the plane's X axis, counter-clockwise.</summary>
        public readonly float AngleStart = angleStart;

        /// <summary>How far the cut extends; 0 means no cut.</summary>
        public readonly float AngleSweep = angleSweep;

        /// <summary>Outer glow width in pixels; 0 for none.</summary>
        public readonly float GlowWidth = glowWidth;
    }

    /// <summary>The dash pattern, in pixels.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DashData(float length, float gap, float phase)
    {
        /// <summary>Length of each dash; 0 for solid.</summary>
        public readonly float Length = length;

        /// <summary>Gap between dashes.</summary>
        public readonly float Gap = gap;

        /// <summary>Where the pattern starts.</summary>
        public readonly float Phase = phase;

        private readonly float _pad = 0f;
    }

    /// <summary>The fill gradient's direction, plus the opacity in the spare slot.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GradientData(Vector2 direction, float opacity)
    {
        /// <summary>The direction the gradient runs in, in the plane's local axes.</summary>
        public readonly Vector2 Direction = direction;

        /// <summary>A multiplier on every alpha the shape produces.</summary>
        public readonly float Opacity = opacity;

        private readonly float _pad = 0f;
    }
}