using System.Security.Cryptography;
using System.Text;

namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Derives stable asset ids from resource paths.
/// </summary>
/// <remarks>
/// The same resource path must always produce the same <see cref="Guid"/>: the id appears both in the
/// asset file and in the package's <c>RootAssets</c>, so a churning id would rewrite two files on every
/// build, produce noisy diffs and defeat the asset compiler's incremental cache.
/// </remarks>
public static class DeterministicId
{
    /// <summary>
    /// Computes the asset id for a resource.
    /// </summary>
    /// <param name="kind">Asset kind discriminator, for example <c>sound</c>.</param>
    /// <param name="projectRelativePath">Resource path relative to the project directory.</param>
    public static Guid FromResourcePath(string kind, string projectRelativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(projectRelativePath);

        var key = $"{kind}:{Normalize(projectRelativePath)}";

        // MD5 is used as a stable 128-bit digest, not for security.
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));

        return new Guid(hash);
    }

    /// <summary>
    /// Normalizes a path so the same file hashes identically regardless of host OS or casing.
    /// </summary>
    public static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');

        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.ToLowerInvariant();
    }
}
