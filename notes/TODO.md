# TODO

Work that is agreed and waiting, ordered by what to do first. Distinct from
[ARCHITECTURE.md](ARCHITECTURE.md), which is unagreed API-design observations.

Most of this came out of one long session investigating a `Body2DComponent` memory runaway; the
measurements referred to below are from that work and are recorded in
`docs/manual/physics-extensions/bepu-transform-ownership.md`. Section 2 came out of a later audit of
every 2D example, prompted by the spawn menu freezing on the default Polygon.

---

## 1. Finish PR stride3d/stride#3349

Reviewers have been waiting since 11 Aug. The `OutOfPlaneInertiaScale` fix is already applied to the
`bepu-2d` branch and builds clean; what remains is the review feedback.

- **Mechanical review fixes** — drop the invented "Stride's default scale" phrasing (2 places), drop
  "simulated by Bepu's ordinary 3D solver", assign velocities unconditionally instead of
  read-compare-write (2 places), mutate `inertia.InverseInertiaTensor.XX` directly rather than
  copy-modify-assign, use `<para/>` instead of empty `///` lines, and restructure the
  `SimulationUpdate` / `AfterSimulationUpdate` summaries to say what the method *is*.
- **`ZTolerance` should throw** — reject non-finite and non-positive values instead of silently
  reverting to the default; pair with `[DataMemberRange]` so Game Studio cannot produce a bad value.
- **Delete the hull tuning block** — two of its three lines are no-ops against Stride's defaults, and
  the third (`MaximumRecoveryVelocity`) was measured to make no difference to the runaway, 3/3. Put
  that measurement in the reply, since removing it answers two reviewer objections at once.
- **Split the docs by audience** — observable consequences (Z velocity is engine-managed, energy is
  not conserved the way a native 2D solver would) belong in user docs; mechanism belongs in code
  comments. The toolkit's copy of `Body2DComponent` needs the same treatment.
- **Reply to Eideren** — lead with the rank table (full tensor fine, all-zeroed fine, partly-zeroed
  fails 3/3 at 20k), then answer the constraint question. He is waiting on this to choose a design.

## 2. 2D shapes — found by auditing the 2D examples

The first three are the same investigation: the 2D spawn menu froze whenever several default
Polygons landed on each other. Full write-up in
`notes/upstream/bepu-hull-contact-nan.md`, runnable repro in `examples/code-only/_Temp2DProbe`.

- **Bepu: hull-vs-hull contact depth is `NaN` for some extruded polygons** — two regular-polygon
  hulls (circumradius 0.5, depth 1) overlapping with an offset in *both* X and Y get a manifold with
  `depth = NaN` on the first timestep; normal and offsets are fine. Reproduces in bare Bepu with no
  Stride, no `Compound` and no `Body2DComponent`, so nothing in the toolkit causes it. Sides 6 and
  32 fail, the rest are clean — but the failing set is knife-edge sensitive: computing the vertex
  angle as `(i * Tau) / sides` instead of `i * (Tau / sides)` also breaks sides 10. That points at
  numerical robustness in the depth refinement, not at hexagons. **Report upstream** — the write-up
  has a verified one-file repro with `sides` as a loop variable.
- **Bepu: `Tree.Add` never returns once a pose is `NaN`** — the freeze users actually see. The
  application stops responding with one core pegged; sampled twice, 100 s of CPU apart, always
  inside `Tree.Add` with about ten bodies in the tree. Worth reporting separately: it turns a
  recoverable `NaN` into a hard hang with no diagnostic, and it is the same unguarded-recursion
  family as the `Tree.Refit2WithCacheOptimization` item below.
- **A Bepu fix would close the spawn-menu freeze outright** — the chain is fully traced: NaN contact
  depth, then NaN poses in the same step, then `Tree.Add` never returning. Nothing else in the
  toolkit contributes, so fixing contact generation removes the freeze without any change here. Two
  caveats: the `Tree.Add` hang would still turn *any* future `NaN` into a freeze rather than an
  error, which is why it is worth reporting separately; and the trigger is narrower than it first
  looked — holding the spawn key on Polygon freezes within a handful of bodies, but the example's
  normal seven-shape mix (about 57 hexagons among 400 bodies in one column) was clean over 2 runs.
