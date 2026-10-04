using System.Diagnostics;
using System.Globalization;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Representations;
using Mathesis.Testing;

namespace Mathesis.Validation.Tests;

/// <summary><see cref="PolynomialExpressionAttribute"/> (PLAN-M9 Phase 4).</summary>
[TestClass]
public class PolynomialAttributeTests
{
    private const int Seed = 20261007;

    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    private sealed record Row(PolynomialExpressionAttribute Attribute, object? Value, MathValidationCode? Code, string? Message = null);

    private static readonly PolynomialExpressionAttribute X = new("x");

    private static string NotPolynomial(string construct) => $"Value must be a polynomial, but it contains {construct}.";

    private static string TooHigh(object degree, int maximum = 20) => $"Value has degree {degree}, but the highest allowed degree is {maximum}.";

    private static List<Row> Rows()
    {
        var cubic = new PolynomialExpressionAttribute("x") { MaxDegree = 3 };
        var latex = new PolynomialExpressionAttribute("x") { Format = InputFormat.Latex };

        return
        [
            // ----- polynomials (the plan's valid rows first) -----
            new(X, "x^2 - 5x + 6", null),
            new(X, "x/2 + 1", null),
            new(X, "(x+1)^2", null),
            new(X, "7", null),
            new(X, "0", null),
            new(X, "", null),
            new(X, "   ", null),
            new(X, null, null),
            new(X, "x", null),
            new(X, "-x^3 + x/3 - 1/7", null),
            new(X, "(x - 1)(x + 1)", null),
            new(X, "2x(x + 1)^3", null),
            new(X, "x^20", null),
            new(X, "0.5x^2 + 1.25", null),
            new(X, "−2x", null),
            new(X, "(x^2 + 1)/2", null),
            new(X, "3^2*x", null),
            new(X, "sqrt(4)*x", null),
            new(X, "2^100*x", null),
            new(X, "x^(4/2)", null),
            new(X, "(x+1)^30 - (x+1)^30 + x", null),
            new(latex, @"\frac{x}{2} + 1", null),
            new(latex, @"x^{2} - 5x + 6", null),
            new(new PolynomialExpressionAttribute("t"), "t^2 + 1", null),
            new(new PolynomialExpressionAttribute("theta"), "theta^2 - 1", null),
            new(new PolynomialExpressionAttribute("θ"), "theta^2 - 1", null),

            // ----- not polynomials: the smallest construct, as written -----
            new(X, "1/x", MathValidationCode.NotAPolynomial, NotPolynomial("1/x")),
            new(X, "sqrt(x)", MathValidationCode.NotAPolynomial, NotPolynomial("sqrt(x)")),
            new(X, "x^(1/2)", MathValidationCode.NotAPolynomial, NotPolynomial("x^(1/2)")),
            new(X, "x^-1", MathValidationCode.NotAPolynomial, NotPolynomial("x^-1")),
            new(X, "sin(x)", MathValidationCode.NotAPolynomial, NotPolynomial("sin(x)")),
            new(X, "x^2 + 3*sin(x) - 1", MathValidationCode.NotAPolynomial, NotPolynomial("sin(x)")),
            new(X, "sin(x)^2", MathValidationCode.NotAPolynomial, NotPolynomial("sin(x)")),
            new(X, "(1/x)^2 + x", MathValidationCode.NotAPolynomial, NotPolynomial("1/x")),
            new(X, "x^2/x", MathValidationCode.NotAPolynomial, NotPolynomial("x^2/x")),
            new(X, "(x+1)/(x-1)", MathValidationCode.NotAPolynomial, NotPolynomial("(x + 1)/(x - 1)")),
            new(X, "abs(x)", MathValidationCode.NotAPolynomial, NotPolynomial("abs(x)")),
            new(X, "f(x)", MathValidationCode.NotAPolynomial, NotPolynomial("f(x)")),
            new(X, "e^x", MathValidationCode.NotAPolynomial, NotPolynomial("e^x")),
            new(X, "2^x", MathValidationCode.NotAPolynomial, NotPolynomial("2^x")),
            new(X, "x^pi", MathValidationCode.NotAPolynomial, NotPolynomial("x^pi")),
            new(X, "pi*x", MathValidationCode.NotAPolynomial, NotPolynomial("pi")),
            new(X, "sqrt(2)*x + 1", MathValidationCode.NotAPolynomial, NotPolynomial("sqrt(2)")),
            new(X, "2^999999999*x", MathValidationCode.NotAPolynomial, NotPolynomial("2^999999999")),
            new(X, "[[x]]", MathValidationCode.NotAPolynomial, NotPolynomial("[[x]]")),
            new(X, "sum(x^k, k, 0, 3)", MathValidationCode.NotAPolynomial),
            new(X, "sin(x)^65", MathValidationCode.NotAPolynomial, NotPolynomial("sin(x)")),
            new(latex, @"\frac{1}{x}", MathValidationCode.NotAPolynomial, NotPolynomial("1/x")),

            // ----- other variables -----
            new(X, "x*y", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: y."),
            new(X, "y", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: y."),
            new(X, "a + b*x", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: a, b."),
            new(X, "theta*x", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: θ."),
            new(X, "y*sin(x)", MathValidationCode.UnknownVariable),
            new(new PolynomialExpressionAttribute("t"), "x^2", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: x."),

            // ----- degree -----
            new(X, "x^21", MathValidationCode.DegreeTooHigh, TooHigh(21)),
            new(X, "(x+1)^65", MathValidationCode.DegreeTooHigh, TooHigh(65)),
            new(X, "x^100", MathValidationCode.DegreeTooHigh, TooHigh(100)),
            new(X, "x^65 + 1", MathValidationCode.DegreeTooHigh, TooHigh(65)),
            new(X, "(x+1)^64*(x+1)", MathValidationCode.DegreeTooHigh, TooHigh(65)),
            new(X, "(x^64 + 1)^64", MathValidationCode.DegreeTooHigh, TooHigh(4096)),
            new(X, "((x+1)^64)^64", MathValidationCode.DegreeTooHigh, TooHigh(4096)),
            new(X, "x^999999999", MathValidationCode.DegreeTooHigh, TooHigh(999999999)),
            new(X, "x^99999999999999999999", MathValidationCode.DegreeTooHigh, TooHigh($"more than {long.MaxValue}")),
            new(cubic, "x^3 - 1", null),
            new(cubic, "x^4", MathValidationCode.DegreeTooHigh, TooHigh(4, 3)),
            new(cubic, "(x+1)^4 - x^4", null),
            new(cubic, "(x+1)^5 - x^5 - 5x^4", null),
            new(cubic, "(x+1)^5 - x^5", MathValidationCode.DegreeTooHigh, TooHigh(4, 3)),
            new(cubic, "x^4 - x^4 + x", null),
            new(new PolynomialExpressionAttribute("x") { MaxDegree = 0 }, "5", null),
            new(new PolynomialExpressionAttribute("x") { MaxDegree = 0 }, "x", MathValidationCode.DegreeTooHigh, TooHigh(1, 0)),
            new(new PolynomialExpressionAttribute("x") { MaxDegree = 64 }, "x^64", null),
            new(new PolynomialExpressionAttribute("x") { MaxDegree = 64 }, "x^65", MathValidationCode.DegreeTooHigh, TooHigh(65, 64)),

            // ----- shape, syntax and length -----
            new(X, "x^2 = 4", MathValidationCode.WrongShape, "Value must be a polynomial in x, but it is an equation."),
            new(X, "x < 1", MathValidationCode.WrongShape, "Value must be a polynomial in x, but it is an inequality."),
            new(X, "[0, 1)", MathValidationCode.WrongShape, "Value must be a polynomial in x, but it is an interval."),
            new(X, "x in R", MathValidationCode.WrongShape, "Value must be a polynomial in x, but it is a statement."),
            new(new PolynomialExpressionAttribute("theta"), "theta = 1", MathValidationCode.WrongShape, "Value must be a polynomial in θ, but it is an equation."),
            new(X, "y = sin(x)", MathValidationCode.WrongShape),
            new(X, "x +", MathValidationCode.Syntax),
            new(X, "(x + 1", MathValidationCode.Syntax),
            new(X, "1e999999999", MathValidationCode.Syntax),
            new(latex, @"\frac{x}{", MathValidationCode.Syntax),
            new(X, new string('1', 1_001), MathValidationCode.TooLong),
        ];
    }

    private static void AssertRows(IEnumerable<Row> rows)
    {
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var label = $"{row.Attribute.Variable}: {row.Value ?? "null"}";
            var result = row.Attribute.Check(row.Value, "Value", "Member");
            if (result?.Code != row.Code)
            {
                failures.Add($"{label}: expected {row.Code?.ToString() ?? "valid"}, got {result?.Code.ToString() ?? "valid"} ({result?.ErrorMessage})");
                continue;
            }

            if (row.Message is not null && row.Message != result?.ErrorMessage) failures.Add($"{label}: message '{result?.ErrorMessage}' != '{row.Message}'");

            if (row.Code == MathValidationCode.Syntax)
            {
                var text = (string)row.Value!;
                var error = (row.Attribute.Format == InputFormat.Latex ? LatexParser.Parse(text) : Parser.Parse(text)).Errors[0];
                if (result!.Span != error.Span || result.Suggestion != error.Suggestion) failures.Add($"{label}: span or suggestion differ from the parser's");
            }

            Assert.AreEqual(row.Code is null, row.Attribute.IsValid(row.Value), label);
            Assert.AreEqual(row.Code, (row.Attribute.GetValidationResult(row.Value, MathValidationAttributeTests.Context()) as MathValidationResult)?.Code, label);

            // An Expr-typed property gets the same verdict and message without parsing.
            if (row.Value is string text2 && !string.IsNullOrWhiteSpace(text2) && row.Code is not (MathValidationCode.Syntax or MathValidationCode.TooLong))
            {
                var tree = (row.Attribute.Format == InputFormat.Latex ? LatexParser.Parse(text2) : Parser.Parse(text2)).Expr!;
                var typed = row.Attribute.Check(tree, "Value", "Member");
                if (typed?.Code != row.Code || typed?.ErrorMessage != result?.ErrorMessage) failures.Add($"{label}: as an Expr {typed?.Code.ToString() ?? "valid"} '{typed?.ErrorMessage}'");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void PolynomialTable()
    {
        var rows = Rows();

        Assert.IsTrue(rows.Count >= 30);
        AssertRows(rows);
    }

    [TestMethod]
    public void ThousandSeededPolynomialsAreAcceptedAtTheirDegreeAndRejectedJustBelowIt()
    {
        var gen = new Gen(Seed);
        var x = new Symbol("x");
        for (var i = 0; i < 1_000; i++)
        {
            var polynomial = gen.RationalPolynomial(maxDegree: 40, maxBits: 16);
            var degree = polynomial.Degree;
            var text = PolynomialConversion.FromPolynomial(polynomial, x).ToString();
            var where = $"seed {Seed}, case {i}, degree {degree}: {text[..Math.Min(text.Length, 80)]}";

            Assert.IsNull(new PolynomialExpressionAttribute("x") { MaxDegree = degree, MaxLength = 100_000 }.Check(text), where);
            if (degree > 0)
            {
                var result = new PolynomialExpressionAttribute("x") { MaxDegree = degree - 1, MaxLength = 100_000 }.Check(text);
                Assert.AreEqual(MathValidationCode.DegreeTooHigh, result?.Code, where);
                Assert.AreEqual(TooHigh(degree, degree - 1), result!.ErrorMessage, where);
            }

            // the same polynomial built as a tree
            Assert.IsNull(new PolynomialExpressionAttribute("x") { MaxDegree = degree }.Check(PolynomialConversion.FromPolynomial(polynomial, x)), where);
        }
    }

    [TestMethod]
    public void TheDegreeIsExactWhenTermsCancel()
    {
        // Written degree 30 with MaxDegree 20: the expansion shows the true degree.
        var attribute = new PolynomialExpressionAttribute("x");

        Assert.IsNull(attribute.Check("(x + 1)^30 - (x^30 + 30x^29 + 435x^28 + 4060x^27 + 27405x^26 + 142506x^25 + 593775x^24 + 2035800x^23 + 5852925x^22 + 14307150x^21) + x"));
        Assert.AreEqual(TooHigh(21), attribute.Check("(x + 1)^30 - (x^30 + 30x^29 + 435x^28 + 4060x^27 + 27405x^26 + 142506x^25 + 593775x^24 + 2035800x^23 + 5852925x^22)")!.ErrorMessage);
        Assert.IsNull(attribute.Check("(x^2 + 1)^11 - (x^2 + 1)^11"));
    }

    [TestMethod]
    public void HostileInputsAreBounded()
    {
        var attributes = new[] { new PolynomialExpressionAttribute("x"), new PolynomialExpressionAttribute("x") { MaxDegree = 64 } };
        foreach (var attribute in attributes)
        {
            _ = attribute.Check("x^2 + 1");
            _ = attribute.Check("sin(x)");
            _ = attribute.Check("x^30");
        }

        foreach (var text in new[]
        {
            "sqrt(1e100000)*x", "(1e100000)^(1/64)*x", "x + 1e100000 + 1e-100000", "2^999999999*x", "9^9^9*x", "x^x^x^x", "((x+1)^64)^64", "(x^64 + 1)^64",
            "(x+1)^64*(x+2)^64*(x+3)^64*(x+4)^64", "(" + new string('9', 900) + "*x + 1)^64", "(x+1)^64*(" + new string('7', 300) + "*x + 1)^64", "x^999999999*(x+1)^64",
            "(x+1)^64 - (x+1)^64 + (x+2)^64 - (x+2)^64 + (x+3)^64 - (x+3)^64 + (x+4)^64", "sin(1e100000)*x", "(1e100000)^65*x",
        })
        {
            foreach (var attribute in attributes)
            {
                var watch = Stopwatch.StartNew();
                var result = attribute.Check(text);
                watch.Stop();

                Assert.IsTrue(watch.ElapsedMilliseconds < 250, $"MaxDegree {attribute.MaxDegree}, {text[..Math.Min(text.Length, 40)]}: {watch.ElapsedMilliseconds} ms ({result?.Code})");
                Assert.IsTrue(result is null || result.Code is MathValidationCode.NotAPolynomial or MathValidationCode.DegreeTooHigh or MathValidationCode.UnknownVariable, $"{text[..Math.Min(text.Length, 40)]}: {result?.Code}");
                if (result is not null) Assert.IsTrue(result.ErrorMessage!.Length < 200, result.ErrorMessage);
            }
        }

        Assert.IsNull(attributes[0].Check("sqrt(1e100000)*x"), "the root is exact: 10^50000 x");
        Assert.AreEqual(MathValidationCode.NotAPolynomial, attributes[0].Check("sin(1e100000)*x")!.Code);
        Assert.AreEqual(NotPolynomial("sin(…)"), attributes[0].Check("sin(1e100000)*x")!.ErrorMessage, "a huge number is not printed");
    }

    [TestMethod]
    public void ConfigurationErrorsThrowAsSpecified()
    {
        var rows = new (string Label, PolynomialExpressionAttribute Attribute, string Message)[]
        {
            ("MaxDegree -1", new PolynomialExpressionAttribute("x") { MaxDegree = -1 }, "MaxDegree must be between 0 and 64 but is -1."),
            ("MaxDegree 65", new PolynomialExpressionAttribute("x") { MaxDegree = 65 }, "MaxDegree must be between 0 and 64 but is 65."),
            ("constant as the variable", new PolynomialExpressionAttribute("e"), "Variable contains 'e', which is not a variable name (it is a constant)."),
            ("expression as the variable", new PolynomialExpressionAttribute("x+1"), "Variable contains 'x+1', which is not a variable name."),
            ("empty variable", new PolynomialExpressionAttribute(""), "Variable contains an empty name."),
            ("null variable", new PolynomialExpressionAttribute(null!), "Variable contains an empty name."),
            ("word with single-letter variables", new PolynomialExpressionAttribute("speed"), "Variable contains 'speed', which is not a variable name."),
            ("undefined format", new PolynomialExpressionAttribute("x") { Format = (InputFormat)3 }, "Format 3 is not a defined value."),
            ("MaxLength 0", new PolynomialExpressionAttribute("x") { MaxLength = 0 }, "MaxLength must be between 1 and 100000 but is 0."),
            ("undefined placeholder", new PolynomialExpressionAttribute("x") { ErrorMessage = "{0} {3}" }, "{0} {3}"),
        };

        foreach (var (label, attribute, message) in rows)
        {
            var text = attribute.GetConfigurationError();
            Assert.IsNotNull(text, label);
            StringAssert.StartsWith(text, "PolynomialExpressionAttribute: ", label);
            StringAssert.Contains(text, message, label);
            foreach (var value in new object?[] { null, "", "x", new Symbol("x") })
            {
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid(value), label).Message, label);
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.Check(value), label).Message, label);
            }
        }

        foreach (var attribute in new[] { new PolynomialExpressionAttribute("x") { MaxDegree = 0 }, new PolynomialExpressionAttribute("x") { MaxDegree = 64 }, new PolynomialExpressionAttribute("theta"), new PolynomialExpressionAttribute("x_1"), new PolynomialExpressionAttribute(@"\alpha") { Format = InputFormat.Latex } })
        {
            Assert.IsNull(attribute.GetConfigurationError(), attribute.Variable);
        }

        Assert.AreEqual("x", X.Variable);
        Assert.AreEqual(20, X.MaxDegree);
        Assert.AreEqual(InputFormat.Text, X.Format);
        Assert.IsTrue(typeof(PolynomialExpressionAttribute).IsSealed);
        Assert.IsNull(typeof(PolynomialExpressionAttribute).GetProperty(nameof(PolynomialExpressionAttribute.Variable))!.SetMethod);
        Assert.ThrowsExactly<InvalidOperationException>(() => X.IsValid(7));
        Assert.IsNull(new PolynomialExpressionAttribute(@"\alpha") { Format = InputFormat.Latex }.Check(@"\alpha^{2} + 1"));
    }

    [TestMethod]
    public void VerdictsAreTheSameUnderEveryCulture()
    {
        var rows = Rows();
        string Describe(CultureInfo culture)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return string.Join("\n", rows.Select(r => r.Attribute.Check(r.Value, "Value", "Member") is { } result ? $"{result.Code}|{result.Span}|{result.Suggestion}|{result.ErrorMessage}" : "valid"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        var baseline = Describe(CultureInfo.InvariantCulture);
        foreach (var name in new[] { "en-US", "de-DE", "fr-FR", "ar-SA", "tr-TR", "ja-JP" })
        {
            Assert.AreEqual(baseline, Describe(new CultureInfo(name)), name);
        }
    }
}
