using Example_CubicleCalamity.Gameplay;
using Example_CubicleCalamity.Setup;
using Example_CubicleCalamity.Shared;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace Example_CubicleCalamity.Scripts;

/// <summary>
/// Turns a mouse click into a cleared group.
/// </summary>
/// <remarks>
/// This script does input and nothing else: raycast, ask <see cref="MatchFinder"/> what is connected,
/// hand the count to <see cref="ScoreKeeper"/>, tell <see cref="CubeGrid"/> to collapse, and spawn the
/// feedback. The rules themselves live in <c>Gameplay/</c> where they can be read and tested on their
/// own - this used to be one class doing input, matching, scoring, sound, popups and the running
/// total at once.
/// </remarks>
public class CubeClickScript : AsyncScript
{
    private readonly CubeGrid _grid;
    private readonly ScoreKeeper _keeper;
    private readonly GameAudio _audio;
    private readonly Random _drift = new(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="CubeClickScript"/> class.
    /// </summary>
    /// <param name="grid">The logical grid, which is the source of truth for what is where.</param>
    /// <param name="keeper">The running total and combo streak.</param>
    /// <param name="audio">The game's sound effects.</param>
    public CubeClickScript(CubeGrid grid, ScoreKeeper keeper, GameAudio audio)
    {
        _grid = grid;
        _keeper = keeper;
        _audio = audio;
    }

    /// <summary>
    /// Gets or sets the scoreboard, so a clear can make the total jump.
    /// </summary>
    public ScoreboardScript? Scoreboard { get; set; }

    /// <summary>
    /// Gets or sets the banner revealed when no clearable group is left.
    /// </summary>
    public EntityTextComponent? GameOverText { get; set; }

    /// <summary>
    /// Gets whether the board has run out of moves.
    /// </summary>
    public bool IsGameOver { get; private set; }

    /// <inheritdoc />
    public override async Task Execute()
    {
        var camera = Entity.Scene.GetCamera();

        if (camera is null) return;

        while (Game.IsRunning)
        {
            if (!IsGameOver && Input.HasMouse && IsClicking())
            {
                TryClear(camera);
            }

            await Script.NextFrame();
        }
    }

    /// <summary>
    /// Whether the player is asking to clear a cube this frame.
    /// </summary>
    /// <remarks>
    /// Holding shift turns the click into a repeat, for taking a board apart quickly. Note that shift
    /// is also the camera controller's speed modifier, so holding it moves the camera faster too.
    /// </remarks>
    private bool IsClicking()
        => Input.IsKeyDown(Keys.LeftShift) && Input.IsMouseButtonDown(MouseButton.Left)
        || Input.IsMouseButtonPressed(MouseButton.Left);

    private void TryClear(CameraComponent camera)
    {
        if (!camera.RaycastMouse(this, 100, out var hitInfo)) return;

        var cube = hitInfo.Collidable.Entity;

        if (cube.Name != EntityNames.Cube) return;

        var group = MatchFinder.FindGroup(_grid, cube);

        if (!MatchFinder.IsClearable(group.Count))
        {
            // A lone cube is a miss, not a mistake: a dull note, no popup, and the combo is left
            // alone so a stray click does not cost a streak the player earned
            _audio.PlayRejected();

            return;
        }

        var result = _keeper.RegisterClear(group.Count);

        _audio.PlayClear(group.Count);

        if (result.ComboStep > 0)
        {
            _audio.PlayComboStep(result.ComboStep);
        }

        Log.Info($"Cleared {group.Count} {cube.Get<Components.CubeComponent>()?.Color} at {cube.Transform.Position}: {result.Breakdown}");

        AddScorePopup(cube.Transform.Position, result);

        // The grid is updated before anything falls, so the next click already matches the finished
        // layout even while the cubes are still visibly dropping into it
        var moved = _grid.RemoveAndCollapse(group);

        foreach (var cleared in group)
        {
            cleared.Remove();
        }

        DropCubes(moved);

        Scoreboard?.Punch();

        CheckForGameOver();
    }

    /// <summary>
    /// Ends the game once no group of the minimum size is left anywhere on the board.
    /// </summary>
    /// <remarks>
    /// Checked after a clear rather than every frame, because the board can only become unplayable as
    /// a result of one. Cubes with no matching neighbour can never be removed once groups of two are
    /// required, so every game ends with some left standing - the point of this is to say so, instead
    /// of leaving the player clicking at a board that has quietly stopped responding.
    /// </remarks>
    private void CheckForGameOver()
    {
        if (IsGameOver || MatchFinder.HasClearableGroup(_grid)) return;

        IsGameOver = true;

        Log.Info($"No moves left. {_grid.Count} cubes stranded, final score {_keeper.TotalScore:N0}.");

        if (GameOverText is null) return;

        GameOverText.Text = $"NO MOVES LEFT\n{_keeper.TotalScore:N0} points\n{_grid.Count} cubes stranded";
        GameOverText.IsVisible = true;
    }

    /// <summary>
    /// Teleports each surviving cube down to the slot the grid now says it occupies.
    /// </summary>
    /// <remarks>
    /// The bodies are moved rather than left to fall, so the picture matches the grid immediately.
    /// Physics still owns everything after this - the cubes settle, jostle and sleep as usual - but
    /// the two can no longer disagree about which slot a cube is in.
    /// <para>
    /// Removing a cube wakes the stack on its own, because <c>Bodies.Remove</c> forces the sleeping
    /// island active. Teleporting one does not, which is why this runs after the removals rather than
    /// before them.
    /// </para>
    /// </remarks>
    private static void DropCubes(List<(Entity Cube, int Dropped)> moved)
    {
        foreach (var (cube, dropped) in moved)
        {
            var body = cube.Get<Components.SlidingCubeComponent>();

            if (body is null) continue;

            var position = cube.Transform.Position;

            position.Y -= dropped * GameSettings.CubeSize.Y;

            body.Teleport(position, body.Orientation);
        }
    }

    private void AddScorePopup(Vector3 position, ScoreResult result)
    {
        var tierLabel = ScoreRules.GetTierLabel(result.Tier);
        var text = string.IsNullOrEmpty(tierLabel)
            ? $"{result.Total:N0}"
            : $"{tierLabel}\n{result.Total:N0}";

        if (result.Multiplier > 1f)
        {
            text += $"  x{result.Multiplier:0.#}";
        }

        var entity = new Entity(EntityNames.ScorePopup, position)
        {
            new EntityTextComponent()
            {
                Text = text,
                FontSize = GetFontSize(result.Tier),
                TextColor = GetTierColour(result.Tier),
                Anchor = TextAnchor.MiddleCenter,
                Alignment = Stride.Graphics.TextAlignment.Center,
                EnableShadow = true,
                LayerDepth = 1f,
            },
            new ScorePopupScript { HorizontalDrift = (float)(_drift.NextDouble() - 0.5) * 0.8f }
        };

        entity.Scene = SceneSystem.SceneInstance.RootScene;
    }

    private static float GetFontSize(ScoreTier tier) => tier switch
    {
        ScoreTier.Calamity => 34,
        ScoreTier.Huge => 30,
        ScoreTier.Great => 26,
        ScoreTier.Nice => 22,
        _ => 18,
    };

    /// <summary>
    /// Colour for each tier, warming as the clear gets bigger.
    /// </summary>
    private static Color GetTierColour(ScoreTier tier) => tier switch
    {
        ScoreTier.Calamity => new Color(255, 80, 80),
        ScoreTier.Huge => new Color(255, 150, 60),
        ScoreTier.Great => new Color(255, 210, 70),
        ScoreTier.Nice => new Color(180, 255, 130),
        _ => Color.White,
    };
}
