using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Patterns;

namespace Mathesis.Symbolics.Rewriting;

/// <summary>What a strategy runs under: the mathematical context, the budget and how rule guards are decided.</summary>
public sealed class RewriteContext
{
    /// <summary>Creates a context.</summary>
    /// <param name="math">Number field and assumptions.</param>
    /// <param name="budget">Limits the work; <see cref="Budget.Unlimited"/> when <c>null</c>.</param>
    /// <param name="mode">How guards are decided when the assumptions cannot answer.</param>
    /// <param name="match">Limits of one match.</param>
    /// <param name="maxIterations">The most iterations a <see cref="Strategies.Fixpoint"/> or <see cref="Strategies.Repeat"/> runs.</param>
    public RewriteContext(MathContext? math = null, Budget? budget = null, ProvisoMode mode = ProvisoMode.Default, MatchOptions? match = null, int maxIterations = 200)
    {
        Math = math ?? MathContext.Default;
        Budget = budget ?? Budget.Unlimited;
        Mode = mode;
        Match = match;
        MaxIterations = maxIterations;
    }

    /// <summary>The mathematical context.</summary>
    public MathContext Math { get; }

    /// <summary>The budget.</summary>
    public Budget Budget { get; }

    /// <summary>How guards are decided.</summary>
    public ProvisoMode Mode { get; }

    /// <summary>Limits of one match.</summary>
    public MatchOptions? Match { get; }

    /// <summary>The iteration cap of loops.</summary>
    public int MaxIterations { get; }

    /// <summary>Rules and algorithms citing entries above this curriculum level are not used (<see cref="MathContext.Level"/>), or <c>null</c> for no limit.</summary>
    public CurriculumLevel? MaxLevel => Math.Level;

    /// <summary>Set when a loop stopped at the iteration cap instead of at a fixed point.</summary>
    public bool HitIterationCap { get; internal set; }

    /// <summary>Set when a loop stopped because an expression reappeared (a rewrite cycle).</summary>
    public bool HitCycle { get; internal set; }

    /// <summary>Normalizes to the Canonical level in this context's number field.</summary>
    public Expr Normalize(Expr e) => Normalizer.Canonical(e, Math.NormalizeOptions);
}

/// <summary>An expression being rewritten: the whole expression, the node a strategy works on, and the steps taken so far.</summary>
/// <param name="Root">The whole expression (always Canonical).</param>
/// <param name="Focus">The path of the node the next strategy applies to.</param>
/// <param name="Steps">The steps taken.</param>
public sealed record RewriteState(Expr Root, ExprPath Focus, ImmutableList<Step> Steps)
{
    /// <summary>The node at <see cref="Focus"/>.</summary>
    public Expr Current => Focus.Get(Root);

    /// <summary>The same state focused elsewhere.</summary>
    public RewriteState At(ExprPath focus) => this with { Focus = focus };
}

/// <summary>What an algorithm produces for a node.</summary>
/// <param name="Replacement">The expression that replaces the node.</param>
/// <param name="Entry">The catalog entry the algorithm implements, if any.</param>
/// <param name="Level">The curriculum level of that entry.</param>
/// <param name="Provisos">Conditions the algorithm assumed.</param>
/// <param name="Substeps">The algorithm's own derivation, if it has one.</param>
/// <param name="Bindings">Named values the explanation mentions.</param>
public sealed record AlgorithmResult(Expr Replacement, EntryId? Entry, CurriculumLevel Level, Provisos Provisos, Derivation? Substeps, ImmutableArray<KeyValuePair<string, Expr>> Bindings);

/// <summary>A specialized algorithm used as a rewrite step (polynomial factoring, partial fractions, …).</summary>
/// <param name="node">The node to rewrite.</param>
/// <param name="context">The rewrite context.</param>
/// <returns>The result, or <c>null</c> if the algorithm does not apply.</returns>
public delegate AlgorithmResult? AlgorithmFunction(Expr node, RewriteContext context);

/// <summary>
/// A composable rewrite strategy in the style of Stratego and ELAN (docs/design/07-engines.md, "Rewrite strategies"). Running a strategy on a
/// state returns the new state or <c>null</c> when the strategy does not apply.
/// </summary>
public abstract class Strategy
{
    /// <summary>Runs the strategy at <paramref name="state"/>'s focus.</summary>
    public abstract RewriteState? Run(RewriteState state, RewriteContext context);

    /// <summary>A name for diagnostics.</summary>
    public override string ToString() => GetType().Name;

