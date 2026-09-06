using Box2D.NET;
using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// The closest hit of a shape cast from <see cref="PhysicsQueries2D"/>: the shape and body struck,
/// where, the surface normal there, and how far along the sweep it happened.
/// </summary>
/// <param name="ShapeId">The shape that was struck.</param>
/// <param name="BodyId">The body that shape belongs to.</param>
/// <param name="Point">World-space contact point.</param>
/// <param name="Normal">World-space surface normal at the contact.</param>
/// <param name="Fraction">How far along the translation the hit occurred, 0 to 1.</param>
public readonly record struct ShapeCastHit(
    B2ShapeId ShapeId,
    B2BodyId BodyId,
    Vector2 Point,
    Vector2 Normal,
    float Fraction);