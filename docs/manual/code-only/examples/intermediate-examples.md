---
generated: true
---

# C# Intermediate Examples

A Stride subsystem used directly, or several concepts combined. These assume you are comfortable with the basics.

## Examples Overview

- [Procedural Geometry](procedural-geometry.md): A triangle, a plane and a circle built at runtime with MeshBuilder, which handles the vertex layout and buffer bookkeeping that raw buffers make you do by hand.
- [Simple Geometry (Labelled Triangle)](simple-geometry.md): The smallest possible custom mesh - one triangle from three vertices - with each vertex labelled on screen so the relationship between the numbers in the code and the shape on screen is visible.
- [Cylinder Mesh](cylinder-mesh.md): A cylinder generated with MeshBuilder, split into the three jobs any surface of revolution needs: place a ring of vertices, join consecutive rings into side walls, then close both ends with caps.
- [Partial Torus Mesh](partial-torus-mesh.md): A torus defined parametrically from two angles - one around the tube, one around the ring - and cut short by limiting the second, which turns the same code into an arc, a horseshoe or a full ring without special cases.
- [3D Letters (Mesh Text)](letters-3d.md): A gallery of every glyph LetterMeshFactory can build - the digits, the full A-Z alphabet and the dash - as solid extruded meshes that catch the light like any other geometry, plus a frame counter whose digits are rebuilt as a new mesh every frame.
- [Give Me a Cube (SimulationUpdate)](simulation-update.md): Drive an entity from the physics clock instead of the render loop.
- [Raycast](raycast.md): Click the ground and a sphere is kicked towards where you clicked; click the sphere and it stops dead.
- [Ship HUD](hud.md): A cockpit HUD composed from the toolkit's shapes and world text: a heading tape that scrolls, a pitch ladder, a gun-sight, speed and altitude tapes with moving readouts, a radar with a sweep and contacts, ring gauges, a sparkline and a spectrum, a comms log in a framed panel, four status tiles with a selected one that glows, mode buttons with one disabled, and a warning strip that goes amber and then red as the shield drops.
- [Spatial Sound](spatial-sound.md): 3D positional audio for a runtime sound: a looping pad on an orb that circles a pillar, heard from the camera.
- [Collision Group](collision-group.md): Two players and an enemy, where the players collide with each other but the enemy passes through both.
- [Collision Layer](collision-layer.md): The same players-and-enemy scene as the collision group example, solved the other way.
- [Multiple Physics Simulations](multiple-simulations.md): Two Bepu simulations in one game, side by side: the left lane falls under Earth gravity, the right under Moon gravity, and an amber ball that belongs to the Moon world sinks straight through the Earth ground because the two worlds never touch.
- [Simple Constraint](simple-constraint.md): One constraint, doing one thing: a distance servo holding two spheres three units apart, pulling them together or pushing them apart until they settle there.
- [Constraints - Servo vs Motor vs Limit](constraint-motors.md): The three kinds of Bepu constraint, side by side.
- [First-Person Character (Bepu)](first-person-character.md): A first-person character built entirely from code - no Game Studio scene - on a Bepu CharacterComponent, with boxes to walk into and jump onto.
- [GPU Instancing](instancing.md): Render two identical walls of cubes built two different ways, side by side.
- [Particles](particles.md): A blue fountain: fifty particles a second launched upward from a small area, pulled back down by gravity, each rendered as a camera-facing billboard.
- [Debug Shapes](debug-shapes.md): The full tour of the DebugShapes package: every immediate-mode primitive it can draw, exercised from a ShapeUpdater component so the shapes animate and the batching can be seen under load.
- [Debug Shapes Usage](debug-shapes-usage.md): The short version of the debug shapes example: turn the system on, draw a sphere and a circle, done.
- [ShapeBatch Shapes](shape-batch.md): The full tour of ShapeBatch in 3D: ground discs and selection rings, decals, glowing HUD panels with world text on them, genuinely thick 3D lines and wire boxes, camera-facing billboards, pie wedges, donut charts and radial progress arcs, a glow that halos any of them, dashed rings and lines that turn and march, fills that run to a colour or fade to nothing, and one opacity over a whole shape.
- [Stride UI - Capsule and Window](stride-ui-capsule-with-rigid-body.md): A capsule in a 3D scene with a "Hello, World" panel drawn over it using Stride's built-in UI.
- [Stride UI - Button Hover Animation](stride-ui-button-hover-animation.md): A main menu built from code whose buttons grow a blue underline while the pointer is over them.
- [ImGui UI](imgui-ui.md): An ImGui overlay for in-game tools, debug panels and live tweaking.
- [2D Spawn Menu](spawn-menu-2d.md): Drive a scene from the keyboard without filling the screen with instructions.
- [Cube Clicker](stride-ui-cube-clicker.md): A small clicker game: cubes appear, left and right clicks are counted, and both the score and the cube positions are written to disk so the next run picks up where the last one stopped.
- [Charts 2D](charts-2d.md): A flat, paper-like chart drawn entirely in code - no assets, no chart control, just meshes built at runtime.
- [Charts 3D](charts-3d.md): The same code-only chart API as the 2D example, in a lit 3D scene.

> [!NOTE]
> Each example references a handful of toolkit packages. The `using` directives at the top of
> every listing name them, and the linked project file on GitHub is authoritative. A few examples
> also need a third-party package - Box2D.NET, Jitter2, Myra or ImGui - which their page calls out.

[!INCLUDE [basic-examples-outro](../../../includes/manual/examples/basic-examples-outro.md)]
