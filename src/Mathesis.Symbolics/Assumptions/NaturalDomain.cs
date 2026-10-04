using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>
/// The natural domain of an expression in one variable: the set of real values of the variable at which every subexpression is
/// defined (docs/design/07-engines.md, "Assumptions and domains"; docs/design/04-type-system.md, "Sets and solution sets").
/// </summary>
/// <remarks>
/// <para>
/// Each operator contributes constraints (denominators ≠ 0, even-root arguments ≥ 0, logarithm arguments &gt; 0,
/// <c>arcsin</c>/<c>arccos</c> arguments in [−1, 1], <c>tan</c> argument ≠ π/2 + kπ, …). Constraints on a rational function with rational
/// coefficients are solved exactly by a sign chart over the real roots of numerator and denominator (rational roots, and quadratic
/// factors in closed form); a periodic exclusion on an argument linear in the variable becomes an image set
/// <c>{… | k ∈ ℤ}</c>. Anything else is returned honestly as a condition set <c>{x ∈ ℝ | condition}</c> instead of being guessed.
/// </para>
/// <para>
/// The result is a canonical-looking set expression: ℝ, ∅, <see cref="IntervalLiteral"/>s with exact endpoints, finite point sets, unions
/// of those, <c>S ∖ {…}</c> for periodic exclusions, or intersections with a condition set. It is computed in real mode.
/// </para>
/// </remarks>
public static class NaturalDomain
{
    private enum Kind : byte
    {
        Positive,
        NonNegative,
        NonZero,
        CosNonZero,
        SinNonZero,
    }

    private readonly record struct Condition(Kind Kind, Expr H);

    /// <summary>Computes the natural domain of <paramref name="expr"/> in <paramref name="x"/>.</summary>
    /// <param name="expr">The expression.</param>
    /// <param name="x">The variable; other free symbols are treated as parameters and constrained through <paramref name="context"/>.</param>
    /// <param name="context">The assumptions used to decide conditions on parameters; <see cref="MathContext.Default"/> when <c>null</c>.</param>
    /// <param name="budget">Limits the work; <see cref="Budget.Unlimited"/> when <c>null</c>.</param>
    /// <returns>
    /// A set expression, or Failed (<see cref="MathErrorKind.Unsupported"/>) when <paramref name="expr"/> contains an operator whose
    /// domain is not described yet (for example <c>gamma</c> or <c>factorial</c>), or Unevaluated when the budget ran out.
    /// </returns>
    public static Outcome<Expr> Of(Expr expr, Symbol x, MathContext? context = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(x);
        context ??= MathContext.Default;
        budget ??= Budget.Unlimited;

        var conditions = new List<Condition>();
        var unsupported = Collect(Normalizer.Canonical(expr), conditions);
        if (unsupported is not null) return Outcome.Fail<Expr>(new MathError(MathErrorKind.Unsupported, $"The natural domain of '{unsupported}' is not described yet.", expr));

        var set = RealSet.All;
        var exclusions = new List<Expr>();
        var residual = new List<Expr>();
        foreach (var condition in conditions.SelectMany(Reduce).Distinct())
        {
            if (!budget.TryCharge()) return new Outcome<Expr>.Unevaluated(expr, budget.ExceededReason ?? "Budget exceeded.");
            var h = Normalizer.Canonical(condition.H);

            if (!h.FreeSymbols.Contains(x))
            {
                // A condition on parameters only: decide it from the assumptions.
                var truth = context.Ask(ToProposition(condition.Kind, h));
                if (truth == Truth.True) continue;
                if (truth == Truth.False) return Outcome.Ok<Expr>(new Constant(ConstantId.EmptySet));
                residual.Add(ToProposition(condition.Kind, h));
                continue;
            }

            if (condition.Kind is Kind.CosNonZero or Kind.SinNonZero)
            {
                var image = PeriodicExclusion(condition.Kind, h, x, context);
                if (image is not null) exclusions.Add(image);
                else residual.Add(ToProposition(condition.Kind, h));
                continue;
            }

            var solved = SolveSign(condition.Kind, h, x);
            if (solved is null) residual.Add(ToProposition(condition.Kind, h));
            else set = set.Intersect(solved);
        }

        Expr result = set.ToExpr();
        if (exclusions.Count > 0 && !set.IsEmpty) result = new Apply(Operators.SetMinus, [result, exclusions.Count == 1 ? exclusions[0] : new Apply(Operators.Union, [.. exclusions])]);
        if (residual.Count > 0 && !set.IsEmpty)
        {
            var condition = residual.Count == 1 ? residual[0] : new Apply(Operators.And, [.. residual]);
            Expr conditionSet = new Bind(Binder.SetBuilder, [x], [new Constant(ConstantId.Reals)], condition);
            result = set.IsAll && exclusions.Count == 0 ? conditionSet : new Apply(Operators.Intersect, [result, conditionSet]);
        }
        return Outcome.Ok(result);
    }

