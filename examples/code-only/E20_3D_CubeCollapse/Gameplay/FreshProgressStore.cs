namespace CubeCollapse.Gameplay;

/// <summary>
/// The default store: every launch starts from level one, and nothing is written anywhere.
/// </summary>
public sealed class FreshProgressStore : IProgressStore
{
    /// <inheritdoc />
    public GameProgress Load() => new();

    /// <inheritdoc />
    public void Save(GameProgress progress)
    {
        // Deliberately nothing: this store is the decision not to persist
    }
}