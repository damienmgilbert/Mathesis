using System.Numerics;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;

namespace Mathesis.Solving;

/// <summary>Exact radicals and the exact values of inverse trigonometric functions, the latter read from the catalog entries <c>trig.exact.*</c>.</summary>
internal static class ExactValues
{
    /// <summary>The exact value of <c>sqrt(q)</c> for a non-negative rational: <c>s·sqrt(t)</c> with <c>t</c> squarefree.</summary>
    public static Expr Sqrt(BigRational q)
    {
        if (q.Sign < 0) throw new ArgumentOutOfRangeException(nameof(q));
        if (q.Sign == 0) return new Number(BigRational.Zero);
        // sqrt(n/d) = sqrt(n·d)/d
        var scaled = q.Numerator * q.Denominator;
        var (outside, inside) = SquareFreePart(scaled);
        var coefficient = new BigRational(outside) / new BigRational(q.Denominator);
        if (inside.IsOne) return new Number(coefficient);
        Expr root = new Apply(Operators.Pow, [new Number(new BigRational(inside)), new Number(new BigRational(1) / new BigRational(2))]);
        return Normalizer.Canonical(coefficient == BigRational.One ? root : new Apply(Operators.Mul, [new Number(coefficient), root]));
    }

    // n = outside² · inside with inside squarefree (trial division; a large remaining cofactor is left inside).
    private static (BigInteger Outside, BigInteger Inside) SquareFreePart(BigInteger n)
    {
        BigInteger outside = BigInteger.One, inside = BigInteger.One;
        var rest = n;
        for (BigInteger p = 2; p * p <= rest && p < 1_000_000; p += p == 2 ? 1 : 2)
        {
            var exponent = 0;
            while ((rest % p).IsZero)
            {
                rest /= p;
                exponent++;
            }
            if (exponent > 0)
            {
                outside *= BigInteger.Pow(p, exponent / 2);
                if (exponent % 2 == 1) inside *= p;
            }
        }
        return (outside, inside * rest);
    }

    private static readonly Lazy<Dictionary<(string Function, Expr Value), Expr>> Table = new(Build);

    // The first-quadrant angles of the entries trig.exact.*: (sin | cos | tan, value) → angle.
    private static Dictionary<(string, Expr), Expr> Build()
    {
        var table = new Dictionary<(string, Expr), Expr>();
        foreach (var entry in KnowledgeBase.Default.Entries)
        {
            if (!entry.Id.Value.StartsWith("trig.exact.", StringComparison.Ordinal) || entry.Statement is null) continue;
            var pending = new Stack<Expr>([entry.Statement]);
            while (pending.Count > 0)
            {
                var e = pending.Pop();
                if (e is Apply { Operator: var and, Arguments: var parts } && and == Operators.And)
                {
                    foreach (var p in parts) pending.Push(p);
                }
                else if (e is Apply { Operator: var eq, Arguments: [Apply { Operator.Id: "sin" or "cos" or "tan" } call, var value] } && eq == Operators.Eq && call.Arguments is [var angle])
                {
                    table[(call.Operator.Id, Normalizer.Canonical(value))] = Normalizer.Canonical(angle);
                }
            }
        }
        return table;
    }

    /// <summary>The angle in [0, π/2] whose <paramref name="function"/> (sin, cos or tan) is <paramref name="value"/>, if it is a catalog value.</summary>
    public static Expr? FirstQuadrantAngle(string function, Expr value) => Table.Value.TryGetValue((function, Normalizer.Canonical(value)), out var angle) ? angle : null;

    /// <summary>The principal value of arcsin, arccos or arctan of an exact value when the catalog knows it; otherwise the unevaluated function.</summary>
    public static Expr Inverse(string function, Expr value)
    {
        var pi = new Constant(ConstantId.Pi);
        value = Normalizer.Canonical(value);
        var negative = DomainNumber(value) is { } d && d < 0;
        var magnitude = negative ? Normalizer.Canonical(new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), value])) : value;
        switch (function)
        {
            case "arcsin" or "arctan":
            {
                var forward = function == "arcsin" ? "sin" : "tan";
                if (FirstQuadrantAngle(forward, magnitude) is { } angle) return Normalizer.Canonical(negative ? new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), angle]) : angle);
                break;
            }
            case "arccos":
                if (FirstQuadrantAngle("cos", magnitude) is { } c) return Normalizer.Canonical(negative ? new Apply(Operators.Add, [pi, new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), c])]) : c);
                break;
        }
        return Normalizer.Canonical(new Apply(Operators.Get(function), [value]));
    }

    private static double? DomainNumber(Expr e) => e.FreeSymbols.Count == 0 && Mathesis.Symbolics.Evaluation.Evaluator.N(e) is Outcome<double>.Success { Value: var v } ? v : null;
}
