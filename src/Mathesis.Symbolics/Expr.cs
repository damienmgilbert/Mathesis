using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Printing;
using System.Text.Json.Serialization;
using Mathesis.Symbolics.Serialization;

namespace Mathesis.Symbolics;

/// <summary>The twelve node kinds of the expression tree (docs/design/05-syntax-trees-and-notation.md, "Node kinds").</summary>
public enum ExprKind : byte
{
    /// <summary>An exact rational number.</summary>
    Number,

    /// <summary>An approximate (floating-point) number.</summary>
    Float,

    /// <summary>A named variable with a declared sort.</summary>
    Symbol,

    /// <summary>A built-in constant such as π or ∞.</summary>
    Constant,

    /// <summary>An operator applied to ordered arguments.</summary>
    Apply,

    /// <summary>A binder (sum, integral, quantifier, lambda, …) with bound symbols.</summary>
    Bind,

    /// <summary>A matrix literal.</summary>
    Matrix,

    /// <summary>A set literal.</summary>
    Set,

    /// <summary>An interval literal.</summary>
    Interval,

    /// <summary>A tuple literal.</summary>
    Tuple,

    /// <summary>A piecewise definition.</summary>
    Piecewise,

    /// <summary>A pattern variable (patterns only).</summary>
    Wild,
}

/// <summary>
/// An immutable mathematical expression. Every node caches its structural hash, leaf count, depth, free symbols, operator
/// families and inferred sort on first use. Equality is structural; <c>==</c> and <c>!=</c> compare structure and never build
/// equations (use <see cref="Sym.Eq"/>), and <c>^</c> is deliberately not overloaded because it binds more loosely than
/// <c>+</c> in C# (use <see cref="Sym.Pow"/>).
/// </summary>
/// <remarks>
/// Bound symbols are local: expressions that differ only in the names of bound symbols are equal and hash alike (α-equivalence).
/// Number display hints do not affect equality, so <c>0.25</c> and <c>1/4</c> as numbers are equal.
/// </remarks>
[JsonConverter(typeof(ExprJsonConverter))]
public abstract partial class Expr : IMathObject, IEquatable<Expr>
{
    private int _hash;
    private int _leafCount;
    private int _depth;
    private ImmutableHashSet<Symbol>? _freeSymbols;
    private long _families = -1;
    private Sort? _sort;

    private protected Expr()
    {
    }

    /// <summary>The kind of this node.</summary>
    public abstract ExprKind Kind { get; }

    /// <summary>
    /// The child expressions, in the order <see cref="ExprPath"/> indexes them: arguments of an application; for a binder the
    /// data expressions then the body; entries row-major; elements; lower and upper bound; piecewise value/condition pairs
    /// interleaved; a wild's constraint if present.
    /// </summary>
    public abstract ImmutableArray<Expr> Children { get; }

    /// <summary>Returns a node of the same kind and data with the given children, or this node if they are identical.</summary>
    /// <exception cref="ArgumentException">The number of children does not match.</exception>
    public abstract Expr WithChildren(ImmutableArray<Expr> children);

    /// <summary>The α-invariant structural hash.</summary>
    public int StructuralHash
    {
        get
        {
            var h = _hash;
            if (h == 0)
            {
                h = ExprEquality.Hash(this, ExprEquality.Scope.Empty);
                if (h == 0) h = 1;
                _hash = h;
            }
            return h;
        }
    }

    /// <summary>The number of leaf nodes (numbers, floats, symbols, constants, wilds).</summary>
    public int LeafCount
    {
        get
        {
            var n = _leafCount;
            if (n == 0)
            {
                var children = Children;
                n = children.Length == 0 ? 1 : Math.Max(1, children.Sum(c => c.LeafCount));
                _leafCount = n;
            }
            return n;
        }
    }

