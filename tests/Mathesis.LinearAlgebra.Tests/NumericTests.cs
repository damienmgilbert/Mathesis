using System.Numerics;
using Mathesis.LinearAlgebra;
using Mathesis.LinearAlgebra.Exact;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Testing;

namespace Mathesis.LinearAlgebra.Tests;

[TestClass]
public class NumericTests
{
    private const int Seed = 20261021;
    private static readonly double Eps = Math.BitIncrement(1.0) - 1.0;

    // Diagonally dominant, hence well conditioned.
    private static DenseMatrix<double> WellConditioned(Gen gen, int n) =>
        DenseMatrix.Create(n, n, (i, j) => gen.Uniform(-1, 1) + (i == j ? n + 1.0 : 0.0));

    private static DenseMatrix<double> RandomMatrix(Gen gen, int rows, int columns) =>
        DenseMatrix.Create(rows, columns, (_, _) => gen.Uniform(-1, 1));

    private static DenseVector<double> RandomVector(Gen gen, int n) => DenseVector.Create(n, _ => gen.Uniform(-5, 5));

    private static DenseMatrix<double> Spd(Gen gen, int n)
    {
        var b = RandomMatrix(gen, n, n);
        return b.Transpose() * b + DenseMatrix.Identity<double>(n);
    }

    private static T Unwrap<T>(Outcome<T> outcome) =>
        outcome.TryGetValue(out var value) ? value : throw new AssertFailedException($"Expected a value but got {outcome}.");

    [TestMethod]
    public void ResidualsOfWellConditionedSystemsAreTiny()
    {
        // The Phase 3 exit check: ||Ax - b|| <= 1e-12 ||b||.
        var gen = new Gen(Seed);
        for (var i = 0; i < 1_000; i++)
        {
            var n = gen.Random.Next(1, 41);
            var a = WellConditioned(gen, n);
            var b = RandomVector(gen, n);
            var ctx = $"seed={Seed} case={i} n={n}";
            var limit = 1e-12 * b.Norm2();

            var viaLu = Unwrap(a.Solve(b));
            Assert.IsTrue((a * viaLu - b).Norm2() <= limit, $"{ctx}: LU");

            var viaQr = a.Qr().Solve(b);
            Assert.IsTrue((a * viaQr - b).Norm2() <= limit, $"{ctx}: QR");

            var spd = Spd(gen, n);
            var cholesky = spd.Cholesky();
            Assert.IsTrue(cholesky.IsPositiveDefinite, ctx);
            Assert.IsTrue((spd * cholesky.Solve(b) - b).Norm2() <= limit * n, $"{ctx}: Cholesky");

            // The three agree.
            Assert.IsTrue((viaLu - viaQr).Norm2() <= 1e-10 * viaLu.Norm2(), ctx);
        }
    }

