using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Numerics.Tensors;
using System.Text;

namespace Mathesis.LinearAlgebra;

/// <summary>The dimensions of a matrix.</summary>
/// <param name="Rows">The number of rows.</param>
/// <param name="Columns">The number of columns.</param>
public readonly record struct Shape(int Rows, int Columns)
{
    /// <summary>Whether the matrix is square.</summary>
    public bool IsSquare => Rows == Columns;

    /// <inheritdoc />
    public override string ToString() => $"{Rows}×{Columns}";
}

/// <summary>
/// An immutable matrix in row-major contiguous storage. Operations return new matrices; the kernels work on spans, and
/// products and dot products use <see cref="TensorPrimitives"/> (SIMD) for <see cref="double"/> and <see cref="float"/>.
/// </summary>
/// <typeparam name="T">The element type: any type with the ring operator interfaces (<see cref="double"/>, <c>BigRational</c>, …).</typeparam>
/// <remarks>
/// Numeric algorithms for floating-point elements are in <see cref="MatrixSolvers"/> and the
/// <c>Mathesis.LinearAlgebra.Decompositions</c> namespace; exact algorithms that also work for symbolic entries are in
/// <c>Mathesis.LinearAlgebra.Exact</c>.
/// </remarks>
public sealed class DenseMatrix<T> : IEquatable<DenseMatrix<T>>
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
        IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
{
    internal DenseMatrix(int rows, int columns, T[] data)
    {
        Rows = rows;
        Columns = columns;
        Data = data;
    }

    internal T[] Data { get; }

    /// <summary>The number of rows.</summary>
    public int Rows { get; }

    /// <summary>The number of columns.</summary>
    public int Columns { get; }

    /// <summary>The dimensions.</summary>
    public Shape Shape => new(Rows, Columns);

    /// <summary>The entry in row <paramref name="row"/> and column <paramref name="column"/> (zero based).</summary>
    public T this[int row, int column]
    {
        get
        {
            if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row), $"Row {row} is outside a {Shape} matrix.");
            if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column), $"Column {column} is outside a {Shape} matrix.");
            return Data[row * Columns + column];
        }
    }

    /// <summary>A read-only view of all entries in row-major order.</summary>
    public ReadOnlySpan<T> AsSpan() => Data;

    /// <summary>A read-only view of one row.</summary>
    public ReadOnlySpan<T> RowSpan(int row) => Data.AsSpan(row * Columns, Columns);

    /// <summary>A copy of one row.</summary>
    public DenseVector<T> Row(int row) => new(RowSpan(row).ToArray(), copy: false);

    /// <summary>A copy of one column.</summary>
    public DenseVector<T> Column(int column)
    {
        var c = new T[Rows];
        for (var i = 0; i < Rows; i++) c[i] = Data[i * Columns + column];
        return new(c, copy: false);
    }

    /// <summary>The transpose Aᵀ (catalog <c>linalg.mat.transpose-involution</c>).</summary>
    public DenseMatrix<T> Transpose()
    {
        var r = new T[Data.Length];
        for (var i = 0; i < Rows; i++)
        {
            for (var j = 0; j < Columns; j++) r[j * Rows + i] = Data[i * Columns + j];
        }
        return new(Columns, Rows, r);
    }

    /// <summary>The trace Σ aᵢᵢ of a square matrix.</summary>
    /// <exception cref="InvalidOperationException">The matrix is not square.</exception>
    public T Trace()
    {
        if (Rows != Columns) throw new InvalidOperationException("The trace needs a square matrix.");
        var sum = T.AdditiveIdentity;
        for (var i = 0; i < Rows; i++) sum += Data[i * Columns + i];
        return sum;
    }

    /// <summary>The submatrix of <paramref name="rows"/> × <paramref name="columns"/> entries starting at the given position.</summary>
    public DenseMatrix<T> Block(int firstRow, int firstColumn, int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstRow);
        ArgumentOutOfRangeException.ThrowIfNegative(firstColumn);
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        if (firstRow + rows > Rows || firstColumn + columns > Columns) throw new ArgumentOutOfRangeException(nameof(rows), "The block extends beyond the matrix.");
        var r = new T[rows * columns];
        for (var i = 0; i < rows; i++) Array.Copy(Data, (firstRow + i) * Columns + firstColumn, r, i * columns, columns);
        return new(rows, columns, r);
    }

    private void RequireSameShape(DenseMatrix<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Shape != other.Shape) throw new ArgumentException($"Shapes differ: {Shape} and {other.Shape}.", nameof(other));
    }

    /// <summary>Adds two matrices of equal shape.</summary>
    public static DenseMatrix<T> operator +(DenseMatrix<T> left, DenseMatrix<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameShape(right);
        var r = new T[left.Data.Length];
        for (var i = 0; i < r.Length; i++) r[i] = left.Data[i] + right.Data[i];
        return new(left.Rows, left.Columns, r);
    }

    /// <summary>Subtracts two matrices of equal shape.</summary>
    public static DenseMatrix<T> operator -(DenseMatrix<T> left, DenseMatrix<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        left.RequireSameShape(right);
        var r = new T[left.Data.Length];
        for (var i = 0; i < r.Length; i++) r[i] = left.Data[i] - right.Data[i];
        return new(left.Rows, left.Columns, r);
    }

    /// <summary>Negates every entry.</summary>
    public static DenseMatrix<T> operator -(DenseMatrix<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Rows, value.Columns, value.Data.Select(x => -x).ToArray());
    }

    /// <summary>Multiplies every entry by a scalar (catalog <c>linalg.mat.scalar</c>).</summary>
    public static DenseMatrix<T> operator *(DenseMatrix<T> matrix, T scalar)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new(matrix.Rows, matrix.Columns, matrix.Data.Select(x => x * scalar).ToArray());
    }

    /// <summary>Multiplies every entry by a scalar.</summary>
    public static DenseMatrix<T> operator *(T scalar, DenseMatrix<T> matrix) => matrix * scalar;

    /// <summary>The matrix product (catalog <c>linalg.mat.product-def</c>); an m×n matrix times an n×p matrix is m×p.</summary>
    /// <exception cref="ArgumentException">The inner dimensions differ.</exception>
    public static DenseMatrix<T> operator *(DenseMatrix<T> left, DenseMatrix<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Columns != right.Rows) throw new ArgumentException($"Cannot multiply {left.Shape} by {right.Shape}.", nameof(right));
        var m = left.Rows;
        var n = left.Columns;
        var p = right.Columns;
        var r = new T[m * p];

        // Multiply rows of A by rows of Bᵀ so both operands of every dot product are contiguous.
        var bt = right.Transpose().Data;
        for (var i = 0; i < m; i++)
        {
            var a = left.Data.AsSpan(i * n, n);
            for (var j = 0; j < p; j++) r[i * p + j] = DenseMatrix.Dot<T>(a, bt.AsSpan(j * n, n));
        }
        return new(m, p, r);
    }

    /// <summary>The matrix–vector product A·x.</summary>
    /// <exception cref="ArgumentException">The vector length differs from the number of columns.</exception>
    public static DenseVector<T> operator *(DenseMatrix<T> matrix, DenseVector<T> vector)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(vector);
        if (matrix.Columns != vector.Length) throw new ArgumentException($"Cannot multiply {matrix.Shape} by a vector of length {vector.Length}.", nameof(vector));
        var r = new T[matrix.Rows];
        for (var i = 0; i < r.Length; i++) r[i] = DenseMatrix.Dot<T>(matrix.RowSpan(i), vector.Data);
        return new(r, copy: false);
    }

    /// <summary>The Hadamard (entrywise) product.</summary>
    public DenseMatrix<T> Hadamard(DenseMatrix<T> other)
    {
        RequireSameShape(other);
        var r = new T[Data.Length];
        for (var i = 0; i < r.Length; i++) r[i] = Data[i] * other.Data[i];
        return new(Rows, Columns, r);
    }

    /// <summary>The Kronecker product A ⊗ B (catalog <c>linalg.kron.mixed-product</c>).</summary>
    public DenseMatrix<T> Kronecker(DenseMatrix<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var rows = Rows * other.Rows;
        var columns = Columns * other.Columns;
        var r = new T[rows * columns];
        for (var i = 0; i < Rows; i++)
        {
            for (var j = 0; j < Columns; j++)
            {
                var a = Data[i * Columns + j];
                for (var k = 0; k < other.Rows; k++)
                {
                    for (var l = 0; l < other.Columns; l++) r[(i * other.Rows + k) * columns + j * other.Columns + l] = a * other.Data[k * other.Columns + l];
                }
            }
        }
        return new(rows, columns, r);
    }

    /// <summary>Raises a square matrix to a non-negative integer power by repeated squaring.</summary>
    public DenseMatrix<T> Pow(int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        if (Rows != Columns) throw new InvalidOperationException("Only square matrices have powers.");
        var result = DenseMatrix.Identity<T>(Rows);
        var square = this;
        while (exponent > 0)
        {
            if ((exponent & 1) == 1) result *= square;
            exponent >>= 1;
            if (exponent > 0) square *= square;
        }
        return result;
    }

    /// <inheritdoc />
    public bool Equals(DenseMatrix<T>? other) => other is not null && Shape == other.Shape && Data.AsSpan().SequenceEqual(other.Data);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DenseMatrix<T>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Rows);
        hash.Add(Columns);
        foreach (var x in Data) hash.Add(x);
        return hash.ToHashCode();
    }

    /// <summary>Structural equality.</summary>
    public static bool operator ==(DenseMatrix<T>? left, DenseMatrix<T>? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(DenseMatrix<T>? left, DenseMatrix<T>? right) => !(left == right);

    /// <summary>Formats as rows of entries, one row per line.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Rows; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.Append('[').Append(string.Join(", ", RowSpan(i).ToArray())).Append(']');
        }
        return sb.ToString();
    }
}