    /// <summary>The height of the tree: 1 for a leaf.</summary>
    public int Depth
    {
        get
        {
            var d = _depth;
            if (d == 0)
            {
                var children = Children;
                d = children.Length == 0 ? 1 : 1 + children.Max(c => c.Depth);
                _depth = d;
            }
            return d;
        }
    }

    /// <summary>The symbols occurring free (not bound by an enclosing binder).</summary>
    public ImmutableHashSet<Symbol> FreeSymbols => _freeSymbols ??= ComputeFreeSymbols();

    private ImmutableHashSet<Symbol> ComputeFreeSymbols()
    {
        switch (this)
        {
            case Symbol s:
                return [s];
            case Bind b:
                var free = ImmutableHashSet.CreateBuilder<Symbol>();
                foreach (var child in b.Children) free.UnionWith(child.FreeSymbols);
                free.ExceptWith(b.Bound);
                return free.ToImmutable();
            default:
                var set = ImmutableHashSet.CreateBuilder<Symbol>();
                foreach (var child in Children) set.UnionWith(child.FreeSymbols);
                return set.ToImmutable();
        }
    }

    /// <summary>The operator families occurring anywhere in this expression, used to skip irrelevant rules quickly.</summary>
    public OperatorFamily Families
    {
        get
        {
            var f = _families;
            if (f < 0)
            {
                var own = this switch
                {
                    Apply a => a.Operator.Family,
                    Bind b => b.Binder.Family(),
                    MatrixLiteral => OperatorFamily.LinearAlgebra,
                    SetLiteral or IntervalLiteral => OperatorFamily.Set,
                    _ => OperatorFamily.None,
                };
                foreach (var child in Children) own |= child.Families;
                f = (long)own;
                _families = f;
            }
            return (OperatorFamily)f;
        }
    }

    /// <summary>The sort inferred by <see cref="SortChecker"/>; <see cref="Sort.Any"/> where a mismatch was found.</summary>
    public Sort Sort => _sort ??= SortChecker.Check(this).Sort;

    /// <summary>The subexpression at <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentException">The path does not address a node of this expression.</exception>
    public Expr At(ExprPath path) => path.Get(this);

    /// <summary>This expression with the subexpression at <paramref name="path"/> replaced.</summary>
    public Expr ReplaceAt(ExprPath path, Expr replacement) => path.Replace(this, replacement);

    /// <summary>Visits every subexpression (this one first, then children depth-first) together with its path.</summary>
    public IEnumerable<(Expr Expr, ExprPath Path)> Walk()
    {
        var stack = new Stack<(Expr, ExprPath)>();
        stack.Push((this, ExprPath.Root));
        while (stack.Count > 0)
        {
            var (e, p) = stack.Pop();
            yield return (e, p);
            var children = e.Children;
            for (var i = children.Length - 1; i >= 0; i--) stack.Push((children[i], p.Child(i)));
        }
    }

    /// <summary>Rebuilds the tree bottom-up, applying <paramref name="rewrite"/> to every node after its children were rewritten.</summary>
    /// <param name="rewrite">Returns the replacement for a node, or <c>null</c> to keep it.</param>
    public Expr Transform(Func<Expr, Expr?> rewrite)
    {
        ArgumentNullException.ThrowIfNull(rewrite);
        var children = Children;
        var changed = false;
        var rebuilt = new Expr[children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            rebuilt[i] = children[i].Transform(rewrite);
            changed |= !ReferenceEquals(rebuilt[i], children[i]);
        }
        var node = changed ? WithChildren([.. rebuilt]) : this;
        return rewrite(node) ?? node;
    }

    /// <summary>Whether any subexpression (including this one) equals <paramref name="target"/>.</summary>
    public bool Contains(Expr target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Walk().Any(w => w.Expr.Equals(target));
    }

    /// <summary>Substitutes <paramref name="replacement"/> for every free occurrence of <paramref name="symbol"/>.</summary>
    public Expr Substitute(Symbol symbol, Expr replacement)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(replacement);
        return Subst(this, symbol, replacement);

