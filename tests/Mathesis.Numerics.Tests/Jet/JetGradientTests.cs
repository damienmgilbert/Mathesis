using Mathesis.Numbers;
using System.Numerics;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// Gradients (ADR-19): 24 elementary functions match analytic derivatives (1e−14 relative for <c>double</c>, 1e−5 for
/// <c>float</c>), and random expression trees of depth at most 6 match the N-pass <c>Dual&lt;double&gt;</c> Jacobian (1e−14) and Richardson
/// central differences (1e−8). Each elementary function is written once, generic over the number type, and evaluated on the jet; the analytic
/// derivative is written separately, in a different algebraic form where one exists, on the plain scalar.
/// </summary>
[TestClass]
public class JetGradientTests
{
    private static readonly string[] UnaryNames =
    [
        "exp", "exp2", "exp10", "expm1", "log", "log2", "log10", "log1p", "sqrt", "cbrt", "sin", "cos",
        "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh", "asinh", "acosh", "atanh",
    ];

    // Domains chosen away from singularities and from points where the derivative is tiny against its terms (acosh near 1, cosh near 0).
    private static readonly (double Low, double High)[] Domains =
    [
        (-2, 2), (-2, 2), (-2, 2), (-2, 2), (0.2, 5), (0.2, 5), (0.2, 5), (-0.7, 4), (0.2, 5), (0.2, 5), (-3, 3), (-3, 3),
        (-1.3, 1.3), (-0.9, 0.9), (-0.9, 0.9), (-5, 5), (-2, 2), (0.3, 2.5), (-2, 2), (-3, 3), (1.3, 5), (-0.8, 0.8),
    ];