- **Decide what the toolkit does in the meantime** — `Primitive2DModelType.Polygon` defaults to
  `Sides = 6`, which is one of the failing cases, so the default shape freezes the app when two of
  them collide. Changing the default to 5 or 7 would dodge it today, but the sensitivity result
  above shows that is luck, not a fix: an unrelated change to how vertices are computed moves the
  failing set. Preference: leave the default, document the hazard next to
  `PolygonProceduralModel.Sides`, and let the upstream fix land.
- **`Create2DPrimitive` writes back into the caller's options** — `options.Size ??= ...` for Capsule
  and Rectangle mutates the object the caller passed in
  (`src/Stride.CommunityToolkit/Games/GameExtensions.cs:72`). Reusing one options instance across
  shapes silently carries the size over; measured: a capsule then a circle from the same instance
  gives a circle of radius 0.25 instead of 0.5. `AddBepu2DPhysics` explicitly encourages that reuse
  in its own comment. Fix is a local variable instead of a write-back; the mutation buys nothing,
  since the class defaults already agree with the collider defaults.
- **`Depth` is ignored unless `Size` is also set** — for Square, Circle and Triangle the null-size
  branch of `Get2DColliderShape` returns a collider with its own default Z (1), so
  `new() { Depth = 0.2f }` alone silently keeps a collider 1 unit deep. Rectangle, Capsule and
  Polygon honour it. Same file, `src/Stride.CommunityToolkit.Bepu/EntityExtensions.cs:112`.
- **Square's collider ignores `Size.Y`** — `new() { Size = new(size.Value.X, size.Value.X, depth) }`
  while the mesh is built from `Size` as given, so a Square with `Size = (2, 1)` draws 2x1 and
  collides as 2x2. Either use both components or reject a non-square size.
- **Capsule throws for a wide capsule** — `Length = size.Y - 2 * size.X` goes negative when
  `Size.Y < 2 * Size.X`, and Stride's `CapsuleCollider.Length` setter validates greater-than-zero,
  so `Size = (0.5f, 0.8f)` throws. The mesh silently clamps to `radius * 2.01f` instead. Pick one
  behaviour and apply it to both.
- **`PolygonCollider` is the only hull collider not using `SharedHullCache`** — cone, teapot, torus
  and triangular prism all share hulls; polygon builds a fresh `DecomposedHulls` per body. Confirmed
  from a running scene: two polygon bodies got shape indices 1 and 2. That is the per-body hull cost
  and the static-`BufferPool`-from-a-finalizer race, both already documented, still live on one path.
- **Bullet's 2D collider mapping is missing shapes and flags** —
  `src/Stride.CommunityToolkit.Bullet/EntityExtensions.cs:135`: Triangle and Polygon fall through to
  a message-less `InvalidOperationException`; the null-size branches for Circle and Capsule omit
  `Is2D = true` that the sized branches set; Rectangle and Square hardcode Z to `0` and the `depth`
  parameter is never read; and Square uses `Size.Y` where the Bepu path uses `Size.X`.

## 3. Toolkit memory work — measured payoff

- **Shared-model helper** — `Create3DPrimitive` builds a mesh and a pair of GPU buffers per call, so
  10,000 spheres cost 1.5 GB against 400 MB when the model is shared. Retires a workaround that
  Example01, Example22 and the stress pile all hand-roll.
- **`NumberOfTextureCoordinates = 1`** — the default of 10 inflates every vertex from 48 to 84 bytes
  by duplicating one UV ten times. One line, 43% off every mesh the toolkit generates.
- **Cached mesh mutation bug** — `PrimitiveProceduralModelBase.Generate` mutates `data.Vertices` in
  place for `LocalOffset` and `Scale`, and five 2D procedural models hand out *shared* cached
  instances. Silent and cumulative: the second model built with a `Scale` comes out wrong.

## 4. Toolkit correctness and tidy-up

