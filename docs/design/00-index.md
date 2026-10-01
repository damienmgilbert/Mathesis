# Mathesis design docs

These docs define the architecture, types, notation, knowledge catalog and per-domain coverage for Mathesis, the symbolic and numerical math library in `PLAN.md` (Plan 1). Claude Code reads the docs a phase names in its "Read first" line before writing code; people read them in the order below.

## Reading order

| Doc | Answers |
| --- | --- |
| [01 Vision and goals](01-vision-and-goals.md) | What Mathesis is for, measurable goals, non-goals, design principles, users |
| [02 Architecture](02-architecture.md) | Layers, packages, data flow, cross-cutting concerns, architecture decisions (ADRs) |
| [03 Namespaces and packages](03-namespaces-and-packages.md) | Every namespace and what lives in it, naming rules, public API style |
| [04 Type system](04-type-system.md) | Number tower, generic-math constraints, sorts, assumptions, sets, representations, derivations |
| [05 Syntax trees and notation](05-syntax-trees-and-notation.md) | Node kinds, canonical levels, operator registry and catalog, constants, binders, input notation, printing |
| [06 Knowledge catalog](06-knowledge-catalog.md) | Entry kinds, IDs, the `.mlaw` format, curriculum levels, rule derivation, verification harness |
| [07 Engines](07-engines.md) | Matching, rewriting, simplification, assumptions, zero testing, polynomial, calculus and solving engines, explanations, compilation |
| [08 Features and abilities](08-features-and-abilities.md) | The public API surface by domain, with milestones |
| [09 Verification](09-verification.md) | How correctness is established: property, catalog, metamorphic, corpus and runtime checks |
| [10 Roadmap](10-roadmap.md) | Eight milestones from Foundation to Ecosystem, with exit criteria |

## Domain docs

Each lists the domain's scope, namespaces, types, operators, and its definitions, laws, theorems, formulas, patterns and methods as catalog entries with IDs, followed by the domain's abilities.

| Doc | Prefix | Completed in milestone |
| --- | --- | --- |
| [D1 Algebra](domains/d1-algebra.md) | `alg` | 2 |
| [D2 Trigonometry](domains/d2-trigonometry.md) | `trig` | 2 |
| [D3 Proofs and Logic](domains/d3-proofs-and-logic.md) | `logic` | 3 |
| [D4 Pre-Calculus](domains/d4-precalculus.md) | `pre` | 2 |
| [D5 Calculus](domains/d5-calculus.md) | `calc` | 4 |
| [D6 Differential Equations](domains/d6-differential-equations.md) | `ode` | 4 |
| [D7 Linear Algebra](domains/d7-linear-algebra.md) | `linalg` | 5 |
| [D8 Finite Mathematics](domains/d8-finite-mathematics.md) | `fin` | 6 |
| [D9 Numerical Analysis](domains/d9-numerical-analysis.md) | `num` | 7 |

## Conventions used in these docs

- **Notation.** Formulas in tables use Mathesis's linear input notation (`05-syntax-trees-and-notation.md`), so they can be pasted into `.mlaw` files: `a^m * a^n = a^(m + n)`, `diff(sin(x), x) = cos(x)`, `integrate(f, x, a, b)`, `sum(f, k, 1, n)`, `limit(f, x, a)`. Unicode symbols (π, √, ≤, ≠, ∈, ℤ, ℝ) are accepted by the parser.
- **Conditions** are for real mode unless marked ℂ. "Both sides defined" is always implied.
- **IDs** in the first column of a domain table are catalog IDs; `eng/check-catalog-coverage.cs` measures how many exist in the catalog.
- **Milestones** are numbered as in `10-roadmap.md`.
- **Name.** Mathesis is the chosen name for namespaces and package IDs.
