using Example_CubicleCalamity.Components;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Rendering.Utilities;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Example_CubicleCalamity.Setup;

/// <summary>
/// Drops a word into the scene as solid 3D letters with physics, one tumbling body per letter.
/// </summary>
/// <remarks>
/// <para>
/// This is the decorative use of the toolkit's extruded letter meshes: real geometry that falls,
/// bounces off whatever is left of the platform and comes to rest among the cubes. Each letter is
/// its own entity with a <see cref="BodyComponent"/>, so the word breaks apart naturally as it lands.
/// </para>
/// <para>
/// The collider is a plain box around the glyph. A box is deliberate: a letter-shaped collider means
/// convex hulls, and extruded-polygon hulls colliding with each other are exactly the shape class
/// behind two documented Bepu failures (`notes/upstream/bepu-hull-contact-nan.md`). A pile of
/// letters jostling one another is the risky configuration, so they collide as boxes and nobody can
/// tell once they are lying in a heap.
/// </para>
/// </remarks>
public static class FallingLetters
{
    /// <summary>Glyph box width in glyph units, matching <see cref="LetterMeshFactory"/>.</summary>
    private const float GlyphWidth = 0.7f;

    /// <summary>Depth of the letter bodies, in world units.</summary>
    private const float Depth = 0.3f;

    /// <summary>
    /// Spawns a word above a point, letters spread across it, each falling under gravity.
    /// </summary>
    /// <param name="game">The running game.</param>
    /// <param name="scene">The scene the letters are added to.</param>
    /// <param name="word">The word to drop. Every character must have an authored glyph, or be a space.</param>
    /// <param name="centre">Where the middle of the word starts, in world units. Height included - drop from high for drama.</param>
    /// <param name="material">The material every letter shares.</param>
    /// <param name="seed">Seed for the small per-letter jitter, so a run is reproducible.</param>
    /// <param name="yaw">
    /// Rotation of the whole word about Y, in radians. Pass the direction toward the camera so the
    /// word spawns readable from wherever the player has orbited to - the letters face the camera at
    /// the moment they appear, and physics owns them from then on.
    /// </param>
    /// <remarks>
    /// Letters get a small random height offset and yaw so they land staggered and tumble
    /// individually instead of falling as a rigid signboard.
    /// </remarks>
    public static void SpawnWord(Game game, Scene scene, string word, Vector3 centre, Material material, float yaw = 0f, int seed = 7)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(material);

        var random = new Random(seed);
        var advance = GlyphWidth + 0.25f;

        // The word is laid out along its own X axis and then turned as one piece, so the line of
        // letters stays perpendicular to the viewer whichever way it faces
        var wordRotation = Quaternion.RotationY(yaw);
        var right = Vector3.Transform(Vector3.UnitX, wordRotation);
        var start = centre - right * ((word.Length - 1) * advance / 2);

        for (var i = 0; i < word.Length; i++)
        {
            var character = word[i];

            if (character == ' ') continue;

            var entity = new Entity($"Letter{character}")
            {
                new ModelComponent
                {
                    Model = new Model
                    {
                        new MaterialInstance { Material = material },
                        new Mesh
                        {
                            // Centred on the origin so the box collider below and the visible letter
                            // share a centre, which is also what makes the tumbling look right
                            Draw = LetterMeshFactory.CreateTextMeshDraw(game.GraphicsDevice, character.ToString(), depth: Depth, centerOrigin: true),
                            MaterialIndex = 0
                        }
                    }
                }
            };

            // SlowFallComponent rather than a plain BodyComponent: at full gravity the words drop
            // past the camera before anyone reads them. It falls under a fraction of gravity while
            // still accelerating, bouncing and tumbling like any rigid body.
            entity.Add(new SlowFallComponent
            {
                Collider = new CompoundCollider
                {
                    Colliders =
                    {
                        new BoxCollider
                        {
                            Size = new Vector3(GlyphWidth, 1f, Depth),
                            Mass = 1f,
                        }
                    }
                }
            });

            entity.Transform.Position = start
                + right * (i * advance)
                + new Vector3(0, (float)random.NextDouble() * 1.5f, 0);

            entity.Transform.Rotation = wordRotation * Quaternion.RotationY((float)(random.NextDouble() - 0.5) * 0.6f);

            entity.Scene = scene;
        }
    }
}
