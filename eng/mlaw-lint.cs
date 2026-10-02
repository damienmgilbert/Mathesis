// Lint for the .mlaw catalog sources. Run: dotnet run eng/mlaw-lint.cs
// Reports syntax errors, statements that do not parse, undeclared variables, missing levels and courses, broken links (everything the
// loader checks), and formatting problems: tabs, trailing spaces, fields indented other than two spaces, fields out of the canonical order,
// entries not separated by a blank line, and a missing final newline.
#:project ../src/Mathesis.Knowledge/Mathesis.Knowledge.csproj

using Mathesis.Knowledge;

var root = Directory.GetCurrentDirectory();
while (root is not null && !File.Exists(Path.Combine(root, "Mathesis.slnx"))) root = Path.GetDirectoryName(root);
if (root is null)
{
    Console.Error.WriteLine("Run from inside the repository (Mathesis.slnx not found).");
    return 1;
}

var knowledge = Path.Combine(root, "knowledge");
var files = Directory.GetFiles(knowledge, "*.mlaw", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
var problems = new List<string>();

foreach (var path in files)
{
    var name = Path.GetRelativePath(root, path).Replace('\\', '/');
    var text = File.ReadAllText(path);
    var lines = text.Split('\n');
    if (!text.EndsWith('\n')) problems.Add($"{name}: the file does not end with a newline.");

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i].TrimEnd('\r');
        if (line.Contains('\t', StringComparison.Ordinal)) problems.Add($"{name}({i + 1}): tab character.");
        if (line.Length > 0 && line[^1] == ' ') problems.Add($"{name}({i + 1}): trailing space.");
    }

    var parsed = MlawParser.Parse(text, name);
    foreach (var d in parsed.Diagnostics) problems.Add(d.ToString());

    var previousEnd = 0;
    foreach (var entry in parsed.Entries)
    {
        // Entries are separated by a blank line (or a comment) from whatever precedes them.
        if (entry.Line >= 2 && previousEnd > 0 && lines[entry.Line - 2].Trim().Length > 0 && !lines[entry.Line - 2].TrimStart().StartsWith('#'))
        {
            problems.Add($"{name}({entry.Line}): {entry.Id} is not preceded by a blank line.");
        }

        var order = entry.Fields.Select(f => MlawParser.Keys.IndexOf(f.Key)).ToList();
        for (var i = 1; i < order.Count; i++)
        {
            if (order[i] >= 0 && order[i - 1] >= 0 && order[i] < order[i - 1])
            {
                problems.Add($"{name}({entry.Fields[i].Line}): {entry.Id}: field '{entry.Fields[i].Key}' should come before '{entry.Fields[i - 1].Key}'.");
                break;
            }
        }
        foreach (var f in entry.Fields)
        {
            var written = lines[f.Line - 1];
            if (written.Length - written.TrimStart().Length != 2) problems.Add($"{name}({f.Line}): {entry.Id}: fields are indented two spaces.");
        }
        previousEnd = entry.Fields.Length == 0 ? entry.Line : entry.Fields[^1].Line + entry.Fields[^1].Lines.Length;
    }
}

var catalog = KnowledgeBase.Load(files.Select(f => (Name: Path.GetRelativePath(knowledge, f).Replace('\\', '/'), Text: File.ReadAllText(f))));
foreach (var d in catalog.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) problems.Add(d.ToString());

foreach (var p in problems.Distinct().Order(StringComparer.Ordinal)) Console.WriteLine(p);
Console.WriteLine(problems.Count == 0 ? $"mlaw-lint: {files.Count} files, {catalog.Count} entries, no problems." : $"mlaw-lint: {problems.Distinct().Count()} problem(s).");
return problems.Count == 0 ? 0 : 1;
