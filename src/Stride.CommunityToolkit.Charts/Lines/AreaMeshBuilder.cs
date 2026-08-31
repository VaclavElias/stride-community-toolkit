using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Buffer = Stride.Graphics.Buffer;

namespace Stride.CommunityToolkit.Charts.Lines;

/// <summary>
/// Builds the filled region between two polylines - the shaded area under a curve, or between two curves.
/// </summary>
/// <remarks>
/// <para>
/// The two polylines are sampled at the same positions, so they form <em>columns</em>: pairs of points, one
/// on each edge of the band. Consecutive columns become two triangles, which is all a filled area needs -
/// no polygon triangulation, because a band sampled this way is already a strip.
/// </para>
/// <para>
/// <see cref="Columns"/> prepares those pairs: it clamps them to a vertical range so the fill stops at the
/// chart's edge instead of spilling past it, and breaks the band wherever a point is not finite or the
/// column lies entirely outside the range - the same treatment <see cref="PolylineClipping"/> gives a line.
/// It is plain arithmetic on the points, so it is covered by unit tests.
/// </para>
/// </remarks>
internal static class AreaMeshBuilder
{
    /// <summary>
    /// Turns two equal-length polylines into the runs of drawable columns between them: each column is a
    /// pair of points sharing a position along the band, clamped to
    /// [<paramref name="yMin"/>, <paramref name="yMax"/>].
    /// </summary>
    /// <param name="upper">One edge of the band.</param>
    /// <param name="lower">The other edge, sampled at the same positions.</param>
    /// <param name="yMin">The bottom of the visible range; the fill is cut here.</param>
    /// <param name="yMax">The top of the visible range.</param>
    /// <returns>Zero or more runs, each with at least two columns.</returns>
    /// <exception cref="ArgumentNullException">If either edge is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If the edges have different lengths, or the range is inside out.</exception>
    internal static List<List<(Vector3 Upper, Vector3 Lower)>> Columns(IReadOnlyList<Vector3> upper, IReadOnlyList<Vector3> lower, float yMin, float yMax)
    {
        ArgumentNullException.ThrowIfNull(upper);
        ArgumentNullException.ThrowIfNull(lower);

        if (upper.Count != lower.Count)
        {
            throw new ArgumentException("The two edges must have the same number of points.", nameof(lower));
        }

        if (yMax < yMin)
        {
            throw new ArgumentException("The range's maximum must not be smaller than its minimum.", nameof(yMax));
        }

        var runs = new List<List<(Vector3, Vector3)>>();
        var run = new List<(Vector3, Vector3)>();

        for (var i = 0; i < upper.Count; i++)
        {
            var a = upper[i];
            var b = lower[i];

            if (!IsFinite(a) || !IsFinite(b))
            {
                Flush(runs, run);
                continue;
            }

            // A column entirely above or below the visible range contributes nothing, and breaks the band
            // so the fill does not slide along the edge between two visible stretches
            var top = MathF.Max(a.Y, b.Y);
            var bottom = MathF.Min(a.Y, b.Y);

            if (bottom > yMax || top < yMin)
            {
                Flush(runs, run);
                continue;
            }

            a.Y = Math.Clamp(a.Y, yMin, yMax);
            b.Y = Math.Clamp(b.Y, yMin, yMax);

            run.Add((a, b));
        }

        Flush(runs, run);

        return runs;
    }

    /// <summary>
    /// Builds one mesh from the runs of columns, two triangles per pair of neighbouring columns.
    /// </summary>
    /// <param name="device">The device to create the vertex and index buffers on.</param>
    /// <param name="runs">The runs from <see cref="Columns"/>; each needs at least two columns.</param>
    /// <param name="options">Fill colour and plane.</param>
    /// <returns>A mesh whose bounds are set, ready to be put in a <see cref="Model"/>.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">If there are no runs, or one has fewer than two columns.</exception>
    internal static Mesh Build(GraphicsDevice device, IReadOnlyList<IReadOnlyList<(Vector3 Upper, Vector3 Lower)>> runs, AreaOptions options)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(options);

        if (runs.Count == 0)
        {
            throw new ArgumentException("At least one run is required.", nameof(runs));
        }

        var normal = options.Normal;

        if (normal.LengthSquared() <= MathUtil.ZeroTolerance)
        {
            throw new ArgumentException("The plane normal must not be zero.", nameof(options));
        }

        normal.Normalize();

        var vertices = new List<VertexPositionNormalTexture>();
        var indices = new List<int>();

        foreach (var run in runs)
        {
            if (run is null || run.Count < 2)
            {
                throw new ArgumentException("Every run needs at least two columns.", nameof(runs));
            }

            // Every run is appended to the same buffers, so its indices start where its vertices do
            var baseIndex = vertices.Count;

            for (var i = 0; i < run.Count; i++)
            {
                // V distinguishes the two edges, U runs along the band, for materials that want to fade it
                var u = run.Count > 1 ? (float)i / (run.Count - 1) : 0f;

                vertices.Add(new VertexPositionNormalTexture(run[i].Upper, normal, new Vector2(u, 0f)));
                vertices.Add(new VertexPositionNormalTexture(run[i].Lower, normal, new Vector2(u, 1f)));
            }

            // Each pair of neighbouring columns spans a quad: upper/lower of this column (a, a+1) and of
            // the next (b, b+1). Two triangles per quad, wound the same way round so the band is one face.
            for (var i = 0; i + 1 < run.Count; i++)
            {
                var a = baseIndex + i * 2;
                var b = a + 2;

                indices.Add(a);
                indices.Add(a + 1);
                indices.Add(b);
                indices.Add(a + 1);
                indices.Add(b + 1);
                indices.Add(b);
            }
        }

        return ToMesh(device, vertices, indices);
    }

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static void Flush(List<List<(Vector3, Vector3)>> runs, List<(Vector3, Vector3)> run)
    {
        if (run.Count >= 2)
        {
            runs.Add([.. run]);
        }

        run.Clear();
    }

    private static Mesh ToMesh(GraphicsDevice device, List<VertexPositionNormalTexture> vertexList, List<int> indexList)
    {
        var vertices = vertexList.ToArray();
        var indices = indexList.ToArray();

        var vertexBuffer = Buffer.New(device, vertices, BufferFlags.VertexBuffer, GraphicsResourceUsage.Default);
        var indexBuffer = Buffer.New(device, indices, BufferFlags.IndexBuffer, GraphicsResourceUsage.Default);

        var meshDraw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.TriangleList,
            VertexBuffers = [new VertexBufferBinding(vertexBuffer, VertexPositionNormalTexture.Layout, vertices.Length)],
            IndexBuffer = new IndexBufferBinding(indexBuffer, is32Bit: true, indices.Length),
            DrawCount = indices.Length,
        };

        var positions = new Vector3[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            positions[i] = vertices[i].Position;
        }

        var boundingBox = BoundingBox.FromPoints(positions);

        return new Mesh
        {
            Draw = meshDraw,
            BoundingBox = boundingBox,
            BoundingSphere = BoundingSphere.FromBox(boundingBox),
        };
    }
}