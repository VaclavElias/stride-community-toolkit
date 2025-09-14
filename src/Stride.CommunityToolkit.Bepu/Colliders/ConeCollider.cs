using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering.ProceduralModels;
using System.Numerics;

namespace Stride.CommunityToolkit.Bepu.Colliders;

public static class ConeCollider
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

        return meshData.ToConvexHullCollider();
    }
}