using System.Collections.Immutable;

namespace Mathesis.Symbolics;

/// <summary>How many arguments an operator takes.</summary>
/// <param name="Min">The minimum number of arguments.</param>
/// <param name="Max">The maximum, or <c>null</c> for variadic operators.</param>
public readonly record struct Arity(int Min, int? Max)
{
    /// <summary>Exactly <paramref name="n"/> arguments.</summary>
    public static Arity Fixed(int n) => new(n, n);

    /// <summary>At least <paramref name="min"/> arguments.</summary>
    public static Arity Variadic(int min) => new(min, null);

    /// <summary>Between <paramref name="min"/> and <paramref name="max"/> arguments.</summary>
    public static Arity Range(int min, int max) => new(min, max);

    /// <summary>Whether <paramref name="count"/> arguments are allowed.</summary>
    public bool Accepts(int count) => count >= Min && (Max is null || count <= Max);

    /// <inheritdoc />
    public override string ToString() => Max is null ? $"{Min} or more" : Min == Max ? $"{Min}" : $"{Min} to {Max}";
}

/// <summary>Algebraic properties of an operator (docs/design/05-syntax-trees-and-notation.md, "Operators").</summary>
[Flags]
public enum OperatorAttributes
{
    /// <summary>No attributes.</summary>
    None = 0,

    /// <summary>Nested applications flatten: <c>add(a, add(b, c))</c> = <c>add(a, b, c)</c>.</summary>
    Associative = 1 << 0,

    /// <summary>Operands may be sorted (for products, scalar factors only).</summary>
    Commutative = 1 << 1,

    /// <summary><c>f(a, a) = f(a)</c>.</summary>
    Idempotent = 1 << 2,

    /// <summary><c>f(f(a)) = a</c>.</summary>
    Involution = 1 << 3,

    /// <summary>Threads elementwise over matrices, tuples and sets of values.</summary>
    Listable = 1 << 4,

    /// <summary>Linear in the first argument.</summary>
    Linear = 1 << 5,

    /// <summary><c>f(−x) = −f(x)</c>.</summary>
    Odd = 1 << 6,

    /// <summary><c>f(−x) = f(x)</c>.</summary>
    Even = 1 << 7,

    /// <summary>One-to-one on its natural domain.</summary>
    Injective = 1 << 8,

    /// <summary>Arguments are not normalized.</summary>
    HoldArguments = 1 << 9,

    /// <summary>Evaluates to a number when all arguments are numbers.</summary>
    NumericFunction = 1 << 10,
}

/// <summary>The branch of mathematics an operator belongs to; used for the <see cref="Expr.Families"/> mask.</summary>
[Flags]
public enum OperatorFamily
{
    /// <summary>No family.</summary>
    None = 0,

    /// <summary>Arithmetic and elementary number functions.</summary>
    Arithmetic = 1 << 0,

    /// <summary>Complex-number operations.</summary>
    Complex = 1 << 1,

    /// <summary>Exponential and logarithmic functions.</summary>
    ExpLog = 1 << 2,

    /// <summary>Trigonometric functions.</summary>
    Trig = 1 << 3,

    /// <summary>Hyperbolic functions.</summary>
    Hyperbolic = 1 << 4,

    /// <summary>Special functions.</summary>
    Special = 1 << 5,

    /// <summary>Orthogonal polynomials.</summary>
    Orthogonal = 1 << 6,

    /// <summary>Number theory and combinatorics.</summary>
    NumberTheory = 1 << 7,

    /// <summary>Relations.</summary>
    Relation = 1 << 8,

    /// <summary>Logic.</summary>
    Logic = 1 << 9,

    /// <summary>Sets.</summary>
    Set = 1 << 10,

    /// <summary>Functions and composition.</summary>
    Function = 1 << 11,

    /// <summary>Calculus, sums and limits.</summary>
    Calculus = 1 << 12,

    /// <summary>Linear algebra.</summary>
    LinearAlgebra = 1 << 13,

    /// <summary>Probability and statistics.</summary>
    Probability = 1 << 14,
}

/// <summary>How an operator is written in linear text.</summary>
public enum Fixity : byte
{
    /// <summary><c>name(args)</c>.</summary>
    Function,

