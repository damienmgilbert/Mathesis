using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Calculus;

/// <summary>Which side a limit is taken from.</summary>
public enum LimitDirection : byte
{
    /// <summary>The two-sided limit.</summary>
    Both,

    /// <summary>From the left (<c>x → a⁻</c>).</summary>
    FromLeft,

    /// <summary>From the right (<c>x → a⁺</c>).</summary>
    FromRight,
}

/// <summary>The value of a limit (docs/design/domains/d5-calculus.md, "Types").</summary>
public abstract record LimitResult
{
    /// <summary>The limit is the finite value <paramref name="Value"/>.</summary>
    public sealed record Finite(Expr Value) : LimitResult;

    /// <summary>The function grows without bound.</summary>
    public sealed record PositiveInfinity : LimitResult;

    /// <summary>The function decreases without bound.</summary>
    public sealed record NegativeInfinity : LimitResult;

    /// <summary>The limit does not exist; <paramref name="Reason"/> says why.</summary>
    public sealed record DoesNotExist(string Reason) : LimitResult;

    /// <summary>The result as an expression: the value, <c>oo</c>, <c>-oo</c> or <c>undefined</c>.</summary>
    public Expr ToExpression() => this switch
    {
        Finite f => f.Value,
        PositiveInfinity => new Constant(ConstantId.PositiveInfinity),
        NegativeInfinity => new Constant(ConstantId.NegativeInfinity),
        _ => new Constant(ConstantId.Undefined),
    };
}

/// <summary>
/// Limits (docs/design/07-engines.md, "Calculus engines"): direct substitution where the function is continuous, algebraic manipulation, the
/// standard limits of the catalog, L'Hôpital's rule (level at least Calculus1) and series expansion (level at least Calculus2). A finite
/// result is sanity-checked along a numeric sequence toward the point; a result that disagrees is not returned.
/// </summary>
public static class Limits
{
    private static readonly KnowledgeBase Catalog = KnowledgeBase.Default;
    private const int MaxDepth = 9;

    private sealed record Lim(LimitResult Result, Step Step);

    private sealed class Ctx(MathContext math, Budget budget)
    {
        public MathContext Math { get; } = math;
        public Budget Budget { get; } = budget;
    }

    private static Number Num(BigRational r) => new(r);

    private static bool IsPositiveInfinity(Expr e) => e is Constant { Id: ConstantId.PositiveInfinity };

    private static bool IsNegativeInfinity(Expr e) => e is Constant { Id: ConstantId.NegativeInfinity } || e is Apply { Operator.Id: "mul", Arguments: [Number { Value.Sign: < 0 }, Constant { Id: ConstantId.PositiveInfinity }] };

    /// <summary>The limit of <paramref name="f"/> as <paramref name="x"/> approaches <paramref name="point"/> (a number, <c>oo</c> or <c>-oo</c>).</summary>
    /// <returns>Success with the result and a one-step derivation citing the catalog entry used, or Unevaluated when no method decides it.</returns>
    public static Outcome<LimitResult> Limit(Expr f, Symbol x, Expr point, LimitDirection direction = LimitDirection.Both, MathContext? math = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(point);
        var c = new Ctx(math ?? MathContext.Default, budget ?? new Budget(maxSteps: 20_000, maxTime: TimeSpan.FromSeconds(20)));
        var p = Normalizer.Canonical(point);
        var integrand = Normalizer.Canonical(f);
        var start = new Bind(Binder.Limit, [x], [p], integrand);

        var found = Compute(integrand, x, p, direction, 0, c, true);
        if (found is null && direction == LimitDirection.Both && !IsPositiveInfinity(p) && !IsNegativeInfinity(p))
        {
            var left = Compute(integrand, x, p, LimitDirection.FromLeft, 0, c, true);
            var right = Compute(integrand, x, p, LimitDirection.FromRight, 0, c, true);
            if (left is not null && right is not null)
            {
                found = left.Result.Equals(right.Result)
                    ? left
                    : new Lim(new LimitResult.DoesNotExist($"the limit from the left is {Describe(left.Result)} and from the right {Describe(right.Result)}"), left.Step);
            }
        }
        found ??= Oscillation(integrand, x, p, direction);
        if (found is null) return new Outcome<LimitResult>.Unevaluated(start, "No method decides this limit.");
        if (!Plausible(integrand, x, p, direction, found.Result)) return new Outcome<LimitResult>.Unevaluated(start, $"The result {Describe(found.Result)} disagrees with the values of the function near the point.");

        var end = found.Result.ToExpression();
        var step = found.Step with { Before = start, After = end };
        return Outcome.Ok(found.Result, new Derivation(start, end, [step]), step.Added, Verification.NumericallyConsistent);
    }

    private static string Describe(LimitResult r) => r switch
    {
        LimitResult.Finite f => f.Value.ToString()!,
        LimitResult.PositiveInfinity => "oo",
        LimitResult.NegativeInfinity => "-oo",
        LimitResult.DoesNotExist d => "undefined (" + d.Reason + ")",
        _ => "?",
    };

