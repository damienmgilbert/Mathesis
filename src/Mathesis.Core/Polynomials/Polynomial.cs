using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Mathesis.Polynomials;

/// <summary>
/// A dense univariate polynomial c₀ + c₁x + … + cₙxⁿ over a field, stored with ascending coefficients and always
/// normalized (no trailing zero coefficients; the zero polynomial has degree −1).
/// </summary>
/// <typeparam name="T">
/// The coefficient type: any type with the field operator interfaces, such as <see cref="double"/>,
/// <see cref="Numbers.BigRational"/>, <see cref="Numbers.Complex{T}"/> or (later) <c>Expr</c>. Integer polynomials are
/// represented over <see cref="Numbers.BigRational"/>.
/// </typeparam>
/// <remarks>
/// Whether a coefficient is zero is decided by an optional zero test (default: equality with <c>T.AdditiveIdentity</c>), which
/// is carried through every operation so that symbolic coefficients can use a simplifying test. Division with remainder, the
/// gcd and the square-free decomposition assume characteristic 0 and exact arithmetic; over <see cref="double"/> they are
/// numerically unstable and meant for exploration only.
/// </remarks>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Zero, One and X are the natural constants of Polynomial<T> (docs/design/04-type-system.md).")]
public sealed class Polynomial<T> : IEquatable<Polynomial<T>>, IReadOnlyList<T>
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
        IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
{
    private readonly ImmutableArray<T> _coefficients;
    private readonly Func<T, bool> _isZero;

    /// <summary>Creates a polynomial from ascending coefficients; trailing zeros are removed.</summary>
    /// <param name="ascendingCoefficients">c₀, c₁, …, cₙ.</param>
    /// <param name="isZero">The zero test for coefficients; <c>null</c> compares with <c>T.AdditiveIdentity</c>.</param>
    public Polynomial(IEnumerable<T> ascendingCoefficients, Func<T, bool>? isZero = null)
    {
        ArgumentNullException.ThrowIfNull(ascendingCoefficients);
        _isZero = isZero ?? (static x => x == T.AdditiveIdentity);
        var builder = ascendingCoefficients.ToList();
        while (builder.Count > 0 && _isZero(builder[^1])) builder.RemoveAt(builder.Count - 1);
        _coefficients = [.. builder];
    }

    /// <summary>The zero polynomial.</summary>
    public static Polynomial<T> Zero => new([]);

    /// <summary>The constant polynomial 1.</summary>
    public static Polynomial<T> One => new([T.MultiplicativeIdentity]);

    /// <summary>The polynomial x.</summary>
    public static Polynomial<T> X => new([T.AdditiveIdentity, T.MultiplicativeIdentity]);

    /// <summary>The monomial <paramref name="coefficient"/>·x<sup><paramref name="degree"/></sup>.</summary>
    public static Polynomial<T> Monomial(T coefficient, int degree)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        var c = new T[degree + 1];
        Array.Fill(c, T.AdditiveIdentity);
        c[degree] = coefficient;
        return new(c);
    }

    /// <summary>Builds the polynomial from a univariate sparse polynomial.</summary>
    /// <exception cref="ArgumentException">The sparse polynomial has more than one variable.</exception>
    public static Polynomial<T> FromSparse(SparsePolynomial<T> sparse)
    {
        ArgumentNullException.ThrowIfNull(sparse);
        if (sparse.VariableCount != 1) throw new ArgumentException("Only univariate sparse polynomials convert to a dense polynomial.", nameof(sparse));
        var c = new T[sparse.TotalDegree + 1];
        Array.Fill(c, T.AdditiveIdentity);
        foreach (var (monomial, coefficient) in sparse.Terms) c[monomial.Exponents[0]] = coefficient;
        return new(c);
    }

    /// <summary>The degree, or −1 for the zero polynomial.</summary>
    public int Degree => _coefficients.Length - 1;

    /// <summary>Whether this is the zero polynomial.</summary>
    public bool IsZero => _coefficients.Length == 0;

    /// <summary>The ascending coefficients c₀ … cₙ (empty for zero).</summary>
    public ImmutableArray<T> Coefficients => _coefficients;

    /// <summary>The leading coefficient.</summary>
    /// <exception cref="InvalidOperationException">The polynomial is zero.</exception>
    public T LeadingCoefficient => IsZero ? throw new InvalidOperationException("The zero polynomial has no leading coefficient.") : _coefficients[^1];

    /// <summary>The coefficient of x<sup>i</sup>; zero beyond the degree.</summary>
    public T this[int i] => (uint)i < (uint)_coefficients.Length ? _coefficients[i] : T.AdditiveIdentity;

    int IReadOnlyCollection<T>.Count => _coefficients.Length;

    /// <summary>The zero test used for coefficients.</summary>
    public Func<T, bool> ZeroTest => _isZero;

    private Polynomial<T> With(IEnumerable<T> coefficients) => new(coefficients, _isZero);

    // ----- Arithmetic -----

    /// <summary>Adds two polynomials.</summary>
    public static Polynomial<T> operator +(Polynomial<T> left, Polynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var n = Math.Max(left._coefficients.Length, right._coefficients.Length);
        var c = new T[n];
        for (var i = 0; i < n; i++) c[i] = left[i] + right[i];
        return left.With(c);
    }

    /// <summary>Subtracts two polynomials.</summary>
    public static Polynomial<T> operator -(Polynomial<T> left, Polynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var n = Math.Max(left._coefficients.Length, right._coefficients.Length);
        var c = new T[n];
        for (var i = 0; i < n; i++) c[i] = left[i] - right[i];
        return left.With(c);
    }

    /// <summary>Negates a polynomial.</summary>
    public static Polynomial<T> operator -(Polynomial<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.With(value._coefficients.Select(x => -x));
    }

    /// <summary>Multiplies two polynomials (schoolbook, O(nm)).</summary>
    public static Polynomial<T> operator *(Polynomial<T> left, Polynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.IsZero || right.IsZero) return new([], left._isZero);
        var c = new T[left._coefficients.Length + right._coefficients.Length - 1];
        Array.Fill(c, T.AdditiveIdentity);
        for (var i = 0; i < left._coefficients.Length; i++)
        {
            for (var j = 0; j < right._coefficients.Length; j++) c[i + j] += left._coefficients[i] * right._coefficients[j];
        }
        return left.With(c);
    }

    /// <summary>Multiplies by a scalar.</summary>
    public static Polynomial<T> operator *(Polynomial<T> polynomial, T scalar)
    {
        ArgumentNullException.ThrowIfNull(polynomial);
        return polynomial.With(polynomial._coefficients.Select(x => x * scalar));
    }

    /// <summary>Multiplies by a scalar.</summary>
    public static Polynomial<T> operator *(T scalar, Polynomial<T> polynomial) => polynomial * scalar;

    /// <summary>Divides every coefficient by a scalar.</summary>
    public static Polynomial<T> operator /(Polynomial<T> polynomial, T scalar)
    {
        ArgumentNullException.ThrowIfNull(polynomial);
        return polynomial.With(polynomial._coefficients.Select(x => x / scalar));
    }

    /// <summary>Raises the polynomial to a non-negative integer power by repeated squaring.</summary>
    public Polynomial<T> Pow(int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        var result = With([T.MultiplicativeIdentity]);
        var square = this;
        while (exponent > 0)
        {
            if ((exponent & 1) == 1) result *= square;
            exponent >>= 1;
            if (exponent > 0) square *= square;
        }
        return result;
    }

    /// <summary>
    /// Division with remainder (catalog <c>alg.poly.division-algorithm</c>): returns q and r with <c>this = divisor·q + r</c> and
    /// deg r &lt; deg divisor, both unique over a field.
    /// </summary>
    /// <exception cref="DivideByZeroException"><paramref name="divisor"/> is zero.</exception>
    public (Polynomial<T> Quotient, Polynomial<T> Remainder) DivRem(Polynomial<T> divisor)
    {
        ArgumentNullException.ThrowIfNull(divisor);
        if (divisor.IsZero) throw new DivideByZeroException("Division by the zero polynomial.");
        if (Degree < divisor.Degree) return (new([], _isZero), this);

        var remainder = _coefficients.ToArray();
        var quotient = new T[Degree - divisor.Degree + 1];
        var lead = divisor.LeadingCoefficient;
        for (var shift = quotient.Length - 1; shift >= 0; shift--)
        {
            var factor = remainder[shift + divisor.Degree] / lead;
            quotient[shift] = factor;
            for (var j = 0; j <= divisor.Degree; j++) remainder[shift + j] -= factor * divisor._coefficients[j];

            // The leading term cancels exactly; set it so a lossy zero test cannot leave residue.
            remainder[shift + divisor.Degree] = T.AdditiveIdentity;
        }
        return (With(quotient), With(remainder));
    }

    /// <summary>The quotient of <see cref="DivRem"/>.</summary>
    public static Polynomial<T> operator /(Polynomial<T> left, Polynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.DivRem(right).Quotient;
    }

    /// <summary>The remainder of <see cref="DivRem"/>.</summary>
    public static Polynomial<T> operator %(Polynomial<T> left, Polynomial<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.DivRem(right).Remainder;
    }

    /// <summary>The derivative p′ (catalog <c>calc.deriv.power</c> applied term by term).</summary>
    public Polynomial<T> Derivative()
    {
        if (Degree <= 0) return new([], _isZero);
        var c = new T[Degree];
        var k = T.MultiplicativeIdentity;
        for (var i = 1; i <= Degree; i++)
        {
            c[i - 1] = k * _coefficients[i];
            k += T.MultiplicativeIdentity;
        }
        return With(c);
    }

    /// <summary>Evaluates p(x) by Horner's rule (catalog <c>num.fp.horner</c>): n multiplications and n additions.</summary>
    public T Evaluate(T x)
    {
        var result = T.AdditiveIdentity;
        for (var i = _coefficients.Length - 1; i >= 0; i--) result = result * x + _coefficients[i];
        return result;
    }

    /// <summary>The composition p(q(x)).</summary>
    public Polynomial<T> Compose(Polynomial<T> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        var result = new Polynomial<T>([], _isZero);
        for (var i = _coefficients.Length - 1; i >= 0; i--) result = result * inner + With([_coefficients[i]]);
        return result;
    }

    /// <summary>The Taylor shift p(x + c), computed by repeated synthetic division by x − (−c)… (Horner shift) in O(n²).</summary>
    public Polynomial<T> TaylorShift(T c)
    {
        var a = _coefficients.ToArray();
        for (var i = 0; i < a.Length - 1; i++)
        {
            for (var j = a.Length - 2; j >= i; j--) a[j] += c * a[j + 1];
        }
        return With(a);
    }

    /// <summary>The polynomial scaled to leading coefficient 1 (the zero polynomial stays zero).</summary>
    public Polynomial<T> Monic() => IsZero ? this : this / LeadingCoefficient;

    /// <summary>Maps every coefficient to another type, for example from exact rationals to <see cref="double"/>.</summary>
    public Polynomial<TOut> Map<TOut>(Func<T, TOut> selector, Func<TOut, bool>? isZero = null)
        where TOut : IAdditionOperators<TOut, TOut, TOut>, ISubtractionOperators<TOut, TOut, TOut>, IMultiplyOperators<TOut, TOut, TOut>, IDivisionOperators<TOut, TOut, TOut>,
            IUnaryNegationOperators<TOut, TOut>, IAdditiveIdentity<TOut, TOut>, IMultiplicativeIdentity<TOut, TOut>, IEqualityOperators<TOut, TOut, bool>
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(_coefficients.Select(selector), isZero);
    }

    // ----- Equality and display -----

    /// <inheritdoc />
    public bool Equals(Polynomial<T>? other)
    {
        if (other is null || other._coefficients.Length != _coefficients.Length) return false;
        for (var i = 0; i < _coefficients.Length; i++)
        {
            if (_coefficients[i] != other._coefficients[i]) return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Polynomial<T>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var c in _coefficients) hash.Add(c);
        return hash.ToHashCode();
    }

    /// <summary>Structural equality of the coefficient lists.</summary>
    public static bool operator ==(Polynomial<T>? left, Polynomial<T>? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(Polynomial<T>? left, Polynomial<T>? right) => !(left == right);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_coefficients).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Formats as <c>c₀ + c₁x + …</c> in ascending order, for diagnostics.</summary>
    public override string ToString() =>
        IsZero ? "0" : string.Join(" + ", _coefficients.Select((c, i) => i == 0 ? $"{c}" : i == 1 ? $"({c})x" : $"({c})x^{i}"));
}
