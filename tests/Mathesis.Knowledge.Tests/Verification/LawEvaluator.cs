using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Numerics.Differentiation;
using Mathesis.Numerics.Integration;
using Mathesis.Symbolics;

namespace Mathesis.Knowledge.Tests.Verification;

/// <summary>The values a statement is evaluated at.</summary>
internal sealed class Env
{
    public Dictionary<string, Complex<double>> Values { get; init; } = [];

    /// <summary>Real mode: every value and every intermediate result is real; a function with a non-real result is undefined.</summary>
    public bool Real { get; init; } = true;

    /// <summary>Concrete functions standing in for function-valued variables: the body is an expression in <c>x</c>, with its derivative.</summary>
    public Dictionary<string, (Expr Body, Expr? Derivative)> Functions { get; init; } = [];

    /// <summary>Base points of indefinite integrals, by integration variable: <c>integrate(g, v)</c> means the integral from the base to the value of v.</summary>
    public Dictionary<string, double> IntegralBase { get; init; } = [];

    /// <summary>Values of matrix- and vector-valued variables (vectors are columns).</summary>
    public Dictionary<string, Complex<double>[,]> Matrices { get; init; } = [];

    /// <summary>The variable function-valued symbols are evaluated at when they appear without an argument.</summary>
    public string FunctionArgument { get; init; } = "x";

    public Env With(string name, Complex<double> value) =>
        new() { Values = new(Values) { [name] = value }, Matrices = Matrices, Real = Real, Functions = Functions, IntegralBase = IntegralBase, FunctionArgument = FunctionArgument };

    public Env With(string name, double value) => With(name, new Complex<double>(value));
}

/// <summary>
/// Evaluates catalog statements numerically in <see cref="double"/> and <c>Complex&lt;double&gt;</c> arithmetic: expressions, Boolean
/// structure, sums, products, definite and indefinite integrals, derivatives and limits. Undefined results are NaN (numbers) or
/// <c>null</c> (propositions); a sample at which something is undefined is skipped, which is how "both sides are defined" is implemented.
/// </summary>
internal sealed class LawEvaluator
{
    private static readonly Complex<double> NaN = new(double.NaN, double.NaN);

    /// <summary>Relative tolerance for comparing the two sides of an equation.</summary>
    public double Tolerance { get; init; } = 1e-9;

    /// <summary>
    /// The largest magnitude of an operand of an addition or subtraction seen since the last reset. Multiplicative operations have relative
    /// rounding error, but additions can cancel, so comparisons allow an absolute error proportional to this scale.
    /// </summary>
    private double _additiveScale;

    // Inside a limit the underflow of a quantity that tends to 0 is the expected behavior, not a loss of precision.
    private int _limitDepth;

    // Round-off measurement (see Sensitivity): while _noise is set, the result of every operation is perturbed by up to one ulp.
    private const int NoiseRuns = 8;
    private const double NoiseFactor = 8;
    private const int NoiseSeed = 0x5EED;
    private static readonly double Ulp = Math.BitIncrement(1.0) - 1.0;
    private Random? _noise;

    // Set by Truth: a failed comparison is then a counterexample only beyond the round-off measured at the sample.
    private bool _robust;

    private void Note(Complex<double> value)
    {
        var m = value.Magnitude;
        if (double.IsFinite(m) && m > _additiveScale) _additiveScale = m;
    }

    private static bool IsNaN(Complex<double> z) => double.IsNaN(z.Real) || double.IsNaN(z.Imaginary);

    private static Complex<double> R(double x) => double.IsNaN(x) ? NaN : new(x);

    // Real operands are multiplied and divided as doubles: the complex formulas overflow for results near the ends of the double range.
    private static Complex<double> Times(Complex<double> a, Complex<double> b) => a.Imaginary == 0 && b.Imaginary == 0 ? new(a.Real * b.Real) : a * b;

    private static Complex<double> Over(Complex<double> a, Complex<double> b) => a.Imaginary == 0 && b.Imaginary == 0 ? new(a.Real / b.Real) : a / b;

    // ----- Numbers -----

    public Complex<double> Eval(Expr e, Env env)
    {
        // A matrix-valued expression used as a scalar must be 1x1.
        if ((e is Symbol or Apply or MatrixLiteral) && MatrixValues.IsMatrixSort(e.Sort))
        {
            var matrix = EvalMatrix(e, env);
            return matrix is not null && matrix.GetLength(0) == 1 && matrix.GetLength(1) == 1 ? matrix[0, 0] : NaN;
        }
        switch (e)
        {
            case Number n: return R(n.Value.ToDouble());
            case Float f: return R(f.Value);
            case Constant c: return Const(c.Id, env);
            case Symbol s:
                if (env.Values.TryGetValue(s.Name, out var v)) return v;
                if (env.Functions.TryGetValue(s.Name, out var fn) && env.Values.TryGetValue(env.FunctionArgument, out var arg)) return Eval(fn.Body, FunctionEnv(env, arg));
                return NaN;
            case Apply a: return EvalApply(a, env);
            case Bind b: return EvalBind(b, env);
            case Piecewise p:
                foreach (var (value, condition) in p.Cases)
                {
                    var truth = TruthCore(condition, env);
                    if (truth is null) return NaN;
                    if (truth.Value) return Eval(value, env);
                }
                return NaN;
            default: return NaN;
        }
    }

    private static Env FunctionEnv(Env env, Complex<double> x) => new() { Values = new() { ["x"] = x }, Real = env.Real };

