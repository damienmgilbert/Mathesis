using System.Collections.Immutable;
using Mathesis.LinearAlgebra;
using Mathesis.LinearAlgebra.Exact;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Assumptions;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Solving;

public static partial class Solver
{
    // ----- Inequalities: sign charts -----

    private sealed record Cell(Expr? Point, double Value, Expr? Lower, Expr? Upper, double LowerValue, double UpperValue, double Test)
    {
        public bool IsPoint => Point is not null;
    }

    private static Outcome<SolutionSet> SolveInequality(Expr relation, Operator op, Expr l, Expr r, Symbol x, Ctx c)
    {
        var f = Canon(Add(l, Negate(r)));
        if (!f.FreeSymbols.Contains(x) || f.FreeSymbols.Any(s => !s.Equals(x))) return new Outcome<SolutionSet>.Unevaluated(relation, "Only inequalities in one unknown with numeric coefficients are supported.");

        // Critical points: the zeros of the expression and the boundaries of its natural domain.
        var inner = new Ctx(c.Math, c.Budget, new SolveOptions());
        var zeros = Candidates(f, x, 0, inner);
        if (zeros is null || zeros.Families.Count > 0 || zeros.All) return new Outcome<SolutionSet>.Unevaluated(relation, "The zeros of the expression could not be found in closed form.");
        var critical = new List<(Expr Expr, double Value)>();
        var approximate = zeros.Approximate;
        foreach (var p in zeros.Points.Select(q => Polish(q, c)))
        {
            if (Value(p) is { } v) critical.Add((p, v));
        }
        if (NaturalDomain.Of(f, x, c.Math) is not Outcome<Expr>.Success { Value: var domain }) return new Outcome<SolutionSet>.Unevaluated(relation, "The domain of the expression is not known.");
        if (!BoundaryPoints(domain, critical)) return new Outcome<SolutionSet>.Unevaluated(relation, "The domain of the expression is too complicated.");
        var sorted = critical.OrderBy(p => p.Value).Where((p, i) => true).ToList();
        var points = new List<(Expr Expr, double Value)>();
        foreach (var p in sorted)
        {
            if (points.Count > 0 && (points[^1].Expr.Equals(p.Expr) || Math.Abs(points[^1].Value - p.Value) <= 1e-12 * Math.Max(1, Math.Abs(p.Value)))) continue;
            points.Add(p);
        }
        Record(c, "alg.ineq.sign-chart", "critical-points", relation, new SetLiteral([.. points.Select(p => p.Expr)]), ("f", f));

        // Cells in order: (-oo, p1), p1, (p1, p2), p2, ..., (pn, oo).
        var cells = new List<Cell>();
        var minusInfinity = new Constant(ConstantId.NegativeInfinity);
        var plusInfinity = new Constant(ConstantId.PositiveInfinity);
        for (var i = 0; i <= points.Count; i++)
        {
            Expr lower = i == 0 ? minusInfinity : points[i - 1].Expr;
            Expr upper = i == points.Count ? plusInfinity : points[i].Expr;
            var lo = i == 0 ? double.NegativeInfinity : points[i - 1].Value;
            var hi = i == points.Count ? double.PositiveInfinity : points[i].Value;
            var test = double.IsInfinity(lo) && double.IsInfinity(hi) ? 0 : double.IsInfinity(lo) ? hi - 1 - Math.Abs(hi) : double.IsInfinity(hi) ? lo + 1 + Math.Abs(lo) : (lo + hi) / 2;
            cells.Add(new Cell(null, 0, lower, upper, lo, hi, test));
            if (i < points.Count) cells.Add(new Cell(points[i].Expr, points[i].Value, null, null, 0, 0, 0));
        }

        var satisfied = new List<bool>();
        foreach (var cell in cells)
        {
            var at = cell.IsPoint ? cell.Point! : (Expr)new Float(cell.Test);
            satisfied.Add(Holds(f, x, at, cell.IsPoint ? cell.Value : cell.Test, op, c));
        }
        Record(c, "alg.ineq.sign-chart", "test-intervals", new SetLiteral([.. points.Select(p => p.Expr)]), new SetLiteral([.. cells.Where((_, i) => satisfied[i]).Select(cell => cell.IsPoint ? cell.Point! : (Expr)new Float(cell.Test))]));

        // Join consecutive satisfied cells into intervals.
        var pieces = new List<IntervalPiece>();
        var pieceExprs = new List<Expr>();
        for (var i = 0; i < cells.Count; i++)
        {
            if (!satisfied[i]) continue;
            var j = i;
            while (j + 1 < cells.Count && satisfied[j + 1]) j++;
            var first = cells[i];
            var last = cells[j];
            Expr lower = first.IsPoint ? first.Point! : first.Lower!;
            Expr upper = last.IsPoint ? last.Point! : last.Upper!;
            var lowerClosed = first.IsPoint;
            var upperClosed = last.IsPoint;
            var lowerValue = first.IsPoint ? first.Value : first.LowerValue;
            var upperValue = last.IsPoint ? last.Value : last.UpperValue;
            pieces.Add(new IntervalPiece(lower, upper, lowerClosed, upperClosed, lowerValue, upperValue));
            pieceExprs.Add(new IntervalLiteral(lower, upper, lowerClosed, upperClosed));
            i = j;
        }
        SolutionSet set;
        if (pieces.Count == 0) set = SolutionSet.Empty;
        else if (pieces.Count == 1 && double.IsNegativeInfinity(pieces[0].LowerValue) && double.IsPositiveInfinity(pieces[0].UpperValue)) set = SolutionSet.All;
        else set = new SolutionSet(SolutionKind.Intervals, pieceExprs.Count == 1 ? pieceExprs[0] : new Apply(Operators.Union, [.. pieceExprs])) { Pieces = [.. pieces], IsApproximate = approximate || points.Any(p => p.Expr is Float) };
        return Wrap(relation, set, c);
    }

