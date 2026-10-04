using Mathesis.Calculus;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class SeriesTests
{
    private static IEnumerable<(string Text, Expr F, double Center, int Order, bool Removable)> Cases() =>
        CalculusHelpers.Corpus("series.txt").Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            return (parts[0], Expr.Parse(parts[0]), double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), parts.Length > 3);
        });

    [TestMethod]
    public void TaylorPolynomialsApproximateTheFunctionWithTheRightOrder()
    {
        var failures = new List<string>();
        var x = CalculusHelpers.X;
        foreach (var (text, f, center, order, removable) in Cases())
        {
            var c = new Number(new BigRational((System.Numerics.BigInteger)center));
            foreach (var byDerivatives in removable || order > 6 ? new[] { false } : new[] { false, true })
            {
                if (Series.Taylor(f, x, c, order, null, byDerivatives) is not Outcome<Expr>.Success { Value: var polynomial })
                {
                    failures.Add($"{text} at {center} (derivatives: {byDerivatives}): no result");
                    continue;
                }
                var errors = new List<double>();
                foreach (var h in new[] { 0.04, 0.02 })
                {
                    var at = new Dictionary<Symbol, double> { [x] = center + h };
                    if (CalculusHelpers.Eval(f, at) is not { } exact || CalculusHelpers.Eval(polynomial, at) is not { } approx) { failures.Add($"{text}: undefined at {center + h}"); break; }
                    errors.Add(Math.Abs(exact - approx));
                }
                if (errors.Count < 2) continue;
                // The error is O(h^(order+1)): at least h^order, and the 0.04 error is about 2^(order+1) times the 0.02 error unless the next coefficient vanishes.
                if (errors[0] > 40 * Math.Pow(0.04, order + 1) * Math.Max(1, Math.Abs(center) + 1)) failures.Add($"{text} at {center}, degree {order} (derivatives: {byDerivatives}): error {errors[0]} is too large for {polynomial}");
                if (errors[1] > errors[0] / Math.Pow(2, order) + 1e-14) failures.Add($"{text} at {center}, degree {order}: the error does not shrink like h^{order + 1} ({errors[0]}, {errors[1]})");
            }
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void SeriesArithmeticAgreesWithTheDefinition()
    {
        var x = CalculusHelpers.X;
        foreach (var (text, f, center, order, removable) in Cases())
        {
            var c = new Number(new BigRational((System.Numerics.BigInteger)center));
            if (removable || order > 6) continue;
            var fast = ((Outcome<Expr>.Success)Series.Taylor(f, x, c, order)).Value;
            var slow = ((Outcome<Expr>.Success)Series.Taylor(f, x, c, order, null, true)).Value;
            foreach (var h in new[] { -0.3, 0.1, 0.45 })
            {
                var at = new Dictionary<Symbol, double> { [x] = center + h };
                Assert.AreEqual(CalculusHelpers.Eval(slow, at)!.Value, CalculusHelpers.Eval(fast, at)!.Value, 1e-9, $"{text} at {center}+{h}");
            }
        }
    }

    [TestMethod]
    public void MaclaurinOfExponentialIsTheFactorialSeries() =>
        Assert.AreEqual(Normalizer.Canonical(Expr.Parse("1 + x + x^2/2 + x^3/6 + x^4/24")), ((Outcome<Expr>.Success)Series.Maclaurin(Expr.Parse("exp(x)"), CalculusHelpers.X, 4)).Value);

    [TestMethod]
    public void DerivativeStepsCiteTheDefinitionAndArithmeticStepsCiteTheirEntry()
    {
        var x = CalculusHelpers.X;
        var byDefinition = (Outcome<Expr>.Success)Series.Maclaurin(Expr.Parse("sin(x)"), x, 3, null, true);
        Assert.AreEqual("calc.series.taylor", ((Derivation)byDefinition.Steps!).Steps[0].Entry!.Value.Value);
        var byArithmetic = (Outcome<Expr>.Success)Series.Maclaurin(Expr.Parse("sin(x)"), x, 3);
        Assert.AreEqual("calc.series.series-arithmetic", ((Derivation)byArithmetic.Steps!).Steps[0].Entry!.Value.Value);
    }

    [TestMethod]
    public void FunctionsWithoutDerivativesAtTheCenterStayUnevaluated()
    {
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Series.Maclaurin(Expr.Parse("1/x"), CalculusHelpers.X, 3));
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Series.Maclaurin(Expr.Parse("ln(x)"), CalculusHelpers.X, 3));
    }

    [TestMethod]
    public void PowerSeriesKeepsTrackOfTheOrderTerm()
    {
        var t = PowerSeries.Variable(6);
        var product = t * t;
        Assert.AreEqual(2, product.Valuation);
        Assert.AreEqual(6, product.Order, "the product of two series known below t^6 is known below t^6 (conservative)");
        var reciprocal = (PowerSeries.Constant(BigRational.One, 5) - t).Reciprocal()!;
        Assert.AreEqual(BigRational.One, reciprocal[4]);
    }
}
