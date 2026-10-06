namespace Mathesis.Numerics;

/// <summary>A scalar function of a vector, evaluated on <typeparamref name="TNumber"/> (a number type such as <c>Jet&lt;double&gt;</c>).</summary>
/// <typeparam name="TNumber">The number type.</typeparam>
/// <param name="x">The inputs.</param>
/// <returns>The function value.</returns>
public delegate TNumber JetScalarFunction<TNumber>(ReadOnlySpan<TNumber> x);

/// <summary>A vector function of a vector, evaluated on <typeparamref name="TNumber"/>.</summary>
/// <typeparam name="TNumber">The number type.</typeparam>
/// <param name="x">The inputs.</param>
/// <param name="y">Receives the outputs; its length is the number of outputs.</param>
public delegate void JetVectorFunction<TNumber>(ReadOnlySpan<TNumber> x, Span<TNumber> y);
