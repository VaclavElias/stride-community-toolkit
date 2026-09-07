# Architecture notes

A running list of API-design observations for the Stride Community Toolkit: places where the shape of
the API, rather than a bug in it, is what trips people up.

This is a **backlog of observations, not a decision record.** Nothing here is agreed or scheduled.
Items get added when something is noticed in passing - usually while writing an example, which is
where API friction shows up first - and removed when they are resolved or rejected.

The toolkit is in Preview, so breaking changes are on the table where they buy a cleaner long-term
API. Each item below states the impact if changed.

> [!NOTE]
> Add to this file when you notice friction, even if you are not going to act on it. An observation
> written down is worth more than one rediscovered three months later.

---

## 1. `Size` means different things for different primitives

**Observation.** `Primitive3DEntityOptions.Size` is a single `Vector3?` whose interpretation changes
per primitive type. Box-like shapes read it as a **full extent**; round shapes read `X` as a
**radius**, which is a half extent.

```csharp
game.Create3DPrimitive(PrimitiveModelType.Cube,   new() { Size = new Vector3(1f) });   // 1 unit across
game.Create3DPrimitive(PrimitiveModelType.Sphere, new() { Size = new Vector3(1f) });   // 2 units across
```

**Impact.** Silent and visual-only. Passing a diameter to a sphere produces a model at twice the
intended size with no error. It is worst when a collider is supplied by hand, because the mesh and
collider then disagree and objects appear to pass through one another - encountered while writing
`E05_3D_Constraints_Rope`.

The generated mesh and generated collider *do* read the value identically, so the toolkit is
internally consistent. The trap is entirely in the caller's expectation.

**How other engines avoid it.** None of the major engines expose one polymorphic size; each names the
property after the convention it uses. Godot went as far as making a breaking rename for exactly this
reason.

| Engine | Box | Sphere | Capsule |
|---|---|---|---|
| Unity | `BoxCollider.size` (full) | `SphereCollider.radius` | `radius` + `height` |
| Godot 4 | `BoxShape3D.size` (full) | `SphereShape3D.radius` | `radius` + `height` |
| Unreal | `UBoxComponent::BoxExtent` (**half**) | `USphereComponent::SphereRadius` | `CapsuleRadius` + `CapsuleHalfHeight` |

Two things worth copying. First, **the name carries the semantics** - Unreal says `Extent` and
`HalfHeight` precisely because they are halves. Second, Godot 3 called the box field `extents` and
meant half-extents; Godot 4 renamed it to `size` meaning full extents, a deliberate breaking change
to remove this ambiguity. That is the same choice facing this API.

*(Engine comparisons above are from general knowledge of those APIs, not verified against their
sources in this repository.)*

**Options.**

1. **Document only** - done for now: the XML docs on `Size` list every primitive's convention.
   Zero impact, but the trap remains.
2. **Normalise to bounding-box semantics.** `Size` always means the axis-aligned box the shape fits
   in, so a sphere of `Size = (1,1,1)` is one unit across. Intuitive and matches Unity's unit
   primitives. Breaking: every call site passing a radius silently halves. Loud but mechanical.
3. **Per-primitive option types** - `SphereOptions { Radius }`, `CubeOptions { Size }`. Impossible to
   misread, but multiplies the options surface and complicates the generic
   `Create3DPrimitive(type, options)` entry point.
4. **Make the primitive itself a closed set that carries its own dimensions.** Replace the
   `PrimitiveModelType` enum plus separate `Size` with one argument per shape, each naming exactly
   the parameters that shape needs. A sphere then cannot be handed a `Vector3` at all, and the entry
   point stays single and generic.

   ```csharp
   public abstract record Primitive
   {
       private Primitive() { }                                 // closed: no cases from outside
       public sealed record Sphere(float Radius) : Primitive;
       public sealed record Cube(Vector3 Size) : Primitive;
       public sealed record Capsule(float Radius, float Length) : Primitive;
   }

   game.Create3DPrimitive(new Primitive.Sphere(0.5f), options);
   ```

   Note this fixes the ambiguity **structurally** rather than by documentation, and it subsumes
   item 6: the mesh and collider switches both match over the same closed set, so a shape added to
   one and not the other is caught at the switch rather than by a test.

   Works on C# 14 / net10.0 today. Breaking, and broad - `PrimitiveModelType` appears across nearly
   every example.

   **C# 15 union types would be the same design, stated more directly**, and would add compiler-
   enforced exhaustiveness so a missing case is a build error rather than a `_ => throw`:

   ```csharp
   public union Primitive(Sphere, Cube, Capsule);
   ```

   Not adoptable yet: unions need .NET 11 Preview 2 and `<LangVersion>preview</LangVersion>`, and are
   early preview. Requiring a preview language version of every consumer is a high price for a
   shipped package. The record hierarchy above is the same shape and upgrades to a union later
   without changing call sites much, so waiting for unions is not a reason to defer the decision.
   (Union syntax and requirements verified against the C# 15 union types announcement; the toolkit
   has not been built against .NET 11.)

---

## 2. `IncludeCollider = false` leaves a half-configured body

**Observation.** Setting `Bepu3DPhysicsOptions.IncludeCollider = false` still attaches a
`BodyComponent`, holding a `CompoundCollider` with no shapes. That never attaches to the simulation,
so the entity ends up with a physics component that does nothing.

**Impact.** Two different intentions collide on one flag: "no physics at all" and "physics, but I
will supply the collider myself". The first is served by the non-physics `Create3DPrimitive`
overload; the second works but leaves an inert component if the caller forgets to add shapes.

**Options.** Rename to something intent-revealing (`SuppliesOwnCollider`); or validate and warn when
an attached body has no shapes; or leave as-is and rely on the documented gotcha.

---

## 3. Two `Create3DPrimitive` overloads separated only by their options type

**Observation.** There is a Bepu overload taking `Bepu3DPhysicsOptions` and a plain one taking
`Primitive3DEntityOptions`, both on `this IGame`, both with the options parameter optional. With
both namespaces imported, a bare `new()` cannot choose between them, and neither can a call that
omits the options altogether: `game.Create3DPrimitive(type)` is `CS0121`, because both candidates
need one default substituted and C# has no further tie-breaker between them
(`Bepu3DPhysicsOptions` deriving from `Primitive3DEntityOptions` does not help; parameter-type
specificity is only consulted for generic candidates).

**What was hiding it (found 2026-09-02, during the `Games`→`Engine` merge).** The physics-free
pair lived in `Stride.CommunityToolkit.Games`, and almost no example imported that namespace, so
Bepu/Bullet callers never had both candidates in scope. The `Games` namespace was load-bearing and
nobody had written it down; the only record was a comment in `E04_ImGuiNet` calling
`SetMaxFPS` by its fully qualified name "because importing Games would make Create3DPrimitive
ambiguous", which read as a workaround. Merging the two `GameExtensions` classes into `Engine`
(the right move — see item 19) put both pairs in scope for every example and surfaced 18 `CS0121`
sites plus the VB example.

