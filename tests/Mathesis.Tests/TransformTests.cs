using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class TransformTests
{
    private static readonly Symbol X = new("x");

    private static Expr Canon(string text) => Normalizer.Canonical(Expr.Parse(text));

    private static Expr Run(Transform t, string text, params string[] facts)
    {
        var math = MathContext.Default;
        foreach (var fact in facts) math = math.Assume(Expr.Parse(fact));
        var outcome = t.Run(Expr.Parse(text), new RewriteContext(math));
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(outcome, $"{t.Name}({text}) did not apply");
        return ((Outcome<Expr>.Success)outcome).Value;
    }

    private static void AssertEqualAtPoints(Expr a, Expr b, params double[] xs)
    {
        foreach (var x in xs)
        {
            var values = new Dictionary<Symbol, double> { [X] = x };
            var left = ((Outcome<double>.Success)Evaluator.N(a, values)).Value;
            var right = ((Outcome<double>.Success)Evaluator.N(b, values)).Value;
            Assert.AreEqual(left, right, 1e-9 * Math.Max(1, Math.Abs(left)), $"{a} vs {b} at x={x}");
        }
    }

    [TestMethod]
    public void FactorFindsRationalRoots() =>
        Assert.AreEqual(Canon("(x - 3)*(x - 2)*(x - 1)"), Run(Transforms.Factor, "x^3 - 6*x^2 + 11*x - 6"));

    [TestMethod]
    public void FactorKeepsAnIrreducibleCofactor() =>
        Assert.AreEqual(Canon("(x - 1)*(x^2 + x + 1)"), Run(Transforms.Factor, "x^3 - 1"));

    [TestMethod]
    public void FactorMovesContentOutOfLinearFactors() =>
        Assert.AreEqual(Canon("(2*x + 1)*(x + 2)"), Run(Transforms.Factor, "2*x^2 + 5*x + 2"));

    [TestMethod]
    public void ExpandMultipliesPowersOfSums()
    {
        Assert.AreEqual(Canon("x^6 + 6*x^5 + 15*x^4 + 20*x^3 + 15*x^2 + 6*x + 1"), Run(Transforms.Expand, "(x + 1)^6"));
        Assert.AreEqual(Canon("a*c + a*d + b*c + b*d"), Run(Transforms.Expand, "(a + b)*(c + d)"));
    }

    [TestMethod]
    public void CancelDividesOutTheCommonFactorAndRecordsItsProviso()
    {
        var success = (Outcome<Expr>.Success)Transforms.Cancel.Run(Expr.Parse("(x^2 - 1)/(x - 1)"));
        Assert.AreEqual(Canon("x + 1"), success.Value);
        Assert.AreEqual(1, success.Provisos.Count);
        Assert.AreEqual(Canon("x - 1 != 0"), Normalizer.Canonical((Expr)success.Provisos[0]));
    }

    [TestMethod]
    public void CancelUsesTheAssumptionsInsteadOfAddingAProviso()
    {
        var math = MathContext.Default.Assume(Expr.Parse("x > 1"));
        var success = (Outcome<Expr>.Success)Transforms.Cancel.Run(Expr.Parse("(x^2 - 1)/(x - 1)"), new RewriteContext(math));
        Assert.AreEqual(0, success.Provisos.Count);
    }

    [TestMethod]
    public void TogetherCombinesOverACommonDenominator()
    {
        var result = Run(Transforms.Together, "1/x + 1/(x + 1)");
        AssertEqualAtPoints(Expr.Parse("1/x + 1/(x + 1)"), result, 0.5, 2, -3.5);
        Assert.IsFalse(result is Apply { Operator.Id: "add" }, "the result should be one fraction");
        Assert.AreEqual(Canon("(a*3 + b*2)/6"), Run(Transforms.Together, "a/2 + b/3"));
    }

    [TestMethod]
    public void ApartSplitsIntoPartialFractions()
    {
        var simple = Run(Transforms.Apart, "(x + 3)/((x + 1)*(x + 2))");
        AssertEqualAtPoints(Expr.Parse("(x + 3)/((x + 1)*(x + 2))"), simple, 0.5, 2, 7, -0.25);
        Assert.AreEqual(Canon("2/(x + 1) - 1/(x + 2)"), simple);

        Assert.AreEqual(Canon("1/x^2 - 1/x + 1/(x + 1)"), Run(Transforms.Apart, "1/(x^2*(x + 1))"));

        var improper = Run(Transforms.Apart, "(x^3 + 1)/(x^2 - 1)");
        AssertEqualAtPoints(Expr.Parse("(x^3 + 1)/(x^2 - 1)"), improper, 0.5, 2, 7, -0.25);
        Assert.AreEqual(Canon("x + 1/(x - 1)"), improper);
    }

    [TestMethod]
    public void CompleteSquareWritesAQuadraticAsAPerfectSquarePlusAConstant()
    {
        Assert.AreEqual(Canon("(x + 2)^2 + 3"), Run(Transforms.CompleteSquare(X), "x^2 + 4*x + 7"));
        AssertEqualAtPoints(Expr.Parse("2*x^2 + 3*x + 1"), Run(Transforms.CompleteSquare(X), "2*x^2 + 3*x + 1"), 0.5, 2, -1.5);
    }

    [TestMethod]
    public void CompleteSquareNeedsANonZeroLeadingCoefficient()
    {
        // With a symbolic leading coefficient the transform applies only when a != 0 is known.
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Transforms.CompleteSquare(X).Run(Expr.Parse("a*x^2 + b*x + c")));
        var math = MathContext.Default.Assume(Expr.Parse("a > 0"));
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(Transforms.CompleteSquare(X).Run(Expr.Parse("a*x^2 + b*x + c"), new RewriteContext(math)));
    }

    [TestMethod]
    public void CollectGroupsTermsByPowersOfTheVariable() =>
        Assert.AreEqual(Canon("c + (a + b)*x + d*x^2"), Run(Transforms.Collect(X), "a*x + b*x + c + d*x^2"));

    [TestMethod]
    public void RationalizeMovesRadicalsOutOfDenominators() =>
        Assert.AreEqual(Canon("sqrt(2)/2"), Run(Transforms.Rationalize, "1/sqrt(2)"));

    [TestMethod]
    public void LogarithmTransformsNeedPositivityAndUseIt()
    {
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Transforms.LogCombine.Run(Expr.Parse("ln(x) + ln(y)")));
        Assert.AreEqual(Canon("ln(x*y)"), Run(Transforms.LogCombine, "ln(x) + ln(y)", "x > 0", "y > 0"));
        Assert.AreEqual(Canon("ln(x) + 2*ln(y)"), Run(Transforms.LogExpand, "ln(x*y^2)", "x > 0", "y > 0"));
    }

    [TestMethod]
    public void TrigTransforms()
    {
        Assert.AreEqual(Canon("2*sin(x)*cos(x)"), Run(Transforms.TrigExpand, "sin(2*x)"));
        Assert.AreEqual(Canon("cos(x)*cos(y) - sin(x)*sin(y)"), Run(Transforms.TrigExpand, "cos(x + y)"));
        Assert.AreEqual(Canon("(1 - cos(2*x))/2"), Run(Transforms.TrigReduce, "sin(x)^2"));
        Assert.AreEqual(Canon("sec(x)^2"), Run(Transforms.TrigSimplify, "1 + tan(x)^2"));
    }

    [TestMethod]
    public void PowerSimplifyCombinesRadicals() =>
        Assert.AreEqual(Canon("sqrt(x*y)"), Run(Transforms.PowerSimplify, "sqrt(x)*sqrt(y)", "x > 0", "y > 0"));
}

