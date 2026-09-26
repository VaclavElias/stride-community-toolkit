using Stride.Core.Mathematics;
using Stride.Rendering.Materials.ComputeColors;

namespace E02_3D_Material_Gallery;

// The inputs a slot can take beyond a texture: a shader class of your own, and a texture computed
// at runtime with no asset behind it.
public static class InputStations
{
    /// <summary>
    /// The extension point: a shader class that derives from <c>ComputeColor</c> and overrides
    /// <c>Compute()</c> sits in any material slot through <c>ComputeShaderClassColor</c>, referenced
    /// by name. The two classes here are in this example's <c>Effects</c> folder, twenty lines
    /// each, compiled by the asset compiler at build like any shader. V switches the class.
    /// </summary>
    public static void CustomNode(MaterialStation s)
    {
        s.Clear();

        var shader = s.Pick("GalleryChecker", "GalleryStripes") == 0 ? "GalleryChecker" : "GalleryStripes";

        s.PlaceTrio(s.Material(Recipes.Mapped(new ComputeShaderClassColor { MixinReference = shader }, glossiness: new ComputeFloat(0.45f))));
    }

    /// <summary>
    /// Textures made in C# a moment ago: a height map of ripples, and the normal map derived from
    /// it by finite differences - both a <c>Color[]</c> handed to <c>Texture.New2D</c>. The left
    /// sphere wears the height map as its colour, the right one the normal map as its surface, and
    /// the cube both. No file was involved; a texture is just pixels.
    /// </summary>
    public static void RuntimeTexturesStation(MaterialStation s)
    {
        s.Clear();

        var device = s.Game.GraphicsDevice;
        var heights = s.Textures.Generated("ripples", () => RuntimeTextures.Ripples(device));
        var normals = s.Textures.Generated("ripple-normals", () => RuntimeTextures.NormalFromHeight(device, RuntimeTextures.RipplePixels(), 256));

        var painted = s.Material(Recipes.Mapped(Recipes.Colour(heights), glossiness: new ComputeFloat(0.5f)));
        var bumped = s.Material(Recipes.Mapped(new ComputeColor(new Color(200, 170, 120)), glossiness: new ComputeFloat(0.7f), normal: Recipes.Colour(normals)));
        var both = s.Material(Recipes.Mapped(Recipes.Colour(heights), glossiness: new ComputeFloat(0.7f), normal: Recipes.Colour(normals)));

        s.PlaceTrio(painted, both, bumped);
    }
}