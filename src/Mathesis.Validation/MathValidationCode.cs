namespace Mathesis.Validation;

/// <summary>
/// Why a value failed validation. The names are stable identifiers: apps can switch on them, store them or map them to their own
/// messages. Each code has a default message template in the embedded <c>Messages.resx</c>, and the placeholders <c>{1}</c>, <c>{2}</c>
/// and <c>{3}</c> of that template are documented per member.
/// </summary>
public enum MathValidationCode
{
    /// <summary>The text is longer than <see cref="MathValidationAttribute.MaxLength"/>. <c>{1}</c> is the limit.</summary>
    TooLong,

    /// <summary>The text is not a number.</summary>
    NotANumber,

    /// <summary>The number is not a whole number.</summary>
    NotAnInteger,

    /// <summary>The number is written as a fraction where fractions are not allowed.</summary>
    FractionNotAllowed,

    /// <summary>The number is written as a decimal where decimals are not allowed.</summary>
    DecimalNotAllowed,

    /// <summary>The number is outside the allowed range. <c>{1}</c> is the range in interval notation, <c>{2}</c> and <c>{3}</c> the minimum and maximum as written.</summary>
    OutOfRange,

    /// <summary>The number is zero where zero is not allowed.</summary>
    Zero,

    /// <summary>The text is not valid notation. <c>{1}</c> is the parser's explanation.</summary>
    Syntax,

    /// <summary>The parser reads the text in a way the author may not intend. <c>{1}</c> is the parser's warning.</summary>
    Ambiguous,

    /// <summary>The text calls a function that is not known. <c>{1}</c> is the parser's warning.</summary>
    UnknownFunction,

    /// <summary>The expression does not make sense mathematically. <c>{1}</c> is the sort checker's explanation.</summary>
    IllSorted,

    /// <summary>The expression has another shape than required. <c>{1}</c> is the shape expected, <c>{2}</c> the shape found.</summary>
    WrongShape,

    /// <summary>The expression uses a variable that is not allowed. <c>{1}</c> names the variables.</summary>
    UnknownVariable,

    /// <summary>The expression lacks a variable that is required. <c>{1}</c> names the variables.</summary>
    MissingVariable,

    /// <summary>The expression uses a function of a disallowed family. <c>{1}</c> names the functions.</summary>
    DisallowedFunction,

    /// <summary>The expression is not a polynomial. <c>{1}</c> is the construct that prevents it.</summary>
    NotAPolynomial,

    /// <summary>The polynomial's degree is too high. <c>{1}</c> is the degree, <c>{2}</c> the maximum.</summary>
    DegreeTooHigh,

    /// <summary>The text is not a matrix.</summary>
    NotAMatrix,

    /// <summary>The matrix has other dimensions than required. <c>{1}</c> is the size required, <c>{2}</c> the size found.</summary>
    WrongDimensions,

    /// <summary>The matrix is not square. <c>{1}</c> is the size found.</summary>
    NotSquare,

    /// <summary>A matrix entry is not a number. <c>{1}</c> is its row, <c>{2}</c> its column (one-based).</summary>
    NonNumericEntry,

    /// <summary>The matrix has too many rows or columns. <c>{1}</c> is the size found, <c>{2}</c> the maximum.</summary>
    DimensionTooLarge,
}
