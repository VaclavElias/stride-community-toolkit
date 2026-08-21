using Example_CubicleCalamity.Components;
using Example_CubicleCalamity.Scripts;
using Example_CubicleCalamity.Shared;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Games;
using Stride.CommunityToolkit.Graphics;
using Stride.CommunityToolkit.Renderers;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Colors;
using Stride.Rendering.Lights;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

namespace Example_CubicleCalamity;

public class CubeStacker
{
    private const int Seed = 1;
    private Vector3 _startPosition = new(-4, 1, -4);

    private readonly Game _game;
    private readonly Random _random = new(Seed);
    private readonly Dictionary<Color, Material> _materials = [];
    private double _elapsedTime;
    private int _layer = 1;
    private bool _layersCreated;
    private BepuSimulation? _simulation;
    private Scene? _scene;

    public CubeStacker(Game game) => _game = game;

    public void Start(Scene scene)
    {
        //_game.SetupBase3DScene();
        _game.Window.AllowUserResizing = true;
        _game.AddGraphicsCompositor().AddCleanUIStage();
        AddCamera();
        //_game.AddEntityDebugSceneRenderer(new()
        //{
        //    ShowFontBackground = false
        //});
        _game.AddSceneRenderer(new EntityTextRenderer());
        _game.AddDirectionalLight();
        _game.Add3DGround();
        _game.AddProfiler();
        _scene = scene;

        AddMaterials();
        AddGizmo(scene);

        //_translationGizmo = new TranslationGizmo(_game.GraphicsDevice);
        //var gizmoEntity = _translationGizmo.Create(scene);
        //gizmoEntity.Transform.Position = new Vector3(-10, 0, 0);

        AddAllDirectionLighting(intensity: 5f);
        AddNewFirstLayer(_startPosition);
        AddFirstLayer(0.5f);
        AddGameManagerEntity();
        AddTotalScoreEntity();

        var camera = _scene.GetCamera();
        camera?.Entity.Add(new CameraRotationScript { RotationCentre = Constants.PlatformCentre });

        _simulation = camera?.Entity.GetSimulation();

        ConfigureSolverForLockedStacks();
    }

    /// <summary>
    /// Raises the solver's substep count, which is what stops a rotation-locked stack from jittering.
    /// </summary>
    /// <remarks>
    /// Locking rotation makes the four contact points on each cube face linearly dependent - they all
    /// control the same single linear degree of freedom - and a single-substep solve cannot converge
    /// them. The stack never settles, so it never sleeps, and the residual impulses read as boiling.
    /// <para>
    /// Measured on a headless 10x10x10 replica of this scene using Stride's defaults: at one substep
    /// all 1000 bodies stay awake with an RMS vertical velocity of 0.166; at two substeps every one
    /// sleeps at 0.00001, and a 15 second run takes 119 ms rather than 1308 ms - sleeping saves far
    /// more than the extra substep costs. Contact spring settings and MaximumRecoveryVelocity made
    /// no useful difference, and an unlocked stack settles fine at one substep, which is what
    /// identifies the rotation lock as the thing being paid for here.
    /// </para>
    /// <para>
    /// Stride's SoftStart temporarily multiplies this by <see cref="BepuSimulation.SoftStartSubstepFactor"/>
    /// and divides it back afterwards, so setting it here round-trips correctly.
    /// </para>
    /// </remarks>
    private void ConfigureSolverForLockedStacks()
    {
        if (_simulation is null) return;

        _simulation.Simulation.Solver.SubstepCount = 2;
    }

    /// <summary>
    /// Adds the camera, aimed at the middle of the platform.
    /// </summary>
    /// <remarks>
    /// Two ordering details matter here.
    /// <para>
    /// <see cref="TransformExtensions.LookAt(Stride.Engine.TransformComponent, Vector3, Vector3, float)"/>
    /// takes the eye position from <c>Transform.LocalMatrix</c> rather than from
    /// <c>Transform.Position</c>, and that matrix is still identity until the transform is updated.
    /// Without the explicit refresh the camera would be treated as sitting at the origin, looking
    /// straight up at a target directly above it - a degenerate rotation, and a blank screen.
    /// </para>
    /// <para>
    /// The aiming also has to happen before the controller is attached, because
    /// <c>Basic3DCameraController.Start</c> caches the transform it finds as the pose that H
    /// restores. Doing it in this order means H resets to a view of the platform too.
    /// </para>
    /// </remarks>
    private void AddCamera()
    {
        var camera = _game.Add3DCamera();

        camera.Transform.UpdateWorldMatrix();
        camera.Transform.LookAt(Constants.PlatformCentre, Vector3.UnitY);

        camera.Add3DCameraController();
    }

