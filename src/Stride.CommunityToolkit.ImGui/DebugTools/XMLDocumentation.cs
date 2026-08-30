using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml;
using WebUtility = System.Net.WebUtility;

namespace Stride.CommunityToolkit.ImGui.DebugTools;

/// <summary>
/// Utility class to provide documentation for various types where available with the assembly
/// </summary>
internal static class XMLDocumentation
{
    static readonly ConcurrentDictionary<Assembly, XmlDocument?> _documents = new();
    static readonly ConcurrentDictionary<MemberInfo, CachedDocumentation?> _documentation = new();

    // Two-level cache: one xml document per assembly, one parsed entry per member. A member without
    // documentation is cached as null so the file is not searched again.
    static bool TryGetDocumentation(MemberInfo member, [NotNullWhen(true)] out CachedDocumentation? documentation)
    {
        if (!_documentation.TryGetValue(member, out documentation))
        {
            // Load and cache the xml documentation file sitting next to the member's assembly.
            var assembly = member.Module.Assembly;

            if (!_documents.TryGetValue(assembly, out XmlDocument? document))
            {
                var filepath = assembly.Location;

                const string LOCAL_PREFIX = "file:///";
                if (filepath.StartsWith(LOCAL_PREFIX))
                {
                    filepath = filepath.Substring(LOCAL_PREFIX.Length);
                    filepath = Path.ChangeExtension(filepath, ".xml");
                    try
                    {
                        using var streamReader = new StreamReader(filepath);
                        document = new XmlDocument();
                        document.Load(streamReader);
                    }
                    catch (FileNotFoundException)
                    {
                        document = null;
                    }
                }
                else
                {
                    // not sure how to safely deal with other prefixes
                    document = null;
                }

                _documents.TryAdd(assembly, document);
            }

            if (document is null)
            {
                documentation = null;
            }
            else
            {
                // Build the doc-comment id the compiler writes: M:Type.Method(Params), T:Type, P:/F: for the rest.
                string fullName;
                switch (member)
                {
                    case MethodInfo methodInfo:
                        var parameters = string.Join(",", methodInfo.GetParameters().Select(p => p.ParameterType.FullName));

                        if (parameters.Length > 0)
                            parameters = $"({parameters})";

                        fullName = $"M:{methodInfo.DeclaringType?.FullName}.{methodInfo.Name}{parameters}";
                        break;
                    case Type type:
                        fullName = $"T:{type.FullName}";
                        break;
                    default:
                        fullName = $"{member.MemberType.ToString()[0]}:{member.DeclaringType}.{member.Name}";
                        break;
                }

                // A member without an entry is cached as null so the document is not searched again.
                if (document["doc"]?["members"]?.SelectSingleNode($"member[@name='{fullName}']") is XmlElement element)
                    documentation = new CachedDocumentation(element);
                else
                    documentation = null;
            }

            _documentation.TryAdd(member, documentation);
        }

        return documentation != null;
    }

    /// <summary> Returns false if the documentation file wasn't found </summary>
    internal static bool TryGetSummary(MemberInfo member, [NotNullWhen(true)] out string? summary)
    {
        if (TryGetDocumentation(member, out CachedDocumentation? doc))
        {
            summary = doc.CleanSummary;
            return true;
        }

        summary = null;
        return false;
    }

    private sealed class CachedDocumentation
    {
        private XmlElement Element { get; }

        internal string CleanSummary
        {
            get
            {
                lock (_lock)
                {
                    return _cleanSummary ??= GetCleanSummary();
                }
            }
        }

        string? _cleanSummary;

        readonly object _lock = new();

        internal CachedDocumentation(XmlElement elem)
        {
            Element = elem;
        }

        string GetCleanSummary()
        {
            string? rawString = Element.SelectSingleNode("summary")?.InnerXml;
            if (rawString is null)
                return "";

            // Decodes xml entities like '&amp;'
            rawString = WebUtility.HtmlDecode(rawString);
            rawString = rawString.Replace("<see cref=", "").Replace("/>", "");

            // cleanup tabs and spaces on new line
            return string.Join('\n', rawString.Split('\n').Select(line => line.Trim()).SkipWhile(line => line.Length == 0));
        }
    }
}