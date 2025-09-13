using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering.ProceduralModels;
using System.Numerics;
using static Stride.BepuPhysics.Definitions.DecomposedHulls;

namespace Stride.CommunityToolkit.Bepu;

public static class ConverHullCollider
{
    public static ConvexHullCollider Create(Vector3? size)
    {
        var validatedSize = Vector3.One;

        if (size is null)
        {
            var coneModel = new ConeProceduralModel();

            validatedSize = new(coneModel.Radius, coneModel.Height, 1);
        }
        else
        {
            validatedSize = size.Value;
        }

        var meshData = GeometricPrimitive.Cone.New(radius: validatedSize.X, height: validatedSize.Y, 16);
        var points = meshData.Vertices.Select(w => w.Position).ToArray();
        var uintIndices = meshData.Indices.Select(w => (uint)w).ToArray();

        var convexHullCollider = new ConvexHullCollider()
        {
            Hull = new DecomposedHulls([new DecomposedMesh([new Hull(points, uintIndices)])])
        };

        return convexHullCollider;
    }
}