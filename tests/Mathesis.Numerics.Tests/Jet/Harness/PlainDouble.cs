namespace Mathesis.Numerics.Tests;

/// <summary>A <see cref="double"/> behind <see cref="ISpikeScalar{TSelf}"/>: the value-only baseline and the finite-difference evaluator.</summary>
public readonly struct PlainDouble : ISpikeScalar<PlainDouble>, IEquatable<PlainDouble>
{
    private readonly double _value;

    /// <summary>Wraps <paramref name="value"/>.</summary>
    public PlainDouble(double value) => _value = value;

    /// <inheritdoc />
    public double Value => _value;

    /// <inheritdoc />
    public static PlainDouble Constant(double value) => new(value);

    /// <inheritdoc />
    public static PlainDouble operator +(PlainDouble left, PlainDouble right) => new(left._value + right._value);

    /// <inheritdoc />
    public static PlainDouble operator -(PlainDouble left, PlainDouble right) => new(left._value - right._value);

    /// <inheritdoc />
    public static PlainDouble operator *(PlainDouble left, PlainDouble right) => new(left._value * right._value);

    /// <inheritdoc />
    public static PlainDouble operator /(PlainDouble left, PlainDouble right) => new(left._value / right._value);

    /// <inheritdoc />
    public static PlainDouble operator -(PlainDouble value) => new(-value._value);

    /// <inheritdoc />
    public static bool operator <(PlainDouble left, PlainDouble right) => left._value < right._value;

    /// <inheritdoc />
    public static bool operator >(PlainDouble left, PlainDouble right) => left._value > right._value;

    /// <inheritdoc />
    public static bool operator <=(PlainDouble left, PlainDouble right) => left._value <= right._value;

    /// <inheritdoc />
    public static bool operator >=(PlainDouble left, PlainDouble right) => left._value >= right._value;

    /// <inheritdoc />
    public static bool operator ==(PlainDouble left, PlainDouble right) => left._value == right._value;

    /// <inheritdoc />
    public static bool operator !=(PlainDouble left, PlainDouble right) => left._value != right._value;

    /// <inheritdoc />
    public static PlainDouble Sin(PlainDouble x) => new(Math.Sin(x._value));

    /// <inheritdoc />
    public static PlainDouble Cos(PlainDouble x) => new(Math.Cos(x._value));

    /// <inheritdoc />
    public static PlainDouble Sqrt(PlainDouble x) => new(Math.Sqrt(x._value));

    /// <inheritdoc />
    public static PlainDouble Exp(PlainDouble x) => new(Math.Exp(x._value));

    /// <inheritdoc />
    public static PlainDouble Log(PlainDouble x) => new(Math.Log(x._value));

    /// <inheritdoc />
    public static PlainDouble Abs(PlainDouble x) => new(Math.Abs(x._value));

    /// <inheritdoc />
    public static PlainDouble Atan2(PlainDouble y, PlainDouble x) => new(Math.Atan2(y._value, x._value));

    /// <inheritdoc />
    public static PlainDouble Pow(PlainDouble x, double exponent) => new(Math.Pow(x._value, exponent));

    /// <inheritdoc />
    public bool Equals(PlainDouble other) => _value.Equals(other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PlainDouble other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => _value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
