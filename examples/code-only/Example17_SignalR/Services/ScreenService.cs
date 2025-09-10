using Example17_SignalR.Core;
using Example17_SignalR_Shared.Dtos;
using Example17_SignalR_Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Threading;

namespace Example17_SignalR.Services;

public class ScreenService
{
    private readonly ConcurrentQueue<MessageDto> _pendingMessages = new();
    private readonly ConcurrentQueue<CountDto> _pendingCounts = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Random _random = new();
    private volatile bool _reconnecting;

    public HubConnection Connection { get; set; }

    public ScreenService()
    {
        Connection = new HubConnectionBuilder()
              .WithUrl("https://localhost:44369/screen1")
              .Build();

        // Only enqueue inside callback (keep it very small, no engine interaction / no broadcasts here)
        Connection.On<MessageDto>(nameof(IScreenClient.ReceiveMessageAsync), (dto) =>
        {
            if (dto != null)
            {
                _pendingMessages.Enqueue(dto);
            }
        });

        Connection.On<CountDto>(nameof(IScreenClient.ReceiveCountAsync), (dto) =>
        {
            if (dto != null)
            {
                _pendingCounts.Enqueue(dto);
            }
        });

        Connection.Closed += async (error) =>
        {
            // Prevent overlapping reconnect attempts
            if (_reconnecting) return;
            _reconnecting = true;
            try
            {
                // Jittered backoff
                await Task.Delay(_random.Next(500, 2000)).ConfigureAwait(false);
                await EnsureStartedAsync().ConfigureAwait(false);
            }
            catch
            {
                // swallow – further reconnect attempts will happen on future Closed events
            }
            finally
            {
                _reconnecting = false;
            }
        };
    }

    /// <summary>
    /// Drains queued hub events and broadcasts them on the (game) thread calling this method.
    /// Call from the main update loop before EventReceivers.TryReceive.
    /// </summary>
    public void DrainEvents()
    {
        while (_pendingMessages.TryDequeue(out var msg))
        {
            GlobalEvents.MessageReceivedEventKey.Broadcast(msg);
        }

        while (_pendingCounts.TryDequeue(out var cnt))
        {
            GlobalEvents.CountReceivedEventKey.Broadcast(cnt);
        }
    }

    /// <summary>
    /// Starts the connection if not already started. Guarded to avoid re-entrancy deadlocks.
    /// </summary>
    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        // Quick exit without lock if already running
        if (Connection.State == HubConnectionState.Connected) return;

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Connection.State == HubConnectionState.Disconnected)
            {
                await Connection.StartAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Connection.State != HubConnectionState.Disconnected)
            {
                await Connection.StopAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}