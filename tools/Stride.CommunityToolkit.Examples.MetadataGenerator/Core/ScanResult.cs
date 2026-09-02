namespace Stride.CommunityToolkit.Examples.MetadataGenerator.Core;

/// <summary>
/// The outcome of a scan: what parsed, and how much did not.
/// </summary>
/// <param name="Examples">The examples whose metadata block parsed successfully.</param>
/// <param name="Failures">
/// How many blocks could not be parsed. Each one is an example silently missing from the manifest, so
/// the count is folded into the error total rather than treated as a warning.
/// </param>
public sealed record ScanResult(IReadOnlyList<ParsedExample> Examples, int Failures);