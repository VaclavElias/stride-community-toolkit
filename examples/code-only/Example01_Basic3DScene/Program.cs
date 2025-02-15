using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Utilities;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    game.SetupBase3DScene();
    game.AddSkybox();

    //var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);

    //entity.Transform.Position = new Vector3(0, 8, 0);

    //entity.Scene = rootScene;

    CreateMeshEntity(game.GraphicsDevice, rootScene, new(0, 1, 0), b => BuildCylinderMesh(b, 1000, 0.5f, 8));
});

void BuildCylinderMesh(MeshBuilder meshBuilder, int segments, float trad, float tlen)
{
    meshBuilder.WithIndexType(IndexingType.Int16);
    meshBuilder.WithPrimitiveType(PrimitiveType.TriangleList);

    var position = meshBuilder.WithPosition<Vector3>();
    var normal = meshBuilder.WithNormal<Vector3>();

    //end 1
    for (var i = 0; i < segments; i++)
    {
        var x = trad * (float)Math.Sin(Math.Tau / segments * i);
        var y = trad * (float)Math.Cos(Math.Tau / segments * i);

        meshBuilder.AddVertex();
        meshBuilder.SetElement(position, new Vector3(x, y, 0));
        meshBuilder.SetElement(normal, new Vector3(x, y, 0));
    }

    //end 2
    for (var i = 0; i < segments; i++)
    {
        var x = trad * (float)Math.Sin(Math.Tau / segments * i);
        var y = trad * (float)Math.Cos(Math.Tau / segments * i);

        meshBuilder.AddVertex();
        meshBuilder.SetElement(position, new Vector3(x, y, tlen));
        meshBuilder.SetElement(normal, new Vector3(x, y, 0));
    }


    for (var i = 0; i < segments; i++)
    {
        var i_next = (i + 1) % segments;

        //triangle 1
        meshBuilder.AddIndex(i);
        meshBuilder.AddIndex(i_next + segments);
        meshBuilder.AddIndex(i + segments);

        //triangle 2
        meshBuilder.AddIndex(i);
        meshBuilder.AddIndex(i_next);
        meshBuilder.AddIndex(i_next + segments);
    }

    //make circles to close ends of cylinder (as backculling hides interior faces)

    //End 1
    //centre
    meshBuilder.AddVertex();
    meshBuilder.SetElement(position, new Vector3(0f, 0f, 0f));
    meshBuilder.SetElement(normal, new Vector3(0, 0, -1));

    //triangle eles
    for (var i = 0; i < segments; i++)
    {
        //build triangles
        meshBuilder.AddIndex((i + 1) % segments);  //% means modulo (remainder), gives 1,2..segments-1,0
        meshBuilder.AddIndex(i);
        meshBuilder.AddIndex(2 * segments);  //centre
    }

    //End 2
    //centre
    meshBuilder.AddVertex();  //vertex index 2*segments + 1
    meshBuilder.SetElement(position, new Vector3(0f, 0f, tlen));
    meshBuilder.SetElement(normal, new Vector3(0, 0, 1));

    //triangle eles
    for (var i = 0; i < segments; i++)
    {
        //build triangles. % means modulo (remainder), gives 1,2..segments-1,0
        meshBuilder.AddIndex(((i + 1) % segments) + segments);
        meshBuilder.AddIndex(2 * segments + 1);  //centre
        meshBuilder.AddIndex(i + segments);
    }

}

Entity CreateMeshEntity(GraphicsDevice graphicsDevice, Scene rootScene, Vector3 position, Action<MeshBuilder> build)
{
    using var meshBuilder = new MeshBuilder();

    build(meshBuilder);

    var entity = new Entity { Scene = rootScene, Transform = { Position = position } };

    var material = Material.New(game.GraphicsDevice, new()
    {
        Attributes = new()
        {
            MicroSurface = new MaterialGlossinessMapFeature
            {
                GlossinessMap = new ComputeFloat(0.9f)
            },
            Diffuse = new MaterialDiffuseMapFeature
            {
                DiffuseMap = new ComputeColor(new Color4(1, 0.3f, 0.5f, 1))
            },
            DiffuseModel = new MaterialDiffuseLambertModelFeature(),
            Specular = new MaterialMetalnessMapFeature
            {
                MetalnessMap = new ComputeFloat(0.0f)
            },
            SpecularModel = new MaterialSpecularMicrofacetModelFeature
            {
                Environment = new MaterialSpecularMicrofacetEnvironmentGGXPolynomial()
            },
        }
    });

    var model = new Model
    {
        new MaterialInstance { Material = material },
        new Mesh {
            Draw = meshBuilder.ToMeshDraw(graphicsDevice),
            MaterialIndex = 0
        }
    };

    entity.Add(new ModelComponent { Model = model });

    return entity;
}