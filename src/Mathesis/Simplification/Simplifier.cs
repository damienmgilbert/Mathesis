using System.Collections.Immutable;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Patterns;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Simplification;

/// <summary>Scores an expression; the simplifier looks for the form with the lowest score.</summary>
public interface IComplexityMeasure
{
    /// <summary>The score of <paramref name="expr"/>: lower is simpler.</summary>
    double Measure(Expr expr);
}

/// <summary>
/// The default measure (docs/design/07-engines.md, "Simplification"): number 1, symbol 1, <c>+</c> and <c>·</c> 1 per operand beyond the first,
/// <c>^</c> 2, elementary function 3, other functions 5, a nested radical 5, plus a penalty for negative exponents and a cost for the size of
/// the digits of rational numbers.
/// </summary>
public sealed class DefaultComplexityMeasure : IComplexityMeasure
{
    /// <summary>The shared instance.</summary>
    public static DefaultComplexityMeasure Instance { get; } = new();

    private static readonly HashSet<string> Elementary = new(StringComparer.Ordinal)
    {
        "sin", "cos", "tan", "cot", "sec", "csc", "exp", "ln", "log", "abs", "sign", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "root", "sqrt",
    };

    /// <inheritdoc />
    public double Measure(Expr expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        return Score(expr, insideRadical: false);
    }

    private static double Score(Expr e, bool insideRadical)
    {
        switch (e)
        {
            case Number n:
                return n.Value.IsInteger ? 1 : 2;
            case Symbol or Constant or Wild:
                return 1;
            case Apply { Operator: var op, Arguments: var args }:
                var radical = op == Operators.Pow && args[1] is Number { Value.IsInteger: false };
                var children = args.Sum(a => Score(a, insideRadical || radical));
                if (op == Operators.Add || op == Operators.Mul) return children + (args.Length - 1);
                if (op == Operators.Pow)
                {
                    var negative = args[1] is Number { Value.Sign: < 0 } ? 1 : 0;
                    var nested = radical && insideRadical ? 5 : 0;
                    return children + 2 + negative + nested;
                }
                return children + (Elementary.Contains(op.Id) ? 3 : 5);
            default:
                return e.Children.Sum(c => Score(c, insideRadical)) + 1;
        }
    }
}

/// <summary>Options of <see cref="Simplifier"/>.</summary>
public sealed record SimplifyOptions
{
    /// <summary>Candidates kept per search level.</summary>
    public int BeamWidth { get; init; } = 8;

    /// <summary>The most transform applications on one path.</summary>
    public int MaxDepth { get; init; } = 6;

    /// <summary>Levels without a better form after which the search stops (a rise, such as an expansion, may precede a fall).</summary>
    public int Patience { get; init; } = 2;

    /// <summary>The measure to minimize.</summary>
    public IComplexityMeasure Measure { get; init; } = DefaultComplexityMeasure.Instance;

    /// <summary>The transforms to search over.</summary>
    public ImmutableArray<Transform> Transforms { get; init; } = Simplification.Transforms.Default;

    /// <summary>How rule guards that the assumptions cannot decide are treated.</summary>
    public ProvisoMode Mode { get; init; } = ProvisoMode.Default;
}

/// <summary>Budgeted best-first search over transforms (docs/design/07-engines.md, "Simplification").</summary>
public static class Simplifier
{
    private sealed record Node(Expr Expr, ImmutableList<Step> Steps, double Cost);

