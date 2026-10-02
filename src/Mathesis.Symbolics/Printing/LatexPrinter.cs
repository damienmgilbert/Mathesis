using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Printing;

/// <summary>
/// Prints expressions as LaTeX: <c>\frac</c> for quotients, <c>\sqrt</c> for roots, <c>\left(\right)</c> only around tall content,
/// <c>\,dx</c> after integrands, <c>\operatorname{…}</c> for functions without a LaTeX macro.
/// </summary>
public static class LatexPrinter
{
    private static readonly Dictionary<string, string> GreekLetters = new(StringComparer.Ordinal)
    {
        ["α"] = "\\alpha", ["β"] = "\\beta", ["γ"] = "\\gamma", ["δ"] = "\\delta", ["ε"] = "\\varepsilon", ["ζ"] = "\\zeta", ["η"] = "\\eta",
        ["θ"] = "\\theta", ["ι"] = "\\iota", ["κ"] = "\\kappa", ["λ"] = "\\lambda", ["μ"] = "\\mu", ["ν"] = "\\nu", ["ξ"] = "\\xi",
        ["ο"] = "o", ["ρ"] = "\\rho", ["σ"] = "\\sigma", ["τ"] = "\\tau", ["υ"] = "\\upsilon", ["φ"] = "\\varphi", ["χ"] = "\\chi",
        ["ψ"] = "\\psi", ["ω"] = "\\omega", ["Γ"] = "\\Gamma", ["Δ"] = "\\Delta", ["Θ"] = "\\Theta", ["Λ"] = "\\Lambda", ["Ξ"] = "\\Xi",
        ["Π"] = "\\Pi", ["Σ"] = "\\Sigma", ["Φ"] = "\\Phi", ["Ψ"] = "\\Psi", ["Ω"] = "\\Omega",
    };

    /// <summary>Prints <paramref name="expr"/> as LaTeX.</summary>
    public static string Print(Expr expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        return new Renderer().Render(expr).Text;
    }

    private readonly record struct Rendered(string Text, int Precedence);

    private sealed class Renderer
    {
        public Rendered Render(Expr e) => e switch
        {
            Number n => RenderNumber(n),
            Float f => new(f.Value.ToString("R", CultureInfo.InvariantCulture), f.Value < 0 ? Precedence.Prefix : Precedence.Atom),
            Symbol s => new(SymbolName(s.Name), Precedence.Atom),
            Constant c => new(Constants.Info(c.Id).Latex, c.Id == ConstantId.NegativeInfinity ? Precedence.Prefix : Precedence.Atom),
            Apply a => RenderApply(a),
            Bind b => RenderBind(b),
            MatrixLiteral m => new("\\begin{pmatrix} " + string.Join(" \\\\ ", Enumerable.Range(0, m.Rows).Select(r => string.Join(" & ", Enumerable.Range(0, m.Columns).Select(c => Text(m[r, c]))))) + " \\end{pmatrix}", Precedence.Atom),
            SetLiteral s => new("\\{" + string.Join(", ", s.Elements.Select(Text)) + "\\}", Precedence.Atom),
            IntervalLiteral i => RenderInterval(i),
            TupleLiteral t => new("(" + string.Join(", ", t.Elements.Select(Text)) + ")", Precedence.Atom),
            Piecewise p => RenderPiecewise(p),
            Wild w => new("\\mathrm{" + w.Name + "\\_}", Precedence.Atom),
            _ => new(e.GetType().Name, Precedence.Atom),
        };

        private string Text(Expr e) => Render(e).Text;

        private static bool IsTall(string latex) =>
            latex.Contains("\\frac", StringComparison.Ordinal) || latex.Contains("\\sum", StringComparison.Ordinal) || latex.Contains("\\int", StringComparison.Ordinal)
            || latex.Contains("\\prod", StringComparison.Ordinal) || latex.Contains("\\begin", StringComparison.Ordinal) || latex.Contains("\\binom", StringComparison.Ordinal)
            || latex.Contains("\\lim", StringComparison.Ordinal);

