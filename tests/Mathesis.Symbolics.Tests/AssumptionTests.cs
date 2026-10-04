using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class AssumptionTests
{
    private const int Seed = 20261060;

    private static readonly Symbol X = Symbol("x");
    private static readonly Symbol Y = Symbol("y");

    private static Expr P(string text) => Expr.Parse(text);

    private static AssumptionSet Facts(params string[] facts) => AssumptionSet.Empty.Add(facts.Select(P));

    // ----- The truth table: no wrong True or False -----

    [TestMethod]
    public void TheTruthTableHasNoWrongAnswers()
    {
        var cases = Corpus.RawLines("assumptions.txt").Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        Assert.IsTrue(cases.Count >= 200, $"only {cases.Count} cases");

        var misses = new List<string>();
        var wrong = new List<string>();
        foreach (var line in cases)
        {
            var arrow = line.LastIndexOf(" ==> ", StringComparison.Ordinal);
            var turnstile = line.IndexOf("|-", StringComparison.Ordinal);
            Assert.IsTrue(arrow > 0 && turnstile >= 0, $"bad line '{line}'");
            var facts = line[..turnstile].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var proposition = line[(turnstile + 2)..arrow].Trim();
            var expected = Enum.Parse<Truth>(line[(arrow + 5)..].Trim());

            var actual = Facts(facts).Ask(P(proposition));
            if (actual == expected) continue;
            if (actual == Truth.Unknown) misses.Add(line);
            else wrong.Add($"{line}   [got {actual}]");
        }

        Assert.AreEqual(0, wrong.Count, "Wrong answers:\n" + string.Join("\n", wrong));

        // Unknown is allowed, but the engine must decide most of what it is meant to decide.
        var decidable = cases.Count(l => !l.EndsWith("==> Unknown", StringComparison.Ordinal));
        Assert.IsTrue(misses.Count <= decidable / 8, $"{misses.Count} of {decidable} decidable cases were missed:\n" + string.Join("\n", misses));
    }

    // ----- Soundness against sampling -----

    private static double Value(Expr e, double x, double y) =>
        ExprGen.Evaluate(e, new Dictionary<string, double> { ["x"] = x, ["y"] = y });

    private static Expr RandomFact(Random r)
    {
        var s = r.Next(2) == 0 ? (Expr)X : Y;
        var c = Number(r.Next(-4, 5));
        return r.Next(9) switch
        {
            0 => Gt(s, c),
            1 => Lt(s, c),
            2 => Ge(s, c),
            3 => Le(s, c),
            4 => Gt(X, Y),
            5 => Gt(Add(X, Y), c),
            6 => Not(Eq(s, Number(0))),
            7 => P($"{(s == X ? "x" : "y")} in [{r.Next(-4, 1)}, {r.Next(1, 5)}]"),
            _ => Lt(Mul(X, Y), c),
        };
    }

    [TestMethod]
    public void AskAgreesWithSamplingOnRandomFactsAndPropositions()
    {
        // MATHESIS_STRESS=n multiplies the number of trials (and moves the seed) for longer local runs.
        var stress = int.TryParse(Environment.GetEnvironmentVariable("MATHESIS_STRESS"), out var multiplier) ? Math.Max(multiplier, 1) : 1;
        var gen = new Gen(Seed + stress - 1);
        var r = gen.Random;
        var decided = 0;
        var checkedTrue = 0;
        var checkedFalse = 0;
        for (var trial = 0; trial < 5000 * stress; trial++)
        {
            var facts = Enumerable.Range(0, r.Next(0, 4)).Select(_ => RandomFact(r)).ToArray();
            var set = AssumptionSet.Empty.Add(facts);

            var left = gen.RandomExpr(2);
            var right = r.Next(3) == 0 ? gen.RandomExpr(1) : Number(r.Next(-3, 4));
            Expr proposition = r.Next(6) switch { 0 => Lt(left, right), 1 => Le(left, right), 2 => Gt(left, right), 3 => Ge(left, right), 4 => Eq(left, right), _ => Ne(left, right) };
            var truth = set.Ask(proposition);
            if (truth == Truth.Unknown) continue;
            decided++;

            // Sample points that satisfy the facts and where both sides are defined.
            var holds = 0;
            var fails = 0;
            for (var i = 0; i < 400; i++)
            {
                var x = r.Next(4) == 0 ? r.Next(-6, 7) : r.NextDouble() * 12 - 6;
                var y = r.Next(4) == 0 ? r.Next(-6, 7) : r.NextDouble() * 12 - 6;
                if (!facts.All(f => FactHolds(f, x, y))) continue;
                var a = Value(left, x, y);
                var b = Value(right, x, y);
                if (!double.IsFinite(a) || !double.IsFinite(b)) continue;
                // Values closer than the tolerance are ambiguous (rounding noise or a genuinely tiny difference): they count for neither side.
                var tolerance = 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
                var equal = a == b;
                var different = Math.Abs(a - b) > tolerance;
                var op = ((Apply)proposition).Operator;
                var clearlyHolds = op == Operators.Lt ? a < b - tolerance : op == Operators.Le ? a <= b - tolerance || equal : op == Operators.Gt ? a > b + tolerance : op == Operators.Ge ? a >= b + tolerance || equal : op == Operators.Eq ? equal : different;
                var clearlyFails = op == Operators.Lt ? a > b + tolerance || equal : op == Operators.Le ? a > b + tolerance : op == Operators.Gt ? a < b - tolerance || equal : op == Operators.Ge ? a < b - tolerance : op == Operators.Eq ? different : equal;
                if (clearlyHolds) holds++;
                if (clearlyFails) fails++;
            }
            if (truth == Truth.True)
            {
                checkedTrue++;
                Assert.AreEqual(0, fails, $"Ask said True for '{proposition}' under [{string.Join("; ", facts.Select(f => f.ToString()))}] but sampling found {fails} counterexamples");
            }
            else
            {
                checkedFalse++;
                Assert.AreEqual(0, holds, $"Ask said False for '{proposition}' under [{string.Join("; ", facts.Select(f => f.ToString()))}] but it held at {holds} sample points");
            }
        }
        Assert.IsTrue(decided >= 300, $"only {decided} decided answers ({checkedTrue} True, {checkedFalse} False): the test is not exercising Ask");
    }

    private static bool FactHolds(Expr fact, double x, double y)
    {
        if (fact is Apply { Operator: var op, Arguments: [var l, var r] } && op != Operators.Element)
        {
            var a = Value(l, x, y);
            var b = Value(r, x, y);
            return op == Operators.Lt ? a < b : op == Operators.Le ? a <= b : op == Operators.Gt ? a > b : op == Operators.Ge ? a >= b : op == Operators.Eq ? a == b : a != b;
        }
        if (fact is Apply { Operator: var not, Arguments: [Apply { Arguments: [var l2, var r2] }] } && not == Operators.Not) return Value(l2, x, y) != Value(r2, x, y);
        if (fact is Apply { Operator: var el, Arguments: [Symbol s, IntervalLiteral i] } && el == Operators.Element)
        {
            var v = s == X ? x : y;
            return v >= Value(i.Lower, 0, 0) && v <= Value(i.Upper, 0, 0);
        }
        return true;
    }

    // ----- Layers -----

    [TestMethod]
    public void SignPropagationFollowsTheLattice()
    {
        var set = Facts("x > 0", "y < 0");
        Assert.AreEqual(SignInfo.Positive, set.Sign(P("x")));
        Assert.AreEqual(SignInfo.Negative, set.Sign(P("y")));
        Assert.AreEqual(SignInfo.Negative, set.Sign(P("x * y")));
        Assert.AreEqual(SignInfo.Positive, set.Sign(P("y^2")));
        Assert.AreEqual(SignInfo.Negative, set.Sign(P("y^3")));
        Assert.AreEqual(SignInfo.Positive, set.Sign(P("exp(y)")));
        Assert.AreEqual(SignInfo.NonNegative, AssumptionSet.Empty.Sign(P("x^2")));
        Assert.AreEqual(SignInfo.Positive, AssumptionSet.Empty.Sign(P("x^2 + 1")));
        Assert.AreEqual(SignInfo.NonNegative, AssumptionSet.Empty.Sign(P("abs(x)")));
        Assert.AreEqual(SignInfo.Real, AssumptionSet.Empty.Sign(P("x")));
        Assert.AreEqual(SignInfo.NonReal, AssumptionSet.Empty.Sign(P("I")));
        Assert.IsTrue(AssumptionSet.Empty.Sign(Symbol("z", Sort.Complex)).HasFlag(SignInfo.NonReal));
        Assert.AreEqual(SignInfo.NonNegative, AssumptionSet.Empty.Sign(Symbol("n", Sort.Natural)));
    }

    [TestMethod]
    public void BoundsComeFromFactsAndSorts()
    {
        var set = Facts("x in [1, 2]", "y > 3");
        var bx = set.Bounds(X)!.Value;
        Assert.IsTrue(bx.Lower <= 1 && bx.Lower > 0.999999 && bx.Upper >= 2 && bx.Upper < 2.000001);
        var by = set.Bounds(Y)!.Value;
        Assert.IsTrue(by.Lower <= 3 && by.Lower > 2.999999 && double.IsPositiveInfinity(by.Upper));
        Assert.IsNull(AssumptionSet.Empty.Bounds(X));
        Assert.AreEqual(0.0, AssumptionSet.Empty.Bounds(Symbol("n", Sort.Natural))!.Value.Lower);

        // Facts about another symbol propagate: x > y, y > 0.
        var chained = Facts("x > y", "y > 0");
        Assert.IsTrue(chained.Bounds(X)!.Value.Lower >= 0);

        // Integer symbols get integer bounds.
        var integer = Facts("n in Z", "n > 0.5", "n < 7.5");
        var bn = integer.Bounds(Symbol("n"))!.Value;
        Assert.AreEqual(1.0, bn.Lower);
        Assert.AreEqual(7.0, bn.Upper);
    }

    [TestMethod]
    public void FactsMustBeBoolean()
    {
        Assert.ThrowsExactly<ArgumentException>(() => AssumptionSet.Empty.Add(P("x + 1")));
        Assert.ThrowsExactly<ArgumentNullException>(() => AssumptionSet.Empty.Add((Expr)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => AssumptionSet.Empty.Ask(null!));
    }

    [TestMethod]
    public void AssumptionSetsCompareByTheirFacts()
    {
        var a = Facts("x > 0", "y > 1");
        var b = Facts("x > 0", "y > 1");
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreNotEqual(a, Facts("x > 0"));
        Assert.AreSame(a, a.Add(P("x > 0")));
        Assert.AreEqual(2, a.Facts.Length);
    }

    [TestMethod]
    public void ConjunctionsAndNegationsAreUsedAsFacts()
    {
        Assert.AreEqual(Truth.True, Facts("x > 0 and y > 0").Ask(P("x * y > 0")));
        Assert.AreEqual(Truth.True, Facts("not (x <= 0)").Ask(P("x > 0")));
        Assert.AreEqual(Truth.True, Facts("not (x <= 0 or y <= 0)").Ask(P("x + y > 0")));
        Assert.AreEqual(Truth.True, Facts("x != 0").Ask(P("x^2 > 0")));
        Assert.AreEqual(Truth.True, Facts("x - y != 0").Ask(P("x != y")));
    }

    [TestMethod]
    public void ContradictoryFactsDoNotCrash()
    {
        var set = Facts("x > 1", "x < 0");
        _ = set.Ask(P("x > 5"));
        _ = set.Sign(P("x"));
        _ = set.Bounds(X);
    }

    [TestMethod]
    public void MathContextCarriesAssumptions()
    {
        var context = MathContext.Default.Assume(P("x > 0"));
        Assert.AreEqual(Truth.True, context.Ask(P("sqrt(x) > 0")));
        Assert.AreEqual(Truth.Unknown, MathContext.Default.Ask(P("x > 0")));
        Assert.AreEqual(NumberField.Real, context.Field);
        Assert.AreEqual(NumberField.Complex, (context with { Field = NumberField.Complex }).NormalizeOptions.Field);
        Assert.AreNotSame(MathContext.Default, context);
        Assert.AreEqual(1, context.Assumptions.Facts.Length);
    }

    [TestMethod]
    public void EnclosuresContainTheValuesUnderTheFacts()
    {
        var set = Facts("x in [1, 2]");
        var e = set.Enclose(P("x^2 + 1"));
        Assert.IsTrue(e.Lower <= 2 && e.Upper >= 5);
        Assert.IsTrue(e.Width < 4.001);
    }
}