    // The finite boundaries of a domain: ends of intervals and excluded points. Returns false for shapes this chart cannot use (periodic exclusions, condition sets).
    private static bool BoundaryPoints(Expr domain, List<(Expr Expr, double Value)> into)
    {
        switch (domain)
        {
            case Constant { Id: ConstantId.Reals }:
                return true;
            case IntervalLiteral i:
                foreach (var end in new[] { i.Lower, i.Upper })
                {
                    if (end is Constant { Id: ConstantId.PositiveInfinity or ConstantId.NegativeInfinity }) continue;
                    if (Value(end) is { } v) into.Add((Canon(end), v));
                    else return false;
                }
                return true;
            case Apply { Operator.Id: "union" or "intersect", Arguments: var parts }:
                return parts.All(p => BoundaryPoints(p, into));
            case Apply { Operator.Id: "setminus", Arguments: [var a, var b] }:
                return BoundaryPoints(a, into) && BoundaryPoints(b, into);
            case SetLiteral s:
                foreach (var e in s.Elements)
                {
                    if (Value(e) is { } v) into.Add((Canon(e), v));
                    else return false;
                }
                return true;
            case Constant { Id: ConstantId.EmptySet }:
                return true;
            default:
                return false;
        }
    }

    // Whether the relation holds at a point of the chart (a critical point, or a representative of an open cell).
    private static bool Holds(Expr f, Symbol x, Expr at, double approx, Operator op, Ctx c)
    {
        var substituted = f.Substitute(x, at);
        if (Evaluator.N(substituted) is not Outcome<double>.Success { Value: var v } || !double.IsFinite(v)) return false;
        var zero = Math.Abs(v) < 1e-11 * Math.Max(1, Math.Abs(approx));
        if (at is not Float)
        {
            var exact = Evaluator.Evaluate(substituted, null, c.Math.NormalizeOptions) is Outcome<Expr>.Success { Value: var e } ? e : Canon(substituted);
            zero = exact is Number { Value.Sign: 0 } || ZeroTest.Test(exact, c.Math) is ZeroTestResult.Zero or ZeroTestResult.ProbablyZero;
        }
        if (op == Operators.Lt) return !zero && v < 0;
        if (op == Operators.Le) return zero || v < 0;
        if (op == Operators.Gt) return !zero && v > 0;
        if (op == Operators.Ge) return zero || v > 0;
        return !zero;
    }

    // ----- Systems -----

