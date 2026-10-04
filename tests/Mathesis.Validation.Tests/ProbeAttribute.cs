using System.Globalization;
using Mathesis.Numbers;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Validation.Tests;

/// <summary>
/// A test-only attribute that exercises every rule of <see cref="MathValidationAttribute"/> before a real attribute exists. Its verdict is a pure
/// function of the text (<see cref="Verdict"/>), it counts how often the verdict core runs, and it supports <see cref="int"/>, <see cref="long"/>
/// and <see cref="BigRational"/> values as typed values.
/// </summary>
public sealed class ProbeAttribute : MathValidationAttribute
{
    private int _evaluations;

    /// <summary>How many times the verdict core has run on this instance.</summary>
    public int Evaluations => Volatile.Read(ref _evaluations);

    /// <summary>When set, <see cref="ValidateOptions"/> reports it as a configuration problem.</summary>
    public string? InvalidOption { get; init; }

    internal override int ExtraPlaceholderCount => 3;

    internal override string? ValidateOptions() => InvalidOption;

    internal override bool IsSupported(object value) => value is int or long or BigRational;

    internal override MathDiagnostic? Evaluate(object value)
    {
        Interlocked.Increment(ref _evaluations);
        return Verdict(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    /// <summary>The verdict for <paramref name="text"/>: a hash of the text picks one of eight outcomes, three of them valid.</summary>
    internal static MathDiagnostic? Verdict(string text)
    {
        var hash = 17;
        foreach (var c in text) hash = unchecked(hash * 31 + c);
        return ((uint)hash % 8) switch
        {
            1 => new MathDiagnostic(MathValidationCode.Syntax, [text.Length], new TextSpan(0, Math.Min(1, text.Length)), "Try again."),
            2 => new MathDiagnostic(MathValidationCode.WrongShape, ["an equation", "an expression"]),
            3 => new MathDiagnostic(MathValidationCode.OutOfRange, ["[1, 2]", "1", "2"]),
            4 => new MathDiagnostic(MathValidationCode.Zero, [], new TextSpan(text.Length, 0)),
            5 => new MathDiagnostic(MathValidationCode.UnknownVariable, ["x"], Suggestion: "Remove x."),
            _ => null,
        };
    }

    /// <summary>The first of the strings <c>"0"</c>, <c>"1"</c>, ... whose verdict has <paramref name="code"/>.</summary>
    internal static string InputFor(MathValidationCode code)
    {
        for (var i = 0; ; i++)
        {
            var text = i.ToString(CultureInfo.InvariantCulture);
            if (Verdict(text)?.Code == code) return text;
        }
    }
}

/// <summary>A resource type for <c>ErrorMessageResourceType</c>.</summary>
public static class CustomMessages
{
    /// <summary>A template with the display name and the first detail.</summary>
    public static string Replaced => "Custom: {0} / {1}";
}

/// <summary>A resource type whose message cannot be read, as with a broken resource store.</summary>
public static class ThrowingMessages
{
    /// <summary>Always throws.</summary>
    public static string Broken => throw new NotSupportedException("The message store is offline.");
}
