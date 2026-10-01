using System.Numerics;
using System.Collections.Immutable;

namespace Mathesis.Numerics.Interpolation;

/// <summary>A function defined by interpolating data points.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public interface IInterpolant<T>
{
    /// <summary>The interpolated value at <paramref name="x"/>.</summary>
    T Evaluate(T x);
}

/// <summary>
/// The interpolating polynomial in Newton's divided-difference form (catalog <c>num.interp.newton</c>):
/// p(x) = f[x₀] + f[x₀, x₁](x − x₀) + f[x₀, x₁, x₂](x − x₀)(x − x₁) + …
/// </summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public sealed class NewtonInterpolant<T> : IInterpolant<T>
    where T : IFloatingPointIeee754<T>
{
    private readonly ImmutableArray<T> _nodes;
    private readonly ImmutableArray<T> _coefficients;

    internal NewtonInterpolant(T[] x, T[] y)
    {
        Interpolate.ValidateNodes(x, y, minimumCount: 1);
        _nodes = [.. x];
        var c = (T[])y.Clone();
        for (var level = 1; level < c.Length; level++)
        {
            for (var i = c.Length - 1; i >= level; i--) c[i] = (c[i] - c[i - 1]) / (x[i] - x[i - level]);
        }
        _coefficients = [.. c];
    }

    /// <summary>The interpolation nodes.</summary>
    public ImmutableArray<T> Nodes => _nodes;

    /// <summary>The divided-difference coefficients f[x₀], f[x₀, x₁], ….</summary>
    public ImmutableArray<T> Coefficients => _coefficients;

    /// <inheritdoc />
    public T Evaluate(T x)
    {
        var result = _coefficients[^1];
        for (var i = _coefficients.Length - 2; i >= 0; i--) result = result * (x - _nodes[i]) + _coefficients[i];
        return result;
    }

    /// <summary>The derivative p′(x) of the interpolating polynomial.</summary>
    public T Derivative(T x)
    {
        var value = _coefficients[^1];
        var slope = T.Zero;
        for (var i = _coefficients.Length - 2; i >= 0; i--)
        {
            slope = slope * (x - _nodes[i]) + value;
            value = value * (x - _nodes[i]) + _coefficients[i];
        }
        return slope;
    }
}

/// <summary>
/// The interpolating polynomial in barycentric form (catalog <c>num.interp.barycentric</c>):
/// p(x) = Σ wⱼyⱼ/(x − xⱼ) / Σ wⱼ/(x − xⱼ), with wⱼ = 1/∏ₖ≠ⱼ(xⱼ − xₖ). Numerically stable and O(n) per evaluation.
/// </summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public sealed class BarycentricInterpolant<T> : IInterpolant<T>
    where T : IFloatingPointIeee754<T>
{
    private readonly ImmutableArray<T> _nodes;
    private readonly ImmutableArray<T> _values;
    private readonly ImmutableArray<T> _weights;

    internal BarycentricInterpolant(T[] x, T[] y, T[]? weights = null)
    {
        Interpolate.ValidateNodes(x, y, minimumCount: 1);
        _nodes = [.. x];
        _values = [.. y];
        if (weights is not null)
        {
            _weights = [.. weights];
            return;
        }
        var w = new T[x.Length];
        for (var j = 0; j < x.Length; j++)
        {
            var product = T.One;
            for (var k = 0; k < x.Length; k++)
            {
                if (k != j) product *= x[j] - x[k];
            }
            w[j] = T.One / product;
        }
        _weights = [.. w];
    }

    /// <summary>The interpolation nodes.</summary>
    public ImmutableArray<T> Nodes => _nodes;

    /// <summary>The barycentric weights.</summary>
    public ImmutableArray<T> Weights => _weights;

    /// <inheritdoc />
    public T Evaluate(T x)
    {
        var numerator = T.Zero;
        var denominator = T.Zero;
        for (var j = 0; j < _nodes.Length; j++)
        {
            var d = x - _nodes[j];
            if (d == T.Zero) return _values[j];
            var term = _weights[j] / d;
            numerator += term * _values[j];
            denominator += term;
        }
        return numerator / denominator;
    }
}

