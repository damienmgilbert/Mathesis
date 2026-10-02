using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Numbers;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Calculus;

/// <summary>
/// Differentiation with steps (docs/design/07-engines.md, "Calculus engines"). One step at a time, a <c>diff(e, x)</c> node is replaced by the
/// right side of the catalog rule for the shape of <c>e</c>; the inner derivatives stay in the result as <c>diff</c> nodes and are expanded by
/// the following steps, so the chain-rule step shows the inner derivative inside it.
/// </summary>
public static class Differentiator
{
    private static readonly RuleLibrary Library = RuleLibrary.Default;

    private static Number Num(BigRational r) => new(r);

    private static Apply Diff(Expr e, Symbol x) => new(Operators.Diff, [e, x]);

    private static Expr Mul(IEnumerable<Expr> factors)
    {
        var list = factors.ToList();
        return list.Count == 1 ? list[0] : new Apply(Operators.Mul, [.. list]);
    }

    private static Apply Pow(Expr b, Expr e) => new Apply(Operators.Pow, [b, e]);

    private static AlgorithmResult Result(string id, Expr replacement, Provisos provisos, Derivation? substeps = null, params (string, Expr)[] arguments) =>
        new(replacement, new EntryId(id), Library.Catalog.Get(id).Level ?? CurriculumLevel.Calculus1, provisos, substeps, [.. arguments.Select(a => new KeyValuePair<string, Expr>(a.Item1, a.Item2))]);

