namespace Mathesis;

/// <summary>Kleene three-valued truth. <see cref="Unknown"/> means "could not decide", never "probably".</summary>
public enum Truth : byte
{
    /// <summary>The proposition is false.</summary>
    False,

    /// <summary>The proposition is true.</summary>
    True,

    /// <summary>The proposition could not be decided.</summary>
    Unknown,
}

/// <summary>Kleene connectives and conversions for <see cref="Truth"/>.</summary>
public static class TruthExtensions
{
    /// <summary>Strong Kleene conjunction: <c>False</c> dominates, then <c>Unknown</c>.</summary>
    public static Truth And(this Truth a, Truth b) =>
        a == Truth.False || b == Truth.False ? Truth.False
        : a == Truth.True && b == Truth.True ? Truth.True
        : Truth.Unknown;

    /// <summary>Strong Kleene disjunction: <c>True</c> dominates, then <c>Unknown</c>.</summary>
    public static Truth Or(this Truth a, Truth b) =>
        a == Truth.True || b == Truth.True ? Truth.True
        : a == Truth.False && b == Truth.False ? Truth.False
        : Truth.Unknown;

    /// <summary>Kleene negation; <c>Unknown</c> stays <c>Unknown</c>.</summary>
    public static Truth Not(this Truth a) =>
        a switch { Truth.True => Truth.False, Truth.False => Truth.True, _ => Truth.Unknown };

    /// <summary>Converts a decided Boolean to a <see cref="Truth"/>.</summary>
    public static Truth FromBool(bool value) => value ? Truth.True : Truth.False;
}
