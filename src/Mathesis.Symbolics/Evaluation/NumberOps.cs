using Mathesis.Numbers;

namespace Mathesis.Symbolics.Evaluation;

/// <summary>The elementary functions every number type of the evaluator supports directly; everything else is composed from these.</summary>
internal enum UnaryFn : byte
{
    Sqrt, Exp, Ln,
    Sin, Cos, Tan, Arcsin, Arccos, Arctan,
    Sinh, Cosh, Tanh, Arsinh, Arcosh, Artanh,
    Abs, Sign, Floor, Ceil, Round,
}

internal enum BinaryFn : byte
{
    Atan2, Min, Max,

    /// <summary>The real n-th root <c>root(x, n)</c>.</summary>
    Root,
}

/// <summary>Arithmetic and elementary functions for one number type of <see cref="CompiledExpr{T}"/>.</summary>
internal abstract class NumberOps<T>
    where T : struct
{
    public abstract T FromDouble(double value);

    /// <summary>A double that approximates a mathematical constant; interval types widen it to an enclosure.</summary>
    public virtual T FromApproximate(double value) => FromDouble(value);

    public abstract T FromRational(BigRational value);

    public abstract bool TryConstant(ConstantId id, out T value);

    public abstract T Add(T a, T b);

    public abstract T Sub(T a, T b);

    public abstract T Mul(T a, T b);

    public abstract T Div(T a, T b);

    public abstract T Neg(T a);

    /// <summary>General power; the principal value for complex numbers, <c>exp(y ln x)</c> (or the real power) otherwise.</summary>
    public abstract T Pow(T x, T y);

    public abstract T PowInt(T x, int n);

    /// <summary><c>x^(p/q)</c> in the real sense: for odd <c>q</c> the real root, so <c>(-8)^(1/3) = -2</c> (<c>conv.real-odd-root</c>).</summary>
    public abstract T PowRational(T x, int p, int q);

    public abstract bool Supports(UnaryFn fn);

    public abstract T Unary(UnaryFn fn, T x);

    public abstract bool Supports(BinaryFn fn);

    public abstract T Binary(BinaryFn fn, T a, T b);

    /// <summary>The ops for <typeparamref name="T"/>, or <c>null</c> when the evaluator does not support it.</summary>
    public static NumberOps<T>? For() =>
        typeof(T) == typeof(double) ? (NumberOps<T>)(object)DoubleOps.Instance
        : typeof(T) == typeof(Complex<double>) ? (NumberOps<T>)(object)ComplexOps.Instance
        : typeof(T) == typeof(Interval<double>) ? (NumberOps<T>)(object)IntervalOps.Instance
        : typeof(T) == typeof(Dual<double>) ? (NumberOps<T>)(object)DualOps.Instance
        : null;

    protected static bool RealConstant(ConstantId id, out double value)
    {
        switch (id)
        {
            case ConstantId.Pi: value = Math.PI; return true;
            case ConstantId.E: value = Math.E; return true;
            case ConstantId.GoldenRatio: value = (1 + Math.Sqrt(5)) / 2; return true;
            case ConstantId.EulerGamma: value = 0.57721566490153286; return true;
            case ConstantId.CatalanG: value = 0.91596559417721901; return true;
            case ConstantId.PositiveInfinity: value = double.PositiveInfinity; return true;
            case ConstantId.NegativeInfinity: value = double.NegativeInfinity; return true;
            default: value = 0; return false;
        }
    }
}

internal sealed class DoubleOps : NumberOps<double>
{
    public static readonly DoubleOps Instance = new();

    public override double FromDouble(double value) => value;

    public override double FromRational(BigRational value) => value.ToDouble();

    public override bool TryConstant(ConstantId id, out double value) => RealConstant(id, out value);

    public override double Add(double a, double b) => a + b;

    public override double Sub(double a, double b) => a - b;

    public override double Mul(double a, double b) => a * b;

    public override double Div(double a, double b) => a / b;

    public override double Neg(double a) => -a;

    public override double Pow(double x, double y) => Math.Pow(x, y);

    public override double PowInt(double x, int n) => Math.Pow(x, n);

    public override double PowRational(double x, int p, int q)
    {
        if (q % 2 == 0 || x >= 0) return Math.Pow(x, (double)p / q);
        var magnitude = Math.Pow(-x, (double)p / q);
        return p % 2 == 0 ? magnitude : -magnitude;
    }

