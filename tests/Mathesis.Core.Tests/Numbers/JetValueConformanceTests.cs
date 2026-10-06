using Mathesis.Numbers;
using System.Numerics;
using Mathesis.Testing;

namespace Mathesis.Core.Tests;

/// <summary>
/// Value conformance (ADR-19): for every <c>IFloatingPointIeee754</c>, <c>IFloatingPoint</c>, <c>INumber</c> and
/// <c>INumberBase</c> member the value part of the jet equals <c>double</c>'s result, bit for bit (NaN equals NaN), on 10,000 seeded inputs
/// plus the special values. Each member is written once, generic over the number type, and run on <c>double</c> and on the jet, with the jet
/// both as a constant and as a variable (the gradient must not change the value).
/// </summary>
[TestClass]
public class JetValueConformanceTests
{
    private const int Seed = 4001;
    private const int Random = 10_000;

    private static readonly double[] Specials =
    [
        double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
        2.2250738585072014E-308 / 2, double.MaxValue, double.MinValue, 1.0, -1.0, 0.5, -0.5, 2.5, -2.5, 3.5, 1e-300, 1e300, Math.PI, -Math.PI, 7.0, -7.0,
    ];

    /// <summary>The seeded random inputs followed by the specials; a variable every few values so the jets mix kinds.</summary>
    private static double[] Inputs(Gen gen)
    {
        var values = new List<double>(Random + Specials.Length);
        for (var i = 0; i < Random; i++)
        {
            var scale = Math.Pow(10.0, gen.Uniform(-8, 8));
            values.Add(gen.Uniform(-1, 1) * scale);
        }
        values.AddRange(Specials);
        return [.. values];
    }

    private static bool Same(double a, double b) => (double.IsNaN(a) && double.IsNaN(b)) || BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    private static int Count;

    private static void Unary(string name, Func<double, double> reference, Func<JetD, JetD> jet, double[] inputs)
    {
        foreach (var x in inputs)
        {
            var expected = reference(x);
            var c = jet(JetD.Constant(x)).Value;
            var v = jet(JetD.Variable(x, 0, 2)).Value;
            Assert.IsTrue(Same(expected, c) && Same(expected, v), $"{name}({x:R}): double {expected:R}, constant jet {c:R}, variable jet {v:R}");
            Count++;
        }
    }

    private static void Binary(string name, Func<double, double, double> reference, Func<JetD, JetD, JetD> jet, double[] xs, double[] ys)
    {
        for (var i = 0; i < xs.Length; i++)
        {
            double x = xs[i], y = ys[i];
            double expected;
            try { expected = reference(x, y); }
            catch (Exception) { continue; }
            var c = jet(JetD.Constant(x), JetD.Constant(y)).Value;
            var v = jet(JetD.Variable(x, 0, 2), JetD.Variable(y, 1, 2)).Value;
            var mixed = jet(JetD.Variable(x, 0, 2), JetD.Constant(y)).Value;
            Assert.IsTrue(Same(expected, c) && Same(expected, v) && Same(expected, mixed), $"{name}({x:R}, {y:R}): double {expected:R}, jets {c:R}, {v:R}, {mixed:R}");
            Count++;
        }
    }