    private static Step MakeStep(string entry, string method, Expr f, Symbol x, Expr p, LimitResult result, Derivation? sub = null, params (string, Expr)[] args) =>
        new(new EntryId(entry), "limit/" + method, new Bind(Binder.Limit, [x], [p], f), result.ToExpression(), ExprPath.Root, Bindings.Empty, Provisos.None,
            new ExplanationKey(entry, [.. args.Select(a => new KeyValuePair<string, Expr>(a.Item1, a.Item2))]), Catalog.Get(entry).Level ?? CurriculumLevel.Calculus1, sub);

    // ----- The methods -----

    private static Lim? Compute(Expr f, Symbol x, Expr p, LimitDirection dir, int depth, Ctx c, bool algebra)
    {
        if (depth > MaxDepth || !c.Budget.TryCharge()) return null;
        f = Normalizer.Canonical(f);
        var infinite = IsPositiveInfinity(p) || IsNegativeInfinity(p);

        if (!f.FreeSymbols.Contains(x)) return new Lim(new LimitResult.Finite(f), MakeStep("calc.lim.direct-substitution", "constant", f, x, p, new LimitResult.Finite(f)));
        if (f.Walk().Any(w => w.Expr is Apply { Operator.Id: "sign" or "floor" or "ceil" or "round" or "frac" or "mod" or "quo" or "piecewise" })) return null;

        if (infinite && RationalAtInfinity(f, x, IsPositiveInfinity(p)) is { } rational) return new Lim(rational, MakeStep("calc.lim.rational-at-infinity", "rational-at-infinity", f, x, p, rational));
        if (!infinite && Direct(f, x, p, dir, c) is { } direct) return new Lim(direct, MakeStep("calc.lim.direct-substitution", "direct-substitution", f, x, p, direct));
        if (Standard(f, x, p, dir) is { } standard) return standard;
        if (!DefinedAround(f, x, p, dir, c, out var undefinedSide)) return new Lim(new LimitResult.DoesNotExist(undefinedSide), MakeStep("calc.lim.def", "undefined", f, x, p, new LimitResult.DoesNotExist(undefinedSide)));
        if (Alg(f, x, p, dir, c) is { } algebraic) return new Lim(algebraic, MakeStep(algebraic is LimitResult.Finite ? "calc.lim.composition" : "calc.lim.infinite", "algebra-of-limits", f, x, p, algebraic));

        if (SumOrConstantFactor(f, x, p, dir, depth, c) is { } rule) return rule;

        if (algebra)
        {
            // abs(u) is u or -u on the side of approach.
            if (f.Walk().Any(w => w.Expr is Apply { Operator.Id: "abs" }))
            {
                var changed = false;
                var unsigned = f.Transform(e =>
                {
                    if (e is not Apply { Operator.Id: "abs", Arguments: [var u] } || NearSign(u, x, p, dir) is not { } sign) return null;
                    changed = true;
                    return sign > 0 ? u : new Apply(Operators.Mul, [Num(BigRational.NegativeOne), u]);
                });
                if (changed && Compute(unsigned, x, p, dir, depth + 1, c, true) is { } viaSign)
                {
                    return new Lim(viaSign.Result, MakeStep("alg.abs.def", "abs-by-sign", f, x, p, viaSign.Result, new Derivation(f, viaSign.Result.ToExpression(), [viaSign.Step]), ("rewritten", Normalizer.Canonical(unsigned))));
                }
            }

            // tan, cot, sec and csc as quotients of sine and cosine, so that a zero denominator shows its sign.
            var quotients = f.Transform(e => e switch
            {
                Apply { Operator.Id: "tan", Arguments: [var a] } => new Apply(Operators.Mul, [new Apply(Operators.Sin, [a]), new Apply(Operators.Pow, [new Apply(Operators.Cos, [a]), Num(BigRational.NegativeOne)])]),
                Apply { Operator.Id: "cot", Arguments: [var a] } => new Apply(Operators.Mul, [new Apply(Operators.Cos, [a]), new Apply(Operators.Pow, [new Apply(Operators.Sin, [a]), Num(BigRational.NegativeOne)])]),
                Apply { Operator.Id: "sec", Arguments: [var a] } => new Apply(Operators.Pow, [new Apply(Operators.Cos, [a]), Num(BigRational.NegativeOne)]),
                Apply { Operator.Id: "csc", Arguments: [var a] } => new Apply(Operators.Pow, [new Apply(Operators.Sin, [a]), Num(BigRational.NegativeOne)]),
                _ => null,
            });
            if (!Normalizer.Canonical(quotients).Equals(f) && Compute(quotients, x, p, dir, depth + 1, c, false) is { } viaQuotients)
            {
                return new Lim(viaQuotients.Result, MakeStep("calc.lim.quotient", "trig-quotients", f, x, p, viaQuotients.Result, new Derivation(f, viaQuotients.Result.ToExpression(), [viaQuotients.Step]), ("rewritten", Normalizer.Canonical(quotients))));
            }
            foreach (var (name, transform) in new (string, Transform)[] { ("cancel", Transforms.Cancel), ("together", Transforms.Together), ("rationalize", Transforms.Rationalize), ("expand", Transforms.Expand), ("factor", Transforms.Factor) })
            {
                if (c.Budget.IsExceeded) return null;
                if (transform.Run(f, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic)) is not Outcome<Expr>.Success { Value: var g, Steps: Derivation d } || g.Equals(f)) continue;
                if (Compute(g, x, p, dir, depth + 1, c, false) is { } inner) return new Lim(inner.Result, MakeStep("calc.lim.direct-substitution", "algebraic-" + name, f, x, p, inner.Result, new Derivation(d.Start, inner.Result.ToExpression(), [.. d.Steps, inner.Step]), ("rewritten", g)));
            }
            if (Simplifier.Simplify(f, c.Math, null, new Budget(maxSteps: 2000, maxTime: TimeSpan.FromSeconds(3))) is Outcome<Expr>.Success { Value: var s, Steps: Derivation sd } && !s.Equals(f)
                && Compute(s, x, p, dir, depth + 1, c, false) is { } simplified)
            {
                return new Lim(simplified.Result, MakeStep("calc.lim.direct-substitution", "algebraic-simplify", f, x, p, simplified.Result, new Derivation(sd.Start, simplified.Result.ToExpression(), [.. sd.Steps, simplified.Step]), ("rewritten", s)));
            }
        }

