using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Mathesis.LinearAlgebra.Decompositions;

namespace Mathesis.LinearAlgebra;

/// <summary>The solution of a linear least-squares problem.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Solution">The vector x minimizing ‖A·x − b‖₂.</param>
/// <param name="ResidualNorm">‖A·x − b‖₂.</param>
public sealed record LeastSquaresSolution<T>(DenseVector<T> Solution, T ResidualNorm)
    where T : IFloatingPointIeee754<T>;

/// <summary>
/// Numerical linear algebra on floating-point <see cref="DenseMatrix{T}"/> values: factorizations, solves, determinant,
/// inverse, least squares, symmetric eigenvalues and a condition-number estimate.
/// </summary>
/// <remarks>
/// Singular or rank-deficient problems are reported as <see cref="Outcome{T}.Failed"/> values, not exceptions. Exact
/// counterparts that also work for symbolic entries are in <c>Mathesis.LinearAlgebra.Exact</c>.
/// </remarks>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Extension members on DenseMatrix<T> (docs/design/03-namespaces-and-packages.md).")]
public static class MatrixSolvers
{
    extension<T>(DenseMatrix<T> a) where T : IFloatingPointIeee754<T>
    {
        /// <summary>The LU factorization P·A = L·U with partial pivoting; A must be square.</summary>
        public LuDecomposition<T> Lu() => new(a);

        /// <summary>The Householder QR factorization A = Q·R; A must have at least as many rows as columns.</summary>
        public QrDecomposition<T> Qr() => new(a);

        /// <summary>The Cholesky factorization A = L·Lᵀ; check <see cref="CholeskyDecomposition{T}.IsPositiveDefinite"/> before using it.</summary>
        public CholeskyDecomposition<T> Cholesky() => new(a);

        /// <summary>
        /// The eigenvalues (ascending) and orthonormal eigenvectors of a real symmetric matrix by cyclic Jacobi rotations.
        /// </summary>
        /// <exception cref="ArgumentException">The matrix is not square or not symmetric.</exception>
        public SymmetricEigen<T> SymmetricEigen(int maxSweeps = 60) => Jacobi.Solve(a, maxSweeps);

        /// <summary>Solves A·x = b for square A by LU factorization; fails when a zero pivot shows A is singular.</summary>
        public Outcome<DenseVector<T>> Solve(DenseVector<T> b)
        {
            var lu = a.Lu();
            return lu.IsSingular ? Outcome.Fail<DenseVector<T>>(MathError.Domain("The matrix is singular.")) : Outcome.Ok(lu.Solve(b));
        }

        /// <summary>The determinant by LU factorization (0 for a matrix with a zero pivot).</summary>
        public T Determinant()
        {
            var lu = a.Lu();
            return lu.IsSingular ? T.Zero : lu.Determinant;
        }

        /// <summary>The inverse; fails when a zero pivot shows the matrix is singular.</summary>
        public Outcome<DenseMatrix<T>> Inverse()
        {
            var lu = a.Lu();
            return lu.IsSingular ? Outcome.Fail<DenseMatrix<T>>(MathError.Domain("The matrix is singular.")) : Outcome.Ok(lu.Inverse());
        }

        /// <summary>
        /// The least-squares solution of A·x ≈ b for an m×n matrix with m ≥ n and full column rank, by Householder QR; fails when
        /// A is rank deficient.
        /// </summary>
        public Outcome<LeastSquaresSolution<T>> LeastSquares(DenseVector<T> b)
        {
            var qr = a.Qr();
            if (qr.IsRankDeficient) return Outcome.Fail<LeastSquaresSolution<T>>(MathError.Domain("The matrix is rank deficient."));
            var x = qr.Solve(b);
            return Outcome.Ok(new LeastSquaresSolution<T>(x, (a * x - b).Norm2()));
        }

        /// <summary>
        /// An estimate of the 1-norm condition number κ₁(A) = ‖A‖₁·‖A⁻¹‖₁ (catalog <c>linalg.norm.condition</c>), using Hager's
        /// algorithm: a few solves with A and Aᵀ instead of forming A⁻¹. It is a lower bound that is usually within a factor of
        /// 3 of the true value; <c>+∞</c> for a singular matrix. Expect to lose about log₁₀ κ digits (<c>num.la.perturbation</c>).
        /// </summary>
        public T ConditionEstimate()
        {
            var lu = a.Lu();
            if (lu.IsSingular) return T.PositiveInfinity;
            var n = a.Rows;
            var x = new T[n];
            Array.Fill(x, T.One / T.CreateChecked(n));
            var estimate = T.Zero;
            for (var iteration = 0; iteration < 5; iteration++)
            {
                var y = lu.Solve(new DenseVector<T>(x));
                estimate = T.Max(estimate, y.Norm1());
                var xi = new DenseVector<T>(y.Select(v => v >= T.Zero ? T.One : -T.One));
                var z = lu.SolveTranspose(xi);
                var best = 0;
                var zx = T.Zero;
                for (var j = 0; j < n; j++)
                {
                    zx += z[j] * x[j];
                    if (T.Abs(z[j]) > T.Abs(z[best])) best = j;
                }
                if (T.Abs(z[best]) <= zx) break;
                Array.Fill(x, T.Zero);
                x[best] = T.One;
            }
            return a.Norm1() * estimate;
        }
    }
}
