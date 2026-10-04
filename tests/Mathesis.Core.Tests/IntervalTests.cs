using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Core.Tests;

[TestClass]
public class IntervalTests
{
    private const int Seed = 31415926;

    private static Interval<double> I(double lo, double hi) => new(lo, hi);

    // Every unary function must enclose its value at every point of the argument interval where it is defined.
    private static readonly (string Name, Func<Interval<double>, Interval<double>> Interval, Func<double, double> Point, double Low, double High)[] Unary =
    [
        ("sqrt", Interval<double>.Sqrt, Math.Sqrt, -2, 20),
        ("exp", Interval<double>.Exp, Math.Exp, -20, 20),
        ("ln", Interval<double>.Ln, Math.Log, -2, 50),
        ("sin", Interval<double>.Sin, Math.Sin, -30, 30),
        ("cos", Interval<double>.Cos, Math.Cos, -30, 30),
        ("tan", Interval<double>.Tan, Math.Tan, -10, 10),
        ("asin", Interval<double>.Asin, Math.Asin, -1.5, 1.5),
        ("acos", Interval<double>.Acos, Math.Acos, -1.5, 1.5),
        ("atan", Interval<double>.Atan, Math.Atan, -50, 50),
        ("sinh", Interval<double>.Sinh, Math.Sinh, -10, 10),
        ("cosh", Interval<double>.Cosh, Math.Cosh, -10, 10),
        ("tanh", Interval<double>.Tanh, Math.Tanh, -10, 10),
        ("asinh", Interval<double>.Asinh, Math.Asinh, -50, 50),
        ("acosh", Interval<double>.Acosh, Math.Acosh, 0, 50),
        ("atanh", Interval<double>.Atanh, Math.Atanh, -1.5, 1.5),
        ("abs", Interval<double>.Abs, Math.Abs, -9, 9),
        ("floor", Interval<double>.Floor, Math.Floor, -9, 9),
        ("ceil", Interval<double>.Ceiling, Math.Ceiling, -9, 9),
        ("square", Interval<double>.Square, x => x * x, -9, 9),
        ("cube", x => Interval<double>.Pow(x, 3), x => x * x * x, -9, 9),
        ("reciprocal", x => Interval<double>.Point(1) / x, x => 1 / x, -5, 5),
    ];

    [TestMethod]
    public void UnaryFunctionsEncloseTheirValues()
    {
        var gen = new Gen(Seed);
        foreach (var (name, f, point, low, high) in Unary)
        {
            for (var trial = 0; trial < 2000; trial++)
            {
                var a = gen.Uniform(low, high);
                var b = a + gen.Random.NextDouble() * (gen.Random.Next(3) == 0 ? 0.01 : 4);
                var arg = I(a, b);
                var result = f(arg);
                for (var k = 0; k < 8; k++)
                {
                    var x = k == 0 ? a : k == 1 ? b : a + gen.Random.NextDouble() * (b - a);
                    var y = point(x);
                    if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                    Assert.IsTrue(result.Contains(y), $"{name}([{a}, {b}]) = {result} does not contain {name}({x}) = {y}");
                }
            }
        }
    }

    [TestMethod]
    public void BinaryOperationsEncloseTheirValues()
    {
        var gen = new Gen(Seed + 1);
        for (var trial = 0; trial < 20000; trial++)
        {
            var a = gen.Uniform(-8, 8);
            var b = a + gen.Random.NextDouble() * 3;
            var c = gen.Uniform(-8, 8);
            var d = c + gen.Random.NextDouble() * 3;
            var x = a + gen.Random.NextDouble() * (b - a);
            var y = c + gen.Random.NextDouble() * (d - c);
            var p = I(a, b);
            var q = I(c, d);
            Assert.IsTrue((p + q).Contains(x + y));
            Assert.IsTrue((p - q).Contains(x - y));
            Assert.IsTrue((p * q).Contains(x * y));
            if (y != 0) Assert.IsTrue((p / q).Contains(x / y), $"{p} / {q} = {p / q} must contain {x / y}");
            Assert.IsTrue(Interval<double>.Min(p, q).Contains(Math.Min(x, y)));
            Assert.IsTrue(Interval<double>.Max(p, q).Contains(Math.Max(x, y)));
            if (x > 0) Assert.IsTrue(Interval<double>.Pow(p, q).Contains(Math.Pow(x, y)), $"{p}^{q}");
        }
    }