    // Conditions on a function of g that are equivalent to conditions on g itself, which the sign chart can solve. Sub-expressions of
    // g are constrained separately (Collect visits them), so definedness of g is assumed here.
    private static IEnumerable<Condition> Reduce(Condition c)
    {
        var h = Normalizer.Canonical(c.H);
        if (c.Kind is Kind.CosNonZero or Kind.SinNonZero || h is not Apply a) return [c with { H = h }];
        var args = a.Arguments;
        var one = new Number(BigRational.One);
        switch (a.Operator.Id)
        {
            case "exp":
                return [];
            case "sqrt" or "abs":
                return c.Kind == Kind.NonNegative ? [] : Reduce(new(c.Kind == Kind.Positive && a.Operator.Id == "abs" ? Kind.NonZero : c.Kind, args[0]));
            case "ln" or "log" when args.Length == 1 || a.Operator.Id == "ln":
                // ln g is zero at g = 1, positive for g > 1 and non-negative for g ≥ 1 (g > 0 is required separately).
                return Reduce(new(c.Kind, new Apply(Operators.Sub, [args[0], one])));
            case "pow" when args[1] is Number { Value: var p } && !p.IsInteger && p.Sign > 0:
                if (p.Denominator.IsEven) return c.Kind == Kind.NonNegative ? [] : Reduce(new(c.Kind, args[0]));
                if (p.Numerator.IsEven) return c.Kind == Kind.NonNegative ? [] : Reduce(new(Kind.NonZero, args[0]));
                return Reduce(new(c.Kind, args[0]));
            case "root" when args[1] is Number { Value: { IsInteger: true } n } && n.Sign > 0:
                if (n.Numerator.IsEven) return c.Kind == Kind.NonNegative ? [] : Reduce(new(c.Kind, args[0]));
                return Reduce(new(c.Kind, args[0]));
            case "mul" when c.Kind == Kind.NonZero:
                return args.SelectMany(f => Reduce(new(Kind.NonZero, f)));
            default:
                return [c with { H = h }];
        }
    }

    private static Apply ToProposition(Kind kind, Expr h)
    {
        var zero = new Number(BigRational.Zero);
        return kind switch
        {
            Kind.Positive => new Apply(Operators.Gt, [h, zero]),
            Kind.NonNegative => new Apply(Operators.Ge, [h, zero]),
            Kind.NonZero => new Apply(Operators.Ne, [h, zero]),
            Kind.CosNonZero => new Apply(Operators.Ne, [new Apply(Operators.Cos, [h]), zero]),
            _ => new Apply(Operators.Ne, [new Apply(Operators.Sin, [h]), zero]),
        };
    }

    // ----- Constraints from operators -----

    private static readonly HashSet<string> Total = ["add", "mul", "sub", "neg", "abs", "sign", "floor", "ceil", "round", "frac", "max", "min", "exp", "sin", "cos", "sinh", "cosh", "tanh", "arctan", "arccot", "arsinh"];

