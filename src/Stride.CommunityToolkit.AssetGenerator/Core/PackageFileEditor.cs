using System.Text;

namespace Stride.CommunityToolkit.AssetGenerator.Core;

/// <summary>
/// Entries the generator needs a package file to contain.
/// </summary>
public sealed record PackageMergeRequest
{
    /// <summary>Name written into <c>Meta.Name</c> when a package file is created from scratch.</summary>
    public string PackageName { get; init; } = "Package";

    /// <summary>Folders that must be listed under <c>AssetFolders</c>, for example <c>Assets</c>.</summary>
    public IReadOnlyList<string> AssetFolders { get; init; } = [];

    /// <summary>Folders that must be listed under <c>ResourceFolders</c>, for example <c>Resources</c>.</summary>
    public IReadOnlyList<string> ResourceFolders { get; init; } = [];

    /// <summary>Entries that must be listed under <c>RootAssets</c>, formatted as <c>id:location</c>.</summary>
    public IReadOnlyList<string> RootAssets { get; init; } = [];
}

/// <summary>
/// Outcome of a package merge.
/// </summary>
/// <param name="Content">Merged package text, or <see langword="null"/> when the merge was skipped.</param>
/// <param name="Changed">Whether <paramref name="Content"/> differs from the input.</param>
/// <param name="SkipReason">Why the merge was skipped, or <see langword="null"/> when it was not.</param>
/// <param name="AddedEntries">Human-readable description of every entry that was added.</param>
public sealed record PackageMergeResult(string? Content, bool Changed, string? SkipReason, IReadOnlyList<string> AddedEntries)
{
    /// <summary>Whether the merge was skipped because the file could not be parsed with confidence.</summary>
    public bool Skipped => SkipReason is not null;
}

/// <summary>
/// Reads, merges and writes <c>.sdpkg</c> files line by line.
/// </summary>
/// <remarks>
/// <para>
/// The package is merged, never regenerated: users have hand-authored content (extra asset folders,
/// <c>Meta</c>, bundles) that must survive byte-identical. Only missing entries are added.
/// </para>
/// <para>
/// A line-based approach is deliberate — Stride itself parses the <c>RootAssets</c> section this way in
/// <c>sources/tools/Stride.TemplateGenerator/TemplatePreprocessor.cs</c> (<c>CollectRootAssets</c>).
/// Round-tripping through a general YAML library would reformat everything else in the file.
/// </para>
/// </remarks>
public sealed class PackageFileEditor
{
    private const string AssetFoldersSection = "AssetFolders";
    private const string ResourceFoldersSection = "ResourceFolders";
    private const string RootAssetsSection = "RootAssets";

    private const string EntryIndent = "    ";

    /// <summary>
    /// Merges the requested entries into <paramref name="existingContent"/>.
    /// </summary>
    /// <param name="existingContent">
    /// Current package text, or <see langword="null"/>/empty to create a package from scratch.
    /// </param>
    /// <param name="request">Entries that must be present after the merge.</param>
    public PackageMergeResult Merge(string? existingContent, PackageMergeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return new PackageMergeResult(CreateNew(request), Changed: true, SkipReason: null, DescribeAll(request));
        }

        var document = LineDocument.Parse(existingContent);

        if (!document.Lines.Any(line => line.Trim() == AssetFormats.PackageTag))
        {
            return new PackageMergeResult(null, false, $"the file does not start with '{AssetFormats.PackageTag}'", []);
        }

        var added = new List<string>();

        var sections = new (string Name, IReadOnlyList<string> Values, Func<string, string> Format, Func<string, string> Payload, Func<string, IEnumerable<string>> Keys)[]
        {
            (AssetFoldersSection, request.AssetFolders, static value => $"{EntryIndent}-   Path: !dir {value}", DirectoryPayload, DirectoryKeys),
            (ResourceFoldersSection, request.ResourceFolders, static value => $"{EntryIndent}- !dir {value}", DirectoryPayload, DirectoryKeys),
            (RootAssetsSection, request.RootAssets, static value => $"{EntryIndent}-   {value}", static value => value, RootAssetKeys),
        };

        foreach (var (name, values, format, payload, keys) in sections)
        {
            if (values.Count == 0) continue;

            var outcome = EnsureEntries(document, name, values, format, payload, keys);

            if (outcome.SkipReason is not null)
            {
                return new PackageMergeResult(null, false, outcome.SkipReason, []);
            }

            added.AddRange(outcome.Added.Select(entry => $"{name}: {entry}"));
        }

        var content = document.ToText();

