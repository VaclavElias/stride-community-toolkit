using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Stride.CommunityToolkit.Rendering.Lines;

/// <summary>
/// Creates entities that draw the filled region between two polylines.
/// </summary>
public static class AreaExtensions
{
    /// <summary>The fill tint, premultiplied; one value per material instance.</summary>
    private static readonly ValueParameterKey<Color4> AreaColorKey = ParameterKeys.NewValue<Color4>();

    /// <summary>
    /// Creates an entity drawing the band described by <paramref name="runs"/> as one filled mesh.
    /// </summary>
    /// <param name="game">The game whose graphics device the mesh is created on.</param>
    /// <param name="runs">The runs of columns from <see cref="AreaMeshBuilder.Columns"/>.</param>
    /// <param name="options">Fill colour, glow and plane; <see langword="null"/> for the defaults.</param>
    /// <param name="name">The entity name, or <c>"Area"</c>.</param>
    /// <returns>An entity holding a <see cref="ModelComponent"/>; add it to a scene or parent it to another entity.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="game"/> or <paramref name="runs"/> is <see langword="null"/>.</exception>
    public static Entity CreateArea(this IGame game, IReadOnlyList<IReadOnlyList<(Vector3 Upper, Vector3 Lower)>> runs, AreaOptions? options = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(runs);

        options ??= new AreaOptions();

        var mesh = AreaMeshBuilder.Build(game.GraphicsDevice, runs, options);

        return new Entity(name ?? "Area") { CreateModel(game, mesh, options) };
    }

    /// <summary>
    /// Builds the flat, double-sided, translucent model an area fill uses.
    /// </summary>
    /// <remarks>
    /// The material is emissive times a colour parameter with a transparency blend feature - the recipe the
    /// scene editor's grid gizmo uses, and the reason a fill can be translucent at all. An emissive material
    /// without that feature ignores its alpha, which is why the gizmo material used for ribbons is not
    /// reused here: a ribbon is opaque, a fill is not.
    /// </remarks>
    /// <param name="game">The game whose graphics device the material is created on.</param>
    /// <param name="mesh">The mesh to draw.</param>
    /// <param name="options">Fill colour and glow.</param>
    /// <returns>A model component ready to add to an entity.</returns>
    internal static ModelComponent CreateModel(IGame game, Mesh mesh, AreaOptions options)
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
