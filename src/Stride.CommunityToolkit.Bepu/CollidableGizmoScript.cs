using Stride.BepuPhysics;
using Stride.BepuPhysics.Systems;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Rendering.Materials;

namespace Stride.CommunityToolkit.Bepu;

/// <summary>
/// Script that toggles collidable gizmos for all <see cref="CollidableComponent"/> in the scene when a specified key is pressed.
/// </summary>
public class CollidableGizmoScript : SyncScript
{
    private const string GizmoEntityName = "CollidableGizmo";

    /// <summary>
    /// The key to toggle the collidable gizmos on and off.
    /// </summary>
    public Keys Key { get; set; } = Keys.P;

    /// <summary>
    /// Optional color to apply to the gizmos. If <see langword="null"/>, the default color is used.
    /// </summary>
    public Color4? Color { get; set; }

    private bool _gizmosActive;
    private readonly List<CollidableGizmo> _gizmos = [];

    /// <summary>
    /// Called once per frame by the engine. This method checks if the specified key is pressed to toggle the collidable gizmos.
    /// </summary>
    public override void Update()
    {
        if (Input.IsKeyPressed(Key))
        {
            if (!_gizmosActive)
            {
                CreateGizmos();

                ApplyGizmoEmissiveColor();
            }
            else
            {
                RemoveGizmos();
            }

            _gizmosActive = !_gizmosActive;
        }

        if (!_gizmosActive) return;

        foreach (var gizmo in _gizmos)
        {
            gizmo.Update();
        }
    }

    private void ApplyGizmoEmissiveColor()
    {
        if (Color is null) return;

        var gizmoEntities = Entity.Scene.Entities.Where(e => e.Name.Contains(GizmoEntityName)).ToList();

        foreach (var gizmoEntity in gizmoEntities)
        {
            gizmoEntity.Get<ModelComponent>().SetMaterialParameter(MaterialKeys.EmissiveValue, Color.Value);
        }
    }

    private void CreateGizmos()
    {
        _gizmos.Clear();

        foreach (var component in GetAllComponents<CollidableComponent>(SceneSystem.SceneInstance.RootScene))
        {
            var gizmo = new CollidableGizmo(component);
            gizmo.SizeFactor = 1f;

            // This needs to be before IsEnabled and IsSelected
            gizmo.Initialize(Game.Services, SceneSystem.SceneInstance.RootScene);
            gizmo.IsEnabled = true;
            gizmo.IsSelected = true;

            _gizmos.Add(gizmo);
        }
    }

    private void RemoveGizmos()
    {
        foreach (var gizmo in _gizmos)
        {
            gizmo.Dispose();
        }

        _gizmos.Clear();
    }

    private static IEnumerable<T> GetAllComponents<T>(Scene scene)
        where T : EntityComponent
    {
        foreach (var entity in GetAllEntities(scene.Entities))
            foreach (var component in entity.Components)
                if (component is T typed)
                    yield return typed;
    }

    private static IEnumerable<Entity> GetAllEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            yield return entity;

            foreach (var child in GetAllEntities(entity.GetChildren()))
                yield return child;
        }
    }
}