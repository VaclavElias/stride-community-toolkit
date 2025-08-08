# GitHub Copilot Instructions for Stride Community Toolkit

## Project Overview

The **Stride Community Toolkit** is a comprehensive collection of C# helpers and extensions designed to enhance development with the [Stride Game Engine](https://www.stride3d.net/). This toolkit serves as a foundation for rapid prototyping and accelerated game development, providing convenience wrappers and utilities that simplify common game development tasks.

### Key Characteristics
- **Primary Language**: C# with .NET 8.0
- **Target Framework**: net8.0 with nullable reference types enabled
- **Game Engine**: Stride Game Engine version 4.2.0.2381
- **Multi-language Support**: Examples in C#, F#, and VB.NET
- **License**: MIT
- **Development Approach**: Fast-paced development with breaking changes expected

## Technology Stack & Dependencies

### Core Technologies
- **.NET 8.0**: Modern C# features, nullable reference types, implicit usings
- **Stride Game Engine 4.2.0.2381**: Primary dependency for all game engine features
- **Physics Engines**: Multiple integrations including Bepu and Bullet Physics
- **Graphics**: Direct integration with Stride's rendering pipeline
- **UI Frameworks**: Support for ImGui and Stride's native UI system

### Key NuGet Packages
```xml
<PackageReference Include="Stride.Engine" Version="4.2.0.2381" />
<PackageReference Include="Stride.Particles" Version="4.2.0.2381" />
<PackageReference Include="Stride.Physics" Version="4.2.0.2381" />
<PackageReference Include="Stride.UI" Version="4.2.0.2381" />
```

## Architecture & Code Organization

### Project Structure
```
src/
├── Stride.CommunityToolkit/              # Core toolkit
│   ├── Engine/                           # Game and Entity extensions
│   ├── Extensions/                       # General-purpose extensions
│   ├── Graphics/                         # Graphics utilities
│   ├── Helpers/                          # Helper classes
│   ├── Mathematics/                      # Math utilities (Easing, etc.)
│   ├── Physics/                          # Physics extensions
│   ├── Rendering/                        # Rendering utilities
│   └── Scripts/                          # Reusable script components
├── Stride.CommunityToolkit.Bepu/         # Bepu Physics integration
├── Stride.CommunityToolkit.Bullet/       # Bullet Physics integration
├── Stride.CommunityToolkit.DebugShapes/  # Debug visualization
├── Stride.CommunityToolkit.ImGui/        # ImGui integration
├── Stride.CommunityToolkit.Skyboxes/     # Skybox utilities
└── Stride.CommunityToolkit.Windows/      # Windows-specific features
```

### Key Patterns

#### Extension Method Pattern
Extensions follow consistent naming and organization:
```csharp
namespace Stride.CommunityToolkit.Engine;

public static class EntityExtensions
{
    /// <summary>
    /// Adds functionality to existing Stride entities
    /// </summary>
    public static void AddSomething(this Entity entity, /* parameters */)
    {
        // Implementation
    }
}
```

#### Fluent API Design
Many extensions support method chaining:
```csharp
entity.Add3DCameraController()
      .AddGizmo(graphicsDevice)
      .SetPosition(Vector3.UnitY);
```

#### Code-Only Approach
The toolkit emphasizes programmatic scene creation:
```csharp
using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    game.SetupBase3DScene();
    game.AddSkybox();
    
    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);
    entity.Transform.Position = new Vector3(0, 8, 0);
    entity.Scene = rootScene;
});
```

## Coding Conventions & Standards

### Documentation Standards
- **XML Documentation**: All public APIs must have comprehensive XML documentation
- **Summary Tags**: Describe what the method/property does
- **Param Tags**: Document all parameters with clear descriptions
- **Example Tags**: Provide usage examples for complex operations
- **Remarks Tags**: Include additional context, warnings, or usage notes

