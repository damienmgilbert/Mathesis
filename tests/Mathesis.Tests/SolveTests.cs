using System.Globalization;
using Mathesis.Knowledge;
using Mathesis.Solving;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class SolveTests
{
    private static readonly Symbol X = CalculusHelpers.X;

    private sealed record Case(string Line, string Kind, string Text, string Variables, string Expected, string Flags);

    private static IEnumerable<Case> Cases() =>
        CalculusHelpers.Corpus("solve.txt").Select(line =>
        {
            var p = line.Split('|', StringSplitOptions.TrimEntries);
            return p[0] switch
            {
                "sys" => new Case(line, "sys", p[1], p[2], p[3], p.Length > 4 ? p[4] : string.Empty),
                _ => new Case(line, p[0], p[1], "x", p[2], p.Length > 3 ? p[3] : string.Empty),
            };
        });

    // Splits at top-level separators (not inside parentheses).
    private static List<string> SplitTop(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '(' or '[' or '{') depth++;
            else if (text[i] is ')' or ']' or '}') depth--;
            else if (text[i] == separator && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(text[start..].Trim());
        return parts;
    }

    private static double Number(string text) => text.Trim() switch
    {
        "oo" => double.PositiveInfinity,
        "-oo" => double.NegativeInfinity,
        var t => CalculusHelpers.Eval(Expr.Parse(t), [])!.Value,
    };

    private static Expr Difference(Expr equation) =>
        equation is Apply { Arguments: [var l, var r] } ? new Apply(Operators.Add, [l, new Apply(Operators.Mul, [new Number(Mathesis.Numbers.BigRational.NegativeOne), r])]) : equation;

    private static double? Residual(Expr f, Dictionary<Symbol, double> values) => CalculusHelpers.Eval(f, values);

    // The values of an expected or computed solution set inside [-20, 20]: points and families over k.
    private static List<double> Values(IEnumerable<Expr> points, IEnumerable<ImageFamily> families)
    {
        var values = new List<double>();
        foreach (var p in points)
        {
            if (CalculusHelpers.Eval(p, []) is { } v) values.Add(v);
        }
        foreach (var family in families)
        {
            for (var k = -60; k <= 60; k++)
            {
                if (CalculusHelpers.Eval(family.Element, new Dictionary<Symbol, double> { [family.Parameter] = k }) is { } v && Math.Abs(v) <= 20.0) values.Add(v);
            }
        }
        return [.. values.OrderBy(v => v)];
    }

    private static bool SameValues(List<double> a, List<double> b) =>
        a.Count == b.Count && a.Zip(b).All(p => Math.Abs(p.First - p.Second) <= 1e-8 * Math.Max(1, Math.Abs(p.Second)));

    [TestMethod]
    public void CorpusHasAtLeast150Equations() => Assert.IsGreaterThanOrEqualTo(150, Cases().Count(c => c.Kind == "eq"));

    [TestMethod]
    public void EquationsHaveTheExpectedSolutionsAndEverySolutionChecksOut()
    {
        var failures = new List<string>();
        var catalog = KnowledgeBase.Default;
        foreach (var c in Cases().Where(c => c.Kind == "eq"))
        {
            var equation = Expr.Parse(c.Text);
            var outcome = Solver.Solve(equation, X);
            if (outcome is not Outcome<SolutionSet>.Success { Value: var set, Steps: Derivation derivation })
            {
                failures.Add($"{c.Line}: {outcome.GetType().Name}");
                continue;
            }
            foreach (var step in derivation.Steps)
            {
                if (step.Entry is not { } id || !catalog.TryGet(id.Value, out _)) failures.Add($"{c.Line}: step {step.RuleName} cites no catalog entry");
            }

            // Against the expected set.
            var f = Difference(equation);
            var expected = c.Expected;
            if (expected == "empty") { if (set.Kind != SolutionKind.Empty) failures.Add($"{c.Line}: expected the empty set, got {set}"); }
            else if (expected == "all") { if (set.Kind is not (SolutionKind.All or SolutionKind.Condition)) failures.Add($"{c.Line}: expected all numbers, got {set}"); }
            else if (expected.StartsWith("approx:", StringComparison.Ordinal))
            {
                var want = expected["approx:".Length..].Split(',', StringSplitOptions.TrimEntries).Select(Number).OrderBy(v => v).ToList();
                var got = set.Kind == SolutionKind.Condition ? [.. set.Approximations] : Values(set.Points, []);
                if (got.Count != want.Count || got.Zip(want).Any(p => Math.Abs(p.First - p.Second) > 1e-8 * Math.Max(1, Math.Abs(p.Second)))) failures.Add($"{c.Line}: expected roots {string.Join(", ", want)}, got {string.Join(", ", got)} ({set})");
            }
            else if (expected.StartsWith("image:", StringComparison.Ordinal))
            {
                var k = new Symbol("k", Sort.Integer);
                var want = new List<double>();
                foreach (var element in expected["image:".Length..].Split(';', StringSplitOptions.TrimEntries))
                {
                    var e = Expr.Parse(element);
                    for (var n = -60; n <= 60; n++)
                    {
                        if (CalculusHelpers.Eval(e, new Dictionary<Symbol, double> { [new Symbol("k")] = n }) is { } v && Math.Abs(v) <= 20.0) want.Add(v);
                    }
                }
                var uniqueWant = want.OrderBy(v => v).Where((v, i) => i == 0 || Math.Abs(v - want.OrderBy(q => q).ElementAt(i - 1)) > 1e-9).ToList();
                var gotAll = Values(set.Points, set.Families);
                var uniqueGot = gotAll.Where((v, i) => i == 0 || Math.Abs(v - gotAll[i - 1]) > 1e-9).ToList();
                if (!SameValues(uniqueGot, uniqueWant)) failures.Add($"{c.Line}: the solutions differ ({set}): expected {uniqueWant.Count} values in [-20, 20], got {uniqueGot.Count}");
                _ = k;
            }
            else
            {
                var want = SplitTop(expected.Trim('{', '}'), ',').Select(Number).OrderBy(v => v).ToList();
                var got = Values(set.Points, set.Families);
                if (!SameValues(got, want)) failures.Add($"{c.Line}: expected {string.Join(", ", want)}, got {set}");
            }

            // Substituting each solution back.
            foreach (var p in set.Points)
            {
                var substituted = f.Substitute(X, p);
                var exact = Evaluator.Evaluate(substituted) is Outcome<Expr>.Success { Value: var e } ? e : Mathesis.Symbolics.Canonical.Normalizer.Canonical(substituted);
                var value = CalculusHelpers.Eval(substituted, []);
                if (value is null) failures.Add($"{c.Line}: {p} is not a point where the equation is defined");
                else if (p is Float ? Math.Abs(value.Value) > 1e-10 * Math.Max(1, Math.Abs(value.Value)) && Math.Abs(value.Value) > 1e-10 * 1e3 : false) failures.Add($"{c.Line}: residual {value} at {p}");
                else if (p is not Float && exact is not Mathesis.Symbolics.Number { Value.Sign: 0 } && ZeroTest.Test(exact) is ZeroTestResult.NonZero) failures.Add($"{c.Line}: {p} does not make the equation exactly zero ({exact})");
            }
            foreach (var family in set.Families)
            {
                for (var n = -3; n <= 3; n++)
                {
                    var at = new Dictionary<Symbol, double> { [family.Parameter] = n };
                    var x = CalculusHelpers.Eval(family.Element, at);
                    if (x is null) continue;
                    var residual = CalculusHelpers.Eval(f, new Dictionary<Symbol, double> { [X] = x.Value });
                    if (residual is null || Math.Abs(residual.Value) > 1e-9 * Math.Max(1, Math.Abs(x.Value))) failures.Add($"{c.Line}: family {family.Element} fails at k = {n} (residual {residual})");
                }
            }

            // Random points outside the solution set must fail the equation.
            var random = new Random(CalculusHelpers.StableSeed(c.Line));
            var known = Values(set.Points, set.Families);
            if (set.Kind == SolutionKind.Condition && set.Approximations.Length > 0) known.AddRange(set.Approximations);
            for (var i = 0; i < 80; i++)
            {
                var x = i < 60 ? Math.Round((random.NextDouble() * 24 - 12) * 64) / 64 + 0.001 : (known.Count == 0 ? 0.37 : known[random.Next(known.Count)] + (random.Next(2) == 0 ? 0.05 : -0.37));
                if (known.Any(v => Math.Abs(v - x) < 1e-3)) continue;
                var residual = CalculusHelpers.Eval(f, new Dictionary<Symbol, double> { [X] = x });
                if (residual is null) continue;
                if (set.Kind is SolutionKind.All or SolutionKind.Condition && expected == "all") { if (Math.Abs(residual.Value) > 1e-9 * Math.Max(1, Math.Abs(x))) failures.Add($"{c.Line}: not an identity at x = {x}"); }
                else if (Math.Abs(residual.Value) < 1e-12) failures.Add($"{c.Line}: x = {x} is outside the solution set but satisfies the equation");
            }

            // Extraneous candidates are rejected with a step.
            if (c.Flags.Contains("rej", StringComparison.Ordinal) && !derivation.Steps.Any(s => s.RuleName == "solve/reject-candidate" && s.Entry!.Value.Value == "alg.eq.extraneous")) failures.Add($"{c.Line}: no extraneous candidate was rejected");
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(40)));
    }

    // Intervals written like "(-oo,-2) U [2,oo)".
    private static List<(double Lo, double Hi, bool LoClosed, bool HiClosed)> Intervals(string text)
    {
        var result = new List<(double, double, bool, bool)>();
        foreach (var piece in SplitTop(text, 'U'))
        {
            var t = piece.Trim();
            var inner = t[1..^1];
            var parts = SplitTop(inner, ',');
            result.Add((Number(parts[0]), Number(parts[1]), t[0] == '[', t[^1] == ']'));
        }
        return result;
    }

    [TestMethod]
    public void InequalitiesHaveTheExpectedIntervalsAndPointsAgree()
    {
        var failures = new List<string>();
        foreach (var c in Cases().Where(c => c.Kind == "ineq"))
        {
            var relation = Expr.Parse(c.Text);
            var outcome = Solver.Solve(relation, X);
            if (outcome is not Outcome<SolutionSet>.Success { Value: var set })
            {
                failures.Add($"{c.Line}: {outcome.GetType().Name}");
                continue;
            }
            List<(double Lo, double Hi, bool LoClosed, bool HiClosed)> want = c.Expected switch
            {
                "all" => [(double.NegativeInfinity, double.PositiveInfinity, false, false)],
                "empty" => [],
                var e => Intervals(e),
            };
            var got = set.Kind == SolutionKind.All ? [(double.NegativeInfinity, double.PositiveInfinity, false, false)] : set.Pieces.Select(p => (p.LowerValue, p.UpperValue, p.LowerClosed, p.UpperClosed)).ToList();
            if (got.Count != want.Count || got.Zip(want).Any(p => !(Same(p.First.Item1, p.Second.Lo) && Same(p.First.Item2, p.Second.Hi) && p.First.Item3 == p.Second.LoClosed && p.First.Item4 == p.Second.HiClosed))) failures.Add($"{c.Line}: expected {c.Expected}, got {set}");

            // Points inside satisfy the relation, points outside do not.
            var (l, r) = ((Apply)relation).Arguments is [var a, var b] ? (a, b) : throw new InvalidOperationException();
            var op = ((Apply)relation).Operator;
            var random = new Random(CalculusHelpers.StableSeed(c.Line));
            for (var i = 0; i < 120; i++)
            {
                var x = Math.Round((random.NextDouble() * 20 - 10) * 128) / 128 + 0.0007;
                var values = new Dictionary<Symbol, double> { [X] = x };
                var lv = CalculusHelpers.Eval(l, values);
                var rv = CalculusHelpers.Eval(r, values);
                var holds = lv is { } p && rv is { } q && (op == Operators.Lt ? p < q : op == Operators.Le ? p <= q : op == Operators.Gt ? p > q : op == Operators.Ge ? p >= q : Math.Abs(p - q) > 1e-12);
                var near = set.Pieces.Any(piece => Math.Abs(piece.LowerValue - x) < 1e-3 || Math.Abs(piece.UpperValue - x) < 1e-3);
                if (near) continue;
                var inside = set.Kind == SolutionKind.All || set.Pieces.Any(piece => piece.Contains(x));
                if (inside != holds) failures.Add($"{c.Line}: at x = {x} the relation {(holds ? "holds" : "fails")} but the point is {(inside ? "inside" : "outside")} {set}");
            }
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(40)));
    }

    private static bool Same(double a, double b) => a.Equals(b) || Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(b));

    [TestMethod]
    public void SystemsHaveTheExpectedSolutions()
    {
        var failures = new List<string>();
        foreach (var c in Cases().Where(c => c.Kind == "sys"))
        {
            var equations = SplitTop(c.Text, ';').Select(e => Expr.Parse(e)).ToList();
            var variables = c.Variables.Split(',').Select(v => new Symbol(v.Trim())).ToList();
            var outcome = Solver.SolveSystem(equations, variables);
            if (outcome is not Outcome<SolutionSet>.Success { Value: var set, Steps: Derivation derivation })
            {
                failures.Add($"{c.Line}: {outcome.GetType().Name}");
                continue;
            }
            foreach (var step in derivation.Steps)
            {
                if (step.Entry is null) failures.Add($"{c.Line}: step {step.RuleName} cites no entry");
            }

            var differences = equations.Select(Difference).ToList();
            if (c.Expected == "empty")
            {
                if (set.Kind != SolutionKind.Empty) failures.Add($"{c.Line}: expected no solution, got {set}");
            }
            else if (c.Expected == "param")
            {
                if (set.Kind != SolutionKind.Parametric) { failures.Add($"{c.Line}: expected a parametric family, got {set}"); continue; }
                var tuple = (TupleLiteral)set.Points[0];
                var random = new Random(CalculusHelpers.StableSeed(c.Line));
                for (var i = 0; i < 10; i++)
                {
                    var values = set.Parameters.ToDictionary(p => p, _ => Math.Round(random.NextDouble() * 10 - 5, 3));
                    var point = variables.Select((v, j) => (v, CalculusHelpers.Eval(tuple.Elements[j], values)!.Value)).ToDictionary(t => t.v, t => t.Item2);
                    foreach (var d in differences)
                    {
                        if (Math.Abs(CalculusHelpers.Eval(d, point)!.Value) > 1e-9) failures.Add($"{c.Line}: the parametric point {string.Join(", ", point.Values)} fails an equation");
                    }
                }
            }
            else
            {
                var tuples = SplitTop(c.Expected.Trim('{', '}'), ',').Select(t => t.Trim('(', ')')).ToList();
                // The tuple text was split at its inner commas too; regroup by the number of variables.
                var flat = SplitTop(c.Expected.Trim('{', '}').Replace("),(", ")|(", StringComparison.Ordinal), '|').Select(t => SplitTop(t.Trim().Trim('(', ')'), ',').Select(Number).ToArray()).OrderBy(t => t[0]).ThenBy(t => t[1]).ToList();
                var got = set.Points.Select(p => ((TupleLiteral)p).Elements.Select(e => CalculusHelpers.Eval(e, [])!.Value).ToArray()).OrderBy(t => t[0]).ThenBy(t => t[1]).ToList();
                if (got.Count != flat.Count || got.Zip(flat).Any(p => p.First.Zip(p.Second).Any(v => Math.Abs(v.First - v.Second) > 1e-9))) failures.Add($"{c.Line}: expected {c.Expected}, got {set}");
                foreach (var point in got)
                {
                    var values = variables.Select((v, j) => (v, point[j])).ToDictionary(t => t.v, t => t.Item2);
                    foreach (var d in differences)
                    {
                        if (Math.Abs(CalculusHelpers.Eval(d, values)!.Value) > 1e-9) failures.Add($"{c.Line}: ({string.Join(", ", point)}) does not satisfy every equation");
                    }
                }
                _ = tuples;
            }
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(40)));
    }
}

