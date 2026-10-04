using Mathesis.Numbers;

namespace Mathesis.Validation;

/// <summary>
/// Requires text or a typed value to be a number inside a range whose bounds are exact: the comparison is done on rational numbers,
/// never on <see cref="double"/>s, so <c>0.1 + 0.2</c> is above <c>3/10</c> and <c>1e-100000</c> is above 0.
/// </summary>
/// <remarks>
/// <para>
/// The bounds are given as text in the notation of <see cref="RationalNumberAttribute"/> (<c>"-1/2"</c>, <c>"0.25"</c>, <c>"1e3"</c>); <c>null</c> or
/// <c>""</c> leaves that side unbounded. Both bounds are inclusive unless <see cref="MinimumIsExclusive"/> or <see cref="MaximumIsExclusive"/> says otherwise.
/// Typed values are those of <see cref="RationalNumberAttribute"/>; a binary value means its shortest decimal text.
/// </para>
/// <para>
/// Text that is not a number fails with <see cref="MathValidationCode.NotANumber"/> so the attribute also works alone; a number outside the
/// range fails with <see cref="MathValidationCode.OutOfRange"/>. Message placeholders: <c>{1}</c> is the range in interval notation such as
/// <c>[1/3, 5/2)</c> (<c>-∞</c> and <c>∞</c> for an unbounded side), <c>{2}</c> and <c>{3}</c> are the minimum and maximum as written (empty when unbounded).
/// </para>
/// <para>
/// Configuration errors, reported as <see cref="MathValidationAttribute.GetConfigurationError"/> and thrown on first use: no bound at all, a bound that is
/// not a number, a minimum above the maximum, an empty range (equal bounds with an exclusive side), and an exclusive flag on a side that has no bound.
/// </para>
/// </remarks>
public sealed class ExactRangeAttribute : MathValidationAttribute
{
    private readonly BigRational? _minimum;
    private readonly BigRational? _maximum;
    private readonly bool _minimumIsNumber;
    private readonly bool _maximumIsNumber;

    /// <summary>Creates the range from bounds written as text; <c>null</c> or <c>""</c> means unbounded.</summary>
    /// <param name="minimum">The lower bound, for example <c>"-1/2"</c>.</param>
    /// <param name="maximum">The upper bound, for example <c>"0.75"</c>.</param>
    public ExactRangeAttribute(string? minimum, string? maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
        _minimumIsNumber = NumberInput.TryParseBound(minimum, out _minimum);
        _maximumIsNumber = NumberInput.TryParseBound(maximum, out _maximum);
    }

    /// <summary>The lower bound as written, or <c>null</c> or empty when there is none.</summary>
    public string? Minimum { get; }

    /// <summary>The upper bound as written, or <c>null</c> or empty when there is none.</summary>
    public string? Maximum { get; }

    /// <summary>Whether a value equal to the minimum fails. Defaults to <c>false</c> (the minimum is allowed).</summary>
    public bool MinimumIsExclusive { get; init; }

    /// <summary>Whether a value equal to the maximum fails. Defaults to <c>false</c> (the maximum is allowed).</summary>
    public bool MaximumIsExclusive { get; init; }

    internal override int ExtraPlaceholderCount => 3;

    internal override bool IsSupported(object value) => NumberInput.IsSupported(value);

    internal override string? ValidateOptions()
    {
        if (!_minimumIsNumber) return $"The minimum '{Minimum}' is not a number.";
        if (!_maximumIsNumber) return $"The maximum '{Maximum}' is not a number.";
        if (_minimum is null && _maximum is null) return "Give a minimum or a maximum; without either there is nothing to check.";
        if (_minimum is null && MinimumIsExclusive) return "MinimumIsExclusive is set but there is no minimum.";
        if (_maximum is null && MaximumIsExclusive) return "MaximumIsExclusive is set but there is no maximum.";
        if (_minimum is { } min && _maximum is { } max)
        {
            if (min > max) return $"The minimum {Minimum!.Trim()} is above the maximum {Maximum!.Trim()}.";
            if (min == max && (MinimumIsExclusive || MaximumIsExclusive)) return $"The range {RangeText()} is empty.";
        }

        return null;
    }

    internal override MathDiagnostic? Evaluate(object value)
    {
        if (NumberInput.Read(value, out var number, out _) is { } problem) return problem;

        var inside = (_minimum is not { } min || number > min || (number == min && !MinimumIsExclusive))
            && (_maximum is not { } max || number < max || (number == max && !MaximumIsExclusive));
        return inside ? null : new MathDiagnostic(MathValidationCode.OutOfRange, [RangeText(), _minimum is null ? string.Empty : Minimum!.Trim(), _maximum is null ? string.Empty : Maximum!.Trim()]);
    }

    /// <summary>The range in interval notation with the bounds as written: <c>[1/3, 5/2)</c>, <c>(-∞, 0.5]</c>.</summary>
    private string RangeText() =>
        $"{(_minimum is null || MinimumIsExclusive ? '(' : '[')}{(_minimum is null ? "-∞" : Minimum!.Trim())}, {(_maximum is null ? "∞" : Maximum!.Trim())}{(_maximum is null || MaximumIsExclusive ? ')' : ']')}";
}
