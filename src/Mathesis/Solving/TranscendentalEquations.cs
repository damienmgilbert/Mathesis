using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Polynomials;
using Mathesis.Simplification;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Evaluation;
using Mathesis.Symbolics.Representations;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Solving;

public static partial class Solver
{
    // Replaces from the top: a rule that matches a node wins over the rules for its children (Expr.Transform works from the leaves up).
    private static Expr TopDown(Expr e, Func<Expr, Expr?> rule)
    {
        if (rule(e) is { } replaced) return replaced;
        var children = e.Children;
        if (children.Length == 0) return e;
        var rebuilt = children.Select(ch => TopDown(ch, rule)).ToImmutableArray();
        return children.Zip(rebuilt).All(p => ReferenceEquals(p.First, p.Second)) ? e : e.WithChildren(rebuilt);
    }

    // ----- Exponential equations -----

    private static bool IsExponential(Expr e, Symbol x) =>
        e is Apply { Operator: var op, Arguments: var args } && ((op == Operators.Exp && args[0].FreeSymbols.Contains(x)) || (op == Operators.Pow && !args[0].FreeSymbols.Contains(x) && args[1].FreeSymbols.Contains(x)));

    // a·x + d with rational a and d.
    private static (BigRational A, BigRational D)? LinearExponent(Expr g, Symbol x) =>
        g.FreeSymbols.Count == 1 && PolynomialConversion.TryToPolynomial(g, x, out var p) && p.Degree == 1 ? (p[1], p[0]) : null;

    // a·x + d with rational a ≠ 0 and d free of x (possibly irrational, such as π/3).
    private static (BigRational A, Expr D)? LinearArgument(Expr g, Symbol x)
    {
        if (PolynomialConversion.Coefficients(g, x) is not { } co || co.Keys.Any(k => k is < 0 or > 1) || !co.TryGetValue(1, out var slope) || slope is not Number { Value: var a } || a.Sign == 0) return null;
        return (a, co.TryGetValue(0, out var d) ? d : Num(0));
    }

    private static Dictionary<BigInteger, int>? PrimeVector(BigRational b)
    {
        if (b.Sign <= 0 || b == BigRational.One) return null;
        var vector = new Dictionary<BigInteger, int>();
        void Add(BigInteger n, int sign)
        {
            for (BigInteger p = 2; p * p <= n && p < 100_000; p += p == 2 ? 1 : 2)
            {
                while ((n % p).IsZero)
                {
                    vector[p] = vector.GetValueOrDefault(p) + sign;
                    n /= p;
                }
            }
            if (n > 1) vector[n] = vector.GetValueOrDefault(n) + sign;
        }
        Add(b.Numerator, 1);
        Add(b.Denominator, -1);
        return vector;
    }

    // r with v = r·w (rational), or null.
    private static BigRational? Ratio(Dictionary<BigInteger, int> v, Dictionary<BigInteger, int> w)
    {
        if (v.Keys.Any(p => !w.ContainsKey(p))) return null;
        var first = w.First();
        var r = new BigRational(v.GetValueOrDefault(first.Key)) / new BigRational(first.Value);
        return w.All(kv => new BigRational(v.GetValueOrDefault(kv.Key)) == r * new BigRational(kv.Value)) ? r : null;
    }

