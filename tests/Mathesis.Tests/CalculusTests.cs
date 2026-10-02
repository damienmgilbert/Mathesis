using Mathesis.Calculus;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

internal static class CalculusHelpers
{
    public static readonly Symbol X = new("x");

    public static IReadOnlyList<string> Corpus(string name) =>
        [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "corpus", name)).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))];

    // FNV-1a: string.GetHashCode is randomized per process.
    public static int StableSeed(string text)
    {
        var h = 2166136261u;
        foreach (var c in text) h = (h ^ c) * 16777619u;
        return (int)(h & 0x7FFFFFFF);
    }

    public static (Expr Expression, MathContext Math, Expr[] Facts) ParseCase(string text)
    {
        var parts = text.Split('@', 2);
        var facts = parts.Length == 1 ? [] : parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(f => Expr.Parse(f)).ToArray();
        var math = MathContext.Default;
        foreach (var fact in facts) math = math.Assume(fact);
        return (Expr.Parse(parts[0]), math, facts);
    }

    public static double? Eval(Expr e, Dictionary<Symbol, double> values) =>
        Evaluator.N(e, values) is Outcome<double>.Success { Value: var v } && double.IsFinite(v) ? v : null;

    public static bool? Holds(Expr proviso, Dictionary<Symbol, double> values)
    {
        if (proviso is not Apply { Operator: var op, Arguments: [var l, var r] }) return null;
        if (Eval(l, values) is not { } a || Eval(r, values) is not { } b) return null;
        if (op == Operators.Ne) return Math.Abs(a - b) > 1e-9;
        if (op == Operators.Gt) return a > b + 1e-9;
        if (op == Operators.Ge) return a >= b - 1e-9;
        if (op == Operators.Lt) return a < b - 1e-9;
        if (op == Operators.Le) return a <= b + 1e-9;
        return null;
    }

    /// <summary>The derivative of <paramref name="f"/> by Richardson extrapolation of central differences, or <c>null</c> where it is unstable.</summary>
    public static double? NumericDerivative(Expr f, Symbol variable, Dictionary<Symbol, double> point)
    {
        double? Central(double h)
        {
            var plus = new Dictionary<Symbol, double>(point) { [variable] = point[variable] + h };
            var minus = new Dictionary<Symbol, double>(point) { [variable] = point[variable] - h };
            return Eval(f, plus) is { } a && Eval(f, minus) is { } b ? (a - b) / (2 * h) : null;
        }
        const double H = 1e-3;
        if (Central(H) is not { } d1 || Central(H / 2) is not { } d2 || Central(H / 4) is not { } d3) return null;
        var r1 = (4 * d2 - d1) / 3;
        var r2 = (4 * d3 - d2) / 3;
        var best = (16 * r2 - r1) / 15;
        return Math.Abs(r2 - r1) <= 1e-6 * Math.Max(1, Math.Abs(best)) ? best : null;
    }
}

[TestClass]
public class DerivativeTests
{
    [TestMethod]
    public void CorpusHasAtLeast100Cases() => Assert.IsGreaterThanOrEqualTo(100, CalculusHelpers.Corpus("derivatives.txt").Count);

