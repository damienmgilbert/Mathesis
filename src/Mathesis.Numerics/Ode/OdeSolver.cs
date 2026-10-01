using System.Collections.Immutable;
using System.Numerics;

namespace Mathesis.Numerics.Ode;

/// <summary>The right-hand side f(t, y) of y′ = f(t, y); writes the derivative into <paramref name="dydt"/>.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="t">The time.</param>
/// <param name="y">The state vector.</param>
/// <param name="dydt">Receives f(t, y); has the same length as <paramref name="y"/>.</param>
public delegate void OdeFunction<T>(T t, ReadOnlySpan<T> y, Span<T> dydt);

/// <summary>Step-size control for <see cref="OdeSolver.DormandPrince{T}(OdeFunction{T}, T, T[], T, OdeOptions?)"/>.</summary>
/// <param name="RelativeTolerance">Relative error tolerance per step; <c>null</c> selects max(1e-8, 100ε).</param>
/// <param name="AbsoluteTolerance">Absolute error tolerance per step; <c>null</c> selects 1% of the relative tolerance.</param>
/// <param name="InitialStep">First step size; <c>null</c> selects one automatically.</param>
/// <param name="MaxStep">Largest step size allowed; <c>null</c> means the whole interval.</param>
/// <param name="MaxSteps">Cap on accepted plus rejected steps.</param>
public sealed record OdeOptions(
    double? RelativeTolerance = null,
    double? AbsoluteTolerance = null,
    double? InitialStep = null,
    double? MaxStep = null,
    int MaxSteps = 100_000);

/// <summary>Initial-value problem solvers for y′ = f(t, y), generic over <see cref="IFloatingPointIeee754{TSelf}"/>.</summary>
/// <remarks>Non-convergence (step too small, step cap reached) is reported in the <see cref="OdeSolution{T}"/>, not thrown.</remarks>
public static class OdeSolver
{
    /// <summary>
    /// Classical fixed-step Runge–Kutta of order 4 (catalog <c>num.ode.rk4</c>) from <paramref name="t0"/> to
    /// <paramref name="t1"/> in <paramref name="steps"/> equal steps; global error O(h⁴). The result has no dense output.
    /// </summary>
    public static OdeSolution<T> Rk4<T>(OdeFunction<T> f, T t0, T[] y0, T t1, int steps)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(y0);
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        Num.ThrowIfNotFinite(t0, nameof(t0));
        Num.ThrowIfNotFinite(t1, nameof(t1));
        var n = y0.Length;
        var h = (t1 - t0) / T.CreateChecked(steps);
        var half = Num.Half<T>();
        var sixth = T.One / Num.C<T>(6);

        var times = ImmutableArray.CreateBuilder<T>(steps + 1);
        var states = ImmutableArray.CreateBuilder<ImmutableArray<T>>(steps + 1);
        var y = (T[])y0.Clone();
        var k1 = new T[n];
        var k2 = new T[n];
        var k3 = new T[n];
        var k4 = new T[n];
        var tmp = new T[n];
        times.Add(t0);
        states.Add([.. y]);
        var evaluations = 0;