[TestClass]
public class SolveBehaviorTests
{
    private static readonly Symbol X = CalculusHelpers.X;

    private static (SolutionSet Set, Derivation Steps, Provisos Provisos) Run(string text, SolveOptions? options = null)
    {
        var success = (Outcome<SolutionSet>.Success)Solver.Solve(Expr.Parse(text), X, options);
        return (success.Value, (Derivation)success.Steps!, success.Provisos);
    }

    [TestMethod]
    public void TheCallerChoosesTheQuadraticMethod()
    {
        var factoring = Run("x^2 - 5*x + 6 = 0", new SolveOptions { Method = SolveMethod.Factoring });
        var formula = Run("x^2 - 5*x + 6 = 0", new SolveOptions { Method = SolveMethod.QuadraticFormula });
        var square = Run("x^2 - 5*x + 6 = 0", new SolveOptions { Method = SolveMethod.CompletingSquare });
        Assert.AreEqual(factoring.Set.ToString(), formula.Set.ToString());
        Assert.AreEqual(factoring.Set.ToString(), square.Set.ToString());
        Assert.IsTrue(factoring.Steps.Steps.Any(s => s.Entry!.Value.Value == "alg.eq.zero-product-solve"));
        Assert.IsTrue(formula.Steps.Steps.Any(s => s.Entry!.Value.Value == "alg.quad.quadratic-formula"));
        Assert.IsTrue(square.Steps.Steps.Any(s => s.Entry!.Value.Value == "alg.quad.completing-the-square"));
    }

