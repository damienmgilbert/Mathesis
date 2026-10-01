using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace Mathesis.Polynomials;

/// <summary>The order in which monomials of a multivariate polynomial are compared.</summary>
public enum MonomialOrder : byte
{
    /// <summary>Lexicographic: compare exponents of x₁ first, then x₂, ….</summary>
    Lex,

    /// <summary>Graded lexicographic: compare total degree first, then lexicographically.</summary>
    GrLex,

    /// <summary>Graded reverse lexicographic: compare total degree, then the last differing exponent in reverse.</summary>
    GrevLex,
}

/// <summary>A product of powers of variables x₀<sup>e₀</sup>·x₁<sup>e₁</sup>·… with non-negative exponents.</summary>
public readonly struct Monomial : IEquatable<Monomial>
{
    private readonly ImmutableArray<int> _exponents;

    /// <summary>Creates a monomial from its exponent vector.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An exponent is negative.</exception>
    public Monomial(IEnumerable<int> exponents)
    {
        ArgumentNullException.ThrowIfNull(exponents);
        var e = exponents.ToImmutableArray();
        foreach (var x in e) ArgumentOutOfRangeException.ThrowIfNegative(x, nameof(exponents));
        _exponents = e;
    }

    /// <summary>The exponents, one per variable.</summary>
    public ImmutableArray<int> Exponents => _exponents.IsDefault ? [] : _exponents;

    /// <summary>The sum of the exponents.</summary>
    public int TotalDegree => Exponents.Sum();

    /// <summary>The number of variables this monomial is written over.</summary>
    public int VariableCount => Exponents.Length;

    /// <summary>The product of two monomials over the same variables.</summary>
    public static Monomial operator *(Monomial left, Monomial right)
    {
        if (left.VariableCount != right.VariableCount) throw new ArgumentException("Monomials must use the same variables.");
        return new(left.Exponents.Zip(right.Exponents, (a, b) => a + b));
    }

    /// <summary>Compares two monomials in the given order; the result is positive when <paramref name="x"/> is larger.</summary>
    public static int Compare(Monomial x, Monomial y, MonomialOrder order)
    {
        var a = x.Exponents;
        var b = y.Exponents;
        if (a.Length != b.Length) throw new ArgumentException("Monomials must use the same variables.");
        if (order != MonomialOrder.Lex)
        {
            var byDegree = x.TotalDegree.CompareTo(y.TotalDegree);
            if (byDegree != 0) return byDegree;
        }
        if (order == MonomialOrder.GrevLex)
        {
            for (var i = a.Length - 1; i >= 0; i--)
            {
                if (a[i] != b[i]) return b[i].CompareTo(a[i]);
            }
            return 0;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        }
        return 0;
    }

    /// <inheritdoc />
    public bool Equals(Monomial other) => Exponents.AsSpan().SequenceEqual(other.Exponents.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Monomial other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var e in Exponents) hash.Add(e);
        return hash.ToHashCode();
    }

    /// <summary>Structural equality.</summary>
    public static bool operator ==(Monomial left, Monomial right) => left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(Monomial left, Monomial right) => !left.Equals(right);

    /// <summary>Formats as <c>x0^2*x1</c>; the constant monomial is <c>1</c>.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Exponents.Length; i++)
        {
            if (Exponents[i] == 0) continue;
            if (sb.Length > 0) sb.Append('*');
            sb.Append('x').Append(i);
            if (Exponents[i] > 1) sb.Append('^').Append(Exponents[i]);
        }
        return sb.Length == 0 ? "1" : sb.ToString();
    }
}

