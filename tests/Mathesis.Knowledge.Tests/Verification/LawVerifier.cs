using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace Mathesis.Knowledge.Tests.Verification;

internal enum VerifyOutcome
{
    /// <summary>Enough samples were valid and none was a counterexample.</summary>
    Passed,

    /// <summary>The entry is not numerically verified (kind or <c>verify</c> field).</summary>
    Skipped,

    /// <summary>A counterexample was found.</summary>
    Failed,

    /// <summary>Too few samples satisfied the conditions and were defined to decide.</summary>
    Inconclusive,
}

internal sealed record VerifyReport(string Id, VerifyOutcome Outcome, string Message, int ValidSamples, ImmutableArray<string> Warnings);

/// <summary>
/// The numeric verification harness of docs/design/06-knowledge-catalog.md ("Verification harness"): draws assignments from each variable's
/// sort, keeps those that satisfy <c>where</c>, evaluates the statement and requires it to hold at every one. Samples where the statement
/// is undefined are skipped ("both sides defined" is implied). The harness also samples points that violate <c>where</c> and warns when the
/// statement still holds at all of them.
/// </summary>
internal static class LawVerifier
{
    public const int DefaultSeed = 20261100;
    public const int WantedSamples = 200;
    public const int MinimumSamples = 30;
    private const int MaxAttempts = 6000;

    // Concrete functions standing in for function-valued variables: smooth on all of ℝ, with their derivatives.
    private static readonly (string Body, string Derivative)[] FunctionPool =
    [
        ("sin(x)", "cos(x)"),
        ("cos(x)", "-sin(x)"),
        ("exp(x/3)", "exp(x/3)/3"),
        ("x^2 + 1", "2*x"),
        ("x^3 - 2*x", "3*x^2 - 2"),
        ("1/(1 + x^2)", "-2*x/(1 + x^2)^2"),
        ("arctan(x)", "1/(1 + x^2)"),
        ("sqrt(1 + x^2)", "x/sqrt(1 + x^2)"),
        ("exp(-x^2)", "-2*x*exp(-x^2)"),
        ("x/2 + 1", "1/2"),
    ];

    private static readonly ImmutableArray<(Expr Body, Expr Derivative)> ParsedPool =
        [.. FunctionPool.Select(f => (Expr.Parse(f.Body), Expr.Parse(f.Derivative)))];

    public static VerifyReport Verify(Entry entry, int seed = DefaultSeed)
    {
        var mode = entry.EffectiveVerify;
        if (mode is not (VerifyMode.Numeric or VerifyMode.Instances)) return new(entry.Id.Value, VerifyOutcome.Skipped, $"verify: {mode.ToString().ToLowerInvariant()}", 0, []);

        var statements = Statements(entry);
        if (statements.Count == 0) return new(entry.Id.Value, VerifyOutcome.Inconclusive, "nothing to verify", 0, []);

        var warnings = new List<string>();
        var total = 0;
        foreach (var statement in statements)
        {
            var report = VerifyStatement(entry, statement, entry.Where, complexMode: false, seed, warnings);
            if (report.Outcome != VerifyOutcome.Passed) return report with { Warnings = [.. warnings] };
            total += report.ValidSamples;
        }

        // Conditions stated for complex mode are verified with complex values.
        if (entry.Complex is not null)
        {
            foreach (var statement in statements)
            {
                var report = VerifyStatement(entry, statement, entry.Complex, complexMode: true, seed + 1, warnings);
                if (report.Outcome != VerifyOutcome.Passed) return report with { Message = "complex mode: " + report.Message, Warnings = [.. warnings] };
                total += report.ValidSamples;
            }
        }
        return new(entry.Id.Value, VerifyOutcome.Passed, "ok", total, [.. warnings]);
    }

    // The propositions to verify: the statement itself, a pattern's match = yields, or each consecutive pair of a method's steps.
    private static List<Expr> Statements(Entry entry)
    {
        if (entry.Kind == EntryKind.Pattern) return entry.Match is { } m && entry.Yields is { } y ? [new Apply(Operators.Eq, [m, y])] : [];
        if (entry.Kind == EntryKind.Method)
        {
            var forms = new List<Expr>();
            if (entry.AppliesTo is not null) forms.Add(entry.AppliesTo);
            forms.AddRange(entry.Steps.Select(s => s.Form));
            if (entry.Result is not null) forms.Add(entry.Result);
            return [.. forms.Zip(forms.Skip(1), (p, q) => (Expr)new Apply(Operators.Eq, [p, q]))];
        }
        return entry.Statement is { } s ? [s] : [];
    }

