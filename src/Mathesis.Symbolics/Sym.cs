using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics;

/// <summary>
/// Static builders for expressions, meant for <c>using static Mathesis.Symbolics.Sym;</c>:
/// <c>Sin(x) * Exp(2 * x)</c>, <c>Pow(x, 2) - 5 * x + 6</c>, <c>Eq(a, b)</c>. Builders create <b>Raw</b> trees exactly as written
/// (a − b stays <c>sub</c>); use <c>Normalizer</c> for the Structural and Canonical levels. Operator overloads on <see cref="Expr"/>
/// cover <c>+ − * /</c>, unary <c>−</c>, comparisons (which build relations), <c>&amp; | !</c> (logic); <c>^</c> is not overloaded, use
/// <see cref="Pow"/>. Short aliases (<see cref="Diff"/>, <see cref="Det"/>) exist only here.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Builder names follow the mathematical function names.")]
public static class Sym
{
    // ----- Leaves -----

    /// <summary>An exact integer.</summary>
    public static Expr Number(int value) => new Number(value);

    /// <summary>An exact integer.</summary>
    public static Expr Number(long value) => new Number(value);

    /// <summary>An exact integer.</summary>
    public static Expr Number(BigInteger value) => new Number(value);

    /// <summary>An exact rational.</summary>
    public static Expr Number(BigRational value) => new Number(value);

    /// <summary>The exact rational <paramref name="numerator"/>/<paramref name="denominator"/>.</summary>
    public static Expr Rational(BigInteger numerator, BigInteger denominator) => new Number(BigRational.Create(numerator, denominator));

    /// <summary>An exact decimal such as <c>"0.25"</c>, displayed as typed.</summary>
    /// <exception cref="FormatException">The text is not a decimal number.</exception>
    public static Expr Decimal(string text)
    {
        var value = BigRational.Parse(text, null);
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        return new Number(value, dot < 0 ? NumberDisplay.Default : NumberDisplay.Decimal(text.Length - dot - 1));
    }

    /// <summary>An approximate number.</summary>
    public static Expr Float(double value) => new Float(value);

    /// <summary>A symbol; the sort defaults to ℝ.</summary>
    public static Symbol Symbol(string name, Sort? sort = null) => new(name, sort);

    /// <summary>Symbols named by the arguments (all of the default sort).</summary>
    public static Symbol[] Symbols(params ReadOnlySpan<string> names) => [.. names.ToArray().Select(n => new Symbol(n))];

    /// <summary>A pattern variable.</summary>
    public static Expr Wild(string name, Expr? constraint = null) => new Wild(name, constraint);

    /// <summary>The constant π.</summary>
    public static Expr Pi { get; } = new Constant(ConstantId.Pi);

    /// <summary>Euler's number e.</summary>
    public static Expr E { get; } = new Constant(ConstantId.E);

    /// <summary>The imaginary unit.</summary>
    public static Expr I { get; } = new Constant(ConstantId.ImaginaryUnit);

    /// <summary>+∞.</summary>
    public static Expr Infinity { get; } = new Constant(ConstantId.PositiveInfinity);

    /// <summary>−∞.</summary>
    public static Expr NegativeInfinity { get; } = new Constant(ConstantId.NegativeInfinity);

    /// <summary>The Boolean true.</summary>
    public static Expr True { get; } = new Constant(ConstantId.True);

    /// <summary>The Boolean false.</summary>
    public static Expr False { get; } = new Constant(ConstantId.False);

    /// <summary>The empty set.</summary>
    public static Expr EmptySet { get; } = new Constant(ConstantId.EmptySet);

    /// <summary>The constant with the given id.</summary>
    public static Expr Constant(ConstantId id) => new Constant(id);

    // ----- Generic application -----

    /// <summary>Applies an operator to arguments.</summary>
    /// <exception cref="ArgumentException">The argument count does not match the operator's arity.</exception>
    public static Expr Apply(Operator op, params ReadOnlySpan<Expr> arguments) => new Apply(op, [.. arguments]);

