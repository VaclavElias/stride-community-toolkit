using Stride.CommunityToolkit.Rendering;
using Stride.Core.Mathematics;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using System.Runtime.InteropServices;
using Buffer = Stride.Graphics.Buffer;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Renders every <see cref="ShapeBatch"/> with <c>ShapeShader</c>: one instanced draw of a shared
/// quad per batch, the shape geometry and colours delivered through a structured buffer and
/// evaluated per fragment as a signed distance function.
/// </summary>
public class ShapeBatchFeature : RootRenderFeature
{
    private DynamicEffectInstance? _effect;
    private MutablePipelineState? _pipelineState;
    private Buffer? _quadBuffer;
    private Buffer? _instanceBuffer;
    private VertexDeclaration? _vertexDeclaration;
    private DisplayScale? _displayScale;

    /// <inheritdoc/>
    public override Type SupportedRenderObjectType => typeof(ShapeBatch);

    /// <inheritdoc/>
    protected override void InitializeCore()
    {
        _effect = new DynamicEffectInstance("ShapeShader");
        _effect.Initialize(Context.Services);
        _effect.UpdateEffect(Context.GraphicsDevice);

        // The display's scale, for batches whose pixel widths follow it. Absent outside a game -
        // the editor, a test harness - which leaves the widths at exactly the pixels asked for.
        if (Context.Services.GetService<IGame>() is { } game)
        {
            _displayScale = DisplayScale.GetOrCreate(game);
        }

        _vertexDeclaration = new VertexDeclaration(VertexElement.Position<Vector2>());

        // A unit quad; the vertex shader grows it per shape to leave room for a thick border
        _quadBuffer = Buffer.Vertex.New(Context.GraphicsDevice, new[]
        {
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(1f, -1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, 1f),
        });

        _instanceBuffer = Buffer.Structured.New<ShapeInstance>(Context.GraphicsDevice, 1);

        _pipelineState = new MutablePipelineState(Context.GraphicsDevice);
        _pipelineState.State.SetDefaults();
        _pipelineState.State.InputElements = _vertexDeclaration.CreateInputElements();
        // The shader produces straight alpha - the testbed's convention, and what its GL blend of
        // SrcAlpha / OneMinusSrcAlpha expects. Stride's AlphaBlend is the premultiplied blend, source
        // factor One: fed straight alpha it adds the colour at full strength and only scales the
        // background, so a fill at a tenth alpha still drew at full brightness over a dark scene,
        // glows read far heavier than their falloff, and a gradient to alpha 0 changed nothing.
        // NonPremultiplied is SrcAlpha / OneMinusSrcAlpha exactly.
        _pipelineState.State.BlendState = BlendStates.NonPremultiplied;
        _pipelineState.State.RasterizerState = RasterizerStates.CullNone;
        _pipelineState.State.PrimitiveType = PrimitiveType.TriangleList;
    }

    /// <inheritdoc/>
    public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
    {
        if (_effect is null || _pipelineState is null) return;

        var commandList = context.CommandList;

        // Pixels per world unit at clip w = 1: projection M22 is 2 / world height for an
        // orthographic view, and 1 / tan(fov / 2) for a perspective one, where the shader's
        // per-fragment w supplies the distance falloff
        var pixelScale = renderView.ViewSize.Y * renderView.Projection.M22 * 0.5f;

        var viewInverse = Matrix.Invert(renderView.View);
        var cameraRight = new Vector3(viewInverse.M11, viewInverse.M12, viewInverse.M13);
        var cameraUp = new Vector3(viewInverse.M21, viewInverse.M22, viewInverse.M23);
        var eyePosition = viewInverse.TranslationVector;

        for (var index = startIndex; index < endIndex; index++)
        {
            var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
            var batch = (ShapeBatch)GetRenderNode(renderNodeReference).RenderObject;
            var instances = CollectionsMarshal.AsSpan(batch.Instances);

            if (instances.IsEmpty) continue;

            UploadInstances(context, instances);

            // A display at 150% has 1.5 physical pixels where a 100% one has one, so the same width
            // in "pixels" needs 1.5 of them: fewer pixels per world unit, as the shader sees it, is
            // what makes every pixel-measured width - border, glow, dashes - come out that much wider
            var displayScale = batch.AutoScale && _displayScale is not null ? _displayScale.Value : 1f;

            _effect.UpdateEffect(context.GraphicsDevice);
            _effect.Parameters.Set(ShapeShaderKeys.ViewProjection, renderView.ViewProjection);
            _effect.Parameters.Set(ShapeShaderKeys.PixelScale, pixelScale / displayScale);
            _effect.Parameters.Set(ShapeShaderKeys.CameraRight, cameraRight);
            _effect.Parameters.Set(ShapeShaderKeys.CameraUp, cameraUp);
            _effect.Parameters.Set(ShapeShaderKeys.EyePosition, eyePosition);
            _effect.Parameters.Set(ShapeShaderKeys.Shapes, _instanceBuffer);

            // Tested but never written: shapes are transparent, so writing depth would let one
            // shape reject another that should blend over it
            _pipelineState.State.DepthStencilState = batch.DepthTest ? DepthStencilStates.DepthRead : DepthStencilStates.None;
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

    private void UploadInstances(RenderDrawContext context, ReadOnlySpan<ShapeInstance> instances)
    {
        if (instances.Length > _instanceBuffer!.ElementCount)
        {
            _instanceBuffer.Dispose();

            var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(instances.Length, 64));
            _instanceBuffer = Buffer.Structured.New<ShapeInstance>(context.GraphicsDevice, capacity);
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