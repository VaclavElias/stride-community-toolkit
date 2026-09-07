namespace Stride.CommunityToolkit.Examples.MetadataGenerator.Core;

/// <summary>
/// A successfully deserialized metadata block, together with the raw text it came from.
/// </summary>
/// <remarks>
/// The raw YAML and the literal key list are kept because two of the most valuable checks cannot be
/// made against the deserialized object: an unknown or mis-cased key has, by then, already been
/// discarded, and an unquoted <c>#</c> has already truncated its value silently. Both are only
/// visible in the source text.
/// </remarks>
/// <param name="Metadata">The deserialized metadata.</param>
/// <param name="SourcePath">The full path of the file the block was read from.</param>
/// <param name="RawYaml">The raw YAML between the block delimiters.</param>
/// <param name="DeclaredKeys">The top-level keys exactly as they were written.</param>
public sealed record ParsedExample(
    ExampleMetadata Metadata,
    string SourcePath,
    string RawYaml,
    IReadOnlyList<string> DeclaredKeys);