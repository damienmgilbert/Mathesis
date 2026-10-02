using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Parsing;

/// <summary>
/// The linear-text parser: a Pratt parser for the notation of docs/design/05-syntax-trees-and-notation.md ("Input notation"),
/// producing <b>Raw</b> trees that keep the shape of the input (<c>a - b</c> is <c>sub</c>, <c>-x</c> is <c>neg</c>, nested sums stay
/// nested). Errors carry the source span, the expected tokens and, where possible, a suggestion; parsing stops at the first error
/// and never throws.
/// </summary>
/// <remarks>
/// Conventions beyond the design table: <c>|</c> opens absolute-value bars where an operand is expected and means divides
/// after an operand; <c>!</c> before an operand is logical not (use <c>subfactorial(n)</c>); <c>(a, b)</c> is a tuple, while
/// intervals are <c>[a, b]</c>, <c>[a, b)</c>, <c>(a, b]</c> and <c>]a, b[</c>; a flat <c>[a, b, c]</c> is a column vector.
/// <c>f(x)</c> is an application when <c>f</c> is a declared function, one of <see cref="ParserOptions.FunctionSymbols"/>, or a
/// word that does not split into single letters; otherwise <c>x(x + 1)</c> is a product. Identifiers directly followed by
/// <c>(</c> and a single trailing letter such as <c>y(</c> stay products.
/// </remarks>
public static class Parser
{
    /// <summary>Parses <paramref name="text"/> into a Raw tree.</summary>
    /// <param name="text">The expression text.</param>
    /// <param name="options">Parser options; <c>null</c> for the defaults.</param>
    public static ParseResult Parse(string text, ParserOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ParserEngine(text, options ?? new ParserOptions()).Run();
    }
}

internal sealed class ParserEngine
{
    private const int MaxDepth = 150;

    private static readonly HashSet<string> Keywords = ["in", "notin", "and", "or", "xor", "not", "mod"];

    private static readonly Dictionary<string, string> Greek = new(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η",
        ["theta"] = "θ", ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ",
        ["omicron"] = "ο", ["rho"] = "ρ", ["sigma"] = "σ", ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "φ", ["chi"] = "χ",
        ["psi"] = "ψ", ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ",
        ["Pi"] = "Π", ["Sigma"] = "Σ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
    };

    // Names that are both function names and Greek letters: functions only when applied with parentheses.
    private static readonly HashSet<string> GreekFunctionNames = ["gamma", "beta", "zeta"];

    private static readonly Dictionary<string, ConstantId> ConstantWords = new(StringComparer.Ordinal)
    {
        ["pi"] = ConstantId.Pi, ["π"] = ConstantId.Pi, ["GoldenRatio"] = ConstantId.GoldenRatio, ["EulerGamma"] = ConstantId.EulerGamma,
        ["CatalanG"] = ConstantId.CatalanG, ["oo"] = ConstantId.PositiveInfinity, ["inf"] = ConstantId.PositiveInfinity,
        ["∞"] = ConstantId.PositiveInfinity, ["zoo"] = ConstantId.ComplexInfinity, ["undefined"] = ConstantId.Undefined,
        ["true"] = ConstantId.True, ["false"] = ConstantId.False, ["EmptySet"] = ConstantId.EmptySet, ["∅"] = ConstantId.EmptySet,
        ["ⅈ"] = ConstantId.ImaginaryUnit, ["ℕ"] = ConstantId.Naturals, ["ℤ"] = ConstantId.Integers, ["ℚ"] = ConstantId.Rationals,
        ["ℝ"] = ConstantId.Reals, ["ℂ"] = ConstantId.Complexes,
    };

    private static readonly Dictionary<string, ConstantId> SetLetters = new(StringComparer.Ordinal)
    {
        ["N"] = ConstantId.Naturals, ["Z"] = ConstantId.Integers, ["Q"] = ConstantId.Rationals, ["R"] = ConstantId.Reals, ["C"] = ConstantId.Complexes,
    };

    // Names whose meaning depends on the number of arguments, or that are abbreviations of a longer operator id.
    private static readonly Dictionary<string, (int Arity, string Id)[]> Overloads = new(StringComparer.Ordinal)
    {
        ["P"] = [(1, "prob"), (2, "perm")], ["C"] = [(2, "binomial")], ["nCr"] = [(2, "binomial")], ["nPr"] = [(2, "perm")],
        ["δ"] = [(1, "dirac"), (2, "kronecker")], ["I"] = [(1, "identity")], ["F"] = [(1, "fibonacci")], ["H"] = [(1, "harmonic")],
        ["E"] = [(1, "expect")], ["W"] = [(1, "lambertw")], ["u"] = [(1, "heaviside")], ["ε"] = [(3, "leviCivita"), (2, "leviCivita")],
        ["φ"] = [(1, "totient")], ["μ"] = [(1, "mobius")], ["σ"] = [(2, "divisorSigma")], ["Var"] = [(1, "var")], ["Cov"] = [(2, "cov")],
        ["Corr"] = [(2, "corr")], ["Pr"] = [(1, "prob")], ["tr"] = [(1, "trace")], ["𝒫"] = [(1, "powerset")], ["N"] = [(1, "N"), (2, "N")],
    };

    // The inverse function written f^-1 for trigonometric and hyperbolic functions.
    private static readonly Dictionary<string, string> InverseNames = new(StringComparer.Ordinal)
    {
        ["sin"] = "arcsin", ["cos"] = "arccos", ["tan"] = "arctan", ["cot"] = "arccot", ["sec"] = "arcsec", ["csc"] = "arccsc",
        ["sinh"] = "arsinh", ["cosh"] = "arcosh", ["tanh"] = "artanh", ["coth"] = "arcoth", ["sech"] = "arsech", ["csch"] = "arcsch",
    };

    private static readonly HashSet<string> BinderNames = ["sum", "product", "integrate", "limit", "Union", "Intersection", "laplace", "fourier", "argmin", "argmax", "Piecewise"];

    private static readonly HashSet<string> RelationOperators = ["=", "!=", "<", "<=", ">", ">=", "~=", "in", "notin", "~", "∣", "⊂", "⊆", "∝", "⊥", "∥", "≡"];

    private readonly string _text;
    private readonly ParserOptions _options;
    private readonly List<ParseWarning> _warnings = [];
    private readonly HashSet<string> _splittable;
    private readonly HashSet<string> _functionSymbols;
    private ImmutableArray<Token> _tokens;
    private int _position;
    private int _noBar;
    private int _depth;
    private int _normDepth;
    private bool _stringsAllowed;

