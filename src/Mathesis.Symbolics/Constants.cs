namespace Mathesis.Symbolics;

/// <summary>The built-in constants (docs/design/05-syntax-trees-and-notation.md, "Constants").</summary>
public enum ConstantId : byte
{
    /// <summary>π.</summary>
    Pi,

    /// <summary>Euler's number e.</summary>
    E,

    /// <summary>The imaginary unit, written <c>I</c> by default so that <c>i</c> stays free for indices.</summary>
    ImaginaryUnit,

    /// <summary>The golden ratio.</summary>
    GoldenRatio,

    /// <summary>The Euler–Mascheroni constant γ.</summary>
    EulerGamma,

    /// <summary>Catalan's constant.</summary>
    CatalanG,

    /// <summary>+∞ on the extended real line.</summary>
    PositiveInfinity,

    /// <summary>−∞ on the extended real line.</summary>
    NegativeInfinity,

    /// <summary>Complex infinity (complex mode).</summary>
    ComplexInfinity,

    /// <summary>The result of <c>0/0</c> and similar; propagates.</summary>
    Undefined,

    /// <summary>The Boolean true.</summary>
    True,

    /// <summary>The Boolean false.</summary>
    False,

    /// <summary>The empty set ∅.</summary>
    EmptySet,

    /// <summary>ℕ, including 0.</summary>
    Naturals,

    /// <summary>ℤ.</summary>
    Integers,

    /// <summary>ℚ.</summary>
    Rationals,

    /// <summary>ℝ.</summary>
    Reals,

    /// <summary>ℂ.</summary>
    Complexes,
}

/// <summary>Display names and the sort of a built-in constant.</summary>
/// <param name="Text">The ASCII input and output spelling.</param>
/// <param name="Unicode">The Unicode spelling used by printers with Unicode symbols on.</param>
/// <param name="Latex">The LaTeX spelling.</param>
/// <param name="Sort">The constant's sort.</param>
public sealed record ConstantInfo(string Text, string Unicode, string Latex, Sort Sort);

/// <summary>Lookup of constant metadata.</summary>
public static class Constants
{
    private static readonly ConstantInfo[] Table = BuildTable();

    private static ConstantInfo[] BuildTable()
    {
        var table = new ConstantInfo[Enum.GetValues<ConstantId>().Length];
        table[(int)ConstantId.Pi] = new("pi", "π", "\\pi", Sort.Real);
        table[(int)ConstantId.E] = new("e", "e", "e", Sort.Real);
        table[(int)ConstantId.ImaginaryUnit] = new("I", "ⅈ", "\\mathrm{i}", Sort.Complex);
        table[(int)ConstantId.GoldenRatio] = new("GoldenRatio", "φ", "\\varphi", Sort.Real);
        table[(int)ConstantId.EulerGamma] = new("EulerGamma", "γ", "\\gamma", Sort.Real);
        table[(int)ConstantId.CatalanG] = new("CatalanG", "G", "G", Sort.Real);
        table[(int)ConstantId.PositiveInfinity] = new("oo", "∞", "\\infty", Sort.ExtendedReal);
        table[(int)ConstantId.NegativeInfinity] = new("-oo", "−∞", "-\\infty", Sort.ExtendedReal);
        table[(int)ConstantId.ComplexInfinity] = new("zoo", "zoo", "\\tilde{\\infty}", Sort.Number);
        table[(int)ConstantId.Undefined] = new("undefined", "undefined", "\\operatorname{undefined}", Sort.Any);
        table[(int)ConstantId.True] = new("true", "true", "\\mathrm{true}", Sort.Boolean);
        table[(int)ConstantId.False] = new("false", "false", "\\mathrm{false}", Sort.Boolean);
        table[(int)ConstantId.EmptySet] = new("EmptySet", "∅", "\\varnothing", Sort.SetOf(Sort.Any));
        table[(int)ConstantId.Naturals] = new("N", "ℕ", "\\mathbb{N}", Sort.SetOf(Sort.Natural));
        table[(int)ConstantId.Integers] = new("Z", "ℤ", "\\mathbb{Z}", Sort.SetOf(Sort.Integer));
        table[(int)ConstantId.Rationals] = new("Q", "ℚ", "\\mathbb{Q}", Sort.SetOf(Sort.Rational));
        table[(int)ConstantId.Reals] = new("R", "ℝ", "\\mathbb{R}", Sort.SetOf(Sort.Real));
        table[(int)ConstantId.Complexes] = new("C", "ℂ", "\\mathbb{C}", Sort.SetOf(Sort.Complex));
        return table;
    }

    /// <summary>The metadata of a constant.</summary>
    public static ConstantInfo Info(ConstantId id) => Table[(int)id];

    /// <summary>Whether the constant is one of the number sets or the empty set.</summary>
    public static bool IsSet(ConstantId id) => id is ConstantId.EmptySet or ConstantId.Naturals or ConstantId.Integers or ConstantId.Rationals or ConstantId.Reals or ConstantId.Complexes;
}

/// <summary>The kinds of binder (docs/design/05-syntax-trees-and-notation.md, "Binders").</summary>
public enum Binder : byte
{
    /// <summary>∑: bound k, data a, b.</summary>
    Sum,

    /// <summary>∏: bound k, data a, b.</summary>
    Product,

    /// <summary>A definite or multiple integral: bound variables, data are the (lower, upper) bounds of each, inner variable first.</summary>
    Integral,

    /// <summary>A limit: bound x, data point and optionally a direction (<c>"+"</c> or <c>"-"</c> as a symbol-free constant string).</summary>
    Limit,

    /// <summary>∀: bound x, data the domain.</summary>
    ForAll,

    /// <summary>∃: bound x, data the domain.</summary>
    Exists,

    /// <summary>∃!: bound x, data the domain.</summary>
    ExistsUnique,

    /// <summary>λ: bound x, no data.</summary>
    Lambda,

    /// <summary>{x ∈ S | P}: bound x, data the domain, body the predicate.</summary>
    SetBuilder,

    /// <summary>{f | k ∈ ℤ}: bound k, data the domain, body the element expression.</summary>
    ImageSet,

    /// <summary>⋃: bound k, data a, b.</summary>
    IndexedUnion,

    /// <summary>⋂: bound k, data a, b.</summary>
    IndexedIntersection,

    /// <summary>Laplace transform: bound t, data s.</summary>
    Laplace,

    /// <summary>Fourier transform: bound t, data ω.</summary>
    Fourier,

    /// <summary>argmin over a domain.</summary>
    ArgMin,

    /// <summary>argmax over a domain.</summary>
    ArgMax,
}

/// <summary>Helpers on <see cref="Binder"/>.</summary>
public static class BinderExtensions
{
    /// <summary>The operator family a binder belongs to, for the <see cref="Expr.Families"/> mask.</summary>
    public static OperatorFamily Family(this Binder binder) => binder switch
    {
        Binder.ForAll or Binder.Exists or Binder.ExistsUnique => OperatorFamily.Logic,
        Binder.SetBuilder or Binder.ImageSet or Binder.IndexedUnion or Binder.IndexedIntersection => OperatorFamily.Set,
        Binder.Lambda => OperatorFamily.Function,
        _ => OperatorFamily.Calculus,
    };
}
