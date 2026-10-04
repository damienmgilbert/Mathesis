using Mathesis.Calculus;
using Mathesis.Knowledge;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class LimitTests
{
    private sealed record Case(string Text, Expr F, Expr Point, LimitDirection Side, string Expected);

    private static IEnumerable<Case> Cases() =>
        CalculusHelpers.Corpus("limits.txt").Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var side = parts[2] switch { "left" => LimitDirection.FromLeft, "right" => LimitDirection.FromRight, _ => LimitDirection.Both };
            return new Case(line, Expr.Parse(parts[0]), Expr.Parse(parts[1]), side, parts[3]);
        });

    // The function along a sequence toward the point, independent of the engine: 2^-k from the side, or 2^k at infinity.
    private static IEnumerable<double> Sequence(Expr f, Case c, int from, int to)
    {
        var x = CalculusHelpers.X;
        var pointValue = c.Point is Constant { Id: ConstantId.PositiveInfinity } ? double.PositiveInfinity : c.Point is Apply { Operator.Id: "mul" } or Apply { Operator.Id: "neg" } ? double.NegativeInfinity : CalculusHelpers.Eval(c.Point, []) ?? double.NaN;
        for (var k = from; k <= to; k++)
        {
            var at = double.IsInfinity(pointValue) ? Math.Sign(pointValue) * Math.Pow(2, k) : pointValue + (c.Side == LimitDirection.FromLeft ? -1 : 1) * Math.Pow(2, -k) * Math.Max(1, Math.Abs(pointValue));
            if (CalculusHelpers.Eval(f, new Dictionary<Symbol, double> { [x] = at }) is { } v) yield return v;
        }
    }

    [TestMethod]
    public void CorpusHasAtLeast100Cases() => Assert.IsGreaterThanOrEqualTo(100, Cases().Count());

    [TestMethod]
    public void LimitsMatchTheExpectedValueAndSequenceEvaluation()
    {
        var failures = new List<string>();
        var report = new List<string>();
        var catalog = KnowledgeBase.Default;
        foreach (var c in Cases())
        {
            var outcome = Limits.Limit(c.F, CalculusHelpers.X, c.Point, c.Side);
            report.Add($"{(outcome is Outcome<LimitResult>.Success ? "ok" : "--")}  {c.Text}  =>  {(outcome is Outcome<LimitResult>.Success s0 ? s0.Value : "")}");
            if (outcome is not Outcome<LimitResult>.Success { Value: var result, Steps: Derivation derivation })
            {
                if (c.Expected != "?") failures.Add($"{c.Text}: no result");
                continue;
            }
            foreach (var step in derivation.Flatten())
            {
                if (step.RuleName != "normalize" && (step.Entry is not { } id || !catalog.TryGet(id.Value, out _))) failures.Add($"{c.Text}: step {step.RuleName} cites no entry");
            }

            // Against the expected value.
            string? mismatch = (c.Expected, result) switch
            {
                ("?", _) => null,
                ("dne", LimitResult.DoesNotExist) => null,
                ("oo", LimitResult.PositiveInfinity) => null,
                ("-oo", LimitResult.NegativeInfinity) => null,
                (var e, LimitResult.Finite { Value: var v }) when e is not ("dne" or "oo" or "-oo") =>
                    CalculusHelpers.Eval(Expr.Parse(e), []) is { } want && CalculusHelpers.Eval(v, []) is { } got && Math.Abs(want - got) <= 1e-9 * Math.Max(1, Math.Abs(want)) ? null : $"expected {e}, got {v}",
                _ => $"expected {c.Expected}, got {result}",
            };
            if (mismatch is not null) failures.Add($"{c.Text}: {mismatch}");

            // Against the sequence, whatever the expected value says.
            var tail = Sequence(c.F, c, 14, 22).ToList();
            if (tail.Count < 4) continue;
            switch (result)
            {
                case LimitResult.Finite { Value: var v } when CalculusHelpers.Eval(v, []) is { } target:
                    if (Math.Abs(tail[^1] - target) > 2e-2 * Math.Max(1, Math.Abs(target))) failures.Add($"{c.Text}: the sequence ends at {tail[^1]}, not {target}");
                    break;
                case LimitResult.PositiveInfinity:
                    if (tail[^1] < 10 || tail[^1] < tail[0]) failures.Add($"{c.Text}: the sequence does not grow to +oo ({tail[0]} … {tail[^1]})");
                    break;
                case LimitResult.NegativeInfinity:
                    if (tail[^1] > -10 || tail[^1] > tail[0]) failures.Add($"{c.Text}: the sequence does not fall to -oo ({tail[0]} … {tail[^1]})");
                    break;
            }
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "limit-report.txt"), report);
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "limit-failures.txt"), failures);
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(40)));
    }

    [TestMethod]
    public void LHopitalIsGatedByTheCurriculumLevel()
    {
        var f = Expr.Parse("(exp(x) - 1 - x)/x^2");
        var zero = new Number(Mathesis.Numbers.BigRational.Zero);
        var low = new MathContext { Level = CurriculumLevel.Algebra2 };
        Assert.IsInstanceOfType<Outcome<LimitResult>.Unevaluated>(Limits.Limit(f, CalculusHelpers.X, zero, LimitDirection.Both, low));
        var calculus1 = new MathContext { Level = CurriculumLevel.Calculus1 };
        var outcome = (Outcome<LimitResult>.Success)Limits.Limit(f, CalculusHelpers.X, zero, LimitDirection.Both, calculus1);
        var steps = ((Derivation)outcome.Steps!).Flatten().ToList();
        Assert.IsTrue(steps.Any(st => st.RuleName == "limit/lhopital" && st.Entry!.Value.Value == "calc.lim.lhopital"));
    }

    [TestMethod]
    public void ASeriesLimitCitesTheSeriesEntry()
    {
        var zero = new Number(Mathesis.Numbers.BigRational.Zero);
        var outcome = (Outcome<LimitResult>.Success)Limits.Limit(Expr.Parse("(tan(x) - sin(x))/x^3"), CalculusHelpers.X, zero);
        Assert.IsTrue(((Derivation)outcome.Steps!).Flatten().Any(st => st.RuleName == "limit/series" && st.Entry!.Value.Value == "calc.series.series-arithmetic"));
    }

    [TestMethod]
    public void OneSidedLimitsDifferWhereTheTwoSidedLimitDoesNotExist()
    {
        var zero = new Number(Mathesis.Numbers.BigRational.Zero);
        var both = (Outcome<LimitResult>.Success)Limits.Limit(Expr.Parse("abs(x)/x"), CalculusHelpers.X, zero);
        Assert.IsInstanceOfType<LimitResult.DoesNotExist>(both.Value);
        Assert.AreEqual(new LimitResult.Finite(new Number(Mathesis.Numbers.BigRational.One)), ((Outcome<LimitResult>.Success)Limits.Limit(Expr.Parse("abs(x)/x"), CalculusHelpers.X, zero, LimitDirection.FromRight)).Value);
        Assert.AreEqual(new LimitResult.Finite(new Number(Mathesis.Numbers.BigRational.NegativeOne)), ((Outcome<LimitResult>.Success)Limits.Limit(Expr.Parse("abs(x)/x"), CalculusHelpers.X, zero, LimitDirection.FromLeft)).Value);
    }
}