    private static VerifyReport VerifyStatement(Entry entry, Expr statement, Expr? condition, bool complexMode, int seed, List<string> warnings)
    {
        var random = new Random(StableSeed(seed, entry.Id.Value));
        var indefinite = IntegrationVariable(statement);
        var tolerance = ToleranceFor(statement);
        var evaluator = new LawEvaluator { Tolerance = tolerance };

        // Numeric differentiation, integration and limits are only reliable on moderate arguments: no huge magnitudes there.
        var tame = tolerance > 1e-9;

        // Statements that mention I or have complex variables are evaluated in the complex field; real mode is the default.
        var evalComplex = complexMode || entry.Vars.Any(v => v.Sort == Sort.Complex) || statement.Walk().Any(w => w.Expr is Constant { Id: ConstantId.ImaginaryUnit }) || (condition?.Walk().Any(w => w.Expr is Constant { Id: ConstantId.ImaginaryUnit }) ?? false);

        var unsupported = entry.Vars.FirstOrDefault(v => v.Sort is SetSort or TupleSort || v.Sort == Sort.Boolean);
        if (unsupported is not null) return new(entry.Id.Value, VerifyOutcome.Inconclusive, $"variable '{unsupported.Name}' has the sort {unsupported.Sort}, which the sampler does not draw", 0, []);

        // A defining equation v = expr of a formula is "solved back" by computing v from the right-hand side, so the verifier checks that the
        // right-hand side is defined and the conditions are satisfiable, not a tautology about independent samples of v.
        var definedVariable = entry.Kind == EntryKind.Formula && statement is Apply { Operator.Id: "eq" } definition && definition.Arguments[0] is Symbol lhs
            && entry.Vars.Any(v => v.Name == lhs.Name) && !definition.Arguments[1].FreeSymbols.Contains(lhs) ? lhs.Name : null;
        var valid = 0;
        var violatingHolds = 0;
        var violatingFails = 0;
        var attempts = 0;
        while (valid < WantedSamples && attempts < MaxAttempts)
        {
            attempts++;
            var env = Draw(entry, random, complexMode, evalComplex, tame);
            if (definedVariable is not null)
            {
                var rhs = evaluator.Eval(((Apply)statement).Arguments[1], env);
                if (double.IsNaN(rhs.Real) || double.IsNaN(rhs.Imaginary)) continue;
                env.Values[definedVariable] = rhs;
            }
            bool? holds;
            if (indefinite is not null)
            {
                holds = IndefiniteHolds(evaluator, statement, condition, env, indefinite, random, tolerance);
                if (holds is null) continue;
            }
            else
            {
                var conditionHolds = condition is null ? true : evaluator.Truth(condition, env);
                var value = evaluator.Truth(statement, env);
                if (conditionHolds is null || value is null) continue;
                if (conditionHolds == false)
                {
                    if (value == true) violatingHolds++;
                    else violatingFails++;
                    continue;
                }
                holds = value;
            }
            valid++;
            if (holds == false) return new(entry.Id.Value, VerifyOutcome.Failed, "counterexample: " + Describe(entry, env), valid, []);
        }

        // Probe the conditions with more samples that violate them.
        if (condition is not null && valid > 0 && violatingHolds + violatingFails < 20)
        {
            for (var i = 0; i < 4000 && violatingHolds + violatingFails < 60 && indefinite is null; i++)
            {
                var env = Draw(entry, random, complexMode, evalComplex, tame);
                var conditionHolds = evaluator.Truth(condition, env);
                var value = evaluator.Truth(statement, env);
                if (conditionHolds != false || value is null) continue;
                if (value == true) violatingHolds++;
                else violatingFails++;
            }
        }
        if (condition is not null && violatingFails == 0 && violatingHolds >= 20) warnings.Add($"{entry.Id}: the statement also held at {violatingHolds} sample points that violate its conditions (conditions may be stronger than necessary)");

        return valid >= MinimumSamples
            ? new(entry.Id.Value, VerifyOutcome.Passed, "ok", valid, [])
            : new(entry.Id.Value, VerifyOutcome.Inconclusive, $"only {valid} valid samples in {attempts} attempts (conditions too strict or statement undefined; add sample hints)", valid, []);
    }

