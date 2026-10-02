using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Evaluation;

namespace Mathesis.Calculus;

/// <summary>Questions about the real sets <see cref="NaturalDomain"/> returns: intervals, unions, differences and periodic exclusions.</summary>
internal static class DomainSets
{
    /// <summary>The value of a closed numeric expression, with <c>oo</c> and <c>-oo</c> as infinities; <c>null</c> when it is not a number.</summary>
    public static double? ToDouble(Expr e)
    {
        switch (e)
        {
            case Constant { Id: ConstantId.PositiveInfinity }: return double.PositiveInfinity;
            case Constant { Id: ConstantId.NegativeInfinity }: return double.NegativeInfinity;
            case Apply { Operator.Id: "mul", Arguments: [Number { Value.Sign: < 0 }, Constant { Id: ConstantId.PositiveInfinity }] }: return double.NegativeInfinity;
            case Apply { Operator.Id: "neg", Arguments: [var inner] } when ToDouble(inner) is { } negated: return -negated;
            case Number n: return (double)n.Value;
        }
        return e.FreeSymbols.Count == 0 && Evaluator.N(e) is Outcome<double>.Success { Value: var v } && double.IsFinite(v) ? v : null;
    }

    /// <summary>
    /// Whether <paramref name="set"/> contains every point of <c>[lo, hi]</c> (the whole closed interval, or a neighborhood when the caller passes
    /// a small interval around a point). Unknown shapes of sets give <c>false</c>.
    /// </summary>
    public static bool Contains(Expr set, double lo, double hi)
    {
        switch (set)
        {
            case Constant { Id: ConstantId.Reals }:
                return true;
            case IntervalLiteral i:
                if (ToDouble(i.Lower) is not { } l || ToDouble(i.Upper) is not { } u) return false;
                return (i.LowerClosed ? lo >= l : lo > l) && (i.UpperClosed ? hi <= u : hi < u);
            case Apply { Operator.Id: "union", Arguments: var parts }:
                return parts.Any(p => Contains(p, lo, hi));
            case Apply { Operator.Id: "setminus", Arguments: [var a, var b] }:
                return Contains(a, lo, hi) && !Meets(b, lo, hi);
            case Apply { Operator.Id: "intersect", Arguments: var parts }:
                return parts.All(p => Contains(p, lo, hi));
            case Bind { Binder: Binder.SetBuilder, Bound: [var v], Body: var condition }:
                return HoldsThroughout(condition, v, lo, hi);
            default:
                return false;
        }
    }

    // A condition set {v in R | condition}: the condition (a conjunction of relations) holds at every sample of the interval and its sides keep their sign,
    // which for continuous expressions means no zero in between.
    private static bool HoldsThroughout(Expr condition, Symbol v, double lo, double hi)
    {
        if (condition is Apply { Operator.Id: "and", Arguments: var parts }) return parts.All(p => HoldsThroughout(p, v, lo, hi));
        if (condition is not Apply { Operator: var op, Arguments: [var l, var r] }) return false;
        if (!condition.FreeSymbols.All(s => s.Equals(v))) return false;
        const double Limit = 1e6;
        var from = Math.Max(lo, -Limit);
        var to = Math.Min(hi, Limit);
        var difference = new Apply(Operators.Add, [l, new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), r])]);
        int? sign = null;
        for (var i = 0; i <= 2000; i++)
        {
            var t = from + (to - from) * i / 2000.0;
            if (Evaluator.N(difference, new Dictionary<Symbol, double> { [v] = t }) is not Outcome<double>.Success { Value: var d } || !double.IsFinite(d)) return false;
            var s = Math.Abs(d) < 1e-12 ? 0 : Math.Sign(d);
            var ok = op == Operators.Ne ? s != 0 : op == Operators.Gt ? s > 0 : op == Operators.Lt ? s < 0 : op == Operators.Ge ? s >= 0 : op == Operators.Le && s <= 0;
            if (!ok) return false;
            if (s != 0 && sign is { } previous && previous != s && op == Operators.Ne) return false;
            sign = s != 0 ? s : sign;
        }
        return true;
    }

    // Whether an excluded set (periodic points {e(k) | k in Z}, a finite set, or a union) has a point in [lo, hi]; unknown shapes count as meeting it.
    private static bool Meets(Expr set, double lo, double hi)
    {
        switch (set)
        {
            case Bind { Bound: [var k], Body: var body } bind:
                for (var n = -2000; n <= 2000; n++)
                {
                    var value = Evaluator.N(body, new Dictionary<Symbol, double> { [k] = n });
                    if (value is Outcome<double>.Success { Value: var v } && v >= lo - 1e-12 && v <= hi + 1e-12) return true;
                }
                return Math.Abs(lo) > 5000 || Math.Abs(hi) > 5000 || double.IsInfinity(lo) || double.IsInfinity(hi) || bind.Body.FreeSymbols.Any(s => !s.Equals(k));
            case Apply { Operator.Id: "union", Arguments: var parts }:
                return parts.Any(p => Meets(p, lo, hi));
            case Apply { Operator.Id: "set", Arguments: var elements }:
                return elements.Any(e => ToDouble(e) is not { } v || (v >= lo - 1e-12 && v <= hi + 1e-12));
            case Constant { Id: ConstantId.EmptySet }:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Whether <paramref name="f"/> is continuous (defined, as an elementary function) on <c>[lo, hi]</c>: its natural domain contains the interval and
    /// it has no jump-type operator.
    /// </summary>
    public static bool ContinuousOn(Expr f, Symbol x, double lo, double hi, MathContext math)
    {
        if (f.Walk().Any(w => w.Expr is Apply { Operator.Id: "sign" or "floor" or "ceil" or "round" or "frac" or "mod" or "quo" or "heaviside" or "piecewise" or "max" or "min" })) return false;
        return NaturalDomain.Of(f, x, math) is Outcome<Expr>.Success { Value: var domain } && Contains(domain, lo, hi);
    }
}
