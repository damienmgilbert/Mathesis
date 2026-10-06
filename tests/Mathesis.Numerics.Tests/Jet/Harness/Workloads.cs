namespace Mathesis.Numerics.Tests;

/// <summary>
/// A vector function written once, generic over <see cref="ISpikeScalar{TSelf}"/>, with the seeded evaluation points the correctness checks and the
/// benchmarks use (Technesis prototype report).
/// </summary>
public interface IWorkload
{
    /// <summary>A short name for reports.</summary>
    static abstract string Name { get; }

    /// <summary>The number of inputs N (the Jacobian has N columns).</summary>
    static abstract int Inputs { get; }

    /// <summary>The number of outputs (the Jacobian has this many rows).</summary>
    static abstract int Outputs { get; }

    /// <summary>The evaluation points: each is an array of <see cref="Inputs"/> values, reproducible from the fixed seed.</summary>
    static abstract double[][] Points();

    /// <summary>Evaluates the function: <paramref name="y"/> receives <see cref="Outputs"/> values from <see cref="Inputs"/> values in <paramref name="x"/>.</summary>
    static abstract void Evaluate<T>(ReadOnlySpan<T> x, Span<T> y) where T : struct, ISpikeScalar<T>;
}

/// <summary>
/// W1: forward kinematics of a 6R arm with classical (standard) Denavit–Hartenberg parameters of a fixed PUMA-like geometry (not a specific
/// published set). Inputs are the six joint angles; outputs are the end-effector position (3) followed by the rotation matrix in row-major
/// order (9), so the Jacobian is 12×6. Each link transform is the standard DH product Rz(θ)·Tz(d)·Tx(a)·Rx(α) (Siciliano et al., chapter 2).
/// </summary>
public readonly struct ForwardKinematics6R : IWorkload
{
    private static readonly double[] LinkA = [0.0, 0.4318, 0.0203, 0.0, 0.0, 0.0];
    private static readonly double[] LinkD = [0.0, 0.0, 0.15, 0.4318, 0.0, 0.05];
    private static readonly double[] CosAlpha = [Math.Cos(-Math.PI / 2), 1.0, Math.Cos(Math.PI / 2), Math.Cos(-Math.PI / 2), Math.Cos(Math.PI / 2), 1.0];
    private static readonly double[] SinAlpha = [Math.Sin(-Math.PI / 2), 0.0, Math.Sin(Math.PI / 2), Math.Sin(-Math.PI / 2), Math.Sin(Math.PI / 2), 0.0];

    /// <inheritdoc />
    public static string Name => "W1 6R kinematics";

    /// <inheritdoc />
    public static int Inputs => 6;

    /// <inheritdoc />
    public static int Outputs => 12;

    /// <inheritdoc />
    public static double[][] Points()
    {
        var random = new Random(1001);
        var points = new double[100][];
        for (var k = 0; k < points.Length; k++)
        {
            points[k] = new double[6];
            for (var i = 0; i < 6; i++) points[k][i] = random.NextDouble() * 2.0 * Math.PI - Math.PI;
        }
        return points;
    }

    /// <inheritdoc />
    public static void Evaluate<T>(ReadOnlySpan<T> x, Span<T> y) where T : struct, ISpikeScalar<T>
    {
        var one = T.Constant(1.0);
        var zero = T.Constant(0.0);
        T r00 = one, r01 = zero, r02 = zero, r10 = zero, r11 = one, r12 = zero, r20 = zero, r21 = zero, r22 = one;
        T px = zero, py = zero, pz = zero;
        for (var i = 0; i < 6; i++)
        {
            var c = T.Cos(x[i]);
            var s = T.Sin(x[i]);
            var ca = T.Constant(CosAlpha[i]);
            var sa = T.Constant(SinAlpha[i]);

            // Position first, with the rotation of the previous links: p += R · (a·cosθ, a·sinθ, d).
            var a = LinkA[i];
            var d = LinkD[i];
            if (a != 0.0)
            {
                var ta = T.Constant(a);
                var t0 = ta * c;
                var t1 = ta * s;
                px = px + (r00 * t0 + r01 * t1);
                py = py + (r10 * t0 + r11 * t1);
                pz = pz + (r20 * t0 + r21 * t1);
            }
            if (d != 0.0)
            {
                var td = T.Constant(d);
                px = px + r02 * td;
                py = py + r12 * td;
                pz = pz + r22 * td;
            }

            // R ← R · [[c, −s·cα, s·sα], [s, c·cα, −c·sα], [0, sα, cα]].
            var m01 = -(s * ca);
            var m02 = s * sa;
            var m11 = c * ca;
            var m12 = -(c * sa);
            var n00 = r00 * c + r01 * s;
            var n01 = r00 * m01 + r01 * m11 + r02 * sa;
            var n02 = r00 * m02 + r01 * m12 + r02 * ca;
            var n10 = r10 * c + r11 * s;
            var n11 = r10 * m01 + r11 * m11 + r12 * sa;
            var n12 = r10 * m02 + r11 * m12 + r12 * ca;
            var n20 = r20 * c + r21 * s;
            var n21 = r20 * m01 + r21 * m11 + r22 * sa;
            var n22 = r20 * m02 + r21 * m12 + r22 * ca;
            r00 = n00; r01 = n01; r02 = n02;
            r10 = n10; r11 = n11; r12 = n12;
            r20 = n20; r21 = n21; r22 = n22;
        }
        y[0] = px; y[1] = py; y[2] = pz;
        y[3] = r00; y[4] = r01; y[5] = r02;
        y[6] = r10; y[7] = r11; y[8] = r12;
        y[9] = r20; y[10] = r21; y[11] = r22;
    }
}

