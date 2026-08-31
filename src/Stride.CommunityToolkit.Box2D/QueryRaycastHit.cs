using Box2D.NET;
using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// A raw 2D raycast hit returned by <see cref="PhysicsQueries2D"/>. Engine-agnostic: contains only
/// Box2D identifiers and value types; see <see cref="RaycastHit"/> for the entity-resolved variant.
/// </summary>
/// <param name="BodyId">The body hit by the ray.</param>
/// <param name="ShapeId">The specific shape on the body that was hit.</param>
/// <param name="Point">World-space intersection point.</param>
/// <param name="Normal">World-space surface normal at the hit.</param>
/// <param name="Fraction">Normalized fraction along the ray in range [0,1].</param>
public readonly record struct QueryRaycastHit(
    B2BodyId BodyId,
    B2ShapeId ShapeId,
    Vector2 Point,
    Vector2 Normal,
    float Fraction);