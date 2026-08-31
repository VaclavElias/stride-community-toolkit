using Stride.Core;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;

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
                        EffectName = "ShapeShader",
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

        // The visibility group is created by the first frame's rendering, after the usual Start
        // callback runs - a one-shot game system registers the batch as soon as it exists
        game.GameSystems.Add(new BatchRegistrar(game.Services, batch));

        return batch;
    }

    private sealed class BatchRegistrar : GameSystemBase
    {
        private readonly ShapeBatch _batch;

        internal BatchRegistrar(IServiceRegistry services, ShapeBatch batch) : base(services)
        {
            _batch = batch;
            Enabled = true;
        }

        public override void Update(GameTime gameTime)
        {
            var visibilityGroup = RenderContext.GetShared(Services).VisibilityGroup;

            if (visibilityGroup == null) return;

            visibilityGroup.RenderObjects.Add(_batch);
            Enabled = false;
        }
    }
}