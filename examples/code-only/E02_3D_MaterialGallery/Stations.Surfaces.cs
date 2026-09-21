using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace E02_3D_MaterialGallery;

// The surfaces: what happens at the boundary of the material - see-through, glass, a coat over a
// coat - and the shading models that replace Lambert and the microfacet, cel and hair.
public static class SurfaceStations
{
    /// <summary>
    /// Four ways to be see-through, on the same blue shape. Blend mixes the surface over what is
    /// behind it by an alpha; additive only brightens, a hologram; cutoff keeps or drops each pixel
    /// by a threshold on a mask, which is how leaves and fences are done with no sorting at all;
    /// dithered is a cutoff whose mask is an ordered dither from a shader class, a screen-door
    /// fade the engine itself uses for shadows of translucent things. V cycles them.
    /// </summary>
    public static void Transparency(MaterialStation s)
    {
        s.Clear();

        var colour = new Color(70, 150, 255);
        var descriptor = Recipes.Pbr(colour, glossiness: 0.7f, metalness: 0f);

        descriptor.Attributes.Transparency = s.Pick("blend", "additive", "cutoff", "dithered") switch
        {
            0 => new MaterialTransparencyBlendFeature { Alpha = new ComputeFloat(0.45f), Tint = new ComputeColor(Color.White) },
            1 => new MaterialTransparencyAdditiveFeature { Alpha = new ComputeFloat(0.7f), Tint = new ComputeColor(Color.White) },
            2 => new MaterialTransparencyCutoffFeature { Alpha = Recipes.Scalar(s.Textures.Generated("holes", () => RuntimeTextures.Holes(s.Game.GraphicsDevice)), tiling: 2f) },
            _ => new MaterialTransparencyCutoffFeature { Alpha = new ComputeShaderClassScalar { MixinReference = "GalleryDither" } },
        };

        // Something behind, so see-through has something to see
        s.Place(PrimitiveModelType.Sphere, s.Material(Recipes.Pbr(new Color(230, 120, 60), 0.5f, 0f)), new Vector3(0f, 0.8f, -2.4f), new Vector3(0.8f));
        s.PlaceTrio(s.Material(descriptor));
    }

    /// <summary>
    /// Glass: the thin-glass specular model, with a refractive index and a Fresnel term of its own.
    /// It is a multi-pass material by itself - a transmittance pass that multiplies what is behind,
    /// then an additive reflection pass, per face side - so it takes no transparency feature; adding
    /// one fights its blend states. The diffuse colour is the transmission tint - what the glass
    /// absorbs, by Beer's law - and there is no diffuse *model*, or the reflection pass would add lit
    /// colour and the glass would go opaque; that is how the engine's own sample glass is built. V
    /// cycles clear, tinted and frosted, where frosted is nothing but a lower glossiness.
    /// </summary>
    public static void ThinGlass(MaterialStation s)
    {
        s.Clear();

        var (tint, glossiness) = s.Pick("clear", "tinted", "frosted") switch
        {
            0 => (new Color(245, 250, 255), 0.95f),
            1 => (new Color(90, 215, 70), 0.85f),
            _ => (new Color(225, 230, 235), 0.45f),
        };

        var glass = s.Material(new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(tint)),
                MicroSurface = new MaterialGlossinessMapFeature(new ComputeFloat(glossiness)),
                Specular = new MaterialMetalnessMapFeature(new ComputeFloat(0.08f)),
                SpecularModel = new MaterialSpecularThinGlassModelFeature
                {
                    RefractiveIndex = 1.563f,
                    Fresnel = new MaterialSpecularMicrofacetFresnelThinGlass(),
                    Environment = new MaterialSpecularMicrofacetEnvironmentThinGlass(),
                },
                // Both faces, so the back of the glass gets its passes too
                CullMode = CullMode.None,
            },
        });

        s.Place(PrimitiveModelType.Sphere, s.Material(Recipes.Pbr(new Color(230, 120, 60), 0.5f, 0f)), new Vector3(-2.8f, 0.8f, -2.4f), new Vector3(0.8f));
        s.Place(PrimitiveModelType.Cube, s.Material(Recipes.Pbr(new Color(60, 160, 90), 0.5f, 0f)), new Vector3(2.8f, 0.8f, -2.4f), new Vector3(1.4f));
        s.PlaceTrio(glass);
    }

    /// <summary>
    /// Car paint: a base colour, metal flakes under it that catch the light from their own random
    /// normals, and a clear coat over both with its own glossiness. One feature holds the three
    /// layers; the flake normal map is a runtime texture of small random tilts. V cycles the paint.
    /// </summary>
    public static void ClearCoat(MaterialStation s)
    {
        s.Clear();

        var paint = s.Pick("candy red", "midnight blue", "british green") switch
        {
            0 => new Color(180, 20, 30),
            1 => new Color(20, 30, 90),
            _ => new Color(10, 70, 40),
        };

        var flakes = s.Textures.Generated("flakes", () => RuntimeTextures.Flakes(s.Game.GraphicsDevice));

        var descriptor = Recipes.Pbr(paint, glossiness: 0.6f, metalness: 0f);

        descriptor.Attributes.ClearCoat = new MaterialClearCoatFeature
        {
            BasePaintDiffuseMap = new ComputeColor(paint),
            BasePaintGlossinessMap = new ComputeFloat(0.6f),
            MetalFlakesDiffuseMap = new ComputeColor(new Color(200, 200, 210)),
            MetalFlakesGlossinessMap = new ComputeFloat(0.85f),
            MetalFlakesMetalnessMap = new ComputeFloat(1f),
            MetalFlakesNormalMap = Recipes.Colour(flakes, tiling: 12f),
            MetalFlakesScaleAndBias = true,
            MetalFlakeslIsXYNormal = true,
            ClearCoatGlossinessMap = new ComputeFloat(0.97f),
            LODDistance = new ComputeFloat(20f),
        };

        s.PlaceTrio(s.Material(descriptor));
    }

    /// <summary>
    /// Cel shading: the diffuse and specular models replaced by ones that quantise the light. The
    /// default function makes a few hard bands from the angle to the light; the ramp function
    /// looks the band up in a texture, here a three-step ramp made at runtime, so the bands are
    /// yours to draw. V switches between them.
    /// </summary>
    public static void CelShading(MaterialStation s)
    {
        s.Clear();

        IMaterialCelShadingLightFunction ramp = s.Pick("default bands", "three-step ramp") == 0
            ? new MaterialCelShadingLightDefault()
            : new MaterialCelShadingLightRamp { RampTexture = s.Textures.Generated("cel-ramp", () => RuntimeTextures.Ramp(s.Game.GraphicsDevice, 0.25f, 0.6f, 1f)) };

        var descriptor = Recipes.Pbr(new Color(240, 120, 90), glossiness: 0.7f, metalness: 0f);

        descriptor.Attributes.DiffuseModel = new MaterialDiffuseCelShadingModelFeature { RampFunction = ramp };
        descriptor.Attributes.SpecularModel = new MaterialSpecularCelShadingModelFeature { RampFunction = ramp };

        s.PlaceTrio(s.Material(descriptor));
    }

}