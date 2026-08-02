namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// A raw resource file that can be turned into a Stride asset.
/// </summary>
/// <param name="FullPath">Absolute path of the resource file.</param>
/// <param name="ProjectRelativePath">Path relative to the project directory, using forward slashes.</param>
/// <param name="AssetLocation">
/// Asset location (path under the assets folder without extension, forward slashes), for example
/// <c>sfx/boom</c>. This is the string passed to <c>Content.Load</c>.
/// </param>
public readonly record struct ResourceFile(string FullPath, string ProjectRelativePath, string AssetLocation);

/// <summary>
/// Enumerates raw resource files that the generator knows how to describe.
/// </summary>
public sealed class ResourceScanner
{
    /// <summary>
    /// Audio extensions recognised by the generator.
    /// </summary>
    /// <remarks>
    /// The engine's <c>RawSoundAssetImporter.FileExtensions</c>
    /// (<c>sources/engine/Stride.Assets/Media/RawSoundAssetImporter.cs</c>) also accepts video
    /// extensions, because a video file can carry an audio track. Those are deliberately excluded here
    /// so that dropping an <c>.mp4</c> into <c>Resources/</c> is not silently turned into a sound asset.
    /// </remarks>
    public static readonly string[] SoundExtensions =
    [
        ".wav", ".mp3", ".ogg", ".aac", ".aiff", ".flac", ".m4a", ".wma", ".mpc"
    ];

    /// <summary>
    /// Recursively enumerates sound resources under <paramref name="resourcesFolder"/>.
    /// </summary>
    /// <param name="projectDirectory">Absolute path of the project directory.</param>
    /// <param name="resourcesFolder">Resource folder name, relative to the project directory.</param>
    /// <returns>Resources ordered by path, or an empty sequence when the folder does not exist.</returns>
    public IReadOnlyList<ResourceFile> ScanSounds(string projectDirectory, string resourcesFolder)
        => Scan(projectDirectory, resourcesFolder, SoundExtensions);

    /// <summary>
    /// Recursively enumerates resources with any of <paramref name="extensions"/>.
    /// </summary>
    public IReadOnlyList<ResourceFile> Scan(string projectDirectory, string resourcesFolder, IReadOnlyCollection<string> extensions)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);
        ArgumentException.ThrowIfNullOrEmpty(resourcesFolder);
        ArgumentNullException.ThrowIfNull(extensions);

        var root = Path.Combine(projectDirectory, resourcesFolder);

        if (!Directory.Exists(root)) return [];

        var results = new List<ResourceFile>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);

            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;

            var fullPath = Path.GetFullPath(file);
            var relativeToProject = PathUtilities.ToRelativePath(projectDirectory, fullPath);
            var relativeToRoot = PathUtilities.ToRelativePath(root, fullPath);

            var location = relativeToRoot[..^extension.Length];

            results.Add(new ResourceFile(fullPath, relativeToProject, location));
        }

        results.Sort(static (a, b) => string.CompareOrdinal(a.ProjectRelativePath, b.ProjectRelativePath));

        return results;
    }
}
