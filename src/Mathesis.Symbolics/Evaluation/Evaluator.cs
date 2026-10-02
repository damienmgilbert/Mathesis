using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics.Canonical;

namespace Mathesis.Symbolics.Evaluation;

/// <summary>
/// Evaluation of expressions: exact (<see cref="Evaluate"/>), to <see cref="double"/> (<see cref="N"/>) and compiled
/// (<see cref="Compile{T}"/>); docs/design/07-engines.md, "Evaluation and compilation".
/// </summary>
public static class Evaluator
{
    /// <summary>The largest argument of <c>n!</c>, <c>n!!</c>, <c>binomial</c> and <c>perm</c> that <see cref="Evaluate"/> computes exactly.</summary>
    public const int MaxExactFactorialArgument = 20_000;

    /// <summary>
    /// Exact evaluation: substitutes <paramref name="bindings"/>, normalizes to the Canonical level and computes the integer
    /// functions (<c>n!</c>, <c>n!!</c>, <c>binomial</c>, <c>perm</c>, <c>gcd</c>, <c>lcm</c>, <c>mod</c>, <c>quo</c>, <c>floor</c>,
    /// <c>ceil</c>, <c>round</c>, <c>frac</c>, <c>abs</c>, <c>sign</c>, <c>min</c>, <c>max</c>) of exact arguments.
    /// Rationals stay exact; constants such as π stay symbolic; a function applied to a <see cref="Float"/> is computed in
    /// double precision. Special values such as <c>sin(π/6)</c> need the knowledge catalog and are not evaluated here.
    /// </summary>
    /// <param name="expr">The expression.</param>
    /// <param name="bindings">Values for free symbols. They are substituted one after another, so a value should not mention another bound symbol.</param>
    /// <param name="options">Normalization options (the number field).</param>
    /// <param name="budget">Limits the work; <see cref="Budget.Unlimited"/> when <c>null</c>.</param>
    public static Outcome<Expr> Evaluate(Expr expr, IReadOnlyDictionary<Symbol, Expr>? bindings = null, NormalizeOptions? options = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(expr);
        budget ??= Budget.Unlimited;
        if (bindings is not null)
        {
            foreach (var (symbol, value) in bindings) expr = expr.Substitute(symbol, value);
        }

        var exceeded = false;
        var result = Normalizer.Canonical(expr, options);
        result = result.Transform(node =>
        {
            if (exceeded || node is not Apply a) return null;
            if (!budget.TryCharge() || !budget.AllowsSize(node.LeafCount))
            {
                exceeded = true;
                return null;
            }
            return (Expr?)FoldFloat(a) ?? FoldExact(a, budget);
        });
        if (exceeded) return new Outcome<Expr>.Unevaluated(expr, budget.ExceededReason ?? "The expression is larger than the budget allows.");
        return Outcome.Ok(Normalizer.Canonical(result, options));
    }

    // f(Float, …) with only numeric operands is computed in double precision.
    private static Float? FoldFloat(Apply a)
    {
        var hasFloat = false;
        foreach (var arg in a.Arguments)
        {
            if (arg is Float) hasFloat = true;
            else if (arg is not (Number or Constant)) return null;
        }
        if (!hasFloat || a.Operator.Id is "add" or "mul" or "neg" or "sub" or "div") return null;
        var compiled = ExprCompiler<double>.Compile(a, []);
        if (compiled is not Outcome<CompiledExpr<double>>.Success { Value: var code }) return null;
        var value = code.Invoke([]);
        return double.IsFinite(value) ? new Float(value) : null;
    }

