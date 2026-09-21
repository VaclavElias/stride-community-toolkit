using Stride.Graphics;

namespace E02_3D_MaterialGallery;

/// <summary>
/// The Material Package's textures, loaded from the resources folder next to the executable on
/// first use and kept for the run. A colour map is sRGB; a normal, gloss, metalness, occlusion or
/// mask map is linear data and must not be gamma-decoded, which is the one thing that goes wrong
/// when every texture is loaded the same way.
/// </summary>
public sealed class MaterialTextures(GraphicsDevice device) : IDisposable
{
    private readonly Dictionary<string, Texture> _loaded = [];
    private readonly Dictionary<string, Texture> _generated = [];

    /// <summary>A colour map, decoded as sRGB.</summary>
    /// <param name="path">The file under <c>Resources/materials</c>, such as <c>brick/brick_dif.png</c>.</param>
    public Texture Color(string path) => Get(path, srgb: true);

    /// <summary>A data map - normal, gloss, metalness, occlusion, mask - kept linear.</summary>
    /// <param name="path">The file under <c>Resources/materials</c>, such as <c>brick/brick_nml.png</c>.</param>
    public Texture Data(string path) => Get(path, srgb: false);

    private Texture Get(string path, bool srgb)
    {
        var key = $"{(srgb ? "srgb" : "linear")}:{path}";

        if (_loaded.TryGetValue(key, out var texture)) return texture;

        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Resources", "materials", path));
        using var image = Image.Load(stream);

        // The texture takes the pixels in the format the file decoded to - a PNG comes out BGRA -
        // because the bytes are copied as they are; asking for RGBA would trade red for blue. Only
        // the colour space is chosen here: a colour map is sRGB, a data map is not.
        var format = srgb ? image.Description.Format.ToSRgb() : image.Description.Format.ToNonSRgb();

        texture = Texture.New2D(device, image.Description.Width, image.Description.Height, format, image.PixelBuffer[0].GetPixels<Stride.Core.Mathematics.Color>());
        _loaded[key] = texture;

        return texture;
    }

    /// <summary>A texture computed in code, made on first use and kept for the run.</summary>
    /// <param name="name">Its name, the key it is kept under.</param>
    /// <param name="make">How to make it.</param>
    public Texture Generated(string name, Func<Texture> make)
    {
        if (!_generated.TryGetValue(name, out var texture))
        {
            texture = make();
            _generated[name] = texture;
        }

        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in _generated.Values) texture.Dispose();

        _generated.Clear();
        foreach (var texture in _loaded.Values) texture.Dispose();

        _loaded.Clear();
    }
}