    private static Complex<double> Const(ConstantId id, Env env) => id switch
    {
        ConstantId.Pi => R(Math.PI),
        ConstantId.E => R(Math.E),
        ConstantId.GoldenRatio => R((1 + Math.Sqrt(5)) / 2),
        ConstantId.EulerGamma => R(0.57721566490153286),
        ConstantId.PositiveInfinity => R(double.PositiveInfinity),
        ConstantId.NegativeInfinity => R(double.NegativeInfinity),
        ConstantId.ImaginaryUnit => env.Real ? NaN : Complex<double>.ImaginaryOne,
        _ => NaN,
    };

    private Complex<double> EvalApply(Apply a, Env env)
    {
        var result = EvalApplyCore(a, env);

        // An infinite result from finite operands is an overflow, which the verifier treats as undefined (the sample is skipped).
        if ((double.IsInfinity(result.Real) || double.IsInfinity(result.Imaginary)) && a.Arguments.All(x => x is not Constant && double.IsFinite(Eval(x, env).Magnitude))) return NaN;

        // Likewise a non-zero result in the subnormal range has lost its precision: the sample is skipped.
        var magnitude = result.Magnitude;
        if (magnitude > 0 && magnitude < 1e-290 && _limitDepth == 0) return NaN;
        return _noise is null ? result : Perturb(result);
    }

    // Up to one ulp of relative error on each non-integer part. Integers stay exact: they are the operands of discrete tests (exponents,
    // sum bounds, floor).
    private Complex<double> Perturb(Complex<double> z)
    {
        double P(double x) => double.IsFinite(x) && x != Math.Floor(x) ? x * (1 + (_noise!.NextDouble() * 2 - 1) * Ulp) : x;
        return new(P(z.Real), P(z.Imaginary));
    }

    // The round-off allowance of a comparison: how far each side moves when the result of every operation is perturbed by up to one ulp
    // (stochastic arithmetic: Vignes' CESTAC, Parker's Monte Carlo arithmetic). Unlike perturbing the sample, this exposes amplification
    // inside the expression, such as the rounding error of cos(x) near -1 divided through by 1 + cos(x). The seed is fixed, so runs repeat.
    private double Sensitivity(Expr left, Expr right, Env env, Complex<double> l, Complex<double> r)
    {
        var scale = _additiveScale;
        _noise = new Random(NoiseSeed);
        double spreadLeft = 0, spreadRight = 0;
        try
        {
            for (var i = 0; i < NoiseRuns; i++)
            {
                // A perturbed value that is undefined or infinite gives a NaN or infinite deviation, which is ignored.
                var dl = (Eval(left, env) - l).Magnitude;
                var dr = (Eval(right, env) - r).Magnitude;
                if (double.IsFinite(dl) && dl > spreadLeft) spreadLeft = dl;
                if (double.IsFinite(dr) && dr > spreadRight) spreadRight = dr;
            }
        }
        finally
        {
            _noise = null;
            _additiveScale = scale;
        }
        return NoiseFactor * (spreadLeft + spreadRight);
    }

