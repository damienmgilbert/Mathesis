using System.Text;
using System.Text.RegularExpressions;
using Mathesis.Knowledge;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Printing;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Explanation;

/// <summary>How much of a derivation an explanation shows.</summary>
public enum Verbosity : byte
{
    /// <summary>One line per top-level step with the law applied, then the result.</summary>
    Brief,

    /// <summary>Every step with its before and after forms; automatic arithmetic is folded into the step that follows it.</summary>
    Standard,

    /// <summary>Every step, including automatic normalization and the sub-steps of methods.</summary>
    Detailed,
}

/// <summary>The output format of an explanation.</summary>
public enum ExplanationFormat : byte
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>Markdown with LaTeX math in <c>$…$</c>.</summary>
    Markdown,

    /// <summary>A LaTeX <c>align*</c> environment.</summary>
    Latex,
}

/// <summary>Options of <see cref="ExplanationRenderer"/>.</summary>
public sealed record ExplainOptions
{
    /// <summary>How much to show.</summary>
    public Verbosity Verbosity { get; init; } = Verbosity.Standard;

    /// <summary>The output format.</summary>
    public ExplanationFormat Format { get; init; } = ExplanationFormat.Text;

    /// <summary>
    /// The curriculum level of the learner. Steps that cite an entry above it are flagged. (<see cref="Simplification.Simplifier"/> already avoids such
    /// entries when its <see cref="MathContext.Level"/> is set; the flag covers derivations built without a level.)
    /// </summary>
    public CurriculumLevel? Level { get; init; }
}

/// <summary>
/// Renders a <see cref="Derivation"/> as text, Markdown or LaTeX (docs/design/07-engines.md, "Explanation rendering"). The sentence of each step is the
/// <c>explain</c> template of the entry it cites, with <c>{name}</c> filled from the step's bindings.
/// </summary>
public static partial class ExplanationRenderer
{
    /// <summary>Renders <paramref name="derivation"/>.</summary>
    public static string Render(Derivation derivation, ExplainOptions? options = null, KnowledgeBase? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        options ??= new ExplainOptions();
        catalog ??= KnowledgeBase.Default;
        var lines = new List<Line>();
        Collect(derivation, options, catalog, 0, lines);
        var result = Print(derivation.End, options.Format);
        return options.Format switch
        {
            ExplanationFormat.Text => AsText(derivation, options, lines, result),
            ExplanationFormat.Markdown => AsMarkdown(derivation, options, lines, result),
            _ => AsLatex(derivation, options, lines, result),
        };
    }

    private sealed record Line(int Depth, string Sentence, string Before, string After, bool Flagged);

    private static void Collect(Derivation derivation, ExplainOptions options, KnowledgeBase catalog, int depth, List<Line> into)
    {
        foreach (var step in derivation.Steps)
        {
            if (step.RuleName == "normalize" && options.Verbosity != Verbosity.Detailed) continue;
            var flagged = options.Level is { } level && step.Level > level;
            into.Add(new Line(depth, Sentence(step, catalog, options.Format), Print(step.Before, options.Format), Print(step.After, options.Format), flagged));
            if (options.Verbosity == Verbosity.Detailed && step.Substeps is { } sub) Collect(sub, options, catalog, depth + 1, into);
        }
    }

    private static string Print(Expr e, ExplanationFormat format) => format == ExplanationFormat.Text ? TextPrinter.Print(e, PrintOptions.Presentation) : LatexPrinter.Print(e);

