using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Printing;

/// <summary>Options for <see cref="TextPrinter"/>.</summary>
public sealed record PrintOptions
{
    /// <summary>Print π, ∞, ℝ and similar as Unicode symbols instead of ASCII names (<c>pi</c>, <c>oo</c>, <c>R</c>).</summary>
    public bool UnicodeSymbols { get; init; }

    /// <summary>
    /// Print sums whose terms are all polynomial terms in one variable in descending degree (<c>6 - 5x + x^2</c> prints as
    /// <c>x^2 - 5x + 6</c>). Meant for Canonical trees; Structural and Raw trees keep their order, so this is off by default.
    /// </summary>
    public bool DescendingPolynomials { get; init; }

    /// <summary>Print exact numbers as decimals rounded half away from zero (catalog <c>conv.rounding</c>) to this many fractional digits.</summary>
    public int? DecimalDigits { get; init; }

    /// <summary>The faithful form used by <c>ToString</c>: ASCII, trees print in the order they have, so parsing the output rebuilds the same Raw tree.</summary>
    public static PrintOptions Faithful { get; } = new();

    /// <summary>Unicode symbols and descending polynomial order, for showing Canonical results to people.</summary>
    public static PrintOptions Presentation { get; } = new() { UnicodeSymbols = true, DescendingPolynomials = true };
}

/// <summary>
/// Prints expressions as linear text with minimal parentheses derived from precedence and associativity
/// (docs/design/05-syntax-trees-and-notation.md, "Printing"). The default output parses back to the same Raw tree.
/// </summary>
/// <remarks>
/// Forms that only normalized trees contain are printed in the friendly way: a sum with a negative coefficient prints as
/// subtraction, a product with negative integer exponents prints as a fraction, <c>x^(1/2)</c> with an exact ½ prints as
/// <c>sqrt(x)</c> and <c>mul(−1, x)</c> as <c>-x</c>. Parsed trees never contain those forms, so printing parsed trees is exact.
/// </remarks>
public static class TextPrinter
{
    /// <summary>Prints with <see cref="PrintOptions.Faithful"/>.</summary>
    public static string Print(Expr expr) => Print(expr, PrintOptions.Faithful);

