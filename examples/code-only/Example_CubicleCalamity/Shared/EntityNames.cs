namespace Example_CubicleCalamity.Shared;

/// <summary>
/// Names the game looks entities up by.
/// </summary>
/// <remarks>
/// Stride has no built-in tag system, so finding an entity at runtime means matching its name. That
/// works, but a name typed out at both the creation site and the lookup site is a rename away from
/// silently finding nothing - there is no compiler error and no exception, the search simply returns
/// empty. Naming them once here is the cheapest fix.
/// </remarks>
public static class EntityNames
{
    /// <summary>A playable cube. Every cube in the platform carries this name.</summary>
    public const string Cube = "Cube";

    /// <summary>Holds the scripts that run the game rather than any one object in it.</summary>
    public const string GameManager = "GameManager";

    /// <summary>The running total shown on screen.</summary>
    public const string Scoreboard = "Scoreboard";

    /// <summary>One floating score, spawned per clear and removed when its animation ends.</summary>
    public const string ScorePopup = "ScorePopup";

    /// <summary>The ground plane, used as a fallback point for the camera to orbit.</summary>
    public const string Ground = "Ground";

    /// <summary>The axis gizmo that shows which way X, Y and Z run.</summary>
    public const string OrientationGizmo = "OrientationGizmo";

    /// <summary>Colliderless reference cube. See the orientation-aids note in the plan.</summary>
    public const string ReferenceCube = "ReferenceCube";
}