using System.Collections.Immutable;
using System.Globalization;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Knowledge;

/// <summary>Interprets the fields of a parsed <see cref="MlawEntry"/>: sorts, statements, enumerations, lists.</summary>
internal static class EntryReader
{
    public static readonly ImmutableHashSet<string> Courses =
    [
        "Algebra", "Trigonometry", "Proofs", "PreCalculus", "Calculus", "DifferentialEquations", "LinearAlgebra", "FiniteMath", "NumericalAnalysis",
    ];

    public static Entry Read(MlawEntry raw, string file, List<KnowledgeDiagnostic> diagnostics)
    {
        var id = raw.Id;

        void Error(int line, string message) => diagnostics.Add(new(DiagnosticSeverity.Error, id, file, line, message));

        MlawField? Field(string key) => raw.Fields.FirstOrDefault(f => f.Key == key);

        // Variables first: every expression is parsed with their sorts.
        var vars = ImmutableArray.CreateBuilder<VarDecl>();
        if (Field("vars") is { } varsField)
        {
            foreach (var item in SplitTopLevel(string.Join(" ", varsField.Lines)))
            {
                var colon = item.IndexOf(':', StringComparison.Ordinal);
                var name = (colon < 0 ? item : item[..colon]).Trim();
                var sortText = colon < 0 ? "real" : item[(colon + 1)..].Trim();
                if (!Symbol.IsValidName(name))
                {
                    Error(varsField.Line, $"'{name}' is not a valid variable name.");
                    continue;
                }
                if (vars.Any(v => v.Name == name))
                {
                    Error(varsField.Line, $"Variable '{name}' is declared twice.");
                    continue;
                }
                if (ParseSort(sortText) is not { } sort)
                {
                    Error(varsField.Line, $"Unknown sort '{sortText}' for variable '{name}'.");
                    continue;
                }
                vars.Add(new(name, sortText, sort));
            }
        }
        var declarations = vars.ToDictionary(v => v.Name, v => v.Sort);
        var options = new ParserOptions { Declarations = declarations };

        Expr? Parse(MlawField? field, string text)
        {
            if (field is null) return null;
            if (text.Length == 0)
            {
                Error(field.Line, $"The '{field.Key}' field is empty.");
                return null;
            }
            var result = Parser.Parse(text, options);
            if (!result.Success)
            {
                Error(field.Line, $"In '{field.Key}': {result.Errors[0]} (in `{text}`)");
                return null;
            }
            var undeclared = result.Expr!.FreeSymbols.Where(s => !declarations.ContainsKey(s.Name)).Select(s => s.Name).Order(StringComparer.Ordinal).ToArray();
            if (undeclared.Length > 0) Error(field.Line, $"In '{field.Key}': undeclared variable{(undeclared.Length > 1 ? "s" : string.Empty)} {string.Join(", ", undeclared.Select(n => "'" + n + "'"))}.");
            return result.Expr;
        }

        Expr? ParseOne(string key) => Field(key) is { } f ? Parse(f, string.Join(" ", f.Lines)) : null;

        var given = ImmutableArray.CreateBuilder<Expr>();
        if (Field("given") is { } givenField)
        {
            foreach (var line in givenField.Lines)
            {
                if (Parse(givenField, line) is { } e) given.Add(e);
            }
        }

        var steps = ImmutableArray.CreateBuilder<MethodStep>();
        if (Field("steps") is { } stepsField)
        {
            foreach (var line in stepsField.Lines)
            {
                var (description, rest) = SplitQuoted(line);
                if (description is null)
                {
                    Error(stepsField.Line, $"A step must start with a quoted description: `{line}`.");
                    continue;
                }
                if (Parse(stepsField, rest) is { } form) steps.Add(new MethodStep(description, form));
            }
        }

        CurriculumLevel? level = null;
        if (Field("level") is { } levelField)
        {
            var text = string.Join(" ", levelField.Lines).Trim();
            if (Enum.TryParse<CurriculumLevel>(text, ignoreCase: false, out var l) && Enum.IsDefined(l) && !text.All(char.IsAsciiDigit)) level = l;
            else Error(levelField.Line, $"Unknown level '{text}'.");
        }

        var courses = ImmutableArray<string>.Empty;
        if (Field("course") is { } courseField)
        {
            courses = [.. SplitTopLevel(string.Join(" ", courseField.Lines)).Select(c => c.Trim())];
            foreach (var c in courses)
            {
                if (!Courses.Contains(c)) Error(courseField.Line, $"Unknown course '{c}'.");
            }
        }

        var tagsText = Field("tags") is { } tagsField ? string.Join(" ", tagsField.Lines) : string.Empty;
        var tags = tagsText.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToImmutableArray();

        OrientDirection? orient = null;
        if (Field("orient") is { } orientField)
        {
            var text = string.Join(" ", orientField.Lines).Trim();
            if (Enum.TryParse<OrientDirection>(text, ignoreCase: true, out var o) && text.All(char.IsAsciiLetter)) orient = o;
            else Error(orientField.Line, $"Unknown orientation '{text}'; expected ltr, rtl, both or none.");
        }

        VerifyMode? verify = null;
        if (Field("verify") is { } verifyField)
        {
            var text = string.Join(" ", verifyField.Lines).Trim();
            if (Enum.TryParse<VerifyMode>(text, ignoreCase: true, out var v) && text.All(char.IsAsciiLetter)) verify = v;
            else Error(verifyField.Line, $"Unknown verify mode '{text}'.");
        }

        var quantities = ImmutableArray.CreateBuilder<Quantity>();
        if (Field("quantities") is { } quantitiesField)
        {
            foreach (var item in SplitTopLevel(string.Join(" ", quantitiesField.Lines)))
            {
                var space = item.IndexOf(' ', StringComparison.Ordinal);
                var name = space < 0 ? item : item[..space];
                var (description, _) = SplitQuoted(space < 0 ? string.Empty : item[(space + 1)..].Trim());
                if (description is null) Error(quantitiesField.Line, $"Expected: name \"description\", found `{item}`.");
                else quantities.Add(new(name, description));
            }
        }

        var refs = ImmutableArray.CreateBuilder<Reference>();
        if (Field("refs") is { } refsField)
        {
            foreach (var item in SplitTopLevel(string.Join(" ", refsField.Lines)))
            {
                var colon = item.IndexOf(':', StringComparison.Ordinal);
                if (colon <= 0) Error(refsField.Line, $"A reference looks like dlmf:4.21.2, book:\"…\" or url:…, found `{item}`.");
                else refs.Add(new(item[..colon].Trim(), item[(colon + 1)..].Trim().Trim('"')));
            }
        }

        ImmutableArray<EntryId> Ids(string key)
        {
            if (Field(key) is not { } f) return [];
            var list = ImmutableArray.CreateBuilder<EntryId>();
            foreach (var item in SplitTopLevel(string.Join(" ", f.Lines)))
            {
                if (EntryId.IsValid(item)) list.Add(new(item));
                else Error(f.Line, $"'{item}' is not a valid entry ID.");
            }
            return list.ToImmutable();
        }

        var sample = ImmutableArray.CreateBuilder<SampleHint>();
        if (Field("sample") is { } sampleField)
        {
            foreach (var item in SplitTopLevel(string.Join(" ", sampleField.Lines)))
            {
                if (ParseSample(item) is { } hint) sample.Add(hint);
                else Error(sampleField.Line, $"Expected a sample hint like 'x in (0, 10)' or 'n in 1..12', found `{item}`.");
            }
        }

        string? Text(string key)
        {
            if (Field(key) is not { } f) return null;
            var joined = string.Join(" ", f.Lines).Trim();
            return joined.Length >= 2 && joined[0] == '"' && joined[^1] == '"' ? joined[1..^1] : joined;
        }

        return new Entry
        {
            Id = new(id),
            Kind = raw.Kind,
            Name = raw.Title,
            Domain = raw.Domain,
            DomainTitle = raw.DomainTitle,
            File = file,
            Line = raw.Line,
            Vars = vars.ToImmutable(),
            Statement = ParseOne("statement"),
            Given = given.ToImmutable(),
            Then = ParseOne("then"),
            Defines = ParseOne("defines"),
            Iff = ParseOne("iff"),
            Where = ParseOne("where"),
            Complex = ParseOne("complex"),
            Orient = orient,
            Match = ParseOne("match"),
            Yields = ParseOne("yields"),
            AppliesTo = ParseOne("applies-to"),
            Steps = steps.ToImmutable(),
            Result = ParseOne("result"),
            SolveFor = Field("solve-for") is { } sf ? [.. SplitTopLevel(string.Join(" ", sf.Lines))] : [],
            Quantities = quantities.ToImmutable(),
            Level = level,
            Courses = courses,
            Tags = tags,
            TagsText = tagsText,
            Explain = Text("explain"),
            Aliases = Ids("aliases"),
            Refs = refs.ToImmutable(),
            See = Ids("see"),
            Verify = verify,
            Sample = sample.ToImmutable(),
            ImplementedBy = Text("implemented-by"),
            Rationale = Text("rationale"),
        };
    }

