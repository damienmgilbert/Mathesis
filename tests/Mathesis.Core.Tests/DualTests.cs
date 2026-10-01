using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Core.Tests;

[TestClass]
public class DualTests
{
    private const int Seed = 27182818;
    private const double Tolerance = 1e-14;

    private static TSelf Checked<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateChecked(value);

    private static TSelf Truncating<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateTruncating(value);

    private static TSelf Saturating<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateSaturating(value);

    // Each case: name, dual implementation, plain function, analytic derivative, sampling interval.
    // The analytic derivatives are written independently of the chain-rule code in DualFunctions.
    private static readonly (string Name, Func<Dual<double>, Dual<double>> Dual, Func<double, double> Plain, Func<double, double> Analytic, double Low, double High)[] Elementary =
    [
        ("sin", Dual<double>.Sin, Math.Sin, Math.Cos, -6, 6),
        ("cos", Dual<double>.Cos, Math.Cos, x => -Math.Sin(x), -6, 6),
        ("tan", Dual<double>.Tan, Math.Tan, x => 1 / (Math.Cos(x) * Math.Cos(x)), -1.3, 1.3),
        ("asin", Dual<double>.Asin, Math.Asin, x => 1 / Math.Sqrt(1 - x * x), -0.95, 0.95),
        ("acos", Dual<double>.Acos, Math.Acos, x => -1 / Math.Sqrt(1 - x * x), -0.95, 0.95),
        ("atan", Dual<double>.Atan, Math.Atan, x => 1 / (1 + x * x), -8, 8),
        ("sinh", Dual<double>.Sinh, Math.Sinh, Math.Cosh, -4, 4),
        ("cosh", Dual<double>.Cosh, Math.Cosh, Math.Sinh, -4, 4),
        ("tanh", Dual<double>.Tanh, Math.Tanh, x => 1 / (Math.Cosh(x) * Math.Cosh(x)), -4, 4),
        ("asinh", Dual<double>.Asinh, Math.Asinh, x => 1 / Math.Sqrt(x * x + 1), -8, 8),
        ("acosh", Dual<double>.Acosh, Math.Acosh, x => 1 / (Math.Sqrt(x - 1) * Math.Sqrt(x + 1)), 1.1, 10),
        ("atanh", Dual<double>.Atanh, Math.Atanh, x => 1 / ((1 - x) * (1 + x)), -0.95, 0.95),
        ("exp", Dual<double>.Exp, Math.Exp, Math.Exp, -5, 5),
        ("exp2", Dual<double>.Exp2, x => Math.Pow(2, x), x => Math.Pow(2, x) * Math.Log(2), -5, 5),
        ("exp10", Dual<double>.Exp10, x => Math.Pow(10, x), x => Math.Pow(10, x) * Math.Log(10), -3, 3),
        ("log", Dual<double>.Log, Math.Log, x => 1 / x, 0.05, 20),
        ("log2", Dual<double>.Log2, Math.Log2, x => 1 / (x * Math.Log(2)), 0.05, 20),
        ("log10", Dual<double>.Log10, Math.Log10, x => 1 / (x * Math.Log(10)), 0.05, 20),
        ("sqrt", Dual<double>.Sqrt, Math.Sqrt, x => 0.5 / Math.Sqrt(x), 0.05, 20),
        ("cbrt", Dual<double>.Cbrt, Math.Cbrt, x => 1 / (3 * Math.Pow(Math.Cbrt(x), 2)), 0.05, 20),
    ];

