using Stride.CommunityToolkit.Engine;
using Stride.Engine;
using Stride.Games;
using System.Reflection;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Engine;

/// <summary>
/// Pins what <see cref="GameLoopExtensions.SetDeterministic"/> sets, including the engine's
/// one-update-per-draw switch that it reaches by reflection. If the engine renames that property
/// the call would silently stop pinning frame N, and every golden image would drift with it;
/// this test is what fails instead.
/// </summary>
[Collection(GameExtensionsRunTests.Name)]
public class GameDeterminismTests
{
    [Fact]
    public void SetDeterministic_PinsTheLoop()
    {
        using var game = new Game();

        game.SetDeterministic(TimeSpan.FromMilliseconds(20));

        Assert.True(game.IsFixedTimeStep);
        Assert.False(game.IsDrawDesynchronized);
        Assert.Equal(TimeSpan.FromMilliseconds(20), game.TargetElapsedTime);

        // The same reflection the extension uses: if the engine renames the property, this is the test that says so
        var property = typeof(GameBase).GetProperty("ForceOneUpdatePerDraw", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal(true, property.GetValue(game));
    }

    [Fact]
    public void SetDeterministic_LeavesTheStepAloneWhenNotGiven()
    {
        using var game = new Game();

        var before = game.TargetElapsedTime;

        game.SetDeterministic();

        Assert.Equal(before, game.TargetElapsedTime);
        Assert.True(game.IsFixedTimeStep);
    }
}