using System.Globalization;
using System.Text;

namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Options written into newly created <c>.sdsnd</c> files.
/// </summary>
/// <remarks>
/// Defaults match <c>SoundAsset</c> in the engine
/// (<c>sources/engine/Stride.Assets/Media/SoundAsset.cs</c>). They only affect files the generator
/// creates — an existing asset file is never rewritten, so changing these does not touch assets that
/// are already on disk.
/// </remarks>
public sealed record SoundAssetOptions
{
    /// <summary>Target sample rate in Hz.</summary>
    public int SampleRate { get; init; } = 44100;

    /// <summary>Celt compression ratio.</summary>
    public int CompressionRatio { get; init; } = 10;

    /// <summary>Whether the sound is streamed from disk instead of loaded into memory.</summary>
    public bool StreamFromDisk { get; init; }

    /// <summary>Whether the sound is spatialized (mono, positional).</summary>
    public bool Spatialized { get; init; }
}

/// <summary>
/// Emits the YAML of a <c>.sdsnd</c> sound asset.
/// </summary>
public sealed class SoundAssetTemplate(SoundAssetOptions? options = null) : IAssetYamlWriter
{
    private readonly SoundAssetOptions _options = options ?? new SoundAssetOptions();

    /// <inheritdoc />
    public string Extension => AssetFormats.SoundExtension;

    /// <inheritdoc />
    public string Kind => "sound";

    /// <inheritdoc />
    public string Write(Guid id, string sourceRelativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceRelativePath);

        var builder = new StringBuilder();

        builder.Append(AssetFormats.SoundTag).Append(AssetFormats.NewLine);
        builder.Append("Id: ").Append(id.ToString("D", CultureInfo.InvariantCulture)).Append(AssetFormats.NewLine);
        builder.Append("SerializedVersion: ").Append(AssetFormats.SoundSerializedVersion).Append(AssetFormats.NewLine);
        builder.Append("Source: !file ").Append(sourceRelativePath.Replace('\\', '/')).Append(AssetFormats.NewLine);
        builder.Append("SampleRate: ").Append(_options.SampleRate.ToString(CultureInfo.InvariantCulture)).Append(AssetFormats.NewLine);
        builder.Append("CompressionRatio: ").Append(_options.CompressionRatio.ToString(CultureInfo.InvariantCulture)).Append(AssetFormats.NewLine);
        builder.Append("StreamFromDisk: ").Append(Bool(_options.StreamFromDisk)).Append(AssetFormats.NewLine);
        builder.Append("Spatialized: ").Append(Bool(_options.Spatialized)).Append(AssetFormats.NewLine);

        return builder.ToString();
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
