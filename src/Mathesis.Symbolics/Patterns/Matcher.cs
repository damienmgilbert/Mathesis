using System.Collections.Immutable;

namespace Mathesis.Symbolics.Patterns;

/// <summary>Limits of a match.</summary>
/// <param name="MaxAttempts">How many operand assignments one match may try before it gives up (associative-commutative matching is NP-hard).</param>
/// <param name="Budget">An optional budget charged for each attempt.</param>
public sealed record MatchOptions(int MaxAttempts = 20_000, Budget? Budget = null);

/// <summary>
/// Matches patterns against expressions (docs/design/07-engines.md, "Pattern matching"): syntactic matching, non-linear patterns
/// (a wild occurring twice must match equal expressions), sort-constrained and free-of wildcards, optional wildcards with defaults,
/// sequence wildcards and budgeted associative-commutative matching for operators like <c>add</c> and <c>mul</c>.
/// </summary>
/// <remarks>
/// Equality of the expressions bound by a repeated wild is structural, so subjects should be normalized first (the rewrite engine keeps
/// every state Canonical). An associative-commutative pattern matches the operands of the subject in any order; wilds that are not
/// optional absorb one operand or, when their names are all distinct and there is no sequence wild, several (<c>a_ + b_</c> matches
/// <c>x + y + z</c> with <c>b</c> bound to <c>y + z</c>). A pattern at the root with no wild to absorb extra operands may leave operands over
/// (<see cref="Bindings.Leftover"/>); nested patterns must account for every operand.
/// </remarks>
public static class Matcher
{
    /// <summary>All matches of <paramref name="pattern"/> against <paramref name="subject"/>, lazily.</summary>
    public static IEnumerable<Bindings> Match(Pattern pattern, Expr subject, MatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(subject);
        var run = new Run(pattern, options ?? new MatchOptions());
        return run.Match(pattern.Template, subject, Bindings.Empty, true);
    }

    /// <summary>The first match, if any.</summary>
    public static bool TryMatch(Pattern pattern, Expr subject, out Bindings bindings, MatchOptions? options = null)
    {
        foreach (var b in Match(pattern, subject, options))
        {
            bindings = b;
            return true;
        }
        bindings = Bindings.Empty;
        return false;
    }

    /// <summary>
    /// Replaces the wilds of <paramref name="template"/> by their bindings. Sequence wilds splice their operands into the enclosing
    /// operator; an optional wild that was not bound takes its default.
    /// </summary>
    /// <exception cref="InvalidOperationException">A wild of the template has no binding and no default.</exception>
    public static Expr Instantiate(Expr template, Bindings bindings, Pattern? source = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(bindings);
        return Inst(template, bindings, source);
    }

    private static Expr Inst(Expr t, Bindings b, Pattern? source)
    {
        if (t is Wild w)
        {
            if (b.TryGetValue(w.Name, out var value)) return value;
            if (source?.OptionsOf(w.Name).Default is { } d) return d;
            throw new InvalidOperationException($"The wild '{w.Name}' has no binding.");
        }
        var children = t.Children;
        if (children.Length == 0) return t;
        var rebuilt = new List<Expr>(children.Length);
        var changed = false;
        foreach (var child in children)
        {
            if (child is Wild sw && b.Sequences.TryGetValue(sw.Name, out var sequence) && t is Apply)
            {
                rebuilt.AddRange(sequence);
                changed = true;
                continue;
            }
            var r = Inst(child, b, source);
            changed |= !ReferenceEquals(r, child);
            rebuilt.Add(r);
        }
        return changed ? t.WithChildren([.. rebuilt]) : t;
    }

    private sealed class Run(Pattern pattern, MatchOptions options)
    {
        private int _attempts;

        private bool Spend()
        {
            _attempts++;
            if (options.Budget is { } budget && !budget.TryCharge()) return false;
            return _attempts <= options.MaxAttempts;
        }

        private static bool IsAssociativeCommutative(Operator op) => op.Has(OperatorAttributes.Associative | OperatorAttributes.Commutative);

        private WildOptions Opts(Wild w) => pattern.OptionsOf(w.Name);

        private bool IsOptional(Expr e) => e is Wild w && Opts(w).IsOptional;

        // ----- Wild binding -----

        private Bindings? Bind(Wild w, Expr s, Bindings b)
        {
            var o = Opts(w);
            if (o.IsSequence) return null;
            if (o.Sort is { } sort && !SortOf(s).IsSubsortOf(sort)) return null;
            if (!o.FreeOf.IsDefaultOrEmpty)
            {
                var free = s.FreeSymbols;
                foreach (var f in o.FreeOf)
                {
                    if (free.Contains(f)) return null;
                }
            }
            if (b.TryGetValue(w.Name, out var bound)) return bound.Equals(s) ? b : null;
            return b.With(w.Name, s);
        }

