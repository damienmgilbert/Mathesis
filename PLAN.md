# Plan 1: Symbolic & numerical math library

Claude Code builds Milestone 1 (Foundation) of Mathesis: six packages, `Mathesis.Core`, `Mathesis.Numerics`, `Mathesis.LinearAlgebra`, `Mathesis.Symbolics`, `Mathesis.Knowledge` and `Mathesis`, on .NET 10 LTS with one external dependency (`System.Numerics.Tensors`), in eleven phases that each end with a green build. The full design, from architecture to per-domain catalogs of laws, theorems and formulas, lives in `docs/design/`; later milestones follow `docs/design/10-roadmap.md`.

## How to run it

1. Save this file as `PLAN.md` in an empty Git repo and copy the `docs/design/` folder beside it.
2. Open Claude Code there and send: `Read PLAN.md and docs/design/00-index.md. Ask me the "Decide first" questions, then do Phase 0 only and stop with a summary.`
3. Then one phase per session: `Do Phase N of PLAN.md. Read the docs its "Read first" line names before writing code. Stop when its exit checks pass and summarize what changed.`

## Decide first

- Root name: `Mathesis` (decided) for namespaces and package IDs. No package with that ID is listed on nuget.org as of 2026-10-01; published NuGet IDs are permanent, so confirm it is still free before the first publish.
- License: MIT unless you say otherwise.
- Defaults: real-valued solving with complex roots listed separately, and `I` as the imaginary unit so `i` stays a free symbol.
- Conventions (decided; do not change; catalog entries `conv.*` in `docs/design/06-knowledge-catalog.md`): decimal literals are exact, bare `log` is base 10, 0⁰ = 1, real odd roots in real mode (`(-8)^(1/3) = -2`), and display rounding half away from zero (`conv.rounding`, confirmed before Phase 4).

## Scope

v1 delivers:

- Exact arithmetic with a `BigRational` that implements `INumber<BigRational>`, plus `Complex<T>` and `Dual<T>` (forward-mode automatic differentiation).
- Numerics generic over `T : IFloatingPointIeee754<T>`: root finding, quadrature, ODEs, interpolation, finite differences, dense linear algebra, polynomials, golden-section and Nelder–Mead optimization.
- Exact linear algebra: RREF with recorded row operations, fraction-free determinants, inverses, rank, null and column spaces, characteristic polynomials, working for `BigRational` and `Expr` entries.
- Symbolics: the full expression tree (all node kinds in `05-syntax-trees-and-notation.md`), the operator registry, sorts, three canonical levels, a parser (linear text and a LaTeX subset), text and LaTeX printers, JSON, assumptions, natural domains, zero testing and `Compile<T>`.
- A knowledge catalog: the `.mlaw` format, loader, generated typed handles, a numeric verification harness and a coverage tool, seeded with about 500 entries from Algebra, Trigonometry, Calculus and Linear Algebra.
- A rule-based simplifier driven by the catalog that records every step, with explanations at a chosen curriculum level; differentiation, limits, Taylor series, heuristic integration, equation and linear-system solving.

v1 leaves out Risch integration, complete rational-function integration and factoring over ℚ beyond rational roots (Milestone 2), the logic kernel (Milestone 3), multivariable calculus and ODE solving by type (Milestone 4), Gröbner bases (Milestone 5), finite mathematics (Milestone 6), arbitrary-precision floats (Milestone 7), and any plotting or rendering.

## Design docs

`docs/design/00-index.md` lists them. Phases cite the sections they implement; when code and docs disagree, stop and ask rather than silently diverging, then update whichever is wrong.

## Layout

