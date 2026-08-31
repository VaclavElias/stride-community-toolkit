namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// Which coordinate planes a chart draws its grid on. A flat chart uses <see cref="XY"/>; a 3D chart
/// (one whose Z range is not degenerate) can add the floor and side walls the way the scene editor's
/// grid gizmo offers one grid per axis.
/// </summary>
[Flags]
public enum ChartGridPlanes
{
    /// <summary>No grid planes.</summary>
    None = 0,

    /// <summary>The chart plane itself - the only plane a flat chart uses.</summary>
    XY = 1,

    /// <summary>The floor plane, spanned by X and Z.</summary>
    XZ = 2,

    /// <summary>The side plane, spanned by Y and Z.</summary>
    YZ = 4,
}