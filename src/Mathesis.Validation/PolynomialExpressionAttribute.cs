using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Representations;

namespace Mathesis.Validation;

/// <summary>
/// Requires text, LaTeX or an <see cref="Expr"/> to be a polynomial in one variable with rational coefficients and a degree of at most
/// <see cref="MaxDegree"/>: <c>x^2 - 5x + 6</c>, <c>x/2 + 1</c>, <c>(x+1)^2</c>, <c>7</c> and <c>0</c> are polynomials in x; <c>1/x</c>,
/// <c>sqrt(x)</c>, <c>x^(1/2)</c>, <c>x^-1</c> and <c>sin(x)</c> are not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order of checks; the first failure is reported:</b> parse (<see cref="MathValidationCode.Syntax"/>), shape (<see cref="MathValidationCode.WrongShape"/>:
/// an equation such as <c>x^2 = 4</c> is not a polynomial), other variables (<see cref="MathValidationCode.UnknownVariable"/>: <c>x*y</c> names <c>y</c>),
/// constructs that are not polynomial (<see cref="MathValidationCode.NotAPolynomial"/>, naming the smallest such part as written, for example <c>1/x</c>),
/// and the degree (<see cref="MathValidationCode.DegreeTooHigh"/>). Message placeholders: <c>{1}</c> is the construct or the degree, <c>{2}</c> the maximum.
/// </para>
/// <para>
/// <b>Reading.</b> The expression is brought to the canonical form of <see cref="Normalizer.Canonical(Expr, NormalizeOptions?)"/>, which folds exact arithmetic
/// but never expands, and read as <see cref="PolynomialConversion.TryToPolynomial"/> reads it: numbers, the variable, sums, products and powers with a
/// natural exponent. Coefficients must be rational, so <c>pi*x</c> is not a polynomial here, and neither is a constant power the canonical form leaves
/// unfolded because it is too large (<c>2^999999999*x</c>). Nothing else is evaluated.
/// </para>
/// <para>
/// <b>Degree.</b> When the degree as written is at most <see cref="MaxDegree"/> the expression is valid without expanding it; cancellation can only lower a degree.
/// Otherwise it is expanded to find the exact degree, so <c>(x+1)^30 - (x+1)^30 + x</c> has degree 1. An exponent above
/// <see cref="PolynomialConversion.MaxExponent"/> (64) on a base that contains the variable (<c>x^65</c>, <c>(x+1)^65</c>), and an expansion beyond degree 256
/// or about two million degree-times-coefficient-bits, are not attempted: those are reported as <see cref="MathValidationCode.DegreeTooHigh"/> with the
/// degree as written.
/// </para>
/// </remarks>
public sealed class PolynomialExpressionAttribute : MathValidationAttribute
{
    // TryToRationalFunction gives up at four times MaxExponent; the work bound keeps one expansion within milliseconds.
    private const long MaxExpandedDegree = 4 * PolynomialConversion.MaxExponent;
    private const long MaxExpansionWork = 2_000_000;

    private static readonly ParserOptions Options = new();

    private readonly Lazy<(string Name, string? Problem)> _variable;

    /// <summary>Creates the attribute for polynomials in <paramref name="variable"/>, a single variable name such as <c>"x"</c> or <c>"t"</c>.</summary>
    public PolynomialExpressionAttribute(string variable)
    {
        Variable = variable;
        _variable = new Lazy<(string, string?)>(() => MathExpressionAttribute.ResolveName(nameof(Variable), Variable, Format, Options, out var name) is { } problem ? (string.Empty, problem) : (name, null));
    }

    /// <summary>The variable of the polynomial, as given.</summary>
    public string Variable { get; }

    /// <summary>The highest allowed degree, between 0 and 64. Defaults to 20.</summary>
    public int MaxDegree { get; init; } = 20;

    /// <summary>The notation of text values. Defaults to <see cref="InputFormat.Text"/>; ignored for <see cref="Expr"/> values.</summary>
    public InputFormat Format { get; init; }

    internal override int ExtraPlaceholderCount => 2;

    internal override bool IsSupported(object value) => value is Expr;

    internal override string? ValidateOptions()
    {
        if (MaxDegree is < 0 or > PolynomialConversion.MaxExponent) return $"MaxDegree must be between 0 and {PolynomialConversion.MaxExponent} but is {MaxDegree}.";
        if (!Enum.IsDefined(Format)) return $"Format {(int)Format} is not a defined value.";
        return _variable.Value.Problem;
    }