    private static Number? FoldExact(Apply a, Budget budget)
    {
        var args = a.Arguments;
        foreach (var arg in args)
        {
            if (arg is not Number) return null;
        }
        var v = args.Select(x => ((Number)x).Value).ToArray();
        switch (a.Operator.Id)
        {
            case "abs": return new Number(BigRational.Abs(v[0]));
            case "sign": return new Number(v[0].Sign);
            case "floor": return new Number(new BigRational(Floor(v[0])));
            case "ceil": return new Number(new BigRational(-Floor(-v[0])));
            case "round" when v.Length == 1:
                {
                    // conv.rounding: half away from zero.
                    var magnitude = Floor(BigRational.Abs(v[0]) + BigRational.Create(1, 2));
                    return new Number(new BigRational(v[0].Sign < 0 ? -magnitude : magnitude));
                }
            case "frac": return new Number(v[0] - new BigRational(Floor(v[0])));
            case "max": return new Number(v.Max());
            case "min": return new Number(v.Min());
            case "mod":
                if (v[1] == BigRational.Zero) return null;
                return new Number(v[0] - v[1] * new BigRational(Floor(v[0] / v[1])));
            case "quo":
                if (v[1] == BigRational.Zero) return null;
                return new Number(new BigRational(Floor(v[0] / v[1])));
            case "gcd" when v.All(x => x.IsInteger): return new Number(new BigRational(v.Select(x => x.Numerator).Aggregate(BigInteger.GreatestCommonDivisor)));
            case "lcm" when v.All(x => x.IsInteger):
                return new Number(new BigRational(v.Select(x => BigInteger.Abs(x.Numerator)).Aggregate((p, q) => p.IsZero || q.IsZero ? BigInteger.Zero : p / BigInteger.GreatestCommonDivisor(p, q) * q)));
            case "factorial" when IsSmallNatural(v[0], out var n) && n <= MaxExactFactorialArgument && budget.TryCharge(n):
                return new Number(new BigRational(Product(1, n)));
            case "factorial2" when IsSmallNatural(v[0], out var m) && m <= MaxExactFactorialArgument && budget.TryCharge(m):
                return new Number(new BigRational(Product(m % 2 == 0 ? 2 : 1, m, 2)));
            case "binomial" when v[0].IsInteger && v[1].IsInteger && IsSmallNatural(v[1], out var k) && k <= MaxExactFactorialArgument && budget.TryCharge(k):
                {
                    // binomial(n, k) for any integer n: n(n−1)…(n−k+1)/k! (negative n included).
                    var top = v[0].Numerator;
                    var result = BigInteger.One;
                    for (var i = 0; i < k; i++) result = result * (top - i) / (i + 1);
                    return new Number(new BigRational(result));
                }
            case "perm" when IsSmallNatural(v[0], out var pn) && IsSmallNatural(v[1], out var pk) && pn <= MaxExactFactorialArgument && budget.TryCharge(pk):
                return new Number(new BigRational(pk > pn ? BigInteger.Zero : Product(pn - pk + 1, pn)));
            default: return null;
        }
    }

    private static bool IsSmallNatural(BigRational x, out int n)
    {
        n = 0;
        if (!x.IsInteger || x.Sign < 0 || x.Numerator > int.MaxValue) return false;
        n = (int)x.Numerator;
        return true;
    }

    private static BigInteger Product(int from, int to, int step = 1)
    {
        var result = BigInteger.One;
        for (var i = from; i <= to; i += step) result *= i;
        return result;
    }

    private static BigInteger Floor(BigRational x) => BigInteger.Divide(x.Numerator - (x.Numerator.Sign < 0 ? x.Denominator - 1 : 0), x.Denominator);

