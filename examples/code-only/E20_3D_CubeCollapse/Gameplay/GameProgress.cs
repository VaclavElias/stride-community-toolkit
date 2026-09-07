namespace CubeCollapse.Gameplay;

/// <summary>
/// What survives between launches: for now, only how far the player has climbed.
/// </summary>
/// <remarks>
/// Deliberately a dumb data bag, so it can be serialized as-is when persistence is wanted. Add to
/// it (best score, chosen palette) rather than storing such things loose.
/// </remarks>
public sealed class GameProgress
{
    /// <summary>Gets or sets the level to play, counting from 1.</summary>
    public int Level { get; set; } = 1;
}