        static Expr Subst(Expr e, Symbol s, Expr r)
        {
            if (e is Symbol sym) return sym.Equals(s) ? r : e;
            if (e is Bind b && b.Bound.Contains(s))
            {
                // The symbol is shadowed in the body, but the data expressions are outside the binder's scope.
                var data = b.Data.Select(d => Subst(d, s, r)).ToImmutableArray();
                return data.AsSpan().SequenceEqual(b.Data.AsSpan()) ? e : new Bind(b.Binder, b.Bound, data, b.Body);
            }
            var children = e.Children;
            var rebuilt = new Expr[children.Length];
            var changed = false;
            for (var i = 0; i < children.Length; i++)
            {
                rebuilt[i] = Subst(children[i], s, r);
                changed |= !ReferenceEquals(rebuilt[i], children[i]);
            }
            return changed ? e.WithChildren([.. rebuilt]) : e;
        }
    }

    // ----- Equality -----

    /// <inheritdoc />
    public bool Equals(Expr? other) =>
        other is not null && (ReferenceEquals(this, other) || (Kind == other.Kind && StructuralHash == other.StructuralHash && ExprEquality.Equal(this, other, ExprEquality.Scope.Empty, ExprEquality.Scope.Empty)));

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) => Equals(obj as Expr);

    /// <inheritdoc />
    public sealed override int GetHashCode() => StructuralHash;

    /// <summary>Structural equality; does not build an equation.</summary>
    public static bool operator ==(Expr? left, Expr? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(Expr? left, Expr? right) => !(left == right);

    // ----- Arithmetic, relations and logic builders -----

    /// <summary>Builds <c>add(left, right)</c>.</summary>
    public static Expr operator +(Expr left, Expr right) => Sym.Add(left, right);

    /// <summary>Builds <c>sub(left, right)</c>.</summary>
    public static Expr operator -(Expr left, Expr right) => Sym.Sub(left, right);

    /// <summary>Builds <c>mul(left, right)</c>.</summary>
    public static Expr operator *(Expr left, Expr right) => Sym.Mul(left, right);

    /// <summary>Builds <c>div(left, right)</c>.</summary>
    public static Expr operator /(Expr left, Expr right) => Sym.Div(left, right);

    /// <summary>Builds <c>neg(value)</c>.</summary>
    public static Expr operator -(Expr value) => Sym.Neg(value);

    /// <summary>Builds the relation <c>lt(left, right)</c>.</summary>
    public static Expr operator <(Expr left, Expr right) => Sym.Lt(left, right);

    /// <summary>Builds the relation <c>gt(left, right)</c>.</summary>
    public static Expr operator >(Expr left, Expr right) => Sym.Gt(left, right);

    /// <summary>Builds the relation <c>le(left, right)</c>.</summary>
    public static Expr operator <=(Expr left, Expr right) => Sym.Le(left, right);

    /// <summary>Builds the relation <c>ge(left, right)</c>.</summary>
    public static Expr operator >=(Expr left, Expr right) => Sym.Ge(left, right);

    /// <summary>Builds the logical conjunction <c>and(left, right)</c>.</summary>
    public static Expr operator &(Expr left, Expr right) => Sym.And(left, right);

    /// <summary>Builds the logical disjunction <c>or(left, right)</c>.</summary>
    public static Expr operator |(Expr left, Expr right) => Sym.Or(left, right);

    /// <summary>Builds the logical negation <c>not(value)</c>.</summary>
    public static Expr operator !(Expr value) => Sym.Not(value);

    /// <summary>An exact integer.</summary>
    public static implicit operator Expr(int value) => Sym.Number(value);

    /// <summary>An exact integer.</summary>
    public static implicit operator Expr(long value) => Sym.Number(value);

    /// <summary>An exact integer.</summary>
    public static implicit operator Expr(BigInteger value) => Sym.Number(value);

    /// <summary>An exact rational.</summary>
    public static implicit operator Expr(BigRational value) => Sym.Number(value);

    /// <summary>An approximate number: C# <see cref="double"/> values become <see cref="Float"/> nodes, never exact numbers.</summary>
    public static implicit operator Expr(double value) => new Float(value);

    // ----- Parsing and printing -----

    /// <summary>Parses linear text (docs/design/05-syntax-trees-and-notation.md, "Input notation") into a Raw tree.</summary>
    /// <exception cref="ParseException">The text is not a valid expression.</exception>
    public static Expr Parse(string text, ParserOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = Parser.Parse(text, options);
        return result.Expr ?? throw new ParseException(result.Errors[0], text);
    }

    /// <summary>Parses linear text, returning the full <see cref="ParseResult"/> (tree, errors and warnings) without throwing.</summary>
    public static ParseResult TryParse(string text, ParserOptions? options = null) => Parser.Parse(text ?? throw new ArgumentNullException(nameof(text)), options);

    /// <summary>Formats the expression as linear text that parses back to the same tree.</summary>
    public override string ToString() => TextPrinter.Print(this);

    /// <summary>Formats as LaTeX.</summary>
    public string ToLatex() => LatexPrinter.Print(this);
}

