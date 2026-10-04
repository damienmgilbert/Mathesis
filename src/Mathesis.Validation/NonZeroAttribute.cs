namespace Mathesis.Validation;

/// <summary>
/// Requires text or a typed value to be a number other than zero, read exactly as <see cref="RationalNumberAttribute"/> reads it:
/// <c>0</c>, <c>-0</c>, <c>0.0</c>, <c>0/5</c>, <c>0e10</c> and <c>0.(0)</c> are zero, <c>1e-100000</c> is not, and <c>-0.0</c> as a <see cref="double"/> is zero.
/// </summary>
/// <remarks>
/// Text that is not a number fails with <see cref="MathValidationCode.NotANumber"/> so the attribute also works alone; zero fails with
/// <see cref="MathValidationCode.Zero"/>. Typed values are those of <see cref="RationalNumberAttribute"/>.
/// </remarks>
public sealed class NonZeroAttribute : MathValidationAttribute
{
    internal override bool IsSupported(object value) => NumberInput.IsSupported(value);

    internal override MathDiagnostic? Evaluate(object value)
    {
        if (NumberInput.Read(value, out var number, out _) is { } problem) return problem;
        return number.Numerator.IsZero ? new MathDiagnostic(MathValidationCode.Zero, []) : null;
    }
}