    /// <summary>Finds the simplest form of <paramref name="expr"/> reachable by the transforms, with the derivation that reaches it.</summary>
    /// <returns>Success with the derivation; Partial with the best form so far when the budget runs out.</returns>
    public static Outcome<Expr> Simplify(Expr expr, MathContext? math = null, SimplifyOptions? options = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(expr);
        options ??= new SimplifyOptions();
        var context = new RewriteContext(math, budget, options.Mode);
        var start = context.Normalize(expr);
        var steps = ImmutableList<Step>.Empty;
        if (!start.Equals(expr)) steps = steps.Add(new Step(null, "normalize", expr, start, ExprPath.Root, Bindings.Empty, Provisos.None, ExplanationKey.Of("normalize"), Mathesis.Knowledge.CurriculumLevel.Arithmetic, null));

        var first = Apply(Transforms.Cleanup, new Node(start, steps, options.Measure.Measure(start)), options, context);
        var root = first ?? new Node(start, steps, options.Measure.Measure(start));
        var best = root;
        var seen = new HashSet<Expr> { root.Expr };
        var beam = new List<Node> { root };
        var stale = 0;

        for (var depth = 0; depth < options.MaxDepth && beam.Count > 0 && stale < options.Patience && !context.Budget.IsExceeded; depth++)
        {
            var next = new List<Node>();
            foreach (var node in beam)
            {
                foreach (var transform in options.Transforms)
                {
                    if (context.Budget.IsExceeded) break;
                    var candidate = Apply(transform, node, options, context);
                    if (candidate is null || !seen.Add(candidate.Expr)) continue;
                    next.Add(candidate);
                }
            }
            beam = [.. next.OrderBy(n => n.Cost).ThenBy(n => n.Steps.Count).Take(options.BeamWidth)];
            if (beam.Count > 0 && Better(beam[0], best))
            {
                best = beam[0];
                stale = 0;
            }
            else
            {
                stale++;
            }
        }

        var derivation = new Derivation(expr, best.Expr, [.. best.Steps]);
        var provisos = Provisos.None;
        foreach (var step in best.Steps) provisos = provisos.Union(step.Added);
        return context.Budget.IsExceeded
            ? new Outcome<Expr>.Partial(best.Expr, context.Budget.ExceededReason ?? "Budget exceeded.", derivation, provisos)
            : Outcome.Ok(best.Expr, derivation, provisos, Verification.NotChecked);
    }

    private static bool Better(Node a, Node b) => a.Cost < b.Cost || (a.Cost == b.Cost && a.Steps.Count < b.Steps.Count);

    // A transform applied to a node; its result is cleaned up so the search compares simplified candidates.
    private static Node? Apply(Transform transform, Node node, SimplifyOptions options, RewriteContext context)
    {
        if (transform.Run(node.Expr, context) is not Outcome<Expr>.Success { Value: var value, Steps: Derivation d } || value.Equals(node.Expr)) return null;
        var steps = node.Steps.AddRange(d.Steps);
        if (!ReferenceEquals(transform, Transforms.Cleanup) && Transforms.Cleanup.Run(value, context) is Outcome<Expr>.Success { Value: var cleaned, Steps: Derivation c })
        {
            value = cleaned;
            steps = steps.AddRange(c.Steps);
        }
        return new Node(value, steps, options.Measure.Measure(value));
    }
}

/// <summary>Replays recorded steps by re-applying the catalog rule or algorithm each one names.</summary>
public sealed class StepReplayer : IStepReplayer
{
    /// <summary>The shared instance, over <see cref="RuleLibrary.Default"/>.</summary>
    public static StepReplayer Default { get; } = new(RuleLibrary.Default);

    private readonly RuleLibrary _library;

    /// <summary>Creates a replayer over <paramref name="library"/>.</summary>
    public StepReplayer(RuleLibrary library) => _library = library ?? throw new ArgumentNullException(nameof(library));

    /// <inheritdoc />
    public Expr? Replay(Step step, Expr current, MathContext context)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(context);
        var normalize = new RewriteContext(context);
        if (step.RuleName == "normalize") return normalize.Normalize(current);
        var node = current;
        foreach (var index in step.Path.Indices)
        {
            if ((uint)index >= (uint)node.Children.Length) return null;
            node = node.Children[index];
        }

        Expr replacement;
        if (step.RuleName.StartsWith("algorithm:", StringComparison.Ordinal))
        {
            var name = step.RuleName["algorithm:".Length..];
            if (!Transforms.Algorithms.TryGetValue(name, out var algorithm) || algorithm(node, normalize) is not { } result) return null;
            replacement = result.Replacement;
        }
        else
        {
            if (!_library.TryGetRule(step.RuleName, out var rule)) return null;
            replacement = Matcher.Instantiate(rule.Rhs, step.Bindings, rule.Lhs);
            if (!step.Bindings.Leftover.IsDefaultOrEmpty && rule.Head is { } head) replacement = new Apply(head, [replacement, .. step.Bindings.Leftover]);
        }
        return normalize.Normalize(step.Path.Replace(current, replacement));
    }
}
