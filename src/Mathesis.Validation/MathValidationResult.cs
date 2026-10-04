using System.ComponentModel.DataAnnotations;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation;

/// <summary>
/// A failed validation: the standard <see cref="ValidationResult"/> (message and member names) plus a stable <see cref="Code"/>, the
/// <see cref="Span"/> of the text concerned and a <see cref="Suggestion"/> when one is known.
/// </summary>
/// <remarks>
/// <see cref="ValidationResult.ErrorMessage"/> is never empty. <see cref="ValidationResult.MemberNames"/> holds the member the
/// validation context names, so form frameworks can attach the message to a field; it is empty when the context names no member.
/// </remarks>
public sealed class MathValidationResult : ValidationResult
{
    internal MathValidationResult(MathValidationCode code, string errorMessage, IEnumerable<string>? memberNames, TextSpan? span, string? suggestion)
        : base(errorMessage, memberNames)
    {
        Code = code;
        Span = span;
        Suggestion = suggestion;
    }

    /// <summary>Why the value failed.</summary>
    public MathValidationCode Code { get; }

    /// <summary>The characters of the validated text the problem concerns, or <c>null</c> when it concerns the whole value (or the value is not text).</summary>
    public TextSpan? Span { get; }

    /// <summary>A concrete fix such as <c>Did you mean sqrt(x)?</c>, or <c>null</c> when none is known.</summary>
    public string? Suggestion { get; }
}
