using Stride.CommunityToolkit.AssetGenerator.Core;
using Generator = Stride.CommunityToolkit.AssetGenerator.Core.AssetGenerator;

namespace Stride.CommunityToolkit.Tests.AssetGenerator;

/// <summary>
/// A throwaway project directory on disk, used to exercise the generator end to end.
/// </summary>
internal sealed class TestProject : IDisposable
{
    public TestProject(string name = "TestGame")
    {
        Name = name;
        Directory = Path.Combine(Path.GetTempPath(), "stct-assetgen", Guid.NewGuid().ToString("N"));

        System.IO.Directory.CreateDirectory(Directory);
    }

    public string Name { get; }

    public string Directory { get; }

    public string PackagePath => At($"{Name}.sdpkg");

    /// <summary>Resolves a path inside the project from a forward-slash relative path.</summary>
    public string At(string relativePath) => Path.Combine(Directory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Creates a raw resource file with dummy content.</summary>
    public string AddResource(string relativePath, byte[]? content = null)
        => WriteBytes($"Resources/{relativePath}", content ?? [0x00, 0x01, 0x02]);

    /// <summary>Creates an asset file with the given text.</summary>
    public string AddAsset(string relativePath, string content) => WriteText($"Assets/{relativePath}", content);

    /// <summary>Writes the project's package file.</summary>
    public string WritePackage(string content) => WriteText($"{Name}.sdpkg", content);

    public bool Exists(string relativePath) => File.Exists(At(relativePath));

    public string Read(string relativePath) => File.ReadAllText(At(relativePath));

    public AssetGeneratorOptions Options(SoundAssetOptions? sound = null) => new()
    {
        ProjectDirectory = Directory,
        ProjectName = Name,
        Sound = sound ?? new SoundAssetOptions()
    };

    public AssetGenerationResult Generate(AssetGeneratorOptions? options = null)
        => new Generator().Generate(options ?? Options());

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A locked file must not fail the test run.
        }
    }

    private string WriteText(string relativePath, string content)
    {
        var fullPath = Prepare(relativePath);

        File.WriteAllText(fullPath, content);

        return fullPath;
    }

    private string WriteBytes(string relativePath, byte[] content)
    {
        var fullPath = Prepare(relativePath);

        File.WriteAllBytes(fullPath, content);

        return fullPath;
    }

    private string Prepare(string relativePath)
    {
        var fullPath = At(relativePath);

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        return fullPath;
    }
}
