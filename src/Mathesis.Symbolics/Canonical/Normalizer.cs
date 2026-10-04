using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Canonical;

/// <summary>The three canonical levels (docs/design/05-syntax-trees-and-notation.md, "Canonical levels").</summary>
public enum NormalizationLevel : byte
{
    /// <summary>As written: <c>sub</c>, <c>div</c>, <c>neg</c> and <c>sqrt</c> exist, nothing is folded or reordered.</summary>
    Raw,

    /// <summary>Subtraction, division, negation and square roots rewritten to sums, products and powers; associative operators flattened.</summary>
    Structural,

    /// <summary>Automatic simplification: exact arithmetic folded, identities removed, like terms and powers collected, operands sorted.</summary>
    Canonical,
}

/// <summary>Options for <see cref="Normalizer.Canonical(Expr, NormalizeOptions?)"/>.</summary>
public sealed record NormalizeOptions
{
    /// <summary>
    /// The number field. In <see cref="NumberField.Real"/> mode (the default) exact odd roots of negative numbers are real:
    /// <c>(-8)^(1/3)</c> is −2 (catalog <c>conv.real-odd-root</c>); in complex mode they are left unevaluated.
    /// </summary>
    public NumberField Field { get; init; } = NumberField.Real;
}

/// <summary>
/// Rewrites expressions to the Structural and Canonical levels.
/// </summary>
/// <remarks>
/// <para>Canonical is conservative: it never applies a law that has a side condition. So <c>x + (−1)x</c> collapses (sums of like
/// terms with exact numeric coefficients), <c>x^0</c> is 1 (0⁰ = 1, ADR-10), and <c>x·x²</c> is <c>x³</c>, but <c>x·x^(−1)</c>
/// stays (it would need x ≠ 0), like powers combine only when their exponents are integers of the same sign, <c>0·x</c> stays
/// (it would need x finite), nothing is distributed, and terms containing ±∞ or <c>undefined</c> are never cancelled.</para>
/// <para>Exact numbers fold: <c>2 + 3 → 5</c>, <c>2^10 → 1024</c>, <c>1/2 + 1/3 → 5/6</c>, <c>8^(1/3) → 2</c>, and <c>e^x</c> is
/// written <c>exp(x)</c>. A <see cref="Float"/> operand makes the folded constant a float. Float coefficients of symbolic terms
/// are not collected.</para>
/// </remarks>
public static class Normalizer
{
    /// <summary>Rewrites <paramref name="expr"/> to the given level.</summary>
    public static Expr To(Expr expr, NormalizationLevel level, NormalizeOptions? options = null) => level switch
    {
        NormalizationLevel.Raw => expr,
        NormalizationLevel.Structural => Structural(expr),
        _ => Canonical(expr, options),
    };

    /// <summary>
    /// The Structural level: <c>a − b</c> → <c>a + (−1)·b</c>, <c>a / b</c> → <c>a · b^(−1)</c>, <c>−a</c> → <c>(−1)·a</c>,
    /// <c>√a</c> → <c>a^(1/2)</c>; associative operators are flattened; operand order is kept; nothing is folded or sorted.
    /// </summary>
    public static Expr Structural(Expr expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        return expr.Transform(StructuralNode);
    }

    private static Expr? StructuralNode(Expr e)
    {
        if (e is not Apply a) return null;
        var args = a.Arguments;
        if (a.Operator == Operators.Sub) return Make(Operators.Add, [args[0], Make(Operators.Mul, [new Number(-1), args[1]])]);
        if (a.Operator == Operators.Div) return Make(Operators.Mul, [args[0], new Apply(Operators.Pow, [args[1], new Number(-1)])]);
        if (a.Operator == Operators.Neg) return Make(Operators.Mul, [new Number(-1), args[0]]);
        if (a.Operator == Operators.Sqrt) return new Apply(Operators.Pow, [args[0], new Number(BigRational.Create(1, 2))]);
        if (a.Operator.Has(OperatorAttributes.Associative) && args.Any(x => x is Apply c && c.Operator == a.Operator)) return Make(a.Operator, args);
        return null;
    }