    // Returns the id of an operator whose domain is not described, or null.
    private static string? Collect(Expr e, List<Condition> into)
    {
        if (e is not Apply a)
        {
            foreach (var child in e.Children)
            {
                if (Collect(child, into) is { } bad) return bad;
            }
            return null;
        }
        foreach (var child in a.Arguments)
        {
            if (Collect(child, into) is { } bad) return bad;
        }

        var args = a.Arguments;
        var id = a.Operator.Id;
        var one = new Number(BigRational.One);
        Expr Minus(Expr p, Expr q) => new Apply(Operators.Sub, [p, q]);
        Expr Plus(Expr p, Expr q) => new Apply(Operators.Add, [p, q]);
        Expr Square(Expr p) => new Apply(Operators.Pow, [p, new Number(2)]);
        switch (id)
        {
            case "div" or "quo": into.Add(new(Kind.NonZero, args[1])); break;
            case "mod": into.Add(new(Kind.NonZero, args[1])); break;
            case "pow": PowerConditions(args[0], args[1], into); break;
            case "sqrt": into.Add(new(Kind.NonNegative, args[0])); break;
            case "root":
                if (args[1] is Number { Value: { IsInteger: true } order } && order.Sign > 0)
                {
                    if (order.Numerator.IsEven) into.Add(new(Kind.NonNegative, args[0]));
                }
                else if (args[1] is Number { Value.IsInteger: true }) into.Add(new(Kind.NonZero, args[0]));
                else into.Add(new(Kind.Positive, args[0]));
                break;
            case "ln": into.Add(new(Kind.Positive, args[0])); break;
            case "log":
                into.Add(new(Kind.Positive, args[0]));
                if (args.Length == 2)
                {
                    into.Add(new(Kind.Positive, args[1]));
                    into.Add(new(Kind.NonZero, Minus(args[1], one)));
                }
                break;
            case "arcsin" or "arccos":
                into.Add(new(Kind.NonNegative, Plus(args[0], one)));
                into.Add(new(Kind.NonNegative, Minus(one, args[0])));
                break;
            case "artanh":
                into.Add(new(Kind.Positive, Plus(args[0], one)));
                into.Add(new(Kind.Positive, Minus(one, args[0])));
                break;
            case "arcosh": into.Add(new(Kind.NonNegative, Minus(args[0], one))); break;
            case "arcsec" or "arccsc": into.Add(new(Kind.NonNegative, Minus(Square(args[0]), one))); break;
            case "arcoth": into.Add(new(Kind.Positive, Minus(Square(args[0]), one))); break;
            case "arsech":
                into.Add(new(Kind.Positive, args[0]));
                into.Add(new(Kind.NonNegative, Minus(one, args[0])));
                break;
            case "arcsch" or "coth" or "csch": into.Add(new(Kind.NonZero, args[0])); break;
            case "tan" or "sec": into.Add(new(Kind.CosNonZero, args[0])); break;
            case "cot" or "csc": into.Add(new(Kind.SinNonZero, args[0])); break;
            default:
                if (!Total.Contains(id)) return id;
                break;
        }
        return null;
    }

    private static void PowerConditions(Expr b, Expr exponent, List<Condition> into)
    {
        if (exponent is Float { Value: var f } && f == Math.Floor(f) && Math.Abs(f) < 1e9) exponent = new Number(BigRational.FromDouble(f));
        if (exponent is Number { Value: var p })
        {
            var den = p.Denominator;
            if (den.IsOne)
            {
                if (p.Sign < 0) into.Add(new(Kind.NonZero, b));
            }
            else if (den.IsEven) into.Add(new(p.Sign > 0 ? Kind.NonNegative : Kind.Positive, b));
            else if (p.Sign < 0) into.Add(new(Kind.NonZero, b));
            return;
        }

        // A general power b^y is real for every y only when b > 0; positive constants need no condition.
        if (b is Number { Value.Sign: > 0 } or Constant { Id: ConstantId.Pi or ConstantId.E or ConstantId.GoldenRatio }) return;
        into.Add(new(Kind.Positive, b));
    }

