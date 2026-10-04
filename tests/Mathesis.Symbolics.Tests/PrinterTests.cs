using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Printing;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class PrinterTests
{
    private const int Seed = 20261051;

    private static Expr P(string text) => Expr.Parse(text);

    // ----- Text printing (docs/design/05, "Printing") -----

    [TestMethod]
    public void MinimalParenthesesFollowPrecedenceAndAssociativity()
    {
        Assert.AreEqual("a - (b - c)", P("a - (b - c)").ToString());
        Assert.AreEqual("a - b - c", P("(a - b) - c").ToString());
        Assert.AreEqual("a/(b*c)", P("a / (b * c)").ToString());
        Assert.AreEqual("2^3^4", P("2^(3^4)").ToString());
        Assert.AreEqual("(2^3)^4", P("(2^3)^4").ToString());
        Assert.AreEqual("-x^2", P("-x^2").ToString());
        Assert.AreEqual("(-x)^2", P("(-x)^2").ToString());
        Assert.AreEqual("(a + b)*c", P("(a + b) * c").ToString());
    }

    [TestMethod]
    public void PrintingNormalizedTreesUsesFriendlyForms()
    {
        Assert.AreEqual("x - y", Normalizer.Canonical(P("x - y")).ToString());
        Assert.AreEqual("x^-1", Normalizer.Canonical(P("1 / x")).ToString());
        Assert.AreEqual("sqrt(x)", Normalizer.Canonical(P("sqrt(x)")).ToString());
        Assert.AreEqual("6 - 5x + x^2 = 0", Normalizer.Canonical(P("x^2 - 5x + 6 = 0")).ToString());
    }

    [TestMethod]
    public void PolynomialsPrintInDescendingDegreeInPresentationMode()
    {
        var canonical = Normalizer.Canonical(P("x^2 - 5x + 6 = 0"));
        Assert.AreEqual("x^2 - 5x + 6 = 0", TextPrinter.Print(canonical, new PrintOptions { DescendingPolynomials = true }));
        Assert.AreEqual("x^2 - 5x + 6 = 0", TextPrinter.Print(canonical, PrintOptions.Presentation));
    }

    [TestMethod]
    public void PresentationModeUsesUnicodeSymbols()
    {
        Assert.AreEqual("π*r^2", TextPrinter.Print(P("pi * r^2"), PrintOptions.Presentation));
        Assert.AreEqual("(0, ∞)", TextPrinter.Print(P("(0, oo)"), PrintOptions.Presentation));
        Assert.AreEqual("x in ℝ", TextPrinter.Print(P("x in R"), PrintOptions.Presentation));
        Assert.AreEqual("pi*r^2", P("pi * r^2").ToString());
        Assert.AreEqual("(0, oo)", P("(0, oo)").ToString());
    }

    [TestMethod]
    public void DecimalDigitsRoundHalfAwayFromZero()
    {
        static string Digits(string text, int digits) => TextPrinter.Print(P(text), new PrintOptions { DecimalDigits = digits });
        Assert.AreEqual("0.33", TextPrinter.Print(Number(BigRational.Create(1, 3)), new PrintOptions { DecimalDigits = 2 }));
        Assert.AreEqual("0.67", TextPrinter.Print(Number(BigRational.Create(2, 3)), new PrintOptions { DecimalDigits = 2 }));
        Assert.AreEqual("0.13", Digits("0.125", 2));
        Assert.AreEqual("-0.13", Digits("-0.125", 2));
        Assert.AreEqual("3", Digits("3", 0));
        Assert.AreEqual("1", TextPrinter.Print(Number(BigRational.Create(1, 2)), new PrintOptions { DecimalDigits = 0 }));
        Assert.AreEqual("-1", TextPrinter.Print(Number(BigRational.Create(-1, 2)), new PrintOptions { DecimalDigits = 0 }));
    }

    [TestMethod]
    public void DecimalsKeepTheirSpellingAndStayExact()
    {
        Assert.AreEqual("0.1", P("0.1").ToString());
        Assert.AreEqual("2.50", P("2.50").ToString());
        Assert.AreEqual(BigRational.Create(1, 10), ((Number)P("0.1")).Value);
    }

    [TestMethod]
    public void NullArgumentsThrow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TextPrinter.Print(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextPrinter.Print(P("x"), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => LatexPrinter.Print(null!));
    }

    // ----- LaTeX snapshots (tests/corpus/latex.txt) -----

    [TestMethod]
    public void LatexMatchesTheSnapshots()
    {
        var cases = Corpus.RawLines("latex.txt").Where(l => l.Trim().Length > 0 && !l.StartsWith('#')).ToList();
        Assert.IsTrue(cases.Count >= 70, $"only {cases.Count} snapshots");
        foreach (var line in cases)
        {
            var split = line.IndexOf(" ==> ", StringComparison.Ordinal);
            Assert.IsTrue(split > 0, $"bad snapshot line '{line}'");
            var input = line[..split];
            var expected = line[(split + 5)..];
            Assert.AreEqual(expected, P(input).ToLatex(), $"snapshot for '{input}'");
        }
    }

    [TestMethod]
    public void LatexSnapshotsParseBackToTheSameValue()
    {
        // The LaTeX parser reads what the printer writes; compare after Structural normalization because LaTeX has no
        // function-call and implicit-multiplication spelling that is identical to the linear notation in every case.
        foreach (var line in Corpus.RawLines("latex.txt").Where(l => l.Trim().Length > 0 && !l.StartsWith('#')))
        {
            var input = line[..line.IndexOf(" ==> ", StringComparison.Ordinal)];
            var tree = P(input);
            var result = LatexParser.Parse(tree.ToLatex());
            Assert.IsTrue(result.Success, $"'{input}' printed as {tree.ToLatex()}: {(result.Success ? string.Empty : result.Errors[0])}");
            Assert.AreEqual(Normalizer.Canonical(tree), Normalizer.Canonical(result.Expr!), $"'{input}' printed as {tree.ToLatex()}");
        }
    }

    [TestMethod]
    public void LatexParserReadsCommonInput()
    {
        Assert.AreEqual(Normalizer.Canonical(P("a/b + c/d")), Normalizer.Canonical(LatexParser.Parse(@"\frac{a}{b} + \frac{c}{d}").Expr!));
        Assert.AreEqual(Normalizer.Canonical(P("sqrt(x^2 + 1)")), Normalizer.Canonical(LatexParser.Parse(@"\sqrt{x^{2} + 1}").Expr!));
        Assert.AreEqual(Normalizer.Canonical(P("integrate(x^2, x, 0, 1)")), Normalizer.Canonical(LatexParser.Parse(@"\int_{0}^{1} x^{2} \,dx").Expr!));
        Assert.AreEqual(Normalizer.Canonical(P("sum(k, k, 1, n)")), Normalizer.Canonical(LatexParser.Parse(@"\sum_{k=1}^{n} k").Expr!));
        Assert.AreEqual(P("x^2 mod 5"), LatexParser.Parse(@"x^{2} \bmod 5").Expr);
    }

    [TestMethod]
    public void LatexParserRejectsWithColumns()
    {
        var result = LatexParser.Parse(@"\frac{a}{");
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors[0].Column >= 1);

        result = LatexParser.Parse(@"x + \unknownmacro");
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public void LatexNumbersNeverBecomeZeroSilently()
    {
        // LaTeX has no exponent literal: in "1e999999999" the e is Euler's number, never part of the number.
        var result = LatexParser.Parse("1e999999999");
        Assert.IsTrue(!result.Success || result.Expr!.Walk().All(w => w.Expr is not Number n || n.Value != BigRational.Zero), $"{result.Expr}");

        // Long literals are read exactly.
        const string digits = "123456789012345678901234567890";
        Assert.AreEqual(System.Numerics.BigInteger.Parse(digits), ((Number)LatexParser.Parse(digits).Expr!).Value.Numerator);
        Assert.AreEqual(BigRational.Parse("0.125"), ((Number)LatexParser.Parse("0.125").Expr!).Value);
    }

    // ----- Round trips over random trees -----

    [TestMethod]
    public void RandomRawTreesRoundTripThroughText()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 2000; i++)
        {
            var e = gen.RandomExpr(4);
            var printed = e.ToString();
            var back = Parser.Parse(printed);
            Assert.IsTrue(back.Success, $"{printed}: {(back.Success ? string.Empty : back.Errors[0])}");
            // Printing a parsed tree is exact; printing a built tree may re-associate, so compare after parsing once more.
            Assert.AreEqual(back.Expr, Expr.Parse(back.Expr!.ToString()), printed);
        }
    }

    [TestMethod]
    public void RandomCanonicalTreesPrintToParsableText()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < 1000; i++)
        {
            var canonical = Normalizer.Canonical(gen.RandomExpr(4));
            foreach (var options in new[] { PrintOptions.Faithful, PrintOptions.Presentation })
            {
                var printed = TextPrinter.Print(canonical, options with { UnicodeSymbols = false });
                Assert.IsTrue(Parser.Parse(printed).Success, printed);
            }
            Assert.IsFalse(string.IsNullOrEmpty(canonical.ToLatex()));
        }
    }
}
