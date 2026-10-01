using System.Globalization;
using System.Numerics;
using System.Text;

namespace Mathesis.Numbers;

/// <summary>
/// An exact rational number p/q over <see cref="BigInteger"/>, always normalized: gcd(p, q) = 1 and q &gt; 0.
/// </summary>
/// <remarks>
/// <para>The default value is 0. Text forms accepted by <see cref="Parse(string, IFormatProvider?)"/>: <c>3/4</c>, <c>-2</c>,
/// <c>0.125</c> (becomes 1/8), repeating decimals <c>0.1(6)</c> (1/6) and decimals with an exponent <c>1.5e-3</c>.
/// Decimal points are always <c>.</c>; the culture is ignored so output is stable.</para>
/// <para>Arithmetic that divides by zero throws <see cref="DivideByZeroException"/>, like the other BCL number types.</para>
/// </remarks>
public readonly struct BigRational : INumber<BigRational>, ISignedNumber<BigRational>, IExactNumber
{
    private const int MaxExponentMagnitude = 100_000;

    private readonly BigInteger _numerator;

    // Denominator, or 0 for "1", so that default(BigRational) is 0/1.
    private readonly BigInteger _denominator;

    private BigRational(BigInteger numerator, BigInteger denominator)
    {
        _numerator = numerator;
        _denominator = denominator.IsOne ? BigInteger.Zero : denominator;
    }

    /// <summary>Creates the integer <paramref name="value"/>.</summary>
    public BigRational(BigInteger value) : this(value, BigInteger.One)
    {
    }

    /// <summary>Creates the normalized fraction <paramref name="numerator"/>/<paramref name="denominator"/>.</summary>
    /// <exception cref="DivideByZeroException"><paramref name="denominator"/> is zero.</exception>
    public static BigRational Create(BigInteger numerator, BigInteger denominator) => Normalize(numerator, denominator);

    private static BigRational Normalize(BigInteger n, BigInteger d)
    {
        if (d.IsZero) throw new DivideByZeroException("The denominator of a rational number cannot be zero.");
        if (d.Sign < 0)
        {
            n = -n;
            d = -d;
        }
        var g = BigInteger.GreatestCommonDivisor(n, d);
        if (!g.IsOne)
        {
            n /= g;
            d /= g;
        }
        return new BigRational(n, d);
    }

    // ----- Parts -----

    /// <summary>The numerator; carries the sign.</summary>
    public BigInteger Numerator => _numerator;

    /// <summary>The denominator; always positive.</summary>
    public BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;

    /// <summary>Whether the value is an integer (denominator 1).</summary>
    public bool IsInteger => _denominator.IsZero;

    /// <summary>-1, 0 or 1.</summary>
    public int Sign => _numerator.Sign;

    // ----- Constants -----

    /// <summary>0.</summary>
    public static BigRational Zero => default;

    /// <summary>1.</summary>
    public static BigRational One => new(BigInteger.One, BigInteger.One);

    /// <summary>-1.</summary>
    public static BigRational NegativeOne => new(BigInteger.MinusOne, BigInteger.One);

    /// <summary>The radix of the representation, 2 as for <see cref="BigInteger"/>.</summary>
    public static int Radix => 2;

    /// <inheritdoc />
    public static BigRational AdditiveIdentity => Zero;

    /// <inheritdoc />
    public static BigRational MultiplicativeIdentity => One;

    // ----- Arithmetic -----

    /// <inheritdoc />
    public static BigRational operator +(BigRational left, BigRational right) =>
        left._denominator.IsZero && right._denominator.IsZero
            ? new BigRational(left._numerator + right._numerator, BigInteger.One)
            : Normalize(left._numerator * right.Denominator + right._numerator * left.Denominator, left.Denominator * right.Denominator);

    /// <inheritdoc />
    public static BigRational operator -(BigRational left, BigRational right) =>
        left._denominator.IsZero && right._denominator.IsZero
            ? new BigRational(left._numerator - right._numerator, BigInteger.One)
            : Normalize(left._numerator * right.Denominator - right._numerator * left.Denominator, left.Denominator * right.Denominator);

    /// <inheritdoc />
    public static BigRational operator *(BigRational left, BigRational right) =>
        left._denominator.IsZero && right._denominator.IsZero
            ? new BigRational(left._numerator * right._numerator, BigInteger.One)
            : Normalize(left._numerator * right._numerator, left.Denominator * right.Denominator);

    /// <inheritdoc />
    /// <exception cref="DivideByZeroException"><paramref name="right"/> is zero.</exception>
    public static BigRational operator /(BigRational left, BigRational right)
    {
        if (right._numerator.IsZero) throw new DivideByZeroException();
        return Normalize(left._numerator * right.Denominator, left.Denominator * right._numerator);
    }

    /// <summary>The remainder of truncated division: <c>left - right * Truncate(left / right)</c>, with the sign of <paramref name="left"/>.</summary>
    /// <exception cref="DivideByZeroException"><paramref name="right"/> is zero.</exception>
    public static BigRational operator %(BigRational left, BigRational right)
    {
        if (right._numerator.IsZero) throw new DivideByZeroException();
        var l = left.Denominator;
        var r = right.Denominator;
        return Normalize(BigInteger.Remainder(left._numerator * r, right._numerator * l), l * r);
    }

    /// <inheritdoc />
    public static BigRational operator -(BigRational value) => new(-value._numerator, value._denominator.IsZero ? BigInteger.One : value._denominator);

    /// <inheritdoc />
    public static BigRational operator +(BigRational value) => value;

    /// <inheritdoc />
    public static BigRational operator ++(BigRational value) => value + One;

    /// <inheritdoc />
    public static BigRational operator --(BigRational value) => value - One;

    /// <summary>The multiplicative inverse 1/<paramref name="value"/>.</summary>
    /// <exception cref="DivideByZeroException"><paramref name="value"/> is zero.</exception>
    public static BigRational Reciprocal(BigRational value)
    {
        if (value._numerator.IsZero) throw new DivideByZeroException();
        return value._numerator.Sign < 0
            ? new BigRational(-value.Denominator, -value._numerator)
            : new BigRational(value.Denominator, value._numerator);
    }

    /// <summary>Raises <paramref name="value"/> to an integer power. 0⁰ is 1 (see catalog convention <c>conv.zero-to-the-zero</c>).</summary>
    /// <exception cref="DivideByZeroException"><paramref name="value"/> is zero and <paramref name="exponent"/> is negative.</exception>
    public static BigRational Pow(BigRational value, int exponent)
    {
        if (exponent == 0) return One;
        var baseValue = exponent < 0 ? Reciprocal(value) : value;
        var e = (uint)Math.Abs((long)exponent);
        return new BigRational(
            BigInteger.Pow(baseValue._numerator, (int)e),
            BigInteger.Pow(baseValue.Denominator, (int)e));
    }

    /// <summary>The absolute value.</summary>
    public static BigRational Abs(BigRational value) => value.Sign < 0 ? -value : value;

    /// <summary>The largest integer not greater than <paramref name="value"/>.</summary>
    public static BigInteger Floor(BigRational value)
    {
        if (value.IsInteger) return value._numerator;
        var q = BigInteger.DivRem(value._numerator, value._denominator, out var r);
        return r.Sign < 0 ? q - BigInteger.One : q;
    }

    /// <summary>The smallest integer not less than <paramref name="value"/>.</summary>
    public static BigInteger Ceiling(BigRational value)
    {
        if (value.IsInteger) return value._numerator;
        var q = BigInteger.DivRem(value._numerator, value._denominator, out var r);
        return r.Sign > 0 ? q + BigInteger.One : q;
    }

    /// <summary>The integer part, rounding toward zero.</summary>
    public static BigInteger Truncate(BigRational value) =>
        value.IsInteger ? value._numerator : BigInteger.Divide(value._numerator, value._denominator);

    // ----- Comparison and equality -----

    /// <inheritdoc />
    public int CompareTo(BigRational other) =>
        _denominator.IsZero && other._denominator.IsZero
            ? _numerator.CompareTo(other._numerator)
            : (_numerator * other.Denominator).CompareTo(other._numerator * Denominator);

    /// <inheritdoc />
    public int CompareTo(object? obj) =>
        obj is null ? 1 : obj is BigRational other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(BigRational)}.", nameof(obj));

    /// <inheritdoc />
    public bool Equals(BigRational other) => _numerator == other._numerator && _denominator == other._denominator;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BigRational other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_numerator, _denominator);

    /// <inheritdoc />
    public static bool operator ==(BigRational left, BigRational right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(BigRational left, BigRational right) => !left.Equals(right);

    /// <inheritdoc />
    public static bool operator <(BigRational left, BigRational right) => left.CompareTo(right) < 0;

    /// <inheritdoc />
    public static bool operator >(BigRational left, BigRational right) => left.CompareTo(right) > 0;

    /// <inheritdoc />
    public static bool operator <=(BigRational left, BigRational right) => left.CompareTo(right) <= 0;

    /// <inheritdoc />
    public static bool operator >=(BigRational left, BigRational right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public static BigRational Max(BigRational x, BigRational y) => x >= y ? x : y;

    /// <inheritdoc />
    public static BigRational Min(BigRational x, BigRational y) => x <= y ? x : y;

    /// <inheritdoc />
    public static BigRational MaxMagnitude(BigRational x, BigRational y) => Abs(x) >= Abs(y) ? x : y;

    /// <inheritdoc />
    public static BigRational MinMagnitude(BigRational x, BigRational y) => Abs(x) <= Abs(y) ? x : y;

    /// <inheritdoc />
    public static BigRational MaxMagnitudeNumber(BigRational x, BigRational y) => MaxMagnitude(x, y);

    /// <inheritdoc />
    public static BigRational MinMagnitudeNumber(BigRational x, BigRational y) => MinMagnitude(x, y);

    /// <inheritdoc />
    public static BigRational MaxNumber(BigRational x, BigRational y) => Max(x, y);

    /// <inheritdoc />
    public static BigRational MinNumber(BigRational x, BigRational y) => Min(x, y);

    /// <inheritdoc />
    public static BigRational Clamp(BigRational value, BigRational min, BigRational max)
    {
        if (min > max) throw new ArgumentException("min must not be greater than max.", nameof(min));
        return value < min ? min : value > max ? max : value;
    }

    /// <inheritdoc />
    public static BigRational CopySign(BigRational value, BigRational sign) =>
        (value.Sign < 0) == (sign.Sign < 0) || value.Sign == 0 ? value : -value;

    static int INumber<BigRational>.Sign(BigRational value) => value.Sign;

    // ----- INumberBase predicates -----

    /// <inheritdoc />
    public static bool IsCanonical(BigRational value) => true;

    /// <inheritdoc />
    public static bool IsComplexNumber(BigRational value) => false;

    /// <inheritdoc />
    public static bool IsEvenInteger(BigRational value) => value.IsInteger && value._numerator.IsEven;

    /// <inheritdoc />
    public static bool IsFinite(BigRational value) => true;

    /// <inheritdoc />
    public static bool IsImaginaryNumber(BigRational value) => false;

    /// <inheritdoc />
    public static bool IsInfinity(BigRational value) => false;

    static bool INumberBase<BigRational>.IsInteger(BigRational value) => value.IsInteger;

    /// <inheritdoc />
    public static bool IsNaN(BigRational value) => false;

    /// <inheritdoc />
    public static bool IsNegative(BigRational value) => value.Sign < 0;

    /// <inheritdoc />
    public static bool IsNegativeInfinity(BigRational value) => false;

    /// <inheritdoc />
    public static bool IsNormal(BigRational value) => value.Sign != 0;

    /// <inheritdoc />
    public static bool IsOddInteger(BigRational value) => value.IsInteger && !value._numerator.IsEven;

    /// <inheritdoc />
    public static bool IsPositive(BigRational value) => value.Sign >= 0;

    /// <inheritdoc />
    public static bool IsPositiveInfinity(BigRational value) => false;

    /// <inheritdoc />
    public static bool IsRealNumber(BigRational value) => true;

    /// <inheritdoc />
    public static bool IsSubnormal(BigRational value) => false;

    /// <inheritdoc />
    public static bool IsZero(BigRational value) => value._numerator.IsZero;

    // ----- Conversions from and to other types -----

    /// <summary>Converts an integer to a rational.</summary>
    public static implicit operator BigRational(BigInteger value) => new(value, BigInteger.One);

    /// <summary>Converts an integer to a rational.</summary>
    public static implicit operator BigRational(int value) => new(value, BigInteger.One);

    /// <summary>Converts an integer to a rational.</summary>
    public static implicit operator BigRational(long value) => new(value, BigInteger.One);

    /// <summary>Converts an integer to a rational.</summary>
    public static implicit operator BigRational(uint value) => new(value, BigInteger.One);

    /// <summary>Converts an integer to a rational.</summary>
    public static implicit operator BigRational(ulong value) => new(value, BigInteger.One);

    /// <summary>Converts a <see cref="decimal"/> exactly.</summary>
    public static implicit operator BigRational(decimal value) => FromDecimal(value);

    /// <summary>Converts to the nearest <see cref="double"/> (see <see cref="ToDouble"/>).</summary>
    public static explicit operator double(BigRational value) => value.ToDouble();

    /// <summary>Converts to the nearest <see cref="float"/>.</summary>
    public static explicit operator float(BigRational value) => (float)value.ToDouble();

    /// <summary>Converts to <see cref="decimal"/> (see <see cref="ToDecimal"/>).</summary>
    public static explicit operator decimal(BigRational value) => value.ToDecimal();

    /// <summary>Truncates toward zero.</summary>
    public static explicit operator BigInteger(BigRational value) => Truncate(value);

    /// <summary>Converts a finite <see cref="double"/> exactly: every double is a dyadic rational.</summary>
    /// <exception cref="OverflowException"><paramref name="value"/> is NaN or infinite.</exception>
    public static explicit operator BigRational(double value) => FromDouble(value);

    /// <summary>Converts a finite <see cref="double"/> exactly: every double is a dyadic rational.</summary>
    /// <exception cref="OverflowException"><paramref name="value"/> is NaN or infinite.</exception>
    public static BigRational FromDouble(double value)
    {
        if (!double.IsFinite(value)) throw new OverflowException("NaN and infinity have no rational value.");
        var bits = BitConverter.DoubleToInt64Bits(value);
        var negative = bits < 0;
        var biased = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        long mantissa;
        int exponent;
        if (biased == 0)
        {
            mantissa = fraction;
            exponent = -1074;
        }
        else
        {
            mantissa = fraction | (1L << 52);
            exponent = biased - 1075;
        }
        if (mantissa == 0) return Zero;
        var trailing = BitOperations.TrailingZeroCount(mantissa);
        mantissa >>= trailing;
        exponent += trailing;
        BigInteger n = negative ? -mantissa : mantissa;
        return exponent >= 0
            ? new BigRational(n << exponent, BigInteger.One)
            : new BigRational(n, BigInteger.One << -exponent);
    }

    /// <summary>
    /// Converts the shortest decimal string that round-trips <paramref name="value"/> to a rational, so 0.1 becomes 1/10
    /// rather than the exact binary value (use <see cref="FromDouble"/> for that).
    /// </summary>
    /// <exception cref="OverflowException"><paramref name="value"/> is NaN or infinite.</exception>
    public static BigRational FromShortestDecimal(double value)
    {
        if (!double.IsFinite(value)) throw new OverflowException("NaN and infinity have no rational value.");
        return Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>Converts a <see cref="decimal"/> exactly.</summary>
    public static BigRational FromDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        BigInteger unscaled = (UInt128)(uint)bits[2] << 64 | (UInt128)(uint)bits[1] << 32 | (uint)bits[0];
        var scale = (bits[3] >> 16) & 0xFF;
        if (bits[3] < 0) unscaled = -unscaled;
        return Normalize(unscaled, BigInteger.Pow(10, scale));
    }

    /// <summary>
    /// The <see cref="double"/> nearest to this value (round half to even), so the result is within half an ulp of the
    /// exact value. Values beyond the double range become infinity.
    /// </summary>
    public double ToDouble()
    {
        if (_numerator.IsZero) return 0.0;
        var negative = _numerator.Sign < 0;
        var n = BigInteger.Abs(_numerator);
        var d = Denominator;

        // Scale so the integer quotient has at least 55 significant bits, then round it to 53 with a sticky bit.
        var shift = 55 - (int)(n.GetBitLength() - d.GetBitLength());
        var scaledN = shift >= 0 ? n << shift : n;
        var scaledD = shift >= 0 ? d : d << -shift;
        var q = BigInteger.DivRem(scaledN, scaledD, out var remainder);
        if (!remainder.IsZero) q |= BigInteger.One;

        var drop = (int)q.GetBitLength() - 53;
        var mantissa = q >> drop;
        var rest = q - (mantissa << drop);
        var half = BigInteger.One << (drop - 1);
        if (rest > half || (rest == half && !mantissa.IsEven)) mantissa += BigInteger.One;

        var result = Math.ScaleB((double)(long)mantissa, drop - shift);
        return negative ? -result : result;
    }

    /// <summary>
    /// Converts to <see cref="decimal"/>, rounding to the nearest value with up to 28 fractional digits (half to even).
    /// </summary>
    /// <exception cref="OverflowException">The value is outside the range of <see cref="decimal"/>.</exception>
    public decimal ToDecimal()
    {
        var limit = new BigInteger(decimal.MaxValue);
        var negative = _numerator.Sign < 0;
        var n = BigInteger.Abs(_numerator);
        var d = Denominator;
        for (var scale = 28; scale >= 0; scale--)
        {
            var scaled = n * BigInteger.Pow(10, scale);
            var q = BigInteger.DivRem(scaled, d, out var r);
            var twice = r << 1;
            if (twice > d || (twice == d && !q.IsEven)) q += BigInteger.One;
            if (q > limit) continue;
            var lo = (int)(uint)(q & uint.MaxValue);
            var mid = (int)(uint)((q >> 32) & uint.MaxValue);
            var hi = (int)(uint)((q >> 64) & uint.MaxValue);
            return new decimal(lo, mid, hi, negative && !q.IsZero, (byte)scale);
        }
        throw new OverflowException("The value is outside the range of decimal.");
    }

    /// <summary>
    /// The rational with denominator at most <paramref name="maxDenominator"/> closest to <paramref name="value"/>,
    /// found with continued-fraction convergents and the best semiconvergent (Stern–Brocot tree; Hardy and Wright, ch. X).
    /// Ties go to the smaller denominator.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDenominator"/> is less than 1.</exception>
    public static BigRational Approximate(BigRational value, BigInteger maxDenominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDenominator, BigInteger.One);
        if (value.Denominator <= maxDenominator) return value;

        BigInteger p0 = 0, q0 = 1, p1 = 1, q1 = 0;
        var n = value._numerator;
        var d = value.Denominator;
        while (true)
        {
            var a = FloorDiv(n, d);
            var q2 = q0 + a * q1;
            if (q2 > maxDenominator) break;
            (p0, q0, p1, q1) = (p1, q1, p0 + a * p1, q2);
            (n, d) = (d, n - a * d);
        }

        var k = (maxDenominator - q0) / q1;
        var bound1 = Normalize(p0 + k * p1, q0 + k * q1);
        var bound2 = Normalize(p1, q1);
        return Abs(bound2 - value) <= Abs(bound1 - value) ? bound2 : bound1;
    }

    /// <summary>The closest rational to the double <paramref name="value"/> with denominator at most <paramref name="maxDenominator"/>.</summary>
    public static BigRational Approximate(double value, BigInteger maxDenominator) => Approximate(FromDouble(value), maxDenominator);

    private static BigInteger FloorDiv(BigInteger n, BigInteger d)
    {
        var q = BigInteger.DivRem(n, d, out var r);
        return r.Sign != 0 && (r.Sign < 0) != (d.Sign < 0) ? q - BigInteger.One : q;
    }

    // ----- INumberBase conversions -----

    /// <inheritdoc />
    public static bool TryConvertFromChecked<TOther>(TOther value, out BigRational result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, out result);

    /// <inheritdoc />
    public static bool TryConvertFromSaturating<TOther>(TOther value, out BigRational result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, out result);

    /// <inheritdoc />
    public static bool TryConvertFromTruncating<TOther>(TOther value, out BigRational result) where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, out result);

    private static bool TryConvertFrom<TOther>(TOther value, out BigRational result) where TOther : INumberBase<TOther>
    {
        if (value is BigRational r)
        {
            result = r;
            return true;
        }
        if (value is BigInteger bi)
        {
            result = new BigRational(bi, BigInteger.One);
            return true;
        }
        if (value is decimal m)
        {
            result = FromDecimal(m);
            return true;
        }
        if (value is double or float or Half)
        {
            var asDouble = double.CreateChecked(value);
            if (!double.IsFinite(asDouble)) throw new OverflowException("NaN and infinity have no rational value.");
            result = FromDouble(asDouble);
            return true;
        }
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or Int128 or UInt128 or nint or nuint or char)
        {
            result = new BigRational(BigInteger.CreateChecked(value), BigInteger.One);
            return true;
        }
        result = default;
        return false;
    }

    /// <inheritdoc />
    public static bool TryConvertToChecked<TOther>(BigRational value, out TOther result) where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(BigRational)) { result = (TOther)(object)value; return true; }
        if (IsIntegerTarget<TOther>()) { result = TOther.CreateChecked(Truncate(value)); return true; }
        if (typeof(TOther) == typeof(decimal)) { result = TOther.CreateChecked(value.ToDecimal()); return true; }
        if (typeof(TOther) == typeof(double) || typeof(TOther) == typeof(float) || typeof(TOther) == typeof(Half))
        {
            result = TOther.CreateChecked(value.ToDouble());
            return true;
        }
        result = default!;
        return false;
    }

    /// <inheritdoc />
    public static bool TryConvertToSaturating<TOther>(BigRational value, out TOther result) where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(BigRational)) { result = (TOther)(object)value; return true; }
        if (IsIntegerTarget<TOther>()) { result = TOther.CreateSaturating(Truncate(value)); return true; }
        if (typeof(TOther) == typeof(decimal))
        {
            result = TOther.CreateSaturating(TryDecimalSaturating(value));
            return true;
        }
        if (typeof(TOther) == typeof(double) || typeof(TOther) == typeof(float) || typeof(TOther) == typeof(Half))
        {
            result = TOther.CreateSaturating(value.ToDouble());
            return true;
        }
        result = default!;
        return false;
    }

    /// <inheritdoc />
    public static bool TryConvertToTruncating<TOther>(BigRational value, out TOther result) where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(BigRational)) { result = (TOther)(object)value; return true; }
        if (IsIntegerTarget<TOther>()) { result = TOther.CreateTruncating(Truncate(value)); return true; }
        if (typeof(TOther) == typeof(decimal))
        {
            result = TOther.CreateTruncating(TryDecimalSaturating(value));
            return true;
        }
        if (typeof(TOther) == typeof(double) || typeof(TOther) == typeof(float) || typeof(TOther) == typeof(Half))
        {
            result = TOther.CreateTruncating(value.ToDouble());
            return true;
        }
        result = default!;
        return false;
    }

    private static bool IsIntegerTarget<TOther>() =>
        typeof(TOther) == typeof(BigInteger)
        || typeof(TOther) == typeof(byte) || typeof(TOther) == typeof(sbyte) || typeof(TOther) == typeof(short)
        || typeof(TOther) == typeof(ushort) || typeof(TOther) == typeof(int) || typeof(TOther) == typeof(uint)
        || typeof(TOther) == typeof(long) || typeof(TOther) == typeof(ulong) || typeof(TOther) == typeof(Int128)
        || typeof(TOther) == typeof(UInt128) || typeof(TOther) == typeof(nint) || typeof(TOther) == typeof(nuint);

    private static decimal TryDecimalSaturating(BigRational value)
    {
        try
        {
            return value.ToDecimal();
        }
        catch (OverflowException)
        {
            return value.Sign < 0 ? decimal.MinValue : decimal.MaxValue;
        }
    }

    // ----- Formatting -----

    /// <summary>Formats as <c>p/q</c>, or <c>p</c> for integers.</summary>
    public override string ToString() => IsInteger ? _numerator.ToString(CultureInfo.InvariantCulture) : $"{_numerator.ToString(CultureInfo.InvariantCulture)}/{_denominator.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Formats the value. <c>null</c>, <c>""</c>, <c>"G"</c> and <c>"R"</c> give <c>p/q</c> (or <c>p</c>);
    /// <c>"D"</c> gives the exact decimal expansion with the repeating block in parentheses (see <see cref="ToDecimalString"/>).
    /// The provider is ignored so the output is culture independent.
    /// </summary>
    /// <exception cref="FormatException">Unknown format.</exception>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        format switch
        {
            null or "" or "G" or "g" or "R" or "r" => ToString(),
            "D" or "d" => ToDecimalString(),
            _ => throw new FormatException($"Unknown format '{format}'. Use G, R or D."),
        };

    /// <summary>
    /// The exact decimal expansion, with a repeating block in parentheses: <c>1/8</c> → <c>0.125</c>, <c>1/6</c> → <c>0.1(6)</c>,
    /// <c>-4</c> → <c>-4</c>. The output parses back to the same value.
    /// </summary>
    /// <param name="maxDigits">Safety limit on fractional digits (the repeating block can be as long as the denominator).</param>
    /// <exception cref="InvalidOperationException">The expansion needs more than <paramref name="maxDigits"/> fractional digits.</exception>
    public string ToDecimalString(int maxDigits = 100_000)
    {
        var sb = new StringBuilder();
        if (_numerator.Sign < 0) sb.Append('-');
        var n = BigInteger.Abs(_numerator);
        var d = Denominator;
        var integerPart = BigInteger.DivRem(n, d, out var remainder);
        sb.Append(integerPart.ToString(CultureInfo.InvariantCulture));
        if (remainder.IsZero) return sb.ToString();

        sb.Append('.');
        var seen = new Dictionary<BigInteger, int>();
        var digits = new StringBuilder();
        while (!remainder.IsZero)
        {
            if (seen.TryGetValue(remainder, out var start))
            {
                digits.Insert(start, '(').Append(')');
                break;
            }
            if (digits.Length >= maxDigits) throw new InvalidOperationException($"The decimal expansion needs more than {maxDigits} fractional digits.");
            seen[remainder] = digits.Length;
            remainder *= 10;
            var digit = BigInteger.DivRem(remainder, d, out remainder);
            digits.Append((char)('0' + (int)digit));
        }
        return sb.Append(digits).ToString();
    }

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        string text;
        try
        {
            text = ToString(format.IsEmpty ? null : format.ToString(), provider);
        }
        catch (FormatException)
        {
            charsWritten = 0;
            return false;
        }
        if (text.TryCopyTo(destination))
        {
            charsWritten = text.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString(format.IsEmpty ? null : format.ToString(), provider);
        return Encoding.UTF8.TryGetBytes(text, utf8Destination, out bytesWritten);
    }

    // ----- Parsing -----

    /// <summary>Parses <c>3/4</c>, <c>-2</c>, <c>0.125</c>, <c>0.1(6)</c> or <c>1.5e-3</c>.</summary>
    /// <exception cref="FormatException">The text is not a rational number, or its denominator is zero.</exception>
    public static BigRational Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does; <paramref name="style"/> is ignored.</summary>
    public static BigRational Parse(string s, NumberStyles style, IFormatProvider? provider) => Parse(s, provider);

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does.</summary>
    public static BigRational Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid rational number.");

    /// <summary>Parses text as <see cref="Parse(string, IFormatProvider?)"/> does; <paramref name="style"/> is ignored.</summary>
    public static BigRational Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => Parse(s, provider);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out BigRational result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>Tries to parse text; <paramref name="style"/> is ignored.</summary>
    public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out BigRational result) =>
        TryParse(s, provider, out result);

    /// <summary>Tries to parse text; <paramref name="style"/> is ignored.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out BigRational result) =>
        TryParse(s, provider, out result);

    /// <summary>Tries to parse UTF-8 text.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out BigRational result) =>
        TryParse(Encoding.UTF8.GetString(utf8Text), provider, out result);

    /// <summary>Tries to parse UTF-8 text; <paramref name="style"/> is ignored.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider, out BigRational result) =>
        TryParse(utf8Text, provider, out result);

    /// <summary>Parses UTF-8 text.</summary>
    public static BigRational Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(Encoding.UTF8.GetString(utf8Text), provider);

    /// <summary>Parses UTF-8 text; <paramref name="style"/> is ignored.</summary>
    public static BigRational Parse(ReadOnlySpan<byte> utf8Text, NumberStyles style, IFormatProvider? provider) =>
        Parse(utf8Text, provider);

    /// <summary>Tries to parse text; returns <c>false</c> instead of throwing.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BigRational result)
    {
        result = default;
        s = s.Trim();
        if (s.IsEmpty) return false;

        var negative = false;
        if (s[0] is '+' or '-')
        {
            negative = s[0] == '-';
            s = s[1..];
        }

        BigRational value;
        var slash = s.IndexOf('/');
        if (slash >= 0)
        {
            if (!TryDigits(s[..slash], out var n) || !TryDigits(s[(slash + 1)..], out var d) || d.IsZero) return false;
            value = Normalize(n, d);
        }
        else
        {
            var exponent = 0;
            var e = s.IndexOfAny('e', 'E');
            if (e >= 0)
            {
                var expText = s[(e + 1)..];
                if (!int.TryParse(expText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)
                    || Math.Abs((long)exponent) > MaxExponentMagnitude)
                {
                    return false;
                }
                s = s[..e];
            }

            var dot = s.IndexOf('.');
            var integerText = dot >= 0 ? s[..dot] : s;
            ReadOnlySpan<char> fixedText = default, repeatText = default;
            if (dot >= 0)
            {
                var fraction = s[(dot + 1)..];
                var open = fraction.IndexOf('(');
                if (open >= 0)
                {
                    if (fraction[^1] != ')') return false;
                    fixedText = fraction[..open];
                    repeatText = fraction[(open + 1)..^1];
                    if (repeatText.IsEmpty) return false;
                }
                else
                {
                    fixedText = fraction;
                }
            }
            if (integerText.IsEmpty && fixedText.IsEmpty && repeatText.IsEmpty) return false;
            if (!TryDigits(integerText, out var integerPart, allowEmpty: true)
                || !TryDigits(fixedText, out var fixedPart, allowEmpty: true)
                || !TryDigits(repeatText, out var repeatPart, allowEmpty: true))
            {
                return false;
            }

            value = new BigRational(integerPart, BigInteger.One);
            var tenToFixed = BigInteger.Pow(10, fixedText.Length);
            if (repeatText.IsEmpty)
            {
                value += Normalize(fixedPart, tenToFixed);
            }
            else
            {
                var nines = BigInteger.Pow(10, repeatText.Length) - BigInteger.One;
                value += Normalize(fixedPart * nines + repeatPart, tenToFixed * nines);
            }
            if (exponent > 0) value *= new BigRational(BigInteger.Pow(10, exponent), BigInteger.One);
            else if (exponent < 0) value /= new BigRational(BigInteger.Pow(10, -exponent), BigInteger.One);
        }

        result = negative ? -value : value;
        return true;

        static bool TryDigits(ReadOnlySpan<char> text, out BigInteger number, bool allowEmpty = false)
        {
            number = BigInteger.Zero;
            if (text.IsEmpty) return allowEmpty;
            foreach (var c in text)
            {
                if (c is < '0' or > '9') return false;
            }
            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }
    }
}
