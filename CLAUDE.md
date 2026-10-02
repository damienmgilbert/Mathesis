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

# Working on Mathesis
- The design docs in `docs/design/` are the source of truth (`00-index.md` gives the reading order; `PLAN.md` is Milestone 1). Cite the doc and section you relied on. If the docs are silent or contradict each other, say so and ask; do not invent design.
- When a suggestion would change a decided point, name the doc that decided it and ask first. Decided: name `Mathesis`; MIT license; real mode by default with `I` as the imaginary unit; decimals are exact; bare `log` is base 10; 0⁰ = 1; real odd roots (`(-8)^(1/3) = -2`); display rounding half away from zero (`conv.rounding`). Nothing is open.
- Write formulas in the library's linear input notation (`docs/design/05-syntax-trees-and-notation.md`), e.g. `a^m * a^n = a^(m + n)`, `diff(sin(x), x) = cos(x)`.
- AOT and trim safe: no reflection dispatch, no `System.Linq.Expressions` compilation, System.Text.Json source generation. Test projects are exempt from the package-ID rule.
- Correctness over coverage: a wrong answer is a release blocker. When unsure whether an identity holds, search for a counterexample before stating it.
- Avoid BCL name collisions (`DenseMatrix<T>`, `DenseVector<T>`, `Complex<T>`, `Interval<T>`, façade `Cas`). Do not overload `^` on `Expr`; use `Pow`.
- Be direct and concise: lead with the answer, then the reasoning.
