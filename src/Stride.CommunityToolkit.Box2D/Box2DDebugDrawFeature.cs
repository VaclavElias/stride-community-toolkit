using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using System.Runtime.InteropServices;
using Buffer = Stride.Graphics.Buffer;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Renders every <see cref="Box2DDebugDraw"/> batch with <c>Box2DDebugShader</c>: one instanced
/// draw of a shared quad per batch, the polygon geometry and colours delivered through a structured
/// buffer and evaluated per fragment as a signed distance function - the Box2D testbed's renderer,
/// ported to Stride.
/// </summary>
public class Box2DDebugDrawFeature : RootRenderFeature
{
    // The quad has a margin around the unit polygon so the anti-aliased border is never clipped
    private const float QuadMargin = 1.1f;

    private DynamicEffectInstance? _effect;
    private MutablePipelineState? _pipelineState;
    private Buffer? _quadBuffer;
    private Buffer? _instanceBuffer;
    private VertexDeclaration? _vertexDeclaration;

    /// <inheritdoc/>
    public override Type SupportedRenderObjectType => typeof(Box2DDebugDraw);

    /// <inheritdoc/>
    protected override void InitializeCore()
    {
        _effect = new DynamicEffectInstance("Box2DDebugShader");
        _effect.Initialize(Context.Services);
        _effect.UpdateEffect(Context.GraphicsDevice);

        _vertexDeclaration = new VertexDeclaration(VertexElement.Position<Vector2>());

        _quadBuffer = Buffer.Vertex.New(Context.GraphicsDevice, new[]
        {
            new Vector2(-QuadMargin, -QuadMargin),
            new Vector2(QuadMargin, -QuadMargin),
            new Vector2(-QuadMargin, QuadMargin),
            new Vector2(QuadMargin, -QuadMargin),
            new Vector2(QuadMargin, QuadMargin),
            new Vector2(-QuadMargin, QuadMargin),
        });

        _instanceBuffer = Buffer.Structured.New<Box2DDebugDraw.PolygonInstance>(Context.GraphicsDevice, 1);

        _pipelineState = new MutablePipelineState(Context.GraphicsDevice);
        _pipelineState.State.SetDefaults();
        _pipelineState.State.InputElements = _vertexDeclaration.CreateInputElements();
        _pipelineState.State.BlendState = BlendStates.AlphaBlend;
        _pipelineState.State.DepthStencilState = DepthStencilStates.None;
        _pipelineState.State.RasterizerState = RasterizerStates.CullNone;
        _pipelineState.State.PrimitiveType = PrimitiveType.TriangleList;
    }

    /// <inheritdoc/>
    public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
    {
        if (_effect is null || _pipelineState is null) return;

        var commandList = context.CommandList;

        for (var index = startIndex; index < endIndex; index++)
        {
            var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
            var batch = (Box2DDebugDraw)GetRenderNode(renderNodeReference).RenderObject;
            var instances = CollectionsMarshal.AsSpan(batch.Instances);

            if (instances.IsEmpty) continue;

            UploadInstances(context, instances);

            // Pixels per world unit for an orthographic view: projection M22 is 2 / world height
            var pixelScale = renderView.ViewSize.Y * renderView.Projection.M22 * 0.5f;

            _effect.UpdateEffect(context.GraphicsDevice);
            _effect.Parameters.Set(Box2DDebugShaderKeys.ViewProjection, renderView.ViewProjection);
            _effect.Parameters.Set(Box2DDebugShaderKeys.PixelScale, pixelScale);
            _effect.Parameters.Set(Box2DDebugShaderKeys.BorderPixels, batch.BorderWidth);
            _effect.Parameters.Set(Box2DDebugShaderKeys.Polygons, _instanceBuffer);

            _pipelineState.State.RootSignature = _effect.RootSignature;
            _pipelineState.State.EffectBytecode = _effect.Effect.Bytecode;
            _pipelineState.State.Output.CaptureState(commandList);
            _pipelineState.Update();

            commandList.SetPipelineState(_pipelineState.CurrentState);
            commandList.SetVertexBuffer(0, _quadBuffer, 0, _vertexDeclaration!.VertexStride);

            _effect.Apply(context.GraphicsContext);

            commandList.DrawInstanced(6, instances.Length);

            // Immediate mode: the frame's submissions are consumed by this draw
            batch.Reset();
        }
    }

    private void UploadInstances(RenderDrawContext context, ReadOnlySpan<Box2DDebugDraw.PolygonInstance> instances)
    {
        if (instances.Length > _instanceBuffer!.ElementCount)
        {
            _instanceBuffer.Dispose();

            var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(instances.Length, 64));
            _instanceBuffer = Buffer.Structured.New<Box2DDebugDraw.PolygonInstance>(context.GraphicsDevice, capacity);
        }

        _instanceBuffer.SetData(context.CommandList, instances);
    }

    /// <inheritdoc/>
    public override void Unload()
    {
        _effect?.Dispose();
        _quadBuffer?.Dispose();
        _instanceBuffer?.Dispose();

        base.Unload();
    }
}