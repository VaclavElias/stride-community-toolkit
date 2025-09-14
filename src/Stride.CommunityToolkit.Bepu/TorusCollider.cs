using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering.ProceduralModels;

namespace Stride.CommunityToolkit.Bepu;

public static class TorusCollider
{
    public static ConvexHullCollider Create(float? majorRadius, float? minorRadius)
    {
        var validatedMajorRadius = 1f;
        var validatedMinorRadius = 0.5f;

        if (majorRadius is null)
        {
            var torusModel = new TorusProceduralModel();

            validatedMajorRadius = torusModel.Radius;
        }
        else
        {
            validatedMajorRadius = majorRadius.Value;
        }

        if (minorRadius is null)
        {
            var torusModel = new TorusProceduralModel();

            validatedMinorRadius = torusModel.Thickness;
        }
        else
        {
            validatedMinorRadius = minorRadius.Value;
        }

        var meshData = GeometricPrimitive.Torus.New(majorRadius: validatedMajorRadius, minorRadius: validatedMinorRadius);

        return meshData.ToConvexHullCollider();
    }
}