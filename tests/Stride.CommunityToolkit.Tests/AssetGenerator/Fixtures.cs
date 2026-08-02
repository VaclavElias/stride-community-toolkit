namespace Stride.CommunityToolkit.Tests.AssetGenerator;

/// <summary>
/// Copies of real, Game Studio-compatible files used as the reference corpus.
/// </summary>
/// <remarks>
/// Taken from <c>examples/code-only/Example_CubicleCalamity</c>, which is a validated working project:
/// its hand-written <c>.sdsnd</c> plus <c>RootAssets</c> entry compile and load at runtime. Note the two
/// indentation styles Game Studio emits — <c>AssetFolders</c> uses <c>-   Path:</c> (dash + three spaces),
/// <c>ResourceFolders</c> a plain <c>- !dir</c>.
/// </remarks>
internal static class Fixtures
{
    /// <summary>The committed <c>Assets/wood-tap-5.sdsnd</c>.</summary>
    public const string WoodTapSound =
        "!Sound\r\n" +
        "Id: daf2da16-0f0e-45fd-b080-43dd9a5d7266\r\n" +
        "SerializedVersion: {Stride: 2.0.0.0}\r\n" +
        "Source: !file ../Resources/wood-tap-5.mp3\r\n" +
        "SampleRate: 24000\r\n" +
        "CompressionRatio: 15\r\n" +
        "StreamFromDisk: false\r\n" +
        "Spatialized: false\r\n";

    /// <summary>The committed <c>Example_CubicleCalamity.sdpkg</c> (no trailing newline, as on disk).</summary>
    public const string CubicleCalamityPackage =
        "!Package\r\n" +
        "SerializedVersion: {Assets: 3.1.0.0}\r\n" +
        "AssetFolders:\r\n" +
        "    -   Path: !dir Assets\r\n" +
        "    -   Path: !dir Effects\r\n" +
        "RootAssets:\r\n" +
        "    -   daf2da16-0f0e-45fd-b080-43dd9a5d7266:wood-tap-5";

    /// <summary>A fuller Game Studio-authored package, from <c>MyGame01.Game.sdpkg</c>.</summary>
    public const string GameStudioPackage =
        "!Package\r\n" +
        "SerializedVersion: {Assets: 3.1.0.0}\r\n" +
        "Meta:\r\n" +
        "    Name: MyGame01\r\n" +
        "    Version: 1.0.0\r\n" +
        "    Authors: []\r\n" +
        "    Owners: []\r\n" +
        "    Dependencies: null\r\n" +
        "AssetFolders:\r\n" +
        "    -   Path: !dir Assets\r\n" +
        "ResourceFolders:\r\n" +
        "    - !dir Resources\r\n";
}
