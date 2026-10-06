namespace Mathesis.Numerics.Tests;

/// <summary>
/// Computes a workload's values and Jacobian at one point with one differentiation strategy. Engines own their scratch arrays, so a call
/// after construction allocates only what the representation itself allocates (the heap jet's gradients).
/// </summary>
public interface IJacobianEngine
{
    /// <summary>A short name for reports.</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates at <paramref name="x"/>: <paramref name="values"/> receives the outputs and <paramref name="jacobian"/> the
    /// outputs × inputs Jacobian in row-major order.
    /// </summary>
    void Jacobian(ReadOnlySpan<double> x, Span<double> values, Span<double> jacobian);
}

/// <summary>
/// Central finite differences with Richardson extrapolation on <see cref="PlainDouble"/>: a four-level Romberg table (Ridders 1982) with
/// steps 0.05, 0.025, 0.0125 and 0.00625, which removes the h², h⁴ and h⁶ error terms of the central difference (the remaining error is of order h⁸).
/// </summary>
/// <typeparam name="TWorkload">The workload.</typeparam>
public sealed class FiniteDifferenceEngine<TWorkload> : IJacobianEngine where TWorkload : IWorkload
{
    private const int Levels = 4;
    private const double FirstStep = 0.05;

    private readonly PlainDouble[] _plus = new PlainDouble[TWorkload.Inputs];
    private readonly PlainDouble[] _minus = new PlainDouble[TWorkload.Inputs];
    private readonly PlainDouble[] _yPlus = new PlainDouble[TWorkload.Outputs];
    private readonly PlainDouble[] _yMinus = new PlainDouble[TWorkload.Outputs];
    private readonly double[] _table = new double[Levels * TWorkload.Outputs];

    /// <inheritdoc />
    public string Name => "Richardson FD";

    /// <inheritdoc />
    public void Jacobian(ReadOnlySpan<double> x, Span<double> values, Span<double> jacobian)
    {
        int n = TWorkload.Inputs, m = TWorkload.Outputs;
        for (var i = 0; i < n; i++) _plus[i] = new PlainDouble(x[i]);
        TWorkload.Evaluate<PlainDouble>(_plus, _yPlus);
        for (var o = 0; o < m; o++) values[o] = _yPlus[o].Value;

        for (var j = 0; j < n; j++)
        {
            var step = FirstStep;
            for (var level = 0; level < Levels; level++, step *= 0.5)
            {
                for (var i = 0; i < n; i++)
                {
                    _plus[i] = new PlainDouble(i == j ? x[i] + step : x[i]);
                    _minus[i] = new PlainDouble(i == j ? x[i] - step : x[i]);
                }
                TWorkload.Evaluate<PlainDouble>(_plus, _yPlus);
                TWorkload.Evaluate<PlainDouble>(_minus, _yMinus);
                for (var o = 0; o < m; o++)
                    _table[level * m + o] = (_yPlus[o].Value - _yMinus[o].Value) / (2.0 * step);
            }

            // Romberg: column k removes the h^(2k) error term of the previous column.
            var factor = 1.0;
            for (var k = 1; k < Levels; k++)
            {
                factor *= 4.0;
                for (var level = Levels - 1; level >= k; level--)
                    for (var o = 0; o < m; o++)
                        _table[level * m + o] = (factor * _table[level * m + o] - _table[(level - 1) * m + o]) / (factor - 1.0);
            }
            for (var o = 0; o < m; o++) jacobian[o * n + j] = _table[(Levels - 1) * m + o];
        }
    }
}

/// <summary>Mathesis <c>Dual&lt;double&gt;</c>, one pass over the workload per input direction.</summary>
/// <typeparam name="TWorkload">The workload.</typeparam>
public sealed class DualPassesEngine<TWorkload> : IJacobianEngine where TWorkload : IWorkload
{
    private readonly MathesisDualScalar[] _x = new MathesisDualScalar[TWorkload.Inputs];
    private readonly MathesisDualScalar[] _y = new MathesisDualScalar[TWorkload.Outputs];

    /// <inheritdoc />
    public string Name => "Dual<double> N passes";

    /// <inheritdoc />
    public void Jacobian(ReadOnlySpan<double> x, Span<double> values, Span<double> jacobian)
    {
        int n = TWorkload.Inputs, m = TWorkload.Outputs;
        for (var j = 0; j < n; j++)
        {
            for (var i = 0; i < n; i++) _x[i] = new MathesisDualScalar(x[i], i == j ? 1.0 : 0.0);
            TWorkload.Evaluate<MathesisDualScalar>(_x, _y);
            for (var o = 0; o < m; o++)
            {
                if (j == 0) values[o] = _y[o].Value;
                jacobian[o * n + j] = _y[o].Derivative;
            }
        }
    }
}
