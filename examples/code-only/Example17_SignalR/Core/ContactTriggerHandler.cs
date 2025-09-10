using Example17_SignalR_Shared.Core;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Contacts;
using Stride.Engine;
using System.Collections.Concurrent;

namespace Example17_SignalR.Core;

public class ContactTriggerHandler : IContactEventHandler
{
    // Thread-safe removal queue. Enqueued from physics threads, processed on main thread.
    public static readonly ConcurrentQueue<Entity> RemovalQueue = new();

    public bool NoContactResponse => false;

    void IContactEventHandler.OnStartedTouching<TManifold>(CollidableComponent eventSource, CollidableComponent other,
        ref TManifold contactManifold,
        bool flippedManifold,
        int workerIndex,
        BepuSimulation bepuSimulation)
    {
        if (eventSource?.Entity == null || other?.Entity == null)
            return;

        var sourceRobot = eventSource.Entity.Get<RobotComponent>();
        var otherRobot = other.Entity.Get<RobotComponent>();

        if (sourceRobot is null || otherRobot is null) return;

        if (sourceRobot.IsDeleted || otherRobot.IsDeleted) return;

        if (sourceRobot.Type == EntityType.Destroyer && otherRobot.Type == EntityType.Destroyer) return;

        if (sourceRobot.Type == EntityType.Destroyer || otherRobot.Type == EntityType.Destroyer)
        {
            MarkForRemoval(sourceRobot);
            MarkForRemoval(otherRobot);
        }

        //Console.WriteLine($"Started touching: {eventSource.Entity.Name} - {other.Entity.Name}");
    }

    public static void MarkForRemoval(RobotComponent? robotComponent)
    {
        if (robotComponent is null) return;

        robotComponent.IsDeleted = true;

        var entity = robotComponent.Entity;

        if (entity != null)
        {
            RemovalQueue.Enqueue(entity);
        }

        //robotComponent.Entity.Remove<BodyComponent>();

        // Can we broadcast removal process, which should queue the removal request?
        // ToDo: Can we use rather this?
        //robotComponent.Entity.Scene = null;

    }
}