    /// <summary>
    /// Solves a system of equations in <paramref name="variables"/>. Linear systems with rational coefficients use exact Gauss–Jordan elimination
    /// (<c>linalg.sys.gauss-jordan</c>) and report a unique solution, none (<c>linalg.sys.rouche-capelli</c>) or a parametric family; other polynomial
    /// systems use substitution (<c>alg.sys.substitution</c>) when an equation is linear in some unknown.
    /// </summary>
    public static Outcome<SolutionSet> SolveSystem(IReadOnlyList<Expr> equations, IReadOnlyList<Symbol> variables, SolveOptions? options = null, MathContext? math = null, Budget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(equations);
        ArgumentNullException.ThrowIfNull(variables);
        var c = new Ctx(math ?? MathContext.Default, budget ?? new Budget(maxSteps: 50_000, maxTime: TimeSpan.FromSeconds(30)), options ?? new SolveOptions());
        var start = new Apply(Operators.And, [.. equations.Select(Canon)]);
        if (equations.Count == 1) start = (Apply)new Apply(Operators.And, [Canon(equations[0]), new Constant(ConstantId.True)]);
        var zeros = new List<Expr>();
        foreach (var e in equations)
        {
            if (Canon(e) is not Apply { Operator: var eq, Arguments: [var l, var r] } || eq != Operators.Eq) return new Outcome<SolutionSet>.Unevaluated(start, "Every equation must have the form a = b.");
            zeros.Add(Canon(Add(l, Negate(r))));
        }

        if (LinearRows(zeros, variables) is { } rows) return LinearSystem(start, rows, variables, c);
        var solved = Substitution(zeros, [.. variables], 0, c);
        if (solved is null) return new Outcome<SolutionSet>.Unevaluated(start, "No method solves this system.");

        // Substitute every candidate back into every equation.
        var accepted = new List<Expr>();
        foreach (var tuple in solved)
        {
            var ok = true;
            for (var i = 0; i < zeros.Count && ok; i++)
            {
                var substituted = zeros[i];
                for (var j = 0; j < variables.Count; j++) substituted = substituted.Substitute(variables[j], tuple[j]);
                var exact = Evaluator.Evaluate(substituted, null, c.Math.NormalizeOptions) is Outcome<Expr>.Success { Value: var v } ? v : Canon(substituted);
                if (exact is Number { Value.Sign: 0 }) continue;
                if (Evaluator.N(substituted) is Outcome<double>.Success { Value: var n } && tuple.Any(t => t is Float)) ok = Math.Abs(n) <= 1e-9;
                else ok = ZeroTest.Test(exact, c.Math) is ZeroTestResult.Zero or ZeroTestResult.ProbablyZero;
            }
            if (ok) accepted.Add(new TupleLiteral([.. tuple]));
            else Record(c, "alg.eq.extraneous", "reject-candidate", new TupleLiteral([.. variables]), new Constant(ConstantId.False), ("candidate", new TupleLiteral([.. tuple])));
        }
        var set = accepted.Count == 0
            ? SolutionSet.Empty
            : new SolutionSet(SolutionKind.Finite, new SetLiteral([.. accepted])) { Points = [.. accepted], Variables = [.. variables], IsApproximate = accepted.Any(t => ((TupleLiteral)t).Elements.Any(e => e is Float)) };
        return Wrap(start, set, c);
    }

    // The augmented rows [a1 … an | b] when every equation is linear with rational coefficients.
    private static BigRational[][]? LinearRows(List<Expr> zeros, IReadOnlyList<Symbol> variables)
    {
        var rows = new List<BigRational[]>();
        foreach (var f in zeros)
        {
            if (f.FreeSymbols.Any(s => !variables.Contains(s)) || !PolynomialConversion.TryToSparse(f, out var atoms, out var sparse)) return null;
            if (atoms.Any(a => a is not Symbol)) return null;
            var row = new BigRational[variables.Count + 1];
            foreach (var (monomial, coefficient) in sparse.Terms)
            {
                if (monomial.TotalDegree > 1) return null;
                if (monomial.TotalDegree == 0) row[^1] -= coefficient;
                else
                {
                    var index = monomial.Exponents.ToList().FindIndex(e => e == 1);
                    var j = variables.ToList().IndexOf((Symbol)atoms[index]);
                    row[j] += coefficient;
                }
            }
            rows.Add(row);
        }
        return [.. rows];
    }