    [TestMethod]
    public void FactoringFallsBackToTheFormulaForIrrationalRoots()
    {
        var result = Run("x^2 - 2 = 0", new SolveOptions { Method = SolveMethod.Factoring });
        Assert.AreEqual(2, result.Set.Points.Length);
        Assert.IsTrue(result.Steps.Steps.Any(s => s.Entry!.Value.Value == "alg.quad.quadratic-formula"));
    }

    [TestMethod]
    public void AnIntervalTurnsImageSetsIntoFinitePoints()
    {
        var result = Run("sin(x) = 1/2", new SolveOptions { IntervalLow = 0, IntervalHigh = 2 * Math.PI });
        Assert.AreEqual(SolutionKind.Finite, result.Set.Kind);
        Assert.AreEqual(2, result.Set.Points.Length);
        Assert.AreEqual(Math.PI / 6, CalculusHelpers.Eval(result.Set.Points[0], [])!.Value, 1e-12);
        Assert.AreEqual(5 * Math.PI / 6, CalculusHelpers.Eval(result.Set.Points[1], [])!.Value, 1e-12);
    }

    [TestMethod]
    public void LiteralEquationsStateTheirConditions()
    {
        var linear = Run("a*x + b = 0");
        Assert.AreEqual(1, linear.Set.Points.Length);
        Assert.IsTrue(linear.Provisos.OfType<Expr>().Any(p => p.ToString()!.Contains("a != 0", StringComparison.Ordinal)));
        var known = (Outcome<SolutionSet>.Success)Solver.Solve(Expr.Parse("a*x + b = 0"), X, null, MathContext.Default.Assume(Expr.Parse("a > 0")));
        Assert.AreEqual(0, known.Provisos.Count);
    }

