namespace E13_SignalR_Shared;

/// <summary>
/// What a client can call on the hub. The Blazor hub implements it; the game and the page send with
/// <c>nameof(IStationHub.Method)</c>, so a rename breaks the build on every side at once instead of
/// failing quietly at runtime with a method the hub no longer has.
/// </summary>
/// <remarks>
/// Two groups. <b>Commands</b> come from either console and are relayed to everyone else; the game
/// acts on them, other browser tabs ignore what is not theirs. <b>Reports</b> come from the game
/// and are relayed to the browsers.
/// </remarks>
public interface IStationHub
{
    // Commands - from either console

    Task ReleaseContainer(ReleaseRequest request);

    Task ReleaseBatch(int count);

    Task ClearDeck();

    Task ShakeDeck();

    Task SetScheme(string name);

    /// <summary>A line of text for the game's console. The one free-text channel.</summary>
    Task Hail(string text);

    // Reports - from the game

    Task ReportReleased(ContainerEvent container);

    Task ReportLanded(ContainerEvent container);

    Task ReportLost(ContainerEvent container);

    Task ReportCleared(int removed);

    Task ReportScheme(string name);

    Task ReportDeck(DeckSnapshot snapshot);
}