    public override bool Supports(UnaryFn fn) => true;

    public override double Unary(UnaryFn fn, double x) => fn switch
    {
        UnaryFn.Sqrt => Math.Sqrt(x),
        UnaryFn.Exp => Math.Exp(x),
        UnaryFn.Ln => Math.Log(x),
        UnaryFn.Sin => Math.Sin(x),
        UnaryFn.Cos => Math.Cos(x),
        UnaryFn.Tan => Math.Tan(x),
        UnaryFn.Arcsin => Math.Asin(x),
        UnaryFn.Arccos => Math.Acos(x),
        UnaryFn.Arctan => Math.Atan(x),
        UnaryFn.Sinh => Math.Sinh(x),
        UnaryFn.Cosh => Math.Cosh(x),
        UnaryFn.Tanh => Math.Tanh(x),
        UnaryFn.Arsinh => Math.Asinh(x),
        UnaryFn.Arcosh => Math.Acosh(x),
        UnaryFn.Artanh => Math.Atanh(x),
        UnaryFn.Abs => Math.Abs(x),
        UnaryFn.Sign => double.IsNaN(x) ? double.NaN : Math.Sign(x),
        UnaryFn.Floor => Math.Floor(x),
        UnaryFn.Ceil => Math.Ceiling(x),
        UnaryFn.Round => Math.Round(x, MidpointRounding.AwayFromZero),
        _ => throw new ArgumentOutOfRangeException(nameof(fn)),
    };

    public override bool Supports(BinaryFn fn) => true;

    public override double Binary(BinaryFn fn, double a, double b) => fn switch
    {
        BinaryFn.Atan2 => Math.Atan2(a, b),
        BinaryFn.Min => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Min(a, b),
        BinaryFn.Max => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Max(a, b),
        BinaryFn.Root => RootOf(a, b),
        _ => throw new ArgumentOutOfRangeException(nameof(fn)),
    };

    // The real n-th root: odd integer n gives the real root of negative numbers.
    private static double RootOf(double x, double n)
    {
        if (n == Math.Floor(n) && Math.Abs(n) < 1e9 && n % 2 != 0 && x < 0) return -Math.Pow(-x, 1 / n);
        return Math.Pow(x, 1 / n);
    }
}

internal sealed class ComplexOps : NumberOps<Complex<double>>
{
    public static readonly ComplexOps Instance = new();

    private static readonly Complex<double> I = Complex<double>.ImaginaryOne;

    public override Complex<double> FromDouble(double value) => new(value);

    public override Complex<double> FromRational(BigRational value) => new(value.ToDouble());

    public override bool TryConstant(ConstantId id, out Complex<double> value)
    {
        if (id == ConstantId.ImaginaryUnit)
        {
            value = I;
            return true;
        }
        var ok = RealConstant(id, out var real);
        value = new(real);
        return ok;
    }

    public override Complex<double> Add(Complex<double> a, Complex<double> b) => a + b;

    public override Complex<double> Sub(Complex<double> a, Complex<double> b) => a - b;

    public override Complex<double> Mul(Complex<double> a, Complex<double> b) => a * b;

    public override Complex<double> Div(Complex<double> a, Complex<double> b) => a / b;

    // 0.0 − x turns −0 into +0, so that −4 is (−4, +0) and sqrt(−4) takes the branch 2i, not −2i.
    public override Complex<double> Neg(Complex<double> a) => new(0.0 - a.Real, 0.0 - a.Imaginary);

    public override Complex<double> Pow(Complex<double> x, Complex<double> y)
    {
        if (x.Real == 0 && x.Imaginary == 0) return y.Real == 0 && y.Imaginary == 0 ? Complex<double>.One : y.Real > 0 ? Complex<double>.Zero : new(double.NaN, double.NaN);
        if (y.Imaginary == 0 && y.Real == Math.Floor(y.Real) && Math.Abs(y.Real) <= 1 << 20) return PowInt(x, (int)y.Real);
        return Complex<double>.Exp(y * Complex<double>.Log(x));
    }

    public override Complex<double> PowInt(Complex<double> x, int n)
    {
        if (n < 0) return Complex<double>.One / PowInt(x, checked(-n));
        var result = Complex<double>.One;
        var b = x;
        for (var m = n; m > 0; m >>= 1)
        {
            if ((m & 1) == 1) result *= b;
            if (m > 1) b *= b;
        }
        return result;
    }

