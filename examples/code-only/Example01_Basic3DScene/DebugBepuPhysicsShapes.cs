using Stride.BepuPhysics;
using Stride.BepuPhysics.Systems;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Rendering.Materials;

namespace Example01_Basic3DScene;

public class DebugBepuPhysicsShapes : SyncScript
{
    private bool gizmosActive = false;
    private readonly List<CollidableGizmo> gizmos = new List<CollidableGizmo>();

    public override void Update()
    {
        // Toggle on P
        if (Input.IsKeyPressed(Keys.P))
        {
            if (!gizmosActive)
            {
                CreateGizmos();

                // change gizmo material color to red


                var gizmoEntities = Entity.Scene.Entities.Where(e => e.Name.Contains("CollidableGizmo")).ToList();

                foreach (var gizmoEntity in gizmoEntities)
                {
                    gizmoEntity.Get<ModelComponent>().SetMaterialParameter(MaterialKeys.EmissiveValue, new Color4(0.25f, 0, 0, 0.5f));
                    //var modelComponent = gizmoEntity.Get<ModelComponent>();
                    //if (modelComponent != null && modelComponent.Materials.Count > 0)
                    //{
                    //    var material = modelComponent.Materials[0].Passes[0];

                    //    material.Parameters.Set(MaterialKeys.DiffuseValue, Color.Red);
                    //}
                }
            }
            else
            {
                RemoveGizmos();
            }

            gizmosActive = !gizmosActive;
        }

        // Update active gizmos
        if (gizmosActive)
        {
            foreach (var gizmo in gizmos)
            {
                gizmo.Update();
            }
        }
    }

    private void CreateGizmos()
    {
        gizmos.Clear();

        foreach (
            var component in GetAllComponents<CollidableComponent>(
                SceneSystem.SceneInstance.RootScene
            )
        )
        {
            var gizmo = new CollidableGizmo(component);
            gizmo.SizeFactor = 1f;
            gizmo.Initialize(Game.Services, SceneSystem.SceneInstance.RootScene);
            gizmo.IsEnabled = true;
            gizmo.IsSelected = true;

            gizmos.Add(gizmo);
        }
    }

    private void RemoveGizmos()
    {
        foreach (var gizmo in gizmos)
        {
            gizmo.Dispose(); // CollidableGizmo implements IDisposable
        }
        gizmos.Clear();
    }

    private IEnumerable<T> GetAllComponents<T>(Scene scene)
        where T : EntityComponent
    {
        foreach (var entity in GetAllEntities(scene.Entities))
        {
            foreach (var component in entity.Components)
            {
                if (component is T typed)
                    yield return typed;
            }
        }
    }

    private IEnumerable<Entity> GetAllEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            yield return entity;

            foreach (var child in GetAllEntities(entity.GetChildren()))
                yield return child;
        }
    }
}