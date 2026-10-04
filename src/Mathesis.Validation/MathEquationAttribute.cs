namespace Mathesis.Validation;

/// <summary>
/// A <see cref="MathExpressionAttribute"/> that requires a single equation <c>a = b</c>: <c>x^2 - 5x + 6 = 0</c> is one, <c>x^2 - 5x + 6</c>,
/// <c>x &lt; 3</c> and <c>1 &lt; x &lt; 5</c> are not. All options of the base attribute apply except <see cref="MathExpressionAttribute.Shape"/>,
/// which is fixed: setting it to anything else is a configuration error.
/// </summary>
public sealed class MathEquationAttribute : MathExpressionAttribute
{
    /// <summary>Creates the attribute, requiring an equation.</summary>
    public MathEquationAttribute()
    {
        Shape = ExpressionShape.Equation;
    }

    internal override string? ValidateOptions() =>
        Shape != ExpressionShape.Equation ? $"Shape is fixed to {nameof(ExpressionShape.Equation)} by MathEquation and cannot be {Shape}; use MathExpression for other shapes." : base.ValidateOptions();
}
