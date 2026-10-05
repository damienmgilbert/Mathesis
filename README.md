# Mathesis

Mathesis is a dependency-light .NET 10 library that represents mathematics as data: numbers, expressions, laws, theorems and proofs share one syntax tree, and every transformation the library performs can be explained step by step and checked independently.

- **Every step cites a law.** Simplifying, differentiating, integrating and solving return the steps they took, and each step points at an entry of the knowledge catalog (a verified law with its conditions, level and explanation).
- **Correct before complete.** A rule fires only when its conditions hold or can be stated as a proviso; every antiderivative is differentiated back, every solution is substituted back, every definite integral is compared with quadrature. What cannot be shown comes back as `Unevaluated`, never as a guess.
- **AOT and trim safe.** No reflection dispatch and no expression compilation; the packages use only `System.*` and `Microsoft.*` dependencies.

## Install

Mathesis is published on [NuGet](https://www.nuget.org/packages/Mathesis) as seven packages ([![NuGet](https://img.shields.io/nuget/v/Mathesis.svg)](https://www.nuget.org/packages/Mathesis)).

```text
dotnet add package Mathesis
```

The `Mathesis` package brings the other five (`Mathesis.Core`, `Mathesis.Numerics`, `Mathesis.LinearAlgebra`, `Mathesis.Symbolics`, `Mathesis.Knowledge`). It targets .NET 10. `Mathesis.Validation` is optional and is not part of the `Mathesis` package.

| Package | What it holds |
| --- | --- |
| [`Mathesis.Core`](https://www.nuget.org/packages/Mathesis.Core) | Exact numbers (`BigRational`, complex, dual, interval), `Outcome<T>`, `Budget`, provisos, polynomials |
| [`Mathesis.Numerics`](https://www.nuget.org/packages/Mathesis.Numerics) | Root finding, quadrature, differentiation, interpolation, optimization, ODE solvers |
| [`Mathesis.LinearAlgebra`](https://www.nuget.org/packages/Mathesis.LinearAlgebra) | Dense matrices and vectors, exact row reduction, determinants, inverses, decompositions |
| [`Mathesis.Symbolics`](https://www.nuget.org/packages/Mathesis.Symbolics) | The expression tree, parser, printers, normalizer, assumptions, pattern matching, rewrite engine, power series |
| [`Mathesis.Knowledge`](https://www.nuget.org/packages/Mathesis.Knowledge) | The catalog of verified laws, formulas and theorems |
| [`Mathesis`](https://www.nuget.org/packages/Mathesis) | `Cas`: simplify, differentiate, integrate, limits, series, solve, linear algebra; step explanations |
| [`Mathesis.Validation`](https://www.nuget.org/packages/Mathesis.Validation) | `System.ComponentModel.DataAnnotations` attributes that validate numbers, expressions, equations, polynomials and matrices typed into forms (optional; references `Mathesis.Symbolics`) |

## Conventions

Real mode is the default (`I` is the imaginary unit and is available), decimals are exact, bare `log` is base 10, `0^0 = 1`, odd roots of negative numbers are real (`(-8)^(1/3) = -2`), and the display rounds half away from zero. Results are `Outcome<T>`: `Success` (with the steps, the provisos the answer depends on and a verification status), `Partial` (a budget ran out), `Unevaluated` (no method applies, with the reason) or `Failed`. Exceptions are for API misuse only. Every symbolic operation takes an optional `Budget`, and a `MathContext` carries assumptions and the curriculum level that restricts which laws may be cited.

## Examples

Every example below is executed by `tests/Mathesis.Tests/ReadmeTests.cs`, which compares each output with the text shown here. They assume

```csharp
using Mathesis;
using Mathesis.Calculus;
using Mathesis.Explanation;
using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Solving;
using Mathesis.Symbolics;
```

### 1. Parse and print

```csharp
var e = Expr.Parse("(x + 1)^2 / (x - 1)");
Console.WriteLine(e);
Console.WriteLine(e.ToLatex());
```

Output:

```text
(x + 1)^2/(x - 1)
\frac{(x + 1)^{2}}{x - 1}
```

### 2. Build with C# and evaluate

```csharp
var x = Sym.Symbol("x");
Expr f = Sym.Sin(x) * Sym.Sin(x) + Sym.Cos(x) * Sym.Cos(x) + Sym.Number(3);
var at = new Dictionary<Symbol, Expr> { [x] = Sym.Number(2) };
Console.WriteLine(f);
Console.WriteLine(((Outcome<Expr>.Success)Cas.Evaluate(Expr.Parse("x^2 + 1/3"), at)).Value);   // exact
Console.WriteLine(((Outcome<double>.Success)Cas.N(Expr.Parse("sqrt(2)"))).Value);               // double precision
```

Output:

```text
sin(x)*sin(x) + cos(x)*cos(x) + 3
13/3
1.4142135623730951
```

### 3. Simplify with steps

```csharp
var outcome = (Outcome<Expr>.Success)Cas.Simplify(Expr.Parse("sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)"));
Console.WriteLine(outcome.Steps.Render(ExplanationFormat.Text, Verbosity.Standard));
Console.WriteLine("provisos: " + outcome.Provisos);   // the cancellation is valid only where x - 1 is not zero
```

Output:

```text
Start: sin(x)^2 + cos(x)^2 + (x^2 - 1)/(x - 1)
1. sin²x + cos²x = 1.
   (x^2 - 1)/(x - 1) + cos(x)^2 + sin(x)^2  →  1 + (x^2 - 1)/(x - 1)
2. x − c divides p exactly when p(c) = 0.
   1 + (x^2 - 1)/(x - 1)  →  1 + (x - 1)*(x + 1)/(x - 1)
3. A non-zero number times its reciprocal is 1.
   1 + (x - 1)*(x + 1)/(x - 1)  →  x + 2
Result: x + 2

provisos: -1 + x != 0
```

### 4. Expand and factor

```csharp
var x = Sym.Symbol("x");
Console.WriteLine(((Outcome<Expr>.Success)Cas.Expand(Expr.Parse("(x + 1)^3 - (x - 1)^3"))).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.Factor(Expr.Parse("x^3 - 6*x^2 + 11*x - 6"))).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.CompleteSquare(Expr.Parse("x^2 + 4*x + 7"), x)).Value);
```

Output:

```text
2 + 6x^2
(-3 + x)*(-2 + x)*(-1 + x)
3 + (2 + x)^2
```

### 5. Together, apart and cancel

```csharp
var x = Sym.Symbol("x");
Console.WriteLine(((Outcome<Expr>.Success)Cas.Together(Expr.Parse("1/x + 1/(x + 1)"))).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.Apart(Expr.Parse("(x + 3)/((x + 1)*(x + 2))"), x)).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.Cancel(Expr.Parse("(x^2 - 4)/(x^2 - 5*x + 6)"))).Value);
```

Output:

```text
(1 + 2x)/(x*(1 + x))
2/(1 + x) - (2 + x)^-1
(2 + x)/(-3 + x)
```

### 6. Trigonometric and logarithmic forms

```csharp
// Logarithm laws need positive arguments: say so with assumptions.
var positive = new MathContext().Assume(Expr.Parse("x > 0")).Assume(Expr.Parse("y > 0"));
Console.WriteLine(((Outcome<Expr>.Success)Cas.TrigExpand(Expr.Parse("sin(2*x)"))).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.TrigReduce(Expr.Parse("sin(x)^2"))).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.LogCombine(Expr.Parse("ln(x) + ln(y)"), positive)).Value);
```

Output:

```text
2cos(x)*sin(x)
1/2*(1 - cos(2x))
ln(x*y)
```

### 7. Differentiate with steps

```csharp
var x = Sym.Symbol("x");
var outcome = (Outcome<Expr>.Success)Cas.Differentiate(Expr.Parse("sin(x^2)"), x);
Console.WriteLine(outcome.Steps.Render(ExplanationFormat.Text, Verbosity.Standard));
```

Output:

```text
Start: diff(sin(x^2), x)
1. The derivative of f(g(x)) is f′(g(x))·g′(x).
   diff(sin(x^2), x)  →  cos(x^2)*diff(x^2, x)
2. d/dx x^2 = 2·x^(2−1).
   cos(x^2)*diff(x^2, x)  →  2x*cos(x^2)
Result: 2x*cos(x^2)
```

### 8. Implicit and higher derivatives

```csharp
var x = Sym.Symbol("x");
var y = Sym.Symbol("y");
Console.WriteLine(((Outcome<Expr>.Success)Cas.ImplicitDerivative(Expr.Parse("x^2 + y^2 = 25"), y, x)).Value);   // dy/dx
Console.WriteLine(((Outcome<Expr>.Success)Cas.Differentiate(Expr.Parse("x^5"), x, 3)).Value);                   // third derivative
```

Output:

```text
-(x/y)
60x^2
```

### 9. Integrate, checked by differentiating back

```csharp
var x = Sym.Symbol("x");
var outcome = (Outcome<Expr>.Success)Cas.Integrate(Expr.Parse("x*cos(x)"), x);
Console.WriteLine(outcome.Value);
Console.WriteLine(outcome.Check);   // Verified: the derivative minus the integrand was zero-tested
Console.WriteLine(outcome.Steps.Render(ExplanationFormat.Markdown, Verbosity.Brief));
```

Output:

```text
cos(x) + x*sin(x)
Verified
Start: $\int x \cdot \cos(x) \,dx$

1. ∫ u dv = uv − ∫ v du.

Result: $\cos(x) + x \cdot \sin(x)$
```

### 10. Definite and improper integrals

```csharp
var x = Sym.Symbol("x");
Console.WriteLine(((Outcome<Expr>.Success)Cas.Integrate(Expr.Parse("x^2"), x, Expr.Parse("0"), Expr.Parse("3"))).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.Integrate(Expr.Parse("1/(x^2 + 1)"), x, Expr.Parse("-oo"), Expr.Parse("oo"))).Value);
// A divergent integral is not returned as a number.
Console.WriteLine(Cas.Integrate(Expr.Parse("1/x"), x, Expr.Parse("0"), Expr.Parse("1")) is Outcome<Expr>.Unevaluated u ? "diverges: " + u.Reason : "?");
```

Output:

```text
9
pi
diverges: The integral diverges or an endpoint value could not be found.
```

### 11. Limits

```csharp
var x = Sym.Symbol("x");
string Show(Outcome<LimitResult> o) => ((Outcome<LimitResult>.Success)o).Value.ToExpression().ToString()!;
Console.WriteLine(Show(Cas.Limit(Expr.Parse("sin(x)/x"), x, Expr.Parse("0"))));
Console.WriteLine(Show(Cas.Limit(Expr.Parse("(1 + 1/x)^x"), x, Expr.Parse("oo"))));
Console.WriteLine(Show(Cas.Limit(Expr.Parse("1/x"), x, Expr.Parse("0"), LimitDirection.FromRight)));
Console.WriteLine(Show(Cas.Limit(Expr.Parse("1/x"), x, Expr.Parse("0"))));   // the two sides differ: undefined
```

Output:

```text
1
e
oo
undefined
```

### 12. Taylor and Laurent series

```csharp
var x = Sym.Symbol("x");
Console.WriteLine(((Outcome<Expr>.Success)Cas.Taylor(Expr.Parse("exp(x)"), x, Expr.Parse("0"), 4)).Value);
Console.WriteLine(((Outcome<Expr>.Success)Cas.Series(Expr.Parse("1/(x*(1 - x))"), x, Expr.Parse("0"), 2)).Value);   // negative powers at a pole
```

Output:

```text
1 + x + 1/2*x^2 + 1/6*x^3 + 1/24*x^4
1 + x^-1 + x + x^2
```

### 13. Solve an equation, with an extraneous solution

```csharp
var x = Sym.Symbol("x");
var outcome = (Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("sqrt(x + 2) = x"), x);
Console.WriteLine(outcome.Value);
Console.WriteLine(outcome.Steps.Render(ExplanationFormat.Text, Verbosity.Brief));   // x = -1 is rejected in the last step
```

Output:

```text
{2}
Start: √(x + 2) = x
1. Isolate the radical, raise both sides to a power, solve, and check every candidate.
2. Squaring both sides is not reversible (the converse is false), so every candidate must be checked in the original equation.
3. The roots of -1x² + 1x + 2 = 0 are x = (−1 ± sqrt(1² − 4(-1) · 2))/(2(-1)).
4. A candidate produced by a step that is not an equivalence and that fails the original equation. Candidate: -1 (does not satisfy the equation).
Result: {2}
```

### 14. Trigonometric equations and inequalities

```csharp
var x = Sym.Symbol("x");
Console.WriteLine(((Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("2*sin(x)^2 - sin(x) - 1 = 0"), x)).Value);   // image sets over k
Console.WriteLine(((Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("(x - 1)/(x + 2) >= 0"), x)).Value);          // intervals
Console.WriteLine(((Outcome<SolutionSet>.Success)Cas.Solve(Expr.Parse("x^5 - x - 1 = 0"), x)).Value);               // numeric root: no closed form
```

Output:

```text
{1/2*pi + 2pi*k | k in Z} ∪ {-(1/6*pi) + 2pi*k | k in Z} ∪ {7/6*pi + 2pi*k | k in Z}
]-oo, -2[ ∪ [1, oo)
{1.1673039782614187}
```

### 15. Systems and exact linear algebra

```csharp
var x = Sym.Symbol("x");
var y = Sym.Symbol("y");
Console.WriteLine(((Outcome<SolutionSet>.Success)Cas.Solve([Expr.Parse("x + y = 3"), Expr.Parse("x - y = 1")], [x, y])).Value);
Console.WriteLine(((Outcome<SolutionSet>.Success)Cas.Solve([Expr.Parse("x^2 + y^2 = 25"), Expr.Parse("x + y = 7")], [x, y])).Value);
var a = DenseMatrix.Create(2, 2, (r, c) => new BigRational(new[,] { { 2, 1 }, { 1, 3 } }[r, c]));
Console.WriteLine(Cas.Determinant(a));
Console.WriteLine(Cas.RowReduce(a).Operations.Length + " row operations");
```

Output:

```text
{(2, 1)}
{(3, 4), (4, 3)}
5
4 row operations
```

### 16. Look up the catalog and apply a law

```csharp
var entry = Cas.Get("alg.factor.diff-squares");
Console.WriteLine($"{entry.Id.Value}: {entry.Name} ({entry.Level})");
Console.WriteLine(entry.Explain);
Console.WriteLine(Cas.Find("pythagorean").First().Id.Value);
var applied = (Outcome<Expr>.Success)Cas.Apply("trig.sum.sin-of-sum", Expr.Parse("sin(a + b)"));
Console.WriteLine(applied.Value);
```

Output:

```text
alg.factor.diff-squares: Difference of squares (Algebra1)
{a}² − {b}² is a difference of squares, so it factors as ({a} − {b})({a} + {b}).
trig.id.pythagorean
cos(b)*sin(a) + cos(a)*sin(b)
```

## Validating input

`Mathesis.Validation` adds seven `System.ComponentModel.DataAnnotations` attributes for forms: `RationalNumber`, `ExactRange` and `NonZero` for numbers, `MathExpression` and `MathEquation` for expressions and equations, `PolynomialExpression` and `MathMatrix`. They are ordinary validation attributes, so Blazor, MAUI, WPF, WinUI and Windows Forms use them the way they use `[Required]`. A failure is a `MathValidationResult` with a stable `Code`, the `Span` of the text concerned and a `Suggestion` when one is known. Input is only parsed and inspected, never evaluated, so even `9^9^9` costs nothing. `Check` validates a value without a `ValidationContext`. These examples also assume `using System.ComponentModel.DataAnnotations;` and `using Mathesis.Validation;`.

### 17. Numbers, compared exactly

```csharp
var range = new ExactRangeAttribute("0", "3/10");
Console.WriteLine(range.Check(0.1 + 0.2, "Total")?.ErrorMessage ?? "valid");
Console.WriteLine(range.Check("0.3", "Total")?.ErrorMessage ?? "valid");
var comma = new RationalNumberAttribute().Check("0,5", "Price")!;
Console.WriteLine($"{comma.Code}: {comma.Suggestion}");
```

Output:

```text
Total must be in the range [0, 3/10].
valid
NotANumber: Use '.' as the decimal point, for example 0.5. Digit grouping is not accepted.
```

### 18. Expressions and equations

```csharp
var formula = new MathExpressionAttribute { Variables = ["x"], DisallowedFamilies = OperatorFamily.Trig };
Console.WriteLine(formula.Check("x^2 + y", "Formula")?.ErrorMessage);
Console.WriteLine(formula.Check("sin(x)", "Formula")?.ErrorMessage);
var syntax = formula.Check("2x +", "Formula")!;
Console.WriteLine($"{syntax.ErrorMessage} (column {syntax.Span!.Value.Start + 1})");
Console.WriteLine(new MathEquationAttribute().Check("x^2 - 4", "Equation")?.ErrorMessage);
```

Output:

```text
Formula uses a variable that is not allowed: y.
Formula uses a function that is not allowed: sin.
Formula is not valid: Expected an expression but the input ended. (column 5)
Equation must be an equation, but it is an expression.
```

### 19. Polynomials

```csharp
var cubic = new PolynomialExpressionAttribute("x") { MaxDegree = 3 };
Console.WriteLine(cubic.Check("(x + 1)^4 - x^4", "Polynomial")?.ErrorMessage ?? "valid");
Console.WriteLine(cubic.Check("x^4 - 1", "Polynomial")?.ErrorMessage);
Console.WriteLine(cubic.Check("x^2 + 1/x", "Polynomial")?.ErrorMessage);
Console.WriteLine(cubic.Check("x*y", "Polynomial")?.ErrorMessage);
```

Output:

```text
valid
Polynomial has degree 4, but the highest allowed degree is 3.
Polynomial must be a polynomial, but it contains 1/x.
Polynomial uses a variable that is not allowed: y.
```

### 20. Matrices

```csharp
var matrix = new MathMatrixAttribute { Rows = 2, Columns = 2 };
Console.WriteLine(matrix.Check("[[1, -1/2], [0.25, 3]]", "A")?.ErrorMessage ?? "valid");
Console.WriteLine(matrix.Check("[[1, x], [2, 3]]", "A")?.ErrorMessage);
Console.WriteLine(matrix.Check("[[1, 2, 3], [4, 5, 6]]", "A")?.ErrorMessage);
var interval = matrix.Check("[1, 2]", "A")!;
Console.WriteLine($"{interval.ErrorMessage} {interval.Suggestion}");
```

Output:

```text
valid
A must contain only numbers; the entry in row 1, column 2 is not a number.
A must be a matrix of size 2×2, but it is 2×3.
A must be a matrix, for example [[1, 2], [3, 4]]. For a column vector write [[1], [2]].
```

### 21. Validate a form model

```csharp
var form = new RootFinderForm { Polynomial = "x^7 - 1", LowerBound = "-100" };
var results = new List<ValidationResult>();
Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
foreach (var result in results)
    Console.WriteLine($"{string.Join(", ", result.MemberNames)}: {result.ErrorMessage}");

public sealed class RootFinderForm
{
    [Required, PolynomialExpression("x", MaxDegree = 6)]
    [Display(Name = "Polynomial")]
    public string? Polynomial { get; set; }

    [ExactRange("-100", "100", MinimumIsExclusive = true)]
    [Display(Name = "Lower bound")]
    public string? LowerBound { get; set; }
}
```

Output:

```text
Polynomial: Polynomial has degree 7, but the highest allowed degree is 6.
LowerBound: Lower bound must be in the range (-100, 100].
```

`Validator` and `new ValidationContext(instance)` find attributes and display names by reflection, so they are not trim-safe. Under NativeAOT, create the context with a display name, `new ValidationContext(instance, "Polynomial", null, null)`, and call `attribute.GetValidationResult(value, context)` or `attribute.Check(value)`, as `samples/AotSmoke` does. Rules across properties belong in `IValidatableObject`. Default messages are neutral English; set `ErrorMessage` or `ErrorMessageResourceType` to replace them.

## Try it

`samples/repl.cs` is a file-based REPL (`dotnet run samples/repl.cs`): type `simplify sin(x)^2 + cos(x)^2`, `solve x^2 - 4 = 0`, `integrate x*cos(x)`, `limit sin(x)/x at 0`, `find difference of squares`, or `help`.

`samples/AotSmoke` is a console app that parses, simplifies with steps, differentiates, integrates, solves, looks up the catalog and validates input with the seven attributes; it is published with NativeAOT in the release gate (`dotnet publish samples/AotSmoke -c Release -r <rid>`, which needs the platform's C++ toolchain).

[MathesisMauiApp](https://github.com/damienmgilbert/MathesisMauiApp) is a separate .NET MAUI showcase app (Windows, Android, iOS, Mac Catalyst) that uses the published `Mathesis` NuGet package. It has a page for each of the seven validation attributes, plus pages for the computer algebra system, the numerics, and the catalog. Clone it and run it to try the library in a real form-based UI. It needs the .NET MAUI workload, which is why it lives outside this repository.

## Build and test

```text
dotnet build Mathesis.slnx -c Release
dotnet test --solution Mathesis.slnx -c Release
dotnet run eng/policy-check.cs            # package policy: System.*, Microsoft.* and CommunityToolkit.* only, nothing deprecated or vulnerable
dotnet run eng/mlaw-lint.cs               # lints the catalog (.mlaw files)
dotnet run eng/gen-knowledge.cs -- --check
dotnet run eng/check-catalog-coverage.cs  # coverage of the design documents' catalog tables
dotnet pack Mathesis.slnx -c Release      # the seven packages in artifacts/packages
```

The design documents are in `docs/design` (`00-index.md` gives the reading order) and the catalog is in `knowledge/*/*.mlaw`. See `RELEASE-NOTES.md` for what the first release covers and does not.

## License

MIT. See `LICENSE`.
