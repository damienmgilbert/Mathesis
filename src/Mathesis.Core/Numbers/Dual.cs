using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Mathesis.Numbers;

/// <summary>
/// A dual number a + bε with ε² = 0, used for forward-mode automatic differentiation: evaluating a function at
/// <c>Dual.Variable(x)</c> yields f(x) in <see cref="Value"/> and f′(x) in <see cref="Derivative"/>, exact up to
/// rounding in <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
/// <remarks>
/// <para>Duals have no order, so <see cref="Dual{T}"/> is not <see cref="IComparable{T}"/>. Equality compares both parts.
/// Text form is <c>(value, derivative)</c>; a bare number parses as a constant. Components use the invariant culture.</para>
/// <para>Elementary functions for <c>T : IFloatingPointIeee754&lt;T&gt;</c> are extension members in <see cref="DualFunctions"/>.
/// <see cref="INumberBase{TSelf}.Abs"/> has derivative sign(a)·b, and takes b at a = 0 where the derivative does not exist.</para>
/// </remarks>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static members are required by INumberBase and specified by docs/design/04-type-system.md.")]
public readonly struct Dual<T> : INumberBase<Dual<T>>
    where T : INumberBase<T>
{
    /// <summary>Creates value + derivative·ε.</summary>
    public Dual(T value, T derivative)
    {
        Value = value;
        Derivative = derivative;
    }

    /// <summary>The primal part a (the function value).</summary>
    public T Value { get; }

    /// <summary>The dual part b (the derivative).</summary>
    public T Derivative { get; }

    /// <summary>The independent variable at <paramref name="x"/>: x + 1·ε.</summary>
    public static Dual<T> Variable(T x) => new(x, T.One);

    /// <summary>A constant: c + 0·ε.</summary>
    public static Dual<T> Constant(T c) => new(c, T.Zero);

    /// <inheritdoc />
    public static Dual<T> Zero => new(T.Zero, T.Zero);

    /// <inheritdoc />
    public static Dual<T> One => new(T.One, T.Zero);

    /// <inheritdoc />
    public static int Radix => T.Radix;

    /// <inheritdoc />
    public static Dual<T> AdditiveIdentity => Zero;

    /// <inheritdoc />
    public static Dual<T> MultiplicativeIdentity => One;

    // ----- Arithmetic -----

    /// <inheritdoc />
    public static Dual<T> operator +(Dual<T> left, Dual<T> right) =>
        new(left.Value + right.Value, left.Derivative + right.Derivative);

    /// <inheritdoc />
    public static Dual<T> operator -(Dual<T> left, Dual<T> right) =>
        new(left.Value - right.Value, left.Derivative - right.Derivative);

    /// <inheritdoc />
    public static Dual<T> operator *(Dual<T> left, Dual<T> right) =>
        new(left.Value * right.Value, left.Value * right.Derivative + left.Derivative * right.Value);

    /// <inheritdoc />
    public static Dual<T> operator /(Dual<T> left, Dual<T> right)
    {
        var quotient = left.Value / right.Value;
        return new(quotient, (left.Derivative - quotient * right.Derivative) / right.Value);
    }

    /// <inheritdoc />
    public static Dual<T> operator -(Dual<T> value) => new(-value.Value, -value.Derivative);

    /// <inheritdoc />
    public static Dual<T> operator +(Dual<T> value) => value;

    /// <inheritdoc />
    public static Dual<T> operator ++(Dual<T> value) => value + One;

    /// <inheritdoc />
    public static Dual<T> operator --(Dual<T> value) => value - One;

    /// <summary>Converts a real number to a constant dual number.</summary>
    public static implicit operator Dual<T>(T value) => Constant(value);

    // ----- Equality -----

    /// <inheritdoc />
    public bool Equals(Dual<T> other) => Value == other.Value && Derivative == other.Derivative;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Dual<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Value, Derivative);

    /// <inheritdoc />
    public static bool operator ==(Dual<T> left, Dual<T> right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(Dual<T> left, Dual<T> right) => !left.Equals(right);

    // ----- INumberBase -----

    /// <inheritdoc />
    public static Dual<T> Abs(Dual<T> value) =>
        T.IsNegative(value.Value) ? new(-value.Value, -value.Derivative) : value;

    /// <inheritdoc />
    public static bool IsCanonical(Dual<T> value) => true;

    /// <inheritdoc />
    public static bool IsComplexNumber(Dual<T> value) => false;

    /// <inheritdoc />
    public static bool IsEvenInteger(Dual<T> value) => T.IsZero(value.Derivative) && T.IsEvenInteger(value.Value);

    /// <inheritdoc />
    public static bool IsFinite(Dual<T> value) => T.IsFinite(value.Value) && T.IsFinite(value.Derivative);

    /// <inheritdoc />
    public static bool IsImaginaryNumber(Dual<T> value) => false;

    /// <inheritdoc />
    public static bool IsInfinity(Dual<T> value) => T.IsInfinity(value.Value) || T.IsInfinity(value.Derivative);

    /// <inheritdoc />
    public static bool IsInteger(Dual<T> value) => T.IsZero(value.Derivative) && T.IsInteger(value.Value);

    /// <inheritdoc />
    public static bool IsNaN(Dual<T> value) => !IsInfinity(value) && (T.IsNaN(value.Value) || T.IsNaN(value.Derivative));

    /// <inheritdoc />
    public static bool IsNegative(Dual<T> value) => T.IsNegative(value.Value);

    /// <inheritdoc />
    public static bool IsNegativeInfinity(Dual<T> value) => T.IsNegativeInfinity(value.Value);

    /// <inheritdoc />
    public static bool IsNormal(Dual<T> value) => T.IsNormal(value.Value);

    /// <inheritdoc />
    public static bool IsOddInteger(Dual<T> value) => T.IsZero(value.Derivative) && T.IsOddInteger(value.Value);

    /// <inheritdoc />
    public static bool IsPositive(Dual<T> value) => T.IsPositive(value.Value);

    /// <inheritdoc />
    public static bool IsPositiveInfinity(Dual<T> value) => T.IsPositiveInfinity(value.Value);

    /// <inheritdoc />
    public static bool IsRealNumber(Dual<T> value) => T.IsRealNumber(value.Value);

    /// <inheritdoc />
    public static bool IsSubnormal(Dual<T> value) => T.IsSubnormal(value.Value);

    /// <inheritdoc />
    public static bool IsZero(Dual<T> value) => T.IsZero(value.Value) && T.IsZero(value.Derivative);

    /// <inheritdoc />
    public static Dual<T> MaxMagnitude(Dual<T> x, Dual<T> y) => T.MaxMagnitude(x.Value, y.Value) == x.Value ? x : y;

    /// <inheritdoc />
    public static Dual<T> MaxMagnitudeNumber(Dual<T> x, Dual<T> y) => MaxMagnitude(x, y);

    /// <inheritdoc />
    public static Dual<T> MinMagnitude(Dual<T> x, Dual<T> y) => T.MinMagnitude(x.Value, y.Value) == x.Value ? x : y;

    /// <inheritdoc />
    public static Dual<T> MinMagnitudeNumber(Dual<T> x, Dual<T> y) => MinMagnitude(x, y);

    // ----- Conversions -----

    /// <inheritdoc />
    public static bool TryConvertFromChecked<TOther>(TOther value, out Dual<T> result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, T.CreateChecked, out result);

    /// <inheritdoc />
    public static bool TryConvertFromSaturating<TOther>(TOther value, out Dual<T> result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, T.CreateSaturating, out result);

    /// <inheritdoc />
    public static bool TryConvertFromTruncating<TOther>(TOther value, out Dual<T> result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, T.CreateTruncating, out result);

    private static bool TryConvertFrom<TOther>(TOther value, Func<TOther, T> convert, out Dual<T> result)
        where TOther : INumberBase<TOther>
    {
        if (value is Dual<T> same)
        {
            result = same;
            return true;
        }
        try
        {
            result = Constant(convert(value));
            return true;
        }
        catch (NotSupportedException)
        {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    /// <exception cref="OverflowException">The derivative is non-zero, so converting to a plain number would lose it.</exception>
    public static bool TryConvertToChecked<TOther>(Dual<T> value, out TOther result) where TOther : INumberBase<TOther>
    {
        if (value is TOther same)
        {
            result = same;
            return true;
        }
        if (!T.IsZero(value.Derivative)) throw new OverflowException("Converting a dual number with a non-zero derivative would discard the derivative.");
        return TryConvertTo(value, TOther.CreateChecked, out result);
    }

    /// <inheritdoc />
    public static bool TryConvertToSaturating<TOther>(Dual<T> value, out TOther result) where TOther : INumberBase<TOther> =>
        value is TOther same ? Assign(same, out result) : TryConvertTo(value, TOther.CreateSaturating, out result);

    /// <inheritdoc />
    public static bool TryConvertToTruncating<TOther>(Dual<T> value, out TOther result) where TOther : INumberBase<TOther> =>
        value is TOther same ? Assign(same, out result) : TryConvertTo(value, TOther.CreateTruncating, out result);

    private static bool Assign<TOther>(TOther value, out TOther result)
    {
        result = value;
        return true;
    }

    private static bool TryConvertTo<TOther>(Dual<T> value, Func<T, TOther> convert, out TOther result)
        where TOther : INumberBase<TOther>
    {
        try
        {
            result = convert(value.Value);
            return true;
        }
        catch (NotSupportedException)
        {
            result = default!;
            return false;
        }
    }

    // ----- Formatting and parsing -----

    /// <summary>Formats as <c>(value, derivative)</c> using the invariant culture.</summary>
    public override string ToString() => ToString(null, null);

    /// <summary>Formats as <c>(value, derivative)</c>, passing <paramref name="format"/> to each component; the provider is ignored.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"({Value.ToString(format, CultureInfo.InvariantCulture)}, {Derivative.ToString(format, CultureInfo.InvariantCulture)})";

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString(format.IsEmpty ? null : format.ToString(), provider);
        if (text.TryCopyTo(destination))
        {
            charsWritten = text.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Encoding.UTF8.TryGetBytes(ToString(format.IsEmpty ? null : format.ToString(), provider), utf8Destination, out bytesWritten);

    /// <summary>Parses <c>(value, derivative)</c> or a bare number (a constant).</summary>
    /// <exception cref="FormatException">The text is not a dual number.</exception>
    public static Dual<T> Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does; <paramref name="style"/> is applied to each component.</summary>
    public static Dual<T> Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), style, provider);
    }

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does.</summary>
    public static Dual<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null) =>
        TryParse(s, NumberStyles.Float, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid dual number.");

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does; <paramref name="style"/> is applied to each component.</summary>
    public static Dual<T> Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) =>
        TryParse(s, style, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid dual number.");

    /// <summary>Parses UTF-8 text.</summary>
    public static Dual<T> Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(Encoding.UTF8.GetString(utf8Text), provider);

    /// <summary>Parses UTF-8 text.</summary>
    public static Dual<T> Parse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider) =>
        Parse(Encoding.UTF8.GetString(utf8Text), style, provider);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Dual<T> result) =>
        TryParse(s, NumberStyles.Float, provider, out result);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out Dual<T> result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), style, provider, out result);
    }

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Dual<T> result) =>
        TryParse(s, NumberStyles.Float, provider, out result);

    /// <summary>Tries to parse UTF-8 text.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Dual<T> result) =>
        TryParse(Encoding.UTF8.GetString(utf8Text), NumberStyles.Float, provider, out result);

    /// <summary>Tries to parse UTF-8 text.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider, out Dual<T> result) =>
        TryParse(Encoding.UTF8.GetString(utf8Text), style, provider, out result);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Dual<T> result)
    {
        result = default;
        s = s.Trim();
        if (s.IsEmpty) return false;

        if (s[0] != '(')
        {
            if (!T.TryParse(s, style, CultureInfo.InvariantCulture, out var value)) return false;
            result = Constant(value);
            return true;
        }
        if (s[^1] != ')') return false;
        var inner = s[1..^1];
        var comma = inner.IndexOf(',');
        if (comma < 0
            || !T.TryParse(inner[..comma].Trim(), style, CultureInfo.InvariantCulture, out var a)
            || !T.TryParse(inner[(comma + 1)..].Trim(), style, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }
        result = new Dual<T>(a, b);
        return true;
    }
}