**What does not fix it.** `[OverloadResolutionPriority]` (C# 13) — it is only compared between
candidates declared in the *same* type, and the shadowing overloads live in a different class from
the one they shadow. The build proved it.

**The bridge (in place).** Each physics package now declares an exact-arity overload beside its
options overload:

```csharp
public static Entity Create3DPrimitive(this IGame game, PrimitiveModelType type)
    => game.Create3DPrimitive(type, (Bepu3DPhysicsOptions?)null);
```

C# prefers, among otherwise-tied applicable candidates, the one that needed no default argument
substituted, and that rule *does* apply across declaring types; F# has the same preference. So the
omitted-options call resolves to the collider-adding version (what every caller already got), a
`new Bepu3DPhysicsOptions { … }` argument reaches the physics options overload by identity, and a
`new Primitive3DEntityOptions { … }` argument still reaches the core method because the physics
overload is not applicable to it. VB does not follow these rules and the VB and F# examples now pass
`Bepu3DPhysicsOptions()` explicitly; the remarks on the new overloads say so. Two residual traps to
keep in the remarks: a literal `null` for the options is still ambiguous (both options overloads
accept it — use a typed null), and giving the *core* method an exact-arity overload some day would
reintroduce the tie.

**Root cause, and the real fix.** Physics is selected by namespace import: the physics packages
*shadow* a core method to change its behaviour, so what `Create3DPrimitive` means depends on a
`using` line — the same smell as `SetupBase3DScene` existing twice with identical signatures. Item
19's Q3(b) (select physics once, explicitly, at setup; the physics package registers a provider the
core method consults) removes the shadowing entirely, at which point the exact-arity overloads go
too, and the behaviour is identical in C#, F# and VB. Shape records (item 1, option 4) let the
entity carry its own shape, so the explicit chained form
`game.Create3DPrimitive(shape).AddBepuPhysics()` no longer has to repeat the type — the second
honest route beside the provider.

**Do not let the trick spread.** The exact-arity overload is acceptable for these four methods
because the shadowing is temporary. If `SetupBase*Scene` or anything else reaches for the same
device, that is the signal to do the provider work instead of adding another overload.

---

## 4. Mass is reachable only by abandoning the generated collider

**Observation.** Mass lives on the collider shape (`ColliderBase.Mass`), not on the body or on the
options. To set it, a caller must switch off `IncludeCollider` and build the whole
`CompoundCollider` by hand - which re-exposes item 1, since the hand-built collider must match the
mesh convention.

**Impact.** Setting one common property costs the entire convenience of the helper.
`E05_3D_Constraints_Rope` does this, and it is the longest part of `RopeBuilder`.

**Options.** Surface `Mass` (or `Density`) on `Bepu3DPhysicsOptions` and apply it to the generated
shapes. Additive, not breaking.

---

## 5. Fluent return values are inconsistent

**Observation.** The contributor guidance asks extensions to return the modified instance where
natural, but several do not - `SetupBase3DScene` returns `void`, while `AddSkybox` returns an entity.

**Impact.** Small, but it makes chaining unpredictable, so callers stop trying.

**Options.** Return `Game` from the scene-setup helpers. Additive and non-breaking, since a discarded
return value compiles unchanged.

---

## 6. No test asserts that generated meshes and colliders agree

**Observation.** For every `PrimitiveModelType`, the procedural model and the Bepu collider derive
their dimensions from the same `Size` in two separate switch statements
(`Procedural3DModelBuilder` and `EntityExtensions`). Nothing enforces that they stay in step.

**Impact.** They currently agree. A future primitive added to one switch but not the other, or with a
different convention, would produce a mesh that does not match its collider - a defect that is
invisible until something falls through the world.

**Options.** A test that, for each primitive type and a fixed `Size`, asserts the model bounds and the
collider bounds match. Cheap, and it pins down the convention that item 1 documents.

Superseded if item 1 option 4 is taken: matching both switches over a closed set of primitive records
moves this from a test to a compile-time check.

---

## 7. Deriving a collider requires an entity and a model it never reads

**Observation.** `Get3DColliderShape(type, size)` is private. The only public route to it is
`AddBepu3DPhysics`, which throws unless the entity already carries a `ModelComponent` - a guard only,
since it derives the collider from the primitive type and reads nothing out of the mesh.

**Impact.** Combined with the shared-model gap (now agreed work - `TODO.md` §3), sharing a model
becomes a four-step dance that needs a comment to explain itself:

```csharp
var entity = new Entity("Item") { new ModelComponent(sharedModel) };   // model attached only to satisfy the guard
entity.AddBepu3DPhysics(type, options);
entity.Remove<ModelComponent>();                                       // ...and immediately taken off again
```

The collider for a shape and a size is a pure function of two values. Nothing about it needs an
entity, a component, or a mesh.

**Options.** Expose it - `public static ColliderBase ColliderFor(PrimitiveModelType, Vector3?)` —
which is additive and also serves callers who want a collider without any of the helper machinery;
and/or drop the `ModelComponent` guard from `AddBepu3DPhysics`, which is breaking only for code
relying on the throw.

---

## 8. Instancing needs three separate registrations and fails silently if one is missed

**Observation.** Drawing an instanced crowd requires, in three different places: an
`InstancingRenderFeature` in the graphics compositor (`AddInstancingSupport`), a master entity in the
scene carrying a `ModelComponent` and an `InstancingComponent`, and - for the buffered variant - a
renderer in the compositor (`AddInstancingBufferUpload`).

**Impact.** Omit the first and nothing is drawn, with no exception, no warning and no log line. The
code-built compositor wires up transform, skinning, material and lighting but not instancing, so this
catches everyone who does not start from a Game Studio project. Hit while writing `Example22`.

The split also has a lifetime consequence worth knowing: `AddInstancingBufferUpload` registers with
the **compositor**, not the scene, so it outlives any scene swap. Creating one instancing object per
scene leaves every previous one registered and being uploaded every frame.

**Options.** A single helper that sets up the render feature, the master and the upload renderer
together; and/or have the instancing processor warn once if it finds an `InstancingComponent` in a
scene whose compositor has no `InstancingRenderFeature`.

---

## 9. Toolkit instancing does not notice entities leaving the scene

**Observation.** Stride's own `InstanceComponent` unregisters itself from its master when its entity
leaves the scene, because the component goes with it. `EntityInstancing` and `BufferedEntityInstancing`
keep their own list and have no such hook.

**Impact.** An entity removed from the scene stays registered, so the master keeps reading its
transform and drawing it - ghosts of objects that are no longer there. The caller has to remember to
call `Clear()` or remove the instance explicitly, and to do it *before* detaching the entities.
Encountered while adding runtime shape switching to `E10_2D_StressPile`.

**Options.** Subscribe to the entity's scene changes and unregister automatically, matching
`InstanceComponent`'s behaviour; or keep the manual model and make the asymmetry loud in the XML docs
of both types.