    internal override MathDiagnostic? Evaluate(object value)
    {
        Expr raw;
        if (value is Expr typed)
        {
            raw = typed;
        }
        else
        {
            var text = (string)value;
            var parsed = Format == InputFormat.Latex ? LatexParser.Parse(text, Options) : Parser.Parse(text, Options);
            if (parsed.Expr is not { } tree)
            {
                var error = parsed.Errors[0];
                return new MathDiagnostic(MathValidationCode.Syntax, [error.Message], error.Span, error.Suggestion);
            }

            raw = tree;
        }

        var name = _variable.Value.Name;
        var (kind, found) = MathExpressionAttribute.ClassifyShape(raw);
        if (kind != ExpressionShape.Expression) return new MathDiagnostic(MathValidationCode.WrongShape, [$"a polynomial in {name}", found]);

        var variables = raw.FreeSymbols.Where(s => s.DeclaredSort is not FunctionSort).ToList();
        if (variables.Where(s => s.Name != name).Select(s => s.Name).Distinct().Order(StringComparer.Ordinal).ToArray() is { Length: > 0 } others)
        {
            return new MathDiagnostic(MathValidationCode.UnknownVariable, [string.Join(", ", others)]);
        }

        var x = variables.FirstOrDefault() ?? new Symbol(name);
        var canonical = Normalizer.Canonical(raw);
        if (Measure(canonical, x) is not { } size) return new MathDiagnostic(MathValidationCode.NotAPolynomial, [NameOffender(raw, x), MaxDegree]);
        if (!size.BeyondMaxExponent && size.Degree <= MaxDegree) return null;

        // Written above the maximum: only the expanded polynomial knows whether terms cancel.
        if (!size.BeyondMaxExponent && size.Degree <= MaxExpandedDegree && size.Degree * size.Bits <= MaxExpansionWork && PolynomialConversion.TryToPolynomial(canonical, x, out var polynomial))
        {
            var degree = Math.Max(polynomial.Degree, 0);
            return degree <= MaxDegree ? null : new MathDiagnostic(MathValidationCode.DegreeTooHigh, [degree, MaxDegree]);
        }

        return new MathDiagnostic(MathValidationCode.DegreeTooHigh, [size.Degree == long.MaxValue ? $"more than {long.MaxValue}" : size.Degree, MaxDegree]);
    }

    /// <summary>
    /// For a canonical tree in the grammar <see cref="PolynomialConversion.TryToPolynomial"/> reads, the degree as written, a bound on the bits of the
    /// coefficients of its expansion, and whether a power of the variable has an exponent above <see cref="PolynomialConversion.MaxExponent"/>; <c>null</c>
    /// for any other tree. Both numbers saturate at <see cref="long.MaxValue"/>.
    /// </summary>
    private static (long Degree, long Bits, bool BeyondMaxExponent)? Measure(Expr e, Symbol x)
    {
        static long Add(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
        static long Times(BigInteger k, long a) => a == 0 ? 0 : k > long.MaxValue / a ? long.MaxValue : (long)k * a;

        switch (e)
        {
            case Number { Value: var v }:
                return (0, v.Numerator.GetBitLength() + v.Denominator.GetBitLength(), false);
            case Symbol s when s.Equals(x):
                return (1, 1, false);
            case Apply { Operator.Id: "add" or "sub" or "mul" } a:
                {
                    var product = a.Operator.Id == "mul";
                    long degree = 0, bits = 0;
                    var beyond = false;
                    foreach (var argument in a.Arguments)
                    {
                        if (Measure(argument, x) is not { } m) return null;
                        degree = product ? Add(degree, m.Degree) : Math.Max(degree, m.Degree);
                        // A sum of rationals adds the bits of the denominators; a product also gains the bits of the number of products summed.
                        bits = Add(bits, product ? Add(m.Bits, 64 - long.LeadingZeroCount(m.Degree + 1)) : m.Bits);
                        beyond |= m.BeyondMaxExponent;
                    }

                    return (degree, bits, beyond);
                }
            case Apply { Operator.Id: "neg", Arguments: [var negated] }:
                return Measure(negated, x);
            case Apply { Operator.Id: "pow", Arguments: [var b, Number { Value: { IsInteger: true, Sign: >= 0 } k }] }:
                {
                    if (Measure(b, x) is not { } m) return null;
                    var exponent = k.Numerator;
                    if (m.Degree == 0) return exponent <= PolynomialConversion.MaxExponent ? (0, Times(exponent, m.Bits), m.BeyondMaxExponent) : null;
                    return (Times(exponent, m.Degree), Times(exponent, Add(m.Bits, 64 - long.LeadingZeroCount(m.Degree + 1))), m.BeyondMaxExponent || exponent > PolynomialConversion.MaxExponent);
                }
            default:
                return null;
        }
    }

    /// <summary>The smallest part of the expression as written that is not a polynomial piece, described for the message.</summary>
    private static string NameOffender(Expr raw, Symbol x)
    {
        // Descend while some part is itself not polynomial; symbols are never the culprit here (other variables were reported already).
        // A power is named whole (e^x, x^pi, x^(1/2)) unless its base contains the variable and fails on its own (sin(x)^2 names sin(x)).
        bool Fails(Expr part) => part is not Symbol && Measure(Normalizer.Canonical(part), x) is null;
        var node = raw;
        while ((node is Apply { Operator.Id: "pow", Arguments: [var b, _] }
            ? (b.FreeSymbols.Any(s => s.Name == x.Name) && Fails(b) ? b : null)
            : node.Children.FirstOrDefault(Fails)) is { } failing)
        {
            node = failing;
        }

        if (ExpressionText.TryDescribe(node, 60, out var text)) return text;
        return node switch
        {
            Apply { Operator.Id: "call", Arguments: [Symbol f, ..] } => $"{f.Name}(…)",
            Apply a => $"{a.Operator.Id}(…)",
            _ => $"a {node.Kind.ToString().ToLowerInvariant()}",
        };
    }
}
