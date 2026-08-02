# Plan: Build-time asset YAML generator for code-only projects

**Repo:** `stride-community-toolkit`
**Status:** Ready to implement. Manually validated (a hand-written `.sdsnd` + `RootAssets` entry compiles and loads at runtime).
**Goal:** A beginner drops `wood-tap-5.mp3` into `Resources/`, builds, and calls
`game.Content.Load<Sound>("wood-tap-5")`. No Game Studio, no hand-written YAML.

---

## 1. Context and approach

Code-only Stride projects can already use any asset type — the asset compiler runs on build
and handles the full pipeline (ffmpeg decode + Celt encode for audio). The only thing missing
is the small YAML metadata file that Game Studio would normally author.

Three approaches were considered:

| Approach | Verdict |
|---|---|
| Runtime `ContentManager.Save` + runtime encoding | Rejected. Requires reimplementing the compiler's Celt encoding at runtime, and `Sound`'s setters are `internal` to `Stride.Audio`. Only suitable for genuinely dynamic (downloaded/generated) audio. |
| Engine change: auto-import raw files in-memory at build | Rejected for now. Requires an engine release, and produces no files — so Game Studio can never see or edit these assets. |
| **Toolkit generates the same YAML files Game Studio writes** | **Chosen.** No engine change. Files are real, committed project metadata, so a code-only project stays openable in Game Studio. |

The decisive advantage of the chosen approach: it moves code-only projects *toward*
Game Studio compatibility instead of creating a parallel invisible pipeline.

---

## 2. Verified engine facts

These were verified by reading the Stride source (`D:\Projects\GitHub\stride`, branch `master`,
4.4.0-dev). **Do not re-derive; do not assume otherwise.** Line references are for context —
re-check them if the engine has moved on.

### 2.1 `RootAssets` is mandatory — this is the non-obvious one

`RootPackageAssetEnumerator` (`sources/assets/Stride.Core.Assets/Compiler/RootPackageAssetEnumerator.cs:56-89`)
compiles only:
1. assets listed in `Package.RootAssets`,
2. assets reachable as dependencies of those,
3. asset types marked `AlwaysMarkAsRoot`.

`SoundAsset` is **not** `AlwaysMarkAsRoot` — it declares a plain `[AssetDescription(FileExtension)]`
(`sources/engine/Stride.Assets/Media/SoundAsset.cs:12-14`). Only effects, game settings and script
source files carry that flag.

In a code-only project nothing references the sound (no scene), so **without a `RootAssets` entry
in the `.sdpkg` the asset is silently culled from the build.** The generator therefore has two
outputs, not one: the `.sdsnd` *and* an updated `.sdpkg`.

This also means the asset `Id` appears in two files and must match.

### 2.2 Package file location and implicit defaults

- The package for a project is `$(StrideCurrentPackagePath)` if set and existing, otherwise
  `$(MSBuildProjectDirectory)\$(MSBuildProjectName).sdpkg`
  (`sources/core/Stride.Core/build/Stride.AssetBuildManifest.targets:220-221`).
  **The generated `.sdpkg` must be named after the project.**
- If no `.sdpkg` exists, `Package.LoadProject` creates an implicit package with
  `AssetFolders = { "Assets" }` and `ResourceFolders = { "Resources" }`
  (`sources/assets/Stride.Core.Assets/Package.cs:646-656`) — but an implicit package has empty
  `RootAssets`, so per 2.1 the asset still won't compile. A real `.sdpkg` is required.
- Asset files are discovered by recursively scanning each `AssetFolders` entry
  (`sources/assets/Stride.Core.Assets/Package.cs:1231-1290`), so subfolders under `Assets/` work.

### 2.3 Build ordering

- `StrideWriteAssetBuildManifest` runs `AfterTargets="ResolveProjectReferences"`
  (`Stride.AssetBuildManifest.targets:181-183`) and records the package path.
- `StrideCompileAsset` runs `AfterTargets="CopyFilesToOutputDirectory"`
  (`sources/assets/Stride.AssetCompiler/build/Stride.AssetCompiler.targets:191`).

**The generator must run before `StrideWriteAssetBuildManifest`**, so the `.sdpkg` and `.sdsnd`
exist by the time the manifest is written and the compiler scans folders.

### 2.4 `SoundAsset` schema

From `sources/engine/Stride.Assets/Media/SoundAsset.cs`:

- `[DataContract("Sound")]` → YAML tag is `!Sound`
- `AssetFormatVersion(..., CurrentVersion = "2.0.0.0")` → `SerializedVersion: {Stride: 2.0.0.0}`
- extension `.sdsnd`
- Defaults: `Index = 0`, `SampleRate = 44100`, `CompressionRatio = 10`,
  `StreamFromDisk = false`, `Spatialized = false`
- `Source` comes from `AssetWithSource` and serializes as `Source: !file <path relative to the asset file>`

### 2.5 Recognised audio extensions

`RawSoundAssetImporter.FileExtensions` (`sources/engine/Stride.Assets/Media/RawSoundAssetImporter.cs:12`)
is `.wav,.mp3,.ogg,.aac,.aiff,.flac,.m4a,.wma,.mpc` **plus the video extensions** (a video file can
carry an audio track).

For v1, use only the unambiguous audio list — `.wav .mp3 .ogg .aac .aiff .flac .m4a .wma .mpc` —
so `.mp4` isn't silently turned into a sound asset.

### 2.6 Can we reuse Stride's own serializer?

`AssetFileSerializer.Save(filePath, asset, yamlMetadata: null)` is public
(`sources/assets/Stride.Core.Assets/AssetFileSerializer.cs:115-130`) and would produce exactly
correct YAML. **But** using it requires:

- referencing `Stride.Core.Assets` (for the serializer) *and* `Stride.Assets` (for `SoundAsset`);
- both are editor-side packages targeting `$(StrideXplatEditorTargetFramework)` and drag in
  `Microsoft.Build`, `Microsoft.CodeAnalysis.*Workspaces`, `FFmpeg.AutoGen`,
  `Microsoft.TemplateEngine.*`, `SSH.NET` and `SixLabors.ImageSharp`;
- bootstrapping `AssetRegistry` — `GetDefaultExtension` only works once the asset assemblies are
  registered through `AssemblyRegistry` with the `Assets` category
  (`sources/assets/Stride.Core.Assets/AssetRegistry.cs:896-916`).

Loading MSBuild-dependent assemblies *inside an MSBuild task* is a well-known source of version
conflicts, and this weight is unjustifiable for emitting ~8 lines of YAML.

**Decision: v1 writes YAML from templates.** Keep the writer behind an interface
(`IAssetYamlWriter`) so an `AssetFileSerializer`-backed implementation can be swapped in later if
the engine ships a lightweight serializer package (see §7).

There is precedent in Stride itself for line-based handling of these files:
`sources/tools/Stride.TemplateGenerator/TemplatePreprocessor.cs:947` (`CollectRootAssets`) parses
the `RootAssets:` section of a `.sdpkg` line by line rather than with a YAML parser.

---

## 3. Golden reference files

Validated, working example — `examples/code-only/Example_CubicleCalamity`.

`Resources/wood-tap-5.mp3` + `Assets/wood-tap-5.sdsnd`:

```yaml
!Sound
Id: daf2da16-0f0e-45fd-b080-43dd9a5d7266
SerializedVersion: {Stride: 2.0.0.0}
Source: !file ../Resources/wood-tap-5.mp3
SampleRate: 24000
CompressionRatio: 15
StreamFromDisk: false
Spatialized: false
```

`Example_CubicleCalamity.sdpkg`:

```yaml
!Package
SerializedVersion: {Assets: 3.1.0.0}
AssetFolders:
    -   Path: !dir Assets
    -   Path: !dir Effects
RootAssets:
    -   daf2da16-0f0e-45fd-b080-43dd9a5d7266:wood-tap-5
```

A fuller Game Studio-authored package for comparison —
`D:\Projects\GitHub\stride-example-projects\MyGame01\MyGame01.Game\MyGame01.Game.sdpkg`:

```yaml
!Package
SerializedVersion: {Assets: 3.1.0.0}
Meta:
    Name: MyGame01
    Version: 1.0.0
    Authors: []
    Owners: []
    Dependencies: null
AssetFolders:
    -   Path: !dir Assets
ResourceFolders:
    - !dir Resources
```

Note the two indentation styles: `AssetFolders` entries use `-   Path:` (dash + 3 spaces),
`ResourceFolders` uses a plain `- !dir`. Match these exactly — they are what Game Studio emits.