/// <summary>Factory methods for <see cref="DenseMatrix{T}"/> and shared kernels.</summary>
public static class DenseMatrix
{
    /// <summary>Creates a matrix by evaluating <paramref name="entry"/> at every (row, column).</summary>
    public static DenseMatrix<T> Create<T>(int rows, int columns, Func<int, int, T> entry)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        var data = new T[rows * columns];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < columns; j++) data[i * columns + j] = entry(i, j);
        }
        return new(rows, columns, data);
    }

    /// <summary>Creates a matrix from a rectangular array (copied).</summary>
    public static DenseMatrix<T> FromRows<T>(T[,] entries)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(entries);
        return Create(entries.GetLength(0), entries.GetLength(1), (i, j) => entries[i, j]);
    }

    /// <summary>Creates a matrix from row-major data (copied).</summary>
    /// <exception cref="ArgumentException">The data length is not rows × columns.</exception>
    public static DenseMatrix<T> FromRowMajor<T>(int rows, int columns, ReadOnlySpan<T> data)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        if (data.Length != rows * columns) throw new ArgumentException($"Expected {rows * columns} entries but got {data.Length}.", nameof(data));
        return new(rows, columns, data.ToArray());
    }

    /// <summary>The zero matrix.</summary>
    public static DenseMatrix<T> Zero<T>(int rows, int columns)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool> =>
        Create(rows, columns, (_, _) => T.AdditiveIdentity);

    /// <summary>The n×n identity matrix.</summary>
    public static DenseMatrix<T> Identity<T>(int n)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool> =>
        Create(n, n, (i, j) => i == j ? T.MultiplicativeIdentity : T.AdditiveIdentity);

    /// <summary>A square matrix with the given diagonal.</summary>
    public static DenseMatrix<T> Diagonal<T>(params ReadOnlySpan<T> diagonal)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        var d = diagonal.ToArray();
        return Create(d.Length, d.Length, (i, j) => i == j ? d[i] : T.AdditiveIdentity);
    }

    /// <summary>Stacks vectors as the columns of a matrix.</summary>
    public static DenseMatrix<T> FromColumns<T>(IReadOnlyList<DenseVector<T>> columns)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(columns);
        var rows = columns.Count == 0 ? 0 : columns[0].Length;
        if (columns.Any(c => c.Length != rows)) throw new ArgumentException("All columns must have the same length.", nameof(columns));
        return Create(rows, columns.Count, (i, j) => columns[j][i]);
    }

    /// <summary>
    /// Σ xᵢyᵢ over equal-length spans. <see cref="double"/> and <see cref="float"/> go through
    /// <see cref="TensorPrimitives.Dot{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>; every other type uses a plain loop with its own operators.
    /// </summary>
    internal static T Dot<T>(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        where T : IAdditionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>
    {
        if (typeof(T) == typeof(double) || typeof(T) == typeof(float)) return TensorPrimitives.Dot(x, y);
        var sum = T.AdditiveIdentity;
        for (var i = 0; i < x.Length; i++) sum += x[i] * y[i];
        return sum;
    }
}

