using Stride.Core.Mathematics;

namespace Example_CubicleCalamity.Shared;

public static class Constants
{
    public const int BasePointsPerCube = 10;
    public const float Interval = 0.33f;
    public const int MaxLayers = 10;
    public const int Rows = 10;
    public const string TotalScore = "Total Score";

    public static readonly List<Color> Colours = [Color.Red, Color.Green, Color.Blue, Color.DarkGoldenrod];
    public static readonly Vector3 CubeSize = new(0.5f);

    /// <summary>
    /// Offset applied to the X and Z of every cube so the platform is centred on the ground's origin
    /// rather than growing out of one corner, whatever <see cref="Rows"/> is set to.
    /// </summary>
    /// <remarks>
    /// Cube centres sit at <c>GridOrigin + i * CubeSize</c> for <c>i</c> in <c>[0, Rows)</c>, so the
    /// footprint spans <c>(Rows - 1) * CubeSize</c> and pulling it back by half of that puts its
    /// middle on zero. At 10 rows of 0.5 that is -2.25, at 5 rows it is -1.
    /// </remarks>
    public static readonly float GridOrigin = -(Rows - 1) * CubeSize.X * 0.5f;

    /// <summary>
    /// Middle of the finished platform, which is what the camera orbits and looks at.
    /// </summary>
    public static readonly Vector3 PlatformCentre = new(0, MaxLayers * CubeSize.Y * 0.5f, 0);
}