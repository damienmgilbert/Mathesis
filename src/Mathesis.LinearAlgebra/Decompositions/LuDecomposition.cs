using System.Collections.Immutable;
using System.Numerics;

namespace Mathesis.LinearAlgebra.Decompositions;

/// <summary>
/// The LU factorization with partial pivoting P·A = L·U of a square matrix (catalog <c>linalg.dec.plu</c>), computed by Gaussian
/// elimination with row exchanges (backward stable in practice, <c>num.la.gepp-stability</c>; about 2n³/3 flops).
/// </summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public sealed class LuDecomposition<T>
    where T : IFloatingPointIeee754<T>
{
    // L (unit lower, below the diagonal) and U (upper, on and above the diagonal) packed in one array.
    private readonly T[] _packed;
    private readonly int[] _permutation;

    internal LuDecomposition(DenseMatrix<T> a)
    {
        if (a.Rows != a.Columns) throw new ArgumentException("LU factorization needs a square matrix.", nameof(a));
        var n = a.Rows;
        _packed = a.AsSpan().ToArray();
        _permutation = Enumerable.Range(0, n).ToArray();
        Size = n;
        PermutationSign = 1;

        for (var k = 0; k < n; k++)
        {
            var pivot = k;
            var largest = T.Abs(_packed[k * n + k]);
            for (var i = k + 1; i < n; i++)
            {
                var v = T.Abs(_packed[i * n + k]);
                if (v > largest)
                {
                    largest = v;
                    pivot = i;
                }
            }
            if (largest == T.Zero)
            {
                IsSingular = true;
                continue;
            }
            if (pivot != k)
            {
                for (var j = 0; j < n; j++) (_packed[k * n + j], _packed[pivot * n + j]) = (_packed[pivot * n + j], _packed[k * n + j]);
                (_permutation[k], _permutation[pivot]) = (_permutation[pivot], _permutation[k]);
                PermutationSign = -PermutationSign;
            }
            var diagonal = _packed[k * n + k];
            for (var i = k + 1; i < n; i++)
            {
                var factor = _packed[i * n + k] / diagonal;
                _packed[i * n + k] = factor;
                for (var j = k + 1; j < n; j++) _packed[i * n + j] -= factor * _packed[k * n + j];
            }
        }
    }

    /// <summary>The matrix dimension n.</summary>
    public int Size { get; }

    /// <summary>Whether a zero pivot was met, so the matrix is exactly singular in floating point.</summary>
    public bool IsSingular { get; }

    /// <summary>The permutation as row indices: row i of P·A is row <c>Permutation[i]</c> of A.</summary>
    public ImmutableArray<int> Permutation => [.. _permutation];

    /// <summary>The sign (+1 or −1) of the row permutation.</summary>
    public int PermutationSign { get; }

    /// <summary>The unit lower-triangular factor L.</summary>
    public DenseMatrix<T> Lower => DenseMatrix.Create(Size, Size, (i, j) => i == j ? T.One : i > j ? _packed[i * Size + j] : T.Zero);

    /// <summary>The upper-triangular factor U.</summary>
    public DenseMatrix<T> Upper => DenseMatrix.Create(Size, Size, (i, j) => i <= j ? _packed[i * Size + j] : T.Zero);

    /// <summary>The permutation matrix P with P·A = L·U.</summary>
    public DenseMatrix<T> PermutationMatrix => DenseMatrix.Create(Size, Size, (i, j) => _permutation[i] == j ? T.One : T.Zero);

    /// <summary>The determinant: the sign times the product of the diagonal of U.</summary>
    public T Determinant
    {
        get
        {
            var d = T.CreateChecked(PermutationSign);
            for (var i = 0; i < Size; i++) d *= _packed[i * Size + i];
            return d;
        }
    }

    /// <summary>Rebuilds the original matrix A = Pᵀ·L·U.</summary>
    public DenseMatrix<T> Reconstruct() => PermutationMatrix.Transpose() * Lower * Upper;

    /// <summary>The Frobenius norm of <paramref name="original"/> − Pᵀ·L·U, a measure of the factorization error.</summary>
    public T Residual(DenseMatrix<T> original) => (original - Reconstruct()).NormFrobenius();

    /// <summary>Solves A·x = b by forward and back substitution.</summary>
    /// <exception cref="InvalidOperationException">The matrix is singular.</exception>
    public DenseVector<T> Solve(DenseVector<T> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.Length != Size) throw new ArgumentException($"Expected a vector of length {Size}.", nameof(b));
        if (IsSingular) throw new InvalidOperationException("The matrix is singular.");
        var n = Size;
        var x = new T[n];
        for (var i = 0; i < n; i++)
        {
            var s = b[_permutation[i]];
            for (var j = 0; j < i; j++) s -= _packed[i * n + j] * x[j];
            x[i] = s;
        }
        for (var i = n - 1; i >= 0; i--)
        {
            var s = x[i];
            for (var j = i + 1; j < n; j++) s -= _packed[i * n + j] * x[j];
            x[i] = s / _packed[i * n + i];
        }
        return new(x);
    }

    /// <summary>Solves Aᵀ·x = b, which the condition estimator needs.</summary>
    /// <exception cref="InvalidOperationException">The matrix is singular.</exception>
    public DenseVector<T> SolveTranspose(DenseVector<T> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.Length != Size) throw new ArgumentException($"Expected a vector of length {Size}.", nameof(b));
        if (IsSingular) throw new InvalidOperationException("The matrix is singular.");
        // Aᵀ = Uᵀ Lᵀ P: solve Uᵀ y = b, Lᵀ z = y, then x = Pᵀ z.
        var n = Size;
        var y = b.ToArray();
        for (var i = 0; i < n; i++)
        {
            var s = y[i];
            for (var j = 0; j < i; j++) s -= _packed[j * n + i] * y[j];
            y[i] = s / _packed[i * n + i];
        }
        for (var i = n - 1; i >= 0; i--)
        {
            var s = y[i];
            for (var j = i + 1; j < n; j++) s -= _packed[j * n + i] * y[j];
            y[i] = s;
        }
        var x = new T[n];
        for (var i = 0; i < n; i++) x[_permutation[i]] = y[i];
        return new(x);
    }

    /// <summary>Solves A·X = B column by column.</summary>
    /// <exception cref="InvalidOperationException">The matrix is singular.</exception>
    public DenseMatrix<T> Solve(DenseMatrix<T> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.Rows != Size) throw new ArgumentException($"Expected {Size} rows.", nameof(b));
        var columns = Enumerable.Range(0, b.Columns).Select(j => Solve(b.Column(j))).ToArray();
        return DenseMatrix.FromColumns(columns);
    }

    /// <summary>The inverse A⁻¹.</summary>
    /// <exception cref="InvalidOperationException">The matrix is singular.</exception>
    public DenseMatrix<T> Inverse() => Solve(DenseMatrix.Identity<T>(Size));
}
