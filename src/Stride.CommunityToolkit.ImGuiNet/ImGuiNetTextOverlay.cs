using ImGuiNET;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;

namespace Stride.CommunityToolkit.ImGuiNet;

/// <summary>
/// The Box2D.NET-style text overlay of <see cref="ImGuiNetSystem"/>: buffers the frame's DrawString
/// requests and draws them into one fullscreen, input-transparent ImGui window. Split out of
/// <see cref="ImGuiNetSystem"/>, which owns the frame and forwards its DrawString API here.
/// </summary>
internal sealed class ImGuiNetTextOverlay
{
    private readonly List<DrawCommand> _drawCommands = [];

    internal void AddScreenText(int x, int y, string message, Vector4? color)
    {
        _drawCommands.Add(new DrawCommand
        {
            Type = DrawCommandType.ScreenText,
            ScreenPosition = new Vector2(x, y),
            Message = message,
            Color = color ?? new Vector4(0.9f, 0.9f, 0.9f, 1.0f)
        });
    }

    internal void AddWorldText(Vector3 worldPosition, string message, Vector4? color)
    {
        _drawCommands.Add(new DrawCommand
        {
            Type = DrawCommandType.WorldText,
            WorldPosition = worldPosition,
            Message = message,
            Color = color ?? new Vector4(0.9f, 0.9f, 0.9f, 1.0f)
        });
    }

    /// <summary>Draws the buffered strings into one overlay window and clears the buffer for the next frame.</summary>
    internal void Draw(bool showUI, CameraComponent? camera, GraphicsDevice graphicsDevice)
    {
        if (!showUI || _drawCommands.Count == 0) return;

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
                : WorldToScreen(camera, graphicsDevice, command.WorldPosition);

            ImGui.SetCursorPos(screenPos);
            ImGui.TextColored(command.Color, command.Message);
        }

        ImGui.End();

        // Clear commands for next frame
        _drawCommands.Clear();
    }

    private static Vector2 WorldToScreen(CameraComponent? camera, GraphicsDevice graphicsDevice, Vector3 worldPosition)
    {
        if (camera is null) return Vector2.Zero;

        var result = camera.WorldToScreenPoint(ref worldPosition, graphicsDevice);

        return result;
    }

    private enum DrawCommandType
    {
        ScreenText,
        WorldText
    }

    private readonly record struct DrawCommand
    {
        internal DrawCommandType Type { get; init; }
        internal Vector2 ScreenPosition { get; init; }
        internal Vector3 WorldPosition { get; init; }
        internal required string Message { get; init; }
        internal Vector4 Color { get; init; }
    }
}