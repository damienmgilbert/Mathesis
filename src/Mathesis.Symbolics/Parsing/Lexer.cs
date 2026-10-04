using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Parsing;

internal enum TokenKind : byte
{
    Number,
    Identifier,
    String,
    Operator,
    End,
}

/// <summary>A lexical token. Operators are normalized: Unicode spellings map to their ASCII or canonical text.</summary>
internal readonly record struct Token(TokenKind Kind, string Text, int Start, int End, bool SpaceBefore, BigRational Value = default, NumberDisplay Display = default)
{
    public TextSpan Span => new(Start, End - Start);

    public bool Is(string op) => Kind == TokenKind.Operator && Text == op;

    public bool IsWord(string word) => Kind == TokenKind.Identifier && Text == word;
}

/// <summary>Splits linear text into tokens.</summary>
internal static class Lexer
{
    private const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    private static readonly string[] MultiCharOperators = ["<=>", "=>", ">=", "<=", "!=", "~=", "&&", "!!", "->", "..."];

    // Single characters that become operator tokens, with the normalized text they map to.
    private static readonly Dictionary<char, string> Singles = new()
    {
        ['+'] = "+", ['-'] = "-", ['−'] = "-", ['*'] = "*", ['·'] = "*", ['⋅'] = "*", ['×'] = "*", ['∗'] = "*",
        ['/'] = "/", ['÷'] = "/", ['^'] = "^", ['('] = "(", [')'] = ")", ['['] = "[", [']'] = "]", ['{'] = "{", ['}'] = "}",
        [','] = ",", [':'] = ":", [';'] = ";", ['='] = "=", ['<'] = "<", ['>'] = ">", ['~'] = "~", ['!'] = "!", ['|'] = "|",
        ['\''] = "'", ['′'] = "'", ['≤'] = "<=", ['≥'] = ">=", ['≠'] = "!=", ['≈'] = "~=", ['∈'] = "in", ['∉'] = "notin",
        ['∪'] = "∪", ['∩'] = "∩", ['∖'] = "∖", ['\\'] = "∖", ['△'] = "△", ['∧'] = "and", ['∨'] = "or", ['¬'] = "not",
        ['→'] = "=>", ['⇒'] = "=>", ['↔'] = "<=>", ['⇔'] = "<=>", ['⊕'] = "xor", ['↦'] = "->", ['∣'] = "∣",
        ['⊂'] = "⊂", ['⊆'] = "⊆", ['∝'] = "∝", ['⊥'] = "⊥", ['∥'] = "∥", ['⊗'] = "⊗", ['∘'] = "∘", ['≡'] = "≡",
        ['√'] = "√", ['∛'] = "∛", ['∜'] = "∜", ['∑'] = "∑", ['∏'] = "∏", ['∫'] = "∫", ['∀'] = "∀", ['∃'] = "∃",
        ['⌊'] = "⌊", ['⌋'] = "⌋", ['⌈'] = "⌈", ['⌉'] = "⌉", ['‖'] = "‖", ['∇'] = "∇", ['∂'] = "∂", ['.'] = ".",
    };

    public static (ImmutableArray<Token> Tokens, ParseError? Error) Tokenize(string text)
    {
        var tokens = ImmutableArray.CreateBuilder<Token>();
        var i = 0;
        var space = false;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                space = true;
                continue;
            }
            var start = i;