    // In the complex field the principal value is used (the real-root convention belongs to real mode).
    public override Complex<double> PowRational(Complex<double> x, int p, int q) => Pow(x, new Complex<double>((double)p / q));

    public override bool Supports(UnaryFn fn) => fn is not (UnaryFn.Sign or UnaryFn.Floor or UnaryFn.Ceil or UnaryFn.Round);

    public override Complex<double> Unary(UnaryFn fn, Complex<double> z)
    {
        var two = new Complex<double>(2);
        switch (fn)
        {
            case UnaryFn.Sqrt: return Complex<double>.Sqrt(z);
            case UnaryFn.Exp: return Complex<double>.Exp(z);
            case UnaryFn.Ln: return Complex<double>.Log(z);
            case UnaryFn.Sin: return new(Math.Sin(z.Real) * Math.Cosh(z.Imaginary), Math.Cos(z.Real) * Math.Sinh(z.Imaginary));
            case UnaryFn.Cos: return new(Math.Cos(z.Real) * Math.Cosh(z.Imaginary), -Math.Sin(z.Real) * Math.Sinh(z.Imaginary));
            case UnaryFn.Tan: return Unary(UnaryFn.Sin, z) / Unary(UnaryFn.Cos, z);
            case UnaryFn.Sinh: return new(Math.Sinh(z.Real) * Math.Cos(z.Imaginary), Math.Cosh(z.Real) * Math.Sin(z.Imaginary));
            case UnaryFn.Cosh: return new(Math.Cosh(z.Real) * Math.Cos(z.Imaginary), Math.Sinh(z.Real) * Math.Sin(z.Imaginary));
            case UnaryFn.Tanh: return Unary(UnaryFn.Sinh, z) / Unary(UnaryFn.Cosh, z);

            // Principal branches (Abramowitz and Stegun 4.4.26, 4.4.25, 4.4.22, 4.6.31, 4.6.21, 4.6.22).
            case UnaryFn.Arcsin: return -I * Complex<double>.Log(I * z + Complex<double>.Sqrt(Complex<double>.One - z * z));
            case UnaryFn.Arccos: return new Complex<double>(Math.PI / 2) - Unary(UnaryFn.Arcsin, z);
            case UnaryFn.Arctan: return -I / two * (Complex<double>.Log(Complex<double>.One + I * z) - Complex<double>.Log(Complex<double>.One - I * z));
            case UnaryFn.Arsinh: return Complex<double>.Log(z + Complex<double>.Sqrt(z * z + Complex<double>.One));
            case UnaryFn.Arcosh: return Complex<double>.Log(z + Complex<double>.Sqrt(z + Complex<double>.One) * Complex<double>.Sqrt(z - Complex<double>.One));
            case UnaryFn.Artanh: return (Complex<double>.Log(Complex<double>.One + z) - Complex<double>.Log(Complex<double>.One - z)) / two;
            case UnaryFn.Abs: return new(z.Magnitude);
            default: throw new ArgumentOutOfRangeException(nameof(fn));
        }
    }

    public override bool Supports(BinaryFn fn) => fn == BinaryFn.Root;

    public override Complex<double> Binary(BinaryFn fn, Complex<double> a, Complex<double> b) =>
        fn == BinaryFn.Root ? Pow(a, Complex<double>.One / b) : throw new ArgumentOutOfRangeException(nameof(fn));
}

internal sealed class IntervalOps : NumberOps<Interval<double>>
{
    public static readonly IntervalOps Instance = new();

    public override Interval<double> FromDouble(double value) => double.IsNaN(value) ? Interval<double>.Entire : Interval<double>.Point(value);

    public override Interval<double> FromApproximate(double value) => Interval<double>.Around(value, 2);

    public override Interval<double> FromRational(BigRational value)
    {
        var d = value.ToDouble();
        if (double.IsInfinity(d)) return Interval<double>.Entire;
        return BigRational.FromDouble(d) == value ? Interval<double>.Point(d) : Interval<double>.Around(d);
    }

    public override bool TryConstant(ConstantId id, out Interval<double> value)
    {
        if (!RealConstant(id, out var d))
        {
            value = default;
            return false;
        }
        value = double.IsInfinity(d) ? Interval<double>.Point(d) : Interval<double>.Around(d, 2);
        return true;
    }

