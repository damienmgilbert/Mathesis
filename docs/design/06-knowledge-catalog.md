# 06 Knowledge catalog

The catalog is where the mathematics lives. Every definition, axiom, law, identity, theorem, formula, named pattern, method and convention is an entry with a stable ID, a formal statement in Mathesis notation, its exact conditions, a plain-language explanation and a curriculum level. Engines turn entries into rewrite rules, solver strategies and explanation text; CI verifies every entry it can.

## Entry kinds

| Kind | What it is | How engines use it | Verified by |
| --- | --- | --- | --- |
| `definition` | Meaning of a term or operator (continuity, injective, determinant) | Unfolding during proofs; glossary; sort checking | Consistency tests where computable |
| `axiom` | Accepted starting point (field axioms, Peano, probability axioms) | Proof kernel rules; explanations ("by the distributive property") | Not verified; must cite a source |
| `law` | Identity or equivalence valid under stated conditions (exponent laws, trig identities, De Morgan) | Oriented into rewrite rules; equational proof steps | Randomized numeric verification (default) |
| `theorem` | Hypotheses ⇒ conclusion (Mean Value Theorem, Rank–Nullity, Bayes) | Proof steps; solver strategies (Rational Root Theorem); explanation citations | Instance checks or kernel proofs |
| `formula` | Named relation among quantities (quadratic formula, compound interest, Law of Cosines) | Solvable for any variable; calculators; explanations | Numeric verification and solve-back checks |
| `pattern` | Named recognizable shape (difference of squares, perfect-square trinomial, separable ODE) | Recognizers in factoring, classification and explanations | Match/yield equivalence checks |
| `method` | Named procedure taught by name (completing the square, integration by parts, simplex) | Links to the implementing engine; step templates; method choice in `Solve` | Engine tests |
| `convention` | A choice where mathematics allows several (0⁰ = 1, bare `log` is base 10, real odd roots) | Parser and evaluator configuration; explanation footnotes | Documented, tested by unit tests |

## IDs

- Format: `<domain>.<topic>.<name>` in lowercase kebab-case, for example `alg.exp.product-of-powers`, `trig.sum.sin-of-sum`, `calc.deriv.chain`.
- Domain prefixes:

| Prefix | Domain | Doc |
| --- | --- | --- |
| `alg` | Algebra (incl. arithmetic, polynomials, rational and radical expressions) | `domains/d1-algebra.md` |
| `trig` | Trigonometry (incl. hyperbolic functions) | `domains/d2-trigonometry.md` |
| `logic` | Proofs, logic, sets, relations | `domains/d3-proofs-and-logic.md` |
| `pre` | Pre-Calculus (functions, conics, complex numbers, sequences, vectors intro) | `domains/d4-precalculus.md` |
| `calc` | Calculus (single and multivariable, vector calculus, series) | `domains/d5-calculus.md` |
| `ode` | Differential equations (ODE, Laplace, Fourier, PDE) | `domains/d6-differential-equations.md` |
| `linalg` | Linear algebra | `domains/d7-linear-algebra.md` |
| `fin` | Finite mathematics (counting, probability, statistics, finance, LP, Markov, games, graphs, number theory under `fin.nt`) | `domains/d8-finite-mathematics.md` |
| `num` | Numerical analysis | `domains/d9-numerical-analysis.md` |
| `conv` | Conventions (cross-domain) | this doc |

- IDs are permanent once released. A renamed entry keeps its old ID in `aliases`.
- Every ID that appears in the first column of a table in `domains/*.md` must exist in the catalog; `eng/check-catalog-coverage.cs` reports coverage per domain (goal G1).

## The `.mlaw` format

Entries are authored in plain-text `.mlaw` files under `knowledge/<domain>/<topic>.mlaw`, embedded in `Mathesis.Knowledge` as resources, and parsed by Mathesis's own parser. Statements use the input notation from `05-syntax-trees-and-notation.md`.

### Grammar

```text
file     := header entry*
header   := "domain" PREFIX STRING NEWLINE ("uses" PREFIX NEWLINE)*
entry    := KIND NAME STRING NEWLINE field*
KIND     := "definition" | "axiom" | "law" | "theorem" | "formula" | "pattern" | "method" | "convention"
field    := INDENT KEY ":" VALUE NEWLINE (INDENT INDENT VALUE NEWLINE)*     # continuation lines
KEY      := vars | statement | given | then | defines | iff | where | complex | orient
          | match | yields | applies-to | steps | result | solve-for | quantities
          | level | course | tags | explain | aliases | refs | see | verify | sample
          | implemented-by | rationale
comment  := "#" to end of line
```