    private static string Sentence(Step step, KnowledgeBase catalog, ExplanationFormat format)
    {
        if (step.RuleName == "normalize") return "Simplify the arithmetic and put the expression in standard order.";
        string? template = null;
        var name = step.RuleName;
        if (step.Entry is { } id && catalog.TryGet(id.Value, out var entry))
        {
            template = entry.Explain;
            name = entry.Name;
        }
        var values = step.Explanation.Arguments.GroupBy(a => a.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
        // A product written by juxtaposition in the template (2{a}{b}) reads as "2 · 1 · x", not as the number 21 and a letter; a negative value after a
        // symbol or digit is parenthesized (4{a} with a = −1 is 4(-1)).
        var source = template ?? name;
        var previousEnd = -1;
        var text = Placeholder().Replace(source, m =>
        {
            var printed = values.TryGetValue(m.Groups[1].Value, out var v) ? (format == ExplanationFormat.Text ? TextPrinter.Print(v, PrintOptions.Presentation) : "$" + LatexPrinter.Print(v) + "$") : m.Value;
            var afterDigit = m.Index > 0 && char.IsAsciiDigit(source[m.Index - 1]) && char.IsAsciiDigit(printed[0]);
            var separator = m.Index == previousEnd || afterDigit ? " · " : string.Empty;
            if (format == ExplanationFormat.Text && printed.StartsWith('-') && m.Index > 0 && source[m.Index - 1] is not (' ' or '(' or '=' or '−')) printed = "(" + printed + ")";
            previousEnd = m.Index + m.Length;
            return separator + printed;
        });

        // A rejected candidate names itself even when the entry's sentence is general.
        if (values.TryGetValue("candidate", out var candidate) && !source.Contains("{candidate}", StringComparison.Ordinal))
        {
            text += " Candidate: " + (format == ExplanationFormat.Text ? TextPrinter.Print(candidate, PrintOptions.Presentation) : "$" + LatexPrinter.Print(candidate) + "$");
            if (values.TryGetValue("reason", out var reason) && reason is Symbol reasonSymbol) text += " (" + reasonSymbol.Name.Replace('_', ' ') + ")";
            text += ".";
        }
        return text;
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9_]*)\}")]
    private static partial Regex Placeholder();

    private static string AsText(Derivation d, ExplainOptions options, List<Line> lines, string result)
    {
        var sb = new StringBuilder();
        sb.Append("Start: ").Append(Print(d.Start, ExplanationFormat.Text)).Append('\n');
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var indent = new string(' ', 2 * line.Depth);
            sb.Append(indent).Append(i + 1).Append(". ").Append(line.Sentence);
            if (line.Flagged) sb.Append(" [above the selected level]");
            sb.Append('\n');
            if (options.Verbosity != Verbosity.Brief) sb.Append(indent).Append("   ").Append(line.Before).Append("  →  ").Append(line.After).Append('\n');
        }
        sb.Append("Result: ").Append(result).Append('\n');
        return sb.ToString();
    }

    private static string AsMarkdown(Derivation d, ExplainOptions options, List<Line> lines, string result)
    {
        var sb = new StringBuilder();
        sb.Append("Start: $").Append(Print(d.Start, ExplanationFormat.Latex)).Append("$\n\n");
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var indent = new string(' ', 4 * line.Depth);
            sb.Append(indent).Append(i + 1).Append(". ").Append(line.Sentence);
            if (line.Flagged) sb.Append(" *(above the selected level)*");
            sb.Append('\n');
            if (options.Verbosity != Verbosity.Brief) sb.Append(indent).Append("   $").Append(line.Before).Append(" \\to ").Append(line.After).Append("$\n");
        }
        sb.Append("\nResult: $").Append(result).Append("$\n");
        return sb.ToString();
    }

    private static string AsLatex(Derivation d, ExplainOptions options, List<Line> lines, string result)
    {
        var sb = new StringBuilder();
        sb.Append("\\begin{align*}\n");
        sb.Append("  ").Append(Print(d.Start, ExplanationFormat.Latex));
        foreach (var line in lines)
        {
            sb.Append(" \\\\\n  &= ").Append(options.Verbosity == Verbosity.Brief ? string.Empty : line.After).Append(" && \\text{").Append(EscapeText(line.Sentence)).Append(line.Flagged ? " (above the selected level)" : string.Empty).Append('}');
        }
        if (options.Verbosity == Verbosity.Brief) sb.Append(" \\\\\n  &= ").Append(result);
        sb.Append("\n\\end{align*}\n");
        return sb.ToString();
    }

    private static string EscapeText(string s) => s.Replace("$", string.Empty, StringComparison.Ordinal).Replace("\\", string.Empty, StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("&", "\\&", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace("#", "\\#", StringComparison.Ordinal);
}
