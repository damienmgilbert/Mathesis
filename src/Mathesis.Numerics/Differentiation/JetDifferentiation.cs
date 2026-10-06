using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Numerics.Differentiation;

/// <summary>
/// Gradients, Jacobians, Hessians and derivatives of functions written once against <c>IFloatingPointIeee754&lt;T&gt;</c>, by forward-mode
/// automatic differentiation with <see cref="Jet{T}"/>. A jet carries up to <see cref="Jet{T}.Lanes"/> variables, so more inputs are handled in
/// passes over chunks of <see cref="Jet{T}.Lanes"/> columns, each pass holding the other inputs constant; second derivatives nest a jet in a
/// jet. The functions allocate their scratch arrays on every call; code that cannot allocate builds the jets itself (the allocation
/// tests do).
/// </summary>
public static class JetDifferentiation
{
    /// <summary>
    /// The gradient of <paramref name="f"/> at <paramref name="x"/>: writes <c>∂f/∂xᵢ</c> to <paramref name="gradient"/> and returns <c>f(x)</c>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="f">The function, written against <c>Jet&lt;T&gt;</c>.</param>
    /// <param name="x">The point.</param>
    /// <param name="gradient">Receives the gradient; the same length as <paramref name="x"/>.</param>
    /// <returns>The function value.</returns>
    /// <exception cref="ArgumentException">The lengths differ.</exception>
    public static T Gradient<T>(JetScalarFunction<Jet<T>> f, ReadOnlySpan<T> x, Span<T> gradient) where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var value = new T[1];
        Jacobian<T>((ReadOnlySpan<Jet<T>> p, Span<Jet<T>> y) => y[0] = f(p), 1, x, value, gradient);
        return value[0];
    }

    /// <summary>
    /// The Jacobian of <paramref name="f"/> at <paramref name="x"/>: writes the outputs to <paramref name="values"/> and <c>∂yₒ/∂xᵢ</c> to
    /// <paramref name="jacobian"/> in row-major order (outputs × inputs). An output that does not depend on any input has a zero row.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="f">The function, written against <c>Jet&lt;T&gt;</c>.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="x">The point.</param>
    /// <param name="values">Receives the outputs; <paramref name="outputs"/> elements.</param>
    /// <param name="jacobian">Receives the Jacobian; <c>outputs × x.Length</c> elements.</param>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void Jacobian<T>(JetVectorFunction<Jet<T>> f, int outputs, ReadOnlySpan<T> x, Span<T> values, Span<T> jacobian)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentOutOfRangeException.ThrowIfNegative(outputs);
        var n = x.Length;
        if (values.Length != outputs) throw new ArgumentException("values must have one element per output.", nameof(values));
        if (jacobian.Length != outputs * n) throw new ArgumentException("jacobian must have outputs × inputs elements.", nameof(jacobian));

        var inputs = new Jet<T>[n];
        var results = new Jet<T>[outputs];
        for (var start = 0; start < n || start == 0; start += Jet<T>.Lanes)
        {
            var count = Math.Min(Jet<T>.Lanes, n - start);
            for (var i = 0; i < n; i++)
                inputs[i] = i >= start && i < start + count ? Jet<T>.Variable(x[i], i - start, count) : Jet<T>.Constant(x[i]);
            f(inputs, results);
            for (var o = 0; o < outputs; o++)
            {
                if (start == 0) values[o] = results[o].Value;
                var row = jacobian.Slice(o * n + start, Math.Max(count, 0));
                if (results[o].IsConstant) row.Clear();
                else results[o].Gradient.CopyTo(row);
            }
        }
    }

    /// <summary>
    /// The Hessian of <paramref name="f"/> at <paramref name="x"/> by nested jets: writes <c>∂²f/∂xᵢ∂xⱼ</c> to <paramref name="hessian"/> in
    /// row-major order and returns <c>f(x)</c>. The mixed partials come from different passes, so symmetry holds to rounding error, not exactly.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="f">The function, written against <c>Jet&lt;Jet&lt;T&gt;&gt;</c>.</param>
    /// <param name="x">The point.</param>
    /// <param name="hessian">Receives the Hessian; <c>x.Length²</c> elements.</param>
    /// <returns>The function value.</returns>
    /// <exception cref="ArgumentException">The Hessian has the wrong length.</exception>
    public static T Hessian<T>(JetScalarFunction<Jet<Jet<T>>> f, ReadOnlySpan<T> x, Span<T> hessian) where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var n = x.Length;
        if (hessian.Length != n * n) throw new ArgumentException("hessian must have n × n elements.", nameof(hessian));

        var value = T.Zero;
        var inputs = new Jet<Jet<T>>[n];
        for (var a = 0; a < n || a == 0; a += Jet<T>.Lanes)
        {
            var countA = Math.Min(Jet<T>.Lanes, n - a);
            for (var b = 0; b < n || b == 0; b += Jet<T>.Lanes)
            {
                var countB = Math.Min(Jet<T>.Lanes, n - b);
                for (var i = 0; i < n; i++)
                {
                    var inner = i >= b && i < b + countB ? Jet<T>.Variable(x[i], i - b, countB) : Jet<T>.Constant(x[i]);
                    inputs[i] = i >= a && i < a + countA ? Jet<Jet<T>>.Variable(inner, i - a, countA) : Jet<Jet<T>>.Constant(inner);
                }
                var r = f(inputs);
                if (a == 0 && b == 0) value = r.Value.Value;
                for (var j = 0; j < countA; j++)
                    for (var k = 0; k < countB; k++)
                        hessian[(a + j) * n + b + k] = r.IsConstant || r.Gradient[j].IsConstant ? T.Zero : r.Gradient[j].Gradient[k];
            }
        }
        return value;
    }

    /// <summary>The derivative of a function of one variable at <paramref name="x"/>; zero when <paramref name="f"/> returns a constant.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="f">The function, written against <c>Jet&lt;T&gt;</c>.</param>
    /// <param name="x">The point.</param>
    /// <returns><c>f′(x)</c>.</returns>
    public static T Derivative<T>(Func<Jet<T>, Jet<T>> f, T x) where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        var r = f(Jet<T>.Variable(x, 0, 1));
        return r.IsConstant ? T.Zero : r.Gradient[0];
    }
}
