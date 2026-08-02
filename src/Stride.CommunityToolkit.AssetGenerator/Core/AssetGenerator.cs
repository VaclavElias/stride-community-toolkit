namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Generates the asset metadata files a code-only Stride project would otherwise need Game Studio to author.
/// </summary>
/// <remarks>
/// <para>
/// For every raw resource under the resources folder the generator writes the matching asset file
/// (for example <c>Resources/wood-tap-5.mp3</c> becomes <c>Assets/wood-tap-5.sdsnd</c>) and registers it in
/// the project's <c>.sdpkg</c> under <c>RootAssets</c>.
/// </para>
/// <para>
/// The <c>RootAssets</c> entry is not optional. <c>RootPackageAssetEnumerator</c>
/// (<c>sources/assets/Stride.Core.Assets/Compiler/RootPackageAssetEnumerator.cs</c>) only compiles assets
/// that are listed there, are reachable from something that is, or whose type is marked
/// <c>AlwaysMarkAsRoot</c> — and <c>SoundAsset</c> is not. In a code-only project nothing else references
/// the sound, so without the package entry the asset is silently culled from the build.
/// </para>
/// <para>
/// The generator only ever adds. It never overwrites an existing asset file and never deletes one, so a
/// file that was hand-written, or generated and then tweaked in Game Studio, is left exactly as it is.
/// </para>
/// </remarks>
public sealed class AssetGenerator
{
    private readonly PackageFileEditor _packageEditor = new();

    /// <summary>
    /// Runs the generator.
    /// </summary>
    public AssetGenerationResult Generate(AssetGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var projectDirectory = Path.GetFullPath(options.ProjectDirectory);

        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException($"Project directory not found: {projectDirectory}");
        }

        var resourcesRoot = Path.Combine(projectDirectory, options.ResourcesFolder);

        // Most projects have no resources folder at all; leave them completely untouched.
        if (!Directory.Exists(resourcesRoot)) return new AssetGenerationResult();

        var index = AssetFileIndex.Build(projectDirectory, options.AssetsFolder);
        var messages = new List<GeneratorMessage>();

        messages.AddRange(FindOrphans(projectDirectory, index));

        var writer = new SoundAssetTemplate(options.Sound);
        var resources = new ResourceScanner().ScanSounds(projectDirectory, options.ResourcesFolder);

        var created = new List<string>();
        var skipped = new List<string>();
        var rootAssets = new List<string>();

        // The index is a snapshot taken before any file is written, so assets created during this run
        // are tracked separately — two resources can otherwise map to the same asset path
        // (for example boom.mp3 and boom.wav both becoming Assets/boom.sdsnd).
        var createdThisRun = new HashSet<string>(PathUtilities.PathComparer);