    [TestMethod]
    public void DerivativesMatchCentralDifferencesAndReplay()
    {
        var failures = new List<string>();
        var catalog = Mathesis.Knowledge.KnowledgeBase.Default;
        foreach (var text in CalculusHelpers.Corpus("derivatives.txt"))
        {
            var (expression, math, facts) = CalculusHelpers.ParseCase(text);
            var outcome = Differentiator.Differentiate(expression, CalculusHelpers.X, 1, math);
            if (outcome is not Outcome<Expr>.Success { Value: var derivative, Steps: Derivation derivation } success)
            {
                failures.Add($"{text}: {outcome.GetType().Name}");
                continue;
            }
            if (derivation.Replay(math, StepReplayer.Default) is not Outcome<Expr>.Success { Value: var replayed } || !replayed.Equals(derivative)) failures.Add($"{text}: replay differs");
            foreach (var step in derivation.Flatten())
            {
                if (step.RuleName != "normalize" && (step.Entry is not { } id || !catalog.TryGet(id.Value, out _))) failures.Add($"{text}: step {step.RuleName} cites no entry");
            }

            var symbols = expression.FreeSymbols.Union(derivative.FreeSymbols).Union([CalculusHelpers.X]).Union(facts.SelectMany(f => f.FreeSymbols)).OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
            var random = new Random(CalculusHelpers.StableSeed(text));
            var provisos = success.Provisos.OfType<Expr>().ToArray();
            var agreed = 0;
            for (var attempt = 0; attempt < 600 && agreed < 12; attempt++)
            {
                var values = symbols.ToDictionary(s => s, _ => Math.Round((random.NextDouble() * 6 - 3) * 16) / 16 + 0.03125);
                if (provisos.Concat(facts).Any(p => CalculusHelpers.Holds(p, values) != true)) continue;
                if (CalculusHelpers.NumericDerivative(expression, CalculusHelpers.X, values) is not { } numeric) continue;
                if (CalculusHelpers.Eval(derivative, values) is not { } exact)
                {
                    failures.Add($"{text}: derivative {derivative} undefined at a point where the original is differentiable");
                    break;
                }
                if (Math.Abs(numeric - exact) > 1e-5 * Math.Max(1, Math.Abs(exact)))
                {
                    failures.Add($"{text}: {derivative} gives {exact}, difference quotient {numeric} at {string.Join(", ", values.Select(kv => $"{kv.Key.Name}={kv.Value}"))}");
                    break;
                }
                agreed++;
            }
            if (agreed < 6 && !failures.Any(f => f.StartsWith(text + ":", StringComparison.Ordinal))) failures.Add($"{text}: only {agreed} usable points");
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "deriv-failures.txt"), failures);
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(30)));
    }

    [TestMethod]
    public void HigherOrderDerivativesAreIteratedFirstDerivatives()
    {
        var x = CalculusHelpers.X;
        Assert.AreEqual(Normalizer.Canonical(Expr.Parse("60*x^2")), ((Outcome<Expr>.Success)Differentiator.Differentiate(Expr.Parse("x^5"), x, 3)).Value);
        Assert.AreEqual(Normalizer.Canonical(Expr.Parse("sin(x)")), ((Outcome<Expr>.Success)Differentiator.Differentiate(Expr.Parse("sin(x)"), x, 4)).Value);
    }

    [TestMethod]
    public void PartialDerivativesTreatOtherSymbolsAsConstants()
    {
        var y = new Symbol("y");
        var point = new Dictionary<Symbol, double> { [CalculusHelpers.X] = 1.5, [y] = 2.5 };
        Assert.AreEqual(3 * 1.5 * 1.5 * 2.5 * 2.5, CalculusHelpers.Eval(((Outcome<Expr>.Success)Differentiator.Differentiate(Expr.Parse("x^2*y^3"), y)).Value, point)!.Value, 1e-12);
        var mixed = Differentiator.Differentiate(((Outcome<Expr>.Success)Differentiator.Differentiate(Expr.Parse("x^2*y^3"), CalculusHelpers.X)).Value, y);
        Assert.AreEqual(Normalizer.Canonical(Expr.Parse("6*x*y^2")), ((Outcome<Expr>.Success)mixed).Value);
    }

    [TestMethod]
    public void ImplicitDifferentiationOfTheCircle()
    {
        var success = (Outcome<Expr>.Success)Differentiator.Implicit(Expr.Parse("x^2 + y^2 = 25"), CalculusHelpers.X, new Symbol("y"));
        var point = new Dictionary<Symbol, double> { [CalculusHelpers.X] = 3, [new Symbol("y")] = 4 };
        Assert.AreEqual(-0.75, CalculusHelpers.Eval(success.Value, point)!.Value, 1e-12);
        Assert.AreEqual(1, success.Provisos.Count);
    }

    [TestMethod]
    public void LogarithmicDifferentiationOfXToTheX()
    {
        var math = MathContext.Default.Assume(Expr.Parse("x > 0"));
        var value = ((Outcome<Expr>.Success)Differentiator.Logarithmic(Expr.Parse("x^x"), CalculusHelpers.X, math)).Value;
        var values = new Dictionary<Symbol, double> { [CalculusHelpers.X] = 1.7 };
        Assert.AreEqual(Math.Pow(1.7, 1.7) * (Math.Log(1.7) + 1), CalculusHelpers.Eval(value, values)!.Value, 1e-9);
    }

    [TestMethod]
    public void OperatorsWithoutARuleStayUnevaluated() =>
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Differentiator.Differentiate(Expr.Parse("floor(x)"), CalculusHelpers.X));
}
