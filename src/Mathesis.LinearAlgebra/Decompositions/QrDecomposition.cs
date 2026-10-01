using System.Numerics;

namespace Mathesis.LinearAlgebra.Decompositions;

/// <summary>
/// The QR factorization A = Q·R of an m×n matrix with m ≥ n by Householder reflections (catalog <c>num.la.householder</c>,
/// backward stable), with Q (m×m) orthogonal and R (m×n) upper triangular. Least squares is solved through R, never through
/// the normal equations, which would square the condition number (<c>num.approx.least-squares</c>).
/// </summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public sealed class QrDecomposition<T>
    where T : IFloatingPointIeee754<T>
{
    private readonly T[][] _reflectors;
    private readonly T[] _betas;
    private readonly T[] _r;

    internal QrDecomposition(DenseMatrix<T> a)
    {
        if (a.Rows < a.Columns) throw new ArgumentException("QR factorization needs at least as many rows as columns.", nameof(a));
        var m = a.Rows;
        var n = a.Columns;
        Rows = m;
        Columns = n;
        _r = a.AsSpan().ToArray();
        _reflectors = new T[n][];
        _betas = new T[n];

        for (var k = 0; k < n; k++)
        {
            // Reflect column k below the diagonal onto a multiple of e1: v = x - alpha*e1, H = I - beta*v*vT.
            var norm = T.Zero;
            var scale = T.Zero;
            for (var i = k; i < m; i++) scale = T.Max(scale, T.Abs(_r[i * n + k]));
            if (scale != T.Zero)
            {
                var sum = T.Zero;
                for (var i = k; i < m; i++)
                {
                    var q = _r[i * n + k] / scale;
                    sum += q * q;
                }
                norm = scale * T.Sqrt(sum);
            }
            var v = new T[m - k];
            if (norm == T.Zero)
            {
                _reflectors[k] = v;
                _betas[k] = T.Zero;
                continue;
            }
            var x0 = _r[k * n + k];
            var alpha = x0 >= T.Zero ? -norm : norm;
            for (var i = k; i < m; i++) v[i - k] = _r[i * n + k];
            v[0] -= alpha;
            var vv = T.Zero;
            foreach (var e in v) vv += e * e;
            var beta = vv == T.Zero ? T.Zero : (T.One + T.One) / vv;
            _reflectors[k] = v;
            _betas[k] = beta;

            for (var j = k; j < n; j++)
            {
                var dot = T.Zero;
                for (var i = k; i < m; i++) dot += v[i - k] * _r[i * n + j];
                dot *= beta;
                for (var i = k; i < m; i++) _r[i * n + j] -= dot * v[i - k];
            }
            _r[k * n + k] = alpha;
            for (var i = k + 1; i < m; i++) _r[i * n + k] = T.Zero;
        }
    }

    /// <summary>The number of rows m of A.</summary>
    public int Rows { get; }

    /// <summary>The number of columns n of A.</summary>
    public int Columns { get; }

    /// <summary>The m×n upper-triangular factor R.</summary>
    public DenseMatrix<T> R => DenseMatrix.FromRowMajor<T>(Rows, Columns, _r);

    /// <summary>The n×n upper-triangular block of R used by the solver.</summary>
    public DenseMatrix<T> ThinR => R.Block(0, 0, Columns, Columns);

    // Applies H_0 H_1 ... H_{n-1} (transpose = false, giving Q·x) or the reverse order (transpose = true, giving Qᵀ·x) in place.
    private void Apply(T[] x, bool transpose)
    {
        for (var step = 0; step < Columns; step++)
        {
            var k = transpose ? step : Columns - 1 - step;
            var v = _reflectors[k];
            var dot = T.Zero;
            for (var i = k; i < Rows; i++) dot += v[i - k] * x[i];
            dot *= _betas[k];
            for (var i = k; i < Rows; i++) x[i] -= dot * v[i - k];
        }
    }

    /// <summary>The m×m orthogonal factor Q.</summary>
    public DenseMatrix<T> Q
    {
        get
        {
            var columns = new DenseVector<T>[Rows];
            for (var j = 0; j < Rows; j++)
            {
                var e = new T[Rows];
                e[j] = T.One;
                Apply(e, transpose: false);
                columns[j] = new(e);
            }
            return DenseMatrix.FromColumns(columns);
        }
    }

    /// <summary>The m×n factor Q₁ whose columns are an orthonormal basis of the column space of A.</summary>
    public DenseMatrix<T> ThinQ => Q.Block(0, 0, Rows, Columns);

    /// <summary>Whether R has a (numerically) zero diagonal entry, so A has rank below n.</summary>
    public bool IsRankDeficient
    {
        get
        {
            var largest = T.Zero;
            for (var i = 0; i < Columns; i++) largest = T.Max(largest, T.Abs(_r[i * Columns + i]));
            var threshold = T.CreateChecked(Rows) * (T.BitIncrement(T.One) - T.One) * largest;
            for (var i = 0; i < Columns; i++)
            {
                if (T.Abs(_r[i * Columns + i]) <= threshold) return true;
            }
            return false;
        }
    }

    /// <summary>Rebuilds the original matrix Q·R.</summary>
    public DenseMatrix<T> Reconstruct() => Q * R;

    /// <summary>The Frobenius norm of <paramref name="original"/> − Q·R.</summary>
    public T Residual(DenseMatrix<T> original) => (original - Reconstruct()).NormFrobenius();

    /// <summary>
    /// The vector x minimizing ‖A·x − b‖₂ (the solution of A·x = b when A is square and invertible).
    /// </summary>
    /// <exception cref="InvalidOperationException">A has rank below n.</exception>
    public DenseVector<T> Solve(DenseVector<T> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.Length != Rows) throw new ArgumentException($"Expected a vector of length {Rows}.", nameof(b));
        if (IsRankDeficient) throw new InvalidOperationException("The matrix is rank deficient.");
        var y = b.ToArray();
        Apply(y, transpose: true);
        var n = Columns;
        var x = new T[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var s = y[i];
            for (var j = i + 1; j < n; j++) s -= _r[i * n + j] * x[j];
            x[i] = s / _r[i * n + i];
        }
        return new(x);
    }
}
