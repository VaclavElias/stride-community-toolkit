namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Path helpers that always emit forward slashes, because that is what Stride's asset files use
/// regardless of host OS.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Returns <paramref name="fullPath"/> relative to <paramref name="basePath"/>, using forward slashes.
    /// </summary>
    public static string ToRelativePath(string basePath, string fullPath)
        => Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');

    /// <summary>
    /// Returns a comparer suitable for comparing file system paths on the current platform.
    /// </summary>
    public static StringComparer PathComparer
        => OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}
