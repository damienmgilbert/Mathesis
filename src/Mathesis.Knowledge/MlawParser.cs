using System.Collections.Immutable;
using System.Text;

namespace Mathesis.Knowledge;

/// <summary>A field of an entry as written: its key, its lines (the value line first, then continuation lines) and where it starts.</summary>
/// <param name="Key">The field key.</param>
/// <param name="Lines">The value lines, trimmed.</param>
/// <param name="Line">The 1-based line of the key.</param>
public sealed record MlawField(string Key, ImmutableArray<string> Lines, int Line);

/// <summary>An entry as written, before its fields are interpreted.</summary>
/// <param name="Kind">The entry kind.</param>
/// <param name="Name">The name (the last segment of the ID).</param>
/// <param name="Title">The display title.</param>
/// <param name="Line">The 1-based line of the header.</param>
/// <param name="Domain">The domain group the entry belongs to.</param>
/// <param name="DomainTitle">The title of the domain group.</param>
/// <param name="Uses">The domain groups the group declares it uses.</param>
/// <param name="Fields">The fields in file order.</param>
public sealed record MlawEntry(EntryKind Kind, string Name, string Title, int Line, string Domain, string DomainTitle, ImmutableArray<string> Uses, ImmutableArray<MlawField> Fields)
{
    /// <summary>The identifier: the domain group followed by the name, or just <c>conv.name</c> for a group named <c>conv</c>.</summary>
    public string Id => Domain + "." + Name;
}

/// <summary>A parsed <c>.mlaw</c> file.</summary>
/// <param name="Name">The file name.</param>
/// <param name="Entries">The entries in order.</param>
/// <param name="Diagnostics">Problems with the file's structure.</param>
public sealed record MlawFile(string Name, ImmutableArray<MlawEntry> Entries, ImmutableArray<KnowledgeDiagnostic> Diagnostics);

/// <summary>
/// Reads the structure of a <c>.mlaw</c> file (docs/design/06-knowledge-catalog.md, "The .mlaw format"): domain groups, entries
/// and their fields. Interpreting the fields (parsing statements with the Mathesis parser) is <see cref="KnowledgeBase"/>'s job.
/// </summary>
/// <remarks>
/// A file may contain several <c>domain</c> groups, each with its own <c>uses</c> lines; the grammar's single header per file is the
/// special case of one group. Continuation lines are indented deeper than the field key they continue.
/// </remarks>
public static class MlawParser
{
    /// <summary>The field keys, in the order the lint tool expects them.</summary>
    public static ImmutableArray<string> Keys { get; } =
    [
        "vars", "statement", "given", "then", "defines", "iff", "where", "complex", "orient", "match", "yields", "applies-to", "steps",
        "result", "solve-for", "quantities", "level", "course", "tags", "explain", "aliases", "refs", "see", "verify", "sample",
        "implemented-by", "rationale",
    ];

    /// <summary>Parses <paramref name="text"/>.</summary>
    /// <param name="text">The file contents.</param>
    /// <param name="fileName">The name used in diagnostics.</param>
    public static MlawFile Parse(string text, string fileName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fileName);
        var diagnostics = new List<KnowledgeDiagnostic>();
        var entries = new List<MlawEntry>();

        void Error(int line, string message, string? id = null) => diagnostics.Add(new(DiagnosticSeverity.Error, id, fileName, line, message));

        string? domain = null;
        var domainTitle = string.Empty;
        var uses = new List<string>();

        (EntryKind Kind, string Name, string Title, int Line)? header = null;
        var fields = new List<MlawField>();
        string? fieldKey = null;
        var fieldLines = new List<string>();
        var fieldLine = 0;
        var fieldIndent = -1;
        var entryUses = ImmutableArray<string>.Empty;
        var entryDomain = string.Empty;
        var entryDomainTitle = string.Empty;

        void EndField()
        {
            if (fieldKey is not null) fields.Add(new(fieldKey, [.. fieldLines], fieldLine));
            fieldKey = null;
            fieldLines.Clear();
        }

