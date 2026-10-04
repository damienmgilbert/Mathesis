using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>Whether an integer is even or odd.</summary>
[Flags]
internal enum Parity : byte
{
    None = 0,
    Even = 1,
    Odd = 2,
    Any = 3,
}

/// <summary>
/// What an <see cref="AssumptionSet"/> says, digested once: the facts as a linear system, integrality, parity and non-zero facts.
/// Everything here is about the points where the expressions involved are defined (docs/design/07, "Assumptions and domains").
/// </summary>
internal sealed class AssumptionAnalysis
{
    private readonly HashSet<Symbol> _integerSymbols = [];
    private readonly HashSet<Symbol> _realSymbols = [];
    private readonly HashSet<Expr> _nonZero = [];
    private readonly Dictionary<Expr, Parity> _parities = [];
    private readonly Dictionary<Symbol, (BigRational? Lower, bool LowerOpen, BigRational? Upper, bool UpperOpen)?> _exactBounds = [];
    private readonly Dictionary<Symbol, Interval<double>> _bounds = [];
    private LinearSystem? _system;

    public AssumptionAnalysis(ImmutableArray<Expr> facts)
    {
        var conjuncts = new List<Expr>();
        foreach (var fact in facts) Flatten(fact, conjuncts);
        Conjuncts = [.. conjuncts];

        foreach (var c in Conjuncts) CollectMembership(c);
    }

    public ImmutableArray<Expr> Conjuncts { get; }

    public bool HasNonZeroFacts => _nonZero.Count > 0;

    public bool IsKnownNonZero(Expr canonical) => _nonZero.Contains(canonical);

    // ----- Reading facts -----

    private static Expr Canonical(Expr e) => Normalizer.Canonical(e);

    private static Apply Sub(Expr a, Expr b) => new(Operators.Sub, [a, b]);

    private static void Flatten(Expr fact, List<Expr> into)
    {
        switch (fact)
        {
            case Constant { Id: ConstantId.True }:
                return;
            case Apply a when a.Operator == Operators.And:
                foreach (var arg in a.Arguments) Flatten(arg, into);
                return;
            case Apply { Operator: var op, Arguments: [var inner] } when op == Operators.Not:
                switch (inner)
                {
                    case Apply disjunction when disjunction.Operator == Operators.Or:
                        foreach (var arg in disjunction.Arguments) Flatten(new Apply(Operators.Not, [arg]), into);
                        return;
                    case Apply { Operator: var innerOp, Arguments: [var twice] } when innerOp == Operators.Not:
                        Flatten(twice, into);
                        return;
                    case Apply rel when Complement(rel.Operator) is { } comp:
                        into.Add(new Apply(comp, rel.Arguments));
                        return;
                    case Apply { Operator: var elem } member when elem == Operators.Element:
                        into.Add(new Apply(Operators.NotElement, member.Arguments));
                        return;
                    default:
                        return;
                }
            default:
                into.Add(fact);
                return;
        }
    }

    public static Operator? Complement(Operator op) =>
        op == Operators.Lt ? Operators.Ge : op == Operators.Ge ? Operators.Lt
        : op == Operators.Le ? Operators.Gt : op == Operators.Gt ? Operators.Le
        : op == Operators.Eq ? Operators.Ne : op == Operators.Ne ? Operators.Eq
        : null;

    private void CollectMembership(Expr fact)
    {
        if (fact is not Apply a) return;
        if (a.Operator == Operators.Element) MembershipFact(a.Arguments[0], a.Arguments[1]);
        else if (a.Operator == Operators.NotElement && a.Arguments[1] is SetLiteral set)
        {
            foreach (var element in set.Elements) _nonZero.Add(Canonical(Sub(a.Arguments[0], element)));
        }
        else if (a.Operator == Operators.Ne) _nonZero.Add(Canonical(Sub(a.Arguments[0], a.Arguments[1])));
        else if (a.Operator == Operators.Divides && a.Arguments[0] is Number { Value: var two } && two == 2) _parities[Canonical(a.Arguments[1])] = Parity.Even;
        else if (a.Operator == Operators.Eq)
        {
            // mod(n, 2) = r gives the parity of n.
            for (var i = 0; i < 2; i++)
            {
                if (a.Arguments[i] is Apply { Operator: var op, Arguments: [var n, Number { Value: var m }] } && op == Operators.Mod && m == 2 && a.Arguments[1 - i] is Number { Value: var r })
                {
                    if (r == BigRational.Zero) _parities[Canonical(n)] = Parity.Even;
                    else if (r == BigRational.One) _parities[Canonical(n)] = Parity.Odd;
                }
            }
        }
    }