    private static Cand? Exponential(Expr f, Symbol x, int depth, Ctx c)
    {
        var nodes = f.Walk().Select(w => w.Expr).Where(e => IsExponential(e, x)).Distinct().ToList();
        if (nodes.Count == 0) return null;
        // (base, a, d) of each exponential; a null base is e.
        var parsed = new List<(Expr Node, BigRational? Base, BigRational A, BigRational D)>();
        foreach (var node in nodes)
        {
            var args = ((Apply)node).Arguments;
            if (node is Apply { Operator.Id: "exp" })
            {
                if (LinearExponent(args[0], x) is not var (a, d)) return null;
                parsed.Add((node, null, a, d));
            }
            else if (args[0] is Number { Value: var b } && b.Sign > 0 && b != BigRational.One)
            {
                if (LinearExponent(args[1], x) is not var (a, d)) return null;
                parsed.Add((node, b, a, d));
            }
            else if (args[0] is Constant { Id: ConstantId.E })
            {
                if (LinearExponent(args[1], x) is not var (a, d)) return null;
                parsed.Add((node, null, a, d));
            }
            else
            {
                return null;
            }
        }

        var common = CommonBase(parsed.Select(p => p.Base).ToList());
        if (common is not { } baseInfo) return TwoTermLogarithms(f, x, parsed, c);
        var (b0, ratios) = baseInfo;
        var m = BigInteger.One;
        for (var i = 0; i < parsed.Count; i++)
        {
            var exponent = ratios[i] * parsed[i].A;
            m = m / BigInteger.GreatestCommonDivisor(m, exponent.Denominator) * exponent.Denominator;
            if (b0 is not null && !(ratios[i] * parsed[i].D).IsInteger) return TwoTermLogarithms(f, x, parsed, c);
            if (b0 is null && false) return null;
        }

        var y = Fresh(f, "y");
        var substituted = TopDown(f, e =>
        {
            var i = parsed.FindIndex(p => p.Node.Equals(e));
            if (i < 0) return null;
            var (_, _, a, d) = parsed[i];
            var coefficient = b0 is null ? (Expr)new Apply(Operators.Pow, [new Constant(ConstantId.E), Num(ratios[i] * d)]) : Num(BigRational.Pow(b0.Value, (int)(ratios[i] * d).Numerator));
            return Mul(coefficient, Pow(y, Num(ratios[i] * a * new BigRational(m))));
        });
        substituted = Canon(substituted);
        if (substituted.FreeSymbols.Contains(x)) return null;
        var baseExpr = b0 is null ? (Expr)new Constant(ConstantId.E) : Num(b0.Value);
        Record(c, "alg.eq.exponential", "substitute-exponential", EqZero(f), EqZero(substituted), ("y", Pow(baseExpr, Mul(x, Num(new BigRational(1) / new BigRational(m))))));
        if (Candidates(substituted, y, depth + 1, c) is not { } inner) return null;
        if (inner.All) return Cand.Everything;

        var points = new List<Expr>();
        foreach (var root in inner.Points)
        {
            if (Value(root) is not { } v || v <= 0) continue;
            points.Add(LogOf(root, b0, m));
        }
        if (inner.Points.Count > 0 && points.Count < inner.Points.Count || inner.Points.Count == 0)
        {
            Record(c, "alg.exp.one", "positive-values-only", EqZero(substituted), EqZero(substituted));
        }
        Record(c, "alg.log.def", "back-substitute", EqZero(substituted), Disjunction(points.Select(p => (Expr)new Apply(Operators.Eq, [x, p]))));
        return Cand.Of(points, inner.Complete, inner.Approximate || points.Any(p => p is Float));
    }

    // A base b0 (null for e) with every base a rational power of it, and the exponents r_i.
    private static (BigRational? Base, List<BigRational> Ratios)? CommonBase(List<BigRational?> bases)
    {
        if (bases.All(b => b is null)) return (null, bases.Select(_ => BigRational.One).ToList());
        if (bases.Any(b => b is null)) return null;
        foreach (var candidate in bases)
        {
            var w = PrimeVector(candidate!.Value);
            if (w is null) continue;
            var ratios = new List<BigRational>();
            foreach (var b in bases)
            {
                var v = PrimeVector(b!.Value);
                if (v is null || Ratio(v, w) is not { } r) { ratios = []; break; }
                ratios.Add(r);
            }
            if (ratios.Count == bases.Count) return (candidate, ratios);
        }
        return null;
    }

