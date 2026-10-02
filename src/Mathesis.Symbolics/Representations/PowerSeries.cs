using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Representations;

/// <summary>
/// A truncated Laurent series in one variable <c>t</c> with rational coefficients: <c>Σ c_k t^k + O(t^Order)</c> for <c>k</c> from
/// <see cref="Low"/> to <c>Order − 1</c> (docs/design/04-type-system.md, "Polynomial and series types"). Arithmetic keeps track of the order term, so
/// a result is exact up to its order and never claims more. Used by Taylor expansion and by limits through series.
/// </summary>
public sealed class PowerSeries
{
    private readonly BigRational[] _coefficients;

    private PowerSeries(int low, int order, BigRational[] coefficients)
    {
        Low = low;
        Order = order;
        _coefficients = coefficients;
    }

    /// <summary>The lowest power that is stored (the coefficients below it are zero).</summary>
    public int Low { get; }

    /// <summary>The series is exact below this power: the error is <c>O(t^Order)</c>.</summary>
    public int Order { get; }

    /// <summary>The coefficient of <c>t^k</c> (zero outside the stored range; for <c>k ≥ Order</c> it is unknown and reads as zero).</summary>
    public BigRational this[int k] => k >= Low && k < Order ? _coefficients[k - Low] : BigRational.Zero;

    /// <summary>The power of the first non-zero coefficient, or <c>null</c> when every stored coefficient is zero (the order is too low to tell).</summary>
    public int? Valuation
    {
        get
        {
            for (var k = Low; k < Order; k++)
            {
                if (_coefficients[k - Low] != BigRational.Zero) return k;
            }
            return null;
        }
    }

    /// <summary>A constant known exactly (as far as <paramref name="order"/>).</summary>
    public static PowerSeries Constant(BigRational c, int order) => order <= 0 ? new(order, order, []) : new(0, order, [c, .. Enumerable.Repeat(BigRational.Zero, order - 1)]);

    /// <summary>The series <c>t</c> to <paramref name="order"/>.</summary>
    public static PowerSeries Variable(int order) => order <= 1 ? new(order, order, []) : new(0, order, [BigRational.Zero, BigRational.One, .. Enumerable.Repeat(BigRational.Zero, order - 2)]);

    /// <summary>The series with the coefficients <paramref name="c"/> starting at power <paramref name="low"/>, exact below <paramref name="order"/>.</summary>
    public static PowerSeries Of(int low, int order, IEnumerable<BigRational> c)
    {
        var list = c.Take(Math.Max(0, order - low)).ToList();
        while (list.Count < order - low) list.Add(BigRational.Zero);
        return new(low, order, [.. list]);
    }

    /// <summary>The sum.</summary>
    public static PowerSeries operator +(PowerSeries a, PowerSeries b)
    {
        var order = Math.Min(a.Order, b.Order);
        var low = Math.Min(a.Low, b.Low);
        var c = new BigRational[Math.Max(0, order - low)];
        for (var k = low; k < order; k++) c[k - low] = a[k] + b[k];
        return new(Math.Min(low, order), order, c);
    }

    /// <summary>The negation.</summary>
    public static PowerSeries operator -(PowerSeries a) => new(a.Low, a.Order, [.. a._coefficients.Select(c => -c)]);

    /// <summary>The difference.</summary>
    public static PowerSeries operator -(PowerSeries a, PowerSeries b) => a + -b;

    /// <summary>The product.</summary>
    public static PowerSeries operator *(PowerSeries a, PowerSeries b)
    {
        var low = a.Low + b.Low;
        var order = Math.Min(a.Order + b.Low, b.Order + a.Low);
        var c = new BigRational[Math.Max(0, order - low)];
        for (var i = a.Low; i < a.Order; i++)
        {
            var ai = a[i];
            if (ai == BigRational.Zero) continue;
            for (var j = b.Low; j < b.Order; j++)
            {
                var k = i + j;
                if (k >= order) break;
                c[k - low] += ai * b[j];
            }
        }
        return new(Math.Min(low, order), order, c);
    }

    /// <summary>The product with a rational constant.</summary>
    public PowerSeries Scale(BigRational r) => new(Low, Order, [.. _coefficients.Select(c => c * r)]);

    /// <summary>The same series with the leading zero coefficients dropped, so that <see cref="Low"/> is the valuation.</summary>
    public PowerSeries Trim() => Valuation is { } v && v > Low ? new(v, Order, _coefficients[(v - Low)..]) : this;

    /// <summary>Multiplies by <c>t^k</c> (k may be negative).</summary>
    public PowerSeries Shift(int k) => new(Low + k, Order + k, _coefficients);

