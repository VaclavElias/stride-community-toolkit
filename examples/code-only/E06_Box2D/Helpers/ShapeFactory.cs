using Example.Common;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Shapes;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace E06_Box2D.Helpers;

/// <summary>
/// Factory class to create 2D shapes for the Box2D simulation with Box2D.NET
/// </summary>
public class ShapeFactory(Scene scene)
{
    private readonly List<Shape2DModel> _shapes =
    [
        new() { Type = Primitive2DModelType.Square, Color = Color.Green, Size = GameConfig.BoxSize },
        new() { Type = Primitive2DModelType.Rectangle, Color = Color.Orange, Size = GameConfig.RectangleSize },
        new() { Type = Primitive2DModelType.Circle, Color = Color.Red, Size = GameConfig.BoxSize / 2 },
        new() { Type = Primitive2DModelType.Circle, Color = Color.Gold, Size = GameConfig.BoxSize },
        new() { Type = Primitive2DModelType.Triangle, Color = Color.Purple, Size = GameConfig.BoxSize },
        new() { Type = Primitive2DModelType.Capsule, Color = Color.Blue, Size = GameConfig.CapsuleSize }
    ];

    public Shape2DModel? GetShapeModel(Primitive2DModelType type)
        => _shapes.Find(x => x.Type == type);

    public Shape2DModel GetRandomShapeModel()
        => _shapes[Random.Shared.Next(_shapes.Count)];

    public Entity CreateEntity(Shape2DModel shape, Color? overrideColor = null, Vector2? position = null, string? name = null)
    {
        var actualColor = overrideColor ?? shape.Color;

        // The Box2D debug-draw component renders the shape testbed-style - fill plus a
        // pixel-constant border from the same numbers the collider uses. No mesh, no material,
        // no outline shader.
        var entity = new Entity(name ?? $"{shape.Type}-{GameConfig.ShapeName}")
        {
            CreateShapeComponent(shape, actualColor)
        };

        entity.Transform.Position = position.HasValue ? (Vector3)position : GetRandomPosition();
        entity.Scene = scene;

        return entity;
    }

    /// <summary>
    /// Builds the debug-draw outline for a shape model: a circle is one vertex plus a radius, a
    /// capsule two vertices plus a radius, the rest are their corner polygons - mirroring exactly
    /// how ShapeFixtureBuilder builds the matching collider.
    /// </summary>
    private static ShapeComponent CreateShapeComponent(Shape2DModel shape, Color color) => shape.Type switch
    {
        Primitive2DModelType.Circle => new() { Vertices = [Vector2.Zero], Radius = shape.Size.X, Color = color },
        Primitive2DModelType.Capsule => new()
        {
            Vertices = [new(0, -(shape.Size.Y / 2 - shape.Size.X)), new(0, shape.Size.Y / 2 - shape.Size.X)],
            Radius = shape.Size.X,
            Color = color,
        },
        Primitive2DModelType.Triangle => new()
        {
            Vertices =
            [
                new(-shape.Size.X * 0.5f, -shape.Size.Y * 0.5f),
                new(shape.Size.X * 0.5f, -shape.Size.Y * 0.5f),
                new(0, shape.Size.Y * 0.5f),
            ],
            Color = color,
        },
        _ => new()
        {
            Vertices =
            [
                new(-shape.Size.X * 0.5f, -shape.Size.Y * 0.5f),
                new(shape.Size.X * 0.5f, -shape.Size.Y * 0.5f),
                new(shape.Size.X * 0.5f, shape.Size.Y * 0.5f),
                new(-shape.Size.X * 0.5f, shape.Size.Y * 0.5f),
            ],
            Color = color,
        },
    };

    private static Vector3 GetRandomPosition() => new(Random.Shared.Next(-5, 5), Random.Shared.Next(10, 30), 0);
}