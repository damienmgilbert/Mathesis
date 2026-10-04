using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation.Tests;

/// <summary>The numeric attributes <see cref="RationalNumberAttribute"/>, <see cref="ExactRangeAttribute"/> and <see cref="NonZeroAttribute"/> (PLAN-M9 Phase 2).</summary>
[TestClass]
public class NumberAttributeTests
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

    /// <summary>One expectation: the attribute's verdict (<c>null</c> code is valid), and optionally the exact suggestion and span.</summary>
    private sealed record Row(MathValidationAttribute Attribute, object? Value, MathValidationCode? Code, string? Suggestion = null, TextSpan? Span = null);

    private static RationalNumberAttribute Rational(bool integerOnly = false, bool fractions = true, bool decimals = true) =>
        new() { IntegerOnly = integerOnly, AllowFractions = fractions, AllowDecimals = decimals };

    private static void AssertRows(IEnumerable<Row> rows)
    {
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var label = $"{row.Attribute.GetType().Name} on {row.Value ?? "null"}";
            var result = row.Attribute.Check(row.Value, "Value", "Member");
            if (result?.Code != row.Code) failures.Add($"{label}: expected {row.Code?.ToString() ?? "valid"}, got {result?.Code.ToString() ?? "valid"}");
            else if (result is not null && row.Suggestion != result.Suggestion) failures.Add($"{label}: suggestion '{result.Suggestion}' != '{row.Suggestion}'");
            else if (result is not null && row.Span != result.Span) failures.Add($"{label}: span {result.Span} != {row.Span}");
            Assert.AreEqual(row.Code is null, row.Attribute.IsValid(row.Value), label);
            var viaContext = row.Attribute.GetValidationResult(row.Value, MathValidationAttributeTests.Context());
            Assert.AreEqual(row.Code, (viaContext as MathValidationResult)?.Code, label);
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void RejectionAndAcceptanceTableForText()
    {
        var number = Rational();
        var rows = new List<Row>
        {
            // blank is the job of [Required]
            new(number, "", null),
            new(number, "   ", null),
            new(number, null, null),
            // not numbers
            new(number, "abc", MathValidationCode.NotANumber),
            new(number, "--1", MathValidationCode.NotANumber),
            new(number, "1.2.3", MathValidationCode.NotANumber),
            new(number, ".", MathValidationCode.NotANumber),
            new(number, "-", MathValidationCode.NotANumber),
            new(number, "+", MathValidationCode.NotANumber),
            new(number, "1/", MathValidationCode.NotANumber),
            new(number, "/2", MathValidationCode.NotANumber),
            new(number, "1/2/3", MathValidationCode.NotANumber),
            new(number, "1/-2", MathValidationCode.NotANumber),
            new(number, "(1)/(2)", MathValidationCode.NotANumber),
            new(number, "0x10", MathValidationCode.NotANumber),
            new(number, "1e", MathValidationCode.NotANumber),
            new(number, "e5", MathValidationCode.NotANumber),
            new(number, "1.5e3.2", MathValidationCode.NotANumber),
            new(number, "NaN", MathValidationCode.NotANumber),
            new(number, "Infinity", MathValidationCode.NotANumber),
            new(number, "∞", MathValidationCode.NotANumber),
            new(number, "１２", MathValidationCode.NotANumber),
            new(number, "٣", MathValidationCode.NotANumber),
            // an exponent beyond the limit is not a number, never zero
            new(number, "1e999999999", MathValidationCode.NotANumber, "The exponent must be between -100000 and 100000."),
            new(number, "1e100001", MathValidationCode.NotANumber, "The exponent must be between -100000 and 100000."),
            new(number, "2.5e-100001", MathValidationCode.NotANumber, "The exponent must be between -100000 and 100000."),
            // a zero denominator
            new(number, "1/0", MathValidationCode.NotANumber, "The denominator of a fraction cannot be zero.", new TextSpan(2, 1)),
            new(number, "-3/000", MathValidationCode.NotANumber, "The denominator of a fraction cannot be zero.", new TextSpan(3, 3)),
            // the decimal point is '.', digit grouping is not accepted
            new(number, "0,5", MathValidationCode.NotANumber, "Use '.' as the decimal point, for example 0.5. Digit grouping is not accepted.", new TextSpan(1, 1)),
            new(number, "1,000", MathValidationCode.NotANumber, "Use '.' as the decimal point, for example 1.000. Digit grouping is not accepted.", new TextSpan(1, 1)),
            new(number, "  -2,5e3 ", MathValidationCode.NotANumber, "Use '.' as the decimal point, for example -2.5e3. Digit grouping is not accepted.", new TextSpan(4, 1)),
            new(number, "1 000", MathValidationCode.NotANumber, "Write the number without digit grouping: 1000."),
            new(number, "1_000", MathValidationCode.NotANumber, "Write the number without digit grouping: 1000."),
            new(number, "1 000", MathValidationCode.NotANumber, "Write the number without digit grouping: 1000."),
            // numbers
            new(number, "−3", null),
            new(number, "+5", null),
            new(number, " 7 ", null),
            new(number, "5.", null),
            new(number, ".5", null),
            new(number, "0.(3)", null),
            new(number, "0.1(6)", null),
            new(number, "1/3", null),
            new(number, "-1/3", null),
            new(number, "1e5", null),
            new(number, "1.5E-3", null),
            new(number, "1e100000", null),
            new(number, "1e-100000", null),
            new(number, "−1e−5", null),
        };

        Assert.IsTrue(rows.Count >= 40);
        AssertRows(rows);
    }

    [TestMethod]
    public void RationalNumberOptionsTable()
    {
        var rows = new List<Row>
        {
            // IntegerOnly is about the value
            new(Rational(integerOnly: true), "7", null),
            new(Rational(integerOnly: true), "4/2", null),
            new(Rational(integerOnly: true), "2.0", null),
            new(Rational(integerOnly: true), "1e3", null),
            new(Rational(integerOnly: true), "1/2", MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true), "0.5", MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true), "0.(3)", MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true), "abc", MathValidationCode.NotANumber),
            // AllowFractions is about how text is written
            new(Rational(fractions: false), "0.5", null),
            new(Rational(fractions: false), "3", null),
            new(Rational(fractions: false), "1/2", MathValidationCode.FractionNotAllowed, "Write it as 0.5."),
            new(Rational(fractions: false), "1/3", MathValidationCode.FractionNotAllowed, "Write it as 0.(3)."),
            new(Rational(fractions: false), "4/2", MathValidationCode.FractionNotAllowed, "Write it as 2."),
            new(Rational(fractions: false), "1/997", MathValidationCode.FractionNotAllowed, null),
            new(Rational(fractions: false, decimals: false), "1/2", MathValidationCode.FractionNotAllowed, null),
            new(Rational(fractions: false, decimals: false), "6/3", MathValidationCode.FractionNotAllowed, "Write it as 2."),
            // AllowDecimals: a point, a repeating block or an exponent
            new(Rational(decimals: false), "1/2", null),
            new(Rational(decimals: false), "5", null),
            new(Rational(decimals: false), "0.5", MathValidationCode.DecimalNotAllowed, "Write it as 1/2."),
            new(Rational(decimals: false), "0.(3)", MathValidationCode.DecimalNotAllowed, "Write it as 1/3."),
            new(Rational(decimals: false), "1e3", MathValidationCode.DecimalNotAllowed, "Write it as 1000."),
            new(Rational(decimals: false), "2.0", MathValidationCode.DecimalNotAllowed, "Write it as 2."),
            new(Rational(decimals: false, fractions: false), "0.5", MathValidationCode.DecimalNotAllowed, null),
            // IntegerOnly is checked before notation: 2.5 is not an integer whatever the notation, 2.0 is one written as a decimal
            new(Rational(integerOnly: true, decimals: false), "2.5", MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true, decimals: false), "2.0", MathValidationCode.DecimalNotAllowed, "Write it as 2."),
            new(Rational(integerOnly: true, fractions: false), "4/2", MathValidationCode.FractionNotAllowed, "Write it as 2."),
            // typed values have no notation: only IntegerOnly applies
            new(Rational(fractions: false, decimals: false), BigRational.Create(1, 2), null),
            new(Rational(integerOnly: true), BigRational.Create(1, 2), MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true), 3L, null),
            new(Rational(integerOnly: true), 2.5m, MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true), 2.0m, null),
        };

        AssertRows(rows);
    }

    [TestMethod]
    public void NonZeroTable()
    {
        var zero = new NonZeroAttribute();
        var rows = new List<Row>
        {
            new(zero, "0", MathValidationCode.Zero),
            new(zero, "0.0", MathValidationCode.Zero),
            new(zero, "-0", MathValidationCode.Zero),
            new(zero, "0/5", MathValidationCode.Zero),
            new(zero, "0e10", MathValidationCode.Zero),
            new(zero, "0.(0)", MathValidationCode.Zero),
            new(zero, "−0", MathValidationCode.Zero),
            new(zero, "1", null),
            new(zero, "-1/3", null),
            new(zero, "1e-100000", null),
            new(zero, "0.000000000000000000000000000001", null),
            new(zero, "", null),
            new(zero, "abc", MathValidationCode.NotANumber),
            new(zero, "0,5", MathValidationCode.NotANumber, "Use '.' as the decimal point, for example 0.5. Digit grouping is not accepted.", new TextSpan(1, 1)),
            new(zero, 0, MathValidationCode.Zero),
            new(zero, 0L, MathValidationCode.Zero),
            new(zero, BigInteger.Zero, MathValidationCode.Zero),
            new(zero, BigRational.Zero, MathValidationCode.Zero),
            new(zero, 0m, MathValidationCode.Zero),
            new(zero, 0.00m, MathValidationCode.Zero),
            new(zero, 1, null),
            new(zero, BigRational.Create(-1, 7), null),
        };

        AssertRows(rows);
    }

    [TestMethod]
    public void ExactRangeTable()
    {
        var unit = new ExactRangeAttribute("0", "1");
        var rows = new List<Row>
        {
            new(unit, "0", null),
            new(unit, "1", null),
            new(unit, "1/2", null),
            new(unit, "0.5", null),
            new(unit, "0.(9)", null),
            new(unit, "1.0000000000000000000001", MathValidationCode.OutOfRange),
            new(unit, "-0.0000001", MathValidationCode.OutOfRange),
            new(unit, "1e-100000", null),
            new(unit, "1e100000", MathValidationCode.OutOfRange),
            new(unit, "-1e100000", MathValidationCode.OutOfRange),
            new(unit, "abc", MathValidationCode.NotANumber),
            new(unit, "", null),
            new(new ExactRangeAttribute("0", "1") { MinimumIsExclusive = true }, "0", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("0", "1") { MinimumIsExclusive = true }, "1", null),
            new(new ExactRangeAttribute("0", "1") { MaximumIsExclusive = true }, "1", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("0", "1") { MaximumIsExclusive = true }, "0", null),
            new(new ExactRangeAttribute("1/3", "5/2") { MaximumIsExclusive = true }, "1/3", null),
            new(new ExactRangeAttribute("1/3", "5/2") { MaximumIsExclusive = true }, "5/2", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("1/3", "5/2") { MaximumIsExclusive = true }, "2.4999999999", null),
            new(new ExactRangeAttribute("1/3", "5/2") { MaximumIsExclusive = true }, "0.(3)", null),
            new(new ExactRangeAttribute("1/3", "5/2") { MaximumIsExclusive = true }, "0.3333333333", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute(null, "100"), "-1e5", null),
            new(new ExactRangeAttribute(null, "100"), "100.0000001", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("-5", ""), "1e100000", null),
            new(new ExactRangeAttribute("-5", ""), "-5.01", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("1e-100000", null), "0", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("1e-100000", null), "2e-100000", null),
            new(new ExactRangeAttribute("0.1", "0.1"), "1/10", null),
            new(new ExactRangeAttribute("0.1", "0.1"), "0.10000000000000001", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("−5", "5"), "-5", null),
            new(new ExactRangeAttribute("−5", "5"), "−5.5", MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute(" 1 ", " 2 "), "1.5", null),
            // typed values
            new(unit, 0, null),
            new(unit, 2, MathValidationCode.OutOfRange),
            new(unit, 1L, null),
            new(unit, BigInteger.Pow(10, 40), MathValidationCode.OutOfRange),
            new(unit, 0.5m, null),
            new(unit, 1.0000000000000000000000000001m, MathValidationCode.OutOfRange),
            new(unit, BigRational.Create(1, 3), null),
            new(unit, BigRational.Create(4, 3), MathValidationCode.OutOfRange),
        };

        AssertRows(rows);
    }

    [TestMethod]
    public void ExactRangeMessageNamesTheRangeAndTheBoundsAsWritten()
    {
        var range = new ExactRangeAttribute("1/3", "5/2") { MaximumIsExclusive = true };
        Assert.AreEqual("Value must be in the range [1/3, 5/2).", range.Check("5/2")!.ErrorMessage);
        Assert.AreEqual("Value must be in the range [1/3, 5/2).", range.Check(3)!.ErrorMessage);

        Assert.AreEqual("Value must be in the range (-∞, 100].", new ExactRangeAttribute(null, "100").Check("101")!.ErrorMessage);
        Assert.AreEqual("Value must be in the range [-5, ∞).", new ExactRangeAttribute("-5", "").Check("-6")!.ErrorMessage);
        Assert.AreEqual("Value must be in the range (0.5, 3/2].", new ExactRangeAttribute(" 0.5 ", "3/2") { MinimumIsExclusive = true }.Check("0.5")!.ErrorMessage);

        var custom = new ExactRangeAttribute("1/3", null) { ErrorMessage = "{0}: {1} | min {2} | max [{3}]" };
        Assert.AreEqual("Rate: [1/3, ∞) | min 1/3 | max []", custom.Check("0", "Rate")!.ErrorMessage);
        Assert.AreEqual("Rate: [1/3, ∞) | min 1/3 | max []", ((MathValidationResult)custom.GetValidationResult("0", MathValidationAttributeTests.Context("R", "Rate"))!).ErrorMessage);

        Assert.AreEqual("Value must be a number, for example 3, -1/2 or 0.25.", range.Check("x")!.ErrorMessage);
        Assert.AreEqual("Value must not be zero.", new NonZeroAttribute().Check("0")!.ErrorMessage);
        Assert.AreEqual("Value must be a whole number.", new RationalNumberAttribute { IntegerOnly = true }.Check("1/2")!.ErrorMessage);
        Assert.AreEqual("Value must not be a fraction.", new RationalNumberAttribute { AllowFractions = false }.Check("1/2")!.ErrorMessage);
        Assert.AreEqual("Value must not be a decimal number.", new RationalNumberAttribute { AllowDecimals = false }.Check("0.5")!.ErrorMessage);
    }

    [TestMethod]
    public void ConstructorArgumentsAreReadOnlyProperties()
    {
        var range = new ExactRangeAttribute("-1/2", null);

        Assert.AreEqual("-1/2", range.Minimum);
        Assert.IsNull(range.Maximum);
        Assert.IsNull(typeof(ExactRangeAttribute).GetProperty(nameof(ExactRangeAttribute.Minimum))!.SetMethod);
        Assert.IsNull(typeof(ExactRangeAttribute).GetProperty(nameof(ExactRangeAttribute.Maximum))!.SetMethod);
        Assert.IsTrue(typeof(RationalNumberAttribute).IsSealed && typeof(ExactRangeAttribute).IsSealed && typeof(NonZeroAttribute).IsSealed);
    }

    [TestMethod]
    public void DoubleAndFloatTable()
    {
        var rows = new List<Row>
        {
            // 0.1 means the decimal the developer wrote, not its binary expansion
            new(new ExactRangeAttribute("0", "1/10"), 0.1, null),
            new(new ExactRangeAttribute("1/10", "1/10"), 0.1, null),
            new(new ExactRangeAttribute("0", "1/10"), 0.1f, null),
            // ... but arithmetic results are what they are
            new(new ExactRangeAttribute(null, "3/10"), 0.1 + 0.2, MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("3/10", null), 0.3, null),
            new(new ExactRangeAttribute("3/10", null) { MinimumIsExclusive = true }, 0.3, MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("1/3", "1"), 1.0 / 3.0, MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute(null, "1"), 1.0000000000000002, MathValidationCode.OutOfRange),
            new(new ExactRangeAttribute("0", "1"), 0.5, null),
            new(new ExactRangeAttribute(null, "0") { MaximumIsExclusive = true }, -1e-300, null),
            new(new ExactRangeAttribute("1e2", "1e2"), 100.0, null),
            // NaN and the infinities are not numbers
            new(Rational(), double.NaN, MathValidationCode.NotANumber),
            new(Rational(), double.PositiveInfinity, MathValidationCode.NotANumber),
            new(Rational(), double.NegativeInfinity, MathValidationCode.NotANumber),
            new(new ExactRangeAttribute("0", "1"), double.NaN, MathValidationCode.NotANumber),
            new(new NonZeroAttribute(), double.PositiveInfinity, MathValidationCode.NotANumber),
            new(Rational(), float.NaN, MathValidationCode.NotANumber),
            new(Rational(), float.NegativeInfinity, MathValidationCode.NotANumber),
            // zero and negative zero
            new(new NonZeroAttribute(), -0.0, MathValidationCode.Zero),
            new(new NonZeroAttribute(), 0.0, MathValidationCode.Zero),
            new(new NonZeroAttribute(), -0.0f, MathValidationCode.Zero),
            new(new NonZeroAttribute(), double.Epsilon, null),
            // the whole double range is rational
            new(Rational(), double.MaxValue, null),
            new(Rational(integerOnly: true), double.MaxValue, null),
            new(Rational(integerOnly: true), double.MinValue, null),
            new(Rational(integerOnly: true), 2.5, MathValidationCode.NotAnInteger),
            new(Rational(integerOnly: true), 3.0, null),
            new(Rational(integerOnly: true), 1e21, null),
            new(Rational(integerOnly: true), float.MaxValue, null),
        };

        Assert.IsTrue(rows.Count >= 20);
        AssertRows(rows);
    }

    [TestMethod]
    public void OtherTypesAreApiMisuse()
    {
        var attributes = new MathValidationAttribute[] { Rational(), new ExactRangeAttribute("0", "1"), new NonZeroAttribute() };

        foreach (var attribute in attributes)
        {
            foreach (object value in new object[] { Guid.NewGuid(), (ushort)1, (byte)1, (short)1, 1u, 1ul, (Half)1, 'x', true, new object(), DateTime.UnixEpoch })
            {
                var ex = Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid(value));
                StringAssert.Contains(ex.Message, attribute.GetType().Name);
                StringAssert.Contains(ex.Message, value.GetType().FullName!);
                Assert.ThrowsExactly<InvalidOperationException>(() => attribute.Check(value));
            }

            Assert.IsTrue(attribute.IsValid(null));
        }
    }

    [TestMethod]
    public void ConfigurationErrorsThrowAsSpecified()
    {
        var rows = new (string Label, MathValidationAttribute Attribute, string Message)[]
        {
            ("unparsable minimum", new ExactRangeAttribute("abc", "1"), "The minimum 'abc' is not a number."),
            ("unparsable maximum", new ExactRangeAttribute("0", "1,5"), "The maximum '1,5' is not a number."),
            ("whitespace is not an empty bound", new ExactRangeAttribute(" ", "1"), "The minimum ' ' is not a number."),
            ("exponent beyond the limit", new ExactRangeAttribute("1e100001", null), "The minimum '1e100001' is not a number."),
            ("minimum above maximum", new ExactRangeAttribute("5", "1"), "The minimum 5 is above the maximum 1."),
            ("equal bounds written differently", new ExactRangeAttribute("0.6", "3/5") { MinimumIsExclusive = true }, "The range (0.6, 3/5] is empty."),
            ("minimum above maximum, fraction", new ExactRangeAttribute("1/2", "0.4"), "The minimum 1/2 is above the maximum 0.4."),
            ("empty range, exclusive minimum", new ExactRangeAttribute("1", "1") { MinimumIsExclusive = true }, "The range (1, 1] is empty."),
            ("empty range, exclusive maximum", new ExactRangeAttribute("1", "1") { MaximumIsExclusive = true }, "The range [1, 1) is empty."),
            ("empty range, both exclusive", new ExactRangeAttribute("1/2", "0.5") { MinimumIsExclusive = true, MaximumIsExclusive = true }, "The range (1/2, 0.5) is empty."),
            ("no bound", new ExactRangeAttribute(null, null), "Give a minimum or a maximum"),
            ("no bound, empty text", new ExactRangeAttribute("", ""), "Give a minimum or a maximum"),
            ("exclusive minimum without a minimum", new ExactRangeAttribute(null, "5") { MinimumIsExclusive = true }, "MinimumIsExclusive is set but there is no minimum."),
            ("exclusive maximum without a maximum", new ExactRangeAttribute("5", null) { MaximumIsExclusive = true }, "MaximumIsExclusive is set but there is no maximum."),
            ("MaxLength 0, ExactRange", new ExactRangeAttribute("0", "1") { MaxLength = 0 }, "MaxLength must be between 1 and 100000 but is 0."),
            ("MaxLength 100001, ExactRange", new ExactRangeAttribute("0", "1") { MaxLength = 100_001 }, "MaxLength must be between 1 and 100000 but is 100001."),
            ("MaxLength -5, RationalNumber", new RationalNumberAttribute { MaxLength = -5 }, "MaxLength must be between 1 and 100000 but is -5."),
            ("MaxLength 100001, RationalNumber", new RationalNumberAttribute { MaxLength = 100_001 }, "MaxLength must be between 1 and 100000 but is 100001."),
            ("MaxLength 0, NonZero", new NonZeroAttribute { MaxLength = 0 }, "MaxLength must be between 1 and 100000 but is 0."),
            ("MaxLength int.MaxValue, NonZero", new NonZeroAttribute { MaxLength = int.MaxValue }, "MaxLength must be between 1 and 100000"),
            ("undefined placeholder", new ExactRangeAttribute("0", "1") { ErrorMessage = "{0} {4}" }, "{0} {4}"),
            ("undefined placeholder, RationalNumber", new RationalNumberAttribute { ErrorMessage = "{0} {2}" }, "{0} {2}"),
        };

        foreach (var (label, attribute, message) in rows)
        {
            var type = attribute.GetType().Name;
            var text = attribute.GetConfigurationError();
            Assert.IsNotNull(text, label);
            StringAssert.StartsWith(text, type + ": ", label);
            StringAssert.Contains(text, message, label);

            // thrown on first use, even for null and blank values, by every entry point, with the same text
            foreach (var value in new object?[] { null, "", "1", 1 })
            {
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid(value), label).Message, label);
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.Check(value), label).Message, label);
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.GetValidationResult(value, MathValidationAttributeTests.Context()), label).Message, label);
            }
        }

        // valid configurations, including the edges
        foreach (var attribute in new MathValidationAttribute[]
        {
            new ExactRangeAttribute("1", "1"),
            new ExactRangeAttribute("1/2", "0.5"),
            new ExactRangeAttribute(null, "5") { MaximumIsExclusive = true },
            new ExactRangeAttribute("-1/2", null) { MinimumIsExclusive = true },
            new ExactRangeAttribute("1e-100000", "1e100000") { MinimumIsExclusive = true, MaximumIsExclusive = true },
            new ExactRangeAttribute("0", "1") { MaxLength = 1 },
            Rational(),
            new RationalNumberAttribute { MaxLength = 100_000, AllowFractions = false, AllowDecimals = false },
            new NonZeroAttribute(),
        })
        {
            Assert.IsNull(attribute.GetConfigurationError(), attribute.GetType().Name);
        }
    }

    [TestMethod]
    public void VerdictsAreTheSameUnderEveryCultureAndOnlyMessageFormattingVaries()
    {
        var rows = new List<Row>();
        var texts = new[] { "0,5", "1,000", "1 000", "0.5", "-3", "−3", "1/3", "1/0", "abc", "1e5", "0.(3)", "2.5", "4/2", " 7 ", "1e999999999", "-0", "" };
        foreach (var text in texts)
        {
            rows.Add(new(Rational(), text, null));
            rows.Add(new(Rational(integerOnly: true), text, null));
            rows.Add(new(Rational(fractions: false), text, null));
            rows.Add(new(Rational(decimals: false), text, null));
            rows.Add(new(new NonZeroAttribute(), text, null));
            rows.Add(new(new ExactRangeAttribute("-1", "3/2") { MaximumIsExclusive = true }, text, null));
        }

        foreach (var value in new object[] { 0.1, 0.1f, 1.5e-7, -0.0, 1e21, 0.1 + 0.2, 1.0 / 3.0, double.NaN, 12.5m, 1.10m, 7, 7L, BigRational.Create(2, 3), BigInteger.Pow(10, 30), float.MaxValue })
        {
            rows.Add(new(Rational(), value, null));
            rows.Add(new(new NonZeroAttribute(), value, null));
            rows.Add(new(new ExactRangeAttribute("0", "3/10"), value, null));
        }

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
        Assert.IsTrue(baseline.Contains("NotANumber", StringComparison.Ordinal) && baseline.Contains("valid", StringComparison.Ordinal));
        foreach (var name in new[] { "en-US", "de-DE", "fr-FR", "ar-SA", "tr-TR", "ja-JP" })
        {
            Assert.AreEqual(baseline, Describe(new CultureInfo(name)), name);
        }

        // Only the formatting of the message's own values depends on the culture.
        var tooLong = new RationalNumberAttribute { MaxLength = 1_000, ErrorMessage = "{1:N0}" };
        var text1001 = new string('1', 1_001);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("1.000", tooLong.Check(text1001)!.ErrorMessage);
            Assert.AreEqual(MathValidationCode.TooLong, tooLong.Check(text1001)!.Code);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestMethod]
    public void HugeExponentsAreCheapAndNothingIsEvaluatedBeyondTheLimit()
    {
        var attributes = new MathValidationAttribute[] { Rational(), new NonZeroAttribute(), new ExactRangeAttribute("0", "1e100000") };
        foreach (var attribute in attributes) _ = attribute.Check("1");   // warm up

        foreach (var text in new[] { "1e100000", "1e-100000", "123456789e-100000", "0.5e100000" })
        {
            foreach (var attribute in attributes)
            {
                var watch = Stopwatch.StartNew();
                var result = attribute.Check(text);
                watch.Stop();

                Assert.IsTrue(watch.ElapsedMilliseconds < 50, $"{attribute.GetType().Name} on {text} took {watch.ElapsedMilliseconds} ms");
                Assert.AreNotEqual((MathValidationCode?)MathValidationCode.NotANumber, result?.Code, text);
            }
        }

        foreach (var text in new[] { "1e999999999", "1e-999999999", "1e100001", "1e-100001" })
        {
            var watch = Stopwatch.StartNew();
            var result = Rational().Check(text);
            watch.Stop();

            Assert.AreEqual(MathValidationCode.NotANumber, result!.Code, text);
            Assert.IsTrue(watch.ElapsedMilliseconds < 50, $"{text} took {watch.ElapsedMilliseconds} ms");
        }
    }

    [TestMethod]
    public void EvenAtTheLargestMaxLengthHostileTextIsExaminedInBoundedTime()
    {
        // Measured about 0.03 to 0.25 s; the bound is generous so a busy machine does not fail it, yet it would catch a quadratic blow-up in the parsing.
        var random = new Random(20261005);
        string Digits(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('1' + random.Next(9))).ToArray());
        var number = new RationalNumberAttribute { MaxLength = 100_000 };
        var range = new ExactRangeAttribute("0", "1e100000") { MaxLength = 100_000 };
        _ = number.Check(new string('9', 100_001));   // warm up

        foreach (var (label, text) in new[]
        {
            ("long mantissa with the largest exponent", Digits(99_980) + "e100000"),
            ("fraction of two 50,000-digit numbers", Digits(50_000) + "/" + Digits(49_999)),
            ("repeating block of 99,990 digits", "0.(" + Digits(99_990) + ")"),
            ("long mantissa with the smallest exponent", Digits(99_980) + "e-100000"),
        })
        {
            foreach (var attribute in new MathValidationAttribute[] { number, range })
            {
                var watch = Stopwatch.StartNew();
                var result = attribute.Check(text);
                watch.Stop();

                Assert.IsTrue(watch.ElapsedMilliseconds < 3_000, $"{attribute.GetType().Name} on {label} took {watch.ElapsedMilliseconds} ms");
                Assert.AreNotEqual((MathValidationCode?)MathValidationCode.NotANumber, result?.Code, label);
            }
        }
    }

    [TestMethod]
    public void TenMegabytesOfTextIsTooLongInUnderFiveMilliseconds()
    {
        var attributes = new MathValidationAttribute[] { Rational(), new NonZeroAttribute(), new ExactRangeAttribute("0", "1") };
        foreach (var attribute in attributes) _ = attribute.Check(new string('9', 2_000));   // warm up: JIT and the TooLong message from the resx
        var huge = new string('9', 10_000_000);

        foreach (var attribute in attributes)
        {
            var watch = Stopwatch.StartNew();
            var result = attribute.Check(huge);
            watch.Stop();

            Assert.AreEqual(MathValidationCode.TooLong, result!.Code);
            Assert.IsTrue(watch.ElapsedMilliseconds < 5, $"{attribute.GetType().Name} took {watch.ElapsedMilliseconds} ms");
        }
    }
}
