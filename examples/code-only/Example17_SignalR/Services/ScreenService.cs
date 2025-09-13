using Example17_SignalR.Core;
using Example17_SignalR_Shared.Dtos;
using Example17_SignalR_Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;

namespace Example17_SignalR.Services;

/// <summary>
/// Encapsulates SignalR connection, event buffering and main-thread dispatch via <see cref="GlobalEvents"/>.
/// Also owns a background loop that sequentially forwards removal requests to the hub.
/// </summary>
public class ScreenService
{
    private readonly ConcurrentQueue<MessageDto> _pendingMessages = new();
    private readonly ConcurrentQueue<CountDto> _pendingCounts = new();
    private readonly ConcurrentQueue<CountDto> _pendingRemovals = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Random _random = new();
    private volatile bool _reconnecting;

    private CancellationTokenSource? _removeSenderCancellationTokenSource;
    private Task? _removeSenderTask;

    /// <summary>
    /// Active SignalR hub connection.
    /// </summary>
    public HubConnection Connection { get; }

    public ScreenService()
    {
        Connection = new HubConnectionBuilder()
              .WithUrl("https://localhost:44369/screen1")
              .Build();

        // Only enqueue inside callback (keep it very small, no engine interaction / no broadcasts here)
        Connection.On<MessageDto>(nameof(IScreenClient.ReceiveMessageAsync), (dto) =>
        {
            if (dto is null) return;

            _pendingMessages.Enqueue(dto);
        });

        Connection.On<CountDto>(nameof(IScreenClient.ReceiveCountAsync), (dto) =>
        {
            if (dto is null) return;

            _pendingCounts.Enqueue(dto);
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
    /// Enqueue a units-removed message to be sent by the background sender.
    /// </summary>
    public void EnqueueUnitsRemoved(CountDto dto)
        => _pendingRemovals.Enqueue(dto);

    /// <summary>
    /// Starts the connection if not already started. Guarded to avoid re-entrancy deadlocks.
    /// Also ensures the background remove-sender loop is running.
    /// </summary>
    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        // Quick exit without lock if already running
        if (Connection.State == HubConnectionState.Connected)
        {
            EnsureRemoveSenderLoop();

            return;
        }

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (Connection.State == HubConnectionState.Disconnected)
            {
                await Connection.StartAsync(ct).ConfigureAwait(false);
            }

            EnsureRemoveSenderLoop();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Stops the SignalR connection and background sender.
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            StopRemoveSenderLoop();

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

    private void EnsureRemoveSenderLoop()
    {
        if (_removeSenderTask is { IsCompleted: false })
        {
            return;
        }

        _removeSenderCancellationTokenSource?.Cancel();
        _removeSenderCancellationTokenSource?.Dispose();

        _removeSenderCancellationTokenSource = new CancellationTokenSource();
        _removeSenderTask = Task.Run(() => RemoveSenderLoopAsync(_removeSenderCancellationTokenSource.Token));
    }

    private void StopRemoveSenderLoop()
    {
        try
        {
            _removeSenderCancellationTokenSource?.Cancel();
            _removeSenderTask?.Wait(250);
        }
        catch
        {
            // ignore
        }
        finally
        {
            _removeSenderCancellationTokenSource?.Dispose();
            _removeSenderCancellationTokenSource = null;
            _removeSenderTask = null;
        }
    }

    // Runs on a background thread; only handles SignalR I/O, no scene graph access
    private async Task RemoveSenderLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_pendingRemovals.TryDequeue(out var nextRemoveRequest) && nextRemoveRequest is not null)
                {
                    try
                    {
                        // Sequential send to avoid piling up many concurrent SendAsync tasks
                        await Connection.SendAsync("SendUnitsRemoved", nextRemoveRequest, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // shutting down
                        break;
                    }
                    catch
                    {
                        // Swallow and continue; consider backoff/retry or enqueue back if needed
                    }

                    // Optional tiny yield to avoid tight loop starving thread pool
                    await Task.Yield();
                }
                else
                {
                    // Nothing to send; wait briefly to avoid busy spin
                    await Task.Delay(1, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal during shutdown
        }
    }
}