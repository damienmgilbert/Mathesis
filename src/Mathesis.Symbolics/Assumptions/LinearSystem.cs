using System.Numerics;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Assumptions;

/// <summary>
/// A system of linear constraints <c>Σ aᵢxᵢ + c ≥ 0</c> or <c>&gt; 0</c> over exact rationals, decided by Fourier–Motzkin
/// elimination (Dantzig and Eaves 1973; Schrijver, <i>Theory of Linear and Integer Programming</i>, §12.2). Variables that are
/// integers get the cutting-plane tightening <c>Σ aᵢxᵢ ≥ ⌈−c⌉</c> after scaling to integer coefficients, which is sound for integer
/// points and makes <c>n &gt; 0</c> with integer <c>n</c> imply <c>n ≥ 1</c>.
/// </summary>
internal sealed class LinearSystem
{
    private const int MaxConstraints = 1500;

    private sealed record Constraint(Dictionary<int, BigRational> Coefficients, BigRational Constant, bool Strict);

    private readonly List<Constraint> _constraints = [];
    private readonly Dictionary<Expr, int> _index = [];
    private readonly List<Expr> _variables = [];
    private readonly HashSet<int> _integers = [];

    public int VariableCount => _variables.Count;

    public IReadOnlyList<Expr> Variables => _variables;

    public LinearSystem Clone()
    {
        var copy = new LinearSystem();
        foreach (var c in _constraints) copy._constraints.Add(new(new(c.Coefficients), c.Constant, c.Strict));
        foreach (var (e, i) in _index) copy._index[e] = i;
        copy._variables.AddRange(_variables);
        copy._integers.UnionWith(_integers);
        return copy;
    }

    /// <summary>The index of a variable, creating it if needed.</summary>
    public int Variable(Expr e, bool isInteger)
    {
        if (!_index.TryGetValue(e, out var i))
        {
            i = _variables.Count;
            _index[e] = i;
            _variables.Add(e);
        }
        if (isInteger) _integers.Add(i);
        return i;
    }

    public bool TryGetVariable(Expr e, out int index) => _index.TryGetValue(e, out index);

    /// <summary>Adds <c>Σ coefficients + constant ≥ 0</c> (or <c>&gt; 0</c> when <paramref name="strict"/>).</summary>
    public void Add(IReadOnlyDictionary<int, BigRational> coefficients, BigRational constant, bool strict)
    {
        var map = new Dictionary<int, BigRational>();
        foreach (var (v, c) in coefficients)
        {
            if (c != BigRational.Zero) map[v] = c;
        }
        _constraints.Add(Tighten(new(map, constant, strict)));
    }

    // Integer tightening: scale to integer coefficients, divide by their gcd, then round the constant down; strict becomes non-strict with c − 1.
    private Constraint Tighten(Constraint c)
    {
        if (c.Coefficients.Count == 0 || !c.Coefficients.Keys.All(_integers.Contains)) return c;
        var lcm = BigInteger.One;
        foreach (var coeff in c.Coefficients.Values) lcm = lcm / BigInteger.GreatestCommonDivisor(lcm, coeff.Denominator) * coeff.Denominator;
        lcm = lcm / BigInteger.GreatestCommonDivisor(lcm, c.Constant.Denominator) * c.Constant.Denominator;
        var scale = new BigRational(lcm);
        var scaled = c.Coefficients.ToDictionary(kv => kv.Key, kv => kv.Value * scale);
        var constant = c.Constant * scale;
        var gcd = BigInteger.Zero;
        foreach (var v in scaled.Values) gcd = BigInteger.GreatestCommonDivisor(gcd, v.Numerator);
        var g = new BigRational(gcd);
        foreach (var k in scaled.Keys.ToArray()) scaled[k] /= g;
        constant /= g;

        // Σ a x ≥ −constant (a integer): the least integer value is ⌈−constant⌉; for a strict inequality it is ⌊−constant⌋ + 1.
        var negated = -constant;
        var minimum = c.Strict ? FloorDiv(negated.Numerator, negated.Denominator) + 1 : -FloorDiv(-negated.Numerator, negated.Denominator);
        return new(scaled, new BigRational(-minimum), false);
    }

    private static BigInteger FloorDiv(BigInteger a, BigInteger b)
    {
        var q = BigInteger.DivRem(a, b, out var r);
        return r.Sign != 0 && (r.Sign < 0) != (b.Sign < 0) ? q - 1 : q;
    }

    /// <summary>Whether the constraints have no real (and, for integer variables, no integer) solution; <c>false</c> also when undecided.</summary>
    public bool IsInfeasible() => Eliminate(_constraints.ToList(), keep: -1, out _);

