using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The scene editor's grid technique - see <c>ViewportGridGizmo</c> in the Stride sources - adapted for
/// charts: the grid is not line geometry but a mip-mapped texture on a large plane. Each texture cell
/// draws its border as one-pixel lines, every mip level is authored by hand so the average luminance
/// stays constant, and the anisotropic sampler blends between them - so grid lines stay stable and evenly
/// bright at every zoom level, with no geometry to rebuild. A chart uses two such planes, one for the
/// major grid and one for the minor, each scaled so a texture cell equals its step.
/// </summary>
internal static class ChartGridTexture
{
    /// <summary>
    /// Cells along each side of the grid plane; the plane is this many world units wide at scale 1, so
    /// scaling the plane entity by the tick step makes each cell one step and still covers any view whose
    /// height stays under ~<c>PlaneCells / 10</c> steps - far beyond what the nice-step picker allows.
    /// </summary>
    public const float PlaneCells = 200f;

    private const int TopSize = 256;

    /// <summary>Tint of the grid lines; premultiplied, multiplied with the texture. One value per material instance.</summary>
    public static readonly ValueParameterKey<Color4> GridColorKey = ParameterKeys.NewValue<Color4>();

    /// <summary>
    /// Creates the mip-mapped grid texture. One texture serves every grid plane; the tint and cell size
    /// come from the material and the plane's scale.
    /// </summary>
    internal static Texture Create(GraphicsDevice device)
    {
        using var image = GenerateGridImage();

        return Texture.New(device, image);
    }

    /// <summary>
    /// Builds the grid material the way the scene editor does: an emissive texture multiplied by a colour
    /// parameter, alpha-blended, double-sided, sampled anisotropically with wrapping.
    /// </summary>
    /// <param name="device">The device to compile the material on.</param>
    /// <param name="texture">The texture from <see cref="Create"/>.</param>
    /// <param name="color">The line tint - a chart's <c>GridColor</c> or <c>MinorGridColor</c>.</param>
    internal static Material CreateMaterial(GraphicsDevice device, Texture texture, Color color)
    {
        var material = Material.New(device, new MaterialDescriptor
        {
            Attributes =
            {
                Emissive = new MaterialEmissiveMapFeature(new ComputeBinaryColor(
                    new ComputeColor { Key = GridColorKey },
                    new ComputeTextureColor { Key = TexturingKeys.Texture0, Texture = texture, Scale = new Vector2(PlaneCells) },
                    BinaryOperator.Multiply))
                {
                    UseAlpha = true,
                },
                Transparency = new MaterialTransparencyBlendFeature(),
                CullMode = CullMode.None,
            },
        });

        material.Passes[0].Parameters.Set(TexturingKeys.Texture0, texture);
        material.Passes[0].Parameters.Set(MaterialKeys.Sampler.ComposeWith("i0"), device.SamplerStates.AnisotropicWrap);

        var tint = color.ToColor3().ToColorSpace(device.ColorSpace);
        material.Passes[0].Parameters.Set(GridColorKey, Color4.PremultiplyAlpha(new Color4(tint, 1f)));

        return material;
    }

    /// <summary>
    /// One cell of grid: its border drawn as one-pixel lines, in every mip level. The intensity of each
    /// mip is chosen so the average luminance of the level matches the top one - the scene editor's trick
    /// that keeps the grid equally bright however far the sampler has minified it, instead of thin lines
    /// aliasing away or dense ones blowing out.
    /// </summary>
    private static Image GenerateGridImage()
    {
        var image = Image.New2D(TopSize, TopSize, true, PixelFormat.R8G8B8A8_UNorm_SRgb);
        image.Clear();

        var average = AverageLuminance(TopSize);

        for (var i = 0; i < image.PixelBuffer.Count; i++)
        {
            var pixelBuffer = image.PixelBuffer[i];

            var lumBase = (float)(average / AverageLuminance(pixelBuffer.Width));
            var intensity = MathF.Pow(lumBase, 1f / 3f);
            var alpha = MathF.Pow(lumBase, 1f / 3f);
            var color = (Color)new Color4(intensity * alpha, intensity * alpha, intensity * alpha, alpha).ToSRgb();

            for (var x = 0; x < pixelBuffer.Width; x++)
            {
                pixelBuffer.SetPixel(x, 0, color);
                pixelBuffer.SetPixel(0, x, color);
                pixelBuffer.SetPixel(x, pixelBuffer.Height - 1, color);
                pixelBuffer.SetPixel(pixelBuffer.Width - 1, x, color);
            }
        }

        return image;
    }

    /// <summary>The share of bright pixels in a grid cell of the given size - the border over the area.</summary>
    private static double AverageLuminance(int size) => 4.0 * (size - 1) / (size * size);
}