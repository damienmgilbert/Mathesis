using System.Collections.Immutable;

namespace Mathesis.Knowledge;

/// <summary>
/// The knowledge catalog: every entry with lookup by ID, domain, kind, tag, course and text
/// (docs/design/06-knowledge-catalog.md). <see cref="Default"/> is the catalog embedded in the assembly.
/// </summary>
public sealed class KnowledgeBase
{
    private static readonly ImmutableHashSet<string> DomainPrefixes = ["alg", "trig", "logic", "pre", "calc", "ode", "linalg", "fin", "num", "conv"];

    private static readonly Lazy<KnowledgeBase> DefaultCatalog = new(LoadEmbedded);

    private readonly Dictionary<string, Entry> _byId;
    private readonly Dictionary<string, Entry> _byAlias;

    private KnowledgeBase(ImmutableArray<Entry> entries, ImmutableArray<KnowledgeDiagnostic> diagnostics)
    {
        Entries = entries;
        Diagnostics = diagnostics;
        _byId = new(StringComparer.Ordinal);
        _byAlias = new(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            _byId.TryAdd(e.Id.Value, e);
            foreach (var alias in e.Aliases) _byAlias.TryAdd(alias.Value, e);
        }
    }

    /// <summary>The catalog embedded in this assembly (the <c>knowledge/**/*.mlaw</c> files).</summary>
    public static KnowledgeBase Default => DefaultCatalog.Value;

    /// <summary>All entries, in file order.</summary>
    public ImmutableArray<Entry> Entries { get; }

    /// <summary>Problems found while loading: syntax errors, unparsable statements, undeclared variables, broken links.</summary>
    public ImmutableArray<KnowledgeDiagnostic> Diagnostics { get; }

    /// <summary>The diagnostics that are errors.</summary>
    public IEnumerable<KnowledgeDiagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>The number of entries.</summary>
    public int Count => Entries.Length;

