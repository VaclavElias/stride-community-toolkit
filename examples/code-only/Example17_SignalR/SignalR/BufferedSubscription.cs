using System.Collections.Concurrent;

namespace Example17_SignalR.SignalR;

public readonly struct BufferedSubscription<T>(ConcurrentQueue<T> queue, IDisposable subscription)
{
    public bool TryDequeue(out T item) => queue.TryDequeue(out item!);
}