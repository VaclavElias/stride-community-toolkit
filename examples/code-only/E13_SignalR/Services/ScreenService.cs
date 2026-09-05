using E13_SignalR.Core;
using E13_SignalR.SignalR;
using E13_SignalR_Shared.Dtos;
using E13_SignalR_Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace E13_SignalR.Services;

/// <summary>
/// Encapsulates SignalR connection, event buffering and main-thread dispatch via <see cref="GlobalEvents"/>.
/// Also owns a background loop that sequentially forwards removal requests to the hub.
/// </summary>
public sealed class ScreenService : IAsyncDisposable
{
    private readonly SignalRHubClient _client;

    private readonly BufferedSubscription<MessageDto> _messages;
    private readonly BufferedSubscription<CountDto> _counts;
    private readonly OutgoingQueue<CountDto> _removals;

    /// <summary>
    /// Active SignalR hub connection.
    /// </summary>
    public HubConnection Connection => _client.Connection;

    public ScreenService(Uri hubUrl, Microsoft.Extensions.Logging.ILogger<SignalRHubClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(hubUrl);

        _client = new SignalRHubClient(new SignalRClientOptions()
        {
            HubUrl = hubUrl
        }, logger);

        // Only enqueue inside callback (keep it very small, no engine interaction / no broadcasts here)
        _messages = _client.RegisterBuffered<MessageDto>(nameof(IScreenClient.ReceiveMessageAsync));
        _counts = _client.RegisterBuffered<CountDto>(nameof(IScreenClient.ReceiveCountAsync));

        // Background sequential sender for units removed
        _removals = _client.CreateOutgoingQueue<CountDto>("SendUnitsRemoved");
    }

    /// <summary>
    /// Drains queued hub events and broadcasts them on the (game) thread calling this method.
    /// Call from the main update loop before EventReceivers.TryReceive.
    /// </summary>
    public void DrainEvents()
    {
        while (_messages.TryDequeue(out var msg))
        {
            GlobalEvents.MessageReceivedEventKey.Broadcast(msg);
        }

        while (_counts.TryDequeue(out var cnt))
        {
            GlobalEvents.CountReceivedEventKey.Broadcast(cnt);
        }
    }

    /// <summary>
    /// Enqueue a units-removed message to be sent by the background sender.
    /// </summary>
    public void EnqueueUnitsRemoved(CountDto dto) => _removals.Enqueue(dto);

    /// <summary>
    /// Starts the connection if not already started.
    /// </summary>
    public Task EnsureStartedAsync(CancellationToken ct = default) => _client.EnsureStartedAsync(ct);

    /// <summary>
    /// Stops the SignalR connection and background sender, leaving the service reusable.
    /// </summary>
    public Task StopAsync(CancellationToken ct = default) => _client.StopAsync(ct);

    /// <summary>
    /// Releases the connection and the background sender for good.
    /// </summary>
    /// <remarks>
    /// <see cref="IAsyncDisposable"/> rather than <see cref="IDisposable"/>, because the thing being
    /// released is a <see cref="HubConnection"/> and closing one is a round trip to the server.
    /// Disposing the queue here is belt and braces - the client stops every queue it handed out
    /// before it disposes the connection - but it makes the ownership explicit, and Stop is safe to
    /// call twice.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        _removals.Dispose();

        await _client.DisposeAsync().ConfigureAwait(false);
    }
}