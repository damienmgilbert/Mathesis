using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Polynomials;

namespace Mathesis.Testing;

/// <summary>
/// A seeded generator of random test inputs. Every test builds one from a fixed seed and reports <see cref="Seed"/>
/// and the failing case, so a failure reproduces exactly.
/// </summary>
public sealed class Gen
{
    /// <summary>Creates a generator for <paramref name="seed"/>.</summary>
    public Gen(int seed)
    {
        Seed = seed;
        Random = new Random(seed);
    }

    /// <summary>The seed this generator was created with.</summary>
    public int Seed { get; }

    /// <summary>The underlying random source.</summary>
    public Random Random { get; }

    /// <summary>A uniformly random integer in [<paramref name="low"/>, <paramref name="high"/>].</summary>
    public int WholeNumber(int low, int high) => Random.Next(low, high + 1);

    /// <summary>Prefixes <paramref name="failingCase"/> with the seed, for assertion messages.</summary>
    public string Describe(string failingCase) => $"seed {Seed}: {failingCase}";

    /// <summary>A uniformly random integer with up to <paramref name="maxBits"/> bits and a random sign.</summary>
    public BigInteger WholeNumber(int maxBits = 64)
    {
        var bits = Random.Next(0, maxBits + 1);
        if (bits == 0) return BigInteger.Zero;
        var bytes = new byte[(bits + 7) / 8 + 1];
        Random.NextBytes(bytes);
        bytes[^1] = 0;
        var value = new BigInteger(bytes) & ((BigInteger.One << bits) - 1);
        return Random.Next(2) == 0 ? value : -value;
    }

    /// <summary>
    /// A random rational. About 10% are zero and 10% integers, and the rest are fractions whose parts have up to
    /// <paramref name="maxBits"/> bits, so edge cases appear in every run.
    /// </summary>
    public BigRational Rational(int maxBits = 48)
    {
        var kind = Random.Next(10);
        if (kind == 0) return BigRational.Zero;
        if (kind == 1) return new BigRational(WholeNumber(maxBits));
        var denominator = BigInteger.Abs(WholeNumber(maxBits)) + 1;
        return BigRational.Create(WholeNumber(maxBits), denominator);
    }

    /// <summary>A random non-zero rational.</summary>
    public BigRational NonZeroRational(int maxBits = 48)
    {
        while (true)
        {
            var r = Rational(maxBits);
            if (!r.IsZero()) return r;
        }
    }

    /// <summary>A random Gaussian rational.</summary>
    public Complex<BigRational> GaussianRational(int maxBits = 32) => new(Rational(maxBits), Rational(maxBits));

    /// <summary>A random non-zero Gaussian rational.</summary>
    public Complex<BigRational> NonZeroGaussianRational(int maxBits = 32)
    {
        while (true)
        {
            var z = GaussianRational(maxBits);
            if (!Complex<BigRational>.IsZero(z)) return z;
        }
    }

    /// <summary>A random non-zero polynomial over the rationals with degree at most <paramref name="maxDegree"/>.</summary>
    public Polynomial<BigRational> RationalPolynomial(int maxDegree = 6, int maxBits = 12)
    {
        while (true)
        {
            var p = new Polynomial<BigRational>(Enumerable.Range(0, Random.Next(1, maxDegree + 2)).Select(_ => Rational(maxBits)));
            if (!p.IsZero) return p;
        }
    }

    /// <summary>A uniformly random double in [<paramref name="low"/>, <paramref name="high"/>).</summary>
    public double Uniform(double low, double high) => low + (high - low) * Random.NextDouble();

    /// <summary>A random finite double with any exponent, including subnormals and both signs.</summary>
    public double AnyFiniteDouble()
    {
        while (true)
        {
            var d = BitConverter.Int64BitsToDouble(Random.NextInt64());
            if (double.IsFinite(d)) return d;
        }
    }
}

/// <summary>Small helpers shared by test projects.</summary>
public static class TestExtensions
{
    /// <summary>Whether the rational is zero.</summary>
    public static bool IsZero(this BigRational value) => value == BigRational.Zero;
}
