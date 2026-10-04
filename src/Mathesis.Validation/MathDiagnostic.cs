using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation;

/// <summary>
/// The verdict of one attribute on one value: what is wrong, where and how to fix it. It is computed once and turned into a
/// <see cref="MathValidationResult"/> or into a plain valid/invalid answer by <see cref="MathValidationAttribute"/>.
/// </summary>
/// <param name="Code">Why the value failed.</param>
/// <param name="Details">The values for the message placeholders <c>{1}</c>, <c>{2}</c> and so on, in order.</param>
/// <param name="Span">The text concerned, or <c>null</c> for the whole value.</param>
/// <param name="Suggestion">A concrete fix, or <c>null</c>.</param>
internal sealed record MathDiagnostic(MathValidationCode Code, object?[] Details, TextSpan? Span = null, string? Suggestion = null);
