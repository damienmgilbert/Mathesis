using System.Numerics;
using Mathesis.LinearAlgebra;
using Mathesis.LinearAlgebra.Exact;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Testing;

namespace Mathesis.LinearAlgebra.Tests;

[TestClass]
public class ExactTests
{
    private const int Seed = 20261020;

    private static BigRational R(string text) => BigRational.Parse(text);

    private static DenseMatrix<BigRational> RandomMatrix(Gen gen, int rows, int columns, int bits = 6) =>
        DenseMatrix.Create(rows, columns, (_, _) => gen.Rational(bits));

    // A matrix of rank at most `rank`, as a product of thin random factors.
    private static DenseMatrix<BigRational> RandomWithRank(Gen gen, int rows, int columns, int rank) =>
        RandomMatrix(gen, rows, rank) * RandomMatrix(gen, rank, columns);

    private static DenseMatrix<BigRational> Hilbert(int n) => DenseMatrix.Create(n, n, (i, j) => BigRational.Create(1, i + j + 1));

    private static BigInteger Binomial(int n, int k)
    {
        if (k < 0 || k > n) return BigInteger.Zero;
        var result = BigInteger.One;
        for (var i = 1; i <= k; i++) result = result * (n - k + i) / i;
        return result;
    }

    [TestMethod]
    public void HilbertEightInverseIsExact()
    {
        // The Phase 3 exit check: the inverse of the (very ill-conditioned) Hilbert matrix is exact over BigRational.
        const int n = 8;
        var h = Hilbert(n);
        var inverse = ExactLinearAlgebra.Inverse(h).Unwrap();
        Assert.AreEqual(DenseMatrix.Identity<BigRational>(n), h * inverse);
        Assert.AreEqual(DenseMatrix.Identity<BigRational>(n), inverse * h);

        // Closed form (1-based i, j): (-1)^(i+j) (i+j-1) C(n+i-1, n-j) C(n+j-1, n-i) C(i+j-2, i-1)^2.
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var expected = (((i + j) % 2 == 0) ? 1 : -1) * (i + j - 1) * Binomial(n + i - 1, n - j) * Binomial(n + j - 1, n - i) * BigInteger.Pow(Binomial(i + j - 2, i - 1), 2);
                Assert.AreEqual(new BigRational(expected), inverse[i - 1, j - 1], $"entry ({i}, {j})");
                Assert.IsTrue(inverse[i - 1, j - 1].IsInteger);
            }
        }
        Assert.AreEqual(new BigRational(64), inverse[0, 0]);

        // The determinant is 1/(a huge integer): exact and positive.
        var det = ExactLinearAlgebra.Determinant(h);
        Assert.AreEqual(BigRational.One, det * ExactLinearAlgebra.Determinant(inverse));
    }

    [TestMethod]
    public void RecordedRowOperationsReproduceEachRref()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 1_000; i++)
        {
            var rows = gen.Random.Next(1, 7);
            var columns = gen.Random.Next(1, 7);
            var a = gen.Random.Next(3) switch
            {
                0 => RandomMatrix(gen, rows, columns),
                1 => RandomWithRank(gen, rows, columns, gen.Random.Next(0, Math.Min(rows, columns) + 1)),
                _ => DenseMatrix.Create(rows, columns, (_, _) => gen.Random.Next(3) == 0 ? BigRational.One : BigRational.Zero),
            };
            var reduction = ExactLinearAlgebra.RowReduce(a);
            var ctx = $"seed={Seed} case={i}\n{a}";

            // Replaying the operations on the original reproduces the RREF exactly.
            Assert.AreEqual(reduction.Reduced, reduction.Replay(a), ctx);

            // The result really is reduced row echelon form.
            var rank = reduction.Rank;
            for (var row = 0; row < rows; row++)
            {
                if (row < rank)
                {
                    var pivot = reduction.PivotColumns[row];
                    Assert.AreEqual(BigRational.One, reduction.Reduced[row, pivot], ctx);
                    for (var other = 0; other < rows; other++)
                    {
                        if (other != row) Assert.AreEqual(BigRational.Zero, reduction.Reduced[other, pivot], ctx);
                    }
                    for (var column = 0; column < pivot; column++) Assert.AreEqual(BigRational.Zero, reduction.Reduced[row, column], ctx);
                }
                else
                {
                    Assert.IsTrue(reduction.Reduced.RowSpan(row).ToArray().All(x => x == BigRational.Zero), ctx);
                }
            }
            for (var p = 1; p < rank; p++) Assert.IsTrue(reduction.PivotColumns[p] > reduction.PivotColumns[p - 1], ctx);

            // Rank is invariant under transposition, and rank + nullity = number of columns.
            Assert.AreEqual(rank, ExactLinearAlgebra.Rank(a.Transpose()), ctx);
            var nullSpace = ExactLinearAlgebra.NullSpace(a);
            Assert.AreEqual(columns - rank, nullSpace.Length, ctx);
            foreach (var v in nullSpace) Assert.IsTrue((a * v).All(x => x == BigRational.Zero), ctx);
            if (nullSpace.Length > 0) Assert.AreEqual(nullSpace.Length, ExactLinearAlgebra.Rank(DenseMatrix.FromColumns(nullSpace)), ctx);

            // The column space basis has `rank` independent columns of A.
            var columnSpace = ExactLinearAlgebra.ColumnSpace(a);
            Assert.AreEqual(rank, columnSpace.Length, ctx);
            if (rank > 0) Assert.AreEqual(rank, ExactLinearAlgebra.Rank(DenseMatrix.FromColumns(columnSpace)), ctx);
            // Reducing an already reduced matrix records no operations.
            Assert.AreEqual(0, ExactLinearAlgebra.RowReduce(reduction.Reduced).Operations.Length, ctx);
        }
    }

    [TestMethod]
    public void RowOperationDescriptionsAndKnownRref()
    {
        // [[1, 2, 3], [4, 5, 6], [7, 8, 9]] has rank 2 and RREF [[1, 0, -1], [0, 1, 2], [0, 0, 0]].
        var a = DenseMatrix.FromRows(new BigRational[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } });
        var reduction = ExactLinearAlgebra.RowReduce(a);
        Assert.AreEqual(2, reduction.Rank);
        Assert.AreEqual(DenseMatrix.FromRows(new BigRational[,] { { 1, 0, -1 }, { 0, 1, 2 }, { 0, 0, 0 } }), reduction.Reduced);
        var null1 = ExactLinearAlgebra.NullSpace(a).Single();
        Assert.AreEqual(DenseVector.Create<BigRational>(1, -2, 1), null1);

        // A swap is needed when the first pivot is zero.
        var swapped = ExactLinearAlgebra.RowReduce(DenseMatrix.FromRows(new BigRational[,] { { 0, 1 }, { 1, 0 } }));
        Assert.AreEqual(RowOperationKind.Swap, swapped.Operations[0].Kind);
        Assert.AreEqual("R1 ↔ R2", swapped.Operations[0].ToString());
        Assert.AreEqual("R2 ← R2 + (-4)·R1", reduction.Operations.First(o => o.Kind == RowOperationKind.AddMultiple && o.Row == 1).ToString());
    }

    [TestMethod]
    public void BareissDeterminantAgreesWithCofactorExpansion()
    {
        BigRational Cofactor(DenseMatrix<BigRational> m)
        {
            if (m.Rows == 1) return m[0, 0];
            var sum = BigRational.Zero;
            for (var j = 0; j < m.Columns; j++)
            {
                var minor = DenseMatrix.Create(m.Rows - 1, m.Columns - 1, (r, c) => m[r + 1, c < j ? c : c + 1]);
                sum += (j % 2 == 0 ? BigRational.One : -BigRational.One) * m[0, j] * Cofactor(minor);
            }
            return sum;
        }

        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 500; i++)
        {
            var n = gen.Random.Next(1, 6);
            var a = gen.Random.Next(4) == 0 ? RandomWithRank(gen, n, n, gen.Random.Next(0, n + 1)) : RandomMatrix(gen, n, n);
            var b = RandomMatrix(gen, n, n);
            var ctx = $"seed={Seed + 1} case={i}\n{a}";
            var det = ExactLinearAlgebra.Determinant(a);

            Assert.AreEqual(Cofactor(a), det, ctx);
            Assert.AreEqual(det, ExactLinearAlgebra.Determinant(a.Transpose()), ctx);
            Assert.AreEqual(det * ExactLinearAlgebra.Determinant(b), ExactLinearAlgebra.Determinant(a * b), ctx);
            var c = gen.NonZeroRational(6);
            Assert.AreEqual(BigRational.Pow(c, n) * det, ExactLinearAlgebra.Determinant(a * c), ctx);

            // det != 0 exactly when the inverse exists, and then A * A^-1 = I.
            var inverse = ExactLinearAlgebra.Inverse(a);
            if (det == BigRational.Zero)
            {
                Assert.IsInstanceOfType<Outcome<DenseMatrix<BigRational>>.Failed>(inverse, ctx);
            }
            else
            {
                Assert.AreEqual(DenseMatrix.Identity<BigRational>(n), a * inverse.Unwrap(), ctx);
                Assert.AreEqual(BigRational.One / det, ExactLinearAlgebra.Determinant(inverse.Unwrap()), ctx);
            }
        }
        Assert.AreEqual(BigRational.One, ExactLinearAlgebra.Determinant(DenseMatrix.Zero<BigRational>(0, 0)));
        Assert.Throws<ArgumentException>(() => ExactLinearAlgebra.Determinant(DenseMatrix.Zero<BigRational>(2, 3)));
    }

    [TestMethod]
    public void BareissIsFractionFreeOverIntegers()
    {
        // Entries stay integers: BigInteger works as the entry type (exact divisions only).
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 6);
            var rational = DenseMatrix.Create(n, n, (_, _) => new BigRational(gen.WholeNumber(10)));
            var integer = DenseMatrix.Create(n, n, (r, c) => rational[r, c].Numerator);
            Assert.AreEqual(ExactLinearAlgebra.Determinant(rational).Numerator, ExactLinearAlgebra.Determinant(integer), $"seed={Seed + 2} case={i}");
        }

        // Vandermonde determinant: product of differences (catalog linalg.det.vandermonde).
        BigRational[] nodes = [R("2"), R("3"), R("5"), R("7")];
        var v = DenseMatrix.Create(4, 4, (r, c) => BigRational.Pow(nodes[r], c));
        var expected = BigRational.One;
        for (var a = 0; a < 4; a++)
        {
            for (var b = a + 1; b < 4; b++) expected *= nodes[b] - nodes[a];
        }
        Assert.AreEqual(expected, ExactLinearAlgebra.Determinant(v));
    }

    [TestMethod]
    public void CharacteristicPolynomialByBerkowitz()
    {
        // [[a, b], [c, d]] has x^2 - (a + d) x + (ad - bc).
        var m = DenseMatrix.FromRows(new BigRational[,] { { 1, 2 }, { 3, 4 } });
        Assert.AreEqual(new Polynomial<BigRational>([new BigRational(-2), new BigRational(-5), BigRational.One]), ExactLinearAlgebra.CharacteristicPolynomial(m));
        Assert.AreEqual(Polynomial<BigRational>.One, ExactLinearAlgebra.CharacteristicPolynomial(DenseMatrix.Zero<BigRational>(0, 0)));

        var gen = new Gen(Seed + 3);
        for (var i = 0; i < 300; i++)
        {
            var n = gen.Random.Next(1, 6);
            var a = RandomMatrix(gen, n, n);
            var p = ExactLinearAlgebra.CharacteristicPolynomial(a);
            var ctx = $"seed={Seed + 3} case={i}\n{a}";

            Assert.AreEqual(n, p.Degree, ctx);
            Assert.AreEqual(BigRational.One, p.LeadingCoefficient, ctx);
            Assert.AreEqual(-a.Trace(), p[n - 1], ctx);
            Assert.AreEqual((n % 2 == 0 ? BigRational.One : -BigRational.One) * ExactLinearAlgebra.Determinant(a), p[0], ctx);

            // p(x0) = det(x0 I - A) at a random rational point, exactly.
            var x0 = gen.Rational(6);
            Assert.AreEqual(ExactLinearAlgebra.Determinant(DenseMatrix.Identity<BigRational>(n) * x0 - a), p.Evaluate(x0), ctx);

            // Cayley-Hamilton: p(A) = 0 by Horner with matrices.
            var evaluated = DenseMatrix.Zero<BigRational>(n, n);
            for (var k = n; k >= 0; k--) evaluated = evaluated * a + DenseMatrix.Identity<BigRational>(n) * p[k];
            Assert.AreEqual(DenseMatrix.Zero<BigRational>(n, n), evaluated, ctx);
        }

        // Division-free over the integers.
        var integer = DenseMatrix.FromRows(new BigInteger[,] { { 2, 1 }, { 1, 2 } });
        var q = ExactLinearAlgebra.CharacteristicPolynomial(integer);
        Assert.IsTrue(q.Coefficients.SequenceEqual([(BigInteger)3, -4, 1]));
    }

    [TestMethod]
    public void ComplexGaussianMatricesWork()
    {
        // Entries in Q(i): the same code, a different field.
        Complex<BigRational> Z(string re, string im) => new(R(re), R(im));
        var a = DenseMatrix.FromRows(new[,] { { Z("1", "1"), Z("0", "2") }, { Z("3", "0"), Z("1", "-1") } });
        var inverse = ExactLinearAlgebra.Inverse(a).Unwrap();
        Assert.AreEqual(DenseMatrix.Identity<Complex<BigRational>>(2), a * inverse);
        var det = ExactLinearAlgebra.Determinant(a);
        Assert.AreEqual(Z("1", "1") * Z("1", "-1") - Z("0", "2") * Z("3", "0"), det);
    }

    [TestMethod]
    public void CustomZeroTestIsUsedForPivoting()
    {
        // With a tolerance-based zero test, a tiny pivot candidate is skipped and rows are exchanged.
        static bool Tiny(double x) => Math.Abs(x) < 1e-9;
        var a = DenseMatrix.FromRows(new[,] { { 1e-12, 1.0 }, { 1.0, 1.0 } });
        var reduction = ExactLinearAlgebra.RowReduce(a, Tiny);
        Assert.AreEqual(RowOperationKind.Swap, reduction.Operations[0].Kind);
        Assert.AreEqual(2, reduction.Rank);
        Assert.AreEqual(1, ExactLinearAlgebra.Rank(DenseMatrix.FromRows(new[,] { { 1e-12, 1e-13 }, { 1.0, 2.0 } }), Tiny));
        Assert.AreEqual(0.0, ExactLinearAlgebra.Determinant(DenseMatrix.FromRows(new[,] { { 1e-12, 1.0 }, { 1e-12, 1.0 } }), Tiny), 1e-9);
    }
}

internal static class OutcomeTestExtensions
{
    public static T Unwrap<T>(this Outcome<T> outcome) =>
        outcome.TryGetValue(out var value) ? value : throw new AssertFailedException($"Expected a value but got {outcome}.");
}
