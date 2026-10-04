using System.Diagnostics;
using System.Globalization;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation.Tests;

/// <summary><see cref="MathExpressionAttribute"/> and <see cref="MathEquationAttribute"/> (PLAN-M9 Phase 3).</summary>
[TestClass]
public class ExpressionAttributeTests
{
    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    /// <summary>One expectation: the verdict (<c>null</c> is valid) and, when given, the exact message. Syntax rows are compared with the parser instead.</summary>
    private sealed record Row(MathExpressionAttribute Attribute, object? Value, MathValidationCode? Code, string? Message = null);

    private static readonly MathExpressionAttribute Any = new();
    private static readonly MathExpressionAttribute LatexAny = new() { Format = InputFormat.Latex };

    private static ParserOptions OptionsOf(MathExpressionAttribute attribute) => new() { SingleLetterVariables = attribute.SingleLetterVariables, LogMeansNatural = attribute.LogMeansNatural };

    private static ParseResult ParseLikeTheAttribute(MathExpressionAttribute attribute, string text) =>
        attribute.Format == InputFormat.Latex ? LatexParser.Parse(text, OptionsOf(attribute)) : Parser.Parse(text, OptionsOf(attribute));

    private static string Deep(int levels) => new string('(', levels) + "x" + new string(')', levels);

