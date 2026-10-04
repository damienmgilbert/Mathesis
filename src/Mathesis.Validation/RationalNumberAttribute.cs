namespace Mathesis.Validation;

/// <summary>
/// Requires text or a typed value to be a rational number, read exactly: <c>3/4</c>, <c>-2</c>, <c>0.125</c>, the repeating decimal
/// <c>0.1(6)</c> and <c>1.5e-3</c> are numbers, and <c>0.1</c> means 1/10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Text</b> is read with the invariant culture whatever the current one is: <c>.</c> is the decimal point, <c>0,5</c> and <c>1,000</c> are
/// not numbers (the suggestion names the point), digit grouping is not accepted, U+2212 (−) is a minus sign, and an exponent beyond
/// ±100,000 is not a number rather than zero.
/// <b>Typed values</b> are <see cref="Mathesis.Numbers.BigRational"/>, <see cref="System.Numerics.BigInteger"/>, <see cref="int"/>, <see cref="long"/>,
/// <see cref="decimal"/> (all exact), <see cref="double"/> and <see cref="float"/>. A binary value means the shortest decimal text that reads
/// back as it, so <c>0.1</c> is 1/10 and not its binary expansion; NaN and the infinities are not numbers.
/// </para>
/// <para>
/// The checks run in this order: number, <see cref="IntegerOnly"/> (a property of the value, so <c>4/2</c> and <c>2.0</c> are integers),
/// <see cref="AllowFractions"/>, <see cref="AllowDecimals"/> (properties of how text is written, which typed values do not have; a decimal
/// is any text with a point, a repeating block or an exponent). Failures: <see cref="MathValidationCode.NotANumber"/>,
/// <see cref="MathValidationCode.NotAnInteger"/>, <see cref="MathValidationCode.FractionNotAllowed"/>, <see cref="MathValidationCode.DecimalNotAllowed"/>
/// and <see cref="MathValidationCode.TooLong"/>. The suggestion, when there is one, is the exact equivalent that the options allow.
/// </para>
/// </remarks>
public sealed class RationalNumberAttribute : MathValidationAttribute
{
    /// <summary>Require a whole number. This is about the value: <c>4/2</c>, <c>2.0</c> and <c>2e0</c> pass, <c>1/2</c> and <c>0.5</c> do not.</summary>
    public bool IntegerOnly { get; init; }

    /// <summary>Whether text may be written as a fraction <c>a/b</c>. Defaults to <c>true</c>; ignored for typed values.</summary>
    public bool AllowFractions { get; init; } = true;

    /// <summary>Whether text may be written with a decimal point, a repeating block <c>0.(3)</c> or an exponent <c>1e3</c>. Defaults to <c>true</c>; ignored for typed values.</summary>
    public bool AllowDecimals { get; init; } = true;

    internal override bool IsSupported(object value) => NumberInput.IsSupported(value);

    internal override MathDiagnostic? Evaluate(object value)
    {
        if (NumberInput.Read(value, out var number, out var notation) is { } problem) return problem;

        if (IntegerOnly && !number.IsInteger) return new MathDiagnostic(MathValidationCode.NotAnInteger, []);

        if (!AllowFractions && notation.HasFlag(Notation.Fraction))
        {
            // The same number in a notation the options allow: an integer, or a decimal when the denominator is small enough for its expansion to be short.
            // A number too long to print in a message gets no suggestion.
            var suggestion = !ExpressionText.IsSmall(number) ? null : number.IsInteger ? number.ToString() : AllowDecimals && number.Denominator <= 500 ? number.ToDecimalString() : null;
            return new MathDiagnostic(MathValidationCode.FractionNotAllowed, [], Suggestion: suggestion is null ? null : $"Write it as {suggestion}.");
        }

        if (!AllowDecimals && notation.HasFlag(Notation.Decimal))
        {
            var suggestion = ExpressionText.IsSmall(number) && (number.IsInteger || AllowFractions) ? number.ToString() : null;
            return new MathDiagnostic(MathValidationCode.DecimalNotAllowed, [], Suggestion: suggestion is null ? null : $"Write it as {suggestion}.");
        }

        return null;
    }
}
