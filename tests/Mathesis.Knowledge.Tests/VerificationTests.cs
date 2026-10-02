using Mathesis.Knowledge.Tests.Verification;

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
            var report = LawVerifier.Verify(e);
            lines.Add($"{e.Id}: {report.Outcome} ({report.ValidSamples} samples) {report.Message}");
        }

        // Failing on purpose makes the report visible in the test output.
        Assert.Fail(string.Join(Environment.NewLine, lines));
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