    private void MembershipFact(Expr x, Expr set)
    {
        if (set is Constant c)
        {
            switch (c.Id)
            {
                case ConstantId.Integers or ConstantId.Naturals:
                    if (x is Symbol s) _integerSymbols.Add(s);
                    break;
                case ConstantId.Reals or ConstantId.Rationals:
                    if (x is Symbol r) _realSymbols.Add(r);
                    break;
            }
        }
        else if (set is SetLiteral literal && x is Symbol sym && literal.Elements.All(e => e is Number { Value.IsInteger: true }))
        {
            _integerSymbols.Add(sym);
        }
        if (x is Symbol bounded && set is IntervalLiteral) _realSymbols.Add(bounded);
    }

    // ----- Integrality and parity -----

    public bool IsIntegerValued(Expr e)
    {
        switch (e)
        {
            case Number n: return n.Value.IsInteger;
            case Symbol s: return s.DeclaredSort.IsSubsortOf(Sort.Integer) || _integerSymbols.Contains(s);
            case Apply a:
                var args = a.Arguments;
                var id = a.Operator.Id;
                switch (id)
                {
                    case "add" or "mul" or "sub" or "neg" or "abs" or "max" or "min" or "gcd" or "lcm" or "quo" or "mod" or "factorial" or "binomial" or "perm":
                        return args.All(IsIntegerValued);
                    case "floor" or "ceil" or "round" or "sign":
                        return true;
                    case "pow":
                        return IsIntegerValued(args[0]) && args[1] is Number { Value: { IsInteger: true, Sign: >= 0 } };
                    default:
                        return false;
                }
            default: return false;
        }
    }

    public Parity ParityOf(Expr e)
    {
        if (!IsIntegerValued(e)) return Parity.Any;
        switch (e)
        {
            case Number n: return n.Value.Numerator.IsEven ? Parity.Even : Parity.Odd;
            case Apply a:
                var args = a.Arguments;
                switch (a.Operator.Id)
                {
                    case "add" or "sub": return args.Select(ParityOf).Aggregate((x, y) => x == Parity.Any || y == Parity.Any ? Parity.Any : x == y ? Parity.Even : Parity.Odd);
                    case "neg" or "abs": return ParityOf(args[0]);
                    case "mul":
                        {
                            var result = Parity.Odd;
                            foreach (var p in args.Select(ParityOf))
                            {
                                if (p == Parity.Even) return Parity.Even;
                                if (p == Parity.Any) result = Parity.Any;
                            }
                            return result;
                        }
                    case "pow":
                        return args[1] is Number { Value: var k } && k == 0 ? Parity.Odd : ParityOf(args[0]);
                }
                break;
        }
        return _parities.TryGetValue(Canonical(e), out var known) ? known : Parity.Any;
    }

    // ----- The linear system -----

    public LinearSystem System => _system ??= BuildSystem();

    private LinearSystem BuildSystem()
    {
        var system = new LinearSystem();
        var builder = new LinearBuilder(this, system, null);
        foreach (var fact in Conjuncts) AddRelation(fact, builder);
        return system;
    }

    private static void AddRelation(Expr fact, LinearBuilder builder)
    {
        if (fact is not Apply a) return;
        var op = a.Operator;
        if (a.Arguments.Length != 2) return;
        var l = a.Arguments[0];
        var r = a.Arguments[1];
        if (op == Operators.Element) { AddMember(l, r, builder); return; }
        if (op != Operators.Lt && op != Operators.Le && op != Operators.Gt && op != Operators.Ge && op != Operators.Eq) return;
        if (!l.Sort.IsNumeric || !r.Sort.IsNumeric) return;
        if (op == Operators.Gt) builder.AddAtLeast(Sub(l, r), true);
        else if (op == Operators.Ge) builder.AddAtLeast(Sub(l, r), false);
        else if (op == Operators.Lt) builder.AddAtLeast(Sub(r, l), true);
        else if (op == Operators.Le) builder.AddAtLeast(Sub(r, l), false);
        else
        {
            builder.AddAtLeast(Sub(l, r), false);
            builder.AddAtLeast(Sub(r, l), false);
        }
    }

