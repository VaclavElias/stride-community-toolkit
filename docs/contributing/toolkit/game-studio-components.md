# Making components work in Game Studio

A toolkit library that ships `EntityComponent` types has to satisfy three separate requirements
before those components are usable from Game Studio. Each one is easy to miss, none of them produces
an error naming the cause, and they fail at three different moments - so it is worth knowing all
three before you start rather than discovering them one at a time.

| Missing | What the user sees |
|---|---|
| `Module.cs` | The component never appears in the Add-component list at all |
| `[DataContract]` | It appears, then throws `No serializer available for type ...` when added |
| A renderer registration call | It adds and configures cleanly, and draws nothing |

Everything below works in code-only projects without any of it, which is exactly why these gaps
survive: the library looks finished, its examples run, and the editor path is never exercised.

## 1. Register the assembly for scanning

Game Studio only inspects assemblies that have registered themselves. Without this file the
component types are invisible to the editor - not greyed out, not erroring, simply absent.

Add a `Module.cs` at the root of the library:

```csharp
using Stride.Core;
using Stride.Core.Reflection;
using System.Reflection;

namespace Stride.CommunityToolkit.YourLibrary;

internal static class Module
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyRegistry.Register(typeof(Module).GetTypeInfo().Assembly, AssemblyCommonCategories.Assets);
    }
}
```

> [!NOTE]
> `ModuleInitializer` here is **`Stride.Core.ModuleInitializerAttribute`**, not the C#
> `System.Runtime.CompilerServices` attribute of the same name. The `using Stride.Core;` matters.

`Stride.CommunityToolkit`, `Stride.CommunityToolkit.DebugShapes` and `Stride.CommunityToolkit.ImGui`
all carry an identical copy of this file.

## 2. Make the component serializable

The editor clones a component from the asset side to the game side through a generated
`DataSerializer`, and Stride's assembly processor only generates one for types marked
`[DataContract]`. Without it, adding the component throws immediately.

Follow the shape the engine's own components use:

```csharp
[DefaultEntityComponentProcessor(typeof(ShapeProcessor), ExecutionMode = ExecutionMode.Runtime)]
[DataContract("ShapeComponent")]
[Display("Shape", Expand = ExpandRule.Once)]
[ComponentCategory("Rendering")]
public sealed class ShapeComponent : ActivableEntityComponent
```

`DataContract`, `Display`, `ExpandRule` and `DataMemberIgnore` live in `Stride.Core`;
`ComponentCategory` lives in `Stride.Engine`. Category names are free-form strings - the engine uses
*Lights*, *Model*, *Physics*, *Scripts*, *Sprites*, *UI* and friends, and anything else you write
becomes a new group in the list.

> [!IMPORTANT]
> `DefaultEntityComponentProcessorAttribute.ExecutionMode` defaults to **`All`**, meaning the
> processor also runs inside the editor. If it can only work in a running game - because it draws
> through something the game registers - say `ExecutionMode.Runtime` explicitly, or it burns editor
> frames doing nothing.

### Audit every public property before you add it

`[DataContract]` turns the whole public surface into editor-authorable state. Anything that is a live
runtime handle has to be excluded, or the editor will try to serialize something meaningless:

```csharp
/// <summary>The batch this shape draws through, or null for the game's default.</summary>
[DataMemberIgnore]
public ShapeBatch? Batch { get; set; }
```

### What you cannot do: a read-only hint property

It is tempting to surface setup instructions as a read-only label:

```csharp
// Does not work - never reaches the property grid
public string Setup => "Call game.AddShapeBatch()";
```

`ObjectDescriptor` forces `DataMemberMode.Never` for a getter-only `string` or value type, **even
with an explicit `[DataMember]`**. Stride has no description or tooltip attribute either -
`DisplayAttribute` carries only `Name`, `Category`, `Order`, `Expand` and `Browsable`. The only
guaranteed-visible surface is the display name itself, which is why the components that need a setup
call currently spell it out there: `"Shape (call AddShapeBatch)"`. Treat that as a stopgap.

## 3. Remember the renderer is still the user's job

Several toolkit components are data only, with a renderer the running game registers -
`AddEntityTextRenderer()`, `AddWorldTextRenderer()`, `AddShapeBatch()`. A code-only user writes that
call while setting the game up. A Game Studio user has no equivalent moment and no hint that one is
needed, so the component adds cleanly and silently does nothing.

Until processors register their own renderers, a library in this position should:

- name the required call in the component's `[Display]` name, and
- document it for users on a manual page - see
  [Using toolkit components in Game Studio](../../manual/game-studio.md).

> [!TIP]
> The tidy fix is for the processor to register its renderer in `OnSystemAdd()`. `EnsureSceneRenderer`
> is already idempotent, so it is safe to call repeatedly. It changes behaviour for existing
> code-only users too - benignly, the manual call becomes redundant - so raise it with the
> maintainers rather than doing it quietly.

## Checklist

- [ ] `Module.cs` registers the assembly with `AssemblyRegistry`
- [ ] Every `EntityComponent` has `[DataContract]`, `[Display]` and `[ComponentCategory]`
- [ ] Runtime-only handles on those components are `[DataMemberIgnore]`
- [ ] Processors that cannot work in the editor declare `ExecutionMode.Runtime`
- [ ] Any required registration call is named in the display name and documented for users
- [ ] Verified by adding the component in Game Studio and pressing Play, against a package built with
      [`build/pack-local.cs`](building.md#building-local-nuget-packages) - a `ProjectReference` does not
      exercise any of this