- **Rewrite `ModelComponentExtensions.GetMeshData`** — it does `*(Vector3*)(bytePtr + vHead)`, which
  assumes the position is the first element of every vertex and is a full `float3`. Any imported mesh
  with a different layout silently yields garbage. Rewrite on `VertexBufferHelper` from Stride
  PR #2858 (present in 4.4.0-beta5); it is a net deletion and removes `unsafe` from the file.
- **Drop the `.ToArray()` round-trips** — five procedural models allocate the array, wrap it in a
  `Span`, then copy it into a second array. Matches Stride's own direction in #2368 / #2369.
- **Cache hygiene** — the procedural model caches are plain `Dictionary` mutated without
  synchronisation, unbounded, and `PolygonProceduralModel` builds a string key per vertex per lookup.
- **Four small ones** — `Get3DColliderShape` throws a message-less `InvalidOperationException` for
  `InfinitePlane`; `Procedural2DModelBuilder` ignores its `depth` argument for the mesh while the XML
  doc claims it makes the shape 3D; `Capsule2DProceduralModel` has a branch that is always taken and
  four lines of commented-out code; `PolygonProceduralModel` validates its points twice.
- **Delete `DebugTextDropdown.Draw()` and `Position`** — unused by anything now that both examples
  register overlay sections, and it is exactly the trap the spawn-menu example fell into: a dropdown
  drawn standalone ignores the overlay's reposition and hide keys.

## 5. Upstream reports and PRs

- **Tell Norbo about bepu #2495** — the technique he advised there, an infinite moment of inertia
  about a *specific* axis, is the exact configuration that corrupts the narrow phase. The
  highest-value report we have, because it is his own recommendation. Now reproduced with no Stride
  at all, single-threaded and deterministic 3/3, and it needs *both* a rank-1 tensor and a
  triangular-prism hull — cube hull, box and sphere are all fine with the same tensor. Write-up in
  `notes/upstream/bepu-rank1-inertia-corruption.md`, runnable harness in `examples/code-only/_Temp2DProbe`.
- **Bepu: `Tree.Refit2WithCacheOptimization`** — unguarded recursion, stack overflow from a perfectly
  regular lattice of touching bodies, deterministic in about ten seconds. Precedent: commit
  `7d0b80d4` fixed the same class of bug for tree *queries*; `Tree_Refit2.cs` is untouched since 2023.
- **Stride: `RestoreHelper` RID fix** — verified working in the local clone, ready to PR.
- **Stride: cylinder over-allocation** — `GeometricPrimitive.Cylinder` reserves `tessellation * 4`
  vertices and writes `* 2`, shipping 64 zeroed vertices per cylinder to the GPU. One character.
- **Stride: `ConvexHullCollider` finalizer** — returns unmanaged buffers to a *static* `BufferPool`
  from a finalizer, racing the simulation's worker threads.
- **Bepu: intermittent `AccessViolationException`** — no longer unexplained, and no longer a
  separate report. It is the multithreaded face of the rank-1 inertia corruption above: the same
  configuration dies as an `AccessViolationException` in `NarrowPhase.ExecutePreflushJob` with many
  threads and as a deterministic `IndexOutOfRangeException` in `NarrowPhase.UpdateConstraint` with
  one. `Stack overflow`, `Internal CLR error (0x80131506)` and silent process death are the same
  corruption surfacing elsewhere. Fold into the report rather than filing separately.
- **`Directory.Packages.props` cleanup** — four dead `PackageVersion` entries, the
  `ServiceWire` → `System.IO.Pipes 4.3.0` chain behind most of the legacy packages, and the SSH.NET
  advisory.
- **Stride docs: instancing manual page** — the manual has no page on instancing at all
  (`grep -ri instancing en/manual` returns nothing). A full draft exists: title, issue body, proposed
  location and page content.
- **NuGet signature verification** — raise `DOTNET_NUGET_SIGNATURE_VERIFICATION` with maintainers.

## 6. Example hygiene

Small, found while auditing every 2D example. All nine build clean.

