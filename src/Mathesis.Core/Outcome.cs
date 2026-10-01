namespace Mathesis;

/// <summary>
/// The result of an engine call. Engines never throw for mathematical failure; they return one of the four cases.
/// </summary>
/// <typeparam name="T">The type of the computed value.</typeparam>
public abstract record Outcome<T>
{
    private protected Outcome()
    {
    }

    /// <summary>The computation finished.</summary>
    /// <param name="Value">The result.</param>
    /// <param name="Steps">The recorded derivation, or <c>null</c> when tracing was off.</param>
    /// <param name="Provisos">Conditions under which the result holds.</param>
    /// <param name="Check">How the result was self-checked.</param>
    public sealed record Success(T Value, IDerivation? Steps, Provisos Provisos, Verification Check) : Outcome<T>;

    /// <summary>A usable but incomplete result (for example a budget ran out part-way).</summary>
    /// <param name="Value">The partial result.</param>
    /// <param name="Reason">Why the result is incomplete.</param>
    /// <param name="Steps">The derivation so far, if any.</param>
    /// <param name="Provisos">Conditions under which the partial result holds.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Case name specified by docs/design/02-architecture.md.")]
    public sealed record Partial(T Value, string Reason, IDerivation? Steps, Provisos Provisos) : Outcome<T>;

    /// <summary>The engine could not evaluate the input and returns it unchanged.</summary>
    /// <param name="Original">The input, typically an <c>Expr</c>.</param>
    /// <param name="Reason">Why nothing was computed.</param>
    public sealed record Unevaluated(IMathObject Original, string Reason) : Outcome<T>;

    /// <summary>The computation failed with a mathematical error.</summary>
    /// <param name="Error">The error.</param>
    public sealed record Failed(MathError Error) : Outcome<T>;

    /// <summary>Gets the value of a <see cref="Success"/> or <see cref="Partial"/> outcome.</summary>
    /// <param name="value">The value, or <c>default</c> when there is none.</param>
    /// <returns><c>true</c> when a value exists.</returns>
    public bool TryGetValue(out T value)
    {
        switch (this)
        {
            case Success s: value = s.Value; return true;
            case Partial p: value = p.Value; return true;
            default: value = default!; return false;
        }
    }
}

/// <summary>Convenience constructors for <see cref="Outcome{T}"/>.</summary>
public static class Outcome
{
    /// <summary>Creates an unconditional, unchecked, untraced success.</summary>
    public static Outcome<T> Ok<T>(T value) => new Outcome<T>.Success(value, null, Provisos.None, Verification.NotChecked);

    /// <summary>Creates a success with explicit steps, provisos and check status.</summary>
    public static Outcome<T> Ok<T>(T value, IDerivation? steps, Provisos provisos, Verification check) =>
        new Outcome<T>.Success(value, steps, provisos, check);

    /// <summary>Creates a failure.</summary>
    public static Outcome<T> Fail<T>(MathError error) => new Outcome<T>.Failed(error);
}
