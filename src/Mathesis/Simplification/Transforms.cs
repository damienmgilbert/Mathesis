using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Simplification;

/// <summary>A named rewriting goal: a strategy over the catalog's rule sets and algorithms (docs/design/07-engines.md, "Simplification").</summary>
/// <param name="Name">The transform name, such as <c>Expand</c>.</param>
/// <param name="Strategy">The strategy that performs it.</param>
public sealed record Transform(string Name, Strategy Strategy)
{
    /// <summary>Runs the transform on <paramref name="expr"/>; Unevaluated when it changes nothing.</summary>
    public Outcome<Expr> Run(Expr expr, RewriteContext? context = null) => RewriteEngine.Run(Strategy, expr, context);
}

/// <summary>The named transforms. Each one cites catalog entries in its steps; none contains mathematics of its own.</summary>
public static class Transforms
{
    private static readonly RuleLibrary Library = RuleLibrary.Default;

    private static Strategy Rules(string name, params string[] tags) => Strategies.Innermost(Strategies.Apply(Library.Union(name, tags)));

    /// <summary>The algorithms by name (the part after <c>algorithm:</c> in a step's rule name), so recorded steps can be replayed.</summary>
    public static IReadOnlyDictionary<string, AlgorithmFunction> Algorithms { get; } = new Dictionary<string, AlgorithmFunction>(StringComparer.Ordinal)
    {
        ["expand-power"] = ExpandPower,
        ["factor-rational-roots"] = FactorRationalRoots,
        ["cancel"] = CancelAlgorithm,
        ["together"] = TogetherTerms,
        ["apart"] = ApartAlgorithm,
        ["complete-square"] = CompleteSquareAlgorithm,
    };

    private static Strategy Algorithm(string name) => Strategies.Algorithm(name, Algorithms[name]);

    /// <summary>Multiplies products over sums and powers of sums out.</summary>
    public static Transform Expand { get; } = new("Expand", Strategies.Fixpoint(Strategies.BottomUp(Strategies.Choice(Strategies.Apply(Library.Union("expand", "expand", "distribute-negation")), Algorithm("expand-power")))));

    /// <summary>Writes a polynomial as a product: common factors, special products and rational roots.</summary>
    public static Transform Factor { get; } = new("Factor", Strategies.Fixpoint(Strategies.BottomUp(Strategies.Choice(Strategies.Apply(Library["factor"]), Algorithm("factor-rational-roots")))));

    /// <summary>Divides out the common polynomial factor of a numerator and denominator.</summary>
    public static Transform Cancel { get; } = new("Cancel", Strategies.Fixpoint(Strategies.BottomUp(Strategies.Choice(Strategies.Apply(Library["fraction-simplify"]), Algorithm("cancel")))));

    /// <summary>Combines a sum of fractions over a common denominator.</summary>
    public static Transform Together { get; } = new("Together", Strategies.Fixpoint(Strategies.BottomUp(Algorithm("together"))));

    /// <summary>Splits a rational function with rational poles into partial fractions.</summary>
    public static Transform Apart { get; } = new("Apart", Strategies.Fixpoint(Strategies.TopDown(Algorithm("apart"))));

    /// <summary>Removes radicals from denominators.</summary>
    public static Transform Rationalize { get; } = new("Rationalize", Rules("rationalize", "rationalize"));

    /// <summary>Combines powers, simplifies radicals and absolute values.</summary>
    public static Transform PowerSimplify { get; } = new("PowerSimplify", Rules("power-simplify", "power-simplify", "combine-powers", "radical-simplify", "radical-combine", "abs-simplify", "sign-simplify", "negative-exponent"));

    /// <summary>Writes sums and differences of logarithms as one logarithm.</summary>
    public static Transform LogCombine { get; } = new("LogCombine", Rules("combine-log", "combine-log", "log-simplify"));

    /// <summary>Splits logarithms of products, quotients and powers.</summary>
    public static Transform LogExpand { get; } = new("LogExpand", Rules("expand-log", "expand-log", "log-simplify"));

    /// <summary>Reduces trigonometric expressions with the Pythagorean, parity, period and shift identities.</summary>
    public static Transform TrigSimplify { get; } = new("TrigSimplify", Rules("trig-simplify", "trig-reduce"));