---

## 10. `DisplayPosition` is a general screen-corner concept living in `Scripts.Utilities`

**Observation.** `DisplayPosition` names the four window corners plus `None` and `Custom`. It began as
an implementation detail of `DebugOverlay`, which is why it sits in
`Stride.CommunityToolkit.Scripts.Utilities`. It now has three consumers - `DebugOverlay`,
`Basic3DCameraController`, and `EntityTextComponent.ScreenAnchor` - and only the first two are scripts.

**Impact.** Anything that wants to anchor to a corner has to reach into a `Scripts.Utilities`
namespace that has nothing to do with what it is doing. Adding `ScreenAnchor` to
`EntityTextComponent` meant every consumer of that property picks up
`using Stride.CommunityToolkit.Scripts.Utilities;` for an enum naming a corner of the screen, which
reads as a mistake at the call site. Hit while migrating `E20_3D_CubeCollapse`'s HUD.

The `None` and `Custom` members compound it. They exist for `DebugOverlay`'s needs - opting out
entirely, and deferring to a separate `CustomPosition` property - and mean nothing to a component that
has its own visibility flag and its own explicit-position mode. `EntityTextRenderer` currently maps
both to the top-left, which is a silent fallback for values that should not have been offerable.

**Options.** Move the enum to a neutral namespace (`Stride.CommunityToolkit.Engine`, or a
`Stride.CommunityToolkit.Rendering` shared home) and leave a type forward or a rename note, since the
toolkit is in Preview and breaking changes are acceptable; and/or split the four real corners into a
`ScreenCorner` enum, leaving `DisplayPosition` as `DebugOverlay`'s own type with its `None` and
`Custom` extras. The second is more churn but stops components offering values they cannot honour.

**The same enum cannot say "centre", either.** `TextPositionMode` offers corners and explicit pixels
and nothing else, so Cube Collapse's game-over banner needs a four-line `ScreenCentreTextScript`
that recomputes its position every frame. Centring on screen is not an exotic request for a HUD, and
a component that can anchor to four corners but not the middle will be asked for it again. Whatever
shape the corner enum settles into should carry it.

---

## 11. There is no screen-anchored orientation gizmo

**Observation.** Every code-only example eventually faces "where the hell is X" with no editor
viewport to answer it. `AddGroundGizmo` draws the world axes at the origin, which is a world-space
answer to a screen-space question: it either buries itself in the scene's content or sits at a
misleading offset once the camera moves.

**Impact.** Low individually, constant in aggregate. It is the reason the demo game places two
world-space markers by hand, and the reason several examples start with the camera pointed somewhere
arbitrary until someone tunes it.

**Options.** An axis widget pinned to a screen corner, the way editor viewports do it - a small
overlay renderer reading the camera's rotation, drawn last, ignoring depth. Additive, and it
generalises to every example rather than being tuned per scene. It wants whatever the corner-anchor
enum in item 10 becomes.

---

## 12. `LetterMeshFactory` bakes its style into a shared segment grid

**Observation.** Stroke width, glyph width, spacing and depth are constants. The stroke in particular
is woven into the shared segment grid - `TopY`, `MiddleY` and the rest all derive from it - so making
it configurable is not a matter of exposing one number. It means passing a metrics or style object
down into every glyph builder.

**Impact.** None today; there are two consumers and both want the current look. It is recorded
because the shape of the fix is decided by how the type is written now, not later: the bar-built
glyphs would follow a style object automatically, but the hand-sketched polygons - V, X, Y, Z, K, the
R leg, the Q tail, D's chamfers - carry tuned constants that must either scale with the stroke or be
re-authored.

**Options.** A `LetterStyle` record threaded through the factory, with the existing area-invariant
tests generalised to run per style over a few stroke values. Additive. Worth doing once a third
consumer appears and actually wants a different weight - not before, since the re-authoring cost is
real and speculative styling would fix the wrong constants.

---

## 13. `DebugTextDropdown` offers two rendering paths and only one respects the overlay

**Observation.** The type can be rendered two ways: `Draw(debugTextSystem)` at its own `Position`, or
`GetLines()` handed to a `DebugOverlay` section. Both are public and neither is marked as the
default.

**Impact.** The standalone path silently ignores the overlay's reposition and hide keys, so a
dropdown drawn that way stays put and stays visible while everything else on screen moves or
disappears. The spawn-menu example fell into exactly this before being switched to `GetLines()`. The
two paths also disagree about who owns layout - `Position` is meaningful in one and dead in the
other.

**Options.** Keep both but make the asymmetry loud in the XML docs of `Draw` and `Position`, naming
`GetLines()` as the path that participates in the overlay; or have `Draw` register with the overlay
when one exists. Both additive. Deleting `Draw` was considered and rejected - standalone rendering is
wanted for cases where no overlay is in play.

---

## 18. `GetComponentInChildren<T>()` resolves to two different searches depending on an optional argument

**Observation.** Two extension methods share the name and differ in what they search:

- `EntityExtensions.GetComponentInChildren<T>(this Entity)` - a recursive depth-first search that
  **checks the entity itself first** (`entity.OfType<T>()`), despite the name and despite the
  toolkit's own `…AndSelf` convention for that.
- `EntitySearchExtensions.GetComponentInChildren<T>(this Entity, bool includeDisabled = false)` —
  the breadth-first, children-only search inherited from the original StrideToolkit, constrained to
  `ActivableEntityComponent` and skipping disabled components. Its parameterless sibling from upstream
  was renamed `GetComponentInChildrenBFS` to avoid clashing with the first method.

C# prefers the applicable candidate that needs no default-argument substitution, so for any
`T : ActivableEntityComponent`:

```csharp
entity.GetComponentInChildren<Foo>();       // DFS, includes self, ignores Enabled
entity.GetComponentInChildren<Foo>(false);  // BFS, children only, enabled only
```

Same name, same intent at the call site, different traversal, different scope and different
filtering - decided by whether a literal `false` was typed. Noticed while auditing the StrideToolkit
port (August 2026); the two methods have coexisted since the merge.

**Impact.** Silent. Neither call errors, and in a shallow hierarchy with no disabled components both
return the same thing, so the divergence only shows up later - usually as "why did it find the
component on the parent" or "why did it find a disabled one". The `BFS` suffix also leaks an
implementation detail into a name whose sibling carries no `DFS`.

**Options.**

1. Rename the `EntityExtensions` method to say what it does - `GetComponentInDescendantsAndSelf<T>()`
   or similar - and give the `BFS` method its original name back. Breaking, mechanical, and the
   names then match the `Descendants` / `AndSelf` vocabulary the search class already uses.
2. Keep the names and make the DFS variant children-only (drop the self check), so at least the
   scope agrees; the traversal-order and `Enabled` differences remain.
3. Document only: an XML `<remarks>` on each pointing at the other. Zero impact, trap remains.

---

