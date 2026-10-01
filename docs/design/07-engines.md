# 07 Engines

Engines are the generic machinery that turns catalog knowledge into answers: normalize, match, rewrite, search for the simplest form, reason about assumptions, and record a derivation. Domain-specific engines (factoring, integration, solving, ODEs) are compositions of these pieces plus specialized algorithms on fast representations.

## Pipeline

```text
Expr ─► Normalizer ─► Engine (strategy over rule sets + algorithms) ─► Verifier ─► Outcome<T>
            ▲                 │        │                │
            │                 │        │                └─ numeric cross-check, replay, zero test
            │                 │        └─ AssumptionSet.Ask for every guard
            │                 └─ RuleIndex lookup → Matcher → Rule.Apply → Step
            └─ every intermediate result is re-normalized at the engine's working level
```

Every engine call is deterministic: the same input and context give the same result and derivation. Randomized checks are seeded from the expression's structural hash.

## Normalizer

Implements the Raw → Structural → Canonical levels in `05-syntax-trees-and-notation.md`. Canonicalization is bottom-up, memoized per call by structural hash, and costs time linear in tree size except for sorting operands. In explain mode the normalizer is itself instrumented: each folding it would do silently ("2 + 3 = 5", "x·x = x²") is emitted as a step citing an arithmetic catalog entry.

## Pattern matching

| Capability | Example | Notes |
| --- | --- | --- |
| Syntactic matching | `sin(a_)` matches `sin(2x)` with a = 2x | Linear time in pattern size |
| Non-linear patterns | `a_ - a_` requires both occurrences equal | Structural equality after normalization |
| Sort-constrained wildcards | `n_ ∈ ℤ`, `c_` free of x | Constraints checked at bind time |
| Optional wildcards with defaults | `a_.·x^n_.` matches `x` with a = 1, n = 1 | Needed so one rule covers `x`, `3x`, `x^4`, `3x^4` |
| Associative-commutative (AC) matching | `a_^2 + 2a_·b_ + b_^2` matches `9 + y^2 + 6y` | Many-to-one; bounded search with early pruning by numeric parts and operator counts |
| Sequence wildcards | `add(x^2, rest__)` | Binds the remaining operands of an n-ary operator |
| Matching under binders | `∫ f(x)·g'(x) dx` patterns respect bound variables | α-renaming before matching |

AC matching is NP-hard in general. The matcher caps attempted assignments per match (budget) and orders operands so cheap discriminating parts (numbers, distinct operators) bind first.

## Rules, rule sets and indexing

- A `Rule` is (pattern, replacement, guard, provisos policy, catalog entry, explanation template).
- Rules derive from catalog entries (see `06-knowledge-catalog.md`); hand-written rules are not allowed (goal G2).
- `RuleSet`s are named by catalog `tags`: `combine-powers`, `expand-powers`, `expand-log`, `combine-log`, `trig-expand`, `trig-reduce`, `trig-pythagorean`, `rationalize`, `factor-patterns` …
- `RuleIndex` keys rules by head operator, then a discrimination tree over the pattern's left-hand side. With thousands of rules, a typical node consults a handful.

## Rewrite strategies

Strategies are composable values in the style of Stratego/ELAN:

| Combinator | Meaning |
| --- | --- |
| `Apply(ruleSet)` | Try the rules at the current node once |
| `Seq(s1, s2)` / `Choice(s1, s2)` / `Try(s)` | Sequence, first success, never fail |
| `TopDown(s)` / `BottomUp(s)` | Traverse the tree applying s |
| `Innermost(s)` / `Outermost(s)` | Normalize to a fixed point from the leaves / root |
| `Repeat(s, max)` | Apply until no change or `max` reached |
| `Fixpoint(s)` | Repeat under the context's budget with cycle detection (seen-set of hashes) |
| `Where(predicate, s)` | Apply only where a predicate holds (contains trig, degree ≥ 2 …) |
| `Algorithm(f)` | Call a specialized algorithm (polynomial factoring, partial fractions) that returns its own sub-derivation |

Termination: each rule set used inside `Fixpoint` must be oriented to decrease a well-founded measure (term size, degree, number of trig functions); a test runs every rule set on the corpus with a step cap and fails on cycles.

## Simplification

`Simplify` searches for the form that minimizes a complexity measure.

1. Normalize to Canonical.
2. Generate candidates by applying transforms: `Expand`, `Factor`, `Cancel`, `Together`, `Apart`, `PowerSimplify`, `LogCombine`, `LogExpand`, `TrigSimplify`, `RadicalSimplify`, `CombinatorialSimplify`, `PiecewiseFold`.
3. Best-first search with a beam (default width 8, depth 6) under the budget; stop when no candidate improves the measure.
4. Return the best form and the shortest derivation reaching it.

