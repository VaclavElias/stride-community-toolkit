using Stride.Graphics;
using Stride.Rendering.ProceduralModels;

namespace Stride.CommunityToolkit.Rendering.ProceduralModels;

/// <summary>
/// Procedural model that generates an equilateral triangular prism.
/// </summary>
/// <remarks>
/// The triangular cross-section lies in the X/Y plane and the prism extends along the Z axis (depth).
/// The triangle is equilateral and centered around Y = 0. Its base width is taken from <see cref="Size"/>.<see cref="Vector3.X"/>,
/// and the corresponding triangle height is derived from that (not from <see cref="Size"/>.<see cref="Vector3.Y"/>).
/// <para>
/// Important: <see cref="Size"/>.<see cref="Vector3.Y"/> is currently ignored by this implementation. The effective triangle height is computed
/// from <see cref="Size"/>.<see cref="Vector3.X"/> using the equilateral relation h = sqrt(3)/2 * X, and vertices are placed at Y = ± h/2.
/// </para>
/// <para>
/// Texture coordinates are generated per-face using a simple 0..1 quad mapping and scaled by <see cref="PrimitiveProceduralModelBase.UvScale"/>.
/// </para>
/// </remarks>
public class TriangularPrismProceduralModel : PrimitiveProceduralModelBase
{
    /// <summary>
    /// Overall extent of the prism.
    /// </summary>
    /// <remarks>
    /// X: Base width of the equilateral triangle (across -X..+X). This value defines the triangle height.<br/>
    /// Y: Currently ignored.<br/>
    /// Z: Depth of the prism (along the Z axis).
    /// </remarks>
    public Vector3 Size { get; set; } = Vector3.One;

    // Base quad UVs used for all faces; scaled by U/V factors.
    private static readonly Vector2[] _textureCoordinates = [new(1, 0), new(1, 1), new(0, 1), new(0, 0)];

    /// <summary>
    /// Builds the mesh data for the current <see cref="Size"/> and <see cref="PrimitiveProceduralModelBase.UvScale"/> settings.
    /// </summary>
    /// <returns>The generated geometric mesh data.</returns>
    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData() => New(Size, UvScale.X, UvScale.Y);