        for (var s = 0; s < steps; s++)
        {
            var t = t0 + T.CreateChecked(s) * h;
            f(t, y, k1);
            for (var i = 0; i < n; i++) tmp[i] = y[i] + half * h * k1[i];
            f(t + half * h, tmp, k2);
            for (var i = 0; i < n; i++) tmp[i] = y[i] + half * h * k2[i];
            f(t + half * h, tmp, k3);
            for (var i = 0; i < n; i++) tmp[i] = y[i] + h * k3[i];
            f(t + h, tmp, k4);
            evaluations += 4;
            for (var i = 0; i < n; i++) y[i] += h * sixth * (k1[i] + Num.Two<T>() * (k2[i] + k3[i]) + k4[i]);

            var tNext = s == steps - 1 ? t1 : t + h;
            times.Add(tNext);
            states.Add([.. y]);
            foreach (var v in y)
            {
                if (!T.IsFinite(v)) return new(times.ToImmutable(), states.ToImmutable(), false, Convergence.NotFinite, s + 1, 0, evaluations, null);
            }
        }
        return new(times.ToImmutable(), states.ToImmutable(), true, Convergence.Tolerance, steps, 0, evaluations, null);
    }

    /// <summary>Fixed-step RK4 for a scalar equation y′ = f(t, y).</summary>
    public static OdeSolution<T> Rk4<T>(Func<T, T, T> f, T t0, T y0, T t1, int steps)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        return Rk4<T>((T t, ReadOnlySpan<T> y, Span<T> dydt) => dydt[0] = f(t, y[0]), t0, [y0], t1, steps);
    }

    /// <summary>Adaptive Dormand–Prince 5(4) for a scalar equation y′ = f(t, y).</summary>
    public static OdeSolution<T> DormandPrince<T>(Func<T, T, T> f, T t0, T y0, T t1, OdeOptions? options = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        return DormandPrince<T>((T t, ReadOnlySpan<T> y, Span<T> dydt) => dydt[0] = f(t, y[0]), t0, [y0], t1, options);
    }

    // Dormand and Prince, J. Comput. Appl. Math. 6 (1980) 19–26: the 5th-order solution is propagated (local extrapolation),
    // the embedded 4th-order solution gives the error estimate, and the last stage is reused as the first of the next step.
    private static readonly double[] C = [0.0, 1.0 / 5, 3.0 / 10, 4.0 / 5, 8.0 / 9, 1.0, 1.0];

    private static readonly double[][] A =
    [
        [],
        [1.0 / 5],
        [3.0 / 40, 9.0 / 40],
        [44.0 / 45, -56.0 / 15, 32.0 / 9],
        [19372.0 / 6561, -25360.0 / 2187, 64448.0 / 6561, -212.0 / 729],
        [9017.0 / 3168, -355.0 / 33, 46732.0 / 5247, 49.0 / 176, -5103.0 / 18656],
        [35.0 / 384, 0.0, 500.0 / 1113, 125.0 / 192, -2187.0 / 6784, 11.0 / 84],
    ];

    // Differences between the 5th- and 4th-order weights.
    private static readonly double[] E = [71.0 / 57600, 0.0, -71.0 / 16695, 71.0 / 1920, -17253.0 / 339200, 22.0 / 525, -1.0 / 40];

    // Hairer, Nørsett and Wanner, Solving ODEs I, 2nd ed., §II.6: coefficients of the continuous extension of dopri5.
    private static readonly double[] D = [-12715105075.0 / 11282082432, 0.0, 87487479700.0 / 32700410799, -10690763975.0 / 1880347072, 701980252875.0 / 199316789632, -1453857185.0 / 822651844, 69997945.0 / 29380423];

    /// <summary>
    /// Adaptive Dormand–Prince 5(4) integration from <paramref name="t0"/> to <paramref name="t1"/> (either direction), with the
    /// step size chosen from the embedded error estimate, h ← h·0.9·(1/err)^(1/5) (catalog <c>num.ode.step-control</c>), and a
    /// continuous extension (<see cref="OdeSolution{T}.Evaluate"/>) of order 4 between steps. The accepted step values are fifth-order
    /// accurate; values between steps come from the dense output, whose error is larger (order 4).
    /// </summary>
    public static OdeSolution<T> DormandPrince<T>(OdeFunction<T> f, T t0, T[] y0, T t1, OdeOptions? options = null)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(y0);
        Num.ThrowIfNotFinite(t0, nameof(t0));
        Num.ThrowIfNotFinite(t1, nameof(t1));
        var n = y0.Length;
        var eps = Num.Eps<T>();
        var rtol = options?.RelativeTolerance is { } r ? T.CreateChecked(r) : Num.Max(Num.C<T>(1e-8), Num.C<T>(100) * eps);
        var atol = options?.AbsoluteTolerance is { } at ? T.CreateChecked(at) : rtol * Num.C<T>(0.01);
        var maxSteps = options?.MaxSteps ?? 100_000;
        var span = t1 - t0;
        var direction = span < T.Zero ? -T.One : T.One;
        var maxStep = options?.MaxStep is { } ms ? T.Abs(T.CreateChecked(ms)) : T.Abs(span);

        var times = ImmutableArray.CreateBuilder<T>();
        var states = ImmutableArray.CreateBuilder<ImmutableArray<T>>();
        var dense = new DenseOutput<T>(direction);
        var y = (T[])y0.Clone();
        times.Add(t0);
        states.Add([.. y]);
        if (span == T.Zero) return new(times.ToImmutable(), states.ToImmutable(), true, Convergence.Tolerance, 0, 0, 0, dense);

        var k = new T[7][];
        for (var i = 0; i < 7; i++) k[i] = new T[n];
        var stage = new T[n];
        var yNew = new T[n];
        var scale = new T[n];
        var evaluations = 0;

        f(t0, y, k[0]);
        evaluations++;

        // Starting step size (Hairer, Nørsett, Wanner, §II.4).
        T h;
        if (options?.InitialStep is { } h0Option)
        {
            h = T.Abs(T.CreateChecked(h0Option));
        }
        else
        {
            for (var i = 0; i < n; i++) scale[i] = atol + rtol * T.Abs(y[i]);
            var d0 = T.Zero;
            var d1 = T.Zero;
            for (var i = 0; i < n; i++)
            {
                d0 += (y[i] / scale[i]) * (y[i] / scale[i]);
                d1 += (k[0][i] / scale[i]) * (k[0][i] / scale[i]);
            }
            d0 = n == 0 ? T.Zero : T.Sqrt(d0 / T.CreateChecked(n));
            d1 = n == 0 ? T.Zero : T.Sqrt(d1 / T.CreateChecked(n));
            var h0 = d0 < Num.C<T>(1e-5) || d1 < Num.C<T>(1e-5) ? Num.C<T>(1e-6) : Num.C<T>(0.01) * d0 / d1;
            h0 = Num.Min(h0, T.Abs(span));
            for (var i = 0; i < n; i++) stage[i] = y[i] + direction * h0 * k[0][i];
            f(t0 + direction * h0, stage, k[1]);
            evaluations++;
            var d2 = T.Zero;
            for (var i = 0; i < n; i++)
            {
                var q = (k[1][i] - k[0][i]) / scale[i];
                d2 += q * q;
            }
            d2 = n == 0 ? T.Zero : T.Sqrt(d2 / T.CreateChecked(n)) / h0;
            var dMax = Num.Max(d1, d2);
            var h1 = dMax <= Num.C<T>(1e-15)
                ? Num.Max(Num.C<T>(1e-6), h0 * Num.C<T>(1e-3))
                : T.Pow(Num.C<T>(0.01) / dMax, Num.C<T>(0.2));
            h = Num.Min(Num.C<T>(100) * h0, h1);
        }
        h = Num.Min(h, maxStep);

        var t = t0;
        var accepted = 0;
        var rejected = 0;
        while (true)
        {
            if (accepted + rejected >= maxSteps)
            {
                return new(times.ToImmutable(), states.ToImmutable(), false, Convergence.MaxIterations, accepted, rejected, evaluations, dense);
            }
            var remaining = T.Abs(t1 - t);
            var last = h >= remaining;
            if (last) h = remaining;
            if (h <= Num.C<T>(16) * eps * T.Abs(t) || h == T.Zero)
            {
                return new(times.ToImmutable(), states.ToImmutable(), false, Convergence.StepTooSmall, accepted, rejected, evaluations, dense);
            }
            var hs = direction * h;

            // Stages 2..7.
            for (var s = 1; s < 7; s++)
            {
                for (var i = 0; i < n; i++)
                {
                    var acc = T.Zero;
                    for (var j = 0; j < s; j++) acc += Num.C<T>(A[s][j]) * k[j][i];
                    stage[i] = y[i] + hs * acc;
                }
                if (s == 6)
                {
                    Array.Copy(stage, yNew, n);
                }
                f(t + Num.C<T>(C[s]) * hs, stage, k[s]);
                evaluations++;
            }

            // Error estimate from the embedded pair.
            var err = T.Zero;
            for (var i = 0; i < n; i++)
            {
                var e = T.Zero;
                for (var j = 0; j < 7; j++) e += Num.C<T>(E[j]) * k[j][i];
                e *= hs;
                var sc = atol + rtol * Num.Max(T.Abs(y[i]), T.Abs(yNew[i]));
                err += (e / sc) * (e / sc);
            }
            err = n == 0 ? T.Zero : T.Sqrt(err / T.CreateChecked(n));
            if (!T.IsFinite(err)) err = Num.C<T>(1e10);

            if (err <= T.One)
            {
                dense.Add(t, hs, y, yNew, k);
                t = last ? t1 : t + hs;
                Array.Copy(yNew, y, n);
                Array.Copy(k[6], k[0], n);
                accepted++;
                times.Add(t);
                states.Add([.. y]);
                foreach (var v in y)
                {
                    if (!T.IsFinite(v)) return new(times.ToImmutable(), states.ToImmutable(), false, Convergence.NotFinite, accepted, rejected, evaluations, dense);
                }
                if (last) return new(times.ToImmutable(), states.ToImmutable(), true, Convergence.Tolerance, accepted, rejected, evaluations, dense);
                var grow = err == T.Zero ? Num.C<T>(5) : Num.Min(Num.C<T>(5), Num.C<T>(0.9) * T.Pow(err, Num.C<T>(-0.2)));
                h = Num.Min(h * Num.Max(Num.C<T>(0.2), grow), maxStep);
            }
            else
            {
                rejected++;
                h *= Num.Max(Num.C<T>(0.2), Num.C<T>(0.9) * T.Pow(err, Num.C<T>(-0.2)));
            }
        }
    }

    private sealed class DenseOutput<T> : IDenseOutput<T>
        where T : IFloatingPointIeee754<T>
    {
        private readonly T _direction;
        private readonly List<T> _starts = [];
        private readonly List<T> _steps = [];
        private readonly List<T[][]> _coefficients = [];

        public DenseOutput(T direction) => _direction = direction;

        public void Add(T t, T h, T[] y, T[] yNew, T[][] k)
        {
            var n = y.Length;
            var rc = new T[5][];
            for (var i = 0; i < 5; i++) rc[i] = new T[n];
            for (var i = 0; i < n; i++)
            {
                var ydiff = yNew[i] - y[i];
                var bspl = h * k[0][i] - ydiff;
                rc[0][i] = y[i];
                rc[1][i] = ydiff;
                rc[2][i] = bspl;
                rc[3][i] = ydiff - h * k[6][i] - bspl;
                var acc = T.Zero;
                for (var j = 0; j < 7; j++) acc += Num.C<T>(D[j]) * k[j][i];
                rc[4][i] = h * acc;
            }
            _starts.Add(t);
            _steps.Add(h);
            _coefficients.Add(rc);
        }

        public T[] Evaluate(T t)
        {
            if (_starts.Count == 0) throw new InvalidOperationException("No steps were taken.");
            var first = _starts[0];
            var lastEnd = _starts[^1] + _steps[^1];
            var lo = T.Min(first, lastEnd);
            var hi = T.Max(first, lastEnd);
            if (!(t >= lo && t <= hi)) throw new ArgumentOutOfRangeException(nameof(t), "Time is outside the integration interval.");

            // Binary search for the last step starting at or before t (in the integration direction).
            var low = 0;
            var high = _starts.Count - 1;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (_direction * (t - _starts[mid]) >= T.Zero) low = mid;
                else high = mid - 1;
            }
            var rc = _coefficients[low];
            var theta = (t - _starts[low]) / _steps[low];
            var theta1 = T.One - theta;
            var n = rc[0].Length;
            var result = new T[n];
            for (var i = 0; i < n; i++)
            {
                result[i] = rc[0][i] + theta * (rc[1][i] + theta1 * (rc[2][i] + theta * (rc[3][i] + theta1 * rc[4][i])));
            }
            return result;
        }
    }
}
