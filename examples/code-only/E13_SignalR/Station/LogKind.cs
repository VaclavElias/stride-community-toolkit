namespace E13_SignalR.Station;

/// <summary>What a feed line is about, which is what colours its tick on the feed board.</summary>
public enum LogKind
{
    Info,
    Released,
    Landed,
    Lost,
    Cleared,
    Hail,
}