/// <summary>
/// W2: one step of a 15-state INS error-state propagation, x = (δp, δv, δθ, bₐ, b_g), dt = 0.01, with R = Exp(δθ) by Rodrigues's formula
/// (Murray, Li and Sastry 1994, chapter 2): R = I + A·[δθ]× + B·[δθ]×² with A = sin‖δθ‖/‖δθ‖ and B = (1 − cos‖δθ‖)/‖δθ‖². Below ‖δθ‖ = 0.1
/// (selected by a value comparison on ‖δθ‖²) A and B come from their Maclaurin series to the θ⁸ term, which has no square root, so the Jacobian
/// is finite at δθ = 0; the series truncation error is below 1e-17.
/// </summary>
public readonly struct InsErrorState15 : IWorkload
{
    private const double Dt = 0.01;
    private const double SeriesLimit = 0.01;    // ‖δθ‖² below which the series is used

    /// <inheritdoc />
    public static string Name => "W2 INS 15-state";

    /// <inheritdoc />
    public static int Inputs => 15;

    /// <inheritdoc />
    public static int Outputs => 15;

    /// <inheritdoc />
    public static double[][] Points()
    {
        var random = new Random(1002);
        var points = new double[101][];
        for (var k = 0; k < 100; k++)
        {
            var x = new double[15];
            for (var i = 0; i < 15; i++) x[i] = random.NextDouble() * 2.0 - 1.0;
            // ‖δθ‖ between 0.01 and 1: scale a random direction.
            var norm = Math.Sqrt(x[6] * x[6] + x[7] * x[7] + x[8] * x[8]);
            var target = 0.01 + 0.99 * random.NextDouble();
            for (var i = 6; i < 9; i++) x[i] *= target / norm;
            points[k] = x;
        }
        var atRest = new double[15];
        for (var i = 0; i < 15; i++) atRest[i] = i is >= 6 and < 9 ? 0.0 : random.NextDouble() * 2.0 - 1.0;
        points[100] = atRest;
        return points;
    }

    /// <inheritdoc />
    public static void Evaluate<T>(ReadOnlySpan<T> x, Span<T> y) where T : struct, ISpikeScalar<T>
    {
        var dt = T.Constant(Dt);
        var one = T.Constant(1.0);
        T tx = x[6], ty = x[7], tz = x[8];
        var t2 = tx * tx + ty * ty + tz * tz;

        T a, b;
        if (t2 < T.Constant(SeriesLimit))
        {
            // Maclaurin series in t2 = θ²: A = Σ (−1)ᵏ t2ᵏ/(2k+1)!, B = Σ (−1)ᵏ t2ᵏ/(2k+2)!.
            a = one + t2 * (T.Constant(-1.0 / 6.0) + t2 * (T.Constant(1.0 / 120.0) + t2 * (T.Constant(-1.0 / 5040.0) + t2 * T.Constant(1.0 / 362880.0))));
            b = T.Constant(0.5) + t2 * (T.Constant(-1.0 / 24.0) + t2 * (T.Constant(1.0 / 720.0) + t2 * (T.Constant(-1.0 / 40320.0) + t2 * T.Constant(1.0 / 3628800.0))));
        }
        else
        {
            var theta = T.Sqrt(t2);
            a = T.Sin(theta) / theta;
            b = (one - T.Cos(theta)) / t2;
        }

        // (R − I)·f = A·(δθ × f) + B·(δθ × (δθ × f)) with the constant body specific force f.
        T fx = T.Constant(0.3), fy = T.Constant(-0.2), fz = T.Constant(9.7);
        var c1x = ty * fz - tz * fy;
        var c1y = tz * fx - tx * fz;
        var c1z = tx * fy - ty * fx;
        var c2x = ty * c1z - tz * c1y;
        var c2y = tz * c1x - tx * c1z;
        var c2z = tx * c1y - ty * c1x;

        // R·(ω + b_g) = u + A·(δθ × u) + B·(δθ × (δθ × u)).
        var ux = T.Constant(0.01) + x[12];
        var uy = T.Constant(0.02) + x[13];
        var uz = T.Constant(-0.015) + x[14];
        var d1x = ty * uz - tz * uy;
        var d1y = tz * ux - tx * uz;
        var d1z = tx * uy - ty * ux;
        var d2x = ty * d1z - tz * d1y;
        var d2y = tz * d1x - tx * d1z;
        var d2z = tx * d1y - ty * d1x;

        y[0] = x[0] + dt * x[3];
        y[1] = x[1] + dt * x[4];
        y[2] = x[2] + dt * x[5];
        y[3] = x[3] + dt * ((a * c1x + b * c2x) - x[9]);
        y[4] = x[4] + dt * ((a * c1y + b * c2y) - x[10]);
        y[5] = x[5] + dt * ((a * c1z + b * c2z) - x[11]);
        y[6] = tx - dt * (ux + a * d1x + b * d2x);
        y[7] = ty - dt * (uy + a * d1y + b * d2y);
        y[8] = tz - dt * (uz + a * d1z + b * d2z);
        y[9] = x[9];
        y[10] = x[10];
        y[11] = x[11];
        y[12] = x[12];
        y[13] = x[13];
        y[14] = x[14];
    }
}

