using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Shapes;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace E13_SignalR.Station;

/// <summary>
/// The station itself: a deck with a lip on three sides and one open edge, a few structures behind
/// it, and the ShapeBatch markings - hatch ring, landing pad, corner ticks, the warning line along
/// the drop - that take their colour from the console scheme, so a scheme change shows in the world
/// and not only in the text.
/// </summary>
public sealed class StationScene(Game game)
{
    /// <summary>Half the deck's width and depth. The open edge is at +Z, facing the camera.</summary>
    public const float DeckHalf = 12f;

    private const float LipHeight = 1.2f;
    private const float LipThickness = 0.4f;
    private const float PadRadius = 6f;
    private const float Lift = 0.03f;

    private ShapeBatch? _shapes;
    private float _hatchPulse;

    public void Build(Scene scene)
    {
        game.SetupBase3D();
        game.Add3DCameraController();
        game.AddSkybox();

        // Looking down at the pad from the open side, so lost cargo slides towards the viewer
        game.SetCameraPosition(new(0, 35, 26));
        game.SetCameraRotation(new(0, -48, 0));

        // Depth-tested, so a container sitting on the pad ring covers it like anything else would
        _shapes = game.AddShapeBatch(depthTest: true);

        var deckMaterial = game.CreateMaterial(new Color(34, 37, 44), specular: 0.05f, microSurface: 0.25f);
        var lipMaterial = game.CreateMaterial(new Color(58, 62, 72), specular: 0.08f, microSurface: 0.3f);
        var hullMaterial = game.CreateMaterial(new Color(74, 80, 94), specular: 0.1f, microSurface: 0.35f);

        AddStatic(scene, "Deck", deckMaterial, new Vector3(DeckHalf * 2f, 0.5f, DeckHalf * 2f), new Vector3(0, -0.25f, 0));

        // The lip: back, left and right. No lip at the front - that is where cargo is lost.
        AddStatic(scene, "Lip back", lipMaterial, new Vector3(DeckHalf * 2f, LipHeight, LipThickness), new Vector3(0, LipHeight / 2f, -DeckHalf + LipThickness / 2f));
        AddStatic(scene, "Lip left", lipMaterial, new Vector3(LipThickness, LipHeight, DeckHalf * 2f), new Vector3(-DeckHalf + LipThickness / 2f, LipHeight / 2f, 0));
        AddStatic(scene, "Lip right", lipMaterial, new Vector3(LipThickness, LipHeight, DeckHalf * 2f), new Vector3(DeckHalf - LipThickness / 2f, LipHeight / 2f, 0));

        // Station structure behind the deck, for the skybox to sit behind and the glow to catch
        AddStatic(scene, "Tower left", hullMaterial, new Vector3(3f, 9f, 3f), new Vector3(-9f, 4.5f, -15f));
        AddStatic(scene, "Tower right", hullMaterial, new Vector3(3f, 13f, 3f), new Vector3(9f, 6.5f, -15f));
        AddStatic(scene, "Bulkhead", hullMaterial, new Vector3(8f, 6f, 2f), new Vector3(0, 3f, -16f));

        // The gantry the hatch hangs from
        AddStatic(scene, "Gantry", hullMaterial, new Vector3(0.6f, 0.6f, 16f), new Vector3(0, Deck.HatchHeight + 1.5f, -8f));
        AddStatic(scene, "Gantry post", hullMaterial, new Vector3(0.6f, Deck.HatchHeight + 1.5f, 0.6f), new Vector3(0, (Deck.HatchHeight + 1.5f) / 2f, -16f));
    }

    /// <summary>Lights the hatch ring for a moment. Called on every release.</summary>
    public void Pulse() => _hatchPulse = 1f;

    /// <summary>The markings, resubmitted every frame in the scheme's colours.</summary>
    public void Draw(StationConsole console, float deltaSeconds, float time)
    {
        if (_shapes is null) return;

        _hatchPulse = MathF.Max(0f, _hatchPulse - deltaSeconds * 2f);

        var accent = console.Accent;
        var glow = console.Glow;
        var dim = Hex.WithAlpha(accent, 90);

        _shapes.BorderWidth = 1.5f;
        _shapes.Fill.Set(null, 0f);

        // Landing pad: a ring, and a dashed ring outside it that turns
        _shapes.Glow.Set(6f, Hex.WithAlpha(glow, 140));
        _shapes.DrawRing(new Vector3(0, Lift, 0), Vector3.UnitY, PadRadius, accent);
        _shapes.Glow.Clear();

        _shapes.Dash.Set(10f, 8f, time * 20f);
        _shapes.DrawRing(new Vector3(0, Lift, 0), Vector3.UnitY, PadRadius + 1.2f, dim);
        _shapes.Dash.Clear();

        // Corner ticks on the deck
        const float Tick = 1.5f;
        var inset = DeckHalf - LipThickness - 0.3f;

        foreach (var (signX, signZ) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var corner = new Vector3(signX * inset, Lift, signZ * inset);

            _shapes.DrawPixelLine(corner, corner - new Vector3(signX * Tick, 0, 0), 2f, accent);
            _shapes.DrawPixelLine(corner, corner - new Vector3(0, 0, signZ * Tick), 2f, accent);
        }

        // The open edge, marked as the hazard it is - in the scheme's own colour, dashed and marching
        _shapes.Dash.Set(14f, 10f, -time * 40f);
        _shapes.DrawPixelLine(new Vector3(-inset, Lift, DeckHalf - 0.3f), new Vector3(inset, Lift, DeckHalf - 0.3f), 2.5f, accent);
        _shapes.Dash.Clear();

        // The hatch: a ring that brightens and widens for a moment when cargo drops through it
        var hatch = new Vector3(0, Deck.HatchHeight, 0);

        _shapes.Glow.Set(6f + _hatchPulse * 18f, Hex.WithAlpha(glow, (byte)(120 + _hatchPulse * 135)));
        _shapes.DrawRing(hatch, Vector3.UnitY, 3f + _hatchPulse * 0.4f, accent);
        _shapes.Glow.Clear();

        _shapes.Dash.Set(6f, 6f, -time * 15f);
        _shapes.DrawRing(hatch, Vector3.UnitY, 3.6f, dim);
        _shapes.Dash.Clear();

        // A guide from the hatch to the pad, faint, so the drop reads as a path
        _shapes.Dash.Set(4f, 12f, time * 30f);
        _shapes.DrawPixelLine(hatch, new Vector3(0, Lift, 0), 1f, dim);
        _shapes.Dash.Clear();
    }

    private void AddStatic(Scene scene, string name, Material material, Vector3 size, Vector3 position)
    {
        var entity = game.Create3DPrimitive(PrimitiveModelType.Cube, new Bepu3DPhysicsOptions
        {
            EntityName = name,
            Material = material,
            Size = size,
            Position = position,
            Component = new StaticComponent { Collider = new CompoundCollider() },
        });

        entity.Scene = scene;
    }
}