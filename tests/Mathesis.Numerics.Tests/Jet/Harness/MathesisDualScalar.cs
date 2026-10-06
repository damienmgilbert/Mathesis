using Mathesis.Numbers;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// Mathesis's <c>Dual&lt;double&gt;</c> behind <see cref="ISpikeScalar{TSelf}"/>. A Jacobian takes one pass per input direction (seed the
/// direction with derivative 1, every other input with 0). Constants carry a dense zero derivative, and <c>==</c> is
/// <c>Dual&lt;T&gt;</c>'s structural equality (value and derivative), which is the reference behaviour the jets deliberately differ from
/// (docs/design/spikes/jet-prototype.md, semantics questions). Ordering looks at the value, because a dual has no order of its own.
/// </summary>
public readonly struct MathesisDualScalar : ISpikeScalar<MathesisDualScalar>, IEquatable<MathesisDualScalar>
{
    private readonly Dual<double> _dual;

    /// <summary>Creates value + derivative·ε.</summary>
    public MathesisDualScalar(double value, double derivative) => _dual = new Dual<double>(value, derivative);

    private MathesisDualScalar(Dual<double> dual) => _dual = dual;

    /// <inheritdoc />
    public double Value => _dual.Value;

    /// <summary>The derivative part.</summary>
    public double Derivative => _dual.Derivative;

    /// <inheritdoc />
    public static MathesisDualScalar Constant(double value) => new(Dual<double>.Constant(value));

    /// <inheritdoc />
    public static MathesisDualScalar operator +(MathesisDualScalar left, MathesisDualScalar right) => new(left._dual + right._dual);

    /// <inheritdoc />
    public static MathesisDualScalar operator -(MathesisDualScalar left, MathesisDualScalar right) => new(left._dual - right._dual);

    /// <inheritdoc />
    public static MathesisDualScalar operator *(MathesisDualScalar left, MathesisDualScalar right) => new(left._dual * right._dual);

    /// <inheritdoc />
    public static MathesisDualScalar operator /(MathesisDualScalar left, MathesisDualScalar right) => new(left._dual / right._dual);

    /// <inheritdoc />
    public static MathesisDualScalar operator -(MathesisDualScalar value) => new(-value._dual);

    /// <inheritdoc />
    public static bool operator <(MathesisDualScalar left, MathesisDualScalar right) => left.Value < right.Value;

    /// <inheritdoc />
    public static bool operator >(MathesisDualScalar left, MathesisDualScalar right) => left.Value > right.Value;

    /// <inheritdoc />
    public static bool operator <=(MathesisDualScalar left, MathesisDualScalar right) => left.Value <= right.Value;

    /// <inheritdoc />
    public static bool operator >=(MathesisDualScalar left, MathesisDualScalar right) => left.Value >= right.Value;

    /// <inheritdoc />
    public static bool operator ==(MathesisDualScalar left, MathesisDualScalar right) => left._dual == right._dual;

    /// <inheritdoc />
    public static bool operator !=(MathesisDualScalar left, MathesisDualScalar right) => left._dual != right._dual;

    /// <inheritdoc />
    public static MathesisDualScalar Sin(MathesisDualScalar x) => new(Dual<double>.Sin(x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Cos(MathesisDualScalar x) => new(Dual<double>.Cos(x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Sqrt(MathesisDualScalar x) => new(Dual<double>.Sqrt(x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Exp(MathesisDualScalar x) => new(Dual<double>.Exp(x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Log(MathesisDualScalar x) => new(Dual<double>.Log(x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Abs(MathesisDualScalar x) => new(Dual<double>.Abs(x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Atan2(MathesisDualScalar y, MathesisDualScalar x) => new(Dual<double>.Atan2(y._dual, x._dual));

    /// <inheritdoc />
    public static MathesisDualScalar Pow(MathesisDualScalar x, double exponent) => new(Dual<double>.Pow(x._dual, exponent));

    /// <inheritdoc />
    public bool Equals(MathesisDualScalar other) => _dual.Equals(other._dual);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MathesisDualScalar other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _dual.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => _dual.ToString();
}
