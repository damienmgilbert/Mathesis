using Mathesis.Numbers;
using Mathesis.Numerics;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

[TestClass]
public class RootsTests
{
    private const int Seed = 20261002;

    // Standard test functions with brackets and reference roots (computed to full double precision).
    private static readonly (string Name, Func<double, double> F, double A, double B, double Root)[] Standard =
    [
        ("x^3 - 2x - 5", x => x * x * x - 2 * x - 5, 2, 3, 2.0945514815423265),
        ("cos x - x", x => Math.Cos(x) - x, 0, 1, 0.7390851332151607),
        ("exp x - 2", x => Math.Exp(x) - 2, 0, 1, 0.6931471805599453),
        ("x^2 - 2", x => x * x - 2, 1, 2, 1.4142135623730951),
        ("sin x", Math.Sin, 3, 4, Math.PI),
        ("ln x - 1", x => Math.Log(x) - 1, 1, 5, Math.E),
        ("x exp x - 1", x => x * Math.Exp(x) - 1, 0, 1, 0.5671432904097838),
        ("1/x - 2", x => 1 / x - 2, 0.1, 1, 0.5),
        ("sin x - x/2", x => Math.Sin(x) - x / 2, Math.PI / 2, Math.PI, 1.8954942670339809),
        ("tanh x - 1/2", x => Math.Tanh(x) - 0.5, 0, 2, 0.5493061443340548),
        ("x^19 - 1/2", x => Math.Pow(x, 19) - 0.5, 0, 1, Math.Pow(0.5, 1.0 / 19)),
        ("exp(-x) - x^2", x => Math.Exp(-x) - x * x, 0, 2, 0.7034674224983917),
        ("atan x - 1", x => Math.Atan(x) - 1, 0, 5, 1.5574077246549023),
    ];

    [TestMethod]
    public void BrentSolvesTheStandardFunctionsWithinFiftyIterations()
    {
        foreach (var (name, f, a, b, root) in Standard)
        {
            var result = Roots.Brent(f, a, b);
            Assert.IsTrue(result.Converged, $"{name}: {result}");
            Assert.IsTrue(result.Iterations <= 50, $"{name}: {result.Iterations} iterations");
            Assert.AreEqual(root, result.Root, 1e-14 * Math.Max(1, Math.Abs(root)), name);
            Assert.AreEqual(f(result.Root), result.FunctionValue, name);
            Assert.IsTrue(result.Evaluations >= result.Iterations, name);

            // Brent needs no more evaluations than plain bisection to the same tolerance.
            var bisection = Roots.Bisection(f, a, b, new StoppingCriteria(RelativeTolerance: 1e-14, MaxIterations: 200));
            Assert.IsTrue(result.Iterations <= bisection.Iterations, $"{name}: Brent {result.Iterations} vs bisection {bisection.Iterations}");
        }
    }

