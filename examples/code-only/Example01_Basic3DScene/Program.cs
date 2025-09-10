using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.ProceduralModels;

using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    game.SetupBase3DScene();
    game.AddSkybox();

    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);

    entity.Transform.Position = new Vector3(0, 8, 0);

    entity.Scene = rootScene;

    // Move to Example01_Basic3DScene_MeshDraw
    var vertices = new VertexPositionTexture[4];
    vertices[0].Position = new Vector3(0f, 0f, 1f);
    vertices[1].Position = new Vector3(0f, 1f, 0f);
    vertices[2].Position = new Vector3(0f, 1f, 1f);
    //vertices[3].Position = new Vector3(1f, 0f, 1f);
    var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(game.GraphicsDevice, vertices,
                                                         GraphicsResourceUsage.Dynamic);
    int[] indices = { 0, 2, 1 };
    var indexBuffer = Stride.Graphics.Buffer.Index.New(game.GraphicsDevice, indices);

    var customMesh = new Mesh
    {
        Draw = new MeshDraw
        {
            /* Vertex buffer and index buffer setup */
            PrimitiveType = PrimitiveType.TriangleList,
            DrawCount = indices.Length,
            IndexBuffer = new IndexBufferBinding(indexBuffer, true, indices.Length),
            VertexBuffers = new[] { new VertexBufferBinding(vertexBuffer,
                                  VertexPositionTexture.Layout, vertexBuffer.ElementCount) },
        }
    };

    var model = new Model();

    model.Meshes.Add(customMesh);

    model.Materials.Add(game.CreateMaterial(Color.BlueViolet));

    var entity3 = new Entity("Name", new Vector3(2f, 0, 2f));

    entity3.Components.Add(new ModelComponent(model));

    entity3.Scene = rootScene;

    var myModel = new MyProceduralModel();
    var model2 = myModel.Generate(game.Services);
    model2.Materials.Add(game.CreateMaterial(Color.Green));

    var meshEntity2 = new Entity("a", new Vector3(1, 1, 1));
    meshEntity2.Components.Add(new ModelComponent(model2));
    meshEntity2.Scene = rootScene;
});

public class MyProceduralModel : PrimitiveProceduralModelBase
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