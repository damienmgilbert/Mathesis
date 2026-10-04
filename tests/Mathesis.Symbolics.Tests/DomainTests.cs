using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Canonical;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class DomainTests
{
    private static readonly Symbol X = Symbol("x");

    private static string Domain(string expression, MathContext? context = null)
    {
        var outcome = NaturalDomain.Of(Expr.Parse(expression), X, context);
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(outcome, $"{expression}: {outcome}");
        return Normalizer.Canonical(((Outcome<Expr>.Success)outcome).Value).ToString();
    }

    [TestMethod]
    public void DomainsMatchTheHandComputedAnswers()
    {
        var cases = Corpus.RawLines("domains.txt").Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        Assert.IsTrue(cases.Count >= 100, $"only {cases.Count} cases");
        var failures = new List<string>();
        foreach (var line in cases)
        {
            var arrow = line.IndexOf(" ==> ", StringComparison.Ordinal);
            Assert.IsTrue(arrow > 0, $"bad line '{line}'");
            var expression = line[..arrow];
            var expected = Normalizer.Canonical(Expr.Parse(line[(arrow + 5)..])).ToString();
            var actual = Domain(expression);
            if (actual != expected) failures.Add($"{expression}\n    expected {expected}\n    actual   {actual}");
        }
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void ConditionsOnParametersUseTheAssumptions()
    {
        var a = Symbol("a");
        var positive = MathContext.Default.Assume(Gt(a, Number(0)));
        Assert.AreEqual("R", Domain("sqrt(a) + x", positive));
        Assert.AreEqual("R", Domain("ln(a)*x", positive));
        Assert.AreEqual("EmptySet", Domain("sqrt(-a) + x", positive));

        // Without a decision the condition on the parameter is kept.
        var unknown = Domain("sqrt(a) + x");
        StringAssert.Contains(unknown, "a >= 0");
        Assert.AreEqual("R", Domain("1/a + x", MathContext.Default.Assume(Ne(a, Number(0)))));
    }

    [TestMethod]
    public void UnsupportedOperatorsFailInsteadOfGuessing()
    {
        var outcome = NaturalDomain.Of(Expr.Parse("gamma(x)"), X);
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(outcome);
        Assert.AreEqual(MathErrorKind.Unsupported, ((Outcome<Expr>.Failed)outcome).Error.Kind);
    }

    [TestMethod]
    public void TheBudgetIsHonored()
    {
        var outcome = NaturalDomain.Of(Expr.Parse("1/x + 1/(x - 1) + 1/(x - 2)"), X, budget: new Budget(maxSteps: 1));
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(outcome);
    }

    [TestMethod]
    public void RootsOfHighDegreeFactorsAreNotGuessed()
    {
        // x^3 - 2 has no rational root and no quadratic factor: the answer is a condition set, not a wrong interval.
        var result = Domain("1/(x^3 - 2)");
        StringAssert.Contains(result, "-2 + x^3 != 0");
    }

    [TestMethod]
    public void DomainsAreSoundAtSamplePoints()
    {
        // At every sample point the expression is either defined (finite value) and inside the domain, or outside it.
        var expressions = new[] { "1/(x^2 - 1)", "sqrt(x^2 - 4)", "ln(x - 1)", "sqrt(9 - x^2)", "sqrt((x-1)/(x+2))", "1/sqrt(x)", "ln(4 - x^2)/(x - 1)", "sqrt(x)/(x - 2)" };
        foreach (var text in expressions)
        {
            var expr = Expr.Parse(text);
            var domain = ((Outcome<Expr>.Success)NaturalDomain.Of(expr, X)).Value;
            for (var i = -80; i <= 80; i++)
            {
                var x = i / 10.0 + 0.013;
                var defined = double.IsFinite(ExprGen.Evaluate(expr, new Dictionary<string, double> { ["x"] = x }));
                var inside = Contains(domain, x);
                Assert.AreEqual(defined, inside, $"{text} at x = {x}: domain {domain}");
            }
        }
    }

    private static bool Contains(Expr set, double x)
    {
        switch (set)
        {
            case Constant { Id: ConstantId.Reals }: return true;
            case IntervalLiteral i:
                var lo = ExprGen.Evaluate(i.Lower, new Dictionary<string, double>());
                var hi = ExprGen.Evaluate(i.Upper, new Dictionary<string, double>());
                return (i.LowerClosed ? x >= lo : x > lo) && (i.UpperClosed ? x <= hi : x < hi);
            case SetLiteral s: return s.Elements.Any(e => Math.Abs(ExprGen.Evaluate(e, new Dictionary<string, double>()) - x) < 1e-12);
            case Apply { Operator.Id: "union" } u: return u.Arguments.Any(a => Contains(a, x));
            default: return false;
        }
    }
}