    [TestMethod]
    public void ARejectedCandidateIsNamedInItsStep()
    {
        var result = Run("sqrt(x + 2) = x");
        var rejection = result.Steps.Steps.Single(s => s.RuleName == "solve/reject-candidate");
        Assert.AreEqual("alg.eq.extraneous", rejection.Entry!.Value.Value);
        Assert.AreEqual("-1", rejection.Explanation.Arguments.Single(a => a.Key == "candidate").Value.ToString());
    }

    [TestMethod]
    public void EquationsWithoutAClosedFormGiveAConditionSetWithRoots()
    {
        var result = Run("cos(x) = x");
        Assert.AreEqual(SolutionKind.Condition, result.Set.Kind);
        Assert.IsFalse(result.Set.IsComplete);
        Assert.AreEqual(1, result.Set.Approximations.Length);
        Assert.AreEqual(0.7390851332151607, result.Set.Approximations[0], 1e-12);
    }

    [TestMethod]
    public void RootsWithoutRadicalsAreMarkedApproximate()
    {
        var result = Run("x^5 - x - 1 = 0");
        Assert.IsTrue(result.Set.IsApproximate);
        Assert.IsInstanceOfType<Float>(result.Set.Points.Single());
    }

    [TestMethod]
    public void SomethingThatIsNotARelationIsUnevaluated() =>
        Assert.IsInstanceOfType<Outcome<SolutionSet>.Unevaluated>(Solver.Solve(Expr.Parse("x^2 + 1"), X));

    [TestMethod]
    public void ASingleStepChainFromTheEquationToItsSolutionSet()
    {
        var result = Run("x^2 - 5*x + 6 = 0");
        Assert.AreEqual(Mathesis.Symbolics.Canonical.Normalizer.Canonical(Expr.Parse("x^2 - 5*x + 6 = 0")), result.Steps.Start);
        Assert.AreEqual(result.Set.Set, result.Steps.End);
    }

    [TestMethod]
    public void EquationsThatCanonicalizeToATruthValueAreSolved()
    {
        // Canonical turns 0 = 0 into true and 1 = 0 into false; every x satisfies the first and none the second.
        Assert.AreEqual(SolutionKind.All, Run("0 = 0").Set.Kind);
        Assert.AreEqual(SolutionKind.All, Run("2 < 3").Set.Kind);
        Assert.AreEqual(SolutionKind.Empty, Run("1 = 0").Set.Kind);
        Assert.AreEqual(SolutionKind.Empty, Run("3 < 2").Set.Kind);
    }
}
