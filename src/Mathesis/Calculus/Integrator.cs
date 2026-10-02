using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Calculus;

/// <summary>
/// Symbolic integration (docs/design/07-engines.md, "Calculus engines": the integration pipeline, steps 1 to 8). The first method that succeeds
/// wins; every antiderivative is differentiated back and the difference is zero-tested before it is returned, otherwise the result is
/// <c>Unevaluated</c>. Methods that need a sub-integral call the pipeline again, so the result's derivation is a tree: the sub-integrals are the
/// <see cref="Step.Substeps"/> of the step that needed them.
/// </summary>
public static partial class Integrator
{
    private static readonly RuleLibrary Library = RuleLibrary.Default;
    private const int MaxDepth = 6;

    /// <summary>An antiderivative a method found.</summary>
    private sealed record Found(Expr Value, string Entry, Provisos Provisos, ImmutableList<Step> Sub, string Method, ImmutableArray<KeyValuePair<string, Expr>> Args)
    {
        public Step ToStep(Expr integrand, Symbol x)
        {
            var start = new Apply(Operators.Integrate, [integrand, x]);
            return new Step(new EntryId(Entry), "integrate/" + Method, start, Value, ExprPath.Root, Bindings.Empty, Provisos, new ExplanationKey(Entry, Args), Library.Catalog.Get(Entry).Level ?? CurriculumLevel.Calculus1, Sub.IsEmpty ? null : new Derivation(start, Value, [.. Sub]));
        }
    }

    private sealed class Ctx(MathContext math, Budget budget)
    {
        public MathContext Math { get; } = math;
        public Budget Budget { get; } = budget;
    }

    private static Number Num(BigRational r) => new(r);

    private static Apply Pow(Expr b, Expr e) => new(Operators.Pow, [b, e]);

    private static Apply Mul(params Expr[] f) => new(Operators.Mul, [.. f]);

    private static Apply Add(params Expr[] t) => new(Operators.Add, [.. t]);

    private static Expr Canon(Expr e) => Normalizer.Canonical(e);

    private static Expr Simple(Expr e, Ctx c) => Simplifier.Simplify(e, c.Math, null, new Budget(maxSteps: 400, maxTime: TimeSpan.FromSeconds(1))) is Outcome<Expr>.Success { Value: var v } ? v : Canon(e);

    // Conditions found for the integral in the substitution variable, restated in x.
    private static Provisos Back(Provisos provisos, Symbol from, Expr to)
    {
        var result = Provisos.None;
        foreach (var p in provisos) result = result.Add(p is Expr e ? Canon(e.Substitute(from, to)) : p);
        return result;
    }

    // 1/e with a product's factors inverted one by one, so that a/(a*b) cancels without distributing a power over a product first.
    private static Expr Reciprocal(Expr e) => e is Apply { Operator: var op, Arguments: var args } && op == Operators.Mul
        ? new Apply(Operators.Mul, [.. args.Select(Reciprocal)])
        : e is Apply { Operator: var pw, Arguments: [var b, Number { Value: var n }] } && pw == Operators.Pow ? Pow(b, Num(-n)) : Pow(e, Num(BigRational.NegativeOne));

