using System.Collections.Immutable;

namespace Mathesis.Numerics;

/// <summary>The outcome of a root-finding routine. Non-convergence is reported here, never thrown.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Root">The best root estimate found.</param>
/// <param name="FunctionValue">f at <paramref name="Root"/>.</param>
/// <param name="ErrorEstimate">An estimate of |root − true root|: the bracket half-width or the last step size.</param>
/// <param name="Iterations">Iterations performed.</param>
/// <param name="Evaluations">Function (and derivative) evaluations performed.</param>
/// <param name="Converged"><c>true</c> if a stopping tolerance was met.</param>
/// <param name="Reason">Why the routine stopped.</param>
public sealed record RootResult<T>(T Root, T FunctionValue, T ErrorEstimate, int Iterations, int Evaluations, bool Converged, Convergence Reason);

/// <summary>The outcome of a quadrature routine.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Value">The integral estimate.</param>
/// <param name="ErrorEstimate">An estimate of the absolute error.</param>
/// <param name="Evaluations">Integrand evaluations performed.</param>
/// <param name="Subintervals">Subintervals used (adaptive routines) or levels (Romberg).</param>
/// <param name="Converged"><c>true</c> if the error estimate met the tolerance.</param>
/// <param name="Warnings">Notes such as a suspected singularity or a round-off limited result.</param>
public sealed record QuadratureResult<T>(T Value, T ErrorEstimate, int Evaluations, int Subintervals, bool Converged, ImmutableArray<string> Warnings);

/// <summary>The outcome of a numerical differentiation.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Value">The derivative estimate.</param>
/// <param name="ErrorEstimate">An estimate of the absolute error.</param>
/// <param name="Evaluations">Function evaluations performed.</param>
public sealed record DerivativeResult<T>(T Value, T ErrorEstimate, int Evaluations);

/// <summary>A continuous extension of an ODE solution within its integration interval.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public interface IDenseOutput<T>
{
    /// <summary>The state at time <paramref name="t"/>, which must lie between the first and last solution times.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="t"/> is outside the integration interval.</exception>
    T[] Evaluate(T t);
}

/// <summary>The outcome of an ODE integration.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Times">The times of the accepted steps, starting at t0.</param>
/// <param name="States">The state vector at each time in <paramref name="Times"/>.</param>
/// <param name="Converged"><c>true</c> if the integration reached the final time.</param>
/// <param name="Reason">Why the integration stopped.</param>
/// <param name="AcceptedSteps">Steps accepted.</param>
/// <param name="RejectedSteps">Steps rejected by error control (always 0 for fixed-step methods).</param>
/// <param name="Evaluations">Right-hand-side evaluations performed.</param>
/// <param name="Dense">A dense-output interpolant, or <c>null</c> for fixed-step methods.</param>
public sealed record OdeSolution<T>(
    ImmutableArray<T> Times,
    ImmutableArray<ImmutableArray<T>> States,
    bool Converged,
    Convergence Reason,
    int AcceptedSteps,
    int RejectedSteps,
    int Evaluations,
    IDenseOutput<T>? Dense)
{
    /// <summary>The final time reached.</summary>
    public T FinalTime => Times[^1];

    /// <summary>The state at the final time reached.</summary>
    public ImmutableArray<T> FinalState => States[^1];

    /// <summary>The state at time <paramref name="t"/> using the dense output.</summary>
    /// <exception cref="InvalidOperationException">The method has no dense output.</exception>
    public T[] Evaluate(T t) =>
        Dense?.Evaluate(t) ?? throw new InvalidOperationException("This solution has no dense output; use DormandPrince.");
}
