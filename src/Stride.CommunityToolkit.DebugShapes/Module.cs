using Stride.Core;
using Stride.Core.Reflection;
using System.Reflection;

namespace Stride.CommunityToolkit.DebugShapes;

internal static class Module
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyRegistry.Register(typeof(Module).GetTypeInfo().Assembly, AssemblyCommonCategories.Assets);
    }
}