/// <summary>
/// W3: the 40-variable Rosenbrock chain f(x) = Σᵢ₌₁ⁿ⁻¹ [100·(xᵢ₊₁ − xᵢ²)² + (1 − xᵢ)²] (Rosenbrock 1960, generalized), whose gradient is longer than
/// the inline size of the 16-lane representation. The body takes its dimension from the input length, so the Hessian test can use n = 10.
/// </summary>
public readonly struct RosenbrockChain40 : IWorkload
{
    /// <inheritdoc />
    public static string Name => "W3 Rosenbrock-40";

    /// <inheritdoc />
    public static int Inputs => 40;

    /// <inheritdoc />
    public static int Outputs => 1;

    /// <inheritdoc />
    public static double[][] Points()
    {
        var random = new Random(1003);
        var points = new double[100][];
        for (var k = 0; k < points.Length; k++)
        {
            points[k] = new double[40];
            for (var i = 0; i < 40; i++) points[k][i] = random.NextDouble() * 3.0 - 1.5;
        }
        return points;
    }

    /// <inheritdoc />
    public static void Evaluate<T>(ReadOnlySpan<T> x, Span<T> y) where T : struct, ISpikeScalar<T>
    {
        var one = T.Constant(1.0);
        var hundred = T.Constant(100.0);
        var sum = T.Constant(0.0);
        for (var i = 0; i < x.Length - 1; i++)
        {
            var d = x[i + 1] - x[i] * x[i];
            var e = one - x[i];
            sum = sum + (hundred * (d * d) + e * e);
        }
        y[0] = sum;
    }
}