    private Complex<double> EvalApplyCore(Apply a, Env env)
    {
        var args = a.Arguments;
        var id = a.Operator.Id;

        // Calls of function-valued variables and their derivatives.
        if (id == "call") return EvalCall(a, env);
        if (id == "diff") return Derivative(a, env);
        if (id == "integrate") return Indefinite(a, env);
        if (id == "piecewise") return NaN;

        Complex<double> Arg(int i) => Eval(args[i], env);
        switch (id)
        {
            case "add":
                {
                    var sum = Complex<double>.Zero;
                    foreach (var x in args)
                    {
                        var term = Eval(x, env);
                        Note(term);
                        sum += term;
                    }
                    return sum;
                }
            case "mul":
                {
                    var product = Complex<double>.One;
                    foreach (var x in args)
                    {
                        var v = Eval(x, env);
                        if (IsNaN(v)) return NaN;
                        product = Times(product, v);
                    }
                    return product;
                }
            case "sub":
                {
                    var l = Arg(0);
                    var r = Arg(1);
                    Note(l);
                    Note(r);
                    return l - r;
                }
            case "neg": return new(0.0 - Arg(0).Real, 0.0 - Arg(0).Imaginary);
            case "div":
                {
                    var d = Arg(1);
                    if (d.Real == 0 && d.Imaginary == 0) return NaN;
                    return Over(Arg(0), d);
                }
            case "pow": return Power(args[0], args[1], env);
            case "root": return Root(Arg(0), Arg(1), env);
            case "log":
                if (args.Length == 1) return Over(Fn("ln", Arg(0), env), R(Math.Log(10)));
                return Over(Fn("ln", Arg(0), env), Fn("ln", Arg(1), env));
            case "atan2":
                {
                    var y = Arg(0);
                    var x = Arg(1);
                    return y.Imaginary != 0 || x.Imaginary != 0 || (x.Real == 0 && y.Real == 0) ? NaN : R(Math.Atan2(y.Real, x.Real));
                }
            case "max" or "min":
                {
                    var best = Arg(0);
                    for (var i = 1; i < args.Length; i++)
                    {
                        var v = Arg(i);
                        if (best.Imaginary != 0 || v.Imaginary != 0) return NaN;
                        best = id == "max" ? (v.Real > best.Real ? v : best) : (v.Real < best.Real ? v : best);
                    }
                    return best;
                }
            case "mod":
                {
                    var x = Arg(0);
                    var n = Arg(1);
                    if (x.Imaginary != 0 || n.Imaginary != 0 || n.Real == 0) return NaN;
                    return R(x.Real - n.Real * Math.Floor(x.Real / n.Real));
                }
            case "quo":
                {
                    var x = Arg(0);
                    var n = Arg(1);
                    return x.Imaginary != 0 || n.Imaginary != 0 || n.Real == 0 ? NaN : R(Math.Floor(x.Real / n.Real));
                }
            case "gcd" or "lcm":
                {
                    BigInteger acc = 0;
                    var first = true;
                    for (var i = 0; i < args.Length; i++)
                    {
                        var v = Arg(i);
                        if (v.Imaginary != 0 || v.Real != Math.Floor(v.Real) || Math.Abs(v.Real) > 1e15) return NaN;
                        var k = new BigInteger(v.Real);
                        acc = first ? BigInteger.Abs(k) : id == "gcd" ? BigInteger.GreatestCommonDivisor(acc, k) : acc.IsZero || k.IsZero ? BigInteger.Zero : BigInteger.Abs(acc * k) / BigInteger.GreatestCommonDivisor(acc, k);
                        first = false;
                    }
                    return R((double)acc);
                }
            case "binomial": return Real2(Arg(0), Arg(1), SpecialFunctions.Binomial);
            case "perm":
                {
                    var n = Arg(0);
                    var k = Arg(1);
                    if (n.Imaginary != 0 || k.Imaginary != 0 || k.Real < 0 || k.Real != Math.Floor(k.Real) || k.Real > 1000) return NaN;
                    var result = 1.0;
                    for (var i = 0; i < (int)k.Real; i++) result *= n.Real - i;
                    return R(result);
                }
            case "chebyshevT":
                {
                    var n = Arg(0);
                    var x = Arg(1);
                    return n.Imaginary != 0 || x.Imaginary != 0 || n.Real != Math.Floor(n.Real) || n.Real > 10000 ? NaN : R(SpecialFunctions.ChebyshevT((int)n.Real, x.Real));
                }
            case "det":
                {
                    var m = EvalMatrix(args[0], env);
                    if (m is null || m.GetLength(0) != m.GetLength(1)) return NaN;

                    // The rounding error of a determinant is bounded by the Hadamard bound (the product of the row norms), not by its value.
                    var hadamard = 1.0;
                    for (var i = 0; i < m.GetLength(0); i++)
                    {
                        var row = 0.0;
                        for (var j = 0; j < m.GetLength(1); j++) row += m[i, j].Magnitude * m[i, j].Magnitude;
                        hadamard *= Math.Sqrt(row);
                    }
                    Note(new Complex<double>(hadamard));
                    return MatrixValues.Determinant(m);
                }
            case "trace":
                {
                    var m = EvalMatrix(args[0], env);
                    if (m is null || m.GetLength(0) != m.GetLength(1)) return NaN;
                    var t = Complex<double>.Zero;
                    for (var i = 0; i < m.GetLength(0); i++) t += m[i, i];
                    return t;
                }
            case "hypot": return NaN;
            default:
                if (args.Length == 1) return Fn(id, Arg(0), env);
                return NaN;
        }
    }

    private static Complex<double> Real2(Complex<double> a, Complex<double> b, Func<double, double, double> f) =>
        a.Imaginary != 0 || b.Imaginary != 0 ? NaN : R(f(a.Real, b.Real));

    // ----- Powers and roots -----

    private Complex<double> Power(Expr baseExpr, Expr exponentExpr, Env env)
    {
        var b = Eval(baseExpr, env);
        var x = Eval(exponentExpr, env);
        if (IsNaN(b) || IsNaN(x)) return NaN;

        // conv.zero-to-the-zero and the zero base.
        if (b.Real == 0 && b.Imaginary == 0)
        {
            if (x.Imaginary == 0 && x.Real == 0) return Complex<double>.One;
            return x.Imaginary == 0 && x.Real > 0 ? Complex<double>.Zero : NaN;
        }
        if (b.Imaginary == 0 && x.Imaginary == 0)
        {
            if (x.Real == Math.Floor(x.Real) && Math.Abs(x.Real) < 1e9) return Pw(b.Real, x.Real);
            if (b.Real > 0) return Pw(b.Real, x.Real);

            // conv.real-odd-root: a literal exponent p/q with odd q is a real root of a negative base.
            if (TryLiteralRational(exponentExpr, out var p, out var q) && q % 2 != 0)
            {
                var magnitude = Math.Pow(-b.Real, (double)p / q);
                return magnitude == 0 ? NaN : R(p % 2 == 0 ? magnitude : -magnitude);
            }
            if (env.Real) return NaN;
        }
        else if (env.Real) return NaN;
        if (x.Imaginary == 0 && x.Real == Math.Floor(x.Real) && Math.Abs(x.Real) <= 1024)
        {
            var n = (int)x.Real;
            var result = Complex<double>.One;
            var factor = n < 0 ? Complex<double>.One / b : b;
            for (var i = 0; i < Math.Abs(n); i++) result *= factor;
            return result;
        }
        return Complex<double>.Exp(x * Complex<double>.Log(b));
    }

    // A power of a non-zero base that underflows to exactly 0 has lost all its precision: undefined for the verifier.
    private Complex<double> Pw(double b, double x)
    {
        var v = Math.Pow(b, x);
        return v == 0 && b != 0 && _limitDepth == 0 ? NaN : R(v);
    }

