# 10 Roadmap

The full scope is nine milestones. Milestone 1 is `PLAN.md`; each later milestone gets its own plan file, drafted by Claude Code from this roadmap and the domain docs once the previous milestone ships.

## Milestones

| # | Name | Packages touched | Domains completed | Catalog size (cumulative) | Depends on |
| --- | --- | --- | --- | --- | --- |
| 1 | Foundation | Core, Numerics, LinearAlgebra, Symbolics, Knowledge, Mathesis | Core of Algebra, Trigonometry identities, single-variable differentiation and series, numeric core | ≈ 500 | — |
| 2 | Algebra and Pre-Calculus | Core, Symbolics, Knowledge, Mathesis | Algebra, Pre-Calculus, Trigonometry (equations, exact values, triangles) | ≈ 1,100 | 1 |
| 3 | Proofs and Logic | Logic, Knowledge, Mathesis | Proofs & Logic | ≈ 1,300 | 1 (2 for identity proofs at full strength) |
| 4 | Calculus and Differential Equations | Symbolics, Knowledge, Mathesis | Calculus (single and multivariable, vector calculus, series), Differential Equations | ≈ 2,000 | 2 |
| 5 | Linear Algebra and advanced algebra | LinearAlgebra, Core, Mathesis | Linear Algebra; Gröbner bases, multivariate and extension-field factoring | ≈ 2,300 | 2 |
| 6 | Finite Mathematics and discrete | Discrete, Knowledge, Mathesis | Finite Mathematics, number theory, graph theory | ≈ 2,700 | 1 (5 for Markov and LP steps) |
| 7 | Numerical Analysis depth | Numerics, LinearAlgebra, Core | Numerical Analysis; `BigFloat`, validated interval methods, special functions | ≈ 3,000 | 1 |
| 8 | Ecosystem | Extensions, Symbolics | DI, AI tools, MathML/MathJSON interop, localization | — | 1 |
| 9 | Input validation | Validation (new) | Validation attributes for forms (no catalog change) | — | 1 |

Milestones 3, 6 and 7 can run in parallel with 2, 4 and 5 once Milestone 1 is done. Milestone 9 depends only on Milestone 1 and can run in parallel with 2–8.

## Milestone 1: Foundation

Delivered by `PLAN.md`.

- `BigRational`, `Complex<T>`, `Dual<T>`; numerical core (roots, quadrature, ODEs, interpolation, finite differences); dense linear algebra and polynomials.
- `Expr` with all node kinds, the operator registry with the built-in catalog, sorts, three canonical levels, parser (linear text and LaTeX subset), printers (text, LaTeX), JSON.
- Assumptions, natural domains, zero testing (exact for rational functions, numeric otherwise), `Compile<T>`.
- Knowledge catalog: `.mlaw` format, loader, generator, verification harness, coverage tool; seeded with Algebra and Trigonometry identity entries.
- Rewrite engine, strategies, `Simplify` with derivations and explanations.
- Calculus core: differentiation, Taylor series, heuristic integration with self-checks.
- Solving core: linear, quadratic, polynomial (rational roots + numeric), isolation, exact linear systems.
- **Exit:** Plan 1 phase checks pass; ≈ 500 verified entries; AOT smoke app clean.

## Milestone 2: Algebra and Pre-Calculus

- Factoring over ℚ (Berlekamp–Zassenhaus with Hensel lifting), resultants, root isolation, algebraic numbers and radical denesting.
- Complete rational-function integration (Hermite + Lazard–Rioboo–Trager).
- Inequalities and sign charts; rational, radical, absolute-value, exponential and logarithmic equations with extraneous-solution steps; polynomial systems via resultants.
- Function analysis: domain, range, intercepts, symmetry, asymptotes, monotonicity, extrema, concavity, transformations, inverses, composition, piecewise functions.
- Conics, parametric and polar curves, complex numbers in polar form, sequences and series (arithmetic, geometric, sigma notation, Faulhaber, Gosper).
- Trigonometry: exact values at rational multiples of π (denominators 1–6, 8, 10, 12), trigonometric equations with general solutions, triangle solver including the ambiguous case.
- Adaptive sampling for plotting.
- **Exit:** Algebra, Pre-Calculus and Trigonometry coverage ≥ 95%; 200 corpus cases per domain; method choice in `Solve`.

## Milestone 3: Proofs and Logic

- `Mathesis.Logic`: propositional and first-order syntax, truth tables, NNF/CNF/DNF/prenex, CDCL SAT solver.
- LCF-style kernel, natural deduction, equality and congruence, ring normalization tactic, linear-arithmetic tactic, induction over ℕ (weak, strong), structural induction on lists and trees.
- Equational proofs from derivations; `ProveIdentity` for algebra and trigonometry; two-column, Fitch and paragraph rendering.
- Sets, relations, functions, cardinality; proof-technique catalog entries.
- Counterexample search.
- **Exit:** every Algebra and Trigonometry law with `verify: numeric` can also be proved by the kernel where it is a ring or field identity.

## Milestone 4: Calculus and Differential Equations