```text
PLAN.md  CLAUDE.md  README.md  LICENSE
global.json  nuget.config  Directory.Build.props  Directory.Packages.props
docs/design/                     # architecture, types, notation, catalog, engines, domains
eng/policy-check.cs              # file-based app: dotnet run eng/policy-check.cs
eng/gen-knowledge.cs             # .mlaw files → src/Mathesis.Knowledge/Catalog.g.cs
eng/check-catalog-coverage.cs    # catalog IDs vs. docs/design/domains tables
eng/mlaw-lint.cs                 # .mlaw syntax and condition checks
.github/workflows/ci.yml         # build, test and policy check on ubuntu and windows
.claude/skills/                  # project skills for Claude Code (catalog, C#, planning)
knowledge/<domain>/<topic>.mlaw  # catalog source, embedded into Mathesis.Knowledge
src/Mathesis.Core/               # numbers, polynomials, Outcome, Truth, Budget
src/Mathesis.Numerics/           # references Core
src/Mathesis.LinearAlgebra/      # references Core
src/Mathesis.Symbolics/          # references Core, Numerics, LinearAlgebra
src/Mathesis.Knowledge/          # references Symbolics
src/Mathesis/                    # engines and façade; references all of the above
tests/Mathesis.*.Tests/          # MSTest, one per src project
tests/Mathesis.Testing/          # seeded generators shared by test projects
tests/corpus/                    # expressions, derivatives, integrals, equations; one case per line
samples/repl.cs                  # file-based REPL for manual poking
samples/AotSmoke/                # NativeAOT smoke console
```

## Phases

### Phase 0: Bootstrap

- **Read first:** `00-index.md`, `02-architecture.md`, `03-namespaces-and-packages.md`.
- `global.json` pinned to the installed 10.0 SDK with `rollForward: latestFeature`, a `.slnx` solution, and a `nuget.config` that clears sources and uses nuget.org only.
- `Directory.Build.props`: `net10.0`, latest C#, nullable and implicit usings on, `TreatWarningsAsErrors`, `AnalysisLevel` latest-recommended, deterministic builds, `IsAotCompatible` for `src/` projects.
- `Directory.Packages.props` with central package management and `CentralPackageTransitivePinningEnabled`.
- Empty projects for all six packages with the references in the layout above, and one MSTest project per package.
- `eng/policy-check.cs` per the spec below, plus a GitHub Actions workflow running build, test and the policy check on `ubuntu-latest` and `windows-latest`.
- `CLAUDE.md` from the block at the end of this plan.
- **Done when:** a Release build has zero warnings, a placeholder test passes, and the policy check passes, fails after temporarily adding `Newtonsoft.Json`, and passes again once it is removed.

### Phase 1: Exact numbers

- **Read first:** `04-type-system.md` (Number tower, Generic-math constraints).
- `BigRational` over `BigInteger`, always normalized (gcd-reduced, positive denominator), implementing `INumber<BigRational>`, `ISignedNumber<BigRational>` and `IExactNumber`.
- Parsing and formatting (`3/4`, `-2`, `0.125` becomes `1/8`, repeating decimals `0.1(6)`) and conversion to and from `double` and `decimal`; best rational approximation of a `double`.
- `Complex<T>` for `T : INumber<T>` (exact Gaussian rationals when `T` is `BigRational`), `Dual<T>`, and `NumberTraits<T>.IsExact`.
- `Outcome<T>`, `Truth`, `Budget`, `Provisos` and `MathError` in the `Mathesis` namespace of `Mathesis.Core`.
- **Done when:** 10,000 seeded random cases hold the field axioms, parse and format round-trip, `double` conversion is within 1 ulp, and `Dual<double>` derivatives of 20 elementary functions match their analytic derivatives to 1e−14 relative.

### Phase 2: Numerical core

- **Read first:** `domains/d9-numerical-analysis.md` (Root finding, Numerical differentiation, Numerical integration, Ordinary differential equations), `09-verification.md`.
- Roots: bisection, Brent, Newton (analytic or finite-difference derivative), secant.
- Quadrature: adaptive Simpson, adaptive Gauss–Kronrod 7/15, Romberg, and infinite intervals by substitution.
- ODEs: fixed-step RK4 and adaptive Dormand–Prince 5(4) with dense output.
- Interpolation (Newton, barycentric, natural and clamped cubic splines) and finite differences with Richardson extrapolation.
- Every iterative or adaptive routine returns a result record (value, error estimate, iterations or evaluations, converged flag) instead of throwing on non-convergence. Closed-form finite-difference stencils return a plain value and interpolant factories return interpolant objects. ODE event location and stiffness detection are not part of this phase (Milestone 7, see `domains/d9-numerical-analysis.md`).
- **Done when:** a tolerance table passes: ∫₀^π sin x dx = 2, ∫₀^∞ e^(−x²) dx = √π/2, y′ = −y matches e^(−t) to 1e−10, and Brent solves the standard test functions within 50 iterations.

