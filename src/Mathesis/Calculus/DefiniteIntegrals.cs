using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Numerics;
using Mathesis.Numerics.Integration;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Calculus;

public static partial class Integrator
{
    /// <summary>
    /// The definite integral of <paramref name="f"/> over <c>[a, b]</c> (the bounds are numbers, <c>oo</c> or <c>-oo</c>): the Fundamental Theorem of
    /// Calculus (catalog <c>calc.ftc.part2</c>) applied to the verified antiderivative, after checking that the antiderivative is continuous on the
    /// interval. Where it is not (a pole, a jump of a Weierstrass antiderivative, an infinite bound) the interval is split at the discontinuities and the
    /// pieces use one-sided limits of the antiderivative (<c>calc.improper.type1</c> and <c>type2</c>); a divergent integral comes back as
    /// Unevaluated. The value is always compared with Gauss–Kronrod quadrature.
    /// </summary>
    /// <returns>Success with the exact value, <see cref="Verification.NumericallyConsistent"/>; Unevaluated when there is no antiderivative, the integral diverges, or the quadrature disagrees.</returns>
    public static Outcome<Expr> IntegrateDefinite(Expr f, Symbol x, Expr a, Expr b, MathContext? math = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(x);
        math ??= MathContext.Default;
        a = Normalizer.Canonical(a);
        b = Normalizer.Canonical(b);
        var start = new Bind(Binder.Integral, [x], [a, b], f);
        if (DomainSets.ToDouble(a) is not { } lo || DomainSets.ToDouble(b) is not { } hi) return new Outcome<Expr>.Unevaluated(start, "The bounds must be numbers.");
        if (lo == hi) return Outcome.Ok<Expr>(new Number(BigRational.Zero), null, Provisos.None, Verification.Verified);
        if (lo > hi)
        {
            var reversed = IntegrateDefinite(f, x, b, a, math, budget);
            return reversed is Outcome<Expr>.Success { Value: var v } success
                ? Outcome.Ok<Expr>(Normalizer.Canonical(new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), v])), success.Steps, success.Provisos, success.Check)
                : reversed;
        }

        if (Integrate(f, x, math, budget) is not Outcome<Expr>.Success { Value: var antiderivative, Steps: Derivation antiderivativeDerivation, Provisos: var provisos })
        {
            return new Outcome<Expr>.Unevaluated(start, "No verified antiderivative was found.");
        }
        var domainOutcome = NaturalDomain.Of(antiderivative, x, math);
        if (domainOutcome is not Outcome<Expr>.Success { Value: var domain }) return new Outcome<Expr>.Unevaluated(start, "The domain of the antiderivative is not known.");

        // The conditions the antiderivative needed must hold on the interval.
        if (!ProvisosHold(provisos, x, lo, hi)) return new Outcome<Expr>.Unevaluated(start, "The conditions of the antiderivative do not hold on the whole interval.");

        // Breakpoints: the interval ends and the discontinuities of the antiderivative inside.
        var points = new List<Expr> { Normalizer.Canonical(a) };
        foreach (var c in Discontinuities(domain, x, lo, hi)) points.Add(c);
        points.Add(Normalizer.Canonical(b));
        // Between consecutive breakpoints the antiderivative must be continuous: the domain has to contain the open piece, or the claim is not made.
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var from = DomainSets.ToDouble(points[i])!.Value;
            var to = DomainSets.ToDouble(points[i + 1])!.Value;
            var width = double.IsInfinity(to - from) ? 1.0 : to - from;
            var tiny = 1e-9 * width;
            if (!DomainSets.Contains(domain, double.IsInfinity(from) ? -1e12 : from + tiny, double.IsInfinity(to) ? 1e12 : to - tiny))
            {
                return new Outcome<Expr>.Unevaluated(start, "The continuity of the antiderivative on the interval could not be established.");
            }
        }
        var improper = points.Count > 2 || double.IsInfinity(lo) || double.IsInfinity(hi);

        var total = new List<Expr>();
        for (var i = 0; i + 1 < points.Count; i++)
        {
            if (EndValue(antiderivative, x, points[i + 1], domain, true, math) is not { } upper || EndValue(antiderivative, x, points[i], domain, false, math) is not { } lower)
            {
                return new Outcome<Expr>.Unevaluated(start, "The integral diverges or an endpoint value could not be found.");
            }
            total.Add(new Apply(Operators.Add, [upper, new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), lower])]));
        }
        var exact = Normalizer.Canonical(total.Count == 1 ? total[0] : new Apply(Operators.Add, [.. total]));
        exact = Simplifier.Simplify(exact, math, null, new Budget(maxSteps: 2000, maxTime: TimeSpan.FromSeconds(5))) is Outcome<Expr>.Success { Value: var simple } ? simple : exact;
        if (exact.Walk().Any(w => w.Expr is Constant { Id: ConstantId.Undefined or ConstantId.PositiveInfinity or ConstantId.NegativeInfinity or ConstantId.ComplexInfinity })) return new Outcome<Expr>.Unevaluated(start, "The integral diverges.");

        // The quadrature cross-check.
        if (Evaluator.N(exact) is not Outcome<double>.Success { Value: var value } || !double.IsFinite(value)) return new Outcome<Expr>.Unevaluated(start, "The value is not a real number.");
        var numeric = Quadrature.Infinite<double>(t => Evaluator.N(f, new Dictionary<Symbol, double> { [x] = t }) is Outcome<double>.Success { Value: var y } ? y : double.NaN, lo, hi);
        if (numeric.Converged && Math.Abs(numeric.Value - value) > Math.Max(1e-7 * Math.Max(1, Math.Abs(value)), 20 * numeric.ErrorEstimate))
        {
            return new Outcome<Expr>.Unevaluated(start, $"The exact value {value} disagrees with quadrature ({numeric.Value}).");
        }
        if (!numeric.Converged && double.IsNaN(numeric.Value)) return new Outcome<Expr>.Unevaluated(start, "The quadrature cross-check could not be evaluated.");

        var entry = improper ? (double.IsInfinity(lo) || double.IsInfinity(hi) ? "calc.improper.type1" : "calc.improper.type2") : "calc.ftc.part2";
        var catalog = KnowledgeBase.Default;
        var step = new Step(new EntryId(entry), "integrate/definite", start, exact, ExprPath.Root, Bindings.Empty, provisos, new ExplanationKey(entry, [new("F", antiderivative), new("a", a), new("b", b)]), catalog.Get(entry).Level ?? CurriculumLevel.Calculus1, antiderivativeDerivation);
        return Outcome.Ok(exact, new Derivation(start, exact, [step]), provisos, Verification.NumericallyConsistent);
    }

    // The value of F at a breakpoint: direct substitution where F is defined there, otherwise the one-sided limit from inside the piece (finite only).
    private static Expr? EndValue(Expr f, Symbol x, Expr point, Expr domain, bool fromLeft, MathContext math)
    {
        var infinite = point is Constant { Id: ConstantId.PositiveInfinity or ConstantId.NegativeInfinity } || DomainSets.ToDouble(point) is { } d && double.IsInfinity(d);
        if (!infinite && DomainSets.ToDouble(point) is { } p && DomainSets.Contains(domain, p, p))
        {
            var substituted = f.Substitute(x, point);
            var exact = Evaluator.Evaluate(substituted, null, math.NormalizeOptions) is Outcome<Expr>.Success { Value: var v } ? v : Normalizer.Canonical(substituted);
            if (!exact.Walk().Any(w => w.Expr is Constant { Id: ConstantId.Undefined or ConstantId.ComplexInfinity or ConstantId.PositiveInfinity or ConstantId.NegativeInfinity })) return exact;
        }
        var side = infinite ? LimitDirection.Both : fromLeft ? LimitDirection.FromLeft : LimitDirection.FromRight;
        return Limits.Limit(f, x, point, side, math) is Outcome<LimitResult>.Success { Value: LimitResult.Finite { Value: var limit } } ? limit : null;
    }

    // Points inside (lo, hi) where the domain has a periodic exclusion {e(k) | k in Z}: exact expressions e(k).
    private static IEnumerable<Expr> Discontinuities(Expr domain, Symbol x, double lo, double hi)
    {
        var excluded = new List<(double Value, Expr Point)>();
        void Collect(Expr set)
        {
            switch (set)
            {
                case Apply { Operator.Id: "setminus", Arguments: [var a, var b] }:
                    Collect(a);
                    Excluded(b);
                    break;
                case Apply { Operator.Id: "union", Arguments: var parts }:
                    foreach (var part in parts) Collect(part);
                    break;
                case IntervalLiteral i:
                    // Open ends of the pieces are discontinuities of the function that live at the piece boundary.
                    foreach (var (end, closed) in new[] { (i.Lower, i.LowerClosed), (i.Upper, i.UpperClosed) })
                    {
                        if (!closed && DomainSets.ToDouble(end) is { } v && v > lo && v < hi && double.IsFinite(v)) excluded.Add((v, end));
                    }
                    break;
            }
        }
        void Excluded(Expr set)
        {
            if (set is Bind { Bound: [var k], Body: var body })
            {
                for (var n = -2000; n <= 2000; n++)
                {
                    if (Evaluator.N(body, new Dictionary<Symbol, double> { [k] = n }) is Outcome<double>.Success { Value: var v } && v > lo && v < hi) excluded.Add((v, Normalizer.Canonical(body.Substitute(k, new Number(n)))));
                }
            }
            else if (set is Apply { Operator.Id: "set", Arguments: var elements })
            {
                foreach (var e in elements)
                {
                    if (DomainSets.ToDouble(e) is { } v && v > lo && v < hi) excluded.Add((v, e));
                }
            }
        }
        Collect(domain);
        return excluded.OrderBy(e => e.Value).DistinctBy(e => Math.Round(e.Value, 12)).Select(e => e.Point);
    }

    private static bool ProvisosHold(Provisos provisos, Symbol x, double lo, double hi)
    {
        var from = double.IsInfinity(lo) ? -50.0 : lo;
        var to = double.IsInfinity(hi) ? 50.0 : hi;
        foreach (var p in provisos.OfType<Expr>())
        {
            if (p is not Apply { Operator: var op, Arguments: [var l, var r] } || !p.FreeSymbols.All(s => s.Equals(x))) continue;
            for (var i = 1; i < 60; i++)
            {
                var t = from + (to - from) * i / 60.0;
                var values = new Dictionary<Symbol, double> { [x] = t };
                if (Evaluator.N(l, values) is not Outcome<double>.Success { Value: var a } || Evaluator.N(r, values) is not Outcome<double>.Success { Value: var b }) continue;
                var ok = op == Operators.Gt ? a > b : op == Operators.Ge ? a >= b : op == Operators.Lt ? a < b : op == Operators.Le ? a <= b : op != Operators.Ne || Math.Abs(a - b) > 1e-12;
                if (!ok)
                {
                    // A proviso that fails only where the antiderivative has a discontinuity is handled by the splitting; one that fails elsewhere is fatal.
                    if (op == Operators.Ne) continue;
                    return false;
                }
            }
        }
        return true;
    }
}
