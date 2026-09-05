using E13_SignalR.Services;
using Stride.Engine;

namespace E13_SignalR.Scripts;

/// <summary>
/// Starts the ScreenService and drains buffered hub events on the main thread every frame.
/// </summary>
public sealed class HubEventPumpScript : AsyncScript
{
    private ScreenService? _screenService;

    public HubEventPumpScript()
    {
        // Run early to make received events available to other scripts
        Priority = -100;
    }

    public override async Task Execute()
    {
        _screenService = Services.GetService<ScreenService>();

        if (_screenService is null) return;

        // Fire and forget by design: the hub is an optional feature. Awaiting the connection here
        // would stall the pump behind a network round trip on the first frame, and a hub that is
        // down would keep the game waiting for something it does not need.
        _screenService.BeginConnect();

        // Draining is all this script owns. Stopping the connection belongs to Program.cs, which
        // creates the service with `await using` and disposes it after game.Run() returns - on the
        // main thread, with the game loop already finished.
        //
        // Stopping it here instead is what froze the window on close: closing a HubConnection is a
        // network round trip, and awaiting it inside an AsyncScript posts the continuation back to
        // Stride's microthread scheduler. By then the game loop has stopped pumping that scheduler,
        // so the await never resumes and the script never completes - while shutdown waits for it.
        while (Game.IsRunning)
        {
            _screenService.DrainEvents();

            await Script.NextFrame();
        }
    }
}