    [TestMethod]
    public void ArithmeticIsRoundedOutward()
    {
        // 1/3 is not representable: the quotient must be strictly wider than the point.
        var third = Interval<double>.Point(1) / Interval<double>.Point(3);
        Assert.IsTrue(third.Lower < 1.0 / 3 && third.Upper > 1.0 / 3);
        Assert.IsTrue(third.Width > 0);

        // 0.1 + 0.2 is not 0.3 in doubles, but the enclosure contains the decimal values.
        var sum = Interval<double>.Point(0.1) + Interval<double>.Point(0.2);
        Assert.IsTrue(sum.Contains(0.3));
    }

    [TestMethod]
    public void EmptyAndEntireBehave()
    {
        var empty = Interval<double>.Empty;
        Assert.IsTrue(empty.IsEmpty);
        Assert.IsTrue(double.IsNaN(empty.Lower));
        Assert.IsTrue((empty + I(1, 2)).IsEmpty);
        Assert.IsTrue((I(1, 2) * empty).IsEmpty);
        Assert.IsTrue(Interval<double>.Sqrt(I(-3, -1)).IsEmpty);
        Assert.IsTrue(Interval<double>.Ln(I(-3, 0)).IsEmpty);
        Assert.IsTrue(Interval<double>.Hull(empty, I(1, 2)) == I(1, 2));
        Assert.IsTrue(Interval<double>.Intersect(I(1, 2), I(3, 4)).IsEmpty);
        Assert.AreEqual(I(2, 3), Interval<double>.Intersect(I(1, 3), I(2, 4)));
        Assert.IsTrue(I(1, 5).Contains(empty));
        Assert.IsFalse(empty.Contains(1.0));
        Assert.IsTrue(Interval<double>.Entire.IsEntire);
        Assert.IsFalse(Interval<double>.Entire.IsBounded);
        Assert.AreEqual("∅", empty.ToString());
        Assert.AreEqual(empty, Interval<double>.Empty);
        Assert.AreNotEqual(empty, I(0, 0));
    }

    [TestMethod]
    public void DivisionByIntervalsContainingZero()
    {
        Assert.IsTrue((I(1, 2) / I(-1, 1)).IsEntire);
        Assert.IsTrue((I(1, 2) / I(0, 0)).IsEmpty);

        // [0, 1] excludes the point 0 only: 1/(0, 1] = [1, ∞).
        var q = I(1, 1) / I(0, 1);
        Assert.IsTrue(q.Lower <= 1 && double.IsPositiveInfinity(q.Upper));
        var r = I(1, 1) / I(-1, 0);
        Assert.IsTrue(double.IsNegativeInfinity(r.Lower) && r.Upper >= -1);
    }

    [TestMethod]
    public void PeriodicFunctionsFindTheirExtrema()
    {
        Assert.IsTrue(Interval<double>.Sin(I(1, 2)).Contains(1.0));
        Assert.IsTrue(Interval<double>.Sin(I(4, 5.5)).Contains(-1.0));
        Assert.IsTrue(Interval<double>.Cos(I(-1, 1)).Contains(1.0));
        Assert.IsTrue(Interval<double>.Cos(I(3, 3.3)).Contains(-1.0));
        var wide = Interval<double>.Sin(I(0, 7));
        Assert.IsTrue(wide.Lower <= -1 && wide.Upper >= 1);
        var small = Interval<double>.Sin(I(0.1, 0.2));
        Assert.IsTrue(small.Upper < 0.21 && small.Lower > 0.09);
        Assert.IsTrue(Interval<double>.Tan(I(1, 2)).IsEntire);
        var t = Interval<double>.Tan(I(0.1, 0.2));
        Assert.IsTrue(t.Upper < 0.21 && t.Lower > 0.09);
        Assert.IsTrue(Interval<double>.Sin(I(0, 1e12)).Lower <= -1);
    }

