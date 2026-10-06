using Mathesis.Numbers;
using System.Numerics;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// Hessians by nested jets (ADR-19): <c>Jet&lt;Jet&lt;double&gt;&gt;</c> Hessians of ten functions match analytic ones to 1e−12 and
/// are symmetric to 1e−13, both measured against the larger of 1 and the largest analytic entry. Each function is written once, generic
/// over the number type, so the same body gives the value on <c>double</c>. One function has 20 inputs, which needs chunked passes.
/// </summary>
[TestClass]
public class JetHessianTests
{
    private delegate TNum Function<TNum>(ReadOnlySpan<TNum> x);

    private sealed record Case(string Name, int Inputs, double Low, double High, JetScalarFunction<JetJetD> Jet, Function<double> Plain, Func<double[], double[,]> Hessian);

    private static readonly Case[] Cases =
    [
        new("x0·x1 + x0²", 2, -2, 2, F1<JetJetD>, F1<double>, x => new[,] { { 2.0, 1.0 }, { 1.0, 0.0 } }),
        new("exp(x0·x1)", 2, -1.5, 1.5, F2<JetJetD>, F2<double>, x =>
        {
            var e = Math.Exp(x[0] * x[1]);
            return new[,] { { e * x[1] * x[1], e * (1 + x[0] * x[1]) }, { e * (1 + x[0] * x[1]), e * x[0] * x[0] } };
        }),
        new("sin(x0)·cos(x1)", 2, -3, 3, F3<JetJetD>, F3<double>, x =>
        {
            double sa = Math.Sin(x[0]), ca = Math.Cos(x[0]), sb = Math.Sin(x[1]), cb = Math.Cos(x[1]);
            return new[,] { { -sa * cb, -ca * sb }, { -ca * sb, -sa * cb } };
        }),
        new("log(1 + x0² + x1²)", 2, -3, 3, F4<JetJetD>, F4<double>, x =>
        {
            var u = 1 + x[0] * x[0] + x[1] * x[1];
            return new[,]
            {
                { 2 / u - 4 * x[0] * x[0] / (u * u), -4 * x[0] * x[1] / (u * u) },
                { -4 * x[0] * x[1] / (u * u), 2 / u - 4 * x[1] * x[1] / (u * u) },
            };
        }),
        new("sqrt(1 + |x|²), 3 inputs", 3, -2, 2, F5<JetJetD>, F5<double>, x =>
        {
            var r = Math.Sqrt(1 + x[0] * x[0] + x[1] * x[1] + x[2] * x[2]);
            var h = new double[3, 3];
            for (var i = 0; i < 3; i++)
                for (var j = 0; j < 3; j++) h[i, j] = (i == j ? 1.0 / r : 0.0) - x[i] * x[j] / (r * r * r);
            return h;
        }),
        new("atan2(x0, x1)", 2, 0.3, 3, F6<JetJetD>, F6<double>, x =>
        {
            var d = x[0] * x[0] + x[1] * x[1];
            return new[,]
            {
                { -2 * x[0] * x[1] / (d * d), (x[0] * x[0] - x[1] * x[1]) / (d * d) },
                { (x[0] * x[0] - x[1] * x[1]) / (d * d), 2 * x[0] * x[1] / (d * d) },
            };
        }),
        new("Rosenbrock", 2, -2, 2, F7<JetJetD>, F7<double>, x => new[,]
        {
            { 1200 * x[0] * x[0] - 400 * x[1] + 2, -400 * x[0] },
            { -400 * x[0], 200.0 },
        }),
        new("x0^x1", 2, 0.4, 3, F8<JetJetD>, F8<double>, x =>
        {
            double a = x[0], b = x[1], la = Math.Log(a);
            return new[,]
            {
                { b * (b - 1) * Math.Pow(a, b - 2), Math.Pow(a, b - 1) * (1 + b * la) },
                { Math.Pow(a, b - 1) * (1 + b * la), Math.Pow(a, b) * la * la },
            };
        }),
        new("tanh(x0 + 2·x1)", 2, -1, 1, F9<JetJetD>, F9<double>, x =>
        {
            var t = Math.Tanh(x[0] + 2 * x[1]);
            var second = -2 * t * (1 - t * t);
            return new[,] { { second, 2 * second }, { 2 * second, 4 * second } };
        }),
        new("Σ xᵢ²·xᵢ₊₁, 20 inputs", 20, -2, 2, F10<JetJetD>, F10<double>, x =>
        {
            var n = x.Length;
            var h = new double[n, n];
            for (var i = 0; i < n - 1; i++)
            {
                h[i, i] = 2 * x[i + 1];
                h[i, i + 1] = 2 * x[i];
                h[i + 1, i] = 2 * x[i];
            }
            return h;
        }),
    ];

    private static TNum Two<TNum>() where TNum : IFloatingPointIeee754<TNum> => TNum.CreateChecked(2);

    private static TNum F1<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => x[0] * x[1] + x[0] * x[0];

    private static TNum F2<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Exp(x[0] * x[1]);

    private static TNum F3<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Sin(x[0]) * TNum.Cos(x[1]);

    private static TNum F4<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Log(TNum.One + x[0] * x[0] + x[1] * x[1]);

    private static TNum F5<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Sqrt(TNum.One + x[0] * x[0] + x[1] * x[1] + x[2] * x[2]);

    private static TNum F6<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Atan2(x[0], x[1]);

    private static TNum F7<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum>
    {
        var a = x[1] - x[0] * x[0];
        var b = TNum.One - x[0];
        return TNum.CreateChecked(100) * a * a + b * b;
    }

    private static TNum F8<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Pow(x[0], x[1]);