    private sealed class ParseFailure(ParseError error) : Exception
    {
        public ParseError Error { get; } = error;
    }

    public ParserEngine(string text, ParserOptions options)
    {
        _text = text;
        _options = options;
        _functionSymbols = [.. options.FunctionSymbols];
        _splittable = BuildSplittableNames(options);
    }

    private static HashSet<string> BuildSplittableNames(ParserOptions options)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in Operators.FunctionNames) if (n.Length >= 2) known.Add(n);
        foreach (var n in ConstantWords.Keys) if (n.Length >= 2) known.Add(n);
        foreach (var n in Greek.Keys) known.Add(n);
        foreach (var n in BinderNames) known.Add(n);
        known.Add("forall");
        known.Add("exists");
        if (options.Declarations is not null) foreach (var n in options.Declarations.Keys) if (n.Length >= 2) known.Add(n);
        foreach (var k in Keywords) known.Remove(k);
        return known;
    }

    // ----- Entry point -----

    public ParseResult Run()
    {
        try
        {
            var (tokens, error) = Lexer.Tokenize(_text);
            if (error is not null) return Failure(error);
            _tokens = Expand(tokens);
            if (_tokens[0].Kind == TokenKind.End) throw Fail("Expected an expression.", _tokens[0].Span, ["expression"]);
            var expr = ParseExpr(0);
            if (Peek().Kind != TokenKind.End)
            {
                var t = Peek();
                throw Fail($"Unexpected {Describe(t)}.", t.Span, ["operator", "end of input"]);
            }
            return new ParseResult(expr, [], [.. _warnings]);
        }
        catch (ParseFailure f)
        {
            return Failure(f.Error);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or FormatException)
        {
            // Defensive: constructing a node rejected something the grammar let through; report it as a syntax error.
            var span = _tokens.IsDefault ? new TextSpan(0, 0) : Peek().Span;
            return Failure(new ParseError(ex.Message, span, []));
        }
    }

    private ParseResult Failure(ParseError error) => new(null, [error], [.. _warnings]);

    // ----- Token stream -----

    private Token Peek(int offset = 0) => _tokens[Math.Min(_position + offset, _tokens.Length - 1)];

    private Token Next()
    {
        var t = _tokens[_position];
        if (t.Kind != TokenKind.End) _position++;
        return t;
    }

    private static string Describe(Token t) => t.Kind switch
    {
        TokenKind.End => "end of input",
        TokenKind.Number => $"number '{t.Text}'",
        TokenKind.String => $"string \"{t.Text}\"",
        _ => $"'{t.Text}'",
    };

    private static ParseFailure Fail(string message, TextSpan span, ImmutableArray<string> expected, string? suggestion = null) =>
        new(new ParseError(message, span, expected, suggestion));

    private void Expect(string op)
    {
        var t = Peek();
        if (!t.Is(op)) throw Fail(t.Kind == TokenKind.End ? $"Expected '{op}' but the input ended." : $"Expected '{op}' but found {Describe(t)}.", t.Span, [$"'{op}'"], op is ")" or "]" or "}" ? $"Add a closing '{op}'." : null);
        Next();
    }

    private bool Accept(string op)
    {
        if (!Peek().Is(op)) return false;
        Next();
        return true;
    }

    private bool IsAdjacent(int offset) => !Peek(offset).SpaceBefore;

    // ----- Identifier splitting (SingleLetterVariables) -----

    private ImmutableArray<Token> Expand(ImmutableArray<Token> tokens)
    {
        var result = ImmutableArray.CreateBuilder<Token>();
        for (var i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t.Kind == TokenKind.Identifier && Keywords.Contains(t.Text))
            {
                result.Add(t with { Kind = TokenKind.Operator });
                continue;
            }
            if (t.Kind != TokenKind.Identifier || !_options.SingleLetterVariables || !CanSplit(tokens, i))
            {
                result.Add(t);
                continue;
            }
            var pieces = Split(t.Text);
            if (pieces.Count <= 1)
            {
                result.Add(t);
                continue;
            }
            var position = t.Start;
            for (var p = 0; p < pieces.Count; p++)
            {
                result.Add(new Token(TokenKind.Identifier, pieces[p], position, position + pieces[p].Length, p == 0 && t.SpaceBefore));
                position += pieces[p].Length;
            }
        }
        return result.ToImmutable();
    }

    private bool CanSplit(ImmutableArray<Token> tokens, int i)
    {
        var name = tokens[i].Text;
        if (name.Length < 2 || !name.All(c => char.IsLetter(c) || char.IsSurrogate(c))) return false;
        if (IsKnownName(name)) return false;

        var next = i + 1 < tokens.Length ? tokens[i + 1] : default;

        // A word directly followed by '(' is a function symbol, like speed(t), unless it contains a known name (xsin(y)).
        if (next.Is("(") && !next.SpaceBefore && Split(name).All(p => p.Length <= 1 || (p.Length == 2 && char.IsHighSurrogate(p[0])))) return false;

        // Leibniz notation: dy/dx, d/dx and d^2y/dx^2 keep their differentials whole.
        if ((name == "d" || IsDifferentialName(name)) && next.Is("/") && i + 2 < tokens.Length && tokens[i + 2].Kind == TokenKind.Identifier && IsDifferentialName(tokens[i + 2].Text)) return false;
        if (IsDifferentialName(name) && i >= 2 && tokens[i - 1].Is("/") && IsLeibnizNumerator(tokens, i - 2)) return false;
        return true;
    }

    // d followed by one variable: dx, dy, dt; with single-letter variables the variable must be a single letter (or contain a digit or underscore).
    private bool IsDifferentialName(string name) => name.Length >= 2 && name[0] == 'd' && IsAtomicSymbolWord(name[1..]);

    // A word that stays one symbol however it is spelled back: a single letter, a name with digits or underscores, or any word when
    // single-letter splitting is off.
    private bool IsAtomicSymbolWord(string word)
    {
        if (!Symbol.IsValidName(word)) return false;
        if (!_options.SingleLetterVariables) return true;
        return word.Length == 1 || (word.Length == 2 && char.IsHighSurrogate(word[0])) || word.Any(c => char.IsDigit(c) || c == '_');
    }

    // The numerator before the '/' of a derivative: d, dy, or d^n y (ending at index end).
    private bool IsLeibnizNumerator(ImmutableArray<Token> tokens, int end)
    {
        // d, dy, or the variable y of d^n y; the exponent n of d^n.
        if (tokens[end].Kind == TokenKind.Number) return end >= 2 && tokens[end - 1].Is("^") && tokens[end - 2].IsWord("d");
        if (tokens[end].Kind != TokenKind.Identifier) return false;
        if (tokens[end].IsWord("d") || IsDifferentialName(tokens[end].Text)) return true;
        return end >= 3 && tokens[end - 1].Kind == TokenKind.Number && tokens[end - 2].Is("^") && tokens[end - 3].IsWord("d") && IsAtomicSymbolWord(tokens[end].Text);
    }

    private bool IsKnownName(string name) =>
        _splittable.Contains(name) || Operators.TryGetByName(name, out _) || Overloads.ContainsKey(name) || _options.Declarations?.ContainsKey(name) == true;

    private List<string> Split(string name)
    {
        var pieces = new List<string>();
        var i = 0;
        while (i < name.Length)
        {
            var best = 0;
            for (var length = name.Length - i; length >= 2; length--)
            {
                if (_splittable.Contains(name.Substring(i, length)))
                {
                    best = length;
                    break;
                }
            }
            if (best == 0) best = char.IsHighSurrogate(name[i]) && i + 1 < name.Length ? 2 : 1;
            pieces.Add(name.Substring(i, best));
            i += best;
        }
        return pieces;
    }

    // ----- Expressions -----

    private Expr ParseExpr(int minBp)
    {
        if (++_depth > MaxDepth) throw Fail("The expression is nested too deeply.", Peek().Span, []);
        try
        {
            var left = ParsePrefix();
            var lastWasDivision = false;
            while (true)
            {
                var t = Peek();
                if (t.Kind == TokenKind.End) break;

                var bp = InfixBp(t);
                if (bp >= 0)
                {
                    if (bp < minBp) break;
                    left = ParseInfix(left, t, ref lastWasDivision);
                    continue;
                }

                if (minBp <= Precedence.Multiplicative && StartsPrimary(t))
                {
                    if (lastWasDivision)
                    {
                        _warnings.Add(new ParseWarning("AmbiguousImplicitMultiplication", "In 'a/bc' the product bc is the divisor only if parenthesized; this is read as (a/b)·c. Write a/(b c) for the other meaning.", t.Span));
                    }
                    var right = ParseExpr(Precedence.Multiplicative + 1);
                    left = new Apply(Operators.Mul, [left, right]);
                    lastWasDivision = false;
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

    private int InfixBp(Token t)
    {
        if (t.Kind != TokenKind.Operator) return -1;
        switch (t.Text)
        {
            case "^": return Precedence.Power;
            case "*" or "/" or "mod" or "∘" or "⊗": return Precedence.Multiplicative;
            case "+" or "-": return Precedence.Additive;
            case "∩": return Precedence.Intersection;
            case "∪" or "∖" or "△": return Precedence.Union;
            case "|": return _noBar > 0 ? -1 : IsDoubleBar(t) ? Precedence.Or : Precedence.Relation;
            case "and": return Precedence.And;
            case "or" or "xor": return Precedence.Or;
            case "=>": return Precedence.Implies;
            case "<=>": return Precedence.Iff;
            case "->": return 5;
            default: return RelationOperators.Contains(t.Text) ? Precedence.Relation : -1;
        }
    }

    // "||" in operator position is logical or; the two bars must touch.
    private bool IsDoubleBar(Token t) => t.Is("|") && Peek(1).Is("|") && !Peek(1).SpaceBefore && Peek().Start == t.Start;

    private bool StartsPrimary(Token t)
    {
        switch (t.Kind)
        {
            case TokenKind.Identifier:
                return t.Text is not ("forall" or "exists");
            case TokenKind.Operator:
                if (t.Text == "(") return !(Peek(1).IsWord("mod") || Peek(1).Is("mod"));
                return t.Text is "√" or "∛" or "∜" or "⌊" or "⌈" || (t.Text == "‖" && _normDepth == 0);
            default:
                return false;
        }
    }

    private Expr ParseInfix(Expr left, Token op, ref bool lastWasDivision)
    {
        var wasDivision = false;
        Expr result;
        switch (op.Text)
        {
            case "^":
                Next();
                if (_options.PostfixPowerLetters && Peek().Kind == TokenKind.Identifier && Peek().Text is "T" or "H" or "c" && !Peek(1).Is("(") && Peek().Text.Length == 1)
                {
                    var letter = Next().Text;
                    result = new Apply(letter == "T" ? Operators.Transpose : letter == "H" ? Operators.ConjTranspose : Operators.Complement, [left]);
                    break;
                }
                result = new Apply(Operators.Pow, [left, ParseExpr(Precedence.Power)]);
                break;
            case "*":
                Next();
                result = new Apply(Operators.Mul, [left, ParseExpr(Precedence.Multiplicative + 1)]);
                break;
            case "/":
                Next();
                result = new Apply(Operators.Div, [left, ParseExpr(Precedence.Multiplicative + 1)]);
                wasDivision = true;
                break;
            case "mod":
                Next();
                result = new Apply(Operators.Mod, [left, ParseExpr(Precedence.Multiplicative + 1)]);
                break;
            case "∘":
                Next();
                result = new Apply(Operators.Compose, [left, ParseExpr(Precedence.Multiplicative + 1)]);
                break;
            case "⊗":
                Next();
                result = new Apply(Operators.Kron, [left, ParseExpr(Precedence.Multiplicative + 1)]);
                break;
            case "+":
                Next();
                result = new Apply(Operators.Add, [left, ParseExpr(Precedence.Additive + 1)]);
                break;
            case "-":
                Next();
                result = new Apply(Operators.Sub, [left, ParseExpr(Precedence.Additive + 1)]);
                break;
            case "∩":
                Next();
                result = new Apply(Operators.Intersect, [left, ParseExpr(Precedence.Intersection + 1)]);
                break;
            case "∪":
                Next();
                result = new Apply(Operators.Union, [left, ParseExpr(Precedence.Union + 1)]);
                break;
            case "∖":
                Next();
                result = new Apply(Operators.SetMinus, [left, ParseExpr(Precedence.Union + 1)]);
                break;
            case "△":
                Next();
                result = new Apply(Operators.SymDiff, [left, ParseExpr(Precedence.Union + 1)]);
                break;
            case "and":
                Next();
                result = new Apply(Operators.And, [left, ParseExpr(Precedence.And + 1)]);
                break;
            case "or":
                Next();
                result = new Apply(Operators.Or, [left, ParseExpr(Precedence.Or + 1)]);
                break;
            case "xor":
                Next();
                result = new Apply(Operators.Xor, [left, ParseExpr(Precedence.Or + 1)]);
                break;
            case "=>":
                Next();
                result = new Apply(Operators.Implies, [left, ParseExpr(Precedence.Implies)]);
                break;
            case "<=>":
                Next();
                result = new Apply(Operators.Iff, [left, ParseExpr(Precedence.Iff + 1)]);
                break;
            case "->":
                Next();
                result = ParseLambda(left, op);
                break;
            case "|" when IsDoubleBar(op):
                Next();
                Next();
                result = new Apply(Operators.Or, [left, ParseExpr(Precedence.Or + 1)]);
                break;
            default:
                result = ParseRelationChain(left);
                break;
        }
        lastWasDivision = wasDivision;
        return result;
    }

    private static Operator RelationOperator(string text) => text switch
    {
        "=" => Operators.Eq,
        "!=" => Operators.Ne,
        "<" => Operators.Lt,
        "<=" => Operators.Le,
        ">" => Operators.Gt,
        ">=" => Operators.Ge,
        "~=" => Operators.Approx,
        "in" => Operators.Element,
        "notin" => Operators.NotElement,
        "~" => Operators.Distributed,
        "|" or "∣" => Operators.Divides,
        "⊂" => Operators.Subset,
        "⊆" => Operators.SubsetEq,
        "∝" => Operators.Proportional,
        "⊥" => Operators.Perpendicular,
        "∥" => Operators.Parallel,
        _ => Operators.Congruent,
    };

    // a < b <= c means a < b and b <= c; a ≡ b (mod n) is congruent(a, b, n).
    private Expr ParseRelationChain(Expr first)
    {
        var relations = new List<Expr>();
        var left = first;
        while (true)
        {
            var t = Peek();
            if (t.Kind != TokenKind.Operator || !(RelationOperators.Contains(t.Text) || (t.Text == "|" && _noBar == 0)) || IsDoubleBar(t)) break;
            Next();
            var right = ParseExpr(Precedence.Relation + 1);
            if (t.Text == "≡")
            {
                Expr modulus;
                if (Peek().Is("(") && (Peek(1).Is("mod") || Peek(1).IsWord("mod")))
                {
                    Next();
                    Next();
                    modulus = ParseExpr(0);
                    Expect(")");
                }
                else
                {
                    throw Fail("Expected '(mod n)' after a congruence.", Peek().Span, ["'(mod n)'"]);
                }
                relations.Add(new Apply(Operators.Congruent, [left, right, modulus]));
            }
            else
            {
                relations.Add(new Apply(RelationOperator(t.Text), [left, right]));
            }
            left = right;
        }
        return relations.Skip(1).Aggregate(relations[0], (all, next) => new Apply(Operators.And, [all, next]));
    }

    private Bind ParseLambda(Expr parameters, Token arrow)
    {
        ImmutableArray<Symbol> bound;
        if (parameters is Symbol s) bound = [s];
        else if (parameters is TupleLiteral t && t.Elements.All(e => e is Symbol)) bound = [.. t.Elements.Cast<Symbol>()];
        else throw Fail("The left side of '->' must be a variable or a tuple of variables.", arrow.Span, ["variable"]);
        var body = ParseExpr(5);
        return new Bind(Binder.Lambda, bound, [], body);
    }

    // ----- Prefix constructs and atoms -----

    private Expr ParsePrefix()
    {
        var t = Next();
        switch (t.Kind)
        {
            case TokenKind.Number:
                return ParsePostfix(new Number(t.Value, t.Display));
            case TokenKind.String:
                if (_stringsAllowed && t.Text is "+" or "-") return new Number(t.Text == "+" ? 1 : -1);
                throw Fail("Unexpected string.", t.Span, ["expression"]);
            case TokenKind.Identifier:
                return ParseIdentifier(t);
            case TokenKind.End:
                throw Fail("Expected an expression but the input ended.", t.Span, ["expression"]);
        }

        switch (t.Text)
        {
            case "(":
                return ParsePostfix(ParseParenthesized(t));
            case "[":
                return ParsePostfix(ParseBracketed(t));
            case "]":
                return ParsePostfix(ParseOpenLeftInterval(t));
            case "{":
                return ParsePostfix(ParseBraced(t));
            case "-":
                return new Apply(Operators.Neg, [ParseExpr(Precedence.Prefix)]);
            case "+":
                return ParseExpr(Precedence.Prefix);
            case "not" or "!":
                return new Apply(Operators.Not, [ParseExpr(Precedence.Not + 1)]);
            case "|":
                return ParsePostfix(ParseDelimited(t, "|", Operators.Abs));
            case "⌊":
                return ParsePostfix(ParseDelimited(t, "⌋", Operators.Floor));
            case "⌈":
                return ParsePostfix(ParseDelimited(t, "⌉", Operators.Ceil));
            case "‖":
                _normDepth++;
                try
                {
                    return ParsePostfix(ParseDelimited(t, "‖", Operators.Norm));
                }
                finally
                {
                    _normDepth--;
                }
            case "√":
                return new Apply(Operators.Sqrt, [ParseExpr(Precedence.Power)]);
            case "∛":
                return new Apply(Operators.Root, [ParseExpr(Precedence.Power), new Number(3)]);
            case "∜":
                return new Apply(Operators.Root, [ParseExpr(Precedence.Power), new Number(4)]);
            case "∑":
                return ParseNamedForm(t, "sum");
            case "∏":
                return ParseNamedForm(t, "product");
            case "∫":
                return ParseNamedForm(t, "integrate");
            case "∀":
                return ParseQuantifier(t, Binder.ForAll);
            case "∃":
                return ParseQuantifier(t, Peek().Is("!") ? Binder.ExistsUnique : Binder.Exists);
            case "∇":
                return new Apply(Operators.Grad, [ParseTightArgument()]);
            case "∂":
                return ParsePartial(t);
            default:
                throw Fail($"Unexpected {Describe(t)}.", t.Span, ["expression"]);
        }
    }

    private Expr ParseNamedForm(Token symbol, string name)
    {
        if (!Peek().Is("(")) throw Fail($"Expected '(' after '{symbol.Text}'.", Peek().Span, ["'('"]);
        return ParseBinderCall(symbol, name);
    }

    private Expr ParsePostfix(Expr atom)
    {
        while (true)
        {
            var t = Peek();
            if (t.Is("!!"))
            {
                Next();
                atom = new Apply(Operators.Factorial2, [atom]);
            }
            else if (t.Is("!"))
            {
                Next();
                atom = new Apply(Operators.Factorial, [atom]);
            }
            else
            {
                return atom;
            }
        }
    }

    private Expr ParseParenthesized(Token open)
    {
        if (Peek().Is(")")) throw Fail("Empty parentheses.", new TextSpan(open.Start, Peek().End - open.Start), ["expression"]);
        var elements = new List<Expr> { ParseExpr(0) };
        while (Accept(",")) elements.Add(ParseExpr(0));
        if (Peek().Is("]") && elements.Count == 2)
        {
            Next();
            return new IntervalLiteral(elements[0], elements[1], false, true);
        }
        Expect(")");
        return elements.Count == 1 ? elements[0] : new TupleLiteral([.. elements]);
    }

    private Expr ParseBracketed(Token open)
    {
        if (Peek().Is("["))
        {
            // Rows: [[a, b], [c, d]]
            var rows = new List<List<Expr>>();
            do
            {
                var rowOpen = Peek();
                Expect("[");
                var row = new List<Expr>();
                if (Peek().Is("]")) throw Fail("A matrix row cannot be empty.", Peek().Span, ["expression"]);
                row.Add(ParseExpr(0));
                while (Accept(",")) row.Add(ParseExpr(0));
                Expect("]");
                if (rows.Count > 0 && rows[0].Count != row.Count) throw Fail($"Matrix rows must have equal length: expected {rows[0].Count} entries but found {row.Count}.", rowOpen.Span, []);
                rows.Add(row);
            }
            while (Accept(","));
            Expect("]");
            return new MatrixLiteral(rows.Count, rows[0].Count, [.. rows.SelectMany(r => r)]);
        }

        if (Peek().Is("]")) throw Fail("Empty brackets.", new TextSpan(open.Start, Peek().End - open.Start), ["expression"]);
        var elements = new List<Expr> { ParseExpr(0) };
        while (Accept(",")) elements.Add(ParseExpr(0));
        var closer = Peek();
        if (elements.Count == 2 && (closer.Is("]") || closer.Is(")")))
        {
            Next();
            return new IntervalLiteral(elements[0], elements[1], true, closer.Is("]"));
        }
        if (elements.Count == 2 && closer.Is("["))
        {
            Next();
            return new IntervalLiteral(elements[0], elements[1], true, false);
        }
        Expect("]");
        return new MatrixLiteral(elements.Count, 1, [.. elements]);
    }

    private IntervalLiteral ParseOpenLeftInterval(Token open)
    {
        var lower = ParseExpr(0);
        Expect(",");
        var upper = ParseExpr(0);
        var closer = Peek();
        if (!closer.Is("[") && !closer.Is("]")) throw Fail($"Expected '[' or ']' to end the interval but found {Describe(closer)}.", closer.Span, ["'['", "']'"]);
        Next();
        return new IntervalLiteral(lower, upper, false, closer.Is("]"));
    }

    private Expr ParseBraced(Token open)
    {
        if (Accept("}")) return new SetLiteral([]);
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

        if (Peek().Is("|") || Peek().Is("∣") || Peek().Is(":"))
        {
            var separator = Next();
            var rest = ParseExpr(0);
            Expect("}");
            if (first is Apply { Operator.Id: "element" } builder && builder.Arguments[0] is Symbol x) return new Bind(Binder.SetBuilder, [x], [builder.Arguments[1]], rest);
            if (rest is Apply { Operator.Id: "element" } image && image.Arguments[0] is Symbol k) return new Bind(Binder.ImageSet, [k], [image.Arguments[1]], first);
            throw Fail("A set needs the form {x in S | P} or {f | k in S}.", separator.Span, ["'x in S | P'", "'f | k in S'"]);
        }

        var elements = new List<Expr> { first };
        while (Accept(",")) elements.Add(ParseExpr(0));
        Expect("}");
        return new SetLiteral([.. elements]);
    }

    private Apply ParseDelimited(Token open, string closer, Operator op)
    {
        _noBar++;
        Expr inner;
        try
        {
            inner = ParseExpr(0);
        }
        finally
        {
            _noBar--;
        }
        var t = Peek();
        if (!t.Is(closer)) throw Fail(t.Kind == TokenKind.End ? $"Expected '{closer}' to close '{open.Text}' but the input ended." : $"Expected '{closer}' to close '{open.Text}' but found {Describe(t)}.", t.Span, [$"'{closer}'"], $"Add a closing '{closer}'.");
        Next();
        return new Apply(op, [inner]);
    }

    // ----- Identifiers -----

    private static bool IsFunctionName(string name) => Operators.TryGetByName(name, out _) || Overloads.ContainsKey(name);

    private bool IsCallAhead() => Peek().Is("(");

    private Expr ParseIdentifier(Token t)
    {
        var name = t.Text;

        if (name is "forall" or "exists" && !(Peek().Is("(") && IsAdjacent(0)))
        {
            return ParseQuantifier(t, name == "forall" ? Binder.ForAll : Peek().Is("!") && IsAdjacent(0) ? Binder.ExistsUnique : Binder.Exists);
        }

        var leibniz = TryParseLeibniz(t);
        if (leibniz is not null) return leibniz;

        if (name.StartsWith("log_", StringComparison.Ordinal) && name.Length > 4) return ParsePostfix(ParseLogWithBase(t, name[4..]));

        if (BinderNames.Contains(name) && Peek().Is("(")) return ParseBinderCall(t, name);
        if (name == "Piecewise" && Peek().Is("[")) return ParsePiecewise(t);

        if (_options.AllowWilds && name.Length > 1 && name.EndsWith('_') && !name.TrimEnd('_').Contains('_', StringComparison.Ordinal)) return ParsePostfix(new Wild(name.TrimEnd('_')));

        var constant = ResolveConstant(name);

        // Overloaded abbreviations (P, C, E, I, F, H, ...) are functions only when applied with an adjacent '('.
        var parenthesized = Peek().Is("(");
        var adjacent = parenthesized && IsAdjacent(0);

        // A function symbol the caller declared shadows an operator name or abbreviation of the same spelling (a declared f, u or H).
        if (adjacent && _options.Declarations is not null && _options.Declarations.TryGetValue(name, out var shadowing) && shadowing is FunctionSort) return ParsePostfix(ParseFunctionSymbolCall(t, name, shadowing));

        var overloaded = Overloads.ContainsKey(name);
        if (overloaded && adjacent) return ParsePostfix(ParseCall(t, name));

        if (!overloaded && Operators.TryGetByName(name, out var op) && !(GreekFunctionNames.Contains(name) && !parenthesized) && !(constant is not null && !parenthesized))
        {
            if (parenthesized || Peek().Is("^")) return ParsePostfix(ParseCall(t, name));
            if (AcceptsBareArgument(op)) return ParseBareFunction(t, name);
            throw Fail($"The function '{name}' needs parentheses: {name}(…).", Peek().Span, ["'('"]);
        }

        if (constant is { } c) return ParsePostfix(new Constant(c));

        // Function symbols: f(x), speed(t), declared functions.
        var declared = _options.Declarations is not null && _options.Declarations.TryGetValue(name, out var declaredSort) ? declaredSort : null;
        var isFunctionSymbol = declared is FunctionSort || _functionSymbols.Contains(name) || (name.Length > 1 && !_options.SingleLetterVariables) || (!IsSingleLetterWord(name) && (name.Contains('_', StringComparison.Ordinal) || name.Any(char.IsDigit) || adjacent));
        if (parenthesized && adjacent && isFunctionSymbol && declared is null or FunctionSort) return ParsePostfix(ParseFunctionSymbolCall(t, name, declared));

        var symbol = MakeSymbol(name);
        if (Greek.TryGetValue(name, out var greek)) symbol = MakeSymbol(greek);
        if (Peek().Is("'") && IsAdjacent(0)) return ParsePostfix(ParseDerivativeSymbol(t, name));
        return ParsePostfix(symbol);
    }

    // log_2(8), log_b x: the base is the text after the underscore.
    private Apply ParseLogWithBase(Token nameToken, string baseText)
    {
        Expr logBase = baseText.All(c => c is >= '0' and <= '9') ? new Number(BigRational.Parse(baseText, null)) : MakeSymbol(baseText);
        Expr argument;
        if (Peek().Is("("))
        {
            var args = ParseArguments(nameToken);
            if (args.Length != 1) throw Fail($"'log_{baseText}' takes 1 argument but {args.Length} were given.", nameToken.Span, []);
            argument = args[0];
        }
        else
        {
            argument = ParseTightArgument();
        }
        return new Apply(Operators.Log, [argument, logBase]);
    }

    private static bool IsSingleLetterWord(string name) => name.Length == 1 || (name.Length == 2 && char.IsHighSurrogate(name[0]));

    private static bool AcceptsBareArgument(Operator op) =>
        op.Arity.Min == 1 && op.Arity.Max is 1 or 2 && op.Notation.Fixity == Fixity.Function
        && op.Family is OperatorFamily.Arithmetic or OperatorFamily.Complex or OperatorFamily.ExpLog or OperatorFamily.Trig or OperatorFamily.Hyperbolic
        && op.Id is not ("N" or "subfactorial" or "binomial" or "perm" or "gcd" or "lcm" or "max" or "min" or "round" or "quo" or "root" or "atan2" or "factorial" or "factorial2" or "mod");

    private ConstantId? ResolveConstant(string name)
    {
        if (name == "e" && !_options.EIsSymbol) return ConstantId.E;
        if (name == _options.ImaginaryUnit) return ConstantId.ImaginaryUnit;
        if (ConstantWords.TryGetValue(name, out var c)) return c;
        if (_options.NumberSetLetters && SetLetters.TryGetValue(name, out var s)) return s;
        return null;
    }

    // A variable named by the user (in d/dx, ∂/∂x, quantifiers): Greek words become Greek letters, and constants are not variables.
    private Symbol VariableSymbol(string name, TextSpan span)
    {
        if (Greek.TryGetValue(name, out var greek)) name = greek;
        else if (ResolveConstant(name) is not null) throw Fail($"'{name}' is a constant, not a variable.", span, ["variable"]);
        return MakeSymbol(name);
    }

    private Symbol MakeSymbol(string name)
    {
        var sort = _options.Declarations is not null && _options.Declarations.TryGetValue(name, out var declared) ? declared : _options.DefaultSort;
        return new Symbol(name, sort);
    }

    private static Symbol MakeFunctionSymbol(string name, int arity, Sort? declared)
    {
        if (declared is not null) return new Symbol(name, declared);
        var domain = arity == 1 ? Sort.Real : Sort.TupleOf([.. Enumerable.Repeat(Sort.Real, arity)]);
        return new Symbol(name, Sort.FunctionOf(domain, Sort.Real));
    }

    private ImmutableArray<Expr> ParseArguments(Token callee)
    {
        Expect("(");
        if (Accept(")")) return [];
        var args = ImmutableArray.CreateBuilder<Expr>();
        args.Add(ParseExpr(0));
        while (Accept(",")) args.Add(ParseExpr(0));
        var t = Peek();
        if (!t.Is(")")) throw Fail(t.Kind == TokenKind.End ? $"Expected ')' to close the arguments of '{callee.Text}' but the input ended." : $"Expected ',' or ')' in the arguments of '{callee.Text}' but found {Describe(t)}.", t.Span, ["','", "')'"], "Add a closing ')'.");
        Next();
        return args.ToImmutable();
    }

    private Apply ParseCall(Token nameToken, string name)
    {
        var inverse = false;
        Expr? exponent = null;
        if (Peek().Is("^"))
        {
            // sin^2(x) and sin^-1(x)
            (exponent, inverse) = ParseFunctionPower(name);
            if (!Peek().Is("(")) return BuildPoweredCall(nameToken, name, [ParseTightArgument()], exponent, inverse);
        }
        var args = ParseArguments(nameToken);
        return BuildPoweredCall(nameToken, name, args, exponent, inverse);
    }

    private Apply ParseBareFunction(Token nameToken, string name)
    {
        Expr? exponent = null;
        var inverse = false;
        if (Peek().Is("^")) (exponent, inverse) = ParseFunctionPower(name);
        if (Peek().Is("(")) return BuildPoweredCall(nameToken, name, ParseArguments(nameToken), exponent, inverse);
        var t = Peek();
        if (t.Kind == TokenKind.End || !(StartsPrimary(t) || t.Kind == TokenKind.Number || t.Is("-") || t.Is("+") || t.Is("|") || t.Is("{") || t.Is("[") || t.Kind == TokenKind.Operator && t.Text is "not" or "!"))
        {
            throw Fail($"The function '{name}' needs an argument.", t.Span, ["expression"], $"Write {name}(x) or {name} x.");
        }
        return BuildPoweredCall(nameToken, name, [ParseTightArgument()], exponent, inverse);
    }

    // After the function name, at '^': returns the exponent, or inverse = true for the textbook sin^-1 convention.
    private (Expr? Exponent, bool Inverse) ParseFunctionPower(string name)
    {
        Next();
        var negative = false;
        if (Peek().Is("-"))
        {
            Next();
            negative = true;
        }
        var exponent = ParseExpr(Precedence.Power + 1);
        if (negative && exponent is Number { Value.IsInteger: true } one && one.Value == BigRational.One && InverseNames.ContainsKey(name)) return (null, true);
        return (negative ? new Apply(Operators.Neg, [exponent]) : exponent, false);
    }

    private Apply BuildPoweredCall(Token nameToken, string name, ImmutableArray<Expr> args, Expr? exponent, bool inverse)
    {
        var call = BuildCall(nameToken, inverse ? InverseNames[name] : name, args);
        return exponent is null ? call : new Apply(Operators.Pow, [call, exponent]);
    }

    private Apply BuildCall(Token nameToken, string name, ImmutableArray<Expr> args)
    {
        var span = new TextSpan(nameToken.Start, Math.Max(nameToken.End, _tokens[Math.Max(_position - 1, 0)].End) - nameToken.Start);
        Operator? op = null;
        if (Overloads.TryGetValue(name, out var table))
        {
            foreach (var (arity, id) in table)
            {
                if (arity == args.Length || (id == "leviCivita" && args.Length >= 2))
                {
                    op = Operators.Get(id);
                    break;
                }
            }
            if (op is null) throw Fail($"'{name}' cannot take {args.Length} argument(s); expected {string.Join(" or ", table.Select(o => o.Arity).Distinct())}.", span, []);
        }
        else if (Operators.TryGetByName(name, out var found))
        {
            op = found;
        }
        if (op is null) throw Fail($"Unknown function '{name}'.", nameToken.Span, []);
        if (!op.Arity.Accepts(args.Length)) throw Fail($"'{name}' takes {op.Arity} argument(s) but {args.Length} were given.", span, []);
        if (op == Operators.Prob && args.Length == 1 && args[0] is Apply { Operator.Id: "divides" } conditional) return new Apply(Operators.Prob, conditional.Arguments);
        if (op == Operators.Log && args.Length == 1) return _options.LogMeansNatural ? new Apply(Operators.Ln, args) : new Apply(Operators.Log, [args[0], new Number(10)]);
        return new Apply(op, args);
    }

    private Apply ParseFunctionSymbolCall(Token nameToken, string name, Sort? declared)
    {
        var args = ParseArguments(nameToken);
        var symbol = MakeFunctionSymbol(name, Math.Max(args.Length, 1), declared);
        SuggestKnownFunction(nameToken, name);
        return new Apply(Operators.Call, [symbol, .. args]);
    }

    private void SuggestKnownFunction(Token nameToken, string name)
    {
        if (name.Length < 3) return;
        foreach (var known in Operators.FunctionNames)
        {
            if (known.Length >= 3 && Math.Abs(known.Length - name.Length) <= 1 && EditDistance(known, name) == 1)
            {
                _warnings.Add(new ParseWarning("UnknownFunction", $"'{name}' is treated as a user-defined function. Did you mean {known}(x)?", nameToken.Span));
                return;
            }
        }
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++) current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    // f' and f'' (derivativeOf), optionally applied: f'(x).
    private Expr ParseDerivativeSymbol(Token nameToken, string name)
    {
        var count = 0;
        while (Peek().Is("'") && IsAdjacent(0))
        {
            Next();
            count++;
        }
        var declared = _options.Declarations is not null && _options.Declarations.TryGetValue(name, out var s) ? s : null;
        var applied = Peek().Is("(") && IsAdjacent(0);
        var args = applied ? ParseArguments(nameToken) : [];
        var symbol = MakeFunctionSymbol(Greek.TryGetValue(name, out var greek) ? greek : name, Math.Max(args.Length, 1), declared);
        Expr derivative = new Apply(Operators.DerivativeOf, [symbol, new Number(count)]);
        return applied ? new Apply(Operators.Call, [derivative, .. args]) : derivative;
    }

    // A function argument without parentheses: sin 2x is sin(2x), sin x cos x is sin(x)·cos(x).
    private Expr ParseTightArgument()
    {
        var first = ParseExpr(Precedence.Power);
        while (true)
        {
            var t = Peek();
            if (t.Kind == TokenKind.Identifier ? !IsFunctionName(t.Text) && !BinderNames.Contains(t.Text) && t.Text is not ("forall" or "exists") : t.Is("(") && !(Peek(1).Is("mod")))
            {
                first = new Apply(Operators.Mul, [first, ParseExpr(Precedence.Power)]);
                continue;
            }
            return first;
        }
    }

    // ----- Derivatives in Leibniz and partial notation -----

    private bool IsDifferential(Token t, out string variable)
    {
        variable = string.Empty;
        if (t.Kind != TokenKind.Identifier || !IsDifferentialName(t.Text)) return false;
        variable = t.Text[1..];
        return true;
    }

    private Apply? TryParseLeibniz(Token t)
    {
        if (t.Text[0] != 'd') return null;
        var start = _position;
        var order = BigRational.One;

        // d^n y / dx^n  or  d^n / dx^n f
        if (t.Text == "d" && Peek().Is("^") && Peek(1).Kind == TokenKind.Number)
        {
            order = Peek(1).Value;
            _position += 2;
        }

        string? numeratorVariable = null;
        if (t.Text == "d" && Peek().Is("/")) { }
        else if (t.Text == "d" && Peek().Kind == TokenKind.Identifier && Peek(1).Is("/") && IsAtomicSymbolWord(Peek().Text)) numeratorVariable = Next().Text;
        else if (t.Text.Length > 1 && IsDifferential(t, out var inline) && Peek().Is("/")) numeratorVariable = inline;
        else
        {
            _position = start;
            return null;
        }

        if (!Peek().Is("/") || !IsDifferential(Peek(1), out var variable))
        {
            _position = start;
            return null;
        }
        Next();
        Next();
        if (Peek().Is("^") && Peek(1).Kind == TokenKind.Number && Peek(1).Value == order)
        {
            Next();
            Next();
        }
        var x = VariableSymbol(variable, _tokens[_position - 1].Span);
        Expr operand = numeratorVariable is not null ? VariableSymbol(numeratorVariable, _tokens[start].Span) : ParseTightArgument();
        return BuildDerivative(operand, x, order);
    }

    private Apply ParsePartial(Token partial)
    {
        var order = BigRational.One;
        if (Peek().Is("^") && Peek(1).Kind == TokenKind.Number)
        {
            order = Peek(1).Value;
            Next();
            Next();
        }
        Expr? operand = null;
        if (!Peek().Is("/")) operand = ParseExpr(Precedence.Power + 1);
        Expect("/");
        var denominator = Peek();
        if (!denominator.Is("∂")) throw Fail("Expected '∂' in the denominator of a partial derivative.", denominator.Span, ["'∂'"]);
        Next();
        if (Peek().Kind != TokenKind.Identifier) throw Fail("Expected a variable after '∂'.", Peek().Span, ["variable"]);
        var variableToken = Next();
        var x = VariableSymbol(variableToken.Text, variableToken.Span);
        if (Peek().Is("^") && Peek(1).Kind == TokenKind.Number && Peek(1).Value == order)
        {
            Next();
            Next();
        }
        operand ??= ParseTightArgument();
        return BuildDerivative(operand, x, order);
    }

    private static Apply BuildDerivative(Expr operand, Symbol x, BigRational order) =>
        order == BigRational.One ? new Apply(Operators.Diff, [operand, x]) : new Apply(Operators.Diff, [operand, x, new Number(order)]);

    // ----- Binders -----

    private Expr ParseBinderCall(Token nameToken, string name)
    {
        var allowStrings = name == "limit";
        var saved = _stringsAllowed;
        _stringsAllowed = allowStrings;
        ImmutableArray<Expr> args;
        try
        {
            args = ParseArguments(nameToken);
        }
        finally
        {
            _stringsAllowed = saved;
        }

        var span = new TextSpan(nameToken.Start, _tokens[_position - 1].End - nameToken.Start);
        Symbol AsSymbol(Expr e, string role)
        {
            if (e is Symbol s) return s;
            throw Fail($"'{name}' expects a variable as its {role} argument.", span, ["variable"]);
        }

        void Count(params int[] counts)
        {
            if (!counts.Contains(args.Length)) throw Fail($"'{name}' takes {string.Join(" or ", counts)} arguments but {args.Length} were given.", span, []);
        }

        switch (name)
        {
            case "sum" or "product" or "Union" or "Intersection":
                Count(4);
                var binder = name switch { "sum" => Binder.Sum, "product" => Binder.Product, "Union" => Binder.IndexedUnion, _ => Binder.IndexedIntersection };
                return ParsePostfix(new Bind(binder, [AsSymbol(args[1], "second")], [args[2], args[3]], args[0]));
            case "integrate":
                Count(2, 3, 4);
                if (args.Length == 2) return ParsePostfix(new Apply(Operators.Integrate, [args[0], args[1]]));
                if (args.Length == 4 && args[1] is Symbol) return ParsePostfix(new Bind(Binder.Integral, [(Symbol)args[1]], [args[2], args[3]], args[0]));
                var bound = ImmutableArray.CreateBuilder<Symbol>();
                var data = ImmutableArray.CreateBuilder<Expr>();
                foreach (var range in args.Skip(1))
                {
                    if (range is not TupleLiteral { Elements.Length: 3 } r || r.Elements[0] is not Symbol v) throw Fail("A multiple integral takes ranges of the form (x, a, b).", span, ["(x, a, b)"]);
                    bound.Add(v);
                    data.Add(r.Elements[1]);
                    data.Add(r.Elements[2]);
                }
                return ParsePostfix(new Bind(Binder.Integral, bound.ToImmutable(), data.ToImmutable(), args[0]));
            case "limit":
                Count(3, 4);
                return ParsePostfix(new Bind(Binder.Limit, [AsSymbol(args[1], "second")], [.. args.Skip(2)], args[0]));
            case "laplace" or "fourier":
                Count(3);
                return ParsePostfix(new Bind(name == "laplace" ? Binder.Laplace : Binder.Fourier, [AsSymbol(args[1], "second")], [args[2]], args[0]));
            case "argmin" or "argmax":
                Count(2);
                if (args[1] is not Apply { Operator.Id: "element" } e || e.Arguments[0] is not Symbol x) throw Fail($"'{name}' expects its second argument in the form x in S.", span, ["x in S"]);
                return ParsePostfix(new Bind(name == "argmin" ? Binder.ArgMin : Binder.ArgMax, [x], [e.Arguments[1]], args[0]));
            default:
                throw Fail($"'{name}' is not a binder.", nameToken.Span, []);
        }
    }

    private Expr ParsePiecewise(Token nameToken)
    {
        Expect("[");
        var cases = ImmutableArray.CreateBuilder<(Expr, Expr)>();
        do
        {
            var open = Peek();
            Expect("(");
            var value = ParseExpr(0);
            Expect(",");
            var condition = ParseExpr(0);
            Expect(")");
            cases.Add((value, condition));
        }
        while (Accept(","));
        Expect("]");
        return ParsePostfix(new Piecewise(cases.ToImmutable()));
    }

    // forall x in S: P, exists x: P, exists! x in S: P
    private Bind ParseQuantifier(Token keyword, Binder binder)
    {
        if (binder == Binder.ExistsUnique && Peek().Is("!")) Next();
        var bound = new List<Symbol>();
        do
        {
            var t = Peek();
            if (t.Kind != TokenKind.Identifier) throw Fail($"Expected a variable after '{keyword.Text}' but found {Describe(t)}.", t.Span, ["variable"]);
            Next();
            bound.Add(VariableSymbol(t.Text, t.Span));
        }
        while (Accept(","));

        ImmutableArray<Expr> data = [];
        if (Accept("in")) data = [ParseExpr(Precedence.Union)];
        Expect(":");
        var body = ParseExpr(Precedence.Quantifier);
        return new Bind(binder, [.. bound], data, body);
    }
}