/// <summary>
/// A sparse multivariate polynomial over a commutative ring: a finite sum of coefficient·<see cref="Monomial"/> terms kept in
/// decreasing <see cref="MonomialOrder"/>, with no zero coefficients.
/// </summary>
/// <typeparam name="T">The coefficient type (ring operators only, so <see cref="BigInteger"/> works).</typeparam>
/// <remarks>
/// Phase 3 provides arithmetic, evaluation, partial derivatives and conversion from a univariate polynomial. Division with
/// remainder, gcd, content and Gröbner bases arrive in later milestones.
/// </remarks>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Constant and Variable are the natural constructors of SparsePolynomial<T> (docs/design/04-type-system.md).")]
public sealed class SparsePolynomial<T>
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
        IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
{
    private readonly ImmutableArray<(Monomial Monomial, T Coefficient)> _terms;

    /// <summary>Creates a polynomial in <paramref name="variableCount"/> variables from terms; like terms are combined.</summary>
    public SparsePolynomial(int variableCount, MonomialOrder order, IEnumerable<(Monomial Monomial, T Coefficient)> terms)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variableCount);
        ArgumentNullException.ThrowIfNull(terms);
        VariableCount = variableCount;
        Order = order;
        var sums = new Dictionary<Monomial, T>();
        foreach (var (monomial, coefficient) in terms)
        {
            if (monomial.VariableCount != variableCount) throw new ArgumentException("A monomial has the wrong number of variables.", nameof(terms));
            sums[monomial] = sums.TryGetValue(monomial, out var existing) ? existing + coefficient : coefficient;
        }
        var list = sums.Where(kv => kv.Value != T.AdditiveIdentity).Select(kv => (kv.Key, kv.Value)).ToList();
        list.Sort((a, b) => Monomial.Compare(b.Key, a.Key, order));
        _terms = [.. list];
    }

    /// <summary>The number of variables.</summary>
    public int VariableCount { get; }

    /// <summary>The monomial order used to sort the terms.</summary>
    public MonomialOrder Order { get; }

    /// <summary>The terms in decreasing monomial order.</summary>
    public ImmutableArray<(Monomial Monomial, T Coefficient)> Terms => _terms;

    /// <summary>Whether this is the zero polynomial.</summary>
    public bool IsZero => _terms.Length == 0;

    /// <summary>The largest total degree of any term, or −1 for zero.</summary>
    public int TotalDegree => IsZero ? -1 : _terms.Max(t => t.Monomial.TotalDegree);

    /// <summary>The leading term in this polynomial's monomial order.</summary>
    /// <exception cref="InvalidOperationException">The polynomial is zero.</exception>
    public (Monomial Monomial, T Coefficient) LeadingTerm => IsZero ? throw new InvalidOperationException("The zero polynomial has no leading term.") : _terms[0];

    /// <summary>The zero polynomial in <paramref name="variableCount"/> variables.</summary>
    public static SparsePolynomial<T> Zero(int variableCount, MonomialOrder order = MonomialOrder.GrevLex) => new(variableCount, order, []);

    /// <summary>The constant polynomial <paramref name="value"/>.</summary>
    public static SparsePolynomial<T> Constant(T value, int variableCount, MonomialOrder order = MonomialOrder.GrevLex) =>
        new(variableCount, order, [(new Monomial(new int[variableCount]), value)]);

    /// <summary>The variable x<sub>index</sub>.</summary>
    public static SparsePolynomial<T> Variable(int index, int variableCount, MonomialOrder order = MonomialOrder.GrevLex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, variableCount);
        var e = new int[variableCount];
        e[index] = 1;
        return new(variableCount, order, [(new Monomial(e), T.MultiplicativeIdentity)]);
    }

    private SparsePolynomial<T> Like(IEnumerable<(Monomial, T)> terms) => new(VariableCount, Order, terms);

    private void RequireSameRing(SparsePolynomial<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.VariableCount != VariableCount) throw new ArgumentException("The polynomials use different numbers of variables.");
    }

    /// <summary>Adds two polynomials in the same variables.</summary>
    public static SparsePolynomial<T> operator +(SparsePolynomial<T> left, SparsePolynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameRing(right);
        return left.Like(left._terms.Concat(right._terms));
    }

    /// <summary>Subtracts two polynomials in the same variables.</summary>
    public static SparsePolynomial<T> operator -(SparsePolynomial<T> left, SparsePolynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameRing(right);
        return left.Like(left._terms.Concat(right._terms.Select(t => (t.Monomial, -t.Coefficient))));
    }

    /// <summary>Negates a polynomial.</summary>
    public static SparsePolynomial<T> operator -(SparsePolynomial<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Like(value._terms.Select(t => (t.Monomial, -t.Coefficient)));
    }

    /// <summary>Multiplies two polynomials in the same variables.</summary>
    public static SparsePolynomial<T> operator *(SparsePolynomial<T> left, SparsePolynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameRing(right);
        var products = new List<(Monomial, T)>(left._terms.Length * right._terms.Length);
        foreach (var (m1, c1) in left._terms)
        {
            foreach (var (m2, c2) in right._terms) products.Add((m1 * m2, c1 * c2));
        }
        return left.Like(products);
    }

    /// <summary>Multiplies by a scalar.</summary>
    public static SparsePolynomial<T> operator *(SparsePolynomial<T> polynomial, T scalar)
    {
        ArgumentNullException.ThrowIfNull(polynomial);
        return polynomial.Like(polynomial._terms.Select(t => (t.Monomial, t.Coefficient * scalar)));
    }

    /// <summary>Raises the polynomial to a non-negative integer power.</summary>
    public SparsePolynomial<T> Pow(int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        var result = Constant(T.MultiplicativeIdentity, VariableCount, Order);
        var square = this;
        while (exponent > 0)
        {
            if ((exponent & 1) == 1) result *= square;
            exponent >>= 1;
            if (exponent > 0) square *= square;
        }
        return result;
    }

    /// <summary>Evaluates the polynomial at a point.</summary>
    /// <exception cref="ArgumentException">The point has the wrong dimension.</exception>
    public T Evaluate(ReadOnlySpan<T> point)
    {
        if (point.Length != VariableCount) throw new ArgumentException("The point has the wrong dimension.", nameof(point));
        var total = T.AdditiveIdentity;
        foreach (var (monomial, coefficient) in _terms)
        {
            var value = coefficient;
            for (var i = 0; i < VariableCount; i++)
            {
                for (var k = 0; k < monomial.Exponents[i]; k++) value *= point[i];
            }
            total += value;
        }
        return total;
    }

    /// <summary>The partial derivative with respect to variable <paramref name="variable"/>.</summary>
    public SparsePolynomial<T> Derivative(int variable)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variable);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(variable, VariableCount);
        var terms = new List<(Monomial, T)>();
        foreach (var (monomial, coefficient) in _terms)
        {
            var power = monomial.Exponents[variable];
            if (power == 0) continue;
            var factor = T.AdditiveIdentity;
            for (var k = 0; k < power; k++) factor += T.MultiplicativeIdentity;
            var exponents = monomial.Exponents.ToArray();
            exponents[variable]--;
            terms.Add((new Monomial(exponents), coefficient * factor));
        }
        return Like(terms);
    }

    /// <summary>Whether two polynomials have the same terms (the order setting is ignored).</summary>
    public bool Equals(SparsePolynomial<T>? other)
    {
        if (other is null || other.VariableCount != VariableCount || other._terms.Length != _terms.Length) return false;
        var lookup = other._terms.ToDictionary(t => t.Monomial, t => t.Coefficient);
        return _terms.All(t => lookup.TryGetValue(t.Monomial, out var c) && c == t.Coefficient);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SparsePolynomial<T>);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(VariableCount, _terms.Length, _terms.Length == 0 ? 0 : _terms[0].Monomial.GetHashCode());

    /// <summary>Formats the terms in decreasing order, for diagnostics.</summary>
    public override string ToString() =>
        IsZero ? "0" : string.Join(" + ", _terms.Select(t => t.Monomial.ToString() == "1" ? $"{t.Coefficient}" : $"({t.Coefficient})*{t.Monomial}"));
}
