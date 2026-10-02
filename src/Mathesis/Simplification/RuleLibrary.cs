using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Simplification;

/// <summary>
/// The rewrite rules derived from the knowledge catalog, grouped into rule sets by tag (docs/design/06-knowledge-catalog.md, "From entries to
/// rules"). Rules exist only for catalog entries: laws, axioms and formulas whose statement is an equation between expressions and whose
/// <c>orient</c> asks for rewriting, and patterns (<c>match → yields</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both sides are normalized to the Canonical level with the declared variables as wilds, because the rewrite engine keeps every expression
/// Canonical: <c>a/sqrt(b)</c> becomes <c>a·b^(−1/2)</c> and matches the same way in the subject. A bare variable that is an operand of a
/// sum or product becomes an optional wild (default 0 or 1), so one rule covers <c>x</c>, <c>3x</c> and <c>x·y</c>.
/// </para>
/// <para>
/// <c>orient: both</c> gives two rules. Their tags are the two alternatives of <c>tags: a | b</c>; without alternatives the right-to-left
/// rule joins the set <c>tag-reverse</c>, so no rule set contains both directions of one law (which would loop).
/// </para>
/// <para>
/// Skipped, with the reason in <see cref="Skipped"/>: entries with a function-, matrix-, set- or boolean-valued variable, statements that
/// are not an equation, equations whose canonical sides coincide or whose left side is a bare variable, and a direction whose replacement
/// mentions a variable the pattern does not bind.
/// </para>
/// </remarks>
public sealed class RuleLibrary
{
    private static readonly Lazy<RuleLibrary> DefaultLibrary = new(() => new RuleLibrary(KnowledgeBase.Default));

    private readonly Dictionary<string, RuleSet> _sets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rule> _rules = new(StringComparer.Ordinal);

    /// <summary>Derives the rules of <paramref name="catalog"/>.</summary>
    public RuleLibrary(KnowledgeBase catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        var skipped = new List<(string Id, string Reason)>();
        var byTag = new Dictionary<string, List<Rule>>(StringComparer.Ordinal);
        foreach (var entry in catalog.Entries)
        {
            foreach (var rule in Derive(entry, skipped))
            {
                _rules[rule.Name] = rule;
                foreach (var tag in rule.Tags)
                {
                    if (!byTag.TryGetValue(tag, out var list)) byTag[tag] = list = [];
                    list.Add(rule);
                }
            }
        }
        foreach (var (tag, rules) in byTag) _sets[tag] = new RuleSet(tag, rules);
        Skipped = [.. skipped];
    }

    /// <summary>The library of <see cref="KnowledgeBase.Default"/>.</summary>
    public static RuleLibrary Default => DefaultLibrary.Value;

    /// <summary>The catalog the rules come from.</summary>
    public KnowledgeBase Catalog { get; }

    /// <summary>The entries that look like rewrite rules (an oriented equation) but could not be turned into one, with the reason.</summary>
    public ImmutableArray<(string Id, string Reason)> Skipped { get; }

    /// <summary>The names of the rule sets (catalog tags that have rules).</summary>
    public IReadOnlyCollection<string> Tags => _sets.Keys;

    /// <summary>All rules.</summary>
    public IEnumerable<Rule> Rules => _rules.Values;

    /// <summary>The rule set named <paramref name="tag"/>; an empty set when no rule has the tag.</summary>
    public RuleSet this[string tag] => _sets.TryGetValue(tag, out var set) ? set : new RuleSet(tag, []);

    /// <summary>Whether any rule has the tag.</summary>
    public bool Contains(string tag) => _sets.ContainsKey(tag);

    /// <summary>Finds a rule by its name (<c>alg.exp.product-of-powers</c>, or <c>…~rtl</c> for the reverse direction).</summary>
    public bool TryGetRule(string name, out Rule rule) => _rules.TryGetValue(name, out rule!);

    /// <summary>The union of the rule sets named by <paramref name="tags"/>.</summary>
    public RuleSet Union(string name, params string[] tags) => RuleSet.Union(name, [.. tags.Select(t => this[t])]);

    // ----- Derivation -----

    private static IEnumerable<Rule> Derive(Entry entry, List<(string, string)> skipped)
    {
        var equations = Equations(entry);
        for (var index = 0; index < equations.Count; index++)
        {
            var (lhs, rhs, orient) = equations[index];
            var suffix = equations.Count > 1 ? "#" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            foreach (var rule in DeriveEquation(entry, lhs, rhs, orient, suffix, skipped)) yield return rule;
        }
    }

    private static IEnumerable<Rule> DeriveEquation(Entry entry, Expr lhsExpr, Expr rhsExpr, OrientDirection? orientation, string suffix, List<(string, string)> skipped)
    {
        if (orientation is null or OrientDirection.None) yield break;

        // Only scalar, numeric variables can be pattern variables of the first-order matcher.
        foreach (var v in entry.Vars)
        {
            if (!(v.Sort.IsNumeric || v.Sort == Sort.Boolean))
            {
                skipped.Add((entry.Id.Value, $"the variable '{v.Name}' has the sort {v.Sort}"));
                yield break;
            }
        }
        var variables = entry.Declarations;
        var guard = entry.Where is null ? null : ToWilds(entry.Where, variables);
        var (forwardTags, reverseTags) = TagGroups(entry, orientation.Value);

        if (orientation is OrientDirection.Ltr or OrientDirection.Both)
        {
            if (Build(entry, entry.Id.Value + suffix, lhsExpr, rhsExpr, guard, variables, forwardTags, skipped) is { } rule) yield return rule;
        }
        if (orientation is OrientDirection.Rtl or OrientDirection.Both)
        {
            var name = entry.Id.Value + suffix + (orientation == OrientDirection.Both ? "~rtl" : string.Empty);
            if (Build(entry, name, rhsExpr, lhsExpr, guard, variables, orientation == OrientDirection.Both ? reverseTags : forwardTags, skipped) is { } rule) yield return rule;
        }
    }

