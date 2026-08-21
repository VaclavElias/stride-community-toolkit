using Example_CubicleCalamity.Shared;
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
/// <para>
/// A material is a GPU resource, so one is built per colour up front and shared by every cube using
/// it. Building one per cube would work and look identical, and would also mean a thousand copies of
/// the same thing - a habit worth avoiding early, because it is invisible until it is not.
/// </para>
/// <para>
/// The cubes are mostly <em>emissive</em>, which is the important choice here. Colour matching is the
/// entire game, so a cube has to read as its own colour from any camera angle - and ordinary diffuse
/// shading cannot do that, because the diffuse term is multiplied by the angle between the surface and
/// each light. A face turned away from the lights goes dark no matter how many lights are added, and a
/// dark red cube next to a lit one is genuinely hard to match by eye. Emissive colour ignores lighting
/// entirely, so every face is the same colour; a small diffuse component is layered on top purely so
/// the edges between adjacent cubes stay visible.
/// </para>
/// </remarks>
public static class MaterialFactory
{
    /// <summary>
    /// How much of the cube's colour comes from emission, independent of any light.
    /// </summary>
    private const float EmissiveShare = 0.85f;

    /// <summary>
    /// How much comes from ordinary diffuse shading, which is what keeps edges and corners readable.
    /// </summary>
    /// <remarks>
    /// Deliberately small. Raising it brings back the shape of the stack at the cost of colour
    /// fidelity, which is the trade this game cannot afford much of.
    /// </remarks>
    private const float DiffuseShare = 0.15f;

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
            //materials.Add(colour, game.CreateFlatMaterial(colour));
            materials.Add(colour, CreateCubeMaterial(game, colour));
        }

        return materials;
    }

    /// <summary>
    /// Creates the material for a single cube colour.
    /// </summary>
    /// <param name="game">The running game, which owns the graphics device.</param>
    /// <param name="color">The colour to paint.</param>
    /// <returns>A new material.</returns>
    /// <remarks>
    /// No specular feature at all: a highlight is a bright white patch that moves with the camera, and
    /// on a board where the player is comparing colours it is one more thing that makes two cubes of
    /// the same colour look different.
    /// </remarks>
    public static Material CreateCubeMaterial(Game game, Color color)
    {
        ArgumentNullException.ThrowIfNull(game);

        var descriptor = new MaterialDescriptor
        {
            Attributes =
            {
                Emissive = new MaterialEmissiveMapFeature(new ComputeColor(color))
                {
                    Intensity = new ComputeFloat(EmissiveShare),
                    UseAlpha = false
                },
                Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color * DiffuseShare)),
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Specular = null,
                SpecularModel = null
            }
        };

        return Material.New(game.GraphicsDevice, descriptor);
    }
}