    // m·log_b0(y): exact when y is a rational power of b0.
    private static Expr LogOf(Expr y, BigRational? b0, BigInteger m)
    {
        var factor = new BigRational(m);
        if (y is Number { Value: var one } && one == BigRational.One) return Num(0);
        if (b0 is { } b && y is Number { Value: var value } && PrimeVector(value) is { } vy && PrimeVector(b) is { } vb && Ratio(vy, vb) is { } exponent) return Canon(Num(factor * exponent));
        if (b0 is null) return Canon(Mul(Num(factor), new Apply(Operators.Ln, [y])));
        return Canon(Mul(Num(factor), new Apply(Operators.Ln, [y]), Pow(new Apply(Operators.Ln, [Num(b0.Value)]), Num(-1))));
    }

    // c1·B1^g1 + c2·B2^g2 = 0 with unrelated bases: take logarithms of both sides, which is linear in x.
    private static Cand? TwoTermLogarithms(Expr f, Symbol x, List<(Expr Node, BigRational? Base, BigRational A, BigRational D)> parsed, Ctx c)
    {
        if (f is not Apply { Operator.Id: "add", Arguments: [var t1, var t2] }) return null;
        (BigRational C, int Index)? Term(Expr t)
        {
            var i = parsed.FindIndex(p => p.Node.Equals(t));
            if (i >= 0) return (BigRational.One, i);
            if (t is Apply { Operator.Id: "mul", Arguments: [Number { Value: var k }, var rest] } && parsed.FindIndex(p => p.Node.Equals(rest)) is var j and >= 0) return (k, j);
            return null;
        }
        if (Term(t1) is not var (c1, i1) || Term(t2) is not var (c2, i2) || i1 == i2) return null;
        var ratio = -c2 / c1;
        if (ratio.Sign <= 0) return Cand.None;
        Expr Ln(BigRational? b) => b is null ? Num(1) : new Apply(Operators.Ln, [Num(b.Value)]);
        // ln(c1) + (a1 x + d1) ln b1 = ln(-c2) + (a2 x + d2) ln b2
        var (p1, p2) = (parsed[i1], parsed[i2]);
        var slope = Add(Mul(Num(p1.A), Ln(p1.Base)), Negate(Mul(Num(p2.A), Ln(p2.Base))));
        var constant = Add(new Apply(Operators.Ln, [Num(c1)]), Mul(Num(p1.D), Ln(p1.Base)), Negate(new Apply(Operators.Ln, [Num(-c2)])), Negate(Mul(Num(p2.D), Ln(p2.Base))));
        var root = ZeroTest.Test(constant, c.Math) == ZeroTestResult.Zero ? Num(0) : Canon(Mul(Negate(constant), Pow(slope, Num(-1))));
        Record(c, "alg.eq.exponential", "take-logarithms", EqZero(f), new Apply(Operators.Eq, [x, root]));
        return Cand.Of([root]);
    }

    // ----- Logarithmic equations -----

