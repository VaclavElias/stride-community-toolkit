using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering.ProceduralModels;
using System.Numerics;
using static Stride.BepuPhysics.Definitions.DecomposedHulls;

namespace Stride.CommunityToolkit.Bepu;

/// <summary>
/// Provides helpers to construct Bepu <see cref="ConvexHullCollider"/> instances from Stride geometric primitives.
/// </summary>
/// <remarks>
/// This implementation builds a convex hull from a generated cone mesh. If no explicit size is provided,
/// the default radius and height are taken from <see cref="ConeProceduralModel"/>.
/// The resulting hull is wrapped in a <see cref="DecomposedHulls"/> containing a single <see cref="DecomposedMesh"/>.
/// </remarks>
/// <example>
/// <code>
/// // Create a collider using defaults from ConeProceduralModel
/// var collider = ConeCollider.Create(null);
///
/// // Or create a collider with explicit radius (X) and height (Y); Z is unused
/// var collider2 = ConeCollider.Create(new Vector3(0.5f, 2.0f, 0f));
/// </code>
/// </example>
/// <seealso cref="ConvexHullCollider"/>
/// <seealso cref="GeometricPrimitive.Cone"/>
public static class ConeCollider
{
    /// <summary>
    /// Creates a Bepu <see cref="ConvexHullCollider"/> from a cone primitive.
    /// </summary>
    /// <param name="size">
    /// Optional size for the cone in world units:
    /// X = radius, Y = height, Z is unused and ignored.
    /// When <c>null</c>, defaults from <see cref="ConeProceduralModel"/> are used.
    /// </param>
    /// <returns>
    /// A configured <see cref="ConvexHullCollider"/> whose hull is computed from the generated cone mesh.
    /// </returns>
    /// <remarks>
    /// The cone mesh is generated via <see cref="GeometricPrimitive.Cone"/> with 16 radial segments.
    /// </remarks>
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