    // Combines powers and cancels common polynomial factors, without the trigonometric identities that Simplify would also try.
    private static Expr Tidy(Expr e, Ctx c)
    {
        e = Canon(e);
        for (var round = 0; round < 4; round++)
        {
            var before = e;
            var context = new RewriteContext(c.Math, new Budget(maxSteps: 400, maxTime: TimeSpan.FromSeconds(1)), ProvisoMode.Generic);
            if (Transforms.PowerSimplify.Run(e, context) is Outcome<Expr>.Success { Value: var powers }) e = powers;
            if (Transforms.Cancel.Run(e, new RewriteContext(c.Math, null, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var cancelled }) e = cancelled;
            if (e.Equals(before)) break;
        }
        return e;
    }

    private static Found Make(Expr value, string entry, Provisos provisos, string method, ImmutableList<Step>? sub = null, params (string, Expr)[] args) =>
        new(Canon(value), entry, provisos, sub ?? [], method, [.. args.Select(a => new KeyValuePair<string, Expr>(a.Item1, a.Item2))]);

    // A condition under the assumptions: false when it fails, otherwise adds it as a proviso unless it is already known.
    private static bool Need(Expr condition, Ctx c, ref Provisos provisos)
    {
        switch (c.Math.Ask(condition))
        {
            case Truth.True: return true;
            case Truth.False: return false;
            default:
                provisos = provisos.Add(condition);
                return true;
        }
    }

    // ----- Public API -----

    /// <summary>
    /// An antiderivative of <paramref name="f"/> in <paramref name="x"/> (without the constant of integration).
    /// </summary>
    /// <returns>
    /// Success with the antiderivative, a derivation, the provisos (conditions the table and substitution rules needed) and
    /// <see cref="Verification.Verified"/> after the differentiate-back check; Unevaluated when no method applies or the check fails.
    /// </returns>
    public static Outcome<Expr> Integrate(Expr f, Symbol x, MathContext? math = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(x);
        var start = new Apply(Operators.Integrate, [f, x]);
        var context = new RewriteContext(math, budget, ProvisoMode.Generic);
        var outcome = RewriteEngine.Run(Strategies.Algorithm("integrate", IntegrateStep), start, context);
        return outcome is Outcome<Expr>.Success { Value: var value, Steps: var steps, Provisos: var provisos }
            ? Outcome.Ok(value, steps, provisos, Verification.Verified)
            : outcome;
    }

    internal static AlgorithmResult? IntegrateStep(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op, Arguments: [var f, Symbol x] } || op != Operators.Integrate) return null;
        var budget = ReferenceEquals(context.Budget, Budget.Unlimited) ? new Budget(maxSteps: 6000, maxTime: TimeSpan.FromSeconds(20)) : context.Budget;
        var c = new Ctx(context.Math, budget);
        var integrand = Canon(f);
        var found = Solve(integrand, x, 0, c);
        if (found is null || !Verify(found.Value, integrand, x, found.Provisos, c)) return null;
        return new AlgorithmResult(found.Value, new EntryId(found.Entry), Library.Catalog.Get(found.Entry).Level ?? CurriculumLevel.Calculus1, found.Provisos, found.Sub.IsEmpty ? null : new Derivation(node, found.Value, [.. found.Sub]), found.Args);
    }

    /// <summary>
    /// Whether the derivative of <paramref name="antiderivative"/> equals <paramref name="f"/>: the difference is zero-tested at points where the
    /// conditions in <paramref name="provisos"/> hold.
    /// </summary>
    public static bool Verify(Expr antiderivative, Expr f, Symbol x, Provisos provisos, MathContext? math = null) => Verify(antiderivative, f, x, provisos, new Ctx(math ?? MathContext.Default, Budget.Unlimited));

    private static bool Verify(Expr antiderivative, Expr f, Symbol x, Provisos provisos, Ctx c)
    {
        var context = c.Math;
        foreach (var p in provisos.OfType<Expr>())
        {
            if (p is Apply { Operator: var op } && (op == Operators.Gt || op == Operators.Lt || op == Operators.Ge || op == Operators.Le)) context = context.Assume(p);
        }
        if (Differentiator.Differentiate(antiderivative, x, 1, context, new Budget(maxSteps: 50_000, maxTime: TimeSpan.FromSeconds(5)), false) is not Outcome<Expr>.Success { Value: var d }) return false;
        var difference = Canon(Add(d, Mul(Num(BigRational.NegativeOne), f)));
        var result = ZeroTest.Test(difference, context);
        return result is ZeroTestResult.Zero or ZeroTestResult.ProbablyZero;
    }

    // ----- The pipeline -----

    private static Found? Solve(Expr f, Symbol x, int depth, Ctx c)
    {
        if (depth > MaxDepth || !c.Budget.TryCharge()) return null;
        f = Canon(f);
        if (f is Number { Value.Sign: 0 }) return Make(Num(BigRational.Zero), "calc.int.linearity", Provisos.None, "linearity");

        foreach (var method in new Func<Expr, Symbol, int, Ctx, Found?>[] { Linearity, Monomial, Table, Rational, TrigPowers, TrigProducts, Substitution, Parts, Weierstrass, TrigSubstitution })
        {
            if (c.Budget.IsExceeded) return null;
            var found = method(f, x, depth, c);
            if (found is null) continue;
            // Methods that rewrite the problem are checked at once, so a wrong sub-result makes the next method run instead of poisoning the answer.
            if (method == Linearity || method == Monomial || method == Table || Verify(found.Value, f, x, found.Provisos, c)) return found;
        }
        return null;
    }

    private static Found? Sub(Expr f, Symbol x, int depth, Ctx c, out Step step)
    {
        var found = Solve(f, x, depth + 1, c);
        step = found?.ToStep(f, x)!;
        return found;
    }

    // 1. Linearity and constant factors.
    private static Found? Linearity(Expr f, Symbol x, int depth, Ctx c)
    {
        if (!f.FreeSymbols.Contains(x)) return Make(Mul(f, x), "calc.int.linearity", Provisos.None, "linearity", null, ("c", f));
        if (f is not Apply { Operator: var op, Arguments: var args }) return null;
        if (op == Operators.Add)
        {
            var total = new List<Expr>();
            var steps = ImmutableList<Step>.Empty;
            var provisos = Provisos.None;
            foreach (var term in args)
            {
                if (Sub(term, x, depth, c, out var step) is not { } found) return null;
                total.Add(found.Value);
                steps = steps.Add(step);
                provisos = provisos.Union(found.Provisos);
            }
            return Make(new Apply(Operators.Add, [.. total]), "calc.int.linearity", provisos, "linearity", steps);
        }
        if (op == Operators.Mul)
        {
            var constants = args.Where(a => !a.FreeSymbols.Contains(x)).ToList();
            if (constants.Count == 0) return null;
            var rest = args.Where(a => a.FreeSymbols.Contains(x)).ToList();
            var factor = constants.Count == 1 ? constants[0] : new Apply(Operators.Mul, [.. constants]);
            var inner = rest.Count == 1 ? rest[0] : new Apply(Operators.Mul, [.. rest]);
            if (Sub(inner, x, depth, c, out var step) is not { } found) return null;
            return Make(Mul(factor, found.Value), "calc.int.linearity", found.Provisos, "constant-multiple", [step], ("c", factor));
        }
        return null;
    }

    // 3. Powers of x: the power rule and 1/x.
    private static Found? Monomial(Expr f, Symbol x, int depth, Ctx c)
    {
        var provisos = Provisos.None;
        Expr? exponent = f.Equals(x) ? Num(BigRational.One) : f is Apply { Operator: var op, Arguments: [var b, var e] } && op == Operators.Pow && b.Equals(x) && !e.FreeSymbols.Contains(x) ? e : null;
        if (exponent is null) return null;
        var minusOne = Num(BigRational.NegativeOne);
        if (exponent.Equals(minusOne))
        {
            if (!Need(new Apply(Operators.Ne, [x, Num(BigRational.Zero)]), c, ref provisos)) return null;
            return Make(new Apply(Operators.Ln, [new Apply(Operators.Abs, [x])]), "calc.itab.reciprocal", provisos, "table");
        }
        if (exponent is Number { Value: var n } && n.IsInteger)
        {
            if (n.Sign < 0 && !Need(new Apply(Operators.Ne, [x, Num(BigRational.Zero)]), c, ref provisos)) return null;
        }
        else if (!Need(new Apply(Operators.Gt, [x, Num(BigRational.Zero)]), c, ref provisos))
        {
            return null;
        }
        var next = Add(exponent, Num(BigRational.One));
        return Make(Mul(Pow(x, next), Pow(next, minusOne)), "calc.itab.power", provisos, "power", null, ("x", x), ("n", exponent));
    }

    // 2. The catalog table.
    private static Found? Table(Expr f, Symbol x, int depth, Ctx c)
    {
        var probe = new Apply(Operators.Integrate, [f, x]);
        foreach (var rule in Library["antiderivative"].Index.Candidates(probe))
        {
            if (rule.Tags.Contains("non-elementary") || rule.Level > (c.Math.Level ?? CurriculumLevel.Advanced)) continue;
            foreach (var match in rule.Matches(probe, c.Math, ProvisoMode.Generic, null))
            {
                return Make(match.Replacement, rule.Entry!.Value.Value, match.Provisos, "table", null, [.. match.Bindings.Select(b => (b.Key, b.Value))]);
            }
        }
        return null;
    }

    // 4. Rational functions: polynomial part plus partial fractions over the rational roots, with one irreducible quadratic factor allowed.
    private static Found? Rational(Expr f, Symbol x, int depth, Ctx c)
    {
        if (f.FreeSymbols.Count != 1 || !f.FreeSymbols.Contains(x)) return null;
        if (!PolynomialConversion.TryToRationalFunction(f, x, out var n, out var d) || n.IsZero) return null;
        var provisos = Provisos.None;
        var g = PolynomialAlgorithms.Gcd(n, d);
        if (g.Degree >= 1)
        {
            if (!Need(new Apply(Operators.Ne, [PolynomialConversion.FromPolynomial(g, x), Num(BigRational.Zero)]), c, ref provisos)) return null;
            n /= g;
            d /= g;
        }

        var terms = new List<Expr>();
        var entry = "calc.itab.power";
        var (quotient, remainder) = n.DivRem(d);
        for (var k = 0; k <= quotient.Degree; k++)
        {
            if (quotient[k] == BigRational.Zero) continue;
            terms.Add(Mul(Num(quotient[k] / new BigRational(k + 1)), Pow(x, Num(k + 1))));
        }
        if (d.Degree == 0 && quotient.Degree < 0) return null;

        if (!remainder.IsZero)
        {
            var lead = d.LeadingCoefficient;
            var roots = PolynomialAlgorithms.RationalRoots(d);
            var rest = d;
            foreach (var (root, m) in roots.Roots)
            {
                var linear = new Polynomial<BigRational>([-root, BigRational.One]);
                for (var i = 0; i < m; i++) rest = rest.DivRem(linear).Quotient;
            }
            var quadratic = rest.Degree == 2 ? rest / rest.LeadingCoefficient : null;
            if (rest.Degree > 2 || rest.Degree == 1 || (quadratic is not null && quadratic[1] * quadratic[1] - 4 * quadratic[0] >= BigRational.Zero)) return null;

            var monic = d / lead;
            var unknowns = new List<(BigRational Root, int Power)>();
            var basis = new List<Polynomial<BigRational>>();
            foreach (var (root, m) in roots.Roots)
            {
                for (var j = 1; j <= m; j++)
                {
                    unknowns.Add((root, j));
                    var other = Polynomial<BigRational>.One;
                    foreach (var (r2, m2) in roots.Roots) other *= new Polynomial<BigRational>([-r2, BigRational.One]).Pow(r2 == root ? m - j : m2);
                    basis.Add(other * (quadratic ?? Polynomial<BigRational>.One));
                }
            }
            if (quadratic is not null)
            {
                var others = Polynomial<BigRational>.One;
                foreach (var (r2, m2) in roots.Roots) others *= new Polynomial<BigRational>([-r2, BigRational.One]).Pow(m2);
                basis.Add(others * Polynomial<BigRational>.X);
                basis.Add(others);
            }
            var size = basis.Count;
            if (size != monic.Degree) return null;
            var matrix = new BigRational[size, size + 1];
            for (var row = 0; row < size; row++)
            {
                for (var col = 0; col < size; col++) matrix[row, col] = basis[col][row];
                matrix[row, size] = remainder[row] / lead;
            }
            if (Transforms.Solve(matrix, size) is not { } solution) return null;

            for (var i = 0; i < unknowns.Count; i++)
            {
                if (solution[i] == BigRational.Zero) continue;
                var (root, power) = unknowns[i];
                var linearExpr = PolynomialConversion.FromPolynomial(new Polynomial<BigRational>([-root, BigRational.One]), x);
                if (power == 1)
                {
                    if (!Need(new Apply(Operators.Ne, [linearExpr, Num(BigRational.Zero)]), c, ref provisos)) return null;
                    terms.Add(Mul(Num(solution[i]), new Apply(Operators.Ln, [new Apply(Operators.Abs, [linearExpr])])));
                }
                else
                {
                    if (!Need(new Apply(Operators.Ne, [linearExpr, Num(BigRational.Zero)]), c, ref provisos)) return null;
                    terms.Add(Mul(Num(solution[i] / new BigRational(1 - power)), Pow(linearExpr, Num(1 - power))));
                }
            }
            if (unknowns.Any(u => u.Power > 1)) entry = "alg.pf.repeated-linear";
            else if (unknowns.Count > 0) entry = "alg.pf.distinct-linear";
            if (quadratic is not null)
            {
                var bCoef = solution[unknowns.Count];
                var cCoef = solution[unknowns.Count + 1];
                var p = quadratic[1];
                var m2 = quadratic[0] - p * p / new BigRational(4);
                var qExpr = PolynomialConversion.FromPolynomial(quadratic, x);
                Expr m = Canon(Pow(Num(m2), Num(new BigRational(1) / new BigRational(2))));
                var shifted = Add(x, Num(p / new BigRational(2)));
                if (bCoef != BigRational.Zero) terms.Add(Mul(Num(bCoef / new BigRational(2)), new Apply(Operators.Ln, [qExpr])));
                var arctanCoefficient = cCoef - bCoef * p / new BigRational(2);
                if (arctanCoefficient != BigRational.Zero) terms.Add(Mul(Num(arctanCoefficient), Pow(m, Num(BigRational.NegativeOne)), new Apply(Operators.Arctan, [Mul(shifted, Pow(m, Num(BigRational.NegativeOne)))])));
                entry = "calc.itab.arctan-form";
            }
        }
        if (terms.Count == 0) return null;
        return Make(terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]), entry, provisos, "rational", null, ("integrand", f));
    }

    // 7. Trigonometric integrals: powers of sine and cosine, tangent and secant.
    private static Found? TrigPowers(Expr f, Symbol x, int depth, Ctx c)
    {
        if (f.FreeSymbols.Count != 1 || !f.FreeSymbols.Contains(x)) return null;
        // f = k * sin(x)^m * cos(x)^n
        var factors = f is Apply { Operator: var op, Arguments: var args } && op == Operators.Mul ? args : [f];
        BigRational k = BigRational.One;
        int sinPower = 0, cosPower = 0, tanPower = 0, secPower = 0;
        foreach (var factor in factors)
        {
            var (b, e) = factor is Apply { Operator: var po, Arguments: [var bb, Number { Value: var ee }] } && po == Operators.Pow && ee.IsInteger ? (bb, (int)ee.Numerator) : (factor, 1);
            if (factor is Number { Value: var number }) k *= number;
            else if (b is Apply { Operator.Id: "sin", Arguments: [var s1] } && s1.Equals(x)) sinPower += e;
            else if (b is Apply { Operator.Id: "cos", Arguments: [var s2] } && s2.Equals(x)) cosPower += e;
            else if (b is Apply { Operator.Id: "tan", Arguments: [var s3] } && s3.Equals(x)) tanPower += e;
            else if (b is Apply { Operator.Id: "sec", Arguments: [var s4] } && s4.Equals(x)) secPower += e;
            else return null;
        }
        var u = Fresh(f, x);
        var sin = new Apply(Operators.Sin, [x]);
        var cos = new Apply(Operators.Cos, [x]);
        var tan = new Apply(Operators.Tan, [x]);
        var sec = new Apply(Operators.Sec, [x]);
        Expr? h = null;
        Expr? back = null;
        var sign = BigRational.One;
        if (tanPower == 0 && secPower == 0 && sinPower >= 0 && cosPower >= 0 && (sinPower + cosPower) >= 2)
        {
            if (cosPower % 2 == 1)
            {
                // u = sin x: sin^m cos^(n-1) cos dx
                h = Mul(Pow(u, Num(sinPower)), Pow(Add(Num(BigRational.One), Mul(Num(BigRational.NegativeOne), Pow(u, Num(2)))), Num((cosPower - 1) / 2)));
                back = sin;
            }
            else if (sinPower % 2 == 1)
            {
                h = Mul(Pow(u, Num(cosPower)), Pow(Add(Num(BigRational.One), Mul(Num(BigRational.NegativeOne), Pow(u, Num(2)))), Num((sinPower - 1) / 2)));
                back = cos;
                sign = BigRational.NegativeOne;
            }
            else
            {
                // Both even: reduce the powers to multiple angles.
                var reduced = Transforms.TrigReduce.Run(f, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic));
                if (reduced is not Outcome<Expr>.Success { Value: var value, Steps: Derivation reduction } || value.Equals(f)) return null;
                if (Sub(Mul(Num(k), value), x, depth, c, out var step) is not { } found) return null;
                var entry = reduction.Steps.FirstOrDefault(s => s.Entry is not null)?.Entry?.Value ?? "trig.power.sin-sq";
                return Make(found.Value, entry, found.Provisos, "trig-power-reduce", [step]);
            }
        }
        else if (sinPower == 0 && cosPower == 0 && tanPower >= 0 && secPower >= 0 && (tanPower + secPower) >= 1 && secPower % 2 == 0 && secPower >= 2)
        {
            // u = tan x: tan^m (1 + tan^2)^((n-2)/2) sec^2
            h = Mul(Pow(u, Num(tanPower)), Pow(Add(Num(BigRational.One), Pow(u, Num(2))), Num((secPower - 2) / 2)));
            back = tan;
        }
        else if (sinPower == 0 && cosPower == 0 && secPower >= 1 && tanPower % 2 == 1 && tanPower >= 1)
        {
            // u = sec x: tan^(m-1) sec^(n-1) (sec tan)
            h = Mul(Pow(Add(Pow(u, Num(2)), Num(BigRational.NegativeOne)), Num((tanPower - 1) / 2)), Pow(u, Num(secPower - 1)));
            back = sec;
        }
        else if (sinPower == 0 && cosPower == 0 && secPower == 0 && tanPower >= 2)
        {
            // tan^n = tan^(n-2) (sec^2 - 1)
            var lowered = Add(Mul(Pow(tan, Num(tanPower - 2)), Pow(sec, Num(2))), Mul(Num(BigRational.NegativeOne), Pow(tan, Num(tanPower - 2))));
            if (Sub(Mul(Num(k), lowered), x, depth, c, out var step) is not { } found) return null;
            return Make(found.Value, "calc.int.reduce-tan", found.Provisos, "trig-reduce-tan", [step], ("n", Num(tanPower)));
        }
        if (h is null || back is null) return null;
        if (Sub(Mul(Num(k * sign), h), u, depth, c, out var inner) is not { } result) return null;
        var value2 = result.Value.Substitute(u, back);
        return Make(value2, "calc.int.substitution", Back(result.Provisos, u, back), "trig-substitution", [inner], ("g", back));
    }

    // Products and powers of sines and cosines of multiples of x: product-to-sum and power reduction until only single trigonometric terms remain.
    private static Found? TrigProducts(Expr f, Symbol x, int depth, Ctx c)
    {
        if (f is not Apply { Operator: var op } || (op != Operators.Mul && op != Operators.Pow) || f.FreeSymbols.Count != 1) return null;
        var trig = f.Walk().Where(w => w.Expr is Apply { Operator.Id: "sin" or "cos" }).Count();
        if (trig < 2 && !(f is Apply { Operator.Id: "pow" })) return null;
        var current = f;
        ImmutableList<Step> reduction = [];
        var entry = "";
        for (var round = 0; round < 4; round++)
        {
            var next = current;
            if (Transforms.TrigReduce.Run(next, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var reduced, Steps: Derivation d })
            {
                next = reduced;
                if (entry.Length == 0) entry = d.Steps.FirstOrDefault(s => s.Entry is not null)?.Entry?.Value ?? "";
            }
            if (Transforms.Expand.Run(next, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var expanded }) next = expanded;
            if (next.Equals(current)) break;
            current = next;
        }
        if (current.Equals(f) || entry.Length == 0) return null;
        if (Sub(current, x, depth, c, out var step) is not { } found) return null;
        return Make(found.Value, entry, found.Provisos, "trig-product-to-sum", [step]);
    }

    private static Symbol Fresh(Expr f, params Symbol[] avoid)
    {
        foreach (var name in new[] { "u", "v", "w", "t", "s", "u1", "u2", "u3" })
        {
            if (!f.FreeSymbols.Any(s => s.Name == name) && !avoid.Any(s => s.Name == name)) return new Symbol(name);
        }
        return new Symbol("u_" + f.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // 5. Derivative-divides substitution u = g(x).
    private static Found? Substitution(Expr f, Symbol x, int depth, Ctx c)
    {
        if (depth >= MaxDepth - 1) return null;
        var candidates = new List<Expr>();
        foreach (var (node, _) in f.Walk())
        {
            if (node is not Apply { Operator: var op, Arguments: var args }) continue;
            if (op == Operators.Add || op == Operators.Mul)
            {
                continue;
            }
            foreach (var a in args)
            {
                if (a is not Number && !a.Equals(x) && a.FreeSymbols.Contains(x) && !candidates.Contains(a)) candidates.Add(a);
            }
            if (op != Operators.Pow && node.FreeSymbols.Contains(x) && !candidates.Contains(node)) candidates.Add(node);
        }
        var u = Fresh(f, x);
        foreach (var g in candidates.OrderByDescending(g => g.LeafCount).Take(6))
        {
            if (!c.Budget.TryCharge()) return null;
            if (Differentiator.Differentiate(g, x, 1, c.Math, null, true) is not Outcome<Expr>.Success { Value: var dg } || dg is Number { Value.Sign: 0 }) continue;
            var ratio = Tidy(Mul(f, Reciprocal(dg)), c);
            if (Environment.GetEnvironmentVariable("MATHESIS_TRACE") == "1") Console.Error.WriteLine($"TRACE subst f={f} g={g} dg={dg} ratio={ratio}");
            var h = ratio.Transform(e => e.Equals(g) ? u : null);
            if (h.FreeSymbols.Contains(x))
            {
                // A linear inner expression a*x + b: solve it for x.
                if (!dg.FreeSymbols.Contains(x) && PolynomialConversion.Coefficients(g, x) is { } co && co.Count == 2 && co.ContainsKey(1))
                {
                    var a = co[1];
                    var b = co.TryGetValue(0, out var b0) ? b0 : Num(BigRational.Zero);
                    var xOfU = Mul(Add(u, Mul(Num(BigRational.NegativeOne), b)), Pow(a, Num(BigRational.NegativeOne)));
                    h = Tidy(ratio.Transform(e => e.Equals(x) ? xOfU : null), c);
                }
                else if (g is Apply { Operator: var gop, Arguments: [var gb, Number { Value: var r }] } && gop == Operators.Pow && gb.Equals(x) && r != BigRational.One && r != BigRational.Zero)
                {
                    // u = x^r: x = u^(1/r) for positive x.
                    var positive = Provisos.None;
                    if (!Need(new Apply(Operators.Gt, [x, Num(BigRational.Zero)]), c, ref positive)) continue;
                    h = Tidy(ratio.Transform(e => e.Equals(x) ? Pow(u, Num(BigRational.One / r)) : null), c);
                }
                if (h.FreeSymbols.Contains(x)) continue;
            }
            if (Sub(h, u, depth, c, out var step) is not { } found) continue;
            return Make(found.Value.Substitute(u, g), "calc.int.substitution", Back(found.Provisos, u, g), "substitution", [step], ("g", g), ("u", u));
        }
        return null;
    }

    // 6. Integration by parts with the LIATE ordering.
    private static Found? Parts(Expr f, Symbol x, int depth, Ctx c)
    {
        if (depth >= MaxDepth - 2 || f.FreeSymbols.Count != 1) return null;
        var factors = f is Apply { Operator: var op, Arguments: var args } && op == Operators.Mul ? [.. args.Where(a => a.FreeSymbols.Contains(x))] : new List<Expr> { f };
        var constants = f is Apply { Operator: var op2, Arguments: var args2 } && op2 == Operators.Mul ? args2.Where(a => !a.FreeSymbols.Contains(x)).ToList() : [];
        if (constants.Count > 0) return null;
        static int Rank(Expr e) => e switch
        {
            Apply { Operator.Id: "arcsin" or "arccos" or "arctan" or "arccot" or "arcsec" or "arccsc" } => 0,
            Apply { Operator.Id: "ln" or "log" } => 1,
            Apply { Operator.Id: "pow", Arguments: [Apply { Operator.Id: "ln" }, _] } => 1,
            Symbol => 2,
            Apply { Operator.Id: "pow", Arguments: [Symbol, Number] } => 2,
            Apply { Operator.Id: "sin" or "cos" or "tan" or "sec" or "csc" or "cot" } => 3,
            Apply { Operator.Id: "pow", Arguments: [Apply { Operator.Id: "sin" or "cos" or "tan" or "sec" or "csc" }, _] } => 3,
            Apply { Operator.Id: "exp" } => 4,
            _ => 5,
        };
        if (factors.Count == 1 && Rank(factors[0]) > 1) return null;
        var order = factors.Select((e, i) => (e, i)).OrderBy(t => Rank(t.e)).ThenBy(t => t.i).ToList();
        var u = order[0].e;
        var dv = factors.Count == 1 ? Num(BigRational.One) : Canon(factors.Count == 2 ? factors[1 - order[0].i] : new Apply(Operators.Mul, [.. factors.Where((_, i) => i != order[0].i)]));
        if (Rank(u) >= 4 && factors.Count > 1 && Rank(dv) >= 4 && Rank(dv) != 5) return null;
        if (Sub(dv, x, depth, c, out var vStep) is not { } v) return null;
        if (Differentiator.Differentiate(u, x, 1, c.Math, null, true) is not Outcome<Expr>.Success { Value: var du }) return null;
        var rest = Simple(Mul(v.Value, du), c);
        if (rest.Equals(f)) return null;
        if (Sub(rest, x, depth + 1, c, out var restStep) is not { } second) return null;
        return Make(Add(Mul(u, v.Value), Mul(Num(BigRational.NegativeOne), second.Value)), "calc.int.parts", v.Provisos.Union(second.Provisos), "parts", [vStep, restStep], ("u", u), ("dv", dv));
    }

    // 8. The Weierstrass substitution t = tan(x/2) for rational functions of sine and cosine.
    private static Found? Weierstrass(Expr f, Symbol x, int depth, Ctx c)
    {
        if (depth >= MaxDepth - 1 || f.FreeSymbols.Count != 1) return null;
        string[] trig = ["sin", "cos", "tan", "sec", "csc", "cot"];
        var uses = false;
        foreach (var (node, _) in f.Walk())
        {
            if (node is Apply { Operator.Id: var id, Arguments: [var arg] } && trig.Contains(id))
            {
                if (!arg.Equals(x)) return null;
                uses = true;
            }
        }
        if (!uses) return null;
        var t = Fresh(f, x);
        var denominator = Add(Num(BigRational.One), Pow(t, Num(2)));
        Expr sinT = Mul(Num(2), t, Pow(denominator, Num(BigRational.NegativeOne)));
        Expr cosT = Mul(Add(Num(BigRational.One), Mul(Num(BigRational.NegativeOne), Pow(t, Num(2)))), Pow(denominator, Num(BigRational.NegativeOne)));
        var replaced = f.Transform(e => e switch
        {
            Apply { Operator.Id: "sin" } => sinT,
            Apply { Operator.Id: "cos" } => cosT,
            Apply { Operator.Id: "tan" } => Mul(sinT, Pow(cosT, Num(BigRational.NegativeOne))),
            Apply { Operator.Id: "cot" } => Mul(cosT, Pow(sinT, Num(BigRational.NegativeOne))),
            Apply { Operator.Id: "sec" } => Pow(cosT, Num(BigRational.NegativeOne)),
            Apply { Operator.Id: "csc" } => Pow(sinT, Num(BigRational.NegativeOne)),
            _ => null,
        });
        var rational = Simple(Mul(replaced, Num(2), Pow(denominator, Num(BigRational.NegativeOne))), c);
        rational = Simple(Transforms.Together.Run(rational, new RewriteContext(c.Math)) is Outcome<Expr>.Success { Value: var together } ? together : rational, c);
        rational = Simple(Transforms.Cancel.Run(rational, new RewriteContext(c.Math, null, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var cancelled } ? cancelled : rational, c);
        if (rational.FreeSymbols.Any(s => !s.Equals(t))) return null;
        if (Sub(rational, t, depth, c, out var step) is not { } found) return null;
        var back = Pow(new Apply(Operators.Tan, [Mul(x, Num(new BigRational(1) / new BigRational(2)))]), Num(BigRational.One));
        var entry = Library["weierstrass"].Rules[0].Entry!.Value.Value;
        return Make(found.Value.Substitute(t, back), entry, Back(found.Provisos, t, back), "weierstrass", [step], ("t", back));
    }

    // 7b. Trigonometric substitution for sqrt(a^2 - x^2), sqrt(a^2 + x^2) and sqrt(x^2 - a^2).
    private static Found? TrigSubstitution(Expr f, Symbol x, int depth, Ctx c)
    {
        if (depth >= MaxDepth - 2 || f.FreeSymbols.Count != 1) return null;
        foreach (var (node, _) in f.Walk())
        {
            if (node is not Apply { Operator: var op, Arguments: [var inner, Number { Value: var p }] } || op != Operators.Pow || p.IsInteger || p.Denominator != 2) continue;
            if (PolynomialConversion.Coefficients(inner, x) is not { } co || co.Keys.Any(k => k is not (0 or 2)) || !co.TryGetValue(2, out var ae) || ae is not Number { Value: var alpha }) continue;
            var beta = co.TryGetValue(0, out var be) && be is Number { Value: var bv } ? bv : BigRational.Zero;
            if (alpha == BigRational.Zero || beta == BigRational.Zero) continue;
            var theta = Fresh(f, x);
            var sMagnitude = Canon(Pow(Num(BigRational.Abs(beta / alpha)), Num(new BigRational(1) / new BigRational(2))));
            Expr xOfTheta, radical, dx, thetaOfX;
            Func<Expr, Expr> back;
            var provisos = Provisos.None;
            var betaMagnitude = BigRational.Abs(beta);
            var sin = new Apply(Operators.Sin, [theta]);
            var cos = new Apply(Operators.Cos, [theta]);
            var tan = new Apply(Operators.Tan, [theta]);
            var sec = new Apply(Operators.Sec, [theta]);
            Expr betaPowerP = Canon(Pow(Num(betaMagnitude), Num(p)));
            if (alpha < BigRational.Zero && beta > BigRational.Zero)
            {
                // x = s sin θ
                xOfTheta = Mul(sMagnitude, sin);
                radical = Mul(betaPowerP, Pow(cos, Num(2 * p)));
                dx = Mul(sMagnitude, cos);
                var xs = Mul(x, Pow(sMagnitude, Num(BigRational.NegativeOne)));
                thetaOfX = new Apply(Operators.Arcsin, [xs]);
                var cosX = Pow(Add(Num(BigRational.One), Mul(Num(BigRational.NegativeOne), Pow(xs, Num(2)))), Num(new BigRational(1) / new BigRational(2)));
                back = e => e.Transform(n => n switch
                {
                    Apply { Operator.Id: "sin", Arguments: [var a] } when a.Equals(theta) => xs,
                    Apply { Operator.Id: "cos", Arguments: [var a] } when a.Equals(theta) => cosX,
                    Apply { Operator.Id: "tan", Arguments: [var a] } when a.Equals(theta) => Mul(xs, Pow(cosX, Num(BigRational.NegativeOne))),
                    Apply { Operator.Id: "sec", Arguments: [var a] } when a.Equals(theta) => Pow(cosX, Num(BigRational.NegativeOne)),
                    Apply { Operator.Id: "csc", Arguments: [var a] } when a.Equals(theta) => Pow(xs, Num(BigRational.NegativeOne)),
                    Apply { Operator.Id: "cot", Arguments: [var a] } when a.Equals(theta) => Mul(cosX, Pow(xs, Num(BigRational.NegativeOne))),
                    _ => n.Equals(theta) ? thetaOfX : null,
                });
            }
            else if (alpha > BigRational.Zero && beta > BigRational.Zero)
            {
                // x = s tan θ
                xOfTheta = Mul(sMagnitude, sin, Pow(cos, Num(BigRational.NegativeOne)));
                radical = Mul(betaPowerP, Pow(cos, Num(-2 * p)));
                dx = Mul(sMagnitude, Pow(cos, Num(-2)));
                var xs = Mul(x, Pow(sMagnitude, Num(BigRational.NegativeOne)));
                thetaOfX = new Apply(Operators.Arctan, [xs]);
                var secX = Pow(Add(Num(BigRational.One), Pow(xs, Num(2))), Num(new BigRational(1) / new BigRational(2)));
                back = e => e.Transform(n => n switch
                {
                    Apply { Operator.Id: "tan", Arguments: [var a] } when a.Equals(theta) => xs,
                    Apply { Operator.Id: "sec", Arguments: [var a] } when a.Equals(theta) => secX,
                    Apply { Operator.Id: "cos", Arguments: [var a] } when a.Equals(theta) => Pow(secX, Num(BigRational.NegativeOne)),
                    Apply { Operator.Id: "sin", Arguments: [var a] } when a.Equals(theta) => Mul(xs, Pow(secX, Num(BigRational.NegativeOne))),
                    Apply { Operator.Id: "cot", Arguments: [var a] } when a.Equals(theta) => Pow(xs, Num(BigRational.NegativeOne)),
                    Apply { Operator.Id: "csc", Arguments: [var a] } when a.Equals(theta) => Mul(secX, Pow(xs, Num(BigRational.NegativeOne))),
                    _ => n.Equals(theta) ? thetaOfX : null,
                });
            }
            else if (alpha > BigRational.Zero && beta < BigRational.Zero)
            {
                // x = s sec θ, for x > s
                xOfTheta = Mul(sMagnitude, Pow(cos, Num(BigRational.NegativeOne)));
                radical = Mul(betaPowerP, Pow(sin, Num(2 * p)), Pow(cos, Num(-2 * p)));
                dx = Mul(sMagnitude, sin, Pow(cos, Num(-2)));
                var xs = Mul(x, Pow(sMagnitude, Num(BigRational.NegativeOne)));
                thetaOfX = new Apply(Operators.Arccos, [Pow(xs, Num(BigRational.NegativeOne))]);
                var tanX = Pow(Add(Pow(xs, Num(2)), Num(BigRational.NegativeOne)), Num(new BigRational(1) / new BigRational(2)));
                if (!Need(new Apply(Operators.Gt, [x, sMagnitude]), c, ref provisos)) continue;
                back = e => e.Transform(n => n switch
                {
                    Apply { Operator.Id: "sec", Arguments: [var a] } when a.Equals(theta) => xs,
                    Apply { Operator.Id: "tan", Arguments: [var a] } when a.Equals(theta) => tanX,
                    Apply { Operator.Id: "cos", Arguments: [var a] } when a.Equals(theta) => Pow(xs, Num(BigRational.NegativeOne)),
                    Apply { Operator.Id: "sin", Arguments: [var a] } when a.Equals(theta) => Mul(tanX, Pow(xs, Num(BigRational.NegativeOne))),
                    Apply { Operator.Id: "cot", Arguments: [var a] } when a.Equals(theta) => Pow(tanX, Num(BigRational.NegativeOne)),
                    Apply { Operator.Id: "csc", Arguments: [var a] } when a.Equals(theta) => Mul(xs, Pow(tanX, Num(BigRational.NegativeOne))),
                    _ => n.Equals(theta) ? thetaOfX : null,
                });
            }
            else
            {
                continue;
            }

            var substituted = f.Transform(e => e.Equals(node) ? radical : null).Substitute(x, xOfTheta);
            var integrand = Tidy(Mul(substituted, dx), c);
            if (integrand.FreeSymbols.Any(s => !s.Equals(theta))) continue;
            if (Sub(integrand, theta, depth + 1, c, out var step) is not { } found) continue;
            var expanded = Transforms.TrigExpand.Run(found.Value, new RewriteContext(c.Math, null, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var te } ? te : found.Value;
            var result = back(expanded);
            if (result.FreeSymbols.Contains(theta)) continue;
            return Make(result, "calc.int.substitution", Back(found.Provisos, theta, thetaOfX).Union(provisos), "trig-substitution", [step], ("x", xOfTheta));
        }
        return null;
    }
}
