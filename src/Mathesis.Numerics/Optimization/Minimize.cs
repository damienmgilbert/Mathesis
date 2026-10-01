using System.Collections.Immutable;
using System.Numerics;

namespace Mathesis.Numerics.Optimization;

/// <summary>The outcome of a minimization.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Minimizer">The best point found (one element for one-dimensional problems).</param>
/// <param name="Minimum">The objective value at <paramref name="Minimizer"/>.</param>
/// <param name="ErrorEstimate">The final bracket width (golden-section) or simplex diameter (Nelder–Mead).</param>
/// <param name="Iterations">Iterations performed.</param>
/// <param name="Evaluations">Objective evaluations performed.</param>
/// <param name="Converged"><c>true</c> if a stopping tolerance was met.</param>
/// <param name="Reason">Why the routine stopped.</param>
public sealed record OptimizationResult<T>(ImmutableArray<T> Minimizer, T Minimum, T ErrorEstimate, int Iterations, int Evaluations, bool Converged, Convergence Reason);

/// <summary>
/// Derivative-free minimization, generic over <see cref="IFloatingPointIeee754{TSelf}"/>. Both methods return an
/// <see cref="OptimizationResult{T}"/> and report non-convergence there instead of throwing.
/// </summary>
/// <remarks>
/// Near a smooth minimum the objective is flat to first order, so a minimizer cannot be located more accurately than about
/// √ε·|x| (one function evaluation cannot tell f(x) from f(x + δ) once δ² f″ falls below ε|f|). The default tolerances respect that.
/// </remarks>
public static class Minimize
{
    private static readonly double InversePhi = (Math.Sqrt(5) - 1) / 2;

    /// <summary>
    /// Golden-section search for a minimum of a unimodal function on [a, b] (catalog <c>num.opt.golden-section</c>): the bracket
    /// shrinks by (√5 − 1)/2 ≈ 0.618 per evaluation, reusing one interior point each step. Without unimodality it finds some local
    /// minimum.
    /// </summary>
    /// <param name="f">The objective.</param>
    /// <param name="a">One end of the interval.</param>
    /// <param name="b">The other end.</param>
    /// <param name="stop">
    /// Stop when the bracket width is at most absTol + relTol·|x| (defaults ε² and √ε); <see cref="StoppingCriteria.MaxIterations"/>
    /// defaults to 200.
    /// </param>
    public static OptimizationResult<T> GoldenSection<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(a, nameof(a));
        Num.ThrowIfNotFinite(b, nameof(b));
        var eps = Num.Eps<T>();
        var (absTol, relTol, _) = Num.Tolerances(stop, eps * eps, T.Sqrt(eps));
        var maxIterations = stop?.MaxIterations ?? 200;
        var invPhi = T.CreateChecked(InversePhi);
        if (a > b) (a, b) = (b, a);

        var c = b - invPhi * (b - a);
        var d = a + invPhi * (b - a);
        var fc = f(c);
        var fd = f(d);
        var evaluations = 2;
        if (T.IsNaN(fc) || T.IsNaN(fd)) return Result(c, fc, d, fd, b - a, 0, evaluations, false, Convergence.NotFinite);

        for (var i = 1; i <= maxIterations; i++)
        {
            if (b - a <= absTol + relTol * (T.Abs(c) + T.Abs(d)) * Num.Half<T>()) return Result(c, fc, d, fd, b - a, i - 1, evaluations, true, Convergence.Tolerance);
            if (fc < fd)
            {
                b = d;
                d = c;
                fd = fc;
                c = b - invPhi * (b - a);
                fc = f(c);
            }
            else
            {
                a = c;
                c = d;
                fc = fd;
                d = a + invPhi * (b - a);
                fd = f(d);
            }
            evaluations++;
            if (T.IsNaN(fc) || T.IsNaN(fd)) return Result(c, fc, d, fd, b - a, i, evaluations, false, Convergence.NotFinite);
        }
        return Result(c, fc, d, fd, b - a, maxIterations, evaluations, false, Convergence.MaxIterations);

