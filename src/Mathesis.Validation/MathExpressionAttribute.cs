using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation;

/// <summary>
/// Requires text, LaTeX or an <see cref="Expr"/> to be a mathematical expression, and optionally of a given shape, over given variables and
/// free of given families of functions. The attribute only parses and inspects: it never simplifies, evaluates or solves, so even
/// <c>9^9^9</c> and <c>2^999999999*x</c> are valid and cost nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order of checks; the first failure is reported:</b> parse (<see cref="MathValidationCode.Syntax"/>, with the parser's span and suggestion),
/// parser warnings when <see cref="WarningsAreErrors"/> (<see cref="MathValidationCode.Ambiguous"/>, <see cref="MathValidationCode.UnknownFunction"/>),
/// sorts when <see cref="CheckSorts"/> (<see cref="MathValidationCode.IllSorted"/>), <see cref="Shape"/> (<see cref="MathValidationCode.WrongShape"/>),
/// <see cref="Variables"/> (<see cref="MathValidationCode.UnknownVariable"/>), <see cref="RequiredVariables"/> (<see cref="MathValidationCode.MissingVariable"/>) and
/// <see cref="DisallowedFamilies"/> (<see cref="MathValidationCode.DisallowedFunction"/>). A typed <see cref="Expr"/> skips the parse and the warnings.
/// Text beyond <see cref="MathValidationAttribute.MaxLength"/> is <see cref="MathValidationCode.TooLong"/> before it is parsed.
/// </para>
/// <para>
/// <b>Variables.</b> <see cref="Variables"/> and <see cref="RequiredVariables"/> name the free, non-function symbols. Constants (<c>e</c>, <c>pi</c>, <c>I</c>),
/// symbols bound by a sum, integral or quantifier, and function symbols such as <c>f</c> in <c>f(x)</c> never count. Names are read with the
/// same parser, so <c>"theta"</c> and <c>"θ"</c> are the same variable; a name that is not a variable (<c>"e"</c>, <c>"x+1"</c>) is a configuration error.
/// <c>Variables = []</c> allows no variable at all; <c>null</c> (the default) allows any.
/// </para>
/// <para>
/// <b>Names are read as the parser reads them, pinned by tests so nobody "fixes" them silently.</b> With the default
/// <see cref="SingleLetterVariables"/>, an unknown word is split into single letters and <c>oo</c> is infinity, so <c>foo(x)</c> is the product f·∞·x:
/// valid, and with <c>Variables = ["x"]</c> an <see cref="MathValidationCode.UnknownVariable"/> naming <c>f</c>. With
/// <c>SingleLetterVariables = false</c> it is a call of the function symbol <c>foo</c>, valid even with <c>Variables = ["x"]</c>.
/// <see cref="MathValidationCode.DisallowedFunction"/> means only an operator of a family in <see cref="DisallowedFamilies"/> (<c>sin(x)</c> with
/// <see cref="OperatorFamily.Trig"/>): an unknown name is not a function to this attribute. <c>sqr(x)</c> is a user function call; the parser warns
/// "Did you mean sqrt(x)?", and with <see cref="WarningsAreErrors"/> that is an <see cref="MathValidationCode.UnknownFunction"/>.
/// </para>
/// <para>
/// <b>Message placeholders:</b> <c>{1}</c> is the parser text, warning, sort-check text or names the code documents; <see cref="MathValidationCode.WrongShape"/>
/// also fills <c>{2}</c>, both as English phrases such as "an equation".
/// </para>
/// </remarks>
public class MathExpressionAttribute : MathValidationAttribute
{
    private readonly Lazy<ParserOptions> _parserOptions;
    private readonly Lazy<ResolvedNames> _names;

    /// <summary>Creates the attribute with no requirement beyond being an expression.</summary>
    public MathExpressionAttribute()
    {
        _parserOptions = new Lazy<ParserOptions>(() => new ParserOptions { SingleLetterVariables = SingleLetterVariables, LogMeansNatural = LogMeansNatural });
        _names = new Lazy<ResolvedNames>(ResolveNames);
    }

    /// <summary>The notation of text values. Defaults to <see cref="InputFormat.Text"/>; ignored for <see cref="Expr"/> values.</summary>
    public InputFormat Format { get; init; }

    /// <summary>The form the expression must have. Defaults to <see cref="ExpressionShape.Any"/>.</summary>
    public ExpressionShape Shape { get; init; }

