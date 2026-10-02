using System.Collections.Immutable;

namespace Mathesis.Symbolics;

/// <summary>Structural, α-invariant equality and hashing of expressions.</summary>
internal static class ExprEquality
{
    /// <summary>The binders enclosing a position, mapping each bound symbol to its nesting level.</summary>
    internal readonly struct Scope
    {
        private sealed record Node(Symbol Symbol, int Level, Node? Parent);

        private readonly Node? _head;
        private readonly int _count;

        private Scope(Node? head, int count)
        {
            _head = head;
            _count = count;
        }

        public static Scope Empty => default;

        public bool IsEmpty => _head is null;

        public Scope Push(ImmutableArray<Symbol> symbols)
        {
            var head = _head;
            var count = _count;
            foreach (var s in symbols) head = new Node(s, count++, head);
            return new Scope(head, count);
        }

        /// <summary>The level of the innermost binding of <paramref name="symbol"/>, or −1 when it is free here.</summary>
        public int Level(Symbol symbol)
        {
            for (var n = _head; n is not null; n = n.Parent)
            {
                if (n.Symbol.Name == symbol.Name && n.Symbol.DeclaredSort.Equals(symbol.DeclaredSort)) return n.Level;
            }
            return -1;
        }
    }

    public static bool Equal(Expr a, Expr b, Scope sa, Scope sb)
    {
        if (ReferenceEquals(a, b) && sa.IsEmpty && sb.IsEmpty) return true;
        if (a.Kind != b.Kind) return false;
        switch (a)
        {
            case Number x:
                return x.Value == ((Number)b).Value;
            case Float x:
                var f = (Float)b;
                return x.Value.Equals(f.Value) && x.PrecisionBits == f.PrecisionBits;
            case Symbol x:
                var y = (Symbol)b;
                var la = sa.Level(x);
                var lb = sb.Level(y);
                return la >= 0 || lb >= 0 ? la == lb : x.Name == y.Name && x.DeclaredSort.Equals(y.DeclaredSort);
            case Constant x:
                return x.Id == ((Constant)b).Id;
            case Apply x:
                var ay = (Apply)b;
                return x.Operator.Equals(ay.Operator) && EqualLists(x.Arguments, ay.Arguments, sa, sb);
            case Bind x:
                var by = (Bind)b;
                if (x.Binder != by.Binder || x.Bound.Length != by.Bound.Length || x.Data.Length != by.Data.Length) return false;
                for (var i = 0; i < x.Bound.Length; i++)
                {
                    if (!x.Bound[i].DeclaredSort.Equals(by.Bound[i].DeclaredSort)) return false;
                }
                return EqualLists(x.Data, by.Data, sa, sb) && Equal(x.Body, by.Body, sa.Push(x.Bound), sb.Push(by.Bound));
            case MatrixLiteral x:
                var my = (MatrixLiteral)b;
                return x.Rows == my.Rows && x.Columns == my.Columns && EqualLists(x.Entries, my.Entries, sa, sb);
            case SetLiteral x:
                return EqualLists(x.Elements, ((SetLiteral)b).Elements, sa, sb);
            case IntervalLiteral x:
                var iy = (IntervalLiteral)b;
                return x.LowerClosed == iy.LowerClosed && x.UpperClosed == iy.UpperClosed && Equal(x.Lower, iy.Lower, sa, sb) && Equal(x.Upper, iy.Upper, sa, sb);
            case TupleLiteral x:
                return EqualLists(x.Elements, ((TupleLiteral)b).Elements, sa, sb);
            case Piecewise x:
                var py = (Piecewise)b;
                if (x.Cases.Length != py.Cases.Length) return false;
                for (var i = 0; i < x.Cases.Length; i++)
                {
                    if (!Equal(x.Cases[i].Value, py.Cases[i].Value, sa, sb) || !Equal(x.Cases[i].Condition, py.Cases[i].Condition, sa, sb)) return false;
                }
                return true;
            case Wild x:
                var wy = (Wild)b;
                return x.Name == wy.Name && (x.Constraint is null ? wy.Constraint is null : wy.Constraint is not null && Equal(x.Constraint, wy.Constraint, sa, sb));
            default:
                return false;
        }
    }

    private static bool EqualLists(ImmutableArray<Expr> a, ImmutableArray<Expr> b, Scope sa, Scope sb)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!Equal(a[i], b[i], sa, sb)) return false;
        }
        return true;
    }

    public static int Hash(Expr e, Scope scope)
    {
        var h = new HashCode();
        h.Add((int)e.Kind);
        switch (e)
        {
            case Number x:
                h.Add(x.Value);
                break;
            case Float x:
                h.Add(x.Value);
                h.Add(x.PrecisionBits);
                break;
            case Symbol x:
                var level = scope.Level(x);
                if (level >= 0)
                {
                    h.Add(-1);
                    h.Add(level);
                }
                else
                {
                    h.Add(x.Name);
                    h.Add(x.DeclaredSort);
                }
                break;
            case Constant x:
                h.Add((int)x.Id);
                break;
            case Apply x:
                h.Add(x.Operator.Id);
                foreach (var a in x.Arguments) h.Add(ChildHash(a, scope));
                break;
            case Bind x:
                h.Add((int)x.Binder);
                h.Add(x.Bound.Length);
                foreach (var d in x.Data) h.Add(ChildHash(d, scope));
                h.Add(ChildHash(x.Body, scope.Push(x.Bound)));
                break;
            case MatrixLiteral x:
                h.Add(x.Rows);
                h.Add(x.Columns);
                foreach (var a in x.Entries) h.Add(ChildHash(a, scope));
                break;
            case IntervalLiteral x:
                h.Add(x.LowerClosed);
                h.Add(x.UpperClosed);
                h.Add(ChildHash(x.Lower, scope));
                h.Add(ChildHash(x.Upper, scope));
                break;
            case Wild x:
                h.Add(x.Name);
                if (x.Constraint is not null) h.Add(ChildHash(x.Constraint, scope));
                break;
            default:
                foreach (var c in e.Children) h.Add(ChildHash(c, scope));
                break;
        }
        return h.ToHashCode();
    }

    // A child's cached hash is valid unless a symbol bound by an enclosing binder occurs free in it.
    private static int ChildHash(Expr child, Scope scope)
    {
        if (scope.IsEmpty) return child.StructuralHash;
        foreach (var s in child.FreeSymbols)
        {
            if (scope.Level(s) >= 0) return Hash(child, scope);
        }
        return child.StructuralHash;
    }
}
