using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Rewriting;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class RewritingTests
{
    private static readonly ParserOptions Wilds = new() { AllowWilds = true };

    private static Expr W(string text) => Normalizer.Canonical(Expr.Parse(text, Wilds));

    private static Expr C(string text) => Normalizer.Canonical(Expr.Parse(text));

    private static Rule R(string name, string lhs, string rhs, string? guard = null, params (string, WildOptions)[] options) =>
        new("alg.t." + name, new EntryId("alg.t." + name), new Pattern(W(lhs), options.ToDictionary(o => o.Item1, o => o.Item2)), W(rhs), guard is null ? null : Expr.Parse(guard, Wilds), CurriculumLevel.Algebra1, "Rule " + name, []);

    private static RuleSet Set(params Rule[] rules) => new("test", rules);

    private static RewriteState Start(string text) => new(C(text), ExprPath.Root, []);

    private static readonly RewriteContext Context = new();

    [TestMethod]
    public void ApplyFiresARuleAndRecordsTheStep()
    {
        var pythagorean = R("pyth", "sin(t_)^2 + cos(t_)^2", "1");
        var state = Strategies.Apply(Set(pythagorean)).Run(Start("sin(u)^2 + cos(u)^2 + 3 + z"), Context);
        Assert.IsNotNull(state);
        Assert.AreEqual(C("4 + z"), state.Root);
        var step = state.Steps.Single();
        Assert.AreEqual("alg.t.pyth", step.RuleName);
        Assert.AreEqual("alg.t.pyth", step.Entry!.Value.Value);
        Assert.AreEqual(C("sin(u)^2 + cos(u)^2 + 3 + z"), step.Before);
        Assert.AreEqual(state.Root, step.After);
        Assert.AreEqual(ExprPath.Root, step.Path);
        Assert.AreEqual("u", step.Bindings["t"].ToString());
        Assert.AreEqual(CurriculumLevel.Algebra1, step.Level);
        Assert.IsNull(step.Substeps);
        Assert.AreEqual("alg.t.pyth", step.Explanation.Key);
    }

    [TestMethod]
    public void ApplyFailsWhenNothingMatchesOrNothingChanges()
    {
        var rules = Set(R("pyth", "sin(t_)^2 + cos(t_)^2", "1"));
        Assert.IsNull(Strategies.Apply(rules).Run(Start("sin(u)^2 + cos(v)^2"), Context));
        Assert.IsNull(Strategies.Apply(Set(R("same", "sin(t_)", "sin(t_)"))).Run(Start("sin(u)"), Context), "a rule that changes nothing does not fire");
    }

    [TestMethod]
    public void StepsRecordThePathOfTheRewrittenNode()
    {
        var rule = R("ex", "exp(a_)*exp(b_)", "exp(a_ + b_)");
        var state = Start("2 + sin(exp(x)*exp(y))");
        var inner = Strategies.TopDown(Strategies.Apply(Set(rule))).Run(state, Context);
        Assert.IsNotNull(inner);
        var step = inner.Steps.Single();
        Assert.IsFalse(step.Path.IsRoot);
        Assert.AreEqual(C("exp(x)*exp(y)"), step.Path.Get(step.Before));
        Assert.AreEqual(C("2 + sin(exp(x + y))"), inner.Root);
    }

    [TestMethod]
    public void GuardsAreDecidedWithTheAssumptions()
    {
        var sqrtSquare = R("sqsq", "sqrt(a_^2)", "a_", "a_ >= 0");
        var set = Set(sqrtSquare);
        Assert.IsNull(Strategies.Apply(set).Run(Start("sqrt(x^2)"), Context), "unknown guard: strict for conditions other than nonzero");
        var positive = new RewriteContext(MathContext.Default.Assume(Expr.Parse("x > 0")));
        var state = Strategies.Apply(set).Run(Start("sqrt(x^2)"), positive);
        Assert.IsNotNull(state);
        Assert.AreEqual(C("x"), state.Root);
        Assert.AreEqual(0, state.Steps[0].Added.Count);
        var negative = new RewriteContext(MathContext.Default.Assume(Expr.Parse("x < 0")));
        Assert.IsNull(Strategies.Apply(set).Run(Start("sqrt(x^2)"), negative), "a false guard blocks the rule");
    }

    [TestMethod]
    public void ProvisoModesDecideUnknownGuards()
    {
        var cancel = R("cancel", "(a_*c_)/(b_*c_)", "a_/b_", "c_ != 0");
        var set = Set(cancel);
        var subject = Start("(x*y)/(z*y)");

        var byDefault = Strategies.Apply(set).Run(subject, Context);
        Assert.IsNotNull(byDefault, "x != 0 style guards become provisos by default");
        Assert.AreEqual(1, byDefault.Steps[0].Added.Count);
        Assert.AreEqual("y != 0", byDefault.Steps[0].Added[0].ToString());

        Assert.IsNull(Strategies.Apply(set).Run(subject, new RewriteContext(mode: ProvisoMode.Strict)));
        var known = new RewriteContext(MathContext.Default.Assume(Expr.Parse("y != 0")), mode: ProvisoMode.Strict);
        Assert.IsNotNull(Strategies.Apply(set).Run(subject, known));

        var positiveOnly = Set(R("pos", "sqrt(a_^2)", "a_", "a_ > 0"));
        Assert.IsNull(Strategies.Apply(positiveOnly).Run(Start("sqrt(x^2)"), Context));
        var generic = Strategies.Apply(positiveOnly).Run(Start("sqrt(x^2)"), new RewriteContext(mode: ProvisoMode.Generic));
        Assert.IsNotNull(generic);
        Assert.AreEqual("x > 0", generic.Steps[0].Added[0].ToString());
    }

    [TestMethod]
    public void SequenceChoiceAndTry()
    {
        var a = Strategies.Apply(Set(R("ab", "sin(t_)", "cos(t_)")));
        var b = Strategies.Apply(Set(R("bc", "cos(t_)", "tan(t_)")));
        var seq = Strategies.Seq(a, b).Run(Start("sin(x)"), Context);
        Assert.AreEqual(C("tan(x)"), seq!.Root);
        Assert.AreEqual(2, seq.Steps.Count);
        Assert.IsNull(Strategies.Seq(b, a).Run(Start("sin(x)"), Context), "b does not apply to sin x");
        Assert.AreEqual(C("tan(x)"), Strategies.Choice(a, b).Run(Start("cos(x)"), Context)!.Root);
        Assert.AreEqual(C("cos(x)"), Strategies.Choice(a, b).Run(Start("sin(x)"), Context)!.Root);
        Assert.AreEqual(C("sin(x)"), Strategies.Try(b).Run(Start("sin(x)"), Context)!.Root);
        Assert.IsNull(Strategies.Choice(b, b).Run(Start("sin(x)"), Context));
    }

    [TestMethod]
    public void TraversalsApplyOnceAtTheFirstPositionInTheirOrder()
    {
        var rule = Strategies.Apply(Set(R("sc", "sin(t_)", "cos(t_)")));
        var top = Strategies.TopDown(rule).Run(Start("sin(sin(x))"), Context)!;
        Assert.AreEqual(C("cos(sin(x))"), top.Root, "pre-order rewrites the outer sine first");
        var bottom = Strategies.BottomUp(rule).Run(Start("sin(sin(x))"), Context)!;
        Assert.AreEqual(C("sin(cos(x))"), bottom.Root, "post-order rewrites the inner sine first");
        Assert.IsNull(Strategies.TopDown(rule).Run(Start("x + 1"), Context));
    }

    [TestMethod]
    public void InnermostAndOutermostReachTheNormalForm()
    {
        var rule = Set(R("sc", "sin(t_)", "cos(t_)"), R("ct", "cos(t_)", "x"));
        var inner = Strategies.Innermost(Strategies.Apply(rule)).Run(Start("sin(sin(y))"), Context);
        Assert.IsNotNull(inner);
        Assert.IsTrue(inner.Root.FreeSymbols.Count > 0);
        Assert.IsFalse(inner.Root.Contains(Sin(Symbol("y"))));
        Assert.IsNotNull(Strategies.Outermost(Strategies.Apply(rule)).Run(Start("sin(sin(y))"), Context));
    }

    [TestMethod]
    public void RepeatStopsAtTheLimitAndAlwaysSucceeds()
    {
        var grow = Strategies.Apply(Set(R("grow", "sin(t_)", "sin(2*t_)")));
        var state = Strategies.Repeat(grow, 3).Run(Start("sin(x)"), Context)!;
        Assert.AreEqual(3, state.Steps.Count);
        Assert.AreEqual(C("sin(8*x)"), state.Root);
        Assert.AreEqual(0, Strategies.Repeat(grow, 0).Run(Start("sin(x)"), Context)!.Steps.Count);
        Assert.IsNotNull(Strategies.Repeat(Strategies.Apply(Set(R("no", "cos(t_)", "1"))), 5).Run(Start("sin(x)"), Context));
    }

    [TestMethod]
    public void FixpointDetectsCyclesAndHonorsTheIterationCap()
    {
        var flip = Strategies.Apply(Set(R("sc", "sin(t_)", "cos(t_)"), R("cs", "cos(t_)", "sin(t_)")));
        var state = Strategies.Fixpoint(flip).Run(Start("sin(x)"), Context);
        Assert.IsNotNull(state);
        Assert.AreEqual(1, state.Steps.Count, "the step that would return to sin x is not taken");
        Assert.IsFalse(Context.HitIterationCap);

        var grow = Strategies.Fixpoint(Strategies.Apply(Set(R("grow", "sin(t_)", "sin(2*t_)"))));
        var capped = new RewriteContext(maxIterations: 5);
        Assert.AreEqual(5, grow.Run(Start("sin(x)"), capped)!.Steps.Count);
        Assert.IsTrue(capped.HitIterationCap);
        Assert.IsNull(Strategies.Fixpoint(Strategies.Apply(Set(R("no", "cos(t_)", "1")))).Run(Start("sin(x)"), Context));
    }

    [TestMethod]
    public void WhereRestrictsAStrategy()
    {
        var rule = Strategies.Apply(Set(R("sc", "sin(t_)", "cos(t_)")));
        var onlyProducts = Strategies.Where(e => e is Apply { Operator.Id: "mul" }, rule);
        Assert.IsNull(onlyProducts.Run(Start("sin(x)"), Context));
        Assert.IsNotNull(Strategies.Where(e => e is Apply, rule).Run(Start("sin(x)"), Context));
    }

    [TestMethod]
    public void AlgorithmsBecomeNamedSteps()
    {
        var doubling = Strategies.Algorithm("double", (node, _) => node is Symbol ? new AlgorithmResult(C("2*" + node), new EntryId("alg.t.double"), CurriculumLevel.PreAlgebra, Provisos.None, null, []) : null);
        var state = doubling.Run(Start("x"), Context)!;
        Assert.AreEqual(C("2*x"), state.Root);
        Assert.AreEqual("algorithm:double", state.Steps[0].RuleName);
        Assert.AreEqual("alg.t.double", state.Steps[0].Entry!.Value.Value);
        Assert.IsNull(doubling.Run(Start("x + 1"), Context));
    }

    [TestMethod]
    public void TheBudgetStopsRewriting()
    {
        var grow = Strategies.Fixpoint(Strategies.Apply(Set(R("grow", "sin(t_)", "sin(2*t_)"))));
        var budget = new Budget(maxSteps: 4);
        var outcome = RewriteEngine.Run(grow, Expr.Parse("sin(x)"), new RewriteContext(budget: budget));
        Assert.IsInstanceOfType<Outcome<Expr>.Partial>(outcome);
        Assert.IsTrue(budget.IsExceeded);
        Assert.IsTrue(((Derivation)((Outcome<Expr>.Partial)outcome).Steps!).Steps.Length <= 5);
    }

    [TestMethod]
    public void TheEngineNormalizesFirstAndPacksADerivation()
    {
        var rule = Strategies.Apply(Set(R("pyth", "sin(t_)^2 + cos(t_)^2", "1")));
        var outcome = RewriteEngine.Run(rule, Expr.Parse("cos(u)^2 + sin(u)^2 + 1 + 1"));
        var success = (Outcome<Expr>.Success)outcome;
        Assert.AreEqual(C("3"), success.Value);
        var derivation = (Derivation)success.Steps!;
        Assert.AreEqual(2, derivation.Steps.Length, "a normalize step, then the rule");
        Assert.AreEqual("normalize", derivation.Steps[0].RuleName);
        Assert.IsNull(derivation.Steps[0].Entry);
        Assert.AreEqual(Expr.Parse("cos(u)^2 + sin(u)^2 + 1 + 1"), derivation.Start);
        Assert.AreEqual(success.Value, derivation.End);
        for (var i = 1; i < derivation.Steps.Length; i++) Assert.AreEqual(derivation.Steps[i - 1].After, derivation.Steps[i].Before);

        var unevaluated = RewriteEngine.Run(rule, Expr.Parse("sin(u)"));
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(unevaluated);
    }

    [TestMethod]
    public void DerivationsFlattenChainAndReplay()
    {
        var inner = new Derivation(C("a"), C("b"), [new Step(null, "inner", C("a"), C("b"), ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of("inner"), CurriculumLevel.Arithmetic, null)]);
        var outer = new Step(null, "outer", C("x"), C("y"), ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of("outer"), CurriculumLevel.Arithmetic, inner);
        var second = new Step(null, "second", C("y"), C("z"), ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of("second"), CurriculumLevel.Arithmetic, null);
        var derivation = new Derivation(C("x"), C("z"), [outer, second]);
        CollectionAssert.AreEqual(new[] { "outer", "inner", "second" }, derivation.Flatten().Select(s => s.RuleName).ToArray());

        var replayer = new TableReplayer(new() { ["outer"] = (C("x"), C("y")), ["second"] = (C("y"), C("z")) });
        Assert.AreEqual(C("z"), ((Outcome<Expr>.Success)derivation.Replay(MathContext.Default, replayer)).Value);

        var wrong = new Derivation(C("x"), C("z"), [outer, second with { After = C("w") }]);
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(wrong.Replay(MathContext.Default, replayer));
        var broken = new Derivation(C("x"), C("z"), [outer, second with { Before = C("q") }]);
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(broken.Replay(MathContext.Default, replayer));
        var wrongEnd = new Derivation(C("x"), C("w"), [outer, second]);
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(wrongEnd.Replay(MathContext.Default, replayer));
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(derivation.Replay(MathContext.Default, new TableReplayer([])), "an unknown step cannot be re-applied");

        var joined = new Derivation(C("x"), C("y"), [outer]).Then(new Derivation(C("y"), C("z"), [second]));
        Assert.AreEqual(2, joined.Steps.Length);
        Assert.ThrowsExactly<ArgumentException>(() => new Derivation(C("x"), C("y"), [outer]).Then(new Derivation(C("q"), C("z"), [second])));
        Assert.AreEqual(0, Derivation.Empty(C("x")).Steps.Length);
    }

    private sealed class TableReplayer(Dictionary<string, (Expr From, Expr To)> table) : IStepReplayer
    {
        public Expr? Replay(Step step, Expr current, MathContext context) => table.TryGetValue(step.RuleName, out var t) && t.From.Equals(current) ? t.To : null;
    }

    [TestMethod]
    public void RuleIndexSelectsCandidatesByHeadAndArguments()
    {
        var rules = new[]
        {
            R("a", "sin(t_)", "cos(t_)"),
            R("b", "cos(t_)", "sin(t_)"),
            R("c", "t_^2 + 1", "t_"),
            R("d", "sin(t_)^2 + cos(t_)^2", "1"),
            R("e", "a_*x^n_", "a_", null, ("a", new WildOptions(Default: Number(1))), ("n", new WildOptions(Default: Number(1)))),
        };
        var index = new RuleIndex(rules);
        Assert.AreEqual(5, index.Count);
        Assert.AreEqual("alg.t.a,alg.t.e", string.Join(",", index.Candidates(C("sin(x)")).Select(r => r.Name)), "the optional rule is always a candidate");
        Assert.AreEqual("alg.t.c,alg.t.d,alg.t.e", string.Join(",", index.Candidates(C("y^2 + 1")).Select(r => r.Name)), "the index is coarse: it looks at argument heads, not at repeated structure");
        Assert.AreEqual("alg.t.d,alg.t.e", string.Join(",", index.Candidates(C("y^2 + 2")).Select(r => r.Name)));
        Assert.IsTrue(index.Candidates(C("sin(x)^2 + cos(x)^2")).Any(r => r.Name == "alg.t.d"));
        Assert.AreEqual(1, new RuleSet("s", rules).Rules.Count(r => r.Name == "alg.t.e"));
        Assert.AreEqual(5, RuleSet.Union("u", new RuleSet("p", rules), new RuleSet("q", rules)).Rules.Length);
    }

    [TestMethod]
    public void RuleMatchesCarryTheReplacement()
    {
        var rule = R("pyth", "sin(t_)^2 + cos(t_)^2", "1");
        var matches = rule.Matches(C("sin(u)^2 + cos(u)^2 + 5"), MathContext.Default).ToList();
        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(C("1 + 5"), Normalizer.Canonical(matches[0].Replacement));
        Assert.AreEqual(Provisos.None, matches[0].Provisos);
        Assert.AreEqual("alg.t.pyth: sin(t_)^2 + cos(t_)^2 → 1".Replace("sin(t_)^2 + cos(t_)^2", rule.Lhs.ToString(), StringComparison.Ordinal), rule.ToString());
        Assert.ThrowsExactly<ArgumentNullException>(() => rule.Matches(null!, MathContext.Default).ToList());
    }
}