### Fields

| Field | Meaning |
| --- | --- |
| `vars` | Pattern variables and their sorts (`a: real, n: integer, f: function(R -> R), A: matrix(n, n)`). Every free symbol in the statement must be declared. |
| `statement` | The law or formula: an equation, equivalence (`<=>`), inequality or proposition |
| `given` / `then` | Theorem hypotheses (one per line) and conclusion |
| `defines` / `iff` | Definition: the defined predicate or operator and its meaning |
| `where` | Conditions in **real mode** (the default). Omitted means "for all values of the declared sorts where both sides are defined" |
| `complex` | Conditions in complex mode when they differ, using principal branches |
| `orient` | `ltr`, `rtl`, `both` or `none`: which directions become rewrite rules |
| `match` / `yields` | Pattern recognizer and the form it produces |
| `applies-to`, `steps`, `result` | Method description: input shape, named steps with intermediate forms, final form |
| `solve-for` | Variables a formula can be solved for (default: all) |
| `quantities` | Plain-language names of formula variables |
| `level` | Curriculum level (below) |
| `course` | Course tags for browsing (`Algebra`, `Trigonometry`, `Proofs`, `PreCalculus`, `Calculus`, `DifferentialEquations`, `LinearAlgebra`, `FiniteMath`, `NumericalAnalysis`) |
| `tags` | Rule-set membership (`combine-powers`, `expand-log`, `trig-reduce`) and search keywords |
| `explain` | Explanation template with `{var}` placeholders, rendered in text or LaTeX |
| `refs` | Sources: `dlmf:4.21.2`, `book:"Stewart, Calculus, 9e, §3.4"`, `url:…` |
| `see` | Related entry IDs |
| `verify` | `numeric` (default for laws and formulas), `instances`, `proof`, `none` (requires `rationale`) |
| `sample` | Hints for the verifier's sampler (`n in 1..12`, `x in (0, 10)`) |
| `implemented-by` | Documentation ID of the engine method that implements a method or formula |

### Examples

```text
domain alg.exp "Laws of exponents"

law product-of-powers "Product of powers"
  vars:      a: real, m: real, n: real
  statement: a^m * a^n = a^(m + n)
  where:     a > 0 or (a != 0 and m in Z and n in Z) or (a = 0 and m > 0 and n > 0)
  complex:   a != 0 or (re(m) > 0 and re(n) > 0)
  orient:    ltr
  level:     Algebra1
  course:    Algebra
  tags:      combine-powers
  explain:   "Same base: add the exponents, {a}^{m}·{a}^{n} = {a}^({m}+{n})."
  see:       alg.exp.quotient-of-powers, alg.exp.power-of-power

law power-of-power "Power of a power"
  vars:      a: real, m: real, n: real
  statement: (a^m)^n = a^(m*n)
  where:     a >= 0 or n in Z
  orient:    ltr
  level:     Algebra1
  tags:      combine-powers
  explain:   "Raise a power to a power: multiply the exponents."
  # Counterexample when a < 0 and n is not an integer: ((-1)^2)^(1/2) = 1 but (-1)^1 = -1.
```

```text
domain trig.sum "Sum and difference identities"

law sin-of-sum "Sine of a sum"
  vars:      u: complex, v: complex
  statement: sin(u + v) = sin(u)*cos(v) + cos(u)*sin(v)
  orient:    both
  level:     PreCalculus
  course:    Trigonometry
  tags:      trig-expand | trig-combine
  explain:   "Angle-sum identity for sine."
  refs:      dlmf:4.21.2
```

```text
domain calc.thm "Theorems of differential calculus"

theorem mean-value "Mean Value Theorem"
  vars:    f: function(R -> R), a: real, b: real
  given:   a < b
           continuous(f, [a, b])
           differentiable(f, (a, b))
  then:    exists c in (a, b): f'(c) = (f(b) - f(a)) / (b - a)
  level:   Calculus1
  verify:  instances     # random polynomials and smooth functions; c located by root finding
  see:     calc.thm.rolle
```

