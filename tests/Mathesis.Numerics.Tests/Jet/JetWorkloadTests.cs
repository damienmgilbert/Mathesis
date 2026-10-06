using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// The Technesis prototype workloads (W1 to W3) on the real <c>Jet&lt;double&gt;</c>: the Jacobian equals the N-pass <c>Dual&lt;double&gt;</c> result to
/// 1e−14 and the Richardson central-difference result to 1e−8 (the Technesis prototype report's bounds, but 1e−14 rather than 1e−15 against Dual
/// because the chain-rule multipliers are formed before the gradient lanes, which rounds differently), and in steady state allocates
/// 0 bytes per Jacobian (the representation is not the heap one).
/// </summary>
[TestClass]
public class JetWorkloadTests
{
    private static void CheckAgreement<TWorkload>() where TWorkload : IWorkload
    {
        int n = TWorkload.Inputs, m = TWorkload.Outputs;
        var engine = new JetEngine<TWorkload>();
        var dual = new DualPassesEngine<TWorkload>();
        var difference = new FiniteDifferenceEngine<TWorkload>();
        var values = new double[m];
        var jacobian = new double[m * n];
        var dualValues = new double[m];
        var dualJacobian = new double[m * n];
        var differenceJacobian = new double[m * n];
        double worstDual = 0.0, worstDifference = 0.0;
        foreach (var point in TWorkload.Points())
        {
            engine.Jacobian(point, values, jacobian);
            dual.Jacobian(point, dualValues, dualJacobian);
            difference.Jacobian(point, dualValues, differenceJacobian);
            var errorDual = Compare.ElementRelative(jacobian, dualJacobian);
            var errorDifference = Compare.MatrixRelative(jacobian, differenceJacobian);
            Assert.IsLessThanOrEqualTo(1e-14, errorDual, $"{TWorkload.Name} at [{string.Join(", ", point)}] against Dual N-pass");
            Assert.IsLessThanOrEqualTo(1e-8, errorDifference, $"{TWorkload.Name} at [{string.Join(", ", point)}] against Richardson");
            worstDual = Math.Max(worstDual, errorDual);
            worstDifference = Math.Max(worstDifference, errorDifference);
        }
        Console.WriteLine($"{TWorkload.Name}: worst against Dual N-pass {worstDual:E2}, against Richardson {worstDifference:E2}");
    }

    [TestMethod]
    public void W1JacobianAgreesWithDualPassesAndFiniteDifferences() => CheckAgreement<ForwardKinematics6R>();

    [TestMethod]
    public void W2JacobianAgreesWithDualPassesAndFiniteDifferences() => CheckAgreement<InsErrorState15>();

    [TestMethod]
    public void W3JacobianAgreesWithDualPassesAndFiniteDifferencesAcrossThreeChunks() => CheckAgreement<RosenbrockChain40>();

    private static void CheckNoAllocation<TWorkload>() where TWorkload : IWorkload
    {
        var engine = new JetEngine<TWorkload>();
        var points = TWorkload.Points();
        var values = new double[TWorkload.Outputs];
        var jacobian = new double[TWorkload.Outputs * TWorkload.Inputs];
        const int Repeats = 20;
        var allocated = Allocation.Measure(() =>
        {
            for (var r = 0; r < Repeats; r++)
                foreach (var point in points)
                    engine.Jacobian(point, values, jacobian);
        });
        Assert.AreEqual(0L, allocated, $"{TWorkload.Name}: Jet<double> allocated {allocated} bytes over {Repeats * points.Length} Jacobians");
    }

    [TestMethod]
    public void W1AllocatesNothingPerJacobian() => CheckNoAllocation<ForwardKinematics6R>();

    [TestMethod]
    public void W2AllocatesNothingPerJacobian() => CheckNoAllocation<InsErrorState15>();

    [TestMethod]
    public void W3AllocatesNothingPerJacobian() => CheckNoAllocation<RosenbrockChain40>();

    [TestMethod]
    public void NestedJetArithmeticAllocatesNothing()
    {
        var x = JetJetD.Variable(JetD.Variable(0.7, 0, 2), 0, 2);
        var y = JetJetD.Variable(JetD.Variable(1.3, 1, 2), 1, 2);
        var sink = 0.0;
        var allocated = Allocation.Measure(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                var r = JetJetD.Sin(x * y) + JetJetD.Exp(x) / (y * y + JetJetD.One);
                sink += r.Value.Value;
            }
        });
        Assert.AreEqual(0L, allocated);
        Assert.IsTrue(double.IsFinite(sink));
    }
}