/// <summary>
/// Elementary functions of <see cref="Dual{T}"/> for floating-point component types. Each applies the chain rule:
/// f(a + bε) = f(a) + b·f′(a)·ε.
/// </summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static extension members on Complex<T>/Dual<T> are the design in docs/design/04-type-system.md.")]
public static class DualFunctions
{
    extension<T>(Dual<T>) where T : IFloatingPointIeee754<T>
    {
        /// <summary>e<sup>x</sup>.</summary>
        public static Dual<T> Exp(Dual<T> x)
        {
            var e = T.Exp(x.Value);
            return new(e, x.Derivative * e);
        }

        /// <summary>2<sup>x</sup>.</summary>
        public static Dual<T> Exp2(Dual<T> x)
        {
            var e = T.Exp2(x.Value);
            return new(e, x.Derivative * e * T.Log(T.CreateChecked(2)));
        }

        /// <summary>10<sup>x</sup>.</summary>
        public static Dual<T> Exp10(Dual<T> x)
        {
            var e = T.Exp10(x.Value);
            return new(e, x.Derivative * e * T.Log(T.CreateChecked(10)));
        }

        /// <summary>The natural logarithm.</summary>
        public static Dual<T> Log(Dual<T> x) => new(T.Log(x.Value), x.Derivative / x.Value);

