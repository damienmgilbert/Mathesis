using System.Numerics;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// The minimal scalar surface the three workloads need, so one generic body runs on <see cref="PlainDouble"/>, on Mathesis's
/// <c>Dual&lt;double&gt;</c> and on every jet representation. Relational operators compare values only for the jets (ADR-19 in
/// docs/design/04-type-system.md); <see cref="MathesisDualScalar"/> keeps <c>Dual&lt;T&gt;</c>'s structural <c>==</c> on purpose, so the difference is measurable.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface ISpikeScalar<TSelf> :
    IAdditionOperators<TSelf, TSelf, TSelf>,
    ISubtractionOperators<TSelf, TSelf, TSelf>,
    IMultiplyOperators<TSelf, TSelf, TSelf>,
    IDivisionOperators<TSelf, TSelf, TSelf>,
    IUnaryNegationOperators<TSelf, TSelf>,
    IComparisonOperators<TSelf, TSelf, bool>
    where TSelf : struct, ISpikeScalar<TSelf>
{
    /// <summary>The primal value.</summary>
    double Value { get; }

    /// <summary>A constant: no dependence on any input.</summary>
    static abstract TSelf Constant(double value);

    /// <summary>The sine.</summary>
    static abstract TSelf Sin(TSelf x);

    /// <summary>The cosine.</summary>
    static abstract TSelf Cos(TSelf x);

    /// <summary>The square root.</summary>
    static abstract TSelf Sqrt(TSelf x);

    /// <summary>e<sup>x</sup>.</summary>
    static abstract TSelf Exp(TSelf x);

    /// <summary>The natural logarithm.</summary>
    static abstract TSelf Log(TSelf x);

    /// <summary>The absolute value.</summary>
    static abstract TSelf Abs(TSelf x);

    /// <summary>The angle of the point (<paramref name="x"/>, <paramref name="y"/>).</summary>
    static abstract TSelf Atan2(TSelf y, TSelf x);

    /// <summary><paramref name="x"/> raised to the constant power <paramref name="exponent"/>.</summary>
    static abstract TSelf Pow(TSelf x, double exponent);
}