    // ----- Helpers -----

    /// <summary>Splits at commas outside parentheses, brackets, braces and double quotes.</summary>
    public static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var inString = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"') inString = !inString;
            if (inString) continue;
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        var last = text[start..].Trim();
        if (last.Length > 0) parts.Add(last);
        return parts.Where(p => p.Length > 0).ToList();
    }

    // `"text" rest` → (text, rest); (null, line) when the line does not start with a quote.
    private static (string? Quoted, string Remainder) SplitQuoted(string line)
    {
        line = line.Trim();
        if (line.Length == 0 || line[0] != '"') return (null, line);
        var end = line.IndexOf('"', 1);
        return end < 0 ? (null, line) : (line[1..end], line[(end + 1)..].Trim());
    }

    private static SampleHint? ParseSample(string item)
    {
        var parts = item.Split(" in ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0) return null;
        var range = parts[1];
        if (range.Contains("..", StringComparison.Ordinal))
        {
            var ends = range.Split("..", StringSplitOptions.TrimEntries);
            if (ends.Length == 2 && double.TryParse(ends[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(ends[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) return new(parts[0], a, b, true, false, false);
            return null;
        }
        if (range.Length < 5 || range[0] is not ('(' or '[') || range[^1] is not (')' or ']')) return null;
        var bounds = range[1..^1].Split(',', StringSplitOptions.TrimEntries);
        if (bounds.Length != 2 || !double.TryParse(bounds[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var low) || !double.TryParse(bounds[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var high)) return null;
        return new(parts[0], low, high, false, range[0] == '(', range[^1] == ')');
    }

    /// <summary>Parses a sort as written in <c>vars</c>: <c>real</c>, <c>integer</c>, <c>function(R -&gt; R)</c>, <c>matrix(m, n)</c>, <c>vector(n)</c>, <c>set(real)</c>, …</summary>
    public static Sort? ParseSort(string text)
    {
        text = text.Trim();
        switch (text.ToLowerInvariant())
        {
            case "real": return Sort.Real;
            case "integer": return Sort.Integer;
            case "natural": return Sort.Natural;
            case "rational": return Sort.Rational;
            case "complex": return Sort.Complex;
            case "boolean": return Sort.Boolean;
            case "number": return Sort.Number;
            case "any": return Sort.Any;
        }
        var open = text.IndexOf('(', StringComparison.Ordinal);
        if (open <= 0 || text[^1] != ')') return null;
        var head = text[..open].Trim().ToLowerInvariant();
        var inner = text[(open + 1)..^1].Trim();
        switch (head)
        {
            case "function":
                {
                    var arrow = inner.IndexOf("->", StringComparison.Ordinal);
                    if (arrow < 0) return null;
                    var domain = ParseDomain(inner[..arrow]);
                    var codomain = ParseDomain(inner[(arrow + 2)..]);
                    return domain is null || codomain is null ? null : Sort.FunctionOf(domain, codomain);
                }
            case "set":
                return ParseSort(inner) is { } element ? Sort.SetOf(element) : null;
            case "vector":
                {
                    var parts = SplitTopLevel(inner);
                    return parts.Count is 1 or 2 && ParseDimension(parts[0]) is { } length ? Sort.VectorOf(length, parts.Count == 2 ? ParseSort(parts[1]) ?? Sort.Real : Sort.Real) : null;
                }
            case "matrix":
                {
                    var parts = SplitTopLevel(inner);
                    return parts.Count is 2 or 3 && ParseDimension(parts[0]) is { } rows && ParseDimension(parts[1]) is { } columns ? Sort.MatrixOf(rows, columns, parts.Count == 3 ? ParseSort(parts[2]) ?? Sort.Real : Sort.Real) : null;
                }
            default:
                return null;
        }
    }

    private static Expr? ParseDimension(string text)
    {
        var result = Parser.Parse(text.Trim());
        return result.Success ? result.Expr : null;
    }

    // A function domain: R, Z, N, Q, C (or a sort name) and products such as "R x R".
    private static Sort? ParseDomain(string text)
    {
        var factors = text.Split([" x ", "×"], StringSplitOptions.TrimEntries);
        var sorts = new List<Sort>();
        foreach (var f in factors)
        {
            Sort? sort = f switch
            {
                "R" => Sort.Real,
                "Z" => Sort.Integer,
                "N" => Sort.Natural,
                "Q" => Sort.Rational,
                "C" => Sort.Complex,
                _ => ParseSort(f),
            };
            if (sort is null) return null;
            sorts.Add(sort);
        }
        return sorts.Count == 1 ? sorts[0] : Sort.TupleOf([.. sorts]);
    }
}
