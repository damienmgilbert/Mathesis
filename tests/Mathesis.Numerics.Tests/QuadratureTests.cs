using Mathesis.Numerics;
using Mathesis.Numerics.Integration;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

[TestClass]
public class QuadratureTests
{
    private const int Seed = 20261003;

    private static readonly StoppingCriteria Tight = new(AbsoluteTolerance: 1e-14, RelativeTolerance: 1e-13);

    [TestMethod]
    public void ToleranceTable()
    {
        // The Phase 2 exit table.
        var sine = Quadrature.GaussKronrod(Math.Sin, 0.0, Math.PI, Tight);
        Assert.IsTrue(sine.Converged);
        Assert.AreEqual(2.0, sine.Value, 1e-13);

        var gaussian = Quadrature.Integrate(x => Math.Exp(-x * x), 0.0, double.PositiveInfinity, Tight);
        Assert.IsTrue(gaussian.Converged, gaussian.ToString());
        Assert.AreEqual(Math.Sqrt(Math.PI) / 2, gaussian.Value, 1e-10);
    }

    [TestMethod]
    public void NodesAndWeightsIntegratePolynomialsExactly()
    {
        // One Gauss-Kronrod panel (no subdivision) on [-1, 1]. K15 is exact to degree 22, so a wrong constant shows up here.
        var onePanel = new StoppingCriteria(AbsoluteTolerance: 0, RelativeTolerance: 0, MaxIterations: 1);
        for (var degree = 0; degree <= 22; degree++)
        {
            var result = Quadrature.GaussKronrod(x => Math.Pow(x, degree), -1.0, 1.0, onePanel);
            var exact = degree % 2 == 1 ? 0.0 : 2.0 / (degree + 1);
            Assert.AreEqual(exact, result.Value, 4e-16 * (degree + 1), $"degree {degree}");
            Assert.AreEqual(15, result.Evaluations);
            Assert.AreEqual(1, result.Subintervals);
        }

        // Degree 24 is beyond the rule, so it must show a visible error: the test would pass vacuously otherwise.
        Assert.IsTrue(Math.Abs(Quadrature.GaussKronrod(x => Math.Pow(x, 24), -1.0, 1.0, onePanel).Value - 2.0 / 25) > 1e-14);

        // The error estimate of one panel is the Gauss-Kronrod difference (zero for low degrees) floored at round-off level.
        Assert.IsTrue(Quadrature.GaussKronrod(x => x * x * x, -1.0, 1.0, onePanel).ErrorEstimate < 1e-13);
    }

    [TestMethod]
    public void AllMethodsAgreeOnSmoothIntegrals()
    {
        (string Name, Func<double, double> F, double A, double B, double Exact)[] cases =
        [
            ("exp on [0,1]", Math.Exp, 0, 1, Math.E - 1),
            ("x^3 - 2x + 1 on [-1, 2]", x => x * x * x - 2 * x + 1, -1, 2, 3.75 - 3 + 3 + 0.0),
            ("1/(1+x^2) on [0,1]", x => 1 / (1 + x * x), 0, 1, Math.PI / 4),
            ("cos on [0, pi/2]", Math.Cos, 0, Math.PI / 2, 1),
            ("sqrt on [0,4]", Math.Sqrt, 0, 4, 16.0 / 3),
            ("x exp(-x) on [0, 5]", x => x * Math.Exp(-x), 0, 5, 1 - 6 * Math.Exp(-5)),
        ];
        foreach (var (name, f, a, b, exact) in cases)
        {
            var gk = Quadrature.GaussKronrod(f, a, b, Tight);
            var simpson = Quadrature.AdaptiveSimpson(f, a, b, new StoppingCriteria(AbsoluteTolerance: 1e-12, RelativeTolerance: 1e-12));
            Assert.IsTrue(gk.Converged, name);
            Assert.IsTrue(simpson.Converged, name);
            Assert.AreEqual(exact, gk.Value, 1e-12 * Math.Max(1, Math.Abs(exact)), $"GK {name}");
            Assert.AreEqual(exact, simpson.Value, 1e-10 * Math.Max(1, Math.Abs(exact)), $"Simpson {name}");
            Assert.IsTrue(Math.Abs(gk.Value - exact) <= Math.Max(gk.ErrorEstimate * 10, 1e-14), $"GK error estimate {name}");

            // Romberg is only for smooth integrands, so skip the sqrt singularity in the derivative at 0.
            if (name != "sqrt on [0,4]")
            {
                var romberg = Quadrature.Romberg(f, a, b, new StoppingCriteria(AbsoluteTolerance: 1e-13, RelativeTolerance: 1e-13));
                Assert.IsTrue(romberg.Converged, name);
                Assert.AreEqual(exact, romberg.Value, 1e-12 * Math.Max(1, Math.Abs(exact)), $"Romberg {name}");
            }
        }
    }