        void EndEntry()
        {
            EndField();
            if (header is { } h) entries.Add(new(h.Kind, h.Name, h.Title, h.Line, entryDomain, entryDomainTitle, entryUses, [.. fields]));
            header = null;
            fields.Clear();
            fieldIndent = -1;
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            var raw = StripComment(lines[i].TrimEnd('\r'));
            if (raw.Trim().Length == 0) continue;
            if (raw.Contains('\t', StringComparison.Ordinal))
            {
                Error(number, "Tabs are not allowed; indent with spaces.");
                raw = raw.Replace("\t", "    ", StringComparison.Ordinal);
            }
            var indent = raw.Length - raw.TrimStart().Length;
            var content = raw.Trim();

            if (indent == 0)
            {
                var space = content.IndexOf(' ', StringComparison.Ordinal);
                var word = space < 0 ? content : content[..space];
                var rest = space < 0 ? string.Empty : content[(space + 1)..].Trim();
                if (word == "domain")
                {
                    EndEntry();
                    var (prefix, title) = SplitNameAndTitle(rest);
                    if (prefix.Length == 0 || title is null) Error(number, "Expected: domain PREFIX \"title\".");
                    domain = prefix;
                    domainTitle = title ?? string.Empty;
                    uses = [];
                }
                else if (word == "uses")
                {
                    EndEntry();
                    if (domain is null) Error(number, "'uses' must follow a 'domain' line.");
                    else if (rest.Length == 0) Error(number, "Expected: uses PREFIX.");
                    else uses.Add(rest);
                }
                else if (word.All(char.IsAsciiLetterLower) && Enum.TryParse<EntryKind>(word, ignoreCase: true, out var kind))
                {
                    EndEntry();
                    var (name, title) = SplitNameAndTitle(rest);
                    if (domain is null)
                    {
                        Error(number, "An entry must follow a 'domain' line.");
                        continue;
                    }
                    if (name.Length == 0 || title is null)
                    {
                        Error(number, $"Expected: {word} NAME \"title\".");
                        continue;
                    }
                    header = (kind, name, title, number);
                    entryDomain = domain;
                    entryDomainTitle = domainTitle;
                    entryUses = [.. uses];
                }
                else
                {
                    Error(number, $"Unexpected '{word}'; expected 'domain', 'uses' or an entry kind.");
                }
                continue;
            }

            if (header is null)
            {
                Error(number, "A field must belong to an entry.");
                continue;
            }

            if (fieldKey is not null && indent > fieldIndent)
            {
                fieldLines.Add(content);
                continue;
            }

            var colon = content.IndexOf(':', StringComparison.Ordinal);
            var key = colon > 0 ? content[..colon] : string.Empty;
            if (key.Length == 0 || !IsKey(key))
            {
                Error(number, $"Expected 'key: value' with a key from the field list, found '{Shorten(content)}'.", header.Value.Name);
                continue;
            }
            if (fieldIndent >= 0 && indent != fieldIndent) Error(number, "Fields of an entry must be indented equally.", header.Value.Name);
            if (!Keys.Contains(key)) Error(number, $"Unknown field '{key}'.", header.Value.Name);
            else if (fields.Any(f => f.Key == key) || fieldKey == key) Error(number, $"Duplicate field '{key}'.", header.Value.Name);
            EndField();
            fieldIndent = indent;
            fieldKey = key;
            fieldLine = number;
            var value = content[(colon + 1)..].Trim();
            if (value.Length > 0) fieldLines.Add(value);
        }
        EndEntry();
        return new(fileName, [.. entries], [.. diagnostics]);
    }

    private static bool IsKey(string key)
    {
        foreach (var c in key)
        {
            if (!(char.IsAsciiLetterLower(c) || c == '-')) return false;
        }
        return true;
    }

    private static string Shorten(string s) => s.Length <= 40 ? s : s[..40] + "…";

    // Splits `name "title"` into the name and the unquoted title.
    private static (string Name, string? Title) SplitNameAndTitle(string text)
    {
        var quote = text.IndexOf('"', StringComparison.Ordinal);
        if (quote < 0) return (text.Trim(), null);
        var end = text.LastIndexOf('"');
        if (end <= quote) return (text[..quote].Trim(), null);
        return (text[..quote].Trim(), text[(quote + 1)..end]);
    }

    // A '#' starts a comment unless it is inside a double-quoted string.
    private static string StripComment(string line)
    {
        var inString = false;
        var sb = new StringBuilder();
        foreach (var c in line)
        {
            if (c == '"') inString = !inString;
            if (c == '#' && !inString) break;
            sb.Append(c);
        }
        return sb.ToString().TrimEnd();
    }
}
