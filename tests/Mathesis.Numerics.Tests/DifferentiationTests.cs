using Mathesis.Numerics.Differentiation;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

[TestClass]
public class DifferentiationTests
{
    private const int Seed = 20261006;

    private static readonly (string Name, Func<double, double> F, Func<double, double> D, Func<double, double> D2)[] Functions =
    [
        ("sin", Math.Sin, Math.Cos, x => -Math.Sin(x)),
        ("exp", Math.Exp, Math.Exp, Math.Exp),
        ("x^3 - 2x", x => x * x * x - 2 * x, x => 3 * x * x - 2, x => 6 * x),
        ("atan", Math.Atan, x => 1 / (1 + x * x), x => -2 * x / ((1 + x * x) * (1 + x * x))),
        ("log(1 + x^2)", x => Math.Log(1 + x * x), x => 2 * x / (1 + x * x), x => 2 * (1 - x * x) / ((1 + x * x) * (1 + x * x))),
    ];

    [TestMethod]
    public void FiniteDifferencesHaveTheirDocumentedAccuracy()
    {
        var gen = new Gen(Seed);
        foreach (var (name, f, d, d2) in Functions)
        {
            for (var i = 0; i < 300; i++)
            {
                var x = gen.Uniform(-2, 2);
                var scale = Math.Max(1, Math.Abs(d(x)));
                var ctx = $"seed={Seed} f={name} x={x:R}";
                Assert.AreEqual(d(x), FiniteDifferences.Forward(f, x), 1e-6 * scale, ctx);
                Assert.AreEqual(d(x), FiniteDifferences.Backward(f, x), 1e-6 * scale, ctx);
                Assert.AreEqual(d(x), FiniteDifferences.Central(f, x), 1e-9 * scale, ctx);
                Assert.AreEqual(d(x), FiniteDifferences.FivePoint(f, x), 1e-10 * scale, ctx);
                Assert.AreEqual(d2(x), FiniteDifferences.Second(f, x), 1e-6 * Math.Max(1, Math.Abs(d2(x))), ctx);
            }
        }
    }

    [TestMethod]
    public void TruncationErrorShrinksWithTheDocumentedOrder()
    {
        // Forward O(h), central O(h^2), five-point O(h^4): the error ratio when h is halved.
        double Ratio(Func<Func<double, double>, double, double, double> method, double h) =>
            Math.Abs(method(Math.Exp, 1.0, h) - Math.E) / Math.Abs(method(Math.Exp, 1.0, h / 2) - Math.E);

        Assert.AreEqual(2.0, Ratio((f, x, h) => FiniteDifferences.Forward(f, x, h), 1e-3), 0.05);
        Assert.AreEqual(4.0, Ratio((f, x, h) => FiniteDifferences.Central(f, x, h), 1e-2), 0.05);
        Assert.AreEqual(16.0, Ratio((f, x, h) => FiniteDifferences.FivePoint(f, x, h), 1e-1), 0.5);
    }

    [TestMethod]
    public void RichardsonExtrapolationIsNearlyExact()
    {
        var gen = new Gen(Seed + 1);
        foreach (var (name, f, d, _) in Functions)
        {
            for (var i = 0; i < 300; i++)
            {
                var x = gen.Uniform(-2, 2);
                var result = FiniteDifferences.Richardson(f, x);
                var ctx = $"seed={Seed + 1} f={name} x={x:R}";
                var scale = Math.Max(1, Math.Abs(d(x)));
                Assert.AreEqual(d(x), result.Value, 1e-11 * scale, ctx);
                Assert.IsTrue(result.ErrorEstimate < 1e-8 * scale, ctx);
                Assert.IsTrue(result.ErrorEstimate > 0 && double.IsFinite(result.ErrorEstimate), ctx);
                Assert.IsTrue(result.Evaluations is >= 4 and <= 20, ctx);
            }
        }
    }

    [TestMethod]
    public void RichardsonBeatsTheSingleCentralDifference()
    {
        // For D(h) with h = 0.1, plain central difference has error ~ h^2 f'''/6; extrapolation removes it.
        var plain = Math.Abs(FiniteDifferences.Central(Math.Exp, 1.0, 0.1) - Math.E);
        var extrapolated = Math.Abs(FiniteDifferences.Richardson(Math.Exp, 1.0, 0.1).Value - Math.E);
        Assert.IsTrue(plain > 1e-3);
        Assert.IsTrue(extrapolated < 1e-12, $"{extrapolated:E2}");
    }

    [TestMethod]
    public void SinglePrecisionAndArguments()
    {
        var single = FiniteDifferences.Richardson<float>(MathF.Sin, 1f);
        Assert.AreEqual(MathF.Cos(1f), single.Value, 1e-4f);
        Assert.AreEqual(MathF.Cos(1f), FiniteDifferences.Central<float>(MathF.Sin, 1f), 1e-3f);
        Assert.Throws<ArgumentNullException>(() => FiniteDifferences.Central<double>(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FiniteDifferences.Richardson(Math.Sin, 0.0, levels: 1));
    }
}
