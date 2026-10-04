using System.Numerics;
using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.LinearAlgebra.Tests;

[TestClass]
public class MatrixTests
{
    private const int Seed = 20261022;

    private static DenseMatrix<BigRational> Q(Gen gen, int rows, int columns) => DenseMatrix.Create(rows, columns, (_, _) => gen.Rational(8));

    [TestMethod]
    public void MatrixAlgebraLawsHoldOverTheRationals()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 500; i++)
        {
            int m = gen.Random.Next(1, 5), n = gen.Random.Next(1, 5), p = gen.Random.Next(1, 5), r = gen.Random.Next(1, 5);
            var a = Q(gen, m, n);
            var b = Q(gen, n, p);
            var c = Q(gen, p, r);
            var b2 = Q(gen, n, p);
            var s = gen.Rational(6);
            var ctx = $"seed={Seed} case={i} {m}x{n}x{p}x{r}";

            // linalg.mat.assoc, dist-left, dist-right, scalar, identity.
            Assert.AreEqual((a * b) * c, a * (b * c), ctx);
            Assert.AreEqual(a * (b + b2), a * b + a * b2, ctx);
            Assert.AreEqual((a + a) * b, a * b + a * b, ctx);
            Assert.AreEqual(s * (a * b), (s * a) * b, ctx);
            Assert.AreEqual(s * (a * b), a * (s * b), ctx);
            Assert.AreEqual(a, a * DenseMatrix.Identity<BigRational>(n), ctx);
            Assert.AreEqual(a, DenseMatrix.Identity<BigRational>(m) * a, ctx);

            // Transpose laws.
            Assert.AreEqual(a, a.Transpose().Transpose(), ctx);
            Assert.AreEqual((a + a).Transpose(), a.Transpose() + a.Transpose(), ctx);
            Assert.AreEqual((a * b).Transpose(), b.Transpose() * a.Transpose(), ctx);

            // Trace: linear and cyclic.
            var square1 = Q(gen, n, n);
            var square2 = Q(gen, n, n);
            Assert.AreEqual(square1.Trace() + square2.Trace(), (square1 + square2).Trace(), ctx);
            Assert.AreEqual((square1 * square2).Trace(), (square2 * square1).Trace(), ctx);
            Assert.AreEqual(square1.Transpose().Trace(), square1.Trace(), ctx);

            // Kronecker mixed-product property (A ⊗ B)(C ⊗ D) = AC ⊗ BD.
            var d = Q(gen, 2, 2);
            var e = Q(gen, 2, 2);
            var f = Q(gen, 2, 2);
            var g = Q(gen, 2, 2);
            Assert.AreEqual(d.Kronecker(e) * f.Kronecker(g), (d * f).Kronecker(e * g), ctx);
            Assert.AreEqual(d.Hadamard(e), e.Hadamard(d), ctx);

            // Matrix-vector product agrees with the one-column matrix product.
            var v = DenseVector.Create(n, _ => gen.Rational(8));
            Assert.AreEqual((a * DenseMatrix.FromColumns([v])).Column(0), a * v, ctx);
        }
    }

    [TestMethod]
    public void NonCommutativityAndZeroDivisors()
    {
        var a = DenseMatrix.FromRows(new[,] { { 0, 1 }, { 0, 0 } });
        var b = DenseMatrix.FromRows(new[,] { { 0, 0 }, { 1, 0 } });
        Assert.AreNotEqual(a * b, b * a);
        Assert.AreEqual(DenseMatrix.Zero<int>(2, 2), a * a);
        Assert.AreNotEqual(DenseMatrix.Zero<int>(2, 2), a);
    }

    [TestMethod]
    public void FloatingPointProductMatchesTheExactProduct()
    {
        // The double path goes through TensorPrimitives; compare with an exact BigRational product of the same entries.
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 100; i++)
        {
            int m = gen.Random.Next(1, 12), n = gen.Random.Next(1, 40), p = gen.Random.Next(1, 12);
            var a = DenseMatrix.Create(m, n, (_, _) => gen.Uniform(-1, 1));
            var b = DenseMatrix.Create(n, p, (_, _) => gen.Uniform(-1, 1));
            var exact = DenseMatrix.Create(m, n, (r, c) => BigRational.FromDouble(a[r, c])) * DenseMatrix.Create(n, p, (r, c) => BigRational.FromDouble(b[r, c]));
            var product = a * b;
            for (var r = 0; r < m; r++)
            {
                for (var c = 0; c < p; c++) Assert.AreEqual((double)exact[r, c], product[r, c], 4 * n * Math.BitIncrement(1.0) - 4 * n, $"seed={Seed + 1} case={i}");
            }

            var single = DenseMatrix.Create(m, n, (r, c) => (float)a[r, c]) * DenseMatrix.Create(n, p, (r, c) => (float)b[r, c]);
            Assert.AreEqual(m, single.Rows);
            Assert.AreEqual((float)product[0, 0], single[0, 0], 1e-4f * n);

            var v = DenseVector.Create(n, _ => gen.Uniform(-1, 1));
            var w = DenseVector.Create(n, _ => gen.Uniform(-1, 1));
            var exactDot = Enumerable.Range(0, n).Aggregate(BigRational.Zero, (acc, k) => acc + BigRational.FromDouble(v[k]) * BigRational.FromDouble(w[k]));
            Assert.AreEqual((double)exactDot, v.Dot(w), 4e-16 * n);
        }
    }

    [TestMethod]
    public void NormsHaveTheirDefinitions()
    {
        var a = DenseMatrix.FromRows(new[,] { { 1.0, -2.0 }, { 3.0, 4.0 } });
        Assert.AreEqual(6.0, a.Norm1());
        Assert.AreEqual(7.0, a.NormInfinity());
        Assert.AreEqual(Math.Sqrt(30), a.NormFrobenius(), 1e-15);

        var v = DenseVector.Create(3.0, -4.0, 12.0);
        Assert.AreEqual(19.0, v.Norm1());
        Assert.AreEqual(13.0, v.Norm2(), 1e-14);
        Assert.AreEqual(12.0, v.NormInfinity());

        // Overflow-safe 2-norm and NaN propagation.
        Assert.AreEqual(Math.Sqrt(2) * 1e200, DenseVector.Create(1e200, 1e200).Norm2(), 1e186);
        Assert.AreEqual(0.0, DenseVector.Zero<double>(4).Norm2());
        Assert.IsTrue(double.IsNaN(DenseVector.Create(1.0, double.NaN).NormInfinity()));

        // Submultiplicativity and the triangle inequality on random matrices (linalg.norm.*).
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 300; i++)
        {
            var x = DenseMatrix.Create(4, 4, (_, _) => gen.Uniform(-3, 3));
            var y = DenseMatrix.Create(4, 4, (_, _) => gen.Uniform(-3, 3));
            Assert.IsTrue((x * y).Norm1() <= x.Norm1() * y.Norm1() * (1 + 1e-12));
            Assert.IsTrue((x * y).NormInfinity() <= x.NormInfinity() * y.NormInfinity() * (1 + 1e-12));
            Assert.IsTrue((x * y).NormFrobenius() <= x.NormFrobenius() * y.NormFrobenius() * (1 + 1e-12));
            Assert.IsTrue((x + y).Norm1() <= (x.Norm1() + y.Norm1()) * (1 + 1e-12));
        }
    }

    [TestMethod]
    public void ConstructionBlocksPowersAndEquality()
    {
        var a = DenseMatrix.FromRows(new[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        Assert.AreEqual(new Shape(2, 3), a.Shape);
        Assert.AreEqual(6, a[1, 2]);
        Assert.AreEqual(DenseVector.Create(4, 5, 6), a.Row(1));
        Assert.AreEqual(DenseVector.Create(3, 6), a.Column(2));
        Assert.AreEqual(DenseMatrix.FromRows(new[,] { { 2, 3 }, { 5, 6 } }), a.Block(0, 1, 2, 2));
        Assert.AreEqual(DenseMatrix.FromRowMajor<int>(2, 3, [1, 2, 3, 4, 5, 6]), a);
        Assert.AreEqual(a.GetHashCode(), DenseMatrix.FromRows(new[,] { { 1, 2, 3 }, { 4, 5, 6 } }).GetHashCode());
        Assert.AreEqual(6, DenseMatrix.Diagonal(1, 2, 3).Trace());
        Assert.AreEqual("[1, 2, 3]\r\n[4, 5, 6]".Replace("\r\n", Environment.NewLine), a.ToString());

        // Powers by squaring: Fibonacci via the matrix [[1, 1], [1, 0]]^n.
        var fib = DenseMatrix.FromRows(new BigInteger[,] { { 1, 1 }, { 1, 0 } });
        Assert.AreEqual(new BigInteger(12586269025), fib.Pow(50)[0, 1]);
        Assert.AreEqual(DenseMatrix.Identity<BigInteger>(2), fib.Pow(0));
    }

    [TestMethod]
    public void MisuseThrowsArgumentExceptions()
    {
        var a = DenseMatrix.Zero<double>(2, 3);
        Assert.Throws<ArgumentException>(() => a * a);
        Assert.Throws<ArgumentException>(() => a + DenseMatrix.Zero<double>(3, 2));
        Assert.Throws<ArgumentException>(() => a * DenseVector.Zero<double>(2));
        Assert.Throws<ArgumentException>(() => DenseVector.Zero<double>(2) + DenseVector.Zero<double>(3));
        Assert.Throws<ArgumentException>(() => DenseVector.Zero<double>(2).Dot(DenseVector.Zero<double>(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = a[2, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = a[0, 3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => a.Block(1, 1, 2, 2));
        Assert.Throws<InvalidOperationException>(() => a.Trace());
        Assert.Throws<InvalidOperationException>(() => a.Pow(2));
        Assert.Throws<ArgumentException>(() => DenseMatrix.FromRowMajor<double>(2, 2, [1.0, 2.0, 3.0]));
        Assert.Throws<ArgumentNullException>(() => DenseMatrix.Create<double>(2, 2, null!));
    }

    [TestMethod]
    public void VectorArithmetic()
    {
        var u = DenseVector.Create(1, 2, 3);
        var v = DenseVector.Create(4, 5, 6);
        Assert.AreEqual(DenseVector.Create(5, 7, 9), u + v);
        Assert.AreEqual(DenseVector.Create(-3, -3, -3), u - v);
        Assert.AreEqual(DenseVector.Create(2, 4, 6), u * 2);
        Assert.AreEqual(DenseVector.Create(2, 4, 6), 2 * u);
        Assert.AreEqual(DenseVector.Create(-1, -2, -3), -u);
        Assert.AreEqual(32, u.Dot(v));
        Assert.AreEqual(3, u.Length);
        Assert.AreEqual("[1, 2, 3]", u.ToString());
        Assert.AreEqual(u.GetHashCode(), DenseVector.Create(1, 2, 3).GetHashCode());
        Assert.IsTrue(u == DenseVector.Create(1, 2, 3));
        Assert.IsTrue(u != v);
        Assert.AreEqual(14, u.Dot(u));
    }
}
