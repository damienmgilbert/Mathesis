using System.Numerics;
using System.Runtime.CompilerServices;

namespace Mathesis.Numbers;

// Elementary functions: the value is T's, the gradient is the chain rule (ADR-19). Where the derivative is infinite or undefined the
// formula's result (±∞ or NaN) is returned, as IEEE arithmetic would. References for the derivative formulas: any calculus text; the
// reciprocal-estimate and remainder rows follow the "Jet semantics" table in docs/design/04-type-system.md.
public readonly partial struct Jet<T>
{
    private static readonly T Two = T.CreateChecked(2);
    private static readonly T Ln2 = T.Log(Two);
    private static readonly T Ln10 = T.Log(T.CreateChecked(10));

    /// <inheritdoc />
    public static Jet<T> Exp(Jet<T> x)
    {
        var e = T.Exp(x._value);
        return Chain(e, in x, e);
    }

    /// <inheritdoc />
    public static Jet<T> ExpM1(Jet<T> x) => Chain(T.ExpM1(x._value), in x, T.Exp(x._value));

    /// <inheritdoc />
    public static Jet<T> Exp2(Jet<T> x)
    {
        var e = T.Exp2(x._value);
        return Chain(e, in x, e * Ln2);
    }

    /// <inheritdoc />
    public static Jet<T> Exp2M1(Jet<T> x) => Chain(T.Exp2M1(x._value), in x, T.Exp2(x._value) * Ln2);

    /// <inheritdoc />
    public static Jet<T> Exp10(Jet<T> x)
    {
        var e = T.Exp10(x._value);
        return Chain(e, in x, e * Ln10);
    }

    /// <inheritdoc />
    public static Jet<T> Exp10M1(Jet<T> x) => Chain(T.Exp10M1(x._value), in x, T.Exp10(x._value) * Ln10);

    /// <inheritdoc />
    public static Jet<T> Log(Jet<T> x) => ChainDivided(T.Log(x._value), in x, x._value);

    /// <inheritdoc />
    public static Jet<T> Log(Jet<T> x, Jet<T> newBase)
    {
        var quotient = Log(x) / Log(newBase);
        return WithValue(in quotient, T.Log(x._value, newBase._value));
    }

    /// <inheritdoc />
    public static Jet<T> Log2(Jet<T> x) => Chain(T.Log2(x._value), in x, T.One / (x._value * Ln2));

    /// <inheritdoc />
    public static Jet<T> Log10(Jet<T> x) => Chain(T.Log10(x._value), in x, T.One / (x._value * Ln10));

    /// <inheritdoc />
    public static Jet<T> LogP1(Jet<T> x) => Chain(T.LogP1(x._value), in x, T.One / (x._value + T.One));

    /// <inheritdoc />
    public static Jet<T> Log2P1(Jet<T> x) => Chain(T.Log2P1(x._value), in x, T.One / ((x._value + T.One) * Ln2));

    /// <inheritdoc />
    public static Jet<T> Log10P1(Jet<T> x) => Chain(T.Log10P1(x._value), in x, T.One / ((x._value + T.One) * Ln10));

    /// <inheritdoc />
    public static Jet<T> Sqrt(Jet<T> x)
    {
        var s = T.Sqrt(x._value);
        return ChainDivided(s, in x, s + s);
    }

    /// <inheritdoc />
    public static Jet<T> Cbrt(Jet<T> x)
    {
        var c = T.Cbrt(x._value);
        return Chain(c, in x, T.One / (T.CreateChecked(3) * c * c));
    }

    /// <inheritdoc />
    public static Jet<T> RootN(Jet<T> x, int n)
    {
        var r = T.RootN(x._value, n);
        var nt = T.CreateChecked(n);
        return Chain(r, in x, n > 0 ? T.One / (nt * T.Pow(r, T.CreateChecked(n - 1))) : r / (nt * x._value));
    }

    /// <inheritdoc />
    public static Jet<T> Hypot(Jet<T> x, Jet<T> y)
    {
        var h = T.Hypot(x._value, y._value);
        return Chain(h, in x, x._value / h, in y, y._value / h);
    }

    /// <summary><c>x<sup>y</sup></c>. A constant exponent of zero gives the constant 1 (the derivative <c>0·x<sup>−1</sup></c> would be NaN at <c>x = 0</c>); a constant base or exponent contributes no gradient term, so <c>(−2)<sup>3</sup></c> has no <c>log(−2)</c>.</summary>
    public static Jet<T> Pow(Jet<T> x, Jet<T> y)
    {
        if (y._count == 0 && T.IsZero(y._value)) return Constant(T.One);
        var z = T.Pow(x._value, y._value);
        if (y._count == 0) return ChainScaled(z, in x, y._value, T.Pow(x._value, y._value - T.One));
        var dx = x._count != 0 ? y._value * T.Pow(x._value, y._value - T.One) : T.Zero;
        var dy = y._count != 0 ? z * T.Log(x._value) : T.Zero;
        return Chain(z, in x, dx, in y, dy);
    }

    /// <inheritdoc />
    public static Jet<T> Sin(Jet<T> x) => Chain(T.Sin(x._value), in x, T.Cos(x._value));

    /// <inheritdoc />
    public static Jet<T> Cos(Jet<T> x) => Chain(T.Cos(x._value), in x, -T.Sin(x._value));

    /// <inheritdoc />
    public static (Jet<T> Sin, Jet<T> Cos) SinCos(Jet<T> x) => (Sin(x), Cos(x));

    /// <inheritdoc />
    public static Jet<T> Tan(Jet<T> x)
    {
        var t = T.Tan(x._value);
        return Chain(t, in x, T.One + t * t);
    }

    /// <inheritdoc />
    public static Jet<T> SinPi(Jet<T> x) => Chain(T.SinPi(x._value), in x, T.Pi * T.CosPi(x._value));

    /// <inheritdoc />
    public static Jet<T> CosPi(Jet<T> x) => Chain(T.CosPi(x._value), in x, -T.Pi * T.SinPi(x._value));

    /// <inheritdoc />
    public static (Jet<T> SinPi, Jet<T> CosPi) SinCosPi(Jet<T> x) => (SinPi(x), CosPi(x));

    /// <inheritdoc />
    public static Jet<T> TanPi(Jet<T> x)
    {
        var t = T.TanPi(x._value);
        return Chain(t, in x, T.Pi * (T.One + t * t));
    }

    /// <inheritdoc />
    public static Jet<T> Asin(Jet<T> x) => Chain(T.Asin(x._value), in x, T.One / T.Sqrt((T.One - x._value) * (T.One + x._value)));

    /// <inheritdoc />
    public static Jet<T> Acos(Jet<T> x) => Chain(T.Acos(x._value), in x, -T.One / T.Sqrt((T.One - x._value) * (T.One + x._value)));

    /// <inheritdoc />
    public static Jet<T> Atan(Jet<T> x) => Chain(T.Atan(x._value), in x, T.One / (T.One + x._value * x._value));

    /// <inheritdoc />
    public static Jet<T> AsinPi(Jet<T> x) => Chain(T.AsinPi(x._value), in x, T.One / (T.Pi * T.Sqrt((T.One - x._value) * (T.One + x._value))));

    /// <inheritdoc />
    public static Jet<T> AcosPi(Jet<T> x) => Chain(T.AcosPi(x._value), in x, -T.One / (T.Pi * T.Sqrt((T.One - x._value) * (T.One + x._value))));

    /// <inheritdoc />
    public static Jet<T> AtanPi(Jet<T> x) => Chain(T.AtanPi(x._value), in x, T.One / (T.Pi * (T.One + x._value * x._value)));

    /// <summary>The angle of the point (<paramref name="x"/>, <paramref name="y"/>) in (−π, π]; gradient <c>(x·y′ − y·x′)/(x² + y²)</c>.</summary>
    public static Jet<T> Atan2(Jet<T> y, Jet<T> x)
    {
        var r = Chain(T.Atan2(y._value, x._value), in y, x._value, in x, -y._value);
        return Divide(r, x._value * x._value + y._value * y._value);
    }

    /// <summary><see cref="Atan2(Jet{T}, Jet{T})"/> divided by π.</summary>
    public static Jet<T> Atan2Pi(Jet<T> y, Jet<T> x)
    {
        var r = Chain(T.Atan2Pi(y._value, x._value), in y, x._value, in x, -y._value);
        return Divide(r, (x._value * x._value + y._value * y._value) * T.Pi);
    }

    /// <summary><paramref name="x"/> with its gradient divided by <paramref name="d"/>; the value is kept.</summary>
    private static Jet<T> Divide(Jet<T> x, T d)
    {
        var n = x._count;
        ref var xs = ref Lane0(in x);
        for (var i = 0; i < n; i++) Unsafe.Add(ref xs, i) /= d;
        return x;
    }

    /// <inheritdoc />
    public static Jet<T> Sinh(Jet<T> x) => Chain(T.Sinh(x._value), in x, T.Cosh(x._value));

    /// <inheritdoc />
    public static Jet<T> Cosh(Jet<T> x) => Chain(T.Cosh(x._value), in x, T.Sinh(x._value));

    /// <inheritdoc />
    public static Jet<T> Tanh(Jet<T> x)
    {
        var t = T.Tanh(x._value);
        return Chain(t, in x, T.One - t * t);
    }

    /// <inheritdoc />
    public static Jet<T> Asinh(Jet<T> x) => Chain(T.Asinh(x._value), in x, T.One / T.Hypot(x._value, T.One));

    /// <inheritdoc />
    public static Jet<T> Acosh(Jet<T> x) => Chain(T.Acosh(x._value), in x, T.One / T.Sqrt((x._value - T.One) * (x._value + T.One)));

    /// <inheritdoc />
    public static Jet<T> Atanh(Jet<T> x) => Chain(T.Atanh(x._value), in x, T.One / ((T.One - x._value) * (T.One + x._value)));

    /// <summary>|x|. The gradient is negated when the value is negative, so +0 and −0 both take the gradient unchanged (a refinement of <c>Dual&lt;T&gt;</c>, which negates at −0).</summary>
    public static Jet<T> Abs(Jet<T> value)
    {
        var a = T.Abs(value._value);
        return value._value < T.Zero ? WithValue(-value, a) : WithValue(in value, a);
    }

    /// <summary>The larger value (NaN if either is NaN); the gradient of the selected operand, the first on ties.</summary>
    public static Jet<T> Max(Jet<T> x, Jet<T> y)
    {
        var v = T.Max(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in x, v);
        if (T.IsNaN(y._value)) return WithValue(in y, v);
        return WithValue(y._value > x._value ? y : x, v);
    }

    /// <summary>The larger value, ignoring a NaN operand; the gradient of the selected operand, the first on ties.</summary>
    public static Jet<T> MaxNumber(Jet<T> x, Jet<T> y)
    {
        var v = T.MaxNumber(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in y, v);
        if (T.IsNaN(y._value)) return WithValue(in x, v);
        return WithValue(y._value > x._value ? y : x, v);
    }

    /// <summary>The smaller value (NaN if either is NaN); the gradient of the selected operand, the first on ties.</summary>
    public static Jet<T> Min(Jet<T> x, Jet<T> y)
    {
        var v = T.Min(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in x, v);
        if (T.IsNaN(y._value)) return WithValue(in y, v);
        return WithValue(y._value < x._value ? y : x, v);
    }

    /// <summary>The smaller value, ignoring a NaN operand; the gradient of the selected operand, the first on ties.</summary>
    public static Jet<T> MinNumber(Jet<T> x, Jet<T> y)
    {
        var v = T.MinNumber(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in y, v);
        if (T.IsNaN(y._value)) return WithValue(in x, v);
        return WithValue(y._value < x._value ? y : x, v);
    }

    /// <inheritdoc />
    public static Jet<T> MaxMagnitude(Jet<T> x, Jet<T> y)
    {
        var v = T.MaxMagnitude(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in x, v);
        if (T.IsNaN(y._value)) return WithValue(in y, v);
        var ax = T.Abs(x._value);
        var ay = T.Abs(y._value);
        return WithValue(ay > ax || (ay == ax && y._value == v && x._value != v) ? y : x, v);
    }

    /// <inheritdoc />
    public static Jet<T> MaxMagnitudeNumber(Jet<T> x, Jet<T> y)
    {
        var v = T.MaxMagnitudeNumber(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in y, v);
        if (T.IsNaN(y._value)) return WithValue(in x, v);
        var ax = T.Abs(x._value);
        var ay = T.Abs(y._value);
        return WithValue(ay > ax || (ay == ax && y._value == v && x._value != v) ? y : x, v);
    }

    /// <inheritdoc />
    public static Jet<T> MinMagnitude(Jet<T> x, Jet<T> y)
    {
        var v = T.MinMagnitude(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in x, v);
        if (T.IsNaN(y._value)) return WithValue(in y, v);
        var ax = T.Abs(x._value);
        var ay = T.Abs(y._value);
        return WithValue(ay < ax || (ay == ax && y._value == v && x._value != v) ? y : x, v);
    }

    /// <inheritdoc />
    public static Jet<T> MinMagnitudeNumber(Jet<T> x, Jet<T> y)
    {
        var v = T.MinMagnitudeNumber(x._value, y._value);
        if (T.IsNaN(x._value)) return WithValue(in y, v);
        if (T.IsNaN(y._value)) return WithValue(in x, v);
        var ax = T.Abs(x._value);
        var ay = T.Abs(y._value);
        return WithValue(ay < ax || (ay == ax && y._value == v && x._value != v) ? y : x, v);
    }

    /// <summary>Clamps to [<paramref name="min"/>, <paramref name="max"/>]; the gradient is the bound's when it clamps (throws like <typeparamref name="T"/> when <c>min &gt; max</c>).</summary>
    public static Jet<T> Clamp(Jet<T> value, Jet<T> min, Jet<T> max)
    {
        var v = T.Clamp(value._value, min._value, max._value);
        if (value._value < min._value) return WithValue(in min, v);
        if (value._value > max._value) return WithValue(in max, v);
        return WithValue(in value, v);
    }

    /// <summary>The magnitude of <paramref name="value"/> with the sign of <paramref name="sign"/>; the gradient is the magnitude operand's, negated when the sign flips. The sign operand contributes none.</summary>
    public static Jet<T> CopySign(Jet<T> value, Jet<T> sign)
    {
        var v = T.CopySign(value._value, sign._value);
        return T.IsNegative(value._value) != T.IsNegative(sign._value) ? WithValue(-value, v) : WithValue(in value, v);
    }

    /// <summary>The sign of the value: −1, 0 or 1 (throws like <typeparamref name="T"/> for NaN).</summary>
    public static int Sign(Jet<T> value) => T.Sign(value._value);

    /// <inheritdoc />
    public static Jet<T> Floor(Jet<T> x) => Constant(T.Floor(x._value));

    /// <inheritdoc />
    public static Jet<T> Ceiling(Jet<T> x) => Constant(T.Ceiling(x._value));

    /// <inheritdoc />
    public static Jet<T> Truncate(Jet<T> x) => Constant(T.Truncate(x._value));

    /// <inheritdoc />
    public static Jet<T> Round(Jet<T> x) => Constant(T.Round(x._value));

    /// <inheritdoc />
    public static Jet<T> Round(Jet<T> x, int digits) => Constant(T.Round(x._value, digits));

    /// <inheritdoc />
    public static Jet<T> Round(Jet<T> x, MidpointRounding mode) => Constant(T.Round(x._value, mode));

    /// <inheritdoc />
    public static Jet<T> Round(Jet<T> x, int digits, MidpointRounding mode) => Constant(T.Round(x._value, digits, mode));

    /// <inheritdoc />
    public static Jet<T> ScaleB(Jet<T> x, int n) => Chain(T.ScaleB(x._value, n), in x, T.ScaleB(T.One, n));

    /// <inheritdoc />
    public static int ILogB(Jet<T> x) => T.ILogB(x._value);

    /// <inheritdoc />
    public static Jet<T> BitIncrement(Jet<T> x) => WithValue(in x, T.BitIncrement(x._value));

    /// <inheritdoc />
    public static Jet<T> BitDecrement(Jet<T> x) => WithValue(in x, T.BitDecrement(x._value));

    /// <summary>An estimate of 1/x; the gradient is the exact derivative <c>−x′/x²</c>.</summary>
    public static Jet<T> ReciprocalEstimate(Jet<T> x) => Chain(T.ReciprocalEstimate(x._value), in x, -T.One / (x._value * x._value));

    /// <summary>An estimate of 1/√x; the gradient is the exact derivative <c>−x′/(2·x·√x)</c>.</summary>
    public static Jet<T> ReciprocalSqrtEstimate(Jet<T> x) =>
        Chain(T.ReciprocalSqrtEstimate(x._value), in x, -T.One / (Two * x._value * T.Sqrt(x._value)));

    /// <summary>The IEEE 754 remainder <c>x − y·round(x/y)</c>; the gradient is <c>x′ − y′·round(x/y)</c>.</summary>
    public static Jet<T> Ieee754Remainder(Jet<T> left, Jet<T> right) =>
        Chain(T.Ieee754Remainder(left._value, right._value), in left, T.One, in right, -T.Round(left._value / right._value));

    /// <summary>The linear interpolation <c>value1 + amount·(value2 − value1)</c>, with <typeparamref name="T"/>'s value and the gradient of the interpolation formula.</summary>
    public static Jet<T> Lerp(Jet<T> value1, Jet<T> value2, Jet<T> amount)
    {
        var r = value1 + amount * (value2 - value1);
        return WithValue(in r, T.Lerp(value1._value, value2._value, amount._value));
    }

    /// <summary><c>left·right + addend</c> with one rounding of the value; the gradient is the sum of the products' gradients.</summary>
    public static Jet<T> FusedMultiplyAdd(Jet<T> left, Jet<T> right, Jet<T> addend)
    {
        var r = left * right + addend;
        return WithValue(in r, T.FusedMultiplyAdd(left._value, right._value, addend._value));
    }
}