- Limits complete for exp-log functions (Gruntz); L'Hôpital with level gating; improper integrals; convergence tests; Taylor's theorem with remainder bounds.
- Integration: Risch–Norman heuristic and parts of the Risch algorithm; definite integrals with discontinuity handling.
- Multivariable: partial derivatives, gradient, Hessian, second-derivative test, Lagrange multipliers, multiple integrals with coordinate changes, line and surface integrals, Green's, Stokes' and divergence theorems.
- ODEs: classification; separable, linear, exact, Bernoulli, homogeneous; second-order constant coefficients, Cauchy–Euler, undetermined coefficients, variation of parameters; Laplace transforms; linear systems via eigenvalues and matrix exponentials; phase-plane classification; power-series and Frobenius solutions; Fourier series; separation-of-variables templates for heat, wave and Laplace equations.
- **Exit:** Calculus and Differential Equations coverage ≥ 95%; every ODE solution self-verified.

## Milestone 5: Linear Algebra and advanced algebra

- Explained Gram–Schmidt, QR, LU, diagonalization, Jordan form, SVD, pseudoinverse, least squares, change of basis, quadratic forms.
- Sparse matrices and iterative solvers; matrix functions.
- Abstract vector spaces (polynomial and function spaces); linear transformations.
- Gröbner bases, multivariate factoring (Wang's EEZ), factoring over algebraic extensions (Trager).
- **Exit:** Linear Algebra coverage ≥ 95%; symbolic and numeric paths agree on the corpus.

## Milestone 6: Finite Mathematics and discrete

- `Mathesis.Discrete`: combinatorics, probability and distributions, descriptive statistics and regression, finance, linear programming (graphical, simplex with tableaux, two-phase, duality), Markov chains, game theory, graph algorithms, number theory, recurrences and generating functions.
- `ModInt<T>`, `ModInteger`, finite fields.
- **Exit:** Finite Mathematics coverage ≥ 95%; finance results match published amortization examples to the cent.

## Milestone 7: Numerical Analysis depth

- `BigFloat` with correctly rounded basic operations and elementary functions; `N(expr, digits)` to any precision.
- Validated numerics with `Interval<T>`: interval Newton, verified quadrature bounds.
- Special functions suite checked against reference values.
- Full quadrature family (Gauss families, tanh-sinh, cubature), stiff ODE solvers (BDF, Rosenbrock), eigenvalue algorithms (QR with shifts, Lanczos, Arnoldi), optimization (BFGS, L-BFGS, Levenberg–Marquardt, constrained), FFT, Chebyshev approximation and Remez, PDE finite differences.
- Error-analysis reporting (condition numbers, backward error) in results.
- **Exit:** Numerical Analysis coverage ≥ 95%; reference accuracy targets met.

## Milestone 8: Ecosystem

- `Mathesis.Extensions`: DI registration, options, `ILogger` tracing, `Microsoft.Extensions.AI` tools.
- MathML (presentation and content) and MathJSON round trips.
- Localized explanation templates.
- Integration samples for the step-by-step solver app (idea #14) and graphing calculator (idea #30).

## Milestone 9: Input validation

Delivered by `PLAN-M9.md`.

- `Mathesis.Validation`: seven `System.ComponentModel.DataAnnotations` attributes (`RationalNumber`, `ExactRange`, `NonZero`, `MathExpression`, `MathEquation`, `PolynomialExpression`, `MathMatrix`) that check numbers, expressions, equations, polynomials and matrices, with stable result codes, source spans, suggestions and embedded default messages.
- No UI-framework reference: every UI stack that consumes DataAnnotations gets the attributes for free (ADR-15).
- **Exit:** the Plan M9 phase checks pass; the attributes work through `Validator.TryValidateObject` on form models; the AOT smoke app is clean.

## Downstream work items (Technesis)

The Technesis control and motion library (a separate repository that consumes the published Mathesis packages) needs one Mathesis change; the rest of its numerics incubate in Technesis and move here later only through the milestones above (Schur and eigen, SVD and `expm`: Milestone 5 or 7; FFT, stiff ODEs, QP, special functions: Milestone 7). Decided 2026-10-05: a new number type goes to Mathesis first, an algorithm may incubate downstream (ADR-19).

| ID | Work item | Notes |
| --- | --- | --- |
| MW1 | `Jet<T> : IFloatingPointIeee754<Jet<T>>` in `Mathesis.Core`, aligned with `HyperDual<T>` | Design is in docs 02 (ADR-19) and 04. Implementation waits for the Technesis prototype that fixes the representation; ships as a Mathesis 0.2.0 phase with a `num.diff.forward-ad` extension, property tests (gradients of 20 elementary functions to 1e−14, nested-jet Hessian) and the AOT gate |
| MW2 | `Compile<T>` kernel-table entry for `Jet<double>` | After MW1 |
| MW3 | Jet-friendly overloads in `Roots` and `Minimize` | After MW1 |

## Future domains

Not in the nine requested domains, but the architecture leaves room for them as catalog domains and engines: Euclidean and coordinate geometry (congruence and similarity, circle theorems, constructions), abstract algebra (groups, rings, fields, Galois theory), real and complex analysis, number theory beyond Milestone 6, inferential statistics, topology, differential geometry, and tensor calculus.

## How to start the next milestone

After Milestone N ships, open Claude Code in the repo and send:

```text
Read docs/design/10-roadmap.md, the domain docs for Milestone N+1 and PLAN.md.
Draft PLAN-M<N+1>.md in the same format as PLAN.md: Decide first, Scope, Layout changes,
phases with "Read first" docs and "Done when" checks. Stop after writing it.
```
