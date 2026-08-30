using Example_CubicleCalamity.Gameplay;
using Example_CubicleCalamity.Shared;
using Xunit;

namespace Stride.CommunityToolkit.Tests.CubicleCalamity;

/// <summary>
/// Covers the scoring arithmetic for Cubicle Calamity.
/// </summary>
public class ScoreRulesTests
{
    [Fact]
    public void SingleCubeEarnsNoGroupBonus()
    {
        var result = ScoreRules.Score(1, comboStep: 0);

        // The bonus is n * (n - 1), so it is zero at one cube by construction rather than by a
        // special case. This is what removed the old 10-versus-60 jump between one cube and two.
        Assert.Equal(0, result.Bonus);
        Assert.Equal(GameSettings.BasePointsPerCube, result.Total);
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(3, 90)]
    [InlineData(5, 250)]
    [InlineData(10, 1000)]
    public void TotalIsBasePlusGroupBonus(int cubeCount, int expected)
    {
        var result = ScoreRules.Score(cubeCount, comboStep: 0);

        Assert.Equal(cubeCount * GameSettings.BasePointsPerCube, result.Base);
        Assert.Equal(cubeCount * (cubeCount - 1) * GameSettings.BasePointsPerCube, result.Bonus);
        Assert.Equal(expected, result.Total);
    }

    [Fact]
    public void ScoreGrowsFasterThanCubeCount()
    {
        // Clearing ten at once must beat clearing two five times over, or there is no reason to
        // hunt for a big group
        var atOnce = ScoreRules.Score(10, comboStep: 0).Total;
        var separately = 5 * ScoreRules.Score(2, comboStep: 0).Total;

        Assert.True(atOnce > separately, $"{atOnce} should beat {separately}");
    }

    [Theory]
    [InlineData(0, 1f)]
    [InlineData(1, 1.5f)]
    [InlineData(2, 2f)]
    [InlineData(3, 3f)]
    [InlineData(4, 5f)]
    public void ComboMultiplierFollowsTheTable(int comboStep, float expected)
        => Assert.Equal(expected, ScoreRules.GetMultiplier(comboStep));

    [Fact]
    public void ComboMultiplierHoldsAtTheTop()
    {
        // Running off the end of the table must not throw or wrap back to the start
        Assert.Equal(ScoreRules.GetMultiplier(4), ScoreRules.GetMultiplier(50));
    }

    [Fact]
    public void ComboMultiplierIsAppliedToTheTotal()
    {
        var plain = ScoreRules.Score(4, comboStep: 0);
        var combo = ScoreRules.Score(4, comboStep: 2);

        Assert.Equal(2f, combo.Multiplier);
        Assert.Equal(plain.Total * 2, combo.Total);
    }

    [Fact]
    public void BreakdownAgreesWithTheTotal()
    {
        // The old breakdown string was assembled separately from the total and had drifted out of
        // step with it, printing arithmetic that did not produce the number beside it
        var result = ScoreRules.Score(6, comboStep: 0);

        Assert.Equal($"{result.Base} + {result.Bonus} = {result.Total}", result.Breakdown);
        Assert.Equal(result.Base + result.Bonus, result.Total);
    }

    [Fact]
    public void BreakdownShowsTheMultiplierWhenThereIsOne()
    {
        var result = ScoreRules.Score(6, comboStep: 1);

        Assert.Contains("1.5", result.Breakdown);
        Assert.Contains(result.Total.ToString(), result.Breakdown);
    }

    [Theory]
    [InlineData(2, ScoreTier.Good)]
    [InlineData(5, ScoreTier.Nice)]
    [InlineData(10, ScoreTier.Great)]
    [InlineData(18, ScoreTier.Huge)]
    [InlineData(30, ScoreTier.Calamity)]
    [InlineData(200, ScoreTier.Calamity)]
    public void TiersRiseWithGroupSize(int cubeCount, ScoreTier expected)
        => Assert.Equal(expected, ScoreRules.GetTier(cubeCount));

    [Fact]
    public void ZeroCubesScoreNothing()
    {
        var result = ScoreRules.Score(0, comboStep: 3);

        Assert.Equal(0, result.Total);
    }
}