        if ((c.Math.Level ?? CurriculumLevel.Advanced) >= CurriculumLevel.Calculus2 && FromSeries(f, x, p, dir) is { } series) return new Lim(series, MakeStep("calc.series.series-arithmetic", "series", f, x, p, series));
        if (LHopital(f, x, p, dir, depth, c) is { } hopital) return hopital;
        if (PowerForm(f, x, p, dir, depth, c) is { } power) return power;
        return null;
    }

    // The sum rule and the constant multiple rule: the limits of the terms (each found by any method, L'Hôpital included) combine when the result is determinate.
    private static Lim? SumOrConstantFactor(Expr f, Symbol x, Expr p, LimitDirection dir, int depth, Ctx c)
    {
        if (f is not Apply { Operator: var op, Arguments: var args } || depth >= MaxDepth - 1) return null;
        if (op == Operators.Add)
        {
            var parts = new List<Lim>();
            foreach (var term in args)
            {
                if (Compute(term, x, p, dir, depth + 1, c, false) is not { } r || r.Result is LimitResult.DoesNotExist) return null;
                parts.Add(r);
            }
            LimitResult? total;
            var infinite = parts.Where(r => r.Result is not LimitResult.Finite).Select(r => r.Result).ToList();
            if (infinite.Count == 0) total = new LimitResult.Finite(Normalizer.Canonical(new Apply(Operators.Add, [.. parts.Select(r => ((LimitResult.Finite)r.Result).Value)])));
            else total = infinite.Distinct().Count() == 1 ? infinite[0] : null;
            if (total is null) return null;
            return new Lim(total, MakeStep("calc.lim.sum", "sum", f, x, p, total, new Derivation(new Bind(Binder.Limit, [x], [p], f), total.ToExpression(), [.. parts.Select(r => r.Step)])));
        }
        if (op == Operators.Mul)
        {
            var constants = args.Where(a => !a.FreeSymbols.Contains(x)).ToList();
            var rest = args.Where(a => a.FreeSymbols.Contains(x)).ToList();
            if (constants.Count == 0 || rest.Count == 0) return null;
            var factor = constants.Count == 1 ? constants[0] : new Apply(Operators.Mul, [.. constants]);
            var inner = rest.Count == 1 ? rest[0] : new Apply(Operators.Mul, [.. rest]);
            if (Compute(inner, x, p, dir, depth + 1, c, false) is not { } r) return null;
            LimitResult? result = r.Result switch
            {
                LimitResult.Finite { Value: var v } => new LimitResult.Finite(Normalizer.Canonical(new Apply(Operators.Mul, [factor, v]))),
                LimitResult.PositiveInfinity when DomainSets.ToDouble(factor) is { } k && k != 0 => k > 0 ? new LimitResult.PositiveInfinity() : new LimitResult.NegativeInfinity(),
                LimitResult.NegativeInfinity when DomainSets.ToDouble(factor) is { } k2 && k2 != 0 => k2 > 0 ? new LimitResult.NegativeInfinity() : new LimitResult.PositiveInfinity(),
                _ => null,
            };
            return result is null ? null : new Lim(result, MakeStep("calc.lim.constant-multiple", "constant-multiple", f, x, p, result, new Derivation(new Bind(Binder.Limit, [x], [p], inner), r.Result.ToExpression(), [r.Step]), ("c", factor)));
        }
        return null;
    }

    // Direct substitution where the function is continuous: its natural domain contains a neighborhood of the point (one-sided for one-sided limits).
    private static LimitResult.Finite? Direct(Expr f, Symbol x, Expr p, LimitDirection dir, Ctx c)
    {
        if (DomainSets.ToDouble(p) is not { } a || p.FreeSymbols.Count != 0) return null;
        var eps = 1e-9 * Math.Max(1, Math.Abs(a));
        var (lo, hi) = dir switch { LimitDirection.FromLeft => (a - eps, a), LimitDirection.FromRight => (a, a + eps), _ => (a - eps, a + eps) };
        if (!DomainSets.ContinuousOn(f, x, lo, hi, c.Math)) return null;
        var value = f.Substitute(x, p);
        var exact = Evaluator.Evaluate(value, null, c.Math.NormalizeOptions) is Outcome<Expr>.Success { Value: var v } ? v : Normalizer.Canonical(value);
        if (exact.Walk().Any(w => w.Expr is Constant { Id: ConstantId.Undefined or ConstantId.ComplexInfinity or ConstantId.PositiveInfinity or ConstantId.NegativeInfinity })) return null;
        return new LimitResult.Finite(exact);
    }

    // Degrees and leading coefficients: the theorem calc.lim.rational-at-infinity.
    private static LimitResult? RationalAtInfinity(Expr f, Symbol x, bool plus)
    {
        if (f.FreeSymbols.Count != 1 || !PolynomialConversion.TryToRationalFunction(f, x, out var n, out var d) || n.IsZero) return null;
        if (n.Degree < d.Degree) return new LimitResult.Finite(Num(BigRational.Zero));
        var ratio = n.LeadingCoefficient / d.LeadingCoefficient;
        if (n.Degree == d.Degree) return new LimitResult.Finite(Num(ratio));
        var positive = (ratio.Sign > 0) == (plus || (n.Degree - d.Degree) % 2 == 0);
        return positive ? new LimitResult.PositiveInfinity() : new LimitResult.NegativeInfinity();
    }

    // The standard limits of the catalog (tag standard-limit): the body is matched against the function with the entry's variables as wilds.
    private static Lim? Standard(Expr f, Symbol x, Expr p, LimitDirection dir)
    {
        foreach (var entry in Catalog.Entries)
        {
            if (!entry.Tags.Contains("standard-limit") || entry.Statement is not Apply { Operator: var eq, Arguments: [Bind { Binder: Binder.Limit, Bound: [var bound], Data: var data, Body: var body }, var rhs] } || eq != Operators.Eq) continue;
            if (data.Length == 0 || !Normalizer.Canonical(data[0]).Equals(p)) continue;
            if (data.Length > 1)
            {
                var text = data[1].ToString() ?? string.Empty;
                if (text.Contains('+', StringComparison.Ordinal) && dir != LimitDirection.FromRight) continue;
                if (text.Contains('-', StringComparison.Ordinal) && dir != LimitDirection.FromLeft) continue;
            }
            var names = entry.Vars.Select(v => v.Name).ToHashSet();
            Expr ToWild(Expr e) => e.Transform(s => s is Symbol sym && names.Contains(sym.Name) ? new Wild(sym.Name) : null);
            var pattern = new Pattern(Normalizer.Canonical(ToWild(body.Substitute(bound, x))), entry.Vars.ToDictionary(v => v.Name, v => new WildOptions(v.Sort)));
            foreach (var binding in Matcher.Match(pattern, f))
            {
                var value = Normalizer.Canonical(Matcher.Instantiate(ToWild(rhs), binding, pattern));
                if (entry.Where is { } where && Normalizer.Canonical(Matcher.Instantiate(ToWild(where), binding, pattern)) is { } guard && MathContext.Default.Ask(guard) != Truth.True) continue;
                var result = value is Constant { Id: ConstantId.PositiveInfinity } ? new LimitResult.PositiveInfinity() : (LimitResult)new LimitResult.Finite(value);
                return new Lim(result, MakeStep(entry.Id.Value, "standard-limit", f, x, p, result));
            }
        }
        return null;
    }

    // Series expansion: the valuation and the leading coefficient of the Laurent series decide the limit; each side is expanded separately.
    private static LimitResult? FromSeries(Expr f, Symbol x, Expr p, LimitDirection dir)
    {
        var positive = IsPositiveInfinity(p);
        var negative = IsNegativeInfinity(p);
        Expr g = f;
        BigRational center;
        if (positive || negative)
        {
            g = Normalizer.Canonical(f.Substitute(x, new Apply(Operators.Pow, [x, Num(BigRational.NegativeOne)])));
            center = BigRational.Zero;
            dir = positive ? LimitDirection.FromRight : LimitDirection.FromLeft;
        }
        else if (p is Number { Value: var c })
        {
            center = c;
        }
        else
        {
            return null;
        }

        var right = dir != LimitDirection.FromLeft ? SeriesSide(g, x, center, true) : null;
        var left = dir != LimitDirection.FromRight ? SeriesSide(g, x, center, false) : null;
        return dir switch
        {
            LimitDirection.FromRight => right,
            LimitDirection.FromLeft => left,
            _ when right is not null && left is not null => right.Equals(left) ? right : new LimitResult.DoesNotExist("the limits from the two sides differ"),
            _ when right is not null && !f.Walk().Any(w => w.Expr is Apply { Operator.Id: "pow", Arguments: [_, Number { Value.IsInteger: false }] }) => right,
            _ => null,
        };
    }

    private static LimitResult? SeriesSide(Expr g, Symbol x, BigRational center, bool rightSide)
    {
        foreach (var order in new[] { 8, 14, 24 })
        {
            var series = PowerSeries.FromExpression(g, x, center, order, rightSide);
            if (series is null) return null;
            if (series.Valuation is not { } v) continue;
            var lead = series[v];
            if (v > 0) return new LimitResult.Finite(Num(BigRational.Zero));
            if (v == 0) return new LimitResult.Finite(Num(lead));
            // c t^v with v < 0: for t < 0 the sign flips when v is odd.
            var positiveValues = (lead.Sign > 0) == (rightSide || v % 2 == 0);
            return positiveValues ? new LimitResult.PositiveInfinity() : new LimitResult.NegativeInfinity();
        }
        return null;
    }

    // The sign of u near the point on the side(s) of approach: +1, -1, or null when it changes or cannot be told.
    private static int? NearSign(Expr u, Symbol x, Expr p, LimitDirection dir)
    {
        var infinitePoint = IsPositiveInfinity(p) || IsNegativeInfinity(p);
        var sides = dir == LimitDirection.Both && !infinitePoint ? new[] { LimitDirection.FromLeft, LimitDirection.FromRight } : new[] { dir };
        int? sign = null;
        foreach (var side in sides)
        {
            foreach (var h in infinitePoint ? new[] { 0.2, 0.05, 0.0125 } : new[] { 1e-3, 1e-5, 1e-7 })
            {
                if (Probe(p, side, h) is not { } at || Evaluator.N(u, new Dictionary<Symbol, double> { [x] = at }) is not Outcome<double>.Success { Value: var value } || value == 0 || !double.IsFinite(value)) return null;
                var current = Math.Sign(value);
                if (sign is { } previous && previous != current) return null;
                sign = current;
            }
        }
        return sign;
    }

    // The algebra of limits (catalog calc.lim.sum, product, quotient, power, composition): limits of the parts combine when the result is determinate.
    private static LimitResult? Alg(Expr f, Symbol x, Expr p, LimitDirection dir, Ctx c)
    {
        if (!f.FreeSymbols.Contains(x)) return new LimitResult.Finite(f);
        if (f.Equals(x)) return IsPositiveInfinity(p) ? new LimitResult.PositiveInfinity() : IsNegativeInfinity(p) ? new LimitResult.NegativeInfinity() : new LimitResult.Finite(p);
        if (f is not Apply { Operator: var op, Arguments: var args }) return null;
        var parts = new List<LimitResult>();
        foreach (var a in args)
        {
            if (Alg(a, x, p, dir, c) is not { } r || r is LimitResult.DoesNotExist) return null;
            // A closed value that is exactly zero (cos(pi/2)) counts as the number 0.
            if (r is LimitResult.Finite { Value: var fv } && fv is not Number && fv.FreeSymbols.Count == 0 && ZeroTest.Test(fv, c.Math) is ZeroTestResult.Zero or ZeroTestResult.ProbablyZero) r = new LimitResult.Finite(Num(BigRational.Zero));
            parts.Add(r);
        }
        static int Sign(LimitResult r) => r switch { LimitResult.PositiveInfinity => 1, LimitResult.NegativeInfinity => -1, _ => 0 };
        if (op == Operators.Add)
        {
            var infinite = parts.Where(r => r is not LimitResult.Finite).ToList();
            if (infinite.Count == 0) return Finite(new Apply(Operators.Add, [.. parts.Select(r => ((LimitResult.Finite)r).Value)]));
            return infinite.Select(Sign).Distinct().Count() == 1 ? infinite[0] : null;
        }
        if (op == Operators.Mul)
        {
            var sign = 1;
            var anyInfinite = parts.Any(q => q is not LimitResult.Finite);
            foreach (var r in parts)
            {
                if (r is LimitResult.Finite { Value: Number { Value.Sign: 0 } } && anyInfinite) return null;
                if (r is not LimitResult.Finite) sign *= Sign(r);
                else if (anyInfinite)
                {
                    if (DomainSets.ToDouble(((LimitResult.Finite)r).Value) is { } d) sign *= Math.Sign(d);
                    else return null;
                }
            }
            if (!anyInfinite) return Finite(new Apply(Operators.Mul, [.. parts.Select(r => ((LimitResult.Finite)r).Value)]));
            return sign > 0 ? new LimitResult.PositiveInfinity() : sign < 0 ? new LimitResult.NegativeInfinity() : null;
        }
        if (op == Operators.Pow)
        {
            var b = parts[0];
            if (args[1] is not Number { Value: var k }) return null;
            if (b is LimitResult.Finite { Value: var bv })
            {
                if (bv is Number { Value.Sign: 0 })
                {
                    // u^k with u -> 0.
                    var s = NearSign(args[0], x, p, dir);
                    if (k.Sign > 0) return k.IsInteger || s is 1 ? new LimitResult.Finite(Num(BigRational.Zero)) : null;
                    if (s is null) return null;
                    if (!k.IsInteger) return s > 0 ? new LimitResult.PositiveInfinity() : null;
                    return s > 0 || k.Numerator.IsEven ? new LimitResult.PositiveInfinity() : new LimitResult.NegativeInfinity();
                }
                var value = Normalizer.Canonical(new Apply(Operators.Pow, [bv, Num(k)]));
                return DomainSets.ToDouble(bv) is { } bd && (bd > 0 || k.IsInteger) ? Finite(value) : null;
            }
            var bs = Sign(b);
            if (k.Sign < 0) return new LimitResult.Finite(Num(BigRational.Zero));
            if (k.Sign == 0) return null;
            if (bs > 0) return new LimitResult.PositiveInfinity();
            return k.IsInteger ? (k.Numerator.IsEven ? new LimitResult.PositiveInfinity() : new LimitResult.NegativeInfinity()) : null;
        }
        if (args.Length != 1) return null;
        var inner = parts[0];
        var id = op.Id;
        switch (inner)
        {
            case LimitResult.PositiveInfinity:
                return id switch
                {
                    "exp" or "ln" or "sinh" or "cosh" or "sqrt" or "abs" or "arsinh" or "arcosh" => new LimitResult.PositiveInfinity(),
                    "arctan" => Finite(new Apply(Operators.Mul, [Num(new BigRational(1) / new BigRational(2)), new Constant(ConstantId.Pi)])),
                    "tanh" => Finite(Num(BigRational.One)),
                    "arccot" => Finite(Num(BigRational.Zero)),
                    _ => null,
                };
            case LimitResult.NegativeInfinity:
                return id switch
                {
                    "exp" => Finite(Num(BigRational.Zero)),
                    "sinh" or "arsinh" => new LimitResult.NegativeInfinity(),
                    "cosh" or "abs" => new LimitResult.PositiveInfinity(),
                    "arctan" => Finite(new Apply(Operators.Mul, [Num(new BigRational(-1) / new BigRational(2)), new Constant(ConstantId.Pi)])),
                    "tanh" => Finite(Num(BigRational.NegativeOne)),
                    _ => null,
                };
            case LimitResult.Finite { Value: var v }:
            {
                // g(u) with u -> v: continuous inside the natural domain of g; the boundary point ln(0+) by the sign of u.
                var y = new Symbol("y_");
                var probe = new Apply(op, [y]);
                if (DomainSets.ToDouble(v) is { } vd)
                {
                    var eps = 1e-9 * Math.Max(1, Math.Abs(vd));
                    if (DomainSets.ContinuousOn(probe, y, vd - eps, vd + eps, c.Math))
                    {
                        var applied = Normalizer.Canonical(new Apply(op, [v]));
                        return Evaluator.Evaluate(applied, null, c.Math.NormalizeOptions) is Outcome<Expr>.Success { Value: var ev } ? Finite(ev) : Finite(applied);
                    }
                    if (id == "ln" && vd == 0 && NearSign(args[0], x, p, dir) is 1) return new LimitResult.NegativeInfinity();
                }
                return null;
            }
        }
        return null;
    }

    private static LimitResult.Finite Finite(Expr e) => new(Normalizer.Canonical(e));

    // L'Hôpital's rule for 0/0 and ∞/∞, and for 0·∞ written as a quotient.
    private static Lim? LHopital(Expr f, Symbol x, Expr p, LimitDirection dir, int depth, Ctx c)
    {
        if ((c.Math.Level ?? CurriculumLevel.Advanced) < CurriculumLevel.Calculus1 || depth >= MaxDepth - 1) return null;
        if (f is not Apply { Operator: var op, Arguments: var args } || op != Operators.Mul) return null;
        var numerator = new List<Expr>();
        var denominator = new List<Expr>();
        foreach (var a in args)
        {
            if (a is Apply { Operator: var po, Arguments: [var b, Number { Value: var e }] } && po == Operators.Pow && e.Sign < 0) denominator.Add(Normalizer.Canonical(new Apply(Operators.Pow, [b, Num(-e)])));
            else numerator.Add(a);
        }
        if (numerator.Count == 0) return null;
        var pairs = new List<(Expr N, Expr D)>();
        var all = numerator.Concat(denominator).ToList();
        if (denominator.Count > 0) pairs.Add((Product(numerator), Product(denominator)));
        else if (numerator.Count == 2)
        {
            // A·B as A/(1/B) and B/(1/A).
            pairs.Add((numerator[0], Normalizer.Canonical(new Apply(Operators.Pow, [numerator[1], Num(BigRational.NegativeOne)]))));
            pairs.Add((numerator[1], Normalizer.Canonical(new Apply(Operators.Pow, [numerator[0], Num(BigRational.NegativeOne)]))));
        }
        foreach (var (n, d) in pairs)
        {
            if (!n.FreeSymbols.Contains(x) && !d.FreeSymbols.Contains(x)) continue;
            var ln = Compute(n, x, p, dir, depth + 1, c, false);
            var ld = Compute(d, x, p, dir, depth + 1, c, false);
            if (Environment.GetEnvironmentVariable("MATHESIS_TRACE") == "1") Console.Error.WriteLine($"TRACE lhopital n={n} d={d} ln={ln?.Result} ld={ld?.Result}");
            if (ln is null || ld is null) continue;
            var zero = (LimitResult r) => r is LimitResult.Finite { Value: Number { Value.Sign: 0 } };
            var infinite = (LimitResult r) => r is LimitResult.PositiveInfinity or LimitResult.NegativeInfinity;
            if (!((zero(ln.Result) && zero(ld.Result)) || (infinite(ln.Result) && infinite(ld.Result)))) continue;
            if (Differentiator.Differentiate(n, x, 1, c.Math, null, true) is not Outcome<Expr>.Success { Value: var dn } ||
                Differentiator.Differentiate(d, x, 1, c.Math, null, true) is not Outcome<Expr>.Success { Value: var dd } || dd is Number { Value.Sign: 0 }) continue;
            var quotient = Normalizer.Canonical(new Apply(Operators.Mul, [dn, new Apply(Operators.Pow, [dd, Num(BigRational.NegativeOne)])]));
            if (Compute(quotient, x, p, dir, depth + 1, c, true) is { } inner)
            {
                var sub = new Derivation(new Bind(Binder.Limit, [x], [p], quotient), inner.Result.ToExpression(), [inner.Step]);
                return new Lim(inner.Result, MakeStep("calc.lim.lhopital", "lhopital", f, x, p, inner.Result, sub, ("f", n), ("g", d)));
            }
        }
        _ = all;
        return null;
    }

    private static Expr Product(List<Expr> factors) => factors.Count == 1 ? factors[0] : new Apply(Operators.Mul, [.. factors]);

    // u^v with an indeterminate form (1^oo, 0^0, oo^0): exp(v ln u) and the limit of v ln u (catalog calc.lim.log-trick).
    private static Lim? PowerForm(Expr f, Symbol x, Expr p, LimitDirection dir, int depth, Ctx c)
    {
        if (depth >= MaxDepth - 1 || f is not Apply { Operator: var op, Arguments: [var u, var v] } || op != Operators.Pow || !v.FreeSymbols.Contains(x)) return null;
        if (!Positive(u, x, p, dir)) return null;
        var exponent = Normalizer.Canonical(new Apply(Operators.Mul, [v, new Apply(Operators.Ln, [u])]));
        if (Compute(exponent, x, p, dir, depth + 1, c, true) is not { } inner) return null;
        LimitResult result = inner.Result switch
        {
            LimitResult.Finite { Value: var l } => new LimitResult.Finite(Normalizer.Canonical(new Apply(Operators.Exp, [l]))),
            LimitResult.PositiveInfinity => new LimitResult.PositiveInfinity(),
            LimitResult.NegativeInfinity => new LimitResult.Finite(Num(BigRational.Zero)),
            _ => new LimitResult.DoesNotExist("the exponent has no limit"),
        };
        if (result is LimitResult.DoesNotExist) return null;
        var sub = new Derivation(new Bind(Binder.Limit, [x], [p], exponent), inner.Result.ToExpression(), [inner.Step]);
        return new Lim(result, MakeStep("calc.lim.log-trick", "log-trick", f, x, p, result, sub));
    }

    // Whether u is positive near the point (numerically, on the side of approach).
    private static bool Positive(Expr u, Symbol x, Expr p, LimitDirection dir)
    {
        foreach (var h in new[] { 1e-3, 1e-5 })
        {
            if (Probe(p, dir, h) is not { } at || Evaluator.N(u, new Dictionary<Symbol, double> { [x] = at }) is not Outcome<double>.Success { Value: var value } || !(value > 0)) return false;
        }
        return true;
    }

    private static double? Probe(Expr p, LimitDirection dir, double h)
    {
        if (IsPositiveInfinity(p)) return 1 / h;
        if (IsNegativeInfinity(p)) return -1 / h;
        if (DomainSets.ToDouble(p) is not { } a) return null;
        var step = h * Math.Max(1, Math.Abs(a)) * (dir == LimitDirection.FromLeft ? -1 : 1);
        return a + step;
    }

    // ----- Checks -----

    // The function must be defined on each side of approach (from the natural domain); otherwise the one-sided or two-sided limit does not exist.
    private static bool DefinedAround(Expr f, Symbol x, Expr p, LimitDirection dir, Ctx c, out string reason)
    {
        reason = string.Empty;
        if (IsPositiveInfinity(p) || IsNegativeInfinity(p) || DomainSets.ToDouble(p) is not { } a || p.FreeSymbols.Count != 0) return true;
        if (NaturalDomain.Of(f, x, c.Math) is not Outcome<Expr>.Success { Value: var domain } || !IsSetShape(domain)) return true;
        var near = 1e-3 * Math.Max(1, Math.Abs(a));
        var tiny = 1e-9 * Math.Max(1, Math.Abs(a));
        if (dir != LimitDirection.FromRight && !DomainSets.Contains(domain, a - near, a - tiny))
        {
            reason = "the function is not defined to the left of the point";
            return false;
        }
        if (dir != LimitDirection.FromLeft && !DomainSets.Contains(domain, a + tiny, a + near))
        {
            reason = "the function is not defined to the right of the point";
            return false;
        }
        return true;
    }

    // Intervals, unions and differences of them, and the whole line: the shapes DomainSets understands.
    private static bool IsSetShape(Expr set) => set switch
    {
        IntervalLiteral or Constant { Id: ConstantId.Reals } => true,
        Apply { Operator.Id: "union", Arguments: var parts } => parts.All(IsSetShape),
        Apply { Operator.Id: "setminus", Arguments: [var a, _] } => IsSetShape(a),
        Apply { Operator.Id: "intersect", Arguments: var parts } => parts.All(IsSetShape),
        _ => false,
    };

    // Sequence evaluation: finite results are compared with the function near the point; infinite results need the function to be large.
    private static bool Plausible(Expr f, Symbol x, Expr p, LimitDirection dir, LimitResult result)
    {
        if (result is LimitResult.DoesNotExist) return true;
        var infinitePoint = IsPositiveInfinity(p) || IsNegativeInfinity(p);
        var sides = dir == LimitDirection.Both && !infinitePoint ? new[] { LimitDirection.FromLeft, LimitDirection.FromRight } : new[] { dir };
        // Moderate steps keep cancellation errors small; the tolerance covers the O(h) distance of the function from its limit.
        var steps = infinitePoint ? new[] { 1e-3, 1e-5, 1e-7 } : new[] { 1e-2, 1e-3, 1e-4 };
        foreach (var side in sides)
        {
            var values = new List<double>();
            foreach (var h in steps)
            {
                if (Probe(p, side, h) is not { } at) return true;
                if (Evaluator.N(f, new Dictionary<Symbol, double> { [x] = at }) is Outcome<double>.Success { Value: var value } && double.IsFinite(value)) values.Add(value);
            }
            if (values.Count == 0) continue;
            switch (result)
            {
                case LimitResult.Finite { Value: var l }:
                    if (DomainSets.ToDouble(l) is { } target && Math.Abs(values[^1] - target) > 0.1 * Math.Max(1, Math.Abs(target))) return false;
                    break;
                case LimitResult.PositiveInfinity:
                    if (values[^1] < 3 || (values.Count > 1 && values[^1] < values[0])) return false;
                    break;
                case LimitResult.NegativeInfinity:
                    if (values[^1] > -3 || (values.Count > 1 && values[^1] > values[0])) return false;
                    break;
            }
        }
        return true;
    }

    // A bounded function whose values keep swinging along a geometric sequence toward the point has no limit.
    private static Lim? Oscillation(Expr f, Symbol x, Expr p, LimitDirection dir)
    {
        if (!f.Walk().Any(w => w.Expr is Apply { Operator.Id: "sin" or "cos" or "tan" })) return null;
        var sides = dir == LimitDirection.Both && !IsPositiveInfinity(p) && !IsNegativeInfinity(p) ? new[] { LimitDirection.FromRight } : new[] { dir };
        foreach (var side in sides)
        {
            var values = new List<double>();
            for (var k = 8; k <= 40; k++)
            {
                if (Probe(p, side, Math.Pow(2, -k)) is not { } at || Evaluator.N(f, new Dictionary<Symbol, double> { [x] = at }) is not Outcome<double>.Success { Value: var value } || !double.IsFinite(value)) return null;
                values.Add(value);
            }
            var tail = values.TakeLast(25).ToList();
            if (tail.Max() - tail.Min() < 0.5 || tail.Max() > 1e3 || tail.Min() < -1e3) return null;
            var reversals = 0;
            for (var i = 2; i < tail.Count; i++)
            {
                if ((tail[i] - tail[i - 1]) * (tail[i - 1] - tail[i - 2]) < 0) reversals++;
            }
            if (reversals < 4) return null;
        }
        var result = new LimitResult.DoesNotExist("the function oscillates");
        return new Lim(result, MakeStep("calc.lim.def", "oscillation", f, x, p, result));
    }
}