    [TestMethod]
    public void RandomPolynomialsAreIntegratedExactly()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 1_000; i++)
        {
            var degree = gen.Random.Next(0, 20);
            var coefficients = Enumerable.Range(0, degree + 1).Select(_ => gen.Uniform(-3, 3)).ToArray();
            var a = gen.Uniform(-2, 1);
            var b = a + gen.Uniform(0.1, 3);
            double P(double x) => coefficients.Reverse().Aggregate(0.0, (acc, c) => acc * x + c);
            double Antiderivative(double x) => coefficients.Select((c, k) => c * Math.Pow(x, k + 1) / (k + 1)).Sum();
            var exact = Antiderivative(b) - Antiderivative(a);

            var result = Quadrature.GaussKronrod(P, a, b, new StoppingCriteria(AbsoluteTolerance: 1e-13, RelativeTolerance: 1e-13));
            var ctx = $"seed={Seed} case={i} degree={degree} [{a:R}, {b:R}]";
            Assert.IsTrue(result.Converged, ctx);
            Assert.AreEqual(exact, result.Value, 1e-11 * (1 + Math.Abs(exact)), ctx);

            // Reversed limits flip the sign.
            Assert.AreEqual(-result.Value, Quadrature.GaussKronrod(P, b, a, new StoppingCriteria(AbsoluteTolerance: 1e-13, RelativeTolerance: 1e-13)).Value, 1e-12 * (1 + Math.Abs(exact)), ctx);
        }
    }

    [TestMethod]
    public void EndpointSingularitiesAndInfiniteRanges()
    {
        var logSqrt = Quadrature.GaussKronrod(x => Math.Log(x) / Math.Sqrt(x), 0.0, 1.0, new StoppingCriteria(RelativeTolerance: 1e-10));
        Assert.IsTrue(logSqrt.Converged, logSqrt.ToString());
        Assert.AreEqual(-4.0, logSqrt.Value, 1e-8);

        Assert.AreEqual(2.0, Quadrature.GaussKronrod(x => 1 / Math.Sqrt(x), 0.0, 1.0, new StoppingCriteria(RelativeTolerance: 1e-10)).Value, 1e-8);

        var cases = new (string Name, Func<double, double> F, double A, double B, double Exact)[]
        {
            ("exp(-x) on [0, inf)", x => Math.Exp(-x), 0, double.PositiveInfinity, 1),
            ("exp(-x^2) on R", x => Math.Exp(-x * x), double.NegativeInfinity, double.PositiveInfinity, Math.Sqrt(Math.PI)),
            ("1/(1+x^2) on R", x => 1 / (1 + x * x), double.NegativeInfinity, double.PositiveInfinity, Math.PI),
            ("exp(x) on (-inf, 0]", Math.Exp, double.NegativeInfinity, 0, 1),
            ("1/(1+x)^2 on [1, inf)", x => 1 / ((1 + x) * (1 + x)), 1, double.PositiveInfinity, 0.5),
            ("exp(-x) on [a, inf), a = 3", x => Math.Exp(-x), 3, double.PositiveInfinity, Math.Exp(-3)),
        };
        foreach (var (name, f, a, b, exact) in cases)
        {
            var result = Quadrature.Integrate(f, a, b, new StoppingCriteria(RelativeTolerance: 1e-11));
            Assert.IsTrue(result.Converged, $"{name}: {result}");
            Assert.AreEqual(exact, result.Value, 1e-9 * Math.Max(1, Math.Abs(exact)), name);
        }

        // Reversed infinite limits.
        Assert.AreEqual(-1.0, Quadrature.Integrate(x => Math.Exp(-x), double.PositiveInfinity, 0.0, new StoppingCriteria(RelativeTolerance: 1e-11)).Value, 1e-9);
        Assert.AreEqual(0.0, Quadrature.Infinite(x => x, double.PositiveInfinity, double.PositiveInfinity).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => Quadrature.Infinite(x => x, double.NaN, 1.0));
    }

    [TestMethod]
    public void NonConvergenceIsReportedNotThrown()
    {
        // 1/x is not integrable at 0: the routine must give up with a warning, not hang or throw.
        var divergent = Quadrature.GaussKronrod(x => 1 / x, 0.0, 1.0, new StoppingCriteria(RelativeTolerance: 1e-10, MaxIterations: 200));
        Assert.IsFalse(divergent.Converged);
        Assert.IsTrue(divergent.Warnings.Length > 0);

        var simpson = Quadrature.AdaptiveSimpson(x => 1 / x, 0.0, 1.0, new StoppingCriteria(RelativeTolerance: 1e-10, MaxIterations: 200));
        Assert.IsFalse(simpson.Converged);

        var nan = Quadrature.GaussKronrod(x => double.NaN, 0.0, 1.0);
        Assert.IsFalse(nan.Converged);
        Assert.IsTrue(nan.Warnings.Length > 0);

        var romberg = Quadrature.Romberg(x => Math.Sqrt(x), 0.0, 1.0, new StoppingCriteria(RelativeTolerance: 1e-14, AbsoluteTolerance: 1e-14, MaxIterations: 6));
        Assert.IsFalse(romberg.Converged);

        // Sharp oscillation within a tiny subinterval budget.
        var oscillatory = Quadrature.GaussKronrod(x => Math.Sin(1000 * x), 0.0, 10.0, new StoppingCriteria(RelativeTolerance: 1e-12, MaxIterations: 3));
        Assert.IsFalse(oscillatory.Converged);
        Assert.AreEqual(0.0, Quadrature.GaussKronrod(x => 1.0, 2.0, 2.0).Value);
    }

    [TestMethod]
    public void ZeroIntegralsConvergeViaTheRoundOffFloor()
    {
        var result = Quadrature.GaussKronrod(Math.Sin, 0.0, 2 * Math.PI);
        Assert.IsTrue(result.Converged);
        Assert.AreEqual(0.0, result.Value, 1e-14);
        Assert.IsTrue(Quadrature.AdaptiveSimpson(Math.Sin, 0.0, 2 * Math.PI).Converged);
    }

    [TestMethod]
    public void SinglePrecisionWorks()
    {
        var result = Quadrature.GaussKronrod<float>(MathF.Sin, 0f, MathF.PI);
        Assert.IsTrue(result.Converged);
        Assert.AreEqual(2f, result.Value, 1e-5f);
    }

    [TestMethod]
    public void RombergConvergesOnIntegralsThatCancelToZero()
    {
        var result = Quadrature.Romberg(Math.Sin, 0.0, 2 * Math.PI);
        Assert.IsTrue(result.Converged, $"evaluations={result.Evaluations}");
        Assert.AreEqual(0.0, result.Value, 1e-14);
        Assert.IsLessThan(10_000, result.Evaluations);
    }

    [TestMethod]
    public void InfiniteRangesDoNotHideAnUndefinedIntegrand()
    {
        // sqrt(x) is undefined for x < 0: the integral over the whole line has no value, so it must not report one as converged.
        var result = Quadrature.Infinite<double>(x => Math.Sqrt(x) * Math.Exp(-x * x), double.NegativeInfinity, double.PositiveInfinity);
        Assert.IsFalse(result.Converged);
        Assert.IsTrue(double.IsNaN(result.Value) || double.IsInfinity(result.ErrorEstimate));

        // Overflow of the change of variable far out is still treated as the integrand's limit, 0.
        Assert.AreEqual(Math.Sqrt(Math.PI), Quadrature.Infinite<double>(x => Math.Exp(-x * x), double.NegativeInfinity, double.PositiveInfinity, Tight).Value, 1e-12);
    }
}
