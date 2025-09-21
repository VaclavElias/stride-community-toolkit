using ImGuiNET;
using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;

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

    // ImGui.NET context
    private IntPtr _context;

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

    public override void Initialize()
    {
        base.Initialize();

        _inputManager = Services.GetService<InputManager>();
        _graphicsDevice = Services.GetService<IGraphicsDeviceService>()?.GraphicsDevice;
        var graphicsContext = Services.GetService<GraphicsContext>();
        _commandList = graphicsContext?.CommandList;

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

            // Set up display size
            var clientBounds = Game.Window.ClientBounds;
            io.DisplaySize = new Vector2(clientBounds.Width, clientBounds.Height);

            _initialized = true;

            // Register this system
            Services.AddService(this);
            Game.GameSystems.Add(this);

            Logger.Info("ImGuiNetSystem initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize ImGuiNetSystem: {ex.Message}");
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (!_initialized) return;

        var deltaTime = (float)gameTime.Elapsed.TotalSeconds;
        var io = ImGui.GetIO();

        // Update display size
        var clientBounds = Game.Window.ClientBounds;
        io.DisplaySize = new Vector2(clientBounds.Width, clientBounds.Height);
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

    public override void EndDraw()
    {
        if (!_initialized) return;

        try
        {
            ImGui.Render();
            // Note: In a full implementation, you'd need to render the ImGui draw data
            // For now, this demonstrates the API structure similar to Box2D.NET
        }
        catch (Exception ex)
        {
            Logger.Warning($"Error in ImGui EndDraw: {ex.Message}");
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

        // Create overlay window (similar to Box2D.NET approach)
        ImGui.Begin("Overlay",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar);

        foreach (var command in _drawCommands)
        {
            Vector2 screenPos;

            if (command.Type == DrawCommandType.ScreenText)
            {
                screenPos = command.ScreenPosition;
            }
            else
            {
                screenPos = WorldToScreen(command.WorldPosition);
            }

            ImGui.SetCursorPos(screenPos);
            ImGui.TextColored(command.Color, command.Message);
        }

        ImGui.End();

        // Clear commands for next frame
        _drawCommands.Clear();
    }

    private Vector2 WorldToScreen(Vector3 worldPosition)
    {
        // Get the main camera for world-to-screen conversion
        var sceneSystem = Services.GetService<SceneSystem>();
        var camera = sceneSystem?.SceneInstance?.RootScene?.Entities
            .SelectMany(e => e.GetAll<CameraComponent>())
            .FirstOrDefault();

        if (camera == null)
            return Vector2.Zero;

        // Simple approximation - in a full implementation, you'd use proper matrix transformations
        var screenX = worldPosition.X * 100 + Game.Window.ClientBounds.Width / 2f;
        var screenY = Game.Window.ClientBounds.Height / 2f - worldPosition.Y * 100;

        return new Vector2(screenX, screenY);
    }

    protected override void Destroy()
    {
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