    // ----- Solving h > 0, h ≥ 0, h ≠ 0 for rational functions h -----

    private readonly record struct Root(Expr Value, double Approx, Interval<double> Enclosure);

    private static RealSet? SolveSign(Kind kind, Expr h, Symbol x)
    {
        if (RationalFunctionOf(h, x) is not var (numerator, denominator)) return null;

        if (numerator.IsZero)
        {
            // h is identically 0 where defined.
            if (kind != Kind.NonNegative) return RealSet.Empty;
            return Exclude(RealSet.All, denominator) is { } all ? all : null;
        }

        var rootsOfNumerator = RealRoots(numerator);
        var rootsOfDenominator = denominator.Degree > 0 ? RealRoots(denominator) : [];
        if (rootsOfNumerator is null || rootsOfDenominator is null) return null;

        // Critical points: roots of either polynomial, in increasing order, without repeats.
        var critical = new List<(Root Root, bool OfNumerator, bool OfDenominator)>();
        foreach (var r in rootsOfNumerator) critical.Add((r, true, false));
        foreach (var r in rootsOfDenominator)
        {
            var i = critical.FindIndex(c => c.Root.Value.Equals(r.Value));
            if (i >= 0) critical[i] = (critical[i].Root, critical[i].OfNumerator, true);
            else critical.Add((r, false, true));
        }
        critical.Sort((p, q) => p.Root.Approx.CompareTo(q.Root.Approx));
        for (var i = 0; i + 1 < critical.Count; i++)
        {
            if (!(critical[i].Root.Enclosure.Upper < critical[i + 1].Root.Enclosure.Lower)) return null;
        }

        // The sign of h on each open gap, by exact evaluation at a rational point strictly inside it.
        var pieces = new List<Piece>();
        for (var i = 0; i <= critical.Count; i++)
        {
            double left = i == 0 ? double.NegativeInfinity : critical[i - 1].Root.Enclosure.Upper;
            double right = i == critical.Count ? double.PositiveInfinity : critical[i].Root.Enclosure.Lower;
            var sample = double.IsInfinity(left) && double.IsInfinity(right) ? 0 : double.IsInfinity(left) ? right - 1 : double.IsInfinity(right) ? left + 1 : left + (right - left) / 2;
            var point = BigRational.FromDouble(sample);
            var sign = numerator.Evaluate(point).Sign * denominator.Evaluate(point).Sign;
            if (sign == 0) return null;
            if (kind == Kind.Positive || kind == Kind.NonNegative ? sign > 0 : true)
            {
                pieces.Add(new Piece(i == 0 ? Endpoint.NegativeInfinity : EndpointOf(critical[i - 1].Root), false, i == critical.Count ? Endpoint.PositiveInfinity : EndpointOf(critical[i].Root), false));
            }
        }

        // A critical point belongs to the set only for h ≥ 0 and only where h is defined (not a root of the denominator).
        if (kind == Kind.NonNegative)
        {
            foreach (var (root, _, ofDenominator) in critical)
            {
                if (!ofDenominator) pieces.Add(new Piece(EndpointOf(root), true, EndpointOf(root), true));
            }
        }
        return RealSet.From(pieces);
    }

    private static Endpoint EndpointOf(Root r) => new(r.Value, r.Approx);

    private static RealSet? Exclude(RealSet set, Polynomial<BigRational> denominator)
    {
        if (denominator.Degree <= 0) return set;
        var roots = RealRoots(denominator);
        if (roots is null) return null;
        var excluded = RealSet.From(roots.Select(r => new Piece(EndpointOf(r), true, EndpointOf(r), true)));
        var gaps = new List<Piece>();
        var previous = Endpoint.NegativeInfinity;
        foreach (var p in excluded.Pieces)
        {
            gaps.Add(new Piece(previous, false, p.Lower, false));
            previous = p.Lower;
        }
        gaps.Add(new Piece(previous, false, Endpoint.PositiveInfinity, false));
        return set.Intersect(RealSet.From(gaps));
    }