Example:
```csharp
/// <summary>
/// Adds an interactive camera script to the specified entity,
/// enabling camera movement and rotation through various input methods.
/// </summary>
/// <param name="entity">The entity to which the camera controller will be added.</param>
/// <remarks>
/// The camera can be moved using WASD, arrow keys, gamepad, or touch input.
/// Rotation is achieved using mouse, gamepad right stick, or touch gestures.
/// </remarks>
/// <example>
/// <code>
/// var cameraEntity = new Entity("Camera");
/// cameraEntity.Add3DCameraController();
/// </code>
/// </example>
public static void Add3DCameraController(this Entity entity)
```

### Naming Conventions
- **Classes**: PascalCase (e.g., `EntityExtensions`, `GameExtensions`)
- **Methods**: PascalCase with descriptive verbs (e.g., `Add3DCameraController`, `Create3DPrimitive`)
- **Parameters**: camelCase with descriptive names
- **Constants**: PascalCase for public, camelCase for private
- **Enums**: PascalCase with descriptive values (e.g., `PrimitiveModelType.Capsule`)

### Code Style
- **Nullable Reference Types**: Enabled - use `?` annotations where appropriate
- **Implicit Usings**: Enabled - avoid redundant using statements
- **File-Scoped Namespaces**: Use modern C# syntax
- **Expression Body Members**: Use for simple one-line methods
- **Pattern Matching**: Leverage modern C# pattern matching features

## Domain-Specific Knowledge

### Stride Game Engine Concepts

#### Entity-Component-System (ECS)
Stride uses an ECS architecture:
- **Entity**: Container for components
- **Component**: Data and behavior (TransformComponent, ModelComponent, etc.)
- **System**: Processes entities with specific components

#### Scene Management
```csharp
// Typical scene setup pattern
game.SetupBase3DScene(); // Creates camera, lighting, ground
var rootScene = game.GetRootScene();
entity.Scene = rootScene; // Add entities to scene
```

#### Transform Hierarchy
```csharp
// Position, rotation, scale manipulation
entity.Transform.Position = new Vector3(0, 5, 0);
entity.Transform.Rotation = Quaternion.RotationY(MathUtil.DegreesToRadians(45));
entity.Transform.Scale = new Vector3(2.0f);
```

#### Common Components
- `TransformComponent`: Position, rotation, scale
- `ModelComponent`: 3D mesh rendering
- `CameraComponent`: Camera behavior
- `RigidbodyComponent`: Physics simulation
- `ScriptComponent`: Custom behavior scripts

### Graphics and Rendering

#### Procedural Model Creation
```csharp
// Create primitive models
var cube = game.Create3DPrimitive(PrimitiveModelType.Cube);
var sphere = game.Create3DPrimitive(PrimitiveModelType.Sphere);

// Custom procedural models
var customModel = new Procedural3DModelBuilder()
    .WithMaterial(material)
    .WithVertices(vertices)
    .Build();
```

#### Materials and Textures
```csharp
// Material creation and manipulation
var material = Material.New(graphicsDevice, new MaterialDescriptor
{
    Attributes = new MaterialAttributes
    {
        Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(Color.Red))
    }
});
```

### Physics Integration

#### Multiple Physics Engines
The toolkit supports multiple physics engines:
- **Default Stride Physics**: Built-in physics system
- **Bepu Physics**: High-performance physics engine
- **Bullet Physics**: Robust physics simulation

```csharp
// Physics component addition
entity.AddRigidBody(RigidBodyTypes.Dynamic);
entity.AddCollider(ColliderShapeTypes.Box);
```

### Input and Controls

#### Camera Controllers
```csharp
// 3D camera with WASD + mouse controls
entity.Add3DCameraController();

// 2D camera for side-scrolling games
entity.Add2DCameraController();
```

#### Input Handling Patterns
Camera controllers handle multiple input methods:
- Keyboard (WASD, arrow keys)
- Mouse (right-click drag for rotation)
- Gamepad (analog sticks)
- Touch (drag and pinch gestures)

## Development Patterns

### Extension Method Best Practices
1. **Null Checking**: Always validate input parameters
2. **Fluent Returns**: Return the extended object for chaining when appropriate
3. **Parameter Validation**: Use appropriate parameter types and defaults
4. **Thread Safety**: Consider thread safety for shared resources

