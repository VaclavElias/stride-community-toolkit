using Microsoft.AspNetCore.SignalR;

namespace E13_SignalR_Blazor.Hubs;

/// <summary>
/// A relay and nothing more. Every call is forwarded to every other client: a command from a page
/// reaches the game (and the other pages), a report from the game reaches the pages. The hub keeps
/// no state - a page that opens late is brought up to date by the game's next census.
/// </summary>
/// <remarks>
/// <c>Clients.Others</c> rather than <c>Clients.All</c>, so nothing echoes back to its sender: a
/// page that asks for a scheme hears about the change from the game, once, like every other page.
/// </remarks>
public class StationHub : Hub<IStationClient>, IStationHub
{
    // Commands, from either console

    public Task ReleaseContainer(ReleaseRequest request) => Clients.Others.ReleaseRequested(request);

    public Task ReleaseBatch(int count) => Clients.Others.BatchRequested(count);

    public Task ClearDeck() => Clients.Others.ClearRequested();

    public Task ShakeDeck() => Clients.Others.ShakeRequested();

    public Task SetScheme(string name) => Clients.Others.SchemeRequested(name);

    public Task Hail(string text) => Clients.Others.HailReceived(text);

    // Reports, from the game

    public Task ReportReleased(ContainerEvent container) => Clients.Others.ContainerReleased(container);

    public Task ReportLanded(ContainerEvent container) => Clients.Others.ContainerLanded(container);

    public Task ReportLost(ContainerEvent container) => Clients.Others.ContainerLost(container);

    public Task ReportCleared(int removed) => Clients.Others.DeckCleared(removed);

    public Task ReportScheme(string name) => Clients.Others.SchemeChanged(name);

    public Task ReportDeck(DeckSnapshot snapshot) => Clients.Others.DeckUpdated(snapshot);
}