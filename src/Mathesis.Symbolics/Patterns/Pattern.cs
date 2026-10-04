using System.Collections;
using System.Collections.Immutable;

namespace Mathesis.Symbolics.Patterns;

/// <summary>What a pattern variable (<see cref="Wild"/>) accepts.</summary>
/// <param name="Sort">The sort the matched expression must have (a subsort of it), or <c>null</c> for anything.</param>
/// <param name="Default">
/// The value of an optional wildcard that matched nothing (<c>1</c> for a factor, <c>0</c> for a term, <c>1</c> for an exponent), or
/// <c>null</c> when the wildcard is not optional. Optional wildcards let one rule cover <c>x</c>, <c>3x</c>, <c>x^4</c> and <c>3x^4</c>.
/// </param>
/// <param name="IsSequence">Whether the wildcard binds all the remaining operands of an n-ary operator (<c>rest__</c>).</param>
/// <param name="FreeOf">Symbols the matched expression must not contain (<c>c_</c> free of <c>x</c>).</param>
public sealed record WildOptions(Sort? Sort = null, Expr? Default = null, bool IsSequence = false, ImmutableArray<Symbol> FreeOf = default)
{
    /// <summary>Whether the wildcard is optional.</summary>
    public bool IsOptional => Default is not null;
}

/// <summary>
/// An expression with pattern variables: <see cref="Wild"/> nodes in <see cref="Template"/> stand for any expression the variable's
/// <see cref="WildOptions"/> accept (docs/design/07-engines.md, "Pattern matching").
/// </summary>
public sealed class Pattern
{
    private static readonly WildOptions Anything = new();

    /// <summary>Creates a pattern.</summary>
    /// <param name="template">The expression; its <see cref="Wild"/> nodes are the variables.</param>
    /// <param name="options">Options per wild name; a wild without an entry accepts any expression.</param>
    public Pattern(Expr template, IReadOnlyDictionary<string, WildOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        Template = template;
        Options = options ?? ImmutableDictionary<string, WildOptions>.Empty;
        Names = [.. template.Walk().Where(w => w.Expr is Wild).Select(w => ((Wild)w.Expr).Name).Distinct()];
    }

    /// <summary>The pattern expression.</summary>
    public Expr Template { get; }

    /// <summary>The options of each wild.</summary>
    public IReadOnlyDictionary<string, WildOptions> Options { get; }

    /// <summary>The names of the wilds occurring in the template.</summary>
    public ImmutableArray<string> Names { get; }

    /// <summary>The options of a wild (accepting anything if none were given).</summary>
    public WildOptions OptionsOf(string name) => Options.TryGetValue(name, out var o) ? o : Anything;

    /// <summary>
    /// Turns the declared variables of <paramref name="expr"/> into wilds: each <see cref="Symbol"/> whose name is in
    /// <paramref name="variables"/> becomes a <see cref="Wild"/> that accepts expressions of the variable's sort.
    /// </summary>
    public static Pattern FromVariables(Expr expr, IReadOnlyDictionary<string, Sort> variables)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(variables);
        var template = expr.Transform(e => e is Symbol s && variables.ContainsKey(s.Name) ? new Wild(s.Name) : null);
        return new(template, variables.ToDictionary(v => v.Key, v => new WildOptions(v.Value)));
    }

    /// <inheritdoc />
    public override string ToString() => Template.ToString();
}

/// <summary>The values a match gave to the pattern variables.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Bindings is the name used by docs/design/04-type-system.md and 07-engines.md.")]
public sealed class Bindings : IReadOnlyDictionary<string, Expr>
{
    private readonly ImmutableDictionary<string, Expr> _values;
    private readonly ImmutableDictionary<string, ImmutableArray<Expr>> _sequences;

    private Bindings(ImmutableDictionary<string, Expr> values, ImmutableDictionary<string, ImmutableArray<Expr>> sequences, ImmutableArray<Expr> leftover)
    {
        _values = values;
        _sequences = sequences;
        Leftover = leftover;
    }

    /// <summary>No bindings.</summary>
    public static Bindings Empty { get; } = new(ImmutableDictionary<string, Expr>.Empty, ImmutableDictionary<string, ImmutableArray<Expr>>.Empty, []);

    /// <summary>
    /// Operands of the matched n-ary expression that no pattern argument used. They exist only when the pattern is an associative-commutative
    /// operator applied to arguments that cannot absorb extra operands (a root pattern like <c>sin(x)^2 + cos(x)^2</c> inside a longer sum).
    /// </summary>
    public ImmutableArray<Expr> Leftover { get; }

    /// <inheritdoc />
    public int Count => _values.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _values.Keys;

    /// <inheritdoc />
    public IEnumerable<Expr> Values => _values.Values;

    /// <inheritdoc />
    public Expr this[string key] => _values[key];

    /// <summary>The operands a sequence wild bound.</summary>
    public IReadOnlyDictionary<string, ImmutableArray<Expr>> Sequences => _sequences;

    /// <inheritdoc />
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out Expr value) => _values.TryGetValue(key, out value!);

    /// <summary>Adds a binding.</summary>
    public Bindings With(string name, Expr value) => new(_values.SetItem(name, value), _sequences, Leftover);

    /// <summary>Adds a sequence binding.</summary>
    public Bindings WithSequence(string name, ImmutableArray<Expr> operands) => new(_values, _sequences.SetItem(name, operands), Leftover);

    /// <summary>Records the operands of the matched expression that were not consumed.</summary>
    public Bindings WithLeftover(ImmutableArray<Expr> leftover) => new(_values, _sequences, leftover);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, Expr>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => "{" + string.Join(", ", _values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key} → {v.Value}")) + "}";
}