    private static bool TryLiteralRational(Expr e, out long p, out long q)
    {
        p = 0;
        q = 1;
        switch (e)
        {
            case Number n when n.Value.Numerator.GetBitLength() < 40 && n.Value.Denominator.GetBitLength() < 40:
                p = (long)n.Value.Numerator;
                q = (long)n.Value.Denominator;
                return true;
            case Apply { Operator.Id: "neg" } neg when TryLiteralRational(neg.Arguments[0], out var np, out var nq):
                p = -np;
                q = nq;
                return true;
            case Apply { Operator.Id: "div" } div when TryLiteralRational(div.Arguments[0], out var a, out var b) && TryLiteralRational(div.Arguments[1], out var c, out var d) && c != 0:
                p = a * d;
                q = b * c;
                if (q < 0)
                {
                    p = -p;
                    q = -q;
                }
                var g = (long)BigInteger.GreatestCommonDivisor(p, q);
                if (g > 1)
                {
                    p /= g;
                    q /= g;
                }
                return true;
            default: return false;
        }
    }

    private static Complex<double> Root(Complex<double> x, Complex<double> n, Env env)
    {
        if (IsNaN(x) || IsNaN(n) || n.Imaginary != 0 || n.Real != Math.Floor(n.Real) || n.Real < 1) return NaN;
        var k = (int)n.Real;
        if (x.Imaginary == 0 && env.Real)
        {
            if (x.Real >= 0) return R(Math.Pow(x.Real, 1.0 / k));
            return k % 2 == 1 ? R(-Math.Pow(-x.Real, 1.0 / k)) : NaN;
        }
        if (x.Imaginary == 0 && x.Real >= 0) return R(Math.Pow(x.Real, 1.0 / k));
        return Complex<double>.Exp(Complex<double>.Log(x) / R(k));
    }

    // ----- Elementary functions of one argument -----

    private Complex<double> Fn(string id, Complex<double> z, Env env)
    {
        if (IsNaN(z)) return NaN;

        // The circular functions have absolute error proportional to the size of the argument.
        if (id is "sin" or "cos" or "tan" or "cot" or "sec" or "csc") Note(z);
        if (z.Imaginary == 0)
        {
            var real = RealFn(id, z.Real);
            if (real is { } r && !double.IsNaN(r)) return new(r);
            if (env.Real) return NaN;
        }
        else if (env.Real) return NaN;
        return ComplexFn(id, z);
    }

    private static double? RealFn(string id, double x) => id switch
    {
        "sqrt" => x < 0 ? double.NaN : Math.Sqrt(x),
        "exp" => Math.Exp(x),
        "ln" => x > 0 ? Math.Log(x) : double.NaN,
        "sin" => Math.Sin(x),
        "cos" => Math.Cos(x),
        "tan" => Math.Cos(x) == 0 ? double.NaN : Math.Tan(x),
        "cot" => Math.Sin(x) == 0 ? double.NaN : Math.Cos(x) / Math.Sin(x),
        "sec" => Math.Cos(x) == 0 ? double.NaN : 1 / Math.Cos(x),
        "csc" => Math.Sin(x) == 0 ? double.NaN : 1 / Math.Sin(x),
        "arcsin" => Math.Asin(x),
        "arccos" => Math.Acos(x),
        "arctan" => Math.Atan(x),
        "arccot" => Math.PI / 2 - Math.Atan(x),
        "arcsec" => Math.Abs(x) < 1 ? double.NaN : Math.Acos(1 / x),
        "arccsc" => Math.Abs(x) < 1 ? double.NaN : Math.Asin(1 / x),
        "sinh" => Math.Sinh(x),
        "cosh" => Math.Cosh(x),
        "tanh" => Math.Tanh(x),
        "coth" => x == 0 ? double.NaN : 1 / Math.Tanh(x),
        "sech" => 1 / Math.Cosh(x),
        "csch" => x == 0 ? double.NaN : 1 / Math.Sinh(x),
        "arsinh" => Math.Asinh(x),
        "arcosh" => Math.Acosh(x),
        "artanh" => Math.Atanh(x),
        "arcoth" => Math.Abs(x) <= 1 ? double.NaN : Math.Atanh(1 / x),
        "arsech" => x <= 0 || x > 1 ? double.NaN : Math.Acosh(1 / x),
        "arcsch" => x == 0 ? double.NaN : Math.Asinh(1 / x),
        "abs" => Math.Abs(x),
        "sign" => Math.Sign(x),
        "floor" => Math.Floor(x),
        "ceil" => Math.Ceiling(x),
        "round" => Math.Round(x, MidpointRounding.AwayFromZero),
        "frac" => x - Math.Floor(x),
        "re" => x,
        "im" => 0,
        "conj" => x,
        "arg" => x >= 0 ? 0 : Math.PI,
        "erf" => SpecialFunctions.Erf(x),
        "gamma" => SpecialFunctions.Gamma(x),
        "digamma" => SpecialFunctions.Digamma(x),
        "lambertw" => SpecialFunctions.LambertW(x),
        "factorial" => x >= 0 && x == Math.Floor(x) && x < 171 ? SpecialFunctions.Factorial((int)x) : double.NaN,
        "si" => Si(x),
        _ => null,
    };

    private static double Si(double x)
    {
        var result = Quadrature.GaussKronrod<double>(t => Math.Abs(t) < 1e-12 ? 1 : Math.Sin(t) / t, 0, x);
        return result.Converged ? result.Value : double.NaN;
    }

