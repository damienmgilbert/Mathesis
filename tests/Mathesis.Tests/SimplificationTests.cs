using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class SimplificationTests
{
    private const int Points = 20;

    private static IReadOnlyList<string> Corpus() =>
        [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "corpus", "simplify.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))];

    // FNV-1a: string.GetHashCode is randomized per process.
    private static int StableSeed(string text)
    {
        var h = 2166136261u;
        foreach (var c in text) h = (h ^ c) * 16777619u;
        return (int)(h & 0x7FFFFFFF);
    }

    private static double? Eval(Expr e, Dictionary<Symbol, double> values) =>
        Evaluator.N(e, values) is Outcome<double>.Success { Value: var v } && double.IsFinite(v) ? v : null;

    // Whether a proviso (a relation between two expressions) holds at the point; null when it cannot be evaluated.
    private static bool? Holds(Expr proviso, Dictionary<Symbol, double> values)
    {
        if (proviso is not Apply { Operator: var op, Arguments: [var l, var r] }) return null;
        if (Eval(l, values) is not { } a || Eval(r, values) is not { } b) return null;
        if (op == Operators.Ne) return Math.Abs(a - b) > 1e-9;
        if (op == Operators.Gt) return a > b + 1e-9;
        if (op == Operators.Ge) return a >= b - 1e-9;
        if (op == Operators.Lt) return a < b - 1e-9;
        if (op == Operators.Le) return a <= b + 1e-9;
        return null;
    }

    // "expression @ fact, fact": the facts are assumed, and sampled points must satisfy them.
    private static (Expr Expression, MathContext Math, Expr[] Facts) ParseCase(string text)
    {
        var parts = text.Split('@', 2);
        var facts = parts.Length == 1 ? [] : parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(f => Expr.Parse(f)).ToArray();
        var math = MathContext.Default;
        foreach (var fact in facts) math = math.Assume(fact);
        return (Expr.Parse(parts[0]), math, facts);
    }

    [TestMethod]
    public void CorpusHasAtLeast200Cases() => Assert.IsGreaterThanOrEqualTo(200, Corpus().Count);

    [TestMethod]
    public void CorpusSimplificationsAgreeReplayAndCiteTheCatalog()
    {
        var failures = new List<string>();
        var catalog = KnowledgeBase.Default;
        foreach (var text in Corpus())
        {
            var (original, math, facts) = ParseCase(text);
            var outcome = Simplifier.Simplify(original, math, null, new Budget(maxSteps: 20_000, maxTime: TimeSpan.FromSeconds(20)));
            if (outcome is not Outcome<Expr>.Success { Value: var simplified, Steps: Derivation derivation } success)
            {
                failures.Add($"{text}: {outcome.GetType().Name}");
                continue;
            }

            // Replaying the recorded steps reproduces the result exactly.
            if (derivation.Replay(math, StepReplayer.Default) is not Outcome<Expr>.Success { Value: var replayed } || !replayed.Equals(simplified))
            {
                failures.Add($"{text}: replay differs");
            }

            // Every step but automatic normalization cites an existing catalog entry.
            foreach (var step in derivation.Flatten())
            {
                if (step.RuleName == "normalize") continue;
                if (step.Entry is not { } id || !catalog.TryGet(id.Value, out _)) failures.Add($"{text}: step {step.RuleName} cites no catalog entry");
            }

            // Agreement at seeded points inside the domain of the original and the provisos of the steps.
            var symbols = original.FreeSymbols.Union(facts.SelectMany(f => f.FreeSymbols)).OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
            var random = new Random(StableSeed(text));
            var provisos = success.Provisos.OfType<Expr>().ToArray();
            var agreed = 0;
            for (var attempt = 0; attempt < 400 && agreed < Points; attempt++)
            {
                var values = symbols.ToDictionary(s => s, _ => Math.Round((random.NextDouble() * 6 - 3) * 8) / 8 + 0.0625);
                if (Eval(original, values) is not { } before) continue;
                if (provisos.Concat(facts).Any(p => Holds(p, values) != true)) continue;
                if (Eval(simplified, values) is not { } after)
                {
                    failures.Add($"{text}: {simplified} undefined where the original is defined");
                    break;
                }
                if (Math.Abs(before - after) > 1e-8 * Math.Max(1, Math.Abs(before)))
                {
                    failures.Add($"{text}: {simplified} differs ({before} vs {after}) at {string.Join(", ", values.Select(kv => $"{kv.Key.Name}={kv.Value}"))}; steps: {string.Join(" ; ", derivation.Steps.Select(st => $"{st.RuleName}: {st.After}"))}");
                    break;
                }
                agreed++;
            }
            if (agreed < Points && !failures.Any(f => f.StartsWith(text + ":", StringComparison.Ordinal))) failures.Add($"{text}: only {agreed} points were inside the domain");
        }
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "corpus-failures.txt"), failures);
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(40)));
    }

    [TestMethod]
    public void NoRuleSetLoopsOnTheCorpus()
    {
        var problems = new List<string>();
        var cases = Corpus().Select(ParseCase).ToList();
        foreach (var tag in RuleLibrary.Default.Tags.Order(StringComparer.Ordinal))
        {
            var strategy = Strategies.Fixpoint(Strategies.BottomUp(Strategies.Apply(RuleLibrary.Default[tag])));
            foreach (var (expression, math, _) in cases)
            {
                var context = new RewriteContext(math, new Budget(maxSteps: 5_000, maxTime: TimeSpan.FromSeconds(5)), maxIterations: 100);
                RewriteEngine.Run(strategy, expression, context);
                if (context.HitCycle || context.HitIterationCap || context.Budget.IsExceeded)
                {
                    problems.Add($"{tag} on {expression}: {(context.HitCycle ? "cycle" : context.HitIterationCap ? "iteration cap" : "budget")}");
                    break;
                }
            }
        }
        Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void BareVariableInTwoPlacesIsNotOptional()
    {
        // (a + b)*(a - b) must not match x*(x - 1) with b defaulting to 1 in the sum and the product.
        var outcome = Transforms.Expand.Run(Expr.Parse("x*(x - 1)"));
        var value = outcome is Outcome<Expr>.Success { Value: var v } ? v : Expr.Parse("x*(x - 1)");
        Assert.AreEqual(Normalizer.Canonical(Expr.Parse("x^2 - x")), value);
        Assert.IsFalse(RuleLibrary.Default.TryGetRule("alg.poly.sum-times-diff", out var rule) && rule.Lhs.Options.Values.Any(o => o.IsOptional));
    }
}
