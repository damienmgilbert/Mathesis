using System.Collections.Immutable;
using Mathesis.Symbolics;

namespace Mathesis.Solving;

/// <summary>The shape of a <see cref="SolutionSet"/> (docs/design/04-type-system.md, "Sets and solution sets").</summary>
public enum SolutionKind : byte
{
    /// <summary>No solutions (∅).</summary>
    Empty,

    /// <summary>A finite set of points (exact, or approximations of roots that have no closed form).</summary>
    Finite,

    /// <summary>A union of intervals (inequalities).</summary>
    Intervals,

    /// <summary>Every real number (an identity).</summary>
    All,

    /// <summary>Image sets <c>{e(k) | k ∈ ℤ}</c>, possibly together with finite points (trigonometric equations).</summary>
    Image,

    /// <summary>A condition set <c>{x ∈ ℝ | f(x) = 0}</c> with the numeric roots found attached.</summary>
    Condition,

    /// <summary>A set of tuples described by parameters, such as <c>{(1 − 2t, t) | t ∈ ℝ}</c> (underdetermined systems).</summary>
    Parametric,
}

/// <summary>An image set <c>{Element | Parameter ∈ ℤ}</c>.</summary>
/// <param name="Parameter">The integer parameter (<c>k</c>).</param>
/// <param name="Element">The element as an expression in the parameter.</param>
public sealed record ImageFamily(Symbol Parameter, Expr Element);

/// <summary>One interval of an interval union, with the endpoints as exact expressions and as numbers.</summary>
/// <param name="Lower">The lower endpoint (an expression, <c>-oo</c> allowed).</param>
/// <param name="Upper">The upper endpoint.</param>
/// <param name="LowerClosed">Whether the lower endpoint belongs to the interval.</param>
/// <param name="UpperClosed">Whether the upper endpoint belongs to the interval.</param>
/// <param name="LowerValue">The lower endpoint as a number.</param>
/// <param name="UpperValue">The upper endpoint as a number.</param>
public sealed record IntervalPiece(Expr Lower, Expr Upper, bool LowerClosed, bool UpperClosed, double LowerValue, double UpperValue)
{
    /// <summary>Whether <paramref name="x"/> lies in the interval.</summary>
    public bool Contains(double x) => (LowerClosed ? x >= LowerValue : x > LowerValue) && (UpperClosed ? x <= UpperValue : x < UpperValue);
}

/// <summary>
/// The solutions of an equation, inequality or system: an expression of the set (a <see cref="SetLiteral"/>, <see cref="IntervalLiteral"/>s, unions,
/// image sets, a condition set) plus the same information in typed form for callers and tests.
/// </summary>
public sealed class SolutionSet
{
    internal SolutionSet(SolutionKind kind, Expr set)
    {
        Kind = kind;
        Set = set;
    }

    /// <summary>What kind of set this is.</summary>
    public SolutionKind Kind { get; }

    /// <summary>The set as an expression.</summary>
    public Expr Set { get; }

    /// <summary>The finite points (for <see cref="SolutionKind.Finite"/> and the point part of <see cref="SolutionKind.Image"/>); tuples for systems.</summary>
    public ImmutableArray<Expr> Points { get; internal init; } = [];

    /// <summary>The image families of a trigonometric solution.</summary>
    public ImmutableArray<ImageFamily> Families { get; internal init; } = [];

    /// <summary>The pieces of an interval union.</summary>
    public ImmutableArray<IntervalPiece> Pieces { get; internal init; } = [];

    /// <summary>The numeric roots attached to a condition set.</summary>
    public ImmutableArray<double> Approximations { get; internal init; } = [];

    /// <summary>The parameters of a <see cref="SolutionKind.Parametric"/> set.</summary>
    public ImmutableArray<Symbol> Parameters { get; internal init; } = [];

    /// <summary>The unknowns, in order, for the tuples of a system.</summary>
    public ImmutableArray<Symbol> Variables { get; internal init; } = [];

    /// <summary>Whether some elements are numeric approximations of roots without a closed form (<see cref="Float"/> literals).</summary>
    public bool IsApproximate { get; internal init; }

    /// <summary>Whether the set is known to be complete (a condition set lists only the roots found in a search window).</summary>
    public bool IsComplete { get; internal init; } = true;

    /// <summary>The empty set.</summary>
    public static SolutionSet Empty { get; } = new(SolutionKind.Empty, new Constant(ConstantId.EmptySet));

    /// <summary>All real numbers.</summary>
    public static SolutionSet All { get; } = new(SolutionKind.All, new Constant(ConstantId.Reals));

    /// <inheritdoc />
    public override string ToString() => Set.ToString() ?? string.Empty;
}
