using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Calculus;
using Mathesis.Explanation;
using Mathesis.Knowledge;
using Mathesis.LinearAlgebra;
using Mathesis.LinearAlgebra.Exact;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Simplification;
using Mathesis.Solving;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis;

/// <summary>How <see cref="Cas.Divide"/> shows polynomial division.</summary>
public enum DivisionStyle : byte
{
    /// <summary>Long division (catalog <c>alg.poly.long-division</c>).</summary>
    LongDivision,

    /// <summary>Synthetic division by a monic linear divisor <c>x − c</c> (catalog <c>alg.poly.synthetic-division</c>).</summary>
    Synthetic,
}

/// <summary>How <see cref="Cas.Determinant"/> computes the determinant.</summary>
public enum DeterminantMethod : byte
{
    /// <summary>Gaussian elimination over the rationals.</summary>
    Elimination,

    /// <summary>Cofactor expansion along the first row.</summary>
    Cofactor,
}

/// <summary>The quotient and remainder of a polynomial division.</summary>
/// <param name="Quotient">The quotient.</param>
/// <param name="Remainder">The remainder (degree lower than the divisor's).</param>
public sealed record DivisionResult(Expr Quotient, Expr Remainder);

/// <summary>An eigenvalue with its algebraic multiplicity and, for a rational eigenvalue, a basis of its eigenspace.</summary>
/// <param name="Value">The eigenvalue (exact when the characteristic polynomial splits in closed form, otherwise a <see cref="Float"/>).</param>
/// <param name="Multiplicity">The algebraic multiplicity.</param>
/// <param name="Eigenvectors">A basis of the eigenspace; empty when the eigenvalue is irrational.</param>
public sealed record EigenPair(Expr Value, int Multiplicity, ImmutableArray<DenseVector<BigRational>> Eigenvectors);

/// <summary>
/// The façade over the engines (docs/design/08-features-and-abilities.md, "Façade"). Every method takes an optional <see cref="MathContext"/> and, where
/// work can be unbounded, an optional <see cref="Budget"/>, and returns an <see cref="Outcome{T}"/> whose steps cite catalog entries.
/// </summary>
public static class Cas
{
    private static readonly RuleLibrary Library = RuleLibrary.Default;

    private static RewriteContext Context(MathContext? math, Budget? budget) => new(math, budget, ProvisoMode.Default);