Default `IComplexityMeasure`: weighted leaf count (number 1, symbol 1, `+` and `·` 1, `^` 2, elementary function 3, special function 5, nested radical 5, plus a penalty for negative exponents and for unsimplified rational numbers). Callers can pass their own measure, for example "prefer factored form."

Named goal transforms (each with steps) correspond to textbook instructions: Expand, Factor, Simplify, Combine, Rationalize the denominator, Complete the square, Write in vertex form, Partial fractions, Rewrite in terms of sin and cos, Express as a single logarithm, Expand the logarithm, Write in polar/exponential form.

## Assumptions and domains

**`AssumptionSet.Ask(p)`** tries, in order, until it gets `True` or `False`:

1. Sorts and declared facts (`n ∈ ℤ`, `x > 0`).
2. Sign propagation over the tree using a sign lattice {−, 0, +, ≥0, ≤0, ≠0, real, unknown}: sums of non-negatives, products of signs, even powers, `exp`, `abs`, `sqrt`, `cosh`.
3. Interval arithmetic (`Interval<double>` with outward rounding) over bounds derived from the facts.
4. Linear arithmetic on small systems (Fourier–Motzkin elimination) for facts like `x > y`, `y > 0` ⊢ `x > 0`.
5. Parity and divisibility for integers.

Anything else is `Unknown`.

**Natural domain.** `Domain(expr, x)` collects constraints from operator domains (denominators ≠ 0, even-root arguments ≥ 0, logarithm arguments > 0, `arcsin`/`arccos` arguments in [−1, 1], `tan` argument ≠ π/2 + kπ …), solves them with the inequality solver and returns a set. Transformations that enlarge or shrink the domain record provisos; solvers use the domain to reject extraneous solutions.

## Zero testing

Deciding `e = 0` is undecidable in general (Richardson, 1968), so `ZeroTest` layers methods:

| Layer | Applies to | Result |
| --- | --- | --- |
| Canonical form | Polynomials and rational functions over ℚ or ℚ(i) | Exact `Zero`/`NonZero` |
| Algebraic numbers | Expressions in radicals | Exact via minimal polynomials (Milestone 2) |
| Structure theorem | exp-log expressions | Exact after detecting algebraic dependencies (Milestone 4) |
| Interval evaluation | Any expression at a point | `NonZero` when the enclosure excludes 0 (certain) |
| High-precision random evaluation | Everything else | `ProbablyZero` with stated confidence; for polynomials the Schwartz–Zippel bound gives the error probability |

## Polynomial engine

| Operation | Algorithm | Milestone |
| --- | --- | --- |
| Arithmetic | Dense/sparse classical; Kronecker substitution and Karatsuba for large inputs | 1 |
| Division | Long division, synthetic division (explained), pseudo-division | 1 |
| GCD | Euclid over fields; primitive/subresultant PRS over ℤ; modular GCD later | 1 |
| Square-free factorization | Yun's algorithm | 1 |
| Rational roots | Rational Root Theorem with bounds; explained | 1 |
| Root isolation | Sturm sequences; Descartes' rule with Vincent–Collins–Akritas | 2 |
| Factoring over ℚ | Content → square-free → Berlekamp or Cantor–Zassenhaus mod p → Hensel lifting → factor recombination | 2 |
| Factoring over ℚ(i) and algebraic extensions | Trager's norm method | 5 |
| Multivariate factoring | Wang's EEZ algorithm | 5 |
| Resultants and discriminants | Subresultant PRS; Sylvester matrix for teaching | 2 |
| Gröbner bases | Buchberger with Gebauer–Möller criteria; F4 later | 5 |

Primary references: Geddes, Czapor and Labahn, *Algorithms for Computer Algebra* (1992); von zur Gathen and Gerhard, *Modern Computer Algebra*.

## Calculus engines

**Differentiation.** Structural recursion: each operator's `DerivativeRule` entry gives its derivative; sum, product, quotient and chain rules produce nested sub-derivations so the explanation shows the inner derivative inside the chain-rule step. Also: implicit differentiation (`dy/dx` from F(x, y) = 0), logarithmic differentiation, higher and partial derivatives, derivatives of undefined functions (`f'(g(x))·g'(x)`).

**Integration pipeline** (first success wins; every antiderivative F is verified by checking `F' − f` is zero):

