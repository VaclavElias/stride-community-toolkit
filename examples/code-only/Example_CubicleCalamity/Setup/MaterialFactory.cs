using Example_CubicleCalamity.Shared;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

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
/// The cubes are <em>emissive</em>, which is the important choice here. Colour matching is the entire
/// game, so a cube has to read as its own colour from any camera angle - and ordinary diffuse shading
/// cannot do that, because the diffuse term is multiplied by the angle between the surface and each
/// light. A face turned away from the lights goes dark no matter how many lights are added, and a dark
/// red cube beside a lit one is genuinely hard to match by eye. Emissive colour ignores lighting
/// entirely, so every face reads the same; the lit diffuse layer on top is only there to keep edges
/// and corners visible.
/// </para>
/// </remarks>
public static class MaterialFactory
{
    /// <summary>
    /// Creates one material per colour in <see cref="GameSettings.Colours"/>, keyed by colour so a
    /// cube can look up the material for the colour it was given.
    /// </summary>
    /// <param name="game">The running game, which owns the graphics device the materials are created on.</param>
    /// <returns>A material for every colour a cube can take.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="GameExtensions.CreateFlatMaterial"/> does exactly what this board needs, so there is
    /// no example-local material code any more: full emissive colour, full diffuse colour, and no
    /// specular. The emissive half is what makes a cube read as its own colour from any angle; the
    /// diffuse half is a normal lit surface layered on top, so the lights still pick out edges and
    /// corners and the light intensity remains a working brightness knob.
    /// </para>
    /// <para>
    /// A hand-rolled version here previously scaled the diffuse colour down to keep the lighting
    /// subtle, which was the wrong knob twice over. Diffuse colour is the albedo the lights multiply
    /// against, so shrinking it removes the lights' ability to do anything - and
    /// <c>Color * float</c> scales the <em>alpha</em> along with the RGB, which then multiplies the
    /// lit contribution a second time through <c>matDiffuseSpecularAlphaBlend</c>. The two compounded
    /// to roughly two percent of the intended colour: cubes that looked dark and barely reacted to
    /// light at all. Balance belongs on the light intensities, not on the albedo.
    /// </para>
    /// </remarks>
    public static Dictionary<Color, Material> CreateCubeMaterials(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var materials = new Dictionary<Color, Material>();

        foreach (var colour in GameSettings.Colours)
        {
            materials.Add(colour, game.CreateFlatMaterial(colour));
        }

        return materials;
    }
}
