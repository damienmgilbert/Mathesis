using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>
/// One call of <see cref="AssumptionSet.Ask"/>: layered reasoning with memoized sign and enclosure results.
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantics.</b> A proposition is judged at the points where every subexpression is defined and the assumptions hold:
/// <c>True</c> means it holds at all of them, <c>False</c> that it fails at all of them. So <c>sqrt(x) ≥ 0</c> is <c>True</c>
/// without any assumption on <c>x</c>, and <c>x/x = 1</c> is not refuted by <c>x = 0</c>. Anything not shown is <c>Unknown</c>.
/// </para>
/// <para>
/// Layers, cheapest first (docs/design/07, "Assumptions and domains"): exact numbers and sorts; sign propagation over the tree;
/// interval arithmetic over bounds derived from the facts; Fourier–Motzkin elimination on linear facts; integer parity.
/// </para>
/// </remarks>
internal sealed class AssumptionQuery(AssumptionAnalysis analysis)
{
    private const int MaxDepth = 24;

    private readonly Dictionary<Expr, SignInfo> _signs = [];
    private readonly Dictionary<Expr, Interval<double>> _enclosures = [];
    private int _depth;

    public Interval<double> Enclose(Expr e) => IntervalEnclosure.Enclose(e, analysis.Bounds, _enclosures);

    // ----- Signs -----

    public SignInfo Sign(Expr e)
    {
        if (_signs.TryGetValue(e, out var known)) return known;
        var sign = Structural(e);
        if (!sign.HasFlag(SignInfo.NonReal))
        {
            sign = SignMath.Meet(sign, SignMath.FromInterval(Enclose(e)));
            if (analysis.HasNonZeroFacts && analysis.IsKnownNonZero(Normalizer.Canonical(e)) && sign != SignInfo.Zero) sign &= ~SignInfo.Zero;
        }
        _signs[e] = sign;
        return sign;
    }

    private SignInfo Structural(Expr e)
    {
        switch (e)
        {
            case Number n: return SignMath.FromRational(n.Value);
            case Float f: return double.IsNaN(f.Value) ? SignInfo.Unknown : f.Value > 0 ? SignInfo.Positive : f.Value < 0 ? SignInfo.Negative : SignInfo.Zero;
            case Constant c:
                return c.Id switch
                {
                    ConstantId.Pi or ConstantId.E or ConstantId.GoldenRatio or ConstantId.EulerGamma or ConstantId.CatalanG or ConstantId.PositiveInfinity => SignInfo.Positive,
                    ConstantId.NegativeInfinity => SignInfo.Negative,
                    ConstantId.ImaginaryUnit => SignInfo.NonReal,
                    _ => SignInfo.Unknown,
                };
            case Symbol s: return analysis.SymbolSign(s);
            case Apply a: return ApplySign(a);
            default: return SignInfo.Unknown;
        }
    }

