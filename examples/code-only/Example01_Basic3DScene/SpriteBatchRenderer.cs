using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Example01_Basic3DScene;

public class SpriteBatchRenderer : SyncScript
{
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;
    private Texture? _texture;
    private float _fontSize = 25;
    private string _text = "This text is in Arial 20 with anti-alias\nand multiline...";
    private DelegateSceneRenderer? _sceneRenderer;
    private CameraComponent? _camera = null;

    public override void Start()
    {
        _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
        _font = Content.Load<SpriteFont>("StrideDefaultFont");
        //_ctx = new RenderDrawContext(Services, RenderContext.GetShared(Services), Game.GraphicsContext);
        _sceneRenderer = new DelegateSceneRenderer(Draw);
        _camera = SceneSystem.SceneInstance.RootScene.Entities.FirstOrDefault(x => x.Get<CameraComponent>() != null)?.Get<CameraComponent>();

        var renderCollection = (SceneRendererCollection)SceneSystem.GraphicsCompositor.Game;

        renderCollection.Add(_sceneRenderer);

        //_texture = Content.Load<Texture>("Path to your texture asset");
    }

    public override void Update()
    {
        //_sceneRenderer.Draw(_ctx);

        //var spriteBatch = new SpriteBatch(GraphicsDevice);

        //// don't forget the begin
        //spriteBatch.Begin(Game.GraphicsContext);

        //// draw the text "Helloworld!" in red from the center of the screen
        //spriteBatch.DrawString(_font, "Hello World!", new Vector2(200, 200), Color.Red);

        //// don't forget the end
        //spriteBatch.End();


        //_spriteBatch.Begin(Game.GraphicsContext);

        //_spriteBatch.DrawString(_font, _text, new Vector2(300, 300), Color.White);

        //_spriteBatch.End();
    }

    private void Draw(RenderDrawContext drawContext)
    {
        var spriteBatch = new SpriteBatch(GraphicsDevice);

        var screen = _camera.WorldToScreenPoint(ref Entity.Transform.Position, GraphicsDevice);

        spriteBatch.Begin(drawContext.GraphicsContext);
        spriteBatch.DrawString(_font, "Hello World 2", 20, screen + new Vector2(0, -50), Color.Red);
        spriteBatch.End();
    }
}