        private static string Paren(string inner) => IsTall(inner) ? "\\left(" + inner + "\\right)" : "(" + inner + ")";

        private string Child(Expr e, int minimum)
        {
            var r = Render(e);
            return r.Precedence < minimum ? Paren(r.Text) : r.Text;
        }

        private static string SymbolName(string name)
        {
            if (GreekLetters.TryGetValue(name, out var greek)) return greek;
            var underscore = name.IndexOf('_', StringComparison.Ordinal);
            if (underscore > 0 && underscore < name.Length - 1) return SymbolName(name[..underscore]) + "_{" + name[(underscore + 1)..] + "}";
            if (name.EndsWith('\'')) return SymbolName(name.TrimEnd('\'')) + new string('\'', name.Length - name.TrimEnd('\'').Length);
            if (name.Length == 1 || (name.Length == 2 && char.IsHighSurrogate(name[0]))) return name;
            return name.All(char.IsLetter) ? "\\mathit{" + name + "}" : name.Replace("_", "\\_", StringComparison.Ordinal);
        }

        private static Rendered RenderNumber(Number n)
        {
            var v = n.Value;
            if (n.Display.Kind == NumberDisplayKind.Decimal && v.Round(n.Display.Digits) == v)
            {
                return new(v.ToFixedString(n.Display.Digits), v.Sign < 0 ? Precedence.Prefix : Precedence.Atom);
            }
            if (v.IsInteger) return new(v.ToString(), v.Sign < 0 ? Precedence.Prefix : Precedence.Atom);
            var fraction = "\\frac{" + System.Numerics.BigInteger.Abs(v.Numerator) + "}{" + v.Denominator + "}";
            return v.Sign < 0 ? new("-" + fraction, Precedence.Prefix) : new(fraction, Precedence.Multiplicative);
        }

        private Rendered RenderInterval(IntervalLiteral i)
        {
            var (lo, hi) = (Text(i.Lower), Text(i.Upper));
            return new($"{(i.LowerClosed ? "[" : "(")}{lo}, {hi}{(i.UpperClosed ? "]" : ")")}", Precedence.Atom);
        }

        private Rendered RenderPiecewise(Piecewise p)
        {
            var rows = p.Cases.Select(c => c.Condition is Constant { Id: ConstantId.True } ? $"{Text(c.Value)} & \\text{{otherwise}}" : $"{Text(c.Value)} & \\text{{if }} {Text(c.Condition)}");
            return new("\\begin{cases} " + string.Join(" \\\\ ", rows) + " \\end{cases}", Precedence.Atom);
        }

        // ----- Applications -----

        private static string FunctionMacro(Operator op) =>
            op.Notation.Latex is { } l && l.StartsWith('\\') ? l : "\\operatorname{" + op.Id + "}";

