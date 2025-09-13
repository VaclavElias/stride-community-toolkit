using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;

namespace Example17_SignalR.Services;

/// <summary>
/// Reusable SignalR hub client that encapsulates connection lifecycle, reconnection,
/// buffered receivers, and background queued senders.
/// Keeps SignalR concerns isolated from engine/game threading concerns.
/// </summary>
public sealed class SignalRHubClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Random _random = new();
    private volatile bool _reconnecting;

    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<IStoppable> _sendQueues = new();

    public HubConnection Connection { get; }

    public SignalRHubClient(string hubUrl, Action<HubConnectionBuilder>? configureBuilder = null)
    {
        var builder = new HubConnectionBuilder().WithUrl(hubUrl);
        configureBuilder?.Invoke((HubConnectionBuilder)builder);
        Connection = builder.Build();

        Connection.Closed += async (error) =>
        {
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
    /// Starts the connection if currently disconnected.
    /// </summary>
    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        if (Connection.State == HubConnectionState.Connected)
            return;

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

    /// <summary>
    /// Stops the connection and any background send queues.
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            foreach (var q in _sendQueues)
                q.Stop();

            if (Connection.State != HubConnectionState.Disconnected)
            {
                await Connection.StopAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);

        foreach (var s in _subscriptions)
        {
            s.Dispose();
        }

        _subscriptions.Clear();

        await Connection.DisposeAsync();
    }

    /// <summary>
    /// Registers a simple pass-through receiver for a hub method.
    /// </summary>
    public IDisposable RegisterHandler<T>(string methodName, Action<T> handler)
    {
        var sub = Connection.On<T>(methodName, (payload) =>
        {
            if (payload is null) return;

            handler(payload);
        });

        _subscriptions.Add(sub);

        return sub;
    }

    /// <summary>
    /// Registers a buffered receiver for the given hub method that enqueues values for later draining on caller's thread.
    /// </summary>
    public BufferedSubscription<T> RegisterBuffered<T>(string methodName)
    {
        var queue = new ConcurrentQueue<T>();
        var sub = Connection.On<T>(methodName, (payload) =>
        {
            if (payload is null) return;
            queue.Enqueue(payload);
        });

        _subscriptions.Add(sub);

        return new BufferedSubscription<T>(queue, sub);
    }

    /// <summary>
    /// Creates a background queued sender for the specified hub method name.
    /// Call <see cref="OutgoingQueue{T}.Enqueue"/> to schedule items for sending.
    /// </summary>
    public OutgoingQueue<T> CreateOutgoingQueue<T>(string methodName)
    {
        var q = new OutgoingQueue<T>(this, methodName);

        _sendQueues.Add(q);

        return q;
    }

    public readonly struct BufferedSubscription<T>(ConcurrentQueue<T> queue, IDisposable subscription)
    {
        public bool TryDequeue(out T item) => queue.TryDequeue(out item!);
    }

    public sealed class OutgoingQueue<T> : IStoppable
    {
        private readonly SignalRHubClient _owner;
        private readonly string _methodName;
        private readonly ConcurrentQueue<T> _queue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loopTask;

        internal OutgoingQueue(SignalRHubClient owner, string methodName)
        {
            _owner = owner;
            _methodName = methodName;
            _loopTask = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Enqueue(T item) => _queue.Enqueue(item);

        public void Stop()
        {
            try
            {
                _cts.Cancel();
                _loopTask.Wait(250);
            }
            catch { }
            finally
            {
                _cts.Dispose();
            }
        }

        private async Task LoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_queue.TryDequeue(out var next) && next is not null)
                    {
                        try
                        {
                            await _owner.Connection.SendAsync(_methodName, next, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch
                        {
                            // Ignore and continue; consider backoff/retry if needed
                        }
                        await Task.Yield();
                    }
                    else
                    {
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

    private interface IStoppable { void Stop(); }
}