    // The distinct real roots of a non-zero polynomial: rational roots, plus the roots of a quadratic cofactor in closed form.
    // Null when a factor of degree three or more has no rational root (no closed form is attempted).
    private static List<Root>? RealRoots(Polynomial<BigRational> p)
    {
        var roots = new List<Root>();
        if (p.Degree <= 0) return roots;
        var rational = PolynomialAlgorithms.RationalRoots(p);
        if (!rational.Complete) return null;
        foreach (var r in rational.Roots) roots.Add(RootOf(new Number(r.Root)));

        var rest = rational.Cofactor;
        if (rest.Degree <= 0) return roots;
        if (rest.Degree > 2) return null;

        // a x² + b x + c with no rational root: the discriminant is not a perfect square of a rational.
        var a = rest[2];
        var b = rest[1];
        var c = rest[0];
        var disc = b * b - 4 * a * c;
        if (disc.Sign < 0) return roots;
        var (scale, radicand) = SquareFreeSqrt(disc);
        if (radicand.IsOne)
        {
            // Cannot happen after RationalRoots; guard anyway.
            var sqrt = scale;
            roots.Add(RootOf(new Number((-b - sqrt) / (2 * a))));
            roots.Add(RootOf(new Number((-b + sqrt) / (2 * a))));
            return roots;
        }
        var centre = -b / (2 * a);
        var spread = scale / (2 * a);
        var root = new Apply(Operators.Sqrt, [new Number(new BigRational(radicand))]);
        foreach (var sign in new[] { -1, 1 })
        {
            var q = spread * sign;
            roots.Add(RootOf(Normalizer.Canonical(new Apply(Operators.Add, [new Number(centre), new Apply(Operators.Mul, [new Number(q), root])]))));
        }
        return roots;
    }

    private static Root RootOf(Expr value)
    {
        var enclosure = IntervalEnclosure.Enclose(value, _ => Interval<double>.Entire);
        return new(value, enclosure.Midpoint, enclosure);
    }

    // sqrt(q) = scale · sqrt(radicand) with radicand a square-free integer.
    private static (BigRational Scale, BigInteger Radicand) SquareFreeSqrt(BigRational q)
    {
        // sqrt(n/d) = sqrt(n·d)/d
        var m = q.Numerator * q.Denominator;
        var scale = BigInteger.One;
        var radicand = m;
        for (BigInteger f = 2; f <= 100_000 && f * f <= radicand; f += f == 2 ? 1 : 2)
        {
            var square = f * f;
            while ((radicand % square).IsZero)
            {
                radicand /= square;
                scale *= f;
            }
        }
        return (BigRational.Create(scale, q.Denominator), radicand);
    }