```text
domain alg.quad "Quadratics"

formula quadratic-formula "Quadratic formula"
  vars:       a: complex, b: complex, c: complex, x: complex
  statement:  a*x^2 + b*x + c = 0 <=> (x = (-b + sqrt(b^2 - 4a*c)) / (2a) or x = (-b - sqrt(b^2 - 4a*c)) / (2a))
  where:      a != 0
  solve-for:  x
  quantities: a "leading coefficient", b "linear coefficient", c "constant term"
  level:      Algebra1
  implemented-by: M:Mathesis.Solving.Quadratic.Solve

pattern difference-of-squares "Difference of squares"
  vars:   a, b
  match:  a^2 - b^2
  yields: (a - b)*(a + b)
  level:  Algebra1
  tags:   factor
  explain: "{a}² − {b}² is a difference of squares, so it factors as ({a} − {b})({a} + {b})."

method completing-the-square "Completing the square"
  vars:       a, b, c, x
  applies-to: a*x^2 + b*x + c
  where:      a != 0
  steps:      "Factor a out of the x terms"      a*(x^2 + (b/a)*x) + c
              "Add and subtract (b/(2a))^2"      a*(x^2 + (b/a)*x + (b/(2a))^2) + c - a*(b/(2a))^2
              "Write the perfect square"         a*(x + b/(2a))^2 + c - b^2/(4a)
  result:     a*(x + b/(2a))^2 + (4a*c - b^2)/(4a)
  level:      Algebra1
  implemented-by: M:Mathesis.Algebra.CompleteSquare.Apply
```

```text
domain conv "Conventions"

convention zero-to-the-zero "0^0 = 1"
  statement: 0^0 = 1
  rationale: "Keeps the binomial theorem, power series and polynomial evaluation uniform; matches IEEE 754 pow and Math.Pow(0, 0). As a limit form 0^0 remains indeterminate (calc.lim.indeterminate-forms)."

convention real-odd-root "Real odd roots"
  statement: x^(p/q) = root(x, q)^p  where q odd, gcd(p, q) = 1, x < 0, real mode
  rationale: "Textbook convention: (-8)^(1/3) = -2 in real mode; complex mode uses the principal branch."

convention bare-log-base-10 "Bare log is base 10"
  rationale: "School convention; ParserOptions.LogMeansNatural switches it."
```

## Curriculum levels

`CurriculumLevel` is ordered; explanations at level L never cite an entry above L.

```text
Arithmetic < PreAlgebra < Algebra1 < Geometry < Algebra2 < PreCalculus < Calculus1 < Calculus2
  < Calculus3 < University < Advanced
```

University courses (Linear Algebra, Differential Equations, Finite Mathematics, Numerical Analysis, Proofs) use `University` plus their `course` tag. Finite Mathematics entries that only need algebra use `Algebra2`.

## From entries to rules

1. `orient: ltr` turns `lhs = rhs` into a rule `lhs → rhs`; `rtl` the reverse; `both` creates two rules in different rule sets (their `tags` decide which, separated by `|`); `none` is never used for rewriting.
2. `vars` become pattern wildcards with sort constraints.
3. `where` (or `complex`) becomes the rule's guard. At match time the engine asks the `AssumptionSet`:
   - `True`: apply.
   - `False`: skip.
   - `Unknown`: in **strict** mode skip; in **generic** mode apply and add the condition to the step's provisos ("assuming x ≠ 0"). Simplify defaults to generic mode for conditions of the form "expression ≠ 0" and strict mode otherwise.
4. Rules are grouped by `tags` into rule sets (`combine-powers`, `expand-log`, `trig-reduce` …) that strategies reference by name.
5. Rules are indexed by the head operator and a discrimination tree over the left-hand side so thousands of rules cost little per match.

## Verification harness

`Mathesis.Knowledge.Tests` runs on every build.

