using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Core.Tests;

[TestClass]
public class BigRationalTests
{
    private const int Seed = 20261001;
    private const int Cases = 10_000;

    private static BigRational R(string text) => BigRational.Parse(text);

    private static TSelf Checked<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateChecked(value);

    private static TSelf Truncating<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateTruncating(value);

    private static TSelf Saturating<TSelf, TOther>(TOther value) where TSelf : INumberBase<TSelf> where TOther : INumberBase<TOther> => TSelf.CreateSaturating(value);

    [TestMethod]
    public void FieldAxiomsHoldOnSeededRandomCases()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < Cases; i++)
        {
            var a = gen.Rational();
            var b = gen.Rational();
            var c = gen.Rational();
            var ctx = $"seed={Seed} case={i} a={a} b={b} c={c}";

            Assert.AreEqual(a + b, b + a, ctx);
            Assert.AreEqual(a * b, b * a, ctx);
            Assert.AreEqual((a + b) + c, a + (b + c), ctx);
            Assert.AreEqual((a * b) * c, a * (b * c), ctx);
            Assert.AreEqual(a * (b + c), a * b + a * c, ctx);
            Assert.AreEqual(a, a + BigRational.Zero, ctx);
            Assert.AreEqual(a, a * BigRational.One, ctx);
            Assert.AreEqual(BigRational.Zero, a + (-a), ctx);
            Assert.AreEqual(a - b, a + (-b), ctx);
            if (a != BigRational.Zero)
            {
                Assert.AreEqual(BigRational.One, a * BigRational.Reciprocal(a), ctx);
                Assert.AreEqual(BigRational.One, a / a, ctx);
            }
            if (b != BigRational.Zero) Assert.AreEqual(a, a / b * b, ctx);

            // Normalization invariant: gcd 1 and positive denominator.
            var sum = a + b * c;
            Assert.IsTrue(sum.Denominator.Sign > 0, ctx);
            Assert.AreEqual(BigInteger.One, BigInteger.GreatestCommonDivisor(sum.Numerator, sum.Denominator), ctx);
        }
    }

    [TestMethod]
    public void OrderAxiomsHoldOnSeededRandomCases()
    {
        var gen = new Gen(Seed + 1);
        for (var i = 0; i < Cases; i++)
        {
            var a = gen.Rational();
            var b = gen.Rational();
            var c = gen.Rational();
            var ctx = $"seed={Seed + 1} case={i} a={a} b={b} c={c}";

            // Totality and antisymmetry.
            Assert.IsTrue(a <= b || b <= a, ctx);
            if (a <= b && b <= a) Assert.AreEqual(a, b, ctx);
            Assert.AreEqual(-a.CompareTo(b), b.CompareTo(a), ctx);

            // Transitivity.
            if (a <= b && b <= c) Assert.IsTrue(a <= c, ctx);

            // Compatibility with + and *.
            if (a < b) Assert.IsTrue(a + c < b + c, ctx);
            if (a < b && c > BigRational.Zero) Assert.IsTrue(a * c < b * c, ctx);
            if (a < b && c < BigRational.Zero) Assert.IsTrue(a * c > b * c, ctx);

            // Order agrees with the sign of the difference, and equal values hash equally.
            Assert.AreEqual(Math.Sign(a.CompareTo(b)), (a - b).Sign, ctx);
            if (a == b) Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), ctx);
        }
    }

    [TestMethod]
    public void FormatAndParseRoundTrip()
    {
        var gen = new Gen(Seed + 2);
        for (var i = 0; i < Cases; i++)
        {
            var a = gen.Rational(24);
            var ctx = $"seed={Seed + 2} case={i} a={a}";
            Assert.AreEqual(a, BigRational.Parse(a.ToString()), ctx);
            Assert.AreEqual(a, BigRational.Parse(a.ToString(null, null)), ctx);
            Assert.IsTrue(BigRational.TryParse(a.ToString(), null, out var viaTryParse) && viaTryParse == a, ctx);
        }
    }

    [TestMethod]
    public void DecimalExpansionRoundTrips()
    {
        var gen = new Gen(Seed + 3);
        for (var i = 0; i < 2_000; i++)
        {
            // Small denominators keep the repeating block short.
            var a = BigRational.Create(gen.WholeNumber(24), gen.Random.Next(1, 2_000));
            var text = a.ToDecimalString();
            Assert.AreEqual(a, BigRational.Parse(text), $"seed={Seed + 3} case={i} a={a} text={text}");
            Assert.AreEqual(text, a.ToString("D", null));
        }
    }

    [TestMethod]
    [DataRow("3/4", 3, 4)]
    [DataRow("-2", -2, 1)]
    [DataRow("+7", 7, 1)]
    [DataRow("0.125", 1, 8)]
    [DataRow("-0.5", -1, 2)]
    [DataRow(".5", 1, 2)]
    [DataRow("0.1(6)", 1, 6)]
    [DataRow("0.(3)", 1, 3)]
    [DataRow("1.2(34)", 611, 495)]
    [DataRow("1.5e-3", 3, 2000)]
    [DataRow("2E3", 2000, 1)]
    [DataRow("  6/8  ", 3, 4)]
    [DataRow("0/5", 0, 1)]
    public void ParsesDocumentedForms(string text, int numerator, int denominator)
    {
        var parsed = BigRational.Parse(text);
        Assert.AreEqual((BigInteger)numerator, parsed.Numerator);
        Assert.AreEqual((BigInteger)denominator, parsed.Denominator);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("abc")]
    [DataRow("1/0")]
    [DataRow("1/")]
    [DataRow("/2")]
    [DataRow("1/-2")]
    [DataRow("--1")]
    [DataRow("0.1(")]
    [DataRow("0.1()")]
    [DataRow("0.1(6")]
    [DataRow("1e")]
    [DataRow("1e999999")]
    [DataRow(".")]
    [DataRow("1.2.3")]
    public void RejectsMalformedText(string text)
    {
        Assert.IsFalse(BigRational.TryParse(text, null, out _));
        Assert.Throws<FormatException>(() => BigRational.Parse(text));
    }

    [TestMethod]
    public void DecimalStringShapes()
    {
        Assert.AreEqual("0.125", R("1/8").ToDecimalString());
        Assert.AreEqual("0.1(6)", R("1/6").ToDecimalString());
        Assert.AreEqual("-4", R("-4").ToDecimalString());
        Assert.AreEqual("-0.(3)", R("-1/3").ToDecimalString());
        Assert.AreEqual("0.(142857)", R("1/7").ToDecimalString());
        Assert.Throws<InvalidOperationException>(() => R("1/9973").ToDecimalString(maxDigits: 100));
    }

    [TestMethod]
    public void ConversionToDoubleIsWithinHalfAnUlp()
    {
        var gen = new Gen(Seed + 4);
        for (var i = 0; i < Cases; i++)
        {
            var r = gen.Rational(gen.Random.Next(1, 200));
            if (r.IsZero()) continue;
            var d = r.ToDouble();
            var ctx = $"seed={Seed + 4} case={i} r={r} d={d:R}";
            Assert.IsTrue(double.IsFinite(d), ctx);

            var magnitude = Math.Abs(d);
            var ulp = BigRational.FromDouble(Math.BitIncrement(magnitude) - magnitude);
            var error = BigRational.Abs(BigRational.FromDouble(d) - r);
            Assert.IsTrue(error <= ulp, ctx);
            Assert.IsTrue(error * 2 <= ulp, ctx);
        }
    }

    [TestMethod]
    public void EveryFiniteDoubleRoundTripsExactly()
    {
        var gen = new Gen(Seed + 5);
        for (var i = 0; i < Cases; i++)
        {
            var d = gen.AnyFiniteDouble();
            var r = BigRational.FromDouble(d);
            Assert.AreEqual(d, r.ToDouble(), $"seed={Seed + 5} case={i} d={d:R}");
            Assert.AreEqual(d < 0, r.Sign < 0);
        }
        Assert.AreEqual(BigRational.Zero, BigRational.FromDouble(-0.0));
        Assert.AreEqual(double.Epsilon, BigRational.FromDouble(double.Epsilon).ToDouble());
        Assert.AreEqual(double.MaxValue, BigRational.FromDouble(double.MaxValue).ToDouble());
    }

    [TestMethod]
    public void DoubleConversionEdgeCases()
    {
        var huge = BigRational.Pow(2, 2000);
        Assert.AreEqual(R("1/10"), BigRational.FromShortestDecimal(0.1));
        Assert.AreNotEqual(R("1/10"), BigRational.FromDouble(0.1));
        Assert.AreEqual(double.PositiveInfinity, huge.ToDouble());
        Assert.AreEqual(double.NegativeInfinity, (-huge).ToDouble());
        Assert.AreEqual(0.0, (BigRational.One / huge).ToDouble());
        Assert.Throws<OverflowException>(() => BigRational.FromDouble(double.NaN));
        Assert.Throws<OverflowException>(() => BigRational.FromDouble(double.PositiveInfinity));
        Assert.AreEqual(0.1 + 0.2, (BigRational.FromDouble(0.1) + BigRational.FromDouble(0.2)).ToDouble());
    }

    [TestMethod]
    public void DecimalConversions()
    {
        Assert.AreEqual(R("12345/100"), BigRational.FromDecimal(123.45m));
        Assert.AreEqual(R("-1/8"), BigRational.FromDecimal(-0.125m));
        Assert.AreEqual(decimal.MaxValue, BigRational.FromDecimal(decimal.MaxValue).ToDecimal());
        Assert.AreEqual(decimal.MinValue, BigRational.FromDecimal(decimal.MinValue).ToDecimal());
        Assert.AreEqual(0.3333333333333333333333333333m, R("1/3").ToDecimal());
        Assert.AreEqual(-2.5m, R("-5/2").ToDecimal());
        Assert.Throws<OverflowException>(() => BigRational.Pow(2, 100).ToDecimal());

        var gen = new Gen(Seed + 6);
        for (var i = 0; i < 2_000; i++)
        {
            var m = new decimal(gen.Random.Next(), gen.Random.Next(), gen.Random.Next(), gen.Random.Next(2) == 0, (byte)gen.Random.Next(0, 29));
            Assert.AreEqual(m, BigRational.FromDecimal(m).ToDecimal());
        }
    }

    [TestMethod]
    public void BestApproximationOfDouble()
    {
        Assert.AreEqual(R("355/113"), BigRational.Approximate(Math.PI, 113));
        Assert.AreEqual(R("22/7"), BigRational.Approximate(Math.PI, 7));
        Assert.AreEqual(R("1/3"), BigRational.Approximate(0.3333, 10));
        Assert.AreEqual(R("-1/3"), BigRational.Approximate(-0.3333, 10));
        Assert.AreEqual(R("3"), BigRational.Approximate(2.9999999, 1));
        Assert.AreEqual(R("1/10"), BigRational.Approximate(BigRational.FromDouble(0.1), 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => BigRational.Approximate(0.5, 0));

        // No fraction with denominator <= bound is closer than the answer (checked by brute force).
        var gen = new Gen(Seed + 7);
        for (var i = 0; i < 300; i++)
        {
            var x = gen.Rational(40);
            var max = gen.Random.Next(1, 60);
            var best = BigRational.Approximate(x, max);
            Assert.IsTrue(best.Denominator <= max);
            var bestError = BigRational.Abs(best - x);
            for (var q = 1; q <= max; q++)
            {
                var p = BigRational.Floor(x * q);
                foreach (var candidate in new[] { BigRational.Create(p, q), BigRational.Create(p + 1, q) })
                {
                    Assert.IsTrue(bestError <= BigRational.Abs(candidate - x), $"x={x} max={max} best={best} candidate={candidate}");
                }
            }
        }
    }

    [TestMethod]
    public void FloorCeilingTruncateAndRemainder()
    {
        Assert.AreEqual((BigInteger)2, BigRational.Floor(R("5/2")));
        Assert.AreEqual((BigInteger)(-3), BigRational.Floor(R("-5/2")));
        Assert.AreEqual((BigInteger)3, BigRational.Ceiling(R("5/2")));
        Assert.AreEqual((BigInteger)(-2), BigRational.Ceiling(R("-5/2")));
        Assert.AreEqual((BigInteger)(-2), BigRational.Truncate(R("-5/2")));
        Assert.AreEqual(R("1/2"), R("5/2") % R("1"));
        Assert.AreEqual(R("-1/2"), R("-5/2") % R("1"));
        Assert.AreEqual(R("1/3"), R("7/3") % R("2"));

        var gen = new Gen(Seed + 8);
        for (var i = 0; i < Cases; i++)
        {
            var a = gen.Rational();
            var b = gen.NonZeroRational();
            var remainder = a % b;
            var quotient = BigRational.Truncate(a / b);
            Assert.AreEqual(a, new BigRational(quotient) * b + remainder, $"a={a} b={b}");
            Assert.IsTrue(BigRational.Abs(remainder) < BigRational.Abs(b));
            Assert.IsTrue(remainder.Sign == 0 || remainder.Sign == a.Sign);
        }
    }

    [TestMethod]
    public void PowerAndReciprocal()
    {
        Assert.AreEqual(R("8/27"), BigRational.Pow(R("2/3"), 3));
        Assert.AreEqual(R("9/4"), BigRational.Pow(R("2/3"), -2));
        Assert.AreEqual(R("-1/8"), BigRational.Pow(R("-1/2"), 3));
        Assert.AreEqual(BigRational.One, BigRational.Pow(BigRational.Zero, 0));
        Assert.AreEqual(BigRational.One, BigRational.Pow(R("5/7"), 0));
        Assert.AreEqual(BigRational.Zero, BigRational.Pow(BigRational.Zero, 5));
        Assert.Throws<DivideByZeroException>(() => BigRational.Pow(BigRational.Zero, -1));
        Assert.AreEqual(R("-3/2"), BigRational.Reciprocal(R("-2/3")));
        Assert.Throws<DivideByZeroException>(() => BigRational.Reciprocal(BigRational.Zero));
        Assert.Throws<DivideByZeroException>(() => R("1") / BigRational.Zero);
        Assert.Throws<DivideByZeroException>(() => BigRational.Create(1, 0));
    }

    [TestMethod]
    public void DefaultIsZeroAndEqualValuesAreEqual()
    {
        Assert.AreEqual(BigRational.Zero, default);
        Assert.AreEqual(BigInteger.One, default(BigRational).Denominator);
        Assert.IsTrue(default(BigRational).IsInteger);
        Assert.AreEqual(BigRational.Create(2, 4), BigRational.Create(-3, -6));
        Assert.AreEqual(BigRational.Create(2, 4).GetHashCode(), BigRational.Create(1, 2).GetHashCode());
        Assert.AreEqual(R("-1/2"), BigRational.Create(1, -2));
        Assert.AreEqual("0", BigRational.Zero.ToString());
        Assert.AreEqual("-1/2", BigRational.Create(1, -2).ToString());
    }

    [TestMethod]
    public void GenericMathAcceptsBigRational()
    {
        static T SumOfSquares<T>(IEnumerable<T> items) where T : INumber<T> => items.Aggregate(T.Zero, (acc, x) => acc + x * x);
        static int SignOf<T>(T x) where T : INumber<T> => T.Sign(x);
        static bool IsInt<T>(T x) where T : INumberBase<T> => T.IsInteger(x);

        Assert.AreEqual(R("25/18"), SumOfSquares([R("1/2"), R("1/3"), R("1"), R("-1/6")]));
        Assert.AreEqual(R("5/2"), Checked<BigRational, double>(2.5));
        Assert.AreEqual(R("7"), Checked<BigRational, int>(7));
        Assert.AreEqual(2.5, Checked<double, BigRational>(R("5/2")));
        Assert.AreEqual(2, Truncating<int, BigRational>(R("5/2")));
        Assert.AreEqual(-2, Truncating<int, BigRational>(R("-5/2")));
        Assert.AreEqual(R("3/2"), BigRational.Max(R("1"), R("3/2")));
        Assert.AreEqual(R("1"), BigRational.Clamp(R("1/2"), R("1"), R("2")));
        Assert.AreEqual(-1, SignOf(R("-1/2")));
        Assert.IsTrue(IsInt(R("4")));
        Assert.IsFalse(IsInt(R("9/2")));
        Assert.IsTrue(BigRational.IsEvenInteger(R("4")));
        Assert.IsTrue(BigRational.IsOddInteger(R("-3")));
        Assert.AreEqual(R("-1/2"), BigRational.CopySign(R("1/2"), R("-3")));
        Assert.AreEqual("-3/4", $"{R("-3/4")}");
        Assert.Throws<OverflowException>(() => Checked<byte, BigRational>(R("300")));
        Assert.AreEqual(byte.MaxValue, Saturating<byte, BigRational>(R("300")));
    }

    [TestMethod]
    public void IsAnExactNumber()
    {
        Assert.IsInstanceOfType<IExactNumber>(BigRational.One);
        Assert.IsTrue(NumberTraits<BigRational>.IsExact);
    }
}
