using Example_CubicleCalamity.Rendering;
using Example_CubicleCalamity.Shared;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Example_CubicleCalamity.Setup;

/// <summary>
/// Builds the materials the cubes are painted with.
/// </summary>
/// <remarks>
/// A material is a GPU resource, so one is built per colour up front and shared by every cube using
/// it. Building one per cube would work and look identical, and would also mean a thousand copies of
/// the same thing - a habit worth avoiding early, because it is invisible until it is not.
/// </remarks>
public static class MaterialFactory
{
    /// <summary>
    /// Creates one material per colour in <see cref="GameSettings.Colours"/>, keyed by colour so a
    /// cube can look up the material for the colour it was given.
    /// </summary>
    /// <param name="game">The running game, which owns the graphics device the materials are created on.</param>
    /// <returns>A material for every colour a cube can take.</returns>
    public static Dictionary<Color, Material> CreateCubeMaterials(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var materials = new Dictionary<Color, Material>();

        foreach (var colour in GameSettings.Colours)
        {
            materials.Add(colour, CreateMaterial(game, colour, specular: 0));
        }

        return materials;
    }

    /// <summary>
    /// Creates a single flat-lit material.
    /// </summary>
    /// <param name="game">The running game, which owns the graphics device.</param>
    /// <param name="color">The colour to paint. Defaults to the toolkit's default material colour.</param>
    /// <param name="specular">How metallic the surface reads. Zero for the cubes, so they stay readable as flat colour.</param>
    /// <param name="microSurface">Surface glossiness.</param>
    /// <returns>A new material.</returns>
    /// <remarks>
    /// This uses the example's own <see cref="MaterialLightmapModelFeature"/> rather than a
    /// standard diffuse model, which is what keeps every face of a cube the same brightness however
    /// the lights fall on it. Colour matching is the whole game, so a face shaded darker than its
    /// neighbour would be reading as a different colour.
    /// </remarks>
    public static Material CreateMaterial(Game game, Color? color = null, float specular = 1.0f, float microSurface = 0.65f)
    {
        ArgumentNullException.ThrowIfNull(game);

        var descriptor = new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color ?? GameDefaults.DefaultMaterialColor)),
                DiffuseModel = new MaterialLightmapModelFeature()
                {
                    Intensity = 20,
                    LightMap = new ComputeColor(color ?? GameDefaults.DefaultMaterialColor)
                },
                Specular = new MaterialMetalnessMapFeature(new ComputeFloat(specular)),
                SpecularModel = new MaterialSpecularMicrofacetModelFeature(),
                MicroSurface = new MaterialGlossinessMapFeature(new ComputeFloat(microSurface))
            }
        };

        return Material.New(game.GraphicsDevice, descriptor);
    }
}