using Stride.Core;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Wires <see cref="Box2DDebugDraw"/> rendering into a game's graphics compositor.
/// </summary>
public static class Box2DDebugDrawExtensions
{
    /// <summary>
    /// Registers the Box2D testbed-style shape renderer and returns the batch to submit shapes to.
    /// </summary>
    /// <param name="game">The game whose compositor to modify.</param>
    /// <returns>The batch: submit shapes to it every frame from your update logic.</returns>
    /// <remarks>
    /// The shapes render in the compositor's "Transparent" stage, alpha-blended in submission order,
    /// before UI and debug text. Call after the graphics compositor exists (from the Start callback).
    /// </remarks>
    /// <exception cref="InvalidOperationException">The compositor has no "Transparent" render stage.</exception>
    public static Box2DDebugDraw AddBox2DDebugDraw(this Game game)
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

        if (!compositor.RenderFeatures.OfType<Box2DDebugDrawFeature>().Any())
        {
            compositor.RenderFeatures.Add(new Box2DDebugDrawFeature
            {
                RenderStageSelectors =
                {
                    new SimpleGroupToRenderStageSelector
                    {
                        RenderStage = transparentStage,
                        EffectName = "Box2DDebugShader",
                        RenderGroup = RenderGroupMask.All,
                    }
                }
            });
        }

        var batch = new Box2DDebugDraw();

        // Expose the batch to Box2DDebugShapeProcessor and anything else that wants to draw
        if (game.Services.GetService<Box2DDebugDraw>() is null)
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
        private readonly Box2DDebugDraw _batch;

        internal BatchRegistrar(IServiceRegistry services, Box2DDebugDraw batch) : base(services)
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