/// <summary>
/// A cubic spline (catalog <c>num.interp.cubic-spline</c>): piecewise cubic, with continuous value, first and second derivative
/// at the interior knots. Outside the knot range the end pieces are extrapolated.
/// </summary>
/// <typeparam name="T">The floating-point type.</typeparam>
public sealed class CubicSpline<T> : IInterpolant<T>
    where T : IFloatingPointIeee754<T>
{
    // Piece i is a[i] + b[i]·s + c[i]·s² + d[i]·s³ with s = x − x[i].
    private readonly T[] _x;
    private readonly T[] _a;
    private readonly T[] _b;
    private readonly T[] _c;
    private readonly T[] _d;

    internal CubicSpline(T[] x, T[] y, bool clamped, T startSlope, T endSlope)
    {
        Interpolate.ValidateNodes(x, y, minimumCount: 2);
        var n = x.Length - 1;
        _x = (T[])x.Clone();
        _a = (T[])y.Clone();
        _b = new T[n];
        _c = new T[n + 1];
        _d = new T[n];

        var h = new T[n];
        for (var i = 0; i < n; i++) h[i] = x[i + 1] - x[i];
        var two = Num.Two<T>();
        var three = Num.C<T>(3);

        // Tridiagonal system for the second-derivative coefficients c (Burden and Faires, Numerical Analysis).
        var alpha = new T[n + 1];
        for (var i = 1; i < n; i++) alpha[i] = three / h[i] * (y[i + 1] - y[i]) - three / h[i - 1] * (y[i] - y[i - 1]);
        var l = new T[n + 1];
        var mu = new T[n + 1];
        var z = new T[n + 1];
        if (clamped)
        {
            alpha[0] = three * (y[1] - y[0]) / h[0] - three * startSlope;
            l[0] = two * h[0];
            mu[0] = Num.Half<T>();
            z[0] = alpha[0] / l[0];
        }
        else
        {
            l[0] = T.One;
        }
        for (var i = 1; i < n; i++)
        {
            l[i] = two * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
            mu[i] = h[i] / l[i];
            z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
        }
        if (clamped)
        {
            alpha[n] = three * endSlope - three * (y[n] - y[n - 1]) / h[n - 1];
            l[n] = h[n - 1] * (two - mu[n - 1]);
            z[n] = (alpha[n] - h[n - 1] * z[n - 1]) / l[n];
            _c[n] = z[n];
        }
        else
        {
            l[n] = T.One;
            _c[n] = T.Zero;
        }
        for (var j = n - 1; j >= 0; j--)
        {
            _c[j] = z[j] - mu[j] * _c[j + 1];
            _b[j] = (y[j + 1] - y[j]) / h[j] - h[j] * (_c[j + 1] + two * _c[j]) / three;
            _d[j] = (_c[j + 1] - _c[j]) / (three * h[j]);
        }
    }

    /// <summary>The knots.</summary>
    public ImmutableArray<T> Knots => [.. _x];

    private int Piece(T x)
    {
        var low = 0;
        var high = _x.Length - 2;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (x >= _x[mid]) low = mid;
            else high = mid - 1;
        }
        return low;
    }

    /// <inheritdoc />
    public T Evaluate(T x)
    {
        var i = Piece(x);
        var s = x - _x[i];
        return _a[i] + s * (_b[i] + s * (_c[i] + s * _d[i]));
    }

    /// <summary>The first derivative S′(x).</summary>
    public T Derivative(T x)
    {
        var i = Piece(x);
        var s = x - _x[i];
        return _b[i] + s * (Num.Two<T>() * _c[i] + s * Num.C<T>(3) * _d[i]);
    }

    /// <summary>The second derivative S″(x), continuous across knots.</summary>
    public T SecondDerivative(T x)
    {
        var i = Piece(x);
        var s = x - _x[i];
        return Num.Two<T>() * _c[i] + Num.C<T>(6) * s * _d[i];
    }

    /// <summary>The definite integral of the spline from <paramref name="lower"/> to <paramref name="upper"/> (end pieces extrapolated).</summary>
    public T Integral(T lower, T upper) => Cumulative(upper) - Cumulative(lower);

    private T PieceIntegral(int i, T s) =>
        s * (_a[i] + s * (Num.Half<T>() * _b[i] + s * (_c[i] / Num.C<T>(3) + s * _d[i] / Num.C<T>(4))));

    // Integral from the first knot to x.
    private T Cumulative(T x)
    {
        var p = Piece(x);
        var total = T.Zero;
        for (var i = 0; i < p; i++) total += PieceIntegral(i, _x[i + 1] - _x[i]);
        return total + PieceIntegral(p, x - _x[p]);
    }
}

