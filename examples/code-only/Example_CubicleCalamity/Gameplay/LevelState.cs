namespace Example_CubicleCalamity.Gameplay;

/// <summary>
/// The one shared answer to "which board are we on?" - everything that depends on the current
/// level's dimensions reads it from here.
/// </summary>
/// <remarks>
/// A plain mutable holder rather than a value passed around, because the level changes at runtime
/// and the spawner, the click script and the scoreboard must all agree at the moment it does.
/// </remarks>
public sealed class LevelState
{
    /// <summary>Gets or sets the level currently being played.</summary>
    public LevelDefinition Current { get; set; } = LevelRules.ForNumber(1);
}