    private static List<Row> Rows()
    {
        var warnings = new MathExpressionAttribute { WarningsAreErrors = true };
        var sorts = new MathExpressionAttribute { CheckSorts = true };
        var onlyX = new MathExpressionAttribute { Variables = ["x"] };
        var none = new MathExpressionAttribute { Variables = [] };
        var xy = new MathExpressionAttribute { Variables = ["x", "y"] };
        var words = new MathExpressionAttribute { SingleLetterVariables = false, Variables = ["speed", "time"] };
        var wordsX = new MathExpressionAttribute { SingleLetterVariables = false, Variables = ["x"] };
        var needX = new MathExpressionAttribute { RequiredVariables = ["x"] };
        var needXY = new MathExpressionAttribute { RequiredVariables = ["x", "y"] };
        var needXYZ = new MathExpressionAttribute { RequiredVariables = ["x", "y", "z"] };
        var trig = new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Trig };
        var hyperbolicToo = new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Trig | OperatorFamily.Hyperbolic };

        return
        [
            // ----- valid, and blank (the job of [Required]) -----
            new(Any, "x^2 - 5x + 6", null),
            new(Any, "x^2 - 5x + 6 = 0", null),
            new(Any, "", null),
            new(Any, "   ", null),
            new(Any, null, null),
            new(Any, "sin(x)/x", null),
            new(Any, "foo(x)", null),
            new(Any, "1/(x - 1)", null),
            new(Any, "sum(k^2, k, 1, n)", null),
            new(Any, "integrate(x^2, x, 0, 1)", null),
            new(Any, "[[1, 2], [3, 4]]", null),
            new(Any, "√x + π", null),
            new(Any, "x² + 1", null),
            new(Any, "−3x", null),
            new(Any, "theta + alpha", null),
            new(Any, "a < b < c", null),
            new(Any, "forall x in R: x^2 >= 0", null),
            new(Any, "{1, 2, 3}", null),
            new(Any, "(1, 2)", null),
            new(LatexAny, @"\frac{1}{2}", null),
            new(LatexAny, @"x^{2} + 1", null),
            new(LatexAny, @"\sin x", null),
            new(LatexAny, @"x \le 3", null),
            new(LatexAny, @"\int_{0}^{1} x^{2} \,dx", null),
            new(LatexAny, @"\begin{pmatrix}1&2\\3&4\end{pmatrix}", null),

            // ----- Syntax: the parser's span and suggestion, unchanged -----
            new(Any, "x +", MathValidationCode.Syntax),
            new(Any, "(x", MathValidationCode.Syntax),
            new(Any, "x)", MathValidationCode.Syntax),
            new(Any, "sin(", MathValidationCode.Syntax),
            new(Any, "x +* 2", MathValidationCode.Syntax),
            new(Any, "x = = 2", MathValidationCode.Syntax),
            new(Any, "x ^", MathValidationCode.Syntax),
            new(Any, "f(x,)", MathValidationCode.Syntax),
            new(Any, "[1, 2", MathValidationCode.Syntax),
            new(Any, "{1, 2", MathValidationCode.Syntax),
            new(Any, "2 +", MathValidationCode.Syntax),
            new(Any, "sqrt", MathValidationCode.Syntax),
            new(Any, "1e999999999", MathValidationCode.Syntax),
            new(Any, "2.5e-100001", MathValidationCode.Syntax),
            new(Any, Deep(400), MathValidationCode.Syntax),
            new(Any, new string('-', 400) + "x", MathValidationCode.Syntax),
            new(LatexAny, @"\frac{1}{", MathValidationCode.Syntax),
            new(LatexAny, @"x + \unknownmacro", MathValidationCode.Syntax),
            new(LatexAny, @"\sqrt{", MathValidationCode.Syntax),
            new(LatexAny, "1e999999999", MathValidationCode.Syntax),
            new(LatexAny, Deep(400), MathValidationCode.Syntax),
            new(new MathExpressionAttribute { Shape = ExpressionShape.Equation }, "x +", MathValidationCode.Syntax),
            new(onlyX, "x +", MathValidationCode.Syntax),

            // ----- TooLong -----
            new(Any, new string('1', 1_001), MathValidationCode.TooLong, "Value must be at most 1000 characters long."),
            new(new MathExpressionAttribute { MaxLength = 5 }, "x+y+z+", MathValidationCode.TooLong, "Value must be at most 5 characters long."),
            new(new MathExpressionAttribute { MaxLength = 5 }, "x+y+z", null),

            // ----- parser warnings: accepted by default, errors with WarningsAreErrors -----
            new(Any, "1/2x", null),
            new(warnings, "1/2x", MathValidationCode.Ambiguous, "Value is ambiguous: In 'a/bc' the product bc is the divisor only if parenthesized; this is read as (a/b)·c. Write a/(b c) for the other meaning."),
            new(warnings, "a/bc", MathValidationCode.Ambiguous),
            new(warnings, "(1/2)x", null),
            new(warnings, "1/(2x)", null),
            new(Any, "sqr(x)", null),
            new(warnings, "sqr(x)", MathValidationCode.UnknownFunction, "Value uses a function that is not known: 'sqr' is treated as a user-defined function. Did you mean sqrt(x)?"),
            new(warnings, "sinh2(x)", MathValidationCode.UnknownFunction, "Value uses a function that is not known: 'sinh2' is treated as a user-defined function. Did you mean sinh(x)?"),
            new(warnings, "gama(x)", MathValidationCode.UnknownFunction),
            new(warnings, "sinn(2)", null),   // the parser reads a known function name followed by letters as sin(n*2), with no warning
            new(warnings, "f(x) + g(y)", null),
            new(warnings, "sin(x) + cos(x)", null),
            new(warnings, "1/2x + sqr(x)", MathValidationCode.Ambiguous),

            // ----- sorts -----
            new(Any, "[[1, 2], [3, 4]] + [[1, 2, 3]]", null),
            new(sorts, "[[1, 2], [3, 4]] + [[1, 2, 3]]", MathValidationCode.IllSorted),
            new(sorts, "x + {1, 2}", MathValidationCode.IllSorted),
            new(sorts, "{1} + 2", MathValidationCode.IllSorted),
            new(sorts, "[[1, 2], [3, 4]]*[[1, 2, 3]]", MathValidationCode.IllSorted),
            new(sorts, "x + [[1, 2]]", MathValidationCode.IllSorted),
            new(sorts, "[[1, 2]] + [[3, 4]]", null),
            new(sorts, "x + 1", null),
            new(sorts, "sin([[1, 2]])", null),
            new(sorts, "x^2 + 5x + 6 = 0", null),

            // ----- Variables: free, non-function symbols only -----
            new(onlyX, "x + 1", null),
            new(onlyX, "x + y", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: y."),
            new(onlyX, "x + y + z", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: y, z."),
            new(onlyX, "f(x) + g(x)", null),
            new(onlyX, "f'(x)", null),
            new(onlyX, "e^x + pi + I", null),
            new(onlyX, "sum(k, k, 1, x)", null),
            new(onlyX, "sum(k, k, 1, n)", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: n."),
            new(onlyX, "integrate(t, t, 0, x)", null),
            new(onlyX, "forall t in R: t < x", null),
            new(onlyX, "2", null),
            new(onlyX, "theta", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: θ."),
            new(none, "x", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: x."),
            new(none, "2 + pi + e + I + 1/3", null),
            new(none, "forall t in R: t^2 >= 0", null),
            new(none, "sin(1)", null),
            new(xy, "x + y", null),
            new(xy, "x*y*z", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: z."),
            new(new MathExpressionAttribute { Variables = ["theta"] }, "theta + 1", null),
            new(new MathExpressionAttribute { Variables = ["θ"] }, "theta + 1", null),
            new(new MathExpressionAttribute { Variables = ["theta"] }, "alpha", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: α."),
            new(new MathExpressionAttribute { Variables = ["x_1", "x_2"] }, "x_1 + x_2", null),
            new(new MathExpressionAttribute { Variables = ["x_1"] }, "x_1 + x_2", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: x_2."),

            // ----- names as the parser reads them (pinned: foo(x) is f * oo * x unless SingleLetterVariables is off) -----
            new(onlyX, "foo(x)", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: f."),
            new(wordsX, "foo(x)", null),
            new(words, "speed*time", null),
            new(words, "speed*time*x", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: x."),
            new(new MathExpressionAttribute { SingleLetterVariables = false, Variables = ["speed"] }, "speed*time", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: time."),
            new(new MathExpressionAttribute { SingleLetterVariables = false, Variables = ["speed", "t"] }, "speed(t)", null),
            new(onlyX, "xy", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: y."),
            new(new MathExpressionAttribute { Variables = ["x", "y"] }, "xy", null),
            new(new MathExpressionAttribute { SingleLetterVariables = false, Variables = ["x", "y"] }, "xy", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: xy."),
            new(trig, "foo(x)", null),
            new(trig, "sqr(x)", null),

            // ----- RequiredVariables -----
            new(needX, "x + 1", null),
            new(needX, "f(x)", null),
            new(needX, "5", MathValidationCode.MissingVariable, "Value must use: x."),
            new(needX, "y", MathValidationCode.MissingVariable, "Value must use: x."),
            new(needX, "sum(x, x, 1, 5)", MathValidationCode.MissingVariable, "Value must use: x."),
            new(needXY, "x*y", null),
            new(needXY, "x", MathValidationCode.MissingVariable, "Value must use: y."),
            new(needXYZ, "x", MathValidationCode.MissingVariable, "Value must use: y, z."),
            new(new MathExpressionAttribute { Variables = ["x", "y"], RequiredVariables = ["x"] }, "y", MathValidationCode.MissingVariable, "Value must use: x."),
            new(new MathExpressionAttribute { Variables = ["x", "y"], RequiredVariables = ["x"] }, "x + z", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: z."),

            // ----- DisallowedFamilies: operators of a family, not unknown names -----
            new(trig, "x^2", null),
            new(trig, "sin(x)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: sin."),
            new(trig, "x + cos(x)*tan(x)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: cos, tan."),
            new(trig, "arcsin(x)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: arcsin."),
            new(trig, "sinh(x)", null),
            new(hyperbolicToo, "sinh(x) + sin(x)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: sin, sinh."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.ExpLog }, "exp(x)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: exp."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.ExpLog }, "ln(x) + log(x, 2)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: ln, log."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.ExpLog }, "sin(x)", null),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Calculus }, "sum(k, k, 1, n)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: sum."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Calculus }, "integrate(x, x, 0, 1)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: integral."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Calculus }, "limit(x, x, 0)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: limit."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.LinearAlgebra }, "[[1, 2], [3, 4]]", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: matrix."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.LinearAlgebra }, "det([[1, 2], [3, 4]])", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: det, matrix."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Set }, "{1, 2}", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: set."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Set }, "[0, 1)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: interval."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Function }, "f(x) + g(y)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: f, g."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Relation }, "x < 1", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: lt."),
            new(new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Trig }, "x < sin(1)", MathValidationCode.DisallowedFunction, "Value uses a function that is not allowed: sin."),

            // ----- the first failing check is reported: parse, warnings, sorts, shape, variables, required variables, families -----
            new(new MathExpressionAttribute { WarningsAreErrors = true, CheckSorts = true, Shape = ExpressionShape.Equation, Variables = ["x"] }, "1/2x + {1}", MathValidationCode.Ambiguous),
            new(new MathExpressionAttribute { CheckSorts = true, Shape = ExpressionShape.Equation, Variables = ["x"] }, "{1} + 2 < y", MathValidationCode.IllSorted),
            new(new MathExpressionAttribute { Shape = ExpressionShape.Equation, Variables = ["x"] }, "y < 2", MathValidationCode.WrongShape),
            new(new MathExpressionAttribute { Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Trig }, "sin(y)", MathValidationCode.UnknownVariable),
            new(new MathExpressionAttribute { RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Trig }, "sin(y)", MathValidationCode.MissingVariable),
            new(new MathExpressionAttribute { RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Trig }, "sin(x)", MathValidationCode.DisallowedFunction),
        ];
    }

    private static List<Row> ShapeRows()
    {
        var expression = new MathExpressionAttribute { Shape = ExpressionShape.Expression };
        var equation = new MathExpressionAttribute { Shape = ExpressionShape.Equation };
        var inequality = new MathExpressionAttribute { Shape = ExpressionShape.Inequality };
        var interval = new MathExpressionAttribute { Shape = ExpressionShape.Interval };
        var strictEquation = new MathEquationAttribute();

        return
        [
            new(expression, "x + 1", null),
            new(expression, "sin(x)", null),
            new(expression, "5", null),
            new(expression, "[[1, 2], [3, 4]]", null),
            new(expression, "{1, 2}", null),
            new(expression, "(1, 2)", null),
            new(expression, "x^2 = 4", MathValidationCode.WrongShape, "Value must be an expression, but it is an equation."),
            new(expression, "x < 3", MathValidationCode.WrongShape, "Value must be an expression, but it is an inequality."),
            new(expression, "[0, 1)", MathValidationCode.WrongShape, "Value must be an expression, but it is an interval."),
            new(expression, "x in R", MathValidationCode.WrongShape, "Value must be an expression, but it is a statement."),
            new(expression, "x < 1 and x > 0", MathValidationCode.WrongShape, "Value must be an expression, but it is a statement."),
            new(expression, "true", MathValidationCode.WrongShape, "Value must be an expression, but it is a statement."),
            new(expression, "forall x in R: x >= 0", MathValidationCode.WrongShape, "Value must be an expression, but it is a statement."),
            new(expression, "not (x < 1)", MathValidationCode.WrongShape, "Value must be an expression, but it is a statement."),
            new(equation, "x^2 = 4", null),
            new(equation, "x^2 - 5x + 6 = 0", null),
            new(equation, "x + 1", MathValidationCode.WrongShape, "Value must be an equation, but it is an expression."),
            new(equation, "x < 3", MathValidationCode.WrongShape, "Value must be an equation, but it is an inequality."),
            new(equation, "x != 3", MathValidationCode.WrongShape, "Value must be an equation, but it is an inequality."),
            new(equation, "a = b = c", MathValidationCode.WrongShape, "Value must be an equation, but it is a statement."),
            new(equation, "x = 1 and y = 2", MathValidationCode.WrongShape, "Value must be an equation, but it is a statement."),
            new(equation, "x = 1 or x = 2", MathValidationCode.WrongShape, "Value must be an equation, but it is a statement."),
            new(equation, "[0, 1)", MathValidationCode.WrongShape, "Value must be an equation, but it is an interval."),
            new(inequality, "x < 3", null),
            new(inequality, "x <= 3", null),
            new(inequality, "x > 3", null),
            new(inequality, "x >= 3", null),
            new(inequality, "x != 3", null),
            new(inequality, "x^2 - 4 >= 0", null),
            new(inequality, "x = 3", MathValidationCode.WrongShape, "Value must be an inequality, but it is an equation."),
            new(inequality, "1 < x < 5", MathValidationCode.WrongShape, "Value must be an inequality, but it is a statement."),
            new(inequality, "x + 1", MathValidationCode.WrongShape, "Value must be an inequality, but it is an expression."),
            new(interval, "[0, 1)", null),
            new(interval, "(0, 1]", null),
            new(interval, "[0, oo)", null),
            new(interval, "[1, 2]", null),
            new(interval, "]0, 1[", null),
            new(interval, "(0, 1)", MathValidationCode.WrongShape, "Value must be an interval, but it is an expression."),
            new(interval, "x < 3", MathValidationCode.WrongShape, "Value must be an interval, but it is an inequality."),
            new(interval, "x", MathValidationCode.WrongShape, "Value must be an interval, but it is an expression."),
            new(strictEquation, "x^2 = 4", null),
            new(strictEquation, "x^2", MathValidationCode.WrongShape, "Value must be an equation, but it is an expression."),
            new(strictEquation, "x < 3", MathValidationCode.WrongShape, "Value must be an equation, but it is an inequality."),
            new(strictEquation, "1 < x < 5", MathValidationCode.WrongShape, "Value must be an equation, but it is a statement."),
            new(new MathEquationAttribute { Format = InputFormat.Latex }, @"x^{2} = 4", null),
            new(new MathEquationAttribute { Format = InputFormat.Latex }, @"x \le 3", MathValidationCode.WrongShape, "Value must be an equation, but it is an inequality."),
            new(new MathExpressionAttribute { Shape = ExpressionShape.Inequality, Format = InputFormat.Latex }, @"x \le 3", null),
            new(new MathEquationAttribute { Variables = ["x"] }, "x^2 = y", MathValidationCode.UnknownVariable, "Value uses a variable that is not allowed: y."),
        ];
    }

    private static void AssertRows(IEnumerable<Row> rows)
    {
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var label = $"{row.Attribute.GetType().Name} on {(row.Value is string s && s.Length > 60 ? s[..57] + "..." : row.Value ?? "null")}";
            var result = row.Attribute.Check(row.Value, "Value", "Member");
            if (result?.Code != row.Code)
            {
                failures.Add($"{label}: expected {row.Code?.ToString() ?? "valid"}, got {result?.Code.ToString() ?? "valid"} ({result?.ErrorMessage})");
                continue;
            }

            if (row.Message is not null && row.Message != result?.ErrorMessage) failures.Add($"{label}: message '{result?.ErrorMessage}' != '{row.Message}'");

            if (row.Code == MathValidationCode.Syntax)
            {
                // Spans, suggestions and text are the parser's, unchanged.
                var error = ParseLikeTheAttribute(row.Attribute, (string)row.Value!).Errors[0];
                if (result!.Span != error.Span) failures.Add($"{label}: span {result.Span} != parser's {error.Span}");
                if (result.Suggestion != error.Suggestion) failures.Add($"{label}: suggestion '{result.Suggestion}' != parser's '{error.Suggestion}'");
                if (result.ErrorMessage != $"Value is not valid: {error.Message}") failures.Add($"{label}: message '{result.ErrorMessage}'");
            }

            // one verdict through every entry point
            Assert.AreEqual(row.Code is null, row.Attribute.IsValid(row.Value), label);
            Assert.AreEqual(row.Code, (row.Attribute.GetValidationResult(row.Value, MathValidationAttributeTests.Context()) as MathValidationResult)?.Code, label);

            // An Expr-typed property gets the same semantic verdicts without parsing.
            if (row.Value is string { Length: > 0 } text && !string.IsNullOrWhiteSpace(text) && row.Code is not (MathValidationCode.Syntax or MathValidationCode.Ambiguous or MathValidationCode.UnknownFunction or MathValidationCode.TooLong))
            {
                var tree = ParseLikeTheAttribute(row.Attribute, text).Expr!;
                var typed = row.Attribute.Check(tree, "Value", "Member");
                if (typed?.Code != row.Code) failures.Add($"{label}: as an Expr the verdict is {typed?.Code.ToString() ?? "valid"}, not {row.Code?.ToString() ?? "valid"}");
                else if (typed?.ErrorMessage != result?.ErrorMessage) failures.Add($"{label}: as an Expr the message is '{typed?.ErrorMessage}', not '{result?.ErrorMessage}'");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void EveryCodeTheAttributesCanReturnIsCoveredByAtLeastSixtyRows()
    {
        var rows = Rows();
        var codes = rows.Select(r => r.Code).Concat(ShapeRows().Select(r => r.Code)).OfType<MathValidationCode>().Distinct().Order().ToList();

        Assert.IsTrue(rows.Count >= 60);
        CollectionAssert.AreEqual(
            new[]
            {
                MathValidationCode.TooLong, MathValidationCode.Syntax, MathValidationCode.Ambiguous, MathValidationCode.UnknownFunction, MathValidationCode.IllSorted,
                MathValidationCode.WrongShape, MathValidationCode.UnknownVariable, MathValidationCode.MissingVariable, MathValidationCode.DisallowedFunction,
            }.Order().ToArray(),
            codes);
        AssertRows(rows);
    }

    [TestMethod]
    public void ShapeTable()
    {
        var rows = ShapeRows();

        Assert.IsTrue(rows.Count >= 30);
        AssertRows(rows);
    }

    [TestMethod]
    public void SortProblemsNameTheOperatorAndTheSortsInTheMessage()
    {
        var result = new MathExpressionAttribute { CheckSorts = true }.Check("[[1, 2], [3, 4]] + [[1, 2, 3]]")!;

        Assert.AreEqual(MathValidationCode.IllSorted, result.Code);
        StringAssert.Contains(result.ErrorMessage, "Operator 'add' cannot take arguments of sorts (Matrix(2, 2, Natural), Matrix(1, 3, Natural))");
        Assert.IsNull(result.Span);
    }

    [TestMethod]
    public void AnOpenIntervalSuggestionIsOfferedForAPair()
    {
        var interval = new MathExpressionAttribute { Shape = ExpressionShape.Interval };

        Assert.AreEqual("For an open interval write ]0, 1[.", interval.Check("(0, 1)")!.Suggestion);
        Assert.AreEqual("For an open interval write ]-oo, 2[.", interval.Check("(-oo, 2)")!.Suggestion);
        Assert.IsNull(interval.Check("(0, 1, 2)")!.Suggestion);
        Assert.IsNull(interval.Check("x < 3")!.Suggestion);
        Assert.IsNull(interval.Check("]0, 1[") as object, "the suggested spelling is itself an interval");
    }

    [TestMethod]
    public void ParserWarningsKeepTheirSpan()
    {
        var warnings = new MathExpressionAttribute { WarningsAreErrors = true };

        Assert.AreEqual(new TextSpan(3, 1), warnings.Check("1/2x")!.Span);
        Assert.AreEqual(new TextSpan(0, 3), warnings.Check("sqr(x)")!.Span);
        Assert.IsNull(warnings.Check("1/2x")!.Suggestion);
    }

    [TestMethod]
    public void ExprValuesAreCheckedWithoutParsing()
    {
        // A tree built in code: no text, so no Syntax, no warnings and no length limit; shape, variables, families and sorts still apply.
        var x = new Symbol("x");
        var y = new Symbol("y");
        Expr equation = Sym.Eq(Sym.Add(Sym.Pow(x, Sym.Number(2)), Sym.Number(-4)), Sym.Number(0));

        Assert.IsNull(new MathEquationAttribute { Variables = ["x"] }.Check(equation));
        Assert.AreEqual(MathValidationCode.UnknownVariable, new MathEquationAttribute { Variables = ["y"] }.Check(equation)!.Code);
        Assert.AreEqual(MathValidationCode.MissingVariable, new MathEquationAttribute { RequiredVariables = ["x"] }.Check(Sym.Eq(y, Sym.Number(1)))!.Code);
        Assert.AreEqual(MathValidationCode.WrongShape, new MathEquationAttribute().Check(Sym.Add(x, y))!.Code);
        Assert.AreEqual(MathValidationCode.DisallowedFunction, new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.Trig }.Check(Sym.Sin(x))!.Code);
        Assert.IsNull(new MathExpressionAttribute { WarningsAreErrors = true }.Check(Sym.Mul(Sym.Rational(1, 2), x)), "no text, so no parser warning");
        Assert.IsNull(new MathExpressionAttribute { MaxLength = 1 }.Check(equation), "MaxLength applies to text only");
        Assert.IsTrue(new MathExpressionAttribute().IsValid(equation));

        // other types are API misuse
        Assert.ThrowsExactly<InvalidOperationException>(() => new MathExpressionAttribute().IsValid(42));
        Assert.ThrowsExactly<InvalidOperationException>(() => new MathEquationAttribute().Check(new object()));
    }

    [TestMethod]
    public void ConfigurationErrorsThrowAsSpecified()
    {
        var rows = new (string Label, MathExpressionAttribute Attribute, string Message)[]
        {
            ("constant as variable", new MathExpressionAttribute { Variables = ["e"] }, "Variables contains 'e', which is not a variable name (it is a constant)."),
            ("pi as required variable", new MathExpressionAttribute { RequiredVariables = ["pi"] }, "RequiredVariables contains 'pi', which is not a variable name (it is a constant)."),
            ("expression as variable", new MathExpressionAttribute { Variables = ["x+1"] }, "Variables contains 'x+1', which is not a variable name."),
            ("call as variable", new MathExpressionAttribute { Variables = ["f(x)"] }, "Variables contains 'f(x)', which is not a variable name."),
            ("product of letters as variable", new MathExpressionAttribute { Variables = ["xy"] }, "Variables contains 'xy', which is not a variable name."),
            ("unparsable variable", new MathExpressionAttribute { RequiredVariables = ["x +"] }, "RequiredVariables contains 'x +', which is not a variable name."),
            ("empty variable name", new MathExpressionAttribute { Variables = [""] }, "Variables contains an empty name."),
            ("blank required name", new MathExpressionAttribute { RequiredVariables = ["x", "  "] }, "RequiredVariables contains an empty name."),
            ("null variable name", new MathExpressionAttribute { Variables = [null!] }, "Variables contains an empty name."),
            ("required but not allowed", new MathExpressionAttribute { Variables = ["x"], RequiredVariables = ["y"] }, "RequiredVariables contains 'y', which Variables does not allow."),
            ("undefined format", new MathExpressionAttribute { Format = (InputFormat)7 }, "Format 7 is not a defined value."),
            ("undefined shape", new MathExpressionAttribute { Shape = (ExpressionShape)9 }, "Shape 9 is not a defined value."),
            ("equation with another shape", new MathEquationAttribute { Shape = ExpressionShape.Inequality }, "Shape is fixed to Equation by MathEquation and cannot be Inequality"),
            ("equation with Any", new MathEquationAttribute { Shape = ExpressionShape.Any }, "Shape is fixed to Equation by MathEquation and cannot be Any"),
            ("equation with a bad variable", new MathEquationAttribute { Variables = ["e"] }, "Variables contains 'e'"),
            ("MaxLength 0", new MathExpressionAttribute { MaxLength = 0 }, "MaxLength must be between 1 and 100000 but is 0."),
            ("MaxLength 100001", new MathEquationAttribute { MaxLength = 100_001 }, "MaxLength must be between 1 and 100000 but is 100001."),
            ("undefined placeholder", new MathExpressionAttribute { ErrorMessage = "{0} {3}" }, "{0} {3}"),
            ("undefined placeholder, equation", new MathEquationAttribute { ErrorMessage = "{5}" }, "{5}"),
            ("latex name that is not a variable", new MathExpressionAttribute { Format = InputFormat.Latex, Variables = [@"\pi"] }, "Variables contains '\\pi'"),
        };

        foreach (var (label, attribute, message) in rows)
        {
            var type = attribute.GetType().Name;
            var text = attribute.GetConfigurationError();
            Assert.IsNotNull(text, label);
            StringAssert.StartsWith(text, type + ": ", label);
            StringAssert.Contains(text, message, label);
            foreach (var value in new object?[] { null, "", "x", "x +", new Symbol("x") })
            {
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid(value), label).Message, label);
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.Check(value), label).Message, label);
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.GetValidationResult(value, MathValidationAttributeTests.Context()), label).Message, label);
            }
        }

        // valid configurations
        foreach (var attribute in new MathExpressionAttribute[]
        {
            new(),
            new() { Variables = [] },
            new() { Variables = ["x", "y", "theta", "θ", "x_1"], RequiredVariables = ["x", "theta"] },
            new() { SingleLetterVariables = false, Variables = ["speed", "time"], RequiredVariables = ["speed"] },
            new() { Format = InputFormat.Latex, Variables = ["x", @"\theta"] },
            new() { DisallowedFamilies = OperatorFamily.Trig | OperatorFamily.Hyperbolic, Shape = ExpressionShape.Interval },
            new MathEquationAttribute(),
            new MathEquationAttribute { Shape = ExpressionShape.Equation, CheckSorts = true, WarningsAreErrors = true },
            new() { MaxLength = 100_000, ErrorMessage = "{0}: {1} / {2}" },
        })
        {
            Assert.IsNull(attribute.GetConfigurationError(), attribute.GetType().Name + " " + attribute.GetConfigurationError());
        }
    }

    [TestMethod]
    public void DefaultsAndTheFixedShapeOfMathEquation()
    {
        var expression = new MathExpressionAttribute();
        var equation = new MathEquationAttribute();

        Assert.AreEqual(InputFormat.Text, expression.Format);
        Assert.AreEqual(ExpressionShape.Any, expression.Shape);
        Assert.IsNull(expression.Variables);
        Assert.IsNull(expression.RequiredVariables);
        Assert.AreEqual(OperatorFamily.None, expression.DisallowedFamilies);
        Assert.IsFalse(expression.WarningsAreErrors);
        Assert.IsFalse(expression.CheckSorts);
        Assert.IsTrue(expression.SingleLetterVariables);
        Assert.IsFalse(expression.LogMeansNatural);
        Assert.AreEqual(1_000, expression.MaxLength);
        Assert.AreEqual(ExpressionShape.Equation, equation.Shape);
        Assert.IsTrue(typeof(MathEquationAttribute).IsSealed);
        Assert.IsFalse(typeof(MathExpressionAttribute).IsSealed);
        Assert.IsTrue(typeof(MathEquationAttribute).IsSubclassOf(typeof(MathExpressionAttribute)));
        Assert.AreEqual(typeof(MathExpressionAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), true).Length, typeof(MathEquationAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), true).Length);
    }

    [TestMethod]
    public void BareLogFollowsLogMeansNatural()
    {
        // log x is base 10 unless LogMeansNatural; either way the attribute parses it with the same options as the library does.
        Assert.IsNull(new MathExpressionAttribute().Check("log x + ln x"));
        Assert.IsNull(new MathExpressionAttribute { LogMeansNatural = true }.Check("log x + ln x"));
        Assert.AreEqual(MathValidationCode.DisallowedFunction, new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.ExpLog }.Check("log x")!.Code);
        Assert.AreEqual("Value uses a function that is not allowed: ln.", new MathExpressionAttribute { LogMeansNatural = true, DisallowedFamilies = OperatorFamily.ExpLog }.Check("log x")!.ErrorMessage);
        Assert.AreEqual("Value uses a function that is not allowed: log.", new MathExpressionAttribute { DisallowedFamilies = OperatorFamily.ExpLog }.Check("log x")!.ErrorMessage);
    }

    [TestMethod]
    public void VerdictsAreTheSameUnderEveryCulture()
    {
        var rows = Rows().Concat(ShapeRows()).ToList();

        string Describe(CultureInfo culture)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return string.Join("\n", rows.Select(r =>
                {
                    var result = r.Attribute.Check(r.Value, "Value", "Member");
                    return result is null ? "valid" : $"{result.Code}|{result.Span}|{result.Suggestion}|{result.ErrorMessage}";
                }));
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

    [TestMethod]
    public void AdversarialInputsAreCheapBecauseNothingIsEvaluated()
    {
        var general = new MathExpressionAttribute { CheckSorts = true, Variables = ["x"], RequiredVariables = ["x"], DisallowedFamilies = OperatorFamily.Trig, Shape = ExpressionShape.Any };
        _ = general.Check("x + 1");
        _ = general.Check("x +");   // warm up the parser, the checker and the resx

        foreach (var text in new[] { "2^999999999*x", "(x+1)^999999999", "9^9^9*x", "x^x^x^x^x^x", "x + 1e100000", "x*1e-100000", "x + 1e100000 + 1e-100000", "x" + string.Concat(Enumerable.Repeat("+x", 300)), "x" + new string('!', 900) })
        {
            foreach (var attribute in new MathExpressionAttribute[] { Any, general, new MathEquationAttribute { CheckSorts = true } })
            {
                var watch = Stopwatch.StartNew();
                var result = attribute.Check(text);
                watch.Stop();

                Assert.IsTrue(watch.ElapsedMilliseconds < 50, $"{attribute.GetType().Name} on {text[..Math.Min(40, text.Length)]} took {watch.ElapsedMilliseconds} ms");
                if (ReferenceEquals(attribute, Any)) Assert.IsNull(result, text);
            }
        }

        foreach (var text in new[] { "1e100000", "1e-100000", "x + 1e100000 + 1e-100000", "2^999999999*x", "(x+1)^999999999", "9^9^9", "x^x^x^x^x^x" })
        {
            Assert.IsNull(Any.Check(text), text);
        }

        var tooDeep = Any.Check(Deep(400))!;
        Assert.AreEqual(MathValidationCode.Syntax, tooDeep.Code);
        Assert.AreEqual(new TextSpan(150, 1), tooDeep.Span, "400 nested parentheses fail at column 151");
        Assert.AreEqual(MathValidationCode.Syntax, Any.Check("1e999999999")!.Code);
        Assert.AreEqual(new TextSpan(0, 11), Any.Check("1e999999999")!.Span);
    }

    [TestMethod]
    public void LatexNestingIsBoundedToo()
    {
        // The default length limit would answer TooLong first; raise it so the parser's own depth limit is what is tested.
        var latex = new MathExpressionAttribute { Format = InputFormat.Latex, MaxLength = 100_000 };

        foreach (var n in new[] { 400, 900, 5_000 })
        {
            foreach (var text in new[]
            {
                Deep(n),
                string.Concat(Enumerable.Repeat(@"\frac{1}{", n)) + "x" + new string('}', n),
                string.Concat(Enumerable.Repeat("x^{", n)) + "x" + new string('}', n),
                string.Concat(Enumerable.Repeat(@"\sqrt{", n)) + "x" + new string('}', n),
                string.Concat(Enumerable.Repeat(@"\left(", n)) + "x" + string.Concat(Enumerable.Repeat(@"\right)", n)),
            })
            {
                var result = latex.Check(text);
                Assert.AreEqual(MathValidationCode.Syntax, result?.Code, $"{n} levels: {text[..20]}");
                StringAssert.Contains(result!.ErrorMessage, "nested too deeply");
            }
        }

        // and with the default limit the same text is rejected before it is parsed
        Assert.AreEqual(MathValidationCode.TooLong, LatexAny.Check(string.Concat(Enumerable.Repeat(@"\sqrt{", 400)) + "x" + new string('}', 400))!.Code);
    }
}