    /// <summary>
    /// Differentiates <paramref name="expr"/> with respect to <paramref name="x"/> <paramref name="order"/> times.
    /// </summary>
    /// <param name="expr">The expression.</param>
    /// <param name="x">The variable.</param>
    /// <param name="order">The order, at least 1.</param>
    /// <param name="math">Assumptions; <see cref="MathContext.Default"/> when <c>null</c>.</param>
    /// <param name="budget">Limits the work.</param>
    /// <param name="simplify">Whether to simplify the result (the simplification steps follow the differentiation steps).</param>
    /// <returns>
    /// Success with the derivative and its derivation; the provisos are the conditions the rules needed (for example <c>x &gt; 0</c> for
    /// <c>ln x</c>). Unevaluated when an operator has no derivative rule or depends on the variable in a way the rules do not cover.
    /// </returns>
    public static Outcome<Expr> Differentiate(Expr expr, Symbol x, int order = 1, MathContext? math = null, Budget? budget = null, bool simplify = true)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);
        var start = order == 1 ? Diff(expr, x) : new Apply(Operators.Diff, [expr, x, Num(order)]);
        var context = new RewriteContext(math, budget, ProvisoMode.Generic, null, 100_000);
        var strategy = Strategies.Fixpoint(Strategies.TopDown(Strategies.Algorithm("diff", DiffStep)));
        var outcome = RewriteEngine.Run(strategy, start, context);
        if (outcome is not Outcome<Expr>.Success { Value: var value, Steps: Derivation derivation } success) return outcome;
        if (value.Walk().Any(w => w.Expr is Apply { Operator: var op } && op == Operators.Diff))
        {
            return new Outcome<Expr>.Unevaluated(start, "Some part of the expression has no derivative rule.");
        }
        if (!simplify) return success;
        if (Simplifier.Simplify(value, math, null, budget) is Outcome<Expr>.Success { Value: var simpler, Steps: Derivation simplification } simple)
        {
            return Outcome.Ok(simpler, derivation.Then(simplification with { Start = value }), success.Provisos.Union(simple.Provisos), Verification.NotChecked);
        }
        return success;
    }

    /// <summary>The derivative as an expression, or <c>null</c> when it cannot be found (convenience for engines that only need the value).</summary>
    public static Expr? Derivative(Expr expr, Symbol x, int order = 1, MathContext? math = null, bool simplify = true) =>
        Differentiate(expr, x, order, math, null, simplify) is Outcome<Expr>.Success { Value: var v } ? v : null;

    /// <summary>
    /// Implicit differentiation (catalog <c>calc.deriv.implicit</c>): <c>dy/dx = −F_x/F_y</c> for the relation <paramref name="relation"/>
    /// (an equation <c>l = r</c> uses <c>F = l − r</c>; any other expression is taken as <c>F</c>), with the proviso <c>F_y ≠ 0</c>.
    /// </summary>
    public static Outcome<Expr> Implicit(Expr relation, Symbol x, Symbol y, MathContext? math = null)
    {
        ArgumentNullException.ThrowIfNull(relation);
        var f = relation is Apply { Operator: var op, Arguments: [var l, var r] } && op == Operators.Eq
            ? new Apply(Operators.Add, [l, new Apply(Operators.Mul, [Num(BigRational.NegativeOne), r])])
            : relation;
        if (Differentiate(f, x, 1, math) is not Outcome<Expr>.Success { Value: var fx, Steps: Derivation dx } ||
            Differentiate(f, y, 1, math) is not Outcome<Expr>.Success { Value: var fy, Steps: Derivation dy })
        {
            return new Outcome<Expr>.Unevaluated(relation, "The relation cannot be differentiated.");
        }
        var nonZero = new Apply(Operators.Ne, [fy, Num(BigRational.Zero)]);
        var provisos = Provisos.None;
        if (fy is Number { Value.Sign: 0 }) return new Outcome<Expr>.Unevaluated(relation, "F_y is zero.");
        if ((math ?? MathContext.Default).Ask(nonZero) != Truth.True) provisos = provisos.Add(nonZero);
        var quotient = new Apply(Operators.Mul, [Num(BigRational.NegativeOne), fx, new Apply(Operators.Pow, [fy, Num(BigRational.NegativeOne)])]);
        var normalized = Simplifier.Simplify(quotient, math) is Outcome<Expr>.Success { Value: var simplified } ? simplified : Normalizer.Canonical(quotient);
        var id = new EntryId("calc.deriv.implicit");
        var step = new Step(id, "implicit", relation, normalized, ExprPath.Root, Bindings.Empty, provisos, new ExplanationKey(id.Value, [new("Fx", fx), new("Fy", fy)]), Library.Catalog.Get(id.Value).Level ?? CurriculumLevel.Calculus1, dx);
        return Outcome.Ok(normalized, new Derivation(relation, normalized, [step]), provisos, Verification.NotChecked);
    }

    /// <summary>
    /// Logarithmic differentiation (catalog <c>calc.deriv.logarithmic</c>): <c>f' = f·(ln f)'</c> with <c>ln f</c> expanded first, valid where
    /// <c>f &gt; 0</c> (a proviso unless the assumptions settle it).
    /// </summary>
    public static Outcome<Expr> Logarithmic(Expr f, Symbol x, MathContext? math = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        math ??= MathContext.Default;
        var positive = new Apply(Operators.Gt, [f, Num(BigRational.Zero)]);
        if (math.Ask(positive) == Truth.False) return new Outcome<Expr>.Unevaluated(f, "f is not positive.");
        var provisos = math.Ask(positive) == Truth.True ? Provisos.None : Provisos.None.Add(positive);
        var lnF = Normalizer.Canonical(new Apply(Operators.Ln, [f]));
        var expanded = Transforms.LogExpand.Run(lnF, new RewriteContext(math.Assume(positive))) is Outcome<Expr>.Success { Value: var v } ? v : lnF;
        if (Differentiate(expanded, x, 1, math.Assume(positive)) is not Outcome<Expr>.Success { Value: var d, Steps: Derivation inner }) return new Outcome<Expr>.Unevaluated(f, "ln f cannot be differentiated.");
        var result = Normalizer.Canonical(new Apply(Operators.Mul, [f, d]));
        var id = new EntryId("calc.deriv.logarithmic");
        var step = new Step(id, "logarithmic", Diff(f, x), result, ExprPath.Root, Bindings.Empty, provisos, new ExplanationKey(id.Value, [new("ln f", expanded)]), Library.Catalog.Get(id.Value).Level ?? CurriculumLevel.Calculus1, inner);
        return Outcome.Ok(result, new Derivation(Diff(f, x), result, [step]), provisos, Verification.NotChecked);
    }

    // Decides a condition of a rule under the assumptions: true (holds), null (fails), or a proviso to add.
    private static bool Require(Expr condition, RewriteContext context, ref Provisos provisos)
    {
        switch (context.Math.Ask(condition))
        {
            case Truth.True: return true;
            case Truth.False: return false;
            default:
                provisos = provisos.Add(condition);
                return true;
        }
    }

    internal static AlgorithmResult? DiffStep(Expr node, RewriteContext context)
    {
        if (node is not Apply { Operator: var op, Arguments: [var e, Symbol x, ..var rest] } || op != Operators.Diff) return null;
        var provisos = Provisos.None;

        // Higher and mixed derivatives: peel one derivative off.
        if (rest.Length == 1)
        {
            if (rest[0] is Number { Value: var n } && n.IsInteger && n >= 1)
            {
                var inner = Diff(e, x);
                var again = n == BigRational.One ? inner : new Apply(Operators.Diff, [inner, x, Num(n - BigRational.One)]);
                return n == BigRational.One ? DiffStep(inner, context) : Result("calc.deriv.def", again, provisos, null, ("x", x), ("n", Num(n)));
            }
            if (rest[0] is Symbol y) return Result("calc.deriv.def", Diff(Diff(e, x), y), provisos, null, ("x", x), ("y", y));
            return null;
        }
        if (rest.Length != 0) return null;

        if (!e.FreeSymbols.Contains(x)) return Result("calc.deriv.constant", Num(BigRational.Zero), provisos, null, ("c", e), ("x", x));
        if (e.Equals(x)) return Result("calc.deriv.power", Num(BigRational.One), provisos, null, ("x", x), ("n", Num(BigRational.One)));

        if (e is not Apply { Operator: var head, Arguments: var args }) return null;

        if (head == Operators.Add)
            return Result("calc.deriv.sum", new Apply(Operators.Add, [.. args.Select(a => Diff(a, x))]), provisos, null, ("x", x));

        if (head == Operators.Mul)
        {
            var constants = args.Where(a => !a.FreeSymbols.Contains(x)).ToList();
            var dependent = args.Where(a => a.FreeSymbols.Contains(x)).ToList();
            if (constants.Count > 0)
            {
                var factor = Mul(constants);
                return Result("calc.deriv.constant-multiple", new Apply(Operators.Mul, [factor, Diff(Mul(dependent), x)]), provisos, null, ("c", factor), ("x", x));
            }
            var f = dependent[0];
            var g = Mul(dependent.Skip(1));
            return Result("calc.deriv.product", new Apply(Operators.Add, [new Apply(Operators.Mul, [Diff(f, x), g]), new Apply(Operators.Mul, [f, Diff(g, x)])]), provisos, null, ("f", f), ("g", g), ("x", x));
        }

        if (head == Operators.Pow)
        {
            var (u, p) = (args[0], args[1]);
            if (!p.FreeSymbols.Contains(x))
            {
                // diff(u^n, x) = n*u^(n - 1)*diff(u, x), for u > 0, or an integer n with u != 0 (n >= 1 needs nothing).
                var nonZero = new Apply(Operators.Ne, [u, Num(BigRational.Zero)]);
                if (p is Number { Value: var k } && k.IsInteger)
                {
                    if (k < BigRational.One && !Require(nonZero, context, ref provisos)) return null;
                }
                else if (!Require(new Apply(Operators.Gt, [u, Num(BigRational.Zero)]), context, ref provisos))
                {
                    return null;
                }
                var power = new Apply(Operators.Mul, [p, Pow(u, new Apply(Operators.Add, [p, Num(BigRational.NegativeOne)]))]);
                if (u.Equals(x)) return Result("calc.deriv.power", power, provisos, null, ("x", x), ("n", p));
                return Result("calc.deriv.general-power", new Apply(Operators.Mul, [power, Diff(u, x)]), provisos, null, ("u", u), ("n", p), ("x", x));
            }
            // diff(u^v, x) = u^v*(diff(v, x)*ln(u) + v*diff(u, x)/u), for u > 0.
            if (!Require(new Apply(Operators.Gt, [u, Num(BigRational.Zero)]), context, ref provisos)) return null;
            var lnU = new Apply(Operators.Ln, [u]);
            var variable = new Apply(Operators.Add, [new Apply(Operators.Mul, [Diff(p, x), lnU]), new Apply(Operators.Mul, [p, Diff(u, x), Pow(u, Num(BigRational.NegativeOne))])]);
            return Result("calc.deriv.var-power", new Apply(Operators.Mul, [e, variable]), provisos, null, ("u", u), ("v", p), ("x", x));
        }

        // An elementary function of an inner expression: the derivative table, with the chain rule when the inner expression is not x.
        if (args.Skip(1).Any(a => a.FreeSymbols.Contains(x))) return null;
        var inside = args[0];
        var probeNode = new Apply(head, [inside, .. args.Skip(1)]);
        var probe = new Apply(Operators.Diff, [probeNode, inside]);
        foreach (var rule in Library["derivative-table"].Index.Candidates(probe))
        {
            foreach (var match in rule.Matches(probe, context.Math, ProvisoMode.Generic, context.Match))
            {
                var table = new Derivation(probe, match.Replacement, [new Step(rule.Entry, rule.Name, probe, match.Replacement, ExprPath.Root, match.Bindings, match.Provisos, new ExplanationKey(rule.Entry?.Value ?? rule.Name, [.. match.Bindings.Select(b => b)]), rule.Level, null)]);
                if (inside.Equals(x))
                    return new AlgorithmResult(match.Replacement, rule.Entry, rule.Level, match.Provisos, null, [.. match.Bindings.Select(b => b)]);
                var chain = new Apply(Operators.Mul, [match.Replacement, Diff(inside, x)]);
                return Result("calc.deriv.chain", chain, match.Provisos, table, ("f", probeNode), ("g", inside), ("x", x));
            }
        }
        return null;
    }
}

/// <summary>The calculus algorithms by name, for <see cref="AlgorithmRegistry"/>.</summary>
public static class CalculusAlgorithms
{
    /// <summary>The algorithms.</summary>
    public static IReadOnlyDictionary<string, AlgorithmFunction> All { get; } = new Dictionary<string, AlgorithmFunction>(StringComparer.Ordinal)
    {
        ["diff"] = Differentiator.DiffStep,
        ["integrate"] = Integrator.IntegrateStep,
    };
}
