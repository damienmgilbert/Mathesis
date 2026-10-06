using Mathesis.Numbers;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// <c>Jet&lt;double&gt;</c> behind the workload interface, so W1 to W3 (written once over <see cref="ISpikeScalar{TSelf}"/>) run on the
/// real type for the Jacobian-agreement and allocation checks. A thin wrapper: every member forwards to the jet.
/// </summary>
internal readonly struct JetScalar : ISpikeScalar<JetScalar>, IEquatable<JetScalar>
{
    private readonly JetD _jet;

    public JetScalar(JetD jet) => _jet = jet;

    public JetD Jet => _jet;

    public double Value => _jet.Value;

    public static JetScalar Constant(double value) => new(JetD.Constant(value));

    public static JetScalar Sin(JetScalar x) => new(JetD.Sin(x._jet));

    public static JetScalar Cos(JetScalar x) => new(JetD.Cos(x._jet));

    public static JetScalar Sqrt(JetScalar x) => new(JetD.Sqrt(x._jet));

    public static JetScalar Exp(JetScalar x) => new(JetD.Exp(x._jet));

    public static JetScalar Log(JetScalar x) => new(JetD.Log(x._jet));

    public static JetScalar Abs(JetScalar x) => new(JetD.Abs(x._jet));

    public static JetScalar Atan2(JetScalar y, JetScalar x) => new(JetD.Atan2(y._jet, x._jet));

    public static JetScalar Pow(JetScalar x, double exponent) => new(JetD.Pow(x._jet, JetD.Constant(exponent)));

    public static JetScalar operator +(JetScalar left, JetScalar right) => new(left._jet + right._jet);

    public static JetScalar operator -(JetScalar left, JetScalar right) => new(left._jet - right._jet);

    public static JetScalar operator *(JetScalar left, JetScalar right) => new(left._jet * right._jet);

    public static JetScalar operator /(JetScalar left, JetScalar right) => new(left._jet / right._jet);

    public static JetScalar operator -(JetScalar value) => new(-value._jet);

    public static bool operator <(JetScalar left, JetScalar right) => left._jet < right._jet;

    public static bool operator >(JetScalar left, JetScalar right) => left._jet > right._jet;

    public static bool operator <=(JetScalar left, JetScalar right) => left._jet <= right._jet;

    public static bool operator >=(JetScalar left, JetScalar right) => left._jet >= right._jet;

    public static bool operator ==(JetScalar left, JetScalar right) => left._jet == right._jet;

    public static bool operator !=(JetScalar left, JetScalar right) => left._jet != right._jet;

    public bool Equals(JetScalar other) => _jet.Equals(other._jet);

    public override bool Equals(object? obj) => obj is JetScalar other && Equals(other);

    public override int GetHashCode() => _jet.GetHashCode();
}

/// <summary>
/// The Jacobian of a workload with <c>Jet&lt;double&gt;</c> inputs in chunks of <see cref="Jet{T}.Lanes"/> columns (a pass per chunk, the
/// other inputs held constant). Scratch arrays are owned by the engine, so a call allocates nothing.
/// </summary>
internal sealed class JetEngine<TWorkload> : IJacobianEngine where TWorkload : IWorkload
{
    private readonly JetScalar[] _x = new JetScalar[TWorkload.Inputs];
    private readonly JetScalar[] _y = new JetScalar[TWorkload.Outputs];

    public string Name => "Jet<double>";

    public void Jacobian(ReadOnlySpan<double> x, Span<double> values, Span<double> jacobian)
    {
        int n = TWorkload.Inputs, m = TWorkload.Outputs;
        for (var start = 0; start < n; start += JetD.Lanes)
        {
            var count = Math.Min(JetD.Lanes, n - start);
            for (var i = 0; i < n; i++)
                _x[i] = new JetScalar(i >= start && i < start + count ? JetD.Variable(x[i], i - start, count) : JetD.Constant(x[i]));
            TWorkload.Evaluate<JetScalar>(_x, _y);
            for (var o = 0; o < m; o++)
            {
                if (start == 0) values[o] = _y[o].Value;
                var target = jacobian.Slice(o * n + start, count);
                var jet = _y[o].Jet;
                if (jet.IsConstant) target.Clear();
                else jet.Gradient.CopyTo(target);
            }
        }
    }
}
