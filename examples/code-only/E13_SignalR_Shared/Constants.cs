namespace E13_SignalR_Shared;

/// <summary>
/// The few values both processes have to agree on. Anything that is only true for one side lives on
/// that side.
/// </summary>
public static class Constants
{
    /// <summary>The station's name, used for window and page titles.</summary>
    public const string StationName = "Orbital Cargo Deck";

    /// <summary>
    /// Where the Blazor app hosting the hub listens. This is the IIS Express HTTPS port from the
    /// Blazor project's launchSettings.json; running the host on Kestrel instead gives it a different
    /// port, which is what <see cref="HubUrlEnvironmentVariable"/> is for.
    /// </summary>
    /// <remarks>
    /// A <see cref="Uri"/> rather than a string, which also means it cannot be <c>const</c>: a caller
    /// that compiled against a constant would have kept the literal rather than picking up a change
    /// here, and this is exactly the value someone edits when they move the server.
    /// </remarks>
    public static readonly Uri HubBaseUrl = new("https://localhost:44369");

    /// <summary>
    /// The hub's route, relative to the host. Not a URL on its own - it is mapped by <c>MapHub</c> on
    /// the server and resolved against the host on the client.
    /// </summary>
    public const string HubPath = "station";

    /// <summary>
    /// Set this to a full hub URL (for example <c>https://localhost:7167/station</c>) to point the game
    /// at a host that is not on the default port.
    /// </summary>
    public const string HubUrlEnvironmentVariable = "STATION_HUB_URL";

    /// <summary>How many containers a batch release drops.</summary>
    public const int BatchSize = 10;
}