    private static Complex<double> ComplexFn(string id, Complex<double> z)
    {
        var i = Complex<double>.ImaginaryOne;
        var one = Complex<double>.One;
        var two = new Complex<double>(2);
        switch (id)
        {
            case "sqrt": return Complex<double>.Sqrt(z);
            case "exp": return Complex<double>.Exp(z);
            case "ln": return z.Real == 0 && z.Imaginary == 0 ? NaN : Complex<double>.Log(z);
            case "sin": return new(Math.Sin(z.Real) * Math.Cosh(z.Imaginary), Math.Cos(z.Real) * Math.Sinh(z.Imaginary));
            case "cos": return new(Math.Cos(z.Real) * Math.Cosh(z.Imaginary), -Math.Sin(z.Real) * Math.Sinh(z.Imaginary));
            case "tan": return ComplexFn("sin", z) / ComplexFn("cos", z);
            case "cot": return ComplexFn("cos", z) / ComplexFn("sin", z);
            case "sec": return one / ComplexFn("cos", z);
            case "csc": return one / ComplexFn("sin", z);
            case "sinh": return new(Math.Sinh(z.Real) * Math.Cos(z.Imaginary), Math.Cosh(z.Real) * Math.Sin(z.Imaginary));
            case "cosh": return new(Math.Cosh(z.Real) * Math.Cos(z.Imaginary), Math.Sinh(z.Real) * Math.Sin(z.Imaginary));
            case "tanh": return ComplexFn("sinh", z) / ComplexFn("cosh", z);
            case "arcsin": return -i * Complex<double>.Log(i * z + Complex<double>.Sqrt(one - z * z));
            case "arccos": return new Complex<double>(Math.PI / 2) - ComplexFn("arcsin", z);
            case "arctan": return -i / two * (Complex<double>.Log(one + i * z) - Complex<double>.Log(one - i * z));
            case "arsinh": return Complex<double>.Log(z + Complex<double>.Sqrt(z * z + one));
            case "arcosh": return Complex<double>.Log(z + Complex<double>.Sqrt(z + one) * Complex<double>.Sqrt(z - one));
            case "artanh": return (Complex<double>.Log(one + z) - Complex<double>.Log(one - z)) / two;
            case "abs": return new(z.Magnitude);
            case "re": return new(z.Real);
            case "im": return new(z.Imaginary);
            case "conj": return new(z.Real, -z.Imaginary);
            case "arg": return new(z.Phase);
            case "sign": return z.Magnitude == 0 ? Complex<double>.Zero : z / new Complex<double>(z.Magnitude);
            default: return NaN;
        }
    }

    // ----- Matrices -----

    /// <summary>Evaluates a matrix- or vector-valued expression; <c>null</c> when it is undefined or not a matrix expression.</summary>
    public Complex<double>[,]? EvalMatrix(Expr e, Env env)
    {
        switch (e)
        {
            case Symbol s:
                return env.Matrices.TryGetValue(s.Name, out var m) ? m : null;
            case MatrixLiteral lit:
                {
                    var result = new Complex<double>[lit.Rows, lit.Columns];
                    for (var i = 0; i < lit.Rows; i++)
                    {
                        for (var j = 0; j < lit.Columns; j++)
                        {
                            var v = Eval(lit[i, j], env);
                            if (IsNaN(v)) return null;
                            result[i, j] = v;
                        }
                    }
                    return result;
                }
            case Apply a:
                return EvalMatrixApply(a, env);
            default:
                return null;
        }
    }

    private Complex<double>[,]? EvalMatrixApply(Apply a, Env env)
    {
        var args = a.Arguments;
        switch (a.Operator.Id)
        {
            case "add" or "sub":
                {
                    Complex<double>[,]? acc = null;
                    for (var i = 0; i < args.Length; i++)
                    {
                        var term = EvalMatrix(args[i], env);
                        if (term is null) return null;
                        if (acc is null) acc = term;
                        else if (acc.GetLength(0) != term.GetLength(0) || acc.GetLength(1) != term.GetLength(1)) return null;
                        else
                        {
                            var sign = a.Operator.Id == "sub" && i == 1 ? -1.0 : 1.0;
                            var left = acc;
                            acc = new Complex<double>[left.GetLength(0), left.GetLength(1)];
                            for (var r = 0; r < acc.GetLength(0); r++)
                            {
                                for (var c = 0; c < acc.GetLength(1); c++) acc[r, c] = left[r, c] + new Complex<double>(sign) * term[r, c];
                            }
                        }
                    }
                    return acc;
                }
            case "neg":
                return EvalMatrix(args[0], env) is { } negated ? MatrixValues.Map(negated, z => -z) : null;
            case "mul":
                {
                    Complex<double>[,]? acc = null;
                    var scalar = Complex<double>.One;
                    foreach (var arg in args)
                    {
                        if (MatrixValues.IsMatrixSort(arg.Sort))
                        {
                            var m = EvalMatrix(arg, env);
                            if (m is null) return null;
                            if (acc is null) acc = m;
                            else if (acc.GetLength(1) != m.GetLength(0)) return null;
                            else acc = MatrixValues.Multiply(acc, m);
                        }
                        else
                        {
                            var v = Eval(arg, env);
                            if (IsNaN(v)) return null;
                            scalar *= v;
                        }
                    }
                    return acc is null ? null : MatrixValues.Map(acc, z => z * scalar);
                }
            case "div":
                {
                    var m = EvalMatrix(args[0], env);
                    var d = Eval(args[1], env);
                    if (m is null || IsNaN(d) || (d.Real == 0 && d.Imaginary == 0)) return null;
                    return MatrixValues.Map(m, z => z / d);
                }
            case "pow":
                {
                    var m = EvalMatrix(args[0], env);
                    var k = Eval(args[1], env);
                    if (m is null || IsNaN(k) || k.Imaginary != 0 || k.Real != Math.Floor(k.Real) || Math.Abs(k.Real) > 64 || m.GetLength(0) != m.GetLength(1)) return null;
                    var baseMatrix = k.Real < 0 ? MatrixValues.Inverse(m) : m;
                    if (baseMatrix is null) return null;
                    var result = MatrixValues.Identity(m.GetLength(0));
                    for (var i = 0; i < (int)Math.Abs(k.Real); i++) result = MatrixValues.Multiply(result, baseMatrix);
                    return result;
                }
            case "transpose":
                return EvalMatrix(args[0], env) is { } t ? MatrixValues.Transpose(t) : null;
            case "conjTranspose":
                return EvalMatrix(args[0], env) is { } h ? MatrixValues.Map(MatrixValues.Transpose(h), z => new Complex<double>(z.Real, -z.Imaginary)) : null;
            case "inverse":
                return EvalMatrix(args[0], env) is { } inv && inv.GetLength(0) == inv.GetLength(1) ? MatrixValues.Inverse(inv) : null;
            case "adj":
                return EvalMatrix(args[0], env) is { } adj && adj.GetLength(0) == adj.GetLength(1) ? MatrixValues.Adjugate(adj) : null;
            case "identity":
                {
                    var n = Eval(args[0], env);
                    return IsNaN(n) || n.Real != Math.Floor(n.Real) || n.Real < 1 || n.Real > 12 ? null : MatrixValues.Identity((int)n.Real);
                }
            default:
                return null;
        }
    }

