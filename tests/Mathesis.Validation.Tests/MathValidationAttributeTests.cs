using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Mathesis.Numbers;

namespace Mathesis.Validation.Tests;

/// <summary>The rules of <see cref="MathValidationAttribute"/>, exercised through the test-only <see cref="ProbeAttribute"/> (PLAN-M9 Phase 1).</summary>
[TestClass]
public class MathValidationAttributeTests
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

    /// <summary>A context with a fixed display name and member, built the trim-safe way (no reflection on the instance).</summary>
    internal static ValidationContext Context(string member = "Formula", string display = "Formula") => new(new object(), display, null, null) { MemberName = member };

    /// <summary>The code the probe must report for <paramref name="value"/> (<c>null</c> when valid), computed from its documented verdict function.</summary>
    internal static MathValidationCode? ExpectedCode(object? value) => value switch
    {
        null => null,
        string text when string.IsNullOrWhiteSpace(text) => null,
        _ => ProbeAttribute.Verdict(Convert.ToString(value, CultureInfo.InvariantCulture)!)?.Code,
    };

    [TestMethod]
    public void UsageIsPropertyFieldAndParameterWithoutMultiples()
    {
        var usage = typeof(ProbeAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: true).Cast<AttributeUsageAttribute>().Single();

        Assert.AreEqual(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, usage.ValidOn);
        Assert.IsFalse(usage.AllowMultiple);
    }

    [TestMethod]
    public void TheBaseClassCannotBeDerivedOutsideThePackage()
    {
        // The constructor and every extension point are internal (ADR-15, MathDiagnostic is internal), so only this package and its test project derive.
        Assert.IsTrue(typeof(MathValidationAttribute).IsAbstract);
        Assert.AreEqual(0, typeof(MathValidationAttribute).GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length);
        Assert.IsTrue(typeof(MathValidationResult).IsSealed);
        Assert.IsTrue(typeof(ValidationResult).IsAssignableFrom(typeof(MathValidationResult)));
    }

    [TestMethod]
    public void NullEmptyAndWhitespaceAreValidAndNeverReachTheVerdict()
    {
        var probe = new ProbeAttribute();

        foreach (var value in new object?[] { null, "", " ", "\t\r\n ", "  " })
        {
            Assert.IsTrue(probe.IsValid(value), $"IsValid({value})");
            Assert.IsNull(probe.Check(value), $"Check({value})");
            Assert.IsNull(probe.GetValidationResult(value, Context()), $"GetValidationResult({value})");
            probe.Validate(value, "Formula");
            probe.Validate(value, Context());
        }

        Assert.AreEqual(0, probe.Evaluations);
    }

    [TestMethod]
    public void TextLongerThanMaxLengthIsTooLongWithoutCallingTheVerdict()
    {
        var probe = new ProbeAttribute { MaxLength = 10 };
        var tooLong = new string('x', 11);

        var result = probe.Check(tooLong, "Formula", "Poly");

        Assert.IsNotNull(result);
        Assert.AreEqual(MathValidationCode.TooLong, result.Code);
        Assert.AreEqual("Formula must be at most 10 characters long.", result.ErrorMessage);
        Assert.IsNull(result.Span);
        Assert.IsNull(result.Suggestion);
        Assert.IsFalse(probe.IsValid(tooLong));
        var viaContext = probe.GetValidationResult(tooLong, Context());
        Assert.AreEqual(MathValidationCode.TooLong, ((MathValidationResult)viaContext!).Code);
        Assert.AreEqual(0, probe.Evaluations, "the verdict core must not run for text beyond MaxLength");

        // Exactly MaxLength characters is examined.
        _ = probe.Check(new string('x', 10));
        Assert.AreEqual(1, probe.Evaluations);
    }

    [TestMethod]
    public void DefaultMaxLengthIsOneThousand()
    {
        var probe = new ProbeAttribute();

        Assert.AreEqual(1_000, probe.MaxLength);
        Assert.AreEqual(MathValidationCode.TooLong, probe.Check(new string('1', 1_001))!.Code);
        Assert.AreEqual(0, probe.Evaluations);
        _ = probe.Check(new string('1', 1_000));
        Assert.AreEqual(1, probe.Evaluations);
    }

    [TestMethod]
    public void TypedValuesAreCheckedThroughTheVerdictAndOtherTypesAreApiMisuse()
    {
        var probe = new ProbeAttribute();

        foreach (object value in new object[] { 7, 12L, 123456789L, BigRational.Create(5, 3), -4 })
        {
            var expected = ProbeAttribute.Verdict(Convert.ToString(value, CultureInfo.InvariantCulture)!);
            Assert.AreEqual(expected is null, probe.IsValid(value), $"typed value {value}");
            Assert.AreEqual(expected?.Code, probe.Check(value)?.Code, $"typed value {value}");
        }

        var unsupported = new ProbeAttribute();
        var guid = Guid.NewGuid();
        var thrown = new List<InvalidOperationException>
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => unsupported.IsValid(guid)),
            Assert.ThrowsExactly<InvalidOperationException>(() => unsupported.Check(guid)),
            Assert.ThrowsExactly<InvalidOperationException>(() => unsupported.GetValidationResult(guid, Context())),
            Assert.ThrowsExactly<InvalidOperationException>(() => unsupported.Validate(guid, "Formula")),
            Assert.ThrowsExactly<InvalidOperationException>(() => unsupported.IsValid(3.5)),
        };
        foreach (var ex in thrown)
        {
            StringAssert.Contains(ex.Message, "ProbeAttribute");
            Assert.IsTrue(ex.Message.Contains("System.Guid", StringComparison.Ordinal) || ex.Message.Contains("System.Double", StringComparison.Ordinal), ex.Message);
        }

        Assert.AreEqual(0, unsupported.Evaluations, "an unsupported type is rejected before the value is examined");
    }

    [TestMethod]
    public void StandaloneEntryPointsNeverThrowNullReference()
    {
        var probe = new ProbeAttribute();
        var invalid = ProbeAttribute.InputFor(MathValidationCode.Syntax);

        foreach (var value in new object?[] { null, "", "0", invalid, 7, 12L })
        {
            var expected = ExpectedCode(value);

            Assert.AreEqual(expected is null, probe.IsValid(value));

            if (expected is null)
            {
                probe.Validate(value, "Poly");
                probe.Validate(value, Context());
                Assert.IsNull(probe.GetValidationResult(value, Context()));
            }
            else
            {
                var plain = Assert.ThrowsExactly<ValidationException>(() => probe.Validate(value, "Poly"));
                Assert.AreEqual("Poly is not valid.", plain.Message);

                var withContext = Assert.ThrowsExactly<ValidationException>(() => probe.Validate(value, Context()));
                Assert.IsInstanceOfType<MathValidationResult>(withContext.ValidationResult);
                Assert.AreEqual(expected, ((MathValidationResult)withContext.ValidationResult).Code);

                var result = probe.GetValidationResult(value, Context());
                Assert.IsInstanceOfType<MathValidationResult>(result, "the base class must not replace the result");
            }
        }
    }

    [TestMethod]
    public void ContextOverloadRequiresAContext()
    {
        var probe = new ProbeAttribute();

        Assert.ThrowsExactly<ArgumentNullException>(() => probe.GetValidationResult("1", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => probe.Check("1", null!));
    }

    [TestMethod]
    public void ResultCarriesCodeSpanSuggestionMessageAndMember()
    {
        var probe = new ProbeAttribute();
        var syntax = ProbeAttribute.InputFor(MathValidationCode.Syntax);
        var zero = ProbeAttribute.InputFor(MathValidationCode.Zero);
        var shape = ProbeAttribute.InputFor(MathValidationCode.WrongShape);
        var range = ProbeAttribute.InputFor(MathValidationCode.OutOfRange);
        var variable = ProbeAttribute.InputFor(MathValidationCode.UnknownVariable);

        var r = probe.Check(syntax, "Formula", "Poly")!;
        Assert.AreEqual(MathValidationCode.Syntax, r.Code);
        Assert.AreEqual(new Mathesis.Symbolics.Parsing.TextSpan(0, 1), r.Span);
        Assert.AreEqual("Try again.", r.Suggestion);
        Assert.AreEqual($"Formula is not valid: {syntax.Length}", r.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "Poly" }, r.MemberNames.ToArray());

        Assert.AreEqual("Value must not be zero.", probe.Check(zero)!.ErrorMessage, "the display name defaults to Value");

        r = probe.Check(zero, "Divisor")!;
        Assert.AreEqual(MathValidationCode.Zero, r.Code);
        Assert.AreEqual(new Mathesis.Symbolics.Parsing.TextSpan(zero.Length, 0), r.Span);
        Assert.IsNull(r.Suggestion);
        Assert.AreEqual("Divisor must not be zero.", r.ErrorMessage);
        Assert.AreEqual(0, r.MemberNames.Count(), "without a member name the result has no member names");

        Assert.AreEqual("Formula must be an equation, but it is an expression.", probe.Check(shape, "Formula")!.ErrorMessage);
        Assert.AreEqual("Formula must be in the range [1, 2].", probe.Check(range, "Formula")!.ErrorMessage);
        Assert.AreEqual("Formula uses a variable that is not allowed: x.", probe.Check(variable, "Formula")!.ErrorMessage);
        Assert.AreEqual("Remove x.", probe.Check(variable, "Formula")!.Suggestion);

        var viaContext = (MathValidationResult)probe.GetValidationResult(syntax, Context("Poly", "Polynomial"))!;
        Assert.AreEqual(MathValidationCode.Syntax, viaContext.Code);
        Assert.AreEqual("Polynomial is not valid: " + syntax.Length, viaContext.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "Poly" }, viaContext.MemberNames.ToArray());

        var noMember = (MathValidationResult)probe.GetValidationResult(syntax, new ValidationContext(new object(), "Polynomial", null, null))!;
        Assert.AreEqual(0, noMember.MemberNames.Count());
    }

    [TestMethod]
    public void InvalidConfigurationThrowsOnFirstUseEvenForNullAndGetConfigurationErrorReportsTheSameText()
    {
        var cases = new (string Label, ProbeAttribute Probe)[]
        {
            ("MaxLength 0", new ProbeAttribute { MaxLength = 0 }),
            ("MaxLength -1", new ProbeAttribute { MaxLength = -1 }),
            ("MaxLength 100001", new ProbeAttribute { MaxLength = 100_001 }),
            ("MaxLength int.MaxValue", new ProbeAttribute { MaxLength = int.MaxValue }),
            ("invalid option", new ProbeAttribute { InvalidOption = "Mode must be even." }),
        };

        foreach (var (label, probe) in cases)
        {
            var text = probe.GetConfigurationError();
            Assert.IsNotNull(text, label);
            StringAssert.Contains(text, "ProbeAttribute", label);
            Assert.AreEqual(text, probe.GetConfigurationError(), label);

            var entryPoints = new Action[]
            {
                () => probe.IsValid(null),
                () => probe.IsValid("1"),
                () => probe.Check(null),
                () => probe.GetValidationResult(null, Context()),
                () => probe.Validate(null, "Formula"),
                () => probe.Validate(null, Context()),
                () => probe.FormatErrorMessage("Formula"),
            };
            foreach (var call in entryPoints)
            {
                var ex = Assert.ThrowsExactly<InvalidOperationException>(call, label);
                Assert.AreEqual(text, ex.Message, label);
            }

            Assert.AreEqual(0, probe.Evaluations, label);
        }

        StringAssert.Contains(cases[0].Probe.GetConfigurationError()!, "between 1 and 100000");
        StringAssert.Contains(cases[4].Probe.GetConfigurationError()!, "Mode must be even.");
    }

    [TestMethod]
    public void BoundaryMaxLengthsAreValidConfigurations()
    {
        Assert.IsNull(new ProbeAttribute { MaxLength = 1 }.GetConfigurationError());
        Assert.IsNull(new ProbeAttribute { MaxLength = 100_000 }.GetConfigurationError());
        Assert.IsNull(new ProbeAttribute().GetConfigurationError());
        Assert.AreNotEqual((MathValidationCode?)MathValidationCode.TooLong, new ProbeAttribute { MaxLength = 100_000 }.Check(new string('0', 100_000))?.Code);
    }

    [TestMethod]
    public void DeveloperErrorMessageReplacesEveryDefaultMessage()
    {
        var probe = new ProbeAttribute { ErrorMessage = "Bad {0}: [{1}] [{2}] [{3}]" };

        Assert.IsNull(probe.GetConfigurationError());
        Assert.AreEqual("Bad Formula: [an equation] [an expression] []", probe.Check(ProbeAttribute.InputFor(MathValidationCode.WrongShape), "Formula")!.ErrorMessage);
        Assert.AreEqual("Bad Formula: [[1, 2]] [1] [2]", probe.Check(ProbeAttribute.InputFor(MathValidationCode.OutOfRange), "Formula")!.ErrorMessage);
        Assert.AreEqual("Bad Formula: [] [] []", probe.Check(ProbeAttribute.InputFor(MathValidationCode.Zero), "Formula")!.ErrorMessage);
        Assert.AreEqual("Bad Formula: [10] [] []", new ProbeAttribute { MaxLength = 10, ErrorMessage = "Bad {0}: [{1}] [{2}] [{3}]" }.Check(new string('x', 11), "Formula")!.ErrorMessage);
        Assert.AreEqual("Bad Poly", new ProbeAttribute { ErrorMessage = "Bad {0}" }.FormatErrorMessage("Poly"));
        Assert.AreEqual("Bad Polynomial: [] [] []", ((MathValidationResult)probe.GetValidationResult(ProbeAttribute.InputFor(MathValidationCode.Zero), Context("Poly", "Polynomial"))!).ErrorMessage);
    }

    [TestMethod]
    public void ErrorMessageResourceTypeReplacesEveryDefaultMessage()
    {
        var probe = new ProbeAttribute { ErrorMessageResourceType = typeof(CustomMessages), ErrorMessageResourceName = nameof(CustomMessages.Replaced) };

        Assert.IsNull(probe.GetConfigurationError());
        Assert.AreEqual("Custom: Formula / an equation", probe.Check(ProbeAttribute.InputFor(MathValidationCode.WrongShape), "Formula")!.ErrorMessage);
        Assert.AreEqual("Custom: Formula / ", probe.Check(ProbeAttribute.InputFor(MathValidationCode.Zero), "Formula")!.ErrorMessage);
    }

    [TestMethod]
    public void ErrorMessageTogetherWithAResourceNameIsTheBclsInvalidOperation()
    {
        var probe = new ProbeAttribute { ErrorMessage = "x", ErrorMessageResourceName = nameof(CustomMessages.Replaced) };

        var text = probe.GetConfigurationError();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "ProbeAttribute");
        StringAssert.Contains(text, "ErrorMessageResourceName");
        Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => probe.IsValid(null)).Message);

        var onlyName = new ProbeAttribute { ErrorMessageResourceName = nameof(CustomMessages.Replaced) };
        StringAssert.Contains(onlyName.GetConfigurationError()!, "ErrorMessageResourceType");

        var missing = new ProbeAttribute { ErrorMessageResourceType = typeof(CustomMessages), ErrorMessageResourceName = "Missing" };
        StringAssert.Contains(missing.GetConfigurationError()!, "Missing");

        // The BCL rejects an empty or null ErrorMessage the same way, so a message can never be empty.
        StringAssert.Contains(new ProbeAttribute { ErrorMessage = "" }.GetConfigurationError()!, "ProbeAttribute");
    }

    [TestMethod]
    public void TemplateWithAnUndefinedPlaceholderIsAConfigurationErrorNamingTheAttribute()
    {
        foreach (var template in new[] { "{0} {4}", "{5}", "{0} {1} {2} {3} {4}", "{0", "bad } brace", "{1:Z}{0}{2}{3}{9}" })
        {
            var probe = new ProbeAttribute { ErrorMessage = template };

            var text = probe.GetConfigurationError();
            Assert.IsNotNull(text, template);
            StringAssert.Contains(text, "ProbeAttribute", template);
            StringAssert.Contains(text, template, template);
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => probe.IsValid(null), template);
            Assert.AreEqual(text, ex.Message, template);
        }

        foreach (var template in new[] { "{0} {1} {2} {3}", "{{0}} literal", "no placeholders", "{3}{2}{1}{0}", "{1,5}|{0:N0}" })
        {
            Assert.IsNull(new ProbeAttribute { ErrorMessage = template }.GetConfigurationError(), template);
        }
    }

    [TestMethod]
    public void FormatSpecifiersThatNumbersRejectAreConfigurationErrors()
    {
        // TooLong fills {1} with an int limit, NonNumericEntry fills {1} and {2} with a row and a column, DegreeTooHigh fills {2} with the maximum.
        // "Q" is no numeric format, so these templates would throw while validating bad input; they must fail at startup instead.
        var misconfigured = new MathValidationAttribute[]
        {
            new RationalNumberAttribute { ErrorMessage = "{0}: {1:Q}" },
            new ProbeAttribute { ErrorMessage = "{0}: {1:Q}" },
            new MathMatrixAttribute { ErrorMessage = "{0}: {2:Q}" },
            new PolynomialExpressionAttribute("x") { ErrorMessage = "{0}: {2:Q}" },
        };
        foreach (var attribute in misconfigured)
        {
            var text = attribute.GetConfigurationError();
            Assert.IsNotNull(text, attribute.GetType().Name);
            StringAssert.Contains(text, attribute.GetType().Name);
            Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid(null)).Message);
        }

        // Placeholders that are always text ignore format specifiers, so those templates stay valid, and numeric specifiers keep working.
        var range = new ExactRangeAttribute("0", "1") { ErrorMessage = "{0} {2:Q} {3:Q}" };
        Assert.IsNull(range.GetConfigurationError());
        Assert.AreEqual("Value 0 1", range.Check("2")!.ErrorMessage);
        Assert.IsNull(new MathExpressionAttribute { ErrorMessage = "{0}: {2:Q}" }.GetConfigurationError());
        Assert.IsNull(new RationalNumberAttribute { ErrorMessage = "{0}: {1:N0}" }.GetConfigurationError());
        Assert.IsNull(new MathMatrixAttribute { ErrorMessage = "{0}: row {1:D2}, column {2:D2}" }.GetConfigurationError());
        Assert.AreEqual("Value: row 01, column 02", new MathMatrixAttribute { ErrorMessage = "{0}: row {1:D2}, column {2:D2}" }.Check("[[1, x]]")!.ErrorMessage);
    }

    [TestMethod]
    public void WhitespaceOnlyTemplateFallsBackToTheDefaultSoTheMessageIsNeverEmpty()
    {
        var probe = new ProbeAttribute { ErrorMessage = "   " };

        Assert.IsNull(probe.GetConfigurationError());
        Assert.AreEqual("Formula must not be zero.", probe.Check(ProbeAttribute.InputFor(MathValidationCode.Zero), "Formula")!.ErrorMessage);

        var blank = new ProbeAttribute { ErrorMessage = "{3}" };
        Assert.AreEqual("Formula must not be zero.", blank.Check(ProbeAttribute.InputFor(MathValidationCode.Zero), "Formula")!.ErrorMessage, "a template that formats to nothing falls back too");
    }

    [TestMethod]
    public void MessagesFormatWithTheCurrentCultureButTheVerdictDoesNotChange()
    {
        var probe = new ProbeAttribute { MaxLength = 1_000, ErrorMessage = "{0}: {1:N0}" };
        var text = new string('x', 1_001);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.AreEqual("Formula: 1,000", probe.Check(text, "Formula")!.ErrorMessage);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("Formula: 1.000", probe.Check(text, "Formula")!.ErrorMessage);
            Assert.AreEqual(MathValidationCode.TooLong, probe.Check(text, "Formula")!.Code);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void CustomMessageIsUsedByTheGenericPathToo()
    {
        var plain = new ProbeAttribute();
        var custom = new ProbeAttribute { ErrorMessage = "{0} is wrong" };

        Assert.AreEqual("Poly is not valid.", plain.FormatErrorMessage("Poly"));
        Assert.AreEqual("Poly is wrong", custom.FormatErrorMessage("Poly"));
        Assert.AreEqual("Poly is wrong", Assert.ThrowsExactly<ValidationException>(() => custom.Validate(ProbeAttribute.InputFor(MathValidationCode.Zero), "Poly")).Message);
    }
}