        private Rendered RenderApply(Apply a)
        {
            var op = a.Operator;
            var args = a.Arguments;
            switch (op.Id)
            {
                case "add" or "sub":
                    return RenderSum(a);
                case "mul":
                    return RenderProduct(args);
                case "neg":
                    return new("-" + Child(args[0], Precedence.Prefix), Precedence.Prefix);
                case "div":
                    return new("\\frac{" + Text(args[0]) + "}{" + Text(args[1]) + "}", Precedence.Multiplicative);
                case "pow":
                    return RenderPower(args[0], args[1]);
                case "sqrt":
                    return new("\\sqrt{" + Text(args[0]) + "}", Precedence.Atom);
                case "root":
                    return new("\\sqrt[" + Text(args[1]) + "]{" + Text(args[0]) + "}", Precedence.Atom);
                case "exp":
                    return new("e^{" + Text(args[0]) + "}", Precedence.Power);
                case "abs":
                    return new("\\left|" + Text(args[0]) + "\\right|", Precedence.Atom);
                case "floor":
                    return new("\\left\\lfloor " + Text(args[0]) + " \\right\\rfloor", Precedence.Atom);
                case "ceil":
                    return new("\\left\\lceil " + Text(args[0]) + " \\right\\rceil", Precedence.Atom);
                case "norm":
                    return new("\\left\\lVert " + Text(args[0]) + " \\right\\rVert" + (args.Length == 2 ? "_{" + Text(args[1]) + "}" : string.Empty), Precedence.Atom);
                case "factorial" or "factorial2":
                    return new(Child(args[0], Precedence.Atom + 1 > 100 ? 100 : Precedence.Atom) + (op.Id == "factorial" ? "!" : "!!"), Precedence.Atom);
                case "binomial":
                    return new("\\binom{" + Text(args[0]) + "}{" + Text(args[1]) + "}", Precedence.Atom);
                case "log":
                    return args[1] is Number { Value.IsInteger: true } ten && ten.Value == 10
                        ? new("\\log" + Paren(Text(args[0])), Precedence.Atom)
                        : new("\\log_{" + Text(args[1]) + "}" + Paren(Text(args[0])), Precedence.Atom);
                case "call":
                    return new(Child(args[0], Precedence.Atom) + Paren(string.Join(", ", args.Skip(1).Select(Text))), Precedence.Atom);
                case "derivativeOf" when args[1] is Number { Value.IsInteger: true } order && args[0] is Symbol f && order.Value.Numerator >= 1 && order.Value.Numerator <= 6:
                    return new(SymbolName(f.Name) + new string('\'', (int)order.Value.Numerator), Precedence.Atom);
                case "diff":
                    return RenderDerivative(args);
                case "integrate":
                    return new("\\int " + Child(args[0], Precedence.Additive + 1) + " \\,d" + Text(args[1]), Precedence.Multiplicative);
                case "transpose":
                    return new(Child(args[0], Precedence.Atom) + "^{\\top}", Precedence.Atom);
                case "conjTranspose":
                    return new(Child(args[0], Precedence.Atom) + "^{H}", Precedence.Atom);
                case "complement":
                    return new(Child(args[0], Precedence.Atom) + "^{c}", Precedence.Atom);
                case "powerset":
                    return new("\\mathcal{P}" + Paren(Text(args[0])), Precedence.Atom);
                case "congruent":
                    return new(Child(args[0], Precedence.Relation + 1) + " \\equiv " + Child(args[1], Precedence.Relation + 1) + " \\pmod{" + Text(args[2]) + "}", Precedence.Relation);
                case "not":
                    return new("\\neg " + Child(args[0], Precedence.Not + 1), Precedence.Not);
            }

            switch (op.Notation.Fixity)
            {
                case Fixity.Infix:
                    var p = op.Notation.Precedence;
                    var right = op.Notation.RightAssociative;
                    var symbol = op.Notation.Latex ?? op.Notation.Text;
                    var parts = new List<string>();
                    for (var i = 0; i < args.Length; i++)
                    {
                        var minimum = right ? (i == args.Length - 1 ? p : p + 1) : (i == 0 ? p : p + 1);
                        parts.Add(Child(args[i], minimum));
                    }
                    return new(string.Join(" " + symbol + " ", parts), p);
                case Fixity.Postfix:
                    return new(Child(args[0], Precedence.Atom) + op.Notation.Text, Precedence.Atom);
                default:
                    return new(FunctionMacro(op) + Paren(string.Join(", ", args.Select(Text))), Precedence.Atom);
            }
        }

        private Rendered RenderDerivative(ImmutableArray<Expr> args)
        {
            var variable = Text(args[1]);
            var operand = Child(args[0], Precedence.Additive + 1);
            if (args.Length == 3 && args[2] is Number { Value.IsInteger: true } order && order.Value != BigRational.One)
            {
                return new($"\\frac{{d^{{{order.Value}}}}}{{d{variable}^{{{order.Value}}}}} {operand}", Precedence.Multiplicative);
            }
            return new($"\\frac{{d}}{{d{variable}}} {operand}", Precedence.Multiplicative);
        }

