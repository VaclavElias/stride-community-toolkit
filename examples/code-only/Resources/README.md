# Shared example resources

Files here are linked into the examples that use them (see each `.csproj`), so one copy serves them all.

## Particle textures

`smoke.png`, `fire8x8.png`, `flame8x8.png`, `bonfire8x8.png`, `dot.png` and `radial-grad-gray.png` come from the
Stride engine's particle samples (`samples/Particles` in the [stride](https://github.com/stride3d/stride) repository),
MIT licensed. The `8x8` sheets and `smoke.png` are flipbooks of 64 frames; the rest are single frames, white on black
with no alpha channel, made for additive blending. `E09_3D_Particles` derives alpha from brightness at load for the
ones that lack it, so the same textures work alpha-blended too.