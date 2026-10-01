---
name: mathesis-catalog
description: Author, review and fix Mathesis knowledge-catalog entries — .mlaw files and the ID tables in the d1–d9 domain docs. Use whenever the user wants to add, write, check or correct a law, identity, theorem, formula, definition, pattern, method or convention for Mathesis, mentions .mlaw, catalog IDs (alg.*, trig.*, calc.*, linalg.* …), conditions/provisos for an identity, curriculum levels, or catalog coverage — even if they just say "add the double-angle identities" or "is this condition right?".
---

# Mathesis catalog entries

The catalog is where Mathesis's mathematics lives (design doc `06-knowledge-catalog.md`). Engines turn entries into rewrite rules, solver strategies and explanations, and CI verifies every entry numerically. A wrong or under-conditioned entry becomes a wrong answer everywhere downstream, so accuracy of statements and conditions matters more than anything else here.

Read `references/mlaw-format.md` before writing your first entry in a conversation. It holds the grammar, every field, the ID prefixes and the curriculum levels. When the project docs are available, also `project_read` the relevant domain doc (`d1-algebra.md` … `d9-numerical-analysis.md`) so new entries reuse its existing IDs, names and conditions instead of inventing parallel ones.

## Workflow

1. **Find the ID first.** Look for the row in the domain doc table. If it exists, use that ID, name, statement and conditions as the starting point. If it does not, propose a new row for the doc as well as the entry, since `eng/check-catalog-coverage.cs` counts doc IDs against the catalog and an entry without a doc row is invisible to coverage.
2. **Pick the kind** by what engines do with it, not by what textbooks call it: an equation usable as a rewrite is a `law`; hypotheses ⇒ conclusion is a `theorem`; a relation solved for any variable is a `formula`; a recognizable shape is a `pattern`; a named procedure is a `method`; a choice among valid options is a `convention`.
3. **Write exact conditions.** This is the step that most needs care:
   - `where:` is real mode (the default). Add `complex:` only when complex-mode conditions differ, using principal branches.
   - Ask of every condition: does the law fail without it? If a condition exists only to exclude one case, add a `# Counterexample:` comment showing that case, e.g. `# ((-1)^2)^(1/2) = 1 but (-1)^1 = -1`.
   - Check the branch-sensitive families explicitly: powers with non-integer exponents, even roots, logs of products/quotients/powers, inverse trig composed with trig, `sqrt(a*b)`, `abs` of complex arguments. These are where textbooks routinely state laws too broadly.
   - Never weaken a condition to make verification pass. If the harness fails, the statement or the condition is wrong; say which.
   - Omitted `where:` means "for all values of the declared sorts where both sides are defined" — only omit it when that is literally true.
4. **Declare every free symbol** in `vars:` with a sort (`real`, `integer`, `complex`, `function(R -> R)`, `matrix(n, n)` …). Undeclared symbols fail lint.
5. **Set the rest:** `orient` (`ltr` for laws that simplify, `both` with `tags: a | b` for identities used in two directions, `none` for statements engines must not rewrite with), `level`, `course`, `tags` for rule-set membership, an `explain` template with `{var}` placeholders, `refs` (DLMF numbers are ideal), `see`, and `implemented-by` for methods and solvable formulas.
6. **Sanity-check numerically** before presenting any law or formula, because a plausible-looking identity with a missing condition is the most common catalog bug:
   ```bash
   python3 scripts/check_identity.py --vars "a: real, m: real, n: real" \
     --statement "(a^m)^n = a^(m*n)" --where "a >= 0 or n in Z"
   ```
   Add `--complex` for the `complex:` conditions and `--sample "n in 1..12"` when a sort needs a range. It samples inside the conditions (any failure is a counterexample: fix the statement or the condition) and outside them (if it never fails there, the conditions may be stronger than necessary; say so, but a stricter condition is acceptable when it keeps the assumption engine able to decide it). It follows the Mathesis conventions (0^0 = 1, real odd roots) and skips points where either side is undefined. It handles equations and inequalities only; for `<=>`, theorems and methods, check each step's equation separately. Report what you ran and what it found.
7. **Lint** with `python3 scripts/mlaw_lint.py <file.mlaw>` when you produce a whole file, and fix errors before presenting.

## Output

- For new entries: the `.mlaw` text in a code block, the matching domain-doc table row(s) in the doc's exact column layout, and a one-line note per entry on anything non-obvious (why a condition is there, why an orientation was chosen).
- For reviews: a table of `ID | issue | fix`, most serious first (wrong statement > missing condition > over-strict condition > metadata). Then the corrected text for changed entries only.
- Keep the field order used in `06-knowledge-catalog.md` examples. When editing an existing `.mlaw` file or doc table, keep its formatting and change only the affected entries.

## Rules of thumb

- IDs are `<domain>.<topic>.<name>` lowercase kebab-case and permanent once released; a rename keeps the old ID in `aliases`.
- One mathematical fact per entry. Two directions of one identity are one entry with `orient: both`; two genuinely different statements (e.g. `cos(2x)` in three forms) are separate entries.
- Respect curriculum levels: explanations at level L never cite entries above L (goal G10), so don't put a `Calculus1` method behind an `Algebra2` pattern.
- Use the input notation from `05-syntax-trees-and-notation.md`: `I` is the imaginary unit, bare `log` is base 10, `ln` is natural, `root(a, n)` is the real n-th root, decimals are exact.
- Conventions already decided (`conv.*`): 0^0 = 1, bare log base 10, real odd roots, exact decimals, order of operations; display rounding half away from zero is still open.
- Write explanations and refs in your own words; don't copy textbook prose.
