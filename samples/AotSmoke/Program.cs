using Mathesis;
using Mathesis.Explanation;
using Mathesis.Solving;
using Mathesis.Symbolics;

// A NativeAOT smoke test: parse, simplify with steps, differentiate, integrate, solve and look up the catalog.
// It exits with code 1 and a message if any answer is wrong, so a successful run proves the AOT build computes correctly.
Console.OutputEncoding = System.Text.Encoding.UTF8;
var x = new Symbol("x");
var failures = 0;

void Check(string name, bool ok, string detail)
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}: {detail}");
    if (!ok) failures++;
}

var parsed = Expr.Parse("sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)");
Check("parse", parsed.ToString() == "sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)", parsed.ToString()!);

var simplified = Cas.Simplify(parsed);
if (simplified is Outcome<Expr>.Success { Value: var simple, Steps: var steps })
{
    Check("simplify", simple.ToString() == "2 + x", simple.ToString()!);
    Console.WriteLine(steps.Render(ExplanationFormat.Text, Verbosity.Standard));
}
else
{
    Check("simplify", false, simplified.ToString()!);
}

var derivative = Cas.Differentiate(Expr.Parse("x*exp(x)"), x);
Check("differentiate", derivative is Outcome<Expr>.Success { Value: var d } && d.ToString() == "exp(x) + x*exp(x)", derivative is Outcome<Expr>.Success s1 ? s1.Value.ToString()! : derivative.ToString()!);

var integral = Cas.Integrate(Expr.Parse("x*cos(x)"), x);
Check("integrate", integral is Outcome<Expr>.Success { Check: Verification.Verified }, integral is Outcome<Expr>.Success s2 ? s2.Value.ToString()! : integral.ToString()!);

var solved = Cas.Solve(Expr.Parse("x^2 - 5*x + 6 = 0"), x);
Check("solve", solved is Outcome<SolutionSet>.Success { Value.Points.Length: 2 }, solved is Outcome<SolutionSet>.Success s3 ? s3.Value.ToString()! : solved.ToString()!);

var entry = Cas.Get("alg.factor.diff-squares");
Check("catalog", entry.Name == "Difference of squares", $"{entry.Id.Value}: {entry.Name}");

return failures == 0 ? 0 : 1;