- **Three 2D examples have no `---example-metadata` block at all** —
  `Example01_Basic2DScene_DebugRender`, `Example18_Box2DPhysics` and `Example_2D_Playground`. The
  first two are legitimate examples that simply never got one.
- **`Example18_Box2DPhysics2` is an empty directory** — delete it, or say what it was meant to be.
- **Metadata key casing and duplicate orders** — `Order:` in Primitives and SpawnMenu against
  `order:` everywhere else; order `1` is used by both `Example01_Basic2DScene` and
  `Example01_Basic2DScene_BulletPhysics`, and `2` by both FallingShapes and Primitives. Low priority,
  since the ordering scheme is expected to change anyway.
- **`Example_2D_Playground` is a scratch file, not an example** — commented-out blocks throughout,
  unused usings (`System.Xml.Linq`, `System.Reflection`), and it calls `Add3DGround` and
  `Add3DCameraController` in a 2D playground. Either finish it or drop it.

## 7. Revisit only if Bepu fixes the rank-1 tensor

Do not act on this speculatively — it is here so the question is not re-derived later.

- **`OutOfPlaneInertiaScale` could go back to `= 0`, but probably should not.** If Bepu makes a
  rank-1 inverse inertia tensor safe, zeroing becomes viable again and is marginally more exact:
  truly infinite out-of-plane inertia rather than 10,000× stiff. Against that: it would require a
  minimum Bepu version the toolkit cannot enforce (Bepu comes in through whatever Stride resolves),
  and the measured behaviour is indistinguishable — every scale from 1e-1 to 1e-12 is stable, so the
  constant is not load-bearing for the crash. Reverting trades a version dependency for no
  observable gain.
- **Watch which way the fix goes.** If Bepu instead decides a rank-1 tensor is unsupported and adds
  validation, scaling becomes the only option and this item is closed.

## 8. Later

- **Custom one-body 2D constraint** — only once Eideren has picked a direction. Prototype in the
  toolkit first, where nothing needs review, score it against the current approach with the harness,
  then a follow-up PR. Stride's own `CharacterMotionConstraint` is a complete worked template, and
  `Solver.Register<T>()` is public, so no engine change is needed.
- **`related:` metadata sweep** — cross-link the two new examples across all example metadata.
- **Keep `examples/code-only/_TempMemProbe`** — the memory measurement rig, and no longer a deletion
  candidate. It is the only thing that can say whether the constraint work above is actually an
  improvement, and it stays useful for any future allocation question.
- **Keep `examples/code-only/_Temp2DProbe`** — the 2D rig: shape/side/radius/depth sweeps, a
  pose-`NaN` detector, a hull inspector, and two Stride-free Bepu reproductions. The write-ups moved
  to `notes/upstream/`; the runnable repros stay here, and they are the fastest way to re-check any
  of the Bepu claims above.

---

## Two things to watch, not tasks

**Do not tune friction yet.** Bepu's August fix removes a `1/N` contact-count divisor, making friction
2–4× stronger and, more importantly, consistent regardless of how many contacts a manifold has. Ross
retuned his own demos in the same commit (`FrictionDemo` went from `3` to `0.75`). It is **not** in
`2.5.0-beta.28`, which is what Stride 4.4.0-beta5 resolves to. Anything tuned today will be far too
grippy when Stride bumps.

**The `AccessViolationException` is explained.** It was thought to be a separate, unexplained
failure from the partly-zeroed inertia tensor. It is not: a pure-Bepu repro produces it from exactly
that tensor, and reduces it to a deterministic `IndexOutOfRangeException` when run single-threaded.
The fix already shipped (scaling instead of zeroing) covers it. See
`notes/upstream/bepu-rank1-inertia-corruption.md`.

**Beware spawn overlap when measuring.** Bodies spawned already interpenetrating produce non-finite
contacts in *every* configuration, including an untouched inertia tensor and analytic shapes. A first
pass at the pure-Bepu repro used a grid spacing narrower than the shape and appeared to show that the
inertia lock was irrelevant. It is not — with a spacing wider than the shape, only the rank-1 tensor
fails. Space the grid wider than the shape before drawing any conclusion.
