using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Stride.CommunityToolkit.Rendering.Text;

/// <summary>
/// Keeps track of every <see cref="WorldTextComponent"/> in the scene for the world text renderer.
/// </summary>
/// <remarks>
/// Registered automatically through the <c>DefaultEntityComponentProcessor</c> attribute on the
/// component, so nothing needs to add it. Collection through a processor covers the whole entity
/// hierarchy and gives each component's cached measurement a lifetime tied to the component itself.
/// It also makes sure a <see cref="WorldTextRenderer"/> is on the scene's compositor, which is what
/// draws the components in Game Studio's scene editor and in a game that never called
/// <c>AddWorldTextRenderer</c>.
/// </remarks>
public class WorldTextProcessor : EntityProcessor<WorldTextComponent, WorldTextRenderData>
{
    private readonly List<WorldTextRenderData> _texts = [];

    // The compositor the renderer was last ensured on; the editor swaps compositors
    private GraphicsCompositor? _ensuredOn;

    /// <summary>
    /// Gets every world text currently in the scene, in no particular order.
    /// </summary>
    public IReadOnlyList<WorldTextRenderData> Texts => _texts;

    /// <inheritdoc />
    public override void Draw(RenderContext context)
    {
        base.Draw(context);

        if (_texts.Count == 0) return;

        SceneRendererRegistration.Ensure(Services, EntityManager, ref _ensuredOn, () => new WorldTextRenderer());
    }

    /// <inheritdoc />
    protected override WorldTextRenderData GenerateComponentData(Entity entity, WorldTextComponent component)
        => new(component);

    /// <inheritdoc />
    protected override bool IsAssociatedDataValid(Entity entity, WorldTextComponent component, WorldTextRenderData associatedData)
        => associatedData.Component == component;

    /// <inheritdoc />
    protected override void OnEntityComponentAdding(Entity entity, WorldTextComponent component, WorldTextRenderData data)
        => _texts.Add(data);

    /// <inheritdoc />
    protected override void OnEntityComponentRemoved(Entity entity, WorldTextComponent component, WorldTextRenderData data)
        => _texts.Remove(data);
}