## 19. Code-only bootstrapping: the front door is right, the floor under it is not

*Written 2026-09-02 after a review of the whole bootstrapping surface, every example's `Program.cs`,
the docs, and how ten other code-first engines and .NET hosting shape their first program. Facts
carry `file:line`; engine comparisons were verified against current docs (URLs in the survey
banked with the session). This item is a proposal with open questions, not a decision.*

### What exists

The front door is two lines plus a callback:

```csharp
using var game = new Game();
game.Run(start: Start, update: Update);          // toolkit: schedules a microthread, then engine Run
void Start(Scene rootScene) { game.SetupBase3DScene(); ... }
```

`Run` (`src\Stride.CommunityToolkit\Engine\GameExtensions.cs:70,118,125`) is twenty lines over public
API; `SetupBase3DScene` (`src\Stride.CommunityToolkit.Bepu\GameExtensions.cs:44`) is a fixed sequence
of six extension calls on `Game` — compositor + clean UI stage, camera, directional light, camera
controller, ground — with **no parameters**. Everything else is ~40 `Add*`/`Create*` extension
methods on `Game`, called inside `Start`, each mutating the running game.

How the 69 example programs actually use it (the whole population, not a sample):

| Shape | Count | Note |
|---|---|---|
| `SetupBase3DScene()` / `SetupBase2DScene()` one call | 46 | the bundle works for two thirds |
| `SetupBase2D()`/`SetupBase3D()` then hand-assembly | 14 | wanted a clear colour, no ground, no controller, or the camera aimed *before* the controller |
| Fully unrolled from `AddGraphicsCompositor()` | 11 | one literally comments `// SetupBase3D() unrolled` |
| Subclass `Game` | 0 | |
| Async `start` overload | 0 | tests only |
| `GameContext` argument | 0 | tests only |
| `game.Run()` with scripts and no callbacks | 0 | |

So the *entry point* is not the problem: nobody subclasses, nobody fights `Run`, and the pattern is
literally the one MonoGame and Foster document (`using var game = new X(); game.Run();`). The
problem is that **a third of the examples cannot use the bundle and copy its body by hand**, with
drift: one hand-rolled copy omits the UI stage (`E08_3D_DebugShapes\Program.cs:21`), one adds the
skybox twice (`E09_3D_Particles\Program.cs:24,37`), one defines a local function *named*
`SetupBase3DScene` that shadows the toolkit's (`E04_Myra_DraggableWindow\Program.cs:26`), and two reach
through the public API with casts to get at things the bundle built but does not hand back
(`E11_3D_Charts\Program.cs:76-79` for `Bloom`; `Charts2D:73-83` for the camera controller).

### How everyone else does it

