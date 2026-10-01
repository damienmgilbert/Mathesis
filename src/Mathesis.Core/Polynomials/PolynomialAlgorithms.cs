using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Polynomials;

/// <summary>One factor of a square-free decomposition together with its multiplicity.</summary>
/// <typeparam name="T">The coefficient type.</typeparam>
/// <param name="Factor">A monic square-free polynomial.</param>
/// <param name="Multiplicity">The power to which the factor divides the original polynomial.</param>
public sealed record SquareFreeFactor<T>(Polynomial<T> Factor, int Multiplicity)
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
        IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>;

/// <summary>
/// The square-free decomposition p = content · ∏ fᵢ<sup>mᵢ</sup> with each fᵢ monic, square-free and pairwise coprime.
/// </summary>
/// <typeparam name="T">The coefficient type.</typeparam>
/// <param name="Content">The leading coefficient of the original polynomial.</param>
/// <param name="Factors">The factors in order of increasing multiplicity.</param>
public sealed record SquareFreeDecomposition<T>(T Content, ImmutableArray<SquareFreeFactor<T>> Factors)
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
        IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
{
    /// <summary>Multiplies the decomposition back out: content · ∏ fᵢ<sup>mᵢ</sup>.</summary>
    public Polynomial<T> Expand()
    {
        var result = new Polynomial<T>([Content]);
        foreach (var (factor, multiplicity) in Factors) result *= factor.Pow(multiplicity);
        return result;
    }
}

/// <summary>A rational root and its multiplicity.</summary>
/// <param name="Root">The root.</param>
/// <param name="Multiplicity">The multiplicity (catalog <c>alg.poly.multiplicity</c>).</param>
public sealed record RationalRoot(BigRational Root, int Multiplicity);

/// <summary>The rational roots of an integer polynomial.</summary>
/// <param name="Roots">The distinct rational roots with multiplicities, in increasing order.</param>
/// <param name="Cofactor">What remains after dividing out every rational root: a polynomial with no rational roots.</param>
/// <param name="Complete">
/// <c>false</c> when the search could not factor |a₀| or |aₙ| completely within the trial-division limit, so a rational
/// root may have been missed; the roots returned are still genuine roots.
/// </param>
public sealed record RationalRootsResult(ImmutableArray<RationalRoot> Roots, Polynomial<BigRational> Cofactor, bool Complete);

/// <summary>The complex roots of a polynomial found numerically.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
/// <param name="Roots">All roots (with multiplicity) sorted by real part, then imaginary part.</param>
/// <param name="Iterations">Aberth–Ehrlich sweeps performed.</param>
/// <param name="Converged"><c>true</c> if every root met the correction tolerance.</param>
/// <param name="MaxRelativeCorrection">The largest relative correction in the last sweep.</param>
public sealed record PolynomialRoots<T>(ImmutableArray<Complex<T>> Roots, int Iterations, bool Converged, T MaxRelativeCorrection)
    where T : INumber<T>;