    /// <summary>Expands sums, multiples and double angles of trigonometric functions.</summary>
    public static Transform TrigExpand { get; } = new("TrigExpand", Rules("trig-expand", "trig-expand"));

    /// <summary>Reduces powers and products of trigonometric functions to sums of multiple angles.</summary>
    public static Transform TrigReduce { get; } = new("TrigReduce", Rules("trig-reduce-powers", "trig-power-reduce", "product-to-sum", "trig-combine"));

    /// <summary>The simplifications that never make an expression larger and are applied before searching.</summary>
    public static Transform Cleanup { get; } = new("Cleanup", Rules("cleanup", "power-simplify", "combine-powers", "radical-simplify", "abs-simplify", "sign-simplify", "log-simplify", "trig-reduce", "fraction-simplify", "zero-product-simplify", "negative-exponent"));

    /// <summary>The transforms the default simplifier searches over.</summary>
    public static ImmutableArray<Transform> Default { get; } = [Expand, Factor, Together, Cancel, PowerSimplify, LogCombine, LogExpand, TrigSimplify, TrigReduce];

    /// <summary>Collects the terms of a polynomial in <paramref name="x"/> by powers of <paramref name="x"/> (catalog <c>alg.poly.like-terms</c>).</summary>
    public static Transform Collect(Symbol x) => new("Collect", Strategies.Algorithm("collect:" + x.Name, (node, context) => CollectTerms(node, x, context)));

    /// <summary>Writes a quadratic in <paramref name="x"/> as a(x + h)² + k (catalog <c>alg.quad.completing-the-square</c>).</summary>
    public static Transform CompleteSquare(Symbol x) => new("CompleteSquare", Strategies.Algorithm("complete-square", (node, context) => CompleteSquareAlgorithm(node, context, x)));

    // ----- Algorithms -----

    private static CurriculumLevel LevelOf(string id) => Library.Catalog.Get(id).Level ?? CurriculumLevel.University;

    private static Number Num(BigRational r) => new Number(r);

    private static Expr Mul(IEnumerable<Expr> factors)
    {
        var list = factors.ToList();
        return list.Count == 1 ? list[0] : new Apply(Operators.Mul, [.. list]);
    }

    private static Expr Add(IEnumerable<Expr> terms)
    {
        var list = terms.ToList();
        return list.Count == 1 ? list[0] : new Apply(Operators.Add, [.. list]);
    }

    private static Expr Pow(Expr b, int n) => n == 1 ? b : new Apply(Operators.Pow, [b, Num(n)]);

    private static AlgorithmResult Result(string id, Expr replacement, Provisos? provisos = null, params (string, Expr)[] arguments) =>
        new(replacement, new EntryId(id), LevelOf(id), provisos ?? Provisos.None, null, [.. arguments.Select(a => new KeyValuePair<string, Expr>(a.Item1, a.Item2))]);

