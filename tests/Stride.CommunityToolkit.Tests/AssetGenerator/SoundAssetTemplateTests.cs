using Stride.CommunityToolkit.AssetGenerator.Core;
using Xunit;

namespace Stride.CommunityToolkit.Tests.AssetGenerator;

public class SoundAssetTemplateTests
{
    [Fact]
    public void Write_MatchesCommittedFixture()
    {
        var template = new SoundAssetTemplate(new SoundAssetOptions { SampleRate = 24000, CompressionRatio = 15 });

        var yaml = template.Write(Guid.Parse("daf2da16-0f0e-45fd-b080-43dd9a5d7266"), "../Resources/wood-tap-5.mp3");

        Assert.Equal(Fixtures.WoodTapSound, yaml);
    }

    [Fact]
    public void Write_UsesEngineDefaults()
    {
        var yaml = new SoundAssetTemplate().Write(Guid.Empty, "../Resources/a.mp3");

        Assert.Contains("SampleRate: 44100\r\n", yaml, StringComparison.Ordinal);
        Assert.Contains("CompressionRatio: 10\r\n", yaml, StringComparison.Ordinal);
        Assert.Contains("StreamFromDisk: false\r\n", yaml, StringComparison.Ordinal);
        Assert.Contains("Spatialized: false\r\n", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_EmitsForwardSlashesRegardlessOfInput()
    {
        var yaml = new SoundAssetTemplate().Write(Guid.Empty, @"..\Resources\sfx\boom.mp3");

        Assert.Contains("Source: !file ../Resources/sfx/boom.mp3\r\n", yaml, StringComparison.Ordinal);
    }
}