    [TestMethod]
    public void BrentOnSeededRandomCubics()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 2_000; i++)
        {
            double[] roots = [gen.Uniform(-10, -2), gen.Uniform(-1, 1), gen.Uniform(2, 10)];
            var scale = gen.Uniform(0.5, 20);
            double F(double x) => scale * (x - roots[0]) * (x - roots[1]) * (x - roots[2]);

            var target = roots[gen.Random.Next(3)];
            var nearest = roots.Where(r => r != target).Min(r => Math.Abs(r - target));
            var reach = Math.Min(1.9, 0.9 * nearest);
            var result = Roots.Brent(F, target - gen.Uniform(0.05 * reach, reach), target + gen.Uniform(0.05 * reach, reach));
            var ctx = $"seed={Seed} case={i} roots=[{string.Join(", ", roots)}] target={target:R}";
            Assert.IsTrue(result.Converged, ctx);
            Assert.IsTrue(result.Iterations <= 50, ctx);
            Assert.AreEqual(target, result.Root, 1e-12, ctx);
        }
    }

    [TestMethod]
    public void BisectionErrorBoundAndIterationCount()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 1_000; i++)
        {
            var root = gen.Uniform(-5, 5);
            var a = root - gen.Uniform(0.1, 3);
            var b = root + gen.Uniform(0.1, 3);
            var tol = Math.Pow(10, -gen.Uniform(3, 12));
            var result = Roots.Bisection(x => Math.Atan(x - root) * (1 + x * x), a, b, new StoppingCriteria(AbsoluteTolerance: tol, RelativeTolerance: 0, MaxIterations: 200));
            var ctx = $"seed={Seed + 1} case={i} root={root:R} tol={tol:E2}";
            Assert.IsTrue(result.Converged, ctx);
            Assert.IsTrue(Math.Abs(result.Root - root) <= tol, ctx);

            // num.root.bisection: n >= log2((b - a)/tol) midpoints suffice.
            var needed = (int)Math.Ceiling(Math.Log2((b - a) / tol));
            Assert.IsTrue(result.Iterations <= needed, $"{ctx} iterations={result.Iterations} needed={needed}");
        }
    }

    [TestMethod]
    public void InvalidBracketsAndExactEndpoints()
    {
        var same = Roots.Brent(x => x * x + 1, -1.0, 1.0);
        Assert.IsFalse(same.Converged);
        Assert.AreEqual(Convergence.InvalidBracket, same.Reason);
        Assert.AreEqual(Convergence.InvalidBracket, Roots.Bisection(x => x * x + 1, -1.0, 1.0).Reason);

        var endpoint = Roots.Brent(x => x - 1, 1.0, 3.0);
        Assert.IsTrue(endpoint.Converged);
        Assert.AreEqual(1.0, endpoint.Root);
        Assert.AreEqual(0, endpoint.Iterations);

        // Reversed bracket and a root found exactly.
        var reversed = Roots.Bisection(x => x - 0.5, 1.0, 0.0);
        Assert.AreEqual(0.5, reversed.Root);
        Assert.AreEqual(Convergence.FunctionTolerance, reversed.Reason);

        Assert.Throws<ArgumentOutOfRangeException>(() => Roots.Brent(x => x, double.NaN, 1.0));
        Assert.Throws<ArgumentNullException>(() => Roots.Brent<double>(null!, 0, 1));
        Assert.IsFalse(Roots.Brent(x => double.NaN, 0.0, 1.0).Converged);
    }

    [TestMethod]
    public void NewtonReproducesTheSqrtTwoTable()
    {
        // docs/design/domains/d9-numerical-analysis.md, "Example: Newton's method for sqrt 2".
        double[] expected = [1.5, 1.4166666666666667, 1.4142156862745099, 1.4142135623746899, 1.4142135623730951];
        for (var n = 1; n <= 5; n++)
        {
            var result = Roots.Newton(x => x * x - 2, x => 2 * x, 1.0, new StoppingCriteria(AbsoluteTolerance: 0, RelativeTolerance: 0, MaxIterations: n));
            Assert.AreEqual(n, result.Iterations);
            Assert.AreEqual(expected[n - 1], result.Root, 1e-15, $"iteration {n}");
        }

        var converged = Roots.Newton(x => x * x - 2, x => 2 * x, 1.0);
        Assert.IsTrue(converged.Converged);
        Assert.AreEqual(Math.Sqrt(2), converged.Root, 1e-15);
        Assert.IsTrue(converged.Iterations <= 7);
    }

    [TestMethod]
    public void DerivativeVariantsAgree()
    {
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 500; i++)
        {
            var target = gen.Uniform(0.5, 3);
            var start = target + gen.Uniform(-0.2, 0.2);
            double F(double x) => Math.Exp(x) - Math.Exp(target);
            var analytic = Roots.Newton(F, Math.Exp, start);
            var finite = Roots.Newton(F, start);
            var automatic = Roots.NewtonAutomatic(x => Dual<double>.Exp(x) - Math.Exp(target), start);
            var ctx = $"seed={Seed + 2} case={i} target={target:R} start={start:R}";
            Assert.IsTrue(analytic.Converged && finite.Converged && automatic.Converged, ctx);
            Assert.AreEqual(target, analytic.Root, 1e-13, ctx);
            Assert.AreEqual(target, finite.Root, 1e-9, ctx);
            Assert.AreEqual(analytic.Root, automatic.Root, 1e-15, ctx);
            Assert.AreEqual(analytic.Iterations, automatic.Iterations, ctx);
        }
    }

    [TestMethod]
    public void NewtonAtAMultipleRootConvergesOnlyLinearly()
    {
        // num.root.newton-multiple: for (x - 1)^3 each step removes only 1/3 of the error.
        var stop = new StoppingCriteria(AbsoluteTolerance: 0, RelativeTolerance: 0, MaxIterations: 10);
        var errors = new List<double>();
        for (var n = 1; n <= 8; n++)
        {
            var r = Roots.Newton(x => Math.Pow(x - 1, 3), x => 3 * Math.Pow(x - 1, 2), 2.0, stop with { MaxIterations = n });
            errors.Add(Math.Abs(r.Root - 1));
        }
        for (var i = 1; i < errors.Count; i++) Assert.AreEqual(2.0 / 3, errors[i] / errors[i - 1], 1e-9);
    }

    [TestMethod]
    public void NewtonAndSecantReportFailureWithoutThrowing()
    {
        var flat = Roots.Newton(x => x * x + 1, x => 0.0, 1.0);
        Assert.IsFalse(flat.Converged);
        Assert.AreEqual(Convergence.ZeroDerivative, flat.Reason);

        var noRoot = Roots.Newton(x => x * x + 1, x => 2 * x, 0.5, new StoppingCriteria(MaxIterations: 20));
        Assert.IsFalse(noRoot.Converged);

        var nan = Roots.Newton(x => double.NaN, x => 1.0, 1.0);
        Assert.AreEqual(Convergence.NotFinite, nan.Reason);

        var secantFlat = Roots.Secant(x => 1.0, 0.0, 1.0);
        Assert.AreEqual(Convergence.ZeroDerivative, secantFlat.Reason);
        Assert.IsFalse(Roots.Secant(x => x * x + 1, 0.0, 1.0, new StoppingCriteria(MaxIterations: 15)).Converged);
    }

    [TestMethod]
    public void SecantConvergesSuperlinearly()
    {
        var result = Roots.Secant(x => Math.Cos(x) - x, 0.0, 1.0);
        Assert.IsTrue(result.Converged);
        Assert.AreEqual(0.7390851332151607, result.Root, 1e-14);
        Assert.IsTrue(result.Iterations <= 8, $"{result.Iterations} iterations");
        Assert.AreEqual(result.Iterations + 2, result.Evaluations);

        // Order (1 + sqrt 5)/2 ~ 1.618: estimate it from the error sequence of x^2 - 2.
        var errors = new List<double>();
        for (var n = 1; n <= 5; n++)
        {
            var r = Roots.Secant(x => x * x - 2, 1.0, 2.0, new StoppingCriteria(AbsoluteTolerance: 0, RelativeTolerance: 0, MaxIterations: n));
            errors.Add(Math.Abs(r.Root - Math.Sqrt(2)));
        }
        var order = Math.Log(errors[3] / errors[4]) / Math.Log(errors[2] / errors[3]);
        Assert.IsTrue(order is > 1.4 and < 1.9, $"order {order}");
    }

    [TestMethod]
    public void GenericOverFloatAndHalf()
    {
        var single = Roots.Brent<float>(x => x * x - 2, 1f, 2f);
        Assert.IsTrue(single.Converged);
        Assert.AreEqual(MathF.Sqrt(2), single.Root, 2e-7f);

        var half = Roots.Newton<Half>(x => x * x - (Half)2, x => (Half)2 * x, (Half)1);
        Assert.IsTrue(half.Converged);
        Assert.AreEqual(1.4142, (double)half.Root, 2e-3);

        var fromCriteria = Roots.Brent(x => x * x - 2, 1.0, 2.0, new StoppingCriteria(FunctionTolerance: 1e-6));
        Assert.AreEqual(Convergence.FunctionTolerance, fromCriteria.Reason);
        Assert.IsTrue(Math.Abs(fromCriteria.FunctionValue) <= 1e-6);
    }
}