    /// <summary>Applies the operator with the given id or function-call name (<c>"asin"</c>) to arguments.</summary>
    /// <exception cref="ArgumentException">There is no such operator, or the argument count is wrong.</exception>
    public static Expr Fn(string name, params ReadOnlySpan<Expr> arguments) =>
        Operators.TryGetByName(name, out var op) ? new Apply(op, [.. arguments]) : throw new ArgumentException($"Unknown operator '{name}'.", nameof(name));

    // ----- Arithmetic -----

    /// <summary>Addition of two or more terms.</summary>
    public static Expr Add(params ReadOnlySpan<Expr> terms) => Apply(Operators.Add, terms);

    /// <summary>Subtraction <c>a − b</c> (Raw).</summary>
    public static Expr Sub(Expr a, Expr b) => Apply(Operators.Sub, a, b);

    /// <summary>Multiplication of two or more factors.</summary>
    public static Expr Mul(params ReadOnlySpan<Expr> factors) => Apply(Operators.Mul, factors);

    /// <summary>Division <c>a / b</c> (Raw).</summary>
    public static Expr Div(Expr a, Expr b) => Apply(Operators.Div, a, b);

    /// <summary>Negation (Raw).</summary>
    public static Expr Neg(Expr a) => Apply(Operators.Neg, a);

    /// <summary>Power <c>b^e</c>. C#'s <c>^</c> is not overloaded because it binds more loosely than <c>+</c>.</summary>
    public static Expr Pow(Expr b, Expr e) => Apply(Operators.Pow, b, e);

    /// <summary>Square root (Raw).</summary>
    public static Expr Sqrt(Expr a) => Apply(Operators.Sqrt, a);

    /// <summary>Real n-th root.</summary>
    public static Expr Root(Expr a, Expr n) => Apply(Operators.Root, a, n);

    /// <summary>Absolute value.</summary>
    public static Expr Abs(Expr a) => Apply(Operators.Abs, a);

    /// <summary>Sign.</summary>
    public static Expr Sign(Expr a) => Apply(Operators.Sign, a);

    /// <summary>Floor.</summary>
    public static Expr Floor(Expr a) => Apply(Operators.Floor, a);

    /// <summary>Ceiling.</summary>
    public static Expr Ceil(Expr a) => Apply(Operators.Ceil, a);

    /// <summary>Minimum.</summary>
    public static Expr Min(params ReadOnlySpan<Expr> values) => Apply(Operators.Min, values);

    /// <summary>Maximum.</summary>
    public static Expr Max(params ReadOnlySpan<Expr> values) => Apply(Operators.Max, values);

    /// <summary>Remainder <c>a mod n</c>.</summary>
    public static Expr Mod(Expr a, Expr n) => Apply(Operators.Mod, a, n);

    /// <summary>Greatest common divisor.</summary>
    public static Expr Gcd(params ReadOnlySpan<Expr> values) => Apply(Operators.Gcd, values);

    /// <summary>Factorial.</summary>
    public static Expr Factorial(Expr n) => Apply(Operators.Factorial, n);

    /// <summary>Binomial coefficient.</summary>
    public static Expr Binomial(Expr n, Expr k) => Apply(Operators.Binomial, n, k);

    // ----- Elementary functions -----

    /// <summary>The exponential function.</summary>
    public static Expr Exp(Expr a) => Apply(Operators.Exp, a);

    /// <summary>The natural logarithm.</summary>
    public static Expr Ln(Expr a) => Apply(Operators.Ln, a);

    /// <summary>Logarithm to base <paramref name="b"/>; base 10 when omitted.</summary>
    public static Expr Log(Expr a, Expr? b = null) => Apply(Operators.Log, a, b ?? Number(10));

    /// <summary>Sine.</summary>
    public static Expr Sin(Expr a) => Apply(Operators.Sin, a);

    /// <summary>Cosine.</summary>
    public static Expr Cos(Expr a) => Apply(Operators.Cos, a);

