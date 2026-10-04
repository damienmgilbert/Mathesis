using System.Collections.Immutable;

namespace Mathesis.Symbolics.Parsing;

/// <summary>A range of characters in the source text.</summary>
/// <param name="Start">The zero-based index of the first character.</param>
/// <param name="Length">The number of characters; 0 for a position (for example the end of the text).</param>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>The index just past the last character.</summary>
    public int End => Start + Length;

    /// <inheritdoc />
    public override string ToString() => Length == 0 ? $"{Start}" : $"{Start}..{End}";
}

/// <summary>A syntax error with its source span, what was expected, and a suggestion when one is known.</summary>
/// <param name="Message">What is wrong.</param>
/// <param name="Span">Where it is wrong.</param>
/// <param name="Expected">The tokens that would have been accepted at this point, if known.</param>
/// <param name="Suggestion">A concrete fix, such as <c>Did you mean sqrt(x)?</c>.</param>
public sealed record ParseError(string Message, TextSpan Span, ImmutableArray<string> Expected, string? Suggestion = null)
{
    /// <summary>The one-based column of the start of the error (the text is treated as a single line).</summary>
    public int Column => Span.Start + 1;

    /// <inheritdoc />
    public override string ToString() =>
        $"column {Column}: {Message}" + (Expected.IsDefaultOrEmpty ? string.Empty : $" (expected {string.Join(", ", Expected)})") + (Suggestion is null ? string.Empty : $" {Suggestion}");
}

/// <summary>A non-fatal note about how the text was interpreted.</summary>
/// <param name="Code">A stable identifier such as <c>AmbiguousImplicitMultiplication</c> or <c>UnknownFunction</c>.</param>
/// <param name="Message">The explanation.</param>
/// <param name="Span">The text concerned.</param>
public sealed record ParseWarning(string Code, string Message, TextSpan Span);

/// <summary>The outcome of parsing: a Raw tree on success, otherwise the first error, plus any warnings.</summary>
/// <param name="Expr">The parsed expression, or <c>null</c> when parsing failed.</param>
/// <param name="Errors">The errors (at most one: parsing stops at the first).</param>
/// <param name="Warnings">Warnings about ambiguous constructs.</param>
public sealed record ParseResult(Expr? Expr, ImmutableArray<ParseError> Errors, ImmutableArray<ParseWarning> Warnings)
{
    /// <summary>Whether parsing succeeded.</summary>
    public bool Success => Expr is not null;

    /// <summary>Converts to an <see cref="Outcome{T}"/>: <c>Success</c> or <c>Failed</c> with a syntax error.</summary>
    public Outcome<Expr> ToOutcome() =>
        Expr is { } e ? Outcome.Ok(e) : Outcome.Fail<Expr>(new MathError(MathErrorKind.Syntax, Errors[0].ToString()));
}

/// <summary>Thrown by <see cref="Expr.Parse(string, ParserOptions?)"/> for text that is not an expression.</summary>
public sealed class ParseException : FormatException
{
    /// <summary>Creates the exception for an error in <paramref name="text"/>.</summary>
    public ParseException(ParseError error, string text)
        : base($"{error.Message} at column {error.Column} of '{text}'.")
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
        Text = text;
    }

    /// <summary>The syntax error.</summary>
    public ParseError Error { get; }

    /// <summary>The text that was being parsed.</summary>
    public string Text { get; }
}

/// <summary>Options for the linear-text parser.</summary>
public sealed record ParserOptions
{
    /// <summary>
    /// With the default <c>true</c>, a word of letters that is not a known function, constant or declared name is split into
    /// single-letter variables: <c>xy</c> means <c>x·y</c>, <c>xsin(x)</c> means <c>x·sin(x)</c>. Turn it off to keep words such as
    /// <c>speed</c> as one symbol.
    /// </summary>
    public bool SingleLetterVariables { get; init; } = true;

    /// <summary>Treat <c>e</c> as an ordinary symbol instead of Euler's number.</summary>
    public bool EIsSymbol { get; init; }

    /// <summary>The spelling of the imaginary unit: <c>"I"</c> (default, so that <c>i</c> stays free for indices) or <c>"i"</c>.</summary>
    public string ImaginaryUnit { get; init; } = "I";

    /// <summary>Make a bare <c>log x</c> the natural logarithm (university and programming convention) instead of base 10.</summary>
    public bool LogMeansNatural { get; init; }

    /// <summary>Read <c>N</c>, <c>Z</c>, <c>Q</c>, <c>R</c>, <c>C</c> as the number sets ℕ, ℤ, ℚ, ℝ, ℂ.</summary>
    public bool NumberSetLetters { get; init; } = true;

    /// <summary>Read the exponents <c>T</c>, <c>H</c> and <c>c</c> as transpose, conjugate transpose and complement: <c>A^T</c>, <c>A^H</c>, <c>A^c</c>.</summary>
    public bool PostfixPowerLetters { get; init; } = true;

    /// <summary>Read names ending in an underscore, such as <c>a_</c>, as pattern variables (<see cref="Wild"/>).</summary>
    public bool AllowWilds { get; init; }

    /// <summary>The sort given to symbols that are not declared; ℝ in real mode.</summary>
    public Sort DefaultSort { get; init; } = Sort.Real;

    /// <summary>Declared sorts by symbol name. A name declared with a function sort is applied with <c>f(x)</c>.</summary>
    public IReadOnlyDictionary<string, Sort>? Declarations { get; init; }

    /// <summary>Names that are always function symbols when followed by <c>(</c>; defaults to <c>f</c>, <c>g</c>, <c>h</c>.</summary>
    public IReadOnlyCollection<string> FunctionSymbols { get; init; } = ["f", "g", "h"];
}