    // A transform that does not apply is not a failure for a caller who asked for a form: the expression already has it.
    private static Outcome<Expr> Run(Transform transform, Expr expr, MathContext? math, Budget? budget)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var context = Context(math, budget);
        var outcome = transform.Run(expr, context);
        if (outcome is Outcome<Expr>.Unevaluated { Reason: var reason } && !context.Budget.IsExceeded && reason.Contains("did not apply", StringComparison.Ordinal))
        {
            var canonical = context.Normalize(expr);
            return Outcome.Ok(canonical, Derivation.Empty(canonical), Provisos.None, Verification.NotChecked);
        }
        return outcome;
    }

    // ----- Parsing, sorts, evaluation -----

    /// <summary>Parses linear text (docs/design/05-syntax-trees-and-notation.md).</summary>
    public static Outcome<Expr> Parse(string text, ParserOptions? options = null) => Expr.TryParse(text, options).ToOutcome();

    /// <summary>Parses LaTeX.</summary>
    public static Outcome<Expr> ParseLatex(string latex, ParserOptions? options = null) => LatexParser.Parse(latex, options).ToOutcome();

    /// <summary>Checks the sorts of <paramref name="expr"/>.</summary>
    public static SortCheckResult CheckSorts(Expr expr) => SortChecker.Check(expr ?? throw new ArgumentNullException(nameof(expr)));

    /// <summary>Evaluates exactly, substituting <paramref name="bindings"/>.</summary>
    public static Outcome<Expr> Evaluate(Expr expr, IReadOnlyDictionary<Symbol, Expr>? bindings = null, MathContext? math = null, Budget? budget = null) =>
        Evaluator.Evaluate(expr, bindings, (math ?? MathContext.Default).NormalizeOptions, budget);

    /// <summary>A numeric value in double precision (up to 15 digits).</summary>
    public static Outcome<double> N(Expr expr, IReadOnlyDictionary<Symbol, double>? values = null, int digits = 15) => Evaluator.N(expr, values, digits);

    // ----- Algebraic transforms -----

    /// <summary>The simplest form of <paramref name="expr"/> reachable by the transforms (see <see cref="Simplifier"/>).</summary>
    public static Outcome<Expr> Simplify(Expr expr, MathContext? math = null, IComplexityMeasure? measure = null, Budget? budget = null) =>
        Simplifier.Simplify(expr, math, measure is null ? null : new SimplifyOptions { Measure = measure }, budget);

    /// <summary>Multiplies products over sums and powers of sums out.</summary>
    public static Outcome<Expr> Expand(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.Expand, expr, math, budget);

    /// <summary>Writes a polynomial as a product over the rationals (common factors, special products, rational roots).</summary>
    public static Outcome<Expr> Factor(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.Factor, expr, math, budget);

    /// <summary>Collects the terms of <paramref name="expr"/> by powers of <paramref name="x"/>.</summary>
    public static Outcome<Expr> Collect(Expr expr, Symbol x, MathContext? math = null, Budget? budget = null) => Run(Transforms.Collect(x), expr, math, budget);

    /// <summary>Combines a sum of fractions over a common denominator.</summary>
    public static Outcome<Expr> Together(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.Together, expr, math, budget);

    /// <summary>Splits a rational function with rational poles into partial fractions in the variable of the expression.</summary>
    public static Outcome<Expr> Apart(Expr expr, Symbol x, MathContext? math = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        return Run(Transforms.Apart, expr, math, budget);
    }

    /// <summary>Divides out the common polynomial factor of a numerator and a denominator.</summary>
    public static Outcome<Expr> Cancel(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.Cancel, expr, math, budget);

    /// <summary>Removes radicals from denominators.</summary>
    public static Outcome<Expr> Rationalize(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.Rationalize, expr, math, budget);

    /// <summary>Simplifies radicals (extracts and combines roots).</summary>
    public static Outcome<Expr> RadicalSimplify(Expr expr, MathContext? math = null, Budget? budget = null) =>
        Run(new Transform("RadicalSimplify", Strategies.Innermost(Strategies.Apply(Library.Union("radical-simplify", "radical-simplify", "radical-combine")))), expr, math, budget);

    /// <summary>Writes a quadratic in <paramref name="x"/> as <c>a(x + h)² + k</c> (vertex form).</summary>
    public static Outcome<Expr> CompleteSquare(Expr expr, Symbol x, MathContext? math = null, Budget? budget = null) => Run(Transforms.CompleteSquare(x), expr, math, budget);

    /// <summary>Combines powers, simplifies radicals and absolute values.</summary>
    public static Outcome<Expr> PowerSimplify(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.PowerSimplify, expr, math, budget);

    /// <summary>Writes sums and differences of logarithms as one logarithm.</summary>
    public static Outcome<Expr> LogCombine(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.LogCombine, expr, math, budget);

    /// <summary>Splits logarithms of products, quotients and powers.</summary>
    public static Outcome<Expr> LogExpand(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.LogExpand, expr, math, budget);

    /// <summary>Reduces trigonometric expressions with the Pythagorean, parity, period and shift identities.</summary>
    public static Outcome<Expr> TrigSimplify(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.TrigSimplify, expr, math, budget);

    /// <summary>Expands sums, multiples and double angles of trigonometric functions.</summary>
    public static Outcome<Expr> TrigExpand(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.TrigExpand, expr, math, budget);

    /// <summary>Reduces powers and products of trigonometric functions to sums of multiple angles.</summary>
    public static Outcome<Expr> TrigReduce(Expr expr, MathContext? math = null, Budget? budget = null) => Run(Transforms.TrigReduce, expr, math, budget);

    /// <summary>Writes sine, cosine and tangent with complex exponentials (Euler's formulas; needs <c>I</c>, the imaginary unit).</summary>
    public static Outcome<Expr> TrigToExp(Expr expr, MathContext? math = null, Budget? budget = null) =>
        Run(new Transform("TrigToExp", Strategies.Innermost(Strategies.Apply(Library["euler-form"]))), expr, math, budget);

    /// <summary>Writes complex exponentials with sine and cosine.</summary>
    public static Outcome<Expr> ExpToTrig(Expr expr, MathContext? math = null, Budget? budget = null) =>
        Run(new Transform("ExpToTrig", Strategies.Innermost(Strategies.Apply(Library["euler-form-reverse"]))), expr, math, budget);

    /// <summary>
    /// Divides the polynomial <paramref name="p"/> by <paramref name="q"/> in <paramref name="x"/> with rational coefficients, by long or synthetic division.
    /// Synthetic division needs a monic linear divisor <c>x − c</c>.
    /// </summary>
    public static Outcome<DivisionResult> Divide(Expr p, Expr q, Symbol x, DivisionStyle style = DivisionStyle.LongDivision)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(x);
        if (!PolynomialConversion.TryToPolynomial(Normalizer.Canonical(p), x, out var dividend) || !PolynomialConversion.TryToPolynomial(Normalizer.Canonical(q), x, out var divisor))
        {
            return new Outcome<DivisionResult>.Unevaluated(p, "Both expressions must be polynomials in the variable with rational coefficients.");
        }
        if (divisor.IsZero) return Outcome.Fail<DivisionResult>(new MathError(MathErrorKind.Domain, "Division by the zero polynomial.", q));
        if (style == DivisionStyle.Synthetic && (divisor.Degree != 1 || divisor.LeadingCoefficient != BigRational.One))
        {
            return new Outcome<DivisionResult>.Unevaluated(q, "Synthetic division needs a divisor of the form x − c.");
        }
        var (quotient, remainder) = dividend.DivRem(divisor);
        var result = new DivisionResult(Normalizer.Canonical(PolynomialConversion.FromPolynomial(quotient, x)), Normalizer.Canonical(PolynomialConversion.FromPolynomial(remainder, x)));
        var entry = new EntryId(style == DivisionStyle.Synthetic ? "alg.poly.synthetic-division" : "alg.poly.long-division");
        var start = Normalizer.Canonical(new Apply(Operators.Mul, [p, new Apply(Operators.Pow, [q, new Number(BigRational.NegativeOne)])]));
        var end = Normalizer.Canonical(new Apply(Operators.Add, [new Apply(Operators.Mul, [result.Quotient, q]), result.Remainder]));
        var step = new Step(entry, "divide", start, end, ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of(entry.Value), KnowledgeBase.Default.Get(entry).Level ?? CurriculumLevel.Algebra2, null);
        return Outcome.Ok(result, new Derivation(start, end, [step]), Provisos.None, Verification.Verified);
    }

    /// <summary>
    /// Applies one catalog law, by entry ID (<c>trig.sum.sin-of-sum</c>, or <c>…~rtl</c> for the reverse direction), at the node of <paramref name="expr"/> at
    /// <paramref name="at"/>. The result is a one-step derivation citing the law.
    /// </summary>
    public static Outcome<Expr> Apply(string law, Expr expr, ExprPath at = default, MathContext? math = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(law);
        ArgumentNullException.ThrowIfNull(expr);
        if (!Library.TryGetRule(law, out var rule)) return new Outcome<Expr>.Unevaluated(expr, $"The catalog has no rewrite rule '{law}'.");
        var context = Context(math, null);
        var canonical = context.Normalize(expr);
        var state = new RewriteState(canonical, at, []);
        var result = Strategies.Apply(new RuleSet(law, [rule])).Run(state, context);
        return result is null
            ? new Outcome<Expr>.Unevaluated(canonical, $"The law '{law}' does not apply at that place.")
            : RewriteEngine.Finish(expr, result, context);
    }

    // ----- Solving -----

    /// <summary>Solves an equation or inequality in <paramref name="x"/> (see <see cref="Solver"/>).</summary>
    public static Outcome<SolutionSet> Solve(Expr equation, Symbol x, SolveMethod method = SolveMethod.Auto, MathContext? math = null, Budget? budget = null) =>
        Solver.Solve(equation, x, new SolveOptions { Method = method }, math, budget);

    /// <summary>Solves a system of equations in <paramref name="variables"/>.</summary>
    public static Outcome<SolutionSet> Solve(IReadOnlyList<Expr> equations, IReadOnlyList<Symbol> variables, MathContext? math = null, Budget? budget = null) =>
        Solver.SolveSystem(equations, variables, null, math, budget);

    /// <summary>The numeric roots of an equation in <c>[low, high]</c>, in increasing order.</summary>
    public static Outcome<ImmutableArray<double>> NSolve(Expr equation, Symbol x, double low, double high, MathContext? math = null, Budget? budget = null)
    {
        var outcome = Solver.Solve(equation, x, new SolveOptions { IntervalLow = low, IntervalHigh = high }, math, budget);
        if (outcome is not Outcome<SolutionSet>.Success { Value: var set }) return outcome is Outcome<SolutionSet>.Unevaluated u ? new Outcome<ImmutableArray<double>>.Unevaluated(u.Original, u.Reason) : Outcome.Fail<ImmutableArray<double>>(new MathError(MathErrorKind.Unsupported, "The equation could not be solved.", equation));
        var values = set.Kind == SolutionKind.Condition
            ? set.Approximations.Where(v => v >= low && v <= high)
            : set.Points.Select(p => Evaluator.N(p) is Outcome<double>.Success { Value: var v } ? v : double.NaN).Where(v => v >= low - 1e-12 && v <= high + 1e-12);
        return Outcome.Ok(values.OrderBy(v => v).ToImmutableArray());
    }

    // ----- Calculus -----

    /// <summary>The limit of <paramref name="f"/> as <paramref name="x"/> approaches <paramref name="point"/> (see <see cref="Limits"/>).</summary>
    public static Outcome<LimitResult> Limit(Expr f, Symbol x, Expr point, LimitDirection direction = LimitDirection.Both, MathContext? math = null, Budget? budget = null) =>
        Limits.Limit(f, x, point, direction, math, budget);

    /// <summary>The <paramref name="order"/>-th derivative of <paramref name="f"/> in <paramref name="x"/>, with steps.</summary>
    public static Outcome<Expr> Differentiate(Expr f, Symbol x, int order = 1, MathContext? math = null, Budget? budget = null) =>
        Differentiator.Differentiate(f, x, order, math, budget);

    /// <summary><c>dy/dx</c> for the relation <paramref name="equation"/> (catalog <c>calc.deriv.implicit</c>).</summary>
    public static Outcome<Expr> ImplicitDerivative(Expr equation, Symbol y, Symbol x, MathContext? math = null) => Differentiator.Implicit(equation, x, y, math);

    /// <summary>An antiderivative of <paramref name="f"/>, verified by differentiating it back.</summary>
    public static Outcome<Expr> Integrate(Expr f, Symbol x, MathContext? math = null, Budget? budget = null) => Integrator.Integrate(f, x, math, budget);

    /// <summary>The definite integral over <c>[a, b]</c> (bounds may be <c>oo</c>), checked against quadrature.</summary>
    public static Outcome<Expr> Integrate(Expr f, Symbol x, Expr a, Expr b, MathContext? math = null, Budget? budget = null) => Integrator.IntegrateDefinite(f, x, a, b, math, budget);

    /// <summary>The Taylor polynomial of degree <paramref name="order"/> around <paramref name="center"/>.</summary>
    public static Outcome<Expr> Taylor(Expr f, Symbol x, Expr center, int order, MathContext? math = null) => Calculus.Series.Taylor(f, x, center, order, math);

    /// <summary>
    /// The series of <paramref name="f"/> around a rational <paramref name="center"/> up to degree <paramref name="order"/>, with negative powers where
    /// <paramref name="f"/> has a pole.
    /// </summary>
    public static Outcome<Expr> Series(Expr f, Symbol x, Expr center, int order, MathContext? math = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(center);
        var c = Normalizer.Canonical(center);
        if (c is Number { Value: var a })
        {
            for (var extra = 0; extra <= 12; extra += 4)
            {
                if (PowerSeries.FromExpression(Normalizer.Canonical(f), x, a, order + 4 + extra) is { } series && series.Valuation is { } v && series.Order > order)
                {
                    var terms = new List<Expr>();
                    for (var k = v; k <= order; k++)
                    {
                        if (series[k] == BigRational.Zero) continue;
                        Expr shifted = a == BigRational.Zero ? x : new Apply(Operators.Add, [x, new Number(-a)]);
                        terms.Add(new Apply(Operators.Mul, [new Number(series[k]), new Apply(Operators.Pow, [shifted, new Number(k)])]));
                    }
                    var result = Normalizer.Canonical(terms.Count == 0 ? new Number(BigRational.Zero) : terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]));
                    var step = new Step(new EntryId("calc.series.series-arithmetic"), "series", f, result, ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of("calc.series.series-arithmetic"), CurriculumLevel.Calculus2, null);
                    return Outcome.Ok(result, new Derivation(f, result, [step]), Provisos.None, Verification.NotChecked);
                }
            }
        }
        return Calculus.Series.Taylor(f, x, center, order, math);
    }

    // ----- Linear algebra (exact, over the rationals) -----

    /// <summary>Gauss–Jordan elimination: the reduced row echelon form, the pivot columns and every row operation.</summary>
    public static RowReduction<BigRational> RowReduce(DenseMatrix<BigRational> a) => ExactLinearAlgebra.RowReduce(a ?? throw new ArgumentNullException(nameof(a)));

    /// <summary>The determinant of a square matrix.</summary>
    public static BigRational Determinant(DenseMatrix<BigRational> a, DeterminantMethod method = DeterminantMethod.Elimination)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (!a.Shape.IsSquare) throw new ArgumentException("The matrix must be square.", nameof(a));
        return method == DeterminantMethod.Elimination ? ExactLinearAlgebra.Determinant(a) : Cofactor(a);
    }

    // det(A) = Σ_j (−1)^j a_0j · det(minor_0j)
    private static BigRational Cofactor(DenseMatrix<BigRational> a)
    {
        if (a.Rows == 1) return a[0, 0];
        var sum = BigRational.Zero;
        for (var j = 0; j < a.Columns; j++)
        {
            if (a[0, j] == BigRational.Zero) continue;
            var minor = DenseMatrix.Create(a.Rows - 1, a.Columns - 1, (r, c) => a[r + 1, c < j ? c : c + 1]);
            sum += (j % 2 == 0 ? a[0, j] : -a[0, j]) * Cofactor(minor);
        }
        return sum;
    }

    /// <summary>The inverse of a square matrix, or a failure when it is singular.</summary>
    public static Outcome<DenseMatrix<BigRational>> Inverse(DenseMatrix<BigRational> a) => ExactLinearAlgebra.Inverse(a ?? throw new ArgumentNullException(nameof(a)));

    /// <summary>The rank.</summary>
    public static int Rank(DenseMatrix<BigRational> a) => ExactLinearAlgebra.Rank(a ?? throw new ArgumentNullException(nameof(a)));

    /// <summary>A basis of the null space.</summary>
    public static ImmutableArray<DenseVector<BigRational>> NullSpace(DenseMatrix<BigRational> a) => ExactLinearAlgebra.NullSpace(a ?? throw new ArgumentNullException(nameof(a)));

    /// <summary>A basis of the column space.</summary>
    public static ImmutableArray<DenseVector<BigRational>> ColumnSpace(DenseMatrix<BigRational> a) => ExactLinearAlgebra.ColumnSpace(a ?? throw new ArgumentNullException(nameof(a)));

    /// <summary>
    /// The eigenvalues of a square matrix as the real roots of its characteristic polynomial (rational and quadratic roots exactly, others as
    /// <see cref="Float"/>), each with its multiplicity and, for rational eigenvalues, a basis of the eigenspace.
    /// </summary>
    public static Outcome<ImmutableArray<EigenPair>> Eigen(DenseMatrix<BigRational> a)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (!a.Shape.IsSquare) throw new ArgumentException("The matrix must be square.", nameof(a));
        var characteristic = ExactLinearAlgebra.CharacteristicPolynomial(a);
        var x = new Symbol("lambda");
        var equation = new Apply(Operators.Eq, [PolynomialConversion.FromPolynomial(characteristic, x), new Number(BigRational.Zero)]);
        if (Solver.Solve(equation, x) is not Outcome<SolutionSet>.Success { Value: var roots }) return new Outcome<ImmutableArray<EigenPair>>.Unevaluated(equation, "The characteristic polynomial could not be solved.");
        var pairs = new List<EigenPair>();
        foreach (var root in roots.Points)
        {
            var multiplicity = 0;
            var rest = characteristic;
            ImmutableArray<DenseVector<BigRational>> vectors = [];
            if (root is Number { Value: var lambda })
            {
                var linear = new Polynomial<BigRational>([-lambda, BigRational.One]);
                while (rest.Degree >= 1 && rest.DivRem(linear).Remainder.IsZero)
                {
                    rest = rest.DivRem(linear).Quotient;
                    multiplicity++;
                }
                var shifted = DenseMatrix.Create(a.Rows, a.Columns, (r, c) => a[r, c] - (r == c ? lambda : BigRational.Zero));
                vectors = ExactLinearAlgebra.NullSpace(shifted);
            }
            else
            {
                multiplicity = 1;
            }
            pairs.Add(new EigenPair(root, multiplicity, vectors));
        }
        return Outcome.Ok(pairs.ToImmutableArray());
    }

    // ----- Catalog -----

    /// <summary>The catalog entries whose name, ID, tags or explanation match <paramref name="text"/>, best match first.</summary>
    public static IEnumerable<Entry> Find(string text) => KnowledgeBase.Default.Search(text);

    /// <summary>The catalog entry with the given ID or alias.</summary>
    /// <exception cref="KeyNotFoundException">There is no such entry.</exception>
    public static Entry Get(string id) => KnowledgeBase.Default.Get(id);

    /// <summary>The catalog entries whose ID starts with <paramref name="prefix"/> (<c>trig.sum</c>).</summary>
    public static IEnumerable<Entry> ByDomain(string prefix) => KnowledgeBase.Default.ByDomain(prefix);
}
