using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Example01_Basic3DScene;

public class MyCustomRenderer : SceneRendererBase
{
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;

    protected override void InitializeCore()
    {
        base.InitializeCore();

        _font = Content.Load<SpriteFont>("StrideDefaultFont");
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
    {
        // Access to the graphics device
        //var graphicsDevice = drawContext.GraphicsDevice;
        //var commandList = drawContext.CommandList;
        // Clears the current render target
        //commandList.Clear(commandList.RenderTargets[0], Color.CornflowerBlue);

        //_spriteBatch = new SpriteBatch(graphicsDevice);
        _spriteBatch.Begin(drawContext.GraphicsContext);
        _spriteBatch.DrawString(_font, "Hello World 1.2", 20, new Vector2(100, 100), Color.White);
        //_spriteBatch.Draw(_texture, Vector2.Zero);
        _spriteBatch.End();
    }
}