    [TestMethod]
    public void LuReconstructsAndTracksTheDeterminant()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 500; i++)
        {
            var n = gen.Random.Next(1, 13);
            var a = RandomMatrix(gen, n, n);
            var lu = a.Lu();
            var ctx = $"seed={Seed + 1} case={i} n={n}";
            if (lu.IsSingular) continue;

            Assert.IsTrue(lu.Residual(a) <= 8 * n * Eps * a.NormFrobenius(), ctx);
            // P*A = L*U with a genuine permutation matrix and unit lower L.
            Assert.IsTrue((lu.PermutationMatrix * a - lu.Lower * lu.Upper).NormFrobenius() <= 8 * n * Eps * a.NormFrobenius(), ctx);
            for (var r = 0; r < n; r++) Assert.AreEqual(1.0, lu.Lower[r, r], ctx);
            Assert.AreEqual(n, lu.Permutation.Distinct().Count(), ctx);

            // Compare with the exact determinant of the same (dyadic rational) entries.
            var exact = ExactLinearAlgebra.Determinant(DenseMatrix.Create(n, n, (r, c) => BigRational.FromDouble(a[r, c])));
            Assert.AreEqual((double)exact, a.Determinant(), 1e-9 * Math.Max(1.0, Math.Abs((double)exact)), ctx);
        }
    }

    [TestMethod]
    public void InversesAndMatrixRightHandSides()
    {
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 16);
            var a = WellConditioned(gen, n);
            var inverse = Unwrap(a.Inverse());
            var ctx = $"seed={Seed + 2} case={i} n={n}";
            Assert.IsTrue((a * inverse - DenseMatrix.Identity<double>(n)).NormFrobenius() <= 1e-12 * n, ctx);
            Assert.IsTrue((inverse * a - DenseMatrix.Identity<double>(n)).NormFrobenius() <= 1e-12 * n, ctx);

            var b = RandomMatrix(gen, n, 3);
            var x = a.Lu().Solve(b);
            Assert.IsTrue((a * x - b).NormFrobenius() <= 1e-12 * b.NormFrobenius(), ctx);

            // Aᵀ x = b through SolveTranspose.
            var v = RandomVector(gen, n);
            Assert.IsTrue((a.Transpose() * a.Lu().SolveTranspose(v) - v).Norm2() <= 1e-12 * v.Norm2(), ctx);
        }
    }

    [TestMethod]
    public void SingularMatricesAreReportedNotThrown()
    {
        var singular = DenseMatrix.FromRows(new[,] { { 1.0, 2.0 }, { 2.0, 4.0 } });
        Assert.IsInstanceOfType<Outcome<DenseVector<double>>.Failed>(singular.Solve(DenseVector.Create(1.0, 2.0)));
        Assert.IsInstanceOfType<Outcome<DenseMatrix<double>>.Failed>(singular.Inverse());
        Assert.AreEqual(0.0, singular.Determinant());
        Assert.AreEqual(double.PositiveInfinity, singular.ConditionEstimate());
        Assert.IsTrue(singular.Lu().IsSingular);
        Assert.Throws<InvalidOperationException>(() => singular.Lu().Solve(DenseVector.Create(1.0, 2.0)));
        Assert.Throws<ArgumentException>(() => DenseMatrix.Zero<double>(2, 3).Lu());
        Assert.Throws<ArgumentException>(() => DenseMatrix.Identity<double>(2).Lu().Solve(DenseVector.Create(1.0)));

        var rankDeficient = DenseMatrix.FromRows(new[,] { { 1.0, 2.0 }, { 2.0, 4.0 }, { 3.0, 6.0 } });
        Assert.IsInstanceOfType<Outcome<LeastSquaresSolution<double>>.Failed>(rankDeficient.LeastSquares(DenseVector.Create(1.0, 1.0, 1.0)));
        Assert.IsTrue(rankDeficient.Qr().IsRankDeficient);
    }

    [TestMethod]
    public void QrIsOrthogonalAndReconstructs()
    {
        var gen = new Gen(Seed + 3);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 9);
            var m = n + gen.Random.Next(0, 8);
            var a = RandomMatrix(gen, m, n);
            var qr = a.Qr();
            var ctx = $"seed={Seed + 3} case={i} {m}x{n}";

            var q = qr.Q;
            Assert.IsTrue((q.Transpose() * q - DenseMatrix.Identity<double>(m)).NormFrobenius() <= 1e-13 * m, ctx);
            Assert.IsTrue(qr.Residual(a) <= 1e-13 * m * Math.Max(1.0, a.NormFrobenius()), ctx);
            for (var r = 0; r < m; r++)
            {
                for (var c = 0; c < Math.Min(r, n); c++) Assert.AreEqual(0.0, qr.R[r, c], ctx);
            }
            Assert.AreEqual(new Shape(m, n), qr.ThinQ.Shape);
            Assert.AreEqual(new Shape(n, n), qr.ThinR.Shape);
            Assert.IsTrue((qr.ThinQ * qr.ThinR - a).NormFrobenius() <= 1e-13 * m * Math.Max(1.0, a.NormFrobenius()), ctx);
        }
        Assert.Throws<ArgumentException>(() => DenseMatrix.Zero<double>(2, 3).Qr());
    }

    [TestMethod]
    public void LeastSquaresMatchesTheNormalEquationsAndIsOrthogonalToTheRange()
    {
        var gen = new Gen(Seed + 4);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 7);
            var m = n + gen.Random.Next(1, 10);
            var a = RandomMatrix(gen, m, n) + DenseMatrix.Create(m, n, (r, c) => r == c ? 2.0 : 0.0);
            var b = RandomVector(gen, m);
            var solution = Unwrap(a.LeastSquares(b));
            var ctx = $"seed={Seed + 4} case={i} {m}x{n}";

            // Normal equations AᵀA x = Aᵀ b (fine here: the problems are well conditioned).
            var normal = Unwrap((a.Transpose() * a).Solve(a.Transpose() * b));
            Assert.IsTrue((solution.Solution - normal).Norm2() <= 1e-8 * Math.Max(1.0, normal.Norm2()), ctx);

            // The residual is orthogonal to every column of A, and its norm is reported correctly.
            var residual = a * solution.Solution - b;
            Assert.AreEqual(residual.Norm2(), solution.ResidualNorm, 1e-12, ctx);
            Assert.IsTrue((a.Transpose() * residual).Norm2() <= 1e-10 * Math.Max(1.0, residual.Norm2() * a.NormFrobenius()), ctx);

            // A consistent system is solved to rounding error.
            var truth = RandomVector(gen, n);
            var exact = Unwrap(a.LeastSquares(a * truth));
            Assert.IsTrue((exact.Solution - truth).Norm2() <= 1e-10, ctx);
            Assert.IsTrue(exact.ResidualNorm <= 1e-10, ctx);
        }

        // Fit a line through (0, 1), (1, 3), (2, 5), (3, 7) exactly: y = 1 + 2x.
        var design = DenseMatrix.Create(4, 2, (r, c) => c == 0 ? 1.0 : r);
        var line = Unwrap(design.LeastSquares(DenseVector.Create(1.0, 3.0, 5.0, 7.0)));
        Assert.AreEqual(1.0, line.Solution[0], 1e-13);
        Assert.AreEqual(2.0, line.Solution[1], 1e-13);
    }

    [TestMethod]
    public void CholeskyFactorsExactlyTheSymmetricPositiveDefiniteMatrices()
    {
        var gen = new Gen(Seed + 5);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 13);
            var a = Spd(gen, n);
            var chol = a.Cholesky();
            var ctx = $"seed={Seed + 5} case={i} n={n}";
            Assert.IsTrue(chol.IsPositiveDefinite, ctx);
            Assert.IsTrue(chol.Residual(a) <= 8 * n * Eps * a.NormFrobenius(), ctx);
            for (var r = 0; r < n; r++)
            {
                Assert.IsTrue(chol.L[r, r] > 0, ctx);
                for (var c = r + 1; c < n; c++) Assert.AreEqual(0.0, chol.L[r, c], ctx);
            }
            Assert.AreEqual(a.Determinant(), chol.Determinant, 1e-9 * Math.Abs(chol.Determinant), ctx);
        }

        // Indefinite and non-square inputs.
        Assert.IsFalse(DenseMatrix.FromRows(new[,] { { 1.0, 2.0 }, { 2.0, 1.0 } }).Cholesky().IsPositiveDefinite);
        Assert.IsFalse(DenseMatrix.FromRows(new[,] { { 0.0, 0.0 }, { 0.0, 1.0 } }).Cholesky().IsPositiveDefinite);
        var notPd = DenseMatrix.FromRows(new[,] { { 1.0, 2.0 }, { 2.0, 1.0 } }).Cholesky();
        Assert.Throws<InvalidOperationException>(() => notPd.Solve(DenseVector.Create(1.0, 1.0)));
        Assert.Throws<InvalidOperationException>(() => _ = notPd.L);
        Assert.Throws<ArgumentException>(() => DenseMatrix.Zero<double>(2, 3).Cholesky());
    }

    [TestMethod]
    public void JacobiEigenvaluesOfSymmetricMatrices()
    {
        var two = DenseMatrix.FromRows(new[,] { { 2.0, 1.0 }, { 1.0, 2.0 } }).SymmetricEigen();
        Assert.AreEqual(1.0, two.Values[0], 1e-14);
        Assert.AreEqual(3.0, two.Values[1], 1e-14);

        var gen = new Gen(Seed + 6);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 11);
            var b = RandomMatrix(gen, n, n);
            var a = (b + b.Transpose()) * 0.5;
            var eigen = a.SymmetricEigen();
            var ctx = $"seed={Seed + 6} case={i} n={n}";
            Assert.IsTrue(eigen.Converged, ctx);

            // A V = V Λ and VᵀV = I, eigenvalues ascending, trace preserved.
            var lambda = DenseMatrix.Diagonal<double>(eigen.Values.ToArray());
            Assert.IsTrue((a * eigen.Vectors - eigen.Vectors * lambda).NormFrobenius() <= 1e-12 * n * Math.Max(1.0, a.NormFrobenius()), ctx);
            Assert.IsTrue((eigen.Vectors.Transpose() * eigen.Vectors - DenseMatrix.Identity<double>(n)).NormFrobenius() <= 1e-12 * n, ctx);
            for (var k = 1; k < n; k++) Assert.IsTrue(eigen.Values[k - 1] <= eigen.Values[k], ctx);
            Assert.AreEqual(a.Trace(), eigen.Values.Sum(), 1e-12 * n, ctx);
        }

        // Repeated eigenvalues and a diagonal matrix.
        var identity = DenseMatrix.Identity<double>(4).SymmetricEigen();
        Assert.IsTrue(identity.Converged);
        Assert.IsTrue(identity.Values.All(v => v == 1.0));
        Assert.IsTrue(DenseMatrix.Zero<double>(3, 3).SymmetricEigen().Converged);
        Assert.Throws<ArgumentException>(() => DenseMatrix.FromRows(new[,] { { 1.0, 2.0 }, { 0.0, 1.0 } }).SymmetricEigen());
        Assert.Throws<ArgumentException>(() => DenseMatrix.Zero<double>(2, 3).SymmetricEigen());
    }

    [TestMethod]
    public void JacobiAgreesWithTheCharacteristicPolynomialRoots()
    {
        // Three independent components agree: exact Berkowitz polynomial, Aberth roots, Jacobi eigenvalues.
        var gen = new Gen(Seed + 7);
        for (var i = 0; i < 200; i++)
        {
            var n = gen.Random.Next(2, 7);
            var upper = DenseMatrix.Create(n, n, (_, _) => new BigRational(gen.Random.Next(-4, 5)));
            var symmetric = upper + upper.Transpose();
            var p = ExactLinearAlgebra.CharacteristicPolynomial(symmetric);
            var roots = PolynomialAlgorithms.AberthRoots(p.Map(r => (double)r)).Roots.Select(z => z.Real).OrderBy(x => x).ToArray();
            var eigenvalues = DenseMatrix.Create(n, n, (r, c) => (double)symmetric[r, c]).SymmetricEigen().Values;
            var ctx = $"seed={Seed + 7} case={i}\n{symmetric}";
            for (var k = 0; k < n; k++) Assert.AreEqual(eigenvalues[k], roots[k], 1e-4, ctx);
        }
    }

    [TestMethod]
    public void ConditionEstimateIsCloseToTheTrueCondition()
    {
        // Hilbert(8): kappa_1 is about 3.4e10, and the exact inverse lets us compute it precisely.
        var exact = DenseMatrix.Create(8, 8, (i, j) => BigRational.Create(1, i + j + 1));
        var exactInverse = ExactLinearAlgebra.Inverse(exact).Unwrap();
        var hilbert = DenseMatrix.Create(8, 8, (i, j) => 1.0 / (i + j + 1));
        double Norm1(DenseMatrix<BigRational> m) => Enumerable.Range(0, m.Columns).Max(j => Enumerable.Range(0, m.Rows).Sum(i => Math.Abs((double)m[i, j])));
        var trueCondition = Norm1(exact) * Norm1(exactInverse);
        Assert.IsTrue(trueCondition is > 3e10 and < 4e10, $"{trueCondition:E3}");
        var estimate = hilbert.ConditionEstimate();
        Assert.IsTrue(estimate <= trueCondition * 1.001 && estimate >= trueCondition / 3, $"estimate {estimate:E3} vs true {trueCondition:E3}");

        // For random matrices the estimate never exceeds the true value (it is a lower bound) and is within a factor of 10.
        var gen = new Gen(Seed + 8);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(2, 13);
            var a = RandomMatrix(gen, n, n);
            if (a.Lu().IsSingular) continue;
            var inverse = Unwrap(a.Inverse());
            var truth = a.Norm1() * inverse.Norm1();
            var guess = a.ConditionEstimate();
            Assert.IsTrue(guess <= truth * (1 + 1e-8) && guess >= truth / 10, $"seed={Seed + 8} case={i}: estimate {guess:E3} vs true {truth:E3}");
        }
        Assert.AreEqual(1.0, DenseMatrix.Identity<double>(5).ConditionEstimate(), 1e-12);
    }
}
