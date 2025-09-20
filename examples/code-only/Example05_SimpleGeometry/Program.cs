using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.ProceduralModels;

using var game = new Game();

game.Run(start: scene =>
{
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddGroundGizmo(new(-4, 0, -4), showAxisName: true);
    game.AddProfiler();

    AddTriangleEntity(scene);

    var myModel = new QuadPrimitiveModel();
    var model2 = myModel.Generate(game.Services);
    model2.Materials.Add(game.CreateFlatMaterial(Color.Green));

    var meshEntity2 = new Entity("a", new Vector3(1, 1, 1));
    meshEntity2.Components.Add(new ModelComponent(model2));
    meshEntity2.Scene = scene;
});

// Create and add a simple triangle entity to the scene
void AddTriangleEntity(Scene scene)
{
    const float startX = -4f;
    const float startZ = -1f;

    var vertices = new VertexPositionTexture[3];
    vertices[0].Position = new Vector3(startX, 0f, startZ);
    vertices[1].Position = new Vector3(startX + 0.5f, 1f, startZ);
    vertices[2].Position = new Vector3(startX + 1f, 0f, startZ);

    var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(game.GraphicsDevice, vertices, GraphicsResourceUsage.Dynamic);
    int[] indices = [0, 1, 2];
    var indexBuffer = Stride.Graphics.Buffer.Index.New(game.GraphicsDevice, indices);

    var mesh = new Mesh
    {
        Draw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.TriangleList,
            DrawCount = indices.Length,
            IndexBuffer = new IndexBufferBinding(indexBuffer, true, indices.Length),
            VertexBuffers = [ new VertexBufferBinding(vertexBuffer,
                                  VertexPositionTexture.Layout, vertexBuffer.ElementCount) ],
        }
    };

    var model = new Model() { Meshes = [mesh] };
    model.Materials.Add(game.CreateFlatMaterial(Color.BlueViolet));

    var entity = new Entity("Name", new Vector3(2f, 0, 2f));
    entity.Components.Add(new ModelComponent(model));
    entity.Scene = scene;
}

public class QuadPrimitiveModel : PrimitiveProceduralModelBase
{
    // A custom property that shows up in Game Studio
    /// <summary>
    /// Gets or sets the size of the model.
    /// </summary>
    public Vector3 Size { get; set; } = Vector3.One;

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        // First generate the arrays for vertices and indices with the correct size
        var vertexCount = 4;
        var indexCount = 6;
        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indexCount];

        // Create custom vertices, in this case just a quad facing in Y direction
        var normal = Vector3.UnitZ;
        vertices[0] = new VertexPositionNormalTexture(new Vector3(-0.5f, 0.5f, 0) * Size, normal, new Vector2(0, 0));
        vertices[1] = new VertexPositionNormalTexture(new Vector3(0.5f, 0.5f, 0) * Size, normal, new Vector2(1, 0));
        vertices[2] = new VertexPositionNormalTexture(new Vector3(-0.5f, -0.5f, 0) * Size, normal, new Vector2(0, 1));
        vertices[3] = new VertexPositionNormalTexture(new Vector3(0.5f, -0.5f, 0) * Size, normal, new Vector2(1, 1));

        // Create custom indices
        indices[0] = 0;
        indices[1] = 1;
        indices[2] = 2;
        indices[3] = 1;
        indices[4] = 3;
        indices[5] = 2;

        // Create the primitive object for further processing by the base class
        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false) { Name = "MyModel" };
    }
}