    [TestMethod]
    public void IntegerAndRealPowers()
    {
        Assert.IsTrue(Interval<double>.Pow(I(-2, 3), 2).Contains(0.0));
        Assert.AreEqual(0.0, Interval<double>.Pow(I(-2, 3), 2).Lower);
        Assert.IsTrue(Interval<double>.Pow(I(-2, 3), 2).Upper >= 9);
        Assert.IsTrue(Interval<double>.Pow(I(-2, 3), 3).Lower <= -8);
        Assert.IsTrue(Interval<double>.Pow(I(2, 3), -2).Contains(1.0 / 6));
        Assert.AreEqual(Interval<double>.Point(1), Interval<double>.Pow(I(-5, 5), 0));
        Assert.IsTrue(Interval<double>.Root(I(-8, 27), 3).Contains(-2.0));
        Assert.IsTrue(Interval<double>.Root(I(-8, 27), 3).Contains(3.0));
        Assert.IsTrue(Interval<double>.Root(I(-8, -1), 2).IsEmpty);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Interval<double>.Root(I(1, 2), 0));

        // A negative base with a non-integer exponent interval has no real power; with integers inside it, anything.
        Assert.IsTrue(Interval<double>.Pow(I(-3, -2), I(0.5, 0.6)).IsEmpty);
        Assert.IsTrue(Interval<double>.Pow(I(-3, -2), I(1, 3)).IsEntire);
        Assert.IsTrue(Interval<double>.Pow(I(0, 2), I(0.5, 2)).Contains(0.0));
    }

    [TestMethod]
    public void ConstructionValidatesAndWorksForSingle()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Interval<double>(2, 1));
        Assert.ThrowsExactly<ArgumentException>(() => new Interval<double>(double.NaN, 1));
        Assert.ThrowsExactly<ArgumentException>(() => Interval<double>.Point(double.NaN));
        var f = new Interval<float>(1f, 2f) + new Interval<float>(0.5f, 0.5f);
        Assert.IsTrue(f.Lower <= 1.5f && f.Upper >= 2.5f);
        var a = Interval<double>.Around(1.0, 2);
        Assert.IsTrue(a.Lower < 1 && a.Upper > 1);
        Assert.AreEqual(2.0, I(1, 3).Midpoint);
        Assert.AreEqual(3.0, I(-3, 2).Magnitude);
        Assert.AreEqual(0.0, I(-3, 2).Mignitude);
        Assert.AreEqual(1.0, I(1, 2).Mignitude);
        Assert.IsTrue(I(1, 2).Overlaps(I(2, 3)));
        Assert.IsFalse(I(1, 2).Overlaps(I(2.1, 3)));
        Assert.AreEqual(I(-2, -1), -I(1, 2));
        Assert.IsTrue(I(1, 2) == (+I(1, 2)));
        Assert.IsTrue(I(1, 2) != I(1, 3));
        Assert.AreEqual(I(1, 2).GetHashCode(), I(1, 2).GetHashCode());
        Assert.IsFalse(I(1, 2).Equals("x"));
        Assert.AreEqual("[1, 2]", I(1, 2).ToString());
        Assert.AreEqual(Interval<double>.Point(3), (Interval<double>)3.0);
    }

    [TestMethod]
    public void RoundingFunctionsAreExactAndMonotone()
    {
        Assert.AreEqual(I(1, 3), Interval<double>.Floor(I(1.5, 3.5)));
        Assert.AreEqual(I(2, 4), Interval<double>.Ceiling(I(1.5, 3.5)));
        Assert.AreEqual(I(2, 4), Interval<double>.Round(I(1.5, 3.5)));
        Assert.AreEqual(I(-3, -1), Interval<double>.Round(I(-2.5, -0.5)));
        Assert.AreEqual(I(-1, 1), Interval<double>.Sign(I(-3, 2)));
        Assert.AreEqual(I(1, 1), Interval<double>.Sign(I(0.5, 2)));
    }
}