    public override Interval<double> Add(Interval<double> a, Interval<double> b) => a + b;

    public override Interval<double> Sub(Interval<double> a, Interval<double> b) => a - b;

    public override Interval<double> Mul(Interval<double> a, Interval<double> b) => a * b;

    public override Interval<double> Div(Interval<double> a, Interval<double> b) => a / b;

    public override Interval<double> Neg(Interval<double> a) => -a;

    public override Interval<double> Pow(Interval<double> x, Interval<double> y) => Interval<double>.Pow(x, y);

    public override Interval<double> PowInt(Interval<double> x, int n) => Interval<double>.Pow(x, n);

    public override Interval<double> PowRational(Interval<double> x, int p, int q) => Interval<double>.Pow(Interval<double>.Root(x, q), p);

    public override bool Supports(UnaryFn fn) => true;

    public override Interval<double> Unary(UnaryFn fn, Interval<double> x) => fn switch
    {
        UnaryFn.Sqrt => Interval<double>.Sqrt(x),
        UnaryFn.Exp => Interval<double>.Exp(x),
        UnaryFn.Ln => Interval<double>.Ln(x),
        UnaryFn.Sin => Interval<double>.Sin(x),
        UnaryFn.Cos => Interval<double>.Cos(x),
        UnaryFn.Tan => Interval<double>.Tan(x),
        UnaryFn.Arcsin => Interval<double>.Asin(x),
        UnaryFn.Arccos => Interval<double>.Acos(x),
        UnaryFn.Arctan => Interval<double>.Atan(x),
        UnaryFn.Sinh => Interval<double>.Sinh(x),
        UnaryFn.Cosh => Interval<double>.Cosh(x),
        UnaryFn.Tanh => Interval<double>.Tanh(x),
        UnaryFn.Arsinh => Interval<double>.Asinh(x),
        UnaryFn.Arcosh => Interval<double>.Acosh(x),
        UnaryFn.Artanh => Interval<double>.Atanh(x),
        UnaryFn.Abs => Interval<double>.Abs(x),
        UnaryFn.Sign => Interval<double>.Sign(x),
        UnaryFn.Floor => Interval<double>.Floor(x),
        UnaryFn.Ceil => Interval<double>.Ceiling(x),
        UnaryFn.Round => Interval<double>.Round(x),
        _ => throw new ArgumentOutOfRangeException(nameof(fn)),
    };

    // atan2 over intervals is not provided: the result depends on which quadrants the box meets.
    public override bool Supports(BinaryFn fn) => fn != BinaryFn.Atan2;

    public override Interval<double> Binary(BinaryFn fn, Interval<double> a, Interval<double> b)
    {
        switch (fn)
        {
            case BinaryFn.Min: return Interval<double>.Min(a, b);
            case BinaryFn.Max: return Interval<double>.Max(a, b);
            case BinaryFn.Root:
                // root(x, n) for a point integer n is exact; any other exponent is x^(1/n) over the positive part.
                if (b.IsPoint && b.Lower == Math.Floor(b.Lower) && b.Lower >= 1 && b.Lower < 1 << 20) return Interval<double>.Root(a, (int)b.Lower);
                return Interval<double>.Pow(a, Interval<double>.Point(1) / b);
            default: throw new ArgumentOutOfRangeException(nameof(fn));
        }
    }
}

internal sealed class DualOps : NumberOps<Dual<double>>
{
    public static readonly DualOps Instance = new();

    public override Dual<double> FromDouble(double value) => Dual<double>.Constant(value);

    public override Dual<double> FromRational(BigRational value) => Dual<double>.Constant(value.ToDouble());

    public override bool TryConstant(ConstantId id, out Dual<double> value)
    {
        var ok = RealConstant(id, out var d);
        value = Dual<double>.Constant(d);
        return ok;
    }

    public override Dual<double> Add(Dual<double> a, Dual<double> b) => a + b;

    public override Dual<double> Sub(Dual<double> a, Dual<double> b) => a - b;

    public override Dual<double> Mul(Dual<double> a, Dual<double> b) => a * b;

    public override Dual<double> Div(Dual<double> a, Dual<double> b) => a / b;

    public override Dual<double> Neg(Dual<double> a) => -a;