    private void AddGizmo(Scene scene)
    {
        var entity = new Entity("MyGizmo");
        entity.AddGizmo(_game.GraphicsDevice, showAxisName: true);
        entity.Transform.Position = new Vector3(-7.5f, 1, -7.5f);
        entity.Scene = scene;
    }

    private void AddGameManagerEntity()
    {
        var entity = new Entity("GameManager")
        {
            new RaycastInteractionScript()
        };
        entity.Scene = _scene;
    }

    private void AddTotalScoreEntity()
    {
        var entity = new Entity(Constants.TotalScore)
        {
            new EntityTextComponent()
            {
                Text = "Total Score: 0",
                FontSize = 20,
                Position = new Vector2(0, 20),
                TextColor = new Color(255, 255, 255),
            }
        };

        entity.Scene = _scene;
    }

    public void Update(Scene scene, GameTime time)
    {
        _elapsedTime += time.Elapsed.TotalSeconds;

        if (_elapsedTime >= Constants.Interval && _layer <= Constants.MaxLayers - 1)
        {
            _elapsedTime = 0;

            CreateCubeLayer(_layer + 0.5f);

            _layer++;
        }

        if (!_layersCreated && _layer == Constants.MaxLayers)
        {
            _layersCreated = true;

            foreach (var cube in scene.Entities)
            {
                if (cube.Name != "Cube") continue;

                var body = cube.Get<SlidingCubeComponent>();

                if (body == null) continue;

                body.Kinematic = false;

                // Going dynamic re-applies the shape inertia, which undoes the rotation lock.
                // SimulationUpdate would catch it on the next step anyway; doing it here closes the
                // one step of freedom in between.
                body.ApplyRotationLock();
            }
        }
    }

    private void AddMaterials()
    {
        foreach (var color in Constants.Colours)
        {
            var material = CreateMaterial(color, specular: 0);

            _materials.Add(color, material);
        }
    }

