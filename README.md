# Mathesis

Mathesis is a dependency-light .NET 10 library that represents mathematics as data: numbers, expressions, laws, theorems and proofs share one syntax tree, and every transformation the library performs can be explained step by step and checked independently.

- **Every step cites a law.** Simplifying, differentiating, integrating and solving return the steps they took, and each step points at an entry of the knowledge catalog (a verified law with its conditions, level and explanation).
- **Correct before complete.** A rule fires only when its conditions hold or can be stated as a proviso; every antiderivative is differentiated back, every solution is substituted back, every definite integral is compared with quadrature. What cannot be shown comes back as `Unevaluated`, never as a guess.
- **AOT and trim safe.** No reflection dispatch and no expression compilation; the packages use only `System.*` and `Microsoft.*` dependencies.

## Install

```text
dotnet add package Mathesis
```

The `Mathesis` package brings the other five (`Mathesis.Core`, `Mathesis.Numerics`, `Mathesis.LinearAlgebra`, `Mathesis.Symbolics`, `Mathesis.Knowledge`). It targets .NET 10.

| Package | What it holds |
| --- | --- |
| `Mathesis.Core` | Exact numbers (`BigRational`, complex, dual, interval), `Outcome<T>`, `Budget`, provisos, polynomials |
| `Mathesis.Numerics` | Root finding, quadrature, differentiation, interpolation, optimization, ODE solvers |
| `Mathesis.LinearAlgebra` | Dense matrices and vectors, exact row reduction, determinants, inverses, decompositions |
| `Mathesis.Symbolics` | The expression tree, parser, printers, normalizer, assumptions, pattern matching, rewrite engine, power series |
| `Mathesis.Knowledge` | The catalog of verified laws, formulas and theorems |
| `Mathesis` | `Cas`: simplify, differentiate, integrate, limits, series, solve, linear algebra; step explanations |

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

## Try it

`samples/repl.cs` is a file-based REPL (`dotnet run samples/repl.cs`): type `simplify sin(x)^2 + cos(x)^2`, `solve x^2 - 4 = 0`, `integrate x*cos(x)`, `limit sin(x)/x at 0`, `find difference of squares`, or `help`.

`samples/AotSmoke` is a console app that parses, simplifies with steps, differentiates, integrates, solves and looks up the catalog; it is published with NativeAOT in the release gate (`dotnet publish samples/AotSmoke -c Release -r <rid>`, which needs the platform's C++ toolchain).

## Build and test

```text
dotnet build Mathesis.slnx -c Release
dotnet test --solution Mathesis.slnx -c Release
dotnet run eng/policy-check.cs            # package policy: System.*, Microsoft.* and CommunityToolkit.* only, nothing deprecated or vulnerable
dotnet run eng/mlaw-lint.cs               # lints the catalog (.mlaw files)
dotnet run eng/gen-knowledge.cs -- --check
dotnet run eng/check-catalog-coverage.cs  # coverage of the design documents' catalog tables
dotnet pack Mathesis.slnx -c Release      # the six packages in artifacts/packages
```

The design documents are in `docs/design` (`00-index.md` gives the reading order) and the catalog is in `knowledge/*/*.mlaw`. See `RELEASE-NOTES.md` for what the first release covers and does not.

## License

MIT. See `LICENSE`.
