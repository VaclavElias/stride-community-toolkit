using Hexa.NET.ImGui;
using Stride.Graphics;

namespace Stride.CommunityToolkit.ImGui;

/// <summary>
/// Owns the textures Dear ImGui manages through its 1.92+ texture protocol (RendererHasTextures):
/// creates, updates and destroys them as the draw data requests, and resolves texture ids at draw
/// time. Split out of <see cref="ImGuiSystem"/>, which forwards the protocol events here.
/// </summary>
internal sealed class ImGuiTextureManager : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly CommandList _commandList;
    private readonly Dictionary<ImTextureID, Texture> _managedTextures = new();

    internal ImGuiTextureManager(GraphicsDevice device, CommandList commandList)
    {
        _device = device;
        _commandList = commandList;
    }

    internal unsafe void ProcessTextureUpdates(ImDrawDataPtr drawData)
    {
        if (drawData.Handle->Textures == null) return;
        var textures = drawData.Textures;
        for (int i = 0; i < textures.Size; i++)
        {
            ImTextureDataPtr textureData = textures.Data[i];
            switch (textureData.Status)
            {
                case ImTextureStatus.WantCreate:
                    CreateManagedTexture(textureData);
                    break;
                case ImTextureStatus.WantUpdates:
                    UpdateManagedTexture(textureData);
                    break;
                case ImTextureStatus.WantDestroy:
                    DestroyManagedTexture(textureData);
                    break;
            }
        }
    }

    private unsafe void CreateManagedTexture(ImTextureDataPtr textureData)
    {
        var pixelFormat = textureData.Format == ImTextureFormat.Rgba32
            ? PixelFormat.R8G8B8A8_UNorm
            : PixelFormat.R8_UNorm;
        var newTexture = Texture.New2D(_device, textureData.Width, textureData.Height, pixelFormat, TextureFlags.ShaderResource);
        newTexture.SetData(_commandList, new ReadOnlySpan<byte>(textureData.Pixels, textureData.GetSizeInBytes()));

        // Use high-bit sentinel to distinguish ImGui-managed IDs from ImGuiExtension user-texture IDs (which start from 1)
        var managedId = (ImTextureID)(nint)(0x80000000u | (uint)textureData.UniqueID);
        textureData.SetTexID(managedId);
        _managedTextures[managedId] = newTexture;
        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private unsafe void UpdateManagedTexture(ImTextureDataPtr textureData)
    {
        var texId = textureData.GetTexID();
        if (_managedTextures.TryGetValue(texId, out var existing))
        {
            var pixelFormat = textureData.Format == ImTextureFormat.Rgba32
                ? PixelFormat.R8G8B8A8_UNorm
                : PixelFormat.R8_UNorm;
            if (existing.Width != textureData.Width || existing.Height != textureData.Height)
            {
                existing.Dispose();
                var newTexture = Texture.New2D(_device, textureData.Width, textureData.Height, pixelFormat, TextureFlags.ShaderResource);
                newTexture.SetData(_commandList, new ReadOnlySpan<byte>(textureData.Pixels, textureData.GetSizeInBytes()));
                _managedTextures[texId] = newTexture;
            }
            else
            {
                existing.SetData(_commandList, new ReadOnlySpan<byte>(textureData.Pixels, textureData.GetSizeInBytes()));
            }
        }
        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private void DestroyManagedTexture(ImTextureDataPtr textureData)
    {
        var texId = textureData.GetTexID();
        if (_managedTextures.TryGetValue(texId, out var texture))
        {
            texture.Dispose();
            _managedTextures.Remove(texId);
        }
        textureData.SetStatus(ImTextureStatus.Ok);
    }

    /// <summary>Resolves a managed texture id to its texture, when the id is one of ours.</summary>
    internal bool TryGet(ImTextureID texId, out Texture texture)
        => _managedTextures.TryGetValue(texId, out texture!);

    /// <summary>The first managed texture, in practice the font atlas; <see langword="null"/> before the atlas exists.</summary>
    internal Texture? FirstTexture()
    {
        foreach (var texture in _managedTextures.Values) return texture;
        return null;
    }

    public void Dispose()
    {
        foreach (var texture in _managedTextures.Values)
            texture.Dispose();
        _managedTextures.Clear();
    }
}