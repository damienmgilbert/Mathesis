using System.Collections.Immutable;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>A real number known exactly (as an expression) and numerically (for ordering), or ±∞.</summary>
internal readonly record struct Endpoint(Expr Value, double Approx)
{
    public static Endpoint NegativeInfinity { get; } = new(new Constant(ConstantId.NegativeInfinity), double.NegativeInfinity);

    public static Endpoint PositiveInfinity { get; } = new(new Constant(ConstantId.PositiveInfinity), double.PositiveInfinity);

    public bool IsInfinite => double.IsInfinity(Approx);

    /// <summary>Orders endpoints: structurally equal values tie; otherwise the numeric approximations decide.</summary>
    public static int Compare(Endpoint a, Endpoint b) => a.Value.Equals(b.Value) ? 0 : a.Approx.CompareTo(b.Approx);
}

/// <summary>A connected piece of the real line: an interval or a single point (<c>Lower == Upper</c>, both closed).</summary>
internal readonly record struct Piece(Endpoint Lower, bool LowerClosed, Endpoint Upper, bool UpperClosed)
{
    public bool IsPoint => Endpoint.Compare(Lower, Upper) == 0;
}

/// <summary>
/// A subset of ℝ as a sorted union of disjoint pieces with exact endpoints. Used to compute natural domains: every constraint of the
/// form <c>h &gt; 0</c>, <c>h ≥ 0</c> or <c>h ≠ 0</c> for a rational function <c>h</c> becomes a <see cref="RealSet"/>, and the domain is their
/// intersection.
/// </summary>
internal sealed class RealSet
{
    private RealSet(ImmutableArray<Piece> pieces) => Pieces = pieces;

    public ImmutableArray<Piece> Pieces { get; }

    public static RealSet Empty { get; } = new([]);

    public static RealSet All { get; } = new([new Piece(Endpoint.NegativeInfinity, false, Endpoint.PositiveInfinity, false)]);

    public bool IsEmpty => Pieces.Length == 0;

    public bool IsAll => Pieces.Length == 1 && Pieces[0].Lower.IsInfinite && Pieces[0].Upper.IsInfinite;

    /// <summary>Builds a set from pieces in any order, dropping empty ones and merging those that overlap or touch.</summary>
    public static RealSet From(IEnumerable<Piece> pieces)
    {
        var list = pieces.Where(NonEmpty).ToList();
        list.Sort((a, b) =>
        {
            var c = Endpoint.Compare(a.Lower, b.Lower);
            return c != 0 ? c : a.LowerClosed == b.LowerClosed ? 0 : a.LowerClosed ? -1 : 1;
        });
        var merged = new List<Piece>();
        foreach (var p in list)
        {
            if (merged.Count > 0)
            {
                var last = merged[^1];
                var c = Endpoint.Compare(p.Lower, last.Upper);
                if (c < 0 || (c == 0 && (p.LowerClosed || last.UpperClosed)))
                {
                    var upper = Endpoint.Compare(p.Upper, last.Upper);
                    merged[^1] = upper > 0 ? last with { Upper = p.Upper, UpperClosed = p.UpperClosed }
                        : upper == 0 ? last with { UpperClosed = last.UpperClosed || p.UpperClosed }
                        : last;
                    continue;
                }
            }
            merged.Add(p);
        }
        return new RealSet([.. merged]);
    }

    private static bool NonEmpty(Piece p)
    {
        var c = Endpoint.Compare(p.Lower, p.Upper);
        return c < 0 || (c == 0 && p.LowerClosed && p.UpperClosed);
    }

    public RealSet Intersect(RealSet other)
    {
        var result = new List<Piece>();
        foreach (var a in Pieces)
        {
            foreach (var b in other.Pieces)
            {
                var lc = Endpoint.Compare(a.Lower, b.Lower);
                var lower = lc >= 0 ? a.Lower : b.Lower;
                var lowerClosed = lc > 0 ? a.LowerClosed : lc < 0 ? b.LowerClosed : a.LowerClosed && b.LowerClosed;
                var uc = Endpoint.Compare(a.Upper, b.Upper);
                var upper = uc <= 0 ? a.Upper : b.Upper;
                var upperClosed = uc < 0 ? a.UpperClosed : uc > 0 ? b.UpperClosed : a.UpperClosed && b.UpperClosed;
                result.Add(new Piece(lower, lowerClosed, upper, upperClosed));
            }
        }
        return From(result);
    }

    /// <summary>The set as an expression: ℝ, ∅, intervals, finite point sets and unions of them.</summary>
    public Expr ToExpr()
    {
        if (IsEmpty) return new Constant(ConstantId.EmptySet);
        if (IsAll) return new Constant(ConstantId.Reals);
        var parts = new List<Expr>();
        var points = new List<Expr>();
        foreach (var p in Pieces)
        {
            if (p.IsPoint) points.Add(p.Lower.Value);
            else parts.Add(new IntervalLiteral(p.Lower.Value, p.Upper.Value, p.LowerClosed, p.UpperClosed));
        }
        if (points.Count > 0) parts.Add(new SetLiteral([.. points]));
        return parts.Count == 1 ? parts[0] : new Apply(Operators.Union, [.. parts]);
    }
}