    /// <summary>
    /// Replaces the focus by <paramref name="replacement"/>, normalizes the whole expression and appends a step. Returns <c>null</c> when the
    /// budget is exhausted or the step changes nothing.
    /// </summary>
    internal static RewriteState? Commit(RewriteState state, RewriteContext context, string ruleName, EntryId? entry, Expr replacement, Bindings bindings, Provisos provisos, CurriculumLevel level, Derivation? substeps, ImmutableArray<KeyValuePair<string, Expr>> arguments)
    {
        if (!context.Budget.TryCharge()) return null;
        var newRoot = context.Normalize(state.Focus.Replace(state.Root, replacement));
        if (newRoot.Equals(state.Root)) return null;
        if (!context.Budget.AllowsSize(newRoot.LeafCount)) return null;
        var step = new Step(entry, ruleName, state.Root, newRoot, state.Focus, bindings, provisos, new ExplanationKey(entry?.Value ?? ruleName, arguments), level, substeps);
        return state with { Root = newRoot, Steps = state.Steps.Add(step) };
    }
}

/// <summary>The strategy combinators.</summary>
public static class Strategies
{
    /// <summary>Tries the rules of <paramref name="rules"/> at the focus once; the first rule whose pattern matches and whose guard holds fires.</summary>
    public static Strategy Apply(RuleSet rules) => new ApplyRules(rules);

    /// <summary>Runs <paramref name="first"/> then <paramref name="second"/>; fails if either fails.</summary>
    public static Strategy Seq(Strategy first, Strategy second) => new SeqStrategy(first, second);

    /// <summary>Runs <paramref name="first"/>; if it fails runs <paramref name="second"/>.</summary>
    public static Strategy Choice(Strategy first, Strategy second) => new ChoiceStrategy(first, second);

    /// <summary>Runs <paramref name="s"/>; if it fails leaves the state unchanged.</summary>
    public static Strategy Try(Strategy s) => new TryStrategy(s);

    /// <summary>Applies <paramref name="s"/> at the first node, in pre-order from the focus, where it succeeds (once).</summary>
    public static Strategy TopDown(Strategy s) => new Traverse(s, preOrder: true);

    /// <summary>Applies <paramref name="s"/> at the first node, in post-order from the focus, where it succeeds (once).</summary>
    public static Strategy BottomUp(Strategy s) => new Traverse(s, preOrder: false);

    /// <summary>Applies <paramref name="s"/> from the leaves repeatedly until nothing changes; fails if it never applied.</summary>
    public static Strategy Innermost(Strategy s) => Fixpoint(BottomUp(s));

    /// <summary>Applies <paramref name="s"/> from the root repeatedly until nothing changes; fails if it never applied.</summary>
    public static Strategy Outermost(Strategy s) => Fixpoint(TopDown(s));

    /// <summary>Applies <paramref name="s"/> until it fails or <paramref name="max"/> applications were made; always succeeds.</summary>
    public static Strategy Repeat(Strategy s, int max) => new RepeatStrategy(s, max);

    /// <summary>
    /// Applies <paramref name="s"/> until it fails, under the context's budget and iteration cap, stopping early when an expression
    /// reappears (a cycle). Fails if <paramref name="s"/> never applied.
    /// </summary>
    public static Strategy Fixpoint(Strategy s) => new FixpointStrategy(s);

    /// <summary>Applies <paramref name="s"/> only where <paramref name="predicate"/> holds for the focus node.</summary>
    public static Strategy Where(Func<Expr, bool> predicate, Strategy s) => new WhereStrategy(predicate, s);

    /// <summary>Calls a specialized algorithm at the focus; its result becomes a step named <c>algorithm:name</c>.</summary>
    public static Strategy Algorithm(string name, AlgorithmFunction function) => new AlgorithmStrategy(name, function);