    /// <summary>The only variables the expression may contain, or <c>null</c> for no restriction. See the remarks for what counts as a variable.</summary>
    public string[]? Variables { get; init; }

    /// <summary>Variables the expression must contain, or <c>null</c> for none. Must be allowed by <see cref="Variables"/> when that is set.</summary>
    public string[]? RequiredVariables { get; init; }

    /// <summary>
    /// Families of operators the expression must not use, for example <see cref="OperatorFamily.Trig"/> | <see cref="OperatorFamily.Hyperbolic"/>.
    /// Defaults to none. This is the only restriction on functions.
    /// </summary>
    public OperatorFamily DisallowedFamilies { get; init; }

    /// <summary>Whether a parser warning (an ambiguous implicit product, an unknown function name) fails validation. Defaults to <c>false</c>.</summary>
    public bool WarningsAreErrors { get; init; }

    /// <summary>Whether the expression must be well sorted (no matrix of the wrong size added to a number, and so on). Defaults to <c>false</c>.</summary>
    public bool CheckSorts { get; init; }

    /// <summary>
    /// The parser option of the same name: with the default <c>true</c>, a word that is not a known function or constant is split into single-letter
    /// variables (<c>xy</c> is x·y); with <c>false</c> it stays one symbol (<c>speed</c>), and <c>speed(t)</c> is a function call.
    /// </summary>
    public bool SingleLetterVariables { get; init; } = true;

    /// <summary>The parser option of the same name: read a bare <c>log x</c> as the natural logarithm. Defaults to <c>false</c> (base 10).</summary>
    public bool LogMeansNatural { get; init; }

    internal override int ExtraPlaceholderCount => 2;

    internal override bool IsSupported(object value) => value is Expr;

    internal override string? ValidateOptions()
    {
        if (!Enum.IsDefined(Format)) return $"Format {(int)Format} is not a defined value.";
        if (!Enum.IsDefined(Shape)) return $"Shape {(int)Shape} is not a defined value.";
        return _names.Value.Problem;
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
            var text = (string)value;
            var options = _parserOptions.Value;
            var parsed = Format == InputFormat.Latex ? LatexParser.Parse(text, options) : Parser.Parse(text, options);
            if (parsed.Expr is not { } tree)
            {
                var error = parsed.Errors[0];
                return new MathDiagnostic(MathValidationCode.Syntax, [error.Message], error.Span, error.Suggestion);
            }

            if (WarningsAreErrors && !parsed.Warnings.IsDefaultOrEmpty)
            {
                var warning = parsed.Warnings[0];
                return new MathDiagnostic(warning.Code == "UnknownFunction" ? MathValidationCode.UnknownFunction : MathValidationCode.Ambiguous, [warning.Message], warning.Span);
            }

            expr = tree;
        }

        if (CheckSorts && SortChecker.Check(expr) is { IsValid: false } sorts)
        {
            return new MathDiagnostic(MathValidationCode.IllSorted, [sorts.Issues[0].Error.Message]);
        }

        if (Shape != ExpressionShape.Any)
        {
            var (kind, found) = ClassifyShape(expr);
            if (kind != Shape)
            {
                var expected = Shape switch
                {
                    ExpressionShape.Equation => "an equation",
                    ExpressionShape.Inequality => "an inequality",
                    ExpressionShape.Interval => "an interval",
                    _ => "an expression",
                };
                // A pair (a, b) is a tuple; the open interval is written with reversed brackets.
                var suggestion = Shape == ExpressionShape.Interval && expr is TupleLiteral { Elements: [var from, var to] } ? $"For an open interval write ]{from}, {to}[." : null;
                return new MathDiagnostic(MathValidationCode.WrongShape, [expected, found], Suggestion: suggestion);
            }
        }

        var names = _names.Value;
        if (names.Allowed is not null || names.Required is not null)
        {
            var present = expr.FreeSymbols.Where(s => s.DeclaredSort is not FunctionSort).Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
            if (names.Allowed is { } allowed && present.Where(n => !allowed.Contains(n)).Order(StringComparer.Ordinal).ToArray() is { Length: > 0 } unknown)
            {
                return new MathDiagnostic(MathValidationCode.UnknownVariable, [string.Join(", ", unknown)]);
            }

            if (names.Required is { } required && required.Where(n => !present.Contains(n)).Order(StringComparer.Ordinal).ToArray() is { Length: > 0 } missing)
            {
                return new MathDiagnostic(MathValidationCode.MissingVariable, [string.Join(", ", missing)]);
            }
        }

