using System.ComponentModel.DataAnnotations;
using Mathesis;
using Mathesis.Explanation;
using Mathesis.Solving;
using Mathesis.Symbolics;
using Mathesis.Validation;

// A NativeAOT smoke test: parse, simplify with steps, differentiate, integrate, solve, look up the catalog and validate input.
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

// Mathesis.Validation through the trim-safe entry points only: a ValidationContext with a display name, and GetValidationResult.
// Validator.* and new ValidationContext(instance) use reflection and are not trim-safe, so the smoke test never calls them.
var form = new object();
MathValidationResult? Validate(ValidationAttribute attribute, object? value, string displayName = "Value") =>
    attribute.GetValidationResult(value, new ValidationContext(form, displayName, serviceProvider: null, items: null) { MemberName = "Field" }) as MathValidationResult;
string Show(MathValidationResult? result) => result is null ? "valid" : $"{result.Code}: {result.ErrorMessage}";

var comma = Validate(new RationalNumberAttribute(), "0,5");
Check("rational number", Validate(new RationalNumberAttribute(), "3/4") is null && comma is { Code: MathValidationCode.NotANumber, Suggestion: { } hint } && hint.Contains("'.'", StringComparison.Ordinal), Show(comma));

var range = Validate(new ExactRangeAttribute("0", "1"), 0.1 + 0.2 + 0.7 + 0.1);
Check("exact range", Validate(new ExactRangeAttribute("0", "1"), "1/2") is null && range is { Code: MathValidationCode.OutOfRange }, Show(range));

var zero = Validate(new NonZeroAttribute(), -0.0);
Check("non-zero", zero is { Code: MathValidationCode.Zero } && Validate(new NonZeroAttribute(), "1e-100000") is null, Show(zero));

var unknown = Validate(new MathExpressionAttribute { Variables = ["x"] }, "x^2 + y");
Check("expression", unknown is { Code: MathValidationCode.UnknownVariable, ErrorMessage: "Value uses a variable that is not allowed: y." }, Show(unknown));

var shape = Validate(new MathEquationAttribute(), "x^2 - 4");
Check("equation", shape is { Code: MathValidationCode.WrongShape } && Validate(new MathEquationAttribute(), "x^2 = 4") is null, Show(shape));

var degree = Validate(new PolynomialExpressionAttribute("x") { MaxDegree = 3 }, "(x + 1)^5 - x^5");
Check("polynomial", degree is { Code: MathValidationCode.DegreeTooHigh } && Validate(new PolynomialExpressionAttribute("x"), "1/x") is { Code: MathValidationCode.NotAPolynomial }, Show(degree));

var entryCheck = Validate(new MathMatrixAttribute { Rows = 2, Columns = 2 }, "[[1, x], [2, 3]]");
Check("matrix", entryCheck is { Code: MathValidationCode.NonNumericEntry } && Validate(new MathMatrixAttribute { Rows = 2, Columns = 2 }, "[[1, -1/2], [0.25, 4]]") is null, Show(entryCheck));

var syntax = Validate(new MathExpressionAttribute(), "x +");
Check("syntax", syntax is { Code: MathValidationCode.Syntax, Span: { } } && Validate(new MathExpressionAttribute { Format = InputFormat.Latex }, @"\frac{1}{2} x^{2}") is null, Show(syntax));

// The default messages come from the embedded Messages.resx; this one proves the resources survive the AOT build.
var message = Validate(new ExactRangeAttribute("0", "1"), "3/2", "Probability");
Check("resx message", message?.ErrorMessage == "Probability must be in the range [0, 1].", message?.ErrorMessage ?? "no message");

var configuration = new ExactRangeAttribute("5", "1").GetConfigurationError();
Check("configuration", configuration == "ExactRangeAttribute: The minimum 5 is above the maximum 1.", configuration ?? "no error");

return failures == 0 ? 0 : 1;
