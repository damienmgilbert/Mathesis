// Catalog coverage report. Run: dotnet run eng/check-catalog-coverage.cs [-- --all]
// Compares the IDs in the first column of the tables of docs/design/domains/*.md with the entries of the knowledge catalog and prints the
// coverage per domain document and the missing IDs. Fails (exit code 1) when the sections seeded in Milestone 1 are covered below 95%.
#:project ../src/Mathesis.Knowledge/Mathesis.Knowledge.csproj

using System.Text.RegularExpressions;
using Mathesis.Knowledge;

const double Threshold = 0.95;

// The sections of each domain document that Phase 6 of PLAN.md seeds (docs/design/domains/<file>: section headings).
var seeded = new Dictionary<string, string[]>(StringComparer.Ordinal)
{
    ["d1-algebra.md"] =
    [
        "Axioms and properties of real numbers", "Fractions, ratios and percents", "Exponents", "Radicals", "Absolute value", "Logarithms",
        "Complex numbers (arithmetic)", "Polynomials: products and expansion", "Factoring", "Polynomial division and roots", "Quadratics",
        "Rational expressions and partial fractions", "Equations", "Conventions",
    ],
    ["d2-trigonometry.md"] =
    [
        "Definitions, angles and units", "Exact values", "Fundamental identities", "Sum and difference", "Multiple angles",
        "Half angles and power reduction", "Product-to-sum and sum-to-product", "Linear combinations, substitutions and exponential forms",
        "Inverse functions",
    ],
    ["d5-calculus.md"] = ["Limits", "Derivatives: definition and rules", "Derivative table", "Antiderivatives", "Integration techniques"],
    ["d7-linear-algebra.md"] = ["Linear systems", "Determinants"],
};

var root = Directory.GetCurrentDirectory();
while (root is not null && !File.Exists(Path.Combine(root, "Mathesis.slnx"))) root = Path.GetDirectoryName(root);
if (root is null)
{
    Console.Error.WriteLine("Run from inside the repository (Mathesis.slnx not found).");
    return 1;
}

var showAll = args.Contains("--all");
var catalog = KnowledgeBase.Default;
var idPattern = new Regex(@"^`?([a-z]+(?:\.[a-z0-9-]+)+)`?$", RegexOptions.Compiled);
var failed = false;
var totalSeeded = 0;
var totalSeededCovered = 0;

foreach (var file in Directory.GetFiles(Path.Combine(root, "docs", "design", "domains"), "*.md").Order(StringComparer.Ordinal))
{
    var name = Path.GetFileName(file);
    var sections = new List<(string Heading, List<string> Ids)>();
    foreach (var line in File.ReadLines(file))
    {
        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            sections.Add((line[3..].Trim(), []));
            continue;
        }
        if (sections.Count == 0 || !line.StartsWith('|')) continue;
        var cells = line.Split('|');
        if (cells.Length < 3) continue;
        var match = idPattern.Match(cells[1].Trim());
        if (match.Success) sections[^1].Ids.Add(match.Groups[1].Value);
    }

    var all = sections.SelectMany(s => s.Ids).Distinct().ToList();
    var allCovered = all.Count(catalog.Contains);
    var wanted = seeded.TryGetValue(name, out var headings) ? sections.Where(s => headings.Contains(s.Heading)).ToList() : [];
    var seededIds = wanted.SelectMany(s => s.Ids).Distinct().ToList();
    var seededCovered = seededIds.Count(catalog.Contains);
    totalSeeded += seededIds.Count;
    totalSeededCovered += seededCovered;

    Console.Write($"{name,-28} all sections {allCovered,4}/{all.Count,-4} ({Percent(allCovered, all.Count)})");
    if (seededIds.Count > 0) Console.Write($"   seeded {seededCovered,4}/{seededIds.Count,-4} ({Percent(seededCovered, seededIds.Count)})");
    Console.WriteLine();

    foreach (var (heading, ids) in wanted)
    {
        var missing = ids.Where(id => !catalog.Contains(id)).ToList();
        if (missing.Count > 0) Console.WriteLine($"    missing in seeded section '{heading}': {string.Join(", ", missing)}");
    }
    if (showAll)
    {
        var missingOutside = sections.Where(s => !wanted.Contains(s)).SelectMany(s => s.Ids).Where(id => !catalog.Contains(id)).Distinct().ToList();
        if (missingOutside.Count > 0) Console.WriteLine($"    not yet seeded: {missingOutside.Count} IDs (first: {string.Join(", ", missingOutside.Take(8))})");
    }
    if (seededIds.Count > 0 && (double)seededCovered / seededIds.Count < Threshold) failed = true;
}

// Entries the documents do not list are fine (conventions, helper entries); report them for information.
var documented = new HashSet<string>(StringComparer.Ordinal);
foreach (var file in Directory.GetFiles(Path.Combine(root, "docs", "design", "domains"), "*.md"))
{
    foreach (var line in File.ReadLines(file))
    {
        if (!line.StartsWith('|')) continue;
        var cells = line.Split('|');
        if (cells.Length >= 3 && idPattern.Match(cells[1].Trim()) is { Success: true } m) documented.Add(m.Groups[1].Value);
    }
}
var extra = catalog.Entries.Where(e => !documented.Contains(e.Id.Value)).Select(e => e.Id.Value).ToList();
Console.WriteLine();
Console.WriteLine($"Catalog entries: {catalog.Count}; seeded sections covered: {totalSeededCovered}/{totalSeeded} ({Percent(totalSeededCovered, totalSeeded)}); entries not listed in the domain docs: {extra.Count}");
if (extra.Count > 0 && extra.Count <= 30) Console.WriteLine("  " + string.Join(", ", extra));
Console.WriteLine(failed ? "Coverage check FAILED (a seeded document is below 95%)." : "Coverage check passed.");
return failed ? 1 : 0;

static string Percent(int part, int whole) => whole == 0 ? "n/a" : (100.0 * part / whole).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%";
