using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Mathesis.Validation.Tests;

/// <summary>
/// The base rules through the BCL entry points a UI framework uses. <c>Validator</c> is not trim-safe, so only tests call it; the library
/// never does (PLAN-M9 attribute contract, Trim and AOT).
/// </summary>
[TestClass]
public class ValidatorIntegrationTests
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

    private sealed class Form
    {
        [Required, StringLength(1), Probe, Display(Name = "Formula")]
        public string? A { get; set; }

        [Probe(MaxLength = 20)]
        public string? B { get; set; }

        [Probe]
        public long? C { get; set; }
    }

    // The probe's verdict on the single digits: "2" Syntax, "3" WrongShape, "4" OutOfRange, "5" Zero, "6" UnknownVariable; "0", "1" and "7" are valid.
    [TestMethod]
    public void TryValidateObjectReturnsMathResultsWhoseMemberNamesAreTheMember()
    {
        var form = new Form { A = "2", B = "3", C = 5 };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.AreEqual(3, results.Count);
        var byMember = results.ToDictionary(r => r.MemberNames.Single());

        Assert.IsInstanceOfType<MathValidationResult>(byMember["A"]);
        Assert.AreEqual(MathValidationCode.Syntax, ((MathValidationResult)byMember["A"]).Code);
        Assert.AreEqual("Formula is not valid: 1", byMember["A"].ErrorMessage, "the [Display] name is the display name");

        Assert.IsInstanceOfType<MathValidationResult>(byMember["B"]);
        Assert.AreEqual(MathValidationCode.WrongShape, ((MathValidationResult)byMember["B"]).Code);
        Assert.AreEqual("B must be an equation, but it is an expression.", byMember["B"].ErrorMessage);

        Assert.IsInstanceOfType<MathValidationResult>(byMember["C"]);
        Assert.AreEqual(MathValidationCode.Zero, ((MathValidationResult)byMember["C"]).Code, "typed values go through the same verdict");
        Assert.AreEqual("C must not be zero.", byMember["C"].ErrorMessage);
    }

    [TestMethod]
    public void ValidValuesProduceNoResults()
    {
        var form = new Form { A = "7", B = "0", C = null };
        var results = new List<ValidationResult>();

        Assert.IsTrue(Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true));
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void TryValidatePropertyReturnsAMathResultForTheMember()
    {
        var form = new Form();
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateProperty("4", new ValidationContext(form) { MemberName = nameof(Form.B) }, results);

        Assert.IsFalse(valid);
        var result = (MathValidationResult)results.Single();
        Assert.AreEqual(MathValidationCode.OutOfRange, result.Code);
        CollectionAssert.AreEqual(new[] { nameof(Form.B) }, result.MemberNames.ToArray());

        results.Clear();
        Assert.IsTrue(Validator.TryValidateProperty("1", new ValidationContext(form) { MemberName = nameof(Form.B) }, results));
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void TryValidateValueWorksWithoutAMember()
    {
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateValue("5", new ValidationContext(new object(), "Divisor", null, null), results, [new ProbeAttribute()]);

        Assert.IsFalse(valid);
        var result = (MathValidationResult)results.Single();
        Assert.AreEqual("Divisor must not be zero.", result.ErrorMessage);
        Assert.AreEqual(0, result.MemberNames.Count());
    }

    [TestMethod]
    public void AFailingRequiredSuppressesTheOtherAttributesOfTheMember()
    {
        // "   " fails [Required] and would also fail [StringLength(1)]; only the [Required] result is reported.
        var form = new Form { A = "   ", B = null, C = null };
        var results = new List<ValidationResult>();

        Assert.IsFalse(Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true));

        var result = results.Single();
        Assert.IsNotInstanceOfType<MathValidationResult>(result);
        CollectionAssert.AreEqual(new[] { nameof(Form.A) }, result.MemberNames.ToArray());
        Assert.AreEqual("The Formula field is required.", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateObjectThrowsWithTheMathResult()
    {
        var form = new Form { A = "2" };

        var ex = Assert.ThrowsExactly<ValidationException>(() => Validator.ValidateObject(form, new ValidationContext(form), validateAllProperties: true));

        Assert.IsInstanceOfType<MathValidationResult>(ex.ValidationResult);
        Assert.AreEqual("Formula is not valid: 1", ex.Message);
    }
}
