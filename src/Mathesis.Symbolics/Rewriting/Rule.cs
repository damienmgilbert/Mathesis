using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Symbolics.Patterns;

namespace Mathesis.Symbolics.Rewriting;

/// <summary>How a rule's guard is decided when the assumptions cannot answer.</summary>
public enum ProvisoMode : byte
{
    /// <summary>Conditions of the form <c>expression ≠ 0</c> that are unknown are added as provisos; every other unknown condition blocks the rule (the default).</summary>
    Default,

    /// <summary>A rule fires only when every condition of its guard is provably true.</summary>
    Strict,

    /// <summary>A rule fires when no condition is provably false; unknown conditions become provisos.</summary>
    Generic,
}

/// <summary>A successful match of a rule at a node.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Replacement">What replaces the matched node (instantiated, not yet normalized).</param>
/// <param name="Bindings">The pattern variables.</param>
/// <param name="Provisos">The conditions the guard could not decide and the mode allowed.</param>
public sealed record RuleMatch(Rule Rule, Expr Replacement, Bindings Bindings, Provisos Provisos);

/// <summary>
/// A rewrite rule: a left-hand pattern, a replacement, and a guard (docs/design/07-engines.md, "Rules, rule sets and indexing"). Rules are
/// derived from catalog entries; the guard is the entry's <c>where</c> condition.
/// </summary>
public sealed class Rule
{
    /// <summary>Creates a rule.</summary>
    /// <param name="name">The rule name: the entry ID, with <c>~rtl</c> appended for a rule read right to left.</param>
    /// <param name="entry">The catalog entry the rule comes from.</param>
    /// <param name="lhs">The pattern to match.</param>
    /// <param name="rhs">The replacement, with the pattern's wilds.</param>
    /// <param name="guard">The condition (with wilds) that must hold, or <c>null</c> for an unconditional rule.</param>
    /// <param name="level">The curriculum level of the entry.</param>
    /// <param name="explain">The explanation template of the entry.</param>
    /// <param name="tags">The rule sets the rule belongs to.</param>
    public Rule(string name, EntryId? entry, Pattern lhs, Expr rhs, Expr? guard, CurriculumLevel level, string? explain, ImmutableArray<string> tags)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(lhs);
        ArgumentNullException.ThrowIfNull(rhs);
        Name = name;
        Entry = entry;
        Lhs = lhs;
        Rhs = rhs;
        Guard = guard;
        Level = level;
        ExplainTemplate = explain;
        Tags = tags;
        Head = lhs.Template is Apply a ? a.Operator : null;
    }

    /// <summary>The rule name.</summary>
    public string Name { get; }

    /// <summary>The catalog entry.</summary>
    public EntryId? Entry { get; }

    /// <summary>The pattern.</summary>
    public Pattern Lhs { get; }

    /// <summary>The replacement.</summary>
    public Expr Rhs { get; }

    /// <summary>The guard, or <c>null</c>.</summary>
    public Expr? Guard { get; }

    /// <summary>The curriculum level of the entry.</summary>
    public CurriculumLevel Level { get; }

    /// <summary>The explanation template.</summary>
    public string? ExplainTemplate { get; }

    /// <summary>The rule sets this rule belongs to.</summary>
    public ImmutableArray<string> Tags { get; }

    /// <summary>The head operator of the pattern, or <c>null</c> if the pattern is not an application.</summary>
    public Operator? Head { get; }

    /// <summary>The matches of the rule at <paramref name="subject"/> whose guard holds.</summary>
    public IEnumerable<RuleMatch> Matches(Expr subject, MathContext context, ProvisoMode mode = ProvisoMode.Default, MatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(context);
        foreach (var b in Matcher.Match(Lhs, subject, options))
        {
            if (!CheckGuard(b, context, mode, out var provisos)) continue;
            var replacement = Matcher.Instantiate(Rhs, b, Lhs);
            if (!b.Leftover.IsDefaultOrEmpty && Head is { } head) replacement = new Apply(head, [replacement, .. b.Leftover]);
            yield return new(this, replacement, b, provisos);
        }
    }

    /// <summary>Decides the guard under <paramref name="bindings"/>: <c>true</c> if the rule may fire, with the provisos it adds.</summary>
    public bool CheckGuard(Bindings bindings, MathContext context, ProvisoMode mode, out Provisos provisos)
    {
        provisos = Provisos.None;
        if (Guard is null) return true;
        var instantiated = Matcher.Instantiate(Guard, bindings, Lhs);
        foreach (var conjunct in Conjuncts(instantiated))
        {
            switch (context.Ask(conjunct))
            {
                case Truth.True:
                    break;
                case Truth.False:
                    return false;
                default:
                    if (mode == ProvisoMode.Generic || (mode == ProvisoMode.Default && IsNonZeroCondition(conjunct))) provisos = provisos.Add(conjunct);
                    else return false;
                    break;
            }
        }
        return true;
    }

    private static IEnumerable<Expr> Conjuncts(Expr e)
    {
        if (e is Apply a && a.Operator == Operators.And)
        {
            foreach (var arg in a.Arguments)
            {
                foreach (var c in Conjuncts(arg)) yield return c;
            }
        }
        else
        {
            yield return e;
        }
    }

    private static bool IsNonZeroCondition(Expr e) =>
        e is Apply { Operator: var op, Arguments: [_, Number { Value.Sign: 0 }] } && op == Operators.Ne;

    /// <inheritdoc />
    public override string ToString() => $"{Name}: {Lhs} → {Rhs}";
}

