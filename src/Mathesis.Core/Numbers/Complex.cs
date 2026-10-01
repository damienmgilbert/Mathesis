using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Mathesis.Numbers;

/// <summary>
/// A complex number a + bi with components of type <typeparamref name="T"/>.
/// With <c>T = BigRational</c> this is the exact field ℚ(i) of Gaussian rationals; with a floating-point <c>T</c> it
/// approximates ℂ (<see cref="System.Numerics.Complex"/> is <see cref="double"/>-only).
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
/// <remarks>
/// <para>Text form is <c>(re, im)</c>, for example <c>(3/4, -2)</c>; a bare real such as <c>5</c> also parses. Components
/// are always formatted and parsed with the invariant culture.</para>
/// <para>Division uses (a+bi)/(c+di) = ((ac+bd) + (bc−ad)i)/(c²+d²), which is exact for exact <typeparamref name="T"/> but does
/// not guard against intermediate overflow for floating point.</para>
/// <para>The magnitude (<see cref="INumberBase{TSelf}.Abs"/>) needs a square root, so it is available only for floating-point
/// <typeparamref name="T"/>; exact types should use <see cref="NormSquared"/>. Transcendental functions for
/// <c>T : IFloatingPointIeee754&lt;T&gt;</c> are extension members in <see cref="ComplexFunctions"/>.</para>
/// </remarks>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static members are required by INumberBase and specified by docs/design/04-type-system.md.")]
public readonly struct Complex<T> : INumberBase<Complex<T>>, ISignedNumber<Complex<T>>
    where T : INumber<T>
{
    static Complex() => NumberTraits<Complex<T>>.Override(NumberTraits<T>.IsExact);

    /// <summary>Creates a + bi.</summary>
    public Complex(T real, T imaginary)
    {
        Real = real;
        Imaginary = imaginary;
    }

    /// <summary>Creates the real number <paramref name="real"/> + 0i.</summary>
    public Complex(T real) : this(real, T.Zero)
    {
    }

    /// <summary>The real part a.</summary>
    public T Real { get; }

    /// <summary>The imaginary part b.</summary>
    public T Imaginary { get; }

    /// <summary>The imaginary unit i.</summary>
    public static Complex<T> ImaginaryOne => new(T.Zero, T.One);

    /// <inheritdoc />
    public static Complex<T> Zero => new(T.Zero, T.Zero);

    /// <inheritdoc />
    public static Complex<T> One => new(T.One, T.Zero);

    /// <inheritdoc />
    public static Complex<T> NegativeOne => new(-T.One, T.Zero);

    /// <inheritdoc />
    public static int Radix => T.Radix;

    /// <inheritdoc />
    public static Complex<T> AdditiveIdentity => Zero;

    /// <inheritdoc />
    public static Complex<T> MultiplicativeIdentity => One;

    /// <summary>The squared magnitude a² + b², which needs no square root and is exact for exact <typeparamref name="T"/>.</summary>
    public T NormSquared => Real * Real + Imaginary * Imaginary;

    /// <summary>The complex conjugate a − bi.</summary>
    public Complex<T> Conjugate => new(Real, -Imaginary);

    /// <summary>The multiplicative inverse 1/z.</summary>
    /// <exception cref="DivideByZeroException">The value is zero and <typeparamref name="T"/> throws on division by zero (exact types).</exception>
    public Complex<T> Reciprocal => One / this;

    // ----- Arithmetic -----

    /// <inheritdoc />
    public static Complex<T> operator +(Complex<T> left, Complex<T> right) =>
        new(left.Real + right.Real, left.Imaginary + right.Imaginary);

    /// <inheritdoc />
    public static Complex<T> operator -(Complex<T> left, Complex<T> right) =>
        new(left.Real - right.Real, left.Imaginary - right.Imaginary);

    /// <inheritdoc />
    public static Complex<T> operator *(Complex<T> left, Complex<T> right) =>
        new(left.Real * right.Real - left.Imaginary * right.Imaginary,
            left.Real * right.Imaginary + left.Imaginary * right.Real);

    /// <inheritdoc />
    public static Complex<T> operator /(Complex<T> left, Complex<T> right)
    {
        var norm = right.NormSquared;
        return new(
            (left.Real * right.Real + left.Imaginary * right.Imaginary) / norm,
            (left.Imaginary * right.Real - left.Real * right.Imaginary) / norm);
    }

    /// <inheritdoc />
    public static Complex<T> operator -(Complex<T> value) => new(-value.Real, -value.Imaginary);

    /// <inheritdoc />
    public static Complex<T> operator +(Complex<T> value) => value;

    /// <inheritdoc />
    public static Complex<T> operator ++(Complex<T> value) => value + One;

    /// <inheritdoc />
    public static Complex<T> operator --(Complex<T> value) => value - One;

    /// <summary>Converts a real number to a complex number with zero imaginary part.</summary>
    public static implicit operator Complex<T>(T real) => new(real);

    // ----- Equality -----

    /// <inheritdoc />
    public bool Equals(Complex<T> other) => Real == other.Real && Imaginary == other.Imaginary;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Complex<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Real, Imaginary);

    /// <inheritdoc />
    public static bool operator ==(Complex<T> left, Complex<T> right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(Complex<T> left, Complex<T> right) => !left.Equals(right);

    // ----- INumberBase -----

    /// <inheritdoc />
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is an exact type; the magnitude is not closed there. Use <see cref="NormSquared"/>.</exception>
    public static Complex<T> Abs(Complex<T> value)
    {
        if (NumberTraits<T>.IsExact)
        {
            throw new NotSupportedException("The magnitude of an exact complex number needs a square root; use NormSquared.");
        }
        return new(T.CreateChecked(double.Hypot(double.CreateChecked(value.Real), double.CreateChecked(value.Imaginary))));
    }

    /// <inheritdoc />
    public static bool IsCanonical(Complex<T> value) => true;

    /// <inheritdoc />
    public static bool IsComplexNumber(Complex<T> value) => !T.IsZero(value.Imaginary);

    /// <inheritdoc />
    public static bool IsEvenInteger(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsEvenInteger(value.Real);

    /// <inheritdoc />
    public static bool IsFinite(Complex<T> value) => T.IsFinite(value.Real) && T.IsFinite(value.Imaginary);

    /// <inheritdoc />
    public static bool IsImaginaryNumber(Complex<T> value) => T.IsZero(value.Real) && !T.IsZero(value.Imaginary);

    /// <inheritdoc />
    public static bool IsInfinity(Complex<T> value) => T.IsInfinity(value.Real) || T.IsInfinity(value.Imaginary);

    /// <inheritdoc />
    public static bool IsInteger(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsInteger(value.Real);

    /// <inheritdoc />
    public static bool IsNaN(Complex<T> value) => !IsInfinity(value) && (T.IsNaN(value.Real) || T.IsNaN(value.Imaginary));

    /// <inheritdoc />
    public static bool IsNegative(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsNegative(value.Real);

    /// <inheritdoc />
    public static bool IsNegativeInfinity(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsNegativeInfinity(value.Real);

    /// <inheritdoc />
    public static bool IsNormal(Complex<T> value) =>
        !IsZero(value)
        && (T.IsNormal(value.Real) || T.IsZero(value.Real))
        && (T.IsNormal(value.Imaginary) || T.IsZero(value.Imaginary));

    /// <inheritdoc />
    public static bool IsOddInteger(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsOddInteger(value.Real);

    /// <inheritdoc />
    public static bool IsPositive(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsPositive(value.Real);

    /// <inheritdoc />
    public static bool IsPositiveInfinity(Complex<T> value) => T.IsZero(value.Imaginary) && T.IsPositiveInfinity(value.Real);

    /// <inheritdoc />
    public static bool IsRealNumber(Complex<T> value) => T.IsZero(value.Imaginary);

    /// <inheritdoc />
    public static bool IsSubnormal(Complex<T> value) => T.IsSubnormal(value.Real) || T.IsSubnormal(value.Imaginary);

    /// <inheritdoc />
    public static bool IsZero(Complex<T> value) => T.IsZero(value.Real) && T.IsZero(value.Imaginary);

    /// <inheritdoc />
    public static Complex<T> MaxMagnitude(Complex<T> x, Complex<T> y) => x.NormSquared >= y.NormSquared ? x : y;

    /// <inheritdoc />
    public static Complex<T> MaxMagnitudeNumber(Complex<T> x, Complex<T> y) => MaxMagnitude(x, y);

    /// <inheritdoc />
    public static Complex<T> MinMagnitude(Complex<T> x, Complex<T> y) => x.NormSquared <= y.NormSquared ? x : y;

    /// <inheritdoc />
    public static Complex<T> MinMagnitudeNumber(Complex<T> x, Complex<T> y) => MinMagnitude(x, y);

    // ----- Conversions -----

    /// <inheritdoc />
    public static bool TryConvertFromChecked<TOther>(TOther value, out Complex<T> result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, T.CreateChecked, out result);

    /// <inheritdoc />
    public static bool TryConvertFromSaturating<TOther>(TOther value, out Complex<T> result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, T.CreateSaturating, out result);

    /// <inheritdoc />
    public static bool TryConvertFromTruncating<TOther>(TOther value, out Complex<T> result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, T.CreateTruncating, out result);

    private static bool TryConvertFrom<TOther>(TOther value, Func<TOther, T> convert, out Complex<T> result)
        where TOther : INumberBase<TOther>
    {
        if (value is Complex<T> same)
        {
            result = same;
            return true;
        }
        try
        {
            result = new Complex<T>(convert(value));
            return true;
        }
        catch (NotSupportedException)
        {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    public static bool TryConvertToChecked<TOther>(Complex<T> value, out TOther result) where TOther : INumberBase<TOther>
    {
        if (value is TOther same)
        {
            result = same;
            return true;
        }
        if (!T.IsZero(value.Imaginary)) throw new OverflowException("A complex number with a non-zero imaginary part has no real value.");
        return TryConvertTo(value, TOther.CreateChecked, out result);
    }

    /// <inheritdoc />
    public static bool TryConvertToSaturating<TOther>(Complex<T> value, out TOther result) where TOther : INumberBase<TOther> =>
        value is TOther same ? Assign(same, out result) : TryConvertTo(value, TOther.CreateSaturating, out result);

    /// <inheritdoc />
    public static bool TryConvertToTruncating<TOther>(Complex<T> value, out TOther result) where TOther : INumberBase<TOther> =>
        value is TOther same ? Assign(same, out result) : TryConvertTo(value, TOther.CreateTruncating, out result);

    private static bool Assign<TOther>(TOther value, out TOther result)
    {
        result = value;
        return true;
    }

    private static bool TryConvertTo<TOther>(Complex<T> value, Func<T, TOther> convert, out TOther result)
        where TOther : INumberBase<TOther>
    {
        try
        {
            result = convert(value.Real);
            return true;
        }
        catch (NotSupportedException)
        {
            result = default!;
            return false;
        }
    }

    // ----- Formatting and parsing -----

    /// <summary>Formats as <c>(re, im)</c> using the invariant culture.</summary>
    public override string ToString() => ToString(null, null);

    /// <summary>Formats as <c>(re, im)</c>, passing <paramref name="format"/> to each component; the provider is ignored.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"({Real.ToString(format, CultureInfo.InvariantCulture)}, {Imaginary.ToString(format, CultureInfo.InvariantCulture)})";

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

    /// <summary>Parses <c>(re, im)</c> or a bare real number.</summary>
    /// <exception cref="FormatException">The text is not a complex number.</exception>
    public static Complex<T> Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does; <paramref name="style"/> is applied to each component.</summary>
    public static Complex<T> Parse(string s, NumberStyles style, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), style, provider);
    }

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does.</summary>
    public static Complex<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null) =>
        TryParse(s, NumberStyles.Float, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid complex number.");

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does; <paramref name="style"/> is applied to each component.</summary>
    public static Complex<T> Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) =>
        TryParse(s, style, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid complex number.");

    /// <summary>Parses UTF-8 text.</summary>
    public static Complex<T> Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(Encoding.UTF8.GetString(utf8Text), provider);

    /// <summary>Parses UTF-8 text.</summary>
    public static Complex<T> Parse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider) =>
        Parse(Encoding.UTF8.GetString(utf8Text), style, provider);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Complex<T> result) =>
        TryParse(s, NumberStyles.Float, provider, out result);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out Complex<T> result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), style, provider, out result);
    }

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Complex<T> result) =>
        TryParse(s, NumberStyles.Float, provider, out result);

    /// <summary>Tries to parse UTF-8 text.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Complex<T> result) =>
        TryParse(Encoding.UTF8.GetString(utf8Text), NumberStyles.Float, provider, out result);

    /// <summary>Tries to parse UTF-8 text.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider, out Complex<T> result) =>
        TryParse(Encoding.UTF8.GetString(utf8Text), style, provider, out result);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Complex<T> result)
    {
        result = default;
        s = s.Trim();
        if (s.IsEmpty) return false;

        if (s[0] != '(')
        {
            if (!T.TryParse(s, style, CultureInfo.InvariantCulture, out var real)) return false;
            result = new Complex<T>(real);
            return true;
        }
        if (s[^1] != ')') return false;
        var inner = s[1..^1];
        var comma = inner.IndexOf(',');
        if (comma < 0
            || !T.TryParse(inner[..comma].Trim(), style, CultureInfo.InvariantCulture, out var re)
            || !T.TryParse(inner[(comma + 1)..].Trim(), style, CultureInfo.InvariantCulture, out var im))
        {
            return false;
        }
        result = new Complex<T>(re, im);
        return true;
    }
}