    private SignInfo ApplySign(Apply a)
    {
        var args = a.Arguments;
        SignInfo S(int i) => Sign(args[i]);
        var real = SignInfo.Real;
        switch (a.Operator.Id)
        {
            case "add": return args.Select(Sign).Aggregate(SignMath.Add);
            case "mul": return args.Select(Sign).Aggregate(SignMath.Mul);
            case "sub": return SignMath.Add(S(0), SignMath.Neg(S(1)));
            case "neg": return SignMath.Neg(S(0));
            case "div": return SignMath.Div(S(0), S(1));
            case "pow": return PowerSign(args[0], args[1]);
            case "sqrt":
                {
                    var s = S(0);
                    if (s.HasFlag(SignInfo.NonReal)) return SignInfo.Unknown;
                    var r = (s & SignInfo.Zero) | (s & SignInfo.Positive);
                    return r == SignInfo.None ? SignInfo.NonNegative : r;
                }
            case "abs":
                {
                    var s = S(0);
                    return ((s & SignInfo.Zero) | (s.HasFlag(SignInfo.Negative) || s.HasFlag(SignInfo.Positive) || s.HasFlag(SignInfo.NonReal) ? SignInfo.Positive : 0)) | (s.HasFlag(SignInfo.NonReal) ? SignInfo.Zero : 0);
                }
            case "exp" or "cosh" or "sech": return S(0).HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : SignInfo.Positive;
            case "sinh" or "tanh" or "arctan" or "arsinh" or "artanh" or "arcsin" or "sign":
                {
                    var s = S(0);
                    return s.HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : s;
                }
            case "arccos" or "arcosh": return S(0).HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : SignInfo.NonNegative;
            case "arccot": return S(0).HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : SignInfo.Positive;
            case "floor":
                {
                    var s = S(0);
                    return s.HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : (s.HasFlag(SignInfo.Negative) ? SignInfo.Negative : 0) | (s.HasFlag(SignInfo.Zero) || s.HasFlag(SignInfo.Positive) ? SignInfo.Zero : 0) | (s.HasFlag(SignInfo.Positive) ? SignInfo.Positive : 0);
                }
            case "ceil":
                {
                    var s = S(0);
                    return s.HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : (s.HasFlag(SignInfo.Negative) ? SignInfo.NonPositive : 0) | (s & SignInfo.Zero) | (s.HasFlag(SignInfo.Positive) ? SignInfo.Positive : 0);
                }
            case "round":
                {
                    var s = S(0);
                    return s.HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : (s.HasFlag(SignInfo.Negative) ? SignInfo.NonPositive : 0) | (s.HasFlag(SignInfo.Zero) ? SignInfo.Zero : 0) | (s.HasFlag(SignInfo.Positive) ? SignInfo.NonNegative : 0);
                }
            case "frac": return S(0).HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : SignInfo.NonNegative;
            case "factorial": return SignInfo.Positive;
            case "gcd" or "lcm": return SignInfo.NonNegative;
            case "max":
                {
                    var all = args.Select(Sign).ToArray();
                    if (all.Any(s => s.HasFlag(SignInfo.NonReal))) return SignInfo.Unknown;
                    if (all.Any(s => s == SignInfo.Positive)) return SignInfo.Positive;
                    if (all.Any(s => (s & SignInfo.Negative) == 0)) return SignInfo.NonNegative;
                    return all.Aggregate((x, y) => x | y);
                }
            case "min":
                {
                    var all = args.Select(Sign).ToArray();
                    if (all.Any(s => s.HasFlag(SignInfo.NonReal))) return SignInfo.Unknown;
                    if (all.Any(s => s == SignInfo.Negative)) return SignInfo.Negative;
                    if (all.Any(s => (s & SignInfo.Positive) == 0)) return SignInfo.NonPositive;
                    return all.Aggregate((x, y) => x | y);
                }
            case "mod":
                {
                    // Floor modulus: the result has the sign of the divisor.
                    var n = S(1);
                    if (n.HasFlag(SignInfo.NonReal) || S(0).HasFlag(SignInfo.NonReal)) return SignInfo.Unknown;
                    var r = (n.HasFlag(SignInfo.Positive) ? SignInfo.NonNegative : 0) | (n.HasFlag(SignInfo.Negative) ? SignInfo.NonPositive : 0);
                    return r == SignInfo.None ? SignInfo.Real : r;
                }
            case "root":
                {
                    var s = S(0);
                    if (s.HasFlag(SignInfo.NonReal) || args[1] is not Number { Value: { IsInteger: true, Sign: > 0 } order }) return SignInfo.Unknown;
                    if (!order.Numerator.IsEven) return s;
                    var even = (s & SignInfo.Zero) | (s & SignInfo.Positive);
                    return even == SignInfo.None ? SignInfo.NonNegative : even;
                }
            default:
                return a.Sort.IsRealValued ? real : SignInfo.Unknown;
        }
    }