        private Rendered RenderPower(Expr b, Expr e)
        {
            if (e is Number { Display.Kind: NumberDisplayKind.Default } half && half.Value == BigRational.Create(1, 2)) return new("\\sqrt{" + Text(b) + "}", Precedence.Atom);

            // sin^2(x): the exponent goes on the function name.
            if (b is Apply { Operator.Family: OperatorFamily.Trig or OperatorFamily.Hyperbolic } f && f.Arguments.Length == 1 && f.Operator.Notation.Latex is { } macro && macro.StartsWith('\\'))
            {
                return new(macro + "^{" + Text(e) + "}" + Paren(Text(f.Arguments[0])), Precedence.Atom);
            }
            var left = Child(b, Precedence.Power + 1);
            return new(left + "^{" + Text(e) + "}", Precedence.Power);
        }

        private Rendered RenderSum(Apply a)
        {
            var args = a.Arguments;
            if (a.Operator.Id == "sub") return new(Child(args[0], Precedence.Additive) + " - " + Child(args[1], Precedence.Additive + 1), Precedence.Additive);
            var sb = new StringBuilder();
            for (var i = 0; i < args.Length; i++)
            {
                var (negative, magnitude) = SplitSign(args[i]);
                if (i == 0) sb.Append(negative ? "-" + Child(magnitude, Precedence.Prefix) : Child(args[i], Precedence.Additive));
                else if (negative) sb.Append(" - ").Append(Child(magnitude, Precedence.Additive + 1));
                else sb.Append(" + ").Append(Child(args[i], Precedence.Additive + 1));
            }
            return new(sb.ToString(), Precedence.Additive);
        }

        private static (bool Negative, Expr Magnitude) SplitSign(Expr term)
        {
            switch (term)
            {
                case Number n when n.Value.Sign < 0:
                    return (true, new Number(-n.Value));
                case Apply { Operator.Id: "mul" } m when m.Arguments[0] is Number c && c.Value.Sign < 0:
                    var rest = m.Arguments.RemoveAt(0);
                    var factors = c.Value == BigRational.NegativeOne ? rest : rest.Insert(0, new Number(-c.Value));
                    return (true, factors.Length == 1 ? factors[0] : new Apply(Operators.Mul, factors));
                default:
                    return (false, term);
            }
        }

        private Rendered RenderProduct(ImmutableArray<Expr> factors)
        {
            if (factors[0] is Number { Value.Sign: < 0 } c && c.Value == BigRational.NegativeOne && factors.Length > 1)
            {
                var rest = factors.RemoveAt(0);
                var inner = rest.Length == 1 ? Render(rest[0]) : RenderProduct(rest);
                return new("-" + (inner.Precedence < Precedence.Prefix ? Paren(inner.Text) : inner.Text), Precedence.Prefix);
            }

            // Negative integer exponents print as a fraction.
            var numerator = new List<Expr>();
            var denominator = new List<Expr>();
            foreach (var f in factors)
            {
                if (f is Apply { Operator.Id: "pow" } p && p.Arguments[1] is Number { Value.IsInteger: true } e && e.Value.Sign < 0 && p.Arguments[0] is not Number)
                {
                    var magnitude = -e.Value;
                    denominator.Add(magnitude == BigRational.One ? p.Arguments[0] : new Apply(Operators.Pow, [p.Arguments[0], new Number(magnitude)]));
                }
                else
                {
                    numerator.Add(f);
                }
            }
            if (denominator.Count > 0)
            {
                var top = numerator.Count == 0 ? "1" : Join([.. numerator]).Text;
                var bottom = Join([.. denominator]).Text;
                return new("\\frac{" + top + "}{" + bottom + "}", Precedence.Multiplicative);
            }
            return Join(factors);
        }

