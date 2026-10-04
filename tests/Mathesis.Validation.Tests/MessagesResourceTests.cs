using System.Collections;
using System.Globalization;

namespace Mathesis.Validation.Tests;

/// <summary>The embedded <c>Messages.resx</c>: one template per <see cref="MathValidationCode"/>, no stray keys, only documented placeholders.</summary>
[TestClass]
public class MessagesResourceTests
{
    // The highest placeholder index each code may use, from the Codes table of PLAN-M9.md ({1}, {2}, {3} are the extra values).
    private static readonly Dictionary<MathValidationCode, int> MaxPlaceholder = new()
    {
        [MathValidationCode.TooLong] = 1,
        [MathValidationCode.NotANumber] = 0,
        [MathValidationCode.NotAnInteger] = 0,
        [MathValidationCode.FractionNotAllowed] = 0,
        [MathValidationCode.DecimalNotAllowed] = 0,
        [MathValidationCode.OutOfRange] = 3,
        [MathValidationCode.Zero] = 0,
        [MathValidationCode.Syntax] = 1,
        [MathValidationCode.Ambiguous] = 1,
        [MathValidationCode.UnknownFunction] = 1,
        [MathValidationCode.IllSorted] = 1,
        [MathValidationCode.WrongShape] = 2,
        [MathValidationCode.UnknownVariable] = 1,
        [MathValidationCode.MissingVariable] = 1,
        [MathValidationCode.DisallowedFunction] = 1,
        [MathValidationCode.NotAPolynomial] = 2,
        [MathValidationCode.DegreeTooHigh] = 2,
        [MathValidationCode.NotAMatrix] = 2,
        [MathValidationCode.WrongDimensions] = 2,
        [MathValidationCode.NotSquare] = 2,
        [MathValidationCode.NonNumericEntry] = 2,
        [MathValidationCode.DimensionTooLarge] = 2,
    };

    /// <summary>The highest placeholder index in <paramref name="template"/> (<c>-1</c> if none); fails the test on a malformed placeholder.</summary>
    private static int HighestPlaceholder(string template)
    {
        var highest = -1;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{') continue;
            if (i + 1 < template.Length && template[i + 1] == '{')
            {
                i++;
                continue;
            }

            var end = i + 1;
            while (end < template.Length && char.IsAsciiDigit(template[end])) end++;
            Assert.IsTrue(end > i + 1 && end < template.Length && template[end] is '}' or ',' or ':', $"malformed placeholder in '{template}'");
            highest = Math.Max(highest, int.Parse(template.AsSpan(i + 1, end - i - 1), CultureInfo.InvariantCulture));
        }

        return highest;
    }

    [TestMethod]
    public void EveryCodeHasATemplateAndTheResxHasNoOtherKeyThanTheGenericFallback()
    {
        var resourceSet = Messages.Resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        Assert.IsNotNull(resourceSet);
        var keys = resourceSet.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToList();

        var expected = Enum.GetNames<MathValidationCode>().Append("Invalid").ToList();

        CollectionAssert.AreEquivalent(expected, keys, "keys are the enum names plus the context-free 'Invalid'");
        Assert.AreEqual(keys.Count, keys.Distinct().Count());
    }

    [TestMethod]
    public void TheTableCoversEveryCode()
    {
        CollectionAssert.AreEquivalent(Enum.GetValues<MathValidationCode>(), MaxPlaceholder.Keys.ToList());
    }

    [TestMethod]
    public void TemplatesAreNonEmptyNameTheDisplayNameAndUseOnlyDocumentedPlaceholders()
    {
        foreach (var code in Enum.GetValues<MathValidationCode>())
        {
            var template = Messages.Get(code.ToString());

            Assert.IsFalse(string.IsNullOrWhiteSpace(template), code.ToString());
            Assert.AreEqual(template.Trim(), template, $"{code} has leading or trailing whitespace");
            StringAssert.Contains(template, "{0}", $"{code} must show the display name");
            var highest = HighestPlaceholder(template);
            Assert.IsTrue(highest <= MaxPlaceholder[code], $"{code} uses {{{highest}}} but is documented up to {{{MaxPlaceholder[code]}}}");

            var formatted = string.Format(CultureInfo.InvariantCulture, template, "Name", "one", "two", "three");
            Assert.IsFalse(formatted.Contains('{', StringComparison.Ordinal), $"{code}: {formatted}");
        }

        var generic = Messages.Get("Invalid");
        Assert.AreEqual(0, HighestPlaceholder(generic));
        Assert.AreEqual("Poly is not valid.", string.Format(CultureInfo.InvariantCulture, generic, "Poly"));
    }

    [TestMethod]
    public void TemplatesAreNeutralEnglishUnderAnyUiCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var name in new[] { "en-US", "de-DE", "fr-FR", "ar-SA", "tr-TR", "ja-JP" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(name);
                Assert.AreEqual("{0} must not be zero.", Messages.Get("Zero"), name);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    public void AMissingKeyIsAnInternalError()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Messages.Get("NoSuchMessage"));
    }
}
