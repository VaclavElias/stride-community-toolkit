using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Core.Mathematics;
using Stride.Graphics;
using static Stride.BepuPhysics.Definitions.DecomposedHulls;

namespace Stride.CommunityToolkit.Bepu;

public static class ConvexHullColliderExtensions
{
    public static ConvexHullCollider ToConvexHullCollider(this GeometricMeshData<VertexPositionNormalTexture> meshData)
    {
        ArgumentNullException.ThrowIfNull(meshData);
        ArgumentNullException.ThrowIfNull(meshData.Vertices);
        ArgumentNullException.ThrowIfNull(meshData.Indices);

        var vertices = meshData.Vertices;
        var indices = meshData.Indices;

        var points = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            points[i] = vertices[i].Position;
        }

        var uintIndices = new uint[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            uintIndices[i] = (uint)indices[i];
        }

        return new ConvexHullCollider
        {
            Hull = new DecomposedHulls([new DecomposedMesh([new Hull(points, uintIndices)])])
        };
    }

    public static ConvexHullCollider ToConvexHullColliderWithWelding(this GeometricMeshData<VertexPositionNormalTexture> meshData)
    {
        ArgumentNullException.ThrowIfNull(meshData);
        ArgumentNullException.ThrowIfNull(meshData.Vertices);
        ArgumentNullException.ThrowIfNull(meshData.Indices);

        // 1) Weld duplicate/near-duplicate vertices to avoid tiny edges and degenerate faces.
        //    Tune epsilon to your content scale; 1e-4 is a good start for unit-scale meshes.
        WeldVertices(meshData.Vertices, meshData.Indices, weldEpsilon: 1e-4f, out var points, out var indices);

        return new ConvexHullCollider
        {
            Hull = new DecomposedHulls([new DecomposedMesh([new Hull(points, indices)])])
        };
    }

    // Quantize a float to an integer grid for welding (avoids rounding issues).
    private static int Quantize(float v, float scale) => (int)System.MathF.Round(v * scale);

    private static void WeldVertices(
        IReadOnlyList<VertexPositionNormalTexture> vertices,
        IReadOnlyList<int> indices,
        float weldEpsilon,
        out Vector3[] outPositions,
        out uint[] outIndices)
    {
        var scale = 1f / weldEpsilon;

        var map = new Dictionary<(int X, int Y, int Z), int>(vertices.Count);
        var unique = new List<Vector3>(vertices.Count);
        var remap = new int[vertices.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            var p = vertices[i].Position;
            var key = (Quantize(p.X, scale), Quantize(p.Y, scale), Quantize(p.Z, scale));

            if (!map.TryGetValue(key, out var idx))
            {
                idx = unique.Count;
                unique.Add(p);
                map.Add(key, idx);
            }

            remap[i] = idx;
        }

        // Remap indices to the welded vertex set.
        var newIndices = new uint[indices.Count];
        for (int i = 0; i < indices.Count; i++)
        {
            newIndices[i] = (uint)remap[indices[i]];
        }

        outPositions = unique.ToArray();
        outIndices = newIndices;
    }
}