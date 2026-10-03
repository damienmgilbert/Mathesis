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

### Phase 5 implementation notes

Behaviors the design left open, as implemented (change them here first):

- **Defined-points semantics.** `AssumptionSet.Ask`, `ZeroTest` and `Sign` judge an expression at the points where every subexpression is defined and the facts hold. `sqrt(x) >= 0` is `True` with no assumption; `x/x = 1` is not refuted by `x = 0`. `Unknown` is returned whenever a layer cannot show the answer.
- **Layers of `Ask`:** exact numbers and sorts, sign lattice ({−, 0, +} plus `NonReal`), interval enclosures over bounds read off the facts, Fourier–Motzkin on linear facts (non-linear subterms are variables of their own; integer variables get cutting-plane tightening), and parity of integers (`mod 2`, `divides(2, n)`). Facts that are disjunctions are ignored.
- **`Interval<T>`** rounds outward by widening endpoints one ulp (arithmetic, `sqrt`) or four ulps (elementary functions), not by switching the rounding mode. Functions with a restricted domain evaluate over the part inside it; an operand wholly outside gives the empty interval. `x^y` with a negative base is `Entire` when an integer can occur in `y`.
- **`NaturalDomain.Of`** solves `h > 0`, `h ≥ 0`, `h ≠ 0` for rational functions with rational coefficients by a sign chart over exact roots (rational roots and quadratic factors in closed form). Periodic exclusions on an argument linear in the variable become image sets. Anything else (cubic factors without a rational root, non-rational `h`, conditions on parameters that the assumptions cannot decide) is returned as a condition set `{x ∈ ℝ | …}`. Operators with undescribed domains (`gamma`, `factorial`, …) return a failed outcome. `x^y` with `y` containing the variable requires `x > 0`.
- **`ZeroTest`** (`Zero`/`NonZero` mean identically zero / not identically zero): exact over ℚ(i) with non-rational subterms as independent variables, then interval exclusion at seeded sample points, then at least eight sample points whose enclosures contain a narrow 0 (`ProbablyZero`). Precision is that of `double` until BigFloat (Milestone 7); no error bound is reported. The algebraic-number (Milestone 2) and exp-log (Milestone 4) layers are not implemented.
- **`Compile<T>`** returns `Outcome<CompiledExpr<T>>` (an unsupported operator or a free symbol is data, not an exception), so the example in doc 03 reads `g.Value.Invoke(1.5)`. Exponents that are closed rationals (`div(1, 3)`) are recognized, so `(-8)^(1/3) = -2` in `double`, `Interval` and `Dual`; `Complex<double>` uses principal values. `arccot` has range (0, π) (`conv.arccot-range`); `log(x)` is base 10.
- **`Evaluate`** computes integer functions of exact arguments (`n!`, `n!!`, `binomial`, `perm`, `gcd`, `lcm`, `mod` and `quo` with floor semantics, `floor`, `ceil`, `round` half away from zero, `frac`, `abs`, `sign`, `min`, `max`); special values such as `sin(π/6)` wait for the catalog. Decimal literals are exact numbers, so only an explicit `Float` makes a function evaluate in double precision.

### Phase 7 implementation notes

Behaviors the design left open, as implemented (change them here first):