/// <summary>How a <see cref="Number"/> prefers to be displayed. The value stays exact either way.</summary>
public enum NumberDisplayKind : byte
{
    /// <summary>An integer, or a fraction <c>p/q</c>.</summary>
    Default,

    /// <summary>A decimal with a fixed number of fractional digits, as the user typed it.</summary>
    Decimal,

    /// <summary>Always <c>p/q</c>.</summary>
    Fraction,

    /// <summary>A mixed number such as <c>2 1/3</c>.</summary>
    MixedNumber,
}

/// <summary>A display hint attached to a <see cref="Number"/>; never part of its identity.</summary>
/// <param name="Kind">The display style.</param>
/// <param name="Digits">For <see cref="NumberDisplayKind.Decimal"/>: the number of fractional digits.</param>
public readonly record struct NumberDisplay(NumberDisplayKind Kind, int Digits = 0)
{
    /// <summary>The default display.</summary>
    public static NumberDisplay Default => default;

    /// <summary>A decimal with the given number of fractional digits.</summary>
    public static NumberDisplay Decimal(int digits) => new(NumberDisplayKind.Decimal, digits);
}

/// <summary>An exact rational number. Decimal literals are exact: <c>0.1</c> is 1/10 with a decimal display hint.</summary>
public sealed class Number : Expr
{
    /// <summary>Creates a number.</summary>
    public Number(BigRational value, NumberDisplay display = default)
    {
        Value = value;
        Display = display;
    }

    /// <summary>The exact value.</summary>
    public BigRational Value { get; }

    /// <summary>The display hint (ignored by equality).</summary>
    public NumberDisplay Display { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Number;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.Length == 0 ? this : throw new ArgumentException("A number has no children.", nameof(children));
}

/// <summary>An approximate number with its precision in bits. Never produced by parsing decimal literals.</summary>
public sealed class Float : Expr
{
    /// <summary>Creates a float.</summary>
    public Float(double value, int precisionBits = 53)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(precisionBits, 1);
        Value = value;
        PrecisionBits = precisionBits;
    }

    /// <summary>The approximate value.</summary>
    public double Value { get; }