    private sealed class ApplyRules(RuleSet rules) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context)
        {
            var node = state.Current;
            foreach (var rule in rules.Index.Candidates(node))
            {
                if (context.MaxLevel is { } max && rule.Level > max) continue;
                foreach (var match in rule.Matches(node, context.Math, context.Mode, context.Match))
                {
                    var arguments = ImmutableArray.CreateRange(match.Bindings.Select(b => b));
                    var next = Commit(state, context, rule.Name, rule.Entry, match.Replacement, match.Bindings, match.Provisos, rule.Level, null, arguments);
                    if (next is not null) return next;
                    if (context.Budget.IsExceeded) return null;
                }
            }
            return null;
        }

        public override string ToString() => $"Apply({rules.Name})";
    }

    private sealed class SeqStrategy(Strategy first, Strategy second) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context) =>
            first.Run(state, context) is { } mid ? second.Run(mid, context) : null;

        public override string ToString() => $"Seq({first}, {second})";
    }

    private sealed class ChoiceStrategy(Strategy first, Strategy second) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context) => first.Run(state, context) ?? second.Run(state, context);

        public override string ToString() => $"Choice({first}, {second})";
    }

    private sealed class TryStrategy(Strategy s) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context) => s.Run(state, context) ?? state;

        public override string ToString() => $"Try({s})";
    }

    private sealed class WhereStrategy(Func<Expr, bool> predicate, Strategy s) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context) => predicate(state.Current) ? s.Run(state, context) : null;

        public override string ToString() => $"Where({s})";
    }

    private sealed class Traverse(Strategy s, bool preOrder) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context)
        {
            var paths = new List<ExprPath>();
            Collect(state.Current, state.Focus, paths);
            foreach (var path in paths)
            {
                if (context.Budget.IsExceeded) return null;
                if (s.Run(state.At(path), context) is { } next) return next.At(state.Focus);
            }
            return null;
        }

        private void Collect(Expr node, ExprPath path, List<ExprPath> into)
        {
            if (preOrder) into.Add(path);
            var children = node.Children;
            for (var i = 0; i < children.Length; i++) Collect(children[i], path.Child(i), into);
            if (!preOrder) into.Add(path);
        }

        public override string ToString() => $"{(preOrder ? "TopDown" : "BottomUp")}({s})";
    }

    private sealed class RepeatStrategy(Strategy s, int max) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context)
        {
            var current = state;
            for (var i = 0; i < max && !context.Budget.IsExceeded; i++)
            {
                if (s.Run(current, context) is not { } next) break;
                current = next;
            }
            return current;
        }

        public override string ToString() => $"Repeat({s}, {max})";
    }

    private sealed class FixpointStrategy(Strategy s) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context)
        {
            var current = state;
            var seen = new HashSet<Expr> { state.Root };
            var applied = false;
            for (var i = 0; i < context.MaxIterations; i++)
            {
                if (context.Budget.IsExceeded) return applied ? current : null;
                if (s.Run(current, context) is not { } next) return applied ? current : null;

                // A repeated expression is a cycle: stop without taking the step that closes it.
                if (!seen.Add(next.Root))
                {
                    context.HitCycle = true;
                    return applied ? current : null;
                }
                current = next;
                applied = true;
            }
            context.HitIterationCap = true;
            return current;
        }

        public override string ToString() => $"Fixpoint({s})";
    }

    private sealed class AlgorithmStrategy(string name, AlgorithmFunction function) : Strategy
    {
        public override RewriteState? Run(RewriteState state, RewriteContext context)
        {
            if (function(state.Current, context) is not { } result) return null;
            if (context.MaxLevel is { } max && result.Level > max) return null;
            return Commit(state, context, "algorithm:" + name, result.Entry, result.Replacement, Bindings.Empty, result.Provisos, result.Level, result.Substeps, result.Bindings);
        }

        public override string ToString() => $"Algorithm({name})";
    }
}

/// <summary>Runs strategies on expressions and returns results with their derivations.</summary>
public static class RewriteEngine
{
    /// <summary>
    /// Runs <paramref name="strategy"/> on <paramref name="start"/> (normalized to Canonical first; the normalization is the first step when it
    /// changes something). The outcome carries the <see cref="Derivation"/> as its steps and the provisos the steps added.
    /// </summary>
    /// <returns>
    /// Success with the result; Unevaluated when the strategy did not apply (the input is returned unchanged inside the reason's original);
    /// Partial when the budget ran out part-way.
    /// </returns>
    public static Outcome<Expr> Run(Strategy strategy, Expr start, RewriteContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(start);
        context ??= new RewriteContext();
        var steps = ImmutableList<Step>.Empty;
        var canonical = context.Normalize(start);
        if (!canonical.Equals(start))
        {
            steps = steps.Add(new Step(null, "normalize", start, canonical, ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of("normalize"), CurriculumLevel.Arithmetic, null));
        }
        var initial = new RewriteState(canonical, ExprPath.Root, steps);
        var result = strategy.Run(initial, context);
        if (result is null) return new Outcome<Expr>.Unevaluated(canonical, context.Budget.IsExceeded ? (context.Budget.ExceededReason ?? "Budget exceeded.") : $"The strategy {strategy} did not apply.");
        return Finish(start, result, context);
    }

    /// <summary>Packs a final state as an outcome.</summary>
    public static Outcome<Expr> Finish(Expr start, RewriteState result, RewriteContext context)
    {
        var derivation = new Derivation(start, result.Root, [.. result.Steps]);
        var provisos = Provisos.None;
        foreach (var step in result.Steps) provisos = provisos.Union(step.Added);
        return context.Budget.IsExceeded
            ? new Outcome<Expr>.Partial(result.Root, context.Budget.ExceededReason ?? "Budget exceeded.", derivation, provisos)
            : Outcome.Ok(result.Root, derivation, provisos, Verification.NotChecked);
    }
}
