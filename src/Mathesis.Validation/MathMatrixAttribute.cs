using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation;

/// <summary>
/// Requires text, LaTeX or an <see cref="Expr"/> to be a matrix literal such as <c>[[1, 2], [3, 4]]</c> (a column vector <c>[1, 2, 3]</c> is a 3×1 matrix),
/// optionally of given dimensions and with numeric entries. Entries are inspected as written and never evaluated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order of checks; the first failure is reported:</b> parse (<see cref="MathValidationCode.Syntax"/>; ragged rows keep the parser's message),
/// <see cref="MathValidationCode.NotAMatrix"/>, <see cref="MathValidationCode.DimensionTooLarge"/> (beyond <see cref="MaxDimension"/>),
/// <see cref="MathValidationCode.WrongDimensions"/> (<see cref="Rows"/>, <see cref="Columns"/>), <see cref="MathValidationCode.NotSquare"/> and
/// <see cref="MathValidationCode.NonNumericEntry"/>. Message placeholders: <c>{1}</c> and <c>{2}</c> are sizes such as <c>3×3</c> (expected, then found; a size
/// that is not fixed shows as <c>m</c> or <c>n</c>), or the one-based row and column of the entry.
/// </para>
/// <para>
/// <b>Numeric entries</b> are recognized by their shape alone: a number (<c>2</c>, <c>+1</c>, <c>0.25</c>, <c>2e3</c>), a negated number (<c>-3</c>), or a fraction
/// <c>a/b</c> of such numbers, possibly negated as a whole (<c>-1/2</c>, <c>1/-2</c>, LaTeX <c>-\frac{1}{2}</c>), with a denominator other than zero. Anything else is
/// not numeric, including a chained division <c>1/2/3</c>, arithmetic such as <c>2^3</c>, constants such as <c>pi</c> and variables.
/// </para>
/// <para>
/// <c>[1, 2]</c> is the closed interval from 1 to 2, not a vector, so it is <see cref="MathValidationCode.NotAMatrix"/> with the suggestion <c>[[1], [2]]</c>;
/// LaTeX <c>\begin{vmatrix}</c> is a determinant, also not a matrix.
/// </para>
/// </remarks>
public sealed class MathMatrixAttribute : MathValidationAttribute
{
    private static readonly ParserOptions Options = new();

    /// <summary>The required number of rows, or 0 (the default) for any.</summary>
    public int Rows { get; init; }

    /// <summary>The required number of columns, or 0 (the default) for any.</summary>
    public int Columns { get; init; }

    /// <summary>Whether the matrix must be square. Defaults to <c>false</c>.</summary>
    public bool Square { get; init; }

    /// <summary>Whether every entry must be a number written as described in the remarks. Defaults to <c>true</c>; <c>false</c> allows symbolic entries.</summary>
    public bool NumericEntries { get; init; } = true;

    /// <summary>The largest number of rows and of columns, between 1 and 1,000. Defaults to 10.</summary>
    public int MaxDimension { get; init; } = 10;

    /// <summary>The notation of text values. Defaults to <see cref="InputFormat.Text"/>; ignored for <see cref="Expr"/> values.</summary>
    public InputFormat Format { get; init; }

    internal override int ExtraPlaceholderCount => 2;

    // NonNumericEntry fills {1} and {2} with the row and the column.
    internal override int NumericPlaceholders => (1 << 1) | (1 << 2);

    internal override bool IsSupported(object value) => value is Expr;

    internal override string? ValidateOptions()
    {
        if (MaxDimension is < 1 or > 1_000) return $"MaxDimension must be between 1 and 1000 but is {MaxDimension}.";
        if (Rows < 0 || Rows > MaxDimension) return $"Rows must be between 0 (any) and MaxDimension ({MaxDimension}) but is {Rows}.";
        if (Columns < 0 || Columns > MaxDimension) return $"Columns must be between 0 (any) and MaxDimension ({MaxDimension}) but is {Columns}.";
        if (Square && Rows > 0 && Columns > 0 && Rows != Columns) return $"Square is set but Rows ({Rows}) and Columns ({Columns}) differ.";
        if (!Enum.IsDefined(Format)) return $"Format {(int)Format} is not a defined value.";
        return null;
    }

    internal override MathDiagnostic? Evaluate(object value)
    {
        Expr expr;
        if (value is Expr typed)
        {
            expr = typed;
        }
        else
        {
            if (ParseText((string)value, Format, Options, out var parsed) is { } syntax) return syntax;
            expr = parsed.Expr!;
        }

        if (expr is not MatrixLiteral matrix)
        {
            var suggestion = expr switch
            {
                // [a, b] is the closed interval; a two-entry column vector needs one bracket per row.
                IntervalLiteral { LowerClosed: true, UpperClosed: true } interval when ExpressionText.TryDescribe(interval.Lower, 30, out var lower) && ExpressionText.TryDescribe(interval.Upper, 30, out var upper)
                    => $"For a column vector write [[{lower}], [{upper}]].",
                Apply { Operator.Id: "det", Arguments: [MatrixLiteral] } when Format == InputFormat.Latex
                    => @"\begin{vmatrix} is a determinant; write \begin{pmatrix} or \begin{bmatrix} for a matrix.",
                _ => null,
            };
            return new MathDiagnostic(MathValidationCode.NotAMatrix, [], Suggestion: suggestion);
        }

        var size = $"{matrix.Rows}×{matrix.Columns}";
        if (matrix.Rows > MaxDimension || matrix.Columns > MaxDimension)
        {
            return new MathDiagnostic(MathValidationCode.DimensionTooLarge, [size, $"{MaxDimension}×{MaxDimension}"]);
        }

        if ((Rows > 0 && matrix.Rows != Rows) || (Columns > 0 && matrix.Columns != Columns))
        {
            return new MathDiagnostic(MathValidationCode.WrongDimensions, [$"{(Rows > 0 ? Rows : "m")}×{(Columns > 0 ? Columns : "n")}", size]);
        }

        if (Square && matrix.Rows != matrix.Columns) return new MathDiagnostic(MathValidationCode.NotSquare, [size]);

        if (NumericEntries)
        {
            static bool IsNumber(Expr e) => e is Number or Apply { Operator.Id: "neg", Arguments: [Number] };
            static bool IsZero(Expr e) => e is Number { Value.Numerator.IsZero: true } or Apply { Operator.Id: "neg", Arguments: [Number { Value.Numerator.IsZero: true }] };
            static bool IsFraction(Expr e) => e is Apply { Operator.Id: "div", Arguments: [var a, var b] } && IsNumber(a) && IsNumber(b) && !IsZero(b);

            for (var row = 0; row < matrix.Rows; row++)
            {
                for (var column = 0; column < matrix.Columns; column++)
                {
                    var entry = matrix[row, column];
                    var numeric = entry switch
                    {
                        Number => true,
                        Float { Value: var f } => double.IsFinite(f),
                        Apply { Operator.Id: "neg", Arguments: [var inner] } => inner is Number || IsFraction(inner),
                        _ => IsFraction(entry),
                    };
                    if (!numeric) return new MathDiagnostic(MathValidationCode.NonNumericEntry, [row + 1, column + 1]);
                }
            }
        }

        return null;
    }
}
