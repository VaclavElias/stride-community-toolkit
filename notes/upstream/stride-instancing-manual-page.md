# ISSUE TITLE

Add a manual page for GPU instancing

---

# ISSUE BODY

## Source material

The original implementation PR, [stride3d/stride#741](https://github.com/stride3d/stride/pull/741),
carries design discussion worth keeping. Everything from it that made its way into the draft below
was re-checked against the 4.4 source first, because the PR is from 2020 and some of it did not
survive contact with the merged code:

- **Picking an instance in Game Studio selects that instance's entity.** Still true —
  `EditorGameEntitySelectionService` resolves the picked `InstanceId` through
  `InstancingEntityTransform.GetInstanceAt`.
- **`InstancingEntityTransform` forces `Ignore` so the editor gizmos line up with the instances.**
  The *why* is from the PR; the behaviour is in the source.
- **Two Game Studio entity factories exist**, *Instanced Model* and *Model Instance*, both under
  **Model**. A third factory that would have created a master with a child instance in one step is
  still commented out in `ModelEntityFactory.cs`.
- **`InstancingRenderFeature` is in the default compositors.** Added in response to review; present
  in both `DefaultGraphicsCompositorLevel9` and `Level10`.
- **Animated models instance fine, but share one animation state** — the PR author confirmed this
  with a screenshot when asked directly.
- **Not carried over:** a late comment suggests the inverse-matrix buffer is only needed when the
  material uses normal maps. That is not what shipped — `NormalFromMeshInstanced.sdsl` uses
  `GetInstanceWorldInverse` too, with no normal map involved. The draft says the buffer is always
  required.
- **Still unanswered:** platform support. It was asked in the PR in 2020 ("is this DirectX only, or
  API neutral?") and never answered, which is why the draft leaves it out rather than guessing.

## Summary

The manual has no page on instancing. `grep -ri "instancing" en/manual` returns nothing, yet
`InstancingComponent`, `InstanceComponent`, `InstancingUserArray`, `InstancingUserBuffer` and
`InstancingEntityTransform` have shipped since 4.1, `InstancingRenderFeature` is part of both default
graphics compositors, and **Instancing** appears in Game Studio's *Add component* list under **Model**.

Users who want to draw thousands of copies of one model currently have to discover the feature from
source or from community samples. It is also easy to get subtly wrong in ways that produce no error —
most commonly by leaving a `ModelComponent` on the instance entities, which draws the whole crowd
twice and is slower than not instancing at all.

## Proposed location

A single page, matching the pattern of `graphics/sprite-fonts.md` and `graphics/graphics-api.md`:

- **New file:** `en/manual/graphics/instancing.md`
- **`en/manual/graphics/index.md`** — add to the *In this section* list, after *Rendering pipeline*
- **`en/manual/toc.yml`** — add under **Graphics**, after *Rendering pipeline*:

```yaml
      - name: Instancing
        href: graphics/instancing.md
```

If it grows (per-instance material data, custom instancing shaders), it can become
`graphics/instancing/index.md` later without breaking links.

Worth cross-linking from `graphics/rendering-pipeline/index.md`, since instancing is a draw-call
concern.

## Draft

A first draft is below. It is written from the 4.4 source rather than from memory — the API names,
the `ModelTransformUsage` behaviour, the compositor requirement and the buffer creation call were all
checked against `sources/engine/Stride.Engine/Engine/Instancing*.cs`,
`Engine/Processors/InstancingProcessor.cs` and `Stride.Rendering/Rendering/InstancingRenderFeature.cs`.

Please treat it as raw material — cut, restructure and rewrite freely to fit house style. One thing I
could not verify and flagged inline: whether instancing is supported on every graphics platform.

### Things an editor may want to decide

- **Screenshots.** The page has none. A before/after of the draw-call counter in the profiler would
  carry a lot of the argument. The Game Studio section would benefit most: the two entity factories
  in the **Add entity** menu, and the *Instancing* component's `Type` dropdown.
- **Badges.** I used `Advanced` + `Programmer`, matching `materials-for-developers.md`.
- **Game Studio coverage.** Now has its own section, based on the factories and picking behaviour in
  the 4.4 editor source. Not screenshotted, and one claim in it is worth a 30-second confirmation in
  a running Game Studio — see the inline note about the selection highlight.
- **Sample project.** The Bepu physics sample already uses instancing —
  `samples/Physics/BepuSample`, prefabs `BepuInstancedCube` and `Bepu2DInstancedCube`, used from
  several scenes including *Cube Fountain* and *Cube Mixer*. Linked from *See also*. There is also a
  standalone proof of concept from the original author,
  [StrideTransformationInstancing](https://github.com/tebjan/StrideTransformationInstancing), but it
  dates from 2020 and I have not run it, so I have not linked it from the page itself.
- **Per-instance variation.** The draft now has a short section on `InstanceID`, which the original
  author flagged as the intended technique. It deliberately stops at pointing the way rather than
  shipping a shader, since I have not written and run one.

---

# PROPOSED PAGE CONTENT — `en/manual/graphics/instancing.md`

# Instancing

<span class="badge text-bg-primary">Advanced</span>
<span class="badge text-bg-success">Programmer</span>

**Instancing** draws many copies of the same model in a single draw call. A forest of a thousand
trees, a field of debris, a crowd of identical props — without instancing each one costs a separate
draw call, and the CPU spends more time telling the GPU what to draw than the GPU spends drawing it.
With instancing, the model is submitted once along with an array of transformation matrices, and the
GPU repeats it.

Instancing removes **draw calls**. It does not reduce vertex or pixel work, and it does not make
anything else in your game cheaper — a thousand instanced entities still cost a thousand entities'
worth of transform updates, physics and scripts. Reach for it when your profiler shows draw calls
dominating.

> [!NOTE]
> Two components with nearly the same name do different jobs.
> @'Stride.Engine.InstancingComponent' goes on the **one** entity that owns the model and decides how
> the crowd is drawn. @'Stride.Engine.InstanceComponent' goes on **each** copy and points back at it.
> Singular for the many, plural-looking for the one — worth reading twice when you first meet them.

## Requirements

Every instance shares one @'Stride.Rendering.Model' and its materials. If the copies need different
meshes or different materials, they need different masters.

The renderer also needs @'Stride.Rendering.InstancingRenderFeature'. It is already present in the
default graphics compositor, so projects created from a template need no setup. If you **build a
graphics compositor in code**, you must add it yourself — nothing warns you, the instanced models
simply never appear:

```csharp
var meshRenderFeature = GraphicsCompositor.RenderFeatures.OfType<MeshRenderFeature>().First();
meshRenderFeature.RenderFeatures.Add(new InstancingRenderFeature());
```

## The master entity

Instancing is driven by one **master** entity carrying both a @'Stride.Engine.ModelComponent' and an
@'Stride.Engine.InstancingComponent'. The model component supplies what to draw; the instancing
component supplies where to draw it, through its `Type` property:

| Type | Where the matrices come from |
|---|---|
| @'Stride.Engine.InstancingEntityTransform' | Collected each frame from entities that have an @'Stride.Engine.InstanceComponent' |
| @'Stride.Engine.InstancingUserArray' | A `Matrix[]` you own and update |
| @'Stride.Engine.InstancingUserBuffer' | GPU buffers you create, fill and own |

`InstancingEntityTransform` is the default, and the only one usable from Game Studio without code.

## In Game Studio

Two entries in the **Add entity** menu, both under **Model**, cover the whole setup:

- **Instanced Model** — asks for a model asset, then creates the master: one entity with a
  @'Stride.Engine.ModelComponent' and an @'Stride.Engine.InstancingComponent'.
- **Model Instance** — creates a copy: one entity with an @'Stride.Engine.InstanceComponent'. Set its
  **Instancing** property to the master, or make it a child of the master and leave the property
  empty.

To fill a scene quickly, place one instance where you want it and press **Ctrl+D** to duplicate it,
or hold **Ctrl** while dragging an entity. Each duplicate keeps its reference to the master.

Clicking an instance in the viewport selects that instance's entity, not the master, so instances can
be moved individually like any other entity.

<!-- EDITOR: worth 30 seconds in a running Game Studio to confirm before publishing. The editor has
     no instance-aware selection highlight in the source, so selecting one instance should highlight
     the whole crowd. The PR author described exactly that in 2020 and said a per-instance wireframe
     was more work than he had time for. If it still behaves that way it is worth a sentence here, as
     it looks like a bug the first time you see it. -->

Only `InstancingEntityTransform` is available this way — the other two types are set from code.

## Instancing an array of matrices

The simplest case: no entities per copy, just transforms. Build the matrices, hand them over, done.

```csharp
var instancing = new InstancingUserArray();

var master = new Entity("Trees")
{
    new ModelComponent(treeModel),
    new InstancingComponent { Type = instancing }
};

master.Scene = SceneSystem.SceneInstance.RootScene;

var matrices = new Matrix[1000];

for (var i = 0; i < matrices.Length; i++)
{
    matrices[i] = Matrix.Translation(PositionFor(i));
}

instancing.UpdateWorldMatrices(matrices);
```

Call `UpdateWorldMatrices` again whenever the transforms change. Stride recomputes the inverse
matrices and the combined bounding box only on the frames you call it, so a static crowd costs
nothing after the first frame.

You can pass a count smaller than the array length to draw only part of it, which lets you keep one
oversized array and vary how much of it is live:

```csharp
instancing.UpdateWorldMatrices(matrices, visibleCount);
```

## Instancing real entities

When each copy needs to be a real entity — because it has physics, a script, or children — give the
copies an @'Stride.Engine.InstanceComponent' pointing at the master:

```csharp
var master = new Entity("CrateMaster")
{
    new ModelComponent(crateModel),
    new InstancingComponent { Type = new InstancingEntityTransform() }
};

master.Scene = scene;

var masterInstancing = master.Get<InstancingComponent>();

foreach (var position in positions)
{
    var crate = new Entity("Crate")
    {
        new InstanceComponent { Master = masterInstancing }
    };

    crate.Transform.Position = position;
    crate.Scene = scene;
}
```

Each frame, `InstancingEntityTransform` reads `Entity.Transform.WorldMatrix` from every registered
instance. Disabled instance components are skipped, so toggling `Enabled` removes a copy from the
crowd without destroying the entity.

If `Master` is left unset, the instance searches its parent entities for an `InstancingComponent`,
which makes "master with its instances as children" work with no wiring.

> [!IMPORTANT]
> The instance entities must **not** have a `ModelComponent` of their own. If they do, each copy is
> drawn twice — once by itself and once by the master — which is slower than not instancing at all.
> This produces no error and looks correct on screen, so it is easy to miss.

## Model transform usage

@'Stride.Engine.ModelTransformUsage' controls how the master entity's own transform combines with
each instance matrix:

| Value | Effect |
|---|---|
| `Ignore` | Instance matrices are world matrices. The master's transform is not applied |
| `PreMultiply` | The master's world matrix is applied before the instance matrix |
| `PostMultiply` | The master's world matrix is applied after the instance matrix |

`PreMultiply` and `PostMultiply` let you move, rotate or scale the whole crowd by moving the master
entity, with instance matrices expressed in the master's local space.

`InstancingEntityTransform` forces `Ignore` and does not let you change it. The matrices it gathers
are already world matrices, so applying the master's transform on top would move the copies away from
the entities they came from — and in Game Studio, away from their own gizmos.

## Owning the GPU buffers

@'Stride.Engine.InstancingUserBuffer' hands you full control: you create the buffers, you fill them,
you decide when. Its `Update()` does nothing. Use it when you generate instance data on the GPU, or
when you want to skip re-uploading matrices that have not changed.

```csharp
var instancing = new InstancingUserBuffer
{
    InstanceWorldBuffer = Buffer.New<Matrix>(GraphicsDevice, capacity,
        BufferFlags.ShaderResource | BufferFlags.StructuredBuffer, GraphicsResourceUsage.Dynamic),
    InstanceWorldInverseBuffer = Buffer.New<Matrix>(GraphicsDevice, capacity,
        BufferFlags.ShaderResource | BufferFlags.StructuredBuffer, GraphicsResourceUsage.Dynamic),
    InstanceCount = count,
};
```

You are responsible for both buffers, for `InstanceCount`, and for `BoundingBox` — nothing is
computed for you. You also own their lifetime: the engine never disposes user-supplied buffers. This
is the one place where ownership differs between the types — with `InstancingUserArray` the engine
creates the GPU buffers behind your matrices, resizes them as the count changes, and disposes them
when the component is removed.

> [!NOTE]
> The inverse matrices are not optional, and they are not only for normal-mapped materials. Stride's
> plain instanced normal shader uses them too, so lighting is wrong without them on any material.

## Culling and bounding boxes

The instancing processor merges the instance transforms into one bounding box on the master, and the
whole crowd is culled as a unit. That is what makes it cheap, and it is also its main limitation: a
crowd spread across the level is never culled, because its bounding box covers the level.

For large worlds, split the crowd into several masters by region so each can be culled separately.

## Animation

An animated model can be instanced, with one caveat: the whole crowd shares a single animation state,
because there is one model and one skeleton being submitted. Every copy is on the same frame of the
same clip, in lockstep.

To break up the uniformity, use several masters — the same model, a separate `InstancingComponent`
each, playing at different speeds or offsets — and split the crowd between them. A handful is usually
enough for the lockstep to stop being noticeable.

## Per-instance variation

Instances share a model *and* its materials, so there is no built-in way to give one copy a different
colour or roughness. What the shader does get is `streams.InstanceID`, the index of the copy being
drawn, which Stride's own instanced shaders use to look up per-instance matrices.

A custom shader can use the same index to look up per-instance values of your own — colour, roughness,
metalness, a texture-atlas offset — from a structured buffer you fill and bind yourself. This is not
wired up for you and is not exposed in Game Studio; it is the extension point rather than a feature.

## Good practice

- **Share one `Model`.** Build the model once and reuse the reference. Generating a model per copy
  defeats the point and costs a vertex and index buffer each.
- **Only update matrices that changed.** For `InstancingUserArray`, skipping `UpdateWorldMatrices`
  skips the inverse-matrix and bounding-box work for that frame. For a crowd that has come to rest,
  that is the whole per-frame cost.
- **Group by region, not by convenience,** so culling can do its job.
- **Watch the instance count.** Very large counts are limited by buffer size and by how much the GPU
  can chew through in one call. Splitting a huge crowd across a few masters is often faster than one
  enormous one.
- **Measure first.** Instancing helps when draw calls are the bottleneck. If the profiler shows you
  are vertex- or fill-bound, fewer draw calls will not help, and level of detail or simpler meshes
  will.
- **Instanced entities still cost.** A thousand `InstanceComponent` entities are a thousand entities
  with transforms to update. If the copies never move and need no behaviour, `InstancingUserArray`
  avoids the entities entirely.

<!-- EDITOR: one claim I could not verify and deliberately left out - platform support. Whether
     instancing works on every backend (Vulkan, OpenGL ES, etc.) or has restrictions worth calling
     out. This was asked on the original implementation PR in 2020 and never answered, so it needs
     someone who knows the backends rather than more source reading. -->

## See also

- [Rendering pipeline](rendering-pipeline/index.md)
- [Render features](rendering-pipeline/render-features.md)
- [Graphics compositor](graphics-compositor/index.md)
- [Materials for developers](materials/materials-for-developers.md)

The **Bepu physics sample** (`samples/Physics/BepuSample`) uses instancing in several scenes,
including *Cube Fountain* and *Cube Mixer*, through the `BepuInstancedCube` and `Bepu2DInstancedCube`
prefabs — a working reference for the entity-transform setup.
