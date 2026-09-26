using Stride.Core;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;

namespace E02_3D_Material_Gallery;

/// <summary>
/// A material feature of the gallery's own, in the displacement slot: the one slot a material has
/// that runs in the vertex stage. It installs <c>Effects/HairSway.sdsl</c> as the stage's final
/// modifier, the same hook the engine's displacement feature uses to move vertices along their
/// normals, and the shader moves them sideways instead, by a wind set every frame through the
/// <c>HairSwayKeys</c> the shader source generator makes from the class's constants. That is the whole of it: a feature is a class that adds shader sources to a
/// stage, and the material generator composes what it adds with everything else.
/// </summary>
[DataContract]
public class HairSwayFeature : MaterialFeature, IMaterialDisplacementFeature
{
    public override void GenerateShader(MaterialGeneratorContext context)
    {
        context.SetStreamFinalModifier<HairSwayFeature>(MaterialShaderStage.Vertex, new ShaderClassSource("HairSway"));
    }
}