using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Core.Mathematics;
using Stride.Graphics;
using static Stride.BepuPhysics.Definitions.DecomposedHulls;

namespace Stride.CommunityToolkit.Bepu;

internal static class ConvexHullColliderExtensions
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
}