    /// <summary>Tangent.</summary>
    public static Expr Tan(Expr a) => Apply(Operators.Tan, a);

    /// <summary>Cotangent.</summary>
    public static Expr Cot(Expr a) => Apply(Operators.Cot, a);

    /// <summary>Secant.</summary>
    public static Expr Sec(Expr a) => Apply(Operators.Sec, a);

    /// <summary>Cosecant.</summary>
    public static Expr Csc(Expr a) => Apply(Operators.Csc, a);

    /// <summary>Inverse sine.</summary>
    public static Expr Arcsin(Expr a) => Apply(Operators.Arcsin, a);

    /// <summary>Inverse cosine.</summary>
    public static Expr Arccos(Expr a) => Apply(Operators.Arccos, a);

    /// <summary>Inverse tangent.</summary>
    public static Expr Arctan(Expr a) => Apply(Operators.Arctan, a);

    /// <summary>Hyperbolic sine.</summary>
    public static Expr Sinh(Expr a) => Apply(Operators.Sinh, a);

    /// <summary>Hyperbolic cosine.</summary>
    public static Expr Cosh(Expr a) => Apply(Operators.Cosh, a);

    /// <summary>Hyperbolic tangent.</summary>
    public static Expr Tanh(Expr a) => Apply(Operators.Tanh, a);

    /// <summary>The gamma function.</summary>
    public static Expr Gamma(Expr a) => Apply(Operators.Gamma, a);

    // ----- Relations and logic -----

    /// <summary>Builds the equation <c>a = b</c> (C#'s <c>==</c> is structural equality and builds nothing).</summary>
    public static Expr Eq(Expr a, Expr b) => Apply(Operators.Eq, a, b);

    /// <summary>Builds <c>a != b</c>.</summary>
    public static Expr Ne(Expr a, Expr b) => Apply(Operators.Ne, a, b);

    /// <summary>Builds <c>a &lt; b</c>.</summary>
    public static Expr Lt(Expr a, Expr b) => Apply(Operators.Lt, a, b);

    /// <summary>Builds <c>a &lt;= b</c>.</summary>
    public static Expr Le(Expr a, Expr b) => Apply(Operators.Le, a, b);

    /// <summary>Builds <c>a &gt; b</c>.</summary>
    public static Expr Gt(Expr a, Expr b) => Apply(Operators.Gt, a, b);

    /// <summary>Builds <c>a &gt;= b</c>.</summary>
    public static Expr Ge(Expr a, Expr b) => Apply(Operators.Ge, a, b);

    /// <summary>Builds <c>x in S</c>.</summary>
    public static Expr Element(Expr x, Expr set) => Apply(Operators.Element, x, set);

    /// <summary>Conjunction.</summary>
    public static Expr And(params ReadOnlySpan<Expr> values) => Apply(Operators.And, values);

    /// <summary>Disjunction.</summary>
    public static Expr Or(params ReadOnlySpan<Expr> values) => Apply(Operators.Or, values);

    /// <summary>Negation.</summary>
    public static Expr Not(Expr a) => Apply(Operators.Not, a);

    /// <summary>Implication.</summary>
    public static Expr Implies(Expr a, Expr b) => Apply(Operators.Implies, a, b);

    /// <summary>Equivalence.</summary>
    public static Expr Iff(Expr a, Expr b) => Apply(Operators.Iff, a, b);

    // ----- Sets, functions, calculus, linear algebra -----

    /// <summary>Union.</summary>
    public static Expr Union(params ReadOnlySpan<Expr> sets) => Apply(Operators.Union, sets);

    /// <summary>Intersection.</summary>
    public static Expr Intersect(params ReadOnlySpan<Expr> sets) => Apply(Operators.Intersect, sets);

    /// <summary>A set literal.</summary>
    public static Expr Set(params ReadOnlySpan<Expr> elements) => new SetLiteral([.. elements]);

