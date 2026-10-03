using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Numerics;

/// <summary>
/// Root finding for <c>f(x) = 0</c>, generic over <see cref="IFloatingPointIeee754{TSelf}"/>. Every method returns a
/// <see cref="RootResult{T}"/> and reports non-convergence there instead of throwing.
/// </summary>
/// <remarks>
/// <para>Bracketing methods (<see cref="Bisection{T}"/>, <see cref="Brent{T}"/>) use |Δx| ≤ absTol + relTol·|x| (Brent adds 2ε|x|)
/// with defaults absTol = ε², relTol = 0 (bisection: 4ε; its iteration cap is 2,200, the other methods' 100). Open methods (<see cref="Newton{T}(Func{T, T}, Func{T, T}, T, StoppingCriteria?)"/>, <see cref="Secant{T}"/>) use the
/// rule from design-doc entry <c>num.root.stopping</c>: |x<sub>n+1</sub> − x<sub>n</sub>| ≤ absTol + relTol·(1 + |x<sub>n+1</sub>|),
/// defaults absTol = 0, relTol = 4ε. ε is the machine epsilon of the floating-point type used.</para>
/// <para>This class lives in <c>Mathesis.Numerics</c> rather than a <c>Mathesis.Numerics.Roots</c> namespace, which would
/// shadow it and break calls such as <c>Roots.Brent(...)</c>.</para>
/// </remarks>
public static class Roots
{
    /// <summary>
    /// Bisection on a bracket [a, b] with f(a)·f(b) &lt; 0 (design entry <c>num.root.bisection</c>). After n midpoints the error
    /// of the latest midpoint is at most (b − a)/2<sup>n</sup>, so n ≥ log₂((b − a)/tol) iterations suffice.
    /// </summary>
    public static RootResult<T> Bisection<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(a, nameof(a));
        Num.ThrowIfNotFinite(b, nameof(b));
        var eps = Num.Eps<T>();
        var (absTol, relTol, fTol) = Num.Tolerances(stop, eps * eps, Num.C<T>(4) * eps);
        var maxIterations = stop?.MaxIterations ?? 2_200; // halving any finite double bracket to its resolution takes fewer steps than this

        if (a > b) (a, b) = (b, a);
        var fa = f(a);
        var fb = f(b);
        var evaluations = 2;
        if (fa == T.Zero) return new(a, fa, T.Zero, 0, evaluations, true, Convergence.FunctionTolerance);
        if (fb == T.Zero) return new(b, fb, T.Zero, 0, evaluations, true, Convergence.FunctionTolerance);
        if (T.IsNaN(fa) || T.IsNaN(fb)) return new(a, fa, b - a, 0, evaluations, false, Convergence.NotFinite);
        if (T.IsNegative(fa) == T.IsNegative(fb)) return new(T.Abs(fa) <= T.Abs(fb) ? a : b, T.Min(T.Abs(fa), T.Abs(fb)), b - a, 0, evaluations, false, Convergence.InvalidBracket);

