using System.Collections.Immutable;
using System.Numerics;

namespace Mathesis.LinearAlgebra.Decompositions;

/// <summary>
/// The Cholesky factorization A = L·Lᵀ of a symmetric positive-definite matrix (catalog <c>linalg.dec.cholesky</c>; about n³/3
/// flops, half the cost of LU). Only the lower triangle of A is read.
/// </summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public sealed class CholeskyDecomposition<T>
    where T : IFloatingPointIeee754<T>
{
    private readonly T[] _l;

    internal CholeskyDecomposition(DenseMatrix<T> a)
    {
        if (a.Rows != a.Columns) throw new ArgumentException("Cholesky factorization needs a square matrix.", nameof(a));
        var n = a.Rows;
        Size = n;
        _l = new T[n * n];
        IsPositiveDefinite = true;
        for (var j = 0; j < n && IsPositiveDefinite; j++)
        {
            var d = a[j, j];
            for (var k = 0; k < j; k++) d -= _l[j * n + k] * _l[j * n + k];
            if (!(d > T.Zero) || !T.IsFinite(d))
            {
                IsPositiveDefinite = false;
                break;
            }
            var diagonal = T.Sqrt(d);
            _l[j * n + j] = diagonal;
            for (var i = j + 1; i < n; i++)
            {
                var s = a[i, j];
                for (var k = 0; k < j; k++) s -= _l[i * n + k] * _l[j * n + k];
                _l[i * n + j] = s / diagonal;
            }
        }
    }

    /// <summary>The matrix dimension n.</summary>
    public int Size { get; }

    /// <summary>Whether the matrix is (numerically) symmetric positive definite; when <c>false</c> no factor is available.</summary>
    public bool IsPositiveDefinite { get; }

    /// <summary>The lower-triangular factor L with positive diagonal.</summary>
    /// <exception cref="InvalidOperationException">The matrix is not positive definite.</exception>
    public DenseMatrix<T> L => IsPositiveDefinite ? DenseMatrix.FromRowMajor<T>(Size, Size, _l) : throw new InvalidOperationException("The matrix is not positive definite.");

    /// <summary>The determinant, ∏ Lᵢᵢ².</summary>
    /// <exception cref="InvalidOperationException">The matrix is not positive definite.</exception>
    public T Determinant
    {
        get
        {
            if (!IsPositiveDefinite) throw new InvalidOperationException("The matrix is not positive definite.");
            var d = T.One;
            for (var i = 0; i < Size; i++) d *= _l[i * Size + i] * _l[i * Size + i];
            return d;
        }
    }

    /// <summary>Rebuilds the original matrix L·Lᵀ.</summary>
    public DenseMatrix<T> Reconstruct() => L * L.Transpose();

    /// <summary>The Frobenius norm of <paramref name="original"/> − L·Lᵀ.</summary>
    public T Residual(DenseMatrix<T> original) => (original - Reconstruct()).NormFrobenius();

    /// <summary>Solves A·x = b by two triangular solves.</summary>
    /// <exception cref="InvalidOperationException">The matrix is not positive definite.</exception>
    public DenseVector<T> Solve(DenseVector<T> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.Length != Size) throw new ArgumentException($"Expected a vector of length {Size}.", nameof(b));
        if (!IsPositiveDefinite) throw new InvalidOperationException("The matrix is not positive definite.");
        var n = Size;
        var y = new T[n];
        for (var i = 0; i < n; i++)
        {
            var s = b[i];
            for (var j = 0; j < i; j++) s -= _l[i * n + j] * y[j];
            y[i] = s / _l[i * n + i];
        }
        for (var i = n - 1; i >= 0; i--)
        {
            var s = y[i];
            for (var j = i + 1; j < n; j++) s -= _l[j * n + i] * y[j];
            y[i] = s / _l[i * n + i];
        }
        return new(y);
    }
}

/// <summary>The eigenvalues and eigenvectors of a real symmetric matrix.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Values">The eigenvalues in ascending order.</param>
/// <param name="Vectors">An orthogonal matrix whose column j is a unit eigenvector for <c>Values[j]</c>.</param>
/// <param name="Sweeps">Jacobi sweeps performed.</param>
/// <param name="Converged"><c>true</c> if the off-diagonal part fell below the tolerance.</param>
public sealed record SymmetricEigen<T>(ImmutableArray<T> Values, DenseMatrix<T> Vectors, int Sweeps, bool Converged)
    where T : IFloatingPointIeee754<T>;

/// <summary>Eigenvalue algorithms for symmetric matrices.</summary>
internal static class Jacobi
{
    /// <summary>Cyclic Jacobi rotations (catalog <c>num.eig.jacobi</c>): accurate, simple and quadratically convergent.</summary>
    public static SymmetricEigen<T> Solve<T>(DenseMatrix<T> a, int maxSweeps)
        where T : IFloatingPointIeee754<T>
    {
        if (a.Rows != a.Columns) throw new ArgumentException("Eigenvalues need a square matrix.", nameof(a));
        var n = a.Rows;
        var eps = T.BitIncrement(T.One) - T.One;
        var norm = a.NormFrobenius();
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (T.Abs(a[i, j] - a[j, i]) > T.CreateChecked(1000) * eps * norm) throw new ArgumentException("The matrix is not symmetric.", nameof(a));
            }
        }

        var m = a.AsSpan().ToArray();
        var v = new T[n * n];
        for (var i = 0; i < n; i++) v[i * n + i] = T.One;
        var two = T.One + T.One;
        var sweeps = 0;
        var converged = n <= 1;

        T OffDiagonal()
        {
            var sum = T.Zero;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++) sum += m[i * n + j] * m[i * n + j];
            }
            return T.Sqrt(two * sum);
        }

        while (!converged && sweeps < maxSweeps)
        {
            sweeps++;
            for (var p = 0; p < n - 1; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    var apq = m[p * n + q];
                    if (apq == T.Zero) continue;
                    var theta = (m[q * n + q] - m[p * n + p]) / (two * apq);
                    var t = (theta >= T.Zero ? T.One : -T.One) / (T.Abs(theta) + T.Sqrt(theta * theta + T.One));
                    var c = T.One / T.Sqrt(t * t + T.One);
                    var s = t * c;
                    for (var k = 0; k < n; k++)
                    {
                        var kp = m[k * n + p];
                        var kq = m[k * n + q];
                        m[k * n + p] = c * kp - s * kq;
                        m[k * n + q] = s * kp + c * kq;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        var pk = m[p * n + k];
                        var qk = m[q * n + k];
                        m[p * n + k] = c * pk - s * qk;
                        m[q * n + k] = s * pk + c * qk;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        var kp = v[k * n + p];
                        var kq = v[k * n + q];
                        v[k * n + p] = c * kp - s * kq;
                        v[k * n + q] = s * kp + c * kq;
                    }
                }
            }
            converged = OffDiagonal() <= eps * norm || norm == T.Zero;
        }

        var order = Enumerable.Range(0, n).OrderBy(i => m[i * n + i]).ToArray();
        var values = order.Select(i => m[i * n + i]).ToImmutableArray();
        var vectors = DenseMatrix.Create(n, n, (i, j) => v[i * n + order[j]]);
        return new(values, vectors, sweeps, converged);
    }
}