        foreach (var resource in resources)
        {
            var assetPath = Path.GetFullPath(Path.Combine(
                projectDirectory,
                options.AssetsFolder,
                resource.AssetLocation.Replace('/', Path.DirectorySeparatorChar) + writer.Extension));

            if (createdThisRun.Contains(assetPath))
            {
                skipped.Add(resource.ProjectRelativePath);

                messages.Add(new GeneratorMessage(
                    MessageSeverity.Warning,
                    DiagnosticCodes.AssetPathTaken,
                    $"'{resource.ProjectRelativePath}' has no asset because '{PathUtilities.ToRelativePath(projectDirectory, assetPath)}' was already generated for another resource with the same name. Rename one of the files.",
                    assetPath));

                continue;
            }

            var existing = index.FindByPath(assetPath);

            if (existing is not null)
            {
                skipped.Add(PathUtilities.ToRelativePath(projectDirectory, assetPath));

                if (existing.Sources.Any(source => PathUtilities.PathComparer.Equals(source, resource.FullPath)))
                {
                    // Ours by location: leave the file alone, but make sure it is rooted so it compiles.
                    if (existing.Id is { } existingId)
                    {
                        rootAssets.Add(AssetFileIndex.FormatRootAssetEntry(existingId, resource.AssetLocation));
                    }
                }
                else
                {
                    messages.Add(new GeneratorMessage(
                        MessageSeverity.Warning,
                        DiagnosticCodes.AssetPathTaken,
                        $"'{resource.ProjectRelativePath}' has no asset because '{PathUtilities.ToRelativePath(projectDirectory, assetPath)}' already exists and describes a different source file. Rename one of them, or point the existing asset at this file.",
                        assetPath));
                }

                continue;
            }

            var importedElsewhere = index.FindBySource(resource.FullPath);

            if (importedElsewhere.Count > 0)
            {
                // Typical of a Game Studio project: the raw file is already described by an asset under a
                // different name. Adding a second asset for it would duplicate the content in the build.
                skipped.Add(resource.ProjectRelativePath);

                messages.Add(new GeneratorMessage(
                    MessageSeverity.Info,
                    DiagnosticCodes.ResourceAlreadyImported,
                    $"'{resource.ProjectRelativePath}' is already imported by '{PathUtilities.ToRelativePath(projectDirectory, importedElsewhere[0].FullPath)}'.",
                    importedElsewhere[0].FullPath));

                continue;
            }

            var id = DeterministicId.FromResourcePath(writer.Kind, resource.ProjectRelativePath);
            var assetDirectory = Path.GetDirectoryName(assetPath)!;
            var sourceRelativePath = PathUtilities.ToRelativePath(assetDirectory, resource.FullPath);

            var content = writer.Write(id, sourceRelativePath);

            if (!options.DryRun)
            {
                Directory.CreateDirectory(assetDirectory);
                File.WriteAllText(assetPath, content);
            }

            created.Add(PathUtilities.ToRelativePath(projectDirectory, assetPath));
            createdThisRun.Add(assetPath);
            rootAssets.Add(AssetFileIndex.FormatRootAssetEntry(id, resource.AssetLocation));
        }

        if (rootAssets.Count == 0)
        {
            return new AssetGenerationResult
            {
                CreatedAssets = created,
                SkippedResources = skipped,
                Messages = messages
            };
        }

        var (packageWritten, packageEntries) = MergePackage(options, rootAssets, messages);

        return new AssetGenerationResult
        {
            CreatedAssets = created,
            SkippedResources = skipped,
            PackageEntriesAdded = packageEntries,
            PackageWritten = packageWritten,
            Messages = messages
        };
    }

    private (bool Written, IReadOnlyList<string> Entries) MergePackage(
        AssetGeneratorOptions options,
        IReadOnlyList<string> rootAssets,
        List<GeneratorMessage> messages)
    {
        var packagePath = options.ResolvePackageFilePath();
        var existingContent = File.Exists(packagePath) ? File.ReadAllText(packagePath) : null;

        var request = new PackageMergeRequest
        {
            PackageName = options.ProjectName,
            AssetFolders = [options.AssetsFolder],
            ResourceFolders = options.EnsureResourceFolder ? [options.ResourcesFolder] : [],
            RootAssets = rootAssets
        };

        var merge = _packageEditor.Merge(existingContent, request);

        if (merge.Skipped)
        {
            messages.Add(new GeneratorMessage(
                MessageSeverity.Warning,
                DiagnosticCodes.PackageNotUnderstood,
                $"The package file was left untouched because {merge.SkipReason}. Generated assets will not be compiled until they are listed under RootAssets.",
                packagePath));

            return (false, []);
        }

        if (!merge.Changed || merge.Content is null) return (false, merge.AddedEntries);

        if (!options.DryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
            File.WriteAllText(packagePath, merge.Content);
        }

        return (true, merge.AddedEntries);
    }

    /// <summary>
    /// Reports asset files whose source file has disappeared. The generator does not track what it
    /// created, so it must never delete — a warning is the whole remedy.
    /// </summary>
    private static IEnumerable<GeneratorMessage> FindOrphans(string projectDirectory, AssetFileIndex index)
    {
        foreach (var asset in index.Assets.OrderBy(static asset => asset.FullPath, StringComparer.Ordinal))
        {
            foreach (var source in asset.Sources.Where(source => !File.Exists(source)))
            {
                yield return new GeneratorMessage(
                    MessageSeverity.Warning,
                    DiagnosticCodes.OrphanAsset,
                    $"'{PathUtilities.ToRelativePath(projectDirectory, asset.FullPath)}' points at a source file that does not exist ('{PathUtilities.ToRelativePath(projectDirectory, source)}'). Restore the source file or delete the asset.",
                    asset.FullPath);
            }
        }
    }
}
