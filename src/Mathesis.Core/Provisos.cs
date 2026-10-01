using System.Collections;
using System.Collections.Immutable;

namespace Mathesis;

/// <summary>
/// An immutable, duplicate-free list of side conditions (such as <c>x ≠ 0</c>) under which a result holds.
/// </summary>
/// <remarks>
/// Conditions are <see cref="IMathObject"/>s (expressions, once Symbolics exists) compared with <c>Equals</c>.
/// Insertion order is preserved so explanations are stable.
/// </remarks>
public sealed class Provisos : IReadOnlyList<IMathObject>, IEquatable<Provisos>
{
    private readonly ImmutableArray<IMathObject> _items;

    private Provisos(ImmutableArray<IMathObject> items) => _items = items;

    /// <summary>The empty set of conditions: the result holds unconditionally.</summary>
    public static Provisos None { get; } = new([]);

    /// <summary>Creates a set from the given conditions, dropping duplicates.</summary>
    public static Provisos Of(params ReadOnlySpan<IMathObject> conditions) => None.AddRange(conditions);

    /// <inheritdoc />
    public int Count => _items.Length;

    /// <inheritdoc />
    public IMathObject this[int index] => _items[index];

    /// <summary>Returns a set that also contains <paramref name="condition"/>.</summary>
    public Provisos Add(IMathObject condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return _items.Contains(condition) ? this : new(_items.Add(condition));
    }

    /// <summary>Returns a set that also contains every condition in <paramref name="conditions"/>.</summary>
    public Provisos AddRange(ReadOnlySpan<IMathObject> conditions)
    {
        var builder = _items.ToBuilder();
        foreach (var c in conditions)
        {
            ArgumentNullException.ThrowIfNull(c);
            if (!builder.Contains(c)) builder.Add(c);
        }
        return builder.Count == _items.Length ? this : new(builder.ToImmutable());
    }

    /// <summary>Returns the union of two sets, keeping this set's order first.</summary>
    public Provisos Union(Provisos other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return AddRange(other._items.AsSpan());
    }

    /// <inheritdoc />
    public bool Equals(Provisos? other) => other is not null && _items.AsSpan().SequenceEqual(other._items.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Provisos);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public IEnumerator<IMathObject> GetEnumerator() => ((IEnumerable<IMathObject>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => _items.Length == 0 ? "(none)" : string.Join(", ", _items);
}