### Phase 3: Linear algebra, polynomials and optimization

- **Read first:** `domains/d7-linear-algebra.md` (Linear systems, Matrix algebra, Determinants), `domains/d1-algebra.md` (Polynomial division and roots), `04-type-system.md` (Representations), `domains/d9-numerical-analysis.md` (Nonlinear systems and optimization: golden-section search, Nelder–Mead).
- `DenseMatrix<T>` and `DenseVector<T>` (named to avoid `System.Numerics.Vector<T>`), row-major `T[]` storage, span-based kernels, `TensorPrimitives` for `float` and `double` hot loops.
- Numeric: LU with partial pivoting, Householder QR, Cholesky, solve, determinant, inverse, least squares, symmetric eigenvalues (Jacobi), condition-number estimate.
- Exact (`Mathesis.LinearAlgebra.Exact`): RREF returning the list of row operations, Bareiss determinant, Gauss–Jordan inverse, rank, null and column space bases, characteristic polynomial (Berkowitz), all with a pluggable zero test so they also work for `Expr` entries later.
- `Polynomial<T>` and `SparsePolynomial<T>` in `Mathesis.Core` (`Mathesis.Polynomials`): arithmetic, division with remainder, GCD, Yun square-free factorization, Horner evaluation, derivative, all roots by Aberth–Ehrlich, rational roots of integer polynomials.
- `Mathesis.Numerics.Optimization`: golden-section search (one dimension) and Nelder–Mead (n dimensions), generic over `IFloatingPointIeee754<T>`, returning an `OptimizationResult<T>` (minimizer, minimum, iterations, evaluations, `Converged`).
- **Done when:** residual ‖Ax − b‖ ≤ 1e−12·‖b‖ on seeded well-conditioned systems, the Hilbert(8) inverse is exact over `BigRational`, the roots of (x−1)(x−2)…(x−10) come back within 1e−8, replaying recorded row operations reproduces each RREF, and `Expand(SquareFree(p)) = p` on 1,000 seeded polynomials, golden-section finds the minimizer of (x − 2)² + 1 and of cos x on [3, 4] to 1e−7 (a minimizer cannot be located better than about √ε) and the minimum value to 1e−14, and Nelder–Mead minimizes the Rosenbrock function from (−1.2, 1) to within 1e−6 using at most 2,000 evaluations.

### Phase 4: Expression model, parser, printers

- **Read first:** `05-syntax-trees-and-notation.md` (all of it), `04-type-system.md` (The syntax tree, Sorts).
- `Expr` with all twelve node kinds, cached structural hash, free symbols and sort; `ExprPath`.
- The `Operator` record, attributes and the built-in operator catalog; constants; binders.
- Sorts with declaration, inference and `SortChecker`.
- The three canonical levels (Raw, Structural, Canonical) with the Canonical invariants as property tests.
- Pratt parser with the precedence table and every convention listed (implicit multiplication, `sin^2 x`, `sin^-1 x`, exact decimals, bare `log`, primes and Leibniz notation, Unicode), errors with spans and suggestions; LaTeX-subset parser.
- Printers: plain text with minimal parentheses, and LaTeX; Mathesis JSON via the System.Text.Json source generator.
- C# builder API (`Sym`) with operator overloads as specified (`^` not overloaded).
- **Done when:** parse, print, parse is the identity on `tests/corpus/expressions.txt` (at least 300 cases spanning every operator family), LaTeX snapshot tests pass, parser errors point to the right column, and the parser fuzz test completes without exceptions.

### Phase 5: Assumptions, domains and evaluation

