using System.Collections.Immutable;
using System.Numerics;

namespace Mathesis.Numerics.Integration;

/// <summary>
/// Numerical integration, generic over <see cref="IFloatingPointIeee754{TSelf}"/>. Every method returns a
/// <see cref="QuadratureResult{T}"/> with an error estimate and a convergence flag; none throws on non-convergence.
/// </summary>
/// <remarks>
/// Tolerances in <see cref="StoppingCriteria"/> apply to the error estimate: a result is converged when the estimated error is at
/// most max(absTol, relTol·|value|). Defaults are absTol = 0 (but the round-off floor below still ends the search) and
/// relTol = √ε. The Gauss–Kronrod nodes and weights are stored as <see cref="double"/> constants, so <c>Half</c> and <c>float</c>
/// are fine but a future extended-precision type would need its own rule.
/// </remarks>
public static class Quadrature
{
    private const int DefaultMaxSubintervals = 2_000;

    // Gauss–Kronrod 7/15 (QUADPACK qk15; Piessens, de Doncker, Kahaner, Quadpack, 1983). The positive Kronrod nodes in
    // decreasing order, the matching Kronrod weights, and the 7-point Gauss weights for nodes 1, 3, 5, 7.
    private static readonly double[] KronrodNodes =
    [
        0.991455371120812639206854697526329,
        0.949107912342758524526189684047851,
        0.864864423359769072789712788640926,
        0.741531185599394439863864773280788,
        0.586087235467691130294144838258730,
        0.405845151377397166906606412076961,
        0.207784955007898467600689403773245,
        0.0,
    ];

    private static readonly double[] KronrodWeights =
    [
        0.022935322010529224963732008058970,
        0.063092092629978553290700663189204,
        0.104790010322250183839876322541518,
        0.140653259715525918745189590510238,
        0.169004726639267902826583426598550,
        0.190350578064785409913256402421014,
        0.204432940075298892414161999234649,
        0.209482141084727828012999174891714,
    ];

    private static readonly double[] GaussWeights =
    [
        0.129484966168869693270611432679082,
        0.279705391489276667901467771423780,
        0.381830050505118944950369775488975,
        0.417959183673469387755102040816327,
    ];

    /// <summary>
    /// Integrates over [a, b], where either end may be infinite: finite ranges use <see cref="GaussKronrod"/>, infinite ones the
    /// substitutions of <see cref="Infinite"/>.
    /// </summary>
    public static QuadratureResult<T> Integrate<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T> =>
        T.IsInfinity(a) || T.IsInfinity(b) ? Infinite(f, a, b, stop) : GaussKronrod(f, a, b, stop);

    /// <summary>
    /// Adaptive Simpson's rule with Richardson correction (design entry <c>num.quad.simpson</c>): an interval is accepted when
    /// |S₂ − S₁| ≤ 15·tol, with the tolerance halved at each bisection.
    /// </summary>
    public static QuadratureResult<T> AdaptiveSimpson<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(a, nameof(a));
        Num.ThrowIfNotFinite(b, nameof(b));
        if (a == b) return new(T.Zero, T.Zero, 0, 0, true, []);
        var sign = T.One;
        if (a > b)
        {
            (a, b) = (b, a);
            sign = -T.One;
        }
        var eps = Num.Eps<T>();
        var maxSubintervals = stop?.MaxIterations ?? DefaultMaxSubintervals;
        var warnings = ImmutableArray.CreateBuilder<string>();

        var evaluations = 0;
        T F(T x)
        {
            evaluations++;
            return f(x);
        }

        var fa = F(a);
        var fb = F(b);
        var m = Num.Half<T>() * (a + b);
        var fm = F(m);
        var whole = Simpson(a, b, fa, fm, fb);
        if (!T.IsFinite(whole)) return new(whole, T.PositiveInfinity, evaluations, 1, false, ["The integrand is not finite on the sampled points."]);

