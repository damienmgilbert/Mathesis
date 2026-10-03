using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Numerics;
using Mathesis.Polynomials;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Solving;

/// <summary>How a quadratic equation is solved when the caller asks for a particular method.</summary>
public enum SolveMethod : byte
{
    /// <summary>The engine chooses: rational roots first, then the quadratic formula.</summary>
    Auto,

    /// <summary>Factor, then the zero-product property (falls back to the formula when the roots are irrational).</summary>
    Factoring,

    /// <summary>The quadratic formula with its discriminant.</summary>
    QuadraticFormula,

    /// <summary>Completing the square.</summary>
    CompletingSquare,
}

/// <summary>Options of <see cref="Solver"/>.</summary>
public sealed record SolveOptions
{
    /// <summary>The method for quadratic equations.</summary>
    public SolveMethod Method { get; init; } = SolveMethod.Auto;

    /// <summary>Restricts the solutions to <c>[IntervalLow, IntervalHigh]</c>: image sets become finite sets of the members in the interval.</summary>
    public double? IntervalLow { get; init; }

    /// <summary>The upper end of the interval (see <see cref="IntervalLow"/>).</summary>
    public double? IntervalHigh { get; init; }
}

/// <summary>
/// Solving equations, inequalities and systems (docs/design/07-engines.md, "Solving engine"). Every method produces candidates; every candidate is then
/// substituted back into the original equation, and one that fails or is undefined there is rejected with a step citing <c>alg.eq.extraneous</c>.
/// </summary>
public static partial class Solver
{
    private static readonly KnowledgeBase Catalog = KnowledgeBase.Default;

    private sealed record Cand(ImmutableList<Expr> Points, ImmutableList<ImageFamily> Families, bool All, bool Complete, bool Approximate)
    {
        public static Cand None { get; } = new([], [], false, true, false);

        public static Cand Everything { get; } = new([], [], true, true, false);

        public static Cand Of(IEnumerable<Expr> points, bool complete = true, bool approximate = false) => new([.. points], [], false, complete, approximate);

        public Cand Union(Cand other) => new([.. Points, .. other.Points], [.. Families, .. other.Families], All || other.All, Complete && other.Complete, Approximate || other.Approximate);
    }

    private sealed class Ctx(MathContext math, Budget budget, SolveOptions options)
    {
        public MathContext Math { get; } = math;
        public Budget Budget { get; } = budget;
        public SolveOptions Options { get; } = options;
        public List<Step> Steps { get; } = [];
        public Provisos Provisos { get; set; } = Provisos.None;
    }

    private static Number Num(BigRational r) => new(r);

    private static Number Num(int n) => new(n);

    private static Expr Canon(Expr e) => Normalizer.Canonical(e);

    private static Apply Pow(Expr b, Expr e) => new(Operators.Pow, [b, e]);

    private static Apply Mul(params Expr[] f) => new(Operators.Mul, [.. f]);

    private static Apply Add(params Expr[] t) => new(Operators.Add, [.. t]);

    private static Apply Negate(Expr e) => Mul(Num(-1), e);

    private static Apply EqZero(Expr e) => new(Operators.Eq, [e, Num(0)]);

    // Simplifies an exact result when that makes it simpler (exp(ln 3) = 3, sqrt(16) = 4).
    private static Expr Polish(Expr e, Ctx c) => e is Float || e.FreeSymbols.Count > 0
        ? Canon(e)
        : SimplifyExact(e, c);

    private static Expr SimplifyExact(Expr e, Ctx c) =>
        Mathesis.Simplification.Simplifier.Simplify(e, c.Math, null, new Budget(maxSteps: 400, maxTime: TimeSpan.FromSeconds(1))) is Outcome<Expr>.Success { Value: var v } ? v : Canon(e);

    private static Expr Disjunction(IEnumerable<Expr> parts)
    {
        var list = parts.ToList();
        return list.Count == 0 ? new Constant(ConstantId.False) : list.Count == 1 ? list[0] : new Apply(Operators.Or, [.. list]);
    }

    private static Expr Half => Num(new BigRational(1) / new BigRational(2));

