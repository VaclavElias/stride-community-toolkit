using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Example_2D_Playground;

public class SpriteBatchRenderer : SyncScript
{
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;
    private Texture? _colorTexture;
    private readonly string _text = "This text is in Arial 20 with anti-alias\nand multiline...";
    private DelegateSceneRenderer? _sceneRenderer;
    private RenderDrawContext? _ctx;

    public override void Start()
    {
        _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
        _font = Content.Load<SpriteFont>("/Stride.Engine/StrideDefaultFont");
        _sceneRenderer = new DelegateSceneRenderer(Draw);
        _ctx = new RenderDrawContext(Services, RenderContext.GetShared(Services), Game.GraphicsContext);
        _colorTexture = Texture.New2D(GraphicsDevice, 1, 1, PixelFormat.R8G8B8A8_UNorm, [Color.White]);
    }

    public override void Cancel()
    {
        _spriteBatch?.Dispose();
        _spriteBatch = null;
        _colorTexture?.Dispose();
        _colorTexture = null;
    }

    public override void Update()
    {
        if (_spriteBatch is null || _font is null || _colorTexture is null || _sceneRenderer is null || _ctx is null) return;

        // don't forget the begin
        _spriteBatch.Begin(Game.GraphicsContext);

        // draw the text "Hello World!" in red from the center of the screen
        _spriteBatch.DrawString(_font, "Hello World!", new Vector2(0.5f, 0.5f), Color.Red);

        // don't forget the end
        _spriteBatch.End();

        _sceneRenderer.Draw(_ctx);

        // This Begin draws in screen space. The overload below draws the same batch in world space,
        // so the text sits in the scene rather than on the window - it needs the scene camera:
        //
        //   var camera = Entity.Scene.Entities.FirstOrDefault(x => x.Get<CameraComponent>() != null)?.Get<CameraComponent>();
        //   var textureToWorldSpace = Matrix.RotationX(MathUtil.Pi) * Matrix.Translation(0, 0, 0.25f);
        //   _spriteBatch.Begin(Game.GraphicsContext, textureToWorldSpace * camera.ViewMatrix, camera.ProjectionMatrix,
        //       SpriteSortMode.BackToFront, BlendStates.AlphaBlend, GraphicsDevice.SamplerStates.LinearClamp,
        //       DepthStencilStates.None);
        _spriteBatch.Begin(Game.GraphicsContext);

        var dim = _font.MeasureString(_text);

        int x = 20, y = 20;

        _spriteBatch.Draw(_colorTexture, new Rectangle(x, y, (int)dim.X, (int)dim.Y), Color.Green);
        _font.PreGenerateGlyphs(_text, _font.Size * Vector2.One);
        _spriteBatch.DrawString(_font, _text, new Vector2(x, y), Color.White);

        _spriteBatch.End();
    }

    private void Draw(RenderDrawContext ctx)
    {
        if (_spriteBatch is null || _font is null) return;

        // don't forget the begin
        _spriteBatch.Begin(Game.GraphicsContext);

        // draw the text "Hello World!" in red from the center of the screen
        _spriteBatch.DrawString(_font, "Hello World!", new Vector2(0.5f, 0.5f), Color.Red);

        // don't forget the end
        _spriteBatch.End();
    }
}