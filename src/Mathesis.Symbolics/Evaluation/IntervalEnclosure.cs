using Mathesis.Numbers;

namespace Mathesis.Symbolics.Evaluation;

/// <summary>
/// Encloses the values of an expression over boxes of its free symbols: the result contains the value at every point of the box where
/// the expression is defined. Operators with no interval implementation give <see cref="Interval{T}.Entire"/>, so the result is always
/// a valid enclosure, never a guess.
/// </summary>
internal static class IntervalEnclosure
{
    public static Interval<double> Enclose(Expr expr, Func<Symbol, Interval<double>> symbol, Dictionary<Expr, Interval<double>>? memo = null)
    {
        memo ??= [];
        return Visit(expr, symbol, memo);
    }

    private static Interval<double> Visit(Expr e, Func<Symbol, Interval<double>> symbol, Dictionary<Expr, Interval<double>> memo)
    {
        if (memo.TryGetValue(e, out var known)) return known;
        var result = Compute(e, symbol, memo);
        memo[e] = result;
        return result;
    }

    private static Interval<double> Compute(Expr e, Func<Symbol, Interval<double>> symbol, Dictionary<Expr, Interval<double>> memo)
    {
        var ops = IntervalOps.Instance;
        switch (e)
        {
            case Number n: return ops.FromRational(n.Value);
            case Float f: return ops.FromDouble(f.Value);
            case Constant c: return ops.TryConstant(c.Id, out var value) ? value : Interval<double>.Entire;
            case Symbol s: return symbol(s);
            case Apply: break;
            default: return Interval<double>.Entire;
        }

        var apply = (Apply)e;
        var args = apply.Arguments;
        Interval<double> Arg(int i) => Visit(args[i], symbol, memo);
        var id = apply.Operator.Id;
        switch (id)
        {
            case "add": return args.Select((_, i) => Arg(i)).Aggregate(ops.Add);
            case "mul": return args.Select((_, i) => Arg(i)).Aggregate(ops.Mul);
            case "sub": return ops.Sub(Arg(0), Arg(1));
            case "div": return ops.Div(Arg(0), Arg(1));
            case "neg": return ops.Neg(Arg(0));
            case "pow":
                if (OperatorMap.TryRational(args[1], out var q))
                {
                    if (q.IsInteger && BigRational.Abs(q) <= 1 << 20) return ops.PowInt(Arg(0), (int)q.Numerator);
                    if (q.Numerator.GetBitLength() < 31 && q.Denominator.GetBitLength() < 31) return ops.PowRational(Arg(0), (int)q.Numerator, (int)q.Denominator);
                }
                return ops.Pow(Arg(0), Arg(1));
            case "max": return args.Select((_, i) => Arg(i)).Aggregate(Interval<double>.Max);
            case "min": return args.Select((_, i) => Arg(i)).Aggregate(Interval<double>.Min);
            case "frac": return Arg(0).IsEmpty ? Interval<double>.Empty : new Interval<double>(0, 1);
            case "factorial": return Arg(0).IsEmpty || Arg(0).Upper < 0 ? Interval<double>.Empty : new Interval<double>(1, double.PositiveInfinity);
            case "gcd" or "lcm": return new Interval<double>(0, double.PositiveInfinity);
            case "mod":
                {
                    var n2 = Arg(1);
                    if (n2.IsEmpty) return n2;
                    return n2.Lower > 0 ? new Interval<double>(0, n2.Upper) : n2.Upper < 0 ? new Interval<double>(n2.Lower, 0) : Interval<double>.Entire;
                }
        }
        if (OperatorMap.Lower(apply) is { } lowered) return Visit(lowered, symbol, memo);
        if (OperatorMap.TryUnary(apply, out var unary)) return ops.Unary(unary, Arg(0));
        if (OperatorMap.TryBinary(apply, out var binary) && ops.Supports(binary)) return ops.Binary(binary, Arg(0), Arg(1));
        return Interval<double>.Entire;
    }
}
