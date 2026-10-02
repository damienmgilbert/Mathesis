using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Mathesis.Symbolics;

/// <summary>
/// The built-in operator registry (docs/design/05-syntax-trees-and-notation.md, "Built-in operator catalog"). Operators are
/// data; a name may have aliases (<c>asin</c> for <c>arcsin</c>) that only the parser consults.
/// </summary>
/// <remarks>
/// The fields <c>Domain</c>, <c>Cut</c>, <c>Kernels</c> and the derivative, antiderivative and series rule ids of the design
/// record arrive in later phases together with the engines that read them; Phase 4 holds identity, absorbing element,
/// attributes, signature and notation.
/// </remarks>
public static partial class Operators
{
    private static readonly Dictionary<string, Operator> ById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Operator> ByName = new(StringComparer.Ordinal);

    private const OperatorAttributes Elem = OperatorAttributes.NumericFunction | OperatorAttributes.Listable;
    private const OperatorAttributes Assoc = OperatorAttributes.Associative;
    private const OperatorAttributes Comm = OperatorAttributes.Commutative;
    private const OperatorAttributes Idem = OperatorAttributes.Idempotent;

    private static Operator Reg(Operator op, params string[] aliases)
    {
        ById.Add(op.Id, op);
        ByName[op.Id] = op;
        foreach (var a in aliases) ByName[a] = op;
        return op;
    }

    private static Operator Fn(string id, Arity arity, OperatorFamily family, Signature signature, OperatorAttributes attributes = OperatorAttributes.None, string? latex = null, params string[] aliases) =>
        Reg(new Operator(id, arity, attributes, family, signature, new Notation(Fixity.Function, Precedence.Atom, id, latex)), aliases);

    private static Operator Infix(string id, string text, int precedence, Arity arity, OperatorFamily family, Signature signature, OperatorAttributes attributes = OperatorAttributes.None, string? latex = null, bool right = false, Expr? identity = null, Expr? absorbing = null) =>
        Reg(new Operator(id, arity, attributes, family, signature, new Notation(Fixity.Infix, precedence, text, latex, right), identity, absorbing));

    private static Operator Prefix(string id, string text, int precedence, OperatorFamily family, Signature signature, OperatorAttributes attributes = OperatorAttributes.None, string? latex = null) =>
        Reg(new Operator(id, Arity.Fixed(1), attributes, family, signature, new Notation(Fixity.Prefix, precedence, text, latex)));

    private static Operator Postfix(string id, string text, OperatorFamily family, Signature signature, OperatorAttributes attributes = OperatorAttributes.None, string? latex = null) =>
        Reg(new Operator(id, Arity.Fixed(1), attributes, family, signature, new Notation(Fixity.Postfix, Precedence.Atom, text, latex)));

    private static readonly Arity One = Arity.Fixed(1);
    private static readonly Arity Two = Arity.Fixed(2);
    private static readonly Arity Three = Arity.Fixed(3);

    // ----- Arithmetic -----

    /// <summary>Addition (variadic, associative, commutative; identity 0).</summary>
    public static Operator Add { get; } = Infix("add", "+", Precedence.Additive, Arity.Variadic(2), OperatorFamily.Arithmetic, ArithmeticRules.Sum, Assoc | Comm, identity: new Number(0));

    /// <summary>Multiplication (variadic, associative; commutative on scalars; identity 1).</summary>
    public static Operator Mul { get; } = Infix("mul", "*", Precedence.Multiplicative, Arity.Variadic(2), OperatorFamily.Arithmetic, ArithmeticRules.Product, Assoc | Comm, "\\cdot", identity: new Number(1), absorbing: new Number(0));

    /// <summary>Exponentiation (right associative).</summary>
    public static Operator Pow { get; } = Infix("pow", "^", Precedence.Power, Two, OperatorFamily.Arithmetic, ArithmeticRules.Power, right: true);

    /// <summary>Subtraction (Raw level only).</summary>
    public static Operator Sub { get; } = Infix("sub", "-", Precedence.Additive, Two, OperatorFamily.Arithmetic, ArithmeticRules.Difference);

    /// <summary>Division (Raw level only).</summary>
    public static Operator Div { get; } = Infix("div", "/", Precedence.Multiplicative, Two, OperatorFamily.Arithmetic, ArithmeticRules.Quotient);

    /// <summary>Negation (Raw level only).</summary>
    public static Operator Neg { get; } = Prefix("neg", "-", Precedence.Prefix, OperatorFamily.Arithmetic, ArithmeticRules.Negation, OperatorAttributes.Involution | Elem);

    /// <summary>Square root (Raw level only).</summary>
    public static Operator Sqrt { get; } = Fn("sqrt", One, OperatorFamily.Arithmetic, Signature.RealFunction, Elem, "\\sqrt");

    /// <summary>Real n-th root: <c>root(a, n)</c>.</summary>
    public static Operator Root { get; } = Fn("root", Two, OperatorFamily.Arithmetic, Signature.RealFunction, Elem);

    /// <summary>Absolute value.</summary>
    public static Operator Abs { get; } = Fn("abs", One, OperatorFamily.Arithmetic, Signature.Elementary(s => s.IsSubsortOf(Sort.Integer) ? Sort.Natural : s.IsSubsortOf(Sort.Rational) ? Sort.Rational : Sort.Real), Elem | OperatorAttributes.Even);

    /// <summary>Sign: −1, 0 or 1.</summary>
    public static Operator Sign { get; } = Fn("sign", One, OperatorFamily.Arithmetic, Signature.Elementary(_ => Sort.Integer), Elem | OperatorAttributes.Odd);

