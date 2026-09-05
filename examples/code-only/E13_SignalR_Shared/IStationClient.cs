namespace E13_SignalR_Shared;

/// <summary>
/// What the hub pushes to clients. The hub is <c>Hub&lt;IStationClient&gt;</c>, so it can only push
/// these; the game and the page register handlers with <c>nameof(IStationClient.Method)</c>.
/// Each method mirrors one on <see cref="IStationHub"/>: a command arrives as a request, a report
/// arrives as a past-tense event.
/// </summary>
public interface IStationClient
{
    // Requests - the game acts on these; browser tabs only need SchemeRequested

    Task ReleaseRequested(ReleaseRequest request);

    Task BatchRequested(int count);

    Task ClearRequested();

    Task ShakeRequested();

    Task SchemeRequested(string name);

    Task HailReceived(string text);

    // Events - the browsers show these

    Task ContainerReleased(ContainerEvent container);

    Task ContainerLanded(ContainerEvent container);

    Task ContainerLost(ContainerEvent container);

    Task DeckCleared(int removed);

    Task SchemeChanged(string name);

    Task DeckUpdated(DeckSnapshot snapshot);
}