```csharp
public static Entity AddSomething(this Entity entity, SomeType parameter = default)
{
    ArgumentNullException.ThrowIfNull(entity);
    
    // Implementation
    
    return entity; // Enable method chaining
}
```

### Error Handling
- Use `ArgumentNullException.ThrowIfNull()` for null parameter checking
- Provide meaningful error messages
- Use appropriate exception types (`ArgumentOutOfRangeException`, `InvalidOperationException`)

### Performance Considerations
- Cache frequently accessed components
- Use object pooling for temporary objects
- Avoid allocations in update loops
- Leverage Stride's built-in performance profiling

## Examples and Usage Patterns

### Basic 3D Scene Setup
```csharp
using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    // Setup basic 3D environment
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();
    
    // Create and position entities
    var cube = game.Create3DPrimitive(PrimitiveModelType.Cube);
    cube.Transform.Position = Vector3.UnitY * 2;
    cube.Scene = rootScene;
});
```

### Physics Integration
```csharp
// Add physics to an entity
var physicsEntity = game.Create3DPrimitive(PrimitiveModelType.Sphere);
physicsEntity.AddRigidBody(RigidBodyTypes.Dynamic);
physicsEntity.AddCollider(ColliderShapeTypes.Sphere);
physicsEntity.Transform.Position = new Vector3(0, 10, 0);
```

### Custom Materials
```csharp
// Create custom materials
var redMaterial = Material.New(game.GraphicsDevice, new MaterialDescriptor
{
    Attributes = new MaterialAttributes
    {
        Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(Color.Red))
    }
});

entity.Get<ModelComponent>().Model.Materials[0] = redMaterial;
```

### Multi-Language Support
The toolkit provides examples in multiple .NET languages:

**C# Example:**
```csharp
var entity = game.Create3DPrimitive(PrimitiveModelType.Cube);
entity.Add3DCameraController();
```

**F# Example:**
```fsharp
let entity = game.Create3DPrimitive(PrimitiveModelType.Cube)
entity.Add3DCameraController()
```

**VB.NET Example:**
```vb
Dim entity = game.Create3DPrimitive(PrimitiveModelType.Cube)
entity.Add3DCameraController()
```

## Contributing Guidelines

### New Extension Methods
When creating new extension methods:
1. Place in appropriate namespace (`Stride.CommunityToolkit.Engine`, etc.)
2. Follow existing naming patterns
3. Provide comprehensive XML documentation
4. Include usage examples
5. Add parameter validation
6. Consider thread safety implications

### Testing Approach
While formal unit tests may be limited, validation should include:
- Create example projects demonstrating functionality
- Test across different platforms (Windows, Linux, etc.)
- Verify performance impact
- Ensure compatibility with different Stride versions

### Documentation Updates
- Update relevant documentation files
- Add examples to the examples directory
- Update API reference if needed
- Consider creating tutorial content

## Common Pitfalls and Best Practices

### Stride-Specific Gotchas
1. **Component Lifecycle**: Components may not be immediately available after addition
2. **Scene Hierarchy**: Entities must be added to a scene to be processed
3. **Resource Management**: Graphics resources need proper disposal
4. **Threading**: Most Stride operations must occur on the main thread

### Performance Tips
1. Cache component references instead of repeated `Get<T>()` calls
2. Use `entity.EntityManager.GetComponent<T>(entity)` for better performance
3. Batch similar operations together
4. Leverage Stride's built-in profiling tools

### Memory Management
1. Dispose of graphics resources properly
2. Use object pooling for frequently created/destroyed objects
3. Be mindful of closure captures in lambda expressions
4. Avoid boxing in hot code paths

## Integration Points

### Stride Engine Integration
- Seamlessly extends existing Stride functionality
- Maintains compatibility with Stride's asset pipeline
- Leverages Stride's component system
- Integrates with Stride's rendering pipeline

### Third-Party Integrations
- **ImGui**: Immediate mode GUI integration
- **Bepu/Bullet Physics**: Alternative physics engines
- **Platform-Specific**: Windows-specific functionality

This toolkit emphasizes rapid development while maintaining high code quality and comprehensive documentation. When contributing, focus on creating intuitive APIs that follow established patterns and provide clear value to game developers using the Stride engine.