        private static Sort SortOf(Expr e)
        {
            try
            {
                return e.Sort;
            }
            catch (ArgumentException)
            {
                return Sort.Any;
            }
        }

        // ----- Matching -----

        public IEnumerable<Bindings> Match(Expr p, Expr s, Bindings b, bool root)
        {
            switch (p)
            {
                case Wild w:
                    if (Bind(w, s, b) is { } bound) yield return bound;
                    yield break;
                case Apply pa:
                    foreach (var r in MatchApply(pa, s, b, root)) yield return r;
                    yield break;
                case Bind pb:
                    if (s is Bind sb && pb.Binder == sb.Binder && pb.Bound.Length == sb.Bound.Length && pb.Data.Length == sb.Data.Length)
                    {
                        // α-renaming: the subject's bound symbols become the pattern's before the bodies are compared.
                        var body = sb.Body;
                        for (var i = 0; i < pb.Bound.Length; i++) body = body.Substitute(sb.Bound[i], pb.Bound[i]);
                        var pairs = pb.Data.Zip(sb.Data).Append((pb.Body, body)).ToList();
                        foreach (var r in Chain(pairs, 0, b)) yield return r;
                    }
                    yield break;
                default:
                    if (p.Equals(s)) yield return b;
                    yield break;
            }
        }

        private IEnumerable<Bindings> MatchApply(Apply pa, Expr s, Bindings b, bool root)
        {
            var op = pa.Operator;
            if (s is Apply sa && sa.Operator == op)
            {
                if (IsAssociativeCommutative(op))
                {
                    foreach (var r in Ac(pa, sa.Arguments, b, root)) yield return r;
                }
                else if (op.Has(OperatorAttributes.Commutative) && pa.Arguments.Length == 2 && sa.Arguments.Length == 2)
                {
                    foreach (var r in Positional(pa.Arguments, sa.Arguments, b)) yield return r;
                    if (!pa.Arguments[0].Equals(pa.Arguments[1]))
                    {
                        foreach (var r in Positional(pa.Arguments, [sa.Arguments[1], sa.Arguments[0]], b)) yield return r;
                    }
                }
                else
                {
                    foreach (var r in Positional(pa.Arguments, sa.Arguments, b)) yield return r;
                }
            }

            // The subject viewed as a one-operand sum or product, so that an optional wild can stand for the missing operands.
            if ((s is not Apply other || other.Operator != op) && IsAssociativeCommutative(op) && pa.Arguments.Any(IsOptional))
            {
                foreach (var r in Ac(pa, [s], b, root)) yield return r;
            }

            // x matches base^exponent with the default exponent.
            if (op == Operators.Pow && pa.Arguments[1] is Wild { } ew && IsOptional(ew) && (s is not Apply sp || sp.Operator != Operators.Pow))
            {
                foreach (var r in Match(pa.Arguments[0], s, b, false))
                {
                    if (Bind(ew, Opts(ew).Default!, r) is { } withDefault) yield return withDefault;
                }
            }
        }

        private IEnumerable<Bindings> Chain(List<(Expr P, Expr S)> pairs, int i, Bindings b)
        {
            if (i == pairs.Count)
            {
                yield return b;
                yield break;
            }
            foreach (var m in Match(pairs[i].P, pairs[i].S, b, false))
            {
                foreach (var r in Chain(pairs, i + 1, m)) yield return r;
            }
        }

        // Ordered arguments, with at most one sequence wild standing for any number of operands.
        private IEnumerable<Bindings> Positional(ImmutableArray<Expr> p, ImmutableArray<Expr> s, Bindings b)
        {
            var k = -1;
            for (var i = 0; i < p.Length && k < 0; i++)
            {
                if (p[i] is Wild w && Opts(w).IsSequence) k = i;
            }
            if (k < 0)
            {
                if (p.Length != s.Length) yield break;
                foreach (var r in Chain([.. p.Zip(s)], 0, b)) yield return r;
                yield break;
            }
            var suffix = p.Length - k - 1;
            if (s.Length < k + suffix) yield break;
            var pairs = new List<(Expr, Expr)>();
            for (var i = 0; i < k; i++) pairs.Add((p[i], s[i]));
            for (var i = 0; i < suffix; i++) pairs.Add((p[k + 1 + i], s[s.Length - suffix + i]));
            var middle = s[k..(s.Length - suffix)];
            var name = ((Wild)p[k]).Name;
            foreach (var r in Chain(pairs, 0, b))
            {
                if (r.Sequences.TryGetValue(name, out var existing))
                {
                    if (existing.AsSpan().SequenceEqual(middle.AsSpan())) yield return r;
                }
                else
                {
                    yield return r.WithSequence(name, middle);
                }
            }
        }

        // ----- Associative-commutative matching -----

        private static int Specificity(Expr e) => e switch
        {
            Number or Constant or Symbol or Float => 0,
            Apply a => 1 + a.LeafCount,
            _ => 2,
        };