    // string.GetHashCode and HashCode are randomized per process; the verifier must draw the same samples on every run.
    private static int StableSeed(int seed, string id)
    {
        var hash = 2166136261u ^ (uint)seed;
        foreach (var c in id) hash = (hash ^ c) * 16777619u;
        return (int)hash;
    }

    private static double ToleranceFor(Expr statement)
    {
        var tolerance = 1e-9;
        foreach (var (node, _) in statement.Walk())
        {
            if (node is Bind { Binder: Binder.Limit }) return 3e-4;
            if (node is Bind { Binder: Binder.Integral } || node is Apply { Operator.Id: "diff" or "integrate" }) tolerance = Math.Max(tolerance, 1e-5);
        }
        return tolerance;
    }

    private static string? IntegrationVariable(Expr statement)
    {
        string? name = null;
        foreach (var (node, _) in statement.Walk())
        {
            if (node is Apply { Operator.Id: "integrate" } a && a.Arguments[1] is Symbol v)
            {
                if (name is not null && name != v.Name) return name;
                name = v.Name;
            }
        }
        return name;
    }

    // An identity between expressions with indefinite integrals holds up to a constant: the difference of the two sides must not change
    // between two points of the domain when every integral starts from the first point.
    private static bool? IndefiniteHolds(LawEvaluator evaluator, Expr statement, Expr? condition, Env env, string variable, Random random, double tolerance)
    {
        if (statement is not Apply { Operator.Id: "eq" } eq || !env.Values.TryGetValue(variable, out var start) || start.Imaginary != 0) return null;
        var x1 = start.Real;
        var x2 = x1 + 0.05 + random.NextDouble() * 0.3;
        var baseEnv = new Env { Values = env.Values, Matrices = env.Matrices, Real = env.Real, Functions = env.Functions, FunctionArgument = env.FunctionArgument, IntegralBase = new() { [variable] = x1 } };

        // The conditions must hold along the whole path, so that no singularity lies between the two points.
        for (var k = 0; k <= 8; k++)
        {
            var at = baseEnv.With(variable, x1 + (x2 - x1) * k / 8);
            if (condition is not null && evaluator.Truth(condition, at) != true) return null;
        }
        Complex<double> Difference(double x)
        {
            var at = baseEnv.With(variable, x);
            return evaluator.Eval(eq.Arguments[0], at) - evaluator.Eval(eq.Arguments[1], at);
        }
        var d1 = Difference(x1);
        var d2 = Difference(x2);
        if (double.IsNaN(d1.Real) || double.IsNaN(d2.Real) || d1.Imaginary != 0 || d2.Imaginary != 0) return null;
        var scale = Math.Max(1, Math.Max(Math.Abs(evaluator.Eval(eq.Arguments[0], baseEnv.With(variable, x2)).Real), Math.Abs(evaluator.Eval(eq.Arguments[1], baseEnv.With(variable, x2)).Real)));
        return Math.Abs(d2.Real - d1.Real) <= tolerance * scale;
    }

    // ----- Sampling -----