        var m = a;
        var fm = fa;
        for (var i = 1; i <= maxIterations; i++)
        {
            m = a + (b - a) / Num.Two<T>();
            fm = f(m);
            evaluations++;
            var bound = (b - a) / Num.Two<T>();
            if (fm == T.Zero) return new(m, fm, T.Zero, i, evaluations, true, Convergence.FunctionTolerance);
            if (T.IsNaN(fm)) return new(m, fm, bound, i, evaluations, false, Convergence.NotFinite);
            if (T.IsNegative(fm) == T.IsNegative(fa))
            {
                a = m;
                fa = fm;
            }
            else
            {
                b = m;
            }
            if (fTol > T.Zero && T.Abs(fm) <= fTol) return new(m, fm, bound, i, evaluations, true, Convergence.FunctionTolerance);
            if (bound <= absTol + relTol * T.Abs(m)) return new(m, fm, bound, i, evaluations, true, Convergence.Tolerance);
        }
        return new(m, fm, (b - a) / Num.Two<T>(), maxIterations, evaluations, false, Convergence.MaxIterations);
    }

    /// <summary>
    /// Brent's method on a bracket [a, b] with f(a)·f(b) &lt; 0 (design entry <c>num.root.brent</c>): inverse quadratic interpolation
    /// or the secant step when it is safe, bisection otherwise, so convergence is guaranteed and usually superlinear.
    /// Brent, <i>Algorithms for Minimization without Derivatives</i>, Prentice-Hall 1973, chapter 4.
    /// </summary>
    public static RootResult<T> Brent<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(a, nameof(a));
        Num.ThrowIfNotFinite(b, nameof(b));
        var eps = Num.Eps<T>();
        var (absTol, relTol, fTol) = Num.Tolerances(stop, eps * eps, T.Zero);
        var maxIterations = stop?.MaxIterations ?? 100;
        var two = Num.Two<T>();
        var half = Num.Half<T>();

        var fa = f(a);
        var fb = f(b);
        var evaluations = 2;
        if (fa == T.Zero) return new(a, fa, T.Zero, 0, evaluations, true, Convergence.FunctionTolerance);
        if (fb == T.Zero) return new(b, fb, T.Zero, 0, evaluations, true, Convergence.FunctionTolerance);
        if (T.IsNaN(fa) || T.IsNaN(fb)) return new(a, fa, T.Abs(b - a), 0, evaluations, false, Convergence.NotFinite);
        if (T.IsNegative(fa) == T.IsNegative(fb)) return new(T.Abs(fa) <= T.Abs(fb) ? a : b, T.Min(T.Abs(fa), T.Abs(fb)), T.Abs(b - a), 0, evaluations, false, Convergence.InvalidBracket);

        // b is the best estimate, c is the other end of the bracket, a is the previous b.
        var c = a;
        var fc = fa;
        var step = b - a;
        var previousStep = step;

        for (var i = 1; i <= maxIterations; i++)
        {
            if (T.Abs(fc) < T.Abs(fb))
            {
                a = b;
                b = c;
                c = a;
                fa = fb;
                fb = fc;
                fc = fa;
            }

            var tol = two * eps * T.Abs(b) + half * (absTol + relTol * T.Abs(b));
            var halfWidth = half * (c - b);
            if (fb == T.Zero) return new(b, fb, T.Zero, i - 1, evaluations, true, Convergence.FunctionTolerance);
            if (fTol > T.Zero && T.Abs(fb) <= fTol) return new(b, fb, T.Abs(halfWidth), i - 1, evaluations, true, Convergence.FunctionTolerance);
            if (T.Abs(halfWidth) <= tol) return new(b, fb, T.Abs(halfWidth), i - 1, evaluations, true, Convergence.Tolerance);

            if (T.Abs(previousStep) >= tol && T.Abs(fa) > T.Abs(fb))
            {
                // Try interpolation: p/q is the correction to b.
                T p, q;
                var s = fb / fa;
                if (a == c)
                {
                    p = two * halfWidth * s;
                    q = T.One - s;
                }
                else
                {
                    var qa = fa / fc;
                    var r = fb / fc;
                    p = s * (two * halfWidth * qa * (qa - r) - (b - a) * (r - T.One));
                    q = (qa - T.One) * (r - T.One) * (s - T.One);
                }
                if (p > T.Zero) q = -q;
                p = T.Abs(p);

                // Accept only if it stays well inside the bracket and shrinks faster than bisection would.
                var inside = Num.C<T>(3) * halfWidth * q - T.Abs(tol * q);
                if (two * p < Num.Min(inside, T.Abs(previousStep * q)))
                {
                    previousStep = step;
                    step = p / q;
                }
                else
                {
                    step = halfWidth;
                    previousStep = step;
                }
            }
            else
            {
                step = halfWidth;
                previousStep = step;
            }

            a = b;
            fa = fb;
            b = T.Abs(step) > tol ? b + step : b + Num.Sign(tol, halfWidth);
            fb = f(b);
            evaluations++;
            if (T.IsNaN(fb)) return new(b, fb, T.Abs(c - b), i, evaluations, false, Convergence.NotFinite);

            if (T.IsNegative(fb) == T.IsNegative(fc) && fb != T.Zero)
            {
                c = a;
                fc = fa;
                step = b - a;
                previousStep = step;
            }
        }
        return new(b, fb, T.Abs(c - b) * half, maxIterations, evaluations, false, Convergence.MaxIterations);
    }

    /// <summary>
    /// Newton's method with an analytic derivative (design entry <c>num.root.newton</c>): x ← x − f(x)/f′(x). Quadratic convergence
    /// at a simple root from a close enough start; there is no bracketing guarantee.
    /// </summary>
    public static RootResult<T> Newton<T>(Func<T, T> f, Func<T, T> derivative, T x0, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(derivative);
        return NewtonCore(x => (f(x), derivative(x)), 2, x0, stop);
    }

    /// <summary>
    /// Newton's method with a central finite-difference derivative, h = ∛ε · max(1, |x|) (design entry <c>num.diff.optimal-step</c>).
    /// Costs three function evaluations per iteration.
    /// </summary>
    public static RootResult<T> Newton<T>(Func<T, T> f, T x0, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var cbrtEps = T.Cbrt(Num.Eps<T>());
        return NewtonCore(
            x =>
            {
                var h = cbrtEps * Num.Max(T.One, T.Abs(x));
                return (f(x), (f(x + h) - f(x - h)) / (Num.Two<T>() * h));
            },
            3,
            x0,
            stop);
    }

    /// <summary>
    /// Newton's method with the derivative computed by forward-mode automatic differentiation (<see cref="Dual{T}"/>), exact to
    /// rounding and costing one evaluation of <paramref name="f"/> per iteration.
    /// </summary>
    public static RootResult<T> NewtonAutomatic<T>(Func<Dual<T>, Dual<T>> f, T x0, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        return NewtonCore(
            x =>
            {
                var y = f(Dual<T>.Variable(x));
                return (y.Value, y.Derivative);
            },
            1,
            x0,
            stop);
    }

    private static RootResult<T> NewtonCore<T>(Func<T, (T Value, T Derivative)> evaluate, int evaluationsPerIteration, T x, StoppingCriteria? stop)
        where T : IFloatingPointIeee754<T>
    {
        Num.ThrowIfNotFinite(x, nameof(x));
        var (absTol, relTol, fTol) = Num.Tolerances(stop, T.Zero, Num.C<T>(4) * Num.Eps<T>());
        var maxIterations = stop?.MaxIterations ?? 100;
        var evaluations = 0;
        var (fx, dfx) = evaluate(x);
        evaluations += evaluationsPerIteration;

        for (var i = 1; i <= maxIterations; i++)
        {
            if (!T.IsFinite(fx) || !T.IsFinite(dfx)) return new(x, fx, T.PositiveInfinity, i - 1, evaluations, false, Convergence.NotFinite);
            if (fx == T.Zero || (fTol > T.Zero && T.Abs(fx) <= fTol)) return new(x, fx, T.Zero, i - 1, evaluations, true, Convergence.FunctionTolerance);
            if (dfx == T.Zero) return new(x, fx, T.PositiveInfinity, i - 1, evaluations, false, Convergence.ZeroDerivative);

            var dx = fx / dfx;
            var next = x - dx;
            (fx, dfx) = evaluate(next);
            evaluations += evaluationsPerIteration;
            x = next;
            if (!T.IsFinite(x)) return new(x, fx, T.PositiveInfinity, i, evaluations, false, Convergence.NotFinite);
            if (T.Abs(dx) <= absTol + relTol * (T.One + T.Abs(x))) return new(x, fx, T.Abs(dx), i, evaluations, true, Convergence.Tolerance);
        }
        return new(x, fx, T.PositiveInfinity, maxIterations, evaluations, false, Convergence.MaxIterations);
    }

    /// <summary>
    /// The secant method from two starting points (design entry <c>num.root.secant</c>); order of convergence (1 + √5)/2 at a simple
    /// root, one function evaluation per iteration.
    /// </summary>
    public static RootResult<T> Secant<T>(Func<T, T> f, T x0, T x1, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(x0, nameof(x0));
        Num.ThrowIfNotFinite(x1, nameof(x1));
        var (absTol, relTol, fTol) = Num.Tolerances(stop, T.Zero, Num.C<T>(4) * Num.Eps<T>());
        var maxIterations = stop?.MaxIterations ?? 100;

        var f0 = f(x0);
        var f1 = f(x1);
        var evaluations = 2;
        for (var i = 1; i <= maxIterations; i++)
        {
            if (!T.IsFinite(f0) || !T.IsFinite(f1)) return new(x1, f1, T.PositiveInfinity, i - 1, evaluations, false, Convergence.NotFinite);
            if (f1 == T.Zero || (fTol > T.Zero && T.Abs(f1) <= fTol)) return new(x1, f1, T.Zero, i - 1, evaluations, true, Convergence.FunctionTolerance);
            if (f1 == f0) return new(x1, f1, T.PositiveInfinity, i - 1, evaluations, false, Convergence.ZeroDerivative);

            var dx = f1 * (x1 - x0) / (f1 - f0);
            var next = x1 - dx;
            x0 = x1;
            f0 = f1;
            x1 = next;
            f1 = f(x1);
            evaluations++;
            if (!T.IsFinite(x1)) return new(x1, f1, T.PositiveInfinity, i, evaluations, false, Convergence.NotFinite);
            if (T.Abs(dx) <= absTol + relTol * (T.One + T.Abs(x1))) return new(x1, f1, T.Abs(dx), i, evaluations, true, Convergence.Tolerance);
        }
        return new(x1, f1, T.PositiveInfinity, maxIterations, evaluations, false, Convergence.MaxIterations);
    }
}
