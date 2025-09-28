using ImGuiNET;
using Stride.CommunityToolkit.Engine;
using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;
using System.Runtime.CompilerServices;
using Rectangle = Stride.Core.Mathematics.Rectangle;

namespace Stride.CommunityToolkit.ImGuiNet;

/// <summary>
/// Provides ImGui.NET integration for Stride with Box2D.NET-style text rendering capabilities.
/// This is an alternative to the existing Hexa.NET.ImGui implementation in the toolkit.
/// </summary>
public class ImGuiNetSystem : GameSystemBase
{
    private static readonly Logger Logger = GlobalLogger.GetLogger("ImGuiNet");

    private readonly List<DrawCommand> _drawCommands = [];
    private bool _showUI = true;
    private bool _initialized = false;

    private InputManager? _inputManager;
    private GraphicsDevice? _graphicsDevice;
    private CommandList? _commandList;
    private Texture? _fontTexture;
    private GraphicsContext? _graphicsContext;
    private CameraComponent? _camera;

    // Rendering infrastructure
    private VertexBufferBinding _vertexBinding;
    private IndexBufferBinding? _indexBinding;
    private EffectInstance? _imguiShader;
    private PipelineState? _pipelineState;
    private VertexDeclaration? _vertexLayout;

    // ImGui.NET context
    private IntPtr _context;

    /// <summary>
    /// Optional path to a custom TTF font. If the file exists, it will be used instead of the default font.
    /// Defaults to 'data/droid_sans.ttf' to match Example11_ImGuiNet.
    /// </summary>
    public string? FontPath { get; set; } = Path.Combine("data", "droid_sans.ttf");

    /// <summary>
    /// Font size in pixels for the custom TTF font. Ignored if <see cref="FontPath"/> doesn't exist.
    /// </summary>
    public float FontSize { get; set; } = 15f;

    /// <summary>
    /// Gets or sets whether UI elements should be displayed.
    /// </summary>
    public bool ShowUI
    {
        get => _showUI;
        set => _showUI = value;
    }

    /// <summary>
    /// Gets whether the ImGui system is initialized and ready for use.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImGuiNetSystem"/> class.
    /// </summary>
    /// <param name="registry">The service registry.</param>
    public ImGuiNetSystem(IServiceRegistry registry) : base(registry)
    {
        Enabled = true;
        Visible = true;
        UpdateOrder = 1;

        Services.AddService(this);
        Game.GameSystems.Add(this);
    }

    /// <summary>
    /// Draws a string at screen coordinates, similar to Box2D.NET's DrawString method.
    /// </summary>
    /// <param name="x">The x coordinate in screen space.</param>
    /// <param name="y">The y coordinate in screen space.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="color">The text color (optional, defaults to light gray).</param>
    public void DrawString(int x, int y, string message, Vector4? color = null)
    {
        if (!_showUI || !_initialized) return;

        _drawCommands.Add(new DrawCommand
        {
            Type = DrawCommandType.ScreenText,
            ScreenPosition = new Vector2(x, y),
            Message = message,
            Color = color ?? new Vector4(0.9f, 0.9f, 0.9f, 1.0f)
        });
    }

    /// <summary>
    /// Draws a string at world coordinates, similar to Box2D.NET's DrawString method.
    /// </summary>
    /// <param name="worldPosition">The position in world space.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="color">The text color (optional, defaults to light gray).</param>
    public void DrawString(Vector3 worldPosition, string message, Vector4? color = null)
    {
        if (!_showUI || !_initialized) return;

        _drawCommands.Add(new DrawCommand
        {
            Type = DrawCommandType.WorldText,
            WorldPosition = worldPosition,
            Message = message,
            Color = color ?? new Vector4(0.9f, 0.9f, 0.9f, 1.0f)
        });
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _inputManager = Services.GetService<InputManager>();
        _graphicsDevice = Game.GraphicsDevice;
        _graphicsContext = Game.GraphicsContext;
        var sceneSystem = Game.Services.GetService<SceneSystem>();
        _commandList = _graphicsContext?.CommandList;

        if (_graphicsDevice == null)
        {
            Logger.Warning("ImGuiNetSystem: GraphicsDevice not available");
            return;
        }

        try
        {
            _context = ImGui.CreateContext();
            ImGui.SetCurrentContext(_context);

            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

            // Compute initial DPI / framebuffer scale using Stride backbuffer vs client bounds
            var clientBounds = Game.Window.ClientBounds;
            var back = _graphicsDevice.Presenter?.BackBuffer;
            float initialScale = 1.0f;
            if (back != null && clientBounds.Width > 0 && clientBounds.Height > 0)
            {
                float scaleX = back.Width / (float)clientBounds.Width;
                float scaleY = back.Height / (float)clientBounds.Height;
                // Use average or X; you can prefer one axis if needed
                initialScale = MathF.Max(1.0f, (scaleX + scaleY) * 0.5f);
                Logger.Info($"ImGuiNetSystem: Initial detected framebuffer scale via Stride: {scaleX:F2} x {scaleY:F2} -> using {initialScale:F2}");
            }

            // Build the font atlas - this is crucial to fix the assertion error
            SetupFontAtlas();

            // Create rendering resources
            CreateRenderingResources();

            _camera = sceneSystem?.SceneInstance.RootScene.GetCamera();

            _initialized = true;

            Logger.Info("ImGuiNetSystem initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize ImGuiNetSystem: {ex.Message}");
        }
    }

