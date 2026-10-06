using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mathesis.Numbers;

// The INumberBase, IFloatingPoint and IFloatingPointIeee754 members that are not calculus: constants, classification of the value (decision
// ADR-19: the gradient is not consulted), parsing and conversion (a parsed or converted number is a constant), and the binary-layout queries.
public readonly partial struct Jet<T>
{
    /// <inheritdoc />
    public static Jet<T> Zero => default;

    /// <inheritdoc />
    public static Jet<T> One => new(T.One);

    /// <inheritdoc />
    public static Jet<T> NegativeOne => new(T.NegativeOne);

    /// <inheritdoc />
    public static Jet<T> AdditiveIdentity => default;

    /// <inheritdoc />
    public static Jet<T> MultiplicativeIdentity => new(T.One);

    /// <inheritdoc />
    public static int Radix => T.Radix;

    /// <inheritdoc />
    public static Jet<T> E => new(T.E);

    /// <inheritdoc />
    public static Jet<T> Pi => new(T.Pi);

    /// <inheritdoc />
    public static Jet<T> Tau => new(T.Tau);

    /// <inheritdoc />
    public static Jet<T> Epsilon => new(T.Epsilon);

    /// <inheritdoc />
    public static Jet<T> NaN => new(T.NaN);

    /// <inheritdoc />
    public static Jet<T> PositiveInfinity => new(T.PositiveInfinity);

    /// <inheritdoc />
    public static Jet<T> NegativeInfinity => new(T.NegativeInfinity);

    /// <inheritdoc />
    public static Jet<T> NegativeZero => new(T.NegativeZero);

    /// <inheritdoc />
    public static bool IsCanonical(Jet<T> value) => T.IsCanonical(value._value);

    /// <inheritdoc />
    public static bool IsComplexNumber(Jet<T> value) => T.IsComplexNumber(value._value);

    /// <inheritdoc />
    public static bool IsEvenInteger(Jet<T> value) => T.IsEvenInteger(value._value);

    /// <summary>Whether the value is finite; the gradient is not consulted (see <see cref="IsGradientFinite"/>).</summary>
    public static bool IsFinite(Jet<T> value) => T.IsFinite(value._value);

    /// <inheritdoc />
    public static bool IsImaginaryNumber(Jet<T> value) => T.IsImaginaryNumber(value._value);

    /// <inheritdoc />
    public static bool IsInfinity(Jet<T> value) => T.IsInfinity(value._value);

    /// <inheritdoc />
    public static bool IsInteger(Jet<T> value) => T.IsInteger(value._value);

    /// <summary>Whether the value is NaN; the gradient is not consulted.</summary>
    public static bool IsNaN(Jet<T> value) => T.IsNaN(value._value);

    /// <inheritdoc />
    public static bool IsNegative(Jet<T> value) => T.IsNegative(value._value);

    /// <inheritdoc />
    public static bool IsNegativeInfinity(Jet<T> value) => T.IsNegativeInfinity(value._value);

    /// <inheritdoc />
    public static bool IsNormal(Jet<T> value) => T.IsNormal(value._value);

    /// <inheritdoc />
    public static bool IsOddInteger(Jet<T> value) => T.IsOddInteger(value._value);

    /// <inheritdoc />
    public static bool IsPositive(Jet<T> value) => T.IsPositive(value._value);

    /// <inheritdoc />
    public static bool IsPositiveInfinity(Jet<T> value) => T.IsPositiveInfinity(value._value);

    /// <inheritdoc />
    public static bool IsRealNumber(Jet<T> value) => T.IsRealNumber(value._value);

    /// <inheritdoc />
    public static bool IsSubnormal(Jet<T> value) => T.IsSubnormal(value._value);

    /// <inheritdoc />
    public static bool IsZero(Jet<T> value) => T.IsZero(value._value);

    /// <inheritdoc />
    public int GetExponentByteCount() => _value.GetExponentByteCount();

    /// <inheritdoc />
    public int GetExponentShortestBitLength() => _value.GetExponentShortestBitLength();

    /// <inheritdoc />
    public int GetSignificandBitLength() => _value.GetSignificandBitLength();

    /// <inheritdoc />
    public int GetSignificandByteCount() => _value.GetSignificandByteCount();

    /// <inheritdoc />
    public bool TryWriteExponentBigEndian(Span<byte> destination, out int bytesWritten) => _value.TryWriteExponentBigEndian(destination, out bytesWritten);

    /// <inheritdoc />
    public bool TryWriteExponentLittleEndian(Span<byte> destination, out int bytesWritten) => _value.TryWriteExponentLittleEndian(destination, out bytesWritten);

    /// <inheritdoc />
    public bool TryWriteSignificandBigEndian(Span<byte> destination, out int bytesWritten) => _value.TryWriteSignificandBigEndian(destination, out bytesWritten);

    /// <inheritdoc />
    public bool TryWriteSignificandLittleEndian(Span<byte> destination, out int bytesWritten) => _value.TryWriteSignificandLittleEndian(destination, out bytesWritten);

    /// <summary>Parses <paramref name="s"/> as a <typeparamref name="T"/> and returns it as a constant.</summary>
    public static Jet<T> Parse(string s, IFormatProvider? provider) => new(T.Parse(s, provider));

    /// <inheritdoc />
    public static Jet<T> Parse(string s, NumberStyles style, IFormatProvider? provider) => new(T.Parse(s, style, provider));

    /// <inheritdoc />
    public static Jet<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(T.Parse(s, provider));

    /// <inheritdoc />
    public static Jet<T> Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => new(T.Parse(s, style, provider));

    /// <inheritdoc />
    public static Jet<T> Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => new(T.Parse(utf8Text, provider));

    /// <inheritdoc />
    public static Jet<T> Parse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider) => new(T.Parse(utf8Text, style, provider));

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out Jet<T> result) => Wrap(T.TryParse(s, provider, out var v), v, out result);

    /// <inheritdoc />
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out Jet<T> result) => Wrap(T.TryParse(s, style, provider, out var v), v, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Jet<T> result) => Wrap(T.TryParse(s, provider, out var v), v, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Jet<T> result) => Wrap(T.TryParse(s, style, provider, out var v), v, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Jet<T> result) => Wrap(T.TryParse(utf8Text, provider, out var v), v, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider, out Jet<T> result) => Wrap(T.TryParse(utf8Text, style, provider, out var v), v, out result);

    private static bool Wrap(bool ok, T? value, out Jet<T> result)
    {
        result = ok ? new(value!) : default;
        return ok;
    }

    // Conversions from and to other number types go through the value; a converted number is a constant, and converting to another type
    // drops the gradient. A target or source that T cannot convert reports false, as the INumberBase contract asks; a value out of range
    // throws OverflowException from the checked conversions, as the built-in types do.

    /// <inheritdoc />
    static bool INumberBase<Jet<T>>.TryConvertFromChecked<TOther>(TOther value, out Jet<T> result)
    {
        if (typeof(TOther) == typeof(Jet<T>))
        {
            result = Unsafe.As<TOther, Jet<T>>(ref value);
            return true;
        }
        try
        {
            result = new(T.CreateChecked(value));
            return true;
        }
        catch (NotSupportedException)
        {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    static bool INumberBase<Jet<T>>.TryConvertFromSaturating<TOther>(TOther value, out Jet<T> result)
    {
        if (typeof(TOther) == typeof(Jet<T>))
        {
            result = Unsafe.As<TOther, Jet<T>>(ref value);
            return true;
        }
        try
        {
            result = new(T.CreateSaturating(value));
            return true;
        }
        catch (NotSupportedException)
        {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    static bool INumberBase<Jet<T>>.TryConvertFromTruncating<TOther>(TOther value, out Jet<T> result)
    {
        if (typeof(TOther) == typeof(Jet<T>))
        {
            result = Unsafe.As<TOther, Jet<T>>(ref value);
            return true;
        }
        try
        {
            result = new(T.CreateTruncating(value));
            return true;
        }
        catch (NotSupportedException)
        {
            result = default;
            return false;
        }
    }

    /// <inheritdoc />
    static bool INumberBase<Jet<T>>.TryConvertToChecked<TOther>(Jet<T> value, out TOther result)
    {
        try
        {
            result = TOther.CreateChecked(value._value);
            return true;
        }
        catch (NotSupportedException)
        {
            result = default!;
            return false;
        }
    }

    /// <inheritdoc />
    static bool INumberBase<Jet<T>>.TryConvertToSaturating<TOther>(Jet<T> value, out TOther result)
    {
        try
        {
            result = TOther.CreateSaturating(value._value);
            return true;
        }
        catch (NotSupportedException)
        {
            result = default!;
            return false;
        }
    }

    /// <inheritdoc />
    static bool INumberBase<Jet<T>>.TryConvertToTruncating<TOther>(Jet<T> value, out TOther result)
    {
        try
        {
            result = TOther.CreateTruncating(value._value);
            return true;
        }
        catch (NotSupportedException)
        {
            result = default!;
            return false;
        }
    }
}