    /// <summary>Prints with the given options.</summary>
    public static string Print(Expr expr, PrintOptions options)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(options);
        return new Renderer(options).Render(expr).Text;
    }

    private readonly record struct Rendered(string Text, int Precedence);

    private sealed class Renderer(PrintOptions options)
    {
        public Rendered Render(Expr e) => e switch
        {
            Number n => RenderNumber(n),
            Float f => RenderFloat(f),
            Symbol s => new(s.Name, Precedence.Atom),
            Constant c => RenderConstant(c),
            Apply a => RenderApply(a),
            Bind b => RenderBind(b),
            MatrixLiteral m => new("[" + string.Join(", ", Enumerable.Range(0, m.Rows).Select(r => "[" + string.Join(", ", Enumerable.Range(0, m.Columns).Select(c => Text(m[r, c]))) + "]")) + "]", Precedence.Atom),
            SetLiteral s => new("{" + string.Join(", ", s.Elements.Select(Text)) + "}", Precedence.Atom),
            IntervalLiteral i => RenderInterval(i),
            TupleLiteral t => new("(" + string.Join(", ", t.Elements.Select(Text)) + ")", Precedence.Atom),
            Piecewise p => new("Piecewise[" + string.Join(", ", p.Cases.Select(c => $"({Text(c.Value)}, {Text(c.Condition)})")) + "]", Precedence.Atom),
            Wild w => new(w.Name + "_", Precedence.Atom),
            _ => new(e.GetType().Name, Precedence.Atom),
        };

        private string Text(Expr e) => Render(e).Text;

        // Parenthesizes a child whose precedence is below the minimum the position allows.
        private string Child(Expr e, int minimum)
        {
            var r = Render(e);
            return r.Precedence < minimum ? "(" + r.Text + ")" : r.Text;
        }

        // ----- Leaves -----

        private Rendered RenderNumber(Number n)
        {
            var v = n.Value;
            string text;
            if (options.DecimalDigits is { } digits) text = v.ToFixedString(digits);
            else if (n.Display.Kind == NumberDisplayKind.Decimal && v.Round(n.Display.Digits) == v) text = v.ToFixedString(n.Display.Digits);
            else text = v.ToString();
            var negative = v.Sign < 0;
            var fraction = text.Contains('/', StringComparison.Ordinal);
            return new(text, negative ? Precedence.Prefix : fraction ? Precedence.Multiplicative : Precedence.Atom);
        }

        private static Rendered RenderFloat(Float f)
        {
            var text = f.Value.ToString("R", CultureInfo.InvariantCulture);
            if (double.IsFinite(f.Value) && !text.Contains('.', StringComparison.Ordinal) && !text.Contains('E', StringComparison.Ordinal)) text += ".0";
            if (!double.IsFinite(f.Value)) return new(double.IsNaN(f.Value) ? "undefined" : f.Value > 0 ? "oo" : "-oo", Precedence.Atom);
            return new(text, f.Value < 0 ? Precedence.Prefix : Precedence.Atom);
        }

        private Rendered RenderConstant(Constant c)
        {
            var info = Constants.Info(c.Id);
            var text = options.UnicodeSymbols ? info.Unicode : info.Text;
            return new(text, c.Id == ConstantId.NegativeInfinity ? Precedence.Prefix : Precedence.Atom);
        }

        private Rendered RenderInterval(IntervalLiteral i)
        {
            var (lo, hi) = (Text(i.Lower), Text(i.Upper));
            var text = (i.LowerClosed, i.UpperClosed) switch
            {
                (true, true) => $"[{lo}, {hi}]",
                (true, false) => $"[{lo}, {hi})",
                (false, true) => $"({lo}, {hi}]",
                _ => $"]{lo}, {hi}[",
            };
            return new(text, Precedence.Atom);
        }

        // ----- Applications -----

        private Rendered RenderApply(Apply a)
        {
            var op = a.Operator;
            var args = a.Arguments;
            var id = op.Id;

            if (id == "add" || id == "sub") return RenderSum(a);
            if (id == "mul") return RenderProduct(args);
            if (id == "neg") return new("-" + Child(args[0], Precedence.Prefix), Precedence.Prefix);
            if (id == "div") return new(Child(args[0], Precedence.Multiplicative) + "/" + Child(args[1], Precedence.Multiplicative + 1), Precedence.Multiplicative);
            if (id == "pow") return RenderPower(args[0], args[1]);
            if (id == "sqrt") return options.UnicodeSymbols ? new("√" + Child(args[0], Precedence.Power), Precedence.Prefix) : new($"sqrt({Text(args[0])})", Precedence.Atom);
            if (id == "call") return new(Child(args[0], Precedence.Atom) + "(" + string.Join(", ", args.Skip(1).Select(Text)) + ")", Precedence.Atom);
            if (id == "derivativeOf" && args[1] is Number { Value.IsInteger: true } order && order.Value.Numerator is { } k && k >= 1 && k <= 6 && args[0] is Symbol f)
            {
                return new(f.Name + new string('\'', (int)k), Precedence.Atom);
            }

            switch (op.Notation.Fixity)
            {
                case Fixity.Infix:
                    return RenderInfix(a);
                case Fixity.Prefix:
                    // not: operand binds tighter than and/or
                    return new(op.Notation.Text + " " + Child(args[0], op.Notation.Precedence + 1), op.Notation.Precedence);
                case Fixity.Postfix:
                    // (n!)! needs its parentheses: n!! is the double factorial.
                    var operand = Child(args[0], Precedence.Atom);
                    if (op.Notation.Text.StartsWith('!') && operand.EndsWith('!')) operand = "(" + operand + ")";
                    return new(operand + op.Notation.Text, Precedence.Atom);
                default:
                    return new(op.Notation.Text + "(" + string.Join(", ", args.Select(Text)) + ")", Precedence.Atom);
            }
        }

        private Rendered RenderInfix(Apply a)
        {
            var op = a.Operator;
            var p = op.Notation.Precedence;
            var right = op.Notation.RightAssociative;
            var separator = $" {op.Notation.Text} ";
            var parts = new List<string>();
            for (var i = 0; i < a.Arguments.Length; i++)
            {
                // Left-associative: the first operand may sit at the same level, later ones must bind tighter. Mirrored for right-associative.
                var minimum = right ? (i == a.Arguments.Length - 1 ? p : p + 1) : (i == 0 ? p : p + 1);
                parts.Add(Child(a.Arguments[i], minimum));
            }
            return new(string.Join(separator, parts), p);
        }

        private Rendered RenderPower(Expr b, Expr e)
        {
            if (e is Number { Display.Kind: NumberDisplayKind.Default } half && half.Value == BigRational.Create(1, 2))
            {
                return options.UnicodeSymbols ? new("√" + Child(b, Precedence.Power), Precedence.Prefix) : new($"sqrt({Text(b)})", Precedence.Atom);
            }
            var left = Child(b, Precedence.Power + 1);

            // A prefix minus may follow ^ without parentheses (x^-1); anything looser needs them.
            var right = Child(e, Precedence.Prefix);

            // x^T, x^H and x^c read as transpose, conjugate transpose and complement, so a symbol with those names is parenthesized.
            if (e is Symbol { Name: "T" or "H" or "c" }) right = "(" + right + ")";
            return new(left + "^" + right, Precedence.Power);
        }

        // ----- Sums -----

        private Rendered RenderSum(Apply a)
        {
            var args = a.Arguments;
            if (a.Operator.Id == "sub")
            {
                return new(Child(args[0], Precedence.Additive) + " - " + Child(args[1], Precedence.Additive + 1), Precedence.Additive);
            }

            var terms = args.ToList();
            if (options.DescendingPolynomials) terms = OrderPolynomialTerms(terms);

            var sb = new StringBuilder();
            for (var i = 0; i < terms.Count; i++)
            {
                var term = terms[i];
                var (negative, magnitude) = SplitSign(term);
                var minimum = Precedence.Additive + (i == 0 ? 0 : 1);
                if (i == 0)
                {
                    sb.Append(negative ? "-" + Child(magnitude, Precedence.Prefix) : Child(term, Precedence.Additive));
                }
                else if (negative)
                {
                    sb.Append(" - ").Append(Child(magnitude, Precedence.Additive + 1));
                }
                else
                {
                    sb.Append(" + ").Append(Child(term, minimum));
                }
            }
            return new(sb.ToString(), Precedence.Additive);
        }

        // For printing a term after '+': (true, |term|) when the term carries a negative numeric coefficient.
        private static (bool Negative, Expr Magnitude) SplitSign(Expr term)
        {
            switch (term)
            {
                case Number n when n.Value.Sign < 0:
                    return (true, new Number(-n.Value));
                case Float f when f.Value < 0:
                    return (true, new Float(-f.Value));
                case Apply { Operator.Id: "mul" } m when m.Arguments[0] is Number c && c.Value.Sign < 0:
                    var rest = m.Arguments.RemoveAt(0);
                    var inner = c.Value == BigRational.NegativeOne ? null : new Number(-c.Value);
                    var factors = inner is null ? rest : rest.Insert(0, inner);
                    return (true, factors.Length == 1 ? factors[0] : new Apply(Operators.Mul, factors));
                default:
                    return (false, term);
            }
        }

        private static List<Expr> OrderPolynomialTerms(List<Expr> terms)
        {
            Symbol? variable = null;
            var degrees = new List<int>();
            foreach (var t in terms)
            {
                if (!TryDegree(t, ref variable, out var degree)) return terms;
                degrees.Add(degree);
            }
            if (variable is null || degrees.Distinct().Count() != degrees.Count) return terms;
            return [.. terms.Zip(degrees).OrderByDescending(p => p.Second).Select(p => p.First)];
        }

        // A polynomial term in one variable: c, c*x, c*x^n, x^n.
        private static bool TryDegree(Expr term, ref Symbol? variable, out int degree)
        {
            degree = 0;
            switch (term)
            {
                case Number or Float:
                    return true;
                case Symbol s:
                    degree = 1;
                    return Same(ref variable, s);
                case Apply { Operator.Id: "pow" } p when p.Arguments[0] is Symbol b && p.Arguments[1] is Number { Value.IsInteger: true } e && e.Value.Sign > 0 && e.Value.Numerator < 1000:
                    degree = (int)e.Value.Numerator;
                    return Same(ref variable, b);
                case Apply { Operator.Id: "mul" } m when m.Arguments.Length == 2 && m.Arguments[0] is Number or Float:
                    return TryDegree(m.Arguments[1], ref variable, out degree) && degree > 0;
                default:
                    return false;
            }

            static bool Same(ref Symbol? current, Symbol s)
            {
                current ??= s;
                return current.Equals(s);
            }
        }

        // ----- Products -----

        private Rendered RenderProduct(ImmutableArray<Expr> factors)
        {
            // Negative coefficient: -x, -(x + y); a coefficient of -1 is just the sign.
            if (factors[0] is Number { Value.Sign: < 0 } c)
            {
                var rest = factors.RemoveAt(0);
                if (c.Value == BigRational.NegativeOne && rest.Length > 0)
                {
                    var inner = rest.Length == 1 ? Render(rest[0]) : RenderProduct(rest);
                    return new("-" + (inner.Precedence < Precedence.Prefix ? "(" + inner.Text + ")" : inner.Text), Precedence.Prefix);
                }
            }

            // Negative integer exponents print as a fraction: a * b^-1 -> a/b.
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
                var top = numerator.Count == 0 ? new Rendered("1", Precedence.Atom) : JoinFactors([.. numerator]);
                var bottom = denominator.Count == 1 ? denominator[0] : new Apply(Operators.Mul, [.. denominator]);
                var topText = top.Precedence < Precedence.Multiplicative ? "(" + top.Text + ")" : top.Text;
                return new(topText + "/" + Child(bottom, Precedence.Multiplicative + 1), Precedence.Multiplicative);
            }
            return JoinFactors(factors);
        }

        private Rendered JoinFactors(ImmutableArray<Expr> factors)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < factors.Length; i++)
            {
                var text = Child(factors[i], i == 0 ? Precedence.Multiplicative : Precedence.Multiplicative + 1);
                if (i > 0) sb.Append(UseImplicit(factors[i - 1], factors[i], sb, text) ? string.Empty : "*");
                sb.Append(text);
            }
            return new(sb.ToString(), Precedence.Multiplicative);
        }

        // Juxtaposition (2x, 2(x + 1)) is used only after a plain number, where it cannot be misread.
        private static bool UseImplicit(Expr left, Expr right, StringBuilder soFar, string rightText)
        {
            if (left is not Number { Value.IsInteger: true, Value.Sign: >= 0 } && !(left is Number n && n.Display.Kind == NumberDisplayKind.Decimal && n.Value.Sign >= 0)) return false;
            if (right is Number or Float || rightText.Length == 0) return false;
            var first = rightText[0];

            // "2e3" would read as a number in scientific notation.
            if ((first is 'e' or 'E') && soFar.Length > 0 && char.IsDigit(soFar[^1])) return false;
            return char.IsLetter(first) || first == '(' || char.IsHighSurrogate(first);
        }

        // ----- Binders -----

        private Rendered RenderBind(Bind b)
        {
            string Data(int i) => Text(b.Data[i]);
            string body = Text(b.Body);
            var x = b.Bound[0].Name;
            switch (b.Binder)
            {
                case Binder.Sum:
                    return new($"sum({body}, {x}, {Data(0)}, {Data(1)})", Precedence.Atom);
                case Binder.Product:
                    return new($"product({body}, {x}, {Data(0)}, {Data(1)})", Precedence.Atom);
                case Binder.IndexedUnion:
                    return new($"Union({body}, {x}, {Data(0)}, {Data(1)})", Precedence.Atom);
                case Binder.IndexedIntersection:
                    return new($"Intersection({body}, {x}, {Data(0)}, {Data(1)})", Precedence.Atom);
                case Binder.Integral:
                    if (b.Bound.Length == 1) return new($"integrate({body}, {x}, {Data(0)}, {Data(1)})", Precedence.Atom);
                    return new($"integrate({body}, {string.Join(", ", b.Bound.Select((s, i) => $"({s.Name}, {Data(2 * i)}, {Data(2 * i + 1)})"))})", Precedence.Atom);
                case Binder.Limit:
                    return new(b.Data.Length == 2 ? $"limit({body}, {x}, {Data(0)}, \"{(b.Data[1] is Number { Value.Sign: < 0 } ? "-" : "+")}\")" : $"limit({body}, {x}, {Data(0)})", Precedence.Atom);
                case Binder.Laplace:
                    return new($"laplace({body}, {x}, {Data(0)})", Precedence.Atom);
                case Binder.Fourier:
                    return new($"fourier({body}, {x}, {Data(0)})", Precedence.Atom);
                case Binder.ArgMin:
                    return new($"argmin({body}, {x} in {Data(0)})", Precedence.Atom);
                case Binder.ArgMax:
                    return new($"argmax({body}, {x} in {Data(0)})", Precedence.Atom);
                case Binder.ForAll or Binder.Exists or Binder.ExistsUnique:
                    var keyword = b.Binder switch { Binder.ForAll => "forall", Binder.Exists => "exists", _ => "exists!" };
                    var names = string.Join(", ", b.Bound.Select(s => s.Name));
                    var domain = b.Data.Length > 0 ? " in " + Child(b.Data[0], Precedence.Union) : string.Empty;
                    return new($"{keyword} {names}{domain}: {body}", Precedence.Quantifier);
                case Binder.Lambda:
                    var parameters = b.Bound.Length == 1 ? x : "(" + string.Join(", ", b.Bound.Select(s => s.Name)) + ")";
                    return new($"{parameters} -> {body}", 5);
                case Binder.SetBuilder:
                    return new($"{{{x} in {Data(0)} | {body}}}", Precedence.Atom);
                case Binder.ImageSet:
                    return new($"{{{body} | {x} in {Data(0)}}}", Precedence.Atom);
                default:
                    return new(b.Binder.ToString(), Precedence.Atom);
            }
        }
    }
}