/// <summary>Matrix norms for floating-point matrices.</summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Extension members on DenseMatrix<T> (docs/design/03-namespaces-and-packages.md).")]
public static class DenseMatrixNorms
{
    extension<T>(DenseMatrix<T> a) where T : IFloatingPointIeee754<T>
    {
        /// <summary>The induced 1-norm: the maximum absolute column sum (catalog <c>linalg.norm.induced</c>).</summary>
        public T Norm1()
        {
            var max = T.Zero;
            for (var j = 0; j < a.Columns; j++)
            {
                var sum = T.Zero;
                for (var i = 0; i < a.Rows; i++) sum += T.Abs(a[i, j]);
                if (sum > max || T.IsNaN(sum)) max = sum;
            }
            return max;
        }

        /// <summary>The induced ∞-norm: the maximum absolute row sum.</summary>
        public T NormInfinity()
        {
            var max = T.Zero;
            for (var i = 0; i < a.Rows; i++)
            {
                var sum = T.Zero;
                foreach (var x in a.RowSpan(i)) sum += T.Abs(x);
                if (sum > max || T.IsNaN(sum)) max = sum;
            }
            return max;
        }

        /// <summary>The Frobenius norm √Σaᵢⱼ² (catalog <c>linalg.norm.frobenius</c>).</summary>
        public T NormFrobenius() => new DenseVector<T>(a.AsSpan().ToArray()).Norm2();
    }
}
