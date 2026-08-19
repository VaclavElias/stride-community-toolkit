namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Produces the YAML text of a single asset file.
/// </summary>
/// <remarks>
/// v1 writes YAML from templates rather than through Stride's own <c>AssetFileSerializer</c>, which is
/// only reachable by referencing <c>Stride.Core.Assets</c> and <c>Stride.Assets</c> — editor-side
/// packages that drag in MSBuild, Roslyn workspaces and FFmpeg. This interface exists so an
/// <c>AssetFileSerializer</c>-backed implementation can replace the template later without touching
/// the rest of the generator.
/// </remarks>
public interface IAssetYamlWriter
{
    /// <summary>File extension of the asset this writer produces, including the leading dot.</summary>
    string Extension { get; }

    /// <summary>Kind discriminator used when deriving the deterministic asset id.</summary>
    string Kind { get; }

    /// <summary>
    /// Writes the asset file content.
    /// </summary>
    /// <param name="id">Deterministic asset id.</param>
    /// <param name="sourceRelativePath">
    /// Path of the raw resource, relative to the asset file, using forward slashes.
    /// </param>
    string Write(Guid id, string sourceRelativePath);
}