        // A coarse 8-panel Simpson sum sets the scale for the relative tolerance and the round-off floor, so integrals
        // whose 3-point estimate happens to vanish (sin over a full period) still get a meaningful target.
        var scale = T.Zero;
        var rough = T.Zero;
        for (var i = 0; i <= 8; i++)
        {
            var fi = F(a + (b - a) * T.CreateChecked(i) / Num.C<T>(8));
            scale = Num.Max(scale, T.Abs(fi));
            rough += (i == 0 || i == 8 ? T.One : i % 2 == 1 ? Num.C<T>(4) : Num.Two<T>()) * fi;
        }
        rough *= (b - a) / Num.C<T>(24);
        var (absTol, relTol, _) = Num.Tolerances<T>(stop, T.Zero, T.Sqrt(eps));
        var target = Num.Max(absTol, relTol * Num.Max(T.Abs(whole), T.Abs(rough)));
        var floor = Num.C<T>(10) * eps * (b - a) * scale;
        target = Num.Max(target, floor);

        var value = T.Zero;
        var error = T.Zero;
        var accepted = 0;
        var stack = new Stack<(T A, T B, T Fa, T Fm, T Fb, T Whole, T Tol)>();
        stack.Push((a, b, fa, fm, fb, whole, target));
        var truncated = false;
        while (stack.Count > 0)
        {
            var (ia, ib, ifa, ifm, ifb, iwhole, itol) = stack.Pop();
            var mid = Num.Half<T>() * (ia + ib);
            var lm = Num.Half<T>() * (ia + mid);
            var rm = Num.Half<T>() * (mid + ib);
            var flm = F(lm);
            var frm = F(rm);
            var left = Simpson(ia, mid, ifa, flm, ifm);
            var right = Simpson(mid, ib, ifm, frm, ifb);
            var delta = left + right - iwhole;
            if (T.Abs(delta) <= Num.C<T>(15) * itol || stack.Count + accepted >= maxSubintervals || mid == ia || mid == ib)
            {
                if (T.Abs(delta) > Num.C<T>(15) * itol) truncated = true;
                value += left + right + delta / Num.C<T>(15);
                error += T.Abs(delta) / Num.C<T>(15);
                accepted++;
            }
            else
            {
                var half = itol / Num.Two<T>();
                stack.Push((mid, ib, ifm, frm, ifb, right, half));
                stack.Push((ia, mid, ifa, flm, ifm, left, half));
            }
        }

        if (truncated) warnings.Add("Subinterval limit reached before the tolerance was met; the integrand may be singular or oscillatory.");
        var converged = !truncated && error <= target;
        if (!T.IsFinite(value)) return new(value, T.PositiveInfinity, evaluations, accepted, false, ["The integrand is not finite."]);
        return new(sign * value, error, evaluations, accepted, converged, warnings.ToImmutable());