| | Configure when | Pattern | Defaults and opting out | Who adds a camera/light |
|---|---|---|---|---|
| MonoGame, Foster, Nez | constructor (pre-run) + overrides | `Game` subclass | constructor settings / `AppConfig` record | you (Nez's `Scene` owns a camera) |
| Silk.NET | options before `Create`, events before `Run` | event-driven | `WindowOptions.Default` | you |
| Bevy | everything before `run()` | fluent `App` builder, functions as systems | explicit `DefaultPlugins` / `MinimalPlugins`, `.set(...)` / `.disable::<T>()` | you, via `commands.spawn` |
| .NET generic host / ASP.NET minimal | builder before `Build()` | builder → build → run | `CreateApplicationBuilder` vs `CreateSlim/EmptyBuilder` | n/a |
| Evergine | ctor services + launcher + `Initialize` | `Application` subclass, DI container | none implicit | you |
| **Toolkit** | **inside `Start`, after the engine is up** | `Game` + extensions + delegates | one bundle; opt-out = unroll it | **the toolkit** |

Three things every good one has that the toolkit lacks:

1. **Two phases.** "Configure the app" (window, plugins, defaults) is separate from "populate the
   world" (entities). The toolkit does both inside `Start`, after the device and the engine's
   default `GameSettings` are already applied, so window size, clear colour, MSAA, physics engine,
   post-effects, and whether a ground exists are all decided by *which* extension calls happen to run
   first. Bevy and .NET register everything up front and apply it in a well-defined startup step.
2. **Named, composable defaults.** `DefaultPlugins.set(WindowPlugin { .. }).disable::<LogPlugin>()`
   and `CreateApplicationBuilder` vs `CreateEmptyBuilder` let you keep the bundle and change one
   thing. `SetupBase3DScene` is all-or-nothing: want a clear colour on 3D and you unroll six lines
   (`SetupBase2D` takes a colour, `SetupBase3D` takes nothing — `GameExtensions.cs:163` vs `:181`).
3. **Failing at configure time with a message that names the missing piece.** Bevy and .NET fail in
   `build()`/`run()`. The toolkit fails on frame 1, after the window is open, and inconsistently —
   see the next list.

Two things the toolkit has that most do not, and should keep: an awaitable `start` (only .NET
hosting matches it), and the choice, made deliberately in the discussion that spawned the toolkit
(stride #1253), *not* to wrap `Game` in a builder type so that the engine stays visible. The docs
promise exactly that: "thin wrappers over Stride APIs, meant to accelerate iteration (not hide the
engine)" (`docs\index.md:15`).

### Where the friction actually is (verified)

Order-dependence that fails late, silently, or three different ways:

- `AddCleanUIStage()` / `AddUIStage()` **replace** `compositor.Game` with a fresh
  `SceneRendererCollection` (`Rendering\Compositing\GraphicsCompositorExtensions.cs:229-245`), so any
  renderer added before them is silently discarded; `AddCleanUIStage` also replaces `PostEffects`
  wholesale (`:191-200`) — which is why every example calling it runs SSAO/SSR/bloom/lens flare
  (engine-example-opportunities.md, toolkit-side findings).
- "Compositor first" is enforced three ways: `Add3DCamera` throws `InvalidOperationException`
  (`GameExtensions.cs:269`), `AddDebugShapes`/`AddParticleRenderer` throw `NullReferenceException`
  ("Opaque RenderStage not found"), `AddInstancingSupport` throws on a missing `MeshRenderFeature`.
- `AddGroundGizmo` and `ShowColliders` silently do nothing when their precondition is missing
  (`GameExtensions.cs:634-636`, `Bullet\GameExtensions.cs:161-163`).
- The camera must be aimed *before* `Add3DCameraController` because the controller caches its
  reset pose in `Start`; documented only inside an example (`E20_3D_CubeCollapse\CubeCollapseGame.cs:225-231`).
- `AddSkybox` is not idempotent; the text renderers are mandatory-but-forgettable to the point that
  a component's display name says "(call AddEntityTextRenderer)" (`EntityTextComponent.cs:58`).

Implicit coupling by name and index:

- The camera is found by entity name `"Main"` (`CameraDefaults.cs:36`, `GameExtensions.cs:828-830`)
  and the ground by `"Ground"`; passing `cameraName: null` produces a camera nothing can find again.
- `Add3DCamera` always takes `cameras[0]` and **renames the slot** (`GameExtensions.cs:272-274`);
  calling it twice leaves two entities on one slot, warned about only in `SetCameraPosition`'s remarks.
- UI is hard-wired to `RenderGroup.Group31`, debug shapes default to `Group1`, primitives to
  `Group0`; nothing names these. Effect-name literals (`"UiStage"`, `"Main"`, `"Test"`, `"Opaque"`)
  are repeated across two files.

Asymmetry (item 5 was the tip of this):

- Return types across one family: `SetupBase*` → `void`; `AddGraphicsCompositor` → compositor;
  `Add3DCamera`/`AddDirectionalLight`/`AddSkybox`/`Add3DGround` → `Entity`; `AddAllDirectionLighting`
  → `void`; `AddStudioLighting` → tuple; `AddInstancingSupport` → `bool`; `Game.AddSceneRenderer`
  → `void` while `GraphicsCompositor.AddSceneRenderer` → compositor; `Game.Add3DCameraController`
  → `Entity` while `Entity.Add3DCameraController` → `void`.
- The same concern reachable two ways with different semantics: `Game.GetCameraEntity` (top-level
  only, throws) vs `Scene.GetCamera` (recursive, nullable, and the named overload is buggy —
  `SceneExtensions.cs:56-68`); `AddEntityDebugSceneRenderer` vs `AddEntityDebugRenderer`;
  `TryGetRenderStage` implemented twice in two assemblies.
- Physics flavour is chosen by **namespace import**: `SetupBase3DScene`, `Add3DGround`,
  `Create3DPrimitive` exist with identical signatures in the Bepu and Bullet packages, so "import one
  or the other, not both" is a documented rule (`extensions.md:35`) and item 3's CS0121 is the
  symptom. `AddInfinite3DGround` and `ShowColliders` exist only in Bullet.
- `SetupBase2D` uses `AddUIStage`, `SetupBase3D` uses `AddCleanUIStage` — different post-effect
  outcomes for the "same" step. `showLightGizmo` defaults `true` in one lighting helper, `false` in
  the other.

Docs vs code: `extensions.md:22-23` says both `SetupBase*Scene` add a skybox; neither does, which is
why 46 examples call `AddSkybox()` on the next line. The XML docs say the default camera name is
`"MainCamera"` in two places and `"Main"` in a third.

Packaging: the front door needs two packages plus a third for the skybox, and the mandatory one is
called `.Windows` for a reason that has nothing to do with Windows (item 14). Two examples do not
reference it and still build, which nobody has explained.

### What the engine forces, and what it does not

Three engine facts cap how "modern" the shape can get without upstream work, and they should be
stated in the docs rather than worked around silently:

- The root scene exists only inside `Run()` → `LoadContent` (item 16), so *entities* cannot be
  created before `Run`. Any "configure before run" design is therefore **declarative**: it records
  intent and the toolkit applies it on the first frame — which is exactly what Bevy does with
  `add_systems(Startup, …)`, so this is not a compromise, it is the normal shape.
- `SceneSystem` starts with an empty compositor that draws nothing (item 15). Assigning
  `game.SceneSystem.GraphicsCompositor` *before* `Run` **does survive**: the default is set in the
  constructor (`SceneSystem.cs:40`) and `LoadContent` replaces it only when
  `InitialGraphicsCompositorUrl` names an existing asset (`:127-132`), which a code-only game never
  has. So the compositor — and with it clear colour, MSAA, post-effects, UI stage, instancing and
  particle features — is the one piece that can be configured eagerly, before any window opens.
  Only the entities (camera, light, ground, skybox) must wait for the root scene.
- Shader sources come from the asset compiler's `data/db` (item 14), which is the only reason the
  platform packages exist.

Everything else in the friction list is the toolkit's own.

### Options

**A. Keep the shape, fix the floor (non-breaking or mechanically breaking).**
Fix the two docs; give `SetupBase3D` the same `clearColor` `SetupBase2D` has; make `AddCleanUIStage`
preserve existing renderers and start from `DisableAll()`; make `AddSkybox` and the renderer adders
idempotent; one guard (`RequireCompositor()` with a message that says *what to call*) instead of
three failure modes; `Game`-returning `SetupBase*` and `Entity`-returning `Add*` consistently;
retire the duplicate routes (item 5). Cheap, and it removes maybe half the unrolling. It does not
give the 14 "bundle minus one thing" examples a way to say so.

**B. Add a declarative setup object to the existing `Run`, keep everything else (breaking, mechanical).**
The bundle becomes data, applied by the toolkit on frame 1 before `start`:

```csharp
using var game = new Game();

game.Run(new SceneSetup3D                       // or SceneSetup2D; both records with init-only members
{
    ClearColor = new Color(16, 18, 28),
    Skybox     = true,                          // default true — and now the docs are right
    Ground     = Ground.Plane(size: 300),       // Ground.None to opt out
    Lighting   = Lighting.Studio,               // Lighting.Directional (default) | Studio | AllDirections | None
    Camera     = Camera.At(position, rotation) .WithController(),   // default: (6,6,6) looking at origin + controller
    PostEffects = fx => fx.Bloom.Enabled = true // applied to the real PostProcessingEffects, no casts
},
start: Start, update: Update);
```

and `game.Run(start: Start)` stays valid as `game.Run(SceneSetup3D.Default, start: Start)` sugar,
so the first program in the docs does not change. The setup object gives every hand-unrolled example
a one-object answer, gives failures a single place to be raised (before any window opens, when
`Run` validates the record), lets `SetupBase3DScene()` remain as `game.Apply(SceneSetup3D.Default)`
for those who like the call, and — because it is data — is trivially testable (today nothing tests
`SetupBase*` at all; only `Run` has tests). Physics becomes an explicit field (`Physics = Physics.Bepu`)
or stays namespace-selected; see Q3. Extension methods on `Game` remain the way to *populate*, exactly
as now. This is the two-phase shape every surveyed framework has, expressed without a builder type and
without hiding `Game`.

**C. A builder around `Game` (ASP.NET shape).** `var builder = StrideApp.CreateBuilder(); builder.Physics.UseBepu(); … var app = builder.Build(); app.Run();`.
Rejected once already in #1253 for the right reason: it puts a toolkit type between the user and the
engine, contradicts the "not hide the engine" promise, and buys nothing over B because Stride's
configuration surface is small and fixed — builders earn their keep where *systems* are composed
(Bevy, DI), and here the systems are the engine's. Not recommended.

> **Recommendation: B, with A's fixes folded in, done as one breaking release.** The front door
> (`new Game()` + `Run(start, update)`) is already the idiomatic C# shape and should not move. The
> layer under it should become a declarative record applied before `start`, replacing the fixed
> `SetupBase*` sequences and the by-name/by-index coupling. The toolkit is in Preview, the change is
> mechanical for the 46 one-call examples (unchanged) and a simplification for the 25 others, and
> it makes the docs true. Upstreaming items 15 and 16 afterwards would let `Run` shrink to sugar.

### Questions before anything is built

- **Q1 — front door.** Agree that `using var game = new Game(); game.Run(start:, update:)` stays
  exactly as is, and no builder type is introduced?
- **Q2 — setup as a record vs a fluent chain.** `new SceneSetup3D { … }` (init-only record, easy to
  test, reads as data) or `game.Run(setup => setup.Base3D().WithSkybox().WithoutGround(), …)`
  (discoverable via IntelliSense, harder to compare/serialize)? Records are recommended; the
  `Camera.At(...).WithController()` sub-builders are where fluency still helps.
- **Q3 — physics selection.** (a) Keep namespace-selected duplicates (`Stride.CommunityToolkit.Bepu`
  vs `.Bullet` both defining `SetupBase3DScene`) — zero change but item 3 stays; (b) one core
  `SceneSetup3D` with `Physics = Physics.Bepu | Bullet | None` where each physics package registers
  a provider — one bundle, no CS0121, the "import one namespace" rule disappears; (c) distinct
  names per package (`SetupBepu3DScene`). (b) is recommended and is the only one that lets a
  no-physics setup (7 examples) be first-class instead of "call `SetupBase3D` and hope".

  **Constraint on (b), from Vaclav (2026-09-02): selecting physics at setup must not prevent using
  both engines in one game.** It may rarely make sense, but it must stay possible. So:

  - **The provider is a default, not a gate.** `Physics = Physics.Bepu` decides only what the
    *implicit* calls mean — `game.Create3DPrimitive(type)` with no engine named, the bundled ground,
    the setup sequence. An implicit call can only ever have one meaning (that is exactly why it is
    ambiguous today with two packages imported); choosing that meaning at setup says nothing about
    what else the game references.
  - **Mixing is the explicit route**, which already exists: the engine-named entity extensions.
    Bepu's `Create3DPrimitive` is core's model-only call followed by `entity.AddBepu3DPhysics(...)`,
    and those names never collide across packages. With shape records the mixed form reads
    `game.Create3DPrimitive(shape).AddBepuPhysics()` beside `game.Create3DPrimitive(shape).AddBulletPhysics()`.
    A deliberately mixed game sets `Physics = Physics.None` so nothing implicit happens and calls
    both engines' world setup explicitly — better than today, where mixing means fully-qualified
    static calls to dodge the collision.
  - **Therefore:** the per-engine entity extensions stay public and are the supported mixing route;
    the setup record must not validate that only one physics package is referenced; and
    `Physics.None` plus explicit calls is a supported shape, not a fallback.
  - **Two limits, stated honestly:** whether two simulations coexist cleanly at runtime is an
    engine question nobody has tested (Bullet's and Bepu's processors are independent and keyed on
    different component types, so nothing suggests a conflict, but the toolkit's job is only to not
    be the thing that prevents it); and in a mixed game the second engine's world setup is the
    caller's explicit call, consistent with everything else on the explicit route.
- **Q4 — packaging.** Rename `Stride.CommunityToolkit.Windows` to what it is
  (`Stride.CommunityToolkit.Build` or `.AssetCompiler`), or fold the asset-compiler reference into
  the core package (item 14 option 2)? Either way the docs' first step becomes one package. Keep
  `WindowsDpiManager` where it is or move it to core behind an OS check?
- **Q5 — engine defaults the toolkit should set on everyone's behalf.** `Run` could register a
  `GameSettings` so the no-settings fallbacks (HRTF, physics, navigation, Bepu, rendering
  settings) become configurable. Should that be part of `Run` unconditionally, part of the setup
  record, or opt-in? *Answered 2026-09-03:* opt-in via `UseGameSettings` (registering
  unconditionally would collide with a project that has the asset — `ServiceRegistry.AddService`
  throws on a duplicate). *Amended 2026-09-05:* the collision hit even the opt-in path, because
  `Game.PrepareContext` registers the loaded asset before any hook; `UseGameSettings` now defers its
  registration to `GameBase.WindowCreated` (after `PrepareContext`, before `Initialize`) and, when
  the asset exists, merges in only the configurations the asset lacks — so it is safe in both kinds
  of project. The shader-compilation-mode half of the original question is moot: on
  D3D11 `Debug` and `Release` produce identical bytecode and Vulkan/D3D12 ignore the level (engine
  doc, correction 4).
- **Q6 — scope of the break.** One release that (i) introduces the record, (ii) fixes return types
  and the two-route duplicates, (iii) renames the package, (iv) migrates all 69 examples — with a
  migration note — or stage it across two releases with `[Obsolete]` forwarding?
- **Q7 — `entity.Scene = rootScene`.** Every example ends entity creation with this line and the
  docs call it "crucial". It is the engine's own idiom and should stay the taught form; should the
  `Create*Primitive` helpers nevertheless take an optional `scene:` argument so one-liners are
  possible, or does that hide the idiom the docs are trying to teach?

### Fix regardless of the answers

Small, non-breaking, and true whatever shape wins:

- `extensions.md:22-23` (skybox claim) and the `"MainCamera"`/`"Main"` XML docs.
- `AddCleanUIStage`: start from `DisableAll()` (the post-effects bug) and keep existing renderers.
  *Done 2026-09-03.* Measured on `E02_3D_Primitives`, vsync off, warm shader
  cache: 116 FPS (8.6 ms) before, 202–211 FPS (4.7–5.0 ms) after. Every example screenshot will
  shift slightly (no SSAO contact darkening, no bloom); regenerate them in one pass.
- Idempotent `AddSkybox`/`AddWorldTextRenderer`/`AddEntityTextRenderer`.
- One `RequireCompositor()` guard with a message naming the call to make.
- Example hygiene already in `TODO.md` §6, plus: `E09_3D_Particles` double skybox,
  `E04_Myra_DraggableWindow` shadowing local function, `Example_Bepu_Playground:50` leftover cast,
  `E06_Box2D:32-36` dead alternatives incl. a method that does not exist,
  `E08_3D_DebugShapes` missing UI stage, `E20_3D_CubeCollapse_BulletPhysics` (bin/obj only).
- A test for `SetupBase3D`/`Add3DCamera` slot handling — today only `Run` is tested.

---

# Upstream (engine) observations

The items above are about the toolkit's own API. The ones below are about what the **engine** makes
the toolkit do for code-only projects. They are recorded here because they decide how much of the
toolkit's code-only layer could ever move into Stride, and which toolkit packages exist only to work
around an engine gap. Each one is something a maintainer could take upstream; none is scheduled.

Context: [Stride issue #1295](https://github.com/stride3d/stride/issues/1295) and
[discussion #1253](https://github.com/stride3d/stride/discussions/1253), which is where the code-only
approach was first proposed and where the toolkit came from.

---

## 14. The asset compiler is required only to copy engine shader sources into `data/db`

**Observation.** A code-only project has no assets of its own, yet it cannot run without the
`Stride.AssetCompiler` build step. The `default.bundle` that step emits (~2.4 MB per project) contains
every engine `.sdsl` file as `/shaders/<Name>.sdsl` plus `StrideDefaultFont`, `StrideDebugSpriteFont`
and the splash screen from `Stride.Engine.sdpkg` - nothing project-specific. At runtime
`ShaderSourceManager` (`Stride.Shaders.Compilers`) resolves shader sources only through the
`IVirtualFileProvider` backed by that database; there is no embedded-resource fallback.

Verified on 4.4.0-beta5 by renaming `data/` under a built `E01_3D_BasicScene` and running it:

```
[EffectCompilerCache]: Warning: Failed to load effect bytecode from application cache: Unable to find shader [LambertianPrefilteringSHNoComputePass1]
[Scheduler]: Error: Unexpected exception while executing a micro-thread.. System.InvalidOperationException: Shader LambertianPrefilteringSHNoComputePass1 could not be found
   at Stride.Shaders.Compilers.ShaderLoaderBase.LoadExternalBuffer(...)
   at Stride.Shaders.Compilers.SDSL.ShaderMixer.MergeSDSL(...)
```

The window opens and stays blank. Note the stack: 4.4's SDSL-to-SPIR-V compiler (`ShaderMixer`,
`SpirvBuilder`) is doing the runtime compilation, so runtime shader compilation is already
cross-platform. The build-time step is not compiling shaders; it is copying their source text.

**Impact.** This single dependency is the reason `Stride.CommunityToolkit.Windows` and
`Stride.CommunityToolkit.Linux` exist - their `.csproj` descriptions say so - and why
`create-project.md` needs two packages instead of one. It also brings the per-project asset build,
the `_StrideCheckVisualCRuntime` registry check, the `obj/` path assumptions that bit the file-based
app example, and a platform-specific package name (`.Windows`) for something that is not about
Windows.

**Options.**

1. Embed the engine `.sdsl` files as assembly resources (or ship a prebuilt `db` as NuGet content in
   `Stride.Rendering` / `Stride.Engine`) and let `ShaderSourceManager` fall back to them when the
   database has no `/shaders/<Name>.sdsl`. The existing `/path` hot-reload lookup keeps working when
   a database is present. Cost is ~2 MB of resources in the engine assemblies. This removes
   `Stride.AssetCompiler` from every code-only project and lets both toolkit platform packages be
   retired. Same treatment for `StrideDefaultFont`, which is the other root asset the bundle carries.
2. Until then, in the toolkit: move the `Stride.AssetCompiler` reference
   (`IncludeAssets="build;buildTransitive"`) into the core package, or rename the platform packages
   to something that says what they do. Editor projects already reference the asset compiler, so a
   duplicate reference unifies rather than conflicts. Breaking for anyone who references `.Windows`
   by name; mechanical.

---

## 15. `SceneSystem` defaults to an empty `GraphicsCompositor` that draws nothing

**Observation.** `SceneSystem`'s constructor sets `GraphicsCompositor = new GraphicsCompositor()`
(`SceneSystem.cs:40`) and `LoadContent` only replaces it when `InitialGraphicsCompositorUrl` points
at an existing asset. With no `GameSettings` asset - the code-only case - the empty compositor stays,
so `new Game().Run()` opens a window and renders nothing, with no warning. The engine already has
`GraphicsCompositorHelper.CreateDefault(...)` in `Stride.Rendering.Compositing`; the toolkit's
`AddGraphicsCompositor()` is a one-line call to it.

**Impact.** The first thing every code-only program has to know is that the compositor exists and
must be set, which is exactly the kind of engine internals code-only is meant to defer. The empty
root `Scene` is already created for this case (issue #1290 was resolved), so the compositor is the one
remaining piece that does not get a usable default.

**Options.** In `SceneSystem.LoadContent`, when `InitialGraphicsCompositorUrl` is null and the
compositor is still the empty default, assign `GraphicsCompositorHelper.CreateDefault(enablePostEffects: false)`.
Editor-created projects always set the URL through `GameSettings`, so they are unaffected. The
toolkit's `AddGraphicsCompositor` would remain as the opt-in for post effects and a clear colour.

---

## 16. There is no post-load hook on `Game`, so `Run(start:)` has to schedule a script

**Observation.** The root scene exists only after `Run()` -> `PrepareContext()` -> `LoadContent()`.
The one instance-level signal the engine offers, `Game.GameStarted`, fires at the end of
`Initialize()` (`Game.cs:403`), *before* `LoadContent`, so the scene is not there yet. The toolkit's
`GameExtensions.Run(start, update)` works around this by adding a microthread to
`game.Script.Scheduler` before calling the engine's `Run`; the microthread runs `start` on the first
frame and loops `update` on `NextFrame()`. It is ~20 lines and uses only public API.

**Impact.** Two things worth knowing, one reassuring and one a gap:

- Running `start` inside a microthread does **not** hide its exceptions. `Scheduler.PropagateExceptions`
  defaults to `true`; a faulting microthread that nothing awaits is rethrown with
  `ExceptionDispatchInfo` from `ScriptSystem.Update`, `GameBase` logs it as
  `[Game]: Error: Unexpected exception` and rethrows, and it escapes `game.Run(...)`. Verified on
  4.4.0-beta5 with a probe that throws from `Start` and, separately, from `Update`: both runs exited
  with a non-zero code and the exception was catchable around `game.Run`. So a typo in `Start` fails
  `dotnet run` loudly, as it should; the message is just logged three times on the way out
  (scheduler, script system, game). The one exception is the live-scripting debugger:
  `GameDebuggerTarget` sets `PropagateExceptions = false`, so under it a faulting `Start` is logged
  and the game keeps running.
- The toolkit side of this has been reshaped (August 2026). `Run` used to offer two overloads told
  apart only by delegate parameter type, `Action<Scene>` versus `Action<Game>`, with `GameContext`
  as the first parameter. Verified against those signatures: an untyped lambda
  `game.Run(start: s => { })` failed with **CS0121** (ambiguous), and an `async` lambda passed to
  the `Action<Scene>` overload compiled silently as `async void`, so the update loop started before
  `start` finished and any exception inside it bypassed the game. The `Action<Game>` overload also
  handed back nothing the caller did not already hold (they call `game.Run` on it) and its `update`
  dropped `GameTime`. The current shape keys everything on `Scene` and pairs sync/async by return
  type, which C# resolves cleanly for method groups and lambdas alike:

  ```csharp
  Run(this Game game, Action<Scene>? start = null, Action<Scene, GameTime>? update = null, GameContext? context = null)
  Run(this Game game, Func<Scene, Task> start,     Action<Scene, GameTime>? update = null, GameContext? context = null)
  ```

  `update` is deliberately synchronous: an async `start` that runs its own
  `while (true) { ...; await game.Script.NextFrame(); }` already *is* an async update loop (the
  `AsyncScript.Execute` idiom), so an async `update` would double the overloads for nothing.
  `context` moved last because nobody passes it positionally and it is the least-used parameter -
  it exists for embedding in a host control, a fixed initial size, or the SDL/headless backends.

**Options.** Upstream, either move `Run(start, update)` into `Game` as instance overloads - it
transplants as-is - or add an instance event raised after `LoadContent` (the composable form
suggested in discussion #1253) and make `Run(start:)` sugar over it.

---

## 17. A consumer-facing MSBuild SDK would hide the package list, but matters less if 14 lands

**Observation.** The engine repository has `sources/sdk/Stride.Build.Sdk`, but its README says it is
consumed by direct `Import` from the source tree and the `Sdk="Stride.Build.Sdk"` NuGet mode is not
used. There is no `Stride.Sdk` a code-only project could name in `<Project Sdk="...">` or, in a
file-based app, `#:sdk Stride.Sdk`; the file-based example instead carries four `#:` lines and a
commented-out `obj/` workaround.

**Impact.** Low on its own. The `#:` lines are short, and most of what an SDK would hide is the
asset-compiler wiring from item 14. If that lands, the remaining boilerplate is one or two package
references, which is not worth an SDK.

**Options.** Defer until item 14 is decided. If the asset compiler stays required, a thin
`Stride.Sdk` that composes `Microsoft.NET.Sdk`, references the engine packages and the asset
compiler, and sets the host RID (what `examples/Directory.Build.props` does by hand) would make the
code-only front door `#:sdk Stride.Sdk` followed by `using var game = new Game();`.

---

## 20. Ranked upstream changes that would shrink the toolkit's bootstrap layer

*Added 2026-09-02 alongside item 19. Items 14–16 above are the three engine facts that force the
toolkit's shape; this item ranks them against the rest of what the 2026-09 engine sweep found, by
payoff ÷ size, so an upstream conversation can start from the cheapest wins. Each is something the
toolkit currently works around; none is scheduled.*

**Surgical — a few lines each**

1. **Default compositor when none is configured** (= item 15). `SceneSystem.cs:40` constructs an
   empty `GraphicsCompositor`; `LoadContent` replaces it only when `InitialGraphicsCompositorUrl`
   names an existing asset (`:127-132`). Fallback to `GraphicsCompositorHelper.CreateDefault(enablePostEffects: false)`
   and `new Game().Run()` renders. Toolkit effect: `AddGraphicsCompositor` becomes the post-effects
   opt-in rather than the "nothing renders without this" call.
2. **Post-load hook on `Game`** (= item 16). An instance event after `LoadContent`, or
   `Run(Action<Scene> start, Action<Scene, GameTime> update)` overloads on `Game` itself — the
   toolkit's `RunCore` transplants as-is. Toolkit effect: `Run` becomes sugar or disappears.
3. **Make `CompilationMode` mean what it says.** `Game.cs:383-386` calls
   `EffectSystem.SetCompilationMode` only when `Settings != null`, so code-only games run on
   `EffectCompilerParameters.Default` (`Debug = true, OptimizationLevel = 0`). That sounded like
   "unoptimised forever" until it was built and measured (2026-09-03): the D3D11 compiler applies
   the level only when `Debug` is false (`Direct3D/ShaderCompiler.cs:84-96`), so `Debug` and
   `Release` both compile at FXC's default level 1 with symbols and yield identical stripped
   bytecode; only `AppStore` differs, and Vulkan/D3D12 never read the level. The honest upstream
   ask is smaller: fix the `Debug` branch (or the enum's docs) so the modes are distinct, and apply
   a mode when settings are absent so `AppStore` is reachable without an asset. *Toolkit-side:*
   `UseGameSettings` (2026-09-03) covers item 4 below for everything except the asset URLs and
   lets a code-only game choose `AppStore`.
4. **A public way to supply `GameSettings`.** `Game.Settings` is private-set (`Game.cs:56`); the only
   route is registering an `IGameSettingsService` before `Run`. A `Game(GameSettings)` constructor or
   setter fixes the whole no-settings fallback table at once: rendering profile, `CompilationMode`,
   streaming, HRTF, Bullet/Bepu configuration, `RecastMeshSystem` throwing, and the "could not find
   game settings" warning the examples silence with `Stride.AssetCompiler.Overrides.targets`.
5. **`CreateDefault` parameters for what it leaves out.** Instancing (`InstancingRenderFeature`,
   item 8), particles (`ParticleEmitterRenderFeature`), and `LightProbeRenderer` are all omitted by
   the default compositor and fail silently when missing. Three optional booleans on
   `GraphicsCompositorHelper.CreateDefault` retire `AddInstancingSupport`, `AddParticleRenderer` and
   the light-probe gap in one go.

**Medium — one focused PR, the largest single payoff**

6. **Engine shaders and the default font as embedded resources** (= item 14). `ShaderSourceManager`
   falls back to assembly resources when `/shaders/<Name>.sdsl` is not in the database. Removes
   `Stride.AssetCompiler` from every code-only project and, with it, the toolkit's `.Windows`/`.Linux`
   packages, the per-project asset build, the `obj/` path bug, the SSH.NET NU1903 pull that breaks
   DocFX, the VC++ redist check, and the one-app-per-folder rule for file-based apps. The docs'
   first step becomes one package. Cost: ~2.4 MB of resources in the engine assemblies.

**Smaller reach, still worth filing**

7. `GameWindowHeadless` honours `IsUserManagingRun` (+ a `GameContextHeadless` ctor parameter) —
   manual ticking for servers and tests; and `GraphicsDevice.Vulkan.cs:450` skips the
   `VK_KHR_swapchain` requirement when there is no window, so Linux CI runs without Xvfb.
8. `RecastMeshSystem.cs:54` falls back to defaults instead of `GetSafeServiceAs<IGameSettingsService>()`
   throwing; `DynamicNavigationMeshSystem` gets a documented public enable (it is auto-registered
   but constructed `Enabled = false`).
9. `NavigationMeshBuilder.Add(Vector3[] vertices, int[] indices, Matrix)` — navigation input for
   non-Bullet physics (`NavigationMeshInputBuilder` is internal).
10. `MaterialBlendLayer` resolving through `Material.Descriptor`, which `Material.New` leaves null —
    runtime layered materials fail with "Unable to find material" (engine doc, compact spec 82).

**Not worth proposing:** a builder or "code-only app model" in the engine. Discussion #1253 settled
it, the configuration surface is small and fixed, and with 1–4 above the engine's own `Game` is a
good code-only front door on its own.

**Target state if 1, 2, 3 and 6 land:** one package (`Stride.Engine` plus a physics package),
`using var game = new Game(); game.Run(scene => { … });`, a window that renders, release shaders,
no asset compiler. The toolkit then provides only what it should — primitives, camera controllers,
lighting rigs, debug tooling — and item 19's setup record becomes an optional convenience rather
than the thing that makes code-only viable.
