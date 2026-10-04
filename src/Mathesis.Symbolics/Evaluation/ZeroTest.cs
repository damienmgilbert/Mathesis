using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Polynomials;

namespace Mathesis.Symbolics.Evaluation;

/// <summary>The verdict of <see cref="ZeroTest"/>.</summary>
public enum ZeroTestResult : byte
{
    /// <summary>The expression is identically zero (certain).</summary>
    Zero,

    /// <summary>The expression is not identically zero (certain).</summary>
    NonZero,

    /// <summary>Every sample evaluated to zero within its rounding error; the expression is probably zero but this is not a proof.</summary>
    ProbablyZero,

    /// <summary>No layer could decide.</summary>
    Unknown,
}

/// <summary>A <see cref="ZeroTestResult"/> with how it was reached.</summary>
/// <param name="Result">The verdict.</param>
/// <param name="Method">The layer that decided: <c>exact</c>, <c>interval</c>, <c>sampling</c> or <c>none</c>.</param>
/// <param name="Samples">How many sample points supported the verdict (0 for the exact layer).</param>
public sealed record ZeroTestReport(ZeroTestResult Result, string Method, int Samples);

/// <summary>
/// Decides whether an expression is identically zero (docs/design/07-engines.md, "Zero testing"). Deciding <c>e = 0</c> is undecidable in general
/// (Richardson 1968), so the layers run from certain to probabilistic.
/// </summary>
/// <remarks>
/// <para>
/// <b>Meaning.</b> For an expression with free symbols, "zero" means zero at every point where it is defined (and the assumptions hold);
/// "non-zero" means some such point gives a value different from zero.
/// </para>
/// <para>
/// <b>Layers.</b> (1) <i>Exact:</i> the expression is written as a quotient of polynomials over ℚ(i) in its symbols and in the other
/// subterms (<c>sin x</c>, π, …) taken as independent variables; a zero numerator proves <see cref="ZeroTestResult.Zero"/>, and when the
/// expression is a rational function of its symbols alone a non-zero numerator proves <see cref="ZeroTestResult.NonZero"/>.
/// (2) <i>Interval exclusion:</i> evaluating over a point with outward-rounded intervals; an enclosure that excludes 0 proves
/// <see cref="ZeroTestResult.NonZero"/>. (3) <i>Seeded random evaluation:</i> at least eight random points where the expression is defined,
/// all enclosures containing 0 and narrow, give <see cref="ZeroTestResult.ProbablyZero"/>. Precision is that of <see cref="double"/> until
/// BigFloat arrives (Milestone 7), so a non-zero value smaller than about 10⁻⁹ of its terms can be reported as probably zero. The
/// algebraic-number and exp-log structure layers of the design arrive in Milestones 2 and 4.
/// </para>
/// </remarks>
public static class ZeroTest
{
    /// <summary>The seed of the random sample points when none is given.</summary>
    public const int DefaultSeed = 20261053;

    private const int WantedPoints = 12;
    private const int MinimumPoints = 8;

    /// <summary>Runs the layers and returns the verdict.</summary>
    public static ZeroTestResult Test(Expr expr, MathContext? context = null, int seed = DefaultSeed, Budget? budget = null) =>
        Analyze(expr, context, seed, budget).Result;

    /// <summary>Runs the layers and returns the verdict together with the layer that produced it.</summary>
    public static ZeroTestReport Analyze(Expr expr, MathContext? context = null, int seed = DefaultSeed, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(expr);
        context ??= MathContext.Default;
        budget ??= Budget.Unlimited;

        var exact = new ExactLayer().Run(expr);
        if (exact is { } decided) return new(decided, "exact", 0);

        var symbols = expr.FreeSymbols.OrderBy(s => s.Name, StringComparer.Ordinal).ToImmutableArray();
        var hasImaginary = expr.Walk().Any(w => w.Expr is Constant { Id: ConstantId.ImaginaryUnit });
        var random = new Random(seed);

        var interval = hasImaginary ? null : Compiled<Interval<double>>(expr, symbols);
        var numeric = hasImaginary ? null : Compiled<double>(expr, symbols);
        var complex = hasImaginary ? Compiled<Complex<double>>(expr, symbols) : null;
        if (interval is null && numeric is null && complex is null) return new(ZeroTestResult.Unknown, "none", 0);

        var valid = 0;
        for (var attempt = 0; attempt < 400 && valid < WantedPoints && budget.TryCharge(); attempt++)
        {
            var point = SamplePoint(symbols, context, random, attempt);
            if (point is null) continue;

            if (complex is not null)
            {
                var z = complex.Invoke([.. point.Select(v => new Complex<double>(v))]);
                if (!double.IsFinite(z.Real) || !double.IsFinite(z.Imaginary)) continue;
                valid++;
                if (z.Magnitude > 1e-6) return new(ZeroTestResult.NonZero, "sampling", valid);
                continue;
            }

            if (interval is not null)
            {
                var enclosure = interval.Invoke([.. point.Select(Interval<double>.Point)]);
                if (enclosure.IsEmpty) continue;
                if (enclosure.Lower > 0 || enclosure.Upper < 0) return new(ZeroTestResult.NonZero, "interval", valid + 1);

                // The enclosure contains 0: it counts as evidence for zero only if it is narrow (no catastrophic cancellation).
                if (!enclosure.IsBounded || enclosure.Width > 1e-8) continue;
                valid++;
                continue;
            }
            var v = numeric!.Invoke([.. point]);
            if (!double.IsFinite(v)) continue;
            valid++;

            // No interval evaluation exists for this expression, so only a clearly non-zero value counts.
            if (Math.Abs(v) > 1e-6) return new(ZeroTestResult.NonZero, "sampling", valid);
        }
        return valid >= MinimumPoints ? new(ZeroTestResult.ProbablyZero, "sampling", valid) : new(ZeroTestResult.Unknown, "none", valid);
    }

