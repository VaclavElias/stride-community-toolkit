using Stride.CommunityToolkit.AssetGenerator.Core;
using Xunit;

namespace Stride.CommunityToolkit.Tests.AssetGenerator;

public class PackageFileEditorTests
{
    private readonly PackageFileEditor _editor = new();

    private static PackageMergeRequest Request(params string[] rootAssets) => new()
    {
        PackageName = "MyGame",
        AssetFolders = ["Assets"],
        ResourceFolders = ["Resources"],
        RootAssets = rootAssets
    };

    [Fact]
    public void Merge_CreatesPackageWhenMissing()
    {
        var result = _editor.Merge(null, Request("11111111-1111-1111-1111-111111111111:boom"));

        Assert.True(result.Changed);
        Assert.False(result.Skipped);

        Assert.Equal(
            "!Package\r\n" +
            "SerializedVersion: {Assets: 3.1.0.0}\r\n" +
            "Meta:\r\n" +
            "    Name: MyGame\r\n" +
            "    Version: 1.0.0\r\n" +
            "    Authors: []\r\n" +
            "    Owners: []\r\n" +
            "    Dependencies: null\r\n" +
            "AssetFolders:\r\n" +
            "    -   Path: !dir Assets\r\n" +
            "ResourceFolders:\r\n" +
            "    - !dir Resources\r\n" +
            "RootAssets:\r\n" +
            "    -   11111111-1111-1111-1111-111111111111:boom\r\n",
            result.Content);
    }

    [Fact]
    public void Merge_PreservesHandAuthoredContent()
    {
        var result = _editor.Merge(Fixtures.CubicleCalamityPackage, Request("11111111-1111-1111-1111-111111111111:boom"));

        Assert.False(result.Skipped);

        var content = result.Content!;

        // The Effects asset folder and the pre-existing root asset must survive untouched.
        Assert.Contains("    -   Path: !dir Effects\r\n", content, StringComparison.Ordinal);
        Assert.Contains("    -   daf2da16-0f0e-45fd-b080-43dd9a5d7266:wood-tap-5\r\n", content, StringComparison.Ordinal);
        Assert.Contains("    -   11111111-1111-1111-1111-111111111111:boom", content, StringComparison.Ordinal);

        // The new root asset goes at the end of the existing block, not before it.
        Assert.True(
            content.IndexOf("daf2da16", StringComparison.Ordinal) < content.IndexOf("11111111", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_PreservesMetaAndDoesNotDuplicateFolders()
    {
        var result = _editor.Merge(Fixtures.GameStudioPackage, Request("11111111-1111-1111-1111-111111111111:boom"));

        var content = result.Content!;

        Assert.Contains("    Name: MyGame01\r\n", content, StringComparison.Ordinal);
        Assert.Contains("    Dependencies: null\r\n", content, StringComparison.Ordinal);

        Assert.Equal(1, CountOccurrences(content, "!dir Assets"));
        Assert.Equal(1, CountOccurrences(content, "!dir Resources"));

        // RootAssets was absent altogether and gets appended.
        Assert.Contains("RootAssets:\r\n    -   11111111-1111-1111-1111-111111111111:boom", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_ReplacesEmptyInlineSection()
    {
        const string package =
            "!Package\r\n" +
            "SerializedVersion: {Assets: 3.1.0.0}\r\n" +
            "AssetFolders:\r\n" +
            "    -   Path: !dir Assets\r\n" +
            "ResourceFolders: []\r\n" +
            "RootAssets: []\r\n";

        var result = _editor.Merge(package, Request("11111111-1111-1111-1111-111111111111:boom"));

        var content = result.Content!;

        Assert.DoesNotContain("RootAssets: []", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceFolders: []", content, StringComparison.Ordinal);
        Assert.Contains("RootAssets:\r\n    -   11111111-1111-1111-1111-111111111111:boom\r\n", content, StringComparison.Ordinal);
        Assert.Contains("ResourceFolders:\r\n    - !dir Resources\r\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_IsIdempotent()
    {
        var first = _editor.Merge(Fixtures.CubicleCalamityPackage, Request("11111111-1111-1111-1111-111111111111:boom"));
        var second = _editor.Merge(first.Content, Request("11111111-1111-1111-1111-111111111111:boom"));

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(first.Content, second.Content);
        Assert.Empty(second.AddedEntries);
    }

    [Fact]
    public void Merge_SkipsRootAssetWhenLocationAlreadyRegistered()
    {
        // Same location, different id: whoever registered it first owns it.
        var result = _editor.Merge(Fixtures.CubicleCalamityPackage, Request("11111111-1111-1111-1111-111111111111:wood-tap-5"));

        Assert.DoesNotContain("11111111", result.Content!, StringComparison.Ordinal);
        Assert.DoesNotContain(result.AddedEntries, entry => entry.StartsWith("RootAssets", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_PreservesLineEndingsAndTrailingNewline()
    {
        var unixPackage = Fixtures.CubicleCalamityPackage.Replace("\r\n", "\n", StringComparison.Ordinal);

        var result = _editor.Merge(unixPackage, Request("11111111-1111-1111-1111-111111111111:boom"));

        Assert.DoesNotContain('\r', result.Content!);
        Assert.False(result.Content!.EndsWith('\n')); // the fixture has no trailing newline
    }

    [Fact]
    public void Merge_SkipsFileThatIsNotAPackage()
    {
        var result = _editor.Merge("not: a package\r\n", Request("11111111-1111-1111-1111-111111111111:boom"));

        Assert.True(result.Skipped);
        Assert.Null(result.Content);
    }

    [Fact]
    public void Merge_SkipsSectionWithUnexpectedInlineValue()
    {
        const string package =
            "!Package\r\n" +
            "SerializedVersion: {Assets: 3.1.0.0}\r\n" +
            "RootAssets: null\r\n";

        var result = _editor.Merge(package, Request("11111111-1111-1111-1111-111111111111:boom"));

        Assert.True(result.Skipped);
        Assert.Contains("RootAssets", result.SkipReason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_HandlesMultiLineEntries()
    {
        const string package =
            "!Package\r\n" +
            "SerializedVersion: {Assets: 3.1.0.0}\r\n" +
            "AssetFolders:\r\n" +
            "    -   Path: !dir Assets\r\n" +
            "        AlternativePath: !dir Other\r\n" +
            "RootAssets: []\r\n";

        var result = _editor.Merge(package, Request("11111111-1111-1111-1111-111111111111:boom"));

        var content = result.Content!;

        // The continuation line belongs to the Assets entry, so Assets must not be added again.
        Assert.Equal(1, CountOccurrences(content, "Path: !dir Assets"));
        Assert.Contains("        AlternativePath: !dir Other\r\n", content, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