    private static Cand? Logarithmic(Expr f, Symbol x, int depth, Ctx c)
    {
        var logs = f.Walk().Select(w => w.Expr).OfType<Apply>().Where(a => (a.Operator == Operators.Ln || a.Operator == Operators.Log) && a.Arguments[0].FreeSymbols.Contains(x)).Distinct().ToList();
        if (logs.Count == 0) return null;

        // Only ln(x) (or log(x, b)) appears: substitute u = ln(x).
        if (logs.Count == 1 && logs[0].Arguments[0].Equals(x) && (logs[0].Operator == Operators.Ln || logs[0].Arguments.Length == 1 || logs[0].Arguments[1] is Number))
        {
            var u = Fresh(f, "u");
            var inU = Canon(TopDown(f, e => e.Equals(logs[0]) ? u : null));
            if (!inU.FreeSymbols.Contains(x) && Candidates(inU, u, depth + 1, c) is { } inner)
            {
                if (inner.All) return Cand.Everything;
                Record(c, "alg.eq.logarithmic", "substitute-logarithm", EqZero(f), EqZero(inU), ("u", logs[0]));
                var baseValue = logs[0].Operator == Operators.Log && logs[0].Arguments.Length == 2 ? logs[0].Arguments[1] : logs[0].Operator == Operators.Log ? Num(10) : new Constant(ConstantId.E);
                var points = inner.Points.Select(r => Canon(Pow(baseValue, r))).ToList();
                Record(c, "alg.log.def", "exponentiate", EqZero(inU), Disjunction(points.Select(p => (Expr)new Apply(Operators.Eq, [x, p]))));
                return Cand.Of(points, inner.Complete, inner.Approximate);
            }
        }

        // Σ c_i·log(A_i) + K = 0 with one base: combine to a single logarithm and compare the arguments.
        var terms = f is Apply { Operator.Id: "add", Arguments: var parts } ? parts : [f];
        var constantTerms = new List<Expr>();
        var coefficients = new List<(BigRational C, Expr Argument)>();
        Expr? baseOfAll = null;
        foreach (var t in terms)
        {
            if (!t.FreeSymbols.Contains(x)) { constantTerms.Add(t); continue; }
            var (k, call) = t is Apply { Operator.Id: "mul", Arguments: [Number { Value: var kk }, var inner] } ? (kk, inner) : (BigRational.One, t);
            if (call is not Apply { Operator: var lop, Arguments: var largs } || (lop != Operators.Ln && lop != Operators.Log)) return null;
            Expr thisBase = lop == Operators.Ln ? new Constant(ConstantId.E) : largs.Length == 2 ? largs[1] : Num(10);
            if (baseOfAll is not null && !baseOfAll.Equals(thisBase)) return null;
            baseOfAll = thisBase;
            coefficients.Add((k, largs[0]));
        }
        if (coefficients.Count == 0 || baseOfAll is null) return null;
        var scale = coefficients.Aggregate(BigInteger.One, (acc, p) => acc / BigInteger.GreatestCommonDivisor(acc, p.C.Denominator) * p.C.Denominator);
        Expr positive = Num(1), negative = Num(1);
        foreach (var (k, argument) in coefficients)
        {
            var e = k * new BigRational(scale);
            if (e.Sign > 0) positive = Mul(positive, Pow(argument, Num(e)));
            else negative = Mul(negative, Pow(argument, Num(-e)));
        }
        var constant = constantTerms.Count == 0 ? (Expr)Num(0) : Add(constantTerms.Count == 1 ? constantTerms[0] : new Apply(Operators.Add, [.. constantTerms]), Num(0));
        // Σ e_i log A_i = −K·scale → Π A_i^e_i = base^(−K·scale)
        var right = Polish(Pow(baseOfAll, Mul(Negate(constant), Num(new BigRational(scale)))), c);
        var equation = Canon(Add(positive, Negate(Mul(right, negative))));
        if (!equation.FreeSymbols.Contains(x)) return null;
        Record(c, "alg.eq.logarithmic", "combine-logarithms", EqZero(f), EqZero(equation), ("base", baseOfAll));
        return Candidates(equation, x, depth + 1, c);
    }

    // ----- Trigonometric equations -----

    private static readonly string[] TrigNames = ["sin", "cos", "tan", "cot", "sec", "csc"];