    /// <summary>
    /// Creates an equilateral triangular prism mesh.
    /// </summary>
    /// <param name="size">
    /// Overall extent of the prism.
    /// X = base width of the equilateral triangle, Y = ignored, Z = depth.
    /// </param>
    /// <param name="uScale">Optional U texture tiling scale.</param>
    /// <param name="vScale">Optional V texture tiling scale.</param>
    /// <param name="toLeftHanded">If true, marks the mesh data as left-handed (indices will be interpreted accordingly).</param>
    /// <returns>Generated mesh data for an equilateral triangular prism (18 vertices, 24 indices).</returns>
    /// <remarks>
    /// The triangle cross-section is centered around Y = 0. The triangle height h is derived from X (base width) by h = sqrt(3)/2 * X,
    /// with vertices placed at Y = ± h/2. Each rectangular side is built as two triangles with per-face normals.
    /// </remarks>
    public static GeometricMeshData<VertexPositionNormalTexture> New(Vector3 size, float uScale = 1.0f, float vScale = 1.0f, bool toLeftHanded = false)
    {
        // There are 3 vertices for each of the 2 triangle faces (6 total), and 4 vertices for each of the 3 rectangle faces (12 total).
        var vertices = new VertexPositionNormalTexture[18];

        // There are 3 indices for each triangle face (6 total), and 6 indices (2 triangles) for each rectangle face (18 total).
        var indices = new int[24];

        var textureCoordinates = new Vector2[4];

        for (var i = 0; i < 4; i++)
        {
            textureCoordinates[i] = _textureCoordinates[i] * new Vector2(uScale, vScale);
        }

        // Height for an equilateral triangle with base width = size.X, expressed as half-height (h/2) so the triangle is centered around Y = 0.
        var equilateralHalfHeight = (float)Math.Sqrt(size.X * size.X - Math.Pow(size.X / 2, 2)) / 2;

        // Use half-extents for positioning so the resulting mesh fits exactly into the given size on X and Z (Y is ignored).
        size /= 2.0f;

        // Vertices for the two triangle faces
        // Triangle face 1 (front)
        vertices[0] = new VertexPositionNormalTexture(new Vector3(-size.X, -equilateralHalfHeight, size.Z), Vector3.UnitZ, textureCoordinates[0]);
        vertices[1] = new VertexPositionNormalTexture(new Vector3(0, equilateralHalfHeight, size.Z), Vector3.UnitZ, textureCoordinates[1]);
        vertices[2] = new VertexPositionNormalTexture(new Vector3(size.X, -equilateralHalfHeight, size.Z), Vector3.UnitZ, textureCoordinates[2]);

        // Triangle face 2 (back)
        vertices[3] = new VertexPositionNormalTexture(new Vector3(-size.X, -equilateralHalfHeight, -size.Z), -Vector3.UnitZ, textureCoordinates[0]);
        vertices[4] = new VertexPositionNormalTexture(new Vector3(0, equilateralHalfHeight, -size.Z), -Vector3.UnitZ, textureCoordinates[1]);
        vertices[5] = new VertexPositionNormalTexture(new Vector3(size.X, -equilateralHalfHeight, -size.Z), -Vector3.UnitZ, textureCoordinates[2]);


        // Vertices for the three rectangle faces
        // Rectangle face 1 (bottom)
        vertices[6] = new VertexPositionNormalTexture(new Vector3(-size.X, -equilateralHalfHeight, size.Z), -Vector3.UnitY, textureCoordinates[0]);
        vertices[7] = new VertexPositionNormalTexture(new Vector3(-size.X, -equilateralHalfHeight, -size.Z), -Vector3.UnitY, textureCoordinates[1]);
        vertices[8] = new VertexPositionNormalTexture(new Vector3(size.X, -equilateralHalfHeight, -size.Z), -Vector3.UnitY, textureCoordinates[2]);
        vertices[9] = new VertexPositionNormalTexture(new Vector3(size.X, -equilateralHalfHeight, size.Z), -Vector3.UnitY, textureCoordinates[3]);

        // Rectangle face 2 (left side)
        vertices[10] = new VertexPositionNormalTexture(new Vector3(-size.X, -equilateralHalfHeight, size.Z), -Vector3.UnitX, textureCoordinates[0]);
        vertices[11] = new VertexPositionNormalTexture(new Vector3(-size.X, -equilateralHalfHeight, -size.Z), -Vector3.UnitX, textureCoordinates[1]);
        vertices[12] = new VertexPositionNormalTexture(new Vector3(0, equilateralHalfHeight, -size.Z), -Vector3.UnitX, textureCoordinates[2]);
        vertices[13] = new VertexPositionNormalTexture(new Vector3(0, equilateralHalfHeight, size.Z), -Vector3.UnitX, textureCoordinates[3]);

        // Rectangle face 3 (right side)
        vertices[14] = new VertexPositionNormalTexture(new Vector3(size.X, -equilateralHalfHeight, size.Z), Vector3.UnitX, textureCoordinates[0]);
        vertices[15] = new VertexPositionNormalTexture(new Vector3(0, equilateralHalfHeight, size.Z), Vector3.UnitX, textureCoordinates[1]);
        vertices[16] = new VertexPositionNormalTexture(new Vector3(0, equilateralHalfHeight, -size.Z), Vector3.UnitX, textureCoordinates[2]);
        vertices[17] = new VertexPositionNormalTexture(new Vector3(size.X, -equilateralHalfHeight, -size.Z), Vector3.UnitX, textureCoordinates[3]);

        // Triangle face indices
        indices[0] = 0; indices[1] = 1; indices[2] = 2; // Front
        indices[3] = 3; indices[4] = 5; indices[5] = 4; // Back

        // Rectangle face indices
        // Bottom
        indices[6] = 6; indices[7] = 9; indices[8] = 8;
        indices[9] = 6; indices[10] = 8; indices[11] = 7;

        // Left
        indices[12] = 10; indices[13] = 11; indices[14] = 12;
        indices[15] = 10; indices[16] = 12; indices[17] = 13;

        // Right
        indices[18] = 14; indices[19] = 15; indices[20] = 16;
        indices[21] = 14; indices[22] = 16; indices[23] = 17;

        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, toLeftHanded) { Name = "TriangularPrism" };
    }
}