    private SignInfo PowerSign(Expr b, Expr x)
    {
        var sb = Sign(b);
        if (sb.HasFlag(SignInfo.NonReal) || sb == SignInfo.None) return SignInfo.Unknown;
        if (x is Number { Value: var p } exponent)
        {
            var num = p.Numerator;
            var den = p.Denominator;
            var nonZeroBase = sb & SignInfo.NonZero;
            var zero = sb.HasFlag(SignInfo.Zero);
            if (p == BigRational.Zero) return SignInfo.Positive; // 0^0 = 1 (ADR-10)
            if (den.IsOne)
            {
                if (p.Sign > 0)
                {
                    if (!num.IsEven) return sb;
                    return (nonZeroBase != 0 ? SignInfo.Positive : 0) | (zero ? SignInfo.Zero : 0);
                }

                // Negative integer power: the base is non-zero where the power is defined.
                var withoutZero = sb & ~SignInfo.Zero;
                if (withoutZero == SignInfo.None) return SignInfo.Unknown;
                return num.IsEven ? SignInfo.Positive : withoutZero;
            }
            if (!den.IsEven)
            {
                // Real odd root (conv.real-odd-root): negative bases keep a sign determined by the numerator's parity.
                if (num.IsEven)
                {
                    var even = (nonZeroBase != 0 ? SignInfo.Positive : 0) | (zero && p.Sign > 0 ? SignInfo.Zero : 0);
                    return even == SignInfo.None ? SignInfo.Positive : even;
                }
                var odd = p.Sign > 0 ? sb : sb & ~SignInfo.Zero;
                return odd == SignInfo.None ? SignInfo.Unknown : odd;
            }

            // Even root: defined for non-negative bases only.
            var r = (sb.HasFlag(SignInfo.Positive) ? SignInfo.Positive : 0) | (zero && p.Sign > 0 ? SignInfo.Zero : 0);
            return r == SignInfo.None ? SignInfo.NonNegative : r;
        }

        // General exponent: a positive base gives a positive power; a non-negative base a non-negative one; otherwise anything real.
        if (sb == SignInfo.Positive) return SignInfo.Positive;
        if ((sb & SignInfo.Negative) == 0) return SignInfo.NonNegative;
        return SignInfo.Real;
    }

    // ----- Asking -----

    public Truth Ask(Expr p)
    {
        if (++_depth > MaxDepth) return Truth.Unknown;
        try
        {
            switch (p)
            {
                case Constant { Id: ConstantId.True }: return Truth.True;
                case Constant { Id: ConstantId.False }: return Truth.False;
                case Apply a: return AskApply(a);
                default: return Truth.Unknown;
            }
        }
        finally
        {
            _depth--;
        }
    }

    private Truth AskApply(Apply a)
    {
        var args = a.Arguments;
        var op = a.Operator;
        if (op == Operators.And) return args.Aggregate(Truth.True, (t, x) => t.And(Ask(x)));
        if (op == Operators.Or) return args.Aggregate(Truth.False, (t, x) => t.Or(Ask(x)));
        if (op == Operators.Not) return Ask(args[0]).Not();
        if (op == Operators.Xor) return args.Aggregate(Truth.False, (t, x) => Xor(t, Ask(x)));
        if (op == Operators.Nand) return args.Aggregate(Truth.True, (t, x) => t.And(Ask(x))).Not();
        if (op == Operators.Nor) return args.Aggregate(Truth.False, (t, x) => t.Or(Ask(x))).Not();
        if (op == Operators.Iff)
        {
            if (args[0].Equals(args[1])) return Truth.True;
            var l = Ask(args[0]);
            var r = Ask(args[1]);
            return Xor(l, r).Not();
        }
        if (op == Operators.Implies)
        {
            var antecedent = Ask(args[0]);
            var consequent = Ask(args[1]);
            var result = antecedent.Not().Or(consequent);
            if (result != Truth.Unknown) return result;

            // Under the antecedent: assume it and ask the consequent.
            return AssumptionSet.Empty.Add(args[0]).Ask(args[1]) == Truth.True ? Truth.True : Truth.Unknown;
        }
        if (op == Operators.Element) return Member(args[0], args[1]);
        if (op == Operators.NotElement) return Member(args[0], args[1]).Not();
        if (op == Operators.Divides) return Divides(args[0], args[1]);
        if (AssumptionAnalysis.Complement(op) is not null) return Relation(op, args[0], args[1]);
        return Truth.Unknown;
    }

    private static Truth Xor(Truth a, Truth b) =>
        a == Truth.Unknown || b == Truth.Unknown ? Truth.Unknown : TruthExtensions.FromBool(a != b);

