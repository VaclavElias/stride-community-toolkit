using E13_SignalR_Shared;

namespace E13_SignalR.Station;

/// <summary>
/// The web console's commands, applied to the deck and the in-game console. Deliberately the same
/// calls the keyboard makes, so the two consoles cannot drift apart - the only difference is the
/// origin stamped on the container.
/// </summary>
public sealed class StationCommands(Deck deck, StationConsole console) : IStationCommands
{
    public void Release(ReleaseRequest request) => deck.Release(request, CommandOrigin.Web);

    public void ReleaseBatch(int count) => deck.ReleaseBatch(Math.Clamp(count, 1, 50), CommandOrigin.Web);

    public void Clear() => deck.Clear();

    public void Shake() => deck.Shake();

    public void SetScheme(string name) => console.Select(name);

    public void Hail(string text) => console.ShowHail(text);
}