    private static Symbol Fresh(Expr f, params string[] preferred)
    {
        foreach (var name in preferred.Concat(["w", "u", "v", "s", "t", "y_1", "y_2", "y_3"]))
        {
            if (!f.FreeSymbols.Any(s => s.Name == name)) return new Symbol(name);
        }
        return new Symbol("w_" + Math.Abs(f.GetHashCode()).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void Record(Ctx c, string entry, string rule, Expr before, Expr after, params (string, Expr)[] args)
    {
        var id = new EntryId(entry);
        c.Steps.Add(new Step(id, "solve/" + rule, before, after, ExprPath.Root, Bindings.Empty, Provisos.None,
            new ExplanationKey(entry, [.. args.Select(a => new KeyValuePair<string, Expr>(a.Item1, a.Item2))]), Catalog.Get(entry).Level ?? CurriculumLevel.Algebra1, null));
    }

    // ----- Public API -----

    /// <summary>Solves <paramref name="equation"/> (an equation or an inequality) for <paramref name="x"/> in the reals.</summary>
    /// <returns>
    /// Success with the <see cref="SolutionSet"/>, a derivation of steps citing catalog entries, the provisos the answer depends on (a literal
    /// equation <c>a·x + b = 0</c> needs <c>a ≠ 0</c>), and <see cref="Verification.Verified"/> after every candidate was substituted back.
    /// Unevaluated when no method applies.
    /// </returns>
    public static Outcome<SolutionSet> Solve(Expr equation, Symbol x, SolveOptions? options = null, MathContext? math = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(equation);
        ArgumentNullException.ThrowIfNull(x);
        var c = new Ctx(math ?? MathContext.Default, budget ?? new Budget(maxSteps: 50_000, maxTime: TimeSpan.FromSeconds(30)), options ?? new SolveOptions());
        equation = Canon(equation);
        if (equation is Apply { Operator: var op, Arguments: [var l, var r] } && (op == Operators.Lt || op == Operators.Le || op == Operators.Gt || op == Operators.Ge || op == Operators.Ne))
        {
            return SolveInequality(equation, op, l, r, x, c);
        }
        if (equation is not Apply { Operator: var eq, Arguments: [var left, var right] } || eq != Operators.Eq)
        {
            return new Outcome<SolutionSet>.Unevaluated(equation, "Expected an equation or an inequality.");
        }

        var f = Canon(Add(left, Negate(right)));
        var start = equation;
        var cand = Candidates(f, x, 0, c);
        if (cand is null) return Numeric(f, x, start, c);
        return Finish(f, x, start, cand, c);
    }

    // ----- Verification and assembly -----

    private static Outcome<SolutionSet> Finish(Expr f, Symbol x, Expr start, Cand cand, Ctx c)
    {
        if (cand.All)
        {
            var domain = NaturalDomain.Of(f, x, c.Math) is Outcome<Expr>.Success { Value: var d } ? d : null;
            var set = domain is null || domain is Constant { Id: ConstantId.Reals } ? SolutionSet.All : new SolutionSet(SolutionKind.Condition, domain) { IsComplete = true };
            Record(c, "alg.eq.equiv-add", "identity", start, set.Set);
            return Wrap(start, set, c);
        }

        var accepted = new List<Expr>();
        foreach (var p in Dedupe(cand.Points.Select(q => Polish(q, c))))
        {
            if (!c.Budget.TryCharge()) return new Outcome<SolutionSet>.Unevaluated(start, "Budget exceeded.");
            var (ok, reason) = Check(f, x, p, c);
            if (ok) accepted.Add(p);
            else Record(c, "alg.eq.extraneous", "reject-candidate", new Apply(Operators.Eq, [x, p]), new Constant(ConstantId.False), ("candidate", p), ("reason", new Symbol(reason.Replace(' ', '_'))));
        }
        var families = new List<ImageFamily>();
        foreach (var family in cand.Families)
        {
            if (FamilyHolds(f, x, family, c)) families.Add(family);
            else Record(c, "alg.eq.extraneous", "reject-family", new Apply(Operators.Eq, [x, family.Element]), new Constant(ConstantId.False), ("candidate", family.Element), ("reason", new Symbol("fails_the_equation")));
        }

        if (c.Options.IntervalLow is { } lo && c.Options.IntervalHigh is { } hi) return Wrap(start, Restrict(accepted, families, lo, hi, cand), c);
        var sorted = accepted.OrderBy(p => Value(p) ?? double.MaxValue).ToList();
        SolutionSet result;
        if (families.Count == 0)
        {
            result = sorted.Count == 0 ? SolutionSet.Empty : new SolutionSet(SolutionKind.Finite, new SetLiteral([.. sorted])) { Points = [.. sorted], IsApproximate = cand.Approximate && sorted.Any(p => p is Float) };
        }
        else
        {
            Expr setExpr = new SetLiteral([.. sorted]);
            var images = families.Select(fam => (Expr)new Bind(Binder.ImageSet, [fam.Parameter], [new Constant(ConstantId.Integers)], fam.Element)).ToList();
            var parts = (sorted.Count > 0 ? new List<Expr> { setExpr } : []).Concat(images).ToList();
            setExpr = parts.Count == 1 ? parts[0] : new Apply(Operators.Union, [.. parts]);
            result = new SolutionSet(SolutionKind.Image, setExpr) { Points = [.. sorted], Families = [.. families], IsApproximate = cand.Approximate };
        }
        Record(c, "alg.eq.equiv-add", "solution-set", start, result.Set);
        return Wrap(start, result, c);
    }

    private static Outcome<SolutionSet> Wrap(Expr start, SolutionSet set, Ctx c)
    {
        var derivation = new Derivation(start, set.Set, [.. c.Steps]);
        return Outcome.Ok(set, derivation, c.Provisos, Verification.Verified);
    }

    private static IEnumerable<Expr> Dedupe(IEnumerable<Expr> points)
    {
        var kept = new List<(Expr Point, double? Value)>();
        foreach (var p in points)
        {
            var v = Value(p);
            if (kept.Any(k => k.Point.Equals(p) || (v is { } a && k.Value is { } b && Math.Abs(a - b) <= 1e-12 * Math.Max(1, Math.Abs(a))))) continue;
            kept.Add((p, v));
        }
        return kept.Select(k => k.Point);
    }

    private static double? Value(Expr e) => e.FreeSymbols.Count == 0 && Evaluator.N(e) is Outcome<double>.Success { Value: var v } && double.IsFinite(v) ? v : null;

    // Substitutes a candidate back: it must be defined (a real number) and make the expression zero (exactly, or within 1e-10 for approximate roots).
    private static (bool Ok, string Reason) Check(Expr f, Symbol x, Expr p, Ctx c)
    {
        var substituted = f.Substitute(x, p);
        var exact = Evaluator.Evaluate(substituted, null, c.Math.NormalizeOptions) is Outcome<Expr>.Success { Value: var v } ? v : Canon(substituted);
        if (exact.Walk().Any(w => w.Expr is Constant { Id: ConstantId.Undefined or ConstantId.ComplexInfinity or ConstantId.PositiveInfinity or ConstantId.NegativeInfinity } or Apply { Operator.Id: "pow", Arguments: [Number { Value.Sign: 0 }, Number { Value.Sign: < 0 }] })) return (false, "undefined at the candidate");
        if (exact.FreeSymbols.Count == 0)
        {
            switch (Evaluator.N(substituted))
            {
                case Outcome<double>.Success { Value: var n }:
                    if (!double.IsFinite(n)) return (false, "undefined at the candidate");
                    if (p is Float) return Math.Abs(n) <= 1e-10 * Math.Max(1, ScaleOf(f, x, p)) ? (true, string.Empty) : (false, "does not satisfy the equation");
                    break;
                case Outcome<double>.Failed:
                    return (false, "not a real number at the candidate");
            }
        }
        if (exact is Number { Value.Sign: 0 }) return (true, string.Empty);
        var zero = ZeroTest.Test(exact, c.Math);
        return zero is ZeroTestResult.Zero or ZeroTestResult.ProbablyZero ? (true, string.Empty) : (false, "does not satisfy the equation");
    }

    // The size of the terms at the candidate, so that the 1e-10 tolerance is relative for large coefficients.
    private static double ScaleOf(Expr f, Symbol x, Expr p)
    {
        var terms = f is Apply { Operator.Id: "add", Arguments: var args } ? args : [f];
        return terms.Select(t => Evaluator.N(t.Substitute(x, p)) is Outcome<double>.Success { Value: var v } && double.IsFinite(v) ? Math.Abs(v) : 0).DefaultIfEmpty(0).Max();
    }

    private static bool FamilyHolds(Expr f, Symbol x, ImageFamily family, Ctx c)
    {
        var tested = 0;
        for (var k = -3; k <= 3; k++)
        {
            var point = Canon(family.Element.Substitute(family.Parameter, Num(k)));
            if (Value(point) is null) continue;
            var (ok, _) = Check(f, x, point, c);
            if (!ok) return false;
            tested++;
        }
        return tested > 0;
    }

    private static SolutionSet Restrict(List<Expr> points, List<ImageFamily> families, double lo, double hi, Cand cand)
    {
        var all = new List<(Expr Point, double Value)>();
        foreach (var p in points)
        {
            if (Value(p) is { } v && v >= lo - 1e-12 && v <= hi + 1e-12) all.Add((p, v));
        }
        foreach (var family in families)
        {
            for (var k = -400; k <= 400; k++)
            {
                var point = Canon(family.Element.Substitute(family.Parameter, Num(k)));
                if (Value(point) is { } v && v >= lo - 1e-12 && v <= hi + 1e-12 && !all.Any(a => Math.Abs(a.Value - v) < 1e-12)) all.Add((point, v));
            }
        }
        var sorted = all.OrderBy(a => a.Value).Select(a => a.Point).ToList();
        return sorted.Count == 0 ? SolutionSet.Empty : new SolutionSet(SolutionKind.Finite, new SetLiteral([.. sorted])) { Points = [.. sorted], IsApproximate = cand.Approximate };
    }

    // Anything else: sign changes of the expression on a window, refined with Brent's method, reported as a condition set with the roots attached.
    private static Outcome<SolutionSet> Numeric(Expr f, Symbol x, Expr start, Ctx c)
    {
        var lo = c.Options.IntervalLow ?? -100.0;
        var hi = c.Options.IntervalHigh ?? 100.0;
        if (f.FreeSymbols.Any(s => !s.Equals(x))) return new Outcome<SolutionSet>.Unevaluated(start, "No method solves this equation.");
        double? At(double t) => Evaluator.N(f, new Dictionary<Symbol, double> { [x] = t }) is Outcome<double>.Success { Value: var v } && double.IsFinite(v) ? v : null;
        var roots = new List<double>();
        const int Samples = 8000;
        double? previous = At(lo);
        var previousX = lo;
        for (var i = 1; i <= Samples; i++)
        {
            var t = lo + (hi - lo) * i / Samples;
            var value = At(t);
            if (previous is { } a && value is { } b && Math.Sign(a) != Math.Sign(b) && a != 0)
            {
                var solved = Roots.Brent<double>(s => At(s) ?? double.NaN, previousX, t, new StoppingCriteria(AbsoluteTolerance: 1e-14, RelativeTolerance: 1e-14, MaxIterations: 200));
                // A sign change at a pole is not a root.
                if (solved.Converged && Math.Abs(At(solved.Root) ?? double.PositiveInfinity) < 1e-8 * (1 + Math.Abs(a) + Math.Abs(b))) roots.Add(solved.Root);
            }
            else if (value is { } z && z == 0)
            {
                roots.Add(t);
            }
            previous = value;
            previousX = t;
        }
        var distinct = roots.OrderBy(r => r).Where((r, i) => i == 0 || Math.Abs(r - roots.OrderBy(q => q).ElementAt(i - 1)) > 1e-9).ToList();
        var condition = new Bind(Binder.SetBuilder, [x], [new Constant(ConstantId.Reals)], new Apply(Operators.Eq, [f, Num(0)]));
        Record(c, "alg.eq.equiv-add", "numeric", start, condition);
        var set = new SolutionSet(SolutionKind.Condition, condition) { Approximations = [.. distinct], IsApproximate = true, IsComplete = false };
        return Wrap(start, set, c);
    }

    // ----- Candidate generation -----

    private static Cand? Candidates(Expr f, Symbol x, int depth, Ctx c)
    {
        if (depth > 8 || !c.Budget.TryCharge()) return null;
        f = Canon(f);
        if (!f.FreeSymbols.Contains(x))
        {
            var zero = ZeroTest.Test(f, c.Math);
            return zero is ZeroTestResult.Zero ? Cand.Everything : zero is ZeroTestResult.NonZero ? Cand.None : null;
        }
        foreach (var method in new Func<Expr, Symbol, int, Ctx, Cand?>[] { ZeroProduct, PolynomialMethod, AbsoluteValue, Radical, Exponential, Logarithmic, Trigonometric })
        {
            if (method(f, x, depth, c) is { } found) return found;
        }
        return null;
    }

    // A product of factors is zero when a factor is: each factor is solved, and the candidates are checked afterwards.
    private static Cand? ZeroProduct(Expr f, Symbol x, int depth, Ctx c)
    {
        if (f is not Apply { Operator: var op, Arguments: var args } || op != Operators.Mul) return null;
        var numerators = new List<Expr>();
        foreach (var a in args)
        {
            if (!a.FreeSymbols.Contains(x)) continue;
            if (a is Apply { Operator.Id: "pow", Arguments: [var b, Number { Value: var e }] } && e.Sign < 0) continue;
            numerators.Add(a is Apply { Operator.Id: "pow", Arguments: [var b2, Number { Value: var e2 }] } && e2.IsInteger && e2.Sign > 0 ? b2 : a);
        }
        if (numerators.Count < 2) return null;
        Record(c, "alg.prop.zero-product", "zero-product", EqZero(f), Disjunction(numerators.Select(e => (Expr)EqZero(e))));
        var total = Cand.None;
        foreach (var factor in numerators)
        {
            if (Candidates(factor, x, depth + 1, c) is not { } part) return null;
            total = total.Union(part);
        }
        return total;
    }

    // ----- Polynomial and rational equations -----

    private static Cand? PolynomialMethod(Expr f, Symbol x, int depth, Ctx c)
    {
        if (f.FreeSymbols.Count == 1 && PolynomialConversion.TryToRationalFunction(f, x, out var n, out var d))
        {
            if (n.IsZero) return Cand.Everything;
            if (d.Degree >= 1)
            {
                var numerator = PolynomialConversion.FromPolynomial(n, x);
                Record(c, "alg.eq.rational", "clear-denominators", EqZero(f), EqZero(Canon(numerator)), ("lcd", Canon(PolynomialConversion.FromPolynomial(d, x))));
                Record(c, "alg.eq.mul-by-variable", "may-add-solutions", EqZero(Canon(numerator)), EqZero(Canon(numerator)));
            }
            return n.Degree == 0 ? Cand.None : SolvePolynomial(n, x, c, f);
        }
        return LiteralPolynomial(f, x, c);
    }

    // Symbolic coefficients (literal equations): a·x + b = 0 and a·x² + b·x + c = 0, with conditions on the parameters stated as provisos.
    private static Cand? LiteralPolynomial(Expr f, Symbol x, Ctx c)
    {
        if (PolynomialConversion.Coefficients(f, x) is not { } coefficients || coefficients.Keys.Any(k => k < 0 || k > 2)) return null;
        coefficients.TryGetValue(0, out var c0);
        coefficients.TryGetValue(1, out var c1);
        coefficients.TryGetValue(2, out var c2);
        c0 ??= Num(0);
        c1 ??= Num(0);
        c2 ??= Num(0);
        if (c2 is Number { Value.Sign: 0 })
        {
            if (c1 is Number { Value.Sign: 0 }) return null;
            var nonZero = new Apply(Operators.Ne, [c1, Num(0)]);
            switch (c.Math.Ask(nonZero))
            {
                case Truth.False: return Cand.None;
                case Truth.Unknown: c.Provisos = c.Provisos.Add(nonZero); break;
            }
            var root = Canon(Mul(Negate(c0), Pow(c1, Num(-1))));
            Record(c, "alg.eq.linear", "linear", EqZero(f), new Apply(Operators.Eq, [x, root]));
            return Cand.Of([root]);
        }
        var lead = new Apply(Operators.Ne, [c2, Num(0)]);
        if (c.Math.Ask(lead) == Truth.False) return null;
        if (c.Math.Ask(lead) == Truth.Unknown) c.Provisos = c.Provisos.Add(lead);
        var discriminant = Canon(Add(Pow(c1, Num(2)), Mul(Num(-4), c2, c0)));
        var sqrt = Pow(discriminant, Half);
        var plus = Canon(Mul(Add(Negate(c1), sqrt), Pow(Mul(Num(2), c2), Num(-1))));
        var minus = Canon(Mul(Add(Negate(c1), Negate(sqrt)), Pow(Mul(Num(2), c2), Num(-1))));
        var real = new Apply(Operators.Ge, [discriminant, Num(0)]);
        switch (c.Math.Ask(real))
        {
            case Truth.False: Record(c, "alg.quad.root-nature", "negative-discriminant", EqZero(f), new Constant(ConstantId.EmptySet), ("D", discriminant)); return Cand.None;
            case Truth.Unknown: c.Provisos = c.Provisos.Add(real); break;
        }
        Record(c, "alg.quad.quadratic-formula", "quadratic-formula", EqZero(f), new Apply(Operators.Or, [new Apply(Operators.Eq, [x, plus]), new Apply(Operators.Eq, [x, minus])]), ("D", discriminant));
        return Cand.Of([plus, minus]);
    }

    private static Cand SolvePolynomial(Polynomial<BigRational> p, Symbol x, Ctx c, Expr original)
    {
        var points = new List<Expr>();
        var approximate = false;
        var polynomialExpr = Canon(PolynomialConversion.FromPolynomial(p, x));

        if (p.Degree == 1)
        {
            var root = Canon(Num(-p[0] / p[1]));
            Record(c, "alg.eq.linear", "linear", EqZero(polynomialExpr), new Apply(Operators.Eq, [x, root]));
            return Cand.Of([root]);
        }
        if (p.Degree == 2)
        {
            points.AddRange(Quadratic(p, x, c, polynomialExpr));
            return Cand.Of(points);
        }

        var rest = p;
        var roots = PolynomialAlgorithms.RationalRoots(p);
        if (!roots.Roots.IsEmpty)
        {
            foreach (var (root, multiplicity) in roots.Roots)
            {
                points.Add(Num(root));
                var linear = new Polynomial<BigRational>([-root, BigRational.One]);
                for (var i = 0; i < multiplicity; i++) rest = rest.DivRem(linear).Quotient;
            }
            Record(c, "alg.poly.rational-root", "rational-roots", EqZero(polynomialExpr), Disjunction(roots.Roots.Select(r => (Expr)new Apply(Operators.Eq, [x, Num(r.Root)]))));
            Record(c, "alg.eq.zero-product-solve", "factor", EqZero(polynomialExpr), EqZero(Canon(Mul(PolynomialConversion.FromPolynomial(rest, x), Num(1)))));
        }

        var complete = true;
        if (rest.Degree >= 1)
        {
            var decomposition = PolynomialAlgorithms.SquareFree(rest);
            foreach (var (factor, _) in decomposition.Factors)
            {
                var found = FactorRoots(factor, x, c, ref complete, ref approximate);
                points.AddRange(found);
            }
        }
        return Cand.Of(points, complete, approximate);
    }

    private static List<Expr> Quadratic(Polynomial<BigRational> p, Symbol x, Ctx c, Expr polynomialExpr)
    {
        var (a, b, k) = (p[2], p[1], p[0]);
        var discriminant = b * b - 4 * a * k;
        var method = c.Options.Method;
        var points = new List<Expr>();

        if (method == SolveMethod.Factoring)
        {
            var roots = PolynomialAlgorithms.RationalRoots(p);
            if (roots.Roots.Sum(r => r.Multiplicity) == 2)
            {
                foreach (var r in roots.Roots) points.Add(Num(r.Root));
                Record(c, "alg.eq.zero-product-solve", "factoring", EqZero(polynomialExpr), Disjunction(points.Select(r => (Expr)new Apply(Operators.Eq, [x, r]))));
                return points;
            }
        }
        if (discriminant.Sign < 0)
        {
            Record(c, "alg.quad.root-nature", "negative-discriminant", EqZero(polynomialExpr), new Constant(ConstantId.EmptySet), ("D", Num(discriminant)));
            return points;
        }
        var twoA = a * 2;
        if (method == SolveMethod.CompletingSquare)
        {
            // a(x + b/(2a))² = (b² − 4ac)/(4a)
            var h = b / twoA;
            var square = new Apply(Operators.Eq, [Canon(Mul(Num(a), Pow(Add(x, Num(h)), Num(2)))), Num(discriminant / (4 * a))]);
            Record(c, "alg.quad.completing-the-square", "completing-the-square", EqZero(polynomialExpr), square, ("h", Num(h)));
        }
        else
        {
            Record(c, "alg.quad.quadratic-formula", "quadratic-formula", EqZero(polynomialExpr), new Apply(Operators.Eq, [x, Canon(Mul(Add(Negate(Num(b)), Pow(Num(discriminant), Half)), Pow(Num(twoA), Num(-1))))]), ("D", Num(discriminant)));
        }
        var root = ExactValues.Sqrt(discriminant);
        var center = Num(-b / twoA);
        points.Add(Canon(Add(center, Mul(Num(1 / twoA), root))));
        if (discriminant.Sign > 0) points.Add(Canon(Add(center, Mul(Num(-1 / twoA), root))));
        return points;
    }

    // Roots of a square-free polynomial without rational roots: quadratic factors in closed form, equations quadratic in form, otherwise numeric real roots.
    private static List<Expr> FactorRoots(Polynomial<BigRational> g, Symbol x, Ctx c, ref bool complete, ref bool approximate)
    {
        var expr = Canon(PolynomialConversion.FromPolynomial(g, x));
        if (g.Degree == 1) return [Num(-g[0] / g[1])];
        if (g.Degree == 2) return Quadratic(g, x, c, expr);

        // Quadratic in form: only powers that are multiples of k.
        var k = Enumerable.Range(0, g.Degree + 1).Where(i => g[i] != BigRational.Zero && i > 0).Aggregate(0, (acc, i) => (int)BigInteger.GreatestCommonDivisor(acc, i));
        if (k >= 2 && g.Degree / k == 2 || (k >= 2 && g.Degree / k >= 3 && false))
        {
            var u = new Polynomial<BigRational>(Enumerable.Range(0, g.Degree / k + 1).Select(i => g[i * k]));
            Record(c, "alg.eq.quadratic-in-form", "quadratic-in-form", EqZero(expr), EqZero(Canon(PolynomialConversion.FromPolynomial(u, new Symbol("u")))), ("k", Num(k)));
            var inner = new Ctx(c.Math, c.Budget, c.Options);
            var uRoots = SolvePolynomial(u, new Symbol("u"), inner, EqZero(expr));
            var result = new List<Expr>();
            foreach (var r in uRoots.Points)
            {
                var value = Value(r);
                if (value is null) continue;
                if (k % 2 == 0)
                {
                    if (value < 0) continue;
                    if (value == 0) { result.Add(Num(0)); continue; }
                    var root = Canon(Pow(r, Num(new BigRational(1) / new BigRational(k))));
                    result.Add(root);
                    result.Add(Canon(Negate(root)));
                }
                else
                {
                    var magnitude = Canon(Pow(value < 0 ? Negate(r) : r, Num(new BigRational(1) / new BigRational(k))));
                    result.Add(value < 0 ? Canon(Negate(magnitude)) : magnitude);
                }
            }
            approximate |= result.Any(r => r is Float);
            return result;
        }

        // x^n = c: the real n-th roots in closed form.
        if (g[0] != BigRational.Zero && Enumerable.Range(1, g.Degree - 1).All(i => g[i] == BigRational.Zero))
        {
            var n = g.Degree;
            var target = -g[0] / g[n];
            var root = Canon(Pow(Num(BigRational.Abs(target)), Num(new BigRational(1) / new BigRational(n))));
            Record(c, "alg.eq.quadratic-in-form", "binomial-root", EqZero(expr), new Apply(Operators.Eq, [Pow(x, Num(n)), Num(target)]), ("n", Num(n)));
            if (n % 2 == 1) return [target.Sign < 0 ? Canon(Negate(root)) : root];
            return target.Sign < 0 ? [] : [root, Canon(Negate(root))];
        }

        // Numeric real roots.
        var numeric = RealRoots(g);
        approximate = true;
        Record(c, "alg.poly.rational-root", "numeric-roots", EqZero(expr), Disjunction(numeric.Select(r => (Expr)new Apply(Operators.Eq, [x, r]))));
        return numeric;
    }

    private static List<Expr> RealRoots(Polynomial<BigRational> g)
    {
        var d = g.Map(r => (double)r);
        var all = PolynomialAlgorithms.AberthRoots(d);
        var found = new List<double>();
        foreach (var z in all.Roots)
        {
            if (Math.Abs(z.Imaginary) > 1e-7 * Math.Max(1, Math.Abs(z.Real))) continue;
            var r = z.Real;
            // Newton polishing in double precision.
            for (var i = 0; i < 20; i++)
            {
                var value = d.Evaluate(r);
                var slope = d.Derivative().Evaluate(r);
                if (slope == 0 || !double.IsFinite(slope)) break;
                var next = r - value / slope;
                if (!double.IsFinite(next) || Math.Abs(next - r) <= 1e-16 * Math.Max(1, Math.Abs(r))) { r = double.IsFinite(next) ? next : r; break; }
                r = next;
            }
            if (!found.Any(q => Math.Abs(q - r) < 1e-9 * Math.Max(1, Math.Abs(r)))) found.Add(r);
        }
        return [.. found.OrderBy(r => r).Select(r => (Expr)new Float(r))];
    }

    // Multiplies out and combines (sqrt(u))^2 = u and the like. These are candidates only: the check afterwards rejects what raising to a power added.
    private static Expr ExpandPowers(Expr e, Ctx c)
    {
        for (var round = 0; round < 3; round++)
        {
            var before = e;
            if (Mathesis.Simplification.Transforms.Expand.Run(e, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var expanded }) e = expanded;
            e = Canon(TopDown(e, n => n is Apply { Operator.Id: "pow", Arguments: [Apply { Operator.Id: "pow", Arguments: [var b, Number { Value: var r1 }] }, Number { Value: var r2 }] } && !r1.IsInteger && (r1 * r2).IsInteger ? Pow(b, Num(r1 * r2)) : null));
            if (e.Equals(before)) break;
        }
        return e;
    }

    // ----- Absolute value -----

    private static Cand? AbsoluteValue(Expr f, Symbol x, int depth, Ctx c)
    {
        var node = f.Walk().Select(w => w.Expr).OfType<Apply>().FirstOrDefault(a => a.Operator == Operators.Abs && a.Arguments[0].FreeSymbols.Contains(x));
        if (node is null) return null;
        var u = node.Arguments[0];
        var positive = Canon(f.Transform(e => e.Equals(node) ? u : null));
        var negative = Canon(f.Transform(e => e.Equals(node) ? Negate(u) : null));
        Record(c, "alg.eq.abs", "cases", EqZero(f), new Apply(Operators.Or, [EqZero(positive), EqZero(negative)]), ("u", u));
        if (Candidates(positive, x, depth + 1, c) is not { } a || Candidates(negative, x, depth + 1, c) is not { } b) return null;
        return a.Union(b);
    }

    // ----- Radical equations -----

    private static Cand? Radical(Expr f, Symbol x, int depth, Ctx c)
    {
        var radicals = f.Walk().Select(w => w.Expr).OfType<Apply>().Where(a => a.Operator == Operators.Pow && a.Arguments[1] is Number { Value.IsInteger: false } && a.Arguments[0].FreeSymbols.Contains(x)).ToList();
        if (radicals.Count == 0) return null;
        var node = radicals.OrderBy(r => r.LeafCount).First();
        var (g, exponent) = (node.Arguments[0], ((Number)node.Arguments[1]).Value);
        var w = Fresh(f);

        // The radicand is x itself: substitute w = x^(1/q) and solve the polynomial in w.
        if (g.Equals(x) && radicals.All(r => r.Arguments[0].Equals(x)))
        {
            var q = (int)exponent.Denominator;
            var inWTerms = Canon(TopDown(f, e => e is Apply { Operator.Id: "pow", Arguments: [var b, Number { Value: var ee }] } && b.Equals(x) && !ee.IsInteger ? Pow(w, Num(ee * new BigRational(q))) : e.Equals(x) ? Pow(w, Num(q)) : null));
            if (!inWTerms.FreeSymbols.Contains(x))
            {
                Record(c, "alg.eq.quadratic-in-form", "substitute-radical", EqZero(f), EqZero(inWTerms), ("w", Pow(x, Num(new BigRational(1) / new BigRational(q)))));
                if (Candidates(inWTerms, w, depth + 1, c) is { } inW)
                {
                    var points = new List<Expr>();
                    foreach (var r in inW.Points)
                    {
                        if (Value(r) is { } v && (q % 2 == 1 || v >= 0)) points.Add(Canon(Pow(r, Num(q))));
                    }
                    return Cand.Of(points, inW.Complete, inW.Approximate);
                }
            }
        }

        // Isolate the radical (F = A + B·w), raise both sides to the power that removes it.
        var inW2 = Canon(f.Transform(e => e.Equals(node) ? w : null));
        if (inW2.FreeSymbols.Contains(w) && PolynomialConversion.Coefficients(inW2, w) is { } coefficients && coefficients.Keys.All(k => k is 0 or 1) && coefficients.TryGetValue(1, out var b1) && b1 is not Number { Value.Sign: 0 })
        {
            coefficients.TryGetValue(0, out var a0);
            a0 ??= Num(0);
            var isolated = Canon(Mul(Negate(a0), Pow(b1, Num(-1))));
            var q = (int)exponent.Denominator;
            var p = (int)exponent.Numerator;
            Record(c, "alg.eq.radical", "isolate-radical", EqZero(f), new Apply(Operators.Eq, [node, isolated]));
            var raised = ExpandPowers(Canon(Add(Pow(g, Num(p)), Negate(Pow(isolated, Num(q))))), c);
            Record(c, "alg.eq.square-both-sides", "raise-to-power", new Apply(Operators.Eq, [node, isolated]), EqZero(raised), ("n", Num(q)));
            if (!raised.FreeSymbols.Contains(x)) return null;
            return Candidates(raised, x, depth + 1, c);
        }
        return null;
    }
}
