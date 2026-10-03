using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Core.Tests;

[TestClass]
public class ComplexTests
{
    private const int Seed = 31415926;
    private const int Cases = 10_000;

    private static Complex<BigRational> Q(string re, string im) => new(BigRational.Parse(re), BigRational.Parse(im));

    private static TSelf Checked<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateChecked(value);

    private static TSelf Truncating<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateTruncating(value);

    private static TSelf Saturating<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateSaturating(value);

    [TestMethod]
    public void FieldAxiomsHoldOverGaussianRationals()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < Cases; i++)
        {
            var a = gen.GaussianRational();
            var b = gen.GaussianRational();
            var c = gen.GaussianRational();
            var ctx = $"seed={Seed} case={i} a={a} b={b} c={c}";

            Assert.AreEqual(a + b, b + a, ctx);
            Assert.AreEqual(a * b, b * a, ctx);
            Assert.AreEqual((a + b) + c, a + (b + c), ctx);
            Assert.AreEqual((a * b) * c, a * (b * c), ctx);
            Assert.AreEqual(a * (b + c), a * b + a * c, ctx);
            Assert.AreEqual(a, a + Complex<BigRational>.Zero, ctx);
            Assert.AreEqual(a, a * Complex<BigRational>.One, ctx);
            Assert.AreEqual(Complex<BigRational>.Zero, a + (-a), ctx);
            if (!Complex<BigRational>.IsZero(a))
            {
                Assert.AreEqual(Complex<BigRational>.One, a * a.Reciprocal, ctx);
                Assert.AreEqual(Complex<BigRational>.One, a / a, ctx);
            }
            if (!Complex<BigRational>.IsZero(b)) Assert.AreEqual(a, a / b * b, ctx);

            // |ab|² = |a|²|b|² and conj is a field automorphism.
            Assert.AreEqual((a * b).NormSquared, a.NormSquared * b.NormSquared, ctx);
            Assert.AreEqual((a * b).Conjugate, a.Conjugate * b.Conjugate, ctx);
            Assert.AreEqual(a.NormSquared, (a * a.Conjugate).Real, ctx);
        }
    }

    [TestMethod]
    public void ImaginaryUnitSquaresToMinusOne()
    {
        Assert.AreEqual(Complex<BigRational>.NegativeOne, Complex<BigRational>.ImaginaryOne * Complex<BigRational>.ImaginaryOne);
        Assert.AreEqual(Q("-1", "0"), Q("0", "1") * Q("0", "1"));
        Assert.AreEqual(Q("0", "-1"), Complex<BigRational>.One / Complex<BigRational>.ImaginaryOne);
        Assert.AreEqual(Q("1/5", "-2/5"), Complex<BigRational>.One / Q("1", "2"));
        Assert.Throws<DivideByZeroException>(() => Complex<BigRational>.One / Complex<BigRational>.Zero);
    }

    [TestMethod]
    public void FormatAndParseRoundTrip()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < Cases; i++)
        {
            var z = gen.GaussianRational(24);
            Assert.AreEqual(z, Complex<BigRational>.Parse(z.ToString()), $"seed={Seed + 1} case={i} z={z}");
        }
        Assert.AreEqual("(3/4, -2)", Q("3/4", "-2").ToString());
        Assert.AreEqual(Q("5", "0"), Complex<BigRational>.Parse("5"));
        Assert.IsFalse(Complex<BigRational>.TryParse("(1, 2", null, out _));
        Assert.IsFalse(Complex<BigRational>.TryParse("(1 2)", null, out _));
        Assert.IsFalse(Complex<BigRational>.TryParse("(1/0, 2)", null, out _));
        Assert.IsFalse(Complex<BigRational>.TryParse("", null, out _));
    }

    [TestMethod]
    public void ExactnessFollowsTheComponentType()
    {
        Assert.IsTrue(NumberTraits<Complex<BigRational>>.IsExact);
        Assert.IsFalse(NumberTraits<Complex<double>>.IsExact);
        Assert.IsFalse(NumberTraits<Complex<float>>.IsExact);
        Assert.IsTrue(NumberTraits<BigRational>.IsExact);
        Assert.IsTrue(NumberTraits<BigInteger>.IsExact);
        Assert.IsTrue(NumberTraits<int>.IsExact);
        Assert.IsTrue(NumberTraits<long>.IsExact);
        Assert.IsFalse(NumberTraits<double>.IsExact);
        Assert.IsFalse(NumberTraits<float>.IsExact);
        Assert.IsFalse(NumberTraits<decimal>.IsExact);
        Assert.IsFalse(NumberTraits<Dual<double>>.IsExact);
    }

    [TestMethod]
    public void MagnitudeNeedsFloatingPoint()
    {
        Assert.Throws<NotSupportedException>(() => Complex<BigRational>.Abs(Q("3", "4")));
        Assert.AreEqual(5.0, Complex<double>.Abs(new Complex<double>(3, 4)).Real);
        Assert.AreEqual(5.0, new Complex<double>(3, 4).Magnitude);
        Assert.AreEqual(Math.PI / 2, new Complex<double>(0, 2).Phase, 1e-15);
        Assert.AreEqual(Q("25", "0").Real, Q("3", "4").NormSquared);
        Assert.AreEqual(Q("3", "4"), Complex<BigRational>.MaxMagnitude(Q("3", "4"), Q("0", "2")));
    }

    [TestMethod]
    public void TranscendentalFunctionsMatchSystemNumerics()
    {
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < 2_000; i++)
        {
            var re = gen.Uniform(-5, 5);
            var im = gen.Uniform(-5, 5);
            var z = new Complex<double>(re, im);
            var reference = new System.Numerics.Complex(re, im);
            var ctx = $"seed={Seed + 2} case={i} z={z}";

            AssertClose(System.Numerics.Complex.Exp(reference), Complex<double>.Exp(z), ctx);
            AssertClose(System.Numerics.Complex.Log(reference), Complex<double>.Log(z), ctx);
            AssertClose(System.Numerics.Complex.Sqrt(reference), Complex<double>.Sqrt(z), ctx);
            AssertClose(reference * reference / (reference + new System.Numerics.Complex(10, 1)), z * z / (z + new Complex<double>(10, 1)), ctx);

            // Euler: exp(iθ) = cos θ + i sin θ, and sqrt(z)² = z.
            AssertClose(new System.Numerics.Complex(Math.Cos(re), Math.Sin(re)), Complex<double>.Exp(new Complex<double>(0, re)), ctx);
            var root = Complex<double>.Sqrt(z);
            AssertClose(reference, root * root, ctx);
        }
        Assert.AreEqual(new Complex<double>(0, 1).Real, Complex<double>.Sqrt(new Complex<double>(-1, 0)).Real, 1e-15);
        Assert.AreEqual(1.0, Complex<double>.Sqrt(new Complex<double>(-1, 0)).Imaginary, 1e-15);
        Assert.AreEqual(double.NegativeInfinity, Complex<double>.Log(Complex<double>.Zero).Real);
    }

    private static void AssertClose(System.Numerics.Complex expected, Complex<double> actual, string context)
    {
        var tolerance = 1e-12 * Math.Max(1.0, expected.Magnitude);
        Assert.AreEqual(expected.Real, actual.Real, tolerance, context);
        Assert.AreEqual(expected.Imaginary, actual.Imaginary, tolerance, context);
    }

    [TestMethod]
    public void PredicatesAndConversions()
    {
        Assert.IsTrue(Complex<BigRational>.IsRealNumber(Q("3", "0")));
        Assert.IsTrue(Complex<BigRational>.IsImaginaryNumber(Q("0", "3")));
        Assert.IsTrue(Complex<BigRational>.IsComplexNumber(Q("1", "3")));
        Assert.IsTrue(Complex<BigRational>.IsInteger(Q("4", "0")));
        Assert.IsFalse(Complex<BigRational>.IsInteger(Q("4", "1")));
        Assert.IsTrue(Complex<BigRational>.IsZero(default));
        Assert.AreEqual(Q("0", "0"), default);

        Assert.AreEqual(Q("7", "0"), Checked<Complex<BigRational>, int>(7));
        Assert.AreEqual(Q("5/2", "0"), Checked<Complex<BigRational>, BigRational>(BigRational.Parse("5/2")));
        Assert.AreEqual(3, Checked<int, Complex<BigRational>>(Q("3", "0")));
        Assert.Throws<OverflowException>(() => Checked<int, Complex<BigRational>>(Q("3", "1")));
        Assert.AreEqual(3, Truncating<int, Complex<BigRational>>(Q("3", "1")));
        Assert.AreEqual(new Complex<double>(2.5, 0), Checked<Complex<double>, double>(2.5));
        Assert.AreEqual(Q("1", "0"), (Complex<BigRational>)BigRational.One);
    }

    [TestMethod]
    public void GenericAlgorithmsAcceptComplex()
    {
        // Horner evaluation of x² + 1 at i is 0, using only the generic-math operator interfaces.
        static T Horner<T>(T[] coefficients, T x) where T : INumberBase<T>
        {
            var acc = T.Zero;
            foreach (var c in coefficients) acc = acc * x + c;
            return acc;
        }
        var one = Complex<BigRational>.One;
        Assert.AreEqual(Complex<BigRational>.Zero, Horner([one, Complex<BigRational>.Zero, one], Complex<BigRational>.ImaginaryOne));
    }

    [TestMethod]
    public void SquareRootOfANegativeRealIsExactlyImaginary()
    {
        Assert.AreEqual(new Complex<double>(0, 2), Complex<double>.Sqrt(new Complex<double>(-4, 0)));
        Assert.AreEqual(new Complex<double>(0, 3), Complex<double>.Sqrt(new Complex<double>(-9, 0)));
        Assert.AreEqual(new Complex<double>(2, 1), Complex<double>.Sqrt(new Complex<double>(3, 4)));
        Assert.AreEqual(new Complex<double>(2, -1), Complex<double>.Sqrt(new Complex<double>(3, -4)));
        Assert.AreEqual(Complex<double>.Zero, Complex<double>.Sqrt(Complex<double>.Zero));
    }

    [TestMethod]
    public void NaNEqualsItselfLikeDouble()
    {
        var z = new Complex<double>(double.NaN, 1);
        Assert.IsTrue(z.Equals(z));
        Assert.IsTrue(new Dual<double>(double.NaN, 0).Equals(new Dual<double>(double.NaN, 0)));
    }
}