    private static CompiledExpr<T>? Compiled<T>(Expr expr, ImmutableArray<Symbol> symbols)
        where T : struct =>
        ExprCompiler<T>.Compile(expr, symbols) is Outcome<CompiledExpr<T>>.Success { Value: var code } ? code : null;

    // A random point satisfying the numeric bounds of the context; null when the facts reject the point.
    private static double[]? SamplePoint(ImmutableArray<Symbol> symbols, MathContext context, Random random, int attempt)
    {
        var point = new double[symbols.Length];
        var radius = attempt % 3 == 0 ? 1.0 : attempt % 3 == 1 ? 3.0 : 10.0;
        for (var i = 0; i < symbols.Length; i++)
        {
            var bounds = context.Assumptions.Bounds(symbols[i]);
            double lo = bounds?.Lower ?? -radius, hi = bounds?.Upper ?? radius;
            if (double.IsInfinity(lo) && double.IsInfinity(hi)) (lo, hi) = (-radius, radius);
            else if (double.IsInfinity(lo)) lo = hi - 2 * radius;
            else if (double.IsInfinity(hi)) hi = lo + 2 * radius;
            var value = lo + random.NextDouble() * (hi - lo);

            // Integer symbols get integer values.
            if (symbols[i].DeclaredSort.IsSubsortOf(Sort.Integer)) value = Math.Round(value);
            point[i] = value;
        }

        // Facts that relate several symbols are checked at the point.
        foreach (var fact in context.Assumptions.Facts)
        {
            if (fact is not Apply { Operator: var op, Arguments: [var l, var r] } || !(op == Operators.Lt || op == Operators.Le || op == Operators.Gt || op == Operators.Ge || op == Operators.Eq)) continue;
            var values = symbols.Select((s, i) => (s, v: point[i])).ToDictionary(t => t.s, t => t.v);
            if (Evaluator.N(l, values) is not Outcome<double>.Success { Value: var a } || Evaluator.N(r, values) is not Outcome<double>.Success { Value: var b }) continue;
            var holds = op == Operators.Lt ? a < b : op == Operators.Le ? a <= b : op == Operators.Gt ? a > b : op == Operators.Ge ? a >= b : Math.Abs(a - b) <= 1e-9;
            if (!holds) return null;
        }
        return point;
    }

    // ----- Layer 1: polynomials over ℚ(i) -----

    private sealed class ExactLayer
    {
        private readonly List<Expr> _variables = [];
        private int _imaginary = -1;
        private bool _hasAtoms;
        private bool _inexact;
        private bool _undefined;

        public ZeroTestResult? Run(Expr expr)
        {
            Collect(expr);
            if (_inexact) return null;
            var count = _variables.Count;
            var result = Build(expr, count);
            if (_undefined || result is null) return null;
            var (numerator, denominator) = result.Value;
            if (denominator.IsZero) return null;
            if (numerator.IsZero) return ZeroTestResult.Zero;
            return _hasAtoms ? null : ZeroTestResult.NonZero;
        }

        private int Index(Expr key)
        {
            var i = _variables.FindIndex(v => v.Equals(key));
            if (i >= 0) return i;
            _variables.Add(key);
            return _variables.Count - 1;
        }

