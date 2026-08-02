# Plan: Build-time asset YAML generator for code-only projects

**Repo:** `stride-community-toolkit`
**Status:** **Audio shipped and verified.** See §5 for what exists, §9 for the roadmap.
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

Verified by reading the Stride source (`D:\Projects\GitHub\stride`, branch `master`, 4.4.0-dev)
**and confirmed by an end-to-end build** (§6). **Do not re-derive.**

### 2.1 `RootAssets` is mandatory — this is the non-obvious one

`RootPackageAssetEnumerator` (`sources/assets/Stride.Core.Assets/Compiler/RootPackageAssetEnumerator.cs`)
compiles only:
1. assets listed in `Package.RootAssets`,
2. assets reachable as dependencies of those,
3. asset types marked `AlwaysMarkAsRoot`.

Exactly five types carry `AlwaysMarkAsRoot` (grep of the whole engine):
`EffectCompositorAsset`, `EffectLogAsset`, `EffectShaderAsset`, `GameSettingsAsset`,
`ScriptSourceFileAsset`. **No source-file-backed asset type is on that list** — not sound, not
texture, not video, not font. So every asset this generator will ever produce needs a `RootAssets`
entry, and the generator always has two outputs: the asset file *and* an updated `.sdpkg`.

This also means the asset `Id` appears in two files and must match.

### 2.2 Package file location and implicit defaults

- The package for a project is `$(StrideCurrentPackagePath)` if set and existing, otherwise
  `$(MSBuildProjectDirectory)\$(MSBuildProjectName).sdpkg`
  (`sources/core/Stride.Core/build/Stride.AssetBuildManifest.targets`).
  **The generated `.sdpkg` must be named after the project.**
- If no `.sdpkg` exists, `Package.LoadProject` creates an implicit package with
  `AssetFolders = { "Assets" }` and `ResourceFolders = { "Resources" }`
  (`sources/assets/Stride.Core.Assets/Package.cs`) — but an implicit package has empty
  `RootAssets`, so per 2.1 the asset still won't compile. A real `.sdpkg` is required.
- Asset files are discovered by recursively scanning each `AssetFolders` entry, so subfolders
  under `Assets/` work.

### 2.3 Build ordering

- `StrideWriteAssetBuildManifest` runs `AfterTargets="ResolveProjectReferences"`.
- `StrideCompileAsset` runs `AfterTargets="CopyFilesToOutputDirectory"`.

The generator runs `BeforeTargets="StrideWriteAssetBuildManifest;PrepareForBuild"`.
`PrepareForBuild` is listed second so the target still fires in projects that do not import the
Stride asset targets, and MSBuild runs a target only once regardless of how many hooks fire.

**Confirmed:** generating the `.sdpkg` during the same build that consumes it works. A clean
build of a project with no `.sdpkg` at all produces the package, the assets, and a fully compiled
bundle in one pass — no second build needed.

### 2.4 `SoundAsset` schema

From `sources/engine/Stride.Assets/Media/SoundAsset.cs`:

- `[DataContract("Sound")]` → YAML tag is `!Sound`
- `AssetFormatVersion(..., CurrentVersion = "2.0.0.0")` → `SerializedVersion: {Stride: 2.0.0.0}`
- extension `.sdsnd`
- Defaults: `Index = 0`, `SampleRate = 44100`, `CompressionRatio = 10`,
  `StreamFromDisk = false`, `Spatialized = false`
- `Source` comes from `AssetWithSource` and serializes as `Source: !file <path relative to the asset file>`

### 2.5 Recognised audio extensions

`RawSoundAssetImporter.FileExtensions` (`sources/engine/Stride.Assets/Media/RawSoundAssetImporter.cs`)
is `.wav,.mp3,.ogg,.aac,.aiff,.flac,.m4a,.wma,.mpc` **plus the video extensions** (a video file can
carry an audio track).

v1 uses only the unambiguous audio list — `.wav .mp3 .ogg .aac .aiff .flac .m4a .wma .mpc` —
so `.mp4` isn't silently turned into a sound asset. Adding video (§9.2) is what resolves that
overlap properly.

### 2.6 Can we reuse Stride's own serializer? — re-examined, answer still no

