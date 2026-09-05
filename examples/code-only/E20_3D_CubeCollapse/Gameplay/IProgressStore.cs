namespace CubeCollapse.Gameplay;

/// <summary>
/// Where <see cref="GameProgress"/> comes from at launch and goes on level change.
/// </summary>
/// <remarks>
/// The game only ever talks to this interface, so switching from fresh-every-launch to a saved file
/// is a one-line change where the store is constructed - see <see cref="JsonProgressStore"/>.
/// </remarks>
public interface IProgressStore
{
    /// <summary>Returns the progress to start from.</summary>
    GameProgress Load();

    /// <summary>Records the given progress.</summary>
    void Save(GameProgress progress);
}