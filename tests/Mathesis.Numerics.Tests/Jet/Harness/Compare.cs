namespace Mathesis.Numerics.Tests;

/// <summary>Error measures between two Jacobians (or any two equally long arrays), used by the tests and the AOT console alike.</summary>
public static class Compare
{
    /// <summary>
    /// The largest element-wise relative difference max |a − b| / max(|a|, |b|), counting two equal elements (including two zeros) as 0.
    /// This is the strict measure: it flags an element that differs in its last bit even when the element is tiny.
    /// </summary>
    public static double ElementRelative(ReadOnlySpan<double> actual, ReadOnlySpan<double> reference)
    {
        if (actual.Length != reference.Length) throw new ArgumentException("Lengths differ.");
        var worst = 0.0;
        for (var i = 0; i < actual.Length; i++)
        {
            if (actual[i] == reference[i]) continue;
            var scale = Math.Max(Math.Abs(actual[i]), Math.Abs(reference[i]));
            var error = Math.Abs(actual[i] - reference[i]) / scale;
            if (!(error <= worst)) worst = error;   // also propagates NaN
        }
        return worst;
    }

    /// <summary>The matrix-wise relative difference max |a − b| / max |reference| (max norm), the measure for comparing with finite differences.</summary>
    public static double MatrixRelative(ReadOnlySpan<double> actual, ReadOnlySpan<double> reference)
    {
        if (actual.Length != reference.Length) throw new ArgumentException("Lengths differ.");
        double difference = 0.0, size = 0.0;
        for (var i = 0; i < actual.Length; i++)
        {
            var d = Math.Abs(actual[i] - reference[i]);
            if (!(d <= difference)) difference = d;
            size = Math.Max(size, Math.Abs(reference[i]));
        }
        return size == 0.0 ? difference : difference / size;
    }

    /// <summary>The number of elements that are bit-for-bit equal (±0 count as equal).</summary>
    public static int EqualElements(ReadOnlySpan<double> actual, ReadOnlySpan<double> reference)
    {
        var count = 0;
        for (var i = 0; i < actual.Length; i++) if (actual[i] == reference[i]) count++;
        return count;
    }
}