- **Laws and formulas (`verify: numeric`).** For each entry, draw 200 assignments from each variable's sort (small integers, simple rationals, uniform reals in [−10, 10], values near 0 and near singularities, large magnitudes; complex values in complex mode), keep those satisfying `where`, evaluate both sides in `double` and `Complex<double>`, and require agreement within a relative tolerance of 1e−9 scaled by an estimated condition number. Exact rational evaluation is used when both sides are rational expressions. A failure prints the counterexample and fails CI.
- **Condition probing.** The harness also samples points that violate `where`. If the law still holds everywhere it warns "conditions may be stronger than necessary"; this keeps conditions honest without failing builds.
- **Theorems (`verify: instances`).** A generator per theorem builds random instances (polynomials, matrices, distributions) and checks the conclusion numerically, for example locating c in the Mean Value Theorem by root finding.
- **Proofs (`verify: proof`).** From Milestone 3, stored proofs are replayed by the logic kernel.
- **Patterns and methods.** `match` and `yields` must be equivalent (numeric check); method `steps` must chain (each step equivalent to the previous).
- **Parse round trip.** Every statement parses, prints and parses back to the same tree.
- **Uniqueness and links.** IDs are unique; `see`, `aliases` and `implemented-by` targets exist.

## Tooling

| Tool | What it does |
| --- | --- |
| `eng/gen-knowledge.cs` | Parses all `.mlaw` files and writes `Catalog.g.cs` with typed handles (`Laws.Algebra.Exponents.ProductOfPowers`); checked in |
| `eng/check-catalog-coverage.cs` | Compares IDs in `docs/design/domains/*.md` tables with the catalog; prints coverage per domain and the missing IDs |
| `eng/mlaw-lint.cs` | Formatting, field order, undeclared variables, missing levels |

## Contributing an entry

1. Find the domain doc row (or add one) and its ID.
2. Add the entry to the right `.mlaw` file with exact conditions; include a counterexample comment when a condition exists only to exclude one.
3. Run `dotnet test` (the harness verifies it) and `dotnet run eng/gen-knowledge.cs`.
4. If an engine should use it, give it `tags` for a rule set or `implemented-by` for a method.

## Size targets

The domain docs already name about 1,640 entries; the rest come from filling out families (more exact values, integral tables, distributions, special-function identities) as each milestone lands.

| Domain | Milestone 1 | At completion |
| --- | --- | --- |
| Algebra (incl. conventions) | 210 | 450 |
| Trigonometry | 120 | 250 |
| Proofs & Logic | 0 | 200 |
| Pre-Calculus | 0 | 250 |
| Calculus | 140 | 600 |
| Differential Equations | 0 | 250 |
| Linear Algebra | 35 | 350 |
| Finite Mathematics | 0 | 400 |
| Numerical Analysis | 0 | 250 |
| **Total** | **≈ 500** | **≈ 3,000** |

## Phase 6 implementation notes

Behaviors the design left open, as implemented (change them here first):

