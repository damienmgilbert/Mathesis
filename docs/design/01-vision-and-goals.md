# 01 Vision and goals

Mathesis is a dependency-light .NET 10 library that represents mathematics as data: numbers, expressions, laws, theorems and proofs share one syntax tree, and every transformation the library performs can be explained step by step and checked independently.

The name comes from Leibniz's *mathesis universalis*, a universal science of quantity and form. No package with the ID `Mathesis` is listed on nuget.org as of 2026-10-01; confirm it is still free before the first publish.

## Vision

One library where a student, an engineer and an app can all ask the same object model:

- **What is it?** Parse `x^2 - 5x + 6 = 0`, `\int_0^1 x^2\,dx` or a C# builder expression into a typed syntax tree.
- **What follows from it?** Simplify, expand, factor, differentiate, integrate, solve, row-reduce, prove.
- **Why?** Every result carries a derivation: the ordered steps, the law applied at each step, and the conditions under which the result holds.
- **How do we know?** Every law in the catalog is machine-checked, and every result can be cross-checked numerically.
- **What's the number?** Evaluate exactly (rationals, radicals), approximately (`double`), or with guaranteed bounds (intervals), from the same tree.

## Goals

Each goal has a test that decides whether it is met.

| ID | Goal | Met when |
| --- | --- | --- |
| G1 | Breadth: the nine domains in `domains/` (Algebra, Trigonometry, Proofs & Logic, Pre-Calculus, Calculus, Differential Equations, Linear Algebra, Finite Mathematics, Numerical Analysis) are represented in the type system, the syntax tree and the knowledge catalog | `eng/check-catalog-coverage.cs` reports at least 95% of IDs in the domain docs present in the catalog |
| G2 | Laws as data: every rule, law, identity, theorem, formula, definition and named pattern is a catalog entry with a stable ID, a formal statement, its conditions and a plain-language explanation | No rewrite rule exists in code that is not backed by a catalog entry |
| G3 | Soundness: transformations never silently change meaning; domain changes are recorded as provisos (for example x/x → 1 "for x ≠ 0") | Metamorphic tests: input and output agree numerically at random points inside the recorded domain |
| G4 | Explainability: every engine returns a derivation whose steps cite catalog entries and can render as text, LaTeX or a two-column proof | Replaying a derivation's steps reproduces its result exactly |
| G5 | Verifiability: every law with a checkable statement passes randomized numeric verification under its conditions; every derivative, antiderivative, solution and decomposition is self-checked | CI fails on any unverified law or failed self-check |
| G6 | Exactness first: exact arithmetic (integers, rationals, algebraic numbers, exact constants) is the default; floating point is opt-in and labelled | `Sqrt(8)` simplifies to `2·√2`, never `2.828…`, unless numeric evaluation is requested |
| G7 | Dependency policy: shipped packages use only the .NET 10 BCL plus non-deprecated `System.*`, `Microsoft.*` and `CommunityToolkit.*` packages, including transitive ones | `eng/policy-check.cs` passes |
| G8 | AOT and trim safe: no reflection-based dispatch, no runtime code generation | A NativeAOT smoke app publishes with zero trim or AOT warnings |
| G9 | Bounded: every symbolic operation accepts a budget (steps, expression size, time, cancellation) and returns a partial or unevaluated result instead of hanging | Fuzzed inputs complete within their budget |
| G10 | Curriculum-aware: entries carry a level (pre-algebra through university) so explanations can avoid methods above the learner's level | `Explain(..., level: PreCalculus)` never cites L'Hôpital's rule |

## Non-goals

