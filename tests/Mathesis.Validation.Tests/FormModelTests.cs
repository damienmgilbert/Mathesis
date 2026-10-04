using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace Mathesis.Validation.Tests;

/// <summary>
/// Three form models validated through the BCL entry points a UI framework uses (<c>Validator.TryValidateObject</c> with all properties,
/// <c>TryValidateProperty</c>, <c>TryValidateValue</c>), with <c>[Display]</c>, <c>[Required]</c> and an <see cref="IValidatableObject"/> rule across
/// properties that runs only when every property is valid (PLAN-M9 Phase 5, ADR-17).
/// </summary>
[TestClass]
public class FormModelTests
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

    // ----- the models -----

    private sealed class CalculatorForm : IValidatableObject
    {
        [Required, MathExpression(Variables = ["x"], WarningsAreErrors = true)]
        [Display(Name = "Expression")]
        public string? Expression { get; set; }

        [RationalNumber]
        [Display(Name = "Value of x")]
        public string? X { get; set; }

        [ExactRange("1/100", "10", MinimumIsExclusive = false)]
        [Display(Name = "Step")]
        public decimal? Step { get; set; }

        // Cross-property rule (ADR-17): an expression in x needs a value for x. It runs only when every property is valid.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Expression is not null && string.IsNullOrWhiteSpace(X) && Expr.Parse(Expression).FreeSymbols.Any(s => s.Name == "x"))
            {
                yield return new ValidationResult("Give a value for x to evaluate the expression.", [nameof(X)]);
            }
        }
    }

    private sealed class PolynomialRootsForm : IValidatableObject
    {
        [Required, PolynomialExpression("x", MaxDegree = 6)]
        [Display(Name = "Polynomial")]
        public string? Polynomial { get; set; }

        [ExactRange("-100", "100", MinimumIsExclusive = true)]
        [Display(Name = "Lower bound")]
        public string? LowerBound { get; set; }

        [ExactRange("-100", "100", MaximumIsExclusive = true)]
        [Display(Name = "Upper bound")]
        public string? UpperBound { get; set; }

        // Exact comparison of the two bounds; both texts are valid numbers when this runs.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LowerBound is not null && UpperBound is not null && BigRational.Parse(LowerBound.Replace('−', '-')) >= BigRational.Parse(UpperBound.Replace('−', '-')))
            {
                yield return new ValidationResult("The lower bound must be below the upper bound.", [nameof(LowerBound), nameof(UpperBound)]);
            }
        }
    }

    private sealed class LinearSystemForm : IValidatableObject
    {
        [Required, MathMatrix(Rows = 3, Columns = 3, Square = true)]
        [Display(Name = "Coefficients")]
        public string? Coefficients { get; set; }

        [Required, MathMatrix(Columns = 1)]
        [Display(Name = "Right-hand side")]
        public string? RightHandSide { get; set; }

        [NonZero]
        [Display(Name = "Scale")]
        public string? Scale { get; set; }

        // The right-hand side needs one row per row of the coefficients; both are valid matrices when this runs.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var rows = ((MatrixLiteral)Expr.Parse(Coefficients!)).Rows;
            if (((MatrixLiteral)Expr.Parse(RightHandSide!)).Rows != rows)
            {
                yield return new ValidationResult($"The right-hand side must have {rows} rows.", [nameof(RightHandSide)]);
            }
        }
    }

    // ----- helpers -----

    private static List<ValidationResult> ValidateAll(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    /// <summary>One line per result: its type, members and message, so a test asserts all three at once.</summary>
    private static string[] Describe(IEnumerable<ValidationResult> results) =>
        [.. results.Select(r => $"{(r is MathValidationResult m ? $"Math:{m.Code}" : "Plain")} [{string.Join(",", r.MemberNames)}] {r.ErrorMessage}")];

    // ----- calculator -----

    [TestMethod]
    public void CalculatorFormReportsEachPropertyWithItsDisplayName()
    {
        var form = new CalculatorForm { Expression = "x^2 + y", X = "0,5", Step = 20m };

        CollectionAssert.AreEqual(
            new[]
            {
                "Math:UnknownVariable [Expression] Expression uses a variable that is not allowed: y.",
                "Math:NotANumber [X] Value of x must be a number, for example 3, -1/2 or 0.25.",
                "Math:OutOfRange [Step] Step must be in the range [1/100, 10].",
            },
            Describe(ValidateAll(form)));

        var number = (MathValidationResult)ValidateAll(form)[1];
        Assert.AreEqual("Use '.' as the decimal point, for example 0.5. Digit grouping is not accepted.", number.Suggestion);
        Assert.AreEqual(new Mathesis.Symbolics.Parsing.TextSpan(1, 1), number.Span);
    }

    [TestMethod]
    public void CalculatorFormRequiredAndWarnings()
    {
        CollectionAssert.AreEqual(new[] { "Plain [Expression] The Expression field is required." }, Describe(ValidateAll(new CalculatorForm { Expression = " " })));
        CollectionAssert.AreEqual(new[] { "Math:Ambiguous [Expression] Expression is ambiguous: In 'a/bc' the product bc is the divisor only if parenthesized; this is read as (a/b)·c. Write a/(b c) for the other meaning." }, Describe(ValidateAll(new CalculatorForm { Expression = "1/2x", X = "1" })));
    }

    [TestMethod]
    public void CalculatorCrossPropertyRuleRunsOnlyWhenEveryPropertyIsValid()
    {
        // every property valid, x missing: the object-level rule reports
        CollectionAssert.AreEqual(new[] { "Plain [X] Give a value for x to evaluate the expression." }, Describe(ValidateAll(new CalculatorForm { Expression = "x^2 + 1" })));

        // a property error suppresses the object-level rule
        CollectionAssert.AreEqual(new[] { "Math:OutOfRange [Step] Step must be in the range [1/100, 10]." }, Describe(ValidateAll(new CalculatorForm { Expression = "x^2 + 1", Step = 0m })));

        // all good
        Assert.AreEqual(0, ValidateAll(new CalculatorForm { Expression = "x^2 + 1", X = "−1/3", Step = 0.01m }).Count);
        var constant = new CalculatorForm { Expression = "2 + 3" };
        Assert.IsTrue(Validator.TryValidateObject(constant, new ValidationContext(constant), [], validateAllProperties: true), "an expression without x needs no value for x");
    }

    // ----- polynomial roots -----

    [TestMethod]
    public void PolynomialRootsFormReportsEachProperty()
    {
        var form = new PolynomialRootsForm { Polynomial = "x^7 - 1", LowerBound = "-100", UpperBound = "100" };

        CollectionAssert.AreEqual(
            new[]
            {
                "Math:DegreeTooHigh [Polynomial] Polynomial has degree 7, but the highest allowed degree is 6.",
                "Math:OutOfRange [LowerBound] Lower bound must be in the range (-100, 100].",
                "Math:OutOfRange [UpperBound] Upper bound must be in the range [-100, 100).",
            },
            Describe(ValidateAll(form)));

        CollectionAssert.AreEqual(new[] { "Math:NotAPolynomial [Polynomial] Polynomial must be a polynomial, but it contains sqrt(x)." }, Describe(ValidateAll(new PolynomialRootsForm { Polynomial = "x^2 + sqrt(x)" })));
        CollectionAssert.AreEqual(new[] { "Math:WrongShape [Polynomial] Polynomial must be a polynomial in x, but it is an equation." }, Describe(ValidateAll(new PolynomialRootsForm { Polynomial = "x^2 = 4" })));
        CollectionAssert.AreEqual(new[] { "Plain [Polynomial] The Polynomial field is required." }, Describe(ValidateAll(new PolynomialRootsForm { Polynomial = "" })));
    }

    [TestMethod]
    public void PolynomialRootsCrossPropertyRuleComparesTheBoundsExactly()
    {
        // 1/3 and 0.3333333333 differ, and the lower bound must be below the upper one
        Assert.AreEqual(0, ValidateAll(new PolynomialRootsForm { Polynomial = "x^2 - 2", LowerBound = "0.3333333333", UpperBound = "1/3" }).Count);
        CollectionAssert.AreEqual(new[] { "Plain [LowerBound,UpperBound] The lower bound must be below the upper bound." }, Describe(ValidateAll(new PolynomialRootsForm { Polynomial = "x^2 - 2", LowerBound = "1/3", UpperBound = "0.(3)" })));

        // not run while a property is invalid
        CollectionAssert.AreEqual(new[] { "Math:NotANumber [UpperBound] Upper bound must be a number, for example 3, -1/2 or 0.25." }, Describe(ValidateAll(new PolynomialRootsForm { Polynomial = "x^2 - 2", LowerBound = "5", UpperBound = "five" })));
    }

    // ----- linear system -----

    [TestMethod]
    public void LinearSystemFormReportsEachProperty()
    {
        var form = new LinearSystemForm { Coefficients = "[[1, 2, 3], [4, 5, 6]]", RightHandSide = "[1, 2]", Scale = "0.0" };

        CollectionAssert.AreEqual(
            new[]
            {
                "Math:WrongDimensions [Coefficients] Coefficients must be a matrix of size 3×3, but it is 2×3.",
                "Math:NotAMatrix [RightHandSide] Right-hand side must be a matrix, for example [[1, 2], [3, 4]].",
                "Math:Zero [Scale] Scale must not be zero.",
            },
            Describe(ValidateAll(form)));
        Assert.AreEqual("For a column vector write [[1], [2]].", ((MathValidationResult)ValidateAll(form)[1]).Suggestion);

        CollectionAssert.AreEqual(new[] { "Math:NonNumericEntry [Coefficients] Coefficients must contain only numbers; the entry in row 2, column 3 is not a number." }, Describe(ValidateAll(new LinearSystemForm { Coefficients = "[[1, 0, 0], [0, 1, a], [0, 0, 1]]", RightHandSide = "[1, 2, 3]" })));
        CollectionAssert.AreEqual(new[] { "Math:Syntax [Coefficients] Coefficients is not valid: Matrix rows must have equal length: expected 3 entries but found 2." }, Describe(ValidateAll(new LinearSystemForm { Coefficients = "[[1, 0, 0], [0, 1]]", RightHandSide = "[1, 2, 3]" })));
    }

    [TestMethod]
    public void LinearSystemCrossPropertyRuleRunsOnlyWhenEveryPropertyIsValid()
    {
        CollectionAssert.AreEqual(new[] { "Plain [RightHandSide] The right-hand side must have 3 rows." }, Describe(ValidateAll(new LinearSystemForm { Coefficients = "[[1, 0, 0], [0, 1, 0], [0, 0, 1]]", RightHandSide = "[1, 2, 3, 4]" })));
        Assert.AreEqual(0, ValidateAll(new LinearSystemForm { Coefficients = "[[1, 0, 0], [0, 1, 0], [0, 0, 1]]", RightHandSide = "[1, -1/2, 0.25]", Scale = "1e-100000" }).Count);
    }

    // ----- the property and value entry points -----

    [TestMethod]
    public void TryValidatePropertyAndTryValidateValueReturnTheSameResults()
    {
        var form = new PolynomialRootsForm();

        var results = new List<ValidationResult>();
        Assert.IsFalse(Validator.TryValidateProperty("1/x", new ValidationContext(form) { MemberName = nameof(PolynomialRootsForm.Polynomial) }, results));
        CollectionAssert.AreEqual(new[] { "Math:NotAPolynomial [Polynomial] Polynomial must be a polynomial, but it contains 1/x." }, Describe(results));

        results.Clear();
        Assert.IsFalse(Validator.TryValidateProperty(null, new ValidationContext(form) { MemberName = nameof(PolynomialRootsForm.Polynomial) }, results));
        CollectionAssert.AreEqual(new[] { "Plain [Polynomial] The Polynomial field is required." }, Describe(results));

        // TryValidateValue with an explicit display name (the trim-safe context) and no member
        results.Clear();
        var attributes = new ValidationAttribute[] { new RequiredAttribute(), new PolynomialExpressionAttribute("t") { MaxDegree = 2 } };
        Assert.IsFalse(Validator.TryValidateValue("t^3", new ValidationContext(form, "Height", null, null), results, attributes));
        CollectionAssert.AreEqual(new[] { "Math:DegreeTooHigh [] Height has degree 3, but the highest allowed degree is 2." }, Describe(results));

        results.Clear();
        Assert.IsTrue(Validator.TryValidateValue("t^2 - 1", new ValidationContext(form, "Height", null, null), results, attributes));
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void TheTypedCheckGivesTheSameVerdictAsTheValidator()
    {
        // ADR-17: IValidatableObject or code can call Check directly, without a ValidationContext.
        var attribute = new MathMatrixAttribute { Rows = 3, Columns = 3, Square = true };
        var viaValidator = (MathValidationResult)ValidateAll(new LinearSystemForm { Coefficients = "[[1]]", RightHandSide = "[1, 2, 3]" }).Single();
        var direct = attribute.Check("[[1]]", "Coefficients", nameof(LinearSystemForm.Coefficients))!;

        Assert.AreEqual(viaValidator.Code, direct.Code);
        Assert.AreEqual(viaValidator.ErrorMessage, direct.ErrorMessage);
        CollectionAssert.AreEqual(viaValidator.MemberNames.ToArray(), direct.MemberNames.ToArray());
    }
}
