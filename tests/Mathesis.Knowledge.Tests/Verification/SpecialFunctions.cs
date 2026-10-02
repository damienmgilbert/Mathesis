using System.Numerics;

namespace Mathesis.Knowledge.Tests.Verification;

/// <summary>Special functions the verifier needs and the BCL lacks (double precision, about 1e-13 accuracy).</summary>
internal static class SpecialFunctions
{
    /// <summary>The error function, from its Maclaurin series for small |x| and the complementary continued fraction otherwise.</summary>
    public static double Erf(double x)
    {
        if (double.IsNaN(x)) return double.NaN;
        var a = Math.Abs(x);
        if (a < 2.5)
        {
            // erf x = 2/√π Σ (−1)^n x^(2n+1) / (n! (2n+1))
            var term = a;
            var sum = a;
            for (var n = 1; n < 200; n++)
            {
                term *= -a * a / n;
                var add = term / (2 * n + 1);
                sum += add;
                if (Math.Abs(add) < 1e-17 * Math.Abs(sum)) break;
            }
            return Math.Sign(x) * 2 / Math.Sqrt(Math.PI) * sum;
        }
        if (a > 6) return Math.Sign(x);

        // erfc x = e^(−x²)/√π · 1/(x + (1/2)/(x + 1/(x + (3/2)/(x + 2/(x + …))))) (Lentz-free backward evaluation)
        var f = 0.0;
        for (var k = 60; k >= 1; k--) f = k / 2.0 / (a + f);
        var erfc = Math.Exp(-a * a) / Math.Sqrt(Math.PI) / (a + f);
        return Math.Sign(x) * (1 - erfc);
    }

    /// <summary>The gamma function for real arguments (Lanczos, g = 7), with the reflection formula for x &lt; 0.5.</summary>
    public static double Gamma(double x)
    {
        if (x == Math.Floor(x) && x <= 0) return double.NaN;
        if (x < 0.5) return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1 - x));
        if (x == Math.Floor(x) && x < 171) return Factorial((int)x - 1);
        double[] g =
        [
            0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059,
            12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
        ];
        x -= 1;
        var a = g[0];
        var t = x + 7.5;
        for (var i = 1; i < 9; i++) a += g[i] / (x + i);
        return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * a;
    }

    public static double Factorial(int n)
    {
        if (n < 0) return double.NaN;
        var result = 1.0;
        for (var i = 2; i <= n; i++) result *= i;
        return result;
    }

    /// <summary>The digamma function ψ(x): recurrence up to x ≥ 10, then the asymptotic series; reflection for x &lt; 0.</summary>
    public static double Digamma(double x)
    {
        if (x == Math.Floor(x) && x <= 0) return double.NaN;
        if (x < 0) return Digamma(1 - x) - Math.PI / Math.Tan(Math.PI * x);
        var result = 0.0;
        while (x < 10)
        {
            result -= 1 / x;
            x++;
        }
        var f = 1 / (x * x);
        return result + Math.Log(x) - 0.5 / x - f * (1.0 / 12 - f * (1.0 / 120 - f * (1.0 / 252 - f * (1.0 / 240 - f * (1.0 / 132)))));
    }

    /// <summary>The principal branch W₀ of the Lambert W function for x ≥ −1/e (Halley iteration).</summary>
    public static double LambertW(double x)
    {
        if (x < -1 / Math.E) return double.NaN;
        if (x == 0) return 0;
        var w = x < 1 ? Math.Sqrt(2 * (Math.E * x + 1)) - 1 : Math.Log(x) - Math.Log(Math.Log(x) + 1);
        if (x > -0.3 && x < 1) w = x * (1 - x + 1.5 * x * x);
        for (var i = 0; i < 60; i++)
        {
            var e = Math.Exp(w);
            var f = w * e - x;
            var step = f / (e * (w + 1) - (w + 2) * f / (2 * w + 2));
            w -= step;
            if (Math.Abs(step) < 1e-15 * (1 + Math.Abs(w))) break;
        }
        return w;
    }

    /// <summary>Chebyshev polynomial of the first kind T_n(x) by recurrence (n ≥ 0).</summary>
    public static double ChebyshevT(int n, double x)
    {
        if (n < 0) return double.NaN;
        double previous = 1, current = x;
        if (n == 0) return previous;
        for (var k = 2; k <= n; k++) (previous, current) = (current, 2 * x * current - previous);
        return current;
    }

    public static double Binomial(double n, double k)
    {
        if (k < 0 || k != Math.Floor(k) || k > 1000) return double.NaN;
        var result = 1.0;
        for (var i = 0; i < (int)k; i++) result = result * (n - i) / (i + 1);
        return result;
    }

    public static BigInteger Gcd(BigInteger a, BigInteger b) => BigInteger.GreatestCommonDivisor(a, b);
}