    /// <summary>The reciprocal, or <c>null</c> when the leading coefficient cannot be found at this order.</summary>
    public PowerSeries? Reciprocal()
    {
        if (Valuation is not { } v) return null;
        // 1/(c_v t^v (1 + u)): invert the unit part by the recurrence b_0 = 1/c_0, b_k = −(1/c_0) Σ_{j=1..k} c_j b_{k−j}.
        var n = Order - v;
        if (n <= 0) return null;
        var unit = new BigRational[n];
        for (var i = 0; i < n; i++) unit[i] = this[v + i];
        var inverse = new BigRational[n];
        inverse[0] = BigRational.One / unit[0];
        for (var k = 1; k < n; k++)
        {
            var sum = BigRational.Zero;
            for (var j = 1; j <= k; j++) sum += unit[j] * inverse[k - j];
            inverse[k] = -sum * inverse[0];
        }
        return new(-v, -v + n, inverse);
    }

    /// <summary>The quotient, or <c>null</c> when the divisor cannot be inverted at this order.</summary>
    public static PowerSeries? Divide(PowerSeries a, PowerSeries b) => b.Reciprocal() is { } r ? a * r : null;

    /// <summary>A non-negative integer power.</summary>
    public PowerSeries Pow(int n)
    {
        if (n == 0) return Constant(BigRational.One, Math.Max(Order, 1));
        var result = this;
        for (var i = 1; i < n; i++) result *= this;
        return result;
    }

    /// <summary>
    /// The composition <c>f(this)</c> for <c>f(u) = Σ f_j u^j</c> given by <paramref name="f"/>, which supplies <c>f_j</c>; <c>this</c> must have
    /// no constant term and no negative powers.
    /// </summary>
    public PowerSeries? Compose(Func<int, BigRational> f)
    {
        var self = Trim();
        if (self.Low < 0) return null;
        var valuation = self.Valuation ?? self.Order;
        if (valuation < 1) return null;
        var terms = self.Order / valuation + 1;
        var order = Math.Min(self.Order, (terms + 1) * valuation);
        var result = Constant(f(0), order);
        var power = Constant(BigRational.One, order);
        for (var j = 1; j <= terms; j++)
        {
            power *= self;
            var c = f(j);
            if (c != BigRational.Zero) result += power.Scale(c);
        }
        return result;
    }

