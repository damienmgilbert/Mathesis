using System.Numerics;

namespace Mathesis.Numbers;

/// <summary>
/// A closed interval <c>[Lower, Upper]</c> of real numbers with outward rounding: every operation returns an interval that
/// contains the exact result for every choice of points in the operands (docs/design/04, "Number tower"; docs/design/07,
/// "Assumptions and domains").
/// </summary>
/// <remarks>
/// <para>
/// Rounding is implemented by widening, not by switching the hardware rounding mode: the four arithmetic operations and
/// <c>sqrt</c> are correctly rounded by IEEE 754, so moving each endpoint one unit in the last place outward encloses the
/// exact result; the elementary functions of the .NET runtime are accurate to about one ulp, so their endpoints move
/// <see cref="TranscendentalUlps"/> units outward. Endpoints may be infinite.
/// </para>
/// <para>
/// Functions with restricted domains (<c>sqrt</c>, <c>ln</c>, <c>arcsin</c>, division, …) evaluate over the part of the
/// operand inside the domain, so the result encloses the function on every point where it is defined; an operand entirely
/// outside the domain gives <see cref="Empty"/>. Division by an interval containing zero is the hull of the quotients at
/// the points where the divisor is non-zero.
/// </para>
/// </remarks>
/// <typeparam name="T">The endpoint type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Interval<double>.Sin(x) and Interval<T>.Empty mirror the static math functions of the BCL numeric types.")]
public readonly struct Interval<T> : IEquatable<Interval<T>>
    where T : IFloatingPointIeee754<T>
{
    /// <summary>How many units in the last place the endpoints of an elementary-function result move outward.</summary>
    public const int TranscendentalUlps = 4;

    private readonly T _lower;
    private readonly T _upper;
    private readonly bool _nonEmpty;

    /// <summary>Creates the interval <c>[lower, upper]</c> without rounding.</summary>
    /// <exception cref="ArgumentException">An endpoint is NaN or <paramref name="lower"/> exceeds <paramref name="upper"/>.</exception>
    public Interval(T lower, T upper)
    {
        if (T.IsNaN(lower) || T.IsNaN(upper) || lower > upper) throw new ArgumentException($"[{lower}, {upper}] is not an interval.");
        _lower = lower;
        _upper = upper;
        _nonEmpty = true;
    }

    /// <summary>The lower endpoint (NaN for <see cref="Empty"/>).</summary>
    public T Lower => _nonEmpty ? _lower : T.NaN;

    /// <summary>The upper endpoint (NaN for <see cref="Empty"/>).</summary>
    public T Upper => _nonEmpty ? _upper : T.NaN;

    /// <summary>Whether the interval contains no points.</summary>
    public bool IsEmpty => !_nonEmpty;

    /// <summary>The empty interval.</summary>
    public static Interval<T> Empty => default;

    /// <summary>The whole real line <c>[−∞, +∞]</c>.</summary>
    public static Interval<T> Entire => new(T.NegativeInfinity, T.PositiveInfinity);

    /// <summary>Whether this is the whole line.</summary>
    public bool IsEntire => _nonEmpty && T.IsNegativeInfinity(_lower) && T.IsPositiveInfinity(_upper);

    /// <summary>Whether both endpoints are finite.</summary>
    public bool IsBounded => _nonEmpty && T.IsFinite(_lower) && T.IsFinite(_upper);

    /// <summary>Whether the interval is a single point.</summary>
    public bool IsPoint => _nonEmpty && _lower == _upper;

    /// <summary>The interval <c>[x, x]</c>.</summary>
    /// <exception cref="ArgumentException"><paramref name="x"/> is NaN.</exception>
    public static Interval<T> Point(T x) => T.IsNaN(x) ? throw new ArgumentException("NaN is not a point.", nameof(x)) : new(x, x);

    /// <summary>The smallest interval containing <paramref name="x"/> after widening by <paramref name="ulps"/> units in the last place.</summary>
    public static Interval<T> Around(T x, int ulps = 1) => T.IsNaN(x) ? Entire : new(Down(x, ulps), Up(x, ulps));

    /// <summary><c>upper − lower</c> rounded up (<c>0</c> for a point, NaN when empty).</summary>
    public T Width => _nonEmpty ? Up(_upper - _lower) : T.NaN;

    /// <summary>The midpoint (NaN when empty; <c>0</c> for the entire line).</summary>
    public T Midpoint => !_nonEmpty ? T.NaN : IsEntire ? T.Zero : T.IsInfinity(_lower) ? _upper : T.IsInfinity(_upper) ? _lower : _lower / (T.One + T.One) + _upper / (T.One + T.One);

    /// <summary>The largest absolute value of a point in the interval.</summary>
    public T Magnitude => !_nonEmpty ? T.NaN : T.Max(T.Abs(_lower), T.Abs(_upper));

    /// <summary>The smallest absolute value of a point in the interval.</summary>
    public T Mignitude => !_nonEmpty ? T.NaN : Contains(T.Zero) ? T.Zero : T.Min(T.Abs(_lower), T.Abs(_upper));

    /// <summary>Whether <paramref name="x"/> lies in the interval.</summary>
    public bool Contains(T x) => _nonEmpty && _lower <= x && x <= _upper;

    /// <summary>Whether every point of <paramref name="other"/> lies in this interval (the empty interval is contained in every interval).</summary>
    public bool Contains(Interval<T> other) => other.IsEmpty || (_nonEmpty && _lower <= other._lower && other._upper <= _upper);

    /// <summary>Whether the two intervals share a point.</summary>
    public bool Overlaps(Interval<T> other) => _nonEmpty && other._nonEmpty && _lower <= other._upper && other._lower <= _upper;

    /// <summary>The smallest interval containing both operands.</summary>
    public static Interval<T> Hull(Interval<T> a, Interval<T> b) =>
        a.IsEmpty ? b : b.IsEmpty ? a : new(T.Min(a._lower, b._lower), T.Max(a._upper, b._upper));

    /// <summary>The intersection (possibly empty).</summary>
    public static Interval<T> Intersect(Interval<T> a, Interval<T> b) =>
        !a.Overlaps(b) ? Empty : new(T.Max(a._lower, b._lower), T.Min(a._upper, b._upper));

    // ----- Outward rounding helpers -----

    private static T Down(T x, int ulps = 1)
    {
        for (var i = 0; i < ulps && T.IsFinite(x); i++) x = T.BitDecrement(x);
        return x;
    }

    private static T Up(T x, int ulps = 1)
    {
        for (var i = 0; i < ulps && T.IsFinite(x); i++) x = T.BitIncrement(x);
        return x;
    }

    private static Interval<T> Rounded(T lower, T upper, int ulps = 1)
    {
        if (T.IsNaN(lower)) lower = T.NegativeInfinity;
        if (T.IsNaN(upper)) upper = T.PositiveInfinity;
        return new(Down(lower, ulps), Up(upper, ulps));
    }

    // ----- Arithmetic -----

    /// <summary>Unary plus.</summary>
    public static Interval<T> operator +(Interval<T> value) => value;

    /// <summary>Negation (exact).</summary>
    public static Interval<T> operator -(Interval<T> value) => value.IsEmpty ? Empty : new(-value._upper, -value._lower);

    /// <summary>Sum.</summary>
    public static Interval<T> operator +(Interval<T> a, Interval<T> b) =>
        a.IsEmpty || b.IsEmpty ? Empty : Rounded(a._lower + b._lower, a._upper + b._upper);

    /// <summary>Difference.</summary>
    public static Interval<T> operator -(Interval<T> a, Interval<T> b) =>
        a.IsEmpty || b.IsEmpty ? Empty : Rounded(a._lower - b._upper, a._upper - b._lower);

    /// <summary>Product (<c>0 · ∞</c> counts as 0).</summary>
    public static Interval<T> operator *(Interval<T> a, Interval<T> b)
    {
        if (a.IsEmpty || b.IsEmpty) return Empty;
        var lo = T.PositiveInfinity;
        var hi = T.NegativeInfinity;
        foreach (var x in (ReadOnlySpan<T>)[a._lower, a._upper])
        {
            foreach (var y in (ReadOnlySpan<T>)[b._lower, b._upper])
            {
                var p = (x == T.Zero || y == T.Zero) ? T.Zero : x * y;
                lo = T.Min(lo, p);
                hi = T.Max(hi, p);
            }
        }
        return Rounded(lo, hi);
    }

    /// <summary>Quotient over the points where the divisor is non-zero.</summary>
    public static Interval<T> operator /(Interval<T> a, Interval<T> b)
    {
        if (a.IsEmpty || b.IsEmpty) return Empty;
        if (b._lower == T.Zero && b._upper == T.Zero) return Empty;
        if (b._lower < T.Zero && b._upper > T.Zero) return Entire;
        Interval<T> reciprocal;
        if (b._lower == T.Zero) reciprocal = Rounded(T.One / b._upper, T.PositiveInfinity);
        else if (b._upper == T.Zero) reciprocal = Rounded(T.NegativeInfinity, T.One / b._lower);
        else reciprocal = Rounded(T.One / b._upper, T.One / b._lower);
        return a * reciprocal;
    }

    /// <summary>Converts a point to a degenerate interval.</summary>
    public static implicit operator Interval<T>(T value) => Point(value);

    // ----- Elementary functions -----

    /// <summary>Absolute value (exact).</summary>
    public static Interval<T> Abs(Interval<T> x) =>
        x.IsEmpty ? Empty : x._lower >= T.Zero ? x : x._upper <= T.Zero ? -x : new(T.Zero, T.Max(-x._lower, x._upper));

    /// <summary>Sign: the hull of the signs (−1, 0, 1) of the points.</summary>
    public static Interval<T> Sign(Interval<T> x) =>
        x.IsEmpty ? Empty : new(T.CreateChecked(T.Sign(x._lower)), T.CreateChecked(T.Sign(x._upper)));

    /// <summary>Floor (exact; monotone).</summary>
    public static Interval<T> Floor(Interval<T> x) => x.IsEmpty ? Empty : new(T.Floor(x._lower), T.Floor(x._upper));

    /// <summary>Ceiling (exact; monotone).</summary>
    public static Interval<T> Ceiling(Interval<T> x) => x.IsEmpty ? Empty : new(T.Ceiling(x._lower), T.Ceiling(x._upper));

    /// <summary>Rounding half away from zero (<c>conv.rounding</c>; exact; monotone).</summary>
    public static Interval<T> Round(Interval<T> x) =>
        x.IsEmpty ? Empty : new(T.Round(x._lower, MidpointRounding.AwayFromZero), T.Round(x._upper, MidpointRounding.AwayFromZero));

    /// <summary>The smaller of two intervals, pointwise.</summary>
    public static Interval<T> Min(Interval<T> a, Interval<T> b) =>
        a.IsEmpty || b.IsEmpty ? Empty : new(T.Min(a._lower, b._lower), T.Min(a._upper, b._upper));

    /// <summary>The larger of two intervals, pointwise.</summary>
    public static Interval<T> Max(Interval<T> a, Interval<T> b) =>
        a.IsEmpty || b.IsEmpty ? Empty : new(T.Max(a._lower, b._lower), T.Max(a._upper, b._upper));

    /// <summary>Square root over the non-negative part.</summary>
    public static Interval<T> Sqrt(Interval<T> x)
    {
        if (x.IsEmpty || x._upper < T.Zero) return Empty;
        var lo = T.Sqrt(T.Max(x._lower, T.Zero));
        return new(T.Max(T.Zero, Down(lo)), Up(T.Sqrt(x._upper)));
    }

    /// <summary>Square (tighter than <c>x * x</c>).</summary>
    public static Interval<T> Square(Interval<T> x) => Pow(x, 2);

    /// <summary>Integer power.</summary>
    public static Interval<T> Pow(Interval<T> x, int n)
    {
        if (x.IsEmpty) return Empty;
        if (n == 0) return Point(T.One);
        if (n < 0)
        {
            return Point(T.One) / Pow(x, checked(-n));
        }
        if (n == 1) return x;

        // Repeated multiplication of exact endpoint powers: each power is rounded outward by IntegerPower.
        T Power(T v) => IntegerPower(v, n);
        if (n % 2 == 1) return Rounded(Power(x._lower), Power(x._upper), 1 + Ulps(n));
        if (x._lower >= T.Zero) return Rounded(Power(x._lower), Power(x._upper), 1 + Ulps(n));
        if (x._upper <= T.Zero) return Rounded(Power(x._upper), Power(x._lower), 1 + Ulps(n));
        return new(T.Zero, Up(Power(T.Max(-x._lower, x._upper)), 1 + Ulps(n)));
    }

    private static int Ulps(int n) => int.Log2(n) + 1;

    // x^n by squaring; every multiplication is within one ulp, so the accumulated error is bounded by 2·log2(n) ulps.
    private static T IntegerPower(T x, int n)
    {
        var result = T.One;
        var b = x;
        while (n > 0)
        {
            if ((n & 1) == 1) result *= b;
            n >>= 1;
            if (n > 0) b *= b;
        }
        return result;
    }

    /// <summary>
    /// Real <paramref name="n"/>-th root (<c>n ≥ 1</c>): odd roots are defined for every real, even roots over the non-negative part.
    /// </summary>
    public static Interval<T> Root(Interval<T> x, int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        if (x.IsEmpty) return Empty;
        if (n == 1) return x;
        if (n == 2) return Sqrt(x);
        T R(T v) => v < T.Zero ? -T.Pow(-v, T.One / T.CreateChecked(n)) : T.Pow(v, T.One / T.CreateChecked(n));
        if (n % 2 == 0)
        {
            if (x._upper < T.Zero) return Empty;
            return new(T.Max(T.Zero, Down(R(T.Max(x._lower, T.Zero)), TranscendentalUlps)), Up(R(x._upper), TranscendentalUlps));
        }
        return Rounded(R(x._lower), R(x._upper), TranscendentalUlps);
    }

    /// <summary>
    /// General power <c>x^y</c> at the points where it is defined in the reals: positive bases always, zero for non-negative
    /// exponents (<c>0^0 = 1</c>), and negative bases only for integer exponents. When a negative base can meet an integer
    /// exponent the result is <see cref="Entire"/>; otherwise it is <c>exp(y ln x)</c> over the positive part.
    /// </summary>
    public static Interval<T> Pow(Interval<T> x, Interval<T> y)
    {
        if (x.IsEmpty || y.IsEmpty) return Empty;
        if (y.IsPoint && T.IsInteger(y._lower) && T.Abs(y._lower) < T.CreateChecked(1 << 20)) return Pow(x, int.CreateChecked(y._lower));
        if (x._lower < T.Zero && T.Ceiling(y._lower) <= T.Floor(y._upper)) return Entire;
        if (x._upper < T.Zero) return Empty;
        if (x._upper == T.Zero) return y._upper >= T.Zero ? (y.Contains(T.Zero) ? Hull(Point(T.Zero), Point(T.One)) : Point(T.Zero)) : Empty;
        if (x._lower <= T.Zero)
        {
            // The positive part (0, upper]; the limit at 0 is 0 for positive exponents, and 0^0 = 1.
            var positive = Pow(new Interval<T>(T.Epsilon, x._upper), y);
            return y._upper >= T.Zero ? Hull(positive, y._upper > T.Zero ? Point(T.Zero) : Empty) : positive;
        }
        return Exp(y * Ln(x));
    }

    /// <summary>Exponential.</summary>
    public static Interval<T> Exp(Interval<T> x) =>
        x.IsEmpty ? Empty : new(T.Max(T.Zero, Down(T.Exp(x._lower), TranscendentalUlps)), Up(T.Exp(x._upper), TranscendentalUlps));

    /// <summary>Natural logarithm over the positive part.</summary>
    public static Interval<T> Ln(Interval<T> x)
    {
        if (x.IsEmpty || x._upper <= T.Zero) return Empty;
        var lo = x._lower <= T.Zero ? T.NegativeInfinity : Down(T.Log(x._lower), TranscendentalUlps);
        return new(lo, Up(T.Log(x._upper), TranscendentalUlps));
    }

    /// <summary>Logarithm of <paramref name="x"/> to the base <paramref name="b"/>, as <c>ln x / ln b</c> (a base of 1 gives the empty interval).</summary>
    public static Interval<T> Log(Interval<T> x, Interval<T> b) => Ln(x) / Ln(b);

    // Critical points of sin and cos: sin has maxima at π/2 + 2πk and minima at −π/2 + 2πk, cos has maxima at 2πk and minima at π + 2πk.
    // "Contains" is tested with a tolerance in units of the period, so a critical point near an endpoint is included (a wider enclosure).
    private static bool ContainsCritical(T lower, T upper, T offsetInPeriods)
    {
        var tau = T.Tau;
        var tolerance = T.CreateChecked(1e-9);
        var a = lower / tau - offsetInPeriods;
        var b = upper / tau - offsetInPeriods;
        return T.Floor(b + tolerance) >= T.Ceiling(a - tolerance);
    }

    private static Interval<T> Periodic(Interval<T> x, Func<T, T> f, T maxOffset, T minOffset)
    {
        if (x.IsEmpty) return Empty;
        if (!x.IsBounded || x._upper - x._lower >= T.Tau || T.Max(T.Abs(x._lower), T.Abs(x._upper)) > T.CreateChecked(1e9)) return new(-T.One, T.One);
        var lo = T.Min(f(x._lower), f(x._upper));
        var hi = T.Max(f(x._lower), f(x._upper));
        var low = Down(lo, TranscendentalUlps);
        var high = Up(hi, TranscendentalUlps);
        if (ContainsCritical(x._lower, x._upper, maxOffset)) high = T.One;
        if (ContainsCritical(x._lower, x._upper, minOffset)) low = -T.One;
        return new(T.Max(-T.One, low), T.Min(T.One, high));
    }

    /// <summary>Sine.</summary>
    public static Interval<T> Sin(Interval<T> x) => Periodic(x, T.Sin, T.One / (T.One + T.One + T.One + T.One), T.CreateChecked(0.75));

    /// <summary>Cosine.</summary>
    public static Interval<T> Cos(Interval<T> x) => Periodic(x, T.Cos, T.Zero, T.CreateChecked(0.5));

    /// <summary>Tangent: the entire line when the interval contains a pole <c>π/2 + kπ</c>.</summary>
    public static Interval<T> Tan(Interval<T> x)
    {
        if (x.IsEmpty) return Empty;
        if (!x.IsBounded || x._upper - x._lower >= T.Pi || T.Max(T.Abs(x._lower), T.Abs(x._upper)) > T.CreateChecked(1e9)) return Entire;
        var tolerance = T.CreateChecked(1e-9);
        var a = x._lower / T.Pi - T.CreateChecked(0.5);
        var b = x._upper / T.Pi - T.CreateChecked(0.5);
        if (T.Floor(b + tolerance) >= T.Ceiling(a - tolerance)) return Entire;
        return Rounded(T.Tan(x._lower), T.Tan(x._upper), TranscendentalUlps);
    }

    /// <summary>Cotangent, as <c>cos / sin</c> over the points where sine is non-zero.</summary>
    public static Interval<T> Cot(Interval<T> x) => Cos(x) / Sin(x);

    /// <summary>Secant.</summary>
    public static Interval<T> Sec(Interval<T> x) => Point(T.One) / Cos(x);

    /// <summary>Cosecant.</summary>
    public static Interval<T> Csc(Interval<T> x) => Point(T.One) / Sin(x);

    /// <summary>Inverse sine over <c>[−1, 1]</c>.</summary>
    public static Interval<T> Asin(Interval<T> x)
    {
        var d = Intersect(x, new Interval<T>(-T.One, T.One));
        return d.IsEmpty ? Empty : new(T.Max(-T.Pi / (T.One + T.One) - T.Epsilon, Down(T.Asin(d._lower), TranscendentalUlps)), T.Min(T.Pi / (T.One + T.One) + T.Epsilon, Up(T.Asin(d._upper), TranscendentalUlps)));
    }

    /// <summary>Inverse cosine over <c>[−1, 1]</c>.</summary>
    public static Interval<T> Acos(Interval<T> x)
    {
        var d = Intersect(x, new Interval<T>(-T.One, T.One));
        return d.IsEmpty ? Empty : new(T.Max(T.Zero, Down(T.Acos(d._upper), TranscendentalUlps)), Up(T.Acos(d._lower), TranscendentalUlps));
    }

    /// <summary>Inverse tangent.</summary>
    public static Interval<T> Atan(Interval<T> x) => x.IsEmpty ? Empty : Rounded(T.Atan(x._lower), T.Atan(x._upper), TranscendentalUlps);

    /// <summary>Hyperbolic sine.</summary>
    public static Interval<T> Sinh(Interval<T> x) => x.IsEmpty ? Empty : Rounded(T.Sinh(x._lower), T.Sinh(x._upper), TranscendentalUlps);

    /// <summary>Hyperbolic cosine.</summary>
    public static Interval<T> Cosh(Interval<T> x)
    {
        if (x.IsEmpty) return Empty;
        var hi = Up(T.Max(T.Cosh(x._lower), T.Cosh(x._upper)), TranscendentalUlps);
        var lo = x.Contains(T.Zero) ? T.One : Down(T.Min(T.Cosh(x._lower), T.Cosh(x._upper)), TranscendentalUlps);
        return new(T.Max(T.One, lo), hi);
    }

    /// <summary>Hyperbolic tangent.</summary>
    public static Interval<T> Tanh(Interval<T> x) =>
        x.IsEmpty ? Empty : new(T.Max(-T.One, Down(T.Tanh(x._lower), TranscendentalUlps)), T.Min(T.One, Up(T.Tanh(x._upper), TranscendentalUlps)));

    /// <summary>Inverse hyperbolic sine.</summary>
    public static Interval<T> Asinh(Interval<T> x) => x.IsEmpty ? Empty : Rounded(T.Asinh(x._lower), T.Asinh(x._upper), TranscendentalUlps);

    /// <summary>Inverse hyperbolic cosine over <c>[1, ∞)</c>.</summary>
    public static Interval<T> Acosh(Interval<T> x)
    {
        if (x.IsEmpty || x._upper < T.One) return Empty;
        return new(T.Max(T.Zero, Down(T.Acosh(T.Max(x._lower, T.One)), TranscendentalUlps)), Up(T.Acosh(x._upper), TranscendentalUlps));
    }

    /// <summary>Inverse hyperbolic tangent over <c>(−1, 1)</c>.</summary>
    public static Interval<T> Atanh(Interval<T> x)
    {
        if (x.IsEmpty || x._upper <= -T.One || x._lower >= T.One) return Empty;
        var lo = x._lower <= -T.One ? T.NegativeInfinity : Down(T.Atanh(x._lower), TranscendentalUlps);
        var hi = x._upper >= T.One ? T.PositiveInfinity : Up(T.Atanh(x._upper), TranscendentalUlps);
        return new(lo, hi);
    }

    // ----- Equality and display -----

    /// <inheritdoc />
    public bool Equals(Interval<T> other) => _nonEmpty == other._nonEmpty && (!_nonEmpty || (_lower == other._lower && _upper == other._upper));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Interval<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _nonEmpty ? HashCode.Combine(_lower, _upper) : 0;

    /// <summary>Equality of endpoints.</summary>
    public static bool operator ==(Interval<T> left, Interval<T> right) => left.Equals(right);

    /// <summary>Inequality of endpoints.</summary>
    public static bool operator !=(Interval<T> left, Interval<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _nonEmpty ? $"[{_lower}, {_upper}]" : "∅";
}
