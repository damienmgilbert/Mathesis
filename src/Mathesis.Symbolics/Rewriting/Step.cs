using System.Collections.Immutable;
using Mathesis.Knowledge;
using Mathesis.Symbolics.Patterns;

namespace Mathesis.Symbolics.Rewriting;

/// <summary>A key into the explanation templates plus the expressions that fill them in (docs/design/04-type-system.md, "Derivations and steps").</summary>
/// <param name="Key">The template key: the catalog entry ID, or the name of the transform for steps that cite none.</param>
/// <param name="Arguments">The named expressions the template mentions (pattern variables and their values).</param>
public sealed record ExplanationKey(string Key, ImmutableArray<KeyValuePair<string, Expr>> Arguments)
{
    /// <summary>A key with no arguments.</summary>
    public static ExplanationKey Of(string key) => new(key, []);
}

/// <summary>
/// One application of a rule or algorithm. <see cref="Before"/> and <see cref="After"/> are the whole expression, so consecutive steps of a
/// derivation chain (<c>steps[i].After == steps[i + 1].Before</c>); <see cref="Path"/> locates the rewritten subexpression in
/// <see cref="Before"/>.
/// </summary>
/// <param name="Entry">The catalog entry applied (law, theorem, method), if any; automatic normalization cites none.</param>
/// <param name="RuleName">The rule that fired: the entry ID (with <c>~rtl</c> for a rule read right to left), <c>algorithm:name</c>, or <c>normalize</c>.</param>
/// <param name="Before">The whole expression before the step.</param>
/// <param name="After">The whole expression after the step (Canonical).</param>
/// <param name="Path">Where in <paramref name="Before"/> the rewrite happened.</param>
/// <param name="Bindings">The pattern variables and their values.</param>
/// <param name="Added">Conditions this step introduced, for example <c>x ≠ 0</c>.</param>
/// <param name="Explanation">The explanation template key and arguments.</param>
/// <param name="Level">The curriculum level of the cited entry.</param>
/// <param name="Substeps">Nested detail, such as the inner derivative of a chain rule, or the steps of a method.</param>
public sealed record Step(
    EntryId? Entry,
    string RuleName,
    Expr Before,
    Expr After,
    ExprPath Path,
    Bindings Bindings,
    Provisos Added,
    ExplanationKey Explanation,
    CurriculumLevel Level,
    Derivation? Substeps);

/// <summary>Reproduces one step: given the expression before it, returns the expression after it, or <c>null</c> if the step does not apply.</summary>
public interface IStepReplayer
{
    /// <summary>Re-applies <paramref name="step"/> to <paramref name="current"/> (which equals <c>step.Before</c>).</summary>
    Expr? Replay(Step step, Expr current, MathContext context);
}

/// <summary>A recorded sequence of steps from <see cref="Start"/> to <see cref="End"/>.</summary>
/// <param name="Start">The expression the derivation starts from.</param>
/// <param name="End">The expression it ends at.</param>
/// <param name="Steps">The steps, in order.</param>
public sealed record Derivation(Expr Start, Expr End, ImmutableArray<Step> Steps) : IDerivation
{
    /// <summary>A derivation with no steps.</summary>
    public static Derivation Empty(Expr e) => new(e, e, []);

    /// <summary>The steps and, depth first, the steps nested inside them.</summary>
    public IEnumerable<Step> Flatten()
    {
        foreach (var step in Steps)
        {
            yield return step;
            if (step.Substeps is { } inner)
            {
                foreach (var s in inner.Flatten()) yield return s;
            }
        }
    }

    /// <summary>This derivation followed by <paramref name="next"/>, which must start where this one ends.</summary>
    /// <exception cref="ArgumentException"><paramref name="next"/> does not start at <see cref="End"/>.</exception>
    public Derivation Then(Derivation next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!End.Equals(next.Start)) throw new ArgumentException("The second derivation does not start where the first ends.", nameof(next));
        return new(Start, next.End, [.. Steps, .. next.Steps]);
    }

    /// <summary>
    /// Re-applies every step with <paramref name="replayer"/> and checks that each reproduces its recorded result exactly and that the steps
    /// chain from <see cref="Start"/> to <see cref="End"/>.
    /// </summary>
    /// <returns>The final expression, or a failure naming the first step that did not replay.</returns>
    public Outcome<Expr> Replay(MathContext context, IStepReplayer replayer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(replayer);
        var current = Start;
        for (var i = 0; i < Steps.Length; i++)
        {
            var step = Steps[i];
            if (!current.Equals(step.Before)) return Outcome.Fail<Expr>(new MathError(MathErrorKind.Domain, $"Step {i + 1} ({step.RuleName}) does not start where step {i} ended.", step.Before));
            var next = replayer.Replay(step, current, context);
            if (next is null) return Outcome.Fail<Expr>(new MathError(MathErrorKind.Domain, $"Step {i + 1} ({step.RuleName}) could not be re-applied.", step.Before));
            if (!next.Equals(step.After)) return Outcome.Fail<Expr>(new MathError(MathErrorKind.Domain, $"Step {i + 1} ({step.RuleName}) produced {next} instead of the recorded {step.After}.", step.After));
            current = next;
        }
        return current.Equals(End)
            ? Outcome.Ok(current)
            : Outcome.Fail<Expr>(new MathError(MathErrorKind.Domain, "The steps do not end at the recorded result.", End));
    }
}