    [TestMethod]
    public void UnaryMembersMatchDoubleOnSeededAndSpecialInputs()
    {
        var gen = new Gen(Seed);
        var xs = Inputs(gen);
        var unit = xs.Select(x => Math.Abs(x) <= 1.0 ? x : (x % 1.0)).ToArray();   // inside the domain of asin, acos, atanh, ...
        Count = 0;

        Unary("Exp", double.Exp, JetD.Exp, xs);
        Unary("ExpM1", double.ExpM1, JetD.ExpM1, xs);
        Unary("Exp2", double.Exp2, JetD.Exp2, xs);
        Unary("Exp2M1", double.Exp2M1, JetD.Exp2M1, xs);
        Unary("Exp10", double.Exp10, JetD.Exp10, xs);
        Unary("Exp10M1", double.Exp10M1, JetD.Exp10M1, xs);
        Unary("Log", double.Log, JetD.Log, xs);
        Unary("Log2", double.Log2, JetD.Log2, xs);
        Unary("Log10", double.Log10, JetD.Log10, xs);
        Unary("LogP1", double.LogP1, JetD.LogP1, xs);
        Unary("Log2P1", double.Log2P1, JetD.Log2P1, xs);
        Unary("Log10P1", double.Log10P1, JetD.Log10P1, xs);
        Unary("Sqrt", double.Sqrt, JetD.Sqrt, xs);
        Unary("Cbrt", double.Cbrt, JetD.Cbrt, xs);
        Unary("Sin", double.Sin, JetD.Sin, xs);
        Unary("Cos", double.Cos, JetD.Cos, xs);
        Unary("Tan", double.Tan, JetD.Tan, xs);
        Unary("SinPi", double.SinPi, JetD.SinPi, xs);
        Unary("CosPi", double.CosPi, JetD.CosPi, xs);
        Unary("TanPi", double.TanPi, JetD.TanPi, xs);
        Unary("Asin", double.Asin, JetD.Asin, [.. xs, .. unit]);
        Unary("Acos", double.Acos, JetD.Acos, [.. xs, .. unit]);
        Unary("Atan", double.Atan, JetD.Atan, xs);
        Unary("AsinPi", double.AsinPi, JetD.AsinPi, [.. xs, .. unit]);
        Unary("AcosPi", double.AcosPi, JetD.AcosPi, [.. xs, .. unit]);
        Unary("AtanPi", double.AtanPi, JetD.AtanPi, xs);
        Unary("Sinh", double.Sinh, JetD.Sinh, xs);
        Unary("Cosh", double.Cosh, JetD.Cosh, xs);
        Unary("Tanh", double.Tanh, JetD.Tanh, xs);
        Unary("Asinh", double.Asinh, JetD.Asinh, xs);
        Unary("Acosh", double.Acosh, JetD.Acosh, xs);
        Unary("Atanh", double.Atanh, JetD.Atanh, [.. xs, .. unit]);
        Unary("Abs", double.Abs, JetD.Abs, xs);
        Unary("Floor", double.Floor, JetD.Floor, xs);
        Unary("Ceiling", double.Ceiling, JetD.Ceiling, xs);
        Unary("Truncate", double.Truncate, JetD.Truncate, xs);
        Unary("Round", double.Round, JetD.Round, xs);
        Unary("BitIncrement", double.BitIncrement, JetD.BitIncrement, xs);
        Unary("BitDecrement", double.BitDecrement, JetD.BitDecrement, xs);
        Unary("ReciprocalEstimate", double.ReciprocalEstimate, JetD.ReciprocalEstimate, xs);
        Unary("ReciprocalSqrtEstimate", double.ReciprocalSqrtEstimate, JetD.ReciprocalSqrtEstimate, xs);
        Unary("Negate", x => -x, x => -x, xs);
        Unary("UnaryPlus", x => +x, x => +x, xs);
        Unary("Increment", x => x + 1.0, x => { var y = x; y++; return y; }, xs);
        Unary("Decrement", x => x - 1.0, x => { var y = x; y--; return y; }, xs);
        foreach (var digits in new[] { 0, 1, 3, 15 })
            Unary($"Round(digits {digits})", x => double.Round(x, digits), x => JetD.Round(x, digits), xs.Where(x => Math.Abs(x) < 1e15).ToArray());
        foreach (var mode in Enum.GetValues<MidpointRounding>())
        {
            Unary($"Round({mode})", x => double.Round(x, mode), x => JetD.Round(x, mode), xs);
            Unary($"Round(2, {mode})", x => double.Round(x, 2, mode), x => JetD.Round(x, 2, mode), xs.Where(x => Math.Abs(x) < 1e15).ToArray());
        }
        foreach (var n in new[] { -3, 0, 1, 5, 100 })
            Unary($"ScaleB({n})", x => double.ScaleB(x, n), x => JetD.ScaleB(x, n), xs);
        foreach (var n in new[] { -3, 1, 2, 3, 5 })
            Unary($"RootN({n})", x => double.RootN(x, n), x => JetD.RootN(x, n), xs);
        Assert.IsGreaterThan(300_000, Count, "the conformance sweep must be large");
    }

