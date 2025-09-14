using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering.ProceduralModels;

namespace Stride.CommunityToolkit.Bepu.Colliders;

public static class TeapotCollider
{
    public static ConvexHullCollider Create(float? size)
    {
        var validatedSize = 1f;

        if (size is null)
        {
            var teapotModel = new TeapotProceduralModel();

            validatedSize = teapotModel.Size;
        }
        else
        {
            validatedSize = size.Value;
        }

        var meshData = GeometricPrimitive.Teapot.New(size: validatedSize, 16);

        return meshData.ToConvexHullCollider();
    }
}