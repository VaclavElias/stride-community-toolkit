using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Stride.CommunityToolkit.Rendering.Compositing;

/// <summary>
/// Finds the camera a scene renderer should draw with, for renderers that sit outside the camera
/// renderer and so have no current camera on the context.
/// </summary>
/// <remarks>
/// A game's compositor names its camera in a slot, and slot zero is the one every toolkit renderer
/// used to read. Game Studio's scene editor clears the slots and hands its own camera to the
/// top-level camera renderer as an external camera instead, so the slots are tried first and the
/// renderer tree is walked for a camera renderer after that. Null when neither has one.
/// </remarks>
public static class CompositorCameras
{
    /// <summary>The camera of the current compositor: its first slot, or its first camera renderer.</summary>
    public static CameraComponent? Find(RenderContext context)
    {
        if (context.Tags.Get(GraphicsCompositor.Current) is not { } compositor) return null;

        if (compositor.Cameras.Count > 0 && compositor.Cameras[0].Camera is { } slotCamera) return slotCamera;

        return FromRenderer(compositor.Game);
    }

    private static CameraComponent? FromRenderer(ISceneRenderer? renderer)
    {
        switch (renderer)
        {
            // The editor's top-level renderer derives from this one and carries the editor camera here
            case SceneExternalCameraRenderer external:
                return external.ExternalCamera;

            case SceneCameraRenderer cameraRenderer:
                return cameraRenderer.Camera?.Camera;

            case SceneRendererCollection collection:
                foreach (var child in collection.Children)
                {
                    if (FromRenderer(child) is { } found) return found;
                }

                return null;

            default:
                return null;
        }
    }
}