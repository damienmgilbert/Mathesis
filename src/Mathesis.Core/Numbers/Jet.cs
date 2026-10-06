using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mathesis.Numbers;

/// <summary>The inline gradient storage of <see cref="Jet{T}"/>: <see cref="Jet{T}.Lanes"/> elements.</summary>
/// <typeparam name="T">The element type.</typeparam>
[InlineArray(16)]
internal struct JetGradient<T>
{
    private T _element;
}

/// <summary>
/// A multivariable forward-mode automatic-differentiation number: a value and its gradient with respect to up to
/// <see cref="Lanes"/> independent variables, stored inline (a value, an active-lane count and <see cref="Lanes"/> gradient lanes; of which only
/// <see cref="Dimension"/> are used), so arithmetic allocates nothing. Decision ADR-19 in <c>docs/design/02-architecture.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <em>constant</em> has dimension 0 and no gradient. It is a structural zero: an operation with a constant never multiplies a dense zero
/// gradient, so <c>Constant(2) × Variable(∞)</c> has gradient 2 and not the NaN of <c>0 × ∞</c>. A constant combines with a jet of any
/// dimension; two jets of different non-zero dimensions are API misuse and throw <see cref="ArgumentException"/>. Functions of a constant
/// are constants.
/// </para>
/// <para>
/// Relational operators and the classification predicates (<see cref="IsNaN(Jet{T})"/>, <see cref="IsFinite(Jet{T})"/>, …) look at the value only, so
/// generic code branches exactly as it does on <typeparamref name="T"/>; <see cref="IsGradientFinite"/> reports the gradient separately.
/// <see cref="Equals(Jet{T})"/> and <see cref="GetHashCode"/> are structural (value, dimension and gradient), which is why <c>==</c> and
/// <c>Equals</c> can disagree. Where the derivative is infinite or undefined the chain-rule result (±∞ or NaN) is returned, as IEEE arithmetic
/// would.
/// </para>
/// <para>
/// More than <see cref="Lanes"/> variables are handled by <c>Mathesis.Numerics.Differentiation.JetDifferentiation</c> in chunks of <see cref="Lanes"/> columns; second
/// derivatives by nesting, <c>Jet&lt;Jet&lt;double&gt;&gt;</c>.
/// </para>
/// </remarks>
/// <typeparam name="T">The value type.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static members are required by INumberBase and specified by docs/design/04-type-system.md.")]
[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Generic-math operators have no named alternates; specified by docs/design/04-type-system.md.")]
public readonly partial struct Jet<T> : IFloatingPointIeee754<Jet<T>>
    where T : IFloatingPointIeee754<T>
{
    /// <summary>The maximum number of independent variables of one jet.</summary>
    public const int Lanes = 16;

    // The count and the gradient are written in place through Unsafe.AsRef while a result is built (see Init), never by a constructor.
#pragma warning disable CS0649
    private readonly T _value;
    private readonly int _count;
    private readonly JetGradient<T> _gradient;
#pragma warning restore CS0649

    private Jet(T value) => _value = value;

    /// <summary>The primal value.</summary>
    public T Value => _value;

    /// <summary>The number of variables the gradient is taken with respect to; 0 for a constant.</summary>
    public int Dimension => _count;

    /// <summary>Whether this jet is a constant, that is, has no gradient at all.</summary>
    public bool IsConstant => _count == 0;

    /// <summary>The gradient, <see cref="Dimension"/> elements; empty for a constant. Valid while this jet is.</summary>
    public ReadOnlySpan<T> Gradient => MemoryMarshal.CreateReadOnlySpan(ref Lane0(in this), _count);

    /// <summary>Whether every gradient element is finite (a constant's empty gradient is). <see cref="IsFinite(Jet{T})"/> looks at the value only.</summary>
    public bool IsGradientFinite
    {
        get
        {
            foreach (var g in Gradient)
                if (!T.IsFinite(g)) return false;
            return true;
        }
    }

    /// <summary>A constant: <paramref name="value"/> with no gradient.</summary>
    public static Jet<T> Constant(T value) => new(value);

    /// <summary>Variable <paramref name="index"/> of <paramref name="dimension"/> (1 to <see cref="Lanes"/>) at <paramref name="value"/>: gradient is the unit vector.</summary>
    public static Jet<T> Variable(T value, int index, int dimension)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimension, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dimension, Lanes);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, dimension);
        Jet<T> r = default;
        Init(ref r, value, dimension);
        Unsafe.Add(ref Lane0(in r), index) = T.One;
        return r;
    }

    /// <summary>One variable per element of <paramref name="values"/> (at most <see cref="Lanes"/>), each with the whole vector as its dimension.</summary>
    public static Jet<T>[] Variables(ReadOnlySpan<T> values)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Length, Lanes);
        var result = new Jet<T>[values.Length];
        for (var i = 0; i < result.Length; i++) result[i] = Variable(values[i], i, values.Length);
        return result;
    }

    /// <summary>A constant with the value <paramref name="value"/>.</summary>
    public static implicit operator Jet<T>(T value) => new(value);

    // Results are built in one local, filled in place (the lesson of the Technesis prototype): a lane an operation does not write stays zero.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref T Lane0(in Jet<T> jet) => ref Unsafe.As<JetGradient<T>, T>(ref Unsafe.AsRef(in jet._gradient));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Init(ref Jet<T> jet, T value, int count)
    {
        Unsafe.AsRef(in jet._value) = value;
        Unsafe.AsRef(in jet._count) = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyLanes(ref Jet<T> to, in Jet<T> from, int n) =>
        MemoryMarshal.CreateReadOnlySpan(ref Lane0(in from), n).CopyTo(MemoryMarshal.CreateSpan(ref Lane0(in to), n));

    private static int Match(int a, int b)
    {
        if (a != b) throw new ArgumentException($"Jet dimensions differ ({a} and {b}).");
        return a;
    }

    /// <summary><paramref name="x"/> with its value replaced and its gradient kept.</summary>
    private static Jet<T> WithValue(in Jet<T> x, T value)
    {
        var r = x;
        Unsafe.AsRef(in r._value) = value;
        return r;
    }

    /// <summary>A jet with value <paramref name="value"/> and gradient <c>d · x.gradient</c> (the chain rule for one argument).</summary>
    private static Jet<T> Chain(T value, in Jet<T> x, T d)
    {
        Jet<T> r = default;
        var n = x._count;
        ref var xs = ref Lane0(in x);
        ref var rs = ref Lane0(in r);
        for (var i = 0; i < n; i++) Unsafe.Add(ref rs, i) = d * Unsafe.Add(ref xs, i);
        Init(ref r, value, n);
        return r;
    }

    /// <summary>A jet with value <paramref name="value"/> and gradient <c>x.gradient / d</c>; dividing, not multiplying by <c>1/d</c>, keeps the rounding of <c>Dual&lt;T&gt;</c>.</summary>
    private static Jet<T> ChainDivided(T value, in Jet<T> x, T d)
    {
        Jet<T> r = default;
        var n = x._count;
        ref var xs = ref Lane0(in x);
        ref var rs = ref Lane0(in r);
        for (var i = 0; i < n; i++) Unsafe.Add(ref rs, i) = Unsafe.Add(ref xs, i) / d;
        Init(ref r, value, n);
        return r;
    }

    /// <summary>A jet with value <paramref name="value"/> and gradient <c>x.gradient · c1 · c2</c> (the power rule's order of multiplication).</summary>
    private static Jet<T> ChainScaled(T value, in Jet<T> x, T c1, T c2)
    {
        Jet<T> r = default;
        var n = x._count;
        ref var xs = ref Lane0(in x);
        ref var rs = ref Lane0(in r);
        for (var i = 0; i < n; i++) Unsafe.Add(ref rs, i) = Unsafe.Add(ref xs, i) * c1 * c2;
        Init(ref r, value, n);
        return r;
    }

    /// <summary>A jet with value <paramref name="value"/> and gradient <c>dx · x.gradient + dy · y.gradient</c>; a constant argument contributes nothing.</summary>
    private static Jet<T> Chain(T value, in Jet<T> x, T dx, in Jet<T> y, T dy)
    {
        Jet<T> r = default;
        ref var rs = ref Lane0(in r);
        int n;
        if (x._count != 0)
        {
            ref var xs = ref Lane0(in x);
            if (y._count != 0)
            {
                n = Match(x._count, y._count);
                ref var ys = ref Lane0(in y);
                for (var i = 0; i < n; i++) Unsafe.Add(ref rs, i) = dx * Unsafe.Add(ref xs, i) + dy * Unsafe.Add(ref ys, i);
            }
            else
            {
                n = x._count;
                for (var i = 0; i < n; i++) Unsafe.Add(ref rs, i) = dx * Unsafe.Add(ref xs, i);
            }
        }
        else
        {
            n = y._count;
            ref var ys = ref Lane0(in y);
            for (var i = 0; i < n; i++) Unsafe.Add(ref rs, i) = dy * Unsafe.Add(ref ys, i);
        }
        Init(ref r, value, n);
        return r;
    }

    /// <summary>The sum.</summary>
    public static Jet<T> operator +(Jet<T> left, Jet<T> right)
    {
        Jet<T> r = default;
        int n;
        if (left._count != 0)
        {
            n = left._count;
            if (right._count != 0)
            {
                Match(n, right._count);
                ref var ls = ref Lane0(in left);
                ref var rs = ref Lane0(in right);
                ref var zs = ref Lane0(in r);
                for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = Unsafe.Add(ref ls, i) + Unsafe.Add(ref rs, i);
            }
            else CopyLanes(ref r, in left, n);
        }
        else
        {
            n = right._count;
            CopyLanes(ref r, in right, n);
        }
        Init(ref r, left._value + right._value, n);
        return r;
    }

    /// <summary>The difference.</summary>
    public static Jet<T> operator -(Jet<T> left, Jet<T> right)
    {
        Jet<T> r = default;
        int n;
        if (left._count != 0)
        {
            n = left._count;
            if (right._count != 0)
            {
                Match(n, right._count);
                ref var ls = ref Lane0(in left);
                ref var rs = ref Lane0(in right);
                ref var zs = ref Lane0(in r);
                for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = Unsafe.Add(ref ls, i) - Unsafe.Add(ref rs, i);
            }
            else CopyLanes(ref r, in left, n);
        }
        else
        {
            n = right._count;
            ref var rs = ref Lane0(in right);
            ref var zs = ref Lane0(in r);
            for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = -Unsafe.Add(ref rs, i);
        }
        Init(ref r, left._value - right._value, n);
        return r;
    }

    /// <summary>The product (product rule).</summary>
    public static Jet<T> operator *(Jet<T> left, Jet<T> right) => Chain(left._value * right._value, in left, right._value, in right, left._value);

    /// <summary>The quotient (quotient rule: <c>(x′ − q·y′)/y</c> with <c>q = x/y</c>).</summary>
    public static Jet<T> operator /(Jet<T> left, Jet<T> right)
    {
        var q = left._value / right._value;
        Jet<T> r = default;
        ref var zs = ref Lane0(in r);
        int n;
        if (right._count != 0)
        {
            n = right._count;
            ref var ys = ref Lane0(in right);
            if (left._count != 0)
            {
                Match(left._count, n);
                ref var xs = ref Lane0(in left);
                for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = (Unsafe.Add(ref xs, i) - q * Unsafe.Add(ref ys, i)) / right._value;
            }
            else
            {
                var nq = -q;
                for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = nq * Unsafe.Add(ref ys, i) / right._value;
            }
        }
        else
        {
            n = left._count;
            ref var xs = ref Lane0(in left);
            for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = Unsafe.Add(ref xs, i) / right._value;
        }
        Init(ref r, q, n);
        return r;
    }

    /// <summary>The remainder <c>x − y·trunc(x/y)</c>; the gradient is <c>x′ − y′·trunc(x/y)</c>.</summary>
    public static Jet<T> operator %(Jet<T> left, Jet<T> right) =>
        Chain(left._value % right._value, in left, T.One, in right, -T.Truncate(left._value / right._value));

    /// <summary>The negation.</summary>
    public static Jet<T> operator -(Jet<T> value)
    {
        Jet<T> r = default;
        var n = value._count;
        ref var xs = ref Lane0(in value);
        ref var zs = ref Lane0(in r);
        for (var i = 0; i < n; i++) Unsafe.Add(ref zs, i) = -Unsafe.Add(ref xs, i);
        Init(ref r, -value._value, n);
        return r;
    }

    /// <summary>The value unchanged.</summary>
    public static Jet<T> operator +(Jet<T> value) => value;

    /// <summary>Adds one to the value.</summary>
    public static Jet<T> operator ++(Jet<T> value) => WithValue(in value, value._value + T.One);

    /// <summary>Subtracts one from the value.</summary>
    public static Jet<T> operator --(Jet<T> value) => WithValue(in value, value._value - T.One);

    /// <summary>Compares values only (ADR-19).</summary>
    public static bool operator <(Jet<T> left, Jet<T> right) => left._value < right._value;

    /// <summary>Compares values only (ADR-19).</summary>
    public static bool operator >(Jet<T> left, Jet<T> right) => left._value > right._value;

    /// <summary>Compares values only (ADR-19).</summary>
    public static bool operator <=(Jet<T> left, Jet<T> right) => left._value <= right._value;

    /// <summary>Compares values only (ADR-19).</summary>
    public static bool operator >=(Jet<T> left, Jet<T> right) => left._value >= right._value;

    /// <summary>Compares values only (ADR-19); <see cref="Equals(Jet{T})"/> is structural.</summary>
    public static bool operator ==(Jet<T> left, Jet<T> right) => left._value == right._value;

    /// <summary>Compares values only (ADR-19).</summary>
    public static bool operator !=(Jet<T> left, Jet<T> right) => left._value != right._value;

    /// <summary>Structural equality: the values, the dimensions and the gradients are identical (NaN equals NaN).</summary>
    public bool Equals(Jet<T> other) => _value.Equals(other._value) && _count == other._count && Gradient.SequenceEqual(other.Gradient);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Jet<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_value);
        foreach (var g in Gradient) hash.Add(g);
        return hash.ToHashCode();
    }

    /// <summary>Orders by value only, as the relational operators do.</summary>
    public int CompareTo(Jet<T> other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj is null ? 1 : obj is Jet<T> other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Jet<T>)}.", nameof(obj));

    /// <summary>The text form <c>(value; g₀, g₁, …)</c>; a constant prints <c>(value)</c>. Uses the invariant culture.</summary>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>The text form <c>(value; g₀, g₁, …)</c> with <paramref name="format"/> and <paramref name="formatProvider"/> applied to every number.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var text = new StringBuilder("(").Append(_value.ToString(format, formatProvider));
        if (_count != 0)
        {
            text.Append(';');
            var first = true;
            foreach (var g in Gradient)
            {
                text.Append(first ? " " : ", ").Append(g.ToString(format, formatProvider));
                first = false;
            }
        }
        return text.Append(')').ToString();
    }

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString(format.IsEmpty ? null : format.ToString(), provider);
        var fits = text.TryCopyTo(destination);
        charsWritten = fits ? text.Length : 0;
        return fits;
    }

    /// <inheritdoc />
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Encoding.UTF8.TryGetBytes(ToString(format.IsEmpty ? null : format.ToString(), provider), utf8Destination, out bytesWritten);
}
