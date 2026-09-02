---
generated: true
slug: shapes-playground
---

# Shapes Playground

The full tour of ShapeBatch in 3D: ground discs and selection rings, decals, panels standing on a
plane, genuinely thick 3D lines and wire boxes, and camera-facing billboards. Every shape is flat
and evaluated per fragment as a signed distance function, so its outline stays a constant number
of pixels wide however far away it is - press 7 and fly down the corridor of rings to see it.

The `Program.cs` file shows how to:

- Registering a shape renderer with AddShapeBatch
- Depth-tested shapes versus overlay shapes from two batches
- Discs, rings and polygons lying on an arbitrary plane in 3D
- Thick 3D lines and wire boxes from camera-facing capsules
- Billboards that keep their shape from any viewpoint
- Why a signed distance function keeps an outline a constant pixel width

![Shapes Playground](media/shapes-playground.webp)

View on [GitHub](https://github.com/stride3d/stride-community-toolkit/tree/main/examples/code-only/Example_Shapes_Playground).

[!code-csharp[](../../../../examples/code-only/Example_Shapes_Playground/Program.cs?start=1&end=346)]