    [TestMethod]
    public void BinaryAndTernaryMembersMatchDoubleOnSeededAndSpecialInputs()
    {
        var gen = new Gen(Seed + 1);
        var xs = new List<double>();
        var ys = new List<double>();
        for (var i = 0; i < Random; i++)
        {
            xs.Add(gen.Uniform(-1, 1) * Math.Pow(10.0, gen.Uniform(-4, 4)));
            ys.Add(gen.Uniform(-1, 1) * Math.Pow(10.0, gen.Uniform(-4, 4)));
        }
        // Every pair of specials, so ties, signed zeros, NaNs and infinities meet each other.
        foreach (var a in Specials)
            foreach (var b in Specials) { xs.Add(a); ys.Add(b); }
        // Equal-magnitude pairs for the magnitude ties.
        for (var i = 0; i < 200; i++) { var m = gen.Uniform(0.1, 9); xs.Add(m); ys.Add(-m); xs.Add(-m); ys.Add(m); xs.Add(m); ys.Add(m); }
        double[] x = [.. xs], y = [.. ys];
        Count = 0;

        Binary("Add", (a, b) => a + b, (a, b) => a + b, x, y);
        Binary("Subtract", (a, b) => a - b, (a, b) => a - b, x, y);
        Binary("Multiply", (a, b) => a * b, (a, b) => a * b, x, y);
        Binary("Divide", (a, b) => a / b, (a, b) => a / b, x, y);
        Binary("Modulus", (a, b) => a % b, (a, b) => a % b, x, y);
        Binary("Pow", double.Pow, JetD.Pow, x, y);
        Binary("Atan2", double.Atan2, JetD.Atan2, x, y);
        Binary("Atan2Pi", double.Atan2Pi, JetD.Atan2Pi, x, y);
        Binary("Hypot", double.Hypot, JetD.Hypot, x, y);
        Binary("Log(x, base)", double.Log, JetD.Log, x, y);
        Binary("Max", double.Max, JetD.Max, x, y);
        Binary("Min", double.Min, JetD.Min, x, y);
        Binary("MaxNumber", double.MaxNumber, JetD.MaxNumber, x, y);
        Binary("MinNumber", double.MinNumber, JetD.MinNumber, x, y);
        Binary("MaxMagnitude", double.MaxMagnitude, JetD.MaxMagnitude, x, y);
        Binary("MinMagnitude", double.MinMagnitude, JetD.MinMagnitude, x, y);
        Binary("MaxMagnitudeNumber", double.MaxMagnitudeNumber, JetD.MaxMagnitudeNumber, x, y);
        Binary("MinMagnitudeNumber", double.MinMagnitudeNumber, JetD.MinMagnitudeNumber, x, y);
        Binary("CopySign", double.CopySign, JetD.CopySign, x, y);
        Binary("Ieee754Remainder", double.Ieee754Remainder, JetD.Ieee754Remainder, x, y);

        // Ternary members take their third operand from a rotated copy.
        double[] z = [.. y.Skip(7), .. y.Take(7)];
        for (var i = 0; i < x.Length; i++)
        {
            double a = x[i], b = y[i], c = z[i];
            AssertSame("FusedMultiplyAdd", double.FusedMultiplyAdd(a, b, c), JetD.FusedMultiplyAdd(JetD.Variable(a, 0, 3), JetD.Variable(b, 1, 3), JetD.Variable(c, 2, 3)).Value, a, b, c);
            AssertSame("Lerp", double.Lerp(a, b, c), JetD.Lerp(JetD.Variable(a, 0, 3), JetD.Variable(b, 1, 3), JetD.Constant(c)).Value, a, b, c);
            if (a <= b || double.IsNaN(a) || double.IsNaN(b))
            {
                double expected;
                try { expected = double.Clamp(c, a, b); }
                catch (Exception) { continue; }
                AssertSame("Clamp", expected, JetD.Clamp(JetD.Variable(c, 0, 1), JetD.Constant(a), JetD.Constant(b)).Value, c, a, b);
            }
            Count++;
        }
        Assert.IsGreaterThan(230_000, Count);   // 20 binary members and 1 ternary pass over 11,176 pairs
    }

