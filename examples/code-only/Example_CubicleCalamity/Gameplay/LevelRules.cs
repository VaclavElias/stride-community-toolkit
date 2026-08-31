using Example_CubicleCalamity.Shared;

namespace Example_CubicleCalamity.Gameplay;

/// <summary>
/// How the board grows from level to level. Pure arithmetic, testable without a running game.
/// </summary>
public static class LevelRules
{
    /// <summary>Cubes along each side of the first level's board.</summary>
    public const int StartingSize = 5;

    /// <summary>
    /// Returns the board for a level: 5x5x5 at level one, one cube larger per side each level,
    /// capped at the full board once it gets there.
    /// </summary>
    /// <param name="number">The level, counting from 1. Values below 1 are treated as 1.</param>
    /// <returns>The level's board definition.</returns>
    /// <remarks>
    /// The cap matters twice over: <see cref="GameSettings.Rows"/> is the size the physics solver
    /// settings were tuned against, and <see cref="CubeGrid"/> scans columns up to
    /// <see cref="GameSettings.MaxLayers"/> when it collapses them. Levels past the cap keep the
    /// full board and rely on the score carrying over to stay worth playing.
    /// </remarks>
    public static LevelDefinition ForNumber(int number)
    {
        var level = Math.Max(1, number);
        var size = Math.Min(StartingSize + level - 1, GameSettings.Rows);

        return new LevelDefinition(level, size, Math.Min(size, GameSettings.MaxLayers));
    }
}