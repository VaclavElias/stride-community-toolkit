using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Submits every enabled <see cref="ShapeComponent"/> to the game's <see cref="ShapeBatch"/> each
/// frame, reading the plane and scale to draw in from the entity's world matrix.
/// </summary>
public sealed class ShapeProcessor : EntityProcessor<ShapeComponent>
{
    private ShapeBatch? _batch;

    /// <inheritdoc/>
    public override void Draw(RenderContext context)
    {
        _batch ??= Services.GetService<ShapeBatch>();

        if (_batch is null) return;

        // The batch's border and fill are current state, so save them once and put them back after
        // any component that asked for its own
        var batchBorderWidth = _batch.BorderWidth;
        var batchFillAlpha = _batch.FillAlpha;

        foreach (var kv in ComponentDatas)
        {
            var component = kv.Key;

            if (!component.Enabled || component.Vertices.Length < 1) continue;

            _batch.BorderWidth = component.BorderWidth ?? batchBorderWidth;
            _batch.FillAlpha = component.FillAlpha ?? batchFillAlpha;

            // The world matrix, so parented entities work too
            ref var world = ref component.Entity.Transform.WorldMatrix;
            var position = world.TranslationVector;

            if (component.Billboard)
            {
                _batch.DrawBillboard(component.Vertices, position, component.Color, component.Radius);

                continue;
            }

            // Rows 1 and 2 are the entity's own X and Y axes in world space, which is the plane the
            // shape lies in; their length is the scale, taken from X so the shape stays undistorted
            var axisX = new Vector3(world.M11, world.M12, world.M13);
            var axisY = new Vector3(world.M21, world.M22, world.M23);
            var scale = axisX.Length();

            if (scale <= float.Epsilon) continue;

            _batch.DrawSolidPolygon(component.Vertices, position, axisX, axisY, component.Color, component.Radius, scale);
        }

        _batch.BorderWidth = batchBorderWidth;
        _batch.FillAlpha = batchFillAlpha;
    }
}