Both `Example_CubicleCalamity` (audio) and `MyGame01.Game` (textures, materials, scenes) are the
reference corpus. Keep a copy of the relevant files as test fixtures.

---

## 4. Design rules

1. **Deterministic IDs.** Derive the asset `Id` GUID from the project-relative resource path
   (e.g. MD5 of `"sound:Resources/wood-tap-5.mp3"`, using the 16 bytes directly as a GUID).
   Same input → identical bytes forever. Non-negotiable: a churning `Id` changes both the
   `.sdsnd` and the `.sdpkg` on every build, producing noisy diffs and defeating the asset
   compiler's incremental cache.
2. **Never overwrite an existing asset file.** If `Assets/wood-tap-5.sdsnd` exists — hand-written,
   or generated then tweaked in Game Studio — skip it entirely. File existence is the whole
   ownership test; no marker comments, no tracking manifest. This gives the escalation path for
   free: defaults for beginners, full control for anyone who edits the file.
3. **Write only when content differs.** For both `.sdsnd` and `.sdpkg`, compare before writing.
   Touching timestamps on every build defeats `StrideCompileAsset`'s up-to-date check.
4. **Merge the `.sdpkg`, never regenerate it.** Users have hand-authored content — the
   `Example_CubicleCalamity` package has an `Effects` asset folder that must survive. Add only
   missing `AssetFolders` / `ResourceFolders` / `RootAssets` entries; leave everything else,
   including key order and `Meta`, byte-identical. If no `.sdpkg` exists, create a minimal one
   in the shape shown above.
5. **Orphans produce a warning, never a deletion.** If a `.sdsnd` points at a missing `Source`,
   emit an MSBuild warning naming the file and telling the user to delete it or restore the
   source. The generator does not track what it created, so it must never delete.
6. **Mirror subfolders.** `Resources/sfx/boom.mp3` → `Assets/sfx/boom.sdsnd`, asset location
   `sfx/boom`, `Source: !file ../../Resources/sfx/boom.mp3`. This avoids the name collisions that
   a flat naming scheme would produce.
7. **Generated files are committed.** They are deterministic, and Game Studio needs them present
   when opening the project (it does not run the MSBuild target). Document this.

---

## 5. Implementation

### Phase 1 — Core library

New project `src/Stride.CommunityToolkit.AssetGenerator/` (netstandard2.0 or net10.0; see
Phase 2 for how it is hosted). No Stride package references.

- `ResourceScanner` — enumerates the resource folder, filters by known extensions, returns
  project-relative paths.
- `DeterministicId` — path → GUID.
- `SoundAssetTemplate` (implements `IAssetYamlWriter`) — emits the `.sdsnd` text.
- `PackageFileEditor` — line-based read/merge/write of `.sdpkg`, preserving unknown content.
  Handles: file missing, section missing, section present but empty (`RootAssets: []`),
  section present with entries.
- `AssetGenerator` — orchestrates; returns a result object listing created files, skipped files
  and warnings (do not log directly from the core library, so it stays testable).

Per-file option overrides (`SampleRate`, `CompressionRatio`, `StreamFromDisk`, `Spatialized`)
should be plumbed through from the start, even if nothing sets them yet in v1 — they only affect
newly created files.

### Phase 2 — MSBuild integration

Host the generator as a **console tool** invoked via `Exec`, packed under `tools/net10.0/`, rather
than as an MSBuild `Task`. Rationale: MSBuild locks task assemblies (painful when the toolkit repo
both builds and consumes the tool), and a task would need `netstandard2.0;net472` dual-targeting
for VS. A tool process also matches the pattern the audience already runs (Stride's own
`Stride.AssetCompiler` is invoked exactly this way — see `Stride.AssetCompiler.targets:191-209`).

Ship `build/Stride.CommunityToolkit.AssetGenerator.targets` in the **main `Stride.CommunityToolkit`
package** so beginners need no extra reference.

```xml
<Target Name="StrideToolkitGenerateAssets"
        BeforeTargets="StrideWriteAssetBuildManifest"
        Condition="'$(StrideToolkitGenerateAssets)' != 'false'">
  <!-- Exec the tool; pass project dir, project name, Assets/ and Resources/ folder names -->
</Target>
```