        private IEnumerable<Bindings> Ac(Apply pa, ImmutableArray<Expr> subject, Bindings b, bool root)
        {
            var op = pa.Operator;
            var fixedArgs = new List<Expr>();
            var wilds = new List<Wild>();
            Wild? sequence = null;
            foreach (var arg in pa.Arguments)
            {
                if (arg is Wild w)
                {
                    if (Opts(w).IsSequence) sequence = w;
                    else wilds.Add(w);
                }
                else
                {
                    fixedArgs.Add(arg);
                }
            }
            fixedArgs.Sort((x, y) => Specificity(x).CompareTo(Specificity(y)));
            var used = new bool[subject.Length];
            return Fixed(0);

            IEnumerable<Bindings> Fixed(int i, Bindings? current = null)
            {
                var bindings = current ?? b;
                if (i == fixedArgs.Count)
                {
                    foreach (var r in Assign(bindings)) yield return r;
                    yield break;
                }
                for (var j = 0; j < subject.Length; j++)
                {
                    if (used[j]) continue;
                    if (!Spend()) yield break;
                    foreach (var m in Match(fixedArgs[i], subject[j], bindings, false))
                    {
                        used[j] = true;
                        foreach (var r in Fixed(i + 1, m)) yield return r;
                        used[j] = false;
                    }
                }
            }

            IEnumerable<Bindings> Assign(Bindings bindings)
            {
                var rest = Enumerable.Range(0, subject.Length).Where(i => !used[i]).ToList();

                if (wilds.Count == 0 && sequence is null)
                {
                    if (rest.Count == 0) yield return bindings;
                    else if (root) yield return bindings.WithLeftover([.. rest.Select(i => subject[i])]);
                    yield break;
                }

                var distinct = wilds.Select(w => w.Name).Distinct().Count() == wilds.Count && wilds.All(w => !bindings.ContainsKey(w.Name));
                if (distinct && sequence is null)
                {
                    foreach (var r in Groups(rest, 0, wilds, new List<int>[wilds.Count].Select(_ => new List<int>()).ToArray(), bindings)) yield return r;
                    yield break;
                }
                foreach (var r in Singles(rest, 0, bindings, [])) yield return r;
            }

            // Each wild takes exactly one operand (optional wilds may take none); a sequence wild takes the rest.
            IEnumerable<Bindings> Singles(List<int> rest, int w, Bindings bindings, HashSet<int> taken)
            {
                if (w == wilds.Count)
                {
                    var left = rest.Where(i => !taken.Contains(i)).Select(i => subject[i]).ToImmutableArray();
                    if (sequence is { } seq)
                    {
                        if (bindings.Sequences.TryGetValue(seq.Name, out var existing) && !existing.AsSpan().SequenceEqual(left.AsSpan())) yield break;
                        yield return bindings.WithSequence(seq.Name, left);
                    }
                    else if (left.Length == 0) yield return bindings;
                    else if (root) yield return bindings.WithLeftover(left);
                    yield break;
                }
                var wild = wilds[w];
                foreach (var i in rest)
                {
                    if (taken.Contains(i) || !Spend()) continue;
                    if (Bind(wild, subject[i], bindings) is not { } next) continue;
                    taken.Add(i);
                    foreach (var r in Singles(rest, w + 1, next, taken)) yield return r;
                    taken.Remove(i);
                }
                if (Opts(wild).IsOptional && Bind(wild, Opts(wild).Default!, bindings) is { } withDefault)
                {
                    foreach (var r in Singles(rest, w + 1, withDefault, taken)) yield return r;
                }
            }

            // Wilds with distinct names absorb the remaining operands: each operand goes to one wild, plain wilds get at least one.
            IEnumerable<Bindings> Groups(List<int> rest, int k, List<Wild> targets, List<int>[] groups, Bindings bindings)
            {
                if (k == rest.Count)
                {
                    var current = bindings;
                    for (var t = 0; t < targets.Count; t++)
                    {
                        Expr value;
                        if (groups[t].Count == 0)
                        {
                            if (Opts(targets[t]).Default is not { } d) yield break;
                            value = d;
                        }
                        else
                        {
                            value = groups[t].Count == 1 ? subject[groups[t][0]] : new Apply(op, [.. groups[t].Select(i => subject[i])]);
                        }
                        if (Bind(targets[t], value, current) is not { } next) yield break;
                        current = next;
                    }
                    yield return current;
                    yield break;
                }
                for (var t = 0; t < targets.Count; t++)
                {
                    if (!Spend()) yield break;
                    groups[t].Add(rest[k]);
                    foreach (var r in Groups(rest, k + 1, targets, groups, bindings)) yield return r;
                    groups[t].RemoveAt(groups[t].Count - 1);
                }
            }
        }
    }
}
