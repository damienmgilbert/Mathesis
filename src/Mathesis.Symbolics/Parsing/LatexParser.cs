using System.Collections.Immutable;
using System.Globalization;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Parsing;

/// <summary>
/// Parses the LaTeX subset of docs/design/05-syntax-trees-and-notation.md ("LaTeX input subset") into Raw trees:
/// <c>\frac</c>, <c>\sqrt[n]{}</c>, <c>^{}</c> and <c>_{}</c>, <c>\cdot \times \div \pm</c>, <c>\left( \right)</c>, function macros,
/// <c>\int_{a}^{b} … \,dx</c>, <c>\sum</c>, <c>\prod</c>, <c>\lim</c>, Greek letters, <c>\mathbb{R}</c>, relations, logic,
/// <c>\forall \exists</c>, <c>pmatrix</c>/<c>bmatrix</c>/<c>vmatrix</c>, <c>cases</c>, sets, <c>\binom</c>, <c>\overline</c>,
/// <c>\vec</c>, <c>\|v\|</c>, floors and ceilings, and <c>\operatorname</c>. Unsupported input is reported as a
/// <see cref="ParseError"/> with its source span; the parser never throws.
/// </summary>
public static class LatexParser
{
    /// <summary>Parses LaTeX into a Raw tree.</summary>
    public static ParseResult Parse(string latex, ParserOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(latex);
        return new LatexEngine(latex, options ?? new ParserOptions()).Run();
    }
}

internal enum LatexKind : byte
{
    Command,
    Char,
    Number,
    End,
}

internal readonly record struct LatexToken(LatexKind Kind, string Text, int Start, int End, BigRational Value = default)
{
    public TextSpan Span => new(Start, End - Start);

    public bool IsChar(char c) => Kind == LatexKind.Char && Text.Length == 1 && Text[0] == c;

    public bool IsCommand(string name) => Kind == LatexKind.Command && Text == name;
}

internal sealed class LatexEngine
{
    private const int MaxDepth = 150;