/// <summary>Algorithms on <see cref="Polynomial{T}"/>: gcd, square-free decomposition and root finding.</summary>
public static class PolynomialAlgorithms
{
    /// <summary>
    /// The monic greatest common divisor by the Euclidean algorithm (catalog <c>alg.poly.gcd</c>): gcd(p, 0) = monic(p) and
    /// gcd(0, 0) = 0.
    /// </summary>
    public static Polynomial<T> Gcd<T>(Polynomial<T> a, Polynomial<T> b)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        while (!b.IsZero) (a, b) = (b, a % b);
        return a.Monic();
    }

    /// <summary>
    /// Bézout's identity: returns the monic gcd g and polynomials s, t with <c>s·a + t·b = g</c> (extended Euclidean algorithm).
    /// </summary>
    public static (Polynomial<T> Gcd, Polynomial<T> S, Polynomial<T> T) ExtendedGcd<T>(Polynomial<T> a, Polynomial<T> b)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var (r0, r1) = (a, b);
        var (s0, s1) = (Polynomial<T>.One, Polynomial<T>.Zero);
        var (t0, t1) = (Polynomial<T>.Zero, Polynomial<T>.One);
        while (!r1.IsZero)
        {
            var (q, r) = r0.DivRem(r1);
            (r0, r1) = (r1, r);
            (s0, s1) = (s1, s0 - q * s1);
            (t0, t1) = (t1, t0 - q * t1);
        }
        if (r0.IsZero) return (r0, s0, t0);
        var lead = r0.LeadingCoefficient;
        return (r0 / lead, s0 / lead, t0 / lead);
    }

    /// <summary>
    /// Yun's square-free decomposition (Yun 1976): writes p as content · ∏ fᵢ<sup>i</sup> with monic square-free pairwise coprime
    /// factors, using only gcds, exact divisions and derivatives. Requires characteristic 0 and exact coefficients.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="p"/> is zero.</exception>
    public static SquareFreeDecomposition<T> SquareFree<T>(Polynomial<T> p)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.IsZero) throw new ArgumentException("The zero polynomial has no square-free decomposition.", nameof(p));
        var content = p.LeadingCoefficient;
        var monic = p.Monic();
        var factors = ImmutableArray.CreateBuilder<SquareFreeFactor<T>>();
        if (monic.Degree == 0) return new(content, factors.ToImmutable());

        var derivative = monic.Derivative();
        var c = Gcd(monic, derivative);
        var w = monic / c;
        var y = derivative / c;
        var z = y - w.Derivative();
        for (var i = 1; w.Degree > 0; i++)
        {
            var g = Gcd(w, z);
            if (g.Degree > 0) factors.Add(new(g, i));
            w /= g;
            y = z / g;
            z = y - w.Derivative();
        }
        return new(content, factors.ToImmutable());
    }

    /// <summary>
    /// All complex roots of <paramref name="p"/> (with multiplicity) by the Aberth–Ehrlich simultaneous iteration (Aberth 1973;
    /// Ehrlich 1967; catalog <c>num.root.aberth</c>): cubic convergence at simple roots, linear at multiple ones. Zero roots are
    /// split off exactly first. The coefficients may be any floating-point type; convert exact polynomials with
    /// <see cref="Polynomial{T}.Map{TOut}"/>.
    /// </summary>
    /// <param name="p">A non-zero polynomial.</param>
    /// <param name="maxIterations">Maximum number of sweeps over all roots.</param>
    /// <param name="relativeTolerance">Stop when every correction is below this fraction of |root|; defaults to 8ε.</param>
    /// <exception cref="ArgumentException"><paramref name="p"/> is zero.</exception>
    public static PolynomialRoots<T> AberthRoots<T>(Polynomial<T> p, int maxIterations = 500, T? relativeTolerance = null)
        where T : struct, IFloatingPointIeee754<T>
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.IsZero) throw new ArgumentException("The zero polynomial has infinitely many roots.", nameof(p));
        var eps = T.BitIncrement(T.One) - T.One;
        var tolerance = relativeTolerance ?? T.CreateChecked(8) * eps;

        var roots = new List<Complex<T>>();
        var low = 0;
        while (p[low] == T.Zero) low++;
        for (var i = 0; i < low; i++) roots.Add(Complex<T>.Zero);
        var q = new Polynomial<T>(p.Coefficients.Skip(low));
        var n = q.Degree;
        if (n == 0) return new([.. roots], 0, true, T.Zero);
        if (n == 1)
        {
            roots.Add(new Complex<T>(-q[0] / q[1]));
            return new([.. roots], 0, true, T.Zero);
        }

        var a = q.Monic().Coefficients.ToArray();
        var two = T.One + T.One;

        // Start on a circle around the centroid of the roots, with radius from the coefficient magnitudes (Fujiwara-style).
        var center = -a[n - 1] / T.CreateChecked(n);
        var radius = T.Zero;
        for (var k = 1; k <= n; k++) radius = T.Max(radius, T.Pow(T.Abs(a[n - k]), T.One / T.CreateChecked(k)));
        if (radius == T.Zero) radius = T.One;
        var z = new Complex<T>[n];
        for (var k = 0; k < n; k++)
        {
            var angle = two * T.Pi * T.CreateChecked(k) / T.CreateChecked(n) + T.CreateChecked(0.4);
            z[k] = new Complex<T>(center + radius * T.Cos(angle), radius * T.Sin(angle));
        }

        var iterations = 0;
        var maxCorrection = T.PositiveInfinity;
        var converged = false;
        var absolute = a.Select(T.Abs).ToArray();
        var noise = T.CreateChecked(2 * n + 1) * eps;
        for (iterations = 1; iterations <= maxIterations; iterations++)
        {
            maxCorrection = T.Zero;
            var pending = false;
            for (var k = 0; k < n; k++)
            {
                // Horner for p(z), p'(z) and the rounding-noise level of p(z): eps * (2n + 1) * sum |a_i||z|^i.
                var value = new Complex<T>(a[n]);
                var slope = Complex<T>.Zero;
                var magnitude = z[k].Magnitude;
                var level = absolute[n];
                for (var i = n - 1; i >= 0; i--)
                {
                    slope = slope * z[k] + value;
                    value = value * z[k] + new Complex<T>(a[i]);
                    level = level * magnitude + absolute[i];
                }

                // A residual at the level of rounding noise cannot be improved; treat the root as settled.
                if (value.Magnitude <= noise * level) continue;
                var ratio = value / slope;
                var sum = Complex<T>.Zero;
                for (var j = 0; j < n; j++)
                {
                    if (j != k) sum += Complex<T>.One / (z[k] - z[j]);
                }
                var correction = ratio / (Complex<T>.One - ratio * sum);
                if (!T.IsFinite(correction.Real) || !T.IsFinite(correction.Imaginary)) continue;
                z[k] -= correction;
                var relative = correction.Magnitude / T.Max(z[k].Magnitude, T.Epsilon);
                maxCorrection = T.Max(maxCorrection, relative);
                if (relative > tolerance) pending = true;
            }
            if (!pending)
            {
                converged = true;
                break;
            }
        }

        roots.AddRange(z);
        roots.Sort((x, y) => x.Real != y.Real ? x.Real.CompareTo(y.Real) : x.Imaginary.CompareTo(y.Imaginary));
        return new([.. roots], Math.Min(iterations, maxIterations), converged, maxCorrection);
    }

    /// <summary>
    /// The rational roots of a polynomial with rational coefficients, by the Rational Root Theorem (catalog
    /// <c>alg.poly.rational-root</c>): after clearing denominators, every root p/q in lowest terms has p | a₀ and q | aₙ. Candidates
    /// are tested exactly and each root is divided out to find its multiplicity. Divisors come from trial division up to
    /// <paramref name="trialLimit"/>; if a larger cofactor remains, the search is reported incomplete rather than guessing.
    /// </summary>
    /// <param name="p">A non-zero polynomial.</param>
    /// <param name="trialLimit">Largest prime tried when factoring a₀ and aₙ.</param>
    /// <exception cref="ArgumentException"><paramref name="p"/> is zero.</exception>
    public static RationalRootsResult RationalRoots(Polynomial<BigRational> p, int trialLimit = 1 << 20)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.IsZero) throw new ArgumentException("The zero polynomial has infinitely many roots.", nameof(p));

        var found = new SortedDictionary<BigRational, int>();
        var work = p;
        var complete = true;

        // Roots at zero.
        var zeroMultiplicity = 0;
        while (work.Degree > 0 && work[0] == BigRational.Zero)
        {
            work = new Polynomial<BigRational>(work.Coefficients.Skip(1));
            zeroMultiplicity++;
        }
        if (zeroMultiplicity > 0) found[BigRational.Zero] = zeroMultiplicity;

        if (work.Degree > 0)
        {
            // Integer polynomial with the same roots.
            var lcm = BigInteger.One;
            foreach (var c in work.Coefficients) lcm = lcm / BigInteger.GreatestCommonDivisor(lcm, c.Denominator) * c.Denominator;
            var integers = work.Coefficients.Select(c => (c * new BigRational(lcm)).Numerator).ToArray();
            var constant = BigInteger.Abs(integers[0]);
            var leading = BigInteger.Abs(integers[^1]);

            var numerators = Divisors(constant, trialLimit, ref complete);
            var denominators = Divisors(leading, trialLimit, ref complete);
            var candidates = new SortedSet<BigRational>();
            foreach (var num in numerators)
            {
                foreach (var den in denominators)
                {
                    if (!BigInteger.GreatestCommonDivisor(num, den).IsOne) continue;
                    candidates.Add(BigRational.Create(num, den));
                    candidates.Add(BigRational.Create(-num, den));
                }
            }
            foreach (var candidate in candidates)
            {
                if (work.Degree == 0 || work.Evaluate(candidate) != BigRational.Zero) continue;
                var linear = new Polynomial<BigRational>([-candidate, BigRational.One]);
                var multiplicity = 0;
                while (work.Degree > 0)
                {
                    var (quotient, remainder) = work.DivRem(linear);
                    if (!remainder.IsZero) break;
                    work = quotient;
                    multiplicity++;
                }
                found[candidate] = multiplicity;
            }
        }

        return new([.. found.Select(kv => new RationalRoot(kv.Key, kv.Value))], work.Degree == 0 ? Polynomial<BigRational>.One : work, complete);
    }

    // Divisors of n > 0 from trial division by primes up to the limit; a remaining cofactor > 1 is treated as prime, which
    // makes the result incomplete only when it is too large to be certainly prime (primality is not tested).
    private static List<BigInteger> Divisors(BigInteger n, int trialLimit, ref bool complete)
    {
        var divisors = new List<BigInteger> { BigInteger.One };
        var remaining = n;
        for (BigInteger f = 2; f <= trialLimit && f * f <= remaining; f += f == 2 ? 1 : 2)
        {
            if (!(remaining % f).IsZero) continue;
            var count = 0;
            while ((remaining % f).IsZero)
            {
                remaining /= f;
                count++;
            }
            var existing = divisors.Count;
            var power = BigInteger.One;
            for (var e = 1; e <= count; e++)
            {
                power *= f;
                for (var i = 0; i < existing; i++) divisors.Add(divisors[i] * power);
            }
        }
        if (remaining > 1)
        {
            // Past the trial limit the cofactor may be composite; below it, it is prime.
            if (remaining >= (BigInteger)(trialLimit + 1) * (trialLimit + 1)) complete = false;
            var existing = divisors.Count;
            for (var i = 0; i < existing; i++) divisors.Add(divisors[i] * remaining);
        }
        return divisors;
    }
}