    private static Cand? Trigonometric(Expr f, Symbol x, int depth, Ctx c)
    {
        var calls = f.Walk().Select(w => w.Expr).OfType<Apply>().Where(a => TrigNames.Contains(a.Operator.Id) && a.Arguments[0].FreeSymbols.Contains(x)).Distinct().ToList();
        if (calls.Count == 0) return null;

        // One common argument g = a·x + d: expand multiples of the angle when the arguments differ.
        var arguments = calls.Select(a => a.Arguments[0]).Distinct().ToList();
        if (arguments.Count > 1)
        {
            var expanded = f;
            for (var round = 0; round < 3 && expanded.Walk().Select(w => w.Expr).OfType<Apply>().Where(a => TrigNames.Contains(a.Operator.Id)).Select(a => a.Arguments[0]).Distinct().Count() > 1; round++)
            {
                if (Transforms.TrigExpand.Run(expanded, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var next } && !next.Equals(expanded)) expanded = next;
                else break;
            }
            if (expanded.Equals(f)) return null;
            Record(c, "trig.mult.sin-double", "expand-angles", EqZero(f), EqZero(expanded));
            return Candidates(expanded, x, depth + 1, c) ?? null;
        }
        var argument = arguments[0];
        if (LinearArgument(argument, x) is not var (a, d)) return null;

        var names = calls.Select(k => k.Operator.Id).Distinct().ToList();
        if (names.Count == 1) return SingleFunction(f, x, calls[0], a, d, depth, c);

        // sin and cos (tan, cot, sec, csc rewritten through them)
        var s = Fresh(f, "s");
        var cs = Fresh(f, "q");
        if (cs.Equals(s)) cs = new Symbol("q_2");
        var sinCall = new Apply(Operators.Sin, [argument]);
        var cosCall = new Apply(Operators.Cos, [argument]);
        var inSC = Canon(TopDown(f, e => e switch
        {
            Apply { Operator.Id: "sin" } => s,
            Apply { Operator.Id: "cos" } => cs,
            Apply { Operator.Id: "tan" } => Mul(s, Pow(cs, Num(-1))),
            Apply { Operator.Id: "cot" } => Mul(cs, Pow(s, Num(-1))),
            Apply { Operator.Id: "sec" } => Pow(cs, Num(-1)),
            Apply { Operator.Id: "csc" } => Pow(s, Num(-1)),
            _ => null,
        }));
        if (inSC.FreeSymbols.Contains(x)) return null;

        // Linear: a·sin g + b·cos g = k  →  R·sin(g + φ) = k (the coefficients may be irrational constants).
        if (PolynomialConversion.Coefficients(inSC, s) is { } bySine && bySine.Keys.All(k => k is 0 or 1) && bySine.TryGetValue(1, out var sineCoefficient) && sineCoefficient.FreeSymbols.Count == 0
            && PolynomialConversion.Coefficients(bySine.TryGetValue(0, out var rest) ? rest : Num(0), cs) is { } byCosine && byCosine.Keys.All(k => k is 0 or 1) && byCosine.TryGetValue(1, out var cosineCoefficient) && cosineCoefficient.FreeSymbols.Count == 0)
        {
            var constantTerm = byCosine.TryGetValue(0, out var k0) ? k0 : Num(0);
            if (constantTerm.FreeSymbols.Count == 0) return Harmonic(f, x, sineCoefficient, cosineCoefficient, Canon(Negate(constantTerm)), a, d, c);
        }

        // Clear denominators first (a rational function of s and q).
        if (!PolynomialConversion.TryToSparse(inSC, out var variables, out var sparse)) return null;
        var sIndex = variables.IndexOf(s);
        var qIndex = variables.IndexOf(cs);
        if (sIndex < 0 || qIndex < 0 || variables.Length != 2) return null;
        var terms = sparse.Terms.Select(t => (t.Monomial.Exponents.ElementAtOrDefault(sIndex), t.Monomial.Exponents.ElementAtOrDefault(qIndex), t.Coefficient)).ToList();

        // Even in cos (or in sin): replace q² by 1 − s² (or the other way) and solve a polynomial in one function.
        if (terms.All(t => t.Item2 % 2 == 0))
        {
            var reduced = Canon(sparse.Terms.Aggregate((Expr)Num(0), (acc, t) =>
            {
                var es = t.Monomial.Exponents.ElementAtOrDefault(sIndex);
                var eq = t.Monomial.Exponents.ElementAtOrDefault(qIndex);
                return Add(acc, Mul(Num(t.Coefficient), Pow(s, Num(es)), Pow(Add(Num(1), Negate(Pow(s, Num(2)))), Num(eq / 2))));
            }));
            Record(c, "trig.id.pythagorean", "pythagorean", EqZero(f), EqZero(reduced));
            return BasicFromPolynomial(reduced, s, "sin", argument, a, d, depth, c);
        }
        if (terms.All(t => t.Item1 % 2 == 0))
        {
            var reduced = Canon(sparse.Terms.Aggregate((Expr)Num(0), (acc, t) =>
            {
                var es = t.Monomial.Exponents.ElementAtOrDefault(sIndex);
                var eq = t.Monomial.Exponents.ElementAtOrDefault(qIndex);
                return Add(acc, Mul(Num(t.Coefficient), Pow(cs, Num(eq)), Pow(Add(Num(1), Negate(Pow(cs, Num(2)))), Num(es / 2))));
            }));
            Record(c, "trig.id.pythagorean", "pythagorean", EqZero(f), EqZero(reduced));
            return BasicFromPolynomial(reduced, cs, "cos", argument, a, d, depth, c);
        }

        // Homogeneous of degree n: divide by cos^n and solve in tan g (and check cos g = 0 afterwards).
        var degrees = terms.Select(t => t.Item1 + t.Item2).Distinct().ToList();
        if (degrees.Count == 1)
        {
            var t = Fresh(f, "t");
            var polynomialInTan = Canon(sparse.Terms.Aggregate((Expr)Num(0), (acc, term) => Add(acc, Mul(Num(term.Coefficient), Pow(t, Num(term.Monomial.Exponents.ElementAtOrDefault(sIndex)))))));
            Record(c, "trig.def.tan", "divide-by-cosine", EqZero(f), EqZero(polynomialInTan));
            var viaTan = BasicFromPolynomial(polynomialInTan, t, "tan", argument, a, d, depth, c);
            var halfTurn = new ImageFamily(new Symbol("k", Sort.Integer), Canon(Mul(Add(Mul(new Constant(ConstantId.Pi), Half), Mul(new Constant(ConstantId.Pi), new Symbol("k", Sort.Integer)), Negate(d)), Num(1 / a))));
            return viaTan is null ? null : viaTan with { Families = viaTan.Families.Add(halfTurn) };
        }
        _ = sinCall;
        _ = cosCall;

        // Otherwise factor the polynomial in sin and cos: a product is zero when a factor is.
        if (Transforms.Factor.Run(f, new RewriteContext(c.Math, c.Budget, ProvisoMode.Generic)) is Outcome<Expr>.Success { Value: var factored } && factored is Apply { Operator.Id: "mul" } && !factored.Equals(f))
        {
            Record(c, "alg.eq.zero-product-solve", "factor", EqZero(f), EqZero(factored));
            return Candidates(factored, x, depth + 1, c);
        }
        return null;
    }

