namespace E13_SignalR.Core;

public static class GameSettings
{
    /// <summary>Where the Blazor app hosting the hub listens.</summary>
    public static Uri HubBaseUrl { get; set; } = new("https://localhost:44369");

    /// <summary>
    /// The hub's route, relative to <see cref="HubBaseUrl"/>. Not a URL on its own.
    /// </summary>
    public static string HubPath { get; set; } = "screen1";
}
