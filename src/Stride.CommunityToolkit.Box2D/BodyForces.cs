using Box2D.NET;
using Stride.Core.Mathematics;
using static Box2D.NET.B2Bodies;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Force, impulse, and velocity helpers applied to bodies by id.
/// </summary>
public static class BodyForces
{
    /// <summary>
    /// Applies a linear impulse to the body's center of mass, waking it.
    /// </summary>
    /// <param name="bodyId">The target body.</param>
    /// <param name="impulse">The impulse in newton-seconds.</param>
    public static void ApplyImpulse(B2BodyId bodyId, Vector2 impulse)
    {
        var b2Impulse = new B2Vec2(impulse.X, impulse.Y);
        b2Body_ApplyLinearImpulseToCenter(bodyId, b2Impulse, true);
    }

    /// <summary>
    /// Applies a linear impulse at a world-space point, waking the body.
    /// </summary>
    /// <param name="bodyId">The target body.</param>
    /// <param name="impulse">The impulse in newton-seconds.</param>
    /// <param name="point">World-space application point.</param>
    public static void ApplyImpulseAtPoint(B2BodyId bodyId, Vector2 impulse, Vector2 point)
    {
        var b2Impulse = new B2Vec2(impulse.X, impulse.Y);
        var b2Point = new B2Vec2(point.X, point.Y);
        b2Body_ApplyLinearImpulse(bodyId, b2Impulse, b2Point, true);
    }

    /// <summary>
    /// Sets the body's linear velocity.
    /// </summary>
    /// <param name="bodyId">The target body.</param>
    /// <param name="velocity">The new velocity.</param>
    public static void SetVelocity(B2BodyId bodyId, Vector2 velocity)
    {
        var b2Velocity = new B2Vec2(velocity.X, velocity.Y);
        b2Body_SetLinearVelocity(bodyId, b2Velocity);
    }

    /// <summary>
    /// Gets the body's linear velocity.
    /// </summary>
    /// <param name="bodyId">The target body.</param>
    /// <returns>The current velocity.</returns>
    public static Vector2 GetVelocity(B2BodyId bodyId)
    {
        var velocity = b2Body_GetLinearVelocity(bodyId);
        return new Vector2(velocity.X, velocity.Y);
    }
}