    /// <summary>The precision in bits (53 for <see cref="double"/>).</summary>
    public int PrecisionBits { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Float;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.Length == 0 ? this : throw new ArgumentException("A float has no children.", nameof(children));
}

/// <summary>A named variable with a declared sort. Two symbols are equal when both name and sort are equal.</summary>
public sealed class Symbol : Expr
{
    /// <summary>Creates a symbol; the sort defaults to ℝ, the default in real mode.</summary>
    /// <exception cref="ArgumentException">The name is empty or contains characters other than letters, digits, <c>_</c> and primes.</exception>
    public Symbol(string name, Sort? sort = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!IsValidName(name)) throw new ArgumentException($"'{name}' is not a valid symbol name: use letters, digits, '_' and primes, starting with a letter or '_'.", nameof(name));
        Name = name;
        DeclaredSort = sort ?? Sort.Real;
    }

    /// <summary>The symbol's name.</summary>
    public string Name { get; }

    /// <summary>The declared sort.</summary>
    public Sort DeclaredSort { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Symbol;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.Length == 0 ? this : throw new ArgumentException("A symbol has no children.", nameof(children));

    /// <summary>Whether <paramref name="name"/> is a valid symbol name.</summary>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var first = name[0];
        if (!(char.IsLetter(first) || char.IsHighSurrogate(first) || first == '_')) return false;
        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!(char.IsLetterOrDigit(c) || char.IsSurrogate(c) || c == '_' || c == '\'' || c is >= '\u2080' and <= '\u2089')) return false;
        }
        return true;
    }
}

/// <summary>A built-in constant.</summary>
public sealed class Constant : Expr
{
    /// <summary>Creates a constant node.</summary>
    public Constant(ConstantId id)
    {
        if (!Enum.IsDefined(id)) throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
    }

    /// <summary>Which constant this is.</summary>
    public ConstantId Id { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Constant;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.Length == 0 ? this : throw new ArgumentException("A constant has no children.", nameof(children));
}

/// <summary>An operator applied to ordered arguments. The argument count must match the operator's arity.</summary>
public sealed class Apply : Expr
{
    /// <summary>Creates an application.</summary>
    /// <exception cref="ArgumentException">The argument count does not match the operator's arity.</exception>
    public Apply(Operator op, ImmutableArray<Expr> arguments)
    {
        ArgumentNullException.ThrowIfNull(op);
        if (arguments.IsDefault) throw new ArgumentNullException(nameof(arguments));
        if (!op.Arity.Accepts(arguments.Length)) throw new ArgumentException($"Operator '{op.Id}' takes {op.Arity} arguments, not {arguments.Length}.", nameof(arguments));
        foreach (var a in arguments) ArgumentNullException.ThrowIfNull(a);
        Operator = op;
        Arguments = arguments;
    }

    /// <summary>The operator.</summary>
    public Operator Operator { get; }

    /// <summary>The ordered arguments.</summary>
    public ImmutableArray<Expr> Arguments { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Apply;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => Arguments;

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.AsSpan().SequenceEqual(Arguments.AsSpan(), ReferenceEqualityComparer.Instance) ? this : new Apply(Operator, children);
}

/// <summary>A binder: sums, products, integrals, limits, quantifiers, lambdas, set builders and similar.</summary>
public sealed class Bind : Expr
{
    /// <summary>Creates a binder node.</summary>
    /// <exception cref="ArgumentException">There are no bound symbols or they repeat.</exception>
    public Bind(Binder binder, ImmutableArray<Symbol> bound, ImmutableArray<Expr> data, Expr body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (bound.IsDefaultOrEmpty) throw new ArgumentException("A binder needs at least one bound symbol.", nameof(bound));
        if (data.IsDefault) data = [];
        if (bound.Distinct().Count() != bound.Length) throw new ArgumentException("Bound symbols must be distinct.", nameof(bound));
        Binder = binder;
        Bound = bound;
        Data = data;
        Body = body;
    }

    /// <summary>The kind of binder.</summary>
    public Binder Binder { get; }

    /// <summary>The bound symbols (local to the body).</summary>
    public ImmutableArray<Symbol> Bound { get; }

    /// <summary>The binder data: bounds, domain or direction, evaluated outside the binder's scope.</summary>
    public ImmutableArray<Expr> Data { get; }

