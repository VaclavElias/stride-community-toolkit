using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Bepu.Colliders;

public static class TriangularPrismCollider
{
    public static ConvexHullCollider Create(Vector3? size)
    {
        var validatedSize = Vector3.One;

        if (size is null)
        {
            var coneModel = new TriangularPrismProceduralModel();

            validatedSize = coneModel.Size;
        }
        else
        {
            validatedSize = size.Value;
        }

        var meshData = TriangularPrismProceduralModel.New(validatedSize);

        return meshData.ToConvexHullCollider();
    }
}