    private static void AssertSame(string name, double expected, double actual, params double[] arguments) =>
        Assert.IsTrue(Same(expected, actual), $"{name}({string.Join(", ", arguments.Select(a => a.ToString("R")))}): double {expected:R}, jet {actual:R}");

    [TestMethod]
    public void ClassificationRelationalAndIntegerResultsMatchDoubleOnValuesOnly()
    {
        var gen = new Gen(Seed + 2);
        var xs = Inputs(gen);
        for (var i = 0; i < xs.Length; i++)
        {
            var x = xs[i];
            var y = xs[(i * 7 + 3) % xs.Length];
            foreach (var jet in new[] { JetD.Constant(x), JetD.Variable(x, 0, 2), JetD.Variable(x, 1, 2) })
            {
                Assert.AreEqual(double.IsNaN(x), JetD.IsNaN(jet), $"IsNaN({x:R})");
                Assert.AreEqual(double.IsFinite(x), JetD.IsFinite(jet), $"IsFinite({x:R})");
                Assert.AreEqual(double.IsInfinity(x), JetD.IsInfinity(jet), $"IsInfinity({x:R})");
                Assert.AreEqual(double.IsPositiveInfinity(x), JetD.IsPositiveInfinity(jet), $"IsPositiveInfinity({x:R})");
                Assert.AreEqual(double.IsNegativeInfinity(x), JetD.IsNegativeInfinity(jet), $"IsNegativeInfinity({x:R})");
                Assert.AreEqual(double.IsNegative(x), JetD.IsNegative(jet), $"IsNegative({x:R})");
                Assert.AreEqual(double.IsPositive(x), JetD.IsPositive(jet), $"IsPositive({x:R})");
                Assert.AreEqual(IsZeroOf(x), JetD.IsZero(jet), $"IsZero({x:R})");
                Assert.AreEqual(double.IsNormal(x), JetD.IsNormal(jet), $"IsNormal({x:R})");
                Assert.AreEqual(double.IsSubnormal(x), JetD.IsSubnormal(jet), $"IsSubnormal({x:R})");
                Assert.AreEqual(double.IsInteger(x), JetD.IsInteger(jet), $"IsInteger({x:R})");
                Assert.AreEqual(double.IsEvenInteger(x), JetD.IsEvenInteger(jet), $"IsEvenInteger({x:R})");
                Assert.AreEqual(double.IsOddInteger(x), JetD.IsOddInteger(jet), $"IsOddInteger({x:R})");
                Assert.AreEqual(double.IsRealNumber(x), JetD.IsRealNumber(jet), $"IsRealNumber({x:R})");
                Assert.AreEqual(IsCanonicalOf(x), JetD.IsCanonical(jet), $"IsCanonical({x:R})");
                Assert.IsFalse(JetD.IsComplexNumber(jet) || JetD.IsImaginaryNumber(jet));
                Assert.AreEqual(x < y, jet < JetD.Constant(y), $"{x:R} < {y:R}");
                Assert.AreEqual(x <= y, jet <= JetD.Variable(y, 0, 2), $"{x:R} <= {y:R}");
                Assert.AreEqual(x > y, jet > JetD.Constant(y), $"{x:R} > {y:R}");
                Assert.AreEqual(x >= y, jet >= JetD.Variable(y, 1, 2), $"{x:R} >= {y:R}");
                Assert.AreEqual(x == y, jet == JetD.Variable(y, 0, 2), $"{x:R} == {y:R}");
                Assert.AreEqual(x != y, jet != JetD.Constant(y), $"{x:R} != {y:R}");
                if (!double.IsNaN(x) && !double.IsNaN(y)) Assert.AreEqual(Math.Sign(x.CompareTo(y)), Math.Sign(jet.CompareTo(JetD.Constant(y))), $"CompareTo({x:R}, {y:R})");
                if (double.IsFinite(x) && x != 0)
                    Assert.AreEqual(double.ILogB(x), JetD.ILogB(jet), $"ILogB({x:R})");
                if (!double.IsNaN(x)) Assert.AreEqual(double.Sign(x), JetD.Sign(jet), $"Sign({x:R})");
            }
        }
    }

