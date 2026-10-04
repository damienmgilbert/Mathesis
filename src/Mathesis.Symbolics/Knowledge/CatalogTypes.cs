namespace Mathesis.Knowledge;

/// <summary>Curriculum levels in increasing order; an explanation at level L never cites an entry above L.</summary>
public enum CurriculumLevel : byte
{
    /// <summary>Arithmetic.</summary>
    Arithmetic,

    /// <summary>Pre-Algebra.</summary>
    PreAlgebra,

    /// <summary>Algebra 1.</summary>
    Algebra1,

    /// <summary>Geometry.</summary>
    Geometry,

    /// <summary>Algebra 2.</summary>
    Algebra2,

    /// <summary>Pre-Calculus.</summary>
    PreCalculus,

    /// <summary>Calculus 1.</summary>
    Calculus1,

    /// <summary>Calculus 2.</summary>
    Calculus2,

    /// <summary>Calculus 3.</summary>
    Calculus3,

    /// <summary>University courses.</summary>
    University,

    /// <summary>Advanced topics.</summary>
    Advanced,
}

/// <summary>A stable catalog identifier <c>domain.topic.name</c> in lowercase kebab-case (or <c>conv.name</c>).</summary>
/// <param name="Value">The identifier text.</param>
public readonly record struct EntryId(string Value)
{
    /// <summary>Whether <paramref name="text"/> has the form of an identifier: two or three dot-separated segments of lowercase letters, digits and hyphens.</summary>
    public static bool IsValid(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('.');
        if (parts.Length is < 2 or > 3) return false;
        foreach (var part in parts)
        {
            if (part.Length == 0 || part[0] == '-' || part[^1] == '-') return false;
            foreach (var c in part)
            {
                if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')) return false;
            }
        }
        return true;
    }

    /// <summary>The domain prefix (the first segment).</summary>
    public string Domain => Value[..Value.IndexOf('.', StringComparison.Ordinal)];

    /// <inheritdoc />
    public override string ToString() => Value;
}
