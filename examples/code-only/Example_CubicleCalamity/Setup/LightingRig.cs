using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering.Colors;
using Stride.Rendering.Lights;

namespace Example_CubicleCalamity.Setup;

/// <summary>
/// Lights the platform from every direction.
/// </summary>
/// <remarks>
/// A single directional light leaves the faces pointing away from it black, which on a board where
/// colour is the thing being matched would make cubes on the shaded side unplayable. Five lights on
/// opposing axes flatten that out. It is not physically motivated - it is the lighting equivalent of
/// the flat-shading material, chosen so every cube reads as its own colour from any camera angle.
/// </remarks>
public static class LightingRig
{
    /// <summary>
    /// Adds the full set of directional lights to the scene.
    /// </summary>
    /// <param name="game">The running game, used for the light gizmos' graphics device.</param>
    /// <param name="scene">The scene the lights are added to.</param>
    /// <param name="intensity">Intensity given to each light.</param>
    /// <param name="showLightGizmo">Whether to draw a gizmo showing each light's direction.</param>
    public static void Add(Game game, Scene scene, float intensity, bool showLightGizmo = true)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(scene);

        var position = new Vector3(7f, 2f, 0);

        AddLight(position, rotation: null);
        AddLight(position, Quaternion.RotationAxis(Vector3.UnitX, MathUtil.DegreesToRadians(180)));
        AddLight(position, Quaternion.RotationAxis(Vector3.UnitX, MathUtil.DegreesToRadians(270)));
        AddLight(position, Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(90)));
        AddLight(position, Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(270)));

        void AddLight(Vector3 lightPosition, Quaternion? rotation)
        {
            var entity = new Entity
            {
                new LightComponent
                {
                    Intensity = intensity,
                    Type = new LightDirectional { Color = new ColorRgbProvider(Color.White) }
                }
            };

            entity.Transform.Position = lightPosition;
            entity.Transform.Rotation = rotation ?? Quaternion.Identity;
            entity.Scene = scene;

            if (showLightGizmo)
            {
                entity.AddLightDirectionalGizmo(game.GraphicsDevice);
            }
        }
    }
}