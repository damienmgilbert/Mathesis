using System.Collections.Immutable;

namespace Mathesis.Symbolics;

/// <summary>Sort rules for operators whose result depends on the shapes of their arguments (matrices, vectors, functions).</summary>
internal static class ArithmeticRules
{
    private static bool Fits(Sort s) => s.IsNumeric || s == Sort.Any;

    /// <summary>Addition and subtraction: numbers join; matrices and vectors must have equal shapes.</summary>
    public static Signature Sum { get; } = Signature.Custom("Number+ → Number, Matrix(m, n)+ → Matrix(m, n)", args =>
    {
        if (args.Length == 0) return null;
        if (args.All(Fits)) return args.Aggregate(Sort.Join);
        if (args.All(a => a is MatrixSort or VectorSort || a == Sort.Any))
        {
            var first = args.FirstOrDefault(a => a is not SimpleSort);
            if (first is null) return Sort.Any;
            foreach (var a in args)
            {
                if (a == Sort.Any) continue;
                if (first is MatrixSort m && !(a is MatrixSort n && m.Rows.Equals(n.Rows) && m.Columns.Equals(n.Columns))) return null;
                if (first is VectorSort v && !(a is VectorSort w && v.Length.Equals(w.Length))) return null;
            }
            return args.Where(a => a != Sort.Any).Aggregate(Sort.Join);
        }
        return null;
    });

    /// <summary>Subtraction: like addition, but the difference of naturals is an integer.</summary>
    public static Signature Difference { get; } = Signature.Custom("Number - Number → Number, Matrix(m, n) - Matrix(m, n) → Matrix(m, n)", args =>
    {
        var sum = Sum.Result(args);
        return sum is { IsNumeric: true } ? Sort.Join(sum, Sort.Integer) : sum;
    });

    /// <summary>Negation: numbers gain the integers; aggregates keep their sort.</summary>
    public static Signature Negation { get; } = Signature.Custom("Number → Number", args =>
        args[0] switch
        {
            var s when s.IsNumeric => Sort.Join(s, Sort.Integer),
            var s when s is MatrixSort or VectorSort || s == Sort.Any => s,
            _ => null,
        });

    private static Sort? Multiply(Sort a, Sort b)
    {
        if (a == Sort.Any || b == Sort.Any) return Sort.Any;
        if (a.IsNumeric && b.IsNumeric) return Sort.Join(a, b);
        if (a.IsNumeric && b is MatrixSort mb) return Sort.MatrixOf(mb.Rows, mb.Columns, Sort.Join(a, mb.Element));
        if (a.IsNumeric && b is VectorSort vb) return Sort.VectorOf(vb.Length, Sort.Join(a, vb.Element));
        if (b.IsNumeric && a is MatrixSort ma) return Sort.MatrixOf(ma.Rows, ma.Columns, Sort.Join(b, ma.Element));
        if (b.IsNumeric && a is VectorSort va) return Sort.VectorOf(va.Length, Sort.Join(b, va.Element));
        if (a is MatrixSort x && b is MatrixSort y) return x.Columns.Equals(y.Rows) ? Sort.MatrixOf(x.Rows, y.Columns, Sort.Join(x.Element, y.Element)) : null;
        if (a is MatrixSort p && b is VectorSort q) return p.Columns.Equals(q.Length) ? Sort.VectorOf(p.Rows, Sort.Join(p.Element, q.Element)) : null;
        return null;
    }

    /// <summary>Multiplication: scalars join; scalar × matrix scales; matrix × matrix needs matching inner dimensions.</summary>
    public static Signature Product { get; } = Signature.Custom("Number+ → Number, Matrix(m, n) × Matrix(n, p) → Matrix(m, p)", args =>
    {
        if (args.Length == 0) return null;
        Sort? acc = args[0];
        for (var i = 1; i < args.Length && acc is not null; i++) acc = Multiply(acc, args[i]);
        return acc;
    });

    /// <summary>Division: the divisor must be a scalar; integers divide into rationals.</summary>
    public static Signature Quotient { get; } = Signature.Custom("Number / Number → Number", args =>
    {
        var (a, b) = (args[0], args[1]);
        if (!Fits(b)) return null;
        if (Fits(a)) return a == Sort.Any || b == Sort.Any ? Sort.Any : Sort.Join(Sort.Join(a, b), Sort.Rational);
        return a switch
        {
            MatrixSort m => Sort.MatrixOf(m.Rows, m.Columns, Sort.Join(Sort.Join(m.Element, b), Sort.Rational)),
            VectorSort v => Sort.VectorOf(v.Length, Sort.Join(Sort.Join(v.Element, b), Sort.Rational)),
            _ => null,
        };
    });

    /// <summary>Powers: natural exponents keep the base's sort, integer exponents reach ℚ, others stay real or go complex.</summary>
    public static Signature Power { get; } = Signature.Custom("Number ^ Number → Number, Matrix ^ Integer → Matrix", args =>
    {
        var (b, e) = (args[0], args[1]);
        if (b == Sort.Any || e == Sort.Any) return Sort.Any;
        if (b is MatrixSort mb) return e.IsSubsortOf(Sort.Integer) && mb.Rows.Equals(mb.Columns) ? b : null;
        if (!b.IsNumeric || !e.IsNumeric) return null;
        if (e.IsSubsortOf(Sort.Natural)) return b;
        if (e.IsSubsortOf(Sort.Integer)) return Sort.Join(b, Sort.Rational);
        return b.IsRealValued && e.IsRealValued ? Sort.Real : Sort.Complex;
    });

    /// <summary>Maximum and minimum: all arguments must be real-valued.</summary>
    public static Signature Extremum { get; } = Signature.Custom("Real+ → Real", args =>
        args.All(a => a.IsRealValued || a == Sort.ExtendedReal || a == Sort.Any) ? args.Aggregate(Sort.Join) : null);

    /// <summary>Function application: the codomain of the function's sort.</summary>
    public static Signature Call { get; } = Signature.Custom("Function(D → C), D → C", args =>
        args[0] switch
        {
            FunctionSort f => f.Codomain,
            var s when s == Sort.Any => Sort.Any,
            _ => null,
        });

    /// <summary>The derivative of a function symbol keeps its sort.</summary>
    public static Signature DerivativeOf { get; } = Signature.Custom("Function → Function", args =>
        args[0] switch
        {
            FunctionSort => args[0],
            var s when s == Sort.Any => Sort.Any,
            _ => null,
        });

    /// <summary>The result has the sort of the first argument.</summary>
    public static Signature SameAsFirst { get; } = Signature.Custom("A → A", args => args[0]);

    /// <summary>Transposition swaps matrix dimensions; a vector becomes a row.</summary>
    public static Signature Transpose { get; } = Signature.Custom("Matrix(m, n) → Matrix(n, m)", args =>
        args[0] switch
        {
            MatrixSort m => Sort.MatrixOf(m.Columns, m.Rows, m.Element),
            VectorSort v => Sort.MatrixOf(Sym.Number(1), v.Length, v.Element),
            var s when s == Sort.Any => Sort.Any,
            _ => null,
        });

    /// <summary>Determinant and trace need a square matrix and return its entry sort.</summary>
    public static Signature Determinant { get; } = Signature.Custom("Matrix(n, n) → Number", args =>
        args[0] switch
        {
            MatrixSort m => m.Rows.Equals(m.Columns) ? m.Element : null,
            var s when s == Sort.Any => Sort.Any,
            var s when s.IsNumeric => s,
            _ => null,
        });
}
