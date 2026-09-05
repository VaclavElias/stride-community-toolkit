using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Submits every enabled <see cref="ShapeComponent"/> to its <see cref="ShapeBatch"/> each frame,
/// reading the plane and scale to draw in from the entity's world matrix.
/// </summary>
public sealed class ShapeProcessor : EntityProcessor<ShapeComponent>
{
    private ShapeBatch? _default;

    /// <inheritdoc/>
    public override void Draw(RenderContext context)
    {
        _default ??= Services.GetService<ShapeBatch>();

        foreach (var kv in ComponentDatas)
        {
            var component = kv.Key;

            if (!component.Enabled || component.Vertices.Length < 1) continue;

            var batch = component.Batch ?? _default;

            if (batch is null) continue;

            // The batch's colours, border, fill and glow are current state shared with whoever else draws
            // through it, so put back whatever was there before moving on
            var borderWidth = batch.BorderWidth;
            var fillAlpha = batch.Fill.Alpha;
            var fillColor = batch.Fill.Color;
            var glowWidth = batch.Glow.Width;
            var glowColor = batch.Glow.Color;

            // Negative means "inherit"; a transparent colour means the same for the colours,
            // because Game Studio cannot edit nullable value types (see ShapeComponent.Inherit)
            batch.BorderWidth = component.BorderWidth < 0f ? borderWidth : component.BorderWidth;
            batch.Fill.Alpha = component.FillAlpha < 0f ? fillAlpha : component.FillAlpha;
            batch.Fill.Color = component.FillColor.A == 0 ? fillColor : component.FillColor;
            batch.Glow.Width = component.GlowWidth < 0f ? glowWidth : component.GlowWidth;
            batch.Glow.Color = component.GlowColor.A == 0 ? glowColor : component.GlowColor;

            Draw(batch, component);

            batch.BorderWidth = borderWidth;
            batch.Fill.Alpha = fillAlpha;
            batch.Fill.Color = fillColor;
            batch.Glow.Width = glowWidth;
            batch.Glow.Color = glowColor;
        }
    }

    private static void Draw(ShapeBatch batch, ShapeComponent component)
    {
        // The world matrix, so parented entities work too
        ref var world = ref component.Entity.Transform.WorldMatrix;
        var position = world.TranslationVector;

        if (component.Billboard)
        {
            batch.DrawBillboard(component.Vertices, position, component.Color, component.Radius);

            return;
        }

        // Rows 1 and 2 are the entity's own X and Y axes in world space, which is the plane the
        // shape lies in; their length is the scale, taken from X so the shape stays undistorted
        var axisX = new Vector3(world.M11, world.M12, world.M13);
        var axisY = new Vector3(world.M21, world.M22, world.M23);
        var scale = axisX.Length();

        if (scale <= float.Epsilon) return;

        batch.DrawSolidPolygon(component.Vertices, position, axisX, axisY, component.Color, component.Radius, scale);
    }
}