[TestClass]
public class RuleLibraryTests
{
    [TestMethod]
    public void RulesComeOnlyFromCatalogEntries()
    {
        var library = RuleLibrary.Default;
        Assert.IsGreaterThan(200, library.Rules.Count());
        foreach (var rule in library.Rules)
        {
            Assert.IsNotNull(rule.Entry, rule.Name);
            Assert.IsTrue(library.Catalog.TryGet(rule.Entry.Value.Value, out _), rule.Name);
            Assert.IsFalse(rule.Tags.IsEmpty, rule.Name);
        }
    }

    [TestMethod]
    public void ConjunctionsGiveOneRuleEach()
    {
        Assert.IsTrue(RuleLibrary.Default.TryGetRule("trig.id.period-sin#1", out _));
        Assert.IsTrue(RuleLibrary.Default.TryGetRule("trig.id.period-sin#4", out _));
    }

    [TestMethod]
    public void BothOrientationsLandInDifferentRuleSets()
    {
        var library = RuleLibrary.Default;
        Assert.IsTrue(library["expand-log"].Rules.Any(r => r.Name == "alg.log.product"));
        Assert.IsFalse(library["expand-log"].Rules.Any(r => r.Name == "alg.log.product~rtl"));
        Assert.IsTrue(library["combine-log"].Rules.Any(r => r.Name == "alg.log.product~rtl"));
    }

