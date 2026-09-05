using E13_SignalR.Core;
using E13_SignalR.Services;
using E13_SignalR_Shared.Dtos;
using Stride.Engine;
using Stride.Engine.Events;

namespace E13_SignalR.Scripts;

/// <summary>
/// Relays remove requests to the hub via ScreenService. No scene graph access here.
/// </summary>
public sealed class RemoveRelayScript : AsyncScript
{
    private ScreenService? _screenService;

    public override async Task Execute()
    {
        _screenService = Services.GetService<ScreenService>();

        if (_screenService is null) return;

        using var removeRequestReceiver = new EventReceiver<CountDto>(GlobalEvents.RemoveRequestEventKey);

        while (Game.IsRunning)
        {
            if (removeRequestReceiver.TryReceive(out var removeDto))
            {
                _screenService.EnqueueUnitsRemoved(removeDto);
            }

            await Script.NextFrame();
        }
    }
}