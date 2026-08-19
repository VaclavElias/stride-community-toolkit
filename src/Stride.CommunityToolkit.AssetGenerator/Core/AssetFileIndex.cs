using System.Globalization;

namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// The bits of an existing asset file the generator needs to know about.
/// </summary>
/// <param name="FullPath">Absolute path of the asset file.</param>
/// <param name="Id">Asset id, or <see langword="null"/> when the file has no readable <c>Id:</c> line.</param>
/// <param name="Sources">
/// Absolute paths of every <c>!file</c> reference in the asset, resolved against the asset's directory.
/// </param>
public sealed record AssetFileInfo(string FullPath, Guid? Id, IReadOnlyList<string> Sources);

/// <summary>
/// Indexes the asset files already present in a project.
/// </summary>
/// <remarks>
/// Two things depend on this:
/// <list type="bullet">
/// <item>an existing asset file is never overwritten — file existence is the whole ownership test;</item>
/// <item>a resource that some other asset already points at is left alone entirely. That is what keeps
/// the generator harmless in a Game Studio project, where <c>Resources/</c> is full of raw files that
/// already have hand-authored assets under different names.</item>
/// </list>
/// </remarks>
public sealed class AssetFileIndex
{
    private readonly Dictionary<string, AssetFileInfo> _byAssetPath;
    private readonly Dictionary<string, List<AssetFileInfo>> _bySourcePath;

    private AssetFileIndex(Dictionary<string, AssetFileInfo> byAssetPath, Dictionary<string, List<AssetFileInfo>> bySourcePath)
    {
        _byAssetPath = byAssetPath;
        _bySourcePath = bySourcePath;
    }

    /// <summary>All indexed asset files.</summary>
    public IReadOnlyCollection<AssetFileInfo> Assets => _byAssetPath.Values;

    /// <summary>
    /// Scans <paramref name="assetsFolder"/> recursively for Stride asset files.
    /// </summary>
    /// <param name="projectDirectory">Absolute path of the project directory.</param>
    /// <param name="assetsFolder">Assets folder name, relative to the project directory.</param>
    public static AssetFileIndex Build(string projectDirectory, string assetsFolder)
    {
        var byAssetPath = new Dictionary<string, AssetFileInfo>(PathUtilities.PathComparer);
        var bySourcePath = new Dictionary<string, List<AssetFileInfo>>(PathUtilities.PathComparer);

        var root = Path.Combine(projectDirectory, assetsFolder);

        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.sd*", SearchOption.AllDirectories))
            {
                var info = Read(file);

                byAssetPath[info.FullPath] = info;

                foreach (var source in info.Sources)
                {
                    if (!bySourcePath.TryGetValue(source, out var list))
                    {
                        list = [];
                        bySourcePath[source] = list;
                    }

                    list.Add(info);
                }
            }
        }

        return new AssetFileIndex(byAssetPath, bySourcePath);
    }

    /// <summary>
    /// Returns the indexed asset at <paramref name="assetFullPath"/>, if any.
    /// </summary>
    public AssetFileInfo? FindByPath(string assetFullPath)
        => _byAssetPath.GetValueOrDefault(Path.GetFullPath(assetFullPath));

    /// <summary>
    /// Returns every asset referencing <paramref name="sourceFullPath"/> as a source file.
    /// </summary>
    public IReadOnlyList<AssetFileInfo> FindBySource(string sourceFullPath)
        => _bySourcePath.GetValueOrDefault(Path.GetFullPath(sourceFullPath)) ?? (IReadOnlyList<AssetFileInfo>)[];

    /// <summary>
    /// Reads the id and source references of a single asset file.
    /// </summary>
    public static AssetFileInfo Read(string assetFilePath)
    {
        var fullPath = Path.GetFullPath(assetFilePath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;

        Guid? id = null;
        var sources = new List<string>();

        string[] lines;

        try
        {
            lines = File.ReadAllLines(fullPath);
        }
        catch (IOException)
        {
            return new AssetFileInfo(fullPath, null, []);
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (id is null && trimmed.StartsWith("Id:", StringComparison.Ordinal)
                && Guid.TryParseExact(trimmed[3..].Trim(), "D", out var parsed))
            {
                id = parsed;
                continue;
            }

            var marker = trimmed.IndexOf("!file ", StringComparison.Ordinal);

            if (marker < 0) continue;

            var value = trimmed[(marker + "!file ".Length)..].Trim();

            if (value.Length == 0) continue;

            sources.Add(Path.GetFullPath(Path.Combine(directory, value.Replace('/', Path.DirectorySeparatorChar))));
        }

        return new AssetFileInfo(fullPath, id, sources);
    }

    /// <summary>
    /// Formats an entry for the package's <c>RootAssets</c> section.
    /// </summary>
    public static string FormatRootAssetEntry(Guid id, string assetLocation)
        => $"{id.ToString("D", CultureInfo.InvariantCulture)}:{assetLocation}";
}