- **Where the catalog lives.** `knowledge/<domain>/<topic>.mlaw` at the repository root is embedded in `Mathesis.Knowledge` under the logical name `knowledge/<domain>/<file>.mlaw`; `KnowledgeBase.Default` loads it, `KnowledgeBase.Load` loads any sources. A file may contain several `domain` groups (each with its own `uses` lines), so one file can hold `alg.ax`, `alg.prop` and `alg.eqprop`; the entry ID is the group followed by the entry name. Continuation lines are indented deeper than their field key.
- **Kinds without statements.** Theorems, definitions, methods and conventions may have only an `explain` or `rationale`; an entry needs at least one of statement, conclusion, definition, pattern, method description, explanation or rationale. Axioms must cite `refs`; `verify: none` needs a `rationale`.
- **What is verified.** `verify` defaults to `numeric` for laws, formulas and patterns, and for methods that give an equivalence (`steps`, or `applies-to` with `result`); everything else defaults to `none`. A theorem or definition with a concrete statement can opt in with `verify: numeric`, and a theorem stated for all n but checked on a concrete shape uses `verify: instances` (the 3×3 determinant theorems).
- **Meaning of a numeric check.** A statement is evaluated at assignments drawn from each variable's sort (small integers, simple rationals, uniform reals, values near 0, a few large ones; hints from `sample` override). An assignment counts only if `where` holds and every subexpression is defined (undefined, overflowing, underflowing or round-off-undecidable samples are skipped); at least 30 and up to 200 counted assignments must all satisfy the statement. Equality is relative (1e-9), plus an absolute allowance of 1e-12 times the largest operand of an addition, subtraction or circular function (cancellation); statements with derivatives, integrals or limits use an absolute and relative tolerance of 1e-5 (3e-4 for limits) and avoid huge arguments. The sampler is seeded per entry, so runs repeat exactly (`MATHESIS_SEED=n` draws another set).
- **Round-off sensitivity** (the "estimated condition number" of the harness). The fixed allowances above do not cover a division by a cancelling sum: in `tan(x/2) = sin(x)/(1 + cos(x))` at `x = -9.4245629`, 2e-4 from the excluded point `-3π`, `1 + cos(x)` is about 2e-8, so the rounding error of `cos(x)` becomes a relative error of a few 1e-9 in the right-hand side (1.4e-9 at that sample, against the tolerance of 1e-9). Perturbing the sample by a few ulps does not reveal this (the rounded `cos(x)` does not change), so the harness measures it directly. When a relation of the statement fails at a sample where `where` holds, both sides are evaluated 8 more times with the result of every operation perturbed by a random relative error of up to 2⁻⁵² (stochastic arithmetic: Vignes' CESTAC, Parker's Monte Carlo arithmetic). Integer-valued results stay exact, because they feed discrete tests (exponents, sum bounds, `floor`); the seed is fixed, so runs repeat; a perturbed run in which a side is undefined is ignored. The largest deviation of each side from its nominal value, summed and multiplied by 8, is the sample's round-off allowance. If the relation would hold with the computed difference off by that allowance in the favorable direction, the sample cannot decide it and is skipped like an undefined one; otherwise it is a counterexample. Relations that hold are never re-evaluated, conditions are always judged without perturbation, and the tolerances above are unchanged: a law that verified before still does, and a wrong law cannot pass by widening, it can only become inconclusive (fewer than 30 decidable samples).
- **Complex mode.** Entries with complex variables, or that mention `I`, are evaluated in the complex field (principal branches). A `complex:` field is verified separately: real-declared variables are then real half the time and complex otherwise, and a condition that is undefined (an order relation on a non-real number) skips the sample.
- **Conventions in the verifier.** `0^0 = 1`; a literal exponent p/q with odd q is a real root of a negative base (`conv.real-odd-root`), a computed exponent is the principal real power.
- **Calculus statements.** Function-valued variables (`f: function(R -> R)`) are replaced by one of ten smooth concrete functions with known derivatives; `diff` is a Richardson-extrapolated difference accepted only if two step sizes agree; `integrate(f, x, a, b)` is Gauss–Kronrod quadrature; an identity between indefinite integrals holds up to a constant, checked as "the difference of the two sides does not change between two points" with every integral starting at the first point; `limit` extrapolates in the distance to the point (or in 1/x). Sample hints keep samples away from singularities.
- **Formulas that define a quantity.** A formula `v = expr` with `v` a bare declared variable is "solved back": `v` is computed from the right-hand side and the verifier checks that the right-hand side is defined and the conditions are satisfiable. It does not test the formula against anything else.
- **Matrices.** Variables of sort `matrix(m, n)` and `vector(n)` are drawn with small integer or rational entries (dimensions from a declared natural variable with a `sample` hint, or random 1–4); products, sums, powers, transpose, inverse, `adj`, `identity`, `det` and `trace` are evaluated.
- **Weak checks.** An equivalence such as the quadratic formula or `a*x + b = 0 <=> x = -b/a` with an independently drawn `x` is almost always false on both sides; the numeric check then confirms little. They are candidates for stronger checks (sampling a root, solve-back) in Milestone 2.
- **Condition probing** warns when a statement also holds on a sample that violates `where`; it never fails the build. Some warnings are expected (a condition may state the textbook hypothesis, not the weakest one).
- **Typed handles.** `eng/gen-knowledge.cs` writes `src/Mathesis.Knowledge/Catalog.g.cs`: one static class per kind (`Laws`, `Axioms`, `Definitions`, `Theorems`, `Formulas`, `Patterns`, `Methods`, `Conventions`), nested by domain name and topic in PascalCase, e.g. `Laws.Algebra.Exp.ProductOfPowers` (names starting with a digit get an underscore: `Laws.Trigonometry.Exact._3pi10`). `--check` fails when the file is out of date.
- **Known gaps.** `calc.itab.inv-log` (`li`) is `verify: none` (no closed form to check against); `linalg.det.block-triangular` is stated for 1×1 blocks (block matrices are not sampled); the Leibniz rule is checked to order 2 (numeric derivatives of order 3 are not accurate enough).
- **Parser change.** A function symbol the caller declares (`f`, `u`, `H`) now shadows an operator abbreviation of the same spelling when it is applied, so a declared `u(x)` is a call of `u`, not the Heaviside function.
