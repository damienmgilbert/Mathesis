namespace Mathesis;

/// <summary>How an <see cref="Outcome{T}"/> was checked.</summary>
public enum Verification : byte
{
    /// <summary>No self-check ran.</summary>
    NotChecked,

    /// <summary>An exact check passed (for example differentiate-back zero-tests to zero).</summary>
    Verified,

    /// <summary>Random-point numeric agreement; not a proof.</summary>
    NumericallyConsistent,

    /// <summary>The self-check failed. The result is returned to the caller, never hidden.</summary>
    Failed,
}
