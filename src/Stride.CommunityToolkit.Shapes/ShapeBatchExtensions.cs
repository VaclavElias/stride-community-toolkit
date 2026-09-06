using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Wires <see cref="ShapeBatch"/> rendering into a game's graphics compositor.
/// </summary>
public static class ShapeBatchExtensions
{
    /// <summary>
    /// Registers the shape renderer and returns the batch to submit shapes to.
    /// </summary>
    /// <param name="game">The game whose compositor to modify.</param>
    /// <param name="depthTest">
    /// Whether scene geometry can occlude these shapes. Leave <c>false</c> for gizmos, 2D scenes and
    /// anything that should stay visible through walls; pass <c>true</c> for decals and ground
    /// markers that belong in the scene.
    /// </param>
    /// <returns>The batch: submit shapes to it every frame from your update logic.</returns>
    /// <remarks>
    /// Shapes render in the compositor's "Transparent" stage, alpha-blended in submission order,
    /// before UI and debug text. Call after the graphics compositor exists (from the Start callback).
    /// Calling this more than once adds another independent batch, which is how you get depth-tested
    /// and overlay shapes in the same scene; the first batch registers as the service that
    /// <see cref="ShapeComponent"/> draws through.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The compositor has no "Transparent" render stage.</exception>
    public static ShapeBatch AddShapeBatch(this Game game, bool depthTest = false)
    {
        ArgumentNullException.ThrowIfNull(game);

        var compositor = game.SceneSystem.GraphicsCompositor
            ?? throw new InvalidOperationException("The game has no graphics compositor.");

        RenderStage? transparentStage = null;

        foreach (var stage in compositor.RenderSystem.RenderStages)
        {
            if (stage.Name == "Transparent")
            {
                transparentStage = stage;
                break;
            }
        }

        if (transparentStage is null)
            throw new InvalidOperationException("The graphics compositor has no Transparent render stage.");

        if (!compositor.RenderFeatures.OfType<ShapeBatchFeature>().Any())
        {
            compositor.RenderFeatures.Add(new ShapeBatchFeature
            {
                RenderStageSelectors =
                {
                    new SimpleGroupToRenderStageSelector
                    {
                        RenderStage = transparentStage,
                        RenderGroup = RenderGroupMask.All,
                    }
                }
            });
        }

        var batch = new ShapeBatch { DepthTest = depthTest };

        // Expose the first batch to ShapeProcessor and anything else that wants to draw
        if (game.Services.GetService<ShapeBatch>() is null)
        {
            game.Services.AddService(batch);
        }

        VisibilityGroupFor(game, compositor).RenderObjects.Add(batch);

        return batch;
    }

    /// <summary>
    /// Takes a batch out of rendering. Its shapes stop drawing from the next frame; the batch itself
    /// can be added again later.
    /// </summary>
    /// <param name="game">The game the batch was added to.</param>
    /// <param name="batch">The batch <see cref="AddShapeBatch"/> returned.</param>
    public static void RemoveShapeBatch(this Game game, ShapeBatch batch)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(batch);

        if (game.SceneSystem.GraphicsCompositor is not { } compositor) return;

        foreach (var visibilityGroup in game.SceneSystem.SceneInstance.VisibilityGroups)
        {
            if (visibilityGroup.RenderSystem == compositor.RenderSystem)
            {
                visibilityGroup.RenderObjects.Remove(batch);
            }
        }

        batch.Reset();
    }

    // The visibility group that pairs the scene with the compositor's render system, which is
    // what a render object is registered with. The compositor makes it on its first draw, after
    // the usual Start callback has run; making it here first, the same way, means the batch is
    // registered before the first frame instead of one frame later, and there is no one-shot
    // system polling for it. The compositor finds it by render system and adopts it.
    private static VisibilityGroup VisibilityGroupFor(Game game, GraphicsCompositor compositor)
    {
        var sceneInstance = game.SceneSystem.SceneInstance
            ?? throw new InvalidOperationException("The game has no scene instance yet; add the batch from the Start callback or later.");

        foreach (var visibilityGroup in sceneInstance.VisibilityGroups)
        {
            if (visibilityGroup.RenderSystem == compositor.RenderSystem)
            {
                return visibilityGroup;
            }
        }

        var created = new VisibilityGroup(compositor.RenderSystem);

        sceneInstance.VisibilityGroups.Add(created);

        return created;
    }
}