namespace Example17_SignalR_Shared.Core;

public static class Constants
{
    /// <summary>
    /// Where the Blazor app hosting the hub listens.
    /// </summary>
    /// <remarks>
    /// A <see cref="Uri"/> rather than a string, which also means it cannot be <c>const</c>: a caller
    /// that compiled against the old constant would have kept the literal rather than picking up a
    /// change here, and this is exactly the value someone edits when they move the server.
    /// </remarks>
    public static readonly Uri HubBaseUrl = new("https://localhost:44304");

    /// <summary>
    /// The hub's route, relative to the host. Not a URL on its own - it is mapped by
    /// <c>MapHub</c> on the server and resolved against the host on the client.
    /// </summary>
    public const string HubPath = "screen1";

    public const string HubName = "Screen1Hub";

    public const int DefaultEntitiesCount = 10;
    public const string DefaultMessage = "Hello";
}