    /// <summary>The body.</summary>
    public Expr Body { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Bind;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [.. Data, Body];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children)
    {
        if (children.Length != Data.Length + 1) throw new ArgumentException("A binder has its data expressions and a body as children.", nameof(children));
        var data = children[..^1];
        return children[^1] == Body && data.AsSpan().SequenceEqual(Data.AsSpan(), ReferenceEqualityComparer.Instance)
            ? this
            : new Bind(Binder, Bound, data, children[^1]);
    }
}

/// <summary>A matrix literal with m ≥ 1 rows and n ≥ 1 columns; a column vector is m × 1.</summary>
public sealed class MatrixLiteral : Expr
{
    /// <summary>Creates a matrix from row-major entries.</summary>
    /// <exception cref="ArgumentException">The dimensions are not positive or do not match the entry count.</exception>
    public MatrixLiteral(int rows, int columns, ImmutableArray<Expr> entries)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        if (entries.IsDefault || entries.Length != rows * columns) throw new ArgumentException($"A {rows}×{columns} matrix needs {rows * columns} entries.", nameof(entries));
        Rows = rows;
        Columns = columns;
        Entries = entries;
    }

    /// <summary>The number of rows.</summary>
    public int Rows { get; }

    /// <summary>The number of columns.</summary>
    public int Columns { get; }

    /// <summary>The entries in row-major order.</summary>
    public ImmutableArray<Expr> Entries { get; }

    /// <summary>The entry in the given row and column (zero based).</summary>
    public Expr this[int row, int column] => Entries[row * Columns + column];

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Matrix;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => Entries;

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.AsSpan().SequenceEqual(Entries.AsSpan(), ReferenceEqualityComparer.Instance) ? this : new MatrixLiteral(Rows, Columns, children);
}

/// <summary>A set literal. Duplicates are removed on construction (keeping the first); sorting happens at the Canonical level.</summary>
public sealed class SetLiteral : Expr
{
    /// <summary>Creates a set, dropping structural duplicates.</summary>
    public SetLiteral(ImmutableArray<Expr> elements)
    {
        if (elements.IsDefault) throw new ArgumentNullException(nameof(elements));
        var seen = new HashSet<Expr>();
        var unique = ImmutableArray.CreateBuilder<Expr>();
        foreach (var e in elements)
        {
            ArgumentNullException.ThrowIfNull(e);
            if (seen.Add(e)) unique.Add(e);
        }
        Elements = unique.ToImmutable();
    }

    /// <summary>The distinct elements.</summary>
    public ImmutableArray<Expr> Elements { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Set;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => Elements;

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.AsSpan().SequenceEqual(Elements.AsSpan(), ReferenceEqualityComparer.Instance) ? this : new SetLiteral(children);
}

/// <summary>An interval of the real line; infinite ends are always open.</summary>
public sealed class IntervalLiteral : Expr
{
    /// <summary>Creates an interval.</summary>
    public IntervalLiteral(Expr lower, Expr upper, bool lowerClosed, bool upperClosed)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(upper);
        Lower = lower;
        Upper = upper;
        LowerClosed = lowerClosed && !IsInfinite(lower);
        UpperClosed = upperClosed && !IsInfinite(upper);
    }

    private static bool IsInfinite(Expr e) => e is Constant { Id: ConstantId.PositiveInfinity or ConstantId.NegativeInfinity };

    /// <summary>The lower end.</summary>
    public Expr Lower { get; }

    /// <summary>The upper end.</summary>
    public Expr Upper { get; }

    /// <summary>Whether the lower end belongs to the interval.</summary>
    public bool LowerClosed { get; }

