namespace E13_SignalR.Station;

/// <summary>One line of the feed.</summary>
public sealed record LogEntry(string Text, LogKind Kind);