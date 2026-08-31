using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Stride.CommunityToolkit.Charts.Lines;

/// <summary>
/// Builds the model - mesh plus material - that draws the filled region between two polylines.
/// </summary>
internal static class AreaModel
{
    /// <summary>The fill tint, premultiplied; one value per material instance.</summary>
    private static readonly ValueParameterKey<Color4> AreaColorKey = ParameterKeys.NewValue<Color4>();

    /// <summary>
    /// Builds the flat, double-sided, translucent model an area fill uses.
    /// </summary>
    /// <remarks>
    /// The material is emissive times a colour parameter with a transparency blend feature - the recipe the
    /// scene editor's grid gizmo uses, and the reason a fill can be translucent at all. An emissive material
    /// without that feature ignores its alpha entirely. The ribbon material (GizmoEmissiveColorMaterial)
    /// follows the same recipe for translucent colours, but is lit and single-sided; a fill wants neither,
    /// so it keeps its own descriptor.
    /// </remarks>
    /// <param name="game">The game whose graphics device the material is created on.</param>
    /// <param name="mesh">The mesh to draw.</param>
    /// <param name="options">Fill colour and glow.</param>
    /// <returns>A model component ready to add to an entity.</returns>
    internal static ModelComponent Create(IGame game, Mesh mesh, AreaOptions options)
    {
        var device = game.GraphicsDevice;

        var material = Material.New(device, new MaterialDescriptor
        {
            Attributes =
            {
                Emissive = new MaterialEmissiveMapFeature(new ComputeColor { Key = AreaColorKey }) { UseAlpha = true },
                Transparency = new MaterialTransparencyBlendFeature(),
                CullMode = CullMode.None,
            },
        });

        var tint = options.Color.ToColor3().ToColorSpace(device.ColorSpace);

        material.Passes[0].Parameters.Set(AreaColorKey, Color4.PremultiplyAlpha(new Color4(tint, options.Color.A / 255f)));
        material.Passes[0].Parameters.Set(MaterialKeys.EmissiveIntensity, options.EmissiveIntensity);

        return new ModelComponent(new Model { material, mesh });
    }
}