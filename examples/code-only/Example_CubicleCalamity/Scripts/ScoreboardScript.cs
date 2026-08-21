using Example_CubicleCalamity.Gameplay;
using Stride.CommunityToolkit.Engine;
using Stride.Engine;

namespace Example_CubicleCalamity.Scripts;

/// <summary>
/// Draws the running total and the combo streak, and animates the total as it changes.
/// </summary>
/// <remarks>
/// The counter eases up to the real total rather than snapping to it. It is a small thing and it is
/// the single cheapest way to make scoring feel like an event: a number that visibly climbs is worth
/// watching, and the same number appearing instantly is just text. The keeper always holds the true
/// total - only the display lags - so nothing downstream has to know this is happening.
/// </remarks>
public class ScoreboardScript : SyncScript
{
    private const string Label = "Total Score";
    private const float CountUpSeconds = 0.4f;
    private const float PunchDuration = 0.2f;
    private const float PunchScale = 1.25f;

    private float _displayedScore;
    private float _punchRemaining;

    /// <summary>
    /// Gets the score keeper this reads from.
    /// </summary>
    /// <remarks>
    /// Set through an object initialiser rather than a constructor, so the class keeps the public
    /// parameterless constructor Stride's STRDIAG010 analyser expects of a component, while
    /// <c>required</c> keeps the compiler enforcing that it is supplied.
    /// </remarks>
    public required ScoreKeeper Keeper { get; init; }

    /// <summary>
    /// Gets or sets the text showing the running total.
    /// </summary>
    public EntityTextComponent? TotalText { get; set; }

    /// <summary>
    /// Gets or sets the text showing the combo multiplier.
    /// </summary>
    /// <remarks>
    /// Assigned explicitly rather than looked up off the entity. Two components of the same type on
    /// one entity can only be told apart by the order they were added, which is a rename or a
    /// reorder away from silently swapping the two labels.
    /// </remarks>
    public EntityTextComponent? ComboText { get; set; }

    /// <inheritdoc />
    public override void Start() => _displayedScore = Keeper.TotalScore;

    /// <inheritdoc />
    public override void Update()
    {
        var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;

        Keeper.Update(deltaTime);

        UpdateTotal(deltaTime);
        UpdateCombo();
    }

    private void UpdateTotal(float deltaTime)
    {
        if (TotalText is null) return;

        var target = Keeper.TotalScore;

        if (_displayedScore < target)
        {
            // Rate is set from whatever gap remains when the clear lands, so a huge clear counts up
            // faster rather than taking proportionally longer. Every clear feels the same length.
            _displayedScore += Math.Max((target - _displayedScore) / CountUpSeconds, 1f) * deltaTime;

            if (_displayedScore >= target)
            {
                _displayedScore = target;
            }
        }

        TotalText.Text = $"{Label}: {(int)_displayedScore:N0}";

        if (_punchRemaining > 0f)
        {
            _punchRemaining = Math.Max(0f, _punchRemaining - deltaTime);

            var punch = _punchRemaining / PunchDuration;

            TotalText.Scale = 1f + (PunchScale - 1f) * punch;
        }
        else
        {
            TotalText.Scale = 1f;
        }
    }

    private void UpdateCombo()
    {
        if (ComboText is null) return;

        if (!Keeper.HasCombo)
        {
            ComboText.IsVisible = false;

            return;
        }

        var multiplier = ScoreRules.GetMultiplier(Keeper.ComboStep);

        ComboText.IsVisible = true;
        ComboText.Text = $"COMBO x{multiplier:0.#}";

        // Fades as the window runs out, so the streak is visibly expiring rather than just vanishing
        ComboText.Opacity = Math.Clamp(Keeper.ComboFraction * 1.5f, 0f, 1f);
    }

    /// <summary>
    /// Makes the total jump, called when a clear has just been scored.
    /// </summary>
    public void Punch() => _punchRemaining = PunchDuration;
}