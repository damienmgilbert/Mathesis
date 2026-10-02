using System.Diagnostics;
using Mathesis.Calculus;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class IntegralTests
{
    private static IEnumerable<(bool Expected, string Text)> Cases() =>
        CalculusHelpers.Corpus("integrals.txt").Select(l => (l[0] == '+', l[2..]));

    [TestMethod]
    public void CorpusHasAtLeast100IntegrableCases() => Assert.IsGreaterThanOrEqualTo(100, Cases().Count(c => c.Expected));

    [TestMethod]
    public void EveryReturnedAntiderivativeDifferentiatesBackAndTheCorpusIsCovered()
    {
        var failures = new List<string>();
        var report = new List<string>();
        var catalog = Mathesis.Knowledge.KnowledgeBase.Default;
        foreach (var (expected, text) in Cases())
        {
            var (f, math, facts) = CalculusHelpers.ParseCase(text);
            var watch = Stopwatch.StartNew();
            var outcome = Integrator.Integrate(f, CalculusHelpers.X, math);
            report.Add($"{(outcome is Outcome<Expr>.Success ? "ok " : "-- ")} {watch.ElapsedMilliseconds,5}ms  {text}  =>  {(outcome is Outcome<Expr>.Success s0 ? Mathesis.Symbolics.Printing.TextPrinter.Print(s0.Value) + "   {" + s0.Provisos + "}" : "")}");
            if (outcome is not Outcome<Expr>.Success { Value: var antiderivative, Steps: Derivation derivation } success)
            {
                if (expected) failures.Add($"{text}: expected an antiderivative, got {outcome.GetType().Name}");
                continue;
            }
            if (!expected) failures.Add($"{text}: marked as out of reach but integrated to {antiderivative}");
            Assert.AreEqual(Verification.Verified, success.Check);

            if (derivation.Replay(math, StepReplayer.Default) is not Outcome<Expr>.Success { Value: var replayed } || !replayed.Equals(antiderivative)) failures.Add($"{text}: replay differs");
            foreach (var step in derivation.Flatten())
            {
                if (step.RuleName != "normalize" && (step.Entry is not { } id || !catalog.TryGet(id.Value, out _))) failures.Add($"{text}: step {step.RuleName} cites no entry");
            }

            // F' = f at seeded points where f, F and the provisos are defined.
            var symbols = f.FreeSymbols.Union(antiderivative.FreeSymbols).Union([CalculusHelpers.X]).Union(facts.SelectMany(p => p.FreeSymbols)).OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
            var random = new Random(CalculusHelpers.StableSeed(text));
            var provisos = success.Provisos.OfType<Expr>().ToArray();
            var agreed = 0;
            for (var attempt = 0; attempt < 800 && agreed < 12; attempt++)
            {
                var values = symbols.ToDictionary(s => s, _ => Math.Round((random.NextDouble() * 8 - 4) * 16) / 16 + 0.03125);
                if (provisos.Concat(facts).Any(p => CalculusHelpers.Holds(p, values) != true)) continue;
                if (CalculusHelpers.Eval(f, values) is not { } integrand || CalculusHelpers.NumericDerivative(antiderivative, CalculusHelpers.X, values) is not { } slope) continue;
                if (Math.Abs(integrand - slope) > 1e-5 * Math.Max(1, Math.Abs(integrand)))
                {
                    failures.Add($"{text}: F = {antiderivative} has slope {slope}, the integrand is {integrand} at {string.Join(", ", values.Select(kv => $"{kv.Key.Name}={kv.Value}"))}");
                    break;
                }
                agreed++;
            }
            if (agreed < 4 && !failures.Any(m => m.StartsWith(text + ":", StringComparison.Ordinal))) failures.Add($"{text}: only {agreed} usable points for the check");
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "integral-report.txt"), report);
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "integral-failures.txt"), failures);
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(30)));
    }
}

[TestClass]
public class IntegratorBehaviorTests
{
    private static readonly Symbol X = CalculusHelpers.X;

    [TestMethod]
    public void ProvisosAreStatedInTheOriginalVariable()
    {
        // The substitution u = x^2 + 1 needs u != 0; the proviso must be about x^2 + 1, not about the internal variable.
        var success = (Outcome<Expr>.Success)Integrator.Integrate(Expr.Parse("x/(x^2 + 1)^2"), X);
        foreach (var proviso in success.Provisos.OfType<Expr>()) Assert.IsFalse(proviso.FreeSymbols.Any(s => s.Name != "x"), proviso.ToString());
    }

    [TestMethod]
    public void AWrongAntiderivativeFailsTheDifferentiateBackCheck()
    {
        var f = Expr.Parse("2*x");
        Assert.IsTrue(Integrator.Verify(Expr.Parse("x^2"), f, X, Provisos.None));
        Assert.IsTrue(Integrator.Verify(Expr.Parse("x^2 + 7"), f, X, Provisos.None), "a constant of integration does not matter");
        Assert.IsFalse(Integrator.Verify(Expr.Parse("x^2 + x"), f, X, Provisos.None));
        Assert.IsFalse(Integrator.Verify(Expr.Parse("2*x^2"), f, X, Provisos.None));
    }

    [TestMethod]
    public void NonElementaryIntegrandsStayUnevaluated()
    {
        foreach (var text in new[] { "exp(-x^2)", "sin(x)/x", "exp(x)/x" })
        {
            Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Integrator.Integrate(Expr.Parse(text), X), text);
        }
    }

    [TestMethod]
    public void AnIntegrationByPartsStepNestsItsSubIntegrals()
    {
        var success = (Outcome<Expr>.Success)Integrator.Integrate(Expr.Parse("x*exp(x)"), X);
        var steps = ((Derivation)success.Steps!).Flatten().Where(s => s.RuleName != "normalize").ToList();
        // Either the catalog table (x*e^x) or integration by parts with its sub-integrals.
        Assert.IsTrue(steps.Count >= 1);
        var parts = (Outcome<Expr>.Success)Integrator.Integrate(Expr.Parse("x*cos(x)"), X);
        var top = ((Derivation)parts.Steps!).Steps.Last();
        Assert.AreEqual("calc.int.parts", top.Entry!.Value.Value);
        Assert.IsNotNull(top.Substeps);
        Assert.IsGreaterThanOrEqualTo(2, top.Substeps!.Steps.Length);
    }

    [TestMethod]
    public void TheLevelFiltersWhatTheTableMayCite()
    {
        // Catalog entries above the level are not used, so a low level cannot integrate with them.
        var low = new MathContext { Level = Mathesis.Knowledge.CurriculumLevel.PreAlgebra };
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Integrator.Integrate(Expr.Parse("sin(x)"), X, low));
        var high = new MathContext { Level = Mathesis.Knowledge.CurriculumLevel.Calculus2 };
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(Integrator.Integrate(Expr.Parse("sin(x)"), X, high));
    }

    [TestMethod]
    public void LinearityThroughConstantsAndSums()
    {
        var success = (Outcome<Expr>.Success)Integrator.Integrate(Expr.Parse("3*x^2 + 5*cos(x)"), X);
        Assert.AreEqual("calc.int.linearity", ((Derivation)success.Steps!).Steps.Last().Entry!.Value.Value);
        Assert.AreEqual(Verification.Verified, success.Check);
    }
}