    public override Dual<double> Pow(Dual<double> x, Dual<double> y) => y.Derivative == 0 ? Dual<double>.Pow(x, y.Value) : Dual<double>.Pow(x, y);

    public override Dual<double> PowInt(Dual<double> x, int n)
    {
        // d/dx x^n = n x^(n-1), valid for every real x (including 0 and negative x), unlike the exp-log form.
        var value = Math.Pow(x.Value, n);
        var derivative = n == 0 ? 0 : n * Math.Pow(x.Value, n - 1) * x.Derivative;
        return new(value, derivative);
    }

    public override Dual<double> PowRational(Dual<double> x, int p, int q)
    {
        var exponent = (double)p / q;
        if (q % 2 == 0 || x.Value >= 0) return Dual<double>.Pow(x, exponent);

        // Real odd root of a negative number: x^(p/q) = (−1)^p (−x)^(p/q).
        var r = Dual<double>.Pow(-x, exponent);
        return p % 2 == 0 ? r : -r;
    }

    public override bool Supports(UnaryFn fn) => true;

    public override Dual<double> Unary(UnaryFn fn, Dual<double> x) => fn switch
    {
        UnaryFn.Sqrt => Dual<double>.Sqrt(x),
        UnaryFn.Exp => Dual<double>.Exp(x),
        UnaryFn.Ln => Dual<double>.Log(x),
        UnaryFn.Sin => Dual<double>.Sin(x),
        UnaryFn.Cos => Dual<double>.Cos(x),
        UnaryFn.Tan => Dual<double>.Tan(x),
        UnaryFn.Arcsin => Dual<double>.Asin(x),
        UnaryFn.Arccos => Dual<double>.Acos(x),
        UnaryFn.Arctan => Dual<double>.Atan(x),
        UnaryFn.Sinh => Dual<double>.Sinh(x),
        UnaryFn.Cosh => Dual<double>.Cosh(x),
        UnaryFn.Tanh => Dual<double>.Tanh(x),
        UnaryFn.Arsinh => Dual<double>.Asinh(x),
        UnaryFn.Arcosh => Dual<double>.Acosh(x),
        UnaryFn.Artanh => Dual<double>.Atanh(x),
        UnaryFn.Abs => Dual<double>.Abs(x),
        UnaryFn.Sign => new(double.IsNaN(x.Value) ? double.NaN : Math.Sign(x.Value), 0),
        UnaryFn.Floor => new(Math.Floor(x.Value), 0),
        UnaryFn.Ceil => new(Math.Ceiling(x.Value), 0),
        UnaryFn.Round => new(Math.Round(x.Value, MidpointRounding.AwayFromZero), 0),
        _ => throw new ArgumentOutOfRangeException(nameof(fn)),
    };

    public override bool Supports(BinaryFn fn) => true;

    public override Dual<double> Binary(BinaryFn fn, Dual<double> a, Dual<double> b)
    {
        switch (fn)
        {
            case BinaryFn.Atan2: return Dual<double>.Atan2(a, b);
            case BinaryFn.Min: return a.Value <= b.Value ? a : b;
            case BinaryFn.Max: return a.Value >= b.Value ? a : b;
            default:
                // root(x, n) = x^(1/n) with the real odd root of a negative x.
                if (b.Derivative == 0 && b.Value == Math.Floor(b.Value) && Math.Abs(b.Value) < 1 << 20 && b.Value != 0) return b.Value > 0 ? PowRational(a, 1, (int)b.Value) : PowRational(a, -1, -(int)b.Value);
                return Dual<double>.Pow(a, Dual<double>.One / b);
        }
    }
}

/// <summary>Which operators are primitives of <see cref="NumberOps{T}"/> and how the others are composed from them.</summary>
internal static class OperatorMap
{
    private static readonly Number One = new(BigRational.One);

    private static Apply Div(Expr a, Expr b) => new(Operators.Div, [a, b]);

    private static Apply Call(Operator op, Expr x) => new(op, [x]);

