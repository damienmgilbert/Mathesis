using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Canonical;

/// <summary>
/// The total order used to sort commutative operands, following Joel S. Cohen's ordering rules for automatically simplified
/// expressions (<i>Computer Algebra and Symbolic Computation: Elementary Algorithms</i>, 2002): numbers first, then constants and
/// symbols alphabetically, then applications by operator and arguments. Powers compare base then exponent; sums and products
/// compare their operands from the last to the first; a symbol or function compared with a sum, product or power is treated as
/// that operator applied to the single operand (so <c>x &lt; x^2 &lt; y</c> and <c>x &lt; 2x</c>). Two expressions compare equal
/// only when they are structurally equal.
/// </summary>
public sealed class ExprOrder : IComparer<Expr>
{
    /// <summary>The shared instance.</summary>
    public static ExprOrder Instance { get; } = new();

    private ExprOrder()
    {
    }

    private static bool IsNumeric(Expr e) => e is Number or Float;

    private static double Value(Expr e) => e is Number n ? (double)n.Value : ((Float)e).Value;

    // Rank of the kind of leaf or composite, for expressions that are not compared structurally by operator.
    private static int Rank(Expr e) => e switch
    {
        Number or Float => 0,
        Constant => 1,
        Symbol => 2,
        Wild => 3,
        Apply a when a.Operator == Operators.Pow => 4,
        Apply a when a.Operator == Operators.Mul => 5,
        Apply a when a.Operator == Operators.Add => 6,
        Apply => 7,
        Bind => 8,
        MatrixLiteral => 9,
        SetLiteral => 10,
        IntervalLiteral => 11,
        TupleLiteral => 12,
        Piecewise => 13,
        _ => 14,
    };

    /// <inheritdoc />
    public int Compare(Expr? x, Expr? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        if (x.Equals(y)) return 0;
        var c = CompareCore(x, y);
        if (c != 0) return c;

        // Distinct expressions that tie (for example the same symbol name with different sorts): break the tie deterministically.
        c = x.StructuralHash.CompareTo(y.StructuralHash);
        return c != 0 ? c : string.CompareOrdinal(x.ToString(), y.ToString());
    }

    private int CompareCore(Expr a, Expr b)
    {
        if (IsNumeric(a) && IsNumeric(b))
        {
            if (a is Number na && b is Number nb) return na.Value.CompareTo(nb.Value);
            var c = Value(a).CompareTo(Value(b));
            return c != 0 ? c : (a is Number ? -1 : b is Number ? 1 : 0);
        }
        if (IsNumeric(a)) return -1;
        if (IsNumeric(b)) return 1;

        switch (a, b)
        {
            case (Constant ca, Constant cb):
                return ((int)ca.Id).CompareTo((int)cb.Id);
            case (Symbol sa, Symbol sb):
                var byName = string.Compare(sa.Name, sb.Name, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                byName = string.CompareOrdinal(sa.Name, sb.Name);
                return byName != 0 ? byName : string.CompareOrdinal(sa.DeclaredSort.ToString(), sb.DeclaredSort.ToString());
            case (Wild wa, Wild wb):
                return string.CompareOrdinal(wa.Name, wb.Name);
        }

        // Cohen's hierarchy (rules O-8 to O-12): of two algebraic expressions of different kinds, the one built from the higher
        // operator (product > power > sum > function or symbol) decides: both are viewed as that operator applied to operands.
        var oa = Compound(a);
        var ob = Compound(b);
        if ((oa is not null || ob is not null) && Rank(a) <= 7 && Rank(b) <= 7)
        {
            var op = Level(oa) >= Level(ob) ? oa! : ob!;
            return CompareOperands(op, Operands(op, a), Operands(op, b));
        }

        var ra = Rank(a);
        var rb = Rank(b);
        if (ra != rb) return ra.CompareTo(rb);

        // Same rank: applications of the same non-compound operator compare arguments; others by kind-specific data.
        switch (a, b)
        {
            case (Apply xa, Apply xb):
                var byOp = string.CompareOrdinal(xa.Operator.Id, xb.Operator.Id);
                return byOp != 0 ? byOp : CompareLists(xa.Arguments, xb.Arguments, fromEnd: false);
            case (Bind ba, Bind bb):
                var byBinder = ((int)ba.Binder).CompareTo((int)bb.Binder);
                return byBinder != 0 ? byBinder : CompareLists(ba.Children, bb.Children, fromEnd: false);
            case (MatrixLiteral ma, MatrixLiteral mb):
                var byRows = ma.Rows.CompareTo(mb.Rows);
                if (byRows != 0) return byRows;
                var byColumns = ma.Columns.CompareTo(mb.Columns);
                return byColumns != 0 ? byColumns : CompareLists(ma.Entries, mb.Entries, fromEnd: false);
            default:
                return CompareLists(a.Children, b.Children, fromEnd: false);
        }
    }

    private static int Level(Operator? op) => op is null ? 0 : op == Operators.Mul ? 3 : op == Operators.Pow ? 2 : 1;

    private static Operator? Compound(Expr e) =>
        e is Apply a && (a.Operator == Operators.Pow || a.Operator == Operators.Mul || a.Operator == Operators.Add) ? a.Operator : null;

    // The operands of e viewed as an application of op: a symbol or function is the single operand of a sum or product,
    // and the base of a power with exponent 1.
    private static ImmutableArray<Expr> Operands(Operator op, Expr e)
    {
        if (e is Apply a && a.Operator == op) return a.Arguments;
        return op == Operators.Pow ? [e, new Number(1)] : [e];
    }

    private int CompareOperands(Operator op, ImmutableArray<Expr> xa, ImmutableArray<Expr> xb)
    {
        if (op == Operators.Pow)
        {
            var c = Compare(xa[0], xb[0]);
            return c != 0 ? c : Compare(xa[1], xb[1]);
        }

        // Sums and products compare from the last operand backwards; a shorter list sorts first.
        return CompareLists(xa, xb, fromEnd: true);
    }

    private int CompareLists(ImmutableArray<Expr> a, ImmutableArray<Expr> b, bool fromEnd)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var c = fromEnd ? Compare(a[a.Length - 1 - i], b[b.Length - 1 - i]) : Compare(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }
}
