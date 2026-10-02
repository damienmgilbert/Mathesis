using Mathesis.Calculus;
using Mathesis.Knowledge;
using Mathesis.Numerics.Integration;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class DefiniteIntegralTests
{
    private sealed record Case(string Text, Expr F, Expr A, Expr B, string Outcome);

    private static IEnumerable<Case> Cases() =>
        CalculusHelpers.Corpus("definite.txt").Select(line =>
        {
            var p = line.Split('|', StringSplitOptions.TrimEntries);
            return new Case(line, Expr.Parse(p[0]), Expr.Parse(p[1]), Expr.Parse(p[2]), p[3]);
        });

    private static double Bound(Expr e) => e switch
    {
        Constant { Id: ConstantId.PositiveInfinity } => double.PositiveInfinity,
        Apply { Operator.Id: "neg", Arguments: [var inner] } => -Bound(inner),
        _ => CalculusHelpers.Eval(e, [])!.Value,
    };

    [TestMethod]
    public void CorpusHasAtLeast60Cases() => Assert.IsGreaterThanOrEqualTo(60, Cases().Count());

    [TestMethod]
    public void DefiniteIntegralsMatchQuadratureAndDivergenceIsNotReturned()
    {
        var failures = new List<string>();
        var report = new List<string>();
        var catalog = KnowledgeBase.Default;
        foreach (var c in Cases())
        {
            var outcome = Integrator.IntegrateDefinite(c.F, CalculusHelpers.X, c.A, c.B);
            report.Add($"{(outcome is Outcome<Expr>.Success ? "ok" : "--")}  {c.Text}  =>  {(outcome is Outcome<Expr>.Success s0 ? s0.Value : outcome is Outcome<Expr>.Unevaluated u ? u.Reason : "")}");
            if (c.Outcome != "ok")
            {
                if (outcome is Outcome<Expr>.Success { Value: var wrong }) failures.Add($"{c.Text}: returned {wrong} for an integral marked {c.Outcome}");
                continue;
            }
            if (outcome is not Outcome<Expr>.Success { Value: var exact, Steps: Derivation derivation, Check: var check })
            {
                failures.Add($"{c.Text}: no value");
                continue;
            }
            Assert.AreEqual(Verification.NumericallyConsistent, check);
            foreach (var step in derivation.Flatten())
            {
                if (step.RuleName != "normalize" && (step.Entry is not { } id || !catalog.TryGet(id.Value, out _))) failures.Add($"{c.Text}: step {step.RuleName} cites no entry");
            }

            var reference = Quadrature.Infinite<double>(t => CalculusHelpers.Eval(c.F, new Dictionary<Symbol, double> { [CalculusHelpers.X] = t }) ?? double.NaN, Bound(c.A), Bound(c.B));
            var value = CalculusHelpers.Eval(exact, [])!.Value;
            // Integrable endpoint singularities converge slowly; the quadrature then reports its own error estimate.
            var tolerance = reference.Converged ? 1e-9 : Math.Max(1e-6, 10 * reference.ErrorEstimate);
            if (Math.Abs(reference.Value - value) > tolerance * Math.Max(1, Math.Abs(value))) failures.Add($"{c.Text}: {exact} = {value} but quadrature gives {reference.Value}");
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "definite-report.txt"), report);
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(40)));
    }

    [TestMethod]
    public void ReversedLimitsChangeTheSign()
    {
        var forward = (Outcome<Expr>.Success)Integrator.IntegrateDefinite(Expr.Parse("x^2"), CalculusHelpers.X, Expr.Parse("0"), Expr.Parse("3"));
        var backward = (Outcome<Expr>.Success)Integrator.IntegrateDefinite(Expr.Parse("x^2"), CalculusHelpers.X, Expr.Parse("3"), Expr.Parse("0"));
        Assert.AreEqual(9, CalculusHelpers.Eval(forward.Value, [])!.Value, 1e-12);
        Assert.AreEqual(-9, CalculusHelpers.Eval(backward.Value, [])!.Value, 1e-12);
    }

    [TestMethod]
    public void AJumpOfTheAntiderivativeIsSplitAtItsDiscontinuity()
    {
        // The Weierstrass antiderivative of 1/(2 + cos x) jumps at x = pi; the integral over [0, 4 pi] is 4 pi/sqrt(3).
        var outcome = (Outcome<Expr>.Success)Integrator.IntegrateDefinite(Expr.Parse("1/(2 + cos(x))"), CalculusHelpers.X, Expr.Parse("0"), Expr.Parse("4*pi"));
        Assert.AreEqual(4 * Math.PI / Math.Sqrt(3), CalculusHelpers.Eval(outcome.Value, [])!.Value, 1e-9);
    }
}