        static OptimizationResult<T> Result(T c, T fc, T d, T fd, T width, int iterations, int evaluations, bool converged, Convergence reason) =>
            fc <= fd ? new([c], fc, width, iterations, evaluations, converged, reason) : new([d], fd, width, iterations, evaluations, converged, reason);
    }

    /// <summary>
    /// The Nelder–Mead simplex method (catalog <c>num.opt.nelder-mead</c>; Nelder and Mead 1965, with the standard coefficients
    /// reflection 1, expansion 2, contraction ½, shrink ½ analysed by Lagarias, Reeds, Wright and Wright, SIAM J. Optim. 9, 1998).
    /// Needs no derivatives; it can stall at non-stationary points for hard problems, so check <see cref="OptimizationResult{T}.Converged"/>
    /// and restart from the result when in doubt.
    /// </summary>
    /// <param name="f">The objective on T[]. NaN values are treated as +∞.</param>
    /// <param name="start">The starting point; its length is the dimension (at least 1).</param>
    /// <param name="step">
    /// The initial simplex size: vertex i adds this to coordinate i. <c>null</c> uses 5% of each non-zero coordinate and
    /// 0.00025 for zero coordinates.
    /// </param>
    /// <param name="stop">
    /// Stop when both the simplex diameter and the spread of the function values are within the tolerances (absTol defaults to
    /// ε<sup>3/4</sup>; <see cref="StoppingCriteria.FunctionTolerance"/> defaults to the same); <see cref="StoppingCriteria.MaxIterations"/>
    /// defaults to 200·n.
    /// </param>
    public static OptimizationResult<T> NelderMead<T>(Func<T[], T> f, T[] start, T? step = null, StoppingCriteria? stop = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(start);
        if (start.Length == 0) throw new ArgumentException("The starting point must have at least one coordinate.", nameof(start));
        foreach (var x in start) Num.ThrowIfNotFinite(x, nameof(start));
        var n = start.Length;
        var eps = Num.Eps<T>();
        var tolerance = T.Pow(eps, Num.C<T>(0.75));
        var absTol = stop?.AbsoluteTolerance is { } a ? T.CreateChecked(a) : tolerance;
        var fTol = stop?.FunctionTolerance is { } ft ? T.CreateChecked(ft) : tolerance;
        var maxIterations = stop?.MaxIterations ?? 200 * n;
        var half = Num.Half<T>();
        var two = Num.Two<T>();

        var evaluations = 0;
        T Eval(T[] x)
        {
            evaluations++;
            var v = f(x);
            return T.IsNaN(v) ? T.PositiveInfinity : v;
        }

        var simplex = new T[n + 1][];
        var values = new T[n + 1];
        simplex[0] = (T[])start.Clone();
        for (var i = 0; i < n; i++)
        {
            var vertex = (T[])start.Clone();
            vertex[i] += step ?? (start[i] != T.Zero ? Num.C<T>(0.05) * start[i] : Num.C<T>(0.00025));
            simplex[i + 1] = vertex;
        }
        for (var i = 0; i <= n; i++) values[i] = Eval(simplex[i]);

        T[] Along(T[] origin, T[] target, T factor)
        {
            var p = new T[n];
            for (var j = 0; j < n; j++) p[j] = origin[j] + factor * (target[j] - origin[j]);
            return p;
        }

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            // Order the vertices by objective value.
            var order = Enumerable.Range(0, n + 1).OrderBy(i => values[i]).ToArray();
            simplex = order.Select(i => simplex[i]).ToArray();
            values = order.Select(i => values[i]).ToArray();

            var diameter = T.Zero;
            for (var i = 1; i <= n; i++)
            {
                for (var j = 0; j < n; j++) diameter = Num.Max(diameter, T.Abs(simplex[i][j] - simplex[0][j]));
            }
            var spread = values[n] - values[0];
            if (diameter <= absTol && (spread <= fTol || !T.IsFinite(spread)))
            {
                return new([.. simplex[0]], values[0], diameter, iteration, evaluations, true, Convergence.Tolerance);
            }

            var centroid = CentroidOfBest();
            var worst = simplex[n];
            var reflected = Along(centroid, worst, -T.One);
            var fr = Eval(reflected);
            if (fr >= values[0] && fr < values[n - 1])
            {
                simplex[n] = reflected;
                values[n] = fr;
            }
            else if (fr < values[0])
            {
                var expanded = Along(centroid, worst, -two);
                var fe = Eval(expanded);
                if (fe < fr)
                {
                    simplex[n] = expanded;
                    values[n] = fe;
                }
                else
                {
                    simplex[n] = reflected;
                    values[n] = fr;
                }
            }
            else
            {
                var outside = fr < values[n];
                var contracted = outside ? Along(centroid, reflected, half) : Along(centroid, worst, half);
                var fcontracted = Eval(contracted);
                if (fcontracted < (outside ? fr : values[n]))
                {
                    simplex[n] = contracted;
                    values[n] = fcontracted;
                }
                else
                {
                    for (var i = 1; i <= n; i++)
                    {
                        simplex[i] = Along(simplex[0], simplex[i], half);
                        values[i] = Eval(simplex[i]);
                    }
                }
            }
        }

        var best = Enumerable.Range(0, n + 1).MinBy(i => values[i]);
        var finalDiameter = T.Zero;
        for (var i = 0; i <= n; i++)
        {
            for (var j = 0; j < n; j++) finalDiameter = Num.Max(finalDiameter, T.Abs(simplex[i][j] - simplex[best][j]));
        }
        return new([.. simplex[best]], values[best], finalDiameter, maxIterations, evaluations, false, Convergence.MaxIterations);

        // The centroid of all vertices except the worst (the last after sorting).
        T[] CentroidOfBest()
        {
            var c = new T[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++) c[j] += simplex[i][j];
            }
            for (var j = 0; j < n; j++) c[j] /= T.CreateChecked(n);
            return c;
        }
    }
}