    public Material CreateMaterial(Color? color = null, float specular = 1.0f, float microSurface = 0.65f)
    {
        var lightmapMaterial = new MaterialDescriptor
        {
            Attributes =
                {
                    Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color ?? GameDefaults.DefaultMaterialColor)),
                    DiffuseModel = new MaterialLightmapModelFeature()
                    {
                        Intensity = 20,
                        LightMap = new ComputeColor(color ?? GameDefaults.DefaultMaterialColor)
                    },
                    Specular =  new MaterialMetalnessMapFeature(new ComputeFloat(specular)),
                    SpecularModel = new MaterialSpecularMicrofacetModelFeature(),
                    MicroSurface = new MaterialGlossinessMapFeature(new ComputeFloat(microSurface))
                }
        };

        return Material.New(_game.GraphicsDevice, lightmapMaterial);
    }

    public Material CreateMaterial2(Color? color = null, float specular = 1.0f, float microSurface = 0.65f)
    {
        var materialDescription = new MaterialDescriptor
        {
            Attributes =
                {
                    Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color ?? GameDefaults.DefaultMaterialColor)),
                    DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                    Specular =  new MaterialMetalnessMapFeature(new ComputeFloat(0)),
                    SpecularModel = new MaterialSpecularMicrofacetModelFeature()
                    {
                        Fresnel = new MaterialSpecularMicrofacetFresnelSchlick(),
                        Visibility = new MaterialSpecularMicrofacetVisibilitySmithSchlickGGX(),
                        NormalDistribution = new MaterialSpecularMicrofacetNormalDistributionGGX(),
                        Environment = new MaterialSpecularMicrofacetEnvironmentGGXLUT(),
                    },
                    MicroSurface = new MaterialGlossinessMapFeature(new ComputeFloat(0)),
                }
        };

        // from toolkit Stride.CommunityToolkit.Graphics
        var windowSize = _game.GraphicsDevice.GetWindowSize();

        // from Stride.Graphics
        var whiteTexture = _game.GraphicsDevice.GetSharedWhiteTexture();

        return Material.New(_game.GraphicsDevice, materialDescription);
        //options.Size /= 2;
    }

    private void AddNewFirstLayer(Vector3 startPosition)
    {
        var cube = _game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions()
        {
            EntityName = "Cube1",
            Material = _materials[Constants.Colours[0]],
            Size = Constants.CubeSize
        });
        cube.Transform.Position = startPosition;
        cube.Scene = _scene;
    }

    private void AddFirstLayer(float y) => CreateCubeLayer(y);

    private void CreateCubeLayer(float y)
    {
        for (var x = 0; x < Constants.Rows; x++)
        {
            for (var z = 0; z < Constants.Rows; z++)
            {
                var entity = CreateCube(Constants.CubeSize);

                var position = new Vector3(x, y, z) * Constants.CubeSize;

                // Centre the platform on the ground rather than letting it grow out of one corner
                position.X += Constants.GridOrigin;
                position.Z += Constants.GridOrigin;

                entity.Transform.Position = position;

                AddCollider(entity);

                entity.Scene = _scene;

                //entity.AddGizmo(_game.GraphicsDevice);

                //entity.Transform.Children.Add(_translationGizmo.Create(scene).Transform);
            }
        }
    }

    private static void AddCollider(Entity entity)
    {
        // A single BoxCollider still has to be wrapped: ColliderBase does not implement ICollider,
        // only CompoundCollider, MeshCollider and EmptyCollider do.
        var compoundCollider = new CompoundCollider();

        compoundCollider.Colliders.Add(new BoxCollider
        {
            Size = Constants.CubeSize,
            // Was 1e9. All cubes shared it so the mass ratios were fine, but it also scales the
            // inertia tensor and puts contact impulses nine orders of magnitude away from where
            // Bepu's absolute epsilons and sleep thresholds are tuned.
            Mass = 1,
        });

        // Kinematic until the whole tower is built, so layers hang in the air while they spawn.
        // Nothing here may touch BodyInertia or the velocities: their setters no-op until the
        // component is added to an entity below, at which point SlidingCubeComponent.AttachInner
        // takes over and locks rotation.
        entity.Add(new SlidingCubeComponent
        {
            Collider = compoundCollider,
            Kinematic = true,
        });
    }

    private Entity CreateCube(Vector3 size)
    {
        var color = Constants.Colours[_random.Next(0, Constants.Colours.Count)];

        var entity = _game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions()
        {
            EntityName = "Cube",
            Material = _materials[color],
            Size = size
        });

        entity.Add(new CubeComponent(color));

        return entity;
    }

    public void AddAllDirectionLighting(float intensity, bool showLightGizmo = true)
    {
        var position = new Vector3(7f, 2f, 0);

        CreateLightEntity(GetLight(), intensity, position);

        CreateLightEntity(GetLight(), intensity, position, Quaternion.RotationAxis(Vector3.UnitX, MathUtil.DegreesToRadians(180)));

        CreateLightEntity(GetLight(), intensity, position, Quaternion.RotationAxis(Vector3.UnitX, MathUtil.DegreesToRadians(270)));

        CreateLightEntity(GetLight(), intensity, position, Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(90)));

        CreateLightEntity(GetLight(), intensity, position, Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(270)));

        LightDirectional GetLight() => new() { Color = GetColor(Color.White) };

        static ColorRgbProvider GetColor(Color color) => new(color);

        void CreateLightEntity(ILight light, float intensity, Vector3 position, Quaternion? rotation = null)
        {
            var entity = new Entity() {
                new LightComponent {
                    Intensity =  intensity,
                    Type = light
                }};

            entity.Transform.Position = position;
            entity.Transform.Rotation = rotation ?? Quaternion.Identity;
            entity.Scene = _scene;

            if (showLightGizmo)
                entity.AddLightDirectionalGizmo(_game.GraphicsDevice);
        }
    }
}
