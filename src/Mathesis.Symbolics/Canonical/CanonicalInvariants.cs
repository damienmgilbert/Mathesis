using System.Collections.Immutable;

namespace Mathesis.Symbolics.Canonical;

/// <summary>
/// Checks the Canonical invariants of docs/design/05-syntax-trees-and-notation.md: no nested <c>add</c> in <c>add</c> or
/// <c>mul</c> in <c>mul</c>; at most one number per <c>add</c>/<c>mul</c>, as the first operand; no single-operand <c>add</c>/<c>mul</c>;
/// no <c>x^1</c>; no <c>0</c> term and no <c>1</c> factor; commutative operands sorted; <c>sub</c>, <c>div</c>, <c>neg</c> and
/// <c>sqrt</c> absent. Used by the property tests and available to engines that assert their output is canonical.
/// </summary>
public static class CanonicalInvariants
{
    /// <summary>Returns a description of every violated invariant in <paramref name="expr"/>, empty when it is canonical.</summary>
    public static ImmutableArray<string> Violations(Expr expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        var found = ImmutableArray.CreateBuilder<string>();
        foreach (var (node, path) in expr.Walk())
        {
            if (node is not Apply a) continue;
            var op = a.Operator;
            var args = a.Arguments;
            if (op == Operators.Sub || op == Operators.Div || op == Operators.Neg || op == Operators.Sqrt)
            {
                found.Add($"{path}: '{op.Id}' must not occur in a canonical expression");
            }
            if (op.Has(OperatorAttributes.Associative))
            {
                if (args.Any(x => x is Apply c && c.Operator == op)) found.Add($"{path}: nested '{op.Id}' in '{op.Id}'");
                if (args.Length < 2 && op.Arity.Min >= 2) found.Add($"{path}: single-operand '{op.Id}'");
            }
            if (op == Operators.Add || op == Operators.Mul)
            {
                var numbers = args.Count(x => x is Number or Float);
                if (numbers > 1) found.Add($"{path}: more than one number in '{op.Id}'");
                if (numbers == 1 && args[0] is not (Number or Float)) found.Add($"{path}: the number in '{op.Id}' is not the first operand");
                if (op == Operators.Add && args.Any(x => x is Number { Value.IsInteger: true, Value.Numerator.IsZero: true } || x is Float { Value: 0.0 })) found.Add($"{path}: a 0 term in 'add'");
                if (op == Operators.Mul && args.Any(x => x is Number n && n.Value == Numbers.BigRational.One || x is Float { Value: 1.0 })) found.Add($"{path}: a 1 factor in 'mul'");
            }
            if (op == Operators.Pow && a.Arguments[1] is Number { Value.IsInteger: true } e && e.Value == Numbers.BigRational.One) found.Add($"{path}: x^1");
            if (op.Has(OperatorAttributes.Commutative) && op != Operators.Mul && op.Family != OperatorFamily.Relation)
            {
                for (var i = 1; i < args.Length; i++)
                {
                    if (ExprOrder.Instance.Compare(args[i - 1], args[i]) > 0)
                    {
                        found.Add($"{path}: operands of '{op.Id}' are not sorted");
                        break;
                    }
                }
            }
            if (op == Operators.Mul)
            {
                // Scalar factors are sorted; non-scalar factors keep their relative order after them.
                var scalars = args.Where(x => !x.Sort.IsAggregate).ToList();
                for (var i = 1; i < scalars.Count; i++)
                {
                    if (ExprOrder.Instance.Compare(scalars[i - 1], scalars[i]) > 0)
                    {
                        found.Add($"{path}: scalar factors of 'mul' are not sorted");
                        break;
                    }
                }
            }
        }
        return found.ToImmutable();
    }
}
