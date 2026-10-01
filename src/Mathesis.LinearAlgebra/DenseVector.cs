using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Numerics.Tensors;

namespace Mathesis.LinearAlgebra;

/// <summary>An immutable vector with contiguous storage (named to avoid <see cref="System.Numerics.Vector{T}"/>).</summary>
/// <typeparam name="T">The element type: any type with the ring operator interfaces (<see cref="double"/>, <c>BigRational</c>, …).</typeparam>
public sealed class DenseVector<T> : IReadOnlyList<T>, IEquatable<DenseVector<T>>
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
        IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
{
    internal DenseVector(T[] data, bool copy)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = copy ? (T[])data.Clone() : data;
    }

    /// <summary>Creates a vector holding a copy of <paramref name="values"/>.</summary>
    public DenseVector(IEnumerable<T> values)
        : this((values ?? throw new ArgumentNullException(nameof(values))).ToArray(), copy: false)
    {
    }

    internal T[] Data { get; }

    /// <summary>The number of elements.</summary>
    public int Length => Data.Length;

    int IReadOnlyCollection<T>.Count => Data.Length;

    /// <summary>The element at <paramref name="index"/>.</summary>
    public T this[int index] => Data[index];

    /// <summary>A read-only view of the elements.</summary>
    public ReadOnlySpan<T> AsSpan() => Data;

    /// <summary>A copy of the elements.</summary>
    public T[] ToArray() => (T[])Data.Clone();

    private void RequireSameLength(DenseVector<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Length != Length) throw new ArgumentException($"Vector lengths differ: {Length} and {other.Length}.", nameof(other));
    }

    /// <summary>Adds two vectors of equal length.</summary>
    public static DenseVector<T> operator +(DenseVector<T> left, DenseVector<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameLength(right);
        var r = new T[left.Length];
        for (var i = 0; i < r.Length; i++) r[i] = left.Data[i] + right.Data[i];
        return new(r, copy: false);
    }

    /// <summary>Subtracts two vectors of equal length.</summary>
    public static DenseVector<T> operator -(DenseVector<T> left, DenseVector<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameLength(right);
        var r = new T[left.Length];
        for (var i = 0; i < r.Length; i++) r[i] = left.Data[i] - right.Data[i];
        return new(r, copy: false);
    }

    /// <summary>Negates every element.</summary>
    public static DenseVector<T> operator -(DenseVector<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Data.Select(x => -x).ToArray(), copy: false);
    }

    /// <summary>Multiplies every element by a scalar.</summary>
    public static DenseVector<T> operator *(DenseVector<T> vector, T scalar)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return new(vector.Data.Select(x => x * scalar).ToArray(), copy: false);
    }

    /// <summary>Multiplies every element by a scalar.</summary>
    public static DenseVector<T> operator *(T scalar, DenseVector<T> vector) => vector * scalar;

    /// <summary>
    /// The dot product Σ xᵢyᵢ (no conjugation). Uses <see cref="TensorPrimitives"/> (SIMD) for <see cref="double"/> and
    /// <see cref="float"/>.
    /// </summary>
    public T Dot(DenseVector<T> other)
    {
        RequireSameLength(other);
        return DenseMatrix.Dot<T>(Data, other.Data);
    }

    /// <inheritdoc />
    public bool Equals(DenseVector<T>? other) => other is not null && Data.AsSpan().SequenceEqual(other.Data);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DenseVector<T>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var x in Data) hash.Add(x);
        return hash.ToHashCode();
    }

    /// <summary>Structural equality.</summary>
    public static bool operator ==(DenseVector<T>? left, DenseVector<T>? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(DenseVector<T>? left, DenseVector<T>? right) => !(left == right);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Data).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Formats as <c>[a, b, c]</c>.</summary>
    public override string ToString() => $"[{string.Join(", ", Data)}]";
}

/// <summary>Factory methods for <see cref="DenseVector{T}"/>.</summary>
public static class DenseVector
{
    /// <summary>Creates a vector from the given elements.</summary>
    public static DenseVector<T> Create<T>(params ReadOnlySpan<T> values)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool> => new(values.ToArray(), copy: false);

    /// <summary>The zero vector of the given length.</summary>
    public static DenseVector<T> Zero<T>(int length)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var data = new T[length];
        Array.Fill(data, T.AdditiveIdentity);
        return new(data, copy: false);
    }

    /// <summary>Creates a vector by evaluating <paramref name="element"/> at each index.</summary>
    public static DenseVector<T> Create<T>(int length, Func<int, T> element)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var data = new T[length];
        for (var i = 0; i < length; i++) data[i] = element(i);
        return new(data, copy: false);
    }
}

/// <summary>Vector norms for floating-point vectors.</summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Extension members on DenseVector<T> (docs/design/03-namespaces-and-packages.md).")]
public static class DenseVectorNorms
{
    extension<T>(DenseVector<T> v) where T : IFloatingPointIeee754<T>
    {
        /// <summary>The 1-norm Σ|xᵢ| (catalog <c>linalg.norm.p-norms</c>).</summary>
        public T Norm1()
        {
            var sum = T.Zero;
            foreach (var x in v.AsSpan()) sum += T.Abs(x);
            return sum;
        }

        /// <summary>The Euclidean norm √Σxᵢ², scaled to avoid overflow and underflow.</summary>
        public T Norm2()
        {
            var scale = v.NormInfinity();
            if (scale == T.Zero || !T.IsFinite(scale)) return scale;
            var sum = T.Zero;
            foreach (var x in v.AsSpan())
            {
                var q = x / scale;
                sum += q * q;
            }
            return scale * T.Sqrt(sum);
        }

        /// <summary>The maximum norm max|xᵢ|.</summary>
        public T NormInfinity()
        {
            var max = T.Zero;
            foreach (var x in v.AsSpan())
            {
                var a = T.Abs(x);
                if (a > max || T.IsNaN(a)) max = a;
            }
            return max;
        }
    }
}