1. Linearity and constant factors.
2. Table lookup (catalog entries tagged `antiderivative`).
3. Polynomials.
4. Rational functions — complete: Hermite reduction plus the Lazard–Rioboo–Trager algorithm; explain mode shows partial fractions instead.
5. Derivative-divides substitution (find g'(x) as a factor up to a constant).
6. Integration by parts with LIATE ordering; tabular method for polynomial × exp/sin/cos.
7. Trigonometric integrals (powers of sin/cos, tan/sec strategies) and trigonometric substitution for √(a² − x²), √(a² + x²), √(x² − a²).
8. Weierstrass substitution for rational functions of sin and cos.
9. Risch–Norman heuristic and parts of the Risch algorithm for exp-log integrands (Milestone 4; Bronstein, *Symbolic Integration I*).
10. Otherwise `Unevaluated`.

Definite integrals use the Fundamental Theorem only after checking the antiderivative is continuous on [a, b]; otherwise they split at discontinuities (Weierstrass antiderivatives jump). Improper integrals become limits. A numeric Gauss–Kronrod value is always computed as a cross-check.

**Limits.** Direct substitution when continuous; algebraic manipulation (factor and cancel, rationalize, common denominator); known limits; L'Hôpital's rule (bounded, level ≥ Calculus1); series expansion; the Gruntz algorithm for exp-log functions (Milestone 4); oscillation detection for non-existent limits; numeric sanity check along a sequence.

**Series and sums.** Taylor series by power-series arithmetic (fast) or by derivatives (explained); Laurent and Puiseux series later. Sums: Faulhaber (power sums via Bernoulli numbers), geometric, telescoping, Gosper's algorithm for hypergeometric terms, Zeilberger later (Petkovšek, Wilf and Zeilberger, *A = B*), known-series table, convergence tests.

## Solving engine

`Solve(equation, x)` classifies, then dispatches; `SolveSet` returns a `SolutionSet` (see `04-type-system.md`).

| Class | Methods | Checks |
| --- | --- | --- |
| Linear, quadratic | Isolation; factoring, quadratic formula or completing the square (caller can choose) | |
| Cubic, quartic | Rational roots first, then Cardano/Ferrari; trigonometric form for the casus irreducibilis | |
| Higher-degree polynomial | Factor over ℚ, `RootOf` for irreducible factors of degree ≥ 5, numeric approximations attached | |
| Rational | Multiply by the LCD, solve, reject excluded values | Extraneous-solution step |
| Radical | Isolate, raise to powers, solve, test candidates | Extraneous-solution step |
| Absolute value | Case split | |
| Exponential, logarithmic | One-to-one properties, logs of both sides, substitution u = aˣ, combine logs | Domain check |
| Trigonometric | Reduce with identities to basic equations; general solutions as image sets; restrict to an interval when given | |
| Lambert-W forms | x·eˣ = c → x = W(c) and reductions to it | Branch notes |
| Anything else | Isolate real roots on an interval by sign changes, refine with Brent | `ConditionSet` with approximations |
| Inequalities | Sign charts from critical points; absolute-value rules; flip on multiplying by negatives | |
| Linear systems | Exact Gaussian elimination with steps; or Cramer's rule, inverse matrix, substitution, elimination by request | Unique / none / parametric |
| Polynomial systems | Substitution and resultants (Milestone 2); Gröbner bases (Milestone 5); numeric Newton | |
| Linear Diophantine | Extended Euclid | |
| Recurrences | Characteristic equation | Initial conditions |

Every candidate is substituted back exactly; a rejection is itself a step with its reason.

## Explanation rendering

- Input: a `Derivation` tree.
- Verbosity: `Brief` (top-level steps), `Standard` (default; arithmetic folded into its parent step), `Detailed` (every arithmetic step).
- Level filter: steps citing entries above `MathContext.Level` are replaced by an alternative method when one exists, or flagged.
- Text comes from each entry's `explain` template, filled with printed subexpressions (text or LaTeX).
- Output: plain text, Markdown, LaTeX `align*`, a JSON structure for apps, two-column proofs.
- Highlights: each step's `ExprPath` lets apps color the changed subexpression.
- Localization: templates are keyed by entry ID; translations live beside the catalog (later milestone).

## Evaluation and compilation

- `Evaluate(expr)` exact: rationals, exact constants kept symbolic, special values from the catalog.
- `N(expr, digits)`: `double` up to 15 digits, `BigFloat` beyond (Milestone 7).
- `Compile<T>(expr, params)`: post-order instruction array (`LoadVar`, `LoadConst`, `Add n`, `Mul n`, `Pow`, `Call op`) run by a span-based interpreter. `T` can be `double`, `Complex<double>`, `Interval<double>`, `Dual<double>` (value and derivative in one pass) or `BigFloat`.
- Vectorized evaluation over `Span<double>` uses `TensorPrimitives` for whole-array operations.

## Performance practices

- Structural hashes and free-symbol sets cached per node.
- Bounded per-call memo tables for normalization and simplification.
- Polynomial work happens in `SparsePolynomial<BigRational>`, never by rewriting trees.
- AC matching budgets; rule indexing by head operator.
- No parallelism in symbolic engines (determinism); numeric kernels may opt in.
