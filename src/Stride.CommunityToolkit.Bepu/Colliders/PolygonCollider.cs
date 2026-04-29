using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Bepu.Extensions;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using static Stride.BepuPhysics.Definitions.DecomposedHulls;

namespace Stride.CommunityToolkit.Bepu.Colliders;

/// <summary>
/// Provides helpers to construct Bepu <see cref="ConvexHullCollider"/> instances
/// from a regular polygon extruded into a prism.
/// </summary>
public static class PolygonCollider
{
    public static ConvexHullCollider Create(Vector2[]? size)
    {
        Vector2[] validatedSize;

        if (size is null)
        {
            var prismModel = new PolygonProceduralModel();

            validatedSize = prismModel.Vertices;
        }
        else
        {
            validatedSize = size;
        }

        var meshData = PolygonProceduralModel.New(validatedSize);

        return meshData.ToConvexHullCollider();
    }

    public static ConvexHullCollider Create(float? radius = null, int? sides = null, float depth = 1f)
    {
        var defaultModel = new PolygonProceduralModel();
        var actualRadius = radius ?? defaultModel.Radius;
        var actualSides = sides ?? defaultModel.Sides;

        var verts2D = PolygonProceduralModel.GenerateRegularPolygonVertices(actualRadius, actualSides);
        var halfDepth = depth / 2f;

        var points = new Vector3[actualSides * 2];
        for (int i = 0; i < actualSides; i++)
        {
            points[i] = new Vector3(verts2D[i].X, verts2D[i].Y, +halfDepth);
            points[i + actualSides] = new Vector3(verts2D[i].X, verts2D[i].Y, -halfDepth);
        }

        var indexList = new List<uint>();

        for (int i = 1; i < actualSides - 1; i++)
        {
            indexList.Add(0);
            indexList.Add((uint)i);
            indexList.Add((uint)(i + 1));
        }

        uint backBase = (uint)actualSides;
        for (int i = 1; i < actualSides - 1; i++)
        {
            indexList.Add(backBase);
            indexList.Add((uint)(backBase + i + 1));
            indexList.Add((uint)(backBase + i));
        }

        for (int i = 0; i < actualSides; i++)
        {
            uint a = (uint)i;
            uint b = (uint)((i + 1) % actualSides);
            uint c = (uint)(i + actualSides);
            uint d = (uint)((i + 1) % actualSides + actualSides);

            indexList.Add(a); indexList.Add(b); indexList.Add(d);
            indexList.Add(a); indexList.Add(d); indexList.Add(c);
        }

        return new ConvexHullCollider
        {
            Hull = new DecomposedHulls(
            [
                new DecomposedMesh(
                [
                    new Hull(points, indexList.ToArray())
                ])
            ])
        };
    }
}