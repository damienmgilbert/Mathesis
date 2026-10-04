namespace Mathesis.Validation;

/// <summary>
/// The overall form an expression must have. The classes follow what <c>Solve</c> accepts: an equation or an inequality is a single relation
/// with two sides, so <c>1 &lt; x &lt; 5</c>, which reads as <c>1 &lt; x and x &lt; 5</c>, is a statement.
/// </summary>
public enum ExpressionShape
{
    /// <summary>No requirement.</summary>
    Any,

    /// <summary>Anything that is not a relation or a logical statement: <c>x + 1</c>, <c>sin(x)</c>, a matrix, a set, a number.</summary>
    Expression,

    /// <summary>A single equation <c>a = b</c>.</summary>
    Equation,

    /// <summary>A single inequality <c>a &lt; b</c>, <c>a &lt;= b</c>, <c>a &gt; b</c>, <c>a &gt;= b</c> or <c>a != b</c>.</summary>
    Inequality,

    /// <summary>An interval with a bracket on at least one side: <c>[0, 1)</c>, <c>(0, 1]</c>, <c>[0, oo)</c>. A pair <c>(0, 1)</c> is a tuple, not an interval.</summary>
    Interval,
}