    private static (Polynomial<BigRational> Numerator, Polynomial<BigRational> Denominator)? RationalFunctionOf(Expr e, Symbol x)
    {
        const int MaxDegree = 64;
        switch (e)
        {
            case Number n:
                return (new Polynomial<BigRational>([n.Value]), Polynomial<BigRational>.One);
            case Symbol s when s.Equals(x):
                return (Polynomial<BigRational>.X, Polynomial<BigRational>.One);
            case Apply a:
                var args = a.Arguments;
                var parts = new List<(Polynomial<BigRational> N, Polynomial<BigRational> D)>();
                if (a.Operator == Operators.Pow)
                {
                    if (args[1] is not Number { Value: { IsInteger: true } k } || BigRational.Abs(k) > MaxDegree) return null;
                    if (RationalFunctionOf(args[0], x) is not var (bn, bd)) return null;
                    var count = (int)BigRational.Abs(k).Numerator;
                    if (k.Sign >= 0) return (bn.Pow(count), bd.Pow(count));
                    return bn.IsZero ? null : (bd.Pow(count), bn.Pow(count));
                }
                foreach (var arg in args)
                {
                    if (RationalFunctionOf(arg, x) is not { } f) return null;
                    parts.Add(f);
                }
                Polynomial<BigRational> num, den;
                if (a.Operator == Operators.Add)
                {
                    (num, den) = parts[0];
                    foreach (var (n2, d2) in parts.Skip(1))
                    {
                        num = num * d2 + n2 * den;
                        den *= d2;
                    }
                }
                else if (a.Operator == Operators.Mul)
                {
                    (num, den) = parts[0];
                    foreach (var (n2, d2) in parts.Skip(1))
                    {
                        num *= n2;
                        den *= d2;
                    }
                }
                else if (a.Operator == Operators.Neg) (num, den) = (-parts[0].N, parts[0].D);
                else if (a.Operator == Operators.Sub) (num, den) = (parts[0].N * parts[1].D - parts[1].N * parts[0].D, parts[0].D * parts[1].D);
                else if (a.Operator == Operators.Div)
                {
                    if (parts[1].N.IsZero) return null;
                    (num, den) = (parts[0].N * parts[1].D, parts[0].D * parts[1].N);
                }
                else return null;
                return Math.Max(num.Degree, den.Degree) > MaxDegree ? null : (num, den);
            default:
                return null;
        }
    }

    // ----- Periodic exclusions: cos(g) ≠ 0 and sin(g) ≠ 0 for g linear in x -----

    private static Bind? PeriodicExclusion(Kind kind, Expr g, Symbol x, MathContext context)
    {
        if (LinearIn(g, x) is not var (a, b)) return null;
        if (context.Ask(new Apply(Operators.Ne, [a, new Number(BigRational.Zero)])) != Truth.True) return null;

        // g = π/2 + kπ (cosine) or kπ (sine), so x = (π/2 + kπ − b)/a.
        var k = new Symbol("k", Sort.Integer);
        var pi = new Constant(ConstantId.Pi);
        Expr angle = new Apply(Operators.Mul, [k, pi]);
        if (kind == Kind.CosNonZero) angle = new Apply(Operators.Add, [new Apply(Operators.Div, [pi, new Number(2)]), angle]);
        var point = Normalizer.Canonical(new Apply(Operators.Div, [new Apply(Operators.Sub, [angle, b]), a]));
        return new Bind(Binder.ImageSet, [k], [new Constant(ConstantId.Integers)], point);
    }

    // g = a·x + b with a and b free of x.
    private static (Expr A, Expr B)? LinearIn(Expr g, Symbol x)
    {
        var zero = new Number(BigRational.Zero);
        var one = new Number(BigRational.One);
        if (!g.FreeSymbols.Contains(x)) return (zero, g);
        switch (g)
        {
            case Symbol s when s.Equals(x):
                return (one, zero);
            case Apply a when a.Operator == Operators.Add:
                {
                    Expr coefficient = zero;
                    Expr constant = zero;
                    foreach (var term in a.Arguments)
                    {
                        if (LinearIn(term, x) is not var (ta, tb)) return null;
                        coefficient = new Apply(Operators.Add, [coefficient, ta]);
                        constant = new Apply(Operators.Add, [constant, tb]);
                    }
                    return (Normalizer.Canonical(coefficient), Normalizer.Canonical(constant));
                }
            case Apply a when a.Operator == Operators.Mul:
                {
                    // Exactly one factor may contain x.
                    var withX = a.Arguments.Where(f => f.FreeSymbols.Contains(x)).ToList();
                    if (withX.Count != 1 || LinearIn(withX[0], x) is not var (fa, fb)) return null;
                    var others = a.Arguments.Where(f => !f.FreeSymbols.Contains(x)).ToList();
                    Expr factor = others.Count == 0 ? one : new Apply(Operators.Mul, [.. others.Append(one)]);
                    return (Normalizer.Canonical(new Apply(Operators.Mul, [factor, fa])), Normalizer.Canonical(new Apply(Operators.Mul, [factor, fb])));
                }
            default:
                return null;
        }
    }
}
