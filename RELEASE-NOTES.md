# Release notes

## 0.1.0 (Milestone 1)

The first release: six packages (`Mathesis`, `Mathesis.Core`, `Mathesis.Numerics`, `Mathesis.LinearAlgebra`, `Mathesis.Symbolics`, `Mathesis.Knowledge`), a catalog of 548 verified entries, and the `Cas` façade over the engines.

### What it does

- **Numbers and numerics:** exact rationals, complex, dual and interval arithmetic; root finding, quadrature, differentiation, interpolation, optimization, ODE solvers; polynomials over any field; exact row reduction, determinants, inverses and decompositions.
- **Expressions:** a typed syntax tree with a parser (linear text and LaTeX), text and LaTeX printers, a normalizer, sort checking, assumptions (`Ask` with sign, interval, linear and parity reasoning), natural domains, zero testing, evaluation and compilation to delegates.
- **Rewriting and simplification:** AC-aware pattern matching, rules derived only from catalog entries, strategies, a budgeted best-first `Simplify` with a replaceable complexity measure, named transforms (expand, factor, collect, together, apart, cancel, rationalize, complete the square, log and trig forms), replayable derivations and explanations in text, Markdown and LaTeX at three verbosity levels with a curriculum-level filter.
- **Calculus:** derivatives with steps (implicit, logarithmic, higher, partial), limits (direct substitution, algebra of limits, catalog standard limits, L'Hôpital gated by level, series), Taylor and Laurent series, integration (linearity, table, rational functions, substitution, parts, trigonometric integrals and substitutions, Weierstrass) with every antiderivative differentiated back, definite and improper integrals compared with quadrature.
- **Solving:** polynomial, rational, radical, absolute-value, exponential, logarithmic and trigonometric equations (image sets for the latter), inequalities by sign charts, linear systems by exact Gauss–Jordan and systems solvable by substitution; every candidate is substituted back and extraneous solutions are rejected with a step.
- **Catalog:** `Cas.Get`, `Cas.Find`, `Cas.ByDomain`; every step carries the entry it cites.

### Gates for this release

| Gate | Result |
| --- | --- |
| Build, `TreatWarningsAsErrors`, XML docs on every public API (`GenerateDocumentationFile`) | 0 warnings |
| Tests (`dotnet test --solution Mathesis.slnx`), including catalog verification of every law and formula | all pass |
| Package policy (`dotnet run eng/policy-check.cs`) | passed: `System.Numerics.Tensors` and the build-time `Microsoft.NET.ILLink.Tasks` only |
| Catalog lint and generated handles (`eng/mlaw-lint.cs`, `eng/gen-knowledge.cs -- --check`) | no problems, up to date |
| Catalog coverage (`eng/check-catalog-coverage.cs`) | the sections seeded in Milestone 1 are covered 516/516 (100%); full report below |
| `dotnet pack` | six `.nupkg` files in `artifacts/packages` |
| NativeAOT publish of `samples/AotSmoke` (`-r win-x64`) | zero ILC warnings (they are errors), the native link succeeds and the executable passes its six checks; see the NativeAOT note below |
| README | sixteen examples, each executed by a test against the output shown |

### Catalog coverage report

The full report is `docs/release/catalog-coverage-0.1.0.txt` (produced by `dotnet run eng/check-catalog-coverage.cs -- --all`). Summary:

| Domain document | All sections | Sections seeded in Milestone 1 |
| --- | --- | --- |
| d1 Algebra | 225/240 (93.8%) | 220/220 (100%) |
| d2 Trigonometry | 125/177 (70.6%) | 122/122 (100%) |
| d5 Calculus | 157/296 (53.0%) | 140/140 (100%) |
| d7 Linear algebra | 34/179 (19.0%) | 34/34 (100%) |
| d3, d4, d6, d8, d9 | 0% | not part of Milestone 1 |

Entries added during Milestone 1 that the domain documents do not list: `alg.log.ln-product`, `ln-quotient`, `ln-power`, `ln-exp`, `exp-ln`, `calc.series.series-arithmetic`, `trig.eqn.cot`.

### Known limits

- **Real mode only** for solving; complex roots of a polynomial are not reported.
- **Integration:** no Hermite reduction or Lazard–Rioboo–Trager (rational functions need a denominator that splits over ℚ, plus at most one irreducible quadratic factor), no Risch algorithm; substitutions whose inner expression is not a subexpression are not found.
- **Limits:** no Gruntz algorithm; exp-log asymptotics beyond L'Hôpital and series are not decided.
- **Solving:** no Cardano or Ferrari (numeric roots instead), no Lambert W, no trigonometric or parametric inequalities, polynomial systems only by substitution.
- **Simplification:** `sqrt(8)` stays as written; multivariate factoring, cancellation and partial fractions are not implemented.
- **Explanations:** solving steps describe equations and are not replayable; the substitution check is the verification.
- Several façade abilities of `docs/design/08-features-and-abilities.md` belong to later milestones (domain and range analysis, conics, sums, differential equations, finite mathematics, proofs).
- **Catalog verification:** every law and formula is verified numerically except `calc.int.substitution` (the indefinite form, whose right-hand side is an integral in `u = g(x)`); its definite form `calc.int.substitution-definite` is verified. The Maclaurin laws are checked as limits of partial sums where the series settles (not at the radius of convergence), and the endpoints of the `ln(1 + x)` and `arctan` series are checked with the alternating-series bound.
- **Naming:** the catalog functions are `Cas.Get`, `Cas.Find` and `Cas.ByDomain`. The design originally wrote them as `Knowledge.Find`, which cannot be a static class in the `Mathesis` namespace because `Mathesis.Knowledge` is a namespace; `docs/design/08-features-and-abilities.md` now records the `Cas.*` names.

### NativeAOT

`samples/AotSmoke` is configured for NativeAOT (`PublishAot`, ILC warnings as errors). Linking needs the platform's C++ toolchain (on Windows the "Desktop development with C++" workload of Visual Studio). Where it is installed, `dotnet publish samples/AotSmoke -c Release -r win-x64` produces the native executable.

Verified on Windows 11 with .NET SDK 10.0.401, ILCompiler 10.0.12, Visual Studio 18 Build Tools (MSVC 14.51.36231) and Windows SDK 10.0.26100.0: the publish has no ILC warnings (`IlcTreatWarningsAsErrors` is on), links a 4.7 MB self-contained `AotSmoke.exe` with no managed assembly beside it, and the executable exits 0 with all six checks (parse, simplify with steps, differentiate, integrate, solve, catalog lookup) passing. `IsAotCompatible` is on every package project, and a trimmed self-contained publish of the smoke app is also warning-free.

**Windows caveat.** ILCompiler finds the toolchain through `findvcvarsall.bat`, which calls Visual Studio's `vcvarsall.bat`; with Visual Studio 18 that script writes `'vswhere.exe' is not recognized` to the error stream when `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` is not on `PATH`, and MSBuild takes that text for part of the linker path. The publish then fails with `MSB3073` and a `link.exe` exit code of 123. Publishing from a Developer PowerShell, or adding that directory to `PATH` for the command, avoids it.