            if (IsAsciiDigit(c) || (c == '.' && i + 1 < text.Length && IsAsciiDigit(text[i + 1])))
            {
                i = ReadNumber(text, i, out var value, out var display, out var numberError);
                if (numberError is not null) return (tokens.ToImmutable(), numberError);
                tokens.Add(new Token(TokenKind.Number, text[start..i], start, i, space, value, display));
            }
            else if (IsIdentifierStart(text, i, out var width))
            {
                i += width;
                while (i < text.Length && IsIdentifierPart(text, i, out width)) i += width;
                tokens.Add(new Token(TokenKind.Identifier, text[start..i], start, i, space));
            }
            else if (c is '∞' or '∅')
            {
                i++;
                tokens.Add(new Token(TokenKind.Identifier, c.ToString(), start, i, space));
            }
            else if (c == '"')
            {
                var end = text.IndexOf('"', i + 1);
                if (end < 0) return (tokens.ToImmutable(), new ParseError("Unterminated string.", new TextSpan(i, text.Length - i), ["'\"'"]));
                i = end + 1;
                tokens.Add(new Token(TokenKind.String, text[(start + 1)..(i - 1)], start, i, space));
            }
            else if (Superscripts.Contains(c, StringComparison.Ordinal) || c is '⁻' or '⁺')
            {
                // A run of superscript characters is a power: x² is x ^ 2, x⁻¹ is x ^ -1.
                tokens.Add(new Token(TokenKind.Operator, "^", i, i + 1, space));
                var digits = new System.Text.StringBuilder();
                var runStart = i;
                if (c == '⁻' || c == '⁺')
                {
                    tokens.Add(new Token(TokenKind.Operator, c == '⁻' ? "-" : "+", i, i + 1, false));
                    i++;
                }
                var digitsStart = i;
                while (i < text.Length && Superscripts.IndexOf(text[i], StringComparison.Ordinal) is var d and >= 0)
                {
                    digits.Append((char)('0' + d));
                    i++;
                }
                if (digits.Length == 0) return (tokens.ToImmutable(), new ParseError("A superscript sign must be followed by superscript digits.", new TextSpan(runStart, i - runStart), ["superscript digit"]));
                tokens.Add(new Token(TokenKind.Number, digits.ToString(), digitsStart, i, false, BigRational.Parse(digits.ToString(), null)));
            }
            else
            {
                var matched = false;
                foreach (var op in MultiCharOperators)
                {
                    if (string.CompareOrdinal(text, i, op, 0, op.Length) != 0) continue;
                    i += op.Length;
                    tokens.Add(new Token(TokenKind.Operator, op == "&&" ? "and" : op, start, i, space));
                    matched = true;
                    break;
                }
                if (!matched)
                {
                    if (!Singles.TryGetValue(c, out var normalized)) return (tokens.ToImmutable(), new ParseError($"Unexpected character '{c}'.", new TextSpan(i, 1), []));
                    i++;
                    tokens.Add(new Token(TokenKind.Operator, normalized, start, i, space));
                }
            }
            space = false;
        }
        tokens.Add(new Token(TokenKind.End, string.Empty, text.Length, text.Length, space));
        return (tokens.ToImmutable(), null);
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsIdentifierStart(string text, int i, out int width)
    {
        width = 1;
        var c = text[i];
        if (c == '_') return true;
        if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLetter(text, i))
        {
            width = 2;
            return true;
        }
        return char.IsLetter(c);
    }

    private static bool IsIdentifierPart(string text, int i, out int width)
    {
        width = 1;
        var c = text[i];
        if (c == '_' || IsAsciiDigit(c) || c is >= '₀' and <= '₉') return true;
        if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLetter(text, i))
        {
            width = 2;
            return true;
        }
        return char.IsLetter(c);
    }

    // digits [. digits] [e|E [+-] digits]; the exponent is taken only when digits follow it, so "2e" stays 2 times e.
    private static int ReadNumber(string text, int start, out BigRational value, out NumberDisplay display, out ParseError? error)
    {
        error = null;
        var i = start;
        while (i < text.Length && IsAsciiDigit(text[i])) i++;
        var hasFraction = false;
        var fractionDigits = 0;
        if (i < text.Length && text[i] == '.' && i + 1 < text.Length && IsAsciiDigit(text[i + 1]))
        {
            hasFraction = true;
            i++;
            var f = i;
            while (i < text.Length && IsAsciiDigit(text[i])) i++;
            fractionDigits = i - f;
        }
        else if (i < text.Length && text[i] == '.' && i > start && !(i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '.')))
        {
            // "3." is read as 3 followed by a dot token only when something other than a digit follows; leave the dot alone.
        }
        var hasExponent = false;
        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
        {
            var j = i + 1;
            if (j < text.Length && (text[j] == '+' || text[j] == '-')) j++;
            if (j < text.Length && IsAsciiDigit(text[j]))
            {
                while (j < text.Length && IsAsciiDigit(text[j])) j++;
                i = j;
                hasExponent = true;
            }
        }
        var literal = text[start..i];
        display = NumberDisplay.Default;
        if (!BigRational.TryParse(literal, CultureInfo.InvariantCulture, out value))
        {
            // The literal is only digits, a fraction and an exponent, so the exponent is what BigRational rejects.
            error = new ParseError($"The exponent of this number is out of range: its magnitude must be at most {BigRational.MaxExponentMagnitude}.", new TextSpan(start, i - start), []);
            return i;
        }
        if (hasExponent && !value.IsInteger)
        {
            // A scientific literal prints back as a plain decimal with the fewest fractional digits that are exact.
            var digits = 0;
            while (digits < 400 && value.Round(digits) != value) digits++;
            display = NumberDisplay.Decimal(digits);
        }
        else if (hasFraction)
        {
            display = NumberDisplay.Decimal(fractionDigits);
        }
        return i;
    }
}
