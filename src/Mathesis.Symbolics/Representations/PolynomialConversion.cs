using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Symbolics.Canonical;

namespace Mathesis.Symbolics.Representations;

/// <summary>
/// Conversions between expressions and the polynomial types of <c>Mathesis.Core</c> (docs/design/03-namespaces-and-packages.md,
/// <c>Mathesis.Symbolics.Representations</c>): univariate polynomials and rational functions over ℚ, multivariate polynomials over ℚ
/// whose variables are symbols or other subexpressions, and the coefficients of an expression in one symbol.
/// </summary>
public static class PolynomialConversion
{
    /// <summary>The largest exponent that is expanded (a guard against enormous powers).</summary>
    public const int MaxExponent = 64;

    // ----- Univariate over ℚ -----

    /// <summary>Reads <paramref name="e"/> as a polynomial in <paramref name="x"/> with rational coefficients (no other symbols).</summary>
    public static bool TryToPolynomial(Expr e, Symbol x, out Polynomial<BigRational> polynomial)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(x);
        var result = ToPolynomial(e, x);
        polynomial = result ?? Polynomial<BigRational>.Zero;
        return result is not null;
    }

    private static Polynomial<BigRational>? ToPolynomial(Expr e, Symbol x)
    {
        switch (e)
        {
            case Number n:
                return new Polynomial<BigRational>([n.Value]);
            case Symbol s when s.Equals(x):
                return Polynomial<BigRational>.X;
            case Apply a:
                var args = a.Arguments;
                switch (a.Operator.Id)
                {
                    case "add":
                    case "mul":
                        {
                            Polynomial<BigRational>? acc = null;
                            foreach (var arg in args)
                            {
                                if (ToPolynomial(arg, x) is not { } p) return null;
                                acc = acc is null ? p : a.Operator == Operators.Add ? acc + p : acc * p;
                            }
                            return acc;
                        }
                    case "neg":
                        return ToPolynomial(args[0], x) is { } negated ? -negated : null;
                    case "sub":
                        return ToPolynomial(args[0], x) is { } l && ToPolynomial(args[1], x) is { } r ? l - r : null;
                    case "pow":
                        return args[1] is Number { Value: { IsInteger: true, Sign: >= 0 } k } && k <= MaxExponent && ToPolynomial(args[0], x) is { } b ? b.Pow((int)k.Numerator) : null;
                    default:
                        return null;
                }
            default:
                return null;
        }
    }

    /// <summary>Writes a polynomial as a sum of terms in <paramref name="x"/>, highest degree first (not normalized).</summary>
    public static Expr FromPolynomial(Polynomial<BigRational> p, Symbol x)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(x);
        if (p.IsZero) return new Number(BigRational.Zero);
        var terms = new List<Expr>();
        for (var k = p.Degree; k >= 0; k--)
        {
            var c = p[k];
            if (c == BigRational.Zero) continue;
            Expr power = k == 0 ? new Number(BigRational.One) : k == 1 ? x : new Apply(Operators.Pow, [x, new Number(k)]);
            terms.Add(k == 0 ? new Number(c) : c == BigRational.One ? power : new Apply(Operators.Mul, [new Number(c), power]));
        }
        return terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]);
    }

    /// <summary>Reads <paramref name="e"/> as a quotient of polynomials in <paramref name="x"/> (neither reduced): sums, products, quotients and integer powers.</summary>
    public static bool TryToRationalFunction(Expr e, Symbol x, out Polynomial<BigRational> numerator, out Polynomial<BigRational> denominator)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(x);
        var r = ToRational(e, x);
        numerator = r?.N ?? Polynomial<BigRational>.Zero;
        denominator = r?.D ?? Polynomial<BigRational>.One;
        return r is not null;
    }

    private static (Polynomial<BigRational> N, Polynomial<BigRational> D)? ToRational(Expr e, Symbol x)
    {
        switch (e)
        {
            case Number n:
                return (new Polynomial<BigRational>([n.Value]), Polynomial<BigRational>.One);
            case Symbol s when s.Equals(x):
                return (Polynomial<BigRational>.X, Polynomial<BigRational>.One);
            case Apply a:
                var args = a.Arguments;
                if (a.Operator == Operators.Pow)
                {
                    if (args[1] is not Number { Value: { IsInteger: true } k } || BigRational.Abs(k) > MaxExponent || ToRational(args[0], x) is not { } b) return null;
                    var count = (int)BigRational.Abs(k).Numerator;
                    if (k.Sign >= 0) return (b.N.Pow(count), b.D.Pow(count));
                    return b.N.IsZero ? null : (b.D.Pow(count), b.N.Pow(count));
                }
                var parts = new List<(Polynomial<BigRational> N, Polynomial<BigRational> D)>();
                foreach (var arg in args)
                {
                    if (ToRational(arg, x) is not { } f) return null;
                    parts.Add(f);
                }
                if (a.Operator == Operators.Neg) return (-parts[0].N, parts[0].D);
                if (a.Operator == Operators.Sub) return (parts[0].N * parts[1].D - parts[1].N * parts[0].D, parts[0].D * parts[1].D);
                if (a.Operator == Operators.Div) return parts[1].N.IsZero ? null : (parts[0].N * parts[1].D, parts[0].D * parts[1].N);
                if (a.Operator != Operators.Add && a.Operator != Operators.Mul) return null;
                var (num, den) = parts[0];
                foreach (var (pn, pd) in parts.Skip(1))
                {
                    if (a.Operator == Operators.Add)
                    {
                        num = num * pd + pn * den;
                        den *= pd;
                    }
                    else
                    {
                        num *= pn;
                        den *= pd;
                    }
                }
                return Math.Max(num.Degree, den.Degree) > 4 * MaxExponent ? null : (num, den);
            default:
                return null;
        }
    }

    // ----- Multivariate over ℚ -----

    /// <summary>
    /// Reads <paramref name="e"/> as a polynomial over ℚ in variables that are symbols or other (non-polynomial) subexpressions such as
    /// <c>sin(x)</c>. Sums, products, negation, subtraction and powers with a non-negative integer exponent are expanded.
    /// </summary>
    /// <param name="e">The expression.</param>
    /// <param name="variables">The variables found, in order of appearance (the position is the variable's index).</param>
    /// <param name="polynomial">The polynomial.</param>
    public static bool TryToSparse(Expr e, out ImmutableArray<Expr> variables, out SparsePolynomial<BigRational> polynomial)
    {
        ArgumentNullException.ThrowIfNull(e);
        var atoms = new List<Expr>();
        Collect(e, atoms);
        variables = [.. atoms];
        var result = ToSparse(e, atoms);
        polynomial = result ?? SparsePolynomial<BigRational>.Zero(atoms.Count);
        return result is not null;
    }

    private static void Collect(Expr e, List<Expr> atoms)
    {
        switch (e)
        {
            case Number:
                return;
            case Apply a when a.Operator == Operators.Add || a.Operator == Operators.Mul || a.Operator == Operators.Neg || a.Operator == Operators.Sub:
                foreach (var arg in a.Arguments) Collect(arg, atoms);
                return;
            case Apply { Operator.Id: "pow" } p when p.Arguments[1] is Number { Value: { IsInteger: true, Sign: >= 0 } k } && k <= MaxExponent:
                Collect(p.Arguments[0], atoms);
                return;
            default:
                if (!atoms.Contains(e)) atoms.Add(e);
                return;
        }
    }

    private static SparsePolynomial<BigRational>? ToSparse(Expr e, List<Expr> atoms)
    {
        var n = atoms.Count;
        switch (e)
        {
            case Number num:
                return SparsePolynomial<BigRational>.Constant(num.Value, n);
            case Apply a when a.Operator == Operators.Add || a.Operator == Operators.Mul:
                {
                    SparsePolynomial<BigRational>? acc = null;
                    foreach (var arg in a.Arguments)
                    {
                        if (ToSparse(arg, atoms) is not { } p) return null;
                        acc = acc is null ? p : a.Operator == Operators.Add ? acc + p : acc * p;
                    }
                    return acc;
                }
            case Apply { Operator.Id: "neg" } neg:
                return ToSparse(neg.Arguments[0], atoms) is { } negated ? -negated : null;
            case Apply { Operator.Id: "sub" } sub:
                return ToSparse(sub.Arguments[0], atoms) is { } l && ToSparse(sub.Arguments[1], atoms) is { } r ? l - r : null;
            case Apply { Operator.Id: "pow" } pow when pow.Arguments[1] is Number { Value: { IsInteger: true, Sign: >= 0 } k } && k <= MaxExponent:
                return ToSparse(pow.Arguments[0], atoms) is { } b ? b.Pow((int)k.Numerator) : null;
            default:
                var i = atoms.IndexOf(e);
                return i < 0 ? null : SparsePolynomial<BigRational>.Variable(i, n);
        }
    }

    /// <summary>Writes a multivariate polynomial as a sum of monomials in <paramref name="variables"/> (not normalized).</summary>
    public static Expr FromSparse(SparsePolynomial<BigRational> p, ImmutableArray<Expr> variables)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.IsZero) return new Number(BigRational.Zero);
        var terms = new List<Expr>();
        foreach (var (monomial, coefficient) in p.Terms)
        {
            var factors = new List<Expr>();
            if (coefficient != BigRational.One || monomial.TotalDegree == 0) factors.Add(new Number(coefficient));
            for (var i = 0; i < variables.Length; i++)
            {
                var e = i < monomial.Exponents.Length ? monomial.Exponents[i] : 0;
                if (e == 0) continue;
                factors.Add(e == 1 ? variables[i] : new Apply(Operators.Pow, [variables[i], new Number(e)]));
            }
            terms.Add(factors.Count == 1 ? factors[0] : new Apply(Operators.Mul, [.. factors]));
        }
        return terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]);
    }

    // ----- Coefficients in one symbol (symbolic coefficients allowed) -----

    /// <summary>
    /// The coefficients of <paramref name="e"/> as a polynomial in <paramref name="x"/>: a map from the exponent to the coefficient expression,
    /// which does not contain <paramref name="x"/>. Products and integer powers of sums are expanded first.
    /// </summary>
    /// <returns><c>null</c> when <paramref name="e"/> is not a polynomial in <paramref name="x"/> (for example <c>sin(x)</c> or <c>x^y</c>).</returns>
    public static IReadOnlyDictionary<int, Expr>? Coefficients(Expr e, Symbol x)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(x);
        var result = new SortedDictionary<int, List<Expr>>();
        if (!TryToSparse(e, out var variables, out var p)) return null;
        var xIndex = variables.IndexOf(x);
        foreach (var (monomial, coefficient) in p.Terms)
        {
            var degree = xIndex >= 0 && xIndex < monomial.Exponents.Length ? monomial.Exponents[xIndex] : 0;
            var factors = new List<Expr>();
            if (coefficient != BigRational.One || monomial.TotalDegree - degree == 0) factors.Add(new Number(coefficient));
            for (var i = 0; i < variables.Length; i++)
            {
                if (i == xIndex) continue;
                var exponent = i < monomial.Exponents.Length ? monomial.Exponents[i] : 0;
                if (exponent == 0) continue;
                if (variables[i].FreeSymbols.Contains(x)) return null;
                factors.Add(exponent == 1 ? variables[i] : new Apply(Operators.Pow, [variables[i], new Number(exponent)]));
            }
            if (!result.TryGetValue(degree, out var list)) result[degree] = list = [];
            list.Add(factors.Count == 1 ? factors[0] : new Apply(Operators.Mul, [.. factors]));
        }
        foreach (var variable in variables)
        {
            if (!variable.Equals(x) && variable.FreeSymbols.Contains(x)) return null;
        }
        return result.ToDictionary(kv => kv.Key, kv => Normalizer.Canonical(kv.Value.Count == 1 ? kv.Value[0] : new Apply(Operators.Add, [.. kv.Value])));
    }
}