    /// <summary><c>-a</c>, <c>not p</c>.</summary>
    Prefix,

    /// <summary><c>a + b</c>.</summary>
    Infix,

    /// <summary><c>n!</c>, <c>A^T</c>.</summary>
    Postfix,
}

/// <summary>Binding strengths used by the parser and printers; higher binds tighter.</summary>
public static class Precedence
{
    /// <summary>Atoms, calls and postfix operators.</summary>
    public const int Atom = 100;

    /// <summary><c>^</c>, right associative.</summary>
    public const int Power = 90;

    /// <summary>Prefix <c>-</c> and <c>+</c>.</summary>
    public const int Prefix = 80;

    /// <summary><c>*</c>, <c>/</c>, <c>mod</c> and implicit multiplication.</summary>
    public const int Multiplicative = 70;

    /// <summary><c>+</c> and <c>-</c>.</summary>
    public const int Additive = 60;

    /// <summary><c>∩</c> binds tighter than <c>∪</c> and <c>∖</c>.</summary>
    public const int Intersection = 56;

    /// <summary><c>∪</c>, <c>∖</c>, <c>△</c>.</summary>
    public const int Union = 55;

    /// <summary>Relations such as <c>=</c> and <c>in</c>; chained.</summary>
    public const int Relation = 50;

    /// <summary>Logical <c>not</c>.</summary>
    public const int Not = 45;

    /// <summary><c>and</c>.</summary>
    public const int And = 40;

    /// <summary><c>or</c> and <c>xor</c>.</summary>
    public const int Or = 30;

    /// <summary><c>=&gt;</c>, right associative.</summary>
    public const int Implies = 20;

    /// <summary><c>&lt;=&gt;</c>.</summary>
    public const int Iff = 10;

    /// <summary>Quantifiers and lambda extend as far right as possible.</summary>
    public const int Quantifier = 0;
}

/// <summary>Fixity, precedence and spelling of an operator.</summary>
/// <param name="Fixity">How the operator is written.</param>
/// <param name="Precedence">The binding strength (see <see cref="Symbolics.Precedence"/>); function-style operators use <see cref="Precedence.Atom"/>.</param>
/// <param name="Text">The ASCII or Unicode symbol or name used in linear text.</param>
/// <param name="Latex">The LaTeX macro or symbol (for functions, the macro such as <c>\sin</c>); <c>null</c> to use <c>\operatorname{name}</c>.</param>
/// <param name="RightAssociative">Whether chains group to the right.</param>
public sealed record Notation(Fixity Fixity, int Precedence, string Text, string? Latex = null, bool RightAssociative = false);

/// <summary>
/// The sorts an operator accepts and returns. A rule maps argument sorts to the result sort, or <c>null</c> when the argument
/// sorts do not fit (a sort mismatch, such as adding a 2×3 matrix to a scalar). <see cref="Sort.Any"/> arguments always fit.
/// </summary>
public sealed class Signature
{
    private readonly Func<ImmutableArray<Sort>, Sort?> _rule;

    private Signature(string description, Func<ImmutableArray<Sort>, Sort?> rule)
    {
        Description = description;
        _rule = rule;
    }

    /// <summary>A short description for diagnostics, such as <c>Number → Real</c>.</summary>
    public string Description { get; }

    /// <summary>The result sort for the given argument sorts, or <c>null</c> if they do not fit.</summary>
    public Sort? Result(ImmutableArray<Sort> argumentSorts) => _rule(argumentSorts);

    /// <summary>Creates a signature from a rule.</summary>
    public static Signature Custom(string description, Func<ImmutableArray<Sort>, Sort?> rule) => new(description, rule ?? throw new ArgumentNullException(nameof(rule)));

    /// <summary>Accepts anything and returns <see cref="Sort.Any"/>.</summary>
    public static Signature AnyToAny { get; } = new("Any → Any", _ => Sort.Any);

    /// <summary>Accepts anything and returns <see cref="Sort.Boolean"/> (relations and predicates).</summary>
    public static Signature Predicate { get; } = new("Any → Boolean", _ => Sort.Boolean);

    /// <summary>Accepts Booleans and returns <see cref="Sort.Boolean"/> (logical connectives).</summary>
    public static Signature Logic { get; } = new("Boolean → Boolean", a => a.All(s => s == Sort.Boolean || s == Sort.Any) ? Sort.Boolean : null);

