using System.Globalization;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation;

/// <summary>How a number was written. Typed values have no notation.</summary>
[Flags]
internal enum Notation
{
    None = 0,
    Fraction = 1,
    Decimal = 2,
}

/// <summary>
/// Reads the text or typed value a numeric attribute checks as an exact <see cref="BigRational"/>, through <see cref="BigRational.TryParse(ReadOnlySpan{char}, IFormatProvider?, out BigRational)"/>
/// and never the expression parser. The invariant culture is always used: <c>.</c> is the decimal point.
/// </summary>
internal static class NumberInput
{
    // Characters people use to group digits; none of them is accepted in a number.
    private static readonly char[] GroupingCharacters = [' ', '_', '\'', ' ', ' ', ' '];

    /// <summary>The typed values numeric attributes accept besides text.</summary>
    internal static bool IsSupported(object value) => value is BigRational or BigInteger or int or long or decimal or double or float;

    /// <summary>Parses <paramref name="text"/> as <see cref="BigRational.TryParse(ReadOnlySpan{char}, IFormatProvider?, out BigRational)"/> does, also accepting U+2212 (−) as minus like the expression lexer.</summary>
    internal static bool TryParse(ReadOnlySpan<char> text, out BigRational number) =>
        text.Contains('−')
            ? BigRational.TryParse(text.ToString().Replace('−', '-'), CultureInfo.InvariantCulture, out number)
            : BigRational.TryParse(text, CultureInfo.InvariantCulture, out number);

    /// <summary>Reads a bound of <see cref="ExactRangeAttribute"/>: <c>null</c> or empty text is no bound (valid, <paramref name="number"/> is <c>null</c>).</summary>
    internal static bool TryParseBound(string? text, out BigRational? number)
    {
        number = null;
        if (string.IsNullOrEmpty(text)) return true;
        if (!TryParse(text, out var value)) return false;
        number = value;
        return true;
    }

    /// <summary>
    /// Reads <paramref name="value"/>, which is text or one of the <see cref="IsSupported"/> types, as an exact number.
    /// </summary>
    /// <returns><c>null</c> when it is a number, otherwise the <see cref="MathValidationCode.NotANumber"/> verdict (including NaN and the infinities).</returns>
    internal static MathDiagnostic? Read(object value, out BigRational number, out Notation notation)
    {
        notation = Notation.None;
        switch (value)
        {
            case string text:
                var trimmed = text.AsSpan().Trim();
                if (!TryParse(trimmed, out number)) return NotANumber(text);
                if (trimmed.Contains('/')) notation |= Notation.Fraction;
                if (trimmed.IndexOfAny('.', 'e', 'E') >= 0) notation |= Notation.Decimal;
                return null;
            case BigRational rational:
                number = rational;
                return null;
            case BigInteger integer:
                number = new BigRational(integer);
                return null;
            case int i:
                number = i;
                return null;
            case long l:
                number = l;
                return null;
            case decimal m:
                number = BigRational.FromDecimal(m);
                return null;
            // A binary floating-point value means the shortest decimal that reads back as it, so 0.1 is 1/10, not its binary expansion.
            case double d when double.IsFinite(d):
                number = BigRational.FromShortestDecimal(d);
                return null;
            case float f when float.IsFinite(f):
                number = BigRational.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                return null;
            default:
                number = default;
                return new MathDiagnostic(MathValidationCode.NotANumber, []);
        }
    }

    /// <summary>The verdict for text that is not a number, with a suggestion for the mistakes people make most.</summary>
    private static MathDiagnostic NotANumber(string text)
    {
        var start = text.Length - text.AsSpan().TrimStart().Length;
        var trimmed = text.AsSpan().Trim();

        // 0,5 and 1,000: the decimal point is '.', and digit grouping is not accepted.
        var comma = trimmed.IndexOf(',');
        var dotted = comma >= 0 ? trimmed.ToString().Replace(',', '.') : null;
        if (dotted is not null && TryParse(dotted, out _))
        {
            return new MathDiagnostic(MathValidationCode.NotANumber, [], new TextSpan(start + comma, 1), $"Use '.' as the decimal point, for example {dotted}. Digit grouping is not accepted.");
        }

        // 1 000 and 1_000 group digits; spaces elsewhere (1 / 2, - 3) are a different mistake with the same cure.
        var ungrouped = string.Concat(trimmed.ToString().Split(GroupingCharacters));
        if (ungrouped.Length != trimmed.Length && TryParse(ungrouped, out _))
        {
            var grouping = true;
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (GroupingCharacters.AsSpan().Contains(trimmed[i]) && !(i > 0 && i + 1 < trimmed.Length && char.IsAsciiDigit(trimmed[i - 1]) && char.IsAsciiDigit(trimmed[i + 1]))) grouping = false;
            }

            return new MathDiagnostic(MathValidationCode.NotANumber, [], Suggestion: grouping ? $"Write the number without digit grouping: {ungrouped}." : $"Write the number without spaces or separators: {ungrouped}.");
        }

        // 1e999999999: the exponent of text is limited, never read as zero or infinity.
        var e = trimmed.IndexOfAny('e', 'E');
        if (e > 0 && TryParse(trimmed[..e], out _)
            && BigInteger.TryParse(trimmed[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent)
            && BigInteger.Abs(exponent) > BigRational.MaxExponentMagnitude)
        {
            return new MathDiagnostic(MathValidationCode.NotANumber, [], Suggestion: $"The exponent must be between -{BigRational.MaxExponentMagnitude} and {BigRational.MaxExponentMagnitude}.");
        }

        // 1/0.
        var slash = trimmed.IndexOf('/');
        if (slash > 0 && BigInteger.TryParse(trimmed[..slash], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            var denominator = trimmed[(slash + 1)..];
            if (!denominator.IsEmpty && !denominator.ContainsAnyExcept('0'))
            {
                return new MathDiagnostic(MathValidationCode.NotANumber, [], new TextSpan(start + slash + 1, denominator.Length), "The denominator of a fraction cannot be zero.");
            }
        }

        return new MathDiagnostic(MathValidationCode.NotANumber, []);
    }
}
