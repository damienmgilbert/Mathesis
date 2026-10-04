using System.Globalization;
using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Parsing;
using Mathesis.Testing;

namespace Mathesis.Validation.Tests;

/// <summary><see cref="MathMatrixAttribute"/> (PLAN-M9 Phase 4).</summary>
[TestClass]
public class MatrixAttributeTests
{
    private const int Seed = 20261008;

    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    private sealed record Row(MathMatrixAttribute Attribute, object? Value, MathValidationCode? Code, string? Message = null, string? Suggestion = null);

    private static readonly MathMatrixAttribute Any = new();

    private static string Entry(int row, int column) => $"Value must contain only numbers; the entry in row {row}, column {column} is not a number.";

    private static string Square(int rows, int columns) => "[" + string.Join(", ", Enumerable.Range(0, rows).Select(r => "[" + string.Join(", ", Enumerable.Range(0, columns).Select(c => (r * columns + c + 1).ToString(CultureInfo.InvariantCulture))) + "]")) + "]";

    private static List<Row> Rows()
    {
        var latex = new MathMatrixAttribute { Format = InputFormat.Latex };
        var threeByThree = new MathMatrixAttribute { Rows = 3, Columns = 3 };
        var symbolic = new MathMatrixAttribute { NumericEntries = false };

        return
        [
            // ----- matrices -----
            new(Any, "[[1, 2], [3, 4]]", null),
            new(Any, "[[1], [2], [3]]", null),
            new(Any, "[1, 2, 3]", null),
            new(Any, "[[1]]", null),
            new(Any, "[[-1/2, 1/-2], [+1, 0.25]]", null),
            new(Any, "[[2e3, -0.5, -(1/2), -1/-2]]", null),
            new(Any, "[[−3, 1.5e-3]]", null),
            new(Any, "", null),
            new(Any, null, null),
            new(new MathMatrixAttribute { Rows = 3, Columns = 1 }, "[1, 2, 3]", null),
            new(new MathMatrixAttribute { Rows = 2, Columns = 2, Square = true }, "[[1, 2], [3, 4]]", null),
            new(latex, @"\begin{pmatrix}1&2\\3&4\end{pmatrix}", null),
            new(latex, @"\begin{bmatrix}1&2\\3&4\end{bmatrix}", null),
            new(latex, @"\begin{pmatrix}-\frac{1}{2}&2\\\frac{-1}{2}&+3\end{pmatrix}", null),

            // ----- not matrices -----
            new(Any, "[1, 2]", MathValidationCode.NotAMatrix, "Value must be a matrix, for example [[1, 2], [3, 4]].", "For a column vector write [[1], [2]]."),
            new(Any, "[-1/2, 3]", MathValidationCode.NotAMatrix, Suggestion: "For a column vector write [[-1/2], [3]]."),
            new(Any, "[1, 2)", MathValidationCode.NotAMatrix),
            new(Any, "x + 1", MathValidationCode.NotAMatrix),
            new(Any, "5", MathValidationCode.NotAMatrix),
            new(Any, "{1, 2}", MathValidationCode.NotAMatrix),
            new(Any, "(1, 2)", MathValidationCode.NotAMatrix),
            new(Any, "[[1, 2], [3, 4]]^T", MathValidationCode.NotAMatrix),
            new(Any, "det([[1, 2], [3, 4]])", MathValidationCode.NotAMatrix),
            new(latex, @"\begin{vmatrix}1&2\\3&4\end{vmatrix}", MathValidationCode.NotAMatrix, Suggestion: @"\begin{vmatrix} is a determinant; write \begin{pmatrix} or \begin{bmatrix} for a matrix."),

            // ----- syntax: ragged rows keep the parser's message -----
            new(Any, "[[1, 2], [3]]", MathValidationCode.Syntax, "Value is not valid: Matrix rows must have equal length: expected 2 entries but found 1."),
            new(Any, "[[1, 2,], [3, 4]]", MathValidationCode.Syntax),
            new(Any, "[[]]", MathValidationCode.Syntax),
            new(Any, "[]", MathValidationCode.Syntax),
            new(Any, "[[1, 2]; [3, 4]]", MathValidationCode.Syntax),
            new(latex, @"\begin{pmatrix}1&2\\3\end{pmatrix}", MathValidationCode.Syntax),

            // ----- dimensions -----
            new(threeByThree, "[[1, 2, 3], [4, 5, 6]]", MathValidationCode.WrongDimensions, "Value must be a matrix of size 3×3, but it is 2×3."),
            new(threeByThree, Square(3, 3), null),
            new(new MathMatrixAttribute { Rows = 2 }, "[1, 2, 3]", MathValidationCode.WrongDimensions, "Value must be a matrix of size 2×n, but it is 3×1."),
            new(new MathMatrixAttribute { Columns = 2 }, "[1, 2, 3]", MathValidationCode.WrongDimensions, "Value must be a matrix of size m×2, but it is 3×1."),
            new(new MathMatrixAttribute { Columns = 2 }, "[[x, 1, 2]]", MathValidationCode.WrongDimensions),
            new(new MathMatrixAttribute { Square = true }, "[[1, 2, 3], [4, 5, 6]]", MathValidationCode.NotSquare, "Value must be a square matrix, but it is 2×3."),
            new(new MathMatrixAttribute { Square = true }, "[1, 2, 3]", MathValidationCode.NotSquare, "Value must be a square matrix, but it is 3×1."),
            new(new MathMatrixAttribute { Square = true }, "[[7]]", null),
            new(Any, Square(11, 11), MathValidationCode.DimensionTooLarge, "Value is 11×11, which is larger than the allowed size 10×10."),
            new(Any, Square(1, 11), MathValidationCode.DimensionTooLarge, "Value is 1×11, which is larger than the allowed size 10×10."),
            new(Any, Square(10, 10), null),
            new(new MathMatrixAttribute { MaxDimension = 2 }, "[1, 2, 3]", MathValidationCode.DimensionTooLarge, "Value is 3×1, which is larger than the allowed size 2×2."),
            new(new MathMatrixAttribute { MaxDimension = 12, MaxLength = 10_000 }, Square(11, 11), null),

            // ----- numeric entries, by their written shape -----
            new(Any, "[[1, x], [2, 3]]", MathValidationCode.NonNumericEntry, Entry(1, 2)),
            new(symbolic, "[[1, x], [2, 3]]", null),
            new(Any, "[[2^3, 1]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(Any, "[[1/2/3, 1]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(Any, "[[1, pi]]", MathValidationCode.NonNumericEntry, Entry(1, 2)),
            new(Any, "[[1, 2], [3, sqrt(2)]]", MathValidationCode.NonNumericEntry, Entry(2, 2)),
            new(Any, "[[1/0]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(Any, "[[1, -1/-0]]", MathValidationCode.NonNumericEntry, Entry(1, 2)),
            new(Any, "[[--1]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(Any, "[[[1]]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(Any, "[[1 + 1]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(Any, "[[2*3]]", MathValidationCode.NonNumericEntry, Entry(1, 1)),
            new(symbolic, "[[sin(x), 1/0], [2^3, [[1]]]]", null),
            new(latex, @"\begin{pmatrix}1&x\\2&3\end{pmatrix}", MathValidationCode.NonNumericEntry, Entry(1, 2)),
            new(latex, @"\begin{pmatrix}\frac{1}{2}&\sqrt{2}\end{pmatrix}", MathValidationCode.NonNumericEntry, Entry(1, 2)),
        ];
    }

    private static void AssertRows(IEnumerable<Row> rows)
    {
        var failures = new List<string>();
        foreach (var row in rows)
        {
            var label = $"{row.Value ?? "null"}";
            var result = row.Attribute.Check(row.Value, "Value", "Member");
            if (result?.Code != row.Code)
            {
                failures.Add($"{label}: expected {row.Code?.ToString() ?? "valid"}, got {result?.Code.ToString() ?? "valid"} ({result?.ErrorMessage})");
                continue;
            }

            if (row.Message is not null && row.Message != result?.ErrorMessage) failures.Add($"{label}: message '{result?.ErrorMessage}' != '{row.Message}'");
            if (result is not null && row.Code != MathValidationCode.Syntax && row.Suggestion != result.Suggestion) failures.Add($"{label}: suggestion '{result.Suggestion}' != '{row.Suggestion}'");

            if (row.Code == MathValidationCode.Syntax)
            {
                var text = (string)row.Value!;
                var error = (row.Attribute.Format == InputFormat.Latex ? LatexParser.Parse(text) : Parser.Parse(text)).Errors[0];
                if (result!.Span != error.Span || result.Suggestion != error.Suggestion || result.ErrorMessage != $"Value is not valid: {error.Message}") failures.Add($"{label}: not the parser's span, suggestion and message");
            }

            Assert.AreEqual(row.Code is null, row.Attribute.IsValid(row.Value), label);
            Assert.AreEqual(row.Code, (row.Attribute.GetValidationResult(row.Value, MathValidationAttributeTests.Context()) as MathValidationResult)?.Code, label);

            if (row.Value is string text2 && !string.IsNullOrWhiteSpace(text2) && row.Code is not (MathValidationCode.Syntax or MathValidationCode.TooLong))
            {
                var tree = (row.Attribute.Format == InputFormat.Latex ? LatexParser.Parse(text2) : Parser.Parse(text2)).Expr!;
                var typed = row.Attribute.Check(tree, "Value", "Member");
                if (typed?.Code != row.Code || typed?.ErrorMessage != result?.ErrorMessage) failures.Add($"{label}: as an Expr {typed?.Code.ToString() ?? "valid"} '{typed?.ErrorMessage}'");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void MatrixTable()
    {
        var rows = Rows();

        Assert.IsTrue(rows.Count >= 40);
        AssertRows(rows);
    }

    [TestMethod]
    public void FiveHundredSeededExactMatricesAreAcceptedWithTheRightDimensions()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 500; i++)
        {
            var rows = gen.Random.Next(1, 11);
            var columns = gen.Random.Next(1, 11);
            var matrix = DenseMatrix.Create(rows, columns, (_, _) => gen.Rational(12));
            var text = "[" + string.Join(", ", Enumerable.Range(0, rows).Select(r => "[" + string.Join(", ", Enumerable.Range(0, columns).Select(c => matrix[r, c].ToString())) + "]")) + "]";
            var where = $"seed {Seed}, case {i}, {rows}×{columns}: {text[..Math.Min(text.Length, 80)]}";

            Assert.IsNull(new MathMatrixAttribute { Rows = rows, Columns = columns, MaxLength = 10_000 }.Check(text), where);
            Assert.AreEqual(rows == columns, new MathMatrixAttribute { Square = true, MaxLength = 10_000 }.Check(text) is null, where);
            Assert.AreEqual(MathValidationCode.WrongDimensions, new MathMatrixAttribute { Rows = rows % 10 + 1, Columns = columns, MaxLength = 10_000 }.Check(text)?.Code, where);
            Assert.AreEqual(MathValidationCode.WrongDimensions, new MathMatrixAttribute { Columns = columns % 10 + 1, MaxLength = 10_000 }.Check(text)?.Code, where);

            // what was accepted is exactly the matrix: same size, and every entry reads back as the same rational
            var literal = (MatrixLiteral)Parser.Parse(text).Expr!;
            Assert.AreEqual(rows, literal.Rows, where);
            Assert.AreEqual(columns, literal.Columns, where);
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++) Assert.AreEqual(new Number(matrix[r, c]), Normalizer.Canonical(literal[r, c]), where);
            }

            // one entry replaced by a symbol is reported at its one-based position
            var (badRow, badColumn) = (gen.Random.Next(rows), gen.Random.Next(columns));
            var broken = "[" + string.Join(", ", Enumerable.Range(0, rows).Select(r => "[" + string.Join(", ", Enumerable.Range(0, columns).Select(c => r == badRow && c == badColumn ? "t" : matrix[r, c].ToString())) + "]")) + "]";
            Assert.AreEqual(Entry(badRow + 1, badColumn + 1), new MathMatrixAttribute { MaxLength = 10_000 }.Check(broken)?.ErrorMessage, where);
        }
    }

    [TestMethod]
    public void TypedMatrixLiteralsAreCheckedWithoutParsing()
    {
        var literal = new MatrixLiteral(2, 2, [new Number(1), new Number(BigRational.Create(-1, 2)), new Float(0.5), new Symbol("x")]);

        Assert.AreEqual(Entry(2, 2), Any.Check(literal)!.ErrorMessage);
        Assert.IsNull(new MathMatrixAttribute { NumericEntries = false, Square = true }.Check(literal));
        Assert.AreEqual(MathValidationCode.NotAMatrix, Any.Check(new Symbol("A"))!.Code);
        Assert.AreEqual(MathValidationCode.NonNumericEntry, Any.Check(new MatrixLiteral(1, 1, [new Float(double.NaN)]))!.Code);
        Assert.IsNull(new MathMatrixAttribute { MaxLength = 1 }.Check(new MatrixLiteral(1, 1, [new Number(1)])), "MaxLength applies to text only");
        Assert.ThrowsExactly<InvalidOperationException>(() => Any.IsValid(DenseMatrix.Create(1, 1, (_, _) => BigRational.One)));
    }

    [TestMethod]
    public void ConfigurationErrorsThrowAsSpecified()
    {
        var rows = new (string Label, MathMatrixAttribute Attribute, string Message)[]
        {
            ("MaxDimension 0", new MathMatrixAttribute { MaxDimension = 0 }, "MaxDimension must be between 1 and 1000 but is 0."),
            ("MaxDimension 1001", new MathMatrixAttribute { MaxDimension = 1_001 }, "MaxDimension must be between 1 and 1000 but is 1001."),
            ("negative rows", new MathMatrixAttribute { Rows = -1 }, "Rows must be between 0 (any) and MaxDimension (10) but is -1."),
            ("rows above the maximum", new MathMatrixAttribute { Rows = 11 }, "Rows must be between 0 (any) and MaxDimension (10) but is 11."),
            ("columns above the maximum", new MathMatrixAttribute { Columns = 4, MaxDimension = 3 }, "Columns must be between 0 (any) and MaxDimension (3) but is 4."),
            ("square with different sizes", new MathMatrixAttribute { Rows = 2, Columns = 3, Square = true }, "Square is set but Rows (2) and Columns (3) differ."),
            ("undefined format", new MathMatrixAttribute { Format = (InputFormat)5 }, "Format 5 is not a defined value."),
            ("MaxLength 100001", new MathMatrixAttribute { MaxLength = 100_001 }, "MaxLength must be between 1 and 100000 but is 100001."),
            ("undefined placeholder", new MathMatrixAttribute { ErrorMessage = "{3}" }, "{3}"),
        };

        foreach (var (label, attribute, message) in rows)
        {
            var text = attribute.GetConfigurationError();
            Assert.IsNotNull(text, label);
            StringAssert.StartsWith(text, "MathMatrixAttribute: ", label);
            StringAssert.Contains(text, message, label);
            foreach (var value in new object?[] { null, "", "[[1]]" })
            {
                Assert.AreEqual(text, Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid(value), label).Message, label);
            }
        }

        foreach (var attribute in new[] { new MathMatrixAttribute { Rows = 10, Columns = 10 }, new MathMatrixAttribute { Rows = 3, Square = true }, new MathMatrixAttribute { MaxDimension = 1_000 }, new MathMatrixAttribute { MaxDimension = 1 } })
        {
            Assert.IsNull(attribute.GetConfigurationError());
        }

        Assert.AreEqual(0, Any.Rows);
        Assert.AreEqual(0, Any.Columns);
        Assert.IsFalse(Any.Square);
        Assert.IsTrue(Any.NumericEntries);
        Assert.AreEqual(10, Any.MaxDimension);
        Assert.AreEqual(InputFormat.Text, Any.Format);
        Assert.IsTrue(typeof(MathMatrixAttribute).IsSealed);
    }

    [TestMethod]
    public void VerdictsAreTheSameUnderEveryCulture()
    {
        var rows = Rows();
        string Describe(CultureInfo culture)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return string.Join("\n", rows.Select(r => r.Attribute.Check(r.Value, "Value", "Member") is { } result ? $"{result.Code}|{result.Span}|{result.Suggestion}|{result.ErrorMessage}" : "valid"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        var baseline = Describe(CultureInfo.InvariantCulture);
        foreach (var name in new[] { "en-US", "de-DE", "fr-FR", "ar-SA", "tr-TR", "ja-JP" })
        {
            Assert.AreEqual(baseline, Describe(new CultureInfo(name)), name);
        }
    }
}
