using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Submits every enabled <see cref="Box2DDebugShapeComponent"/> to the game's <see cref="Box2DDebugDraw"/>
/// each frame, reading position and Z rotation from the entity's world matrix.
/// </summary>
public sealed class Box2DDebugShapeProcessor : EntityProcessor<Box2DDebugShapeComponent>
{
    private Box2DDebugDraw? _batch;

    /// <inheritdoc/>
    public override void Draw(RenderContext context)
    {
        _batch ??= Services.GetService<Box2DDebugDraw>();

        if (_batch is null) return;

        foreach (var kv in ComponentDatas)
        {
            var component = kv.Key;

            if (!component.Enabled || component.Vertices.Length < 1) continue;

            // Position and Z rotation from the world matrix, so parented entities work too
            ref var world = ref component.Entity.Transform.WorldMatrix;
            var rotation = MathF.Atan2(world.M12, world.M11);

            _batch.DrawSolidPolygon(component.Vertices, new Vector2(world.M41, world.M42), rotation, component.Color, component.Radius);
        }
    }
}
