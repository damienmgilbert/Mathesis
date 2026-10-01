# 09 Verification

Mathesis is only useful if its answers are right and its conditions are honest. Verification therefore has seven layers, from unit tests to a self-check attached to every result, and a wrong answer anywhere in the corpus blocks a release.

## Layers

| Layer | What it catches | Where | Runs |
| --- | --- | --- | --- |
| 1. Unit tests | Broken APIs and edge cases | `tests/*` (MSTest) | Every build |
| 2. Property tests | Violations of algebraic laws in number types and representations | Seeded generators in `tests/Mathesis.Testing` | Every build |
| 3. Catalog verification | Wrong laws, missing or wrong conditions | `Mathesis.Knowledge.Tests` (see `06-knowledge-catalog.md`) | Every build |
| 4. Metamorphic and round-trip tests | Engines that change meaning | Engine test projects | Every build |
| 5. Golden corpora | Regressions in answers and step-by-step output | `tests/corpus/` | Every build |
| 6. Reference comparisons | Numeric inaccuracy | Tables of high-precision reference values | Every build |
| 7. Runtime self-checks | Wrong answers in production | `Outcome.Check` on every result | Every call (cheap checks), optional (expensive) |

## Property tests

The test projects include a small seeded generator library (no third-party dependency needed, though test projects are exempt from the package policy):

- `BigRational`: field axioms, order axioms, parse/format round trip, `double` conversion within 1 ulp.
- `Complex<T>`, `Dual<T>`, `ModInt<T>`: ring/field axioms; `Dual` derivatives match analytic derivatives.
- `Interval<T>`: inclusion property — for random x in X and y in Y, x ∘ y ∈ X ∘ Y for every operation and function.
- Polynomials: (p·q)/q = p, gcd divides both, `Expand(Factor(p)) = p`, Euclid's extended identity s·p + t·q = gcd.
- `Expr`: parse ∘ print = identity on Canonical trees; Canonical invariants hold (see `05-syntax-trees-and-notation.md`); normalization is idempotent.

## Metamorphic tests

| Engine | Relation checked |
| --- | --- |
| Simplify, Expand, Factor, Together, Apart, TrigSimplify | Output equals input numerically at 20 random points inside the recorded domain and provisos |
| Differentiate | Matches central finite differences (Richardson-extrapolated) at random points |
| Integrate (indefinite) | `Differentiate(F) − f` zero-tests to `Zero` |
| Integrate (definite) | Matches adaptive Gauss–Kronrod to 1e−9 relative |
| Limit | Agrees with evaluating along a sequence approaching the point |
| Series | Truncated series matches the function to the expected order at small offsets |
| Solve | Each solution substituted back gives exactly 0 (exact) or ≤ 1e−10 (numeric); random points outside the set are not solutions |
| Inequalities | Random points inside the solution set satisfy the inequality; points outside do not |
| ODE solutions | Substituting the solution satisfies the equation; initial conditions hold; numeric integrator agrees |
| Linear algebra | A·A⁻¹ = I, P·A = L·U, Q·R = A with QᵀQ = I, A·v = λ·v, det(AB) = det(A)·det(B), rank + nullity = n |
| Derivations | `Derivation.Replay` reproduces the result; each step is an instance of its cited entry |
| Proofs | Kernel re-checks every proof produced by tactics |

## Golden corpora

`tests/corpus/` holds problems written for this project (never copied from copyrighted textbooks), one case per line or block:

```text
# simplify.corpus
input:    (x^2 - 1)/(x - 1)
expect:   x + 1
provisos: x != 1
steps:    snapshot simplify/0001.steps.md
```

- At least 200 cases per domain by its completion milestone.
- Step snapshots are reviewed by a human when they change.
- Each case records its curriculum level, so explanation filtering is tested too.

## Reference values

- Special functions: reference values computed with high precision and stored with their source (for example DLMF tables and identities); tests require relative error ≤ 4 ulp in `double` where the function is well conditioned.
- Quadrature, ODE and root-finding test problems with known answers (standard test sets such as the "hard" integrands with endpoint singularities, stiff Robertson and Van der Pol problems).
- Wilkinson's polynomial and Hilbert matrices to confirm ill-conditioning is reported, not hidden.

## Runtime self-checks

Every `Outcome` carries a `Verification` value: `Verified` (exact check passed), `NumericallyConsistent` (random-point agreement), `NotChecked` or `Failed`. Defaults:

| Result | Check | Cost |
| --- | --- | --- |
| Antiderivative | Differentiate and zero-test | Always |
| Solutions | Substitute back | Always |
| Simplification | 5-point numeric agreement | Always in explain mode, optional otherwise |
| Linear algebra decompositions | Residual norm | Always |
| Numeric routines | Error estimate and convergence flag in the result record | Always |

A `Failed` self-check is returned to the caller, never hidden, and logged as a bug.

## Tooling and gates

| Gate | Tool | Fails the build when |
| --- | --- | --- |
| Package policy | `eng/policy-check.cs` | Any non-allowed, deprecated or vulnerable package, including transitive ones |
| Catalog verification | `Mathesis.Knowledge.Tests` | Any law or formula fails numeric verification |
| Catalog coverage | `eng/check-catalog-coverage.cs` | Coverage for a completed milestone's domains drops below 95% |
| Termination | Rule-set cycle test | A rule set exceeds its step cap on the corpus |
| AOT | `samples/AotSmoke` publish | Any trim or AOT warning |
| Docs | Build with `GenerateDocumentationFile` | A public API lacks XML docs |
| Performance | Optional benchmark project | Regression beyond 20% on tracked benchmarks (advisory, not blocking) |

## Fuzzing

- Parser: random token streams and mutated corpus inputs must produce a tree or a `ParseError`, never an exception or hang.
- Engines: random expression generator by sort; every call must return within its budget.