    // ----- Relations -----

    private Truth Relation(Operator op, Expr l, Expr r)
    {
        if (l.Equals(r))
        {
            // x = x holds wherever x is defined; the order relations with equality hold too, the strict ones fail.
            if (op == Operators.Eq || op == Operators.Le || op == Operators.Ge) return Truth.True;
            if (op == Operators.Ne || op == Operators.Lt || op == Operators.Gt) return Truth.False;
        }
        if (!l.Sort.IsNumeric || !r.Sort.IsNumeric) return Truth.Unknown;

        var comparable = !Sign(l).HasFlag(SignInfo.NonReal) && !Sign(r).HasFlag(SignInfo.NonReal);
        if (!comparable) return Truth.Unknown;

        if (ParityRelation(op, l, r) is { } parity) return parity;

        if (Prove(op, l, r)) return Truth.True;
        return Prove(AssumptionAnalysis.Complement(op)!, l, r) ? Truth.False : Truth.Unknown;
    }

    private bool Prove(Operator op, Expr l, Expr r)
    {
        if (op == Operators.Gt) return ProveLt(r, l);
        if (op == Operators.Ge) return ProveLe(r, l);
        if (op == Operators.Lt) return ProveLt(l, r);
        if (op == Operators.Le) return ProveLe(l, r);
        if (op == Operators.Eq) return ProveEq(l, r);
        return ProveNe(l, r);
    }

    private static Expr Difference(Expr l, Expr r) => Normalizer.Canonical(new Apply(Operators.Sub, [l, r]));

    private bool ProveLt(Expr l, Expr r)
    {
        var d = Difference(l, r);
        if (d is Number n) return n.Value.Sign < 0;
        if ((Sign(d) & ~SignInfo.Negative) == SignInfo.None) return true;
        if (Strictly(Enclose(l), Enclose(r))) return true;
        return Refutes(d, strict: false);
    }

    private bool ProveLe(Expr l, Expr r)
    {
        var d = Difference(l, r);
        if (d is Number n) return n.Value.Sign <= 0;
        if ((Sign(d) & ~SignInfo.NonPositive) == SignInfo.None) return true;
        var el = Enclose(l);
        var er = Enclose(r);
        if (!el.IsEmpty && !er.IsEmpty && el.Upper <= er.Lower) return true;
        return Refutes(d, strict: true);
    }

    private bool ProveEq(Expr l, Expr r)
    {
        var d = Difference(l, r);
        if (d is Number n) return n.Value.Sign == 0;
        if (Sign(d) == SignInfo.Zero) return true;
        return ProveLe(l, r) && ProveLe(r, l);
    }

    private bool ProveNe(Expr l, Expr r)
    {
        var d = Difference(l, r);
        if (d is Number n) return n.Value.Sign != 0;
        var s = Sign(d);
        if (!s.HasFlag(SignInfo.Zero) && (s & SignInfo.Real) != SignInfo.None) return true;
        if (Strictly(Enclose(l), Enclose(r)) || Strictly(Enclose(r), Enclose(l))) return true;
        if (analysis.IsIntegerValued(d) && analysis.ParityOf(d) == Parity.Odd) return true;
        return ProveLt(l, r) || ProveLt(r, l);
    }

    private static bool Strictly(Interval<double> a, Interval<double> b) => !a.IsEmpty && !b.IsEmpty && a.Upper < b.Lower;

    // The facts plus "e ≥ 0" (or "e > 0") have no solution.
    private bool Refutes(Expr e, bool strict)
    {
        var system = analysis.System.Clone();
        new LinearBuilder(analysis, system, this).AddAtLeast(e, strict);
        return system.IsInfeasible();
    }

    // ----- Integers: parity and divisibility -----

