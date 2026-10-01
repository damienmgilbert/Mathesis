using System.Numerics;

namespace Mathesis.Numerics;

/// <summary>Why an iterative numerical routine stopped.</summary>
public enum Convergence : byte
{
    /// <summary>The step or error estimate met the absolute/relative tolerance.</summary>
    Tolerance,

    /// <summary>The function value (residual) met the function tolerance, or was exactly zero.</summary>
    FunctionTolerance,

    /// <summary>The iteration or subinterval cap was reached first.</summary>
    MaxIterations,

    /// <summary>The interval does not bracket a sign change (f(a)·f(b) &gt; 0).</summary>
    InvalidBracket,

    /// <summary>A NaN or infinity appeared in the iteration.</summary>
    NotFinite,

    /// <summary>The derivative (or a secant slope) was zero, so the step is undefined.</summary>
    ZeroDerivative,

    /// <summary>The step size shrank below the representable limit before the target was reached.</summary>
    StepTooSmall,
}

/// <summary>
/// Stopping rules shared by the iterative routines. A <c>null</c> tolerance selects a default that depends on the
/// floating-point type (see each routine). Tolerances are given as <see cref="double"/> and converted to the working type.
/// </summary>
/// <param name="AbsoluteTolerance">Absolute tolerance on the step or error estimate.</param>
/// <param name="RelativeTolerance">Tolerance relative to the magnitude of the iterate or integral.</param>
/// <param name="FunctionTolerance">Stop once |f(x)| is at most this value (root finders only).</param>
/// <param name="MaxIterations">Iteration cap (maximum subintervals for adaptive quadrature); <c>null</c> selects the routine's default.</param>
public sealed record StoppingCriteria(
    double? AbsoluteTolerance = null,
    double? RelativeTolerance = null,
    double? FunctionTolerance = null,
    int? MaxIterations = null);

/// <summary>Helpers shared by the numerical routines.</summary>
internal static class Num
{
    /// <summary>Converts a double constant to the working type.</summary>
    public static T C<T>(double value) where T : IFloatingPointIeee754<T> => T.CreateChecked(value);

    /// <summary>Machine epsilon: the gap between 1 and the next representable number.</summary>
    public static T Eps<T>() where T : IFloatingPointIeee754<T> => T.BitIncrement(T.One) - T.One;

    public static T Two<T>() where T : IFloatingPointIeee754<T> => T.One + T.One;

    public static T Half<T>() where T : IFloatingPointIeee754<T> => T.One / Two<T>();

    public static T Max<T>(T a, T b) where T : IFloatingPointIeee754<T> => a >= b ? a : b;

    public static T Min<T>(T a, T b) where T : IFloatingPointIeee754<T> => a <= b ? a : b;

    public static T Sign<T>(T magnitude, T sign) where T : IFloatingPointIeee754<T> => T.CopySign(magnitude, sign);

    /// <summary>Resolves optional tolerances against defaults expressed in terms of machine epsilon.</summary>
    public static (T Abs, T Rel, T Fun) Tolerances<T>(StoppingCriteria? stop, T defaultAbs, T defaultRel) where T : IFloatingPointIeee754<T> =>
        (stop?.AbsoluteTolerance is { } a ? T.CreateChecked(a) : defaultAbs,
         stop?.RelativeTolerance is { } r ? T.CreateChecked(r) : defaultRel,
         stop?.FunctionTolerance is { } f ? T.CreateChecked(f) : T.Zero);

    public static void ThrowIfNotFinite<T>(T value, string paramName) where T : IFloatingPointIeee754<T>
    {
        if (!T.IsFinite(value)) throw new ArgumentOutOfRangeException(paramName, "Value must be finite.");
    }
}
