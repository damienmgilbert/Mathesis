using Mathesis.Numbers;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>
/// The sign lattice of docs/design/07-engines.md ("Assumptions and domains"): the set of signs an expression can have at the
/// points where it is defined. <see cref="Negative"/>, <see cref="Zero"/> and <see cref="Positive"/> are the possible signs of a
/// real value; <see cref="NonReal"/> says the value may be non-real. <see cref="Real"/> is "some real number" and
/// <see cref="Unknown"/> is "anything".
/// </summary>
[Flags]
public enum SignInfo : byte
{
    /// <summary>No sign is possible: the expression is defined nowhere.</summary>
    None = 0,

    /// <summary>The value may be negative.</summary>
    Negative = 1,

    /// <summary>The value may be zero.</summary>
    Zero = 2,

    /// <summary>The value may be positive.</summary>
    Positive = 4,

    /// <summary>The value may be a non-real complex number.</summary>
    NonReal = 8,

    /// <summary>At most zero: <c>≤ 0</c>.</summary>
    NonPositive = Negative | Zero,

    /// <summary>At least zero: <c>≥ 0</c>.</summary>
    NonNegative = Zero | Positive,

    /// <summary>Not zero: <c>≠ 0</c>.</summary>
    NonZero = Negative | Positive,

    /// <summary>Some real number.</summary>
    Real = Negative | Zero | Positive,

    /// <summary>Nothing is known.</summary>
    Unknown = Real | NonReal,
}

internal static class SignMath
{
    // An operand that may be non-real, or that is defined nowhere, makes the result unknown.
    private static bool Opaque(SignInfo a, SignInfo b) => a.HasFlag(SignInfo.NonReal) || b.HasFlag(SignInfo.NonReal) || a == SignInfo.None || b == SignInfo.None;

    public static SignInfo Neg(SignInfo a) =>
        a.HasFlag(SignInfo.NonReal) ? SignInfo.Unknown
        : (a.HasFlag(SignInfo.Negative) ? SignInfo.Positive : 0) | (a.HasFlag(SignInfo.Positive) ? SignInfo.Negative : 0) | (a & SignInfo.Zero);

    public static SignInfo Add(SignInfo a, SignInfo b)
    {
        if (Opaque(a, b)) return SignInfo.Unknown;
        var r = SignInfo.None;
        foreach (var x in Parts(a))
        {
            foreach (var y in Parts(b))
            {
                r |= (x, y) switch
                {
                    (SignInfo.Negative, SignInfo.Negative) or (SignInfo.Negative, SignInfo.Zero) or (SignInfo.Zero, SignInfo.Negative) => SignInfo.Negative,
                    (SignInfo.Positive, SignInfo.Positive) or (SignInfo.Positive, SignInfo.Zero) or (SignInfo.Zero, SignInfo.Positive) => SignInfo.Positive,
                    (SignInfo.Zero, SignInfo.Zero) => SignInfo.Zero,
                    _ => SignInfo.Real,
                };
            }
        }
        return r;
    }

    public static SignInfo Mul(SignInfo a, SignInfo b)
    {
        if (Opaque(a, b)) return SignInfo.Unknown;
        var r = SignInfo.None;
        foreach (var x in Parts(a))
        {
            foreach (var y in Parts(b))
            {
                r |= x == SignInfo.Zero || y == SignInfo.Zero ? SignInfo.Zero : x == y ? SignInfo.Positive : SignInfo.Negative;
            }
        }
        return r;
    }

    /// <summary>The sign of <c>a / b</c> at the points where <c>b ≠ 0</c>.</summary>
    public static SignInfo Div(SignInfo a, SignInfo b)
    {
        var nonZero = b & ~SignInfo.Zero;
        return (nonZero & SignInfo.Real) == SignInfo.None && !nonZero.HasFlag(SignInfo.NonReal) ? SignInfo.Unknown : Mul(a, nonZero);
    }

    public static SignInfo FromInterval(Interval<double> x)
    {
        if (x.IsEmpty) return SignInfo.Real;
        if (x.Lower > 0) return SignInfo.Positive;
        if (x.Upper < 0) return SignInfo.Negative;
        if (x.Lower == 0 && x.Upper == 0) return SignInfo.Zero;
        if (x.Lower >= 0) return SignInfo.NonNegative;
        if (x.Upper <= 0) return SignInfo.NonPositive;
        return SignInfo.Real;
    }

    public static SignInfo FromRational(BigRational x) => x.Sign < 0 ? SignInfo.Negative : x.Sign > 0 ? SignInfo.Positive : SignInfo.Zero;

    /// <summary>The signs a value may have given two independent facts about it.</summary>
    public static SignInfo Meet(SignInfo a, SignInfo b)
    {
        var m = a & b;

        // An expression that two analyses call defined nowhere stays "no information" rather than becoming a vacuous claim.
        return m == SignInfo.None ? (a | b) : m;
    }

    private static IEnumerable<SignInfo> Parts(SignInfo s)
    {
        if (s.HasFlag(SignInfo.Negative)) yield return SignInfo.Negative;
        if (s.HasFlag(SignInfo.Zero)) yield return SignInfo.Zero;
        if (s.HasFlag(SignInfo.Positive)) yield return SignInfo.Positive;
    }
}
