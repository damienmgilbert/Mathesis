using System.Collections.Immutable;
using System.Numerics;

namespace Mathesis.Symbolics;

/// <summary>
/// A mathematical type of an expression (docs/design/04-type-system.md, "Sorts"): ℕ ⊂ ℤ ⊂ ℚ ⊂ ℝ ⊂ ℂ, Boolean, sets, tuples,
/// vectors, matrices, functions and so on. Sorts are values: equal sorts compare equal and print as <c>Matrix(2, 3, Real)</c>.
/// </summary>
public abstract record Sort
{
    private protected Sort()
    {
    }

    /// <summary>The top sort: every expression has it.</summary>
    public static Sort Any { get; } = new SimpleSort("Any");

    /// <summary>Propositions.</summary>
    public static Sort Boolean { get; } = new SimpleSort("Boolean");

    /// <summary>The parent of all number sorts.</summary>
    public static Sort Number { get; } = new SimpleSort("Number");

    /// <summary>ℂ, the complex numbers.</summary>
    public static Sort Complex { get; } = new SimpleSort("Complex");

    /// <summary>ℝ, the real numbers; the default sort of a symbol in real mode.</summary>
    public static Sort Real { get; } = new SimpleSort("Real");

    /// <summary>ℝ ∪ {−∞, +∞}.</summary>
    public static Sort ExtendedReal { get; } = new SimpleSort("ExtendedReal");

    /// <summary>𝔸, the algebraic numbers (ℚ ⊂ 𝔸 ⊂ ℂ).</summary>
    public static Sort Algebraic { get; } = new SimpleSort("Algebraic");

    /// <summary>ℚ, the rational numbers.</summary>
    public static Sort Rational { get; } = new SimpleSort("Rational");

    /// <summary>ℤ, the integers.</summary>
    public static Sort Integer { get; } = new SimpleSort("Integer");

    /// <summary>ℕ, the natural numbers (0 ∈ ℕ).</summary>
    public static Sort Natural { get; } = new SimpleSort("Natural");

    /// <summary>The sort of sets whose elements have sort <paramref name="element"/>.</summary>
    public static Sort SetOf(Sort element) => new SetSort(element ?? throw new ArgumentNullException(nameof(element)));

    /// <summary>The sort of tuples with the given component sorts.</summary>
    public static Sort TupleOf(params ReadOnlySpan<Sort> components) => new TupleSort([.. components]);

    /// <summary>The sort of vectors of the given length (a number or a symbol) over <paramref name="element"/>.</summary>
    public static Sort VectorOf(Expr length, Sort element) => new VectorSort(length ?? throw new ArgumentNullException(nameof(length)), element ?? throw new ArgumentNullException(nameof(element)));

    /// <summary>The sort of m×n matrices (dimensions are numbers or symbols) over <paramref name="element"/>.</summary>
    public static Sort MatrixOf(Expr rows, Expr columns, Sort element) =>
        new MatrixSort(rows ?? throw new ArgumentNullException(nameof(rows)), columns ?? throw new ArgumentNullException(nameof(columns)), element ?? throw new ArgumentNullException(nameof(element)));

    /// <summary>The sort of m×n matrices with literal dimensions.</summary>
    public static Sort MatrixOf(int rows, int columns, Sort element) => MatrixOf(Sym.Number(rows), Sym.Number(columns), element);

    /// <summary>The sort of functions from <paramref name="domain"/> to <paramref name="codomain"/>.</summary>
    public static Sort FunctionOf(Sort domain, Sort codomain) =>
        new FunctionSort(domain ?? throw new ArgumentNullException(nameof(domain)), codomain ?? throw new ArgumentNullException(nameof(codomain)));

    /// <summary>A sequence: a function from ℕ to <paramref name="element"/>.</summary>
    public static Sort SequenceOf(Sort element) => FunctionOf(Natural, element);

    /// <summary>The residue classes ℤ/nℤ.</summary>
    public static Sort ResidueOf(BigInteger modulus) => new ResidueSort(modulus);

    /// <summary>Random variables with values of sort <paramref name="element"/>.</summary>
    public static Sort RandomVariableOf(Sort element) => new RandomVariableSort(element ?? throw new ArgumentNullException(nameof(element)));