    private Truth? ParityRelation(Operator op, Expr l, Expr r)
    {
        if (op != Operators.Eq && op != Operators.Ne) return null;
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? l : r;
            var other = i == 0 ? r : l;
            if (side is Apply { Operator: var m, Arguments: [var n, Number { Value: var two }] } && m == Operators.Mod && two == 2 && other is Number { Value: var value } && analysis.IsIntegerValued(n))
            {
                var parity = analysis.ParityOf(n);
                var wanted = value == BigRational.Zero ? Parity.Even : value == BigRational.One ? Parity.Odd : Parity.None;
                Truth equal = wanted == Parity.None ? Truth.False : parity == Parity.Any ? Truth.Unknown : TruthExtensions.FromBool(parity == wanted);
                return op == Operators.Eq ? equal : equal.Not();
            }
        }
        return null;
    }

    private Truth Divides(Expr a, Expr b)
    {
        if (a is Number { Value: var m } && b is Number { Value: var n })
        {
            if (!m.IsInteger || !n.IsInteger) return Truth.Unknown;
            if (m == BigRational.Zero) return TruthExtensions.FromBool(n == BigRational.Zero);
            return TruthExtensions.FromBool((n.Numerator % m.Numerator).IsZero);
        }
        if (!analysis.IsIntegerValued(b)) return Truth.Unknown;
        if (a is Number { Value: var one } && BigRational.Abs(one) == BigRational.One) return Truth.True;
        if (a is Number { Value: var two } && two == 2)
        {
            var parity = analysis.ParityOf(b);
            return parity == Parity.Any ? Truth.Unknown : TruthExtensions.FromBool(parity == Parity.Even);
        }
        if (b is Number { Value: var zero } && zero == BigRational.Zero && analysis.IsIntegerValued(a)) return Truth.True;
        return Truth.Unknown;
    }

    // ----- Membership -----

    private Truth Member(Expr x, Expr set)
    {
        switch (set)
        {
            case Constant { Id: var id }:
                return Member(x, id);
            case IntervalLiteral i:
                {
                    var lower = i.Lower is Constant { Id: ConstantId.NegativeInfinity } ? Truth.True : Ask(new Apply(i.LowerClosed ? Operators.Le : Operators.Lt, [i.Lower, x]));
                    var upper = i.Upper is Constant { Id: ConstantId.PositiveInfinity } ? Truth.True : Ask(new Apply(i.UpperClosed ? Operators.Le : Operators.Lt, [x, i.Upper]));
                    return lower.And(upper);
                }
            case SetLiteral s:
                return s.Elements.Aggregate(Truth.False, (t, e) => t.Or(Ask(new Apply(Operators.Eq, [x, e]))));
            default:
                return Truth.Unknown;
        }
    }

    private Truth Member(Expr x, ConstantId set)
    {
        var sign = Sign(x);
        switch (set)
        {
            case ConstantId.EmptySet: return Truth.False;
            case ConstantId.Complexes: return x.Sort.IsNumeric ? Truth.True : Truth.Unknown;
            case ConstantId.Reals:
                if (!x.Sort.IsNumeric) return Truth.Unknown;
                return sign == SignInfo.NonReal ? Truth.False : !sign.HasFlag(SignInfo.NonReal) ? Truth.True : Truth.Unknown;
            case ConstantId.Rationals:
                if (x is Number) return Truth.True;
                return analysis.IsIntegerValued(x) ? Truth.True : Truth.Unknown;
            case ConstantId.Integers:
                if (analysis.IsIntegerValued(x)) return Truth.True;
                if (sign == SignInfo.NonReal) return Truth.False;
                return StrictlyBetweenIntegers(x) ? Truth.False : Truth.Unknown;
            case ConstantId.Naturals:
                {
                    var integer = Member(x, ConstantId.Integers);
                    if (integer == Truth.False) return Truth.False;
                    if (sign == SignInfo.Negative) return Truth.False;
                    return integer == Truth.True && (sign & SignInfo.Negative) == 0 && !sign.HasFlag(SignInfo.NonReal) ? Truth.True : Truth.Unknown;
                }
            default: return Truth.Unknown;
        }
    }

    private bool StrictlyBetweenIntegers(Expr x)
    {
        var iv = Enclose(x);
        if (iv.IsEmpty || !iv.IsBounded) return false;
        var k = Math.Floor(iv.Lower);
        return iv.Lower > k && iv.Upper < k + 1;
    }
}