    [TestMethod]
    public void TwentyElementaryFunctionsMatchAnalyticDerivatives()
    {
        Assert.AreEqual(20, Elementary.Length);
        var gen = new Gen(Seed);
        foreach (var (name, dual, plain, analytic, low, high) in Elementary)
        {
            for (var i = 0; i < 500; i++)
            {
                var x = gen.Uniform(low, high);
                var result = dual(Dual<double>.Variable(x));
                var ctx = $"seed={Seed} f={name} case={i} x={x:R}";

                Assert.AreEqual(plain(x), result.Value, ToleranceFor(plain(x), Tolerance), ctx);
                var expected = analytic(x);
                var error = Math.Abs(result.Derivative - expected) / Math.Max(Math.Abs(expected), double.Epsilon);
                Assert.IsTrue(error <= Tolerance, $"{ctx} derivative={result.Derivative:R} expected={expected:R} relative error={error:E2}");
            }
        }
    }

    private static double ToleranceFor(double reference, double relative) => Math.Abs(reference) * relative;

    [TestMethod]
    public void ChainRuleThroughACompositeFunction()
    {
        // f(x) = sin(x^2) * exp(-x) / (1 + x^2),  f' by the product, quotient and chain rules.
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 2_000; i++)
        {
            var x = gen.Uniform(-3, 3);
            var d = Dual<double>.Variable(x);
            var f = Dual<double>.Sin(d * d) * Dual<double>.Exp(-d) / (Dual<double>.One + d * d);

            var u = Math.Sin(x * x) * Math.Exp(-x);
            var du = 2 * x * Math.Cos(x * x) * Math.Exp(-x) - Math.Sin(x * x) * Math.Exp(-x);
            var v = 1 + x * x;
            var expected = (du * v - u * 2 * x) / (v * v);
            Assert.AreEqual(u / v, f.Value, 1e-14 * Math.Max(1, Math.Abs(u / v)), $"x={x:R}");
            Assert.AreEqual(expected, f.Derivative, 1e-12 * Math.Max(1, Math.Abs(expected)), $"x={x:R}");
        }
    }

    [TestMethod]
    public void PowerAtan2AndHypot()
    {
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 1_000; i++)
        {
            var x = gen.Uniform(0.2, 5);
            var c = gen.Uniform(-3, 3);
            var d = Dual<double>.Variable(x);

            var constantPower = Dual<double>.Pow(d, c);
            Assert.AreEqual(Math.Pow(x, c), constantPower.Value, 1e-13 * Math.Abs(Math.Pow(x, c)));
            Assert.AreEqual(c * Math.Pow(x, c - 1), constantPower.Derivative, 1e-13 * Math.Abs(c * Math.Pow(x, c - 1)) + 1e-300);

            // x^x has derivative x^x (ln x + 1).
            var selfPower = Dual<double>.Pow(d, d);
            Assert.AreEqual(Math.Pow(x, x) * (Math.Log(x) + 1), selfPower.Derivative, 1e-13 * Math.Max(1, Math.Abs(selfPower.Derivative)));

            // d/dx atan2(x, 2) = 2 / (x² + 4);  d/dx hypot(x, 2) = x / hypot(x, 2).
            Assert.AreEqual(2 / (x * x + 4), Dual<double>.Atan2(d, 2.0).Derivative, 1e-15);
            Assert.AreEqual(x / Math.Sqrt(x * x + 4), Dual<double>.Hypot(d, 2.0).Derivative, 1e-14);
        }

        // Negative bases are fine for a constant exponent, and x^0 is constant 1 with derivative 0.
        Assert.AreEqual(-8.0, Dual<double>.Pow(Dual<double>.Variable(-2), 3.0).Value);
        Assert.AreEqual(12.0, Dual<double>.Pow(Dual<double>.Variable(-2), 3.0).Derivative);
        Assert.AreEqual(new Dual<double>(1, 0), Dual<double>.Pow(Dual<double>.Variable(0), 0.0));
    }

    [TestMethod]
    public void ExactDerivativesOfRationalFunctionsOverBigRational()
    {
        // f(x) = (x^3 - 2x + 1) / (x^2 + 3);  f'(x) = ((3x^2 - 2)(x^2 + 3) - (x^3 - 2x + 1)(2x)) / (x^2 + 3)^2, exactly.
        var gen = new Gen(Seed + 3);
        for (var i = 0; i < 2_000; i++)
        {
            var x = gen.Rational(20);
            var d = Dual<BigRational>.Variable(x);
            var f = (d * d * d - BigRational.Parse("2") * d + Dual<BigRational>.One) / (d * d + BigRational.Parse("3"));

            var numerator = x * x * x - 2 * x + 1;
            var denominator = x * x + 3;
            var expected = ((3 * x * x - 2) * denominator - numerator * 2 * x) / (denominator * denominator);
            Assert.AreEqual(numerator / denominator, f.Value, $"seed={Seed + 3} case={i} x={x}");
            Assert.AreEqual(expected, f.Derivative, $"seed={Seed + 3} case={i} x={x}");
        }
    }

    [TestMethod]
    public void RingAxiomsAndAbs()
    {
        var gen = new Gen(Seed + 4);
        for (var i = 0; i < 5_000; i++)
        {
            Dual<BigRational> Make() => new(gen.Rational(16), gen.Rational(16));
            var a = Make();
            var b = Make();
            var c = Make();
            var ctx = $"seed={Seed + 4} case={i}";
            Assert.AreEqual(a + b, b + a, ctx);
            Assert.AreEqual(a * b, b * a, ctx);
            Assert.AreEqual((a * b) * c, a * (b * c), ctx);
            Assert.AreEqual(a * (b + c), a * b + a * c, ctx);
            Assert.AreEqual(a, a * Dual<BigRational>.One, ctx);
            Assert.AreEqual(Dual<BigRational>.Zero, a - a, ctx);
            if (b.Value != BigRational.Zero) Assert.AreEqual(a, a / b * b, ctx);
        }

        // ε² = 0.
        var epsilon = new Dual<double>(0, 1);
        Assert.AreEqual(Dual<double>.Zero, epsilon * epsilon);
        Assert.AreEqual(new Dual<double>(3, -2), Dual<double>.Abs(new Dual<double>(-3, 2)));
        Assert.AreEqual(new Dual<double>(3, 2), Dual<double>.Abs(new Dual<double>(3, 2)));
    }

    [TestMethod]
    public void FormatParseAndConversion()
    {
        Assert.AreEqual("(1.5, 2)", new Dual<double>(1.5, 2).ToString());
        Assert.AreEqual(new Dual<double>(1.5, 2), Dual<double>.Parse("(1.5, 2)"));
        Assert.AreEqual(new Dual<double>(4, 0), Dual<double>.Parse("4"));
        Assert.IsFalse(Dual<double>.TryParse("(1.5 2)", null, out _));
        Assert.AreEqual(new Dual<double>(3, 0), Checked<Dual<double>, int>(3));
        Assert.AreEqual(3.0, Checked<double, Dual<double>>(new Dual<double>(3, 0)));
        Assert.Throws<OverflowException>(() => Checked<double, Dual<double>>(new Dual<double>(3, 1)));
        Assert.AreEqual(3.0, Truncating<double, Dual<double>>(new Dual<double>(3, 1)));
        Assert.IsTrue(Dual<double>.IsNaN(new Dual<double>(double.NaN, 0)));
        Assert.IsTrue(Dual<double>.IsFinite(new Dual<double>(1, 2)));
        Assert.IsTrue(Dual<double>.IsZero(default));
    }

    [TestMethod]
    public void GenericAlgorithmsAcceptDual()
    {
        // Differentiate a polynomial through a generic Horner scheme: p(x) = 3x^3 - x + 7, p'(2) = 35.
        static T Horner<T>(T[] coefficients, T x) where T : INumberBase<T>
        {
            var acc = T.Zero;
            foreach (var c in coefficients) acc = acc * x + c;
            return acc;
        }
        var p = Horner([Dual<double>.Constant(3), Dual<double>.Zero, Dual<double>.Constant(-1), Dual<double>.Constant(7)], Dual<double>.Variable(2));
        Assert.AreEqual(29.0, p.Value);
        Assert.AreEqual(35.0, p.Derivative);
    }
}