- **Read first:** `07-engines.md` (Assumptions and domains, Zero testing, Evaluation and compilation), `04-type-system.md` (Truth and assumptions, Sets and solution sets).
- `AssumptionSet.Ask` with sign propagation, interval reasoning over `Interval<double>`, small Fourier–Motzkin systems and integer parity; `MathContext`.
- Natural domain computation returning set expressions; `Interval<T>` with outward rounding.
- `ZeroTest`: exact for rational functions over ℚ and ℚ(i), interval exclusion, seeded high-precision random evaluation otherwise.
- `Evaluate` (exact), `N` (double), and `Compile<T>` (flat instruction array) for `double`, `Complex<double>`, `Interval<double>` and `Dual<double>`.
- **Done when:** `Ask` answers a 200-case truth table with no wrong `True`/`False` (only `Unknown` allowed as a miss), domains match hand-computed answers on 100 expressions, `Compile<double>` agrees with `Evaluate` to 1e−14 relative, and interval evaluation encloses 10,000 random point evaluations.

### Phase 6: Knowledge catalog

- **Read first:** `06-knowledge-catalog.md` (all of it), `domains/d1-algebra.md`, `domains/d2-trigonometry.md`, `domains/d5-calculus.md` (Limits through Integration techniques), `domains/d7-linear-algebra.md` (Linear systems, Determinants).
- `Mathesis.Knowledge`: entry records, the `.mlaw` lexer and parser (statements parsed with the Phase 4 parser), validation, embedded-resource loading, `KnowledgeBase` lookup by ID, domain, tag and text.
- `eng/gen-knowledge.cs` writing typed handles; `eng/check-catalog-coverage.cs`; `eng/mlaw-lint.cs`.
- The verification harness (numeric checks under conditions, condition probing, parse round trip, link checks).
- Seed entries: every table in D1 sections Axioms through Equations, plus Conventions; D2 Definitions through Inverse functions (Exact values included); D5 Limits, Derivatives: definition and rules, Derivative table, Antiderivatives and Integration techniques; D7 Linear systems and Determinants.
- **Done when:** at least 450 entries load; every `law` and `formula` passes numeric verification; the coverage report shows ≥ 95% for the seeded sections; typed handles are generated and checked in.

### Phase 7: Rewrite engine and simplifier with steps

- **Read first:** `07-engines.md` (Pattern matching through Simplification, Explanation rendering), `06-knowledge-catalog.md` (From entries to rules), `04-type-system.md` (Derivations and steps).
- Matcher with non-linear patterns, sort constraints, optional wildcards with defaults, sequence wildcards and budgeted associative-commutative matching.
- Rules derived from catalog entries only (orientation, guards, provisos in strict and generic mode); rule sets by tag; head-operator and discrimination-tree indexing.
- Strategy combinators; `Simplify` as a budgeted best-first search over transforms with `IComplexityMeasure`; transforms Expand, Factor (patterns and rational roots), Collect, Together, Apart (rational roots), Cancel, Rationalize, CompleteSquare, LogCombine, LogExpand, PowerSimplify, TrigSimplify, TrigExpand, TrigReduce.
- Each application yields a `Step` citing its catalog entry, with path, bindings and provisos; `Derivation` with `Replay`; explanation rendering (text, Markdown, LaTeX) with verbosity levels and the curriculum-level filter.
- **Done when:** replaying the recorded steps reproduces each result exactly, original and simplified forms agree at 20 seeded points inside their domain and provisos for every corpus case (at least 200), every step cites an existing catalog entry, a bounded-iteration test proves no rule set loops, and explanation snapshots are approved.

### Phase 8: Calculus

- **Read first:** `domains/d5-calculus.md` (Limits through Definite integrals, Sequences and series for Taylor series), `07-engines.md` (Calculus engines).
- Differentiation with steps from the derivative-table entries: sum, constant, power, product, quotient and chain rules with nested sub-derivations; implicit and logarithmic differentiation; higher derivatives.
- Limits: direct substitution, algebraic manipulation, special limits, L'Hôpital's rule gated by curriculum level, series expansion.
- Taylor and Maclaurin series to order n via `PowerSeries`.
- Integration pipeline steps 1–8 from `07-engines.md` (linearity, table, polynomials, rational functions with rational-root denominators, substitution, parts, trigonometric integrals and substitutions, Weierstrass).
- Every antiderivative is checked by differentiating it and zero-testing the difference; anything that fails comes back as `Unevaluated`. Definite integrals check the antiderivative's continuity on [a, b] before applying the FTC.
- **Done when:** derivatives match central finite differences at random points, 100% of returned integrals pass the differentiate-back check, definite integrals match Phase 2 quadrature to 1e−9, and limits match sequence evaluation on the corpus.