    /// <summary>Compiles <paramref name="expr"/> to an instruction array over <paramref name="parameters"/>.</summary>
    /// <typeparam name="T">
    /// <see cref="double"/>, <c>Complex&lt;double&gt;</c>, <c>Interval&lt;double&gt;</c> or <c>Dual&lt;double&gt;</c>. Real mode semantics apply:
    /// <c>x^(p/q)</c> with odd <c>q</c> is the real root; the complex type uses principal values.
    /// </typeparam>
    /// <param name="expr">The expression.</param>
    /// <param name="parameters">The free symbols, in the order of the arguments of <see cref="CompiledExpr{T}.Invoke(ReadOnlySpan{T})"/>.</param>
    /// <returns>Failed (<see cref="MathErrorKind.Unsupported"/>) when the expression contains a symbol that is not a parameter or an operator that cannot be compiled.</returns>
    /// <exception cref="ArgumentException">A parameter is repeated.</exception>
    public static Outcome<CompiledExpr<T>> Compile<T>(this Expr expr, params Symbol[] parameters)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Distinct().Count() != parameters.Length) throw new ArgumentException("The parameters must be distinct.", nameof(parameters));
        return ExprCompiler<T>.Compile(expr, [.. parameters]);
    }

    /// <summary>
    /// Evaluates to a <see cref="double"/> in real mode.
    /// </summary>
    /// <param name="expr">The expression.</param>
    /// <param name="values">Values of the free symbols.</param>
    /// <param name="digits">Significant digits of the result, 1 to 15 (BigFloat precision beyond 15 digits arrives in Milestone 7).</param>
    /// <returns>
    /// Failed when the value is not a real number (a domain error such as <c>sqrt(-1)</c>) or an operator cannot be compiled;
    /// Unevaluated when a free symbol has no value or <paramref name="digits"/> is beyond 15.
    /// </returns>
    public static Outcome<double> N(Expr expr, IReadOnlyDictionary<Symbol, double>? values = null, int digits = 15)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 1);
        if (digits > 15) return new Outcome<double>.Unevaluated(expr, "More than 15 digits needs BigFloat (Milestone 7).");
        var parameters = expr.FreeSymbols.OrderBy(s => s.Name, StringComparer.Ordinal).ToImmutableArray();
        foreach (var p in parameters)
        {
            if (values is null || !values.ContainsKey(p)) return new Outcome<double>.Unevaluated(expr, $"The symbol '{p.Name}' has no value.");
        }
        var compiled = ExprCompiler<double>.Compile(expr, parameters);
        if (compiled is not Outcome<CompiledExpr<double>>.Success { Value: var code }) return CarryFailure<CompiledExpr<double>, double>(compiled, expr);
        var result = code.Invoke([.. parameters.Select(p => values![p])]);
        if (double.IsNaN(result)) return Outcome.Fail<double>(new MathError(MathErrorKind.Domain, "The value is not a real number.", expr));
        if (digits < 15 && double.IsFinite(result) && result != 0) result = double.Parse(result.ToString("E" + (digits - 1), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return Outcome.Ok(result);
    }

    /// <summary>Evaluates to a <c>Complex&lt;double&gt;</c> (principal values; <c>I</c> is the imaginary unit).</summary>
    /// <param name="expr">The expression.</param>
    /// <param name="values">Values of the free symbols.</param>
    public static Outcome<Complex<double>> NComplex(Expr expr, IReadOnlyDictionary<Symbol, Complex<double>>? values = null)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var parameters = expr.FreeSymbols.OrderBy(s => s.Name, StringComparer.Ordinal).ToImmutableArray();
        foreach (var p in parameters)
        {
            if (values is null || !values.ContainsKey(p)) return new Outcome<Complex<double>>.Unevaluated(expr, $"The symbol '{p.Name}' has no value.");
        }
        var compiled = ExprCompiler<Complex<double>>.Compile(expr, parameters);
        if (compiled is not Outcome<CompiledExpr<Complex<double>>>.Success { Value: var code }) return CarryFailure<CompiledExpr<Complex<double>>, Complex<double>>(compiled, expr);
        var result = code.Invoke([.. parameters.Select(p => values![p])]);
        if (double.IsNaN(result.Real) || double.IsNaN(result.Imaginary)) return Outcome.Fail<Complex<double>>(new MathError(MathErrorKind.Domain, "The value is undefined.", expr));
        return Outcome.Ok(result);
    }

    private static Outcome<TOut> CarryFailure<TIn, TOut>(Outcome<TIn> failed, Expr expr) =>
        failed is Outcome<TIn>.Failed f ? Outcome.Fail<TOut>(f.Error) : new Outcome<TOut>.Unevaluated(expr, "The expression could not be compiled.");
}
