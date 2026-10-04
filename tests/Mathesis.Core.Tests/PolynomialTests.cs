using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Testing;

namespace Mathesis.Core.Tests;

[TestClass]
public class PolynomialTests
{
    private const int Seed = 20261010;

    private static BigRational R(string text) => BigRational.Parse(text);

    private static Polynomial<BigRational> P(params string[] ascending) => new(ascending.Select(R));

    private static Polynomial<BigRational> Linear(BigRational root) => new([-root, BigRational.One]);

    [TestMethod]
    public void ArithmeticIdentitiesOnRandomPolynomials()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 1_000; i++)
        {
            var a = gen.RationalPolynomial();
            var b = gen.RationalPolynomial();
            var c = gen.RationalPolynomial(3);
            var ctx = $"seed={Seed} case={i} a={a} b={b}";

            Assert.AreEqual(a + b, b + a, ctx);
            Assert.AreEqual(a * b, b * a, ctx);
            Assert.AreEqual(a * (b + c), a * b + a * c, ctx);
            Assert.AreEqual(a, a + Polynomial<BigRational>.Zero, ctx);
            Assert.AreEqual(Polynomial<BigRational>.Zero, a - a, ctx);
            Assert.AreEqual(a.Degree + b.Degree, (a * b).Degree, ctx);

            // (p*q)/q = p, and the division algorithm.
            Assert.AreEqual(a, a * b / b, ctx);
            var (q, r) = a.DivRem(b);
            Assert.AreEqual(a, b * q + r, ctx);
            Assert.IsTrue(r.Degree < b.Degree, ctx);

            // Evaluation is a ring homomorphism; derivative obeys the product rule.
            var x = gen.Rational(10);
            Assert.AreEqual((a * b).Evaluate(x), a.Evaluate(x) * b.Evaluate(x), ctx);
            Assert.AreEqual((a + b).Evaluate(x), a.Evaluate(x) + b.Evaluate(x), ctx);
            Assert.AreEqual((a * b).Derivative(), a.Derivative() * b + a * b.Derivative(), ctx);
            Assert.AreEqual(a.Compose(b).Evaluate(x), a.Evaluate(b.Evaluate(x)), ctx);
            Assert.AreEqual(a.TaylorShift(x).Evaluate(BigRational.One), a.Evaluate(x + BigRational.One), ctx);
        }
    }

    [TestMethod]
    public void GcdAndBezoutIdentity()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 1_000; i++)
        {
            var common = gen.RationalPolynomial(3);
            var a = common * gen.RationalPolynomial(3);
            var b = common * gen.RationalPolynomial(3);
            var g = PolynomialAlgorithms.Gcd(a, b);
            var ctx = $"seed={Seed + 1} case={i} a={a} b={b} g={g}";

            Assert.AreEqual(BigRational.One, g.LeadingCoefficient, ctx);
            Assert.IsTrue((a % g).IsZero && (b % g).IsZero, ctx);
            Assert.IsTrue(g.Degree >= common.Degree, ctx);

            // Euclid's extended identity s*a + t*b = g.
            var (eg, s, t) = PolynomialAlgorithms.ExtendedGcd(a, b);
            Assert.AreEqual(g, eg, ctx);
            Assert.AreEqual(g, s * a + t * b, ctx);
        }
        var p = P("1", "2", "1");
        Assert.AreEqual(p.Monic(), PolynomialAlgorithms.Gcd(p, Polynomial<BigRational>.Zero));
        Assert.IsTrue(PolynomialAlgorithms.Gcd(Polynomial<BigRational>.Zero, Polynomial<BigRational>.Zero).IsZero);
    }

    [TestMethod]
    public void ExpandOfSquareFreeIsTheOriginal()
    {
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 1_000; i++)
        {
            // Half the cases are built with repeated factors so the multiplicities matter.
            var p = gen.RationalPolynomial(5);
            if (i % 2 == 0)
            {
                p = gen.NonZeroRational(8) * Polynomial<BigRational>.One;
                for (var k = 0; k < gen.Random.Next(1, 4); k++) p *= gen.RationalPolynomial(2).Pow(gen.Random.Next(1, 4));
            }
            var decomposition = PolynomialAlgorithms.SquareFree(p);
            var ctx = $"seed={Seed + 2} case={i} p={p}";

            Assert.AreEqual(p, decomposition.Expand(), ctx);
            Assert.AreEqual(p.LeadingCoefficient, decomposition.Content, ctx);
            var factors = decomposition.Factors;
            foreach (var (factor, multiplicity) in factors)
            {
                Assert.AreEqual(BigRational.One, factor.LeadingCoefficient, ctx);
                Assert.AreEqual(1, PolynomialAlgorithms.Gcd(factor, factor.Derivative()).Degree + 1, $"{ctx}: factor {factor} is not square-free");
                Assert.IsTrue(multiplicity >= 1, ctx);
            }
            for (var a = 0; a < factors.Length; a++)
            {
                for (var b = a + 1; b < factors.Length; b++)
                {
                    Assert.AreEqual(0, PolynomialAlgorithms.Gcd(factors[a].Factor, factors[b].Factor).Degree, $"{ctx}: factors not coprime");
                }
            }
        }

        // (x - 1)^3 (x + 2)^2 (x - 5) splits by multiplicity.
        var q = Linear(1).Pow(3) * Linear(-2).Pow(2) * Linear(5);
        var d = PolynomialAlgorithms.SquareFree(q);
        Assert.IsTrue(d.Factors.Select(f => f.Multiplicity).SequenceEqual([1, 2, 3]));
        Assert.AreEqual(Linear(5), d.Factors[0].Factor);
        Assert.AreEqual(Linear(-2), d.Factors[1].Factor);
        Assert.AreEqual(Linear(1), d.Factors[2].Factor);
        Assert.Throws<ArgumentException>(() => PolynomialAlgorithms.SquareFree(Polynomial<BigRational>.Zero));
    }

    [TestMethod]
    public void AberthRecoversWilkinsonRootsWithinOneEMinusEight()
    {
        // The Phase 3 exit check: the roots of (x - 1)(x - 2)...(x - 10).
        var p = Enumerable.Range(1, 10).Aggregate(Polynomial<BigRational>.One, (acc, k) => acc * Linear(k));
        var result = PolynomialAlgorithms.AberthRoots(p.Map(r => (double)r));
        Assert.IsTrue(result.Converged, result.ToString());
        Assert.AreEqual(10, result.Roots.Length);
        for (var k = 0; k < 10; k++)
        {
            Assert.AreEqual(k + 1, result.Roots[k].Real, 1e-8, $"root {k + 1}");
            Assert.AreEqual(0.0, result.Roots[k].Imaginary, 1e-8, $"root {k + 1}");
        }
    }

    [TestMethod]
    public void AberthOnRandomRootsAndComplexPairs()
    {
        var gen = new Gen(Seed + 3);
        for (var i = 0; i < 500; i++)
        {
            // Real coefficients built from well separated real roots and conjugate pairs.
            var expected = new List<Complex<double>>();
            var p = new Polynomial<double>([1.0]);
            var realRoots = gen.Random.Next(0, 4);
            for (var k = 0; k < realRoots; k++)
            {
                var r = -6 + 3.0 * k + gen.Uniform(0, 1.5);
                expected.Add(new Complex<double>(r));
                p *= new Polynomial<double>([-r, 1.0]);
            }
            var pairs = gen.Random.Next(0, 3);
            for (var k = 0; k < pairs; k++)
            {
                var (re, im) = (gen.Uniform(-4, 4), gen.Uniform(0.7, 3));
                expected.Add(new Complex<double>(re, im));
                expected.Add(new Complex<double>(re, -im));
                p *= new Polynomial<double>([re * re + im * im, -2 * re, 1.0]);
            }
            if (expected.Count == 0) continue;
            p *= new Polynomial<double>([gen.Uniform(0.5, 4)]);

            var result = PolynomialAlgorithms.AberthRoots(p);
            var ctx = $"seed={Seed + 3} case={i} p={p}";
            Assert.IsTrue(result.Converged, ctx);
            Assert.AreEqual(expected.Count, result.Roots.Length, ctx);

            // Each expected root is matched by some computed root, and each computed root is used once.
            var remaining = result.Roots.ToList();
            foreach (var e in expected)
            {
                var best = remaining.MinBy(z => (z - e).Magnitude)!;
                Assert.IsTrue((best - e).Magnitude < 1e-6, $"{ctx}: no root near {e}");
                remaining.Remove(best);
            }
        }
    }

    [TestMethod]
    public void AberthEdgeCases()
    {
        // Zero roots are exact; a double root is found to half precision; degree 1 is exact.
        var withZeros = new Polynomial<double>([0.0, 0.0, -1.0, 0.0, 1.0]); // x^2 (x^2 - 1)
        var roots = PolynomialAlgorithms.AberthRoots(withZeros).Roots;
        Assert.AreEqual(4, roots.Length);
        CollectionAssert.AreEqual(new[] { -1.0, 0.0, 0.0, 1.0 }, roots.Select(r => Math.Round(r.Real, 9)).ToArray());

        var doubleRoot = new Polynomial<double>([1.0, -2.0, 1.0]);
        var result = PolynomialAlgorithms.AberthRoots(doubleRoot);
        Assert.IsTrue(result.Roots.All(r => (r - Complex<double>.One).Magnitude < 1e-6));

        Assert.AreEqual(2.5, PolynomialAlgorithms.AberthRoots(new Polynomial<double>([-5.0, 2.0])).Roots[0].Real);
        Assert.AreEqual(0, PolynomialAlgorithms.AberthRoots(new Polynomial<double>([3.0])).Roots.Length);
        Assert.Throws<ArgumentException>(() => PolynomialAlgorithms.AberthRoots(Polynomial<double>.Zero));

        // x^2 + 1 has the roots +-i.
        var i = PolynomialAlgorithms.AberthRoots(new Polynomial<double>([1.0, 0.0, 1.0])).Roots;
        Assert.AreEqual(0.0, i[0].Real, 1e-12);
        Assert.AreEqual(1.0, Math.Abs(i[0].Imaginary), 1e-12);
    }

    [TestMethod]
    public void RationalRootsAreFoundWithMultiplicity()
    {
        // (x - 1/2)(x + 3)^2 (x^2 + 1) * 6
        var p = Linear(R("1/2")) * Linear(-3).Pow(2) * P("1", "0", "1") * new Polynomial<BigRational>([R("6")]);
        var result = PolynomialAlgorithms.RationalRoots(p);
        Assert.IsTrue(result.Complete);
        Assert.AreEqual(2, result.Roots.Length);
        Assert.AreEqual(new RationalRoot(R("-3"), 2), result.Roots[0]);
        Assert.AreEqual(new RationalRoot(R("1/2"), 1), result.Roots[1]);
        Assert.AreEqual(P("1", "0", "1") * new Polynomial<BigRational>([R("6")]), result.Cofactor);

        // Roots at zero, and polynomials without rational roots.
        var withZero = new Polynomial<BigRational>([0, 0, R("-1/4"), 0, 1]); // x^2 (x^2 - 1/4)
        var zeroResult = PolynomialAlgorithms.RationalRoots(withZero);
        CollectionAssert.AreEqual(new[] { R("-1/2"), R("0"), R("1/2") }, zeroResult.Roots.Select(r => r.Root).ToArray());
        Assert.AreEqual(2, zeroResult.Roots.First(r => r.Root == BigRational.Zero).Multiplicity);

        var none = PolynomialAlgorithms.RationalRoots(P("-2", "0", "1")); // x^2 - 2
        Assert.AreEqual(0, none.Roots.Length);
        Assert.AreEqual(P("-2", "0", "1"), none.Cofactor);
        Assert.Throws<ArgumentException>(() => PolynomialAlgorithms.RationalRoots(Polynomial<BigRational>.Zero));
    }

    [TestMethod]
    public void RationalRootsOnSeededPolynomialsMatchConstruction()
    {
        var gen = new Gen(Seed + 4);
        for (var i = 0; i < 300; i++)
        {
            var roots = Enumerable.Range(0, gen.Random.Next(1, 5)).Select(_ => BigRational.Create(gen.Random.Next(-12, 13), gen.Random.Next(1, 7))).ToArray();
            var p = new Polynomial<BigRational>([gen.NonZeroRational(6)]);
            foreach (var r in roots) p *= Linear(r);
            var irreducible = P("1", "0", "1"); // no rational roots
            if (gen.Random.Next(2) == 0) p *= irreducible;

            var result = PolynomialAlgorithms.RationalRoots(p);
            var expected = roots.GroupBy(r => r).OrderBy(g => g.Key).Select(g => new RationalRoot(g.Key, g.Count())).ToArray();
            var ctx = $"seed={Seed + 4} case={i} p={p}";
            Assert.IsTrue(result.Complete, ctx);
            CollectionAssert.AreEqual(expected, result.Roots.ToArray(), ctx);
            Assert.AreEqual(0, PolynomialAlgorithms.RationalRoots(result.Cofactor).Roots.Length, ctx);
        }
    }

    [TestMethod]
    public void RationalRootsReportAnIncompleteSearch()
    {
        // a0 = a product of two primes above the trial limit: divisors cannot be enumerated honestly.
        BigInteger p1 = 2_000_003, p2 = 2_000_029;
        var p = new Polynomial<BigRational>([new BigRational(p1 * p2), BigRational.One]); // x + p1*p2
        var result = PolynomialAlgorithms.RationalRoots(p);
        Assert.IsFalse(result.Complete);
        var cheap = PolynomialAlgorithms.RationalRoots(new Polynomial<BigRational>([new BigRational(p1), BigRational.One]));
        Assert.IsTrue(cheap.Complete);
        Assert.AreEqual(new RationalRoot(new BigRational(-p1), 1), cheap.Roots[0]);
    }

    [TestMethod]
    public void NormalizationAndZeroTest()
    {
        Assert.AreEqual(-1, P("0", "0").Degree);
        Assert.AreEqual(2, P("1", "2", "3", "0", "0").Degree);
        Assert.IsTrue(Polynomial<BigRational>.Zero.IsZero);
        Assert.AreEqual(P("0", "0", "3"), Polynomial<BigRational>.Monomial(R("3"), 2));
        Assert.Throws<InvalidOperationException>(() => _ = Polynomial<BigRational>.Zero.LeadingCoefficient);
        Assert.Throws<DivideByZeroException>(() => P("1", "1").DivRem(Polynomial<BigRational>.Zero));
        Assert.AreEqual(R("0"), P("1", "1")[7]);

        // A custom zero test (here: tolerance) is carried through arithmetic, so tiny residues vanish.
        static bool Tiny(double x) => Math.Abs(x) < 1e-9;
        var a = new Polynomial<double>([1.0, 1e-12, 2e-13], Tiny);
        Assert.AreEqual(0, a.Degree);
        var b = new Polynomial<double>([1.0, 2.0], Tiny);
        Assert.AreEqual(0, (b + new Polynomial<double>([0.0, -2.0 + 1e-13], Tiny)).Degree);
        Assert.AreEqual("0", Polynomial<double>.Zero.ToString());
    }

    [TestMethod]
    public void SparsePolynomialArithmetic()
    {
        // (x + y)^2 = x^2 + 2xy + y^2, in each order.
        foreach (var order in new[] { MonomialOrder.Lex, MonomialOrder.GrLex, MonomialOrder.GrevLex })
        {
            var x = SparsePolynomial<BigRational>.Variable(0, 2, order);
            var y = SparsePolynomial<BigRational>.Variable(1, 2, order);
            var square = (x + y).Pow(2);
            var expected = x * x + x * y * new BigRational(2) * SparsePolynomial<BigRational>.Constant(BigRational.One, 2, order) + y * y;
            Assert.AreEqual(expected, square, order.ToString());
            Assert.AreEqual(3, square.Terms.Length);
            Assert.AreEqual(2, square.TotalDegree);
            Assert.AreEqual(R("4"), square.Evaluate([R("1"), R("1")]));
            Assert.AreEqual(R("-2"), (x - y - SparsePolynomial<BigRational>.Constant(R("1"), 2, order)).Evaluate([R("0"), R("1")]));
            Assert.IsTrue((square - square).IsZero);
        }
    }

    [TestMethod]
    public void SparsePolynomialEvaluationAndDerivativesOnRandomInputs()
    {
        var gen = new Gen(Seed + 5);
        for (var i = 0; i < 300; i++)
        {
            SparsePolynomial<BigRational> Random() => new(3, MonomialOrder.GrevLex,
                Enumerable.Range(0, gen.Random.Next(1, 6)).Select(_ => (new Monomial([gen.Random.Next(0, 4), gen.Random.Next(0, 4), gen.Random.Next(0, 4)]), gen.Rational(8))));
            var a = Random();
            var b = Random();
            BigRational[] point = [gen.Rational(6), gen.Rational(6), gen.Rational(6)];
            var ctx = $"seed={Seed + 5} case={i} a={a} b={b}";

            Assert.AreEqual(a.Evaluate(point) * b.Evaluate(point), (a * b).Evaluate(point), ctx);
            Assert.AreEqual(a.Evaluate(point) + b.Evaluate(point), (a + b).Evaluate(point), ctx);
            Assert.AreEqual(a * b, b * a, ctx);
            for (var v = 0; v < 3; v++)
            {
                Assert.AreEqual((a * b).Derivative(v), a.Derivative(v) * b + a * b.Derivative(v), $"{ctx} product rule d/dx{v}");
            }
        }
    }

    [TestMethod]
    public void MonomialOrders()
    {
        Monomial M(params int[] e) => new(e);

        // Same degree: lex and grlex agree here, grevlex prefers the smaller last exponent.
        Assert.IsTrue(Monomial.Compare(M(1, 2, 1), M(0, 1, 3), MonomialOrder.Lex) > 0);
        Assert.IsTrue(Monomial.Compare(M(1, 2, 1), M(0, 1, 3), MonomialOrder.GrLex) > 0);
        Assert.IsTrue(Monomial.Compare(M(1, 2, 1), M(0, 1, 3), MonomialOrder.GrevLex) > 0);

        // The standard example where grlex and grevlex differ.
        Assert.IsTrue(Monomial.Compare(M(1, 0, 2), M(0, 2, 1), MonomialOrder.GrLex) > 0);
        Assert.IsTrue(Monomial.Compare(M(1, 0, 2), M(0, 2, 1), MonomialOrder.GrevLex) < 0);

        // Lex ignores degree, the graded orders do not.
        Assert.IsTrue(Monomial.Compare(M(1, 0, 0), M(0, 5, 0), MonomialOrder.Lex) > 0);
        Assert.IsTrue(Monomial.Compare(M(1, 0, 0), M(0, 5, 0), MonomialOrder.GrLex) < 0);
        Assert.IsTrue(Monomial.Compare(M(1, 0, 0), M(0, 5, 0), MonomialOrder.GrevLex) < 0);
        Assert.AreEqual(0, Monomial.Compare(M(2, 1), M(2, 1), MonomialOrder.GrevLex));

        // Leading terms follow the order.
        BigRational one = BigRational.One;
        var terms = new[] { (M(1, 0, 2), one), (M(0, 2, 1), one) };
        Assert.AreEqual(M(1, 0, 2), new SparsePolynomial<BigRational>(3, MonomialOrder.GrLex, terms).LeadingTerm.Monomial);
        Assert.AreEqual(M(0, 2, 1), new SparsePolynomial<BigRational>(3, MonomialOrder.GrevLex, terms).LeadingTerm.Monomial);
        Assert.AreEqual("x0^2*x1", M(2, 1).ToString());
        Assert.AreEqual("1", M(0, 0).ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => M(-1));
    }

    [TestMethod]
    public void UnivariateSparseConvertsToDense()
    {
        var x = SparsePolynomial<BigRational>.Variable(0, 1);
        var sparse = (x + SparsePolynomial<BigRational>.Constant(R("2"), 1)).Pow(3);
        var dense = Polynomial<BigRational>.FromSparse(sparse);
        Assert.AreEqual(Linear(-2).Pow(3), dense);
        Assert.Throws<ArgumentException>(() => Polynomial<BigRational>.FromSparse(SparsePolynomial<BigRational>.Variable(0, 2)));
    }

    [TestMethod]
    public void SparsePolynomialsEqualAcrossMonomialOrdersHashEqually()
    {
        var terms = new[]
        {
            (new Monomial([2, 0]), (BigRational)1),
            (new Monomial([0, 3]), (BigRational)1),
            (new Monomial([1, 1]), (BigRational)(-2)),
        };
        var lex = new SparsePolynomial<BigRational>(2, MonomialOrder.Lex, terms);
        var grlex = new SparsePolynomial<BigRational>(2, MonomialOrder.GrLex, terms);
        Assert.AreNotEqual(lex.LeadingTerm.Monomial, grlex.LeadingTerm.Monomial);
        Assert.IsTrue(lex.Equals(grlex));
        Assert.AreEqual(lex.GetHashCode(), grlex.GetHashCode());
    }
}