    private static Cand? SingleFunction(Expr f, Symbol x, Apply sample, BigRational a, Expr d, int depth, Ctx c)
    {
        var w = Fresh(f, "w");
        var inW = Canon(TopDown(f, e => e is Apply { Operator.Id: var id } call && id == sample.Operator.Id && call.Arguments[0].Equals(sample.Arguments[0]) ? w : null));
        if (inW.FreeSymbols.Contains(x)) return null;
        Record(c, "alg.eq.quadratic-in-form", "substitute-function", EqZero(f), EqZero(inW), ("w", sample));
        return BasicFromPolynomial(inW, w, sample.Operator.Id, sample.Arguments[0], a, d, depth, c);
    }

    // Solves the equation in the function value w, then f(g) = root for each real root.
    private static Cand? BasicFromPolynomial(Expr inW, Symbol w, string function, Expr argument, BigRational a, Expr d, int depth, Ctx c)
    {
        if (Candidates(inW, w, depth + 1, c) is not { } roots) return null;
        if (roots.All) return Cand.Everything;
        var families = new List<ImageFamily>();
        var approximate = roots.Approximate;
        foreach (var root in roots.Points)
        {
            var solutions = BasicSolutions(function, root, a, d, 0, c);
            families.AddRange(solutions);
            Record(c, "trig.eqn." + (function is "sec" ? "cos" : function is "csc" ? "sin" : function), "basic-equation", new Apply(Operators.Eq, [new Apply(Operators.Get(function), [argument]), root]), Disjunction(solutions.Select(s => (Expr)new Apply(Operators.Eq, [new Symbol("x"), s.Element]))));
        }
        return new Cand([], [.. families], false, roots.Complete, approximate);
    }

