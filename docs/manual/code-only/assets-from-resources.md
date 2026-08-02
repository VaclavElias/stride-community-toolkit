# Assets from Resources

A code-only project can use any Stride asset type — the asset compiler runs on build and handles the
whole pipeline. The only thing missing is the small YAML metadata file that Game Studio would normally
author for you.

The toolkit ships a build step that writes those files. Drop `wood-tap-5.mp3` into `Resources/`, build,
and load it:

```csharp
var sound = game.Content.Load<Sound>("wood-tap-5");
```

> [!NOTE]
> Audio is supported today (`.wav`, `.mp3`, `.ogg`, `.aac`, `.aiff`, `.flac`, `.m4a`, `.wma`, `.mpc`).
> Other asset types, such as textures, are planned.

## Enabling it

The build step is **opt-in**. Add this to your `.csproj`:

```xml
<PropertyGroup>
    <StrideToolkitGenerateAssets>true</StrideToolkitGenerateAssets>
</PropertyGroup>
```

It requires the `Stride.CommunityToolkit` package (which `Stride.CommunityToolkit.Windows` and
`Stride.CommunityToolkit.Linux` both bring in) plus `Stride.AssetCompiler`, which those platform
packages already reference.

It is off by default because the same package is used by regular Game Studio projects, where the
`Resources/` folder is managed by the editor. Nothing should edit a Game Studio project's `.sdpkg`
behind the user's back.

## What it does

Given this project:

```
MyGame/
├── MyGame.csproj
├── Program.cs
└── Resources/
    ├── wood-tap-5.mp3
    └── sfx/
        └── boom.mp3
```

building produces:

```
MyGame/
├── MyGame.sdpkg          <- created or updated
├── Assets/
│   ├── wood-tap-5.sdsnd  <- created
│   └── sfx/
│       └── boom.sdsnd    <- created
└── Resources/
    └── ...
```

`Assets/wood-tap-5.sdsnd` is exactly what Game Studio would have written:

```yaml
!Sound
Id: 695620a7-fe9e-d4b8-a920-4ceed9e6be4f
SerializedVersion: {Stride: 2.0.0.0}
Source: !file ../Resources/wood-tap-5.mp3
SampleRate: 44100
CompressionRatio: 10
StreamFromDisk: false
Spatialized: false
```

and the asset is registered in `MyGame.sdpkg`:

```yaml
RootAssets:
    -   695620a7-fe9e-d4b8-a920-4ceed9e6be4f:wood-tap-5
```

That `RootAssets` entry is not optional. The asset compiler only builds assets listed there, assets
reachable from them, and a handful of always-root types that sounds are not. Without the entry the
asset is silently dropped from the build.

Subfolders are mirrored, so `Resources/sfx/boom.mp3` becomes `Assets/sfx/boom.sdsnd` and loads as
`sfx/boom`.

## Commit the generated files

`Assets/*.sdsnd` and the `.sdpkg` are normal project files — commit them. Asset ids are derived from
the resource path, so they are identical on every machine and every build; the files do not churn.
Game Studio does not run the MSBuild step, so it needs them present to open the project.

## Editing what was generated

The generator only ever **adds**. It never overwrites an existing asset file and never deletes one.

So to change how a sound is compiled, just edit its `.sdsnd` — by hand, or by opening the project in
Game Studio — and the build will leave your version alone from then on. File existence is the whole
ownership test; there are no marker comments to preserve.

For a project-wide default, set the options on the build step instead. They apply to newly created
files only:

```xml
<PropertyGroup>
    <StrideToolkitGenerateAssets>true</StrideToolkitGenerateAssets>
    <StrideToolkitSoundSampleRate>24000</StrideToolkitSoundSampleRate>
    <StrideToolkitSoundCompressionRatio>15</StrideToolkitSoundCompressionRatio>
    <StrideToolkitSoundSpatialized>false</StrideToolkitSoundSpatialized>
    <StrideToolkitSoundStreamFromDisk>false</StrideToolkitSoundStreamFromDisk>
</PropertyGroup>
```

Folder names can be changed too:

```xml
<StrideToolkitAssetsFolder>Assets</StrideToolkitAssetsFolder>
<StrideToolkitResourcesFolder>Resources</StrideToolkitResourcesFolder>
```

## Running it manually

The generator is a small console tool shipped inside the `Stride.CommunityToolkit` package at
`tools/net10.0/Stride.CommunityToolkit.AssetGenerator.dll`. You can run it yourself instead of
enabling the build step:

```
dotnet "%USERPROFILE%\.nuget\packages\stride.communitytoolkit\<version>\tools\net10.0\Stride.CommunityToolkit.AssetGenerator.dll" ^
    --project-dir . --project-name MyGame --dry-run --verbose
```

Drop `--dry-run` to write the files. `--help` lists every option.

## Warnings

| Code | Meaning |
|---|---|
| `STCT0001` | An asset points at a source file that no longer exists. Restore the file or delete the asset — the generator never deletes. |
| `STCT0002` | Two resources map to the same asset name (`boom.mp3` and `boom.wav`). Rename one. |
| `STCT0003` | The `.sdpkg` could not be parsed with confidence, so it was left untouched. Generated assets will not compile until they are listed under `RootAssets`. |
| `STCT0005` | The build step is enabled but the tool was not found. Set `StrideToolkitAssetGeneratorPath`. |

A resource that some other asset already points at is left alone entirely — that is how a project
that has been through Game Studio avoids getting duplicate assets.