- **Layers.** Matching, rules, strategies and `Derivation`/`Step` live in `Mathesis.Symbolics` (`Patterns/`, `Rewriting/`); `PolynomialConversion` (expression ⇄ polynomial over ℚ, `TryToSparse` with non-polynomial subexpressions as atoms) is in `Representations/`. `RuleLibrary`, the transforms, `Simplifier`, `StepReplayer` and `ExplanationRenderer` are in `Mathesis` (`Simplification/`, `Explanation/`). `EntryId` and `CurriculumLevel` moved from `Mathesis.Knowledge` into `Mathesis.Symbolics` (same namespace) so that a `Step` can carry them; `MathContext` gained `Level`.
- **All rewrite states are Canonical.** A step replaces the node, normalizes the whole expression, and records the Canonical before and after. Rules derived from the catalog are normalized to Canonical too (wilds are atoms), so `a/sqrt(b)` is matched as `a·b^(−1/2)`. A first `normalize` step (no entry) records what Canonical did to the input; it is the only step that cites no entry.
- **Matching** is AC-aware and budgeted (`MatchOptions.MaxAttempts`, default 20 000). A plain wild operand of a sum or product absorbs a group of operands; a sequence wild takes the rest; a root pattern may leave operands over (`Bindings.Leftover`), which the rule re-attaches. Optional wilds (default 0 or 1) let one rule cover `x`, `3x` and `x·y`. A wild is optional only if it is a bare operand of a sum or product **and occurs once** in the pattern: with two occurrences its default would differ between a sum and a product (`(a + b)·(a − b)` would otherwise match `x·(x − 1)`).
- **Rules come from entries only.** An entry yields rules when it is a law, formula, axiom or theorem whose statement is an equation (or a conjunction of equations: one rule each, named `id#1`, `id#2`, …) between numeric expressions, or a pattern (`match → yields`), and its `orient` is not `none`. `where` is the guard, decided by `AssumptionSet.Ask` per conjunct: `True` fires, `False` blocks, `Unknown` becomes a proviso only for `e ≠ 0` in the default mode (`Strict` blocks, `Generic` adds every unknown condition). `orient: both` gives two rules; the right-to-left rule is named `id~rtl` and joins the second alternative of `tags: a | b`, or `<tag>-reverse` without alternatives, so no rule set contains both directions of one law. Entries are skipped (listed in `RuleLibrary.Skipped` with the reason) when a variable is not numeric, when both canonical sides coincide or the left side is a bare variable, or when the replacement or guard uses a variable the pattern does not bind.
- **Rule sets must terminate.** `NoRuleSetLoopsOnTheCorpus` runs every rule set to a fixed point on every corpus case with `maxIterations = 100` and fails on a cycle, the iteration cap or an exhausted budget. It found five catalog laws oriented the wrong way, now fixed in the catalog: `alg.abs.square` (reverse), `alg.cplx.div` (`orient: none`), `trig.exp.euler` (stated trig-first), `alg.log.natural` (`rtl`) and `alg.log.swap` (`none`), and the cofunction identities (`ltr`). `Fixpoint` stops at a repeated expression (`RewriteContext.HitCycle`) or at the iteration cap (`HitIterationCap`).
- **Strategies.** `TopDown` and `BottomUp` apply the strategy once, at the first node (pre-order or post-order from the focus) where it succeeds; `Innermost` and `Outermost` are their fixed points. `Algorithm` steps are named `algorithm:<name>` and look up the function in `Transforms.Algorithms`, which is how a recorded step is replayed.
- **Level filter.** `MathContext.Level` is honored where the steps are produced: a rule or algorithm whose entry is above it is never applied, so a derivation made at `Algebra1` cites nothing higher (the design's "alternative method" is another route found by the search). `ExplainOptions.Level` flags steps above the level in a derivation made without one.
- **`Replay`** takes an `IStepReplayer` (`StepReplayer.Default`): it re-applies the named rule with the recorded bindings, or re-runs the named algorithm, normalizes, and requires the recorded `After` exactly. A step whose `Before` does not equal the previous `After`, or whose replay differs, fails the replay.
- **`Simplify`** normalizes, applies the `Cleanup` rule sets (power, radical, absolute value, sign, logarithm and trig-reduce identities, fraction simplification, zero-product, negative exponents; not the complex or base-conversion sets), then runs a beam search (width 8, depth 6, `Patience = 2` levels without improvement, because an expansion may precede a cancellation) over `Expand`, `Factor`, `Together`, `Cancel`, `PowerSimplify`, `LogCombine`, `LogExpand`, `TrigSimplify`, `TrigReduce`, cleaning up after each. A budget that runs out gives `Partial` with the best form and its derivation. `Collect`, `Apart`, `Rationalize`, `CompleteSquare` and `TrigExpand` are goal transforms, not part of the default search, because they usually raise the default measure.
- **Algorithms** (each cites an entry): `Expand` of a sum raised to an integer power of 4 to 24 (`alg.poly.binomial-theorem` or `multinomial-theorem`; squares and cubes come from the `expand` rules); `Factor` by rational roots (`alg.poly.factor-theorem`; linear factors have integer content, the rest is a primitive cofactor); `Cancel` by polynomial gcd (`alg.frac.equivalent`, with the proviso `g ≠ 0` unless the assumptions settle it); `Together` by the least common multiple of denominators (`alg.frac.add-common`); `Apart` for denominators that split completely over ℚ, with polynomial division when improper (`alg.pf.distinct-linear` or `repeated-linear`); `CompleteSquare` with `a ≠ 0` decided by the assumptions (`alg.quad.completing-the-square`). All but `Expand` of general expressions work on one free symbol.
- **Explanation text** is the entry's `explain` with `{name}` filled from the step's arguments; placeholders written side by side, or a digit before a number, are separated by `·`. Verbosity: `Brief` (law sentences, then the result), `Standard` (adds before → after; automatic normalization is omitted), `Detailed` (adds normalization and sub-steps). Snapshots are in `tests/corpus/explain`; rewrite them with `MATHESIS_APPROVE=1` and review the diff.
- **Deferred:** `Derivation.ToProof` (Milestone 3, needs the logic kernel); radical simplification of numbers (`sqrt(8)` stays), `PiecewiseFold`, `CombinatorialSimplify`; multivariate `Factor`, `Cancel` and `Apart`; `ln(x^2)` for unknown sign of `x` (the catalog has the `abs` law only for `x^(2k)` forms).

### Phase 8 implementation notes

Behaviors the design left open, as implemented (change them here first):

- **Where things live.** `PowerSeries` is in `Mathesis.Symbolics` (`Representations/`); the engines are in `Mathesis.Calculus`: `Differentiator`, `Integrator` (with `IntegrateDefinite`), `Limits`, `Series`. Their algorithms are registered by name in `AlgorithmRegistry` (with the Phase 7 algorithms) so that `StepReplayer` can replay `diff` and `integrate` steps.
- **Differentiation is rewriting.** `Differentiate` rewrites `diff(e, x)` one catalog rule at a time (`TopDown` + `Fixpoint` over the algorithm `diff`, `ProvisoMode.Generic`, an iteration cap of 100 000 because derivatives of nested expressions need thousands of steps). The inner derivatives stay in the result as `diff` nodes and are expanded by the next steps, so the chain-rule step contains the inner derivative; the table lookup of a chain step is also kept as its `Substeps`. Structural rules (sum, constant multiple, product, general power, variable power, chain) are cited as `calc.deriv.*`; elementary functions are looked up in the `derivative-table` rules of the catalog by matching `diff(f(g), g)`. Guards that the assumptions cannot decide become provisos (`x > 0` for `ln x`, `u ≠ 0` for negative powers); an integer power with `n ≥ 1` needs none. `Implicit` returns `−F_x/F_y` with the proviso `F_y ≠ 0`; `Logarithmic` returns `f·(ln f)'` with `f > 0`. Operators without a rule (`floor`, …) leave `Unevaluated`.
- **Nested sub-derivations** are in `Step.Substeps` for the chain rule, implicit and logarithmic differentiation, integration (a step that needs a sub-integral carries it), limits (L'Hôpital, sums) and definite integrals (the antiderivative's derivation).
- **Integration pipeline.** The order is linearity, powers of x, the catalog table (rules tagged `antiderivative`, not `non-elementary`), rational functions, powers of sine and cosine and of tangent and secant, products of trigonometric functions (power reduction and product-to-sum, then expansion, up to four rounds), derivative-divides substitution (inner expression candidates from the arguments of functions and the bases of powers; `u = x^r` and linear `u` are inverted), integration by parts (LIATE; `u` is the highest-ranked factor, nested up to depth 6), the Weierstrass substitution for rational functions of sine and cosine, and trigonometric substitution for `sqrt(αx² + β)` (all three sign cases, expressed with sines and cosines). Methods that rewrite the problem are verified at once (differentiate back, `ZeroTest`: `Zero` or `ProbablyZero`) so a wrong sub-result makes the next method run; the answer is verified again at the top. **Hermite reduction and Lazard–Rioboo–Trager are not implemented**: rational functions are integrated when the denominator splits over ℚ, plus at most one irreducible quadratic factor of multiplicity one; others come back `Unevaluated`. Substitutions that need an inner expression that is not a subexpression (`x/(x⁴+1)` with `u = x²`) are not found.
- **Provisos from sub-integrals** are restated in the original variable after a substitution (`u ≠ 0` becomes `x² + 1 ≠ 0`); verification assumes the order-type provisos (`>`, `<`) while sampling.
- **Definite integrals.** `IntegrateDefinite` takes the verified antiderivative `F`, finds its natural domain (`NaturalDomain`), splits the interval at the discontinuities it can read from the domain (periodic exclusions `{e(k) | k ∈ ℤ}`, open ends of intervals), requires `F` to be continuous on each open piece, and takes one-sided limits of `F` at breakpoints where it is not defined (`calc.improper.type1`/`type2`); otherwise it uses `calc.ftc.part2`. If the continuity cannot be established the result is `Unevaluated`; a divergent integral is `Unevaluated`, never a principal value. The exact value is compared with `Quadrature.Infinite` (relative `1e−7` or 20 error estimates); a disagreement is `Unevaluated`.
- **Limits.** Methods in order: constant, rational function at ±∞ (degrees), direct substitution (the natural domain contains a neighborhood on the side of approach, and the function has no jump operator), catalog standard limits (entries tagged `standard-limit`, matched with the entry's variables as wilds), the "defined on each side" check (otherwise `DoesNotExist`), the algebra of limits (sums, products, powers, composition with the elementary functions at ±∞ and at poles, using the sign of the argument sampled near the point), the sum and constant-multiple rules for terms that need other methods, algebraic variants (`abs` by sign, trigonometric quotients, Cancel, Together, Rationalize, Expand, Factor, `Simplify`), series expansion (`PowerSeries`, level ≥ Calculus2), L'Hôpital's rule (level ≥ Calculus1; 0/0, ∞/∞, and 0·∞ as a quotient) and `u^v` through `exp(v ln u)`. Without a decision from those, a function that is bounded and keeps reversing direction along `2⁻ᵏ` is reported as oscillating. Every returned result is sanity-checked against the function at three steps toward the point (a disagreement becomes `Unevaluated`). A two-sided limit that no method decides is tried from each side and compared. `LimitResult` is `Finite`, `PositiveInfinity`, `NegativeInfinity` or `DoesNotExist`.
- **Series.** `PowerSeries` is a truncated Laurent series over ℚ with an order term; `(c·t^v·(1+u))^p` needs `v·p ∈ ℤ`, `c > 0` with `c^p` rational, and for `t < 0` an even `v`. `Series.Taylor` uses series arithmetic when the center is a rational number and the function is built from `exp`, `sin`, `cos`, `sinh`, `cosh`, `tan`, `arctan`, `arcsin`, `arsinh`, `artanh`, `ln(1 + …)` and powers (`calc.series.series-arithmetic`); otherwise, or on request, the definition by derivatives at the center (`calc.series.taylor`). A removable singularity at the center is filled in by the arithmetic path; the derivative path returns `Unevaluated`.
- **Catalog additions** (all `verify: none` with a rationale, because their statements are about infinite sums or integrals with bounds): `knowledge/calc/series.mlaw` (Taylor, remainder, series arithmetic, the Maclaurin laws), `knowledge/calc/definite.mlaw` (zero width, reversing limits, FTC part 2, improper integrals of both types). Also `alg.ax.mul-inverse` is now a `fraction-simplify` rule (`a·(1/a) = 1` for `a ≠ 0`), which the integrator needs to cancel transcendental factors.
- **Deferred:** Hermite/LRT, the Risch algorithm and Gruntz (Milestone 4); improper integrals whose singularities cannot be read from the domain; limits needing exp-log asymptotics beyond L'Hôpital and series; parametric and polar derivatives; the table of non-elementary integrals (`si`, `li`, `erf` forms are skipped because their derivatives are not evaluated numerically yet).

### Phase 9 implementation notes

Behaviors the design left open, as implemented (change them here first):

- **Where things live.** `Mathesis.Solving`: `Solver` (`Solve` for equations and inequalities, `SolveSystem`), `SolutionSet`, `SolveOptions` (quadratic method, interval restriction). Real mode only; complex roots of a polynomial are not reported (`x² + 1 = 0` is `∅`).
- **`SolutionSet`** wraps the set expression (`SetLiteral`, `IntervalLiteral` unions, image sets `{e(k) | k ∈ ℤ}` as `Bind(ImageSet)`, a `SetBuilder` condition set) with typed access: `Kind` (`Empty`, `Finite`, `Intervals`, `All`, `Image`, `Condition`, `Parametric`), `Points`, `Families`, `Pieces`, `Approximations`, `Parameters`. Elements without a closed form are `Float` literals and `IsApproximate` is set. A condition set lists the numeric roots found by sign changes on a window (`[-100, 100]` unless an interval is given) and is marked `IsComplete = false`.
- **Candidates, then a check.** Every method only produces candidates (it may add solutions: raising to a power, clearing denominators, taking logarithms of positive quantities). Every candidate is then substituted back into the original equation exactly (`Evaluate` and `ZeroTest`; within 1e−10 for approximate roots); one that is undefined or non-zero is rejected with a step citing `alg.eq.extraneous` and the candidate and reason as arguments. Image families are checked at `k = −3 … 3`.
- **Methods, in order:** the zero-product property for products; polynomial and rational functions over ℚ (clear the denominator, rational roots, square-free factors; quadratic factors in closed form by the quadratic formula, by factoring or by completing the square as the caller chooses; quadratic in form `x^k`; binomials `xⁿ = c` as radicals; anything else numeric real roots by Aberth iteration polished with Newton's method); literal equations of degree 1 and 2 with symbolic coefficients (conditions such as `a ≠ 0` and `b² − 4ac ≥ 0` are provisos unless the assumptions decide them); absolute value by cases; radicals (substitution `w = x^(1/q)` when the radicand is `x`, otherwise isolate, raise and expand); exponentials (a common base `b` turns `b^(ax + d)` into powers of `y = b^(x/m)`; unrelated bases take logarithms); logarithms (substitution `u = ln x`, or combination of logarithms of one base into one equation between arguments); trigonometric equations in one function of one linear argument, in sine and cosine (linear forms by `R·sin(g + φ)`, even forms by the Pythagorean identity, homogeneous forms through the tangent, otherwise factoring), after expanding multiples of the angle. Exact inverse values come from the catalog entries `trig.exact.*` (a table built from their statements); other values stay `arcsin(a)` and so on.
- **Not implemented:** Cardano and Ferrari, `RootOf`, Lambert W reductions, complex solutions, inequalities with trigonometric functions or symbolic parameters, absolute-value inequalities whose boundaries are not found by the equation methods, polynomial systems beyond substitution (resultants and Gröbner bases are Milestones 2 and 5), Diophantine equations and recurrences.
- **Inequalities** use a sign chart: the critical points are the zeros (from the equation methods) and the finite boundaries of `NaturalDomain`; each open cell is tested at an interior point and each critical point on its own, so closed and open ends and isolated points come out right. Equations whose domain is periodic are left `Unevaluated`.
- **Linear systems** use `ExactLinearAlgebra.RowReduce` over `BigRational`, one step per row operation (`linalg.sys.row-ops`), then the classification (`linalg.sys.rouche-capelli`, `alg.sys.classification`): a unique tuple, `∅`, or a parametric family whose free unknowns become parameters `t, s, …`. Other systems use substitution (`alg.sys.substitution`): an equation linear in some unknown with a numeric coefficient is solved for it, and the last unknown is solved as an equation; every tuple is checked in every equation.
- **Steps** are the same `Step` records as the simplifier's, but they describe equations: `Before` and `After` are equations (or disjunctions of equations, or the solution set). They are not replayable (there is no algorithm to re-run); the check by substitution is the proof.
- **Catalog additions** (`verify: none` with a rationale): `knowledge/trig/equations.mlaw` (`trig.eqn.sin`, `cos`, `tan`, `cot`) and `knowledge/alg/systems.mlaw` (`alg.ineq.sign-chart`, `compound`, `alg.sys.substitution`, `elimination`, `classification`).