        /// <summary>The base-2 logarithm.</summary>
        public static Dual<T> Log2(Dual<T> x) => new(T.Log2(x.Value), x.Derivative / (x.Value * T.Log(T.CreateChecked(2))));

        /// <summary>The base-10 logarithm.</summary>
        public static Dual<T> Log10(Dual<T> x) => new(T.Log10(x.Value), x.Derivative / (x.Value * T.Log(T.CreateChecked(10))));

        /// <summary>The square root.</summary>
        public static Dual<T> Sqrt(Dual<T> x)
        {
            var s = T.Sqrt(x.Value);
            return new(s, x.Derivative / (s + s));
        }

        /// <summary>The real cube root.</summary>
        public static Dual<T> Cbrt(Dual<T> x)
        {
            var c = T.Cbrt(x.Value);
            return new(c, x.Derivative / (T.CreateChecked(3) * c * c));
        }

        /// <summary>The sine.</summary>
        public static Dual<T> Sin(Dual<T> x) => new(T.Sin(x.Value), x.Derivative * T.Cos(x.Value));

        /// <summary>The cosine.</summary>
        public static Dual<T> Cos(Dual<T> x) => new(T.Cos(x.Value), -x.Derivative * T.Sin(x.Value));

        /// <summary>The tangent.</summary>
        public static Dual<T> Tan(Dual<T> x)
        {
            var t = T.Tan(x.Value);
            return new(t, x.Derivative * (T.One + t * t));
        }