    private void CreateRenderingResources()
    {
        if (_graphicsDevice == null || _graphicsContext == null) return;

        // Load or create ImGui shader (reuse existing one or create a fallback)
        var effectSystem = Services.GetService<EffectSystem>();
        if (effectSystem != null)
        {
            try
            {
                // Try to reuse the existing ImGui shader from the other ImGui implementation
                var effect = effectSystem.LoadEffect("ImGuiNetShader").WaitForResult();
                _imguiShader = new EffectInstance(effect);
                _imguiShader.UpdateEffect(_graphicsDevice);
                Logger.Info("Using ImGuiNetShader for rendering");
            }
            catch
            {
                Logger.Warning("Could not load any ImGui shader, text will not be visible");
                return;
            }
        }

        // Create vertex layout
        _vertexLayout = new VertexDeclaration(
            VertexElement.Position<Vector2>(),
            VertexElement.TextureCoordinate<Vector2>(),
            VertexElement.Color(PixelFormat.R8G8B8A8_UNorm)
        );

        // Create pipeline state
        var pipelineDesc = new PipelineStateDescription()
        {
            BlendState = BlendStates.NonPremultiplied,
            RasterizerState = new RasterizerStateDescription()
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                FillMode = FillMode.Solid,
                MultisampleAntiAliasLine = false,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = 0,
            },
            PrimitiveType = PrimitiveType.TriangleList,
            InputElements = _vertexLayout.CreateInputElements(),
            DepthStencilState = DepthStencilStates.None,
            EffectBytecode = _imguiShader?.Effect.Bytecode,
            RootSignature = _imguiShader?.RootSignature,
            Output = new RenderOutputDescription(PixelFormat.R8G8B8A8_UNorm)
        };

        _pipelineState = PipelineState.New(_graphicsDevice, ref pipelineDesc);

        // Create initial buffers
        var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(_graphicsDevice, 1024 * _vertexLayout.CalculateSize(), GraphicsResourceUsage.Dynamic);
        _vertexBinding = new VertexBufferBinding(vertexBuffer, _vertexLayout, 0);

