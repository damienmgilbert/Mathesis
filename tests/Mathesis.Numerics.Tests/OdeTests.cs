using Mathesis.Numerics;
using Mathesis.Numerics.Ode;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

[TestClass]
public class OdeTests
{
    private const int Seed = 20261004;

    private static readonly int[] StepCounts = [8, 16, 32, 64, 128];

    private static readonly OdeOptions Tight = new(RelativeTolerance: 1e-12, AbsoluteTolerance: 1e-14);

    [TestMethod]
    public void DormandPrinceMatchesExponentialDecay()
    {
        // The Phase 2 exit table: y' = -y against e^(-t) to 1e-10.
        var solution = OdeSolver.DormandPrince((t, y) => -y, 0.0, 1.0, 5.0, Tight);
        Assert.IsTrue(solution.Converged, solution.ToString());
        Assert.AreEqual(5.0, solution.FinalTime);
        for (var i = 0; i < solution.Times.Length; i++)
        {
            Assert.AreEqual(Math.Exp(-solution.Times[i]), solution.States[i][0], 1e-10, $"t={solution.Times[i]:R}");
        }
        Assert.IsTrue(solution.AcceptedSteps is > 10 and < 1_000, $"{solution.AcceptedSteps} steps");
        Assert.AreEqual(solution.AcceptedSteps + 1, solution.Times.Length);
    }

    [TestMethod]
    public void DenseOutputInterpolatesBetweenSteps()
    {
        var solution = OdeSolver.DormandPrince((t, y) => -y, 0.0, 1.0, 5.0, Tight);

        // Exact at the step ends.
        for (var i = 0; i < solution.Times.Length; i++)
        {
            Assert.AreEqual(solution.States[i][0], solution.Evaluate(solution.Times[i])[0], 1e-14, $"node {i}");
        }

        // Between steps the continuous extension has order 4, so it is looser than the step values.
        var gen = new Gen(Seed);
        for (var i = 0; i < 2_000; i++)
        {
            var t = gen.Uniform(0, 5);
            Assert.AreEqual(Math.Exp(-t), solution.Evaluate(t)[0], 1e-8, $"seed={Seed} t={t:R}");
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => solution.Evaluate(5.0001));
        Assert.Throws<ArgumentOutOfRangeException>(() => solution.Evaluate(-0.0001));
    }

    [TestMethod]
    public void SystemsAndBackwardIntegration()
    {
        // y'' = -y as the first-order system (y, v)' = (v, -y); y(0) = 0, v(0) = 1 gives (sin t, cos t).
        void Oscillator(double t, ReadOnlySpan<double> y, Span<double> dydt)
        {
            dydt[0] = y[1];
            dydt[1] = -y[0];
        }
        var forward = OdeSolver.DormandPrince<double>(Oscillator, 0, [0, 1], 20, Tight);
        Assert.IsTrue(forward.Converged);
        Assert.AreEqual(Math.Sin(20), forward.FinalState[0], 1e-9);
        Assert.AreEqual(Math.Cos(20), forward.FinalState[1], 1e-9);

        // Energy y^2 + v^2 stays at 1 along the solution.
        foreach (var state in forward.States) Assert.AreEqual(1.0, state[0] * state[0] + state[1] * state[1], 1e-9);

        var backward = OdeSolver.DormandPrince<double>(Oscillator, 0, [0, 1], -7, Tight);
        Assert.IsTrue(backward.Converged);
        Assert.AreEqual(-7.0, backward.FinalTime);
        Assert.AreEqual(Math.Sin(-7), backward.FinalState[0], 1e-9);
        Assert.AreEqual(Math.Sin(-3.3), backward.Evaluate(-3.3)[0], 1e-8);
        Assert.IsTrue(backward.Times.Zip(backward.Times.Skip(1), (a, b) => b < a).All(x => x));

        var empty = OdeSolver.DormandPrince((t, y) => y, 1.0, 1.0, 1.0);
        Assert.IsTrue(empty.Converged);
        Assert.AreEqual(0, empty.AcceptedSteps);
    }

    [TestMethod]
    public void Rk4HasOrderFour()
    {
        // Global error O(h^4): halving the step divides the error by about 16.
        double ErrorWith(int steps) => Math.Abs(OdeSolver.Rk4((t, y) => y, 0.0, 1.0, 1.0, steps).FinalState[0] - Math.E);
        var errors = StepCounts.Select(ErrorWith).ToArray();
        for (var i = 1; i < errors.Length; i++)
        {
            var ratio = errors[i - 1] / errors[i];
            Assert.IsTrue(ratio is > 14 and < 18, $"ratio {ratio} at {i}");
        }

        var solution = OdeSolver.Rk4((t, y) => -y, 0.0, 1.0, 5.0, 500);
        Assert.IsTrue(solution.Converged);
        Assert.AreEqual(501, solution.Times.Length);
        Assert.AreEqual(5.0, solution.FinalTime);
        Assert.AreEqual(2_000, solution.Evaluations);
        Assert.AreEqual(Math.Exp(-5), solution.FinalState[0], 1e-10);
        Assert.Throws<InvalidOperationException>(() => solution.Evaluate(1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => OdeSolver.Rk4((t, y) => y, 0.0, 1.0, 1.0, 0));
    }

    [TestMethod]
    public void NonConvergenceIsReportedNotThrown()
    {
        // y' = y^2, y(0) = 1 blows up at t = 1.
        var blowup = OdeSolver.DormandPrince((t, y) => y * y, 0.0, 1.0, 2.0);
        Assert.IsFalse(blowup.Converged);
        Assert.IsTrue(blowup.Reason is Convergence.StepTooSmall or Convergence.NotFinite or Convergence.MaxIterations, blowup.Reason.ToString());
        Assert.IsTrue(blowup.FinalTime < 1.0 + 1e-6);

        var capped = OdeSolver.DormandPrince((t, y) => -y, 0.0, 1.0, 100.0, new OdeOptions(MaxSteps: 5));
        Assert.IsFalse(capped.Converged);
        Assert.AreEqual(Convergence.MaxIterations, capped.Reason);

        var nan = OdeSolver.Rk4((t, y) => double.NaN, 0.0, 1.0, 1.0, 10);
        Assert.IsFalse(nan.Converged);
        Assert.AreEqual(Convergence.NotFinite, nan.Reason);
    }

    [TestMethod]
    public void TighterToleranceGivesSmallerError()
    {
        double Error(double rtol)
        {
            var s = OdeSolver.DormandPrince((t, y) => y * Math.Cos(t), 0.0, 1.0, 10.0, new OdeOptions(RelativeTolerance: rtol, AbsoluteTolerance: rtol * 1e-2));
            return Math.Abs(s.FinalState[0] - Math.Exp(Math.Sin(10.0)));
        }
        var loose = Error(1e-4);
        var tight = Error(1e-10);
        Assert.IsTrue(tight < loose / 100, $"loose={loose:E2} tight={tight:E2}");
        Assert.IsTrue(tight < 1e-8);
    }

    [TestMethod]
    public void SinglePrecisionWorks()
    {
        var solution = OdeSolver.DormandPrince<float>((t, y) => -y, 0f, 1f, 2f);
        Assert.IsTrue(solution.Converged);
        Assert.AreEqual(MathF.Exp(-2f), solution.FinalState[0], 1e-5f);
    }
}