    // (a + b)^n for an integer n ≥ 2, and sums of three or more terms to powers: the multinomial theorem, computed over sparse polynomials.
    private static AlgorithmResult? ExpandPower(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op, Arguments: [Apply { Operator: var inner, Arguments: var terms } sum, Number { Value: var n }] } || op != Operators.Pow || inner != Operators.Add) return null;
        if (!n.IsInteger || n < 4 || n > 24 || terms.Length < 2) return null;
        // Exponents 2 and 3 of binomials have their own catalog laws (the expand rule set); larger ones come from the binomial theorem.
        if (!PolynomialConversion.TryToSparse(sum, out var variables, out var p)) return null;
        var power = p;
        for (var i = 1; i < (int)n.Numerator; i++) power *= p;
        var expanded = PolynomialConversion.FromSparse(power, variables);
        var id = terms.Length == 2 ? "alg.poly.binomial-theorem" : "alg.poly.multinomial-theorem";
        return Result(id, expanded, null, ("base", sum), ("n", Num(n)));
    }

    private static AlgorithmResult? FactorRationalRoots(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op } || op != Operators.Add) return null;
        if (node.FreeSymbols.Count != 1) return null;
        var x = node.FreeSymbols.Single();
        if (!PolynomialConversion.TryToPolynomial(node, x, out var p) || p.Degree < 2) return null;
        var roots = PolynomialAlgorithms.RationalRoots(p);
        if (roots.Roots.IsEmpty) return null;

        // p = c · ∏ (q·x − r)^m · cofactor, with the cofactor primitive with a positive leading coefficient.
        var constant = BigRational.One;
        var factors = new List<Expr>();
        var rest = p;
        foreach (var (root, multiplicity) in roots.Roots)
        {
            var linear = new Polynomial<BigRational>([-root, BigRational.One]);
            for (var i = 0; i < multiplicity; i++)
            {
                var (quotient, remainder) = rest.DivRem(linear);
                if (!remainder.IsZero) return null;
                rest = quotient;
            }
            // x − r = (q·x − p')/q for r = p'/q.
            var q = new BigRational(root.Denominator);
            var primitive = new Polynomial<BigRational>([-(root * q), q]);
            for (var i = 0; i < multiplicity; i++) constant /= q;
            factors.Add(Pow(PolynomialConversion.FromPolynomial(primitive, x), multiplicity));
        }

        if (rest.Degree > 0)
        {
            var lcm = BigInteger.One;
            foreach (var c in rest.Coefficients) lcm = lcm / BigInteger.GreatestCommonDivisor(lcm, c.Denominator) * c.Denominator;
            var ints = rest.Coefficients.Select(c => (c * new BigRational(lcm)).Numerator).ToArray();
            var gcd = ints.Aggregate(BigInteger.Zero, BigInteger.GreatestCommonDivisor);
            var scale = new BigRational(lcm) / new BigRational(gcd);
            var primitiveRest = rest * scale;
            if (primitiveRest.LeadingCoefficient.Sign < 0)
            {
                primitiveRest = -primitiveRest;
                scale = -scale;
            }
            constant /= scale;
            factors.Add(PolynomialConversion.FromPolynomial(primitiveRest, x));
        }
        else
        {
            constant *= rest[0];
        }

        var all = new List<Expr>();
        if (constant != BigRational.One) all.Add(Num(constant));
        all.AddRange(factors);
        if (all.Count == 1 && all[0] is Apply { Operator: var single } && single == Operators.Add) return null;
        return Result("alg.poly.factor-theorem", Mul(all), null, ("polynomial", node), ("roots", Add(roots.Roots.Select(r => Num(r.Root)))));
    }

    private static AlgorithmResult? CancelAlgorithm(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op } || (op != Operators.Mul && op != Operators.Pow)) return null;
        if (node.FreeSymbols.Count != 1) return null;
        var x = node.FreeSymbols.Single();
        if (!PolynomialConversion.TryToRationalFunction(node, x, out var n, out var d) || d.Degree < 1 || n.IsZero) return null;
        var g = PolynomialAlgorithms.Gcd(n, d);
        if (g.Degree < 1) return null;
        var gExpr = PolynomialConversion.FromPolynomial(g, x);
        var numerator = PolynomialConversion.FromPolynomial(n / g, x);
        var denominator = PolynomialConversion.FromPolynomial(d / g, x);
        var replacement = new Apply(Operators.Mul, [numerator, new Apply(Operators.Pow, [denominator, Num(-1)])]);
        var proviso = new Apply(Operators.Ne, [gExpr, Num(BigRational.Zero)]);
        var provisos = context.Math.Ask(proviso) == Truth.True ? Provisos.None : Provisos.None.Add(proviso);
        return Result("alg.frac.equivalent", replacement, provisos, ("factor", gExpr), ("c", gExpr));
    }

    // (numerator, denominator factors with exponents) of a term
    private static (List<Expr> Num, List<(Expr Base, int Exp)> Den, BigInteger NumericDen) Split(Expr term)
    {
        var num = new List<Expr>();
        var den = new List<(Expr, int)>();
        var numericDen = BigInteger.One;
        foreach (var f in term is Apply { Operator: var op, Arguments: var args } && op == Operators.Mul ? args : [term])
        {
            switch (f)
            {
                case Apply { Operator: var pw, Arguments: [var b, Number { Value: var e }] } when pw == Operators.Pow && e.IsInteger && e < 0:
                    den.Add((b, (int)(-e.Numerator)));
                    break;
                case Number { Value: var r } when !r.IsInteger:
                    num.Add(Num(new BigRational(r.Numerator)));
                    numericDen = r.Denominator;
                    break;
                default:
                    num.Add(f);
                    break;
            }
        }
        return (num, den, numericDen);
    }

    private static AlgorithmResult? TogetherTerms(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op, Arguments: var terms } || op != Operators.Add) return null;
        var parts = terms.Select(Split).ToList();
        if (parts.All(p => p.Den.Count == 0 && p.NumericDen.IsOne)) return null;

        var lcdBases = new List<(Expr Base, int Exp)>();
        var lcdNumeric = BigInteger.One;
        foreach (var part in parts)
        {
            lcdNumeric = lcdNumeric / BigInteger.GreatestCommonDivisor(lcdNumeric, part.NumericDen) * part.NumericDen;
            foreach (var (b, e) in part.Den)
            {
                var i = lcdBases.FindIndex(x => x.Base.Equals(b));
                if (i < 0) lcdBases.Add((b, e));
                else if (lcdBases[i].Exp < e) lcdBases[i] = (b, e);
            }
        }

        var numerators = new List<Expr>();
        foreach (var part in parts)
        {
            var factors = new List<Expr>(part.Num);
            var numericFactor = lcdNumeric / part.NumericDen;
            if (!numericFactor.IsOne) factors.Add(Num(new BigRational(numericFactor)));
            foreach (var (b, e) in lcdBases)
            {
                var have = part.Den.Where(d => d.Base.Equals(b)).Select(d => d.Exp).FirstOrDefault();
                if (e - have > 0) factors.Add(Pow(b, e - have));
            }
            numerators.Add(factors.Count == 0 ? Num(BigRational.One) : Mul(factors));
        }

        var denominator = new List<Expr>();
        if (!lcdNumeric.IsOne) denominator.Add(Num(new BigRational(lcdNumeric)));
        denominator.AddRange(lcdBases.Select(d => Pow(d.Base, d.Exp)));
        var replacement = new Apply(Operators.Mul, [Add(numerators), new Apply(Operators.Pow, [Mul(denominator), Num(-1)])]);
        return Result("alg.frac.add-common", replacement, null, ("denominator", Mul(denominator)));
    }

    private static AlgorithmResult? ApartAlgorithm(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op } || (op != Operators.Mul && op != Operators.Pow)) return null;
        if (node.FreeSymbols.Count != 1) return null;
        var x = node.FreeSymbols.Single();
        if (!PolynomialConversion.TryToRationalFunction(node, x, out var n, out var d) || d.Degree < 1 || n.IsZero) return null;
        var g = PolynomialAlgorithms.Gcd(n, d);
        var provisos = Provisos.None;
        if (g.Degree >= 1)
        {
            // A common factor cancels first; that is valid only where it is not zero.
            var cancelled = new Apply(Operators.Ne, [PolynomialConversion.FromPolynomial(g, x), Num(BigRational.Zero)]);
            if (context.Math.Ask(cancelled) != Truth.True) provisos = provisos.Add(cancelled);
            n /= g;
            d /= g;
        }
        if (d.Degree < 1) return null;

        var roots = PolynomialAlgorithms.RationalRoots(d);
        if (roots.Roots.Sum(r => r.Multiplicity) != d.Degree) return null;
        var (quotient, remainder) = n.DivRem(d);
        if (remainder.IsZero || (d.Degree < 2 && quotient.IsZero)) return null;

        // remainder / d = Σ A_ij / (x − r_i)^j; match coefficients of Σ A_ij · (d/lc) / (x − r_i)^j = remainder / lc.
        var lead = d.LeadingCoefficient;
        var unknowns = new List<(BigRational Root, int Power)>();
        var basis = new List<Polynomial<BigRational>>();
        foreach (var (root, m) in roots.Roots)
        {
            for (var j = 1; j <= m; j++)
            {
                unknowns.Add((root, j));
                var other = Polynomial<BigRational>.One;
                foreach (var (r2, m2) in roots.Roots)
                {
                    var linear = new Polynomial<BigRational>([-r2, BigRational.One]);
                    other *= linear.Pow(r2 == root ? m - j : m2);
                }
                basis.Add(other);
            }
        }

        var size = unknowns.Count;
        var matrix = new BigRational[size, size + 1];
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++) matrix[row, col] = basis[col][row];
            matrix[row, size] = remainder[row] / lead;
        }
        var solution = Solve(matrix, size);
        if (solution is null) return null;

        var terms = new List<Expr>();
        if (!quotient.IsZero) terms.Add(PolynomialConversion.FromPolynomial(quotient, x));
        for (var i = 0; i < size; i++)
        {
            if (solution[i] == BigRational.Zero) continue;
            var linear = PolynomialConversion.FromPolynomial(new Polynomial<BigRational>([-unknowns[i].Root, BigRational.One]), x);
            terms.Add(new Apply(Operators.Mul, [Num(solution[i]), new Apply(Operators.Pow, [Pow(linear, unknowns[i].Power), Num(-1)])]));
        }
        if (terms.Count < 2 && quotient.IsZero && roots.Roots.Length == 1 && roots.Roots[0].Multiplicity == 1) return null;
        var id = roots.Roots.All(r => r.Multiplicity == 1) ? "alg.pf.distinct-linear" : "alg.pf.repeated-linear";
        return Result(id, Add(terms), provisos, ("fraction", node));
    }

    private static BigRational[]? Solve(BigRational[,] m, int n)
    {
        for (var col = 0; col < n; col++)
        {
            var pivot = -1;
            for (var row = col; row < n; row++)
            {
                if (m[row, col] != BigRational.Zero) { pivot = row; break; }
            }
            if (pivot < 0) return null;
            if (pivot != col)
            {
                for (var k = 0; k <= n; k++) (m[col, k], m[pivot, k]) = (m[pivot, k], m[col, k]);
            }
            var inv = BigRational.One / m[col, col];
            for (var k = col; k <= n; k++) m[col, k] *= inv;
            for (var row = 0; row < n; row++)
            {
                if (row == col || m[row, col] == BigRational.Zero) continue;
                var f = m[row, col];
                for (var k = col; k <= n; k++) m[row, k] -= f * m[col, k];
            }
        }
        return Enumerable.Range(0, n).Select(i => m[i, n]).ToArray();
    }

    private static AlgorithmResult? CollectTerms(Expr node, Symbol x, RewriteContext context)
    {
        if (node is not Apply { Operator: var op } || op != Operators.Add || !node.FreeSymbols.Contains(x)) return null;
        var coefficients = PolynomialConversion.Coefficients(node, x);
        if (coefficients is null) return null;
        var terms = new List<Expr>();
        foreach (var (degree, c) in coefficients.OrderByDescending(kv => kv.Key))
        {
            if (c is Number { Value.Sign: 0 }) continue;
            terms.Add(degree == 0 ? c : new Apply(Operators.Mul, [c, Pow(x, degree)]));
        }
        return terms.Count == 0 ? null : Result("alg.poly.like-terms", Add(terms), null, ("variable", x));
    }

    private static AlgorithmResult? CompleteSquareAlgorithm(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op } || op != Operators.Add) return null;
        foreach (var x in node.FreeSymbols.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            if (CompleteSquareAlgorithm(node, context, x) is { } result) return result;
        }
        return null;
    }

    private static AlgorithmResult? CompleteSquareAlgorithm(Expr node, RewriteContext context, Symbol x)
    {
        var coefficients = PolynomialConversion.Coefficients(node, x);
        if (coefficients is null || coefficients.Keys.Any(k => k > 2) || !coefficients.TryGetValue(2, out var a) || !coefficients.TryGetValue(1, out var b)) return null;
        coefficients.TryGetValue(0, out var c);
        c ??= Num(BigRational.Zero);
        var nonZero = new Apply(Operators.Ne, [a, Num(BigRational.Zero)]);
        if (context.Math.Ask(nonZero) != Truth.True) return null;
        var h = new Apply(Operators.Mul, [b, new Apply(Operators.Pow, [new Apply(Operators.Mul, [Num(2), a]), Num(-1)])]);
        var k = new Apply(Operators.Add, [c, new Apply(Operators.Mul, [Num(-1), new Apply(Operators.Pow, [b, Num(2)]), new Apply(Operators.Pow, [new Apply(Operators.Mul, [Num(4), a]), Num(-1)])])]);
        var result = new Apply(Operators.Add, [new Apply(Operators.Mul, [a, new Apply(Operators.Pow, [new Apply(Operators.Add, [x, h]), Num(2)])]), k]);
        return Result("alg.quad.completing-the-square", result, null, ("variable", x));
    }
}
