namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Hardcoded pieces of Stride's asset file format.
/// </summary>
/// <remarks>
/// These mirror the engine, which is the source of truth:
/// <list type="bullet">
/// <item><c>sources/engine/Stride.Assets/Media/SoundAsset.cs</c> — <c>[DataContract("Sound")]</c>,
/// <c>AssetFormatVersion(CurrentVersion = "2.0.0.0")</c>, extension <c>.sdsnd</c>.</item>
/// <item><c>sources/assets/Stride.Core.Assets/Package.cs</c> — package serialized version.</item>
/// </list>
/// Stride ships asset upgraders for version bumps, so these values change rarely; when one does
/// move, it is a one-line change here.
/// </remarks>
public static class AssetFormats
{
    /// <summary>YAML header line of a package file.</summary>
    public const string PackageTag = "!Package";

    /// <summary><c>SerializedVersion</c> value written into a <c>.sdpkg</c>.</summary>
    public const string PackageSerializedVersion = "{Assets: 3.1.0.0}";

    /// <summary>YAML header line of a sound asset file.</summary>
    public const string SoundTag = "!Sound";

    /// <summary><c>SerializedVersion</c> value written into a <c>.sdsnd</c>.</summary>
    public const string SoundSerializedVersion = "{Stride: 2.0.0.0}";

    /// <summary>File extension of a sound asset.</summary>
    public const string SoundExtension = ".sdsnd";

    /// <summary>Line ending used when writing new files; matches what Game Studio emits on Windows.</summary>
    public const string NewLine = "\r\n";

    /// <summary>Prefix of every Stride asset file extension.</summary>
    public const string AssetExtensionPrefix = ".sd";
}
