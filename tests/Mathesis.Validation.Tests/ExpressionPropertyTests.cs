using System.Diagnostics;
using System.Globalization;
using System.Text;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Parsing;
using Mathesis.Testing;

namespace Mathesis.Validation.Tests;

/// <summary>Seeded property and fuzz tests of the expression attributes (PLAN-M9 Phase 3).</summary>
[TestClass]
public class ExpressionPropertyTests
{
    private const int Seed = 20261006;

    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    /// <summary>The free symbols that are variables (not function symbols), by name, in order.</summary>
    private static string[] VariableNames(Expr expr) => [.. expr.FreeSymbols.Where(s => s.DeclaredSort is not FunctionSort).Select(s => s.Name).Order(StringComparer.Ordinal)];

    /// <summary>Checks the Variables and RequiredVariables rules for one accepted expression of the given format.</summary>
    private static void AssertVariableRules(string text, InputFormat format, string[] names, int index, string where)
    {
        // Variables set to exactly the free symbols accepts; with one symbol removed it names exactly that symbol.
        Assert.IsNull(new MathExpressionAttribute { Format = format, Variables = names }.Check(text), $"{where}: Variables = exactly the free symbols");
        if (names.Length > 0)
        {
            var removed = names[index % names.Length];
            var result = new MathExpressionAttribute { Format = format, Variables = [.. names.Where(n => n != removed)] }.Check(text);
            Assert.AreEqual(MathValidationCode.UnknownVariable, result?.Code, $"{where}: one symbol removed ({removed})");
            Assert.AreEqual($"Value uses a variable that is not allowed: {removed}.", result!.ErrorMessage, where);
        }

        // RequiredVariables is symmetric: all of the free symbols is accepted, any subset is accepted, and one more name is missing.
        Assert.IsNull(new MathExpressionAttribute { Format = format, RequiredVariables = names }.Check(text), $"{where}: RequiredVariables = exactly the free symbols");
        if (names.Length > 0) Assert.IsNull(new MathExpressionAttribute { Format = format, RequiredVariables = [names[index % names.Length]] }.Check(text), $"{where}: RequiredVariables = a subset");
        var missing = new MathExpressionAttribute { Format = format, RequiredVariables = [.. names, "w"] }.Check(text);
        Assert.AreEqual(MathValidationCode.MissingVariable, missing?.Code, $"{where}: one required name added");
        Assert.AreEqual("Value must use: w.", missing!.ErrorMessage, where);
    }

    [TestMethod]
    public void TwoThousandSeededExpressionsAreAcceptedAsTextAndLatexWithTheVariableRules()
    {
        var gen = new Gen(Seed);
        var text = new MathExpressionAttribute();
        var latex = new MathExpressionAttribute { Format = InputFormat.Latex };
        var latexAccepted = 0;
        var withVariables = 0;

        for (var i = 0; i < 2_000; i++)
        {
            var built = gen.RandomExpr(4);
            var printed = built.ToString();
            var where = $"seed {Seed}, case {i}, '{printed}'";

            // as ToString() text
            Assert.IsNull(text.Check(printed), where);
            var tree = Parser.Parse(printed).Expr!;
            var names = VariableNames(tree);
            if (names.Length > 0) withVariables++;
            AssertVariableRules(printed, InputFormat.Text, names, i, where);

            // as ToLatex() text, where the existing LaTeX round-trip tests accept it: the LaTeX parser reads what the printer writes, and both trees agree after Canonical
            var latexText = tree.ToLatex();
            var back = LatexParser.Parse(latexText);
            var existingTestsAccept = back.Success && Normalizer.Canonical(tree) == Normalizer.Canonical(back.Expr!);
            var verdict = latex.Check(latexText);
            if (existingTestsAccept)
            {
                latexAccepted++;
                Assert.IsNull(verdict, $"{where} as LaTeX '{latexText}'");
                AssertVariableRules(latexText, InputFormat.Latex, VariableNames(back.Expr!), i, $"{where} as LaTeX '{latexText}'");
            }
            else
            {
                // whatever the parser decides, the attribute agrees with it
                Assert.AreEqual(back.Success, verdict is null, $"{where} as LaTeX '{latexText}'");
                if (!back.Success)
                {
                    Assert.AreEqual(back.Errors[0].Span, verdict!.Span, $"{where} as LaTeX '{latexText}'");
                    Assert.AreEqual(back.Errors[0].Suggestion, verdict.Suggestion, $"{where} as LaTeX '{latexText}'");
                }
            }
        }

        Assert.IsTrue(withVariables > 1_500, $"most generated expressions should contain variables: {withVariables} (seed {Seed})");
        Assert.IsTrue(latexAccepted > 1_000, $"the LaTeX round trip should accept most generated expressions: {latexAccepted} (seed {Seed})");
    }