- **A complete decision procedure for "is this zero?"** Richardson's theorem (1968) shows zero-equivalence is undecidable for expressions built from π, ln 2, eˣ, sin x and |x|. Mathesis uses exact normal forms where they exist (polynomials, rational functions, many algebraic and exp-log expressions) and reports `ProbablyZero` with a stated confidence elsewhere.
- **A full proof assistant.** The logic kernel checks propositional, first-order and equational proofs, ring identities and induction over ℕ. It does not replace Lean or Coq, and it does not search for proofs of arbitrary first-order statements, which is only semi-decidable.
- **Complete symbolic integration.** Rational functions get a complete algorithm. Elementary functions get table lookup, heuristics and parts of the Risch algorithm; anything else returns `Unevaluated`.
- **Rendering, plotting or UI.** Mathesis produces data (trees, LaTeX, MathML, sample points, phase-portrait vectors). Apps such as the solver app (idea #14) and graphing calculator (idea #30) draw it. The validation attributes of `Mathesis.Validation` carry rules and messages as data; drawing an error state stays in the app.
- **Physical units.** Dimensional analysis is a separate library (idea #15).

## Design principles

1. **One tree, many views.** The syntax tree is the single source of truth. Polynomials, rational functions, power series and matrices are faster *representations* that convert to and from it losslessly.
2. **Knowledge is data, engines are generic.** The rewrite engine knows nothing about trigonometry; trigonometry lives in catalog entries. Adding mathematics means adding entries and tests, not engine code.
3. **Every step is a proof step.** A derivation from the simplifier is also an equational proof that the logic kernel can check.
4. **Conditions travel with results.** Results carry provisos and assumptions; nothing is true "in general" unless it is.
5. **Exact, then approximate, then bounded.** Prefer exact answers; fall back to floating point with an error estimate; offer interval enclosures when guarantees matter.
6. **Fail soft and visibly.** Math failures return `Outcome` values (`Success`, `Partial`, `Unevaluated`, `Failed`) with reasons. Exceptions are for programming errors only.
7. **Immutable and thread-safe.** Expressions, catalogs and contexts are immutable; concurrent use needs no locks.
8. **Small trusted core.** Only the proof kernel and the exact number types must be trusted; everything else is checked by them or by numeric cross-validation.
9. **Implement from mathematics, not from other people's code.** Algorithms come from papers and textbooks. Never transcribe code from Numerical Recipes (restrictive license) or from GPL computer algebra systems such as Maxima; BSD/MIT sources such as SymPy may be studied but not copied without attribution.

## Users and use cases

| User | Typical call | Needs |
| --- | --- | --- |
| Student, via a solver app | `Solve("2x + 3 = 7", "x", explain: true)` | Steps at their level, method choice (factor vs. formula), clear provisos |
| Teacher or textbook author | `ProveIdentity("sin(2x) = 2 sin(x) cos(x)")` | Two-column proofs, LaTeX output, exercise generation |
| Engineer | `Ode.Solve(rhs, y0, span)`, `Lu.Decompose(A)` | Fast, accurate numerics with error estimates |
| App developer | `Expr.Parse`, `Compile<double>()`, `ToLatex()` | Stable API, AOT-friendly, JSON and MathJSON interchange |
| AI assistant (idea #21) | `AIFunction` tools over `Simplify`, `Solve`, `Integrate` | Deterministic, bounded, explainable tool results |
| Library author | Generic algorithms over `INumberBase<T>` | Reusable number types: `BigRational`, `Complex<T>`, `Dual<T>`, `Interval<T>`, `ModInt<TM>` |

## Quality bars

- **Correctness:** zero known wrong answers in the corpus; any wrong answer is a release blocker.
- **Coverage:** the catalog grows by milestone (see `10-roadmap.md`): about 500 entries in Milestone 1, 1,100 by Milestone 2, 3,000 or more at completion.
- **Performance targets** (Release, x64, single thread):
  - parse and print a 1,000-node expression in under 1 ms;
  - expand (x + y + z)^20 (231 terms) in under 50 ms;
  - exact 50×50 rational determinant (Bareiss) in under 200 ms;
  - 1,000×1,000 `double` LU in under 1 s.
- **Documentation:** XML docs on every public API, with every catalog entry browsable by ID.