    /// <summary>A tuple literal.</summary>
    public static Expr Tuple(params ReadOnlySpan<Expr> elements) => new TupleLiteral([.. elements]);

    /// <summary>An interval; ends are closed by default.</summary>
    public static Expr Interval(Expr lower, Expr upper, bool lowerClosed = true, bool upperClosed = true) => new IntervalLiteral(lower, upper, lowerClosed, upperClosed);

    /// <summary>A matrix from rows of equal length.</summary>
    /// <exception cref="ArgumentException">There are no rows or the rows differ in length.</exception>
    public static Expr Matrix(params Expr[][] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Length == 0 || rows[0].Length == 0 || rows.Any(r => r.Length != rows[0].Length)) throw new ArgumentException("A matrix needs at least one row and rows of equal, non-zero length.", nameof(rows));
        return new MatrixLiteral(rows.Length, rows[0].Length, [.. rows.SelectMany(r => r)]);
    }

    /// <summary>A piecewise expression from (value, condition) pairs.</summary>
    public static Expr Piecewise(params (Expr Value, Expr Condition)[] cases) => new Piecewise([.. cases]);

    /// <summary>Applies a function symbol or expression: <c>f(x)</c>.</summary>
    public static Expr Call(Expr function, params ReadOnlySpan<Expr> arguments) =>
        new Apply(Operators.Call, [function, .. arguments]);

    /// <summary><c>diff(f, x)</c>, <c>diff(f, x, n)</c> or a mixed partial.</summary>
    public static Expr Diff(Expr f, params ReadOnlySpan<Expr> variables) => new Apply(Operators.Diff, [f, .. variables]);

    /// <summary>Indefinite integral.</summary>
    public static Expr Integrate(Expr f, Expr x) => Apply(Operators.Integrate, f, x);

    /// <summary>Definite integral of <paramref name="f"/> over <paramref name="x"/> from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static Expr Integrate(Expr f, Symbol x, Expr a, Expr b) => new Bind(Binder.Integral, [x], [a, b], f);

    /// <summary>Sum of <paramref name="f"/> for <paramref name="k"/> from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static Expr Sum(Expr f, Symbol k, Expr a, Expr b) => new Bind(Binder.Sum, [k], [a, b], f);

    /// <summary>Product of <paramref name="f"/> for <paramref name="k"/> from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static Expr Product(Expr f, Symbol k, Expr a, Expr b) => new Bind(Binder.Product, [k], [a, b], f);

    /// <summary>Two-sided limit of <paramref name="f"/> as <paramref name="x"/> approaches <paramref name="a"/>.</summary>
    public static Expr Limit(Expr f, Symbol x, Expr a) => new Bind(Binder.Limit, [x], [a], f);

    /// <summary>One-sided limit; <paramref name="direction"/> is +1 (from above) or −1 (from below).</summary>
    public static Expr Limit(Expr f, Symbol x, Expr a, int direction) =>
        new Bind(Binder.Limit, [x], [a, Number(direction >= 0 ? 1 : -1)], f);

    /// <summary>A universally quantified statement over a domain.</summary>
    public static Expr ForAll(Symbol x, Expr domain, Expr body) => new Bind(Binder.ForAll, [x], [domain], body);

    /// <summary>An existentially quantified statement over a domain.</summary>
    public static Expr Exists(Symbol x, Expr domain, Expr body) => new Bind(Binder.Exists, [x], [domain], body);

    /// <summary>A lambda <c>x -&gt; body</c>.</summary>
    public static Expr Lambda(Symbol x, Expr body) => new Bind(Binder.Lambda, [x], [], body);

    /// <summary>Determinant (alias).</summary>
    public static Expr Det(Expr a) => Apply(Operators.Det, a);

    /// <summary>Transpose.</summary>
    public static Expr Transpose(Expr a) => Apply(Operators.Transpose, a);

    /// <summary>Trace.</summary>
    public static Expr Trace(Expr a) => Apply(Operators.Trace, a);
}
