using Mathesis.Numerics;
using Mathesis.Numerics.Optimization;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

[TestClass]
public class OptimizationTests
{
    private const int Seed = 20261030;

    private static double Rosenbrock(double[] p) => 100 * Math.Pow(p[1] - p[0] * p[0], 2) + Math.Pow(1 - p[0], 2);

    [TestMethod]
    public void GoldenSectionFindsTheMinimaFromThePlan()
    {
        // (x - 2)^2 + 1 on [0, 5]: the minimizer is only determined to about sqrt(eps), the minimum value to rounding.
        var parabola = Minimize.GoldenSection(x => (x - 2) * (x - 2) + 1, 0.0, 5.0);
        Assert.IsTrue(parabola.Converged, parabola.ToString());
        Assert.AreEqual(2.0, parabola.Minimizer[0], 1e-7);
        Assert.AreEqual(1.0, parabola.Minimum, 1e-14);
        Assert.AreEqual(parabola.Iterations + 2, parabola.Evaluations);

        var cosine = Minimize.GoldenSection(Math.Cos, 3.0, 4.0);
        Assert.IsTrue(cosine.Converged);
        Assert.AreEqual(Math.PI, cosine.Minimizer[0], 1e-7);
        Assert.AreEqual(-1.0, cosine.Minimum, 1e-14);
    }

    [TestMethod]
    public void GoldenSectionOnSeededQuadraticsAndEdgeCases()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 500; i++)
        {
            var center = gen.Uniform(-10, 10);
            var curvature = gen.Uniform(0.1, 10);
            var offset = gen.Uniform(-5, 5);
            var result = Minimize.GoldenSection(x => curvature * (x - center) * (x - center) + offset, center - gen.Uniform(0.5, 8), center + gen.Uniform(0.5, 8));
            var ctx = $"seed={Seed} case={i} center={center:R}";
            Assert.IsTrue(result.Converged, ctx);
            Assert.AreEqual(center, result.Minimizer[0], 1e-7 * (1 + Math.Abs(center)), ctx);
            Assert.AreEqual(offset, result.Minimum, 1e-13 * (1 + Math.Abs(offset)), ctx);
        }

        // The minimum of a monotone function is at the boundary; |x - 1| has a kink; a reversed bracket is accepted.
        Assert.AreEqual(0.0, Minimize.GoldenSection(Math.Exp, 0.0, 1.0).Minimizer[0], 1e-7);
        Assert.AreEqual(1.0, Minimize.GoldenSection(x => Math.Abs(x - 1), -3.0, 4.0).Minimizer[0], 1e-7);
        Assert.AreEqual(2.0, Minimize.GoldenSection(x => (x - 2) * (x - 2), 5.0, 0.0).Minimizer[0], 1e-7);

        // Non-convergence and bad values are reported, not thrown.
        var capped = Minimize.GoldenSection(x => x * x, -1.0, 1.0, new StoppingCriteria(MaxIterations: 5));
        Assert.IsFalse(capped.Converged);
        Assert.AreEqual(Convergence.MaxIterations, capped.Reason);
        Assert.AreEqual(Convergence.NotFinite, Minimize.GoldenSection(x => double.NaN, 0.0, 1.0).Reason);
        Assert.Throws<ArgumentOutOfRangeException>(() => Minimize.GoldenSection(x => x, double.NaN, 1.0));
    }

    [TestMethod]
    public void NelderMeadMinimizesRosenbrockWithinTheEvaluationBudget()
    {
        // The Phase 3 exit check: from (-1.2, 1) to within 1e-6 of (1, 1) using at most 2,000 evaluations.
        var result = Minimize.NelderMead(Rosenbrock, [-1.2, 1.0]);
        Assert.IsTrue(result.Converged, result.ToString());
        Assert.IsTrue(result.Evaluations <= 2_000, $"{result.Evaluations} evaluations");
        Assert.AreEqual(1.0, result.Minimizer[0], 1e-6);
        Assert.AreEqual(1.0, result.Minimizer[1], 1e-6);
        Assert.IsTrue(result.Minimum < 1e-12);
    }

    [TestMethod]
    public void NelderMeadOnOtherProblems()
    {
        // A 5-dimensional shifted sphere from random starts, seeded.
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 100; i++)
        {
            var target = Enumerable.Range(0, 5).Select(_ => gen.Uniform(-3, 3)).ToArray();
            var start = target.Select(t => t + gen.Uniform(-2, 2)).ToArray();
            var result = Minimize.NelderMead(p => p.Zip(target, (x, t) => (x - t) * (x - t)).Sum(), start);
            Assert.IsTrue(result.Converged, $"seed={Seed + 1} case={i}: {result}");
            for (var k = 0; k < 5; k++) Assert.AreEqual(target[k], result.Minimizer[k], 1e-6, $"seed={Seed + 1} case={i}");
        }

        // Booth's function has the single minimum (1, 3) with value 0.
        var booth = Minimize.NelderMead(p => Math.Pow(p[0] + 2 * p[1] - 7, 2) + Math.Pow(2 * p[0] + p[1] - 5, 2), [0.0, 0.0]);
        Assert.AreEqual(1.0, booth.Minimizer[0], 1e-6);
        Assert.AreEqual(3.0, booth.Minimizer[1], 1e-6);

        // One dimension, an explicit step, and a start at the origin (zero coordinates use the absolute default step).
        var line = Minimize.NelderMead(p => Math.Pow(p[0] - 4, 2), [0.0], step: 1.0);
        Assert.AreEqual(4.0, line.Minimizer[0], 1e-6);
        Assert.AreEqual(0.0, Minimize.NelderMead(p => Math.Abs(p[0]) + Math.Abs(p[1]), [0.0, 0.0]).Minimum, 1e-9);
    }

    [TestMethod]
    public void NelderMeadReportsFailureWithoutThrowing()
    {
        var capped = Minimize.NelderMead(Rosenbrock, [-1.2, 1.0], stop: new StoppingCriteria(MaxIterations: 10));
        Assert.IsFalse(capped.Converged);
        Assert.AreEqual(Convergence.MaxIterations, capped.Reason);
        Assert.IsTrue(capped.Minimum < Rosenbrock([-1.2, 1.0]));

        // NaN objective values are treated as +infinity: the search still returns a finite point.
        var nan = Minimize.NelderMead(p => p[0] < 0 ? double.NaN : (p[0] - 1) * (p[0] - 1), [2.0]);
        Assert.AreEqual(1.0, nan.Minimizer[0], 1e-6);

        Assert.Throws<ArgumentException>(() => Minimize.NelderMead(p => 0.0, Array.Empty<double>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Minimize.NelderMead(p => 0.0, [double.NaN]));
        Assert.Throws<ArgumentNullException>(() => Minimize.NelderMead<double>(null!, [1.0]));
    }

    [TestMethod]
    public void SinglePrecisionWorks()
    {
        var golden = Minimize.GoldenSection<float>(x => (x - 2f) * (x - 2f), 0f, 5f);
        Assert.IsTrue(golden.Converged);
        Assert.AreEqual(2f, golden.Minimizer[0], 1e-3f);
        var simplex = Minimize.NelderMead<float>(p => (p[0] - 1f) * (p[0] - 1f) + (p[1] + 2f) * (p[1] + 2f), [0f, 0f]);
        Assert.AreEqual(1f, simplex.Minimizer[0], 1e-3f);
        Assert.AreEqual(-2f, simplex.Minimizer[1], 1e-3f);
    }
}