    // ----- Calls of function-valued variables -----

    private Complex<double> EvalCall(Apply a, Env env)
    {
        var callee = a.Arguments[0];
        var args = a.Arguments.Skip(1).Select(x => Eval(x, env)).ToArray();
        if (args.Length != 1 || IsNaN(args[0])) return NaN;
        switch (callee)
        {
            case Symbol s when env.Functions.TryGetValue(s.Name, out var fn):
                return Eval(fn.Body, FunctionEnv(env, args[0]));
            case Apply { Operator.Id: "derivativeOf" } d when d.Arguments[0] is Symbol f && d.Arguments[1] is Number { Value.IsInteger: true } order && order.Value == BigRational.One && env.Functions.TryGetValue(f.Name, out var fd) && fd.Derivative is { } derivative:
                return Eval(derivative, FunctionEnv(env, args[0]));
            default:
                return NaN;
        }
    }

    // ----- Binders: sums, products, definite integrals, limits -----

    private Complex<double> EvalBind(Bind b, Env env)
    {
        switch (b.Binder)
        {
            case Binder.Sum or Binder.Product:
                {
                    var lo = Eval(b.Data[0], env);
                    var hi = Eval(b.Data[1], env);
                    if (IsNaN(lo) || IsNaN(hi) || lo.Real != Math.Floor(lo.Real) || hi.Real != Math.Floor(hi.Real) || hi.Real - lo.Real > 100_000) return NaN;
                    var acc = b.Binder == Binder.Sum ? Complex<double>.Zero : Complex<double>.One;
                    for (var k = (long)lo.Real; k <= (long)hi.Real; k++)
                    {
                        var term = Eval(b.Body, env.With(b.Bound[0].Name, k));
                        if (b.Binder == Binder.Sum) Note(term);
                        acc = b.Binder == Binder.Sum ? acc + term : acc * term;
                    }
                    return acc;
                }
            case Binder.Integral:
                {
                    var lo = Eval(b.Data[0], env);
                    var hi = Eval(b.Data[1], env);
                    if (lo.Imaginary != 0 || hi.Imaginary != 0 || IsNaN(lo) || IsNaN(hi)) return NaN;
                    return Integrate(b.Body, b.Bound[0].Name, lo.Real, hi.Real, env);
                }
            case Binder.Limit:
                return Limit(b, env);
            default:
                return NaN;
        }
    }

    private Complex<double> Integrate(Expr body, string variable, double a, double b, Env env)
    {
        if (a == b) return Complex<double>.Zero;
        var undefined = false;
        double F(double t)
        {
            var v = Eval(body, env.With(variable, t));
            if (IsNaN(v) || v.Imaginary != 0) undefined = true;
            return double.IsNaN(v.Real) ? 0 : v.Real;
        }
        var infinite = double.IsInfinity(a) || double.IsInfinity(b);
        var result = infinite ? Quadrature.Infinite<double>(F, a, b) : Quadrature.GaussKronrod<double>(F, a, b);
        return undefined || !result.Converged || !double.IsFinite(result.Value) ? NaN : R(result.Value);
    }

    private Complex<double> Indefinite(Apply a, Env env)
    {
        if (a.Arguments[1] is not Symbol v || !env.IntegralBase.TryGetValue(v.Name, out var start) || !env.Values.TryGetValue(v.Name, out var end) || end.Imaginary != 0) return NaN;
        return Integrate(a.Arguments[0], v.Name, start, end.Real, env);
    }

    // The derivative of an expression with respect to a real variable, by Richardson extrapolation of central differences.
    private Complex<double> Derivative(Apply a, Env env)
    {
        if (a.Arguments[1] is not Symbol v || !env.Values.TryGetValue(v.Name, out var point) || point.Imaginary != 0) return NaN;
        var order = 1;
        if (a.Arguments.Length == 3)
        {
            var n = Eval(a.Arguments[2], env);
            if (IsNaN(n) || n.Real != Math.Floor(n.Real) || n.Real < 0 || n.Real > 3) return NaN;
            order = (int)n.Real;
        }
        var undefined = false;
        double G(double t)
        {
            var value = Eval(a.Arguments[0], env.With(v.Name, t));
            if (IsNaN(value) || value.Imaginary != 0) undefined = true;
            return value.Real;
        }
        var result = Differentiate(G, point.Real, order);

        // The absolute accuracy of a numeric derivative is bounded by the size of the function.
        Note(new Complex<double>(G(point.Real)));
        return undefined || !double.IsFinite(result) ? NaN : R(result);
    }