    // ----- fuzzing -----

    internal static readonly string[] TextTokens =
    [
        "0", "1", "2", "10", "3.14", ".5", "1e5", "1e-7", "x", "y", "z", "xy", "theta", "alpha", "f", "g", "foo", "pi", "e", "I", "oo", "inf", "sin", "cos", "tan", "ln", "log", "sqrt", "exp",
        "sum", "product", "integrate", "limit", "diff", "det", "forall", "exists", "in", "and", "or", "not", "mod", "true", "R", "N",
        "+", "-", "*", "/", "^", "!", "'", "|", "=", "<", ">", "<=", ">=", "!=", "=>", "<=>", "(", ")", "[", "]", "{", "}", ",", ";", ":", "_", ".", " ", "  ", "\t",
        "√", "∫", "∑", "π", "≤", "≥", "≠", "∈", "∞", "·", "×", "÷", "²", "−", "θ", "٣", "😀", "\u0000",
    ];

    internal static readonly string[] LatexTokens =
    [
        @"\frac", @"\dfrac", @"\sqrt", @"\sin", @"\cos", @"\ln", @"\log", @"\int", @"\sum", @"\prod", @"\lim", @"\infty", @"\pi", @"\theta", @"\cdot", @"\times", @"\le", @"\ge", @"\ne", @"\in", @"\mathbb",
        @"\left(", @"\right)", @"\begin{pmatrix}", @"\end{pmatrix}", @"\begin{cases}", @"\end{cases}", @"\\", @"&", @"\,", @"\unknown", @"\operatorname", @"\binom", @"\overline", @"\vec",
        "{", "}", "^", "_", "(", ")", "[", "]", "=", "<", "+", "-", "*", "/", "0", "1", "2", "x", "y", "n", "k", "dx", " ", ",", "'", "|",
    ];

    internal static string RandomStream(Gen gen, string[] tokens, int maxLength)
    {
        var length = gen.Random.Next(0, maxLength + 1);
        var stream = new StringBuilder();
        while (stream.Length < length) stream.Append(tokens[gen.Random.Next(tokens.Length)]);
        return stream.Length > maxLength ? stream.ToString(0, maxLength) : stream.ToString();
    }

    /// <summary>One to five random edits of <paramref name="text"/>: delete, insert a token, replace, truncate, duplicate a piece, swap, or repeat a character.</summary>
    internal static void Damage(Gen gen, StringBuilder text, string[] tokens)
    {
        var random = gen.Random;
        for (var mutations = random.Next(1, 6); mutations > 0 && text.Length > 0; mutations--)
        {
            var at = random.Next(text.Length);
            switch (random.Next(7))
            {
                case 0: text.Remove(at, 1); break;
                case 1: text.Insert(at, tokens[random.Next(tokens.Length)]); break;
                case 2: text[at] = tokens[random.Next(tokens.Length)][0]; break;
                case 3: text.Length = at; break;
                case 4: text.Insert(at, text.ToString(at, Math.Min(random.Next(1, 8), text.Length - at))); break;
                case 5 when at + 1 < text.Length: (text[at], text[at + 1]) = (text[at + 1], text[at]); break;
                default: text.Insert(at, new string(text[at], random.Next(2, 40))); break;
            }
        }
    }

    internal static string Mutate(Gen gen, List<string> corpus)
    {
        var random = gen.Random;
        var text = new StringBuilder(corpus[random.Next(corpus.Count)]);
        if (random.Next(10) == 0) text.Append(' ').Append(TextTokens[random.Next(TextTokens.Length)]).Append(' ').Append(corpus[random.Next(corpus.Count)]);
        Damage(gen, text, TextTokens);

        // sometimes grow to the length limit and beyond
        if (random.Next(15) == 0) while (text.Length < 1_200) text.Append(' ').Append(corpus[random.Next(corpus.Count)]);
        return text.Length > 1_200 ? text.ToString(0, 1_200) : text.ToString();
    }

    /// <summary>A printed random expression, damaged a little, so that a good share of the inputs are still valid LaTeX.</summary>
    internal static string MutateLatex(Gen gen)
    {
        var text = new StringBuilder(gen.RandomExpr(3).ToLatex());
        if (gen.Random.Next(4) != 0) Damage(gen, text, LatexTokens);
        return text.ToString();
    }