    private static readonly Dictionary<string, string> Greek = new(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["varepsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η",
        ["theta"] = "θ", ["vartheta"] = "θ", ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ",
        ["rho"] = "ρ", ["sigma"] = "σ", ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "φ", ["varphi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω",
        ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ", ["Pi"] = "Π", ["Sigma"] = "Σ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
    };

    private static readonly Dictionary<string, string> FunctionMacros = new(StringComparer.Ordinal)
    {
        ["sin"] = "sin", ["cos"] = "cos", ["tan"] = "tan", ["cot"] = "cot", ["sec"] = "sec", ["csc"] = "csc",
        ["arcsin"] = "arcsin", ["arccos"] = "arccos", ["arctan"] = "arctan", ["sinh"] = "sinh", ["cosh"] = "cosh", ["tanh"] = "tanh",
        ["coth"] = "coth", ["ln"] = "ln", ["log"] = "log", ["exp"] = "exp", ["det"] = "det", ["gcd"] = "gcd", ["max"] = "max", ["min"] = "min",
        ["arg"] = "arg", ["Re"] = "re", ["Im"] = "im", ["deg"] = "deg",
    };

    private static readonly Dictionary<string, string> InverseNames = new(StringComparer.Ordinal)
    {
        ["sin"] = "arcsin", ["cos"] = "arccos", ["tan"] = "arctan", ["cot"] = "arccot", ["sec"] = "arcsec", ["csc"] = "arccsc",
        ["sinh"] = "arsinh", ["cosh"] = "arcosh", ["tanh"] = "artanh",
    };

    private static readonly Dictionary<string, Operator> RelationCommands = new(StringComparer.Ordinal)
    {
        ["le"] = Operators.Le, ["leq"] = Operators.Le, ["ge"] = Operators.Ge, ["geq"] = Operators.Ge, ["ne"] = Operators.Ne, ["neq"] = Operators.Ne,
        ["approx"] = Operators.Approx, ["in"] = Operators.Element, ["notin"] = Operators.NotElement, ["subset"] = Operators.Subset,
        ["subseteq"] = Operators.SubsetEq, ["propto"] = Operators.Proportional, ["perp"] = Operators.Perpendicular, ["parallel"] = Operators.Parallel,
    };

    private readonly string _text;
    private readonly ParserOptions _options;
    private ImmutableArray<LatexToken> _tokens;
    private int _position;
    private int _depth;
    private int _noBar;
    private int _integrals;
    private int? _limitDirection;
    private bool _readingLimit;

    private sealed class Failure(ParseError error) : Exception
    {
        public ParseError Error { get; } = error;
    }

    public LatexEngine(string text, ParserOptions options)
    {
        _text = text;
        _options = options;
    }

    private static Failure Fail(string message, TextSpan span, ImmutableArray<string> expected = default) =>
        new(new ParseError(message, span, expected.IsDefault ? [] : expected));

    public ParseResult Run()
    {
        try
        {
            _tokens = Tokenize();
            if (_tokens[0].Kind == LatexKind.End) throw Fail("Expected an expression.", _tokens[0].Span, ["expression"]);
            var expr = ParseExpr(0);
            if (Peek().Kind != LatexKind.End) throw Fail($"Unexpected {Describe(Peek())}.", Peek().Span, ["operator", "end of input"]);
            return new ParseResult(expr, [], []);
        }
        catch (Failure f)
        {
            return new ParseResult(null, [f.Error], []);
        }
    }

    // ----- Tokens -----

    private ImmutableArray<LatexToken> Tokenize()
    {
        var tokens = ImmutableArray.CreateBuilder<LatexToken>();
        var i = 0;
        while (i < _text.Length)
        {
            var c = _text[i];
            var start = i;
            if (char.IsWhiteSpace(c) || c == '~')
            {
                i++;
            }
            else if (c == '\\')
            {
                i++;
                if (i >= _text.Length) throw Fail("A backslash must be followed by a command.", new TextSpan(start, 1));
                if (char.IsLetter(_text[i]))
                {
                    while (i < _text.Length && char.IsLetter(_text[i])) i++;
                    var name = _text[(start + 1)..i];
                    if (name is "quad" or "qquad" or "displaystyle" or "bigl" or "bigr" or "Bigl" or "Bigr") continue;
                    tokens.Add(new LatexToken(LatexKind.Command, name, start, i));
                }
                else
                {
                    i++;
                    var symbol = _text[(start + 1)..i];
                    if (symbol is "," or ";" or "!" or ":" or " ") continue;
                    tokens.Add(new LatexToken(LatexKind.Command, symbol, start, i));
                }
            }
            else if (c is >= '0' and <= '9' || (c == '.' && i + 1 < _text.Length && _text[i + 1] is >= '0' and <= '9'))
            {
                while (i < _text.Length && _text[i] is >= '0' and <= '9') i++;
                if (i + 1 < _text.Length && _text[i] == '.' && _text[i + 1] is >= '0' and <= '9')
                {
                    i++;
                    while (i < _text.Length && _text[i] is >= '0' and <= '9') i++;
                }
                var literal = _text[start..i];
                tokens.Add(new LatexToken(LatexKind.Number, literal, start, i, BigRational.TryParse(literal, CultureInfo.InvariantCulture, out var v) ? v : BigRational.Zero));
            }
            else
            {
                if (char.IsHighSurrogate(c) && i + 1 < _text.Length) i++;
                i++;
                tokens.Add(new LatexToken(LatexKind.Char, _text[start..i], start, i));
            }
        }
        tokens.Add(new LatexToken(LatexKind.End, string.Empty, _text.Length, _text.Length));
        return tokens.ToImmutable();
    }

    private LatexToken Peek(int offset = 0) => _tokens[Math.Min(_position + offset, _tokens.Length - 1)];

    private LatexToken Next()
    {
        var t = _tokens[_position];
        if (t.Kind != LatexKind.End) _position++;
        return t;
    }

    private static string Describe(LatexToken t) => t.Kind switch
    {
        LatexKind.End => "end of input",
        LatexKind.Command => $"'\\{t.Text}'",
        _ => $"'{t.Text}'",
    };

    private void ExpectChar(char c)
    {
        var t = Peek();
        if (!t.IsChar(c)) throw Fail(t.Kind == LatexKind.End ? $"Expected '{c}' but the input ended." : $"Expected '{c}' but found {Describe(t)}.", t.Span, [$"'{c}'"]);
        Next();
    }

    private void ExpectCommand(string name)
    {
        var t = Peek();
        if (!t.IsCommand(name)) throw Fail($"Expected '\\{name}' but found {Describe(t)}.", t.Span, [$"'\\{name}'"]);
        Next();
    }

    private bool AcceptChar(char c)
    {
        if (!Peek().IsChar(c)) return false;
        Next();
        return true;
    }

    // ----- Expressions -----

    private int InfixBp(LatexToken t)
    {
        if (t.Kind == LatexKind.Char)
        {
            return t.Text switch
            {
                "*" or "/" => Precedence.Multiplicative,
                "+" or "-" => Precedence.Additive,
                "=" or "<" or ">" => Precedence.Relation,
                "|" => _noBar > 0 ? -1 : Precedence.Relation,
                _ => -1,
            };
        }
        if (t.Kind != LatexKind.Command) return -1;
        return t.Text switch
        {
            "cdot" or "times" or "div" or "bmod" or "circ" or "otimes" => Precedence.Multiplicative,
            "pm" => Precedence.Additive,
            "cap" => Precedence.Intersection,
            "cup" or "setminus" => Precedence.Union,
            "mid" => _noBar > 0 ? -1 : Precedence.Relation,
            "land" or "wedge" => Precedence.And,
            "lor" or "vee" or "oplus" => Precedence.Or,
            "implies" or "Rightarrow" => Precedence.Implies,
            "iff" or "Leftrightarrow" => Precedence.Iff,
            "mapsto" => 5,
            _ => RelationCommands.ContainsKey(t.Text) || t.Text == "equiv" ? Precedence.Relation : -1,
        };
    }

    private bool StartsAtom(LatexToken t)
    {
        switch (t.Kind)
        {
            case LatexKind.Number:
                return false;
            case LatexKind.Char:
                if (IsDifferential()) return false;
                return char.IsLetter(t.Text[0]) || char.IsHighSurrogate(t.Text[0]) || t.Text is "(" or "[" ;
            case LatexKind.Command:
                return t.Text is not ("cdot" or "times" or "div" or "bmod" or "pm" or "cap" or "cup" or "setminus" or "land" or "wedge" or "lor" or "vee" or "oplus" or "implies" or "Rightarrow"
                    or "iff" or "Leftrightarrow" or "mapsto" or "mid" or "equiv" or "to" or "right" or "end" or "\\" or "rbrace" or "}" or "rVert" or "rvert" or "rfloor" or "rceil" or "text" or "forall" or "exists" or "neg" or "lnot")
                    && !RelationCommands.ContainsKey(t.Text);
            default:
                return false;
        }
    }

    // Inside an integral, a 'd' followed by a variable ends the integrand.
    private bool IsDifferential()
    {
        if (_integrals == 0 || !Peek().IsChar('d')) return false;
        var next = Peek(1);
        return (next.Kind == LatexKind.Char && char.IsLetter(next.Text[0])) || (next.Kind == LatexKind.Command && Greek.ContainsKey(next.Text));
    }

    private Expr ParseExpr(int minBp)
    {
        if (++_depth > MaxDepth) throw Fail("The expression is nested too deeply.", Peek().Span);
        try
        {
            var left = ParsePrefix();
            while (true)
            {
                var t = Peek();
                if (t.Kind == LatexKind.End) break;
                var bp = InfixBp(t);
                if (bp >= 0)
                {
                    if (bp < minBp) break;
                    left = ParseInfix(left, t);
                    continue;
                }
                if (minBp <= Precedence.Multiplicative && StartsAtom(t))
                {
                    left = new Apply(Operators.Mul, [left, ParseExpr(Precedence.Multiplicative + 1)]);
                    continue;
                }
                break;
            }
            return left;
        }
        finally
        {
            _depth--;
        }
    }

    private Expr ParseInfix(Expr left, LatexToken op)
    {
        Next();
        string key = op.Kind == LatexKind.Char ? op.Text : op.Text;
        switch (key)
        {
            case "*" or "cdot" or "times":
                return new Apply(Operators.Mul, [left, ParseExpr(Precedence.Multiplicative + 1)]);
            case "/" or "div":
                return new Apply(Operators.Div, [left, ParseExpr(Precedence.Multiplicative + 1)]);
            case "bmod":
                return new Apply(Operators.Mod, [left, ParseExpr(Precedence.Multiplicative + 1)]);
            case "circ":
                return new Apply(Operators.Compose, [left, ParseExpr(Precedence.Multiplicative + 1)]);
            case "otimes":
                return new Apply(Operators.Kron, [left, ParseExpr(Precedence.Multiplicative + 1)]);
            case "+":
                return new Apply(Operators.Add, [left, ParseExpr(Precedence.Additive + 1)]);
            case "-":
                return new Apply(Operators.Sub, [left, ParseExpr(Precedence.Additive + 1)]);
            case "pm":
                var right = ParseExpr(Precedence.Additive + 1);
                return new SetLiteral([new Apply(Operators.Add, [left, right]), new Apply(Operators.Sub, [left, right])]);
            case "cap":
                return new Apply(Operators.Intersect, [left, ParseExpr(Precedence.Intersection + 1)]);
            case "cup":
                return new Apply(Operators.Union, [left, ParseExpr(Precedence.Union + 1)]);
            case "setminus":
                return new Apply(Operators.SetMinus, [left, ParseExpr(Precedence.Union + 1)]);
            case "land" or "wedge":
                return new Apply(Operators.And, [left, ParseExpr(Precedence.And + 1)]);
            case "lor" or "vee":
                return new Apply(Operators.Or, [left, ParseExpr(Precedence.Or + 1)]);
            case "oplus":
                return new Apply(Operators.Xor, [left, ParseExpr(Precedence.Or + 1)]);
            case "implies" or "Rightarrow":
                return new Apply(Operators.Implies, [left, ParseExpr(Precedence.Implies)]);
            case "iff" or "Leftrightarrow":
                return new Apply(Operators.Iff, [left, ParseExpr(Precedence.Iff + 1)]);
            case "mapsto":
                if (left is not Symbol s) throw Fail("The left side of '\\mapsto' must be a variable.", op.Span, ["variable"]);
                return new Bind(Binder.Lambda, [s], [], ParseExpr(5));
            default:
                _position--;
                return ParseRelationChain(left);
        }
    }

    private bool IsRelation(LatexToken t) =>
        (t.Kind == LatexKind.Char && (t.Text is "=" or "<" or ">" || (t.Text == "|" && _noBar == 0)))
        || (t.Kind == LatexKind.Command && (RelationCommands.ContainsKey(t.Text) || t.Text == "equiv" || (t.Text == "mid" && _noBar == 0)));

    private Expr ParseRelationChain(Expr first)
    {
        var relations = new List<Expr>();
        var left = first;
        while (IsRelation(Peek()))
        {
            var t = Next();
            var right = ParseExpr(Precedence.Relation + 1);
            if (t.IsCommand("equiv"))
            {
                if (!Peek().IsCommand("pmod")) throw Fail("Expected '\\pmod{n}' after a congruence.", Peek().Span, ["'\\pmod{n}'"]);
                Next();
                relations.Add(new Apply(Operators.Congruent, [left, right, ParseBraced()]));
            }
            else
            {
                var op = t.Kind == LatexKind.Char
                    ? t.Text switch { "=" => Operators.Eq, "<" => Operators.Lt, ">" => Operators.Gt, _ => Operators.Divides }
                    : t.Text == "mid" ? Operators.Divides : RelationCommands[t.Text];
                relations.Add(new Apply(op, [left, right]));
            }
            left = right;
        }
        return relations.Skip(1).Aggregate(relations[0], (all, next) => new Apply(Operators.And, [all, next]));
    }

    // ----- Prefix and atoms -----

    private Expr ParsePrefix()
    {
        var t = Peek();
        if (t.IsChar('-'))
        {
            Next();
            return new Apply(Operators.Neg, [ParseExpr(Precedence.Prefix)]);
        }
        if (t.IsChar('+'))
        {
            Next();
            return ParseExpr(Precedence.Prefix);
        }
        if (t.IsCommand("neg") || t.IsCommand("lnot"))
        {
            Next();
            return new Apply(Operators.Not, [ParseExpr(Precedence.Not + 1)]);
        }
        if (t.IsCommand("forall") || t.IsCommand("exists")) return ParseQuantifier();
        return ParsePostfix(ParseAtom());
    }

    private Expr ParsePostfix(Expr atom)
    {
        while (true)
        {
            var t = Peek();
            if (t.IsChar('^'))
            {
                if (_readingLimit && (Peek(1).IsChar('{') ? Peek(2).IsChar('+') || Peek(2).IsChar('-') : Peek(1).IsChar('+') || Peek(1).IsChar('-')))
                {
                    Next();
                    var braced = AcceptChar('{');
                    _limitDirection = Next().Text == "+" ? 1 : -1;
                    if (braced) ExpectChar('}');
                    continue;
                }
                Next();
                var bracedTranspose = Peek().IsChar('{') && (Peek(1).IsCommand("top") || Peek(1).IsCommand("intercal")) && Peek(2).IsChar('}');
                if (bracedTranspose || Peek().IsCommand("top") || Peek().IsCommand("intercal"))
                {
                    _position += bracedTranspose ? 3 : 1;
                    atom = new Apply(Operators.Transpose, [atom]);
                    continue;
                }
                var exponent = ParseScriptArgument();
                atom = new Apply(Operators.Pow, [atom, exponent]);
            }
            else if (t.IsChar('!'))
            {
                Next();
                atom = AcceptChar('!') ? new Apply(Operators.Factorial2, [atom]) : new Apply(Operators.Factorial, [atom]);
            }
            else if (t.IsChar('\'') && atom is Symbol f)
            {
                var count = 0;
                while (AcceptChar('\'')) count++;
                var symbol = new Symbol(f.Name, Sort.FunctionOf(Sort.Real, Sort.Real));
                Expr derivative = new Apply(Operators.DerivativeOf, [symbol, new Number(count)]);
                atom = Peek().IsChar('(') ? new Apply(Operators.Call, [derivative, .. ParseParenthesizedArguments()]) : derivative;
            }
            else
            {
                return atom;
            }
        }
    }

    // The argument of ^ or _: a braced expression or a single token.
    private Expr ParseScriptArgument()
    {
        if (Peek().IsChar('{')) return ParseBraced();
        var t = Peek();
        if (t.IsChar('-'))
        {
            Next();
            return new Apply(Operators.Neg, [ParseScriptArgument()]);
        }
        if (t.Kind == LatexKind.Number)
        {
            Next();
            return new Number(t.Value);
        }
        return ParseAtom();
    }

    private Expr ParseBraced()
    {
        var open = Peek();
        ExpectChar('{');
        _noBar++;
        try
        {
            var inner = ParseExpr(0);
            ExpectChar('}');
            return inner;
        }
        finally
        {
            _noBar--;
        }
    }

    private ImmutableArray<Expr> ParseParenthesizedArguments()
    {
        ExpectChar('(');
        var args = ImmutableArray.CreateBuilder<Expr>();
        args.Add(ParseExpr(0));
        while (AcceptChar(',')) args.Add(ParseExpr(0));
        ExpectChar(')');
        return args.ToImmutable();
    }

    private Symbol MakeSymbol(string name)
    {
        var sort = _options.Declarations is not null && _options.Declarations.TryGetValue(name, out var declared) ? declared : _options.DefaultSort;
        return new Symbol(name, sort);
    }

    private Expr ParseAtom()
    {
        var t = Next();
        switch (t.Kind)
        {
            case LatexKind.Number:
                return new Number(t.Value, t.Text.Contains('.', StringComparison.Ordinal) ? NumberDisplay.Decimal(t.Text.Length - t.Text.IndexOf('.', StringComparison.Ordinal) - 1) : NumberDisplay.Default);
            case LatexKind.End:
                throw Fail("Expected an expression but the input ended.", t.Span, ["expression"]);
            case LatexKind.Char:
                return ParseCharAtom(t);
            default:
                return ParseCommandAtom(t);
        }
    }

    private Expr ParseCharAtom(LatexToken t)
    {
        switch (t.Text)
        {
            case "(":
                var elements = new List<Expr> { ParseExpr(0) };
                while (AcceptChar(',')) elements.Add(ParseExpr(0));
                if (Peek().IsChar(']') && elements.Count == 2)
                {
                    Next();
                    return new IntervalLiteral(elements[0], elements[1], false, true);
                }
                ExpectChar(')');
                return elements.Count == 1 ? elements[0] : new TupleLiteral([.. elements]);
            case "[":
                var items = new List<Expr> { ParseExpr(0) };
                while (AcceptChar(',')) items.Add(ParseExpr(0));
                if (items.Count != 2 || !(Peek().IsChar(']') || Peek().IsChar(')'))) throw Fail("Expected an interval [a, b] or [a, b).", Peek().Span, ["']'", "')'"]);
                var closed = Next().IsChar(']');
                return new IntervalLiteral(items[0], items[1], true, closed);
            case "|":
                _noBar++;
                try
                {
                    var inner = ParseExpr(0);
                    ExpectChar('|');
                    return new Apply(Operators.Abs, [inner]);
                }
                finally
                {
                    _noBar--;
                }
            default:
                break;
        }
        var c = t.Text[0];
        if (!(char.IsLetter(c) || char.IsHighSurrogate(c))) throw Fail($"Unexpected {Describe(t)}.", t.Span, ["expression"]);
        return LetterSymbol(t.Text);
    }

    private Expr LetterSymbol(string letter)
    {
        if (letter == "e" && !_options.EIsSymbol) return Sym.E;
        if (letter == _options.ImaginaryUnit) return Sym.I;
        var name = letter;
        if (Peek().IsChar('_')) name += "_" + ParseSubscriptText();
        var declared = _options.Declarations is not null && _options.Declarations.TryGetValue(name, out var declaredSort) ? declaredSort : null;
        if (Peek().IsChar('(') && (declared is FunctionSort || (declared is null && _options.FunctionSymbols.Contains(name))))
        {
            var args = ParseParenthesizedArguments();
            var function = new Symbol(name, declared ?? Sort.FunctionOf(args.Length == 1 ? Sort.Real : Sort.TupleOf([.. Enumerable.Repeat(Sort.Real, args.Length)]), Sort.Real));
            return new Apply(Operators.Call, [function, .. args]);
        }
        return MakeSymbol(name);
    }

    // x_1, x_{12}, a_{ij}: the subscript text is kept in the symbol name.
    private string ParseSubscriptText()
    {
        Next();
        string text;
        TextSpan span;
        if (Peek().IsChar('{'))
        {
            var open = Next();
            var sb = new System.Text.StringBuilder();
            while (!Peek().IsChar('}'))
            {
                var t = Next();
                if (t.Kind == LatexKind.End) throw Fail("Unterminated subscript.", t.Span, ["'}'"]);
                sb.Append(t.Kind == LatexKind.Command && Greek.TryGetValue(t.Text, out var g) ? g : t.Text);
            }
            var close = Next();
            text = sb.ToString();
            span = new TextSpan(open.Span.Start, close.Span.End - open.Span.Start);
        }
        else
        {
            var single = Next();
            if (single.Kind == LatexKind.End) throw Fail("Expected a subscript.", single.Span, ["subscript"]);
            text = single.Text;
            span = single.Span;
        }

        // The subscript becomes part of the symbol name, which allows only letters, digits, '_' and primes.
        if (!Symbol.IsValidName("_" + text)) throw Fail($"The subscript '{text}' cannot be part of a variable name: use letters, digits and '_'.", span);
        return text;
    }

    private Expr ParseCommandAtom(LatexToken t)
    {
        var name = t.Text;
        switch (name)
        {
            case "frac" or "dfrac" or "tfrac":
                var leibniz = TryParseLeibnizFraction();
                if (leibniz is not null) return leibniz;
                var numerator = ParseBraced();
                return new Apply(Operators.Div, [numerator, ParseBraced()]);
            case "sqrt":
                if (Peek().IsChar('['))
                {
                    Next();
                    var degree = ParseExpr(0);
                    ExpectChar(']');
                    return new Apply(Operators.Root, [ParseBraced(), degree]);
                }
                return new Apply(Operators.Sqrt, [ParseBraced()]);
            case "binom":
                var n = ParseBraced();
                return new Apply(Operators.Binomial, [n, ParseBraced()]);
            case "overline":
                return new Apply(Operators.Conj, [ParseBraced()]);
            case "vec" or "mathbf" or "boldsymbol" or "hat" or "bar":
                return ParseBraced();
            case "left":
                return ParseLeftRight();
            case "lvert":
                return ParseDelimited(Operators.Abs, "rvert");
            case "lVert" or "|":
                return ParseDelimited(Operators.Norm, name == "|" ? "|" : "rVert");
            case "lfloor":
                return ParseDelimited(Operators.Floor, "rfloor");
            case "lceil":
                return ParseDelimited(Operators.Ceil, "rceil");
            case "{" or "lbrace":
                return ParseSet();
            case "infty":
                return Sym.Infinity;
            case "pi":
                return Sym.Pi;
            case "emptyset" or "varnothing":
                return Sym.EmptySet;
            case "mathbb":
                return ParseBlackboard(t);
            case "operatorname" or "mathrm" or "text":
                return ParseNamedFunction(t);
            case "sum" or "prod" or "bigcup" or "bigcap":
                return ParseBigOperator(t);
            case "int":
                return ParseIntegral();
            case "lim":
                return ParseLimit();
            case "begin":
                return ParseEnvironment(t);
            case "pmod":
                throw Fail("'\\pmod' may only follow a congruence.", t.Span);
        }
        if (Greek.TryGetValue(name, out var greek))
        {
            var symbolName = greek;
            if (Peek().IsChar('_')) symbolName += "_" + ParseSubscriptText();
            return MakeSymbol(symbolName);
        }
        if (FunctionMacros.TryGetValue(name, out var function)) return ParseFunction(t, function);
        throw Fail($"Unsupported LaTeX command '\\{name}'.", t.Span);
    }

    private Constant ParseBlackboard(LatexToken command)
    {
        var inner = ParseBraced();
        var set = inner is Symbol s ? s.Name : string.Empty;
        return set switch
        {
            "N" => new Constant(ConstantId.Naturals),
            "Z" => new Constant(ConstantId.Integers),
            "Q" => new Constant(ConstantId.Rationals),
            "R" => new Constant(ConstantId.Reals),
            "C" => new Constant(ConstantId.Complexes),
            _ => throw Fail("\\mathbb supports N, Z, Q, R and C.", command.Span, ["N", "Z", "Q", "R", "C"]),
        };
    }

    private Expr ParseLeftRight()
    {
        var open = Next();
        string opening = open.Kind == LatexKind.Command ? "\\" + open.Text : open.Text;
        switch (opening)
        {
            case "(":
                var elements = new List<Expr> { ParseExpr(0) };
                while (AcceptChar(',')) elements.Add(ParseExpr(0));
                ExpectCommand("right");
                var close = Next();
                if (close.IsChar(']') && elements.Count == 2) return new IntervalLiteral(elements[0], elements[1], false, true);
                if (!close.IsChar(')')) throw Fail("Expected ')' after \\right.", close.Span, ["')'"]);
                return elements.Count == 1 ? elements[0] : new TupleLiteral([.. elements]);
            case "[":
                var items = new List<Expr> { ParseExpr(0) };
                while (AcceptChar(',')) items.Add(ParseExpr(0));
                ExpectCommand("right");
                var closer = Next();
                if (items.Count != 2 || !(closer.IsChar(']') || closer.IsChar(')'))) throw Fail("Expected an interval [a, b] or [a, b).", closer.Span, ["']'", "')'"]);
                return new IntervalLiteral(items[0], items[1], true, closer.IsChar(']'));
            case "|" or "\\vert" or "\\lvert":
                return FinishDelimited(Operators.Abs);
            case "\\|" or "\\Vert" or "\\lVert":
                return FinishDelimited(Operators.Norm);
            case "\\lfloor":
                return FinishDelimited(Operators.Floor);
            case "\\lceil":
                return FinishDelimited(Operators.Ceil);
            case "\\{":
                var set = ParseSetBody();
                ExpectCommand("right");
                var brace = Next();
                if (!(brace.IsCommand("}") || brace.IsCommand("rbrace"))) throw Fail("Expected '\\}' after \\right.", brace.Span, ["'\\}'"]);
                return set;
            default:
                throw Fail($"Unsupported delimiter after \\left: '{opening}'.", open.Span);
        }
    }

    private Apply FinishDelimited(Operator op)
    {
        _noBar++;
        try
        {
            var inner = ParseExpr(0);
            ExpectCommand("right");
            Next();
            return new Apply(op, [inner]);
        }
        finally
        {
            _noBar--;
        }
    }

    private Apply ParseDelimited(Operator op, string closer)
    {
        _noBar++;
        try
        {
            var inner = ParseExpr(0);
            var t = Peek();
            if (!(t.IsCommand(closer) || (closer == "|" && t.IsChar('|')))) throw Fail($"Expected '\\{closer}' but found {Describe(t)}.", t.Span, [$"'\\{closer}'"]);
            Next();
            return new Apply(op, [inner]);
        }
        finally
        {
            _noBar--;
        }
    }

    private Expr ParseSet()
    {
        var set = ParseSetBody();
        var t = Next();
        if (!(t.IsCommand("}") || t.IsCommand("rbrace"))) throw Fail("Expected '\\}' to close the set.", t.Span, ["'\\}'"]);
        return set;
    }

    // After \{ : { a, b } or { x \in S \mid P } or { f \mid k \in S }; leaves the closing brace unread.
    private Expr ParseSetBody()
    {
        if (Peek().IsCommand("}") || Peek().IsCommand("rbrace")) return new SetLiteral([]);
        _noBar++;
        Expr first;
        try
        {
            first = ParseExpr(0);
        }
        finally
        {
            _noBar--;
        }
        if (Peek().IsCommand("mid") || Peek().IsChar('|') || Peek().IsChar(':'))
        {
            var separator = Next();
            var rest = ParseExpr(0);
            if (first is Apply { Operator.Id: "element" } builder && builder.Arguments[0] is Symbol x) return new Bind(Binder.SetBuilder, [x], [builder.Arguments[1]], rest);
            if (rest is Apply { Operator.Id: "element" } image && image.Arguments[0] is Symbol k) return new Bind(Binder.ImageSet, [k], [image.Arguments[1]], first);
            throw Fail("A set needs the form \\{x \\in S \\mid P\\} or \\{f \\mid k \\in S\\}.", separator.Span);
        }
        var elements = new List<Expr> { first };
        while (AcceptChar(',')) elements.Add(ParseExpr(0));
        return new SetLiteral([.. elements]);
    }

    private string ReadBracedName(LatexToken command)
    {
        ExpectChar('{');
        var sb = new System.Text.StringBuilder();
        while (!Peek().IsChar('}'))
        {
            var t = Next();
            if (t.Kind == LatexKind.End) throw Fail($"Unterminated argument of \\{command.Text}.", t.Span, ["'}'"]);
            sb.Append(t.Kind == LatexKind.Command ? t.Text : t.Text);
        }
        Next();
        return sb.ToString();
    }

    // \frac{d}{dx} f and \frac{d^{n}}{dx^{n}} f: the fraction is a derivative when its parts have exactly this token shape.
    private Apply? TryParseLeibnizFraction()
    {
        var offset = 0;

        bool Order(out BigRational order)
        {
            order = BigRational.One;
            if (!Peek(offset).IsChar('^')) return true;
            if (Peek(offset + 1).Kind == LatexKind.Number) { order = Peek(offset + 1).Value; offset += 2; return true; }
            if (!Peek(offset + 1).IsChar('{') || Peek(offset + 2).Kind != LatexKind.Number || !Peek(offset + 3).IsChar('}')) return false;
            order = Peek(offset + 2).Value;
            offset += 4;
            return true;
        }

        if (!Peek(offset).IsChar('{') || !Peek(offset + 1).IsChar('d')) return null;
        offset += 2;
        if (!Order(out var order) || !Peek(offset).IsChar('}') || !Peek(offset + 1).IsChar('{') || !Peek(offset + 2).IsChar('d')) return null;
        var variable = Peek(offset + 3);
        if (!(variable.Kind == LatexKind.Char && char.IsLetter(variable.Text[0])) && !(variable.Kind == LatexKind.Command && Greek.ContainsKey(variable.Text))) return null;
        offset += 4;
        if (!Order(out var denominatorOrder) || denominatorOrder != order || !Peek(offset).IsChar('}')) return null;
        _position += offset + 1;

        var x = variable.Kind == LatexKind.Command ? MakeSymbol(Greek[variable.Text]) : MakeSymbol(variable.Text);
        var operand = ParseExpr(Precedence.Additive + 1);
        return order == BigRational.One ? new Apply(Operators.Diff, [operand, x]) : new Apply(Operators.Diff, [operand, x, new Number(order)]);
    }

    private Expr ParseNamedFunction(LatexToken command)
    {
        var name = ReadBracedName(command);
        if (command.Text == "text") throw Fail("\\text is only supported inside cases.", command.Span);
        if (command.Text == "mathrm" && name == "i") return Sym.I;
        if (FunctionMacros.TryGetValue(name, out var known)) return ParseFunction(command, known);
        if (Operators.TryGetByName(name, out _)) return ParseFunction(command, name);
        if (!Symbol.IsValidName(name)) throw Fail($"'{name}' is not a valid name: use letters, digits, '_' and primes, starting with a letter or '_'.", command.Span);
        if (Peek().IsChar('('))
        {
            var args = ParseParenthesizedArguments();
            var function = new Symbol(name, Sort.FunctionOf(args.Length == 1 ? Sort.Real : Sort.TupleOf([.. Enumerable.Repeat(Sort.Real, args.Length)]), Sort.Real));
            return new Apply(Operators.Call, [function, .. args]);
        }
        return MakeSymbol(name);
    }

    private Expr ParseFunction(LatexToken command, string name)
    {
        Expr? exponent = null;
        Expr? logBase = null;
        var inverse = false;
        while (Peek().IsChar('^') || Peek().IsChar('_'))
        {
            if (Peek().IsChar('_'))
            {
                Next();
                logBase = ParseScriptArgument();
            }
            else
            {
                Next();
                var negative = Peek().IsChar('{') && Peek(1).IsChar('-') && Peek(2).Kind == LatexKind.Number && Peek(2).Value == BigRational.One && Peek(3).IsChar('}');
                if (negative && InverseNames.ContainsKey(name))
                {
                    _position += 4;
                    inverse = true;
                }
                else
                {
                    exponent = ParseScriptArgument();
                }
            }
        }

        var effective = inverse ? InverseNames[name] : name;
        if (!Operators.TryGetByName(effective, out var op)) throw Fail($"Unsupported function '{name}'.", command.Span);

        ImmutableArray<Expr> args;
        if (Peek().IsChar('(')) args = ParseParenthesizedArguments();
        else if (Peek().IsCommand("left") && (Peek(1).IsChar('(') )) args = ParseLeftParenthesizedArguments();
        else args = [ParseTightArgument(command, name)];

        Expr call;
        if (op == Operators.Log)
        {
            call = args.Length == 1 ? (logBase is not null ? new Apply(op, [args[0], logBase]) : _options.LogMeansNatural ? new Apply(Operators.Ln, args) : new Apply(op, [args[0], new Number(10)])) : new Apply(op, args);
        }
        else
        {
            if (!op.Arity.Accepts(args.Length)) throw Fail($"'{name}' takes {op.Arity} argument(s) but {args.Length} were given.", command.Span);
            call = new Apply(op, args);
        }
        return exponent is null ? call : new Apply(Operators.Pow, [call, exponent]);
    }

    private ImmutableArray<Expr> ParseLeftParenthesizedArguments()
    {
        Next();
        Next();
        var args = ImmutableArray.CreateBuilder<Expr>();
        args.Add(ParseExpr(0));
        while (AcceptChar(',')) args.Add(ParseExpr(0));
        ExpectCommand("right");
        var close = Next();
        if (!close.IsChar(')')) throw Fail("Expected ')' after \\right.", close.Span, ["')'"]);
        return args.ToImmutable();
    }

    // \sin x, \sin 2x: a product of atoms, stopping at operators and at the next function.
    private Expr ParseTightArgument(LatexToken command, string name)
    {
        var t = Peek();
        if (t.Kind == LatexKind.End || !(StartsAtom(t) || t.Kind == LatexKind.Number || t.IsChar('-') || t.IsChar('|') || t.IsCommand("lvert") || t.IsCommand("lVert")))
        {
            throw Fail($"The function '{name}' needs an argument.", t.Span, ["expression"]);
        }
        var first = ParseExpr(Precedence.Power);
        while (true)
        {
            var next = Peek();
            if (next.Kind == LatexKind.Command ? !FunctionMacros.ContainsKey(next.Text) && StartsAtom(next) && next.Text is not ("sum" or "prod" or "int" or "lim" or "operatorname") : next.Kind == LatexKind.Char && StartsAtom(next))
            {
                first = new Apply(Operators.Mul, [first, ParseExpr(Precedence.Power)]);
                continue;
            }
            return first;
        }
    }

    // ----- Big operators, integrals, limits -----

    private (Symbol Variable, Expr Lower, Expr Upper) ReadSumLimits(LatexToken op)
    {
        if (!Peek().IsChar('_')) throw Fail($"'\\{op.Text}' needs limits such as _{{k=1}}^{{n}}.", Peek().Span, ["'_'"]);
        Next();
        ExpectChar('{');
        var v = ParseAtom();
        if (v is not Symbol variable) throw Fail($"'\\{op.Text}' needs an index variable.", op.Span, ["variable"]);
        ExpectChar('=');
        var lower = ParseExpr(0);
        ExpectChar('}');
        if (!Peek().IsChar('^')) throw Fail($"'\\{op.Text}' needs an upper limit.", Peek().Span, ["'^'"]);
        Next();
        return (variable, lower, ParseScriptArgument());
    }

    private Bind ParseBigOperator(LatexToken op)
    {
        var (variable, lower, upper) = ReadSumLimits(op);
        var body = ParseExpr(Precedence.Multiplicative);
        var binder = op.Text switch { "sum" => Binder.Sum, "prod" => Binder.Product, "bigcup" => Binder.IndexedUnion, _ => Binder.IndexedIntersection };
        return new Bind(binder, [variable], [lower, upper], body);
    }

    private Expr ParseIntegral()
    {
        Expr? lower = null;
        Expr? upper = null;
        if (Peek().IsChar('_'))
        {
            Next();
            lower = ParseScriptArgument();
        }
        if (Peek().IsChar('^'))
        {
            Next();
            upper = ParseScriptArgument();
        }
        _integrals++;
        Expr integrand;
        try
        {
            integrand = ParseExpr(0);
        }
        finally
        {
            _integrals--;
        }
        if (!Peek().IsChar('d')) throw Fail("Expected a differential such as \\,dx after the integrand.", Peek().Span, ["'dx'"]);
        Next();
        var variableToken = Peek();
        var variable = ParseAtom();
        if (variable is not Symbol x) throw Fail("Expected the integration variable after 'd'.", variableToken.Span, ["variable"]);
        if (lower is null && upper is null) return new Apply(Operators.Integrate, [integrand, x]);
        if (lower is null || upper is null) throw Fail("A definite integral needs both limits.", variableToken.Span);
        return new Bind(Binder.Integral, [x], [lower, upper], integrand);
    }

    private Bind ParseLimit()
    {
        if (!Peek().IsChar('_')) throw Fail("'\\lim' needs a subscript such as _{x \\to a}.", Peek().Span, ["'_'"]);
        Next();
        ExpectChar('{');
        var v = ParseAtom();
        if (v is not Symbol variable) throw Fail("'\\lim' needs a variable.", Peek().Span, ["variable"]);
        ExpectCommand("to");
        var savedDirection = _limitDirection;
        var savedReading = _readingLimit;
        _limitDirection = null;
        _readingLimit = true;
        Expr point;
        int? direction;
        try
        {
            point = ParseExpr(0);
            direction = _limitDirection;
        }
        finally
        {
            _readingLimit = savedReading;
            _limitDirection = savedDirection;
        }
        ExpectChar('}');
        var body = ParseExpr(Precedence.Multiplicative);
        ImmutableArray<Expr> data = direction is { } d ? [point, new Number(d)] : [point];
        return new Bind(Binder.Limit, [variable], data, body);
    }

    private Bind ParseQuantifier()
    {
        var keyword = Next();
        var binder = keyword.Text == "forall" ? Binder.ForAll : Binder.Exists;
        if (binder == Binder.Exists && AcceptChar('!')) binder = Binder.ExistsUnique;
        var bound = new List<Symbol>();
        do
        {
            var atom = ParseAtom();
            if (atom is not Symbol s) throw Fail($"Expected a variable after '\\{keyword.Text}'.", keyword.Span, ["variable"]);
            bound.Add(s);
        }
        while (AcceptChar(','));
        ImmutableArray<Expr> data = [];
        if (Peek().IsCommand("in"))
        {
            Next();
            data = [ParseExpr(Precedence.Union)];
        }
        if (!(AcceptChar(':') || AcceptChar(','))) throw Fail("Expected ':' before the body of the quantifier.", Peek().Span, ["':'"]);
        return new Bind(binder, [.. bound], data, ParseExpr(Precedence.Quantifier));
    }

    // ----- Environments -----

    private Expr ParseEnvironment(LatexToken begin)
    {
        var name = ReadBracedName(begin);
        switch (name)
        {
            case "pmatrix" or "bmatrix" or "vmatrix" or "matrix" or "Bmatrix" or "Vmatrix":
                var rows = new List<List<Expr>>();
                var row = new List<Expr>();
                while (true)
                {
                    row.Add(ParseExpr(0));
                    if (AcceptChar('&')) continue;
                    if (Peek().IsCommand("\\"))
                    {
                        Next();
                        rows.Add(row);
                        row = [];
                        if (Peek().IsCommand("end")) break;
                        continue;
                    }
                    rows.Add(row);
                    break;
                }
                ExpectCommand("end");
                var closing = ReadBracedName(begin);
                if (closing != name) throw Fail($"\\begin{{{name}}} is closed by \\end{{{closing}}}.", begin.Span);
                if (rows.Count == 0 || rows.Any(r => r.Count != rows[0].Count)) throw Fail("Matrix rows must have equal length.", begin.Span);
                var matrix = new MatrixLiteral(rows.Count, rows[0].Count, [.. rows.SelectMany(r => r)]);
                return name is "vmatrix" or "Vmatrix" ? new Apply(Operators.Det, [matrix]) : matrix;
            case "cases":
                return ParseCases(begin);
            default:
                throw Fail($"Unsupported environment '{name}'.", begin.Span);
        }
    }

    private Piecewise ParseCases(LatexToken begin)
    {
        var cases = ImmutableArray.CreateBuilder<(Expr, Expr)>();
        while (true)
        {
            var value = ParseExpr(0);
            Expr condition = Sym.True;
            if (AcceptChar('&'))
            {
                if (Peek().IsCommand("text"))
                {
                    var word = ReadBracedName(Next());
                    if (word.Trim() == "otherwise") condition = Sym.True;
                    else if (word.Trim() == "if") condition = ParseExpr(0);
                    else throw Fail("Expected '\\text{if}' or '\\text{otherwise}'.", Peek().Span, ["'\\text{if}'", "'\\text{otherwise}'"]);
                }
                else
                {
                    condition = ParseExpr(0);
                }
            }
            cases.Add((value, condition));
            if (Peek().IsCommand("\\"))
            {
                Next();
                if (Peek().IsCommand("end")) break;
                continue;
            }
            break;
        }
        ExpectCommand("end");
        var closing = ReadBracedName(begin);
        if (closing != "cases") throw Fail($"\\begin{{cases}} is closed by \\end{{{closing}}}.", begin.Span);
        return new Piecewise(cases.ToImmutable());
    }
}