        if ((expr.Families & DisallowedFamilies) != 0)
        {
            var used = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (node, _) in expr.Walk())
            {
                switch (node)
                {
                    case Apply a when (a.Operator.Family & DisallowedFamilies) != 0:
                        // A user function is named by its symbol, any other operator by its id.
                        used.Add(a.Operator.Id == "call" && a.Arguments[0] is Symbol head ? head.Name : a.Operator.Id);
                        break;
                    case Bind b when (b.Binder.Family() & DisallowedFamilies) != 0:
                        used.Add(b.Binder.ToString().ToLowerInvariant());
                        break;
                    case MatrixLiteral when (DisallowedFamilies & OperatorFamily.LinearAlgebra) != 0:
                        used.Add("matrix");
                        break;
                    case SetLiteral when (DisallowedFamilies & OperatorFamily.Set) != 0:
                        used.Add("set");
                        break;
                    case IntervalLiteral when (DisallowedFamilies & OperatorFamily.Set) != 0:
                        used.Add("interval");
                        break;
                }
            }

            if (used.Count > 0) return new MathDiagnostic(MathValidationCode.DisallowedFunction, [string.Join(", ", used)]);
        }

        return null;
    }

    /// <summary>
    /// The shape of <paramref name="expr"/> and an English phrase for it. <see cref="ExpressionShape.Any"/> stands for a relation or logical statement that is
    /// not an equation, an inequality or an interval, so it matches no requirement.
    /// </summary>
    internal static (ExpressionShape Kind, string Phrase) ClassifyShape(Expr expr) => expr switch
    {
        IntervalLiteral => (ExpressionShape.Interval, "an interval"),
        Apply { Operator.Id: "eq", Arguments.Length: 2 } => (ExpressionShape.Equation, "an equation"),
        Apply { Operator.Id: "ne" or "lt" or "le" or "gt" or "ge", Arguments.Length: 2 } => (ExpressionShape.Inequality, "an inequality"),
        Apply { Operator.Family: OperatorFamily.Relation or OperatorFamily.Logic } or Bind { Binder: Binder.ForAll or Binder.Exists or Binder.ExistsUnique } or Constant { Id: ConstantId.True or ConstantId.False }
            => (ExpressionShape.Any, "a statement"),
        _ => (ExpressionShape.Expression, "an expression"),
    };

    /// <summary>
    /// Reads a configured variable name the way input is read, so "theta" and "θ" are one symbol while "e" and "x+1" are not variables.
    /// Returns the problem for <paramref name="property"/>, or <c>null</c> with the symbol's name in <paramref name="resolved"/>.
    /// </summary>
    internal static string? ResolveName(string property, string? name, InputFormat format, ParserOptions options, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return $"{property} contains an empty name.";
        var parsed = format == InputFormat.Latex ? LatexParser.Parse(name, options) : Parser.Parse(name, options);
        if (parsed.Expr is not Symbol { DeclaredSort: not FunctionSort } symbol)
        {
            return $"{property} contains '{name}', which is not a variable name{(parsed.Expr is Constant ? " (it is a constant)" : string.Empty)}.";
        }

        resolved = symbol.Name;
        return null;
    }

    /// <summary>The configured variable names as the parser reads them, and the first problem with them.</summary>
    private sealed record ResolvedNames(HashSet<string>? Allowed, HashSet<string>? Required, string? Problem);

    private ResolvedNames ResolveNames()
    {
        var options = _parserOptions.Value;

        string? Resolve(string property, string[] names, out HashSet<string> resolved)
        {
            resolved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (ResolveName(property, name, Format, options, out var symbolName) is { } problem) return problem;
                resolved.Add(symbolName);
            }

            return null;
        }

        HashSet<string>? allowed = null, required = null;
        if (Variables is { } variables && Resolve(nameof(Variables), variables, out allowed) is { } allowedProblem) return new ResolvedNames(null, null, allowedProblem);
        if (RequiredVariables is { } requiredNames && Resolve(nameof(RequiredVariables), requiredNames, out required) is { } requiredProblem) return new ResolvedNames(null, null, requiredProblem);
        if (allowed is not null && required is not null && required.FirstOrDefault(n => !allowed.Contains(n)) is { } notAllowed)
        {
            return new ResolvedNames(null, null, $"RequiredVariables contains '{notAllowed}', which Variables does not allow.");
        }

        return new ResolvedNames(allowed, required, null);
    }
}
