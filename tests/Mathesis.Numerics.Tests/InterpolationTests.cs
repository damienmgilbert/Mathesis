using Mathesis.Numerics.Interpolation;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

[TestClass]
public class InterpolationTests
{
    private const int Seed = 20261005;

    private static double[] RandomNodes(Gen gen, int count, double low, double high)
    {
        // Distinct, well separated, ascending.
        var step = (high - low) / count;
        return Enumerable.Range(0, count).Select(i => low + step * (i + gen.Uniform(0.1, 0.9))).ToArray();
    }

    [TestMethod]
    public void PolynomialInterpolantsReproduceRandomPolynomials()
    {
        // Exactly one polynomial of degree <= n passes through n + 1 points (num.interp.unique).
        var gen = new Gen(Seed);
        for (var i = 0; i < 500; i++)
        {
            var degree = gen.Random.Next(0, 9);
            var coefficients = Enumerable.Range(0, degree + 1).Select(_ => gen.Uniform(-2, 2)).ToArray();
            double P(double x) => coefficients.Reverse().Aggregate(0.0, (acc, c) => acc * x + c);
            double dP(double x) => coefficients.Skip(1).Select((c, k) => (k + 1) * c).Reverse().Aggregate(0.0, (acc, c) => acc * x + c);

            var x = RandomNodes(gen, degree + 1, -1, 1);
            var y = x.Select(P).ToArray();
            var newton = Interpolate.Newton(x, y);
            var barycentric = Interpolate.Barycentric(x, y);

            for (var j = 0; j < 10; j++)
            {
                var t = gen.Uniform(-1, 1);
                var ctx = $"seed={Seed} case={i} degree={degree} t={t:R}";
                Assert.AreEqual(P(t), newton.Evaluate(t), 1e-9, ctx);
                Assert.AreEqual(P(t), barycentric.Evaluate(t), 1e-9, ctx);
                Assert.AreEqual(dP(t), newton.Derivative(t), 1e-7 * (1 + Math.Abs(dP(t))), ctx);
            }
            for (var j = 0; j < x.Length; j++)
            {
                Assert.AreEqual(y[j], newton.Evaluate(x[j]), 1e-12);
                Assert.AreEqual(y[j], barycentric.Evaluate(x[j]));
            }
        }
    }

    [TestMethod]
    public void NewtonCoefficientsAreDividedDifferences()
    {
        // f(x) = x^2 at 0, 1, 3: f[0] = 0, f[0,1] = 1, f[0,1,3] = 1.
        var p = Interpolate.Newton([0.0, 1.0, 3.0], [0.0, 1.0, 9.0]);
        Assert.AreEqual(3, p.Coefficients.Length);
        Assert.AreEqual(0.0, p.Coefficients[0]);
        Assert.AreEqual(1.0, p.Coefficients[1]);
        Assert.AreEqual(1.0, p.Coefficients[2]);
        Assert.AreEqual(4.0, p.Evaluate(2.0), 1e-14);
        Assert.AreEqual(4.0, p.Derivative(2.0), 1e-14);
        Assert.AreEqual(7.0, Interpolate.Newton([2.0], [7.0]).Evaluate(100));
    }

    [TestMethod]
    public void ChebyshevNodesBeatEquispacedNodesOnRunge()
    {
        double Runge(double x) => 1 / (1 + 25 * x * x);

        // num.interp.runge: equispaced interpolation of degree 20 diverges near the ends...
        var equispaced = Enumerable.Range(0, 21).Select(i => -1 + 2.0 * i / 20).ToArray();
        var bad = Interpolate.Barycentric(equispaced, equispaced.Select(Runge).ToArray());
        var badError = Enumerable.Range(0, 1001).Max(i => Math.Abs(bad.Evaluate(-1 + 2.0 * i / 1000) - Runge(-1 + 2.0 * i / 1000)));
        Assert.IsTrue(badError > 1.0, $"equispaced error {badError}");

        // ...while Chebyshev points converge geometrically (num.interp.chebyshev-nodes).
        var previous = double.MaxValue;
        foreach (var degree in new[] { 10, 20, 40, 80, 160 })
        {
            var good = Interpolate.Chebyshev(Runge, -1.0, 1.0, degree);
            var error = Enumerable.Range(0, 1001).Max(i => Math.Abs(good.Evaluate(-1 + 2.0 * i / 1000) - Runge(-1 + 2.0 * i / 1000)));
            Assert.IsTrue(error < previous, $"degree {degree}: {error}");
            previous = error;
        }
        Assert.IsTrue(previous < 1e-12, $"degree 160 error {previous}");

        // Endpoints are nodes.
        var p = Interpolate.Chebyshev(Math.Exp, 0.0, 2.0, 12);
        Assert.AreEqual(0.0, p.Nodes[0]);
        Assert.AreEqual(2.0, p.Nodes[12]);
        Assert.AreEqual(Math.Exp(1.3), p.Evaluate(1.3), 1e-9);
    }