    private static TNum Function<TNum>(int index, TNum x) where TNum : IFloatingPointIeee754<TNum> => index switch
    {
        0 => TNum.Exp(x),
        1 => TNum.Exp2(x),
        2 => TNum.Exp10(x),
        3 => TNum.ExpM1(x),
        4 => TNum.Log(x),
        5 => TNum.Log2(x),
        6 => TNum.Log10(x),
        7 => TNum.LogP1(x),
        8 => TNum.Sqrt(x),
        9 => TNum.Cbrt(x),
        10 => TNum.Sin(x),
        11 => TNum.Cos(x),
        12 => TNum.Tan(x),
        13 => TNum.Asin(x),
        14 => TNum.Acos(x),
        15 => TNum.Atan(x),
        16 => TNum.Sinh(x),
        17 => TNum.Cosh(x),
        18 => TNum.Tanh(x),
        19 => TNum.Asinh(x),
        20 => TNum.Acosh(x),
        21 => TNum.Atanh(x),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <summary>The analytic derivative, in a form independent of the implementation's where possible.</summary>
    private static TNum Derivative<TNum>(int index, TNum x) where TNum : IFloatingPointIeee754<TNum>
    {
        var one = TNum.One;
        var two = TNum.CreateChecked(2);
        var three = TNum.CreateChecked(3);
        return index switch
        {
            0 => TNum.Exp(x),
            1 => TNum.Pow(two, x) * TNum.Log(two),
            2 => TNum.Pow(TNum.CreateChecked(10), x) * TNum.Log(TNum.CreateChecked(10)),
            3 => TNum.Exp(x),
            4 => one / x,
            5 => one / (x * TNum.Log(two)),
            6 => one / (x * TNum.Log(TNum.CreateChecked(10))),
            7 => one / (one + x),
            8 => TNum.CreateChecked(0.5) / TNum.Sqrt(x),
            9 => TNum.Pow(x, -two / three) / three,
            10 => TNum.Cos(x),
            11 => -TNum.Sin(x),
            12 => one / (TNum.Cos(x) * TNum.Cos(x)),
            13 => one / TNum.Sqrt(one - x * x),
            14 => -one / TNum.Sqrt(one - x * x),
            15 => one / (one + x * x),
            16 => TNum.Cosh(x),
            17 => TNum.Sinh(x),
            18 => one / (TNum.Cosh(x) * TNum.Cosh(x)),
            19 => one / TNum.Sqrt(x * x + one),
            20 => one / TNum.Sqrt(x * x - one),
            21 => one / (one - x * x),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    private static double RelativeError<TNum>(TNum actual, TNum expected) where TNum : IFloatingPointIeee754<TNum> =>
        double.CreateChecked(TNum.Abs(actual - expected)) / double.CreateChecked(TNum.Abs(expected));

    private static void CheckUnary<TJet, TNum>(Gen gen, Func<TNum, int, int, TJet> variable, Func<TJet, TNum> value, Func<TJet, int, TNum> lane, Func<int, TJet, TJet> apply, double tolerance)
        where TNum : IFloatingPointIeee754<TNum>
        where TJet : IFloatingPointIeee754<TJet>
    {
        for (var f = 0; f < UnaryNames.Length; f++)
        {
            var (low, high) = Domains[f];
            for (var s = 0; s < 200; s++)
            {
                var x = TNum.CreateChecked(gen.Uniform(low, high));
                var k = gen.WholeNumber(0, 2);
                var result = apply(f, variable(x, k, 3));
                var expected = Function(f, x);
                var slope = Derivative(f, x);
                var context = gen.Describe($"{UnaryNames[f]}({x}) variable {k} of 3");
                Assert.AreEqual(double.CreateChecked(expected), double.CreateChecked(value(result)), context + " (value)");
                for (var lanes = 0; lanes < 3; lanes++)
                {
                    if (lanes == k) Assert.IsLessThanOrEqualTo(tolerance, RelativeError(lane(result, lanes), slope), context + $" (gradient lane {lanes}: {lane(result, lanes)} against {slope})");
                    else Assert.AreEqual(TNum.Zero, lane(result, lanes), context + $" (lane {lanes} must be zero)");
                }
            }
        }
    }

    [TestMethod]
    public void TwentyTwoUnaryFunctionsMatchAnalyticDerivativesForDouble() =>
        CheckUnary<JetD, double>(new Gen(4101), (x, k, n) => JetD.Variable(x, k, n), j => j.Value, (j, i) => j.Gradient[i], Function, 1e-14);

    [TestMethod]
    public void TwentyTwoUnaryFunctionsMatchAnalyticDerivativesForFloat() =>
        CheckUnary<JetF, float>(new Gen(4102), (x, k, n) => JetF.Variable(x, k, n), j => j.Value, (j, i) => j.Gradient[i], Function, 1e-5);

    private static void CheckBinary<TJet, TNum>(Gen gen, Func<TNum, int, int, TJet> variable, Func<TJet, int, TNum> lane, double tolerance)
        where TNum : IFloatingPointIeee754<TNum>
        where TJet : IFloatingPointIeee754<TJet>
    {
        for (var s = 0; s < 400; s++)
        {
            // pow(a, b) = a^b in both arguments.
            var a = TNum.CreateChecked(gen.Uniform(0.3, 3.0));
            var b = TNum.CreateChecked(gen.Uniform(-2.0, 2.5));
            var p = TJet.Pow(variable(a, 0, 2), variable(b, 1, 2));
            var context = gen.Describe($"pow({a}, {b})");
            Assert.IsLessThanOrEqualTo(tolerance, RelativeError(lane(p, 0), b * TNum.Pow(a, b - TNum.One)), context + " (d/da)");
            Assert.IsLessThanOrEqualTo(tolerance, RelativeError(lane(p, 1), TNum.Pow(a, b) * TNum.Log(a)), context + " (d/db)");

            // atan2(y, x) = atan(y/x) for x > 0, so d/dy = 1/(x(1 + (y/x)²)) and d/dx = −y/(x²(1 + (y/x)²)); x is kept away from 0.
            var x = TNum.CreateChecked(gen.Uniform(0.3, 3.0) * (gen.WholeNumber(0, 1) == 0 ? 1 : -1));
            var y = TNum.CreateChecked(gen.Uniform(0.3, 3.0) * (gen.WholeNumber(0, 1) == 0 ? 1 : -1));
            var angle = TJet.Atan2(variable(y, 0, 2), variable(x, 1, 2));
            var ratio = y / x;
            var denominator = TNum.One + ratio * ratio;
            context = gen.Describe($"atan2({y}, {x})");
            Assert.IsLessThanOrEqualTo(tolerance, RelativeError(lane(angle, 0), TNum.One / (x * denominator)), context + " (d/dy)");
            Assert.IsLessThanOrEqualTo(tolerance, RelativeError(lane(angle, 1), -y / (x * x * denominator)), context + " (d/dx)");
        }
    }

    [TestMethod]
    public void PowAndAtan2MatchAnalyticDerivativesInBothArgumentsForDouble() =>
        CheckBinary<JetD, double>(new Gen(4103), (x, k, n) => JetD.Variable(x, k, n), (j, i) => j.Gradient[i], 1e-14);

    [TestMethod]
    public void PowAndAtan2MatchAnalyticDerivativesInBothArgumentsForFloat() =>
        CheckBinary<JetF, float>(new Gen(4104), (x, k, n) => JetF.Variable(x, k, n), (j, i) => j.Gradient[i], 1e-5);

    // ----- Random expression trees -----

    /// <summary>A node of a random expression over the inputs; the generator keeps every operation inside its smooth domain.</summary>
    private sealed record Node(int Operation, Node? Left, Node? Right, int Input, double Constant);

    private const int Inputs = 4;

    private static Node RandomTree(Gen gen, int depth)
    {
        if (depth == 0 || gen.WholeNumber(0, 5) == 0)
            return gen.WholeNumber(0, 3) == 0 ? new Node(0, null, null, -1, gen.Uniform(-2, 2)) : new Node(1, null, null, gen.WholeNumber(0, Inputs - 1), 0);
        var op = gen.WholeNumber(2, 13);
        var left = RandomTree(gen, depth - 1);
        var right = op <= 6 ? RandomTree(gen, depth - 1) : null;
        return new Node(op, left, right, -1, op == 13 ? gen.Uniform(0.5, 1.5) : 0);
    }

    // Operations: 0 constant, 1 input, 2 +, 3 −, 4 ×, 5 / (b² + 1), 6 atan2, 7 sin, 8 cos, 9 exp(sin(a)), 10 √(a² + 1), 11 log(a² + 1), 12 −a, 13 a^c (a² + 1 base).
    private static T Evaluate<T>(Node node, ReadOnlySpan<T> x) where T : struct, ISpikeScalar<T>
    {
        var one = T.Constant(1.0);
        switch (node.Operation)
        {
            case 0: return T.Constant(node.Constant);
            case 1: return x[node.Input];
            case 2: return Evaluate(node.Left!, x) + Evaluate(node.Right!, x);
            case 3: return Evaluate(node.Left!, x) - Evaluate(node.Right!, x);
            case 4: return Evaluate(node.Left!, x) * Evaluate(node.Right!, x);
            case 5: { var b = Evaluate(node.Right!, x); return Evaluate(node.Left!, x) / (b * b + one); }
            case 6: return T.Atan2(Evaluate(node.Left!, x), Evaluate(node.Right!, x) * Evaluate(node.Right!, x) + one);
            case 7: return T.Sin(Evaluate(node.Left!, x));
            case 8: return T.Cos(Evaluate(node.Left!, x));
            case 9: return T.Exp(T.Sin(Evaluate(node.Left!, x)));
            case 10: { var a = Evaluate(node.Left!, x); return T.Sqrt(a * a + one); }
            case 11: { var a = Evaluate(node.Left!, x); return T.Log(a * a + one); }
            case 12: return -Evaluate(node.Left!, x);
            default: { var a = Evaluate(node.Left!, x); return T.Pow(a * a + one, node.Constant); }
        }
    }

    private static JetD EvaluateJet(Node node, ReadOnlySpan<JetD> x)
    {
        var one = JetD.One;
        switch (node.Operation)
        {
            case 0: return JetD.Constant(node.Constant);
            case 1: return x[node.Input];
            case 2: return EvaluateJet(node.Left!, x) + EvaluateJet(node.Right!, x);
            case 3: return EvaluateJet(node.Left!, x) - EvaluateJet(node.Right!, x);
            case 4: return EvaluateJet(node.Left!, x) * EvaluateJet(node.Right!, x);
            case 5: { var b = EvaluateJet(node.Right!, x); return EvaluateJet(node.Left!, x) / (b * b + one); }
            case 6: { var b = EvaluateJet(node.Right!, x); return JetD.Atan2(EvaluateJet(node.Left!, x), b * b + one); }
            case 7: return JetD.Sin(EvaluateJet(node.Left!, x));
            case 8: return JetD.Cos(EvaluateJet(node.Left!, x));
            case 9: return JetD.Exp(JetD.Sin(EvaluateJet(node.Left!, x)));
            case 10: { var a = EvaluateJet(node.Left!, x); return JetD.Sqrt(a * a + one); }
            case 11: { var a = EvaluateJet(node.Left!, x); return JetD.Log(a * a + one); }
            case 12: return -EvaluateJet(node.Left!, x);
            default: { var a = EvaluateJet(node.Left!, x); return JetD.Pow(a * a + one, JetD.Constant(node.Constant)); }
        }
    }

    /// <summary>The Richardson central-difference gradient (steps 0.01 halved three times: the trees compound up to six levels of curvature, so the first step of 0.05 is too coarse for 1e−8) of a tree on plain doubles.</summary>
    private static double[] RichardsonGradient(Node tree, double[] x)
    {
        const int Levels = 4;
        var gradient = new double[x.Length];
        var table = new double[Levels];
        for (var j = 0; j < x.Length; j++)
        {
            var step = 0.01;
            for (var level = 0; level < Levels; level++, step *= 0.5)
            {
                var plus = new PlainDouble[x.Length];
                var minus = new PlainDouble[x.Length];
                for (var i = 0; i < x.Length; i++)
                {
                    plus[i] = new PlainDouble(i == j ? x[i] + step : x[i]);
                    minus[i] = new PlainDouble(i == j ? x[i] - step : x[i]);
                }
                table[level] = (Evaluate<PlainDouble>(tree, plus).Value - Evaluate<PlainDouble>(tree, minus).Value) / (2.0 * step);
            }
            var factor = 1.0;
            for (var k = 1; k < Levels; k++)
            {
                factor *= 4.0;
                for (var level = Levels - 1; level >= k; level--) table[level] = (factor * table[level] - table[level - 1]) / (factor - 1.0);
            }
            gradient[j] = table[Levels - 1];
        }
        return gradient;
    }

    [TestMethod]
    public void RandomExpressionTreesMatchDualPassesAndRichardsonDifferences()
    {
        var gen = new Gen(4105);
        double worstDual = 0.0, worstDifference = 0.0;
        for (var t = 0; t < 300; t++)
        {
            var tree = RandomTree(gen, 6);
            var point = new double[Inputs];
            for (var i = 0; i < Inputs; i++) point[i] = gen.Uniform(-1.5, 1.5);

            var jets = JetD.Variables(point);
            var result = EvaluateJet(tree, jets);
            var gradient = result.IsConstant ? new double[Inputs] : result.Gradient.ToArray();

            // N passes of Dual<double>.
            var dual = new double[Inputs];
            for (var j = 0; j < Inputs; j++)
            {
                var duals = new MathesisDualScalar[Inputs];
                for (var i = 0; i < Inputs; i++) duals[i] = new MathesisDualScalar(point[i], i == j ? 1.0 : 0.0);
                var d = Evaluate<MathesisDualScalar>(tree, duals);
                dual[j] = d.Derivative;
                Assert.AreEqual(d.Value, result.Value, 1e-12 * Math.Max(1.0, Math.Abs(d.Value)), gen.Describe($"tree {t}: value"));
            }
            var difference = RichardsonGradient(tree, point);

            var context = gen.Describe($"tree {t}, point [{string.Join(", ", point)}]");
            var errorDual = Compare.ElementRelative(gradient, dual);
            var errorDifference = Compare.MatrixRelative(gradient, difference);
            Assert.IsLessThanOrEqualTo(1e-14, errorDual, context + $": jet {string.Join(", ", gradient)} against Dual {string.Join(", ", dual)}");
            Assert.IsLessThanOrEqualTo(1e-8, errorDifference, context + $": jet {string.Join(", ", gradient)} against Richardson {string.Join(", ", difference)}");
            worstDual = Math.Max(worstDual, errorDual);
            worstDifference = Math.Max(worstDifference, errorDifference);
        }
        TestContext.WriteLine($"worst against Dual N-pass {worstDual:E2}, worst against Richardson {worstDifference:E2}");
    }

    public TestContext TestContext { get; set; } = null!;
}