    /// <summary>Accepts numbers and returns <paramref name="result"/>.</summary>
    public static Signature NumbersTo(Sort result) => new($"Number → {result}", a => a.All(s => s.IsNumeric || s == Sort.Any) ? result : null);

    /// <summary>Accepts sets and returns the join of their sorts (set operations).</summary>
    public static Signature SetOperation { get; } = new("Set → Set", a =>
    {
        if (!a.All(s => s is SetSort || s == Sort.Any)) return null;
        var sets = a.OfType<SetSort>().ToList();
        return sets.Count == 0 ? Sort.SetOf(Sort.Any) : sets.Skip(1).Aggregate((Sort)sets[0], (x, y) => Sort.Join(x, y));
    });

    /// <summary>Accepts sets and returns <paramref name="result"/>.</summary>
    public static Signature SetsTo(Sort result) => new($"Set → {result}", a => a.All(s => s is SetSort || s == Sort.Any) ? result : null);

    /// <summary>
    /// An elementary function applied to a number (result computed by <paramref name="scalar"/>), threading over matrices and
    /// vectors entry by entry.
    /// </summary>
    public static Signature Elementary(Func<Sort, Sort> scalar) => new("Number → Number", a =>
    {
        if (a.Length == 0) return null;
        return a[0] switch
        {
            MatrixSort m when m.Element.IsNumeric || m.Element == Sort.Any => Sort.MatrixOf(m.Rows, m.Columns, scalar(m.Element)),
            VectorSort v when v.Element.IsNumeric || v.Element == Sort.Any => Sort.VectorOf(v.Length, scalar(v.Element)),
            var s when s.IsNumeric || s == Sort.Any => scalar(s),
            _ => null,
        };
    });

    /// <summary>The usual real-valued elementary function: real for real input, complex otherwise.</summary>
    public static Signature RealFunction { get; } = Elementary(s => s.IsRealValued || s == Sort.ExtendedReal ? Sort.Real : Sort.Complex);

    /// <summary>The usual complex-valued elementary function: complex for every input.</summary>
    public static Signature ComplexFunction { get; } = Elementary(_ => Sort.Complex);
}

/// <summary>
/// An operator is data: nothing in the expression tree is hard-wired to a specific function. The built-in operators are in
/// <see cref="Operators"/>. Two operators are equal when their <see cref="Id"/>s are equal.
/// </summary>
public sealed class Operator : IEquatable<Operator>
{
    /// <summary>Creates an operator.</summary>
    public Operator(string id, Arity arity, OperatorAttributes attributes, OperatorFamily family, Signature signature, Notation notation, Expr? identity = null, Expr? absorbing = null, string? inverseOf = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(notation);
        Id = id;
        Arity = arity;
        Attributes = attributes;
        Family = family;
        Signature = signature;
        Notation = notation;
        Identity = identity;
        Absorbing = absorbing;
        InverseOf = inverseOf;
    }

    /// <summary>The stable identifier: <c>sin</c>, <c>add</c>, <c>det</c>.</summary>
    public string Id { get; }

    /// <summary>The number of arguments.</summary>
    public Arity Arity { get; }

    /// <summary>The algebraic properties.</summary>
    public OperatorAttributes Attributes { get; }

    /// <summary>The family this operator belongs to.</summary>
    public OperatorFamily Family { get; }

    /// <summary>The sorts in and out.</summary>
    public Signature Signature { get; }

    /// <summary>How the operator is written.</summary>
    public Notation Notation { get; }

    /// <summary>The identity element (0 for add, 1 for mul), if any.</summary>
    public Expr? Identity { get; }

    /// <summary>The absorbing element (0 for mul), if any.</summary>
    public Expr? Absorbing { get; }

    /// <summary>The id of the operator this one inverts (<c>"exp"</c> for <c>ln</c>), if any.</summary>
    public string? InverseOf { get; }

    /// <summary>Whether the operator has the given attribute.</summary>
    public bool Has(OperatorAttributes attribute) => (Attributes & attribute) == attribute;

    /// <inheritdoc />
    public bool Equals(Operator? other) => other is not null && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Operator);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Id;
}
