using Mathesis.Numbers;
using System.Globalization;
using System.Numerics;

namespace Mathesis.Core.Tests;

/// <summary>
/// One test per row of the "Jet semantics" table in docs/design/04-type-system.md (ADR-19), plus the four semantics questions of the Technesis prototype report.
/// </summary>
[TestClass]
public class JetSemanticsTableTests
{
    private static JetD V(double value, int index, int dimension) => JetD.Variable(value, index, dimension);

    private static JetD C(double value) => JetD.Constant(value);

    private static void AssertGradient(JetD jet, params double[] expected)
    {
        Assert.AreEqual(expected.Length, jet.Dimension, $"dimension of {jet}");
        for (var i = 0; i < expected.Length; i++)
            Assert.IsTrue(expected[i].Equals(jet.Gradient[i]) || Math.Abs(expected[i] - jet.Gradient[i]) <= 1e-15 * Math.Abs(expected[i]), $"{jet}: lane {i} is {jet.Gradient[i]:R}, expected {expected[i]:R}");
    }

    // Row: + − × ÷ and unary minus.
    [TestMethod]
    public void ArithmeticFollowsTheSumProductAndQuotientRules()
    {
        JetD x = V(3.0, 0, 2), y = V(4.0, 1, 2);
        AssertGradient(x + y, 1, 1);
        AssertGradient(x - y, 1, -1);
        AssertGradient(x * y, 4, 3);
        AssertGradient(x / y, 0.25, -3.0 / 16.0);
        AssertGradient(-x, -1, 0);
        AssertGradient(C(5.0) - x, -1, 0);
        AssertGradient(x - C(5.0), 1, 0);
        AssertGradient(C(6.0) / x, -6.0 / 9.0, 0);
        AssertGradient(x / C(6.0), 1.0 / 6.0, 0);
        AssertGradient(x % y, 1, -Math.Truncate(3.0 / 4.0));
        AssertGradient(V(7.5, 0, 2) % y, 1, -1);
        Assert.AreEqual(7.0, (x + y).Value);
        var z = x;
        z++;
        AssertGradient(z, 1, 0);
        Assert.AreEqual(4.0, z.Value);
        z--;
        Assert.AreEqual(3.0, z.Value);
        AssertGradient(+x, 1, 0);
    }