        private Rendered Join(ImmutableArray<Expr> factors)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < factors.Length; i++)
            {
                var text = Child(factors[i], i == 0 ? Precedence.Multiplicative : Precedence.Multiplicative + 1);
                if (i > 0)
                {
                    var implicitProduct = factors[i - 1] is Number { Value.IsInteger: true, Value.Sign: >= 0 } && factors[i] is not (Number or Float) && text.Length > 0 && (char.IsLetter(text[0]) || text[0] is '\\' or '(');
                    sb.Append(implicitProduct ? string.Empty : " \\cdot ");
                }
                sb.Append(text);
            }
            return new(sb.ToString(), Precedence.Multiplicative);
        }

        // ----- Binders -----

        private Rendered RenderBind(Bind b)
        {
            string Data(int i) => Text(b.Data[i]);
            var x = SymbolName(b.Bound[0].Name);
            var body = Child(b.Body, Precedence.Additive + 1);
            var rawBody = Text(b.Body);
            switch (b.Binder)
            {
                case Binder.Sum:
                    return new($"\\sum_{{{x}={Data(0)}}}^{{{Data(1)}}} {body}", Precedence.Multiplicative);
                case Binder.Product:
                    return new($"\\prod_{{{x}={Data(0)}}}^{{{Data(1)}}} {body}", Precedence.Multiplicative);
                case Binder.IndexedUnion:
                    return new($"\\bigcup_{{{x}={Data(0)}}}^{{{Data(1)}}} {body}", Precedence.Multiplicative);
                case Binder.IndexedIntersection:
                    return new($"\\bigcap_{{{x}={Data(0)}}}^{{{Data(1)}}} {body}", Precedence.Multiplicative);
                case Binder.Integral:
                    var limits = string.Concat(b.Bound.Select((s, i) => $"\\int_{{{Data(2 * i)}}}^{{{Data(2 * i + 1)}}}"));
                    var differentials = string.Concat(b.Bound.Select(s => $"\\,d{SymbolName(s.Name)}"));
                    return new($"{limits} {body} {differentials}", Precedence.Multiplicative);
                case Binder.Limit:
                    var point = b.Data.Length == 2 ? $"{Data(0)}^{{{(b.Data[1] is Number { Value.Sign: < 0 } ? "-" : "+")}}}" : Data(0);
                    return new($"\\lim_{{{x} \\to {point}}} {body}", Precedence.Multiplicative);
                case Binder.Laplace:
                    return new($"\\mathcal{{L}}\\left\\{{{rawBody}\\right\\}}({Data(0)})", Precedence.Atom);
                case Binder.Fourier:
                    return new($"\\mathcal{{F}}\\left\\{{{rawBody}\\right\\}}({Data(0)})", Precedence.Atom);
                case Binder.ArgMin:
                    return new($"\\operatorname*{{arg\\,min}}_{{{x} \\in {Data(0)}}} {body}", Precedence.Multiplicative);
                case Binder.ArgMax:
                    return new($"\\operatorname*{{arg\\,max}}_{{{x} \\in {Data(0)}}} {body}", Precedence.Multiplicative);
                case Binder.ForAll or Binder.Exists or Binder.ExistsUnique:
                    var quantifier = b.Binder switch { Binder.ForAll => "\\forall", Binder.Exists => "\\exists", _ => "\\exists!" };
                    var names = string.Join(", ", b.Bound.Select(s => SymbolName(s.Name)));
                    var domain = b.Data.Length > 0 ? " \\in " + Child(b.Data[0], Precedence.Union) : string.Empty;
                    return new($"{quantifier} {names}{domain}:\\ {rawBody}", Precedence.Quantifier);
                case Binder.Lambda:
                    var parameters = b.Bound.Length == 1 ? x : "(" + string.Join(", ", b.Bound.Select(s => SymbolName(s.Name))) + ")";
                    return new($"{parameters} \\mapsto {rawBody}", 5);
                case Binder.SetBuilder:
                    return new($"\\{{{x} \\in {Data(0)} \\mid {rawBody}\\}}", Precedence.Atom);
                case Binder.ImageSet:
                    return new($"\\{{{rawBody} \\mid {x} \\in {Data(0)}\\}}", Precedence.Atom);
                default:
                    return new(b.Binder.ToString(), Precedence.Atom);
            }
        }
    }
}