    // f(g) = value with g = a·x + d (shifted by an extra angle): the families x = (θ − shift − d)/a for the angles θ with f(θ) = value.
    private static List<ImageFamily> BasicSolutions(string function, Expr value, BigRational a, Expr d, int shiftUnused, Ctx c, Expr? shift = null)
    {
        var k = new Symbol("k", Sort.Integer);
        var pi = new Constant(ConstantId.Pi);
        var twoPiK = Mul(Num(2), pi, k);
        var piK = Mul(pi, k);
        var number = Value(value);
        var families = new List<ImageFamily>();
        void AddAngle(Expr theta)
        {
            var angle = shift is null ? theta : Add(theta, Negate(shift));
            families.Add(new ImageFamily(k, Canon(Mul(Add(angle, Negate(d)), Num(1 / a)))));
        }
        switch (function)
        {
            case "sin":
            case "csc":
            {
                var v = function == "csc" ? (number is { } n0 && n0 != 0 ? Canon(Pow(value, Num(-1))) : null) : value;
                if (v is null || Value(v) is not { } vn || Math.Abs(vn) > 1 + 1e-12) return families;
                var arc = ExactValues.Inverse("arcsin", v);
                AddAngle(Add(arc, twoPiK));
                var other = Canon(Add(pi, Negate(arc)));
                if (!SameMod(Value(arc), Value(other), 2 * Math.PI)) AddAngle(Add(other, twoPiK));
                break;
            }
            case "cos":
            case "sec":
            {
                var v = function == "sec" ? (number is { } n1 && n1 != 0 ? Canon(Pow(value, Num(-1))) : null) : value;
                if (v is null || Value(v) is not { } vn || Math.Abs(vn) > 1 + 1e-12) return families;
                var arc = ExactValues.Inverse("arccos", v);
                AddAngle(Add(arc, twoPiK));
                if (!SameMod(Value(arc), -Value(arc), 2 * Math.PI)) AddAngle(Add(Negate(arc), twoPiK));
                break;
            }
            case "tan":
                AddAngle(Add(ExactValues.Inverse("arctan", value), piK));
                break;
            case "cot":
                if (number is { } nc && Math.Abs(nc) < 1e-15) AddAngle(Add(Mul(pi, Half), piK));
                else AddAngle(Add(ExactValues.Inverse("arctan", Canon(Pow(value, Num(-1)))), piK));
                break;
        }
        _ = shiftUnused;
        _ = c;
        return families;
    }

    private static bool SameMod(double? a, double? b, double period)
    {
        if (a is not { } x || b is not { } y) return false;
        var q = (x - y) / period;
        return Math.Abs(q - Math.Round(q)) < 1e-9;
    }

    // a·sin g + b·cos g = k: R·sin(g + φ) = k with R = sqrt(a² + b²) and φ = atan2(b, a); the coefficients are constants, possibly irrational.
    private static Cand? Harmonic(Expr f, Symbol x, Expr ca, Expr cb, Expr ck, BigRational a, Expr d, Ctx c)
    {
        if (Value(ca) is not { } an || Value(cb) is not { } bn) return null;
        var pi = new Constant(ConstantId.Pi);
        var squares = Canon(Add(Pow(ca, Num(2)), Pow(cb, Num(2))));
        var radius = Polish(Pow(squares, Half), c);
        Expr phi = an > 0
            ? ExactValues.Inverse("arctan", Polish(Mul(cb, Pow(ca, Num(-1))), c))
            : an < 0 ? Canon(Add(ExactValues.Inverse("arctan", Polish(Mul(cb, Pow(ca, Num(-1))), c)), pi)) : Canon(Mul(Num(Math.Sign(bn)), pi, Half));
        Expr value = Value(ck) is { } kn && Math.Abs(kn) < 1e-15 ? Num(0) : Polish(Mul(ck, radius, Pow(squares, Num(-1))), c);
        Record(c, "trig.lin.sin-form", "harmonic-form", EqZero(f), new Apply(Operators.Eq, [Mul(radius, new Apply(Operators.Sin, [Add(new Symbol("g"), phi)])), ck]), ("R", radius), ("phi", phi));
        var families = BasicSolutions("sin", value, a, d, 0, c, phi);
        return new Cand([], [.. families], false, true, false);
    }
}
