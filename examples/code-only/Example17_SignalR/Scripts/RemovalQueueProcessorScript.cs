using Example17_SignalR.Core;
using Example17_SignalR_Shared.Core;
using Example17_SignalR_Shared.Dtos;
using Stride.Engine;

namespace Example17_SignalR.Scripts;

public class RemovalQueueProcessorScript : AsyncScript
{
    public override async Task Execute()
    {
        while (Game.IsRunning)
        {
            // Drain the queue on the main thread
            while (ContactTriggerHandler.RemovalQueue.TryDequeue(out var entity))
            {
                if (entity == null) continue;

                // Do any main-thread-only work here:
                // - broadcast counts / SignalR messages
                // - remove physics-related components if needed
                // - finally remove entity from scene

                BroadcastEntityRemovalRequest(entity);

                if (entity.Scene != null)
                {
                    entity.Scene = null;
                }
            }

            await Script.NextFrame();
        }
    }

    private void BroadcastEntityRemovalRequest(Entity entity)
    {
        var robotComponent = entity.Get<RobotComponent>();

        if (robotComponent is null) return;

        if (robotComponent.Type == EntityType.Destroyer)
        {
            return;
        }

        var message = new CountDto(robotComponent.Type, 1);

        GlobalEvents.RemoveRequestEventKey.Broadcast(message);
    }
}