    // The derivative by Richardson extrapolation of central differences with a step relative to |x|. It is computed with two step sizes and
    // accepted only if they agree, so a sample whose stencil straddles a pole or a kink is skipped instead of giving a wrong value.
    private static double Differentiate(Func<double, double> g, double x, int order)
    {
        if (order == 0) return g(x);
        var step = 0.02 * Math.Max(Math.Abs(x), 0.05);
        Func<double, double> inner = order == 1 ? g : t => Differentiate(g, t, order - 1);
        var coarse = FiniteDifferences.Richardson<double>(inner, x, step).Value;
        var fine = FiniteDifferences.Richardson<double>(inner, x, step / 4).Value;
        return Math.Abs(coarse - fine) <= 1e-7 * Math.Max(1, Math.Abs(fine)) * (order == 1 ? 1 : 100) ? fine : double.NaN;
    }

    // A numeric limit: Richardson extrapolation in the distance h (or 1/x at infinity), accepted together with the value at the
    // smallest distance; both sides must agree for a two-sided limit.
    private Complex<double> Limit(Bind b, Env env)
    {
        _limitDepth++;
        try
        {
            return LimitCore(b, env);
        }
        finally
        {
            _limitDepth--;
        }
    }

    private Complex<double> LimitCore(Bind b, Env env)
    {
        var variable = b.Bound[0].Name;
        var target = Eval(b.Data[0], env);
        if (IsNaN(target) || target.Imaginary != 0) return NaN;
        var direction = b.Data.Length > 1 ? Eval(b.Data[1], env).Real : 0;
        var sides = direction > 0 ? new[] { 1.0 } : direction < 0 ? new[] { -1.0 } : [1.0, -1.0];
        var results = new List<double>();
        foreach (var side in sides)
        {
            var atInfinity = double.IsInfinity(target.Real);
            double Value(double h)
            {
                var x = atInfinity ? Math.Sign(target.Real) / h : target.Real + side * h;
                var v = Eval(b.Body, env.With(variable, x));
                return v.Imaginary != 0 ? double.NaN : v.Real;
            }
            if (atInfinity && side < 0) continue;
            var h0 = atInfinity ? 0.01 : 0.01 * Math.Max(1, Math.Abs(target.Real));
            double f1 = Value(h0), f2 = Value(h0 / 2), f4 = Value(h0 / 4), tiny = Value(1e-6 * Math.Max(1, atInfinity ? 1 : Math.Abs(target.Real)));
            if (double.IsNaN(f1) || double.IsNaN(f2) || double.IsNaN(f4) || double.IsNaN(tiny)) return NaN;
            var r1 = 2 * f2 - f1;
            var r2 = 2 * f4 - f2;
            var extrapolated = (4 * r2 - r1) / 3;

            // Prefer whichever estimate the sequence supports: they agree when the limit exists and is smooth.
            results.Add(Math.Abs(extrapolated - tiny) <= 1e-4 * Math.Max(1, Math.Abs(tiny)) ? extrapolated : tiny);
        }
        if (results.Count == 0) return NaN;
        if (results.Count == 2 && Math.Abs(results[0] - results[1]) > 1e-4 * Math.Max(1, Math.Abs(results[0]))) return NaN;
        return R(results[0]);
    }

    // ----- Propositions -----

    /// <summary>Evaluates a proposition; <c>null</c> when something in it is undefined.</summary>
    public bool? Truth(Expr e, Env env, bool robust = false)
    {
        _additiveScale = 0;
        _robust = robust;
        return TruthCore(e, env);
    }

    private bool? TruthCore(Expr e, Env env)
    {
        switch (e)
        {
            case Constant { Id: ConstantId.True }: return true;
            case Constant { Id: ConstantId.False }: return false;
            case Apply a: return TruthOfApply(a, env);
            case Bind { Binder: Binder.ForAll or Binder.Exists }: return null;
            default: return null;
        }
    }

