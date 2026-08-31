using Box2D.NET;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Hulls;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Builds Box2D shapes (fixtures) matching the toolkit's 2D primitive types, so a body's collision
/// shape lines up with the rendered <see cref="Primitive2DModelType"/> mesh of the same size.
/// </summary>
public static class ShapeFixtureBuilder
{
    /// <summary>
    /// Creates and attaches a Box2D fixture matching the given primitive type and dimensions.
    /// </summary>
    /// <param name="type">The 2D primitive type describing the shape's geometry.</param>
    /// <param name="size">
    /// The shape dimensions: width/height for boxes and triangles, X as radius for circles,
    /// X as radius and Y as total height for capsules.
    /// </param>
    /// <param name="bodyId">The Box2D body to which the generated shape will be attached.</param>
    /// <param name="shapeDef">
    /// Optional shape definition (density, friction, restitution, sensor flag). If <c>null</c>,
    /// <see cref="CreateDefaultShapeDef"/> is used.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if the shape type is not supported.</exception>
    /// <example>
    /// <code>
    /// var bodyId = simulation.CreateStaticBody(entity, position);
    /// ShapeFixtureBuilder.AttachShape(Primitive2DModelType.Circle, new Vector2(0.5f), bodyId);
    /// </code>
    /// </example>
    public static void AttachShape(Primitive2DModelType type, Vector2 size, B2BodyId bodyId, B2ShapeDef? shapeDef = null)
    {
        var finalShapeDef = shapeDef ?? CreateDefaultShapeDef();

        switch (type)
        {
            case Primitive2DModelType.Square:
            case Primitive2DModelType.Rectangle:
                CreateBox(size, bodyId, finalShapeDef);
                break;
            case Primitive2DModelType.Circle:
                CreateCircle(size, bodyId, finalShapeDef);
                break;
            case Primitive2DModelType.Triangle:
                CreateTriangle(size, bodyId, finalShapeDef);
                break;
            case Primitive2DModelType.Capsule:
                CreateCapsule(size, bodyId, finalShapeDef);
                break;
            default:
                throw new ArgumentException($"Unsupported shape type: {type}");
        }
    }

    /// <summary>
    /// Creates a <see cref="B2ShapeDef"/> with Box2D's own defaults (density 1, friction 0.6,
    /// restitution 0). Use <see cref="CreateCustomShapeDef"/> to specify material properties.
    /// </summary>
    /// <returns>A shape definition with Box2D default material properties.</returns>
    public static B2ShapeDef CreateDefaultShapeDef() => b2DefaultShapeDef();

    /// <summary>
    /// Creates a custom <see cref="B2ShapeDef"/> with explicitly specified physics material properties.
    /// </summary>
    /// <param name="density">Mass density (kg/m^2). Higher values increase mass.</param>
    /// <param name="friction">Coefficient of friction (typical range 0-1).</param>
    /// <param name="restitution">Bounciness (0 = inelastic, 1 = perfectly elastic).</param>
    /// <param name="isSensor">If true, the shape detects contacts but produces no collision response.</param>
    /// <returns>A shape definition initialized with the provided parameters.</returns>
    /// <example>
    /// <code>
    /// var customDef = ShapeFixtureBuilder.CreateCustomShapeDef(2.0f, 0.6f, 0.1f, isSensor: true);
    /// ShapeFixtureBuilder.AttachShape(Primitive2DModelType.Square, size, bodyId, customDef);
    /// </code>
    /// </example>
    public static B2ShapeDef CreateCustomShapeDef(float density, float friction, float restitution, bool isSensor = false)
    {
        var shapeDef = b2DefaultShapeDef();

        shapeDef.density = density;
        shapeDef.material.friction = friction;
        shapeDef.material.restitution = restitution;
        shapeDef.isSensor = isSensor;

        return shapeDef;
    }

    private static void CreateBox(Vector2 size, B2BodyId bodyId, B2ShapeDef shapeDef)
    {
        var box = b2MakeBox(size.X / 2, size.Y / 2);

        b2CreatePolygonShape(bodyId, in shapeDef, in box);
    }

    private static void CreateCircle(Vector2 size, B2BodyId bodyId, B2ShapeDef shapeDef)
    {
        var circle = new B2Circle(new B2Vec2(0.0f, 0.0f), size.X);

        b2CreateCircleShape(bodyId, in shapeDef, in circle);
    }

    private static void CreateTriangle(Vector2 size, B2BodyId bodyId, B2ShapeDef shapeDef)
    {
        // Reuse the procedural mesh so the fixture matches the rendered triangle exactly.
        var meshData = TriangleProceduralModel.New(size);
        var points = meshData.Vertices
            .Take(3)
            .Select(v => new B2Vec2(v.Position.X, v.Position.Y)).ToArray();

        if (points.Length < 3) throw new InvalidOperationException("Triangle must have at least 3 vertices");

        var hull = b2ComputeHull(points, 3);
        var triangle = b2MakePolygon(in hull, 0.0f);

        b2CreatePolygonShape(bodyId, in shapeDef, in triangle);
    }

    private static void CreateCapsule(Vector2 size, B2BodyId bodyId, B2ShapeDef shapeDef)
    {
        var halfHeight = size.Y / 2;
        var radius = size.X;
        var capsuleHeight = halfHeight - radius;

        var capsule = new B2Capsule(new B2Vec2(0, -capsuleHeight), new B2Vec2(0, capsuleHeight), radius);

        b2CreateCapsuleShape(bodyId, in shapeDef, in capsule);
    }
}