using System.Numerics;
namespace Mathesis.Numerics.Differentiation;

/// <summary>
/// Numerical derivatives by finite differences and Richardson extrapolation, generic over
/// <see cref="IFloatingPointIeee754{TSelf}"/>. For exact derivatives use forward-mode automatic differentiation
/// (<see cref="Mathesis.Numbers.Dual{T}"/>).
/// </summary>
/// <remarks>
/// Default step sizes balance truncation against rounding error (catalog <c>num.diff.optimal-step</c>), each scaled by
/// max(1, |x|): √ε for the forward and backward differences, ∛ε for the central difference, ε<sup>1/4</sup> for the second
/// derivative and ε<sup>1/5</sup> for the five-point stencil.
/// </remarks>
public static class FiniteDifferences
{
    /// <summary>Forward difference (f(x + h) − f(x))/h, error −h·f″(ξ)/2 (catalog <c>num.diff.forward</c>).</summary>
    public static T Forward<T>(Func<T, T> f, T x, T? step = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var h = step ?? T.Sqrt(Num.Eps<T>()) * Num.Max(T.One, T.Abs(x));
        return (f(x + h) - f(x)) / h;
    }

    /// <summary>Backward difference (f(x) − f(x − h))/h.</summary>
    public static T Backward<T>(Func<T, T> f, T x, T? step = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var h = step ?? T.Sqrt(Num.Eps<T>()) * Num.Max(T.One, T.Abs(x));
        return (f(x) - f(x - h)) / h;
    }

    /// <summary>Central difference (f(x + h) − f(x − h))/(2h), error −h²·f‴(ξ)/6 (catalog <c>num.diff.central</c>).</summary>
    public static T Central<T>(Func<T, T> f, T x, T? step = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var h = step ?? T.Cbrt(Num.Eps<T>()) * Num.Max(T.One, T.Abs(x));
        return (f(x + h) - f(x - h)) / (Num.Two<T>() * h);
    }

    /// <summary>Second derivative (f(x + h) − 2f(x) + f(x − h))/h², error −h²·f⁗(ξ)/12 (catalog <c>num.diff.second</c>).</summary>
    public static T Second<T>(Func<T, T> f, T x, T? step = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var h = step ?? T.RootN(Num.Eps<T>(), 4) * Num.Max(T.One, T.Abs(x));
        return (f(x + h) - Num.Two<T>() * f(x) + f(x - h)) / (h * h);
    }

    /// <summary>Five-point stencil (−f(x + 2h) + 8f(x + h) − 8f(x − h) + f(x − 2h))/(12h), error O(h⁴) (catalog <c>num.diff.five-point</c>).</summary>
    public static T FivePoint<T>(Func<T, T> f, T x, T? step = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var h = step ?? T.RootN(Num.Eps<T>(), 5) * Num.Max(T.One, T.Abs(x));
        var eight = Num.C<T>(8);
        return (-f(x + Num.Two<T>() * h) + eight * f(x + h) - eight * f(x - h) + f(x - Num.Two<T>() * h)) / (Num.C<T>(12) * h);
    }

    /// <summary>
    /// Richardson extrapolation of the central difference (catalog <c>num.diff.richardson</c>; Ridders 1982): the step is halved
    /// at each level and the tableau D<sub>i,j</sub> = (4<sup>j</sup>·D<sub>i,j−1</sub> − D<sub>i−1,j−1</sub>)/(4<sup>j</sup> − 1)
    /// cancels the h², h⁴, … error terms. The result with the smallest difference-based error estimate is returned, and the reported <see cref="DerivativeResult{T}.ErrorEstimate"/>
    /// adds twice the round-off level ε·|f|/h of that step; extrapolation stops once the estimate starts to grow (round-off). Typically accurate to a few ulps for smooth f.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="x">The point.</param>
    /// <param name="step">The initial step, which should be large compared with the final one; defaults to 0.1·max(1, |x|).</param>
    /// <param name="levels">The maximum number of step halvings.</param>
    public static DerivativeResult<T> Richardson<T>(Func<T, T> f, T x, T? step = null, int levels = 10)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentOutOfRangeException.ThrowIfLessThan(levels, 2);
        var h = step ?? Num.C<T>(0.1) * Num.Max(T.One, T.Abs(x));
        var table = new T[levels, levels];
        var evaluations = 0;
        var best = T.NaN;
        var bestError = T.PositiveInfinity;
        var bestRoundOff = T.Zero;
        var eps = Num.Eps<T>();

        for (var i = 0; i < levels; i++)
        {
            var forward = f(x + h);
            var backward = f(x - h);
            table[i, 0] = (forward - backward) / (Num.Two<T>() * h);
            evaluations += 2;

            // Rounding in f(x ± h) alone perturbs D(h) by about ε·|f|/h; difference tables can look better than that by chance.
            var roundOff = eps * Num.Max(T.Abs(forward), T.Abs(backward)) / h;
            var factor = Num.C<T>(4);
            for (var j = 1; j <= i; j++)
            {
                table[i, j] = (factor * table[i, j - 1] - table[i - 1, j - 1]) / (factor - T.One);
                var error = Num.Max(T.Abs(table[i, j] - table[i, j - 1]), T.Abs(table[i, j] - table[i - 1, j - 1]));
                if (error <= bestError)
                {
                    bestError = error;
                    best = table[i, j];
                    bestRoundOff = roundOff;
                }
                factor *= Num.C<T>(4);
            }

            // Once the highest-order estimate is clearly worse than the best seen, round-off dominates. Early tableaus are not
            // monotone (the error estimate can dip by coincidence), so the safeguard only applies from the fifth level on.
            if (i >= 4 && T.Abs(table[i, i] - table[i - 1, i - 1]) >= Num.Two<T>() * bestError) break;
            h /= Num.Two<T>();
        }

        if (T.IsNaN(best)) return new(table[0, 0], T.PositiveInfinity, evaluations);
        return new(best, bestError + Num.Two<T>() * bestRoundOff, evaluations);
    }
}
