# `.mlaw` format reference

Condensed from `06-knowledge-catalog.md` and `05-syntax-trees-and-notation.md`. If the project docs are available and seem to differ, the docs win — flag the difference.

## Contents
1. Grammar
2. Fields
3. Entry kinds
4. ID prefixes
5. Curriculum levels and course tags
6. From entries to rules (what `orient`, `where`, `tags` do)
7. Verification harness (what CI will check)
8. Examples

## 1. Grammar

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

Files live at `knowledge/<domain>/<topic>.mlaw`. The header's PREFIX is the ID stem (e.g. `domain alg.exp "Laws of exponents"`), and each entry NAME is the last ID segment, so `law product-of-powers` in that file has ID `alg.exp.product-of-powers`.

## 2. Fields

| Field | Meaning |
| --- | --- |
| `vars` | Pattern variables and sorts (`a: real, n: integer, f: function(R -> R), A: matrix(n, n)`). Every free symbol must be declared. |
| `statement` | Equation, equivalence (`<=>`), inequality or proposition |
| `given` / `then` | Theorem hypotheses (one per line) and conclusion |
| `defines` / `iff` | Definition: defined predicate/operator and its meaning |
| `where` | Conditions in real mode (default). Omitted = all values of the declared sorts where both sides are defined |
| `complex` | Complex-mode conditions when they differ (principal branches) |
| `orient` | `ltr`, `rtl`, `both`, `none` |
| `match` / `yields` | Pattern recognizer and produced form |
| `applies-to`, `steps`, `result` | Method: input shape, named steps with intermediate forms, final form |
| `solve-for` | Variables a formula can be solved for (default all) |
| `quantities` | Plain-language names of formula variables |
| `level` | Curriculum level |
| `course` | Course tag for browsing |
| `tags` | Rule-set membership and keywords; `|` separates the two rule sets of an `orient: both` entry |
| `explain` | Template with `{var}` placeholders |
| `refs` | `dlmf:4.21.2`, `book:"Author, Title, ed., §x"`, `url:…` |
| `see` | Related IDs |
| `verify` | `numeric` (default for laws/formulas), `instances`, `proof`, `none` (needs `rationale`) |
| `sample` | Sampler hints (`n in 1..12`, `x in (0, 10)`) |
| `implemented-by` | Doc ID of the engine method (`M:Mathesis.Solving.Quadratic.Solve`) |
| `aliases` | Old IDs of a renamed entry |
| `rationale` | Why (conventions, `verify: none`) |

## 3. Entry kinds

| Kind | Engines use it for | Verified by |
| --- | --- | --- |
| `definition` | Unfolding, glossary, sort checking | Consistency tests where computable |
| `axiom` | Proof-kernel rules, explanations | Not verified; must cite a source |
| `law` | Oriented rewrite rules, equational proof steps | Randomized numeric check (default) |
| `theorem` | Proof steps, solver strategies, citations | Instance checks or kernel proofs |
| `formula` | Solve for any variable, calculators | Numeric + solve-back |
| `pattern` | Recognizers in factoring/classification | `match` ≡ `yields` numerically |
| `method` | Step templates, method choice in `Solve` | Engine tests; `steps` must chain |
| `convention` | Parser/evaluator configuration, footnotes | Unit tests |

## 4. ID prefixes

| Prefix | Domain | Doc |
| --- | --- | --- |
| `alg` | Algebra | `d1-algebra.md` |
| `trig` | Trigonometry (incl. hyperbolic) | `d2-trigonometry.md` |
| `logic` | Proofs, logic, sets, relations | `d3-proofs-and-logic.md` |
| `pre` | Pre-Calculus | `d4-precalculus.md` |
| `calc` | Calculus | `d5-calculus.md` |
| `ode` | Differential equations | `d6-differential-equations.md` |
| `linalg` | Linear algebra | `d7-linear-algebra.md` |
| `fin` | Finite math (number theory under `fin.nt`) | `d8-finite-mathematics.md` |
| `num` | Numerical analysis | `d9-numerical-analysis.md` |
| `conv` | Cross-domain conventions | `06-knowledge-catalog.md` |

Format `<domain>.<topic>.<name>`, lowercase kebab-case, permanent once released.

## 5. Curriculum levels and course tags

```text
Arithmetic < PreAlgebra < Algebra1 < Geometry < Algebra2 < PreCalculus < Calculus1 < Calculus2
  < Calculus3 < University < Advanced
```

University courses (Linear Algebra, Differential Equations, Finite Mathematics, Numerical Analysis, Proofs) use `University` plus their course tag. Finite-math entries needing only algebra use `Algebra2`.

Course tags: `Algebra`, `Trigonometry`, `Proofs`, `PreCalculus`, `Calculus`, `DifferentialEquations`, `LinearAlgebra`, `FiniteMath`, `NumericalAnalysis`.

## 6. From entries to rules

1. `ltr`: `lhs → rhs`; `rtl` the reverse; `both` makes two rules in the rule sets named by `tags: left | right`; `none` never rewrites.
2. `vars` become sort-constrained wildcards.
3. `where`/`complex` becomes the guard. `AssumptionSet` answers True (apply), False (skip) or Unknown: strict mode skips, generic mode applies and records the condition as a proviso. Simplify uses generic mode for "expr ≠ 0" conditions and strict otherwise — so a condition written as `x != 0` will often be applied with a proviso, while `x > 0` will block the rule unless known.
4. Rules group by `tags` into rule sets (`combine-powers`, `expand-log`, `trig-reduce`, `factor` …).

Practical consequence: split a disjunctive condition into the forms the assumption engine can decide. `a > 0 or (a != 0 and m in Z and n in Z)` is fine; a condition like "a is not a negative real with non-integer exponent" is not.

## 7. Verification harness (CI)

- Laws/formulas: 200 assignments per variable drawn by sort (small integers, simple rationals, uniform reals in [−10, 10], near 0, near singularities, large magnitudes; complex in complex mode); keep those satisfying `where`; compare both sides in `double` and `Complex<double>` within 1e−9 relative (scaled by a condition estimate). Exact rational evaluation when both sides are rational.
- Condition probing: sample points violating `where`; if the law still holds everywhere, warn that conditions may be stronger than necessary.
- Theorems with `verify: instances`: a generator builds random instances.
- Patterns: `match` ≡ `yields`. Methods: each step equivalent to the previous.
- Parse round trip; unique IDs; `see`, `aliases`, `implemented-by` targets exist.

## 8. Examples

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
  verify:  instances
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
  rationale: "Keeps the binomial theorem, power series and polynomial evaluation uniform; matches IEEE 754 pow. As a limit form 0^0 remains indeterminate (calc.lim.indeterminate-forms)."
```
