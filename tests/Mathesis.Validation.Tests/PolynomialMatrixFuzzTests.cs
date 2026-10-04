using System.Diagnostics;
using System.Globalization;
using System.Text;
using Mathesis.Symbolics.Parsing;
using Mathesis.Testing;

namespace Mathesis.Validation.Tests;

/// <summary>The Phase 3 fuzz run repeated over <see cref="PolynomialExpressionAttribute"/> and <see cref="MathMatrixAttribute"/> (PLAN-M9 Phase 4).</summary>
[TestClass]
public class PolynomialMatrixFuzzTests
{
    private const int Seed = 20261009;

    private static readonly string[] MatrixTokens = ["[", "]", "[[", "]]", ", ", ",", "1", "0", "-", "+", "/", "2^3", "x", "pi", "0.5", "2e3", ";", " ", "1/0", "[1, 2]", "(", ")"];

    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    /// <summary>A matrix literal of random size and entries, damaged a little.</summary>
    private static string MutateMatrix(Gen gen, bool latex)
    {
        var rows = gen.Random.Next(1, 13);
        var columns = gen.Random.Next(1, 13);
        string EntryText() => gen.Random.Next(8) switch
        {
            0 => "x",
            1 => "-1/2",
            2 => "2^3",
            _ => gen.Rational(8).ToString(),
        };

        var text = new StringBuilder();
        if (latex)
        {
            text.Append(@"\begin{pmatrix}").Append(string.Join(@"\\", Enumerable.Range(0, rows).Select(_ => string.Join("&", Enumerable.Range(0, columns).Select(_ => EntryText()))))).Append(@"\end{pmatrix}");
        }
        else
        {
            text.Append('[').Append(string.Join(", ", Enumerable.Range(0, rows).Select(_ => "[" + string.Join(", ", Enumerable.Range(0, columns).Select(_ => EntryText())) + "]"))).Append(']');
        }

        if (gen.Random.Next(3) != 0) ExpressionPropertyTests.Damage(gen, text, latex ? ExpressionPropertyTests.LatexTokens : MatrixTokens);
        return text.ToString();
    }

    private static void Run(Gen gen, int count, Func<int, string> input, MathValidationAttribute[] attributes, Func<string, ParseResult> parse, int maxLength)
    {
        foreach (var attribute in attributes)
        {
            _ = attribute.Check("x^2 + 1");
            _ = attribute.Check("[[1, 2], [3, x]]");
            _ = attribute.Check("x +");
        }

        var slowest = 0L;
        var parsed = 0;
        var rejected = 0;
        for (var i = 0; i < count; i++)
        {
            var text = input(i);
            var where = $"seed {gen.Seed}, case {i}, length {text.Length}: '{(text.Length > 80 ? text[..80] + "..." : text)}'";
            var blank = string.IsNullOrWhiteSpace(text);
            var parseResult = blank || text.Length > maxLength ? null : parse(text);
            if (parseResult is { Success: true }) parsed++;
            if (parseResult is { Success: false }) rejected++;

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
                slowest = Math.Max(slowest, watch.ElapsedMilliseconds);
                Assert.IsTrue(watch.ElapsedMilliseconds < 250, $"{where}: {attribute.GetType().Name} took {watch.ElapsedMilliseconds} ms");

                // The parse decides Syntax, with the parser's span; nothing is TooLong below the limit.
                if (blank) Assert.IsNull(result, where);
                else if (text.Length > maxLength) Assert.AreEqual(MathValidationCode.TooLong, result?.Code, where);
                else if (parseResult is { Success: false } failure)
                {
                    Assert.AreEqual(MathValidationCode.Syntax, result?.Code, where);
                    Assert.AreEqual(failure.Errors[0].Span, result!.Span, where);
                }
                else Assert.AreNotEqual((MathValidationCode?)MathValidationCode.Syntax, result?.Code, where);

                if (result is not null) Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage), where);
            }
        }

        Assert.IsTrue(parsed > count / 10 && rejected > count / 10, $"the inputs should mix valid and invalid text: {parsed} parsed, {rejected} rejected (seed {gen.Seed})");
        Console.WriteLine($"{parsed} parsed, {rejected} rejected; slowest call {slowest} ms");
    }

    [TestMethod]
    public void TwentyThousandTextInputsNeverThrowAndAreFast()
    {
        var corpus = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "corpus", "expressions.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        var gen = new Gen(Seed);
        var attributes = new MathValidationAttribute[]
        {
            new PolynomialExpressionAttribute("x"),
            new PolynomialExpressionAttribute("x") { MaxDegree = 64 },
            new MathMatrixAttribute(),
            new MathMatrixAttribute { Rows = 2, Columns = 2, Square = true, NumericEntries = false },
        };

        Run(gen, 20_000, i => (i % 3) switch
        {
            0 => ExpressionPropertyTests.Mutate(gen, corpus),
            1 => ExpressionPropertyTests.RandomStream(gen, ExpressionPropertyTests.TextTokens, 1_200),
            _ => MutateMatrix(gen, latex: false),
        }, attributes, text => Parser.Parse(text), 1_000);
    }

    [TestMethod]
    public void FiveThousandLatexInputsNeverThrowAndAreFast()
    {
        var gen = new Gen(Seed + 1);
        var attributes = new MathValidationAttribute[]
        {
            new PolynomialExpressionAttribute("x") { Format = InputFormat.Latex },
            new MathMatrixAttribute { Format = InputFormat.Latex },
            new MathMatrixAttribute { Format = InputFormat.Latex, Square = true, NumericEntries = false, MaxDimension = 12 },
        };

        Run(gen, 5_000, i => (i % 3) switch
        {
            0 => ExpressionPropertyTests.MutateLatex(gen),
            1 => ExpressionPropertyTests.RandomStream(gen, ExpressionPropertyTests.LatexTokens, 1_000),
            _ => MutateMatrix(gen, latex: true),
        }, attributes, text => LatexParser.Parse(text), 1_000);
    }
}