    [TestMethod]
    public void ThrowingMembersThrowLikeDouble()
    {
        Assert.Throws<ArithmeticException>(() => double.Sign(double.NaN));
        Assert.Throws<ArithmeticException>(() => JetD.Sign(JetD.NaN));
        Assert.Throws<ArgumentException>(() => double.Clamp(1.0, 2.0, 1.0));
        Assert.Throws<ArgumentException>(() => JetD.Clamp(JetD.One, JetD.Constant(2.0), JetD.One));
    }

    private static bool IsZeroOf<TNum>(TNum x) where TNum : INumberBase<TNum> => TNum.IsZero(x);

    private static bool IsCanonicalOf<TNum>(TNum x) where TNum : INumberBase<TNum> => TNum.IsCanonical(x);

    private static TTarget Checked<TTarget, TSource>(TSource v) where TTarget : INumberBase<TTarget> where TSource : INumberBase<TSource> => TTarget.CreateChecked(v);

    private static TTarget Truncating<TTarget, TSource>(TSource v) where TTarget : INumberBase<TTarget> where TSource : INumberBase<TSource> => TTarget.CreateTruncating(v);

    private static TTarget Saturating<TTarget, TSource>(TSource v) where TTarget : INumberBase<TTarget> where TSource : INumberBase<TSource> => TTarget.CreateSaturating(v);

    [TestMethod]
    public void ConstantsAndIdentitiesMatchDouble()
    {
        Assert.AreEqual(double.E, JetD.E.Value);
        Assert.AreEqual(double.Pi, JetD.Pi.Value);
        Assert.AreEqual(double.Tau, JetD.Tau.Value);
        Assert.AreEqual(double.Epsilon, JetD.Epsilon.Value);
        Assert.IsTrue(double.IsNaN(JetD.NaN.Value));
        Assert.IsTrue(double.IsPositiveInfinity(JetD.PositiveInfinity.Value) && double.IsNegativeInfinity(JetD.NegativeInfinity.Value));
        Assert.IsTrue(double.IsNegative(JetD.NegativeZero.Value) && JetD.NegativeZero.Value == 0.0);
        Assert.AreEqual(1.0, JetD.One.Value);
        Assert.AreEqual(-1.0, JetD.NegativeOne.Value);
        Assert.AreEqual(0.0, JetD.Zero.Value);
        Assert.AreEqual(2, JetD.Radix);
        Assert.IsTrue(JetD.Zero.IsConstant && JetD.One.IsConstant && JetD.Pi.IsConstant, "constants have no gradient");
        Assert.AreEqual(JetD.Zero, JetD.AdditiveIdentity);
        Assert.AreEqual(JetD.One, JetD.MultiplicativeIdentity);
        var x = JetD.Variable(3.0, 0, 1);
        Assert.AreEqual(x, x + JetD.AdditiveIdentity);
        Assert.AreEqual(x, x * JetD.MultiplicativeIdentity);
    }

