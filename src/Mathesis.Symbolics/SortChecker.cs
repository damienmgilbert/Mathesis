using System.Collections.Immutable;

namespace Mathesis.Symbolics;

/// <summary>A sort mismatch found by <see cref="SortChecker"/>.</summary>
/// <param name="Path">The node where the mismatch occurs.</param>
/// <param name="Error">A <see cref="MathErrorKind.SortMismatch"/> error naming the operator and the argument sorts.</param>
public sealed record SortIssue(ExprPath Path, MathError Error);

/// <summary>The inferred sort of an expression and any mismatches found on the way.</summary>
/// <param name="Sort">The sort of the whole expression; <see cref="Symbolics.Sort.Any"/> where a mismatch was found.</param>
/// <param name="Issues">The mismatches, outermost first.</param>
public sealed record SortCheckResult(Sort Sort, ImmutableArray<SortIssue> Issues)
{
    /// <summary>Whether no mismatch was found.</summary>
    public bool IsValid => Issues.IsEmpty;
}

/// <summary>
/// Infers the sort of each node from its operator's <see cref="Signature"/> and reports mismatches, such as adding a 2×3
/// matrix to a scalar, as <see cref="MathErrorKind.SortMismatch"/> errors with the node path.
/// </summary>
public static class SortChecker
{
    /// <summary>Infers the sort of <paramref name="expr"/> and collects sort mismatches.</summary>
    public static SortCheckResult Check(Expr expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var issues = ImmutableArray.CreateBuilder<SortIssue>();
        var sort = Infer(expr, ExprPath.Root, issues);
        return new(sort, issues.ToImmutable());
    }

    private static Sort Infer(Expr e, ExprPath path, ImmutableArray<SortIssue>.Builder issues)
    {
        switch (e)
        {
            case Number n:
                return n.Value.IsInteger ? (n.Value.Sign >= 0 ? Sort.Natural : Sort.Integer) : Sort.Rational;
            case Float:
                return Sort.Real;
            case Symbol s:
                return s.DeclaredSort;
            case Constant c:
                return Constants.Info(c.Id).Sort;
            case Wild:
                return Sort.Any;
            case Apply a:
                var sorts = ImmutableArray.CreateBuilder<Sort>(a.Arguments.Length);
                for (var i = 0; i < a.Arguments.Length; i++) sorts.Add(Infer(a.Arguments[i], path.Child(i), issues));
                var result = a.Operator.Signature.Result(sorts.ToImmutable());
                if (result is null)
                {
                    issues.Insert(0, new SortIssue(path, MathError.SortMismatch($"Operator '{a.Operator.Id}' cannot take arguments of sorts ({string.Join(", ", sorts)}); expected {a.Operator.Signature.Description}.", a)));
                    return Sort.Any;
                }
                return result;
            case Bind b:
                var dataSorts = new Sort[b.Data.Length];
                for (var i = 0; i < b.Data.Length; i++) dataSorts[i] = Infer(b.Data[i], path.Child(i), issues);
                var body = Infer(b.Body, path.Child(b.Data.Length), issues);
                return b.Binder switch
                {
                    Binder.ForAll or Binder.Exists or Binder.ExistsUnique => Sort.Boolean,
                    Binder.Lambda => Sort.FunctionOf(b.Bound.Length == 1 ? b.Bound[0].DeclaredSort : Sort.TupleOf([.. b.Bound.Select(x => x.DeclaredSort)]), body),
                    Binder.SetBuilder => Sort.SetOf(b.Bound[0].DeclaredSort),
                    Binder.ImageSet or Binder.IndexedUnion or Binder.IndexedIntersection => body is SetSort && b.Binder != Binder.ImageSet ? body : Sort.SetOf(body),
                    Binder.ArgMin or Binder.ArgMax => b.Bound[0].DeclaredSort,
                    Binder.Integral or Binder.Limit or Binder.Sum or Binder.Product => body,
                    _ => body,
                };
            case MatrixLiteral m:
                var entry = Sort.Natural;
                var first = true;
                for (var i = 0; i < m.Entries.Length; i++)
                {
                    var s = Infer(m.Entries[i], path.Child(i), issues);
                    entry = first ? s : Sort.Join(entry, s);
                    first = false;
                }
                return Sort.MatrixOf(m.Rows, m.Columns, entry);
            case SetLiteral set:
                Sort? element = null;
                for (var i = 0; i < set.Elements.Length; i++)
                {
                    var s = Infer(set.Elements[i], path.Child(i), issues);
                    element = element is null ? s : Sort.Join(element, s);
                }
                return Sort.SetOf(element ?? Sort.Any);
            case IntervalLiteral interval:
                Infer(interval.Lower, path.Child(0), issues);
                Infer(interval.Upper, path.Child(1), issues);
                return Sort.SetOf(Sort.Real);
            case TupleLiteral t:
                var components = new Sort[t.Elements.Length];
                for (var i = 0; i < components.Length; i++) components[i] = Infer(t.Elements[i], path.Child(i), issues);
                return Sort.TupleOf(components);
            case Piecewise p:
                Sort? value = null;
                for (var i = 0; i < p.Cases.Length; i++)
                {
                    var v = Infer(p.Cases[i].Value, path.Child(2 * i), issues);
                    var c = Infer(p.Cases[i].Condition, path.Child(2 * i + 1), issues);
                    if (c != Sort.Boolean && c != Sort.Any)
                    {
                        issues.Add(new SortIssue(path.Child(2 * i + 1), MathError.SortMismatch($"A piecewise condition must be Boolean, not {c}.", p.Cases[i].Condition)));
                    }
                    value = value is null ? v : Sort.Join(value, v);
                }
                return value ?? Sort.Any;
            default:
                return Sort.Any;
        }
    }
}