- Default **on**, opt out with `<StrideToolkitGenerateAssets>false</StrideToolkitGenerateAssets>`.
  Justified because the generator is non-destructive (only ever adds files) and the entire point
  is zero-config for beginners. If this feels too intrusive during review, flip to opt-in and
  revisit after real-world validation.
- No-op quickly when the `Resources/` folder is absent — most projects.
- Surface warnings as real MSBuild warnings with a stable code (e.g. `STCT0001`).

### Phase 3 — Tests

`tests/` — the core library is pure file-in/file-out, so cover it directly:

- deterministic ID stability (golden values pinned in the test);
- generated `.sdsnd` byte-matches the committed `Example_CubicleCalamity` fixture;
- existing `.sdsnd` is not overwritten (including when its contents differ from what would be generated);
- `.sdpkg` merge preserves the `Effects` asset folder and existing `Meta`;
- merge is idempotent — running twice produces identical bytes and reports no writes on the second run;
- subfolder mapping and relative `Source` path correctness;
- orphan detection warns and deletes nothing.

Plus one end-to-end check: delete the generated files from a scratch copy of
`Example_CubicleCalamity`, build, and assert they are regenerated identically and the build
succeeds.

### Phase 4 — Docs

Add a `docs/manual/` page: drop files in `Resources/`, build, load by name; explain that generated
files are committed, that editing them (or opening the project in Game Studio) is supported and
the generator will not clobber the edits, and how to opt out.

### Phase 5 — Beyond audio (follow-up, not v1)

The structure is per-type templates, so textures (`.sdtex`) come next — `MyGame01.Game`'s
`Skybox texture.sdtex` is the reference. Note textures need a `Type:` block
(`!ColorTextureType` etc.), so the template is slightly richer than audio's. Do not start this
until audio is shipped and validated.

---

## 6. Risks

- **`.sdpkg` merge corrupting hand-authored packages.** Highest-impact risk. Mitigate with the
  fixture-based round-trip tests in Phase 3, and by making the editor conservative: if the file
  cannot be parsed with confidence, warn and skip rather than rewrite.
- **Format drift.** `SerializedVersion` values (`{Stride: 2.0.0.0}` for sounds, `{Assets: 3.1.0.0}`
  for packages) are hardcoded. They have been stable for years, and Stride has asset upgraders for
  version bumps, but pin them in one constants file so a future bump is a one-line change.
- **Windows-only path assumptions.** Use `/` in emitted YAML paths regardless of host OS — the
  reference files use forward slashes.

---

## 7. Engine API asks (separate track, do not block this work)

Worth raising as issues/PRs against `stride3d/stride` once the toolkit implementation proves the
shape. Until then the toolkit duplicates a small amount of knowledge; note that in code comments
pointing back at the engine source of truth.

1. **A lightweight YAML/asset-serialization package.** Today `AssetFileSerializer` is only
   reachable by taking `Stride.Core.Assets` with its `Microsoft.Build` + Roslyn dependency set.
   Splitting `Stride.Core.Assets.Yaml` (currently a shared `.projitems` compiled into
   `Stride.Core.Assets`) into its own package would let external tooling serialize assets properly
   instead of templating them.
2. **Deterministic IDs in `RawAssetImporterBase.Import`.** It currently does `new TAsset()`
   (`sources/assets/Stride.Core.Assets/RawAssetImporterBase.cs:20`), so the `Id` is random per
   call. An overload accepting an `AssetId` would let any tool produce reproducible output.
3. **A public "create asset file for this source file" helper**, wrapping importer lookup +
   ID assignment + `RootAssets` registration — the operation Game Studio performs on drag-and-drop.
4. **Longer term: engine-side auto-import.** A flag (e.g. `StrideAutoImportAssets`) that makes the
   compiler import raw files from `Resources/` in-memory via the existing
   `AssetRegistry.FindImporterForFile` machinery (`sources/assets/Stride.Core.Assets/AssetRegistry.cs:371`).
   That would make this toolkit feature redundant for users who don't need Game Studio round-trip —
   but the file-generating approach stays valuable precisely because it *does* round-trip.

---

## 8. Open questions for the maintainer

- Opt-in vs opt-out default for `StrideToolkitGenerateAssets` (§5 Phase 2 argues opt-out; easy to flip).
- Ship the generator in the main `Stride.CommunityToolkit` package (zero-config, chosen here) or
  a separate `Stride.CommunityToolkit.AssetGenerator` package (smaller main package, one more step
  for beginners)?