    // The equations an entry states: a statement may be a conjunction of equations; a pattern is its match and yields.
    private static List<(Expr Lhs, Expr Rhs, OrientDirection? Orient)> Equations(Entry entry)
    {
        switch (entry.Kind)
        {
            case EntryKind.Law or EntryKind.Formula or EntryKind.Axiom or EntryKind.Theorem:
                var pending = new Stack<Expr>();
                if (entry.Statement is { } statement) pending.Push(statement);
                var found = new List<(Expr Lhs, Expr Rhs, OrientDirection? Orient)>();
                while (pending.Count > 0)
                {
                    var e = pending.Pop();
                    if (e is Apply { Operator: var and, Arguments: var parts } && and == Operators.And)
                    {
                        foreach (var part in parts.Reverse()) pending.Push(part);
                    }
                    else if (e is Apply { Operator: var op, Arguments: [var l, var r] } && op == Operators.Eq && l.Sort.IsNumeric && r.Sort.IsNumeric)
                    {
                        found.Add((l, r, entry.Orient));
                    }
                }
                return found;
            case EntryKind.Pattern when entry.Match is { } match && entry.Yields is { } yields:
                return [(match, yields, entry.Orient ?? OrientDirection.Ltr)];
            default:
                return [];
        }
    }

    // "a, b | c" → ltr tags [a, b], rtl tags [c]; without '|' the reverse rule joins the sets "<tag>-reverse".
    private static (ImmutableArray<string> Forward, ImmutableArray<string> Reverse) TagGroups(Entry entry, OrientDirection orientation)
    {
        var text = entry.TagsText;
        if (orientation == OrientDirection.Both && text.Contains('|', StringComparison.Ordinal))
        {
            var groups = text.Split('|', 2);
            string[] Split(string g) => g.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return ([.. Split(groups[0])], [.. Split(groups[1])]);
        }
        return (entry.Tags, [.. entry.Tags.Select(t => t + "-reverse")]);
    }

    private static Expr ToWilds(Expr e, IReadOnlyDictionary<string, Sort> variables) =>
        e.Transform(x => x is Symbol s && variables.ContainsKey(s.Name) ? new Wild(s.Name) : null);

    private static Rule? Build(Entry entry, string name, Expr lhs, Expr rhs, Expr? guard, IReadOnlyDictionary<string, Sort> variables, ImmutableArray<string> tags, List<(string, string)> skipped)
    {
        if (tags.IsEmpty)
        {
            skipped.Add((name, "the entry has no tags, so no rule set would contain its rule"));
            return null;
        }
        if (ContainsBinder(rhs) || ContainsBinder(lhs))
        {
            skipped.Add((name, "a sum, product or other binder in the statement"));
            return null;
        }

        var pattern = Normalizer.Canonical(ToWilds(lhs, variables));
        var replacement = Normalizer.Canonical(ToWilds(rhs, variables));
        if (pattern is Wild || pattern.Equals(replacement) || !pattern.Walk().Any(w => w.Expr is Wild))
        {
            skipped.Add((name, "the canonical sides coincide, or the left side is a bare variable or has no variable"));
            return null;
        }

        var options = Options(pattern, variables);
        var bound = pattern.Walk().Where(w => w.Expr is Wild).Select(w => ((Wild)w.Expr).Name).ToHashSet();
        var unbound = replacement.Walk().Concat(guard is null ? [] : guard.Walk()).Where(w => w.Expr is Wild).Select(w => ((Wild)w.Expr).Name).Where(n => !bound.Contains(n)).Distinct().ToList();
        if (unbound.Count > 0)
        {
            skipped.Add((name, $"the replacement or condition uses {string.Join(", ", unbound)}, which the pattern does not bind"));
            return null;
        }
        return new Rule(name, entry.Id, new Pattern(pattern, options), replacement, guard, entry.Level ?? CurriculumLevel.University, entry.Explain, tags);
    }

    private static bool ContainsBinder(Expr e) => e.Walk().Any(w => w.Expr is Bind);

    // Sort constraints from the declarations. A variable that is a bare operand of a sum or product and occurs nowhere else in the pattern is
    // optional (default 0 or 1). One that occurs twice stays required: its default would differ between a sum and a product, and an omitted
    // operand in one place would have to agree with the binding in the other.
    private static Dictionary<string, WildOptions> Options(Expr pattern, IReadOnlyDictionary<string, Sort> variables)
    {
        var options = variables.ToDictionary(v => v.Key, v => new WildOptions(v.Value));
        var occurrences = pattern.Walk().Where(w => w.Expr is Wild).GroupBy(w => ((Wild)w.Expr).Name).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (node, _) in pattern.Walk())
        {
            if (node is not Apply { Operator: var op } a || a.Arguments.Length < 2 || (op != Operators.Add && op != Operators.Mul)) continue;
            if (a.Arguments.All(arg => arg is Wild)) continue;
            foreach (var arg in a.Arguments)
            {
                if (arg is Wild w && occurrences[w.Name] == 1 && options.TryGetValue(w.Name, out var o)) options[w.Name] = o with { Default = new Number(op == Operators.Add ? Numbers.BigRational.Zero : Numbers.BigRational.One) };
            }
        }
        return options;
    }
}
