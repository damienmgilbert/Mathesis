using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class EvaluationTests
{
    private const int Seed = 20261070;

    private static readonly Symbol X = Symbol("x");
    private static readonly Symbol Y = Symbol("y");
    private static readonly Symbol Z = Symbol("z");

    private static Expr P(string text) => Expr.Parse(text);

    private static Expr E(string text) => Normalizer.Canonical(Expr.Parse(text));

    private static CompiledExpr<T> Compile<T>(Expr e, params Symbol[] parameters)
        where T : struct
    {
        var outcome = e.Compile<T>(parameters);
        Assert.IsInstanceOfType<Outcome<CompiledExpr<T>>.Success>(outcome, $"{e}: {outcome}");
        return ((Outcome<CompiledExpr<T>>.Success)outcome).Value;
    }

    private static Expr Exact(string text, params (Symbol, string)[] bindings)
    {
        var map = bindings.ToDictionary(b => b.Item1, b => P(b.Item2));
        var outcome = Evaluator.Evaluate(P(text), map);
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(outcome, $"{text}: {outcome}");
        return ((Outcome<Expr>.Success)outcome).Value;
    }

    // ----- Compile<double> against an independent evaluator -----

    [TestMethod]
    public void CompiledDoubleAgreesWithTheTreeEvaluator()
    {
        var gen = new Gen(Seed);
        var checkedPoints = 0;
        for (var i = 0; i < 4000; i++)
        {
            var e = gen.RandomExpr(4);
            var code = Compile<double>(e, X, Y, Z);
            for (var k = 0; k < 6; k++)
            {
                double x = gen.Uniform(-3, 3), y = gen.Uniform(-3, 3), z = gen.Uniform(0.1, 4);
                var expected = ExprGen.Evaluate(e, new Dictionary<string, double> { ["x"] = x, ["y"] = y, ["z"] = z });
                var actual = code.Invoke(x, y, z);
                if (!double.IsFinite(expected)) continue;
                checkedPoints++;
                var tolerance = 1e-14 * Math.Max(1, Math.Abs(expected));
                Assert.IsTrue(Math.Abs(expected - actual) <= tolerance, $"{e} at ({x}, {y}, {z}): compiled {actual}, evaluator {expected}");
            }
        }
        Assert.IsTrue(checkedPoints > 5000, $"only {checkedPoints} points had finite values");
    }

    [TestMethod]
    public void CompiledFunctionsMatchTheirDefinitions()
    {
        var cases = new (string Text, Func<double, double> Expected, double Low, double High)[]
        {
            ("cot(x)", x => 1 / Math.Tan(x), 0.1, 3),
            ("sec(x)", x => 1 / Math.Cos(x), -1.4, 1.4),
            ("csc(x)", x => 1 / Math.Sin(x), 0.1, 3),
            ("coth(x)", x => Math.Cosh(x) / Math.Sinh(x), 0.1, 4),
            ("sech(x)", x => 1 / Math.Cosh(x), -4, 4),
            ("csch(x)", x => 1 / Math.Sinh(x), 0.1, 4),
            ("arccot(x)", x => Math.PI / 2 - Math.Atan(x), -5, 5),
            ("arcsec(x)", x => Math.Acos(1 / x), 1.1, 6),
            ("arccsc(x)", x => Math.Asin(1 / x), 1.1, 6),
            ("arsinh(x)", Math.Asinh, -5, 5),
            ("arcosh(x)", Math.Acosh, 1.01, 8),
            ("artanh(x)", Math.Atanh, -0.99, 0.99),
            ("arcoth(x)", x => Math.Atanh(1 / x), 1.1, 6),
            ("arsech(x)", x => Math.Acosh(1 / x), 0.05, 0.99),
            ("arcsch(x)", x => Math.Asinh(1 / x), 0.1, 5),
            ("log(x)", Math.Log10, 0.1, 50),
            ("log(x, 2)", Math.Log2, 0.1, 50),
            ("ln(x)", Math.Log, 0.1, 50),
            ("root(x, 3)", x => Math.Cbrt(x), -50, 50),
            ("x^(2/3)", x => Math.Cbrt(x) * Math.Cbrt(x), -50, 50),
            ("x^(-1/3)", x => 1 / Math.Cbrt(x), 0.1, 50),
            ("x^(3/5)", x => x < 0 ? -Math.Pow(-x, 0.6) : Math.Pow(x, 0.6), -50, 50),
            ("sign(x)", x => Math.Sign(x), -5, 5),
            ("floor(x)", Math.Floor, -5, 5),
            ("ceil(x)", Math.Ceiling, -5, 5),
            ("round(x)", x => Math.Round(x, MidpointRounding.AwayFromZero), -5, 5),
            ("max(x, 1, 2)", x => Math.Max(Math.Max(x, 1), 2), -5, 5),
            ("min(x, 1)", x => Math.Min(x, 1), -5, 5),
            ("atan2(x, 2)", x => Math.Atan2(x, 2), -5, 5),
            ("x^x", x => Math.Pow(x, x), 0.1, 3),
            ("2^x", x => Math.Pow(2, x), -5, 5),
            ("(-x)^3", x => -x * x * x, -5, 5),
        };
        var gen = new Gen(Seed + 1);
        foreach (var (text, expected, low, high) in cases)
        {
            var code = Compile<double>(P(text), X);
            for (var k = 0; k < 50; k++)
            {
                var x = gen.Uniform(low, high);
                var want = expected(x);
                Assert.IsTrue(Math.Abs(want - code.Invoke(x)) <= 1e-13 * Math.Max(1, Math.Abs(want)), $"{text} at {x}");
            }
        }
    }

    [TestMethod]
    public void RealOddRootsAreReal()
    {
        // conv.real-odd-root: (-8)^(1/3) = -2 in real mode.
        Assert.AreEqual(-2.0, Compile<double>(P("x^(1/3)"), X).Invoke(-8), 1e-14);
        Assert.AreEqual(-2.0, Compile<double>(P("root(x, 3)"), X).Invoke(-8), 1e-14);
        Assert.IsTrue(double.IsNaN(Compile<double>(P("x^(1/2)"), X).Invoke(-4)));
        Assert.IsTrue(double.IsNaN(Compile<double>(P("root(x, 2)"), X).Invoke(-4)));
    }

    // ----- Compile<Interval<double>> -----

    [TestMethod]
    public void IntervalEvaluationEncloses10000PointEvaluations()
    {
        var gen = new Gen(Seed + 2);
        var points = 0;
        while (points < 10_000)
        {
            var e = gen.RandomExpr(4);
            var code = Compile<Interval<double>>(e, X, Y, Z);
            double x0 = gen.Uniform(-3, 3), y0 = gen.Uniform(-3, 3), z0 = gen.Uniform(0.1, 4);
            double wx = gen.Random.NextDouble() * 2, wy = gen.Random.NextDouble() * 2, wz = gen.Random.NextDouble() * 2;
            var enclosure = code.Invoke(new Interval<double>(x0, x0 + wx), new Interval<double>(y0, y0 + wy), new Interval<double>(z0, z0 + wz));
            for (var k = 0; k < 8; k++)
            {
                double x = x0 + wx * (k == 0 ? 0 : gen.Random.NextDouble()), y = y0 + wy * gen.Random.NextDouble(), z = z0 + wz * gen.Random.NextDouble();
                var value = ExprGen.Evaluate(e, new Dictionary<string, double> { ["x"] = x, ["y"] = y, ["z"] = z });
                if (!double.IsFinite(value)) continue;
                points++;
                Assert.IsTrue(enclosure.Contains(value), $"{e} over the box at ({x}, {y}, {z}): value {value} is outside {enclosure}");
            }
        }
    }

    [TestMethod]
    public void IntervalEvaluationOfNamedFunctionsEncloses()
    {
        var gen = new Gen(Seed + 3);
        foreach (var text in new[] { "cot(x)", "sec(x)", "arccot(x)", "arsinh(x)", "log(x)", "root(x, 3)", "x^(2/3)", "x^(3/5)", "floor(x)", "max(x, 1)", "2^x", "x^x", "tanh(x)/x", "sin(x)^2 + cos(x)^2" })
        {
            var e = P(text);
            var interval = Compile<Interval<double>>(e, X);
            var plain = Compile<double>(e, X);
            for (var k = 0; k < 500; k++)
            {
                var a = gen.Uniform(-4, 4);
                var b = a + gen.Random.NextDouble() * 1.5;
                var enclosure = interval.Invoke(new Interval<double>(a, b));
                var x = a + gen.Random.NextDouble() * (b - a);
                var v = plain.Invoke(x);
                if (double.IsFinite(v)) Assert.IsTrue(enclosure.Contains(v), $"{text} over [{a}, {b}] = {enclosure} misses {v} at {x}");
            }
        }
    }

    [TestMethod]
    public void IntervalConstantsAreEnclosures()
    {
        var pi = Compile<Interval<double>>(P("pi"));
        Assert.IsTrue(pi.Invoke([]).Contains(Math.PI));
        Assert.IsTrue(pi.Invoke([]).Width > 0);
        var third = Compile<Interval<double>>(P("1/3")).Invoke([]);
        Assert.IsTrue(third.Lower < 1.0 / 3 && third.Upper > 1.0 / 3);
        var half = Compile<Interval<double>>(new Number(BigRational.Create(1, 2))).Invoke([]);
        Assert.IsTrue(half.IsPoint);
        Assert.IsTrue(Compile<Interval<double>>(P("0.1")).Invoke([]).Contains(0.1));
    }

    // ----- Compile<Complex<double>> -----

    [TestMethod]
    public void ComplexEvaluationFollowsPrincipalValues()
    {
        static Complex<double> Value(string text) => Compile<Complex<double>>(Expr.Parse(text)).Invoke([]);

        var euler = Value("exp(I*pi) + 1");
        Assert.IsTrue(euler.Magnitude < 1e-15);
        var root = Value("sqrt(-4)");
        Assert.AreEqual(0.0, root.Real, 1e-14);
        Assert.AreEqual(2.0, root.Imaginary, 1e-14);
        var square = Value("(1 + I)^2");
        Assert.AreEqual(0.0, square.Real, 1e-14);
        Assert.AreEqual(2.0, square.Imaginary, 1e-14);
        var cube = Value("(-8)^(1/3)");
        Assert.AreEqual(1.0, cube.Real, 1e-14);
        Assert.AreEqual(Math.Sqrt(3), cube.Imaginary, 1e-14);
        Assert.AreEqual(Math.Exp(-Math.PI / 2), Value("I^I").Real, 1e-15);
        var log = Value("ln(-1)");
        Assert.AreEqual(0.0, log.Real, 1e-15);
        Assert.AreEqual(Math.PI, log.Imaginary, 1e-15);
        var sin = Value("sin(I)");
        Assert.AreEqual(0.0, sin.Real, 1e-15);
        Assert.AreEqual(Math.Sinh(1), sin.Imaginary, 1e-14);
        Assert.AreEqual(Math.Cosh(1), Value("cos(I)").Real, 1e-14);
        Assert.AreEqual(Math.PI / 2, Value("arsinh(I)").Imaginary, 1e-14);
        Assert.AreEqual(Math.Asinh(1), Value("arcsin(I)").Imaginary, 1e-14);
        Assert.AreEqual(5.0, Value("abs(3 + 4*I)").Real, 1e-14);
    }

    [TestMethod]
    public void ComplexEvaluationOfRealExpressionsMatchesDouble()
    {
        var gen = new Gen(Seed + 4);
        for (var i = 0; i < 1500; i++)
        {
            var e = gen.RandomExpr(3);
            var real = Compile<double>(e, X, Y, Z);
            var complex = Compile<Complex<double>>(e, X, Y, Z);
            double x = gen.Uniform(0.2, 3), y = gen.Uniform(0.2, 3), z = gen.Uniform(0.2, 4);
            var expected = real.Invoke(x, y, z);
            if (!double.IsFinite(expected)) continue;
            var actual = complex.Invoke(new Complex<double>(x), new Complex<double>(y), new Complex<double>(z));
            Assert.IsTrue(Math.Abs(actual.Real - expected) <= 1e-11 * Math.Max(1, Math.Abs(expected)) && Math.Abs(actual.Imaginary) <= 1e-11 * Math.Max(1, Math.Abs(expected)), $"{e}: complex {actual}, real {expected}");
        }
    }

    // ----- Compile<Dual<double>> -----

    [TestMethod]
    public void DualEvaluationGivesDerivatives()
    {
        var cases = new (string Text, Func<double, double> Derivative, double Low, double High)[]
        {
            ("sin(x)*exp(x)", x => Math.Exp(x) * (Math.Sin(x) + Math.Cos(x)), -3, 3),
            ("x^3 - 2*x", x => 3 * x * x - 2, -3, 3),
            ("sqrt(x)/(1 + x^2)", x => (1 + x * x) / (2 * Math.Sqrt(x)) / ((1 + x * x) * (1 + x * x)) - 2 * x * Math.Sqrt(x) / ((1 + x * x) * (1 + x * x)), 0.1, 5),
            ("ln(x)/x", x => (1 - Math.Log(x)) / (x * x), 0.1, 5),
            ("arctan(x)", x => 1 / (1 + x * x), -5, 5),
            ("tanh(x)^2", x => 2 * Math.Tanh(x) / (Math.Cosh(x) * Math.Cosh(x)), -3, 3),
            ("cot(x)", x => -1 / (Math.Sin(x) * Math.Sin(x)), 0.2, 2.9),
            ("sec(x)", x => Math.Sin(x) / (Math.Cos(x) * Math.Cos(x)), -1.4, 1.4),
            ("arccot(x)", x => -1 / (1 + x * x), -5, 5),
            ("log(x)", x => 1 / (x * Math.Log(10)), 0.1, 5),
            ("log(x, 2)", x => 1 / (x * Math.Log(2)), 0.1, 5),
            ("x^x", x => Math.Pow(x, x) * (Math.Log(x) + 1), 0.1, 3),
            ("2^x", x => Math.Pow(2, x) * Math.Log(2), -3, 3),
            ("abs(x)", x => Math.Sign(x), 0.1, 5),
            ("x^(2/3)", x => 2.0 / 3 * Math.Pow(Math.Abs(x), -1.0 / 3) * Math.Sign(x), 0.5, 20),
            ("x^(1/3)", x => 1.0 / 3 * Math.Pow(Math.Abs(x), -2.0 / 3), 0.5, 20),
            ("arcsin(x/2)", x => 1 / Math.Sqrt(4 - x * x), -1.9, 1.9),
            ("x^5", x => 5 * Math.Pow(x, 4), -3, 3),
            ("x^(-2)", x => -2 * Math.Pow(x, -3), 0.2, 3),
        };
        var gen = new Gen(Seed + 5);
        foreach (var (text, derivative, low, high) in cases)
        {
            var code = Compile<Dual<double>>(P(text), X);
            for (var k = 0; k < 30; k++)
            {
                var x = gen.Uniform(low, high);
                var result = code.Invoke(Dual<double>.Variable(x));
                var want = derivative(x);
                Assert.IsTrue(Math.Abs(result.Derivative - want) <= 1e-12 * Math.Max(1, Math.Abs(want)), $"d/dx {text} at {x}: dual {result.Derivative}, analytic {want}");
            }
        }

        // The real odd root of a negative number: d/dx x^(1/3) at -27 is 1/27, and x^(2/3) has derivative (2/3)·x^(-1/3) = -2/9 there.
        Assert.AreEqual(1.0 / 27, Compile<Dual<double>>(P("x^(1/3)"), X).Invoke(Dual<double>.Variable(-27)).Derivative, 1e-14);
        Assert.AreEqual(-2.0 / 9, Compile<Dual<double>>(P("x^(2/3)"), X).Invoke(Dual<double>.Variable(-27)).Derivative, 1e-14);
        Assert.AreEqual(-3.0, Compile<Dual<double>>(P("x^(1/3)"), X).Invoke(Dual<double>.Variable(-27)).Value, 1e-14);
    }

    // ----- Compilation errors and API misuse -----

    [TestMethod]
    public void CompilationFailuresAreOutcomesNotExceptions()
    {
        var free = P("x + y").Compile<double>(X);
        Assert.IsInstanceOfType<Outcome<CompiledExpr<double>>.Failed>(free);
        Assert.AreEqual(MathErrorKind.Unsupported, ((Outcome<CompiledExpr<double>>.Failed)free).Error.Kind);

        Assert.IsInstanceOfType<Outcome<CompiledExpr<double>>.Failed>(P("gamma(x)").Compile<double>(X));
        Assert.IsInstanceOfType<Outcome<CompiledExpr<double>>.Failed>(P("I*x").Compile<double>(X));
        Assert.IsInstanceOfType<Outcome<CompiledExpr<Interval<double>>>.Failed>(P("atan2(x, 2)").Compile<Interval<double>>(X));
        Assert.IsInstanceOfType<Outcome<CompiledExpr<Complex<double>>>.Failed>(P("floor(x)").Compile<Complex<double>>(X));
        Assert.IsInstanceOfType<Outcome<CompiledExpr<int>>.Failed>(P("x + 1").Compile<int>(X));
        Assert.IsInstanceOfType<Outcome<CompiledExpr<double>>.Failed>(P("{1, 2}").Compile<double>());
    }

    [TestMethod]
    public void MisuseOfTheCompilerApiThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(() => P("x").Compile<double>(X, X));
        Assert.ThrowsExactly<ArgumentNullException>(() => ((Expr)null!).Compile<double>());
        var code = Compile<double>(P("x + y"), X, Y);
        Assert.ThrowsExactly<ArgumentException>(() => code.Invoke(1.0));
        Assert.AreEqual(3.0, code.Invoke(1.0, 2.0));
        Assert.AreEqual(2, code.Parameters.Length);
        Assert.IsTrue(code.InstructionCount >= 3);
        Assert.ThrowsExactly<ArgumentException>(() => code.InvokeMany([1.0], new double[1]));
    }

    [TestMethod]
    public void InvokeManyEvaluatesElementwise()
    {
        var code = Compile<double>(P("x^2 + 1"), X);
        var input = new[] { 0.0, 1.0, 2.0, 3.0 };
        var output = new double[4];
        code.InvokeMany(input, output);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 5.0, 10.0 }, output);
        Assert.ThrowsExactly<ArgumentException>(() => code.InvokeMany(input, new double[3]));
    }

    [TestMethod]
    public void ConstantsAndNoParameters()
    {
        Assert.AreEqual(Math.PI, Compile<double>(P("pi")).Invoke([]), 0);
        Assert.AreEqual(Math.E, Compile<double>(P("e")).Invoke([]), 0);
        Assert.AreEqual((1 + Math.Sqrt(5)) / 2, Compile<double>(new Constant(ConstantId.GoldenRatio)).Invoke([]), 1e-15);
        Assert.IsTrue(double.IsPositiveInfinity(Compile<double>(P("oo")).Invoke([])));
        Assert.AreEqual(0.1, Compile<double>(P("0.1")).Invoke([]), 0);
    }

    // ----- N -----

    [TestMethod]
    public void NEvaluatesToDouble()
    {
        Assert.AreEqual(Math.PI, ((Outcome<double>.Success)Evaluator.N(P("pi"))).Value);
        Assert.AreEqual(3.1416, ((Outcome<double>.Success)Evaluator.N(P("pi"), digits: 5)).Value);
        Assert.AreEqual(2.0 / 3, ((Outcome<double>.Success)Evaluator.N(P("2/3"))).Value, 0);
        Assert.AreEqual(7.0, ((Outcome<double>.Success)Evaluator.N(P("x^2 + y"), new Dictionary<Symbol, double> { [X] = 2, [Y] = 3 })).Value);

        Assert.IsInstanceOfType<Outcome<double>.Unevaluated>(Evaluator.N(P("x + 1")));
        Assert.IsInstanceOfType<Outcome<double>.Unevaluated>(Evaluator.N(P("pi"), digits: 30));
        var domain = Evaluator.N(P("sqrt(-1)"));
        Assert.IsInstanceOfType<Outcome<double>.Failed>(domain);
        Assert.AreEqual(MathErrorKind.Domain, ((Outcome<double>.Failed)domain).Error.Kind);
        Assert.IsInstanceOfType<Outcome<double>.Failed>(Evaluator.N(P("gamma(2)")));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Evaluator.N(P("1"), digits: 0));
    }

    [TestMethod]
    public void NComplexEvaluatesToComplex()
    {
        var value = ((Outcome<Complex<double>>.Success)Evaluator.NComplex(P("sqrt(-4) + x"), new Dictionary<Symbol, Complex<double>> { [X] = new(1, 1) })).Value;
        Assert.AreEqual(1.0, value.Real, 1e-14);
        Assert.AreEqual(3.0, value.Imaginary, 1e-14);
        Assert.IsInstanceOfType<Outcome<Complex<double>>.Unevaluated>(Evaluator.NComplex(P("x")));
        Assert.IsInstanceOfType<Outcome<Complex<double>>.Failed>(Evaluator.NComplex(P("1/0")));
    }

    // ----- Exact evaluation -----

    [TestMethod]
    public void ExactEvaluationKeepsRationalsAndConstantsExact()
    {
        Assert.AreEqual(E("5/6"), Exact("2/3 + 1/6"));
        Assert.AreEqual(E("1/9"), Exact("x^2", (X, "1/3")));
        Assert.AreEqual(E("2*pi"), Exact("pi + pi"));
        Assert.AreEqual(E("5/2"), Exact("x + y", (X, "1/2"), (Y, "2")));
        Assert.AreEqual(E("1"), Exact("sin(x)^2 + cos(x)^2 - sin(x)^2 - cos(x)^2 + 1"));
    }

    [TestMethod]
    public void ExactEvaluationComputesIntegerFunctions()
    {
        Assert.AreEqual(E("3628800"), Exact("10!"));
        Assert.AreEqual(E("120"), Exact("binomial(10, 3)"));
        Assert.AreEqual(E("1"), Exact("binomial(7, 0)"));
        Assert.AreEqual(E("0"), Exact("binomial(3, 5)"));
        Assert.AreEqual(E("10"), Exact("binomial(5, 2)"));
        Assert.AreEqual(E("-4"), Exact("binomial(-2, 3)"));
        Assert.AreEqual(E("6"), Exact("gcd(12, 18)"));
        Assert.AreEqual(E("36"), Exact("lcm(12, 18)"));
        Assert.AreEqual(E("1"), Exact("7 mod 3"));
        Assert.AreEqual(E("2"), Exact("-7 mod 3"));
        Assert.AreEqual(E("-2"), Exact("7 mod -3"));
        Assert.AreEqual(E("2"), Exact("quo(7, 3)"));
        Assert.AreEqual(E("-3"), Exact("quo(-7, 3)"));
        Assert.AreEqual(E("3"), Exact("floor(7/2)"));
        Assert.AreEqual(E("-4"), Exact("floor(-7/2)"));
        Assert.AreEqual(E("4"), Exact("ceil(7/2)"));
        Assert.AreEqual(E("-3"), Exact("ceil(-7/2)"));
        Assert.AreEqual(E("3"), Exact("round(5/2)"));
        Assert.AreEqual(E("-3"), Exact("round(-5/2)"));
        Assert.AreEqual(E("2"), Exact("round(7/4)"));
        Assert.AreEqual(E("1/2"), Exact("frac(7/2)"));
        Assert.AreEqual(E("1/2"), Exact("frac(-5/2)"));
        Assert.AreEqual(E("3/4"), Exact("abs(-3/4)"));
        Assert.AreEqual(E("-1"), Exact("sign(-5)"));
        Assert.AreEqual(E("7"), Exact("max(3, 7, 5)"));
        Assert.AreEqual(E("3"), Exact("min(3, 7, 5)"));
        Assert.AreEqual(E("15"), Exact("5!!"));
        Assert.AreEqual(E("48"), Exact("6!!"));
        Assert.AreEqual(E("60"), Exact("perm(5, 3)"));
        Assert.AreEqual(E("720"), Exact("x!", (X, "6")));
    }

    [TestMethod]
    public void ExactEvaluationLeavesWhatItCannotCompute()
    {
        Assert.AreEqual(E("factorial(x)").ToString(), Exact("x!").ToString());
        Assert.AreEqual(E("(1/2)!").ToString(), Exact("(1/2)!").ToString());

        // A function of a Float is computed in double precision; decimal literals are exact numbers, not Floats.
        var floated = Evaluator.Evaluate(Sin(new Float(0.5)));
        Assert.AreEqual(Math.Sin(0.5), ((Float)((Outcome<Expr>.Success)floated).Value).Value, 1e-16);
        Assert.IsNotInstanceOfType<Float>(Exact("sin(0.5)"));
        Assert.AreEqual(5, ((Number)Exact("2 + 3")).Value.Numerator);
    }

    [TestMethod]
    public void ExactEvaluationHonorsTheBudget()
    {
        var huge = Evaluator.Evaluate(P("100000!"));
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(huge);
        Assert.IsFalse(((Outcome<Expr>.Success)huge).Value is Number, "a factorial beyond the exact limit must stay symbolic");

        var limited = Evaluator.Evaluate(P("1000! + 999!"), budget: new Budget(maxSteps: 10));
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(limited);
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(Evaluator.Evaluate(P("5!"), budget: new Budget(maxSteps: 100)));
    }

    // ----- Zero testing -----

    [TestMethod]
    public void ZeroTestIsExactForRationalFunctions()
    {
        foreach (var text in new[] { "x - x", "(x + 1)^2 - x^2 - 2*x - 1", "(x^2 - 1)/(x - 1) - x - 1", "(1 + I)^2 - 2*I", "1/x - x^(-1)", "sin(x) - sin(x)", "(x + y)^3 - x^3 - 3*x^2*y - 3*x*y^2 - y^3", "1/(x*(x + 1)) - 1/x + 1/(x + 1)", "(1 + I)*(1 - I) - 2", "I^2 + 1", "I^4 - 1" })
        {
            var report = ZeroTest.Analyze(P(text));
            Assert.AreEqual(ZeroTestResult.Zero, report.Result, text);
            Assert.AreEqual("exact", report.Method, text);
        }
        foreach (var text in new[] { "x - 1", "x^2 + 1", "1 + I", "(x + 1)^2 - x^2", "1/x - 1/(x + 1)", "x*y - x", "I - 1" })
        {
            var report = ZeroTest.Analyze(P(text));
            Assert.AreEqual(ZeroTestResult.NonZero, report.Result, text);
            Assert.AreEqual("exact", report.Method, text);
        }
    }

    [TestMethod]
    public void ZeroTestUsesIntervalsForCertainNonZero()
    {
        foreach (var text in new[] { "sin(x) - x", "exp(x) - 1", "pi - 3.14159", "sqrt(2) - 1.4142135", "ln(2) - 0.69", "cos(x) - 2" })
        {
            var report = ZeroTest.Analyze(P(text));
            Assert.AreEqual(ZeroTestResult.NonZero, report.Result, text);
            Assert.AreEqual("interval", report.Method, text);
        }
    }

    [TestMethod]
    public void ZeroTestSamplesTranscendentalIdentities()
    {
        foreach (var text in new[] { "sin(x)^2 + cos(x)^2 - 1", "exp(x)*exp(-x) - 1", "sqrt(2)*sqrt(2) - 2", "ln(8) - 3*ln(2)", "cos(2*x) - 2*cos(x)^2 + 1", "sin(x + y) - sin(x)*cos(y) - cos(x)*sin(y)", "exp(I*pi) + 1", "cosh(x)^2 - sinh(x)^2 - 1", "arctan(x) + arctan(1/x) - pi/2*sign(x)" })
        {
            var report = ZeroTest.Analyze(P(text));
            Assert.AreEqual(ZeroTestResult.ProbablyZero, report.Result, $"{text}: {report}");
            Assert.AreEqual("sampling", report.Method);
            Assert.IsTrue(report.Samples >= 8);
        }
    }

    [TestMethod]
    public void ZeroTestRespectsAssumptions()
    {
        var expr = P("sqrt(x^2) - x");
        Assert.AreEqual(ZeroTestResult.NonZero, ZeroTest.Test(expr));
        Assert.AreEqual(ZeroTestResult.ProbablyZero, ZeroTest.Test(expr, MathContext.Default.Assume(P("x > 0"))));
        Assert.AreEqual(ZeroTestResult.ProbablyZero, ZeroTest.Test(P("sqrt((x - y)^2) - (x - y)"), MathContext.Default.Assume(P("x > y"))));
        Assert.AreEqual(ZeroTestResult.NonZero, ZeroTest.Test(P("sqrt((x - y)^2) - (x - y)"), MathContext.Default.Assume(P("x < y"))));
    }

    [TestMethod]
    public void ZeroTestReportsUnknownWhenNothingIsDefined()
    {
        Assert.AreEqual(ZeroTestResult.Unknown, ZeroTest.Test(P("ln(-1 - x^2)")));
        Assert.AreEqual(ZeroTestResult.Unknown, ZeroTest.Test(P("ln(-1 - x^2) + 1")));
        Assert.AreEqual(ZeroTestResult.Unknown, ZeroTest.Test(P("1/0")));
    }

    [TestMethod]
    public void ZeroTestIsDeterministicForASeed()
    {
        var a = ZeroTest.Analyze(P("sin(x)^2 + cos(x)^2 - 1"), seed: 7);
        var b = ZeroTest.Analyze(P("sin(x)^2 + cos(x)^2 - 1"), seed: 7);
        Assert.AreEqual(a, b);
        Assert.ThrowsExactly<ArgumentNullException>(() => ZeroTest.Test(null!));
    }

    [TestMethod]
    public void ZeroTestHonorsTheBudget()
    {
        var report = ZeroTest.Analyze(P("sin(x)^2 + cos(x)^2 - 1"), budget: new Budget(maxSteps: 2));
        Assert.AreNotEqual(ZeroTestResult.ProbablyZero, report.Result);
    }
}
