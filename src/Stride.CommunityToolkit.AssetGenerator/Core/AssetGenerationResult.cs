namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Severity of a generator message.
/// </summary>
public enum MessageSeverity
{
    /// <summary>Informational; not surfaced as an MSBuild diagnostic.</summary>
    Info,

    /// <summary>Surfaced as an MSBuild warning.</summary>
    Warning,

    /// <summary>Surfaced as an MSBuild error.</summary>
    Error
}

/// <summary>
/// A diagnostic produced by the generator.
/// </summary>
/// <param name="Severity">How the message should be surfaced.</param>
/// <param name="Code">Stable diagnostic code, for example <c>STCT0001</c>.</param>
/// <param name="Text">Message text.</param>
/// <param name="File">File the message is about, if any.</param>
public sealed record GeneratorMessage(MessageSeverity Severity, string Code, string Text, string? File = null);

/// <summary>
/// Diagnostic codes the generator emits.
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>An asset file points at a source file that no longer exists.</summary>
    public const string OrphanAsset = "STCT0001";

    /// <summary>An asset file already exists at the target path but describes a different source.</summary>
    public const string AssetPathTaken = "STCT0002";

    /// <summary>The package file could not be parsed with confidence and was left untouched.</summary>
    public const string PackageNotUnderstood = "STCT0003";

    /// <summary>A resource is already described by an asset elsewhere, so it was left alone.</summary>
    public const string ResourceAlreadyImported = "STCT0004";
}

/// <summary>
/// What a generator run did.
/// </summary>
public sealed record AssetGenerationResult
{
    /// <summary>Asset files that were created.</summary>
    public IReadOnlyList<string> CreatedAssets { get; init; } = [];

    /// <summary>Resources that were left alone because an asset file already describes them.</summary>
    public IReadOnlyList<string> SkippedResources { get; init; } = [];

    /// <summary>Entries added to the package file.</summary>
    public IReadOnlyList<string> PackageEntriesAdded { get; init; } = [];

    /// <summary>Whether the package file was written.</summary>
    public bool PackageWritten { get; init; }

    /// <summary>Diagnostics produced during the run.</summary>
    public IReadOnlyList<GeneratorMessage> Messages { get; init; } = [];

    /// <summary>Whether anything was written to disk.</summary>
    public bool AnyChanges => CreatedAssets.Count > 0 || PackageWritten;
}
