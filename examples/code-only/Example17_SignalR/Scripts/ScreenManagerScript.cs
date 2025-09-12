using Example17_SignalR.Builders;
using Example17_SignalR.Core;
using Example17_SignalR.Managers;
using Example17_SignalR.Services;
using Example17_SignalR_Shared.Core;
using Example17_SignalR_Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Input;
using System.Collections.Concurrent;

namespace Example17_SignalR.Scripts;

/// <summary>
/// Orchestrates SignalR event draining, message display, robot creation throttling and removal requests.
/// </summary>
public class ScreenManagerScript : AsyncScript
{
    private readonly ConcurrentQueue<CountDto> _primitiveCreationQueue = new();
    private readonly ConcurrentQueue<CountDto> _removeRequestQueue = new();
    private RobotBuilder? _robotBuilder;
    private MaterialManager? _materialManager;
    private MessagePrinter? _messagePrinter;
    private ScreenService? _screenService;

    // Creation throttling state
    private CountDto? _currentCreationBatch;
    private int _currentCreationIndex;
    private const int MaxCreatesPerFrame = 25; // tune per hardware

    // Remove sender background loop
    private CancellationTokenSource? _removeSenderCancellationTokenSource;
    private Task? _removeSenderTask;

    public override async Task Execute()
    {
        _screenService = Services.GetService<ScreenService>();

        if (_screenService is null) return;

        try
        {
            // Do NOT use ConfigureAwait(false) in AsyncScript to preserve micro-thread context
            await _screenService.EnsureStartedAsync(CancellationToken);
            Console.WriteLine("Connection started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting connection: {ex.Message}");
        }

        _materialManager = new MaterialManager(new MaterialBuilder(Game.GraphicsDevice));
        _robotBuilder = new RobotBuilder(Game);
        _messagePrinter = new MessagePrinter(DebugText);

        var countReceiver = new EventReceiver<CountDto>(GlobalEvents.CountReceivedEventKey);
        var messageReceiver = new EventReceiver<MessageDto>(GlobalEvents.MessageReceivedEventKey);
        var removeRequestReceiver = new EventReceiver<CountDto>(GlobalEvents.RemoveRequestEventKey);

        // Start a single background loop that drains remove requests sequentially
        _removeSenderCancellationTokenSource = new CancellationTokenSource();
        _removeSenderTask = Task.Run(() => RemoveSenderLoopAsync(_removeSenderCancellationTokenSource.Token));

        try
        {
            while (Game.IsRunning)
            {
                // Drain hub events (broadcasts) on main thread to keep ordering predictable
                _screenService.DrainEvents();

                if (countReceiver.TryReceive(out var countDto))
                {
                    QueuePrimitiveCreation(countDto);
                }

                if (removeRequestReceiver.TryReceive(out var countDto2))
                {
                    QueueRemoveRequest(countDto2);
                }

                if (messageReceiver.TryReceive(out var messageDto))
                {
                    _messagePrinter.Enqueue(messageDto);
                }

                _messagePrinter.PrintMessage();

                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    QueuePrimitiveCreation(new CountDto
                    {
                        Type = EntityType.Destroyer,
                        Count = 10,
                    });
                }

                // Throttled primitive creation; spreads work across frames
                ProcessPrimitiveQueue();

                await Script.NextFrame();
            }
        }
        finally
        {
            // Stop background sender gracefully
            try
            {
                _removeSenderCancellationTokenSource?.Cancel();

                if (_removeSenderTask is not null)
                {
                    // No ConfigureAwait(false) needed; end of script
                    await _removeSenderTask;
                }

                if (_screenService is not null)
                {
                    try { await _screenService.StopAsync(); } catch { }
                }
            }
            catch
            {
                // ignore cancellation exceptions
            }
        }
    }

    private void QueuePrimitiveCreation(CountDto countDto)
        => _primitiveCreationQueue.Enqueue(countDto);

    private void QueueRemoveRequest(CountDto countDto)
        => _removeRequestQueue.Enqueue(countDto);

    private void ProcessPrimitiveQueue()
    {
        // If no active batch, try to fetch one
        if (_currentCreationBatch is null)
        {
            if (!_primitiveCreationQueue.TryDequeue(out var nextBatch) || nextBatch is null)
            {
                return;
            }

            _currentCreationBatch = nextBatch;
            _currentCreationIndex = 0;
        }

        // Process a limited number per frame
        var remaining = _currentCreationBatch.Count - _currentCreationIndex;
        var toCreate = Math.Min(MaxCreatesPerFrame, remaining);

        for (int i = 0; i < toCreate; i++)
        {
            var id = _currentCreationIndex + i;
            _robotBuilder!.CreateRobot(
                id,
                _currentCreationBatch.Type,
                Entity.Scene,
                _materialManager!.GetMaterial(_currentCreationBatch.Type)
                );
        }

        _currentCreationIndex += toCreate;

        // If finished, clear current batch so next one can be dequeued next frame
        if (_currentCreationIndex >= _currentCreationBatch.Count)
        {
            _currentCreationBatch = null;
            _currentCreationIndex = 0;
        }
    }

    // Runs on a background thread; only handles SignalR I/O, no scene graph access
    private async Task RemoveSenderLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_removeRequestQueue.TryDequeue(out var nextRemoveRequest) && nextRemoveRequest is not null)
                {
                    try
                    {
                        // Sequential send to avoid piling up many concurrent SendAsync tasks
                        await _screenService!.Connection.SendAsync("SendUnitsRemoved", nextRemoveRequest, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // shutting down
                        break;
                    }
                    catch (Exception)
                    {
                        // Swallow and continue; consider backoff/retry or enqueue back if needed
                    }

                    // Optional tiny yield to avoid tight loop starving thread pool
                    await Task.Yield();
                }
                else
                {
                    // Nothing to send; wait briefly to avoid busy spin
                    await Task.Delay(1, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal during shutdown
        }
    }
}