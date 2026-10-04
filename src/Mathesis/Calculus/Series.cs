using System.Numerics;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Calculus;

/// <summary>Taylor and Maclaurin polynomials (docs/design/07-engines.md, "Series and sums").</summary>
public static class Series
{
    private static readonly KnowledgeBase Catalog = KnowledgeBase.Default;

    /// <summary>
    /// The Taylor polynomial of <paramref name="f"/> in <paramref name="x"/> around <paramref name="center"/> up to degree <paramref name="order"/>.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="x">The variable.</param>
    /// <param name="center">The center (a number or an expression free of <paramref name="x"/>).</param>
    /// <param name="order">The degree of the polynomial, at least 0.</param>
    /// <param name="math">Assumptions and level.</param>
    /// <param name="byDerivatives">Use the definition (derivatives at the center, explained step by step) even when series arithmetic would do.</param>
    /// <returns>
    /// Success with the polynomial (without the remainder) and a derivation citing <c>calc.series.series-arithmetic</c> or
    /// <c>calc.series.taylor</c>; Unevaluated when a derivative does not exist or is undefined at the center.
    /// </returns>
    public static Outcome<Expr> Taylor(Expr f, Symbol x, Expr center, int order, MathContext? math = null, bool byDerivatives = false)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(center);
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        math ??= MathContext.Default;
        var canonicalCenter = Normalizer.Canonical(center);

        if (!byDerivatives && canonicalCenter is Number { Value: var c })
        {
            for (var extra = 0; extra <= 8; extra += 4)
            {
                var series = PowerSeries.FromExpression(Normalizer.Canonical(f), x, c, order + 1 + extra);
                if (series is null || series.Valuation is < 0) break;
                if (series.Order <= order) continue;
                var polynomial = Normalizer.Canonical(TruncatedExpression(series, x, canonicalCenter, order));
                return Done(f, polynomial, "calc.series.series-arithmetic", math, null, "series");
            }
        }

        // The definition: c_k = f⁽ᵏ⁾(a)/k!.
        var terms = new List<Expr>();
        var current = Normalizer.Canonical(f);
        Derivation? lastDerivation = null;
        var bindings = new Dictionary<Symbol, Expr> { [x] = canonicalCenter };
        var factorial = BigInteger.One;
        for (var k = 0; k <= order; k++)
        {
            if (k > 0)
            {
                factorial *= k;
                if (Differentiator.Differentiate(current, x, 1, math, null, false) is not Outcome<Expr>.Success { Value: var next, Steps: Derivation d }) return new Outcome<Expr>.Unevaluated(f, $"The derivative of order {k} cannot be found.");
                current = next;
                lastDerivation = d;
            }
            if (Evaluator.Evaluate(current, bindings, math.NormalizeOptions) is not Outcome<Expr>.Success { Value: var value } || value.Walk().Any(w => w.Expr is Apply { Operator.Id: "diff" } or Constant { Id: ConstantId.Undefined } or Apply { Operator.Id: "pow", Arguments: [Number { Value.Sign: 0 }, Number { Value.Sign: < 0 }] }))
            {
                return new Outcome<Expr>.Unevaluated(f, $"The derivative of order {k} is not defined at the center.");
            }
            var coefficient = new Apply(Operators.Mul, [value, new Apply(Operators.Pow, [new Number(new BigRational(factorial)), new Number(BigRational.NegativeOne)])]);
            Expr power = k == 0 ? new Number(BigRational.One) : new Apply(Operators.Pow, [Shifted(x, canonicalCenter), new Number(k)]);
            terms.Add(new Apply(Operators.Mul, [coefficient, power]));
        }
        var result = Normalizer.Canonical(new Apply(Operators.Add, [.. terms]), math.NormalizeOptions);
        return Done(f, result, "calc.series.taylor", math, lastDerivation, "taylor");
    }

    /// <summary>The Maclaurin polynomial (center 0).</summary>
    public static Outcome<Expr> Maclaurin(Expr f, Symbol x, int order, MathContext? math = null, bool byDerivatives = false) =>
        Taylor(f, x, new Number(BigRational.Zero), order, math, byDerivatives);

    private static Expr Shifted(Symbol x, Expr center) =>
        center is Number { Value.Sign: 0 } ? x : new Apply(Operators.Add, [x, new Apply(Operators.Mul, [new Number(BigRational.NegativeOne), center])]);

    private static Expr TruncatedExpression(PowerSeries series, Symbol x, Expr center, int order)
    {
        var terms = new List<Expr>();
        for (var k = 0; k <= order; k++)
        {
            var c = series[k];
            if (c == BigRational.Zero) continue;
            Expr power = k == 0 ? new Number(BigRational.One) : new Apply(Operators.Pow, [Shifted(x, center), new Number(k)]);
            terms.Add(new Apply(Operators.Mul, [new Number(c), power]));
        }
        return terms.Count == 0 ? new Number(BigRational.Zero) : new Apply(Operators.Add, [.. terms]);
    }

    private static Outcome<Expr> Done(Expr f, Expr result, string entry, MathContext math, Derivation? substeps, string name)
    {
        var id = new EntryId(entry);
        var step = new Step(id, name, f, result, ExprPath.Root, Mathesis.Symbolics.Patterns.Bindings.Empty, Provisos.None, ExplanationKey.Of(entry), Catalog.Get(entry).Level ?? CurriculumLevel.Calculus2, substeps);
        return Outcome.Ok(result, new Derivation(f, result, [step]), Provisos.None, Verification.NotChecked);
    }
}