/// <summary>Factory methods for the interpolants.</summary>
public static class Interpolate
{
    /// <summary>The Newton-form interpolating polynomial through the points (<paramref name="x"/>[i], <paramref name="y"/>[i]).</summary>
    /// <exception cref="ArgumentException">The arrays differ in length, are empty, or the x values are not distinct and finite.</exception>
    public static NewtonInterpolant<T> Newton<T>(T[] x, T[] y)
        where T : IFloatingPointIeee754<T> => new(x, y);

    /// <summary>The barycentric interpolating polynomial through the points; weights cost O(n²) to build.</summary>
    /// <exception cref="ArgumentException">The arrays differ in length, are empty, or the x values are not distinct and finite.</exception>
    public static BarycentricInterpolant<T> Barycentric<T>(T[] x, T[] y)
        where T : IFloatingPointIeee754<T> => new(x, y);

    /// <summary>
    /// Interpolates <paramref name="f"/> at the <paramref name="degree"/> + 1 Chebyshev points of the second kind on [a, b]
    /// (catalog <c>num.interp.chebyshev-nodes</c>), using their closed-form barycentric weights (−1)ʲ, halved at the ends, so
    /// construction is O(n). High degrees are stable, unlike interpolation at equispaced nodes (<c>num.interp.runge</c>).
    /// </summary>
    public static BarycentricInterpolant<T> Chebyshev<T>(Func<T, T> f, T a, T b, int degree)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentOutOfRangeException.ThrowIfLessThan(degree, 1);
        var x = new T[degree + 1];
        var y = new T[degree + 1];
        var w = new T[degree + 1];
        var mid = Num.Half<T>() * (a + b);
        var half = Num.Half<T>() * (b - a);
        for (var j = 0; j <= degree; j++)
        {
            // Ascending order: x[0] = a.
            x[j] = mid - half * T.Cos(T.Pi * T.CreateChecked(j) / T.CreateChecked(degree));
            y[j] = f(x[j]);
            w[j] = (j % 2 == 0 ? T.One : -T.One) * (j == 0 || j == degree ? Num.Half<T>() : T.One);
        }
        x[0] = a;
        x[degree] = b;
        return new(x, y, w);
    }

    /// <summary>A natural cubic spline (S″ = 0 at both ends) through the points; x must be strictly increasing.</summary>
    /// <exception cref="ArgumentException">Fewer than two points, mismatched lengths, or x not strictly increasing.</exception>
    public static CubicSpline<T> NaturalSpline<T>(T[] x, T[] y)
        where T : IFloatingPointIeee754<T> => new(x, y, false, T.Zero, T.Zero);

    /// <summary>A clamped cubic spline with the given end slopes S′(x₀) and S′(xₙ); x must be strictly increasing.</summary>
    /// <exception cref="ArgumentException">Fewer than two points, mismatched lengths, or x not strictly increasing.</exception>
    public static CubicSpline<T> ClampedSpline<T>(T[] x, T[] y, T startSlope, T endSlope)
        where T : IFloatingPointIeee754<T> => new(x, y, true, startSlope, endSlope);

    internal static void ValidateNodes<T>(T[] x, T[] y, int minimumCount)
        where T : IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Length != y.Length) throw new ArgumentException("x and y must have the same length.", nameof(y));
        if (x.Length < minimumCount) throw new ArgumentException($"At least {minimumCount} point(s) are required.", nameof(x));
        for (var i = 0; i < x.Length; i++)
        {
            if (!T.IsFinite(x[i]) || !T.IsFinite(y[i])) throw new ArgumentException("Points must be finite.", nameof(x));
            for (var j = 0; j < i; j++)
            {
                if (x[i] == x[j]) throw new ArgumentException("The x values must be distinct.", nameof(x));
            }
            if (minimumCount >= 2 && i > 0 && x[i] <= x[i - 1]) throw new ArgumentException("The x values must be strictly increasing.", nameof(x));
        }
    }
}