        static T Simpson(T lo, T hi, T flo, T fmid, T fhi) => (hi - lo) / Num.C<T>(6) * (flo + Num.C<T>(4) * fmid + fhi);
    }

    /// <summary>
    /// Adaptive Gauss–Kronrod G7/K15 quadrature (design entry <c>num.quad.gauss-kronrod</c>): the difference between the nested 7-point
    /// Gauss and 15-point Kronrod rules estimates the error, and the interval with the largest error is bisected until the total
    /// meets the tolerance. Error scaling follows QUADPACK (Piessens et al. 1983). The Kronrod rule integrates polynomials of
    /// degree up to 22 exactly and the integrand is never evaluated at the end points, so mild endpoint singularities such as
    /// ln(x)/√x on (0, 1] are handled.
    /// </summary>
    public static QuadratureResult<T> GaussKronrod<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(a, nameof(a));
        Num.ThrowIfNotFinite(b, nameof(b));
        if (a == b) return new(T.Zero, T.Zero, 0, 0, true, []);
        var sign = T.One;
        if (a > b)
        {
            (a, b) = (b, a);
            sign = -T.One;
        }
        var eps = Num.Eps<T>();
        var maxSubintervals = stop?.MaxIterations ?? DefaultMaxSubintervals;
        var (absTol, relTol, _) = Num.Tolerances<T>(stop, T.Zero, T.Sqrt(eps));
        var warnings = ImmutableArray.CreateBuilder<string>();
        var evaluations = 0;

        var first = Rule(f, a, b);
        evaluations += 15;
        if (!T.IsFinite(first.Value)) return new(first.Value, T.PositiveInfinity, evaluations, 1, false, ["The integrand is not finite on the sampled points."]);

        // Max-heap on the error estimate.
        var queue = new PriorityQueue<Segment<T>, T>(Comparer<T>.Create((x, y) => y.CompareTo(x)));
        queue.Enqueue(first, first.Error);
        var value = first.Value;
        var error = first.Error;
        var absIntegral = first.AbsIntegral;
        var count = 1;
        var roundOff = false;

        while (true)
        {
            var target = Num.Max(absTol, relTol * T.Abs(value));
            if (error <= target) break;
            if (error <= Num.C<T>(50) * eps * absIntegral)
            {
                roundOff = true;
                break;
            }
            if (count >= maxSubintervals) break;

            var worst = queue.Dequeue();
            var mid = Num.Half<T>() * (worst.A + worst.B);
            if (mid <= worst.A || mid >= worst.B)
            {
                queue.Enqueue(worst, worst.Error);
                warnings.Add("An interval shrank to machine precision; the integrand may be singular.");
                break;
            }
            var left = Rule(f, worst.A, mid);
            var right = Rule(f, mid, worst.B);
            evaluations += 30;
            queue.Enqueue(left, left.Error);
            queue.Enqueue(right, right.Error);
            count++;

            value = T.Zero;
            error = T.Zero;
            absIntegral = T.Zero;
            foreach (var (segment, _) in queue.UnorderedItems)
            {
                value += segment.Value;
                error += segment.Error;
                absIntegral += segment.AbsIntegral;
            }
            if (!T.IsFinite(value)) return new(value, T.PositiveInfinity, evaluations, count, false, ["The integrand is not finite."]);
        }

        var converged = error <= Num.Max(absTol, relTol * T.Abs(value)) || roundOff;
        if (roundOff) warnings.Add("The result is limited by round-off.");
        if (!converged && count >= maxSubintervals) warnings.Add("Subinterval limit reached before the tolerance was met; the integrand may be singular or oscillatory.");
        return new(sign * value, error, evaluations, count, converged, warnings.ToImmutable());
    }

    private readonly record struct Segment<T>(T A, T B, T Value, T Error, T AbsIntegral);

    private static Segment<T> Rule<T>(Func<T, T> f, T a, T b)
        where T : IFloatingPointIeee754<T>
    {
        var center = Num.Half<T>() * (a + b);
        var half = Num.Half<T>() * (b - a);
        var fc = f(center);
        var resultK = Num.C<T>(KronrodWeights[7]) * fc;
        var resultG = Num.C<T>(GaussWeights[3]) * fc;
        var resabs = T.Abs(resultK);
        var f1 = new T[7];
        var f2 = new T[7];
        for (var j = 0; j < 7; j++)
        {
            var dx = half * Num.C<T>(KronrodNodes[j]);
            var y1 = f(center - dx);
            var y2 = f(center + dx);
            f1[j] = y1;
            f2[j] = y2;
            var wk = Num.C<T>(KronrodWeights[j]);
            resultK += wk * (y1 + y2);
            resabs += wk * (T.Abs(y1) + T.Abs(y2));
            if (j % 2 == 1) resultG += Num.C<T>(GaussWeights[j / 2]) * (y1 + y2);
        }

        var mean = resultK * Num.Half<T>();
        var resasc = Num.C<T>(KronrodWeights[7]) * T.Abs(fc - mean);
        for (var j = 0; j < 7; j++)
        {
            resasc += Num.C<T>(KronrodWeights[j]) * (T.Abs(f1[j] - mean) + T.Abs(f2[j] - mean));
        }

        var h = T.Abs(half);
        var value = resultK * half;
        var absIntegral = resabs * h;
        resasc *= h;
        var error = T.Abs((resultK - resultG) * half);
        if (resasc != T.Zero && error != T.Zero)
        {
            error = resasc * Num.Min(T.One, T.Pow(Num.C<T>(200) * error / resasc, Num.C<T>(1.5)));
        }
        var eps = Num.Eps<T>();
        if (absIntegral > T.Epsilon / (Num.C<T>(50) * eps))
        {
            error = Num.Max(Num.C<T>(50) * eps * absIntegral, error);
        }
        return new(a, b, value, error, absIntegral);
    }

    /// <summary>
    /// Integrates over an infinite or half-infinite range by a change of variable onto a finite interval followed by
    /// <see cref="GaussKronrod"/>: [a, ∞) uses x = a + t/(1 − t), (−∞, b] uses x = b − t/(1 − t), both for t ∈ [0, 1), and
    /// (−∞, ∞) uses x = t/(1 − t²) for t ∈ (−1, 1). Either bound may be infinite; a finite range falls through unchanged.
    /// </summary>
    public static QuadratureResult<T> Infinite<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        if (T.IsNaN(a) || T.IsNaN(b)) throw new ArgumentOutOfRangeException(nameof(a), "Bounds must not be NaN.");
        if (a == b) return new(T.Zero, T.Zero, 0, 0, true, []);
        if (a > b)
        {
            var r = Infinite(f, b, a, stop);
            return r with { Value = -r.Value };
        }
        if (!T.IsInfinity(a) && !T.IsInfinity(b)) return GaussKronrod(f, a, b, stop);
        var one = T.One;
        if (T.IsNegativeInfinity(a) && T.IsPositiveInfinity(b))
        {
            return GaussKronrod<T>(
                t =>
                {
                    var d = one - t * t;
                    var x = t / d;
                    var jacobian = (one + t * t) / (d * d);
                    var y = f(x) * jacobian;
                    return T.IsFinite(x) && T.IsFinite(y) ? y : T.Zero;
                },
                -one,
                one,
                stop);
        }
        if (T.IsPositiveInfinity(b))
        {
            return GaussKronrod<T>(
                t =>
                {
                    var d = one - t;
                    var y = f(a + t / d) * (one / (d * d));
                    return T.IsFinite(y) ? y : T.Zero;
                },
                T.Zero,
                one,
                stop);
        }
        return GaussKronrod<T>(
            t =>
            {
                var d = one - t;
                var y = f(b - t / d) * (one / (d * d));
                return T.IsFinite(y) ? y : T.Zero;
            },
            T.Zero,
            one,
            stop);
    }

    /// <summary>
    /// Romberg integration (design entry <c>num.quad.romberg</c>): Richardson extrapolation of trapezoid sums with 1, 2, 4, … panels.
    /// Converges quickly for smooth integrands; use <see cref="GaussKronrod"/> for singular ones. <see cref="StoppingCriteria.MaxIterations"/>
    /// is the maximum number of halvings (default 20, at most 30).
    /// </summary>
    public static QuadratureResult<T> Romberg<T>(Func<T, T> f, T a, T b, StoppingCriteria? stop = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        Num.ThrowIfNotFinite(a, nameof(a));
        Num.ThrowIfNotFinite(b, nameof(b));
        if (a == b) return new(T.Zero, T.Zero, 0, 0, true, []);
        var maxLevels = Math.Clamp(stop?.MaxIterations ?? 20, 2, 30);
        var eps = Num.Eps<T>();
        var (absTol, relTol, _) = Num.Tolerances<T>(stop, T.Zero, T.Sqrt(eps));

        var width = b - a;
        var evaluations = 2;
        var previous = new T[maxLevels + 1];
        var current = new T[maxLevels + 1];
        previous[0] = Num.Half<T>() * width * (f(a) + f(b));
        if (!T.IsFinite(previous[0])) return new(previous[0], T.PositiveInfinity, evaluations, 1, false, ["The integrand is not finite at an end point."]);

        for (var level = 1; level <= maxLevels; level++)
        {
            // Trapezoid sum with 2^level panels reuses the previous sum and adds the new midpoints.
            var panels = 1 << (level - 1);
            var h = width / T.CreateChecked(panels);
            var sum = T.Zero;
            for (var i = 0; i < panels; i++) sum += f(a + (T.CreateChecked(i) + Num.Half<T>()) * h);
            evaluations += panels;
            current[0] = Num.Half<T>() * previous[0] + Num.Half<T>() * h * sum;

            var factor = Num.C<T>(4);
            for (var k = 1; k <= level; k++)
            {
                current[k] = current[k - 1] + (current[k - 1] - previous[k - 1]) / (factor - T.One);
                factor *= Num.C<T>(4);
            }
            var error = T.Abs(current[level] - previous[level - 1]);
            var value = current[level];
            if (!T.IsFinite(value)) return new(value, T.PositiveInfinity, evaluations, level, false, ["The integrand is not finite."]);
            if (level >= 3 && error <= Num.Max(absTol, relTol * T.Abs(value)))
            {
                return new(value, error, evaluations, level, true, []);
            }
            if (level == maxLevels)
            {
                return new(value, error, evaluations, level, false, ["Level limit reached before the tolerance was met; the integrand may not be smooth."]);
            }
            (previous, current) = (current, previous);
        }
        throw new InvalidOperationException("Unreachable.");
    }
}