    // Builds an application, flattening associative operators.
    private static Apply Make(Operator op, ImmutableArray<Expr> args) =>
        new Apply(op, op.Has(OperatorAttributes.Associative) ? Flatten(op, args) : args);

    private static ImmutableArray<Expr> Flatten(Operator op, IEnumerable<Expr> args)
    {
        var result = ImmutableArray.CreateBuilder<Expr>();
        foreach (var arg in args)
        {
            if (arg is Apply a && a.Operator == op) result.AddRange(a.Arguments);
            else result.Add(arg);
        }
        return result.ToImmutable();
    }

    /// <summary>The Canonical level (automatic simplification); see <see cref="Normalizer"/> for exactly what it does and never does.</summary>
    public static Expr Canonical(Expr expr, NormalizeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expr);
        return new Engine(options ?? new NormalizeOptions()).Canon(Structural(expr));
    }

    private sealed class Engine(NormalizeOptions options)
    {
        private static readonly Expr Zero = new Number(0);
        private static readonly Expr One = new Number(1);

        public Expr Canon(Expr e)
        {
            switch (e)
            {
                case Apply a:
                    var args = new Expr[a.Arguments.Length];
                    for (var i = 0; i < args.Length; i++) args[i] = Canon(a.Arguments[i]);
                    return CanonApply(a.Operator, [.. args]);
                case Bind b:
                    return new Bind(b.Binder, b.Bound, [.. b.Data.Select(Canon)], Canon(b.Body));
                case SetLiteral s:
                    var elements = s.Elements.Select(Canon).Distinct().OrderBy(x => x, ExprOrder.Instance).ToImmutableArray();
                    return new SetLiteral(elements);
                case Wild w:
                    return w.Constraint is null ? w : new Wild(w.Name, Canon(w.Constraint));
                default:
                    var children = e.Children;
                    if (children.Length == 0) return e;
                    return e.WithChildren([.. children.Select(Canon)]);
            }
        }

        private Expr CanonApply(Operator op, ImmutableArray<Expr> args)
        {
            if (op == Operators.Add) return MakeSum(Flatten(op, args));
            if (op == Operators.Mul) return MakeProduct(Flatten(op, args));
            if (op == Operators.Pow) return MakePower(args[0], args[1]);
            if (op == Operators.Exp) return MakeExp(args[0]);
            if (op == Operators.Not) return MakeNot(args[0]);

            var folded = Fold(op, args);
            if (folded is not null) return folded;

            if (op.Has(OperatorAttributes.Associative)) args = Flatten(op, args);
            if (op.Absorbing is { } absorbing && op != Operators.Mul && args.Any(x => x.Equals(absorbing))) return absorbing;
            if (op.Identity is { } identity && op != Operators.Add && op != Operators.Mul)
            {
                var kept = args.Where(x => !x.Equals(identity)).ToImmutableArray();
                if (kept.Length != args.Length)
                {
                    if (kept.Length == 0) return identity;
                    if (kept.Length == 1 && op.Arity.Min >= 2) return kept[0];
                    args = kept;
                }
            }
            if (op == Operators.Intersect && args.Any(x => x is Constant { Id: ConstantId.EmptySet })) return Sym.EmptySet;
            if (op.Has(OperatorAttributes.Idempotent)) args = [.. args.Distinct()];
            // The two sides of an equation or inequality keep their orientation (x^2 - 5x + 6 = 0), so relations are not sorted.
            if (op.Has(OperatorAttributes.Commutative) && op.Family != OperatorFamily.Relation) args = [.. args.OrderBy(x => x, ExprOrder.Instance)];
            if (args.Length == 1 && op.Arity.Min >= 2 && op.Has(OperatorAttributes.Associative)) return args[0];
            return new Apply(op, args);
        }

        // Evaluation at identity points and exact numeric evaluation of simple functions and relations.
        private static Expr? Fold(Operator op, ImmutableArray<Expr> args)
        {
            if (args.Length == 1 && args[0] is Number n)
            {
                var v = n.Value;
                switch (op.Id)
                {
                    case "sin" or "tan" or "sinh" or "tanh" or "arcsin" or "arctan" or "arsinh" or "artanh" when v == BigRational.Zero: return Zero;
                    case "cos" or "cosh" or "sec" or "sech" when v == BigRational.Zero: return One;
                    case "arccos" or "arcosh" when v == BigRational.One: return Zero;
                    case "ln" when v == BigRational.One: return Zero;
                    case "abs": return new Number(BigRational.Abs(v));
                    case "sign": return new Number(v.Sign);
                    case "floor": return new Number(BigRational.Floor(v));
                    case "ceil": return new Number(BigRational.Ceiling(v));
                }
            }
            if (args.Length == 1 && op == Operators.Ln && args[0] is Constant { Id: ConstantId.E }) return One;
            if (args.Length == 2 && args[0] is Number a && args[1] is Number b)
            {
                bool? result = op.Id switch
                {
                    "eq" => a.Value == b.Value,
                    "ne" => a.Value != b.Value,
                    "lt" => a.Value < b.Value,
                    "le" => a.Value <= b.Value,
                    "gt" => a.Value > b.Value,
                    "ge" => a.Value >= b.Value,
                    _ => null,
                };
                if (result is { } r) return new Constant(r ? ConstantId.True : ConstantId.False);
            }
            return null;
        }

        private static Expr MakeNot(Expr a)
        {
            if (a is Constant { Id: ConstantId.True }) return Sym.False;
            if (a is Constant { Id: ConstantId.False }) return Sym.True;
            if (a is Apply { Operator.Id: "not" } inner) return inner.Arguments[0];
            return new Apply(Operators.Not, [a]);
        }

        private static Expr MakeExp(Expr a)
        {
            if (a is Number n)
            {
                if (n.Value == BigRational.Zero) return One;
                if (n.Value == BigRational.One) return Sym.E;
            }
            return new Apply(Operators.Exp, [a]);
        }

        private static bool ContainsNonFinite(Expr e) =>
            e.Walk().Any(w => w.Expr is Constant { Id: ConstantId.PositiveInfinity or ConstantId.NegativeInfinity or ConstantId.ComplexInfinity or ConstantId.Undefined });

        // ----- Sums -----

        private static (BigRational Coefficient, Expr Remainder) SplitCoefficient(Expr term)
        {
            if (term is Apply { Operator.Id: "mul" } m && m.Arguments[0] is Number c)
            {
                var rest = m.Arguments.RemoveAt(0);
                return (c.Value, rest.Length == 1 ? rest[0] : new Apply(Operators.Mul, rest));
            }
            return (BigRational.One, term);
        }

        private static Expr MakeSum(ImmutableArray<Expr> terms)
        {
            var exact = BigRational.Zero;
            var floatSum = 0.0;
            var hasFloat = false;
            var order = new List<Expr>();
            var coefficients = new Dictionary<Expr, BigRational>();
            var uncollected = new List<Expr>();
            foreach (var term in terms)
            {
                switch (term)
                {
                    case Number number:
                        exact += number.Value;
                        continue;
                    case Float f:
                        floatSum += f.Value;
                        hasFloat = true;
                        continue;
                }
                var (coefficient, rest) = SplitCoefficient(term);
                if (ContainsNonFinite(rest))
                {
                    uncollected.Add(term);
                    continue;
                }
                if (coefficients.TryGetValue(rest, out var existing)) coefficients[rest] = existing + coefficient;
                else
                {
                    coefficients[rest] = coefficient;
                    order.Add(rest);
                }
            }

            var result = new List<Expr>();
            var needsFlattening = false;
            foreach (var rest in order)
            {
                var c = coefficients[rest];
                if (c == BigRational.Zero) continue;
                result.Add(c == BigRational.One ? rest : MakeProduct([new Number(c), rest]));

                // 2(x + y) - (x + y) leaves a bare sum, which must join its siblings.
                needsFlattening |= c == BigRational.One && rest is Apply { Operator.Id: "add" };
            }
            result.AddRange(uncollected);
            if (needsFlattening)
            {
                if (hasFloat) result.Add(new Float((double)exact + floatSum));
                else if (exact != BigRational.Zero) result.Add(new Number(exact));
                return MakeSum(Flatten(Operators.Add, result));
            }
            if (hasFloat)
            {
                var total = (double)exact + floatSum;
                if (total != 0.0 || result.Count == 0) result.Add(new Float(total));
            }
            else if (exact != BigRational.Zero)
            {
                result.Add(new Number(exact));
            }

            result.Sort(ExprOrder.Instance);
            return result.Count switch
            {
                0 => Zero,
                1 => result[0],
                _ => new Apply(Operators.Add, [.. result]),
            };
        }

        // ----- Products -----

        private static bool IsNonScalar(Expr e) => e.Sort.IsAggregate;

        private static Expr MakeProduct(ImmutableArray<Expr> factors)
        {
            var exact = BigRational.One;
            var floatProduct = 1.0;
            var hasFloat = false;
            var scalars = new List<Expr>();
            var others = new List<Expr>();
            foreach (var factor in Flatten(Operators.Mul, factors))
            {
                switch (factor)
                {
                    case Number number:
                        exact *= number.Value;
                        break;
                    case Float f:
                        floatProduct *= f.Value;
                        hasFloat = true;
                        break;
                    default:
                        if (IsNonScalar(factor)) others.Add(factor);
                        else scalars.Add(factor);
                        break;
                }
            }

            // Collect like powers: x * x^2 -> x^3. Only integer exponents of the same sign combine (no side conditions).
            var order = new List<Expr>();
            var positive = new Dictionary<Expr, BigInteger>();
            var negative = new Dictionary<Expr, BigInteger>();
            var singles = new List<Expr>();
            foreach (var s in scalars)
            {
                Expr baseExpr = s;
                BigInteger exponent = BigInteger.One;
                if (s is Apply { Operator.Id: "pow" } p && p.Arguments[1] is Number en && en.Value.IsInteger && en.Value != BigRational.Zero)
                {
                    baseExpr = p.Arguments[0];
                    exponent = en.Value.Numerator;
                }
                else if (s is Apply { Operator.Id: "pow" })
                {
                    singles.Add(s);
                    continue;
                }
                var table = exponent.Sign > 0 ? positive : negative;
                if (!positive.ContainsKey(baseExpr) && !negative.ContainsKey(baseExpr)) order.Add(baseExpr);
                table[baseExpr] = table.TryGetValue(baseExpr, out var sum) ? sum + exponent : exponent;
            }

            var rebuilt = new List<Expr>(singles);
            foreach (var b in order)
            {
                if (positive.TryGetValue(b, out var pos)) rebuilt.Add(pos.IsOne ? b : new Apply(Operators.Pow, [b, new Number(new BigRational(pos))]));
                if (negative.TryGetValue(b, out var neg)) rebuilt.Add(new Apply(Operators.Pow, [b, new Number(new BigRational(neg))]));
            }
            rebuilt.Sort(ExprOrder.Instance);

            var result = new List<Expr>();
            if (hasFloat)
            {
                var total = (double)exact * floatProduct;
                if (total != 1.0 || (rebuilt.Count == 0 && others.Count == 0)) result.Add(new Float(total));
            }
            else if (exact != BigRational.One || (rebuilt.Count == 0 && others.Count == 0))
            {
                result.Add(new Number(exact));
            }
            result.AddRange(rebuilt);
            result.AddRange(others);
            return result.Count switch
            {
                0 => One,
                1 => result[0],
                _ => new Apply(Operators.Mul, [.. result]),
            };
        }

        // ----- Powers -----

        private Expr MakePower(Expr b, Expr e)
        {
            if (e is Number en)
            {
                if (en.Value == BigRational.Zero) return One;
                if (en.Value == BigRational.One) return b;
            }
            if (b is Number bn && bn.Value == BigRational.One && !ContainsNonFinite(e)) return One;
            if (b is Constant { Id: ConstantId.E }) return MakeExp(e);

            if (b is Number bExact && e is Number eExact && TryExactPower(bExact.Value, eExact.Value, out var folded)) return new Number(folded);
            if ((b is Float || e is Float) && IsNumeric(b) && IsNumeric(e))
            {
                var value = Math.Pow(ToDouble(b), ToDouble(e));
                if (double.IsFinite(value)) return new Float(value);
            }
            return new Apply(Operators.Pow, [b, e]);
        }

        private static bool IsNumeric(Expr e) => e is Number or Float;

        private static double ToDouble(Expr e) => e is Number n ? (double)n.Value : ((Float)e).Value;

        private bool TryExactPower(BigRational b, BigRational e, out BigRational result)
        {
            result = default;
            if (e.IsInteger)
            {
                if (BigInteger.Abs(e.Numerator) > 4096) return false;
                var n = (int)e.Numerator;
                if (b == BigRational.Zero)
                {
                    if (n <= 0) return false;
                    result = BigRational.Zero;
                    return true;
                }
                if ((b.Numerator.GetBitLength() + b.Denominator.GetBitLength()) * Math.Abs((long)n) > 20_000) return false;
                result = BigRational.Pow(b, n);
                return true;
            }

            // Rational exponent p/q: exact only when numerator and denominator are perfect q-th powers.
            var p = e.Numerator;
            var q = e.Denominator;
            if (q > 64 || BigInteger.Abs(p) > 64) return false;
            if (b == BigRational.Zero)
            {
                if (p.Sign <= 0) return false;
                result = BigRational.Zero;
                return true;
            }
            var negative = b.Sign < 0;
            var odd = !q.IsEven;
            if (negative && !(options.Field == NumberField.Real && odd)) return false;
            if (!TryRoot(BigInteger.Abs(b.Numerator), (int)q, out var rn) || !TryRoot(b.Denominator, (int)q, out var rd)) return false;
            var root = new BigRational(rn) / new BigRational(rd);
            if (negative) root = -root;
            result = BigRational.Pow(root, (int)p);
            return true;
        }

        // Exact integer q-th root of n >= 0, if n is a perfect power: Newton's iteration for the integer root, started above the root
        // (Brent and Zimmermann, Modern Computer Arithmetic, 2010, section 1.5.2). From any x >= floor(n^(1/q)) the integer step
        // decreases strictly until it reaches floor(n^(1/q)) and never goes below it, so the loop stops exactly there.
        private static bool TryRoot(BigInteger n, int q, out BigInteger root)
        {
            root = BigInteger.Zero;
            if (n.IsZero || n.IsOne)
            {
                root = n;
                return true;
            }

            // Start from the double estimate of 2^(log2(n)/q), widened and checked so that it is not below the root.
            var e = BigInteger.Log(n, 2) / q;
            var whole = (int)Math.Floor(e);
            var x = whole >= 52 ? new BigInteger(Math.Pow(2, e - whole + 52)) << (whole - 52) : new BigInteger(Math.Ceiling(Math.Pow(2, e)));
            x += (x >> 30) + 2;
            while (BigInteger.Pow(x, q) < n) x <<= 1;

            while (true)
            {
                var next = ((q - 1) * x + n / BigInteger.Pow(x, q - 1)) / q;
                if (next >= x) break;
                x = next;
            }
            root = x;
            return BigInteger.Pow(x, q) == n;
        }
    }
}