    private static void AddMember(Expr x, Expr set, LinearBuilder builder)
    {
        switch (set)
        {
            case Constant { Id: ConstantId.Naturals }:
                builder.AddAtLeast(x, false);
                break;
            case IntervalLiteral i:
                if (i.Lower is not Constant { Id: ConstantId.NegativeInfinity }) builder.AddAtLeast(Sub(x, i.Lower), !i.LowerClosed);
                if (i.Upper is not Constant { Id: ConstantId.PositiveInfinity }) builder.AddAtLeast(Sub(i.Upper, x), !i.UpperClosed);
                break;
            case SetLiteral s when s.Elements.Length > 0 && s.Elements.All(e => e.FreeSymbols.Count == 0 && e.Sort.IsNumeric):
                // Between the smallest and the largest element: use enclosures of the elements.
                var lows = s.Elements.Select(e => IntervalEnclosure.Enclose(e, _ => Interval<double>.Entire)).ToArray();
                if (lows.All(i => i.IsBounded))
                {
                    var lo = lows.Min(i => i.Lower);
                    var hi = lows.Max(i => i.Upper);
                    builder.AddAtLeast(Sub(x, new Number(BigRational.FromDouble(lo))), false);
                    builder.AddAtLeast(Sub(new Number(BigRational.FromDouble(hi)), x), false);
                }
                break;
        }
    }

    // ----- What the facts say about one symbol -----

    private (BigRational? Lower, bool LowerOpen, BigRational? Upper, bool UpperOpen)? ExactBounds(Symbol s)
    {
        if (_exactBounds.TryGetValue(s, out var cached)) return cached;
        (BigRational?, bool, BigRational?, bool)? result = null;
        if (System.TryGetVariable(s, out var index)) result = System.Project(index);
        _exactBounds[s] = result;
        return result;
    }

    /// <summary>The numeric interval the facts and the symbol's sort allow, outward rounded.</summary>
    public Interval<double> Bounds(Symbol s)
    {
        if (_bounds.TryGetValue(s, out var known)) return known;
        double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
        if (s.DeclaredSort.IsSubsortOf(Sort.Natural)) lo = 0;
        if (ExactBounds(s) is { } b)
        {
            if (b.Lower is { } l) lo = Math.Max(lo, DownOf(l, b.LowerOpen, IsIntegerValued(s)));
            if (b.Upper is { } u) hi = Math.Min(hi, UpOf(u, b.UpperOpen, IsIntegerValued(s)));
        }
        var result = lo <= hi ? new Interval<double>(lo, hi) : Interval<double>.Empty;
        _bounds[s] = result;
        return result;
    }

    private static double DownOf(BigRational x, bool open, bool integer)
    {
        if (integer)
        {
            var ceil = -Floor(-x);
            if (open && ceil == x) ceil += BigRational.One;
            x = ceil;
        }
        var d = x.ToDouble();
        return BigRational.FromDouble(d) > x || double.IsInfinity(d) ? double.BitDecrement(d) : d;
    }

    private static double UpOf(BigRational x, bool open, bool integer)
    {
        if (integer)
        {
            var floor = Floor(x);
            if (open && floor == x) floor -= BigRational.One;
            x = floor;
        }
        var d = x.ToDouble();
        return BigRational.FromDouble(d) < x || double.IsInfinity(d) ? double.BitIncrement(d) : d;
    }

    private static BigRational Floor(BigRational x)
    {
        if (x.IsInteger) return x;
        var q = BigInteger.Divide(x.Numerator, x.Denominator);
        return new BigRational(x.Numerator.Sign < 0 ? q - 1 : q);
    }

    /// <summary>The sign of a symbol from its sort and the facts (with exact strictness).</summary>
    public SignInfo SymbolSign(Symbol s)
    {
        var sign = s.DeclaredSort.IsRealValued || _realSymbols.Contains(s) ? SignInfo.Real : SignInfo.Unknown;
        if (s.DeclaredSort.IsSubsortOf(Sort.Natural)) sign &= SignInfo.NonNegative | SignInfo.NonReal;
        if (ExactBounds(s) is { } b && !sign.HasFlag(SignInfo.NonReal))
        {
            if (b.Lower is { } l && (l.Sign > 0 || (l.Sign == 0 && b.LowerOpen))) sign &= SignInfo.Positive;
            else if (b.Lower is { } l0 && l0.Sign == 0) sign &= SignInfo.NonNegative;
            if (b.Upper is { } u && (u.Sign < 0 || (u.Sign == 0 && b.UpperOpen))) sign &= SignInfo.Negative;
            else if (b.Upper is { } u0 && u0.Sign == 0) sign &= SignInfo.NonPositive;
        }
        if (_nonZero.Contains(s)) sign &= ~SignInfo.Zero;
        return sign == SignInfo.None ? SignInfo.Real : sign;
    }
}