    /// <summary>Loads a catalog from <c>.mlaw</c> sources.</summary>
    /// <param name="files">File names and contents.</param>
    public static KnowledgeBase Load(IEnumerable<(string Name, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var diagnostics = new List<KnowledgeDiagnostic>();
        var entries = new List<Entry>();
        var rawEntries = new List<MlawEntry>();
        foreach (var (name, text) in files.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            var parsed = MlawParser.Parse(text, name);
            diagnostics.AddRange(parsed.Diagnostics);
            foreach (var raw in parsed.Entries)
            {
                rawEntries.Add(raw);
                entries.Add(EntryReader.Read(raw, name, diagnostics));
            }
        }
        Validate(entries, rawEntries, diagnostics);
        return new([.. entries], [.. diagnostics]);
    }

    private static KnowledgeBase LoadEmbedded()
    {
        var assembly = typeof(KnowledgeBase).Assembly;
        var files = new List<(string, string)>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.EndsWith(".mlaw", StringComparison.Ordinal)) continue;
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            files.Add((resource, reader.ReadToEnd()));
        }
        return Load(files);
    }

    /// <summary>Whether an entry (or an alias of one) has this ID.</summary>
    public bool Contains(string id) => _byId.ContainsKey(id) || _byAlias.ContainsKey(id);

    /// <summary>Finds an entry by ID or alias.</summary>
    public bool TryGet(string id, out Entry entry)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_byId.TryGetValue(id, out var found) || _byAlias.TryGetValue(id, out found))
        {
            entry = found;
            return true;
        }
        entry = null!;
        return false;
    }

    /// <summary>Finds an entry by ID or alias.</summary>
    /// <exception cref="KeyNotFoundException">No entry has this ID.</exception>
    public Entry Get(string id) => TryGet(id, out var entry) ? entry : throw new KeyNotFoundException($"No catalog entry '{id}'.");

    /// <summary>Finds an entry by ID or alias.</summary>
    /// <exception cref="KeyNotFoundException">No entry has this ID.</exception>
    public Entry Get(EntryId id) => Get(id.Value);

    /// <summary>Entries whose domain group is <paramref name="prefix"/> or lies below it (<c>alg</c> includes <c>alg.exp</c>).</summary>
    public IEnumerable<Entry> ByDomain(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return Entries.Where(e => e.Domain == prefix || e.Domain.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    /// <summary>Entries of one kind.</summary>
    public IEnumerable<Entry> ByKind(EntryKind kind) => Entries.Where(e => e.Kind == kind);

    /// <summary>Entries with a tag (rule-set membership or search keyword).</summary>
    public IEnumerable<Entry> ByTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return Entries.Where(e => e.Tags.Contains(tag));
    }

    /// <summary>Entries tagged with a course.</summary>
    public IEnumerable<Entry> ByCourse(string course)
    {
        ArgumentNullException.ThrowIfNull(course);
        return Entries.Where(e => e.Courses.Contains(course));
    }

    /// <summary>Entries at or below a curriculum level.</summary>
    public IEnumerable<Entry> UpToLevel(CurriculumLevel level) => Entries.Where(e => e.Level is { } l && l <= level);

    /// <summary>Entries whose ID, name, tags or explanation contain every word of <paramref name="text"/> (case-insensitive), best matches first.</summary>
    public IEnumerable<Entry> Search(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        return Entries
            .Select(e => (Entry: e, Score: Score(e, words)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.Id.Value, StringComparer.Ordinal)
            .Select(x => x.Entry);
    }

    private static int Score(Entry e, string[] words)
    {
        var score = 0;
        foreach (var word in words)
        {
            var best = 0;
            if (e.Id.Value.Contains(word, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 3);
            if (e.Name.Contains(word, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 4);
            if (e.Tags.Any(t => t.Contains(word, StringComparison.OrdinalIgnoreCase))) best = Math.Max(best, 2);
            if (e.Explain?.Contains(word, StringComparison.OrdinalIgnoreCase) == true) best = Math.Max(best, 1);
            if (best == 0) return 0;
            score += best;
        }
        return score;
    }

    // ----- Validation across entries -----

    private static void Validate(List<Entry> entries, List<MlawEntry> raw, List<KnowledgeDiagnostic> diagnostics)
    {
        void Error(Entry e, string message) => diagnostics.Add(new(DiagnosticSeverity.Error, e.Id.Value, e.File, e.Line, message));
        void Warn(Entry e, string message) => diagnostics.Add(new(DiagnosticSeverity.Warning, e.Id.Value, e.File, e.Line, message));

        var ids = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var groups = raw.Select(r => r.Domain).ToHashSet(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            if (!EntryId.IsValid(e.Id.Value)) Error(e, $"'{e.Id}' is not a valid ID (lowercase kebab-case, <domain>.<topic>.<name>).");
            else if (!DomainPrefixes.Contains(e.Id.Domain)) Error(e, $"Unknown domain prefix '{e.Id.Domain}'.");
            if (!ids.TryAdd(e.Id.Value, e)) Error(e, $"Duplicate ID '{e.Id}' (also at {ids[e.Id.Value].File}:{ids[e.Id.Value].Line}).");
        }
        foreach (var e in entries)
        {
            foreach (var alias in e.Aliases)
            {
                if (ids.ContainsKey(alias.Value)) Error(e, $"The alias '{alias}' is the ID of another entry.");
            }
        }
        foreach (var e in entries)
        {
            foreach (var target in e.See)
            {
                if (!ids.ContainsKey(target.Value) && !entries.Any(x => x.Aliases.Contains(target))) Error(e, $"'see' refers to the unknown entry '{target}'.");
            }
            if (e.Level is null) Error(e, "Missing 'level'.");
            if (e.Courses.IsEmpty) Error(e, "Missing 'course'.");

            var hasContent = e.Statement is not null || e.Then is not null || e.Defines is not null || e.Match is not null || e.AppliesTo is not null || !string.IsNullOrWhiteSpace(e.Explain) || !string.IsNullOrWhiteSpace(e.Rationale);
            if (!hasContent) Error(e, "The entry has no statement, conclusion, definition, pattern, method description or explanation.");
            switch (e.Kind)
            {
                case EntryKind.Law or EntryKind.Formula:
                    if (e.Statement is null) Error(e, $"A {e.Kind.ToString().ToLowerInvariant()} needs a 'statement'.");
                    break;
                case EntryKind.Pattern:
                    if (e.Match is null || e.Yields is null) Error(e, "A pattern needs 'match' and 'yields'.");
                    break;
                case EntryKind.Axiom:
                    if (e.Refs.IsEmpty) Error(e, "An axiom must cite a source in 'refs'.");
                    break;
            }
            if (e.Verify == VerifyMode.None && string.IsNullOrWhiteSpace(e.Rationale)) Error(e, "'verify: none' requires a 'rationale'.");
            if (e.TagsText.Contains('|', StringComparison.Ordinal) && e.Orient != OrientDirection.Both) Warn(e, "Tag alternatives with '|' are only meaningful with 'orient: both'.");
            foreach (var name in e.SolveFor)
            {
                if (e.Vars.All(v => v.Name != name)) Error(e, $"'solve-for' names the undeclared variable '{name}'.");
            }
            foreach (var q in e.Quantities)
            {
                if (e.Vars.All(v => v.Name != q.Variable)) Error(e, $"'quantities' names the undeclared variable '{q.Variable}'.");
            }
            if (e.Kind is EntryKind.Law or EntryKind.Formula && e.Statement is { } s && s.Sort != Symbolics.Sort.Boolean) Warn(e, $"The statement has sort {s.Sort}, not Boolean.");
        }
        foreach (var r in raw)
        {
            foreach (var u in r.Uses)
            {
                if (!groups.Contains(u)) diagnostics.Add(new(DiagnosticSeverity.Error, r.Id, string.Empty, r.Line, $"'uses {u}' names a domain group that has no entries."));
            }
        }
    }
}