    [TestMethod]
    public void TwentyThousandMutatedAndRandomInputsNeverThrowAndAreFast()
    {
        var corpus = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "corpus", "expressions.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        Assert.IsTrue(corpus.Count > 400);

        var gen = new Gen(Seed + 1);
        var attributes = new MathExpressionAttribute[]
        {
            new(),
            new() { WarningsAreErrors = true, CheckSorts = true, Variables = ["x", "y"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Trig | OperatorFamily.Calculus },
            new MathEquationAttribute { CheckSorts = true, SingleLetterVariables = false, LogMeansNatural = true },
            new() { Shape = ExpressionShape.Interval, Variables = [] },
        };

        // warm up the parser, the checker and the resx so the 250 ms bound measures the work and not the JIT
        foreach (var attribute in attributes)
        {
            _ = attribute.Check("x + 1 = 2");
            _ = attribute.Check("x +");
            _ = attribute.Check("sin(x) + 1/2x + sqr(x) + [[1]] + [[1, 2]]");
        }

        var slowest = TimeSpan.Zero;
        var slowestText = string.Empty;
        var parsed = 0;
        var rejected = 0;
        for (var i = 0; i < 20_000; i++)
        {
            var text = i % 2 == 0 ? Mutate(gen, corpus) : RandomStream(gen, TextTokens, 1_200);
            var where = $"seed {Seed + 1}, case {i}, length {text.Length}: '{(text.Length > 80 ? text[..80] + "..." : text)}'";

            foreach (var attribute in attributes)
            {
                MathValidationResult? result = null;
                var watch = Stopwatch.StartNew();
                try
                {
                    result = attribute.Check(text);
                    _ = attribute.IsValid(text);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{where}: {attribute.GetType().Name} threw {ex.GetType().Name}: {ex.Message}");
                }

                watch.Stop();
                if (watch.Elapsed > slowest)
                {
                    slowest = watch.Elapsed;
                    slowestText = where;
                }

                Assert.IsTrue(watch.ElapsedMilliseconds < 250, $"{where}: {attribute.GetType().Name} took {watch.ElapsedMilliseconds} ms");
                if (result is not null) Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage), where);
            }

            // The default attribute accepts exactly the text the parser accepts (and blank text), and reports TooLong before parsing long text.
            var plain = attributes[0].Check(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                Assert.IsNull(plain, where);
            }
            else if (text.Length > 1_000)
            {
                Assert.AreEqual(MathValidationCode.TooLong, plain?.Code, where);
            }
            else if (Parser.Parse(text) is { Success: false } failed)
            {
                rejected++;
                Assert.AreEqual(MathValidationCode.Syntax, plain?.Code, where);
                Assert.AreEqual(failed.Errors[0].Span, plain!.Span, where);
                Assert.AreEqual(failed.Errors[0].Suggestion, plain.Suggestion, where);
            }
            else
            {
                parsed++;
                Assert.IsNull(plain, where);
            }
        }

        Assert.IsTrue(parsed > 2_000 && rejected > 2_000, $"the inputs should include many valid and many invalid expressions: {parsed} valid, {rejected} invalid (seed {Seed + 1})");
        Console.WriteLine($"{parsed} valid, {rejected} invalid; slowest call {slowest.TotalMilliseconds:F1} ms on {slowestText}");
    }

    [TestMethod]
    public void FiveThousandRandomLatexInputsNeverThrowAndAreFast()
    {
        var gen = new Gen(Seed + 2);
        var latex = new MathExpressionAttribute { Format = InputFormat.Latex };
        var strict = new MathEquationAttribute { Format = InputFormat.Latex, CheckSorts = true, Variables = ["x", "y"], DisallowedFamilies = OperatorFamily.Calculus };
        _ = latex.Check(@"\frac{1}{2}");
        _ = strict.Check(@"x +");

        var parsed = 0;
        for (var i = 0; i < 5_000; i++)
        {
            var text = i % 2 == 0 ? MutateLatex(gen) : RandomStream(gen, LatexTokens, 1_000);
            var where = $"seed {Seed + 2}, case {i}, length {text.Length}: '{(text.Length > 80 ? text[..80] + "..." : text)}'";

            foreach (var attribute in new MathExpressionAttribute[] { latex, strict })
            {
                MathValidationResult? result = null;
                var watch = Stopwatch.StartNew();
                try
                {
                    result = attribute.Check(text);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{where}: {attribute.GetType().Name} threw {ex.GetType().Name}: {ex.Message}");
                }

                watch.Stop();
                Assert.IsTrue(watch.ElapsedMilliseconds < 250, $"{where}: took {watch.ElapsedMilliseconds} ms");
                if (result is not null) Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage), where);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var ok = LatexParser.Parse(text).Success;
                if (ok) parsed++;
                Assert.AreEqual(ok, latex.Check(text) is null, where);
            }
        }

        Assert.IsTrue(parsed > 500 && parsed < 4_800, $"the inputs should include much valid and much invalid LaTeX: {parsed} valid of 5000 (seed {Seed + 2})");
    }
}