    private static Apply RowEquation(BigRational[] row, IReadOnlyList<Symbol> variables)
    {
        var terms = new List<Expr>();
        for (var j = 0; j < variables.Count; j++)
        {
            if (row[j] != BigRational.Zero) terms.Add(row[j] == BigRational.One ? variables[j] : Mul(Num(row[j]), variables[j]));
        }
        Expr left = terms.Count == 0 ? Num(0) : terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]);
        return new Apply(Operators.Eq, [left, Num(row[^1])]);
    }

    private static Apply RowsExpression(BigRational[][] rows, IReadOnlyList<Symbol> variables)
    {
        var equations = rows.Select(r => RowEquation(r, variables)).ToList();
        return equations.Count == 1 ? equations[0] : new Apply(Operators.And, [.. equations]);
    }

    private static Outcome<SolutionSet> LinearSystem(Expr start, BigRational[][] rows, IReadOnlyList<Symbol> variables, Ctx c)
    {
        var n = variables.Count;
        var matrix = DenseMatrix.FromRows(ToArray(rows, n + 1));
        var reduction = ExactLinearAlgebra.RowReduce(matrix);

        // Show the row operations one by one.
        var state = rows.Select(r => (BigRational[])r.Clone()).ToArray();
        var before = RowsExpression(state, variables);
        foreach (var op in reduction.Operations)
        {
            switch (op.Kind)
            {
                case RowOperationKind.Swap:
                    (state[op.Row], state[op.OtherRow]) = (state[op.OtherRow], state[op.Row]);
                    break;
                case RowOperationKind.Scale:
                    state[op.Row] = state[op.Row].Select(v => v * op.Factor).ToArray();
                    break;
                default:
                    state[op.Row] = state[op.Row].Select((v, j) => v + op.Factor * state[op.OtherRow][j]).ToArray();
                    break;
            }
            var after = RowsExpression(state, variables);
            Record(c, "linalg.sys.row-ops", "row-operation", before, after, ("operation", new Symbol(op.ToString().Replace(' ', '_').Replace('↔', 'x').Replace('←', '_').Replace('(', '_').Replace(')', '_').Replace('·', '_').Replace('+', 'p').Replace('/', 'd').Replace('-', 'm').Replace(',', '_'))));
            before = after;
        }
        Record(c, "linalg.sys.gauss-jordan", "gauss-jordan", start, RowsExpression(ToJagged(reduction.Reduced), variables));

        var pivots = reduction.PivotColumns;
        if (pivots.Contains(n))
        {
            Record(c, "linalg.sys.rouche-capelli", "inconsistent", RowsExpression(ToJagged(reduction.Reduced), variables), new Constant(ConstantId.False));
            return Wrap(start, SolutionSet.Empty, c);
        }

        var reduced = ToJagged(reduction.Reduced);
        var free = Enumerable.Range(0, n).Where(j => !pivots.Contains(j)).ToList();
        if (free.Count == 0)
        {
            var tuple = new Expr[n];
            for (var i = 0; i < pivots.Length; i++) tuple[pivots[i]] = Canon(Num(reduced[i][n]));
            var point = new TupleLiteral([.. tuple]);
            Record(c, "alg.sys.classification", "unique-solution", RowsExpression(reduced, variables), point);
            return Wrap(start, new SolutionSet(SolutionKind.Finite, new SetLiteral([point])) { Points = [point], Variables = [.. variables] }, c);
        }

        // Free unknowns become parameters.
        var names = new[] { "t", "s", "r", "p", "q", "u", "v", "w" };
        var parameters = free.Select((j, i) => new Symbol(names.Where(nm => !variables.Any(v => v.Name == nm)).ElementAt(i))).ToList();
        var components = new Expr[n];
        for (var k = 0; k < free.Count; k++) components[free[k]] = parameters[k];
        for (var i = 0; i < pivots.Length; i++)
        {
            var terms = new List<Expr> { Num(reduced[i][n]) };
            for (var k = 0; k < free.Count; k++)
            {
                if (reduced[i][free[k]] != BigRational.Zero) terms.Add(Mul(Num(-reduced[i][free[k]]), parameters[k]));
            }
            components[pivots[i]] = Canon(terms.Count == 1 ? terms[0] : new Apply(Operators.Add, [.. terms]));
        }
        var generic = new TupleLiteral([.. components]);
        Record(c, "alg.sys.classification", "infinitely-many-solutions", RowsExpression(reduced, variables), generic, ("free", new Symbol(string.Join("_", parameters.Select(p => p.Name)))));
        var set = new SolutionSet(SolutionKind.Parametric, new Bind(Binder.ImageSet, [.. parameters], [.. parameters.Select(_ => (Expr)new Constant(ConstantId.Reals))], generic)) { Points = [generic], Parameters = [.. parameters], Variables = [.. variables] };
        return Wrap(start, set, c);
    }

    private static BigRational[,] ToArray(BigRational[][] rows, int columns)
    {
        var array = new BigRational[rows.Length, columns];
        for (var i = 0; i < rows.Length; i++)
        {
            for (var j = 0; j < columns; j++) array[i, j] = rows[i][j];
        }
        return array;
    }

    private static BigRational[][] ToJagged(DenseMatrix<BigRational> m)
    {
        var rows = new BigRational[m.Rows][];
        for (var i = 0; i < m.Rows; i++)
        {
            rows[i] = new BigRational[m.Columns];
            for (var j = 0; j < m.Columns; j++) rows[i][j] = m[i, j];
        }
        return rows;
    }

    // Substitution: solve an equation that is linear in some unknown (with a numeric coefficient), substitute into the others, and recurse; one unknown left is solved as an equation.
    private static List<Expr[]>? Substitution(List<Expr> zeros, List<Symbol> variables, int depth, Ctx c)
    {
        if (depth > 6 || !c.Budget.TryCharge()) return null;
        if (variables.Count == 1)
        {
            // All remaining equations are in one unknown: the solutions of the first, filtered by the others later.
            var first = zeros.FirstOrDefault(z => z.FreeSymbols.Contains(variables[0]));
            if (first is null) return null;
            var inner = new Ctx(c.Math, c.Budget, c.Options);
            if (Candidates(first, variables[0], 0, inner) is not { } cand || cand.Families.Count > 0 || cand.All) return null;
            c.Steps.AddRange(inner.Steps);
            return cand.Points.Select(p => new[] { Polish(p, c) }).ToList();
        }

        foreach (var f in zeros)
        {
            foreach (var v in variables)
            {
                if (!f.FreeSymbols.Contains(v) || PolynomialConversion.Coefficients(f, v) is not { } co || co.Keys.Any(k => k > 1) || !co.TryGetValue(1, out var slope) || slope is not Number { Value.Sign: not 0 }) continue;
                co.TryGetValue(0, out var intercept);
                var value = Canon(Mul(Negate(intercept ?? Num(0)), Pow(slope, Num(-1))));
                Record(c, "alg.sys.substitution", "solve-for-variable", f, new Apply(Operators.Eq, [v, value]), ("variable", v));
                var rest = zeros.Where(z => !ReferenceEquals(z, f)).Select(z => Canon(z.Substitute(v, value))).Where(z => !(z is Number { Value.Sign: 0 })).ToList();
                var remaining = variables.Where(s => !s.Equals(v)).ToList();
                if (rest.Count == 0) return null;
                if (rest.Any(z => z.FreeSymbols.Count == 0 && !(z is Number { Value.Sign: 0 }) && ZeroTest.Test(z, c.Math) == ZeroTestResult.NonZero)) return [];
                var inner = Substitution(rest, remaining, depth + 1, c);
                if (inner is null) continue;
                var result = new List<Expr[]>();
                foreach (var tuple in inner)
                {
                    var full = new Expr[variables.Count];
                    var expression = value;
                    for (var i = 0; i < remaining.Count; i++) expression = expression.Substitute(remaining[i], tuple[i]);
                    for (var i = 0; i < variables.Count; i++)
                    {
                        var j = remaining.IndexOf(variables[i]);
                        full[i] = j >= 0 ? tuple[j] : Polish(Canon(expression), c);
                    }
                    result.Add(full);
                }
                return result;
            }
        }
        return null;
    }
}