    [TestMethod]
    public void ParsingFormattingAndConversionGoThroughTheValue()
    {
        Assert.AreEqual(JetD.Constant(2.5), JetD.Parse("2.5", System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsTrue(JetD.TryParse("-1e3", System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed.Value == -1000.0 && parsed.IsConstant);
        Assert.IsFalse(JetD.TryParse("not a number", System.Globalization.CultureInfo.InvariantCulture, out var failed));
        Assert.IsTrue(failed.IsConstant && failed.Value == 0.0);
        Assert.IsTrue(JetD.TryParse("12".AsSpan(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var span) && span.Value == 12.0);
        Assert.IsTrue(JetD.TryParse("7"u8, System.Globalization.CultureInfo.InvariantCulture, out var utf8) && utf8.Value == 7.0);

        // Generic conversions: a converted number is a constant; converting out drops the gradient.
        Assert.AreEqual(JetD.Constant(3.0), Checked<JetD, int>(3));
        Assert.AreEqual(JetD.Constant(3.0), Truncating<JetD, long>(3L));
        Assert.AreEqual(JetD.Constant(2.5), Saturating<JetD, float>(2.5f));
        Assert.AreEqual(2.5, Checked<double, JetD>(JetD.Variable(2.5, 0, 1)));
        Assert.AreEqual(2, Truncating<int, JetD>(JetD.Variable(2.9, 0, 1)));
        Assert.AreEqual(int.MaxValue, Saturating<int, JetD>(JetD.Constant(1e30)));
        Assert.Throws<OverflowException>(() => Checked<int, JetD>(JetD.Constant(1e30)));
        var jet = JetD.Variable(1.5, 0, 2);
        Assert.AreEqual(jet, Checked<JetD, JetD>(jet), "converting a jet to its own type keeps the gradient");
        Assert.AreEqual(JetF.Constant(1.5f), Checked<JetF, double>(1.5));

        var destination = new char[64];
        Assert.IsTrue(JetD.Variable(2.0, 1, 2).TryFormat(destination, out var written, default, System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual("(2; 0, 1)", new string(destination, 0, written));
        Assert.IsFalse(JetD.Variable(2.0, 1, 2).TryFormat(new char[3], out written, default, null));
        Assert.AreEqual(0, written);
        var bytes = new byte[64];
        Assert.IsTrue(JetD.Variable(2.0, 0, 2).TryFormat(bytes, out written, default, System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual("(2; 1, 0)", System.Text.Encoding.UTF8.GetString(bytes, 0, written));
    }

    [TestMethod]
    public void BinaryLayoutQueriesDelegateToTheValue()
    {
        IFloatingPoint<double> d = 6.5;
        var x = JetD.Variable(6.5, 0, 1);
        Assert.AreEqual(d.GetExponentByteCount(), x.GetExponentByteCount());
        Assert.AreEqual(d.GetExponentShortestBitLength(), x.GetExponentShortestBitLength());
        Assert.AreEqual(d.GetSignificandBitLength(), x.GetSignificandBitLength());
        Assert.AreEqual(d.GetSignificandByteCount(), x.GetSignificandByteCount());
        Span<byte> expected = stackalloc byte[16], actual = stackalloc byte[16];
        int ne, na;
        Assert.IsTrue(d.TryWriteSignificandLittleEndian(expected, out ne) && x.TryWriteSignificandLittleEndian(actual, out na) && ne == na && expected[..ne].SequenceEqual(actual[..na]));
        Assert.IsTrue(d.TryWriteSignificandBigEndian(expected, out ne) && x.TryWriteSignificandBigEndian(actual, out na) && ne == na && expected[..ne].SequenceEqual(actual[..na]));
        Assert.IsTrue(d.TryWriteExponentLittleEndian(expected, out ne) && x.TryWriteExponentLittleEndian(actual, out na) && ne == na && expected[..ne].SequenceEqual(actual[..na]));
        Assert.IsTrue(d.TryWriteExponentBigEndian(expected, out ne) && x.TryWriteExponentBigEndian(actual, out na) && ne == na && expected[..ne].SequenceEqual(actual[..na]));
    }
}
