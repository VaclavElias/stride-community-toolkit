using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// The GPU-facing layout of one shape, mirrored field for field by the shader's ShapeData. The
/// field order is the wire format, so do not reorder it without changing ShapeShader.sdsl to match.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct ShapeInstance
{
    public readonly Vector4 Position;
    public readonly Vector4 AxisX;
    public readonly Vector4 AxisY;
    public readonly Vector4 Points12;
    public readonly Vector4 Points34;
    public readonly Vector4 Points56;
    public readonly Vector4 Points78;
    public readonly int Count;
    public readonly float Radius;
    public readonly Color Color;
    public readonly float Scale;
    public readonly Color FillColor;

    // Keeps the stride a multiple of 16 bytes, which every GPU is happy with
    private readonly float _pad0;
    private readonly float _pad1;
    private readonly float _pad2;

    internal ShapeInstance(in ShapePlane plane, in ShapeStyle style, ReadOnlySpan<Vector4> packedPoints, int count, float radius, float scale)
    {
        // The spare w of each vector carries the per-shape values the layout has no other room for;
        // the shader unpacks them from the same slots
        Position = new Vector4(plane.Origin, (float)plane.Mode);
        AxisX = new Vector4(plane.AxisX, style.BorderWidth);
        AxisY = new Vector4(plane.AxisY, style.FillAlpha);
        Points12 = packedPoints[0];
        Points34 = packedPoints[1];
        Points56 = packedPoints[2];
        Points78 = packedPoints[3];
        Count = count;
        Radius = radius;
        Color = style.Color;
        Scale = scale;
        FillColor = style.FillColor;
        _pad0 = 0;
        _pad1 = 0;
        _pad2 = 0;
    }
}
