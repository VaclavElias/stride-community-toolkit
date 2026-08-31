namespace Stride.CommunityToolkit.Examples.Core;

/// <summary>
/// The <c>examples-manifest.json</c> document, as the launchers read it.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberate second copy of the shape the metadata generator writes, rather than a project
/// reference to it. The generator is a console application carrying a generic host, Serilog and
/// YamlDotNet; a launcher needs none of that to read a JSON file. The two are coupled by the file
/// format and by <see cref="SchemaVersion"/>, which is checked on load, not by a shared assembly.
/// </para>
/// <para>
/// Only the fields a launcher actually uses are declared. Unknown properties are ignored by
/// <c>System.Text.Json</c>, so the generator can add fields without breaking either launcher.
/// </para>
/// </remarks>
public sealed class ExampleManifest
{
    /// <summary>The schema version this code understands.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>Gets or sets the schema version of the document.</summary>
    public int SchemaVersion { get; set; }

    /// <summary>Gets or sets when the manifest was generated, in ISO 8601.</summary>
    public string? GeneratedAt { get; set; }

    /// <summary>Gets or sets the examples, already ordered by language, level and order.</summary>
    public List<ManifestExample> Examples { get; set; } = [];
}