        var indexBuffer = Stride.Graphics.Buffer.Index.New(_graphicsDevice, 2048 * sizeof(ushort), GraphicsResourceUsage.Dynamic);
        _indexBinding = new IndexBufferBinding(indexBuffer, false, 0);
    }

    private unsafe void SetupFontAtlas()
    {
        var io = ImGui.GetIO();

        // Clear existing fonts
        io.Fonts.Clear();

        bool customFontLoaded = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(FontPath) && File.Exists(FontPath))
            {
                io.Fonts.AddFontFromFileTTF(FontPath, FontSize);
                customFontLoaded = true;
                Logger.Info($"Loaded custom ImGui font: '{FontPath}' at {FontSize}px");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load custom font '{FontPath}': {ex.Message}. Falling back to default font.");
            customFontLoaded = false;
        }

        if (!customFontLoaded)
        {
            io.Fonts.AddFontDefault();
        }

        // Build the font atlas
        byte* pixels;
        int width, height, bytesPerPixel;
        io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out bytesPerPixel);

        _fontTexture?.Dispose();
        _fontTexture = null;

        if (_graphicsDevice != null && pixels != null)
        {
            // Create Stride texture from ImGui font data
            _fontTexture = Texture.New2D(_graphicsDevice, width, height, PixelFormat.R8G8B8A8_UNorm, TextureFlags.ShaderResource);

            if (_commandList != null)
            {
                _fontTexture.SetData(_commandList, new DataPointer(pixels, width * height * bytesPerPixel));
            }

            // Set a simple texture ID for ImGui (using texture hashcode as a simple identifier)
            io.Fonts.SetTexID((IntPtr)_fontTexture.GetHashCode());
        }
        else
        {
            // Fallback: just mark as built without texture
            io.Fonts.SetTexID(IntPtr.Zero);
        }

        Logger.Info($"Font atlas built successfully: {width}x{height}, {bytesPerPixel} bytes per pixel");
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        if (!_initialized) return;

        var client = Game.Window.ClientBounds;
        var back2 = _graphicsDevice?.Presenter?.BackBuffer;
        if (back2 != null)
        {
            var scaleX = back2.Width / (float)Math.Max(1, client.Width);
            var scaleY = back2.Height / (float)Math.Max(1, client.Height);
            Logger.Info($"Backbuffer = {back2.Width}x{back2.Height}, ClientBounds = {client.Width}x{client.Height}, scale = {scaleX:F2} x {scaleY:F2}");
        }
        else
        {
            Logger.Info($"Backbuffer not ready yet; ClientBounds = {client.Width}x{client.Height}");
        }

        var deltaTime = (float)gameTime.Elapsed.TotalSeconds;
        var io = ImGui.GetIO();

        // Update display size
        var clientBounds = Game.Window.ClientBounds;
        io.DisplaySize = new Vector2(clientBounds.Width, clientBounds.Height);

        // HiDPI/backbuffer scaling (matches Box2D.NET pattern)
        if (_graphicsDevice?.Presenter?.BackBuffer != null)
        {
            var back = _graphicsDevice.Presenter.BackBuffer;
            if (clientBounds.Width > 0 && clientBounds.Height > 0)
            {
                io.DisplayFramebufferScale = new Vector2(
                    back.Width / (float)clientBounds.Width,
                    back.Height / (float)clientBounds.Height);
            }
        }

        io.DeltaTime = deltaTime > 0 ? deltaTime : 1f / 60f;

        // Handle input if available
        if (_inputManager != null)
        {
            UpdateInput();
        }

        // Start new ImGui frame
        ImGui.NewFrame();

        // Process draw commands
        ProcessDrawCommands();
    }

    /// <inheritdoc/>
    public override void EndDraw()
    {
        if (!_initialized) return;

        try
        {
            ImGui.Render();
            RenderImGuiDrawData();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Error in ImGui EndDraw: {ex.Message}");
        }
    }

    private unsafe void RenderImGuiDrawData()
    {
        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount == 0 || _commandList == null || _imguiShader == null || _pipelineState == null)
            return;

        // Set up projection matrix
        var clientBounds = Game.Window.ClientBounds;
        var projMatrix = Matrix.OrthoRH(clientBounds.Width, -clientBounds.Height, -1, 1);

        // Set pipeline state
        _commandList.SetPipelineState(_pipelineState);

        // Set shader parameters using the existing ImGui shader keys
        try
        {
            _imguiShader.Parameters.Set(ImGuiNetShaderKeys.proj, ref projMatrix);
            _imguiShader.Parameters.Set(ImGuiNetShaderKeys.tex, _fontTexture);
        }
        catch
        {
            // Fallback to string-based parameter setting
            Logger.Warning("Using fallback shader parameter setting");
            return; // Skip rendering if we can't set parameters
        }

        _imguiShader.Apply(_graphicsContext);

        // Render each command list
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            // Update vertex buffer if needed
            if (cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>() > _vertexBinding.Buffer.SizeInBytes)
            {
                var newVertexBuffer = Stride.Graphics.Buffer.Vertex.New(_graphicsDevice,
                    cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>() * 2, GraphicsResourceUsage.Dynamic);
                _vertexBinding = new VertexBufferBinding(newVertexBuffer, _vertexLayout, 0);
            }

            // Update index buffer if needed
            if (cmdList.IdxBuffer.Size * sizeof(ushort) > _indexBinding!.Buffer.SizeInBytes)
            {
                var newIndexBuffer = Stride.Graphics.Buffer.Index.New(_graphicsDevice,
                    cmdList.IdxBuffer.Size * sizeof(ushort) * 2, GraphicsResourceUsage.Dynamic);
                _indexBinding = new IndexBufferBinding(newIndexBuffer, false, 0);
            }

            // Upload vertex and index data
            _vertexBinding.Buffer.SetData(_commandList,
                new DataPointer(cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()));
            _indexBinding.Buffer.SetData(_commandList,
                new DataPointer(cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort)));

            // Set buffers
            _commandList.SetVertexBuffer(0, _vertexBinding.Buffer, 0, Unsafe.SizeOf<ImDrawVert>());
            _commandList.SetIndexBuffer(_indexBinding.Buffer, 0, false);

            // Render draw commands
            int idxOffset = 0;
            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var cmd = cmdList.CmdBuffer[i];

                // Set scissor rectangle
                _commandList.SetScissorRectangle(new Rectangle(
                    (int)cmd.ClipRect.X,
                    (int)cmd.ClipRect.Y,
                    (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                    (int)(cmd.ClipRect.W - cmd.ClipRect.Y)
                ));

                // Draw indexed
                _commandList.DrawIndexed((int)cmd.ElemCount, idxOffset, 0);
                idxOffset += (int)cmd.ElemCount;
            }
        }
    }

    private void UpdateInput()
    {
        if (_inputManager == null) return;

        var io = ImGui.GetIO();

        // Update mouse position
        if (_inputManager.HasMouse && !_inputManager.IsMousePositionLocked)
        {
            var mousePos = _inputManager.AbsoluteMousePosition;
            io.MousePos = new Vector2(mousePos.X, mousePos.Y);

            // Mouse buttons
            io.MouseDown[0] = _inputManager.IsMouseButtonDown(MouseButton.Left);
            io.MouseDown[1] = _inputManager.IsMouseButtonDown(MouseButton.Right);
            io.MouseDown[2] = _inputManager.IsMouseButtonDown(MouseButton.Middle);
        }

        // Handle input events
        foreach (var inputEvent in _inputManager.Events)
        {
            switch (inputEvent)
            {
                case TextInputEvent textEvent:
                    if (textEvent.Text != "\t")
                        ImGui.GetIO().AddInputCharactersUTF8(textEvent.Text);
                    break;

                case MouseWheelEvent wheelEvent:
                    io.MouseWheel += wheelEvent.WheelDelta;
                    break;
            }
        }

        // Modifier keys
        io.KeyAlt = _inputManager.IsKeyDown(Keys.LeftAlt) || _inputManager.IsKeyDown(Keys.RightAlt);
        io.KeyShift = _inputManager.IsKeyDown(Keys.LeftShift) || _inputManager.IsKeyDown(Keys.RightShift);
        io.KeyCtrl = _inputManager.IsKeyDown(Keys.LeftCtrl) || _inputManager.IsKeyDown(Keys.RightCtrl);
        io.KeySuper = _inputManager.IsKeyDown(Keys.LeftWin) || _inputManager.IsKeyDown(Keys.RightWin);
    }

    private void ProcessDrawCommands()
    {
        if (!_showUI || _drawCommands.Count == 0) return;

        // Create a single, fullscreen transparent overlay window and place all items into it
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(io.DisplaySize);
        ImGui.SetNextWindowBgAlpha(0.0f); // no background without needing NoBackground flag

        ImGui.Begin("Overlay",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings);

        foreach (var command in _drawCommands)
        {
            Vector2 screenPos = command.Type == DrawCommandType.ScreenText
                ? command.ScreenPosition
                : WorldToScreen(command.WorldPosition);

            ImGui.SetCursorPos(screenPos);
            ImGui.TextColored(command.Color, command.Message);
        }

        ImGui.End();

        // Clear commands for next frame
        _drawCommands.Clear();
    }

    private Vector2 WorldToScreen(Vector3 worldPosition)
    {
        if (_camera is null) return Vector2.Zero;

        var result = _camera.WorldToScreenPoint(ref worldPosition, GraphicsDevice);

        return result;
    }

    /// <inheritdoc/>
    protected override void Destroy()
    {
        _fontTexture?.Dispose();
        _fontTexture = null;
        _vertexBinding.Buffer?.Dispose();
        _indexBinding?.Buffer?.Dispose();
        _imguiShader?.Dispose();

        if (_initialized && _context != IntPtr.Zero)
        {
            ImGui.DestroyContext(_context);
        }
        base.Destroy();
    }

    private enum DrawCommandType
    {
        ScreenText,
        WorldText
    }

    private struct DrawCommand
    {
        public DrawCommandType Type;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public string Message;
        public Vector4 Color;
    }
}