    private static TNum F9<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum> => TNum.Tanh(x[0] + Two<TNum>() * x[1]);

    private static TNum F10<TNum>(ReadOnlySpan<TNum> x) where TNum : IFloatingPointIeee754<TNum>
    {
        var sum = TNum.Zero;
        for (var i = 0; i < x.Length - 1; i++) sum += x[i] * x[i] * x[i + 1];
        return sum;
    }

    [TestMethod]
    public void NestedJetHessiansMatchAnalyticOnesAndAreSymmetric()
    {
        var gen = new Gen(4201);
        double worstError = 0.0, worstAsymmetry = 0.0;
        foreach (var c in Cases)
        {
            for (var s = 0; s < 20; s++)
            {
                var x = new double[c.Inputs];
                for (var i = 0; i < x.Length; i++) x[i] = gen.Uniform(c.Low, c.High);
                var n = x.Length;
                var h = new double[n * n];
                var value = JetDiff.Hessian(c.Jet, x, h);
                var expected = c.Hessian(x);

                var scale = 1.0;
                for (var i = 0; i < n; i++)
                    for (var j = 0; j < n; j++) scale = Math.Max(scale, Math.Abs(expected[i, j]));
                double error = 0.0, asymmetry = 0.0;
                for (var i = 0; i < n; i++)
                    for (var j = 0; j < n; j++)
                    {
                        error = Math.Max(error, Math.Abs(h[i * n + j] - expected[i, j]) / scale);
                        asymmetry = Math.Max(asymmetry, Math.Abs(h[i * n + j] - h[j * n + i]) / scale);
                    }
                var context = gen.Describe($"{c.Name} at [{string.Join(", ", x)}]");
                Assert.AreEqual(c.Plain(x), value, 1e-14 * Math.Max(1.0, Math.Abs(value)), context + " (value)");
                Assert.IsLessThanOrEqualTo(1e-12, error, context + " (Hessian against analytic)");
                Assert.IsLessThanOrEqualTo(1e-13, asymmetry, context + " (symmetry)");
                worstError = Math.Max(worstError, error);
                worstAsymmetry = Math.Max(worstAsymmetry, asymmetry);
            }
        }
        TestContext.WriteLine($"worst Hessian error {worstError:E2}, worst asymmetry {worstAsymmetry:E2}");
    }

    [TestMethod]
    public void GradientAndJacobianOfMoreThanSixteenInputsUseChunkedPasses()
    {
        var gen = new Gen(4202);
        // f(x) = Σ i·xᵢ² has gradient 2·i·xᵢ; 40 inputs need three chunks of at most 16.
        var x = new double[40];
        for (var i = 0; i < x.Length; i++) x[i] = gen.Uniform(-2, 2);
        var gradient = new double[40];
        var value = JetDiff.Gradient<double>(p =>
        {
            var sum = JetD.Zero;
            for (var i = 0; i < p.Length; i++) sum += JetD.Constant(i) * p[i] * p[i];
            return sum;
        }, x, gradient);
        var expectedValue = 0.0;
        for (var i = 0; i < 40; i++)
        {
            expectedValue += i * x[i] * x[i];
            Assert.AreEqual(2.0 * i * x[i], gradient[i], 1e-14 * (1 + Math.Abs(2.0 * i * x[i])), gen.Describe($"gradient {i}"));
        }
        Assert.AreEqual(expectedValue, value, 1e-12);

        // Jacobian of (Σ xᵢ, x₀·x₃₉, constant 7): row 2 is a constant output and must be zero.
        var jacobian = new double[3 * 40];
        var values = new double[3];
        JetDiff.Jacobian<double>((p, y) =>
        {
            var sum = JetD.Zero;
            for (var i = 0; i < p.Length; i++) sum += p[i];
            y[0] = sum;
            y[1] = p[0] * p[39];
            y[2] = JetD.Constant(7.0);
        }, 3, x, values, jacobian);
        for (var i = 0; i < 40; i++)
        {
            Assert.AreEqual(1.0, jacobian[i], 1e-15);
            Assert.AreEqual(i == 0 ? x[39] : i == 39 ? x[0] : 0.0, jacobian[40 + i], 1e-15, $"d(x0·x39)/dx{i}");
            Assert.AreEqual(0.0, jacobian[80 + i]);
        }
        Assert.AreEqual(7.0, values[2]);
    }

    [TestMethod]
    public void DerivativeOfAOneVariableFunction()
    {
        Assert.AreEqual(Math.Cos(0.7), JetDiff.Derivative<double>(x => JetD.Sin(x), 0.7), 1e-15);
        Assert.AreEqual(0.0, JetDiff.Derivative<double>(_ => JetD.Constant(3.0), 0.7));
        Assert.AreEqual(2.0 * 0.7, JetDiff.Derivative<double>(x => x * x, 0.7), 1e-15);
    }

    [TestMethod]
    public void DifferentiationRejectsMisusedArguments()
    {
        Assert.Throws<ArgumentException>(() => JetDiff.Gradient<double>(p => p[0], new double[2], new double[3]));
        Assert.Throws<ArgumentException>(() => JetDiff.Hessian<double>(p => p[0] * p[1], new double[2], new double[3]));
        Assert.Throws<ArgumentException>(() => JetDiff.Jacobian<double>((p, y) => y[0] = p[0], 1, new double[2], new double[2], new double[2]));
        Assert.Throws<ArgumentNullException>(() => JetDiff.Gradient<double>(null!, new double[1], new double[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => JetD.Variable(1.0, 2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => JetD.Variable(1.0, 0, 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => JetD.Variables(new double[17]));
    }

    public TestContext TestContext { get; set; } = null!;
}
