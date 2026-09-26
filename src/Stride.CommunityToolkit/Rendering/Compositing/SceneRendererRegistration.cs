using Stride.Engine;
using Stride.Rendering.Compositing;

namespace Stride.CommunityToolkit.Rendering.Compositing;

/// <summary>
/// How a component's processor makes sure the scene renderer that draws it is on the compositor,
/// where nothing else put it there: Game Studio's scene editor, which runs processors but never a
/// game's setup code, or a game that skipped the <c>Add*Renderer</c> call.
/// </summary>
/// <remarks>
/// The compositor is the one of the scene system that owns the processor's scene, not whatever
/// the render context carries - in the editor that is the gizmo compositor's. It is checked every
/// frame because the editor swaps compositors: at start, from its fallback to the project's, and
/// on every change of the view mode. A renderer on the old one draws in nothing.
/// </remarks>
public static class SceneRendererRegistration
{
    /// <summary>
    /// Puts a <typeparamref name="TRenderer"/> on the owning scene system's compositor unless one is
    /// there already, remembering the compositor so the check is one reference comparison per frame.
    /// </summary>
    /// <param name="services">The processor's services.</param>
    /// <param name="entityManager">The processor's entity manager, which must be the scene system's scene instance for anything to happen.</param>
    /// <param name="ensuredOn">The compositor the renderer was last ensured on; updated here.</param>
    /// <param name="create">Makes the renderer when the compositor has none.</param>
    public static void Ensure<TRenderer>(IServiceRegistry services, EntityManager entityManager, ref GraphicsCompositor? ensuredOn, Func<TRenderer> create)
        where TRenderer : SceneRendererBase
    {
        if (OwningCompositor(services, entityManager) is not { } compositor || ReferenceEquals(compositor, ensuredOn)) return;

        compositor.EnsureSceneRenderer(create);

        ensuredOn = compositor;
    }

    /// <summary>
    /// The compositor that draws the scene <paramref name="entityManager"/> belongs to: the one on the
    /// scene system whose scene instance it is. Null for any other entity manager, and until the scene
    /// system has a compositor. What a processor registers its renderer, feature or render object with -
    /// not the render context's render system, which is whichever compositor drew last and in Game
    /// Studio is the gizmo compositor's.
    /// </summary>
    public static GraphicsCompositor? OwningCompositor(IServiceRegistry services, EntityManager entityManager)
    {
        if (services.GetService<SceneSystem>() is not { } sceneSystem || !ReferenceEquals(sceneSystem.SceneInstance, entityManager)) return null;

        return sceneSystem.GraphicsCompositor;
    }
}