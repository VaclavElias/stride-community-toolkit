using Stride.CommunityToolkit.AssetGenerator.Core;
using Xunit;

namespace Stride.CommunityToolkit.Tests.AssetGenerator;

public class AssetGeneratorTests
{
    [Fact]
    public void Generate_CreatesAssetAndRegistersRoot()
    {
        using var project = new TestProject("MyGame");

        project.AddResource("wood-tap-5.mp3");

        var result = project.Generate(project.Options(new SoundAssetOptions { SampleRate = 24000, CompressionRatio = 15 }));

        Assert.Equal(["Assets/wood-tap-5.sdsnd"], result.CreatedAssets);
        Assert.True(result.PackageWritten);

        var expectedId = DeterministicId.FromResourcePath("sound", "Resources/wood-tap-5.mp3");

        // Same shape as the committed Example_CubicleCalamity asset, only the id differs.
        Assert.Equal(
            Fixtures.WoodTapSound.Replace("daf2da16-0f0e-45fd-b080-43dd9a5d7266", expectedId.ToString("D"), StringComparison.Ordinal),
            project.Read("Assets/wood-tap-5.sdsnd"));

        Assert.Contains($"    -   {expectedId:D}:wood-tap-5", project.Read("MyGame.sdpkg"), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_MirrorsSubfolders()
    {
        using var project = new TestProject();

        project.AddResource("sfx/boom.mp3");

        var result = project.Generate();

        Assert.Equal(["Assets/sfx/boom.sdsnd"], result.CreatedAssets);

        var asset = project.Read("Assets/sfx/boom.sdsnd");

        Assert.Contains("Source: !file ../../Resources/sfx/boom.mp3\r\n", asset, StringComparison.Ordinal);
        Assert.Contains(":sfx/boom", project.Read("TestGame.sdpkg"), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_IsIdempotent()
    {
        using var project = new TestProject();

        project.AddResource("a.mp3");
        project.AddResource("sfx/b.wav");
        project.Generate();

        var assetBefore = project.Read("Assets/a.sdsnd");
        var packageBefore = project.Read("TestGame.sdpkg");

        var second = project.Generate();

        Assert.Empty(second.CreatedAssets);
        Assert.False(second.PackageWritten);
        Assert.False(second.AnyChanges);
        Assert.Equal(assetBefore, project.Read("Assets/a.sdsnd"));
        Assert.Equal(packageBefore, project.Read("TestGame.sdpkg"));
    }

    [Fact]
    public void Generate_NeverOverwritesExistingAsset()
    {
        using var project = new TestProject();

        project.AddResource("wood-tap-5.mp3");

        const string handWritten =
            "!Sound\r\n" +
            "Id: daf2da16-0f0e-45fd-b080-43dd9a5d7266\r\n" +
            "SerializedVersion: {Stride: 2.0.0.0}\r\n" +
            "Source: !file ../Resources/wood-tap-5.mp3\r\n" +
            "SampleRate: 8000\r\n" +
            "CompressionRatio: 20\r\n" +
            "StreamFromDisk: true\r\n" +
            "Spatialized: true\r\n";

        project.AddAsset("wood-tap-5.sdsnd", handWritten);

        var result = project.Generate();

        Assert.Empty(result.CreatedAssets);
        Assert.Equal(handWritten, project.Read("Assets/wood-tap-5.sdsnd"));

        // The hand-written asset still gets rooted, using its own id.
        Assert.Contains(
            "    -   daf2da16-0f0e-45fd-b080-43dd9a5d7266:wood-tap-5",
            project.Read("TestGame.sdpkg"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WarnsWhenAssetPathIsTakenByAnotherSource()
    {
        using var project = new TestProject();

        project.AddResource("boom.mp3");
        project.AddResource("boom.wav");

        // Both resources map to Assets/boom.sdsnd; the first one wins, the second is reported.
        var result = project.Generate();

        Assert.Single(result.CreatedAssets);

        var warning = Assert.Single(result.Messages, message => message.Code == DiagnosticCodes.AssetPathTaken);

        Assert.Contains("boom.wav", warning.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_LeavesResourcesAlreadyImportedElsewhereAlone()
    {
        using var project = new TestProject();

        project.AddResource("wood-tap-5.mp3");

        // A Game Studio-style asset: same source file, different asset name.
        project.AddAsset("Audio/Tap.sdsnd", Fixtures.WoodTapSound.Replace(
            "../Resources/wood-tap-5.mp3",
            "../../Resources/wood-tap-5.mp3",
            StringComparison.Ordinal));

        var result = project.Generate();

        Assert.Empty(result.CreatedAssets);
        Assert.False(project.Exists("Assets/wood-tap-5.sdsnd"));
        Assert.False(result.PackageWritten);
        Assert.Contains(result.Messages, message => message.Code == DiagnosticCodes.ResourceAlreadyImported);
    }

    [Fact]
    public void Generate_WarnsAboutOrphansAndDeletesNothing()
    {
        using var project = new TestProject();

        project.AddResource("a.mp3");
        project.AddAsset("gone.sdsnd", Fixtures.WoodTapSound);

        var result = project.Generate();

        var warning = Assert.Single(result.Messages, message => message.Code == DiagnosticCodes.OrphanAsset);

        Assert.Contains("gone.sdsnd", warning.Text, StringComparison.Ordinal);
        Assert.True(project.Exists("Assets/gone.sdsnd"));
    }

    [Fact]
    public void Generate_IgnoresUnknownExtensions()
    {
        using var project = new TestProject();

        project.AddResource("clip.mp4");
        project.AddResource("notes.md");
        project.AddResource("texture.png");

        var result = project.Generate();

        Assert.Empty(result.CreatedAssets);
        Assert.False(result.PackageWritten);
        Assert.False(File.Exists(project.PackagePath));
    }

    [Fact]
    public void Generate_DoesNothingWithoutResourcesFolder()
    {
        using var project = new TestProject();

        var result = project.Generate();

        Assert.False(result.AnyChanges);
        Assert.Empty(result.Messages);
        Assert.False(File.Exists(project.PackagePath));
    }

    [Fact]
    public void Generate_PreservesExistingPackageContent()
    {
        using var project = new TestProject("Example_CubicleCalamity");

        project.AddResource("boom.mp3");
        project.WritePackage(Fixtures.CubicleCalamityPackage);

        project.Generate();

        var package = project.Read("Example_CubicleCalamity.sdpkg");

        Assert.Contains("    -   Path: !dir Effects\r\n", package, StringComparison.Ordinal);
        Assert.Contains("    -   daf2da16-0f0e-45fd-b080-43dd9a5d7266:wood-tap-5\r\n", package, StringComparison.Ordinal);
        Assert.Contains(":boom", package, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_DryRunWritesNothing()
    {
        using var project = new TestProject();

        project.AddResource("a.mp3");

        var result = project.Generate(project.Options() with { DryRun = true });

        Assert.Single(result.CreatedAssets);
        Assert.True(result.PackageWritten);
        Assert.False(project.Exists("Assets/a.sdsnd"));
        Assert.False(File.Exists(project.PackagePath));
    }

    [Fact]
    public void Generate_SkipsPackageItCannotParse()
    {
        using var project = new TestProject();

        project.AddResource("a.mp3");
        project.WritePackage("this is not a package\r\n");

        var result = project.Generate();

        Assert.Single(result.CreatedAssets);
        Assert.False(result.PackageWritten);
        Assert.Contains(result.Messages, message => message.Code == DiagnosticCodes.PackageNotUnderstood);
        Assert.Equal("this is not a package\r\n", project.Read("TestGame.sdpkg"));
    }
}