    /// <summary>Floor ⌊a⌋.</summary>
    public static Operator Floor { get; } = Fn("floor", One, OperatorFamily.Arithmetic, Signature.Elementary(_ => Sort.Integer), Elem);

    /// <summary>Ceiling ⌈a⌉.</summary>
    public static Operator Ceil { get; } = Fn("ceil", One, OperatorFamily.Arithmetic, Signature.Elementary(_ => Sort.Integer), Elem);

    /// <summary>Rounding half away from zero; an optional second argument gives the number of digits.</summary>
    public static Operator Round { get; } = Fn("round", Arity.Range(1, 2), OperatorFamily.Arithmetic, Signature.Elementary(s => s.IsSubsortOf(Sort.Rational) ? Sort.Rational : Sort.Real), Elem);

    /// <summary>Fractional part.</summary>
    public static Operator Frac { get; } = Fn("frac", One, OperatorFamily.Arithmetic, Signature.Elementary(_ => Sort.Real), Elem);

    /// <summary>Remainder <c>a mod n</c>.</summary>
    public static Operator Mod { get; } = Infix("\bmod", "mod", Precedence.Multiplicative, Two, OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Real), OperatorAttributes.NumericFunction, latex: "\\bmod");

    /// <summary>Integer quotient.</summary>
    public static Operator Quo { get; } = Fn("quo", Two, OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Integer), OperatorAttributes.NumericFunction);

    /// <summary>Greatest common divisor.</summary>
    public static Operator Gcd { get; } = Fn("gcd", Arity.Variadic(2), OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Integer), Assoc | Comm | Idem | OperatorAttributes.NumericFunction);

    /// <summary>Least common multiple.</summary>
    public static Operator Lcm { get; } = Fn("lcm", Arity.Variadic(2), OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Integer), Assoc | Comm | Idem | OperatorAttributes.NumericFunction);

    /// <summary>Maximum.</summary>
    public static Operator Max { get; } = Fn("max", Arity.Variadic(2), OperatorFamily.Arithmetic, ArithmeticRules.Extremum, Assoc | Comm | Idem | OperatorAttributes.NumericFunction);

    /// <summary>Minimum.</summary>
    public static Operator Min { get; } = Fn("min", Arity.Variadic(2), OperatorFamily.Arithmetic, ArithmeticRules.Extremum, Assoc | Comm | Idem | OperatorAttributes.NumericFunction);

    /// <summary>Factorial <c>n!</c>.</summary>
    public static Operator Factorial { get; } = Postfix("factorial", "!", OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "!");

    /// <summary>Double factorial <c>n!!</c>.</summary>
    public static Operator Factorial2 { get; } = Postfix("factorial2", "!!", OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "!!");

    /// <summary>Subfactorial (derangements).</summary>
    public static Operator Subfactorial { get; } = Fn("subfactorial", One, OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction);

    /// <summary>Binomial coefficient.</summary>
    public static Operator Binomial { get; } = Fn("binomial", Two, OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "\\binom", "C", "nCr");

    /// <summary>Permutations <c>perm(n, k)</c>.</summary>
    public static Operator Perm { get; } = Fn("perm", Two, OperatorFamily.Arithmetic, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, null, "P", "nPr");

    // ----- Complex numbers -----

    /// <summary>Real part.</summary>
    public static Operator Re { get; } = Fn("re", One, OperatorFamily.Complex, Signature.Elementary(_ => Sort.Real), Elem, "\\operatorname{Re}");

    /// <summary>Imaginary part.</summary>
    public static Operator Im { get; } = Fn("im", One, OperatorFamily.Complex, Signature.Elementary(_ => Sort.Real), Elem, "\\operatorname{Im}");

    /// <summary>Complex conjugate.</summary>
    public static Operator Conj { get; } = Fn("conj", One, OperatorFamily.Complex, Signature.Elementary(s => s), Elem | OperatorAttributes.Involution, "\\overline");

    /// <summary>Argument (phase) of a complex number.</summary>
    public static Operator Arg { get; } = Fn("arg", One, OperatorFamily.Complex, Signature.Elementary(_ => Sort.Real), Elem, "\\arg");

    /// <summary><c>cis(θ) = cos θ + i sin θ</c>.</summary>
    public static Operator Cis { get; } = Fn("cis", One, OperatorFamily.Complex, Signature.ComplexFunction, Elem, "\\operatorname{cis}");

    // ----- Exponential and logarithmic -----

    /// <summary>The exponential function.</summary>
    public static Operator Exp { get; } = Fn("exp", One, OperatorFamily.ExpLog, Signature.RealFunction, Elem | OperatorAttributes.Injective, "\\exp");

    /// <summary>The natural logarithm.</summary>
    public static Operator Ln { get; } = Fn("ln", One, OperatorFamily.ExpLog, Signature.RealFunction, Elem | OperatorAttributes.Injective, "\\ln");

    /// <summary>Logarithm <c>log(x, b)</c>; a bare <c>log x</c> parses to base 10 (catalog <c>conv.log-base-10</c>).</summary>
    public static Operator Log { get; } = Fn("log", Arity.Range(1, 2), OperatorFamily.ExpLog, Signature.RealFunction, Elem, "\\log");

    /// <summary>The Lambert W function.</summary>
    public static Operator LambertW { get; } = Fn("lambertw", One, OperatorFamily.ExpLog, Signature.RealFunction, Elem, "W");

    // ----- Trigonometric -----

    /// <summary>Sine.</summary>
    public static Operator Sin { get; } = Fn("sin", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\sin");

    /// <summary>Cosine.</summary>
    public static Operator Cos { get; } = Fn("cos", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Even, "\\cos");

    /// <summary>Tangent.</summary>
    public static Operator Tan { get; } = Fn("tan", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\tan");

    /// <summary>Cotangent.</summary>
    public static Operator Cot { get; } = Fn("cot", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\cot");

    /// <summary>Secant.</summary>
    public static Operator Sec { get; } = Fn("sec", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Even, "\\sec");

    /// <summary>Cosecant.</summary>
    public static Operator Csc { get; } = Fn("csc", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\csc");

    /// <summary>Inverse sine.</summary>
    public static Operator Arcsin { get; } = Fn("arcsin", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Odd | OperatorAttributes.Injective, "\\arcsin", "asin");

    /// <summary>Inverse cosine.</summary>
    public static Operator Arccos { get; } = Fn("arccos", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Injective, "\\arccos", "acos");

    /// <summary>Inverse tangent.</summary>
    public static Operator Arctan { get; } = Fn("arctan", One, OperatorFamily.Trig, Signature.RealFunction, Elem | OperatorAttributes.Odd | OperatorAttributes.Injective, "\\arctan", "atan");

    /// <summary>Inverse cotangent.</summary>
    public static Operator Arccot { get; } = Fn("arccot", One, OperatorFamily.Trig, Signature.RealFunction, Elem, "\\operatorname{arccot}", "acot");

    /// <summary>Inverse secant.</summary>
    public static Operator Arcsec { get; } = Fn("arcsec", One, OperatorFamily.Trig, Signature.RealFunction, Elem, "\\operatorname{arcsec}", "asec");

    /// <summary>Inverse cosecant.</summary>
    public static Operator Arccsc { get; } = Fn("arccsc", One, OperatorFamily.Trig, Signature.RealFunction, Elem, "\\operatorname{arccsc}", "acsc");

    /// <summary>Two-argument arctangent <c>atan2(y, x)</c>.</summary>
    public static Operator Atan2 { get; } = Fn("atan2", Two, OperatorFamily.Trig, Signature.NumbersTo(Sort.Real), OperatorAttributes.NumericFunction);

    // ----- Hyperbolic -----

    /// <summary>Hyperbolic sine.</summary>
    public static Operator Sinh { get; } = Fn("sinh", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\sinh");

    /// <summary>Hyperbolic cosine.</summary>
    public static Operator Cosh { get; } = Fn("cosh", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Even, "\\cosh");

    /// <summary>Hyperbolic tangent.</summary>
    public static Operator Tanh { get; } = Fn("tanh", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\tanh");

    /// <summary>Hyperbolic cotangent.</summary>
    public static Operator Coth { get; } = Fn("coth", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\coth");

    /// <summary>Hyperbolic secant.</summary>
    public static Operator Sech { get; } = Fn("sech", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Even, "\\operatorname{sech}");

    /// <summary>Hyperbolic cosecant.</summary>
    public static Operator Csch { get; } = Fn("csch", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\operatorname{csch}");

    /// <summary>Inverse hyperbolic sine.</summary>
    public static Operator Arsinh { get; } = Fn("arsinh", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\operatorname{arsinh}", "asinh");

    /// <summary>Inverse hyperbolic cosine.</summary>
    public static Operator Arcosh { get; } = Fn("arcosh", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem, "\\operatorname{arcosh}", "acosh");

    /// <summary>Inverse hyperbolic tangent.</summary>
    public static Operator Artanh { get; } = Fn("artanh", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\operatorname{artanh}", "atanh");

    /// <summary>Inverse hyperbolic cotangent.</summary>
    public static Operator Arcoth { get; } = Fn("arcoth", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem, "\\operatorname{arcoth}", "acoth");

    /// <summary>Inverse hyperbolic secant.</summary>
    public static Operator Arsech { get; } = Fn("arsech", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem, "\\operatorname{arsech}", "asech");

    /// <summary>Inverse hyperbolic cosecant.</summary>
    public static Operator Arcsch { get; } = Fn("arcsch", One, OperatorFamily.Hyperbolic, Signature.RealFunction, Elem, "\\operatorname{arcsch}", "acsch");

    // ----- Special functions -----

    /// <summary>The gamma function.</summary>
    public static Operator Gamma { get; } = Fn("gamma", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\Gamma");

    /// <summary>The beta function.</summary>
    public static Operator Beta { get; } = Fn("beta", Two, OperatorFamily.Special, Signature.RealFunction, Elem, "\\mathrm{B}");

    /// <summary>The digamma function.</summary>
    public static Operator Digamma { get; } = Fn("digamma", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\psi");

    /// <summary>The error function.</summary>
    public static Operator Erf { get; } = Fn("erf", One, OperatorFamily.Special, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\operatorname{erf}");

    /// <summary>The complementary error function.</summary>
    public static Operator Erfc { get; } = Fn("erfc", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{erfc}");

    /// <summary>The imaginary error function.</summary>
    public static Operator Erfi { get; } = Fn("erfi", One, OperatorFamily.Special, Signature.RealFunction, Elem | OperatorAttributes.Odd, "\\operatorname{erfi}");

    /// <summary>The Riemann zeta function.</summary>
    public static Operator Zeta { get; } = Fn("zeta", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\zeta");

    /// <summary>The polylogarithm <c>polylog(s, z)</c>.</summary>
    public static Operator Polylog { get; } = Fn("polylog", Two, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{Li}");

    /// <summary>Bessel function of the first kind.</summary>
    public static Operator BesselJ { get; } = Fn("besselj", Two, OperatorFamily.Special, Signature.RealFunction, Elem, "J");

    /// <summary>Bessel function of the second kind.</summary>
    public static Operator BesselY { get; } = Fn("bessely", Two, OperatorFamily.Special, Signature.RealFunction, Elem, "Y");

    /// <summary>Modified Bessel function of the first kind.</summary>
    public static Operator BesselI { get; } = Fn("besseli", Two, OperatorFamily.Special, Signature.RealFunction, Elem, "I");

    /// <summary>Modified Bessel function of the second kind.</summary>
    public static Operator BesselK { get; } = Fn("besselk", Two, OperatorFamily.Special, Signature.RealFunction, Elem, "K");

    /// <summary>Airy function Ai.</summary>
    public static Operator AiryAi { get; } = Fn("airyai", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{Ai}");

    /// <summary>Airy function Bi.</summary>
    public static Operator AiryBi { get; } = Fn("airybi", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{Bi}");

    /// <summary>Complete elliptic integral of the first kind.</summary>
    public static Operator EllipK { get; } = Fn("ellipk", One, OperatorFamily.Special, Signature.RealFunction, Elem, "K");

    /// <summary>Complete elliptic integral of the second kind.</summary>
    public static Operator EllipE { get; } = Fn("ellipe", One, OperatorFamily.Special, Signature.RealFunction, Elem, "E");

    /// <summary>Sine integral.</summary>
    public static Operator Si { get; } = Fn("si", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{Si}");

    /// <summary>Cosine integral.</summary>
    public static Operator Ci { get; } = Fn("ci", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{Ci}");

    /// <summary>Exponential integral.</summary>
    public static Operator Ei { get; } = Fn("ei", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{Ei}");

    /// <summary>Logarithmic integral.</summary>
    public static Operator Li { get; } = Fn("li", One, OperatorFamily.Special, Signature.RealFunction, Elem, "\\operatorname{li}");

    /// <summary>Fresnel S integral.</summary>
    public static Operator FresnelS { get; } = Fn("fresnels", One, OperatorFamily.Special, Signature.RealFunction, Elem, "S");

    /// <summary>Fresnel C integral.</summary>
    public static Operator FresnelC { get; } = Fn("fresnelc", One, OperatorFamily.Special, Signature.RealFunction, Elem, "C");

    /// <summary>The Heaviside step function.</summary>
    public static Operator Heaviside { get; } = Fn("heaviside", One, OperatorFamily.Special, Signature.RealFunction, Elem, "u");

    /// <summary>The Dirac delta distribution.</summary>
    public static Operator Dirac { get; } = Fn("dirac", One, OperatorFamily.Special, Signature.RealFunction, Elem | OperatorAttributes.Even, "\\delta");

    /// <summary>The Kronecker delta <c>kronecker(i, j)</c>.</summary>
    public static Operator Kronecker { get; } = Fn("kronecker", Two, OperatorFamily.Special, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction | Comm, "\\delta");

    /// <summary>The Levi-Civita symbol <c>leviCivita(i, j, k)</c>.</summary>
    public static Operator LeviCivita { get; } = Fn("leviCivita", Arity.Variadic(2), OperatorFamily.Special, Signature.NumbersTo(Sort.Integer), OperatorAttributes.NumericFunction, "\\varepsilon");

    // ----- Orthogonal polynomials -----

    /// <summary>Legendre polynomial <c>legendreP(n, x)</c>.</summary>
    public static Operator LegendreP { get; } = Fn("legendreP", Two, OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "P");

    /// <summary>Hermite polynomial <c>hermiteH(n, x)</c>.</summary>
    public static Operator HermiteH { get; } = Fn("hermiteH", Two, OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "H");

    /// <summary>Laguerre polynomial <c>laguerreL(n, x)</c>.</summary>
    public static Operator LaguerreL { get; } = Fn("laguerreL", Two, OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "L");

    /// <summary>Chebyshev polynomial of the first kind <c>chebyshevT(n, x)</c>.</summary>
    public static Operator ChebyshevT { get; } = Fn("chebyshevT", Two, OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "T");

    /// <summary>Chebyshev polynomial of the second kind <c>chebyshevU(n, x)</c>.</summary>
    public static Operator ChebyshevU { get; } = Fn("chebyshevU", Two, OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "U");

    /// <summary>Jacobi polynomial <c>jacobiP(n, a, b, x)</c>.</summary>
    public static Operator JacobiP { get; } = Fn("jacobiP", Arity.Fixed(4), OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "P");

    /// <summary>Gegenbauer polynomial <c>gegenbauerC(n, a, x)</c>.</summary>
    public static Operator GegenbauerC { get; } = Fn("gegenbauerC", Three, OperatorFamily.Orthogonal, Signature.RealFunction, Elem, "C");

    // ----- Number theory and combinatorics -----

    /// <summary>Euler's totient φ(n).</summary>
    public static Operator Totient { get; } = Fn("totient", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "\\varphi", "φ");

    /// <summary>The Möbius function μ(n).</summary>
    public static Operator Mobius { get; } = Fn("mobius", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Integer), OperatorAttributes.NumericFunction, "\\mu", "μ");

    /// <summary>Divisor function σ<sub>k</sub>(n): <c>divisorSigma(k, n)</c>.</summary>
    public static Operator DivisorSigma { get; } = Fn("divisorSigma", Two, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "\\sigma", "σ");

    /// <summary>The prime counting function π(x).</summary>
    public static Operator PrimePi { get; } = Fn("primePi", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "\\pi");

    /// <summary>The n-th prime.</summary>
    public static Operator Prime { get; } = Fn("prime", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "p");

    /// <summary>Fibonacci numbers.</summary>
    public static Operator Fibonacci { get; } = Fn("fibonacci", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "F");

    /// <summary>Lucas numbers.</summary>
    public static Operator Lucas { get; } = Fn("lucas", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "L");

    /// <summary>Catalan numbers.</summary>
    public static Operator Catalan { get; } = Fn("catalan", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "C");

    /// <summary>Stirling numbers of the first kind.</summary>
    public static Operator Stirling1 { get; } = Fn("stirling1", Two, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Integer), OperatorAttributes.NumericFunction, "s");

    /// <summary>Stirling numbers of the second kind.</summary>
    public static Operator Stirling2 { get; } = Fn("stirling2", Two, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "S");

    /// <summary>Bell numbers.</summary>
    public static Operator Bell { get; } = Fn("bell", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "B");

    /// <summary>The number of integer partitions p(n).</summary>
    public static Operator Partitions { get; } = Fn("partitions", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction, "p");

    /// <summary>Harmonic numbers.</summary>
    public static Operator Harmonic { get; } = Fn("harmonic", One, OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Rational), OperatorAttributes.NumericFunction, "H");

    /// <summary>Multinomial coefficient.</summary>
    public static Operator Multinomial { get; } = Fn("multinomial", Arity.Variadic(2), OperatorFamily.NumberTheory, Signature.NumbersTo(Sort.Natural), OperatorAttributes.NumericFunction | Comm);

    // ----- Relations -----

    /// <summary>Equality <c>a = b</c>.</summary>
    public static Operator Eq { get; } = Infix("eq", "=", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, Comm, "=");

    /// <summary>Inequality <c>a != b</c>.</summary>
    public static Operator Ne { get; } = Infix("ne", "!=", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, Comm, "\\ne");

    /// <summary>Less than.</summary>
    public static Operator Lt { get; } = Infix("lt", "<", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "<");

    /// <summary>Less than or equal.</summary>
    public static Operator Le { get; } = Infix("le", "<=", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\le");

    /// <summary>Greater than.</summary>
    public static Operator Gt { get; } = Infix("gt", ">", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: ">");

    /// <summary>Greater than or equal.</summary>
    public static Operator Ge { get; } = Infix("ge", ">=", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\ge");

    /// <summary>Approximately equal <c>a ~= b</c>.</summary>
    public static Operator Approx { get; } = Infix("approx", "~=", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, Comm, "\\approx");

    /// <summary>Divisibility <c>a | b</c>.</summary>
    public static Operator Divides { get; } = Infix("divides", "|", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\mid");

    /// <summary>Congruence <c>congruent(a, b, n)</c> (a ≡ b mod n).</summary>
    public static Operator Congruent { get; } = Fn("congruent", Three, OperatorFamily.Relation, Signature.Predicate, OperatorAttributes.None, "\\equiv");

    /// <summary>Set membership <c>x in S</c>.</summary>
    public static Operator Element { get; } = Infix("element", "in", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\in");

    /// <summary>Non-membership <c>x notin S</c>.</summary>
    public static Operator NotElement { get; } = Infix("notElement", "notin", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\notin");

    /// <summary>Proper subset.</summary>
    public static Operator Subset { get; } = Infix("subset", "⊂", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\subset");

    /// <summary>Subset or equal.</summary>
    public static Operator SubsetEq { get; } = Infix("subsetEq", "⊆", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\subseteq");

    /// <summary>Matrix similarity.</summary>
    public static Operator Similar { get; } = Fn("similar", Two, OperatorFamily.Relation, Signature.Predicate, Comm, "\\sim");

    /// <summary>Proportionality.</summary>
    public static Operator Proportional { get; } = Infix("proportional", "∝", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, latex: "\\propto");

    /// <summary>Perpendicularity.</summary>
    public static Operator Perpendicular { get; } = Infix("perpendicular", "⊥", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, Comm, "\\perp");

    /// <summary>Parallelism.</summary>
    public static Operator Parallel { get; } = Infix("parallel", "∥", Precedence.Relation, Two, OperatorFamily.Relation, Signature.Predicate, Comm, "\\parallel");

    /// <summary>"Distributed as": <c>X ~ Normal(0, 1)</c>.</summary>
    public static Operator Distributed { get; } = Infix("distributed", "~", Precedence.Relation, Two, OperatorFamily.Probability, Signature.Predicate, latex: "\\sim");

    // ----- Logic -----

    /// <summary>Conjunction (identity true, absorbing false).</summary>
    public static Operator And { get; } = Infix("and", "and", Precedence.And, Arity.Variadic(2), OperatorFamily.Logic, Signature.Logic, Assoc | Comm | Idem, "\\land", identity: new Constant(ConstantId.True), absorbing: new Constant(ConstantId.False));

    /// <summary>Disjunction (identity false, absorbing true).</summary>
    public static Operator Or { get; } = Infix("or", "or", Precedence.Or, Arity.Variadic(2), OperatorFamily.Logic, Signature.Logic, Assoc | Comm | Idem, "\\lor", identity: new Constant(ConstantId.False), absorbing: new Constant(ConstantId.True));

    /// <summary>Negation.</summary>
    public static Operator Not { get; } = Prefix("not", "not", Precedence.Not, OperatorFamily.Logic, Signature.Logic, OperatorAttributes.Involution, "\\neg");

    /// <summary>Implication (right associative).</summary>
    public static Operator Implies { get; } = Infix("implies", "=>", Precedence.Implies, Two, OperatorFamily.Logic, Signature.Logic, latex: "\\implies", right: true);

    /// <summary>Equivalence.</summary>
    public static Operator Iff { get; } = Infix("iff", "<=>", Precedence.Iff, Two, OperatorFamily.Logic, Signature.Logic, latex: "\\iff");

    /// <summary>Exclusive or.</summary>
    public static Operator Xor { get; } = Infix("xor", "xor", Precedence.Or, Arity.Variadic(2), OperatorFamily.Logic, Signature.Logic, Assoc | Comm, "\\oplus", identity: new Constant(ConstantId.False));

    /// <summary>Not-and.</summary>
    public static Operator Nand { get; } = Fn("nand", Two, OperatorFamily.Logic, Signature.Logic, Comm, "\\uparrow");

    /// <summary>Not-or.</summary>
    public static Operator Nor { get; } = Fn("nor", Two, OperatorFamily.Logic, Signature.Logic, Comm, "\\downarrow");

    // ----- Sets -----

    /// <summary>Union (identity ∅).</summary>
    public static Operator Union { get; } = Infix("union", "∪", Precedence.Union, Arity.Variadic(2), OperatorFamily.Set, Signature.SetOperation, Assoc | Comm | Idem, "\\cup", identity: new Constant(ConstantId.EmptySet));

    /// <summary>Intersection.</summary>
    public static Operator Intersect { get; } = Infix("intersect", "∩", Precedence.Intersection, Arity.Variadic(2), OperatorFamily.Set, Signature.SetOperation, Assoc | Comm | Idem, "\\cap");

    /// <summary>Set difference.</summary>
    public static Operator SetMinus { get; } = Infix("setminus", "∖", Precedence.Union, Two, OperatorFamily.Set, Signature.SetOperation, latex: "\\setminus");

    /// <summary>Complement <c>A^c</c>.</summary>
    public static Operator Complement { get; } = Postfix("complement", "^c", OperatorFamily.Set, Signature.SetOperation, OperatorAttributes.Involution, "^{c}");

    /// <summary>Symmetric difference.</summary>
    public static Operator SymDiff { get; } = Infix("symdiff", "△", Precedence.Union, Arity.Variadic(2), OperatorFamily.Set, Signature.SetOperation, Assoc | Comm, "\\triangle");

    /// <summary>Cartesian product.</summary>
    public static Operator Cartesian { get; } = Fn("cartesian", Arity.Variadic(2), OperatorFamily.Set, Signature.SetOperation, Assoc, "\\times");

    /// <summary>Power set.</summary>
    public static Operator PowerSet { get; } = Fn("powerset", One, OperatorFamily.Set, Signature.Custom("Set → Set(Set)", a => a[0] is SetSort s ? Sort.SetOf(s) : a[0] == Sort.Any ? Sort.SetOf(Sort.SetOf(Sort.Any)) : null), OperatorAttributes.None, "\\mathcal{P}", "𝒫");

    /// <summary>Cardinality.</summary>
    public static Operator Card { get; } = Fn("card", One, OperatorFamily.Set, Signature.SetsTo(Sort.Natural));

    // ----- Functions -----

    /// <summary>Function application <c>call(f, x…)</c>.</summary>
    public static Operator Call { get; } = Reg(new Operator("call", Arity.Variadic(2), OperatorAttributes.None, OperatorFamily.Function, ArithmeticRules.Call, new Notation(Fixity.Function, Precedence.Atom, "call")));

    /// <summary>Function composition <c>f ∘ g</c>.</summary>
    public static Operator Compose { get; } = Infix("compose", "∘", Precedence.Multiplicative, Arity.Variadic(2), OperatorFamily.Function, Signature.AnyToAny, Assoc, "\\circ");

    /// <summary>Inverse of a function (as opposed to a reciprocal).</summary>
    public static Operator InverseFunction { get; } = Fn("inverseFunction", One, OperatorFamily.Function, Signature.AnyToAny);

    /// <summary>The n-th derivative of a function symbol: <c>f'</c> is <c>derivativeOf(f, 1)</c>.</summary>
    public static Operator DerivativeOf { get; } = Fn("derivativeOf", Two, OperatorFamily.Function, ArithmeticRules.DerivativeOf);

    // ----- Calculus -----

    /// <summary><c>diff(f, x)</c>, <c>diff(f, x, n)</c> or the mixed partial <c>diff(f, x, y)</c>.</summary>
    public static Operator Diff { get; } = Fn("diff", Arity.Variadic(2), OperatorFamily.Calculus, ArithmeticRules.SameAsFirst, OperatorAttributes.Linear, "\\frac{d}{d}");

    /// <summary>Indefinite integral <c>integrate(f, x)</c>.</summary>
    public static Operator Integrate { get; } = Fn("integrate", Two, OperatorFamily.Calculus, ArithmeticRules.SameAsFirst, OperatorAttributes.Linear, "\\int");

    /// <summary>Big-O term <c>O(x^n)</c>.</summary>
    public static Operator BigO { get; } = Fn("bigO", One, OperatorFamily.Calculus, Signature.AnyToAny, OperatorAttributes.None, "O");

    /// <summary>Gradient.</summary>
    public static Operator Grad { get; } = Fn("grad", One, OperatorFamily.Calculus, Signature.AnyToAny, OperatorAttributes.Linear, "\\nabla");

    /// <summary>Divergence.</summary>
    public static Operator Divergence { get; } = Fn("divergence", One, OperatorFamily.Calculus, Signature.AnyToAny, OperatorAttributes.Linear, "\\nabla\\cdot");

    /// <summary>Curl.</summary>
    public static Operator Curl { get; } = Fn("curl", One, OperatorFamily.Calculus, Signature.AnyToAny, OperatorAttributes.Linear, "\\nabla\\times");

    /// <summary>Laplacian.</summary>
    public static Operator Laplacian { get; } = Fn("laplacian", One, OperatorFamily.Calculus, Signature.AnyToAny, OperatorAttributes.Linear, "\\nabla^{2}");

    /// <summary>Jacobian matrix <c>jacobian(F, vars)</c>.</summary>
    public static Operator Jacobian { get; } = Fn("jacobian", Two, OperatorFamily.Calculus, Signature.AnyToAny);

    /// <summary>Hessian matrix <c>hessian(f, vars)</c>.</summary>
    public static Operator Hessian { get; } = Fn("hessian", Two, OperatorFamily.Calculus, Signature.AnyToAny);

    // ----- Linear algebra -----

    /// <summary>Transpose <c>A^T</c>.</summary>
    public static Operator Transpose { get; } = Postfix("transpose", "^T", OperatorFamily.LinearAlgebra, ArithmeticRules.Transpose, OperatorAttributes.Involution, "^{\\top}");

    /// <summary>Conjugate transpose <c>A^H</c>.</summary>
    public static Operator ConjTranspose { get; } = Postfix("conjTranspose", "^H", OperatorFamily.LinearAlgebra, ArithmeticRules.Transpose, OperatorAttributes.Involution, "^{H}");

    /// <summary>Matrix inverse.</summary>
    public static Operator Inverse { get; } = Fn("inverse", One, OperatorFamily.LinearAlgebra, ArithmeticRules.SameAsFirst, OperatorAttributes.Involution);

    /// <summary>Determinant.</summary>
    public static Operator Det { get; } = Fn("det", One, OperatorFamily.LinearAlgebra, ArithmeticRules.Determinant, OperatorAttributes.None, "\\det");

    /// <summary>Trace.</summary>
    public static Operator Trace { get; } = Fn("trace", One, OperatorFamily.LinearAlgebra, ArithmeticRules.Determinant, OperatorAttributes.Linear, "\\operatorname{tr}", "tr");

    /// <summary>Rank.</summary>
    public static Operator Rank { get; } = Fn("rank", One, OperatorFamily.LinearAlgebra, Signature.Custom("Matrix → Natural", _ => Sort.Natural), OperatorAttributes.None, "\\operatorname{rank}");

    /// <summary>Adjugate.</summary>
    public static Operator Adj { get; } = Fn("adj", One, OperatorFamily.LinearAlgebra, ArithmeticRules.SameAsFirst, OperatorAttributes.None, "\\operatorname{adj}");

    /// <summary>Dot product.</summary>
    public static Operator Dot { get; } = Fn("dot", Two, OperatorFamily.LinearAlgebra, Signature.Custom("Vector × Vector → Number", _ => Sort.Real), Comm, "\\cdot");

    /// <summary>Cross product.</summary>
    public static Operator Cross { get; } = Fn("cross", Two, OperatorFamily.LinearAlgebra, ArithmeticRules.SameAsFirst, OperatorAttributes.None, "\\times");

    /// <summary>Outer product.</summary>
    public static Operator Outer { get; } = Fn("outer", Two, OperatorFamily.LinearAlgebra, Signature.AnyToAny, OperatorAttributes.None, "\\otimes");

    /// <summary>Kronecker product.</summary>
    public static Operator Kron { get; } = Infix("kron", "⊗", Precedence.Multiplicative, Two, OperatorFamily.LinearAlgebra, Signature.AnyToAny, OperatorAttributes.Associative, "\\otimes");

    /// <summary>Norm <c>norm(v)</c> or <c>norm(v, p)</c>.</summary>
    public static Operator Norm { get; } = Fn("norm", Arity.Range(1, 2), OperatorFamily.LinearAlgebra, Signature.Custom("Vector → Real", _ => Sort.Real), OperatorAttributes.None, "\\lVert");

    /// <summary>Reduced row echelon form.</summary>
    public static Operator Rref { get; } = Fn("rref", One, OperatorFamily.LinearAlgebra, ArithmeticRules.SameAsFirst);

    /// <summary>Null space.</summary>
    public static Operator NullSpace { get; } = Fn("nullspace", One, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Column space.</summary>
    public static Operator ColSpace { get; } = Fn("colspace", One, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Row space.</summary>
    public static Operator RowSpace { get; } = Fn("rowspace", One, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Eigenvalues.</summary>
    public static Operator Eigenvalues { get; } = Fn("eigenvalues", One, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Eigenvectors.</summary>
    public static Operator Eigenvectors { get; } = Fn("eigenvectors", One, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Characteristic polynomial <c>charpoly(A, x)</c>.</summary>
    public static Operator CharPoly { get; } = Fn("charpoly", Two, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Identity matrix <c>I(n)</c>.</summary>
    public static Operator Identity { get; } = Fn("identity", One, OperatorFamily.LinearAlgebra, Signature.Custom("Natural → Matrix", a => Sort.MatrixOf(Sym.Number(1), Sym.Number(1), Sort.Integer)), OperatorAttributes.None, "I", "I");

    /// <summary>Zero matrix <c>zeros(m, n)</c>.</summary>
    public static Operator Zeros { get; } = Fn("zeros", Two, OperatorFamily.LinearAlgebra, Signature.AnyToAny);

    /// <summary>Diagonal matrix <c>diag(…)</c>.</summary>
    public static Operator Diag { get; } = Fn("diag", Arity.Variadic(1), OperatorFamily.LinearAlgebra, Signature.AnyToAny, OperatorAttributes.None, "\\operatorname{diag}");

    /// <summary>Projection <c>proj(u, v)</c>.</summary>
    public static Operator Proj { get; } = Fn("proj", Two, OperatorFamily.LinearAlgebra, ArithmeticRules.SameAsFirst, OperatorAttributes.None, "\\operatorname{proj}");

    // ----- Probability -----

    /// <summary>Probability <c>P(A)</c> or conditional probability <c>P(A | B)</c> (as <c>prob(A, B)</c>).</summary>
    public static Operator Prob { get; } = Fn("prob", Arity.Range(1, 2), OperatorFamily.Probability, Signature.Custom("Boolean → Real", _ => Sort.Real), OperatorAttributes.None, "P", "P", "Pr");

    /// <summary>Expectation.</summary>
    public static Operator Expect { get; } = Fn("expect", One, OperatorFamily.Probability, Signature.Custom("RandomVariable → Real", _ => Sort.Real), OperatorAttributes.Linear, "\\mathbb{E}", "E");

    /// <summary>Variance.</summary>
    public static Operator Var { get; } = Fn("var", One, OperatorFamily.Probability, Signature.Custom("RandomVariable → Real", _ => Sort.Real), OperatorAttributes.None, "\\operatorname{Var}", "Var");

    /// <summary>Covariance.</summary>
    public static Operator Cov { get; } = Fn("cov", Two, OperatorFamily.Probability, Signature.Custom("RandomVariable² → Real", _ => Sort.Real), Comm, "\\operatorname{Cov}", "Cov");

    /// <summary>Correlation.</summary>
    public static Operator Corr { get; } = Fn("corr", Two, OperatorFamily.Probability, Signature.Custom("RandomVariable² → Real", _ => Sort.Real), Comm, "\\operatorname{Corr}", "Corr");

    private static Operator Distribution(string id, int arity) =>
        Fn(id, Arity.Fixed(arity), OperatorFamily.Probability, Signature.Custom("Number → RandomVariable", _ => Sort.RandomVariableOf(Sort.Real)), OperatorAttributes.None, $"\\operatorname{{{id}}}");

    /// <summary>Bernoulli distribution.</summary>
    public static Operator Bernoulli { get; } = Distribution("Bernoulli", 1);

    /// <summary>Binomial distribution.</summary>
    public static Operator BinomialDist { get; } = Distribution("Binomial", 2);

    /// <summary>Geometric distribution.</summary>
    public static Operator Geometric { get; } = Distribution("Geometric", 1);

    /// <summary>Poisson distribution.</summary>
    public static Operator Poisson { get; } = Distribution("Poisson", 1);

    /// <summary>Hypergeometric distribution.</summary>
    public static Operator Hypergeometric { get; } = Distribution("Hypergeometric", 3);

    /// <summary>Uniform distribution.</summary>
    public static Operator Uniform { get; } = Distribution("Uniform", 2);

    /// <summary>Normal distribution.</summary>
    public static Operator Normal { get; } = Distribution("Normal", 2);

    /// <summary>Exponential distribution.</summary>
    public static Operator ExponentialDist { get; } = Distribution("Exponential", 1);

    /// <summary>Student's t distribution.</summary>
    public static Operator StudentT { get; } = Distribution("StudentT", 1);

    /// <summary>Chi-squared distribution.</summary>
    public static Operator ChiSquared { get; } = Distribution("ChiSquared", 1);

    // ----- Numeric evaluation -----

    /// <summary><c>N(x)</c> or <c>N(x, digits)</c>: the only way (besides C# doubles) to introduce approximate numbers.</summary>
    public static Operator N { get; } = Fn("N", Arity.Range(1, 2), OperatorFamily.Arithmetic, Signature.Custom("Number → Number", a => a[0]), OperatorAttributes.HoldArguments);

    // ----- Lookup -----

    /// <summary>Every built-in operator.</summary>
    public static IReadOnlyCollection<Operator> All { get; } = new ReadOnlyCollection<Operator>([.. ById.Values]);

    /// <summary>Finds an operator by its exact id.</summary>
    public static bool TryGet(string id, out Operator op) => ById.TryGetValue(id, out op!);

    /// <summary>Finds an operator by id or by an alias used in function-call syntax (<c>asin</c> for <c>arcsin</c>).</summary>
    public static bool TryGetByName(string name, out Operator op) => ByName.TryGetValue(name, out op!);

    /// <summary>All function-call names (ids and aliases) known to the parser.</summary>
    public static IReadOnlyCollection<string> FunctionNames { get; } = new ReadOnlyCollection<string>([.. ByName.Keys]);

    /// <summary>The operator with the given id.</summary>
    /// <exception cref="KeyNotFoundException">There is no such operator.</exception>
    public static Operator Get(string id) => ById.TryGetValue(id, out var op) ? op : throw new KeyNotFoundException($"Unknown operator '{id}'.");
}
