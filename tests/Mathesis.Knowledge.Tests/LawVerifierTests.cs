using Mathesis.Knowledge.Tests.Verification;
using Mathesis.Numbers;
using Mathesis.Numerics.Integration;
using Mathesis.Symbolics;

namespace Mathesis.Knowledge.Tests;

[TestClass]
public class LawVerifierTests
{
    private static VerifyReport Verify(string entry)
    {
        var kb = KnowledgeBase.Load([("t.mlaw", "domain alg.t \"Test\"\n\n" + entry)]);
        Assert.AreEqual(0, kb.Errors.Count(), string.Join("\n", kb.Errors));
        return LawVerifier.Verify(kb.Entries[0]);
    }

    private const string Tail = "  level: Algebra1\n  course: Algebra\n";

    [TestMethod]
    public void ATrueLawPasses()
    {
        var report = Verify("law a \"A\"\n  vars: a, b\n  statement: (a + b)^2 = a^2 + 2*a*b + b^2\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome, report.Message);
        Assert.IsTrue(report.ValidSamples >= LawVerifier.MinimumSamples);
    }

    [TestMethod]
    public void AFalseLawFailsWithACounterexample()
    {
        var report = Verify("law a \"A\"\n  vars: a, b\n  statement: (a + b)^2 = a^2 + b^2\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Failed, report.Outcome);
        StringAssert.Contains(report.Message, "counterexample");
        StringAssert.Contains(report.Message, "a = ");
    }

    [TestMethod]
    public void ConditionsRestrictTheSamples()
    {
        // sqrt(a^2) = a is false for negative a, true under a >= 0.
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  vars: a\n  statement: sqrt(a^2) = a\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: a\n  statement: sqrt(a^2) = a\n  where: a >= 0\n" + Tail).Outcome);
    }

    [TestMethod]
    public void UnnecessaryConditionsAreReportedAsWarnings()
    {
        var report = Verify("law a \"A\"\n  vars: a\n  statement: a + 0 = a\n  where: a > 0\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome);
        Assert.IsTrue(report.Warnings.Any(w => w.Contains("stronger than necessary", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void NecessaryConditionsDoNotWarn()
    {
        var report = Verify("law a \"A\"\n  vars: a\n  statement: sqrt(a^2) = a\n  where: a >= 0\n" + Tail);
        Assert.AreEqual(0, report.Warnings.Length);
    }

    [TestMethod]
    public void UndefinedSamplesAreSkippedNotFailed()
    {
        // 1/a is undefined at 0 only: the law holds wherever both sides are defined.
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: a\n  statement: a*(1/a) = 1\n" + Tail).Outcome);
    }

    [TestMethod]
    public void AnUnsatisfiableConditionIsInconclusive()
    {
        var report = Verify("law a \"A\"\n  vars: a\n  statement: a = a\n  where: a > 5 and a < 4\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Inconclusive, report.Outcome);
    }

    [TestMethod]
    public void SampleHintsReachNarrowConditions()
    {
        var report = Verify("law a \"A\"\n  vars: a\n  statement: sin(arcsin(a)) = a\n  where: -1 <= a and a <= 1\n  sample: a in [-1, 1]\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome, report.Message);
    }

    [TestMethod]
    public void IntegerAndNaturalVariablesAreDrawnFromTheirSorts()
    {
        // binomial coefficients only make sense for natural n: the identity is checked for integers.
        var report = Verify("law a \"A\"\n  vars: n: natural\n  statement: sum(k, k, 0, n) = n*(n + 1)/2\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome, report.Message);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  vars: n: natural\n  statement: sum(k, k, 0, n) = n*n/2\n" + Tail).Outcome);
    }

    [TestMethod]
    public void ComplexVariablesAreEvaluatedInTheComplexField()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: z: complex\n  statement: abs(z)^2 = z*conj(z)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  vars: z: complex\n  statement: abs(z) = z\n" + Tail).Outcome);
    }

    [TestMethod]
    public void ComplexConditionsAreVerifiedWithComplexValues()
    {
        // Real mode: sqrt(a*b) = sqrt(a)*sqrt(b) for a, b >= 0. Complex mode: true when a >= 0 or b >= 0, false in general.
        var good = Verify("law a \"A\"\n  vars: a, b\n  statement: sqrt(a*b) = sqrt(a)*sqrt(b)\n  where: a >= 0 and b >= 0\n  complex: a >= 0 or b >= 0\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, good.Outcome, good.Message);
        var bad = Verify("law a \"A\"\n  vars: a, b\n  statement: sqrt(a*b) = sqrt(a)*sqrt(b)\n  where: a >= 0 and b >= 0\n  complex: true\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Failed, bad.Outcome);
        StringAssert.StartsWith(bad.Message, "complex mode:");
    }

    [TestMethod]
    public void RealOddRootsFollowTheConvention()
    {
        // conv.real-odd-root: a literal exponent p/q with odd q is a real root of a negative base.
        var report = Verify("law a \"A\"\n  vars: a\n  statement: (a^3)^(1/3) = a\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome, report.Message);
    }

    [TestMethod]
    public void ZeroToTheZeroIsOne()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: a\n  statement: a^0 = 1\n" + Tail).Outcome);
    }

    [TestMethod]
    public void IndefiniteIntegralsAreCheckedUpToAConstant()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: x\n  statement: integrate(x^2, x) = x^3/3 + 7\n  sample: x in (-2, 2)\n" + Tail).Outcome);
        var wrong = Verify("law a \"A\"\n  vars: x\n  statement: integrate(x^2, x) = x^3/2\n  sample: x in (-2, 2)\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Failed, wrong.Outcome);
    }

    [TestMethod]
    public void InfiniteSumsAreLimitsOfPartialSumsAndAreOnlyDefinedWhereTheySettle()
    {
        static double Sum(string text, double x)
        {
            var env = new Env { Values = new() { ["x"] = new Complex<double>(x) } };
            return new LawEvaluator().Eval(Expr.Parse(text), env).Real;
        }

        Assert.AreEqual(2.0, Sum("sum(x^n, n, 0, oo)", 0.5), 1e-13);
        Assert.AreEqual(Math.E, Sum("sum(x^n/factorial(n), n, 0, oo)", 1), 1e-14);
        Assert.AreEqual(Math.Sin(0.7), Sum("sum((-1)^n*x^(2*n + 1)/factorial(2*n + 1), n, 0, oo)", 0.7), 1e-14);

        // Divergent series, and series that have not settled within the term limit (terms that shrink algebraically), are undefined, never guessed.
        Assert.IsTrue(double.IsNaN(Sum("sum(x^n, n, 0, oo)", 1)), "terms that stay at 1");
        Assert.IsTrue(double.IsNaN(Sum("sum(x^n, n, 0, oo)", 1.01)), "terms that grow");
        Assert.IsTrue(double.IsNaN(Sum("sum(x^n, n, 0, oo)", -2)), "terms that grow and alternate");
        Assert.IsTrue(double.IsNaN(Sum("sum(1/n, n, 1, oo)", 0)), "the harmonic series");
        Assert.IsTrue(double.IsNaN(Sum("sum(1/n^2, n, 1, oo)", 0)), "convergent, but the tail is still about 1/N after the term limit");
        Assert.IsTrue(double.IsNaN(Sum("sum((-1)^(n + 1)/n, n, 1, oo)", 0)), "conditionally convergent");

        // A term that is undefined (here 1/0) makes the sum undefined.
        Assert.IsTrue(double.IsNaN(Sum("sum(1/(n - 3)^2, n, 0, oo)", 0)));
    }

    [TestMethod]
    public void InfiniteSeriesLawsAreCheckedWhereTheyConvergeAndFailWhenWrong()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: x\n  statement: 1/(1 - x) = sum(x^n, n, 0, oo)\n  where: abs(x) < 1\n" + Tail).Outcome);

        // Without the condition the law is still checked where the series converges (the other samples are undefined and skipped).
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: x\n  statement: 1/(1 - x) = sum(x^n, n, 0, oo)\n" + Tail).Outcome);

        // A series that never converges verifies nothing, and says so.
        Assert.AreEqual(VerifyOutcome.Inconclusive, Verify("law a \"A\"\n  vars: x\n  statement: x = sum(1, n, 0, oo)\n" + Tail).Outcome);
    }

    [TestMethod]
    public void WrongMaclaurinSeriesAreRejected()
    {
        // Each case is a catalog series (knowledge/calc/series.mlaw) with one plausible mistake.
        (string Vars, string Statement, string? Where)[] wrong =
        [
            ("x", "e^x = sum(x^n/factorial(n + 1), n, 0, oo)", null),
            ("x", "sin(x) = sum(x^(2*n + 1)/factorial(2*n + 1), n, 0, oo)", null),
            ("x", "sin(x) = sum((-1)^n*x^(2*n + 1)/factorial(2*n), n, 0, oo)", null),
            ("x", "cos(x) = sum((-1)^n*x^(2*n)/factorial(2*n + 1), n, 0, oo)", null),
            ("x", "1/(1 - x) = sum(x^n, n, 1, oo)", "abs(x) < 1"),
            ("x", "ln(1 + x) = sum((-1)^n*x^n/n, n, 1, oo)", "x > -1 and x <= 1"),
            ("x", "arctan(x) = sum((-1)^n*x^(2*n + 1)/(2*n), n, 1, oo)", "abs(x) <= 1"),
            ("x, k", "(1 + x)^k = sum(binomial(k, n + 1)*x^n, n, 0, oo)", "abs(x) < 1"),
            ("x", "sinh(x) = sum((-1)^n*x^(2*n + 1)/factorial(2*n + 1), n, 0, oo)", null),
            ("x", "cosh(x) = sum(x^(2*n)/factorial(2*n + 1), n, 0, oo)", null),
        ];
        foreach (var (vars, statement, where) in wrong)
        {
            var report = Verify($"law a \"A\"\n  vars: {vars}\n  statement: {statement}\n" + (where is null ? "" : $"  where: {where}\n") + Tail);
            Assert.AreEqual(VerifyOutcome.Failed, report.Outcome, $"{statement}: {report.Outcome} {report.Message}");
        }
    }

    [TestMethod]
    public void IntegralsWithBoundsAreEvaluatedWithTheIntegrandOfTheirVariable()
    {
        // f is evaluated at the integration variable x, so a wrong law fails (with f taken at a fixed point both integrals below would be f(a)·(b − a)).
        const string Vars = "  vars: f: function(R -> R), a, b, x\n";
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n" + Vars + "  statement: integrate(f, x, b, a) = -integrate(f, x, a, b)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n" + Vars + "  statement: integrate(f, x, b, a) = integrate(f, x, a, b)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n" + Vars + "  statement: integrate(f, x, a, a) = 0\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n" + Vars + "  statement: integrate(f, x, a, b) = 0\n" + Tail).Outcome);
    }

    [TestMethod]
    public void TheLogarithmicIntegralMatchesItsKnownValuesAndItsDerivative()
    {
        // li(2) and the Ramanujan-Soldner constant μ (li(μ) = 0) from the reference tables (DLMF 6.2(ii), Abramowitz and Stegun 5.1).
        Assert.AreEqual(1.0451637801174927848, SpecialFunctions.Li(2), 1e-14);
        Assert.AreEqual(0.0, SpecialFunctions.Li(1.4513692348833810503), 1e-14);
        foreach (var x in new[] { 0, 1, -3, double.PositiveInfinity, double.NaN, 1e-20, 1e20 }) Assert.IsTrue(double.IsNaN(SpecialFunctions.Li(x)), $"li({x}) is undefined here");

        // li' = 1/ln: differences of li equal the integral of 1/ln t over intervals that avoid the pole at 1, on either side of it.
        foreach (var (a, b) in new[] { (2.0, 10.0), (1.01, 1.5), (30.0, 31.0), (0.1, 0.9), (0.3, 0.97), (0.0001, 0.5) })
        {
            var integral = Quadrature.GaussKronrod<double>(t => 1 / Math.Log(t), a, b);
            Assert.IsTrue(integral.Converged);
            Assert.AreEqual(integral.Value, SpecialFunctions.Li(b) - SpecialFunctions.Li(a), 1e-9 * Math.Max(1, Math.Abs(integral.Value)), $"li({b}) - li({a})");
        }
    }

    [TestMethod]
    public void DerivativesAreCheckedNumerically()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: x\n  statement: diff(sin(x)*x, x) = cos(x)*x + sin(x)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  vars: x\n  statement: diff(sin(x)*x, x) = cos(x)*x\n" + Tail).Outcome);
    }

    [TestMethod]
    public void LimitsAreCheckedNumerically()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  statement: limit(sin(x)/x, x, 0) = 1\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  statement: limit(sin(x)/x, x, 0) = 2\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  statement: limit((1 + 1/n)^n, n, oo) = e\n" + Tail).Outcome);
    }

    [TestMethod]
    public void FunctionVariablesAreReplacedByConcreteFunctions()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: f: function(R -> R), g: function(R -> R), x\n  statement: diff(f + g, x) = diff(f, x) + diff(g, x)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  vars: f: function(R -> R), g: function(R -> R), x\n  statement: diff(f*g, x) = diff(f, x)*diff(g, x)\n" + Tail).Outcome);
    }

    [TestMethod]
    public void MatricesAreSampledAndMultiplied()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: n: natural, A: matrix(n, n), B: matrix(n, n)\n  statement: det(A*B) = det(A)*det(B)\n  sample: n in 1..4\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("law a \"A\"\n  vars: n: natural, A: matrix(n, n), B: matrix(n, n)\n  statement: det(A + B) = det(A) + det(B)\n  sample: n in 2..4\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Passed, Verify("law a \"A\"\n  vars: n: natural, A: matrix(n, n)\n  statement: A*adj(A) = det(A)*identity(n)\n  sample: n in 1..4\n" + Tail).Outcome);
    }

    [TestMethod]
    public void DefiningEquationsOfFormulasAreSolvedBack()
    {
        // y = k*x is a definition of y: the verifier computes y from the right-hand side and checks the conditions can hold.
        var report = Verify("formula a \"A\"\n  vars: y, k, x\n  statement: y = k*x\n  where: k != 0\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome, report.Message);
        var undefined = Verify("formula a \"A\"\n  vars: y, x\n  statement: y = sqrt(x) + ln(0 - x^2)\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Inconclusive, undefined.Outcome, "a right-hand side that is nowhere defined cannot be verified");
    }

    [TestMethod]
    public void PatternsAndMethodsAreCheckedForEquivalence()
    {
        Assert.AreEqual(VerifyOutcome.Passed, Verify("pattern a \"A\"\n  vars: a, b\n  match: a^2 - b^2\n  yields: (a - b)*(a + b)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("pattern a \"A\"\n  vars: a, b\n  match: a^2 - b^2\n  yields: (a - b)^2\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Passed, Verify("method a \"A\"\n  vars: a, x\n  applies-to: a*x + a\n  steps: \"factor\" a*(x + 1)\n  result: a*(1 + x)\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Failed, Verify("method a \"A\"\n  vars: a, x\n  applies-to: a*x + a\n  steps: \"factor\" a*(x + 2)\n  result: a*(x + 2)\n" + Tail).Outcome);
    }

    [TestMethod]
    public void EntriesThatAreNotVerifiedAreSkipped()
    {
        Assert.AreEqual(VerifyOutcome.Skipped, Verify("theorem a \"A\"\n  explain: \"Some theorem.\"\n" + Tail).Outcome);
        Assert.AreEqual(VerifyOutcome.Skipped, Verify("law a \"A\"\n  vars: x\n  statement: x = x\n  verify: none\n  rationale: \"Because.\"\n" + Tail).Outcome);
    }

    [TestMethod]
    public void TheoremsWithInstancesAreChecked()
    {
        var report = Verify("theorem a \"A\"\n  vars: a, b, c, d\n  statement: det([[a, b], [c, d]]) = a*d - b*c\n  verify: instances\n" + Tail);
        Assert.AreEqual(VerifyOutcome.Passed, report.Outcome, report.Message);
    }

    [TestMethod]
    public void TheVerifierIsDeterministic()
    {
        var entry = "law a \"A\"\n  vars: a, b\n  statement: (a + b)^2 = a^2 + b^2\n" + Tail;
        Assert.AreEqual(Verify(entry).Message, Verify(entry).Message);
    }
}