    /// <summary>The expression <c>Σ c_k (x − center)^k</c> for the non-negative powers (the order term is not part of the expression).</summary>
    public Expr ToExpression(Symbol x, Expr center)
    {
        var shifted = center is Number { Value.Sign: 0 } ? (Expr)x : new Apply(Operators.Add, [x, new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), center])]);
        var terms = new List<Expr>();
        for (var k = Low; k < Order; k++)
        {
            var c = this[k];
            if (c == BigRational.Zero) continue;
            Expr power = k == 0 ? new Number(BigRational.One) : k == 1 ? shifted : new Apply(Operators.Pow, [shifted, new Number(k)]);
            terms.Add(k == 0 ? new Number(c) : new Apply(Operators.Mul, [new Number(c), power]));
        }
        return terms.Count == 0 ? new Number(BigRational.Zero) : terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]);
    }

    /// <summary>
    /// The series of <paramref name="e"/> around <paramref name="center"/> in <paramref name="x"/> to relative precision <paramref name="order"/>
    /// (powers of <c>t = x − center</c> below <paramref name="order"/> are exact), or <c>null</c> when <paramref name="e"/> contains something the
    /// rational series arithmetic cannot expand: a symbol other than <paramref name="x"/>, a function value that is irrational at the center, an
    /// unsupported function.
    /// </summary>
    public static PowerSeries? FromExpression(Expr e, Symbol x, BigRational center, int order, bool rightSide = true)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(x);
        return Build(e, x, center, order, rightSide);
    }

    private static PowerSeries? Build(Expr e, Symbol x, BigRational center, int order, bool right)
    {
        switch (e)
        {
            case Number n:
                return Constant(n.Value, order);
            case Symbol s when s.Equals(x):
                return Variable(order) + Constant(center, order);
            case Apply { Operator: var op, Arguments: var args }:
                return BuildApply(op, args, x, center, order, right);
            default:
                return null;
        }
    }

    private static PowerSeries? BuildApply(Operator op, ImmutableArray<Expr> args, Symbol x, BigRational center, int order, bool right)
    {
        if (op == Operators.Add || op == Operators.Mul)
        {
            // Products of series with poles lose precision; ask for more terms of the factors.
            var padded = op == Operators.Mul ? order + 4 : order;
            PowerSeries? total = null;
            foreach (var a in args)
            {
                if (Build(a, x, center, padded, right) is not { } s) return null;
                total = total is null ? s : op == Operators.Add ? total + s : total * s;
            }
            return total;
        }
        if (op == Operators.Pow)
        {
            if (args[1] is not Number { Value: var p }) return null;
            if (Build(args[0], x, center, order + 4, right) is not { } b) return null;
            if (p.IsInteger)
            {
                var k = (int)p.Numerator;
                if (Math.Abs(k) > 64) return null;
                if (k >= 0) return b.Pow(k);
                return b.Reciprocal() is { } r ? r.Pow(-k) : null;
            }
            // (c t^v (1 + u))^p = c^p t^(v p) (1 + u)^p: v·p must be an integer, c positive with c^p rational, and for t < 0 only even v is allowed
            // ((t^v)^p = |t|^(v p)).
            if (b.Valuation is not { } v) return null;
            var c0 = b[v];
            var vp = new BigRational(v) * p;
            if (c0.Sign <= 0 || !vp.IsInteger || !TryRationalPower(c0, p, out var c0p)) return null;
            if (!right && v % 2 != 0) return null;
            var unit = b.Trim().Shift(-v).Scale(BigRational.One / c0) - Constant(BigRational.One, b.Order - v);
            var binomial = unit.Compose(j => Binomial(p, j));
            if (binomial is null) return null;
            var sign = !right && vp.Numerator.IsEven == false ? BigRational.NegativeOne : BigRational.One;
            return binomial.Scale(c0p * sign).Shift((int)vp.Numerator);
        }
        if (args.Length != 1) return null;
        if (Build(args[0], x, center, order + 2, right) is not { } inner) return null;
        var c = inner[0];
        var u0 = inner - Constant(c, inner.Order);
        switch (op.Id)
        {
            case "exp" when c == BigRational.Zero: return u0.Compose(j => BigRational.One / Factorial(j));
            case "sin" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 1 ? Sign(j / 2) / Factorial(j) : BigRational.Zero);
            case "cos" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 0 ? Sign(j / 2) / Factorial(j) : BigRational.Zero);
            case "sinh" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 1 ? BigRational.One / Factorial(j) : BigRational.Zero);
            case "cosh" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 0 ? BigRational.One / Factorial(j) : BigRational.Zero);
            case "arctan" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 1 ? Sign(j / 2) / new BigRational(j) : BigRational.Zero);
            case "arsinh" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 1 ? Sign(j / 2) * Central(j / 2) / new BigRational(j) : BigRational.Zero);
            case "arcsin" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 1 ? Central(j / 2) / new BigRational(j) : BigRational.Zero);
            case "artanh" when c == BigRational.Zero: return u0.Compose(j => j % 2 == 1 ? BigRational.One / new BigRational(j) : BigRational.Zero);
            case "ln" when c == BigRational.One: return u0.Compose(j => j == 0 ? BigRational.Zero : Sign(j + 1) / new BigRational(j));
            case "tan" when c == BigRational.Zero:
                return Build(new Apply(Operators.Mul, [new Apply(Operators.Sin, [args[0]]), new Apply(Operators.Pow, [new Apply(Operators.Cos, [args[0]]), new Number(BigRational.NegativeOne)])]), x, center, order, right);
            default: return null;
        }
    }

    private static BigRational Sign(int k) => k % 2 == 0 ? BigRational.One : BigRational.NegativeOne;

    private static BigRational Factorial(int n)
    {
        var r = BigInteger.One;
        for (var i = 2; i <= n; i++) r *= i;
        return new BigRational(r);
    }

    // binomial(2k, k) / (4^k (2k + 1)... the coefficient helper for arcsin: (2k)! / (4^k (k!)^2)
    private static BigRational Central(int k) => Factorial(2 * k) / (new BigRational(BigInteger.Pow(4, k)) * Factorial(k) * Factorial(k));

    private static BigRational Binomial(BigRational alpha, int j)
    {
        var r = BigRational.One;
        for (var i = 0; i < j; i++) r = r * (alpha - new BigRational(i)) / new BigRational(i + 1);
        return r;
    }

    private static bool TryRationalPower(BigRational b, BigRational p, out BigRational result)
    {
        result = BigRational.One;
        if (b == BigRational.One) return true;
        // Only exact rational results: b^(m/n) with b a perfect n-th power of rationals.
        var n = (int)p.Denominator;
        var m = (int)p.Numerator;
        if (!TryRoot(b.Numerator, n, out var num) || !TryRoot(b.Denominator, n, out var den)) return false;
        result = BigRational.Pow(new BigRational(num) / new BigRational(den), m);
        return true;
    }

    private static bool TryRoot(BigInteger value, int n, out BigInteger root)
    {
        root = BigInteger.Zero;
        if (value.Sign < 0) return false;
        var guess = (BigInteger)Math.Round(Math.Pow((double)value, 1.0 / n));
        for (var d = -1; d <= 1; d++)
        {
            var candidate = guess + d;
            if (candidate.Sign >= 0 && BigInteger.Pow(candidate, n) == value)
            {
                root = candidate;
                return true;
            }
        }
        return false;
    }
}