`AssetFileSerializer.Save(filePath, asset, yamlMetadata: null)` is public and would produce exactly
correct YAML. Using it requires referencing `Stride.Core.Assets` (serializer) *and* `Stride.Assets`
(`SoundAsset`). Measured cost of `Stride.Assets.csproj`:

- packages: `Microsoft.Build` + `.Framework` + `.Utilities.Core` + `.Tasks.Core`,
  `Microsoft.CodeAnalysis.CSharp.Workspaces`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`,
  `SixLabors.ImageSharp`, `SSH.NET`, `FFmpeg.AutoGen`, three `Microsoft.TemplateEngine.*`;
- project references: Stride.Engine, Physics, BepuPhysics, UI, Video, Navigation, TextureConverter,
  Core.Assets.Quantum;
- plus bootstrapping `AssetRegistry` through `AssemblyRegistry` before serializer lookup works.

To replace ~20 lines of string building. Loading MSBuild-dependent assemblies inside an MSBuild
build is also a well-known source of version conflicts.

**Decision confirmed: write YAML from templates.** The writer sits behind `IAssetYamlWriter` so an
`AssetFileSerializer`-backed implementation can be swapped in if the engine ever ships a
lightweight serializer package (§8).

Three related dead ends, checked so nobody re-checks them:

- **`Stride.Audio.Sound` cannot supply the options.** It is the runtime content type. `SampleRate`,
  `Spatialized`, `Channels`, `NumberOfPackets` (`SoundBase.cs`), `StreamFromDisk`,
  `CompressedDataUrl`, `Samples` (`Sound.cs`) are all `internal`, and **`CompressionRatio` does not
  exist on it at all** — it is authoring-only, consumed by the compiler to pick the Celt bitrate.
  Referencing `Stride.Audio` would also drag the native audio layer into a build-time tool.
  The type that matches is `SoundAsset`, in the wrong package.
- **`Stride.Core.Serialization` has nothing to offer.** Its `Assets/` folder contains exactly one
  file, `AssetId.cs`. Everything else is runtime *binary* serialization. No YAML.
- **`Stride.Core.Yaml` does ship as a standalone package** and is light (its only project reference
  is `Stride.Core.Reflection`) — this corrects an assumption in the original plan. But it is the raw
  layer: Scanner, Parser, Emitter, Events, Tokens. The *asset* conventions (`!Sound` tag mapping,
  `!file` for `UFile`, `SerializedVersion`, Game Studio's exact indentation) live in
  `Stride.Core.Assets.Yaml` (a `.projitems` compiled into `Stride.Core.Assets`) and in
  `AssetFileSerializer` itself. Going through `Stride.Core.Yaml` means re-declaring the asset types
  and re-registering tag mappings — more code and more drift surface than the template.

There is precedent in Stride itself for line-based handling of these files:
`sources/tools/Stride.TemplateGenerator/TemplatePreprocessor.cs` (`CollectRootAssets`) parses the
`RootAssets:` section of a `.sdpkg` line by line rather than with a YAML parser.

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
`ResourceFolders` uses a plain `- !dir`. Both files are CRLF; the `.sdsnd` ends with a trailing
newline, this hand-written `.sdpkg` does not.

Both are pinned as string constants in `tests/.../AssetGenerator/Fixtures.cs`. The corpus for
future asset types is listed per type in §9.

---

## 4. Design rules

1. **Deterministic IDs.** The asset `Id` GUID is MD5 of `"<kind>:<project-relative resource path>"`,
   the 16 bytes used directly as a GUID, with the path lowercased and forward-slashed first so the
   result is identical on Windows and Linux. Same input → identical bytes forever. Non-negotiable:
   a churning `Id` changes both the asset file and the `.sdpkg` on every build, producing noisy
   diffs and defeating the asset compiler's incremental cache.
2. **Never overwrite an existing asset file.** File existence is the whole ownership test; no marker
   comments, no tracking manifest. This gives the escalation path for free: defaults for beginners,
   full control for anyone who edits the file. An existing file is still *registered* in
   `RootAssets` (using the id read out of it) so a hand-written asset compiles.
3. **Never touch a resource another asset already claims.** If any asset anywhere in the asset
   folder references the resource as a `!file` source, the resource is skipped entirely — no new
   asset, no root entry. This is what makes the generator harmless in a project that has been
   through Game Studio, where `Resources/` is full of files already imported under other names.
   *(Added during implementation; rule 2 alone only catches same-name collisions.)*
4. **Write only when content differs.** For both the asset file and the `.sdpkg`, compare before
   writing. Touching timestamps on every build defeats `StrideCompileAsset`'s up-to-date check.
5. **Merge the `.sdpkg`, never regenerate it.** Add only missing `AssetFolders` /
   `ResourceFolders` / `RootAssets` entries; leave everything else, including key order and `Meta`,
   byte-identical, and preserve the file's line endings and trailing-newline style. If the file
   cannot be parsed with confidence, warn and skip rather than rewrite. If no `.sdpkg` exists,
   create a minimal one with a `Meta` block.
6. **Orphans produce a warning, never a deletion.** The generator does not track what it created,
   so it must never delete.
7. **Mirror subfolders.** `Resources/sfx/boom.mp3` → `Assets/sfx/boom.sdsnd`, asset location
   `sfx/boom`, `Source: !file ../../Resources/sfx/boom.mp3`. This avoids the name collisions that
   a flat naming scheme would produce.
8. **Generated files are committed.** They are deterministic, and Game Studio needs them present
   when opening the project (it does not run the MSBuild target).

---

## 5. What was built

### 5.1 Core library + tool — `src/Stride.CommunityToolkit.AssetGenerator/`

A dependency-free net10.0 console tool (~55 KB, no package references).

| File | Role |
|---|---|
| `Core/ResourceScanner.cs` | enumerates the resource folder, filters by extension, returns project-relative paths + asset locations |
| `Core/DeterministicId.cs` | path → GUID, OS- and case-invariant |
| `Core/IAssetYamlWriter.cs` | seam for a future `AssetFileSerializer`-backed writer |
| `Core/SoundAssetTemplate.cs` | emits `.sdsnd`; `SoundAssetOptions` mirrors `SoundAsset`'s defaults |
| `Core/PackageFileEditor.cs` | line-based `.sdpkg` read/merge/write, pure string in/out |
| `Core/AssetFileIndex.cs` | reads `Id:` and every `!file` reference out of existing assets |
| `Core/AssetGenerator.cs` | orchestrator; returns a result object, logs nothing |
| `Core/AssetFormats.cs` | the hardcoded format constants, one place to change on a version bump |
| `Program.cs` | argument parsing, MSBuild-canonical diagnostic output |

Per-file option overrides (`SampleRate`, `CompressionRatio`, `StreamFromDisk`, `Spatialized`) are
plumbed through and only affect newly created files.

### 5.2 MSBuild integration

`src/Stride.CommunityToolkit/build/Stride.CommunityToolkit.targets`, packed to
`buildTransitive/net10.0/`, invokes the tool via `Exec`. The tool is packed to `tools/net10.0/`
(dll + runtimeconfig.json + deps.json) through `TargetsForTfmSpecificContentInPackage` in
`Stride.CommunityToolkit.csproj`, which builds the generator project on demand — there is no
`ProjectReference`, so a normal build of the library does not depend on it.

Properties: `StrideToolkitGenerateAssets`, `StrideToolkitAssetsFolder`,
`StrideToolkitResourcesFolder`, `StrideToolkitAssetGeneratorPath`, `StrideToolkitSoundSampleRate`,
`StrideToolkitSoundCompressionRatio`, `StrideToolkitSoundSpatialized`,
`StrideToolkitSoundStreamFromDisk`.

Diagnostics: `STCT0001` orphan asset, `STCT0002` asset path taken, `STCT0003` package not
understood, `STCT0004` resource already imported (info), `STCT0005` tool not found.

### 5.3 Tests — `tests/Stride.CommunityToolkit.Tests/AssetGenerator/`

34 xunit tests: pinned deterministic ids, byte-match against the committed fixture, never-overwrite
(including when contents differ), `.sdpkg` merge preserving `Effects` / `Meta` / multi-line entries,
`RootAssets: []` conversion, idempotency, line-ending and trailing-newline preservation, subfolder
mapping, orphan warnings, Game Studio guard, refusing to touch an unparseable package.

### 5.4 Docs

`docs/manual/code-only/assets-from-resources.md`, linked from `docs/manual/toc.yml`.

---

## 6. Verification performed

- **Packaged end to end.** `dotnet pack` → local feed → fresh consumer project referencing only
  `Stride.CommunityToolkit` + `Stride.AssetCompiler`, with `Resources/wood-tap-5.mp3` and
  `Resources/sfx/boom.mp3`. Clean build from zero (no `Assets/`, no `.sdpkg`) produced both assets
  and the package, the asset compiler ran, and `bin/Debug/net10.0/data/db/aliases` contained
  `wood-tap-5|/ConsumerGame/wood-tap-5` and `sfx/boom|/ConsumerGame/sfx/boom`. Rebuild is a no-op.
  Stride's own source generator also picked the assets up into `StrideAssetConstants`.
- **Against the real example.** Deleted the committed `.sdsnd` from `Example_CubicleCalamity`,
  emptied `RootAssets`, regenerated, built — the sound compiled into the bundle. Restored afterwards.
- 39 tests pass (34 new + 5 pre-existing).

Two bugs the verification caught, both now covered by tests: the folder key extractor was applied to
the raw value instead of the entry payload (so `AssetFolders: Assets` was reported missing when it
existed), and the asset index was a pre-run snapshot, so two same-named resources both wrote to one
path.

---

## 7. Decisions on the original open questions

- **Opt-in, not opt-out.** `StrideToolkitGenerateAssets` defaults to `false`. The plan argued
  opt-out, and that argument holds for code-only projects — but the same package is referenced by
  regular Game Studio projects, where `Resources/` can hold files that were never imported. Those
  would get silently imported and the user's `.sdpkg` edited by a build step nobody asked for.
  Flipping the default is a one-word change once there is real-world validation.
- **Shipped in the main `Stride.CommunityToolkit` package**, not a separate one. The tool is ~55 KB
  and inert unless enabled, so the smaller-package argument does not pay for the extra install step.

---

## 8. Engine API asks (separate track)

Worth raising as issues/PRs against `stride3d/stride`. Until then the toolkit duplicates a small
amount of knowledge; code comments point back at the engine source of truth.

1. **A lightweight asset-serialization package.** Sharper than the original ask now that
   `Stride.Core.Yaml` is known to ship standalone: the missing piece is not the YAML layer but
   `AssetFileSerializer` + the asset tag registry + the `UFile`/`!file` serializers, which are
   welded to `Stride.Core.Assets` and therefore to `Microsoft.Build` and Roslyn. A package with the
   asset conventions and no build-system dependency would let external tooling serialize assets
   properly instead of templating them.
2. **Deterministic IDs in `RawAssetImporterBase.Import`.** It currently does `new TAsset()`
   (`sources/assets/Stride.Core.Assets/RawAssetImporterBase.cs`), so the `Id` is random per call.
   An overload accepting an `AssetId` would let any tool produce reproducible output.
3. **A public "create asset file for this source file" helper**, wrapping importer lookup +
   ID assignment + `RootAssets` registration — the operation Game Studio performs on drag-and-drop.
4. **Longer term: engine-side auto-import.** A flag (e.g. `StrideAutoImportAssets`) that makes the
   compiler import raw files from `Resources/` in-memory via the existing
   `AssetRegistry.FindImporterForFile` machinery. That would make this toolkit feature redundant for
   users who don't need Game Studio round-trip — but the file-generating approach stays valuable
   precisely because it *does* round-trip.

---

## 9. Roadmap: which asset types come next

Ordered by implementation convenience. Each entry lists what was verified in the engine source, so
the next implementer starts where this one finished.

The generator is structured for this: add an `IAssetYamlWriter`, add an extension list to
`ResourceScanner`, and the id derivation, ownership rules, package merge, orphan detection and
MSBuild plumbing are all reused unchanged. **Only the writer and the extension list are new work per
type** — except where noted below.

### 9.1 Texture (`.sdtex`) — do this first

*Effort: small. Value: high — textures are the most-requested asset in code-only projects.*

- `sources/engine/Stride.Assets/Textures/TextureAsset.cs`: `[DataContract("Texture")]`,
  `CurrentVersion = "2.0.0.0"`, extension `.sdtex`, derives from **`AssetWithSource`** — the same
  base as `SoundAsset`, so `Source: !file …` works identically.
- Importer extensions (`Textures/TextureImporter.cs`):
  `.dds .jpg .jpeg .png .gif .bmp .tga .psd .tif .tiff`. No overlap with any other importer.
- Flat defaults that can be omitted: `Width = 100`, `Height = 100`, `IsSizeInPercentage = true`,
  `IsCompressed = true`, `GenerateMipmaps = true`, `IsStreamable = true`.
- **The one new thing:** `Type` is a polymorphic `ITextureType` and Game Studio always emits it.
  Three implementations, all in `sources/engine/Stride.Assets/Textures/`:
  `!ColorTextureType` (default; `UseSRgbSampling = true`, `ColorKeyEnabled = false`,
  `ColorKeyColor = {R: 255, G: 0, B: 255, A: 255}`, `Alpha = Auto`, `PremultiplyAlpha = true`),
  `!NormalMapTextureType`, `!GrayscaleTextureType`. So the template gains a nested block —
  `SoundAssetTemplate` has no equivalent, but it is still just indented string building.
- Golden reference (modern, Game Studio-authored):
  `D:\Projects\GitHub\stride-example-projects\MyGame01\MyGame01.Game\Assets\Skybox texture.sdtex`:

  ```yaml
  !Texture
  Id: 30AFB54E-D2C5-404A-A693-6667855FD5B1
  SerializedVersion: {Stride: 2.0.0.0}
  Tags: []
  Source: !file ../Resources/skybox_texture_hdr.dds
  Type: !ColorTextureType
      UseSRgbSampling: false
      ColorKeyColor: {R: 255, G: 0, B: 255, A: 255}
  ```

  Do **not** use `sources/engine/Stride.Assets.Tests*/**/*.sdtex` as fixtures — those are
  pre-`SerializedVersion` format and would encode a stale shape.
- Open design question: how a user picks a non-Color type. Suggested: default everything to
  `!ColorTextureType`, and let the escalation path handle the rest (edit the generated `.sdtex`,
  the generator will not clobber it). A filename-suffix heuristic (`_nm`, `_normal`) is tempting
  but silently misclassifies; if it is added, make it opt-in.

### 9.2 Video (`.sdvid`)

*Effort: small. Value: moderate — and it closes the `.mp4` hole left by §2.5.*

- `sources/engine/Stride.Assets/Media/VideoAsset.cs`: `[DataContract("Video")]`,
  `CurrentVersion = "2.1.0.0"` (note: **not** 2.0.0.0 like sound and texture), extension `.sdvid`.
- Importer extensions (`Media/RawVideoAssetImporter.cs`): `.avi .mkv .mov .mp4`.
- Derives from `Asset` and implements `IAssetWithSource` with its own `Source` property rather than
  inheriting `AssetWithSource`. Serialized shape is the same `Source: !file …`, but confirm against
  a real file rather than assuming.
- Flat defaults: `Width = 100`, `Height = 100`, `IsSizeInPercentage = true`,
  `IsAudioChannelMono = false`.
- **Wrinkle:** `VideoDuration` is a public *field* of struct type `VideoAssetDuration` whose
  `EndTime` is set to `TimeSpan.MaxValue` in the constructor. Whether and how Game Studio emits it
  needs checking against a real authored `.sdvid` before writing the template — there is no such
  file in either reference repo, so one must be produced in Game Studio first.
- Ships with a behaviour change worth calling out in the docs: once video is supported, `.mp4` and
  friends stop being ignored and become video assets, not sound assets.

### 9.3 Effects folder registration (`.sdsl`) — cheapest possible win

*Effort: tiny. Value: small but real. Can ship alongside any other item.*

Not a new asset type at all. `EffectShaderAsset` **is** `AlwaysMarkAsRoot` (§2.1), so a `.sdsl`
needs no `RootAssets` entry — but the folder containing it still has to appear under `AssetFolders`,
which is why `Example_CubicleCalamity.sdpkg` lists `Effects` by hand. A rule of "any folder under
the project that contains `.sdsl` files gets an `AssetFolders` entry" reuses `PackageFileEditor`
exactly as it stands and writes no new files.

### 9.4 SpriteFont (`.sdfnt`)

*Effort: medium. Value: high — custom text rendering is awkward in code-only projects today.*

- `sources/engine/Stride.Assets/SpriteFont/SpriteFontAsset.cs`: `[DataContract("SpriteFont")]`,
  `CurrentVersion = "2.1.0.0"`, extension `.sdfnt`.
- **There is no font importer in the engine** — the grep for `FileExtensions` finds only sound,
  video, texture and 3D. Game Studio creates a font asset and lets the user point at a file. So the
  toolkit defines its own extension list (`.ttf`, `.otf`) rather than mirroring an importer.
- Derives from `Asset`, **not** `AssetWithSource`. The source path is nested one level down:
  `FontSource: !FileFontProvider` with a `Source: !file …` inside it (`FileFontProvider.cs`), versus
  the default `!SystemFontProvider`. This is the first type where `AssetFileIndex`'s `!file`
  scanning matters for a non-top-level key — it already handles that, since it scans every line.
- `FontType` is polymorphic and the choice is consequential:
  `!OfflineRasterizedSpriteFontType` (fixed size, precompiled, `Size = 20`, has `CharacterRegions`),
  `!RuntimeRasterizedSpriteFontType` (scalable at runtime, `Size = 20`),
  `!SignedDistanceFieldSpriteFontType`. Suggested default: `!RuntimeRasterizedSpriteFontType`,
  which needs no character-region configuration and is the friendliest for a beginner. Expose the
  size as an MSBuild property.
- Golden reference: `sources/engine/Stride.Engine/AssetPackage/Assets/Shared/StrideDefaultFont.sdfnt`
  (current format, shows both nested blocks). `sources/editor/Stride.GameStudio.Tests/Assets/*.sdfnt`
  are the old flat format — do not copy those.

### 9.5 GameSettings (`.sdgamesettings`) — adjacent, not source-driven

*Effort: medium. Value: moderate. Different shape of feature.*

Every code-only build currently logs
`[AssetCompiler] Could not find game settings asset at location [GameSettings]. Use a Default One`.
`GameSettingsAsset` is `AlwaysMarkAsRoot`, so it needs no `RootAssets` entry — just the file. It has
no source file either, so this is a "scaffold once if absent" feature rather than a
resource-to-asset mapping, and it does not fit `IAssetYamlWriter` cleanly. It would silence the
warning and give code-only users a real place to set default scene, graphics profile and physics
settings. Worth doing, but as its own small feature, not as part of the resource pipeline.

### 9.6 Models (`.sdm3d` + `.sdmat` + `.sdskel`) — last, and probably not template-able

*Effort: large. Value: high, but the approach does not extend here.*

`ThreeDAssetImporter.FileExtensions` is
`.dae .3ds .gltf .glb .obj .blend .x .md2 .md3 .dxf .ply .stl .stp .fbx`, and importing one file
produces *several* assets: the model, one `.sdmat` per material, a `.sdskel`, and animation assets —
all cross-referencing each other by `AssetId`. `ImportThreeDCommand` has to actually parse the mesh
(via Assimp) to enumerate materials and bones. That is not something a template can do; it needs the
engine's importer, which is exactly what engine ask §8.3 is about.

Two possible fallbacks if this is attempted: emit a model asset with an empty `Materials` list and
accept the default material at runtime (needs verification that the compiler tolerates it), or
shell out to the asset compiler's import path. Do not start until §9.1–9.4 are shipped and the
demand is proven.

### 9.7 Explicitly not targets

- **`.sdmat` materials, `.sdsky` skyboxes, `.sdsheet` sprite sheets, `.sdscene` scenes** — these are
  authored, not derived from a raw file. A skybox references a *texture asset*, a sprite sheet needs
  region definitions. Generating them would mean inventing content, not transcribing metadata, and
  the toolkit already exposes code APIs for all of these.

### 9.8 Cross-cutting work that unlocks the whole roadmap

Two things are worth doing *before* the second asset type, while there is still only one:

1. **Extract the per-type knowledge into a table** — extension list, kind string, asset extension,
   tag, serialized version, writer — so `AssetGenerator` loops over registered types instead of
   hardcoding `SoundAssetTemplate`. Cheap now, tedious after three types have grown their own paths.
2. **Decide how per-type options reach the tool.** Today they are individual MSBuild properties
   (`StrideToolkitSoundSampleRate`, …). Four types × five options is not tenable; a small
   `.json`/`.props` options file next to the project, or an MSBuild item group with metadata, scales
   better. Worth settling before texture adds its `Type` selection and font adds its `FontType`.
