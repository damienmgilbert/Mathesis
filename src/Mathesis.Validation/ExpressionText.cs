using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace Mathesis.Validation;

/// <summary>Short, cheap texts of numbers and subexpressions for messages and suggestions; every suggestion that shows a value goes through here.</summary>
internal static class ExpressionText
{
    // Printing a number of 100,000 digits takes a third of a second, and no message needs one; 3,400 bits is about 1,000 digits.
    private const long MaxNumberBits = 3_400;

    /// <summary>Whether <paramref name="value"/> is small enough to print in a message (about 1,000 digits).</summary>
    internal static bool IsSmall(BigRational value) => value.Numerator.GetBitLength() + value.Denominator.GetBitLength() <= MaxNumberBits;

    /// <summary>The text of <paramref name="expr"/> when it has at most <paramref name="maxLength"/> characters and no huge number; otherwise <c>false</c>, without printing.</summary>
    internal static bool TryDescribe(Expr expr, int maxLength, out string text)
    {
        text = string.Empty;
        if (expr.LeafCount > maxLength) return false;
        foreach (var (node, _) in expr.Walk())
        {
            if (node is Number { Value: var value } && !IsSmall(value)) return false;
        }

        var printed = expr.ToString();
        if (printed.Length > maxLength) return false;
        text = printed;
        return true;
    }
}