    private static Env Draw(Entry entry, Random random, bool complexMode, bool evalComplex, bool tame)
    {
        var values = new Dictionary<string, Complex<double>>();
        var functions = new Dictionary<string, (Expr, Expr?)>();
        var matrices = new Dictionary<string, Complex<double>[,]>();

        // Scalars first: a matrix dimension may be the value of a scalar variable (matrix(n, n) with n: natural).
        foreach (var v in entry.Vars)
        {
            if (v.Sort is MatrixSort or VectorSort) continue;
            var hint = entry.Sample.FirstOrDefault(h => h.Variable == v.Name);
            if (v.Sort is FunctionSort)
            {
                var (body, derivative) = ParsedPool[random.Next(ParsedPool.Length)];
                functions[v.Name] = (body, derivative);
                continue;
            }
            values[v.Name] = DrawValue(v.Sort, hint, random, complexMode, tame);
        }

        var freeDimensions = new Dictionary<string, int>();
        int Dimension(Expr dimension)
        {
            if (dimension is Number { Value.IsInteger: true } n) return (int)n.Value.Numerator;
            if (dimension is Symbol s)
            {
                if (values.TryGetValue(s.Name, out var value) && value.Real >= 1 && value.Real <= 6) return (int)value.Real;
                if (!freeDimensions.TryGetValue(s.Name, out var chosen)) freeDimensions[s.Name] = chosen = random.Next(1, 5);
                return chosen;
            }
            return 3;
        }
        foreach (var v in entry.Vars)
        {
            var (rows, columns) = v.Sort switch
            {
                MatrixSort m => (Dimension(m.Rows), Dimension(m.Columns)),
                VectorSort vec => (Dimension(vec.Length), 1),
                _ => (0, 0),
            };
            if (rows == 0) continue;
            var matrix = new Complex<double>[rows, columns];
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++) matrix[i, j] = new(random.Next(3) == 0 ? random.NextDouble() * 6 - 3 : random.Next(-4, 5));
            }
            matrices[v.Name] = matrix;
        }

        // Function-valued variables standing alone are evaluated at x if the entry has a real variable x, else at t, else at the first real variable.
        var real = entry.Vars.Where(v => v.Sort is not FunctionSort).Select(v => v.Name).ToList();
        var argument = real.Contains("x") ? "x" : real.Contains("t") ? "t" : real.FirstOrDefault() ?? "x";
        return new Env { Values = values, Functions = functions, Matrices = matrices, Real = !evalComplex, FunctionArgument = argument };
    }

    private static Complex<double> DrawValue(Sort sort, SampleHint? hint, Random random, bool complexMode, bool tame)
    {
        if (hint is not null)
        {
            if (hint.IsInteger) return new(random.Next((int)hint.Low, (int)hint.High + 1));
            var lo = hint.Low;
            var hi = hint.High;
            double u;
            do
            {
                u = lo + (hi - lo) * random.NextDouble();
            }
            while ((hint.LowOpen && u <= lo) || (hint.HighOpen && u >= hi));
            return new(u);
        }
        if (sort == Sort.Natural) return new(random.Next(0, 13));
        if (sort == Sort.Integer) return new(random.Next(-12, 13));
        if (sort == Sort.Rational) return new(SimpleRational(random));

        // In complex mode a real-declared variable is real half the time and complex otherwise: statements about it may use Re, Im or order
        // conditions, and a condition that is undefined (an order on a non-real number) skips the sample.
        if (sort == Sort.Complex || (complexMode && sort == Sort.Real && random.Next(2) == 0)) return new(Real(random, 5, tame), Real(random, 5, tame));
        return new(Real(random, tame ? 3 : 10, tame));
    }

    private static double SimpleRational(Random random) => (double)random.Next(-12, 13) / random.Next(1, 9);

    private static double Real(Random random, double span, bool tame = false)
    {
        var strategy = random.Next(100);
        if (strategy < 22) return random.Next(-5, 6);
        if (strategy < 44) return SimpleRational(random);
        if (strategy < 76) return (random.NextDouble() * 2 - 1) * span;
        if (strategy < 84) return (random.NextDouble() * 2 - 1) * 1e-2;
        if (strategy < 93 || tame) return 0.01 + random.NextDouble() * 3;
        return (random.Next(2) == 0 ? -1 : 1) * (span + random.NextDouble() * 20 * span);
    }

    private static string Describe(Entry entry, Env env)
    {
        var parts = entry.Vars.Select(v => env.Values.TryGetValue(v.Name, out var z) ? $"{v.Name} = {Format(z)}" : env.Matrices.TryGetValue(v.Name, out var m) ? $"{v.Name} = {MatrixText(m)}" : $"{v.Name} = (function)");
        var functions = env.Functions.Select(f => $"{f.Key}(x) = {f.Value.Body}");
        return string.Join(", ", parts.Concat(functions));
    }

    private static string MatrixText(Complex<double>[,] m)
    {
        var rows = Enumerable.Range(0, m.GetLength(0)).Select(i => "[" + string.Join(", ", Enumerable.Range(0, m.GetLength(1)).Select(j => Format(m[i, j]))) + "]");
        return "[" + string.Join(", ", rows) + "]";
    }

    private static string Format(Complex<double> z) => z.Imaginary == 0 ? z.Real.ToString("R") : $"{z.Real:R} + {z.Imaginary:R}i";
}
