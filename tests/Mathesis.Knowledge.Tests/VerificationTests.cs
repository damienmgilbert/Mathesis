using Mathesis.Knowledge.Tests.Verification;
using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace Mathesis.Knowledge.Tests;

[TestClass]
public class VerificationTests
{
    // MATHESIS_SEED=n draws different samples: a way to check that no entry passes only by luck.
    private static int Seed => int.TryParse(Environment.GetEnvironmentVariable("MATHESIS_SEED"), out var seed) ? seed : LawVerifier.DefaultSeed;

    [TestMethod]
    public void EveryLawAndFormulaPassesNumericVerification()
    {
        
        var failures = new List<string>();
        var warnings = new List<string>();
        var passed = 0;
        foreach (var e in KnowledgeBase.Default.Entries)
        {
            var report = LawVerifier.Verify(e, Seed);
            warnings.AddRange(report.Warnings);
            switch (report.Outcome)
            {
                case VerifyOutcome.Passed:
                    passed++;
                    break;
                case VerifyOutcome.Failed or VerifyOutcome.Inconclusive:
                    failures.Add($"{e.Id} [{report.Outcome}]: {report.Message}");
                    break;
            }
        }
        foreach (var w in warnings) Console.WriteLine("warning: " + w);
        Assert.AreEqual(0, failures.Count, $"{failures.Count} entries did not verify ({passed} passed):\n" + string.Join("\n", failures));
        Assert.IsTrue(passed >= 250, $"only {passed} entries were numerically verified");
    }

    // MATHESIS_VERIFY=alg.exp.rational runs one entry (any ID or prefix) and prints its report: a debugging aid for catalog authors.
    [TestMethod]
    public void VerifyTheEntriesNamedByTheEnvironment()
    {
        var names = Environment.GetEnvironmentVariable("MATHESIS_VERIFY");
        if (string.IsNullOrWhiteSpace(names)) return;
        var lines = new List<string>();
        foreach (var e in KnowledgeBase.Default.Entries.Where(e => names.Split(',').Any(n => e.Id.Value.StartsWith(n.Trim(), StringComparison.Ordinal))))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var report = LawVerifier.Verify(e);
            lines.Add($"{e.Id}: {report.Outcome} ({report.ValidSamples} samples, {watch.ElapsedMilliseconds} ms) {report.Message}");
        }

        // Failing on purpose makes the report visible in the test output.
        Assert.Fail(string.Join(Environment.NewLine, lines));
    }

    // These laws were once `verify: none` because the verifier could not evaluate infinite sums, integrals with bounds or li. Each is now checked
    // numerically by EveryLawAndFormulaPassesNumericVerification; this guards against one being switched back without a reason.
    [TestMethod]
    public void TheSeriesAndIntegralLawsAreNumericallyVerified()
    {
        string[] ids =
        [
            "calc.mac.exp", "calc.mac.sin", "calc.mac.cos", "calc.mac.geometric", "calc.mac.ln", "calc.mac.arctan", "calc.mac.binomial", "calc.mac.sinh", "calc.mac.cosh",
            "calc.def.zero-width", "calc.def.reverse", "calc.itab.inv-log",
        ];
        foreach (var id in ids) Assert.AreEqual(VerifyMode.Numeric, KnowledgeBase.Default.Get(id).EffectiveVerify, id);
    }

    // The sampler cannot reach the endpoints where a series converges conditionally (the partial sums settle like 1/n), so the verifier treats those
    // samples as undefined. The endpoints named by `where` are checked here with the alternating-series bound |S − S_N| ≤ |a_(N+1)|, which holds because
    // the terms alternate in sign and decrease to 0 in magnitude (1/n at x = 1 for ln(1 + x); 1/(2n + 1) at x = ±1 for arctan).
    [TestMethod]
    public void TheEndpointsOfTheLogAndArctanSeriesAreWithinTheAlternatingSeriesBound()
    {
        const int N = 100_000;
        foreach (var (id, x, firstOmittedTerm) in new[] { ("calc.mac.ln", 1.0, 1.0 / (N + 1)), ("calc.mac.arctan", 1.0, 1.0 / (2 * N + 3)), ("calc.mac.arctan", -1.0, 1.0 / (2 * N + 3)) })
        {
            var entry = KnowledgeBase.Default.Get(id);
            var statement = (Apply)entry.Statement!;
            var series = (Bind)statement.Arguments[1];
            var truncated = new Bind(Binder.Sum, series.Bound, [series.Data[0], new Number(new BigRational(N))], series.Body);
            var evaluator = new LawEvaluator();
            var env = new Env { Values = new() { ["x"] = new Complex<double>(x) } };
            Assert.IsTrue(evaluator.Truth(entry.Where!, env), $"{id}: x = {x} satisfies the conditions");
            var exact = evaluator.Eval(statement.Arguments[0], env).Real;
            var partial = evaluator.Eval(truncated, env).Real;
            var error = Math.Abs(exact - partial);

            // 1e-12 is the rounding error of adding 100,000 terms in double; the bound itself is about 1e-5.
            Assert.IsTrue(error <= firstOmittedTerm + 1e-12, $"{id} at x = {x}: |{exact} − S_{N}| = {error} exceeds {firstOmittedTerm}");
        }
    }

    [TestMethod]
    public void EveryLawAndFormulaIsEitherVerifiedOrSaysWhyNot()
    {
        var unexplained = KnowledgeBase.Default.Entries
            .Where(e => e.Kind is EntryKind.Law or EntryKind.Formula && e.EffectiveVerify != VerifyMode.Numeric && string.IsNullOrWhiteSpace(e.Rationale))
            .Select(e => e.Id.Value)
            .ToList();
        Assert.AreEqual(0, unexplained.Count, string.Join(", ", unexplained));
    }
}
