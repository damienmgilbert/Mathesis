using System.Collections.Immutable;

namespace Mathesis.Symbolics;

/// <summary>
/// The address of a subexpression: the list of child indices from the root (see <see cref="Expr.Children"/> for the order).
/// Steps record the path they rewrote, so a UI can highlight what changed.
/// </summary>
public readonly struct ExprPath : IEquatable<ExprPath>
{
    private readonly ImmutableArray<int> _indices;

    /// <summary>Creates a path from child indices.</summary>
    public ExprPath(IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        _indices = [.. indices];
        foreach (var i in _indices) ArgumentOutOfRangeException.ThrowIfNegative(i);
    }

    /// <summary>The empty path, addressing the whole expression.</summary>
    public static ExprPath Root => default;

    /// <summary>The child indices, outermost first.</summary>
    public ImmutableArray<int> Indices => _indices.IsDefault ? [] : _indices;

    /// <summary>Whether this is the empty path.</summary>
    public bool IsRoot => Indices.Length == 0;

    /// <summary>The path to child <paramref name="index"/> of the addressed node.</summary>
    public ExprPath Child(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new ExprPath(Indices.Add(index));
    }

    /// <summary>The path to the parent of the addressed node.</summary>
    /// <exception cref="InvalidOperationException">This is the root path.</exception>
    public ExprPath Parent => IsRoot ? throw new InvalidOperationException("The root has no parent.") : new ExprPath(Indices.RemoveAt(Indices.Length - 1));

    /// <summary>The subexpression of <paramref name="root"/> at this path.</summary>
    /// <exception cref="ArgumentException">The path does not address a node of <paramref name="root"/>.</exception>
    public Expr Get(Expr root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var node = root;
        foreach (var i in Indices)
        {
            var children = node.Children;
            if (i >= children.Length) throw new ArgumentException($"Path {this} does not exist in the expression.", nameof(root));
            node = children[i];
        }
        return node;
    }

    /// <summary><paramref name="root"/> with the subexpression at this path replaced by <paramref name="replacement"/>.</summary>
    /// <exception cref="ArgumentException">The path does not address a node of <paramref name="root"/>.</exception>
    public Expr Replace(Expr root, Expr replacement)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(replacement);
        return Replace(root, 0, replacement);
    }

    private Expr Replace(Expr node, int depth, Expr replacement)
    {
        if (depth == Indices.Length) return replacement;
        var children = node.Children;
        var i = Indices[depth];
        if (i >= children.Length) throw new ArgumentException($"Path {this} does not exist in the expression.", nameof(node));
        return node.WithChildren(children.SetItem(i, Replace(children[i], depth + 1, replacement)));
    }

    /// <inheritdoc />
    public bool Equals(ExprPath other) => Indices.AsSpan().SequenceEqual(other.Indices.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ExprPath other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var i in Indices) hash.Add(i);
        return hash.ToHashCode();
    }

    /// <summary>Structural equality.</summary>
    public static bool operator ==(ExprPath left, ExprPath right) => left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(ExprPath left, ExprPath right) => !left.Equals(right);

    /// <summary>Formats as <c>/0/2/1</c>; the root is <c>/</c>.</summary>
    public override string ToString() => IsRoot ? "/" : "/" + string.Join('/', Indices);
}