    /// <summary>
    /// The tightest bounds on variable <paramref name="variable"/> implied by the constraints, or <c>null</c> when the system is
    /// infeasible or elimination exceeded its size limit.
    /// </summary>
    public (BigRational? Lower, bool LowerOpen, BigRational? Upper, bool UpperOpen)? Project(int variable)
    {
        if (Eliminate(_constraints.ToList(), variable, out var remaining)) return null;
        if (remaining is null) return null;
        BigRational? lo = null, hi = null;
        bool loOpen = false, hiOpen = false;
        foreach (var c in remaining)
        {
            if (!c.Coefficients.TryGetValue(variable, out var a) || a == BigRational.Zero) continue;

            // a x + constant ≥ 0  →  x ≥ −constant/a (a > 0) or x ≤ −constant/a (a < 0)
            var bound = -c.Constant / a;
            if (a.Sign > 0)
            {
                if (lo is null || bound > lo || (bound == lo && c.Strict)) { lo = bound; loOpen = c.Strict; }
            }
            else if (hi is null || bound < hi || (bound == hi && c.Strict))
            {
                hi = bound;
                hiOpen = c.Strict;
            }
        }
        return (lo, loOpen, hi, hiOpen);
    }

    // Eliminates every variable except `keep` (or all when keep < 0). Returns true when a contradiction was found.
    private bool Eliminate(List<Constraint> constraints, int keep, out List<Constraint>? remaining)
    {
        remaining = null;
        var variables = new HashSet<int>(constraints.SelectMany(c => c.Coefficients.Keys));
        variables.Remove(keep);
        if (constraints.Any(Contradicts)) return true;
        constraints = [.. constraints.Where(c => c.Coefficients.Count > 0)];
        while (variables.Count > 0)
        {
            // Pick the variable that creates the fewest new constraints.
            var best = -1;
            long bestCost = long.MaxValue;
            foreach (var v in variables)
            {
                long pos = 0, neg = 0;
                foreach (var c in constraints)
                {
                    if (!c.Coefficients.TryGetValue(v, out var a)) continue;
                    if (a.Sign > 0) pos++;
                    else neg++;
                }
                var cost = pos * neg - pos - neg;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = v;
                }
            }
            variables.Remove(best);
            var positive = new List<Constraint>();
            var negative = new List<Constraint>();
            var rest = new List<Constraint>();
            foreach (var c in constraints)
            {
                if (!c.Coefficients.TryGetValue(best, out var a)) rest.Add(c);
                else if (a.Sign > 0) positive.Add(c);
                else negative.Add(c);
            }
            var seen = new HashSet<string>();
            foreach (var c in rest) seen.Add(Key(c));
            foreach (var p in positive)
            {
                foreach (var n in negative)
                {
                    var a = p.Coefficients[best];
                    var b = -n.Coefficients[best];
                    var combined = new Dictionary<int, BigRational>();
                    foreach (var (v, coeff) in p.Coefficients)
                    {
                        if (v != best) combined[v] = coeff * b;
                    }
                    foreach (var (v, coeff) in n.Coefficients)
                    {
                        if (v == best) continue;
                        combined[v] = combined.TryGetValue(v, out var existing) ? existing + coeff * a : coeff * a;
                    }
                    foreach (var k in combined.Where(kv => kv.Value == BigRational.Zero).Select(kv => kv.Key).ToArray()) combined.Remove(k);
                    var result = Tighten(new(combined, p.Constant * b + n.Constant * a, p.Strict || n.Strict));
                    if (Contradicts(result)) return true;
                    if (result.Coefficients.Count == 0) continue;
                    result = Normalize(result);
                    if (seen.Add(Key(result))) rest.Add(result);
                    if (rest.Count > MaxConstraints) return false;
                }
            }
            constraints = rest;
        }
        remaining = constraints;
        return false;
    }

    private static bool Contradicts(Constraint c) =>
        c.Coefficients.Count == 0 && (c.Strict ? c.Constant <= BigRational.Zero : c.Constant < BigRational.Zero);

    // Scale so that the first coefficient has absolute value one: equal constraints then compare equal.
    private static Constraint Normalize(Constraint c)
    {
        var first = c.Coefficients.OrderBy(kv => kv.Key).First().Value;
        var scale = BigRational.One / BigRational.Abs(first);
        return new(c.Coefficients.ToDictionary(kv => kv.Key, kv => kv.Value * scale), c.Constant * scale, c.Strict);
    }

    private static string Key(Constraint c) =>
        string.Join(",", c.Coefficients.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}")) + "|" + c.Constant + (c.Strict ? ">" : "≥");
}
