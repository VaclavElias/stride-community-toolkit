namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Inputs of a generator run.
/// </summary>
public sealed record AssetGeneratorOptions
{
    /// <summary>Absolute path of the project directory.</summary>
    public required string ProjectDirectory { get; init; }

    /// <summary>
    /// Project name, used to name the package file when <see cref="PackageFilePath"/> is not set.
    /// </summary>
    /// <remarks>
    /// The engine looks for <c>$(MSBuildProjectDirectory)\$(MSBuildProjectName).sdpkg</c> when
    /// <c>$(StrideCurrentPackagePath)</c> is unset
    /// (<c>sources/core/Stride.Core/build/Stride.AssetBuildManifest.targets</c>), so the generated
    /// package must be named after the project.
    /// </remarks>
    public required string ProjectName { get; init; }

    /// <summary>Explicit package file path; defaults to <c>{ProjectDirectory}/{ProjectName}.sdpkg</c>.</summary>
    public string? PackageFilePath { get; init; }

    /// <summary>Folder holding generated asset files, relative to the project directory.</summary>
    public string AssetsFolder { get; init; } = "Assets";

    /// <summary>Folder holding raw resource files, relative to the project directory.</summary>
    public string ResourcesFolder { get; init; } = "Resources";

    /// <summary>Options written into newly created sound assets.</summary>
    public SoundAssetOptions Sound { get; init; } = new();

    /// <summary>Whether to also list the resources folder under the package's <c>ResourceFolders</c>.</summary>
    public bool EnsureResourceFolder { get; init; } = true;

    /// <summary>When set, nothing is written to disk; the result still describes what would change.</summary>
    public bool DryRun { get; init; }

    /// <summary>Resolves the package file path.</summary>
    public string ResolvePackageFilePath()
        => PackageFilePath is { Length: > 0 }
            ? Path.GetFullPath(PackageFilePath)
            : Path.Combine(Path.GetFullPath(ProjectDirectory), $"{ProjectName}.sdpkg");
}
