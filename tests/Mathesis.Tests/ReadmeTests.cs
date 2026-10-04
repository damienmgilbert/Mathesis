using System.Text;
using Mathesis.Calculus;
using Mathesis.Explanation;
using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Solving;
using Mathesis.Symbolics;

namespace Mathesis.Tests;

/// <summary>
/// Runs the examples of README.md. Each lambda is the code of one example (the README shows it with Console.WriteLine instead of the writer); the output
/// must equal the text block that follows the code in the README. Set MATHESIS_APPROVE=1 to print the outputs to the temp directory (readme-examples.txt).
/// </summary>
[TestClass]
public class ReadmeTests
{
    private static readonly (string Title, Action<StringBuilder> Run)[] Examples =
    [
        ("Parse and print", o =>
        {
            var e = Expr.Parse("(x + 1)^2 / (x - 1)");
            o.AppendLine(e.ToString());
            o.AppendLine(e.ToLatex());
        }),
        ("Build with C# and evaluate", o =>
        {
            var x = Sym.Symbol("x");
            Expr f = Sym.Sin(x) * Sym.Sin(x) + Sym.Cos(x) * Sym.Cos(x) + Sym.Number(3);
            var at = new Dictionary<Symbol, Expr> { [x] = Sym.Number(2) };
            o.AppendLine(f.ToString());
            o.AppendLine(Cas.Evaluate(Expr.Parse("x^2 + 1/3"), at) is Outcome<Expr>.Success { Value: var v } ? v.ToString() : "?");
            o.AppendLine(Cas.N(Expr.Parse("sqrt(2)")) is Outcome<double>.Success { Value: var d } ? d.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "?");
        }),
        ("Simplify with steps", o =>
        {
            var outcome = (Outcome<Expr>.Success)Cas.Simplify(Expr.Parse("sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)"));
            o.AppendLine(outcome.Steps.Render(ExplanationFormat.Text, Verbosity.Standard));
            o.AppendLine("provisos: " + outcome.Provisos);
        }),
        ("Expand and factor", o =>
        {
            var x = Sym.Symbol("x");
            o.AppendLine(((Outcome<Expr>.Success)Cas.Expand(Expr.Parse("(x + 1)^3 - (x - 1)^3"))).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.Factor(Expr.Parse("x^3 - 6*x^2 + 11*x - 6"))).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.CompleteSquare(Expr.Parse("x^2 + 4*x + 7"), x)).Value.ToString());
        }),
        ("Together, apart and cancel", o =>
        {
            var x = Sym.Symbol("x");
            o.AppendLine(((Outcome<Expr>.Success)Cas.Together(Expr.Parse("1/x + 1/(x + 1)"))).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.Apart(Expr.Parse("(x + 3)/((x + 1)*(x + 2))"), x)).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.Cancel(Expr.Parse("(x^2 - 4)/(x^2 - 5*x + 6)"))).Value.ToString());
        }),
        ("Trigonometric and logarithmic forms", o =>
        {
            var positive = new MathContext().Assume(Expr.Parse("x > 0")).Assume(Expr.Parse("y > 0"));
            o.AppendLine(((Outcome<Expr>.Success)Cas.TrigExpand(Expr.Parse("sin(2*x)"))).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.TrigReduce(Expr.Parse("sin(x)^2"))).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.LogCombine(Expr.Parse("ln(x) + ln(y)"), positive)).Value.ToString());
        }),
        ("Differentiate with steps", o =>
        {
            var x = Sym.Symbol("x");
            var outcome = (Outcome<Expr>.Success)Cas.Differentiate(Expr.Parse("sin(x^2)"), x);
            o.AppendLine(outcome.Steps.Render(ExplanationFormat.Text, Verbosity.Standard));
        }),
        ("Implicit and higher derivatives", o =>
        {
            var x = Sym.Symbol("x");
            var y = Sym.Symbol("y");
            o.AppendLine(((Outcome<Expr>.Success)Cas.ImplicitDerivative(Expr.Parse("x^2 + y^2 = 25"), y, x)).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.Differentiate(Expr.Parse("x^5"), x, 3)).Value.ToString());
        }),
        ("Integrate, checked by differentiating back", o =>
        {
            var x = Sym.Symbol("x");
            var outcome = (Outcome<Expr>.Success)Cas.Integrate(Expr.Parse("x*cos(x)"), x);
            o.AppendLine(outcome.Value.ToString());
            o.AppendLine(outcome.Check.ToString());
            o.AppendLine(outcome.Steps.Render(ExplanationFormat.Markdown, Verbosity.Brief));
        }),
        ("Definite and improper integrals", o =>
        {
            var x = Sym.Symbol("x");
            o.AppendLine(((Outcome<Expr>.Success)Cas.Integrate(Expr.Parse("x^2"), x, Expr.Parse("0"), Expr.Parse("3"))).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.Integrate(Expr.Parse("1/(x^2 + 1)"), x, Expr.Parse("-oo"), Expr.Parse("oo"))).Value.ToString());
            o.AppendLine(Cas.Integrate(Expr.Parse("1/x"), x, Expr.Parse("0"), Expr.Parse("1")) is Outcome<Expr>.Unevaluated u ? "diverges: " + u.Reason : "?");
        }),
        ("Limits", o =>
        {
            var x = Sym.Symbol("x");
            o.AppendLine(Show(Cas.Limit(Expr.Parse("sin(x)/x"), x, Expr.Parse("0"))));
            o.AppendLine(Show(Cas.Limit(Expr.Parse("(1 + 1/x)^x"), x, Expr.Parse("oo"))));
            o.AppendLine(Show(Cas.Limit(Expr.Parse("1/x"), x, Expr.Parse("0"), LimitDirection.FromRight)));
            o.AppendLine(Show(Cas.Limit(Expr.Parse("1/x"), x, Expr.Parse("0"))));
        }),
        ("Taylor and Laurent series", o =>
        {
            var x = Sym.Symbol("x");
            o.AppendLine(((Outcome<Expr>.Success)Cas.Taylor(Expr.Parse("exp(x)"), x, Expr.Parse("0"), 4)).Value.ToString());
            o.AppendLine(((Outcome<Expr>.Success)Cas.Series(Expr.Parse("1/(x*(1 - x))"), x, Expr.Parse("0"), 2)).Value.ToString());
        }),
        ("Solve an equation, with an extraneous solution", o =>
        {
            var x = Sym.Symbol("x");
            var outcome = (Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("sqrt(x + 2) = x"), x);
            o.AppendLine(outcome.Value.ToString());
            o.AppendLine(outcome.Steps.Render(ExplanationFormat.Text, Verbosity.Brief));
        }),
        ("Trigonometric equations and inequalities", o =>
        {
            var x = Sym.Symbol("x");
            o.AppendLine(((Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("2*sin(x)^2 - sin(x) - 1 = 0"), x)).Value.ToString());
            o.AppendLine(((Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("(x - 1)/(x + 2) >= 0"), x)).Value.ToString());
            o.AppendLine(((Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("x^5 - x - 1 = 0"), x)).Value.ToString());
        }),
        ("Systems and exact linear algebra", o =>
        {
            var x = Sym.Symbol("x");
            var y = Sym.Symbol("y");
            o.AppendLine(((Outcome<SolutionSet>.Success)Cas.Solve([Expr.Parse("x + y = 3"), Expr.Parse("x - y = 1")], [x, y])).Value.ToString());
            o.AppendLine(((Outcome<SolutionSet>.Success)Cas.Solve([Expr.Parse("x^2 + y^2 = 25"), Expr.Parse("x + y = 7")], [x, y])).Value.ToString());
            var a = DenseMatrix.Create(2, 2, (r, c) => new BigRational(new[,] { { 2, 1 }, { 1, 3 } }[r, c]));
            o.AppendLine(Cas.Determinant(a).ToString());
            o.AppendLine(Cas.RowReduce(a).Operations.Length + " row operations");
        }),
        ("Look up the catalog and apply a law", o =>
        {
            var entry = Cas.Get("alg.factor.diff-squares");
            o.AppendLine($"{entry.Id.Value}: {entry.Name} ({entry.Level})");
            o.AppendLine(entry.Explain);
            o.AppendLine(Cas.Find("pythagorean").First().Id.Value);
            var applied = (Outcome<Expr>.Success)Cas.Apply("trig.sum.sin-of-sum", Expr.Parse("sin(a + b)"));
            o.AppendLine(applied.Value.ToString());
        }),
    ];

    private static string Show(Outcome<LimitResult> outcome) => outcome is Outcome<LimitResult>.Success { Value: var v } ? v.ToExpression().ToString()! : outcome.ToString()!;

    private static string ReadmePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mathesis.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Mathesis.slnx"), "README.md");
    }

    private static string Normalize(string s) => string.Join("\n", s.ReplaceLineEndings("\n").Split('\n').Select(l => l.TrimEnd())).Trim();

    [TestMethod]
    public void TheReadmeHasAtLeastFifteenExamples() => Assert.IsGreaterThanOrEqualTo(15, Examples.Length);

    [TestMethod]
    public void EveryExampleOutputInTheReadmeIsWhatTheCodePrints()
    {
        var outputs = Examples.Select(e =>
        {
            var o = new StringBuilder();
            e.Run(o);
            return Normalize(o.ToString());
        }).ToList();
        if (Environment.GetEnvironmentVariable("MATHESIS_APPROVE") == "1")
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "readme-examples.txt"), string.Join("\n\n=====\n\n", outputs.Select((o, i) => $"### {i + 1}. {Examples[i].Title}\n{o}")));
        }

        var readme = File.ReadAllText(ReadmePath()).ReplaceLineEndings("\n");
        var blocks = System.Text.RegularExpressions.Regex.Matches(readme, "### (?<n>\\d+)\\. (?<title>[^\\n]+)\\n+```csharp\\n.*?```\\n+Output:\\n+```text\\n(?<out>.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.AreEqual(Examples.Length, blocks.Count, "every example needs a heading, a csharp block and an Output text block in the README");
        for (var i = 0; i < Examples.Length; i++)
        {
            Assert.AreEqual($"{i + 1}", blocks[i].Groups["n"].Value);
            Assert.AreEqual(Examples[i].Title, blocks[i].Groups["title"].Value.Trim());
            Assert.AreEqual(outputs[i], Normalize(blocks[i].Groups["out"].Value), $"example {i + 1} ({Examples[i].Title})");
        }
    }
}