    [TestMethod]
    public void SplinesPassThroughKnotsAndAreTwiceDifferentiable()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 200; i++)
        {
            var n = gen.Random.Next(3, 12);
            var x = RandomNodes(gen, n, 0, 10);
            var y = x.Select(_ => gen.Uniform(-5, 5)).ToArray();
            var natural = Interpolate.NaturalSpline(x, y);
            var clamped = Interpolate.ClampedSpline(x, y, gen.Uniform(-2, 2), gen.Uniform(-2, 2));
            foreach (var spline in new[] { natural, clamped })
            {
                for (var j = 0; j < n; j++) Assert.AreEqual(y[j], spline.Evaluate(x[j]), 1e-12, $"case {i} knot {j}");

                // C0, C1, C2 at interior knots: compare the two sides.
                for (var j = 1; j < n - 1; j++)
                {
                    const double d = 1e-9;
                    foreach (var (name, f) in new (string, Func<double, double>)[] { ("value", spline.Evaluate), ("slope", spline.Derivative), ("curvature", spline.SecondDerivative) })
                    {
                        var left = f(x[j] - d);
                        var right = f(x[j] + d);
                        Assert.AreEqual(left, right, 1e-5 * (1 + Math.Abs(left)), $"{name} case {i} knot {j}");
                    }
                }
            }
            Assert.AreEqual(0.0, natural.SecondDerivative(x[0]), 1e-10);
            Assert.AreEqual(0.0, natural.SecondDerivative(x[n - 1]), 1e-9);
        }
    }

    [TestMethod]
    public void ClampedSplineErrorBound()
    {
        // num.interp.spline-error: max|f - S| <= (5/384) h^4 max|f''''| for f in C^4. For sin on [0, pi], max|f''''| = 1.
        var previous = double.NaN;
        foreach (var n in new[] { 4, 8, 16, 32 })
        {
            var h = Math.PI / n;
            var x = Enumerable.Range(0, n + 1).Select(i => h * i).ToArray();
            var spline = Interpolate.ClampedSpline(x, x.Select(Math.Sin).ToArray(), 1.0, -1.0);
            var error = Enumerable.Range(0, 2_001).Max(i => Math.Abs(spline.Evaluate(Math.PI * i / 2_000) - Math.Sin(Math.PI * i / 2_000)));
            var bound = 5.0 / 384 * Math.Pow(h, 4);
            Assert.IsTrue(error <= bound, $"n={n}: error {error:E3} > bound {bound:E3}");
            if (!double.IsNaN(previous)) Assert.IsTrue(previous / error is > 12 and < 20, $"order at n={n}: ratio {previous / error}");
            previous = error;
        }
    }

    [TestMethod]
    public void SplineIntegralAndDerivative()
    {
        var x = Enumerable.Range(0, 41).Select(i => Math.PI * i / 40).ToArray();
        var spline = Interpolate.ClampedSpline(x, x.Select(Math.Sin).ToArray(), 1.0, -1.0);
        Assert.AreEqual(2.0, spline.Integral(0, Math.PI), 1e-6);
        Assert.AreEqual(Math.Cos(1.0) - Math.Cos(2.0), spline.Integral(1.0, 2.0), 1e-6);
        Assert.AreEqual(-spline.Integral(1.0, 2.0), spline.Integral(2.0, 1.0), 1e-15);
        Assert.AreEqual(0.0, spline.Integral(1.0, 1.0), 1e-15);
        Assert.AreEqual(Math.Cos(0.9), spline.Derivative(0.9), 1e-6);

        // Integral across several pieces equals the sum of the parts.
        Assert.AreEqual(spline.Integral(0.3, 1.1) + spline.Integral(1.1, 2.7), spline.Integral(0.3, 2.7), 1e-13);
        Assert.AreEqual(41, spline.Knots.Length);
    }

    [TestMethod]
    public void InvalidInputIsRejected()
    {
        Assert.Throws<ArgumentException>(() => Interpolate.Newton([0.0, 1.0], [1.0]));
        Assert.Throws<ArgumentException>(() => Interpolate.Newton([0.0, 0.0], [1.0, 2.0]));
        Assert.Throws<ArgumentException>(() => Interpolate.Barycentric(Array.Empty<double>(), Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => Interpolate.NaturalSpline([1.0, 0.0], [1.0, 2.0]));
        Assert.Throws<ArgumentException>(() => Interpolate.NaturalSpline([1.0], [1.0]));
        Assert.Throws<ArgumentException>(() => Interpolate.Newton([0.0, double.NaN], [1.0, 2.0]));
        Assert.Throws<ArgumentNullException>(() => Interpolate.Newton<double>(null!, [1.0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolate.Chebyshev(Math.Sin, 0.0, 1.0, 0));
    }
}
