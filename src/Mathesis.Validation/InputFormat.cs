namespace Mathesis.Validation;

/// <summary>The notation of text that a validation attribute reads.</summary>
public enum InputFormat
{
    /// <summary>The linear input notation of <c>docs/design/05-syntax-trees-and-notation.md</c>: <c>x^2 - 5x + 6 = 0</c>.</summary>
    Text,

    /// <summary>The LaTeX subset the library reads: <c>x^{2} - 5x + 6 = 0</c>, <c>\frac{1}{2}</c>.</summary>
    Latex,
}
