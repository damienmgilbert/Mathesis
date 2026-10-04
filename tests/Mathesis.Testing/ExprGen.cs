using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace Mathesis.Testing;

/// <summary>Seeded generation of random expressions, and a plain double evaluator to check that rewrites preserve value.</summary>
public static class ExprGen
{
    private static readonly Symbol X = new("x");
    private static readonly Symbol Y = new("y");
    private static readonly Symbol Z = new("z");

    /// <summary>The symbols the generator uses.</summary>
    public static IReadOnlyList<Symbol> Symbols { get; } = [X, Y, Z];

    /// <summary>
    /// A random Raw expression over x, y, z with exact small numbers and the operators <c>+ − · / neg ^ sqrt exp ln sin cos abs</c>.
    /// Exponents are small integers (including negative ones), ½ and 2, so most trees evaluate at positive points.
    /// </summary>
    public static Expr RandomExpr(this Gen gen, int depth = 4)
    {
        ArgumentNullException.ThrowIfNull(gen);
        if (depth <= 0 || gen.Random.Next(6) == 0) return Leaf(gen);
        var r = gen.Random;
        switch (r.Next(15))
        {
            case 0 or 1: return Sym.Add(gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1));
            case 2: return Sym.Add(gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1));
            case 3: return Sym.Sub(gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1));
            case 4 or 5: return Sym.Mul(gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1));
            case 6: return Sym.Mul(gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1));
            case 7: return Sym.Div(gen.RandomExpr(depth - 1), gen.RandomExpr(depth - 1));
            case 8: return Sym.Neg(gen.RandomExpr(depth - 1));
            case 9 or 10:
                var exponents = new[] { 0, 1, 2, 3, -1, -2 };
                return Sym.Pow(gen.RandomExpr(depth - 1), Sym.Number(exponents[r.Next(exponents.Length)]));
            case 11: return Sym.Sqrt(gen.RandomExpr(depth - 1));
            case 12: return r.Next(3) switch { 0 => Sym.Exp(gen.RandomExpr(depth - 1)), 1 => Sym.Sin(gen.RandomExpr(depth - 1)), _ => Sym.Cos(gen.RandomExpr(depth - 1)) };
            case 13: return Sym.Abs(gen.RandomExpr(depth - 1));
            default: return Sym.Pow(gen.RandomExpr(depth - 1), Sym.Rational(1, 2));
        }
    }

    private static Expr Leaf(Gen gen)
    {
        var r = gen.Random;
        return r.Next(10) switch
        {
            0 or 1 or 2 => X,
            3 or 4 => Y,
            5 => Z,
            6 => Sym.Number(r.Next(-3, 4)),
            7 => Sym.Number(r.Next(1, 6)),
            8 => Sym.Rational(r.Next(-5, 6), r.Next(1, 5)),
            _ => Sym.Pi,
        };
    }

    /// <summary>
    /// Evaluates the arithmetic and elementary-function subset of expressions in <see cref="double"/> arithmetic, for checking
    /// that normalization preserves value. Returns NaN for anything it does not understand or for undefined results.
    /// </summary>
    public static double Evaluate(Expr expr, IReadOnlyDictionary<string, double> values)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(values);
        switch (expr)
        {
            case Number n:
                return (double)n.Value;
            case Float f:
                return f.Value;
            case Symbol s:
                return values.TryGetValue(s.Name, out var v) ? v : double.NaN;
            case Constant c:
                return c.Id switch
                {
                    ConstantId.Pi => Math.PI,
                    ConstantId.E => Math.E,
                    ConstantId.PositiveInfinity => double.PositiveInfinity,
                    ConstantId.NegativeInfinity => double.NegativeInfinity,
                    _ => double.NaN,
                };
            case Apply a:
                var args = a.Arguments.Select(x => Evaluate(x, values)).ToArray();
                return a.Operator.Id switch
                {
                    "add" => args.Sum(),
                    "sub" => args[0] - args[1],
                    "mul" => args.Aggregate(1.0, (p, q) => p * q),
                    "div" => args[1] == 0 ? double.NaN : args[0] / args[1],
                    "neg" => -args[0],
                    "pow" => RealPow(args[0], args[1]),
                    "sqrt" => args[0] < 0 ? double.NaN : Math.Sqrt(args[0]),
                    "exp" => Math.Exp(args[0]),
                    "ln" => args[0] > 0 ? Math.Log(args[0]) : double.NaN,
                    "sin" => Math.Sin(args[0]),
                    "cos" => Math.Cos(args[0]),
                    "tan" => Math.Tan(args[0]),
                    "abs" => Math.Abs(args[0]),
                    _ => double.NaN,
                };
            default:
                return double.NaN;
        }
    }

    // Real power: x^n for integer n is defined for any x; fractional powers need x >= 0.
    private static double RealPow(double b, double e)
    {
        if (double.IsNaN(b) || double.IsNaN(e) || (b == 0 && e < 0)) return double.NaN;
        if (e == Math.Floor(e)) return Math.Pow(b, e);
        return b < 0 ? double.NaN : Math.Pow(b, e);
    }
}
