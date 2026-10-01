namespace Mathesis;

/// <summary>The category of a <see cref="MathError"/>.</summary>
public enum MathErrorKind : byte
{
    /// <summary>An argument lies outside a function's domain.</summary>
    Domain,

    /// <summary>Division by zero.</summary>
    DivisionByZero,

    /// <summary>An iterative method did not converge.</summary>
    NonConvergence,

    /// <summary>A <see cref="Budget"/> limit was exceeded.</summary>
    BudgetExceeded,

    /// <summary>The operation is not supported for these inputs.</summary>
    Unsupported,

    /// <summary>An expression's sorts do not fit its operator's signature.</summary>
    SortMismatch,
}

/// <summary>A mathematical failure, carried as data inside <see cref="Outcome{T}.Failed"/>; never thrown.</summary>
/// <param name="Kind">The error category.</param>
/// <param name="Message">A human-readable description.</param>
/// <param name="Subject">The object the error is about (for example the offending expression), if any.</param>
public sealed record MathError(MathErrorKind Kind, string Message, IMathObject? Subject = null)
{
    /// <summary>Creates a <see cref="MathErrorKind.Domain"/> error.</summary>
    public static MathError Domain(string message, IMathObject? subject = null) => new(MathErrorKind.Domain, message, subject);

    /// <summary>Creates a <see cref="MathErrorKind.DivisionByZero"/> error.</summary>
    public static MathError DivisionByZero(string message = "Division by zero.", IMathObject? subject = null) => new(MathErrorKind.DivisionByZero, message, subject);

    /// <summary>Creates a <see cref="MathErrorKind.NonConvergence"/> error.</summary>
    public static MathError NonConvergence(string message, IMathObject? subject = null) => new(MathErrorKind.NonConvergence, message, subject);

    /// <summary>Creates a <see cref="MathErrorKind.BudgetExceeded"/> error.</summary>
    public static MathError BudgetExceeded(string message = "Budget exceeded.", IMathObject? subject = null) => new(MathErrorKind.BudgetExceeded, message, subject);

    /// <summary>Creates a <see cref="MathErrorKind.Unsupported"/> error.</summary>
    public static MathError Unsupported(string message, IMathObject? subject = null) => new(MathErrorKind.Unsupported, message, subject);

    /// <summary>Creates a <see cref="MathErrorKind.SortMismatch"/> error.</summary>
    public static MathError SortMismatch(string message, IMathObject? subject = null) => new(MathErrorKind.SortMismatch, message, subject);

    /// <inheritdoc />
    public override string ToString() => $"{Kind}: {Message}";
}
