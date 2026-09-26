---
generated: true
slug: particles
---

# Particles

A blue fountain: fifty particles a second launched upward from a small area, pulled back down by
gravity, each rendered as a camera-facing billboard. Built entirely from code, so every part of the
system is visible - the emitter and its spawn rate, the initializers that randomise starting
position and velocity, and the gravity updater that acts on them afterwards.

The `Program.cs` file shows how to:

- Creating a ParticleSystemComponent from code
- Setting lifetime, size range and spawn rate on an emitter
- Randomising start position and velocity with initializers
- Applying gravity with an updater
- Rendering particles as billboards
- Using helpers: SetupBase3DScene, Add3DGround

![Particles](media/particles.webp)

View on [GitHub](https://github.com/stride3d/stride-community-toolkit/tree/main/examples/code-only/E09_3D_Particles).

[!code-csharp[](../../../../examples/code-only/E09_3D_Particles/Program.cs?start=1&end=111)]