    public static bool TryUnary(Apply a, out UnaryFn fn)
    {
        fn = a.Operator.Id switch
        {
            "sqrt" => UnaryFn.Sqrt,
            "exp" => UnaryFn.Exp,
            "ln" => UnaryFn.Ln,
            "sin" => UnaryFn.Sin,
            "cos" => UnaryFn.Cos,
            "tan" => UnaryFn.Tan,
            "arcsin" => UnaryFn.Arcsin,
            "arccos" => UnaryFn.Arccos,
            "arctan" => UnaryFn.Arctan,
            "sinh" => UnaryFn.Sinh,
            "cosh" => UnaryFn.Cosh,
            "tanh" => UnaryFn.Tanh,
            "arsinh" => UnaryFn.Arsinh,
            "arcosh" => UnaryFn.Arcosh,
            "artanh" => UnaryFn.Artanh,
            "abs" => UnaryFn.Abs,
            "sign" => UnaryFn.Sign,
            "floor" => UnaryFn.Floor,
            "ceil" => UnaryFn.Ceil,
            "round" when a.Arguments.Length == 1 => UnaryFn.Round,
            _ => (UnaryFn)255,
        };
        return fn != (UnaryFn)255;
    }

    public static bool TryBinary(Apply a, out BinaryFn fn)
    {
        fn = a.Operator.Id switch
        {
            "atan2" => BinaryFn.Atan2,
            "root" => BinaryFn.Root,
            _ => (BinaryFn)255,
        };
        return fn != (BinaryFn)255;
    }

    /// <summary>
    /// Evaluates a closed expression built from numbers with <c>+ − · /</c> and integer powers exactly, so that exponents such as
    /// <c>1/3</c> (which a Raw tree writes as <c>div(1, 3)</c>) are recognized as the rational they are.
    /// </summary>
    public static bool TryRational(Expr e, out BigRational value)
    {
        value = default;
        switch (e)
        {
            case Number n:
                value = n.Value;
                return true;
            case Apply a:
                var args = a.Arguments;
                var parts = new BigRational[args.Length];
                for (var i = 0; i < args.Length; i++)
                {
                    if (!TryRational(args[i], out parts[i])) return false;
                }
                switch (a.Operator.Id)
                {
                    case "neg": value = -parts[0]; return true;
                    case "add": value = parts.Aggregate((p, q) => p + q); return true;
                    case "sub": value = parts[0] - parts[1]; return true;
                    case "mul": value = parts.Aggregate((p, q) => p * q); return true;
                    case "div":
                        if (parts[1] == BigRational.Zero) return false;
                        value = parts[0] / parts[1];
                        return true;
                    case "pow":
                        if (!parts[1].IsInteger || BigRational.Abs(parts[1]) > 64 || (parts[0] == BigRational.Zero && parts[1].Sign <= 0)) return false;
                        var k = (int)parts[1].Numerator;
                        var result = BigRational.One;
                        for (var i = 0; i < Math.Abs(k); i++) result *= parts[0];
                        value = k >= 0 ? result : BigRational.One / result;
                        return true;
                    default: return false;
                }
            default:
                return false;
        }
    }

    /// <summary>The primitive expression a composite operator stands for, or <c>null</c>.</summary>
    public static Expr? Lower(Apply a)
    {
        var args = a.Arguments;
        switch (a.Operator.Id)
        {
            // log(x) is base 10 (conv.log-base); log(x, b) = ln x / ln b.
            case "log": return Div(Call(Operators.Ln, args[0]), Call(Operators.Ln, args.Length == 2 ? args[1] : new Number(10)));
            case "cot": return Div(One, Call(Operators.Tan, args[0]));
            case "sec": return Div(One, Call(Operators.Cos, args[0]));
            case "csc": return Div(One, Call(Operators.Sin, args[0]));
            case "coth": return Div(One, Call(Operators.Tanh, args[0]));
            case "sech": return Div(One, Call(Operators.Cosh, args[0]));
            case "csch": return Div(One, Call(Operators.Sinh, args[0]));

            // conv.arccot-range: arccot: ℝ → (0, π), so arccot x = π/2 − arctan x.
            case "arccot": return new Apply(Operators.Sub, [Div(new Constant(ConstantId.Pi), new Number(2)), Call(Operators.Arctan, args[0])]);
            case "arcsec": return Call(Operators.Arccos, Div(One, args[0]));
            case "arccsc": return Call(Operators.Arcsin, Div(One, args[0]));
            case "arcoth": return Call(Operators.Artanh, Div(One, args[0]));
            case "arsech": return Call(Operators.Arcosh, Div(One, args[0]));
            case "arcsch": return Call(Operators.Arsinh, Div(One, args[0]));
            default: return null;
        }
    }
}
