using Stride.BepuPhysics;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Physics;

namespace Stride.CommunityToolkit.Bepu;

/// <summary>
/// Provides a set of static methods for working with <see cref="CameraComponent"/> instances.
/// </summary>
/// <remarks>
/// This class includes extension methods for performing various operations with <see cref="CameraComponent"/> instances,
/// such as raycasting, converting screen positions to world positions, and more. These methods are useful for implementing
/// features like object picking, camera control, and coordinate transformations in a 3D environment.
/// </remarks>
public static class CameraComponentExtensions
{
    /// <summary>
    /// Performs a raycasting operation from the specified <see cref="CameraComponent"/>'s position through a specified screen position,
    /// using the provided <see cref="BepuSimulation"/>, and returns information about the hit result.
    /// </summary>
    /// <param name="camera">The <see cref="CameraComponent"/> from which the ray should be cast.</param>
    /// <param name="simulation">The <see cref="BepuSimulation"/> used to perform the raycasting operation.</param>
    /// <param name="screenPosition">The screen position in screen coordinates (e.g., mouse position) from which the ray should be cast.</param>
    /// <param name="collisionGroups">Optional. The collision filter group to consider during the raycasting. Default is <see cref="CollisionFilterGroups.DefaultFilter"/>.</param>
    /// <param name="collisionFilterGroupFlags">Optional. The collision filter group flags to consider during the raycasting. Default is <see cref="CollisionFilterGroupFlags.DefaultFilter"/>.</param>
    /// <returns>A <see cref="HitResult"/> containing information about the hit result, including the hit location and other collision data.</returns>
    public static HitInfo RaycastMouse(this CameraComponent camera, BepuSimulation simulation, Vector2 screenPosition, CollisionMask collisionMask = CollisionMask.Everything)
    {
        return camera.Raycast(simulation, screenPosition, collisionMask);
    }

    /// <summary>
    /// Performs a raycasting operation from the specified <see cref="CameraComponent"/>'s position through the mouse cursor position in screen coordinates,
    /// using input from the specified <see cref="Entity"/>, and returns information about the hit result.
    /// </summary>
    /// <param name="camera">The <see cref="CameraComponent"/> from which the ray should be cast.</param>
    /// <param name="component">The <see cref="ScriptComponent"/> from which the mouse position should be taken.</param>
    /// <param name="collisionGroups">Optional. The collision filter group to consider during the raycasting. Default is <see cref="CollisionFilterGroups.DefaultFilter"/>.</param>
    /// <param name="collisionFilterGroupFlags">Optional. The collision filter group flags to consider during the raycasting. Default is <see cref="CollisionFilterGroupFlags.DefaultFilter"/>.</param>
    /// <returns>A <see cref="HitResult"/> containing information about the hit result, including the hit location and other collision data.</returns>
    public static HitInfo RaycastMouse(this CameraComponent camera, Entity entity, ScriptComponent component, CollisionMask collisionMask = CollisionMask.Everything)
    {
        return Raycast(camera, entity, component.Input.MousePosition, collisionMask);
    }

    /// <summary>
    /// Performs a raycasting operation from the specified <see cref="CameraComponent"/>'s position through the specified screen position in world coordinates,
    /// using the <see cref="BepuSimulation"/> from the specified <see cref="Entity"/>, and returns information about the hit result.
    /// </summary>
    /// <param name="camera">The <see cref="CameraComponent"/> from which the ray should be cast.</param>
    /// <param name="entity">The <see cref="Entity"/> that contains the <see cref="BepuSimulation"/> used for raycasting.</param>
    /// <param name="screenPosition">The screen position in world coordinates through which the ray should be cast.</param>
    /// <param name="collisionGroups">Optional. The collision filter group to consider during the raycasting. Default is <see cref="CollisionFilterGroups.DefaultFilter"/>.</param>
    /// <param name="collisionFilterGroupFlags">Optional. The collision filter group flags to consider during the raycasting. Default is <see cref="CollisionFilterGroupFlags.DefaultFilter"/>.</param>
    /// <returns>A <see cref="HitResult"/> containing information about the hit result, including the hit location and other collision data.</returns>
    public static HitInfo Raycast(this CameraComponent camera, Entity entity, Vector2 screenPosition, CollisionMask collisionMask = CollisionMask.Everything)
        => Raycast(camera, entity.GetSimulation(), screenPosition, collisionMask);

    /// <summary>
    /// Performs a raycasting operation from the specified <see cref="CameraComponent"/>'s position through a specified screen position,
    /// using the provided <see cref="BepuSimulation"/>, and returns information about the hit result.
    /// </summary>
    /// <param name="camera">The <see cref="CameraComponent"/> from which the ray should be cast.</param>
    /// <param name="simulation">The <see cref="BepuSimulation"/> used to perform the raycasting operation.</param>
    /// <param name="screenPosition">The screen position in normalized screen coordinates (e.g., mouse position) where the ray should be cast.</param>
    /// <param name="collisionGroups">Optional. The collision filter group to consider during the raycasting. Default is <see cref="CollisionFilterGroups.DefaultFilter"/>.</param>
    /// <param name="collisionFilterGroupFlags">Optional. The collision filter group flags to consider during the raycasting. Default is <see cref="CollisionFilterGroupFlags.DefaultFilter"/>.</param>
    /// <returns>A <see cref="HitResult"/> containing information about the raycasting hit, including the hit location and other collision data.</returns>
    /// <remarks>
    /// This method is useful for implementing features like object picking, where you want to select or interact with objects in the 3D world based on screen coordinates.
    /// </remarks>///
    public static HitInfo Raycast(this CameraComponent camera, BepuSimulation simulation, Vector2 screenPosition, CollisionMask collisionMask = CollisionMask.Everything)
    {
        var (nearPoint, farPoint) = camera.CalculateRayFromScreenPosition(screenPosition);

        // Perform the raycast from the near point to the far point and return the result
        var result = simulation.RayCast(nearPoint, farPoint, 100, out HitInfo hitResult, collisionMask);

        return result ? hitResult : default;
    }
}