/// <summary>Transcendental functions of <see cref="Complex{T}"/> for floating-point component types (principal branches).</summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static extension members on Complex<T>/Dual<T> are the design in docs/design/04-type-system.md.")]
public static class ComplexFunctions
{
    extension<T>(Complex<T> z) where T : IFloatingPointIeee754<T>
    {
        /// <summary>The magnitude √(a² + b²), computed without intermediate overflow.</summary>
        public T Magnitude => T.Hypot(z.Real, z.Imaginary);

        /// <summary>The argument (phase) in (−π, π].</summary>
        public T Phase => T.Atan2(z.Imaginary, z.Real);
    }

    extension<T>(Complex<T>) where T : IFloatingPointIeee754<T>
    {
        /// <summary>The number with magnitude <paramref name="magnitude"/> and argument <paramref name="phase"/>.</summary>
        public static Complex<T> FromPolar(T magnitude, T phase) =>
            new(magnitude * T.Cos(phase), magnitude * T.Sin(phase));

        /// <summary>e<sup>z</sup>.</summary>
        public static Complex<T> Exp(Complex<T> z) => Complex<T>.FromPolar(T.Exp(z.Real), z.Imaginary);

        /// <summary>The principal natural logarithm ln|z| + i·arg z. The logarithm of zero is −∞ + 0i.</summary>
        public static Complex<T> Log(Complex<T> z) => new(T.Log(z.Magnitude), z.Phase);

        /// <summary>The principal square root (non-negative real part).</summary>
        public static Complex<T> Sqrt(Complex<T> z) =>
            Complex<T>.FromPolar(T.Sqrt(z.Magnitude), z.Phase / (T.One + T.One));
    }
}
