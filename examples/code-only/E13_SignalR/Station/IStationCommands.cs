using E13_SignalR_Shared;

namespace E13_SignalR.Station;

/// <summary>
/// What the web console can make the game do. <see cref="Net.StationLink"/> queues these as they
/// arrive on SignalR's threads and replays them on the game thread, which is the only thread that
/// may touch the deck.
/// </summary>
public interface IStationCommands
{
    void Release(ReleaseRequest request);

    void ReleaseBatch(int count);

    void Clear();

    void Shake();

    void SetScheme(string name);

    void Hail(string text);
}