    /// <summary>Whether the upper end belongs to the interval.</summary>
    public bool UpperClosed { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Interval;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [Lower, Upper];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children)
    {
        if (children.Length != 2) throw new ArgumentException("An interval has two children.", nameof(children));
        return ReferenceEquals(children[0], Lower) && ReferenceEquals(children[1], Upper) ? this : new IntervalLiteral(children[0], children[1], LowerClosed, UpperClosed);
    }
}

/// <summary>An ordered tuple: a point, an ordered pair, an argument list.</summary>
public sealed class TupleLiteral : Expr
{
    /// <summary>Creates a tuple.</summary>
    public TupleLiteral(ImmutableArray<Expr> elements)
    {
        if (elements.IsDefault) throw new ArgumentNullException(nameof(elements));
        foreach (var e in elements) ArgumentNullException.ThrowIfNull(e);
        Elements = elements;
    }

    /// <summary>The elements in order.</summary>
    public ImmutableArray<Expr> Elements { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Tuple;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => Elements;

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children) =>
        children.AsSpan().SequenceEqual(Elements.AsSpan(), ReferenceEqualityComparer.Instance) ? this : new TupleLiteral(children);
}

/// <summary>
/// A piecewise definition: (value, condition) pairs whose conditions are evaluated in order; a final <c>true</c> condition is
/// the "otherwise" branch.
/// </summary>
public sealed class Piecewise : Expr
{
    /// <summary>Creates a piecewise expression.</summary>
    /// <exception cref="ArgumentException">There are no cases.</exception>
    public Piecewise(ImmutableArray<(Expr Value, Expr Condition)> cases)
    {
        if (cases.IsDefaultOrEmpty) throw new ArgumentException("A piecewise expression needs at least one case.", nameof(cases));
        foreach (var (v, c) in cases)
        {
            ArgumentNullException.ThrowIfNull(v);
            ArgumentNullException.ThrowIfNull(c);
        }
        Cases = cases;
    }

    /// <summary>The cases in evaluation order.</summary>
    public ImmutableArray<(Expr Value, Expr Condition)> Cases { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Piecewise;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => [.. Cases.SelectMany(c => new[] { c.Value, c.Condition })];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children)
    {
        if (children.Length != Cases.Length * 2) throw new ArgumentException("A piecewise expression has a value and a condition per case.", nameof(children));
        var cases = ImmutableArray.CreateBuilder<(Expr, Expr)>(Cases.Length);
        for (var i = 0; i < Cases.Length; i++) cases.Add((children[2 * i], children[2 * i + 1]));
        var rebuilt = cases.ToImmutable();
        var same = true;
        for (var i = 0; i < Cases.Length && same; i++) same = ReferenceEquals(rebuilt[i].Item1, Cases[i].Value) && ReferenceEquals(rebuilt[i].Item2, Cases[i].Condition);
        return same ? this : new Piecewise(rebuilt);
    }
}

/// <summary>A pattern variable; appears only inside patterns.</summary>
public sealed class Wild : Expr
{
    /// <summary>Creates a wild with an optional constraint expression (for example <c>n in Z</c> written as the sort set).</summary>
    public Wild(string name, Expr? constraint = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!Symbol.IsValidName(name)) throw new ArgumentException($"'{name}' is not a valid wild name.", nameof(name));
        Name = name;
        Constraint = constraint;
    }

    /// <summary>The wild's name.</summary>
    public string Name { get; }

    /// <summary>An optional constraint the matched expression must satisfy.</summary>
    public Expr? Constraint { get; }

    /// <inheritdoc />
    public override ExprKind Kind => ExprKind.Wild;

    /// <inheritdoc />
    public override ImmutableArray<Expr> Children => Constraint is null ? [] : [Constraint];

    /// <inheritdoc />
    public override Expr WithChildren(ImmutableArray<Expr> children)
    {
        if (children.Length > 1) throw new ArgumentException("A wild has at most one child.", nameof(children));
        return children.Length == 0 ? (Constraint is null ? this : new Wild(Name)) : ReferenceEquals(children[0], Constraint) ? this : new Wild(Name, children[0]);
    }
}