    [TestMethod]
    public void SkippedEntriesStateWhy()
    {
        foreach (var skipped in RuleLibrary.Default.Skipped) Assert.IsFalse(string.IsNullOrWhiteSpace(skipped.Reason), skipped.Id);

        // The left-to-right direction of alg.frac.equivalent needs c, which only the right side binds.
        Assert.IsTrue(RuleLibrary.Default.Skipped.Any(s => s.Id == "alg.frac.equivalent"));
    }

    [TestMethod]
    public void AWildInTwoPlacesIsNotAnOmittedOperand()
    {
        // (a + b)*(a - b) = a^2 - b^2 must not match x*(x - 1): b cannot be an omitted operand of both the sum and the product.
        Assert.IsTrue(RuleLibrary.Default.TryGetRule("alg.poly.sum-times-diff", out var rule));
        var subject = Normalizer.Canonical(Expr.Parse("x*(x - 1)"));
        Assert.IsFalse(rule.Matches(subject, MathContext.Default).Any());
    }
}

[TestClass]
public class ReplayTests
{
    [TestMethod]
    public void ReplayRejectsATamperedStep()
    {
        var outcome = (Outcome<Expr>.Success)Simplifier.Simplify(Expr.Parse("sin(x)^2 + cos(x)^2 + y"));
        var derivation = (Derivation)outcome.Steps!;
        Assert.IsTrue(derivation.Replay(MathContext.Default, StepReplayer.Default) is Outcome<Expr>.Success);

        var steps = derivation.Steps.ToArray();
        steps[^1] = steps[^1] with { After = Normalizer.Canonical(Expr.Parse("2 + y")) };
        var tampered = derivation with { Steps = [.. steps], End = steps[^1].After };
        Assert.IsFalse(tampered.Replay(MathContext.Default, StepReplayer.Default) is Outcome<Expr>.Success);
    }

    [TestMethod]
    public void ASmallBudgetGivesAPartialResultWithItsDerivation()
    {
        var outcome = Simplifier.Simplify(Expr.Parse("(x + 1)^5 - x^5 + sin(x)^2 + cos(x)^2"), null, null, new Budget(maxSteps: 2));
        var partial = outcome as Outcome<Expr>.Partial;
        Assert.IsNotNull(partial);
        Assert.IsNotNull(partial.Steps);
    }

    [TestMethod]
    public void SimplifyNeverReturnsAMoreComplexForm()
    {
        var measure = DefaultComplexityMeasure.Instance;
        foreach (var text in new[] { "x^2 - 1", "1/x + 1/(x + 1)", "(x + 1)^4", "sin(x)^2 + cos(x)^2" })
        {
            var input = Normalizer.Canonical(Expr.Parse(text));
            var result = ((Outcome<Expr>.Success)Simplifier.Simplify(input)).Value;
            Assert.IsLessThanOrEqualTo(measure.Measure(input), measure.Measure(result), text);
        }
    }

    [TestMethod]
    public void ACustomMeasureChangesTheAnswer()
    {
        var options = new SimplifyOptions { Measure = new PreferFactored() };
        var result = ((Outcome<Expr>.Success)Simplifier.Simplify(Expr.Parse("x^2 + 5*x + 6"), null, options)).Value;
        Assert.AreEqual(Normalizer.Canonical(Expr.Parse("(x + 2)*(x + 3)")), result);
    }

    private sealed class PreferFactored : IComplexityMeasure
    {
        public double Measure(Expr expr) => (expr is Apply { Operator.Id: "add" } ? 10 : 0) + DefaultComplexityMeasure.Instance.Measure(expr);
    }
}