        /// <summary>The arcsine.</summary>
        public static Dual<T> Asin(Dual<T> x) => new(T.Asin(x.Value), x.Derivative / T.Sqrt(T.One - x.Value * x.Value));

        /// <summary>The arccosine.</summary>
        public static Dual<T> Acos(Dual<T> x) => new(T.Acos(x.Value), -x.Derivative / T.Sqrt(T.One - x.Value * x.Value));

        /// <summary>The arctangent.</summary>
        public static Dual<T> Atan(Dual<T> x) => new(T.Atan(x.Value), x.Derivative / (T.One + x.Value * x.Value));

        /// <summary>The hyperbolic sine.</summary>
        public static Dual<T> Sinh(Dual<T> x) => new(T.Sinh(x.Value), x.Derivative * T.Cosh(x.Value));

        /// <summary>The hyperbolic cosine.</summary>
        public static Dual<T> Cosh(Dual<T> x) => new(T.Cosh(x.Value), x.Derivative * T.Sinh(x.Value));

        /// <summary>The hyperbolic tangent.</summary>
        public static Dual<T> Tanh(Dual<T> x)
        {
            // 1/cosh² rather than 1 − tanh², which cancels badly for large |x|.
            var c = T.Cosh(x.Value);
            return new(T.Tanh(x.Value), x.Derivative / (c * c));
        }

        /// <summary>The inverse hyperbolic sine.</summary>
        public static Dual<T> Asinh(Dual<T> x) => new(T.Asinh(x.Value), x.Derivative / T.Sqrt(x.Value * x.Value + T.One));

        /// <summary>The inverse hyperbolic cosine.</summary>
        public static Dual<T> Acosh(Dual<T> x) => new(T.Acosh(x.Value), x.Derivative / T.Sqrt(x.Value * x.Value - T.One));

        /// <summary>The inverse hyperbolic tangent.</summary>
        public static Dual<T> Atanh(Dual<T> x) => new(T.Atanh(x.Value), x.Derivative / (T.One - x.Value * x.Value));

        /// <summary>The angle of the point (<paramref name="x"/>, <paramref name="y"/>), in (−π, π].</summary>
        public static Dual<T> Atan2(Dual<T> y, Dual<T> x) =>
            new(T.Atan2(y.Value, x.Value),
                (x.Value * y.Derivative - y.Value * x.Derivative) / (x.Value * x.Value + y.Value * y.Value));

        /// <summary>√(x² + y²) without intermediate overflow.</summary>
        public static Dual<T> Hypot(Dual<T> x, Dual<T> y)
        {
            var h = T.Hypot(x.Value, y.Value);
            return new(h, (x.Value * x.Derivative + y.Value * y.Derivative) / h);
        }

        /// <summary><paramref name="x"/> raised to the constant power <paramref name="exponent"/>.</summary>
        public static Dual<T> Pow(Dual<T> x, T exponent) =>
            T.IsZero(exponent)
                ? new(T.One, T.Zero)
                : new(T.Pow(x.Value, exponent), x.Derivative * exponent * T.Pow(x.Value, exponent - T.One));

        /// <summary>
        /// <paramref name="x"/> raised to <paramref name="exponent"/>. When the exponent is constant (derivative zero) this is the
        /// power rule and works for negative bases; otherwise it uses d(xʸ) = xʸ(y′ ln x + y x′/x), which needs x &gt; 0.
        /// </summary>
        public static Dual<T> Pow(Dual<T> x, Dual<T> exponent)
        {
            if (T.IsZero(exponent.Derivative)) return Dual<T>.Pow(x, exponent.Value);
            var p = T.Pow(x.Value, exponent.Value);
            return new(p, p * (exponent.Derivative * T.Log(x.Value) + exponent.Value * x.Derivative / x.Value));
        }
    }
}
