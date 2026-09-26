using Stride.Core.Mathematics;
using Stride.Particles;
using Stride.Particles.Modules;

namespace E09_3D_Particles_Gallery;

/// <summary>
/// An updater of our own: every particle is pulled towards a point that something else moves,
/// with a little damping so the swarm settles into a cloud around it rather than shooting through.
/// The engine's updaters are ordinary classes over a pool of fields, and this is all one takes.
/// </summary>
/// <remarks>
/// A particle is a row of fields - position, velocity, life, a random seed - and an updater reads
/// and writes the fields it declared in <see cref="ParticleModule.RequiredFields"/>. Nothing here
/// is special-cased by the engine: the gravity updater is written the same way.
/// </remarks>
public sealed class SwarmUpdater : ParticleUpdater
{
    /// <summary>Where the swarm is drawn to, in world space.</summary>
    public Vector3 Target { get; set; }

    /// <summary>How hard each particle is pulled, in units per second squared per unit of distance.</summary>
    public float Strength { get; set; } = 6f;

    /// <summary>How much of the velocity survives a second; 1 never slows, 0.1 settles fast.</summary>
    public float Damping { get; set; } = 0.35f;

    /// <summary>How far from the target each particle's own spot lies, so the swarm has a body rather than a point.</summary>
    public float Spread { get; set; } = 1.5f;

    /// <inheritdoc/>
    public override bool IsPostUpdater => false;

    /// <summary>Declares the fields the update reads and writes.</summary>
    public SwarmUpdater()
    {
        RequiredFields.Add(ParticleFields.Position);
        RequiredFields.Add(ParticleFields.Velocity);
        RequiredFields.Add(ParticleFields.RandomSeed);
    }

    /// <inheritdoc/>
    public override void Update(float dt, ParticlePool pool)
    {
        if (!pool.FieldExists(ParticleFields.Position) || !pool.FieldExists(ParticleFields.Velocity) || !pool.FieldExists(ParticleFields.RandomSeed)) return;

        var positions = pool.GetField(ParticleFields.Position);
        var velocities = pool.GetField(ParticleFields.Velocity);
        var seeds = pool.GetField(ParticleFields.RandomSeed);
        var keep = MathF.Pow(Damping, dt);

        foreach (var particle in pool)
        {
            var seed = particle.Get(seeds);

            // Each particle's own spot in the cloud, fixed for its life by its seed
            var spot = Target + new Vector3(
                seed.GetFloat(RandomOffset.Offset3A) - 0.5f,
                seed.GetFloat(RandomOffset.Offset3B) - 0.5f,
                seed.GetFloat(RandomOffset.Offset3C) - 0.5f) * (Spread * 2f);

            var velocity = particle.Get(velocities);

            velocity += (spot - particle.Get(positions)) * (Strength * dt);
            velocity *= keep;

            particle.Set(velocities, velocity);
        }
    }
}