        private void Collect(Expr e)
        {
            switch (e)
            {
                case Number:
                    return;
                case Float:
                    _inexact = true;
                    return;
                case Symbol:
                    Index(e);
                    return;
                case Constant { Id: ConstantId.ImaginaryUnit }:
                    _imaginary = Index(e);
                    return;
                case Constant:
                    _hasAtoms = true;
                    Index(e);
                    return;
                case Apply a when IsArithmetic(a):
                    foreach (var arg in a.Arguments) Collect(arg);
                    return;
                default:
                    // Any other subterm is an independent variable; its own subterms are not looked into.
                    _hasAtoms = true;
                    Index(e);
                    return;
            }
        }

        private static bool IsArithmetic(Apply a) =>
            a.Operator == Operators.Add || a.Operator == Operators.Sub || a.Operator == Operators.Neg || a.Operator == Operators.Mul || a.Operator == Operators.Div
            || (a.Operator == Operators.Pow && OperatorMap.TryRational(a.Arguments[1], out var k) && k.IsInteger && BigRational.Abs(k) <= 64);

        private (SparsePolynomial<BigRational> N, SparsePolynomial<BigRational> D)? Build(Expr e, int n)
        {
            SparsePolynomial<BigRational> One() => SparsePolynomial<BigRational>.Constant(BigRational.One, n);
            switch (e)
            {
                case Number num:
                    return (SparsePolynomial<BigRational>.Constant(num.Value, n), One());
                case Apply a when IsArithmetic(a):
                    break;
                default:
                    return (SparsePolynomial<BigRational>.Variable(_variables.FindIndex(v => v.Equals(e)), n), One());
            }

            var apply = (Apply)e;
            var args = apply.Arguments;
            if (apply.Operator == Operators.Pow)
            {
                if (Build(args[0], n) is not var (bn, bd)) return null;
                OperatorMap.TryRational(args[1], out var exponent);
                var k = (int)exponent.Numerator;
                if (k >= 0) return (Power(bn, k), Power(bd, k));
                if (IsZero(bn)) return Undefined();
                return (Power(bd, -k), Power(bn, -k));
            }

            var parts = new List<(SparsePolynomial<BigRational> N, SparsePolynomial<BigRational> D)>();
            foreach (var arg in args)
            {
                if (Build(arg, n) is not { } p) return null;
                parts.Add(p);
            }
            if (apply.Operator == Operators.Neg) return (-parts[0].N, parts[0].D);
            if (apply.Operator == Operators.Sub) return (Reduce(parts[0].N * parts[1].D - parts[1].N * parts[0].D), Reduce(parts[0].D * parts[1].D));
            if (apply.Operator == Operators.Div)
            {
                if (IsZero(parts[1].N)) return Undefined();
                return (Reduce(parts[0].N * parts[1].D), Reduce(parts[0].D * parts[1].N));
            }
            var (num2, den2) = parts[0];
            foreach (var (pn, pd) in parts.Skip(1))
            {
                if (apply.Operator == Operators.Add)
                {
                    num2 = Reduce(num2 * pd + pn * den2);
                    den2 = Reduce(den2 * pd);
                }
                else
                {
                    num2 = Reduce(num2 * pn);
                    den2 = Reduce(den2 * pd);
                }
            }
            return (num2, den2);
        }

        private (SparsePolynomial<BigRational>, SparsePolynomial<BigRational>)? Undefined()
        {
            _undefined = true;
            return null;
        }

        private static bool IsZero(SparsePolynomial<BigRational> p) => p.IsZero;

        private SparsePolynomial<BigRational> Power(SparsePolynomial<BigRational> p, int k)
        {
            var result = SparsePolynomial<BigRational>.Constant(BigRational.One, p.VariableCount);
            for (var i = 0; i < k; i++) result = Reduce(result * p);
            return result;
        }

        // I² = −1: every exponent of the imaginary unit is reduced to 0 or 1.
        private SparsePolynomial<BigRational> Reduce(SparsePolynomial<BigRational> p)
        {
            if (_imaginary < 0) return p;
            var terms = new List<(Monomial, BigRational)>();
            foreach (var (monomial, coefficient) in p.Terms)
            {
                var exponents = monomial.Exponents.ToArray();
                var e = exponents[_imaginary];
                if (e < 2)
                {
                    terms.Add((monomial, coefficient));
                    continue;
                }
                exponents[_imaginary] = e % 2;
                terms.Add((new Monomial(exponents), (e / 2) % 2 == 0 ? coefficient : -coefficient));
            }
            return new SparsePolynomial<BigRational>(p.VariableCount, p.Order, terms);
        }
    }
}