/// <summary>Turns expressions into rows of a <see cref="LinearSystem"/>; non-linear subterms become variables of their own.</summary>
internal sealed class LinearBuilder(AssumptionAnalysis analysis, LinearSystem system, AssumptionQuery? query)
{
    /// <summary>Adds <c>e ≥ 0</c> (or <c>e &gt; 0</c>).</summary>
    public void AddAtLeast(Expr e, bool strict)
    {
        var coefficients = new Dictionary<int, BigRational>();
        var constant = BigRational.Zero;
        Walk(Normalizer.Canonical(e), BigRational.One, coefficients, ref constant);
        system.Add(coefficients, constant, strict);
    }

    private void Walk(Expr e, BigRational factor, Dictionary<int, BigRational> coefficients, ref BigRational constant)
    {
        switch (e)
        {
            case Number n:
                constant += factor * n.Value;
                return;
            case Apply a when a.Operator == Operators.Add:
                foreach (var arg in a.Arguments) Walk(arg, factor, coefficients, ref constant);
                return;
            case Apply a when a.Operator == Operators.Neg:
                Walk(a.Arguments[0], -factor, coefficients, ref constant);
                return;
            case Apply a when a.Operator == Operators.Sub:
                Walk(a.Arguments[0], factor, coefficients, ref constant);
                Walk(a.Arguments[1], -factor, coefficients, ref constant);
                return;
            case Apply a when a.Operator == Operators.Mul:
                var k = BigRational.One;
                var rest = new List<Expr>();
                foreach (var arg in a.Arguments)
                {
                    if (arg is Number num) k *= num.Value;
                    else rest.Add(arg);
                }
                if (rest.Count == 0) constant += factor * k;
                else if (rest.Count == 1) Walk(rest[0], factor * k, coefficients, ref constant);
                else Atom(rest.Count == a.Arguments.Length ? e : new Apply(Operators.Mul, [.. rest]), factor * k, coefficients);
                return;
            case Apply { Operator: var div, Arguments: [var numerator, Number { Value: var d }] } when div == Operators.Div && d != BigRational.Zero:
                Walk(numerator, factor / d, coefficients, ref constant);
                return;
            default:
                Atom(e, factor, coefficients);
                return;
        }
    }

    private void Atom(Expr e, BigRational factor, Dictionary<int, BigRational> coefficients)
    {
        var known = system.TryGetVariable(e, out var index);
        if (!known)
        {
            index = system.Variable(e, analysis.IsIntegerValued(e));
            Introduce(e, index);
        }
        coefficients[index] = coefficients.TryGetValue(index, out var existing) ? existing + factor : factor;
    }

    // Facts a new variable brings with it: natural symbols are non-negative, and any term has the sign and enclosure that can be read off it.
    private void Introduce(Expr e, int index)
    {
        var row = new Dictionary<int, BigRational> { [index] = BigRational.One };
        if (e is Symbol { DeclaredSort: var sort } && sort.IsSubsortOf(Sort.Natural))
        {
            system.Add(row, BigRational.Zero, false);
            return;
        }
        if (e is Symbol) return;

        var enclosure = query is not null ? query.Enclose(e) : IntervalEnclosure.Enclose(e, _ => Interval<double>.Entire);
        if (enclosure.IsEmpty) return;
        if (double.IsFinite(enclosure.Lower)) system.Add(row, -BigRational.FromDouble(enclosure.Lower), false);
        if (double.IsFinite(enclosure.Upper)) system.Add(new Dictionary<int, BigRational> { [index] = -BigRational.One }, BigRational.FromDouble(enclosure.Upper), false);

        if (query is not null)
        {
            var sign = query.Sign(e);
            if (sign.HasFlag(SignInfo.NonReal)) return;
            if (sign == SignInfo.Positive) system.Add(row, BigRational.Zero, true);
            else if (sign == SignInfo.NonNegative) system.Add(row, BigRational.Zero, false);
            else if (sign == SignInfo.Negative) system.Add(new Dictionary<int, BigRational> { [index] = -BigRational.One }, BigRational.Zero, true);
            else if (sign == SignInfo.NonPositive) system.Add(new Dictionary<int, BigRational> { [index] = -BigRational.One }, BigRational.Zero, false);
        }
    }
}
