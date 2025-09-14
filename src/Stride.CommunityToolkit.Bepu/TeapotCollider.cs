using Stride.BepuPhysics.Definitions;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering.ProceduralModels;
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
public static class TeapotCollider
{
    /// <summary>
    /// Creates a Bepu <see cref="ConvexHullCollider"/> from a teapot primitive.
    /// </summary>
    /// <param name="size">
    /// Optional size for the teapot in world units:
    /// X = scale, Y = unused, Z = unused.
    /// When <c>null</c>, defaults from <see cref="TeapotProceduralModel"/> are used.
    /// </param>
    /// <returns>
    /// A configured <see cref="ConvexHullCollider"/> whose hull is computed from the generated teapot mesh.
    /// </returns>
    /// <remarks>
    /// The teapot mesh is generated via <see cref="GeometricPrimitive.Teapot"/> with 16 radial segments.
    /// </remarks>
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
        var points = meshData.Vertices.Select(w => w.Position).ToArray();
        var uintIndices = meshData.Indices.Select(w => (uint)w).ToArray();

        var convexHullCollider = new ConvexHullCollider()
        {
            Hull = new DecomposedHulls([new DecomposedMesh([new Hull(points, uintIndices)])])
        };

        return convexHullCollider;
    }
}