    private bool? TruthOfApply(Apply a, Env env)
    {
        var args = a.Arguments;
        var op = a.Operator;
        if (op == Operators.And)
        {
            var anyFalse = false;
            var anyUnknown = false;
            foreach (var x in args)
            {
                var t = TruthCore(x, env);
                if (t is null) anyUnknown = true;
                else if (!t.Value) anyFalse = true;
            }
            return anyFalse ? false : anyUnknown ? null : true;
        }
        if (op == Operators.Or)
        {
            var anyTrue = false;
            var anyUnknown = false;
            foreach (var x in args)
            {
                var t = TruthCore(x, env);
                if (t is null) anyUnknown = true;
                else if (t.Value) anyTrue = true;
            }
            return anyTrue ? true : anyUnknown ? null : false;
        }
        if (op == Operators.Not) return TruthCore(args[0], env) is { } t0 ? !t0 : null;
        if (op == Operators.Implies)
        {
            var l = TruthCore(args[0], env);
            var r = TruthCore(args[1], env);
            if (l == false || r == true) return true;
            return l is null || r is null ? null : false;
        }
        if (op == Operators.Iff)
        {
            var l = TruthCore(args[0], env);
            var r = TruthCore(args[1], env);
            return l is null || r is null ? null : l == r;
        }
        if (op == Operators.Xor)
        {
            var count = 0;
            foreach (var x in args)
            {
                var t = TruthCore(x, env);
                if (t is null) return null;
                if (t.Value) count++;
            }
            return count % 2 == 1;
        }
        if (op == Operators.Element) return Member(args[0], args[1], env);
        if (op == Operators.NotElement) return Member(args[0], args[1], env) is { } m ? !m : null;
        if (op == Operators.Divides)
        {
            var d = Eval(args[0], env);
            var n = Eval(args[1], env);
            if (IsNaN(d) || IsNaN(n) || d.Imaginary != 0 || n.Imaginary != 0 || d.Real != Math.Floor(d.Real) || n.Real != Math.Floor(n.Real)) return null;
            return d.Real == 0 ? n.Real == 0 : Math.IEEERemainder(n.Real, d.Real) == 0 || n.Real % d.Real == 0;
        }
        if ((op == Operators.Eq || op == Operators.Ne) && (MatrixValues.IsMatrixSort(args[0].Sort) || MatrixValues.IsMatrixSort(args[1].Sort)))
        {
            var l = EvalMatrix(args[0], env);
            var r = EvalMatrix(args[1], env);
            if (l is null || r is null) return null;
            var equal = MatrixValues.Close(l, r, Tolerance);
            return op == Operators.Eq ? equal : !equal;
        }
        if (op == Operators.Eq || op == Operators.Ne || op == Operators.Lt || op == Operators.Le || op == Operators.Gt || op == Operators.Ge || op == Operators.Approx)
        {
            var l = Eval(args[0], env);
            var r = Eval(args[1], env);
            if (IsNaN(l) || IsNaN(r)) return null;
            var verdict = Compare(op, l, r);
            if (!_robust || _noise is not null || verdict != false) return verdict;

            // The comparison failed: that is a counterexample only if it still fails with the computed difference off by the measured
            // round-off. Otherwise the sample cannot decide the relation.
            var slack = Sensitivity(args[0], args[1], env, l, r);
            return slack > 0 && Compare(op, l, r, slack) != false ? null : verdict;
        }
        return null;
    }

    private bool? Compare(Operator op, Complex<double> l, Complex<double> r, double slack = 0)
    {
        // Relative tolerance, plus an absolute allowance for cancellation in additions (see _additiveScale). A round-off allowance (see
        // Sensitivity) moves the tolerance the way that favors the relation: wider for equality and non-strict order, narrower for
        // inequality and strict order.
        static double Finite(double x) => double.IsFinite(x) ? x : 0;
        var noise = (Tolerance > 1e-9 ? Tolerance : 1e-12) * _additiveScale;
        var floor = Tolerance > 1e-9 ? 1.0 : 0.0; // numeric limits, derivatives and integrals are only accurate to an absolute tolerance
        var tolerance = Tolerance * Math.Max(floor, Math.Max(Finite(l.Magnitude), Finite(r.Magnitude))) + noise + (op == Operators.Ne || op == Operators.Lt || op == Operators.Gt ? -slack : slack);
        bool Equal()
        {
            if (double.IsInfinity(l.Real) || double.IsInfinity(r.Real)) return l.Real == r.Real && l.Imaginary == r.Imaginary;
            return Math.Abs(l.Real - r.Real) <= tolerance && Math.Abs(l.Imaginary - r.Imaginary) <= tolerance;
        }
        if (op == Operators.Eq || op == Operators.Approx) return Equal();
        if (op == Operators.Ne) return !Equal();
        if (l.Imaginary != 0 || r.Imaginary != 0) return null;

        // Order relations hold unless the values are clearly in the other order; values within the tolerance count as equal.
        var difference = l.Real - r.Real;
        if (double.IsInfinity(l.Real) || double.IsInfinity(r.Real)) difference = l.Real == r.Real ? 0 : double.IsPositiveInfinity(l.Real) || double.IsNegativeInfinity(r.Real) ? 1 : -1;
        if (op == Operators.Lt) return difference < -tolerance;
        if (op == Operators.Le) return difference <= tolerance;
        if (op == Operators.Gt) return difference > tolerance;
        return difference >= -tolerance;
    }

    private bool? Member(Expr x, Expr set, Env env)
    {
        var v = Eval(x, env);
        if (IsNaN(v)) return null;
        switch (set)
        {
            case Constant c:
                var real = v.Imaginary == 0;
                var integer = real && Math.Abs(v.Real - Math.Round(v.Real)) <= 1e-9 * Math.Max(1, Math.Abs(v.Real));
                return c.Id switch
                {
                    ConstantId.Integers => integer,
                    ConstantId.Naturals => integer && v.Real > -0.5,
                    ConstantId.Rationals => real,
                    ConstantId.Reals => real,
                    ConstantId.Complexes => true,
                    ConstantId.EmptySet => false,
                    _ => null,
                };
            case IntervalLiteral i:
                {
                    var lo = Eval(i.Lower, env);
                    var hi = Eval(i.Upper, env);
                    if (v.Imaginary != 0 || IsNaN(lo) || IsNaN(hi)) return null;
                    var low = i.LowerClosed ? v.Real >= lo.Real : v.Real > lo.Real;
                    var high = i.UpperClosed ? v.Real <= hi.Real : v.Real < hi.Real;
                    return low && high;
                }
            case SetLiteral s:
                {
                    foreach (var element in s.Elements)
                    {
                        var ev = Eval(element, env);
                        if (IsNaN(ev)) return null;
                        if (Math.Abs(ev.Real - v.Real) <= 1e-9 && Math.Abs(ev.Imaginary - v.Imaginary) <= 1e-9) return true;
                    }
                    return false;
                }
            default:
                return null;
        }
    }
}