    // Row: sqrt, cbrt, rootn, pow, exp, log, trig and hyperbolic functions: the chain-rule result, even where it is infinite or undefined.
    [TestMethod]
    public void FunctionsReturnTheChainRuleResultAtSingularPoints()
    {
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Sqrt(V(0.0, 0, 1)).Gradient[0]), "sqrt at 0");
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Cbrt(V(0.0, 0, 1)).Gradient[0]), "cbrt at 0");
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Log(V(0.0, 0, 1)).Gradient[0]), "log at 0");
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Asin(V(1.0, 0, 1)).Gradient[0]), "asin at 1");
        Assert.IsTrue(double.IsNegativeInfinity(JetD.Acos(V(1.0, 0, 1)).Gradient[0]), "acos at 1");
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Atanh(V(1.0, 0, 1)).Gradient[0]), "atanh at 1");
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Acosh(V(1.0, 0, 1)).Gradient[0]), "acosh at 1");
        Assert.IsTrue(double.IsPositiveInfinity(JetD.Pow(V(0.0, 0, 1), C(0.5)).Gradient[0]), "pow(x, 0.5) at 0");
        Assert.IsTrue(double.IsNaN(JetD.Hypot(V(0.0, 0, 2), V(0.0, 1, 2)).Gradient[0]), "hypot at the origin is 0/0");
        Assert.IsTrue(double.IsNaN(JetD.Log(V(-1.0, 0, 1)).Value), "log of a negative number is NaN in the value");
        // ‖x‖ at the origin: the square root's gradient is +∞ and the radicand's is 0, and 0 × ∞ is NaN (a finite derivative needs a branch).
        var norm = JetD.Sqrt(V(0.0, 0, 2) * V(0.0, 0, 2) + V(0.0, 1, 2) * V(0.0, 1, 2));
        Assert.IsTrue(double.IsNaN(norm.Gradient[0]) && double.IsNaN(norm.Gradient[1]), "‖x‖ at the origin has a NaN gradient");
        // A constant 0 gives a constant 0, never a gradient.
        Assert.IsTrue(JetD.Sqrt(C(0.0)).IsConstant && JetD.Sqrt(C(0.0)).Value == 0.0);
        // The value is finite although the gradient is not: the predicates look at the value, IsGradientFinite at the gradient.
        var root = JetD.Sqrt(V(0.0, 0, 1));
        Assert.IsTrue(JetD.IsFinite(root) && !root.IsGradientFinite);
        Assert.IsTrue(C(2.0).IsGradientFinite && V(2.0, 0, 3).IsGradientFinite);

        // Ordinary points: spot values of the remaining families.
        AssertGradient(JetD.RootN(V(8.0, 0, 1), 3), 1.0 / 12.0);
        AssertGradient(JetD.Pow(V(2.0, 0, 2), V(3.0, 1, 2)), 12.0, 8.0 * Math.Log(2.0));
        AssertGradient(JetD.Pow(C(-2.0), V(3.0, 0, 1)), double.NaN);   // d/db (−2)^b = (−2)^b·ln(−2) is NaN, as IEEE arithmetic gives
        AssertGradient(JetD.Pow(V(-2.0, 0, 1), C(3.0)), 12.0);
        Assert.IsTrue(JetD.Pow(V(0.0, 0, 1), C(0.0)).IsConstant && JetD.Pow(V(0.0, 0, 1), C(0.0)).Value == 1.0, "x⁰ is the constant 1, also at x = 0");
        AssertGradient(JetD.Hypot(V(3.0, 0, 2), V(4.0, 1, 2)), 0.6, 0.8);
        AssertGradient(JetD.FusedMultiplyAdd(V(2.0, 0, 3), V(3.0, 1, 3), V(5.0, 2, 3)), 3, 2, 1);
        AssertGradient(JetD.Lerp(V(1.0, 0, 3), V(5.0, 1, 3), V(0.25, 2, 3)), 0.75, 0.25, 4);
        Assert.AreEqual(2.0, JetD.Lerp(V(1.0, 0, 3), V(5.0, 1, 3), V(0.25, 2, 3)).Value);
        AssertGradient(JetD.Log(V(8.0, 0, 1), C(2.0)), 1.0 / (8.0 * Math.Log(2.0)));
        var (sin, cos) = JetD.SinCos(V(0.5, 0, 1));
        AssertGradient(sin, Math.Cos(0.5));
        AssertGradient(cos, -Math.Sin(0.5));
        var (sinPi, cosPi) = JetD.SinCosPi(V(0.25, 0, 1));
        AssertGradient(sinPi, Math.PI * Math.Cos(Math.PI * 0.25));
        AssertGradient(cosPi, -Math.PI * Math.Sin(Math.PI * 0.25));
    }

    // Row: Abs takes g at a = 0 (decision of 2026-10-05: at both +0 and −0, a refinement of Dual<T>, which negates at −0).
    [TestMethod]
    public void AbsTakesTheGradientAtBothZerosAndNegatesBelow()
    {
        AssertGradient(JetD.Abs(V(2.0, 0, 1)), 1);
        AssertGradient(JetD.Abs(V(-2.0, 0, 1)), -1);
        AssertGradient(JetD.Abs(V(0.0, 0, 1)), 1);
        AssertGradient(JetD.Abs(V(-0.0, 0, 1)), 1);
        Assert.AreEqual(2.0, JetD.Abs(V(-2.0, 0, 1)).Value);
        Assert.IsTrue(JetD.Abs(C(-3.0)).IsConstant);
        Assert.IsFalse(double.IsNegative(JetD.Abs(V(-0.0, 0, 1)).Value), "|−0| is +0");
    }

    // Row: Min, Max, MinMagnitude, MaxMagnitude, Clamp: the gradient of the selected operand; ties select the first.
    [TestMethod]
    public void SelectionFunctionsTakeTheSelectedOperandsGradientAndTiesTakeTheFirst()
    {
        JetD a = V(2.0, 0, 2), b = V(3.0, 1, 2);
        AssertGradient(JetD.Max(a, b), 0, 1);
        AssertGradient(JetD.Min(a, b), 1, 0);
        AssertGradient(JetD.MaxNumber(a, b), 0, 1);
        AssertGradient(JetD.MinNumber(a, b), 1, 0);
        AssertGradient(JetD.Max(a, V(2.0, 1, 2)), 1, 0);          // tie: the first operand
        AssertGradient(JetD.Min(a, V(2.0, 1, 2)), 1, 0);
        AssertGradient(JetD.Max(V(2.0, 1, 2), a), 0, 1);
        AssertGradient(JetD.MaxMagnitude(a, V(-3.0, 1, 2)), 0, 1);
        AssertGradient(JetD.MinMagnitude(a, V(-3.0, 1, 2)), 1, 0);
        AssertGradient(JetD.MaxMagnitude(V(-2.0, 0, 2), V(2.0, 1, 2)), 0, 1);   // equal magnitudes: the positive one, as double does
        AssertGradient(JetD.MinMagnitude(V(-2.0, 0, 2), V(2.0, 1, 2)), 1, 0);
        AssertGradient(JetD.MaxMagnitudeNumber(a, V(-3.0, 1, 2)), 0, 1);
        AssertGradient(JetD.MinMagnitudeNumber(a, V(-3.0, 1, 2)), 1, 0);
        // A NaN operand: Max propagates it with its own gradient; MaxNumber skips it.
        AssertGradient(JetD.MaxNumber(V(double.NaN, 0, 2), b), 0, 1);
        Assert.IsTrue(double.IsNaN(JetD.Max(V(double.NaN, 0, 2), b).Value));
        // Clamp: the value's gradient inside, the bound's when it clamps (a constant bound makes the result a constant).
        var low = V(0.0, 0, 3);
        var high = V(1.0, 1, 3);
        AssertGradient(JetD.Clamp(V(0.5, 2, 3), low, high), 0, 0, 1);
        AssertGradient(JetD.Clamp(V(-4.0, 2, 3), low, high), 1, 0, 0);
        AssertGradient(JetD.Clamp(V(4.0, 2, 3), low, high), 0, 1, 0);
        Assert.IsTrue(JetD.Clamp(V(4.0, 0, 1), C(0.0), C(1.0)).IsConstant);
        Assert.AreEqual(1.0, JetD.Clamp(V(4.0, 0, 1), C(0.0), C(1.0)).Value);
    }

    // Row: Floor, Ceiling, Round, Truncate: zero gradient (piecewise constant); the result is a constant.
    [TestMethod]
    public void RoundingFunctionsAreConstants()
    {
        foreach (var x in new[] { -2.5, -0.4, 0.0, 0.5, 1.0, 2.5, 1e300 })
        {
            Assert.IsTrue(JetD.Floor(V(x, 0, 2)).IsConstant && JetD.Floor(V(x, 0, 2)).Value == Math.Floor(x));
            Assert.IsTrue(JetD.Ceiling(V(x, 0, 2)).IsConstant && JetD.Ceiling(V(x, 0, 2)).Value == Math.Ceiling(x));
            Assert.IsTrue(JetD.Truncate(V(x, 0, 2)).IsConstant && JetD.Truncate(V(x, 0, 2)).Value == Math.Truncate(x));
            Assert.IsTrue(JetD.Round(V(x, 0, 2)).IsConstant && JetD.Round(V(x, 0, 2), 1).IsConstant && JetD.Round(V(x, 0, 2), MidpointRounding.AwayFromZero).IsConstant);
        }
        // A constant combined with a variable keeps the variable's gradient, so floor(x) + x differentiates to 1.
        AssertGradient(JetD.Floor(V(2.5, 0, 1)) + V(2.5, 0, 1), 1);
    }

    // Row: CopySign.
    [TestMethod]
    public void CopySignTakesTheMagnitudeOperandsGradientNegatedWhenTheSignFlips()
    {
        AssertGradient(JetD.CopySign(V(2.0, 0, 2), V(-1.0, 1, 2)), -1, 0);
        AssertGradient(JetD.CopySign(V(2.0, 0, 2), V(1.0, 1, 2)), 1, 0);
        AssertGradient(JetD.CopySign(V(-2.0, 0, 2), V(1.0, 1, 2)), -1, 0);
        AssertGradient(JetD.CopySign(V(-2.0, 0, 2), V(-1.0, 1, 2)), 1, 0);
        Assert.AreEqual(-2.0, JetD.CopySign(V(2.0, 0, 2), V(-1.0, 1, 2)).Value);
        Assert.IsTrue(JetD.CopySign(C(2.0), V(-1.0, 0, 1)).IsConstant, "the sign operand contributes no gradient");
    }

    // Row: ScaleB, ILogB, BitIncrement, BitDecrement, ReciprocalEstimate, ReciprocalSqrtEstimate, Ieee754Remainder.
    [TestMethod]
    public void ExponentManipulationEstimatesAndRemainderFollowTheirRows()
    {
        AssertGradient(JetD.ScaleB(V(3.0, 0, 1), 4), 16);
        AssertGradient(JetD.ScaleB(V(3.0, 0, 1), -2), 0.25);
        Assert.AreEqual(48.0, JetD.ScaleB(V(3.0, 0, 1), 4).Value);
        Assert.AreEqual(1, JetD.ILogB(V(3.0, 0, 1)));
        AssertGradient(JetD.BitIncrement(V(1.0, 0, 2)), 1, 0);
        AssertGradient(JetD.BitDecrement(V(1.0, 1, 2)), 0, 1);
        Assert.AreEqual(Math.BitIncrement(1.0), JetD.BitIncrement(V(1.0, 0, 2)).Value);
        AssertGradient(JetD.ReciprocalEstimate(V(4.0, 0, 1)), -1.0 / 16.0);
        AssertGradient(JetD.ReciprocalSqrtEstimate(V(4.0, 0, 1)), -0.5 * Math.Pow(4.0, -1.5));
        // IEEE remainder x − y·round(x/y): 7 rem 2 = −1 (7/2 = 3.5 rounds to 4), gradient (1, −4).
        var remainder = JetD.Ieee754Remainder(V(7.0, 0, 2), V(2.0, 1, 2));
        Assert.AreEqual(-1.0, remainder.Value);
        AssertGradient(remainder, 1, -4);
    }

    // Row: < <= > >= == != compare values only (ADR-19), against Equals/GetHashCode, which are structural.
    [TestMethod]
    public void RelationalOperatorsLookAtValuesAndEqualsIsStructural()
    {
        JetD a = V(2.0, 0, 2), b = V(2.0, 1, 2);
        Assert.IsTrue(a == b && !(a != b) && a <= b && a >= b && !(a < b) && !(a > b), "same value, different gradient: relational operators agree on equality");
        Assert.IsFalse(a.Equals(b), "Equals is structural");
        Assert.IsTrue(a.Equals(V(2.0, 0, 2)) && a.GetHashCode() == V(2.0, 0, 2).GetHashCode());
        Assert.IsFalse(a.Equals(C(2.0)), "a constant is not a variable with the same value");
        Assert.IsFalse(a.Equals((object?)null) || a.Equals("(2; 1, 0)"));
        Assert.IsTrue(a.Equals((object)V(2.0, 0, 2)));
        Assert.IsTrue(C(double.NaN).Equals(C(double.NaN)), "structural equality treats NaN as equal to itself");
        Assert.IsFalse(C(double.NaN) == C(double.NaN), "== is IEEE equality of the values");
        Assert.IsTrue(V(1.0, 0, 2) < V(2.0, 1, 2) && C(3.0) > V(2.0, 1, 2));
        Assert.IsFalse(C(double.NaN) < C(1.0) || C(double.NaN) >= C(1.0));
        Assert.AreEqual(0, a.CompareTo(b));
        Assert.AreEqual(-1, Math.Sign(a.CompareTo(C(3.0))));
        Assert.AreEqual(1, a.CompareTo((object?)null));
        Assert.AreEqual(0, a.CompareTo((object)b));
        Assert.Throws<ArgumentException>(() => a.CompareTo("two"));
        Assert.AreEqual(0.0.CompareTo(-0.0), C(0.0).CompareTo(C(-0.0)));
    }

    // Row: Constants. A structural zero is never multiplied (question 1 of the Technesis prototype report).
    [TestMethod]
    public void ConstantsHaveNoGradientAndNeverPollute()
    {
        var product = C(2.0) * V(double.PositiveInfinity, 0, 1);
        Assert.IsTrue(double.IsPositiveInfinity(product.Value));
        AssertGradient(product, 2);
        AssertGradient(V(double.PositiveInfinity, 0, 1) * C(2.0), 2);
        AssertGradient(V(double.PositiveInfinity, 0, 1) / C(2.0), 0.5);
        AssertGradient(C(2.0) + V(double.PositiveInfinity, 0, 1), 1);
        Assert.IsTrue(C(2.0).IsConstant && C(2.0).Dimension == 0 && C(2.0).Gradient.IsEmpty);
        Assert.IsTrue(default(JetD).IsConstant && default(JetD).Value == 0.0);
        foreach (var f in new Func<JetD, JetD>[] { JetD.Sin, JetD.Sqrt, JetD.Exp, JetD.Log, JetD.Atan, JetD.Tanh, JetD.Abs, JetD.Cbrt, x => -x, x => x * x, x => x / C(3.0), x => JetD.Pow(x, C(2.5)) })
            Assert.IsTrue(f(C(2.0)).IsConstant, "a function of a constant is a constant");
        Assert.IsTrue(JetD.Atan2(C(1.0), C(2.0)).IsConstant && JetD.Hypot(C(3.0), C(4.0)).IsConstant && JetD.Max(C(1.0), C(2.0)).IsConstant);
        JetD implicitConstant = 2.5;
        Assert.IsTrue(implicitConstant.IsConstant && implicitConstant.Value == 2.5);
    }

    // Row: Dimensions.
    [TestMethod]
    public void OperandsOfDifferentNonZeroDimensionAreRejectedAndConstantsCombineWithAny()
    {
        JetD two = V(1.0, 0, 2), three = V(1.0, 0, 3);
        Assert.Throws<ArgumentException>(() => two + three);
        Assert.Throws<ArgumentException>(() => two - three);
        Assert.Throws<ArgumentException>(() => two * three);
        Assert.Throws<ArgumentException>(() => two / three);
        Assert.Throws<ArgumentException>(() => two % three);
        Assert.Throws<ArgumentException>(() => JetD.Atan2(two, three));
        Assert.Throws<ArgumentException>(() => JetD.Pow(two, three));
        Assert.Throws<ArgumentException>(() => JetD.Hypot(two, three));
        Assert.AreEqual(3, (C(1.0) + three).Dimension);
        Assert.AreEqual(2, (two * C(5.0)).Dimension);
        Assert.AreEqual(2, JetD.Atan2(C(1.0), two).Dimension);
        Assert.AreEqual(Math.Max(2, 3), Math.Max((two + C(1.0)).Dimension, (three * C(2.0)).Dimension));
    }

    // Row: Text form.
    [TestMethod]
    public void TextFormIsValueSemicolonGradient()
    {
        Assert.AreEqual("(2.5)", C(2.5).ToString());
        Assert.AreEqual("(2; 1, 0)", V(2.0, 0, 2).ToString());
        Assert.AreEqual("(3; 0, 0, 1)", V(3.0, 2, 3).ToString());
        Assert.AreEqual("(NaN; 1)", V(double.NaN, 0, 1).ToString());
        var comma = new NumberFormatInfo { NumberDecimalSeparator = "," };
        Assert.AreEqual("(2,5; 1, 0)", (V(2.5, 0, 2)).ToString(null, comma));
        Assert.AreEqual("(2.50; 1.00)", V(2.5, 0, 1).ToString("F2", CultureInfo.InvariantCulture));
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.AreEqual("(2.5; 1)", V(2.5, 0, 1).ToString(), "ToString() uses the invariant culture");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    // Question 2 of the Technesis prototype report: == inside a generic Newton loop branches as double does.
    private static int NewtonIterations<TNum>(TNum start) where TNum : IFloatingPointIeee754<TNum>
    {
        var nine = TNum.CreateChecked(9);
        var x = start;
        for (var i = 0; i < 50; i++)
        {
            var f = x * x - nine;
            if (f == TNum.Zero) return i;
            x -= f / (x + x);
        }
        return 50;
    }

    [TestMethod]
    public void EqualityInAGenericNewtonLoopBranchesAsDoubleDoes()
    {
        var plain = NewtonIterations(4.0);
        Assert.AreEqual(plain, NewtonIterations(V(4.0, 0, 1)), "the jet stops on the same iteration as double");
        Assert.AreEqual(plain, NewtonIterations(C(4.0)));
        // A guard on zero takes the same branch: x == 0 ? 0 : 1/x at the variable 0 returns the constant 0, not (+∞; −∞).
        var x = V(0.0, 0, 1);
        var guarded = x == JetD.Zero ? JetD.Zero : JetD.One / x;
        Assert.IsTrue(guarded.IsConstant && guarded.Value == 0.0);
    }

    // The jets are usable as the number type of a generic algorithm.
    [TestMethod]
    public void AGenericAlgorithmDifferentiatesThroughAnIteration()
    {
        // Newton's iteration for √a converges quadratically; its derivative converges to 1/(2√a) (Griewank and Walther, chapter 15).
        var a = V(2.0, 0, 1);
        var x = C(1.0);
        for (var i = 0; i < 8; i++) x = (x + a / x) / C(2.0);
        Assert.AreEqual(Math.Sqrt(2.0), x.Value, 1e-15);
        Assert.AreEqual(1.0 / (2.0 * Math.Sqrt(2.0)), x.Gradient[0], 1e-14);
    }
}