    /// <summary>Whether this is a number sort (ℕ … ℂ, extended reals, residues, or <see cref="Number"/>).</summary>
    public bool IsNumeric => this is SimpleSort s && NumberRank(s) >= 0 || this is ResidueSort;

    /// <summary>Whether this sort is contained in the reals (ℕ, ℤ, ℚ, ℝ, 𝔸 does not count: it may be complex).</summary>
    public bool IsRealValued => this is SimpleSort s && (s == Natural || s == Integer || s == Rational || s == Real);

    /// <summary>Whether this is a matrix, vector or tuple sort (a non-scalar, non-commuting factor in products).</summary>
    public bool IsAggregate => this is MatrixSort or VectorSort or TupleSort;

    // Position in the scalar chain; -1 for non-numbers.
    private static int NumberRank(SimpleSort s) =>
        s.Name switch { "Natural" => 0, "Integer" => 1, "Rational" => 2, "Real" => 3, "Algebraic" => 3, "ExtendedReal" => 4, "Complex" => 5, "Number" => 6, _ => -1 };

    /// <summary>
    /// Whether every value of this sort is also a value of <paramref name="other"/>: ℕ ⊂ ℤ ⊂ ℚ ⊂ ℝ ⊂ ℂ, ℚ ⊂ 𝔸 ⊂ ℂ, ℝ ⊂ ℝ̄,
    /// and covariantly for sets, tuples, vectors and matrices.
    /// </summary>
    public bool IsSubsortOf(Sort other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Equals(other) || other == Any) return true;
        if (this is SimpleSort a && other is SimpleSort b)
        {
            return (a.Name, b.Name) switch
            {
                ("Natural", "Integer" or "Rational" or "Real" or "Algebraic" or "ExtendedReal" or "Complex" or "Number") => true,
                ("Integer", "Rational" or "Real" or "Algebraic" or "ExtendedReal" or "Complex" or "Number") => true,
                ("Rational", "Real" or "Algebraic" or "ExtendedReal" or "Complex" or "Number") => true,
                ("Algebraic", "Complex" or "Number") => true,
                ("Real", "ExtendedReal" or "Complex" or "Number") => true,
                ("ExtendedReal" or "Complex", "Number") => true,
                _ => false,
            };
        }
        if (this is ResidueSort && other is SimpleSort { Name: "Number" }) return true;
        return (this, other) switch
        {
            (SetSort x, SetSort y) => x.Element.IsSubsortOf(y.Element),
            (RandomVariableSort x, RandomVariableSort y) => x.Element.IsSubsortOf(y.Element),
            (VectorSort x, VectorSort y) => x.Length.Equals(y.Length) && x.Element.IsSubsortOf(y.Element),
            (MatrixSort x, MatrixSort y) => x.Rows.Equals(y.Rows) && x.Columns.Equals(y.Columns) && x.Element.IsSubsortOf(y.Element),
            (TupleSort x, TupleSort y) => x.Components.Length == y.Components.Length && x.Components.Zip(y.Components, (p, q) => p.IsSubsortOf(q)).All(t => t),
            (FunctionSort x, FunctionSort y) => x.Domain.Equals(y.Domain) && x.Codomain.IsSubsortOf(y.Codomain),
            _ => false,
        };
    }

    /// <summary>The smallest sort containing both sorts; <see cref="Any"/> when they have nothing in common.</summary>
    public static Sort Join(Sort a, Sort b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.IsSubsortOf(b)) return b;
        if (b.IsSubsortOf(a)) return a;
        switch (a, b)
        {
            case (SimpleSort x, SimpleSort y) when NumberRank(x) >= 0 && NumberRank(y) >= 0:
                // Incomparable numbers: ℝ and 𝔸 join in ℂ; ExtendedReal with ℂ-ish joins in Number.
                return x.Name is "Algebraic" || y.Name is "Algebraic"
                    ? (NumberRank(x) <= 3 && NumberRank(y) <= 3 ? Complex : Number)
                    : Number;
            case (SetSort x, SetSort y):
                return SetOf(Join(x.Element, y.Element));
            case (RandomVariableSort x, RandomVariableSort y):
                return RandomVariableOf(Join(x.Element, y.Element));
            case (VectorSort x, VectorSort y) when x.Length.Equals(y.Length):
                return VectorOf(x.Length, Join(x.Element, y.Element));
            case (MatrixSort x, MatrixSort y) when x.Rows.Equals(y.Rows) && x.Columns.Equals(y.Columns):
                return MatrixOf(x.Rows, x.Columns, Join(x.Element, y.Element));
            case (TupleSort x, TupleSort y) when x.Components.Length == y.Components.Length:
                return new TupleSort([.. x.Components.Zip(y.Components, Join)]);
            case (ResidueSort, SimpleSort other) when NumberRank(other) >= 0:
                return Number;
            case (SimpleSort other, ResidueSort) when NumberRank(other) >= 0:
                return Number;
            case (ResidueSort, ResidueSort):
                return Number;
            default:
                return Any;
        }
    }

    /// <summary>The element sort for scalars: the sort itself; for vectors and matrices the entry sort.</summary>
    public Sort ElementSort => this switch { VectorSort v => v.Element, MatrixSort m => m.Element, SetSort s => s.Element, _ => this };

    /// <inheritdoc />
    public sealed override string ToString() => this switch
    {
        SimpleSort s => s.Name,
        ResidueSort r => $"Residue({r.Modulus})",
        SetSort s => $"Set({s.Element})",
        TupleSort t => $"Tuple({string.Join(", ", t.Components)})",
        VectorSort v => $"Vector({v.Length}, {v.Element})",
        MatrixSort m => $"Matrix({m.Rows}, {m.Columns}, {m.Element})",
        FunctionSort f => $"Function({f.Domain} -> {f.Codomain})",
        RandomVariableSort r => $"RandomVariable({r.Element})",
        _ => base.ToString() ?? string.Empty,
    };
}