/// <summary>A named set of rules, indexed for retrieval.</summary>
public sealed class RuleSet
{
    private readonly Lazy<RuleIndex> _index;

    /// <summary>Creates a rule set.</summary>
    public RuleSet(string name, IEnumerable<Rule> rules)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(rules);
        Name = name;
        Rules = [.. rules];
        _index = new(() => new RuleIndex(Rules));
    }

    /// <summary>The name of the set (a catalog tag).</summary>
    public string Name { get; }

    /// <summary>The rules, in order.</summary>
    public ImmutableArray<Rule> Rules { get; }

    /// <summary>The index over the rules.</summary>
    public RuleIndex Index => _index.Value;

    /// <summary>The union of several sets under a new name.</summary>
    public static RuleSet Union(string name, params RuleSet[] sets) => new(name, sets.SelectMany(s => s.Rules).DistinctBy(r => r.Name));

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Rules.Length} rules)";
}

/// <summary>
/// Retrieves the rules that can match a node: first by the head operator of the pattern, then by the heads of the pattern's immediate
/// arguments (a discrimination tree of depth two), so that with thousands of rules a node consults a handful.
/// </summary>
public sealed class RuleIndex
{
    private readonly Dictionary<Operator, List<(Rule Rule, ImmutableArray<string> Keys)>> _byHead = [];
    private readonly Dictionary<Rule, int> _order = [];
    private readonly List<Rule> _optional = [];
    private readonly List<Rule> _others = [];

    /// <summary>Indexes <paramref name="rules"/>.</summary>
    public RuleIndex(IEnumerable<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            _order[rule] = _order.Count;
            if (rule.Head is not { } head || rule.Lhs.Template is not Apply pattern)
            {
                _others.Add(rule);
                continue;
            }

            // A pattern with an optional operand (a_.*x) can match a subject with another head (x), so it is always a candidate.
            if (rule.Lhs.Options.Values.Any(o => o.IsOptional))
            {
                _optional.Add(rule);
                continue;
            }
            if (!_byHead.TryGetValue(head, out var list)) _byHead[head] = list = [];
            list.Add((rule, [.. pattern.Arguments.Select(Key).Where(k => k is not null).Select(k => k!)]));
        }
    }

    /// <summary>The number of indexed rules.</summary>
    public int Count => _byHead.Values.Sum(l => l.Count) + _optional.Count + _others.Count;

    // The head of a non-wild argument: an operator ID, a number, a symbol or a constant.
    private static string? Key(Expr e) => e switch
    {
        Wild => null,
        Apply a => "op:" + a.Operator.Id,
        Number n => "num:" + n.Value,
        Symbol s => "sym:" + s.Name,
        Constant c => "const:" + c.Id,
        _ => null,
    };

    /// <summary>The rules whose pattern could match <paramref name="subject"/> at its root, in the order they were added.</summary>
    public IEnumerable<Rule> Candidates(Expr subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var found = new List<Rule>();
        if (subject is Apply a && _byHead.TryGetValue(a.Operator, out var list))
        {
            var keys = new HashSet<string>(a.Arguments.Select(Key).Where(k => k is not null)!);
            foreach (var (rule, required) in list)
            {
                if (required.All(keys.Contains)) found.Add(rule);
            }
        }
        found.AddRange(_optional);
        found.AddRange(_others);
        return found.OrderBy(r => _order[r]);
    }
}
