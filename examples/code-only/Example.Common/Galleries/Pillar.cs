namespace Example.Common.Galleries;

/// <summary>A solid block a station can hide things behind or hang markers from.</summary>
/// <param name="Base">The centre of its footprint on the ground, in world coordinates.</param>
/// <param name="Height">Its height; the top is <see cref="Base"/> lifted by this.</param>
public readonly record struct Pillar(Vector3 Base, float Height)
{
    /// <summary>The centre of the top face.</summary>
    public Vector3 Top => Base + Vector3.UnitY * Height;

    /// <summary>The centre of the block.</summary>
    public Vector3 Centre => Base + Vector3.UnitY * (Height * 0.5f);
}