/// <summary>A sort without parameters: <c>Any</c>, <c>Boolean</c>, <c>Real</c>, <c>Integer</c> and so on.</summary>
/// <param name="Name">The sort's name.</param>
public sealed record SimpleSort(string Name) : Sort;

/// <summary>ℤ/nℤ.</summary>
/// <param name="Modulus">The modulus n.</param>
public sealed record ResidueSort(BigInteger Modulus) : Sort;

/// <summary>Sets whose elements all have a given sort.</summary>
/// <param name="Element">The element sort.</param>
public sealed record SetSort(Sort Element) : Sort;

/// <summary>Random variables.</summary>
/// <param name="Element">The sort of the values.</param>
public sealed record RandomVariableSort(Sort Element) : Sort;

/// <summary>Functions between sorts.</summary>
/// <param name="Domain">The argument sort.</param>
/// <param name="Codomain">The result sort.</param>
public sealed record FunctionSort(Sort Domain, Sort Codomain) : Sort;

/// <summary>Vectors of a given length over a scalar sort.</summary>
/// <param name="Length">The length: a number or a symbol.</param>
/// <param name="Element">The entry sort.</param>
public sealed record VectorSort(Expr Length, Sort Element) : Sort;

/// <summary>Matrices of given dimensions over a scalar sort.</summary>
/// <param name="Rows">The number of rows: a number or a symbol.</param>
/// <param name="Columns">The number of columns: a number or a symbol.</param>
/// <param name="Element">The entry sort.</param>
public sealed record MatrixSort(Expr Rows, Expr Columns, Sort Element) : Sort;

/// <summary>Tuples with a fixed list of component sorts.</summary>
public sealed record TupleSort : Sort
{
    /// <summary>Creates a tuple sort.</summary>
    public TupleSort(ImmutableArray<Sort> components) => Components = components;

    /// <summary>The component sorts.</summary>
    public ImmutableArray<Sort> Components { get; }

    /// <inheritdoc />
    public bool Equals(TupleSort? other) => other is not null && Components.AsSpan().SequenceEqual(other.Components.AsSpan());

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var c in Components) hash.Add(c);
        return hash.ToHashCode();
    }
}
