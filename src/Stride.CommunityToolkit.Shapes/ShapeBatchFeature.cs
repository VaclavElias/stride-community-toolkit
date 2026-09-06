using Stride.CommunityToolkit.Rendering;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using System.Runtime.InteropServices;
using Buffer = Stride.Graphics.Buffer;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// Renders every <see cref="ShapeBatch"/> with <c>ShapeShader</c>: one instanced draw of a shared
/// quad per batch, the shape records and their points delivered through two structured buffers and
/// evaluated per fragment as a signed distance function.
/// </summary>
/// <remarks>
/// The frame's shapes are uploaded once, in <see cref="Prepare"/>, for every batch together, and
/// the batches are emptied in <see cref="Flush"/>, after the last view has drawn - so a second
/// camera, a render-to-texture pass or the editor's view sees the same shapes as the first, and a
/// batch whose stage never runs does not keep growing.
/// </remarks>
public class ShapeBatchFeature : RootRenderFeature
{
    /// <summary>The GPU timing scope around each batch's draw, for Stride's profiler and for a frame capture.</summary>
    public static readonly ProfilingKey ProfilingKey = new("ShapeBatch");

    private static readonly Color4 ProfileColor = new(1f, 0.6f, 0.1f, 1f);

    private DynamicEffectInstance? _effect;
    private MutablePipelineState? _pipelineState;
    private Buffer? _quadBuffer;
    private Buffer? _instanceBuffer;
    private Buffer? _pointBuffer;
    private VertexDeclaration? _vertexDeclaration;
    private DisplayScale? _displayScale;

    // Every batch's records and points for the frame, one after another
    private readonly List<ShapeInstance> _instances = [];
    private readonly List<Vector2> _points = [];

    // The effect, its parameters and the pipeline state are shared by every draw of this
    // feature. Stride draws a stage on worker threads when it knows the stage's depth access;
    // for a feature without render effects it does not, so today the draws are sequential - but
    // a compositor may say so explicitly, and then two batches would draw at once.
    private readonly object _drawLock = new();

    /// <summary>
    /// Creates the feature. Shapes sort after everything else in their stage, so an overlay batch
    /// draws over the scene's other transparent objects and a depth-tested one still hides behind
    /// the opaque geometry it reads.
    /// </summary>
    public ShapeBatchFeature()
    {
        SortKey = 255;
    }

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
        _pointBuffer = Buffer.Structured.New<Vector2>(Context.GraphicsDevice, 1);

        _pipelineState = new MutablePipelineState(Context.GraphicsDevice);
        _pipelineState.State.SetDefaults();
        _pipelineState.State.InputElements = _vertexDeclaration.CreateInputElements();
        // The shader composites its layers premultiplied and emits premultiplied colour, so the
        // blend is Stride's own AlphaBlend: source One, destination InverseSourceAlpha, which also
        // leaves a correct alpha in the target when a batch renders into a texture. Fed straight
        // alpha this state would add colour at full strength whatever the alpha - which is what
        // the shader used to produce, and why it once needed NonPremultiplied instead.
        _pipelineState.State.BlendState = BlendStates.AlphaBlend;
        _pipelineState.State.RasterizerState = RasterizerStates.CullNone;
        _pipelineState.State.PrimitiveType = PrimitiveType.TriangleList;
    }

    /// <summary>
    /// Gathers every batch's shapes into the two buffers, once per frame, before any view draws.
    /// </summary>
    public override void Prepare(RenderDrawContext context)
    {
        base.Prepare(context);

        _instances.Clear();
        _points.Clear();

        foreach (var renderObject in RenderObjects)
        {
            var batch = (ShapeBatch)renderObject;

            // Where this batch's records and points start, for the shader to add to its indices
            batch.InstanceBase = _instances.Count;
            batch.PointBase = _points.Count;

            _instances.AddRange(batch.Instances);
            _points.AddRange(batch.Points);
        }

        Upload(context, ref _instanceBuffer!, CollectionsMarshal.AsSpan(_instances));
        Upload(context, ref _pointBuffer!, CollectionsMarshal.AsSpan(_points));
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

        // Stride decides once per device whether the backbuffer is sRGB; the shader decodes its
        // palette only then, the way SpriteBatch picks its sRGB effect
        var linearOutput = context.GraphicsDevice.ColorSpace == ColorSpace.Linear ? 1u : 0u;

        lock (_drawLock)
        {
            for (var index = startIndex; index < endIndex; index++)
            {
                var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
                var batch = (ShapeBatch)GetRenderNode(renderNodeReference).RenderObject;

                if (batch.Instances.Count == 0) continue;

                using var _ = context.QueryManager.BeginProfile(ProfileColor, ProfilingKey);

                // A display at 150% has 1.5 physical pixels where a 100% one has one, so the same
                // width in "pixels" needs 1.5 of them: fewer pixels per world unit, as the shader
                // sees it, is what makes every pixel-measured width come out that much wider
                var displayScale = batch.AutoScale && _displayScale is not null ? _displayScale.Value : 1f;

                _effect.UpdateEffect(context.GraphicsDevice);
                _effect.Parameters.Set(ShapeShaderKeys.ViewProjection, renderView.ViewProjection);
                _effect.Parameters.Set(ShapeShaderKeys.PixelScale, pixelScale / displayScale);
                _effect.Parameters.Set(ShapeShaderKeys.CameraRight, cameraRight);
                _effect.Parameters.Set(ShapeShaderKeys.CameraUp, cameraUp);
                _effect.Parameters.Set(ShapeShaderKeys.EyePosition, eyePosition);
                _effect.Parameters.Set(ShapeShaderKeys.LinearOutput, linearOutput);
                _effect.Parameters.Set(ShapeShaderKeys.InstanceBase, (uint)batch.InstanceBase);
                _effect.Parameters.Set(ShapeShaderKeys.PointBase, (uint)batch.PointBase);
                _effect.Parameters.Set(ShapeShaderKeys.Shapes, _instanceBuffer);
                _effect.Parameters.Set(ShapeDistanceKeys.Points, _pointBuffer);

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

                commandList.DrawInstanced(6, batch.Instances.Count);
            }
        }
    }

    /// <summary>
    /// Empties every batch once the frame's last view has drawn: immediate mode, one frame's
    /// submissions drawn by every view that wants them and then gone.
    /// </summary>
    public override void Flush(RenderDrawContext context)
    {
        base.Flush(context);

        foreach (var renderObject in RenderObjects)
        {
            ((ShapeBatch)renderObject).Reset();
        }
    }

    // Default usage and a whole-buffer update: the one contiguous upload per frame that every
    // backend takes on its fast path. Grown by powers of two, never shrunk.
    private static void Upload<T>(RenderDrawContext context, ref Buffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.IsEmpty) return;

        if (data.Length > buffer.ElementCount)
        {
            buffer.Dispose();

            var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(data.Length, 64));
            buffer = Buffer.Structured.New<T>(context.GraphicsDevice, capacity);
        }

        buffer.SetData(context.CommandList, data);
    }

    /// <inheritdoc/>
    public override void Unload()
    {
        _effect?.Dispose();
        _quadBuffer?.Dispose();
        _instanceBuffer?.Dispose();
        _pointBuffer?.Dispose();

        base.Unload();
    }
}