### Phase 9: Solving

- **Read first:** `07-engines.md` (Solving engine), `domains/d1-algebra.md` (Equations, Inequalities, Systems), `domains/d2-trigonometry.md` (Trigonometric equations), `04-type-system.md` (Sets and solution sets).
- `SolutionSet` kinds (finite, interval unions, image sets, condition sets, parametric).
- Linear and quadratic equations exactly, with radicals, and a caller-chosen method (factoring, quadratic formula, completing the square).
- Polynomial equations: rational-root search and square-free factoring first, then numeric roots for what remains.
- Rational and radical equations with extraneous-solution steps; isolation for transcendental equations where the unknown appears once, with principal branches and general solutions for trigonometric equations.
- Linear systems by exact Gaussian elimination over `BigRational`, reporting unique, none, or infinitely many (parametric) solutions.
- Solving emits the same `Step` records as the simplifier, citing `alg.eq.*` entries for every equivalence or non-equivalence step.
- **Done when:** substituting each solution back gives exactly 0 (exact) or at most 1e−10 (numeric) across a corpus of at least 150 equations with expected solution sets, every extraneous candidate is rejected with a step, and random points outside each solution set fail the equation.

### Phase 10: Packaging and docs

- **Read first:** `08-features-and-abilities.md` (Milestone 1 rows), `09-verification.md` (Tooling and gates).
- XML docs on every public API (missing docs fail the build), a README with fifteen runnable examples (including step-by-step output and a catalog lookup), package metadata, and `dotnet pack` for all six packages.
- `Cas` façade covering every Milestone 1 ability in `08-features-and-abilities.md`.
- A NativeAOT smoke console that runs parse, simplify with steps, differentiate, integrate, solve and a catalog lookup, and `samples/repl.cs`, a file-based REPL for manual poking.
- **Done when:** all six `.nupkg` files build, the AOT smoke app publishes with zero trim or AOT warnings, the policy check is green, and the catalog coverage report is attached to the release notes.

## Policy check spec

`eng/policy-check.cs` is a .NET 10 file-based app.

1. Restore every project under `src/`, read its `obj/project.assets.json`, and collect every package, transitive ones included.
2. Fail on any ID that does not start with `System.`, `Microsoft.` or `CommunityToolkit.`, except `runtime.*` shims whose ID contains `.System.` or `.Microsoft.`.
3. Run `dotnet package list` (the .NET 10 spelling of `dotnet list package`) per project with `--include-transitive --deprecated --format json`, then again with `--vulnerable`, and fail on any hit.
4. Print every package with its version and exit with code 1 on any failure.

## CLAUDE.md

```markdown
# Repo rules
- .NET 10 LTS only. Never add net9.0 or net11.0 targets.
- Latest C#, nullable on, warnings are errors.
- Central package management. Shipped projects may only use package IDs starting with System., Microsoft. or CommunityToolkit., including transitive packages, and never deprecated or vulnerable ones. Run `dotnet run eng/policy-check.cs` after any package change.
- Before coding a phase, read the docs/design files its "Read first" line names. If code must differ from the design, stop and ask.
- Mathematics lives in the catalog: no rewrite rule, derivative rule or antiderivative may exist in code without a .mlaw entry. Every law states exact conditions; never weaken a condition or a tolerance to make verification pass.
- Engines return Outcome values; exceptions are for API misuse only. Every symbolic operation honors the Budget.
- Implement algorithms from papers and textbooks. Never copy code from Numerical Recipes or from GPL computer algebra systems.
- Prefer inlining small helpers into the calling method over extracting new tiny methods.
- When editing an existing file, keep its formatting and change only what the task needs.
- Tests are MSTest. New public API gets tests; a bug fix starts with a failing test.
- A phase is done only when build, tests, catalog verification and the policy check are green. Then stop and summarize.
```
