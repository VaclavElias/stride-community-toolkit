using Stride.Graphics;
using Stride.Rendering.ProceduralModels;

namespace Stride.CommunityToolkit.Rendering.ProceduralModels;

/// <summary>
/// Procedural model that generates a triangular prism.
/// </summary>
/// <remarks>
/// The triangular cross-section lies in the X/Y plane and the prism extends along the Z axis (depth).
/// X specifies the base width of the triangle (across -X..+X), Y specifies the triangle height (across -Y..+Y),
/// and Z specifies the prism depth. The triangle is centered around Y = 0 with the apex at Y = +Y/2 and the base at Y = -Y/2.
/// Texture coordinates are generated per-face using a simple 0..1 quad mapping and scaled by <see cref="PrimitiveProceduralModelBase.UvScale"/>.
/// </remarks>
public class TriangularPrismProceduralModel : PrimitiveProceduralModelBase
{
    /// <summary>
    /// Overall extent of the prism.
    /// </summary>
    /// <remarks>
    /// X: Base width of the triangle (across -X..+X).<br/>
    /// Y: Height of the triangle (apex-to-base, centered around 0).<br/>
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
    /// Creates a triangular prism mesh.
    /// </summary>
    /// <param name="size">Overall extent where X = base width, Y = triangle height, Z = depth.</param>
    /// <param name="uScale">Optional U texture tiling scale.</param>
    /// <param name="vScale">Optional V texture tiling scale.</param>
    /// <param name="toLeftHanded">If true, marks the mesh data as left-handed (indices will be interpreted accordingly).</param>
    /// <returns>Generated mesh data for a triangular prism (18 vertices, 24 indices).</returns>
    public static GeometricMeshData<VertexPositionNormalTexture> New(Vector3 size, float uScale = 1.0f, float vScale = 1.0f, bool toLeftHanded = false)
    {
        // 3 vertices for each of the 2 triangle faces (6 total), and 4 vertices for each of the 3 rectangle faces (12 total).
        var vertices = new VertexPositionNormalTexture[18];

        // 3 indices for each triangle face (6 total), and 6 indices (2 triangles) for each rectangle face (18 total).
        var indices = new int[24];

        var tex = new Vector2[4];
        for (var i = 0; i < 4; i++)
            tex[i] = _textureCoordinates[i] * new Vector2(uScale, vScale);

        // Half extents
        var halfBase = size.X * 0.5f;    // along X
        var halfHeight = size.Y * 0.5f;  // along Y (triangle apex-to-base)
        var halfDepth = size.Z * 0.5f;   // along Z

        // Triangle XY points (projected at Z = +/-halfDepth)
        var A = new Vector2(-halfBase, -halfHeight); // left base
        var B = new Vector2(0, +halfHeight);         // apex
        var C = new Vector2(+halfBase, -halfHeight); // right base

        // Front triangle (Z = +halfDepth)
        vertices[0] = new VertexPositionNormalTexture(new Vector3(A.X, A.Y, +halfDepth), Vector3.UnitZ, tex[0]);
        vertices[1] = new VertexPositionNormalTexture(new Vector3(B.X, B.Y, +halfDepth), Vector3.UnitZ, tex[1]);
        vertices[2] = new VertexPositionNormalTexture(new Vector3(C.X, C.Y, +halfDepth), Vector3.UnitZ, tex[2]);

        // Back triangle (Z = -halfDepth)
        vertices[3] = new VertexPositionNormalTexture(new Vector3(A.X, A.Y, -halfDepth), -Vector3.UnitZ, tex[0]);
        vertices[4] = new VertexPositionNormalTexture(new Vector3(B.X, B.Y, -halfDepth), -Vector3.UnitZ, tex[1]);
        vertices[5] = new VertexPositionNormalTexture(new Vector3(C.X, C.Y, -halfDepth), -Vector3.UnitZ, tex[2]);

        // Bottom rectangle (Y = -halfHeight), normal points down
        vertices[6] = new VertexPositionNormalTexture(new Vector3(A.X, -halfHeight, +halfDepth), -Vector3.UnitY, tex[0]);
        vertices[7] = new VertexPositionNormalTexture(new Vector3(A.X, -halfHeight, -halfDepth), -Vector3.UnitY, tex[1]);
        vertices[8] = new VertexPositionNormalTexture(new Vector3(C.X, -halfHeight, -halfDepth), -Vector3.UnitY, tex[2]);
        vertices[9] = new VertexPositionNormalTexture(new Vector3(C.X, -halfHeight, +halfDepth), -Vector3.UnitY, tex[3]);

        // Left rectangle: spans A<->B along Z; compute accurate face normal.
        var leftV0 = new Vector3(A.X, A.Y, +halfDepth);
        var leftV1 = new Vector3(A.X, A.Y, -halfDepth);
        var leftV2 = new Vector3(B.X, B.Y, -halfDepth);
        var leftV3 = new Vector3(B.X, B.Y, +halfDepth);
        var leftNormal = ComputeFaceNormal(leftV0, leftV1, leftV2);

        vertices[10] = new VertexPositionNormalTexture(leftV0, leftNormal, tex[0]);
        vertices[11] = new VertexPositionNormalTexture(leftV1, leftNormal, tex[1]);
        vertices[12] = new VertexPositionNormalTexture(leftV2, leftNormal, tex[2]);
        vertices[13] = new VertexPositionNormalTexture(leftV3, leftNormal, tex[3]);

        // Right rectangle: spans B<->C along Z; compute accurate face normal.
        var rightV0 = new Vector3(C.X, C.Y, +halfDepth);
        var rightV1 = new Vector3(B.X, B.Y, +halfDepth);
        var rightV2 = new Vector3(B.X, B.Y, -halfDepth);
        var rightV3 = new Vector3(C.X, C.Y, -halfDepth);
        var rightNormal = ComputeFaceNormal(rightV0, rightV1, rightV2);

        vertices[14] = new VertexPositionNormalTexture(rightV0, rightNormal, tex[0]);
        vertices[15] = new VertexPositionNormalTexture(rightV1, rightNormal, tex[1]);
        vertices[16] = new VertexPositionNormalTexture(rightV2, rightNormal, tex[2]);
        vertices[17] = new VertexPositionNormalTexture(rightV3, rightNormal, tex[3]);

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

        static Vector3 ComputeFaceNormal(in Vector3 v0, in Vector3 v1, in Vector3 v2)
        {
            var e1 = v1 - v0;
            var e2 = v2 - v0;
            var n = Vector3.Cross(e1, e2);
            n.Normalize();
            return n;
        }
    }
}