        return new PackageMergeResult(content, !string.Equals(content, existingContent, StringComparison.Ordinal), null, added);
    }

    private static string CreateNew(PackageMergeRequest request)
    {
        var builder = new StringBuilder();

        void Line(string text) => builder.Append(text).Append(AssetFormats.NewLine);

        Line(AssetFormats.PackageTag);
        Line($"SerializedVersion: {AssetFormats.PackageSerializedVersion}");
        Line("Meta:");
        Line($"{EntryIndent}Name: {request.PackageName}");
        Line($"{EntryIndent}Version: 1.0.0");
        Line($"{EntryIndent}Authors: []");
        Line($"{EntryIndent}Owners: []");
        Line($"{EntryIndent}Dependencies: null");

        Line($"{AssetFoldersSection}:");

        foreach (var folder in request.AssetFolders)
        {
            Line($"{EntryIndent}-   Path: !dir {folder}");
        }

        Line($"{ResourceFoldersSection}:");

        foreach (var folder in request.ResourceFolders)
        {
            Line($"{EntryIndent}- !dir {folder}");
        }

        Line($"{RootAssetsSection}:");

        foreach (var rootAsset in request.RootAssets)
        {
            Line($"{EntryIndent}-   {rootAsset}");
        }

        return builder.ToString();
    }

    private static List<string> DescribeAll(PackageMergeRequest request)
        =>
        [
            .. request.AssetFolders.Select(value => $"{AssetFoldersSection}: {value}"),
            .. request.ResourceFolders.Select(value => $"{ResourceFoldersSection}: {value}"),
            .. request.RootAssets.Select(value => $"{RootAssetsSection}: {value}"),
        ];

    private static (IReadOnlyList<string> Added, string? SkipReason) EnsureEntries(
        LineDocument document,
        string section,
        IReadOnlyList<string> values,
        Func<string, string> format,
        Func<string, string> payloadSelector,
        Func<string, IEnumerable<string>> keySelector)
    {
        var sectionIndex = document.Lines.FindIndex(line =>
            line.StartsWith(section, StringComparison.Ordinal)
            && line.Length > section.Length
            && line[section.Length] == ':');

        if (sectionIndex < 0)
        {
            var appended = new List<string> { $"{section}:" };
            appended.AddRange(values.Select(format));

            document.Lines.AddRange(appended);

            return (values, null);
        }

        var inlineValue = document.Lines[sectionIndex][(section.Length + 1)..].Trim();

        if (inlineValue.Length > 0 && inlineValue != "[]")
        {
            return ([], $"section '{section}' has an inline value ('{inlineValue}') the generator does not understand");
        }

        var (blockEnd, existingKeys) = ScanBlock(document, sectionIndex, keySelector);

        var missing = values.Where(value => !keySelector(payloadSelector(value)).Any(existingKeys.Contains)).ToList();

        if (missing.Count == 0) return ([], null);

        if (inlineValue == "[]")
        {
            document.Lines[sectionIndex] = $"{section}:";
        }

        document.Lines.InsertRange(blockEnd, missing.Select(format));

        return (missing, null);
    }

    /// <summary>
    /// Returns the insertion point at the end of a section's block, plus the keys of its existing entries.
    /// </summary>
    private static (int BlockEnd, HashSet<string> ExistingKeys) ScanBlock(
        LineDocument document,
        int sectionIndex,
        Func<string, IEnumerable<string>> keySelector)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        var index = sectionIndex + 1;
        var blockEnd = sectionIndex + 1;
        var currentEntry = new StringBuilder();

        void FlushEntry()
        {
            if (currentEntry.Length == 0) return;

            foreach (var key in keySelector(currentEntry.ToString()))
            {
                keys.Add(key);
            }

            currentEntry.Clear();
        }

        while (index < document.Lines.Count)
        {
            var line = document.Lines[index];

            if (line.Trim().Length == 0)
            {
                // A blank line may sit inside or after the block; only extend the insertion point once
                // a further indented line proves the block continues.
                index++;
                continue;
            }

            if (!char.IsWhiteSpace(line[0])) break;

            var trimmed = line.TrimStart();

            if (trimmed.StartsWith('-'))
            {
                FlushEntry();
                currentEntry.Append(trimmed.TrimStart('-').Trim());
            }
            else if (currentEntry.Length > 0)
            {
                // Continuation of the current entry (a nested key such as AlternativePath). Kept on its
                // own line so key extraction can treat one line as one value.
                currentEntry.Append('\n').Append(trimmed);
            }

            index++;
            blockEnd = index;
        }

        FlushEntry();

        return (blockEnd, keys);
    }

    /// <summary>Renders a requested folder value in the same shape an existing entry has.</summary>
    private static string DirectoryPayload(string value) => $"!dir {value}";

    private static IEnumerable<string> DirectoryKeys(string entry)
    {
        foreach (var line in entry.Split('\n'))
        {
            var marker = line.IndexOf("!dir ", StringComparison.Ordinal);

            if (marker < 0) continue;

            var value = line[(marker + "!dir ".Length)..].Trim();

            if (value.Length == 0) continue;

            yield return $"dir:{DeterministicId.Normalize(value)}";
        }
    }

    private static IEnumerable<string> RootAssetKeys(string entry)
    {
        // A root asset is always a single `id:location` line; ignore anything nested under it.
        var line = entry.Split('\n')[0];

        var separator = line.IndexOf(':');

        if (separator < 0)
        {
            yield return $"root:{line.Trim().ToLowerInvariant()}";
            yield break;
        }

        var id = line[..separator].Trim().ToLowerInvariant();
        var location = line[(separator + 1)..].Trim();

        // Either half is enough to consider the asset already registered: the same id must never be
        // listed twice, and a location that is already rooted belongs to whoever put it there.
        yield return $"id:{id}";
        yield return $"location:{DeterministicId.Normalize(location)}";
    }

    /// <summary>
    /// A text file split into lines, remembering its line ending and whether it ended with one.
    /// </summary>
    private sealed class LineDocument
    {
        private LineDocument(List<string> lines, string newLine, bool trailingNewLine)
        {
            Lines = lines;
            NewLine = newLine;
            TrailingNewLine = trailingNewLine;
        }

        public List<string> Lines { get; }

        public string NewLine { get; }

        public bool TrailingNewLine { get; }

        public static LineDocument Parse(string content)
        {
            var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var trailingNewLine = content.EndsWith('\n');

            var body = trailingNewLine ? content[..^newLine.Length] : content;

            var lines = body.Split('\n').Select(static line => line.TrimEnd('\r')).ToList();

            return new LineDocument(lines, newLine, trailingNewLine);
        }

        public string ToText()
        {
            var text = string.Join(NewLine, Lines);

            return TrailingNewLine ? text + NewLine : text;
        }
    }
}
