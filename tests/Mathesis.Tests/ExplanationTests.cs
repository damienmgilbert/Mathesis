using Mathesis.Explanation;
using Mathesis.Knowledge;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

/// <summary>
/// Explanation snapshots live in tests/corpus/explain. A snapshot is approved by reading it and committing it; set <c>MATHESIS_APPROVE=1</c>
/// to rewrite the files from the current output, then review the diff.
/// </summary>
[TestClass]
public class ExplanationTests
{
    private static Derivation Derive(string text, CurriculumLevel? level = null, params string[] facts)
    {
        var math = new MathContext { Level = level };
        foreach (var fact in facts) math = math.Assume(Expr.Parse(fact));
        var outcome = Simplifier.Simplify(Expr.Parse(text), math);
        return (Derivation)((Outcome<Expr>.Success)outcome).Steps!;
    }

    private static string SnapshotDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "corpus"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("tests/corpus"), "tests", "corpus", "explain");
    }

    private static void Verify(string name, string actual)
    {
        var path = Path.Combine(SnapshotDirectory(), name);
        if (Environment.GetEnvironmentVariable("MATHESIS_APPROVE") == "1")
        {
            Directory.CreateDirectory(SnapshotDirectory());
            File.WriteAllText(path, actual.ReplaceLineEndings("\n"));
            return;
        }
        Assert.IsTrue(File.Exists(path), $"The snapshot {name} is missing; run with MATHESIS_APPROVE=1 and review it.");
        Assert.AreEqual(File.ReadAllText(path).ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"), $"The explanation differs from the approved snapshot {name}.");
    }

    [TestMethod]
    public void FactorQuadraticAsText() =>
        Verify("factor-quadratic.text.txt", ExplanationRenderer.Render(Derive("x^2 + 5*x + 6")));

    [TestMethod]
    public void CancelAsMarkdown() =>
        Verify("cancel.markdown.txt", ExplanationRenderer.Render(Derive("(x^2 - 1)/(x - 1)"), new ExplainOptions { Format = ExplanationFormat.Markdown }));

    [TestMethod]
    public void ExpandIdentityDetailed() =>
        Verify("expand-identity.detailed.txt", ExplanationRenderer.Render(Derive("(x + 1)^2 - x^2 - 2*x"), new ExplainOptions { Verbosity = Verbosity.Detailed }));

    [TestMethod]
    public void PythagoreanAsLatex() =>
        Verify("pythagorean.latex.txt", ExplanationRenderer.Render(Derive("sin(x)^2 + cos(x)^2 + y"), new ExplainOptions { Format = ExplanationFormat.Latex }));

    [TestMethod]
    public void LogarithmUnderAssumptionsBrief() =>
        Verify("log-combine.brief.txt", ExplanationRenderer.Render(Derive("log(x, 2) + log(y, 2)", null, "x > 0", "y > 0"), new ExplainOptions { Verbosity = Verbosity.Brief }));

    [TestMethod]
    public void LevelFilterFlagsStepsAboveTheLevel()
    {
        var derivation = Derive("sin(x)^2 + cos(x)^2 + y");
        var text = ExplanationRenderer.Render(derivation, new ExplainOptions { Level = CurriculumLevel.Arithmetic });
        StringAssert.Contains(text, "[above the selected level]");
        Assert.DoesNotContain("[above the selected level]", ExplanationRenderer.Render(derivation, new ExplainOptions { Level = CurriculumLevel.University }));
    }

    [TestMethod]
    public void SimplifierWithALevelNeverCitesEntriesAboveIt()
    {
        var catalog = KnowledgeBase.Default;
        foreach (var text in new[] { "(x^2 - 1)/(x - 1)", "x^2 + 5*x + 6", "(x + 1)^2 - x^2 - 2*x", "sin(x)^2 + cos(x)^2 + y", "1/x + 1/x", "(x^3 - 1)/(x - 1)" })
        {
            var derivation = Derive(text, CurriculumLevel.Algebra1);
            foreach (var step in derivation.Flatten().Where(s => s.Entry is not null))
            {
                Assert.IsLessThanOrEqualTo((int)CurriculumLevel.Algebra1, (int)(catalog.Get(step.Entry!.Value).Level ?? CurriculumLevel.University), $"{text}: {step.RuleName}");
            }
        }
    }
}
