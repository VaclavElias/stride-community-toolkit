using Stride.CommunityToolkit.AssetGenerator.Core;
using Xunit;

namespace Stride.CommunityToolkit.Tests.AssetGenerator;

public class DeterministicIdTests
{
    [Fact]
    public void FromResourcePath_IsPinned()
    {
        // Pinned on purpose: this value ends up in both the .sdsnd and the .sdpkg of every project
        // using the generator. Changing it would rewrite both files and invalidate the asset
        // compiler's incremental cache for everyone.
        Assert.Equal(
            Guid.Parse("695620a7-fe9e-d4b8-a920-4ceed9e6be4f"),
            DeterministicId.FromResourcePath("sound", "Resources/wood-tap-5.mp3"));

        Assert.Equal(
            Guid.Parse("80a56485-d82b-59b2-4670-d6c13a86471c"),
            DeterministicId.FromResourcePath("sound", "Resources/sfx/boom.mp3"));
    }

    [Theory]
    [InlineData("Resources/wood-tap-5.mp3")]
    [InlineData(@"Resources\wood-tap-5.mp3")]
    [InlineData("./Resources/wood-tap-5.mp3")]
    [InlineData("/Resources/wood-tap-5.mp3")]
    [InlineData("resources/WOOD-TAP-5.MP3")]
    public void FromResourcePath_IgnoresSeparatorStyleAndCase(string path)
    {
        var expected = DeterministicId.FromResourcePath("sound", "Resources/wood-tap-5.mp3");

        Assert.Equal(expected, DeterministicId.FromResourcePath("sound", path));
    }

    [Fact]
    public void FromResourcePath_DiffersByKindAndPath()
    {
        var sound = DeterministicId.FromResourcePath("sound", "Resources/a.mp3");

        Assert.NotEqual(sound, DeterministicId.FromResourcePath("texture", "Resources/a.mp3"));
        Assert.NotEqual(sound, DeterministicId.FromResourcePath("sound", "Resources/b.mp3"));
    }
}
