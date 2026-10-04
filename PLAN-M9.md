# Plan M9: Input validation attributes (`Mathesis.Validation`)

Claude Code adds one package, `Mathesis.Validation`, on .NET 10 LTS with no external dependency: seven `System.ComponentModel.DataAnnotations` validation attributes that check mathematical input (numbers, expressions, equations, polynomials, matrices), in six phases that each end with a green build. The attributes are plain `ValidationAttribute` subclasses, so any UI stack that already consumes DataAnnotations (Blazor, MAUI, WPF, WinUI 3, Windows Forms) gets them for free; this plan contains no platform-specific code.

## How to run it

1. Phases 0–2 can start now. Phase 3 needs the open prerequisite below (parsing `1e-100000` takes about 0.6 s today); do not start it until that Symbolics fix is merged.
2. Open Claude Code in the repo and send: `Read PLAN-M9.md, CLAUDE.md and docs/design/00-index.md. Ask me the "Decide first" questions, then do Phase 0 only and stop with a summary.`
3. Then one phase per session: `Do Phase N of PLAN-M9.md. Read the docs its "Read first" line names before writing code. Stop when its exit checks pass and summarize what changed.`

## Prerequisites

Two Symbolics bugs found while planning, fixed outside this plan:

- **Done.** `Expr.Parse("1e999999999")` used to return the number 0. Merged in PR #5: an exponent beyond `BigRational.MaxExponentMagnitude` (100,000) is now a `ParseError` spanning the literal. The LaTeX parser was never affected (it has no exponent literal). Phase 3 keeps the input as a regression check.
- **Open, blocks Phase 3.** In a Release build `Parser.Parse("1e-100000")` takes about 620 ms and `x + 1e100000 + 1e-100000` about 600 ms, against 8 ms for `BigRational.TryParse` on the same text. The cause is the display-digit loop in `Lexer.ReadNumber`, which runs 400 roundings of a 332,000-bit denominator. Without the fix, one 9-character input stalls `[MathExpression]` for most of a second per call and the Phase 3 timing checks cannot pass. The fix belongs to Symbolics (its own session, failing test first). `RationalNumber`, `ExactRange` and `NonZero` call `BigRational.TryParse` directly and are not affected.

## Decide first

Each has a recommended default; answering "defaults" accepts all of them.

- **Name.** Package and root namespace `Mathesis.Validation` (recommended). The alternative `Mathesis.DataAnnotations` is rejected because the segment would shadow `System.ComponentModel.DataAnnotations` inside `Mathesis.*` namespaces, and CLAUDE.md says to avoid BCL name collisions.
- **Culture.** Input notation is culture-invariant: `.` is the decimal point and `,` separates arguments, as the parser already behaves. `0,5` is rejected with a suggestion to use `.`. Culture affects only how messages format their placeholders (the BCL default). `docs/design/05-syntax-trees-and-notation.md` is silent on this; Phase 0 records it there.
- **`double` and `float` values.** Compared by their shortest round-trip decimal text, so `0.1` means 1/10 (what the user typed and saw), not its binary expansion. Exactness belongs to text, `decimal` and `BigRational` properties.
- **No `Budget` parameter.** Attribute arguments must be constants, and an attribute runs no engine: it parses and inspects, bounded by `MaxLength` (default 1,000 characters) and the parser's nesting limit of 150. Goal G9 is met by a fuzz exit check, not a parameter. If you want a `Budget` anyway it would have to come from `ValidationContext.GetService(typeof(Budget))`.
- **Messages.** Default messages are neutral English in an embedded `Messages.resx`. Developers localize with the standard `ErrorMessage` / `ErrorMessageResourceType`. Parser error text is English and embedded as-is until Milestone 8 localizes it.

Constraints stated, not asked: `net10.0` only; only the .NET 10 DataAnnotations API (the async types `AsyncValidationAttribute`, `IAsyncValidatableObject` and `Validator.*Async` are .NET 11, although the net-10.0 namespace page on Microsoft Learn lists them); no UI-framework references of any kind.

## Scope

v1 delivers:

- `MathValidationAttribute` (shared base), `MathValidationResult : ValidationResult` (stable `Code`, source `Span`, `Suggestion`), `MathValidationCode`.
- Seven attributes: `RationalNumber`, `ExactRange`, `NonZero`, `MathExpression`, `MathEquation`, `PolynomialExpression`, `MathMatrix` (contract below).
- Configuration validation: a misconfigured attribute fails loudly and early (`InvalidOperationException` on first use, `GetConfigurationError()` for startup checks), never silently.
- Embedded `Messages.resx`, XML docs, tests, a NativeAOT smoke check, README examples, `dotnet pack`.

v1 leaves out:

- UI-framework adapters, samples or TFMs (`-windows`, MAUI, Razor). Each framework consumes DataAnnotations itself. Note for later: MAUI's Android and iOS builds pull `Xamarin.*` packages, which `eng/policy-check.cs` rejects, so a MAUI project under `src/` would need a policy decision first.
- Async validation (.NET 11), cross-property attributes (resolving a property by name needs reflection and `RequiresUnreferencedCode`; use `IValidatableObject`, ADR-17), a Roslyn analyzer for misconfiguration, `Complex<T>` input (its text grammar is `(re, im)`), MathJSON input, assumption-aware validation through `MathContext`, localized parser messages (Milestone 8).

## Design docs

`docs/design/00-index.md` lists them. Phases cite the sections they implement; when code and docs disagree, stop and ask rather than silently diverging. Planning found three existing gaps, fixed in Phase 0: `02-architecture.md` says "eight packages" but its table lists nine; `05` is silent on culture; `01` lists UI as a non-goal (validation metadata is data, not UI, and the doc should say so).

## Layout changes

```text
src/Mathesis.Validation/                 # references Symbolics only; no PackageReference
  MathValidationAttribute.cs  MathValidationResult.cs  MathValidationCode.cs
  RationalNumberAttribute.cs  ExactRangeAttribute.cs  NonZeroAttribute.cs
  MathExpressionAttribute.cs  MathEquationAttribute.cs
  PolynomialExpressionAttribute.cs  MathMatrixAttribute.cs
  Messages.resx  Messages.cs
tests/Mathesis.Validation.Tests/         # MSTest; references Validation and Mathesis.Testing
samples/AotSmoke/                        # gains a Validation section and a project reference
```

`Mathesis.slnx` gets both projects. CI needs no change (no workloads, no Windows-only TFMs). The `Mathesis` meta-package does not reference `Mathesis.Validation` (optional, like `Mathesis.Extensions`).

## Phases

### Phase 0: Docs and skeleton

- **Read first:** `00-index.md`, `01-vision-and-goals.md` (Non-goals, G7–G9), `02-architecture.md` (Layers, Package dependencies, ADRs), `03-namespaces-and-packages.md`, `10-roadmap.md`.
- Doc edits, keeping each doc's formatting and changing only what the decision touches:

| Doc | Section | Change |
| --- | --- | --- |
| `02` | Layers sentence, layer diagram | Count the packages in the table and fix "eight packages in five layers"; add `Mathesis.Validation` beside `Mathesis.Extensions` in Layer 4 |
| `02` | Package dependencies | Row `Mathesis.Validation`: references Symbolics; external packages none (the in-box `System.ComponentModel.Annotations` is part of the shared framework) |
| `02` | Architecture decisions | ADR-15 to ADR-18 below, same columns |
| `03` | Namespace tree, Naming conventions | New `Mathesis.Validation` package table (one namespace, `Mathesis.Validation`); naming rule: attributes end in `Attribute`, none may reuse a `System.ComponentModel.DataAnnotations` name (`Range`, `Required`, `Compare` …) |
| `05` | Conventions the parser applies | Bullet **Culture**: input is invariant; `0,5` and digit grouping are not numbers; U+2212 is accepted as minus (the lexer already maps it) |
| `01` | Non-goals, Rendering/plotting/UI | One sentence: validation attributes carry rules and messages as data; drawing error states stays in the app |
| `08` | Integration points | Row: input validation for forms, `Mathesis.Validation`, milestone 9 |
| `10`, `00` | Roadmap table, "eight milestones" wording | Row 9 "Input validation", packages touched Validation (new), depends on 1, can run in parallel with 2–8; fix the milestone count in both docs |

| ID | Decision | Chosen | Main alternative | Why |
| --- | --- | --- | --- | --- |
| ADR-15 | Validation package | `Mathesis.Validation`: layer 4, optional, references Symbolics only, `net10.0` only, no UI-framework reference | Put it in `Mathesis.Extensions`, or one package per UI framework | The attributes are plain BCL types every UI stack already consumes; no workloads or `-windows` TFMs; avoids the `Xamarin.*` transitive packages the policy check rejects |
| ADR-16 | One verdict, two entry points | Each attribute computes its verdict once and exposes it through `IsValid(object?)` and `IsValid(object?, ValidationContext)`; failures are `MathValidationResult` with code, span and suggestion | Override only the context overload | Verified on .NET 10.0.401: a context-only attribute throws `NullReferenceException` from `IsValid(value)` and `Validate(value, name)`; a `ValidationResult` subclass survives `Validator.TryValidateObject` and `TryValidateProperty` |
| ADR-17 | Cross-property rules | `IValidatableObject` (and each attribute's typed `Check`) | A `CompareAttribute`-style attribute naming another property | Resolving a property by name needs reflection (`RequiresUnreferencedCode`), which G8 forbids |
| ADR-18 | Sync only, .NET 10 surface | Synchronous validation; never use `AsyncValidationAttribute`, `IAsyncValidatableObject` or `Validator.*Async` | Adopt the async types | They ship in .NET 11 only; validation here is parse-and-inspect and does no I/O |

- Projects: `src/Mathesis.Validation/Mathesis.Validation.csproj` (description, one `ProjectReference` to Symbolics) and `tests/Mathesis.Validation.Tests/` with a placeholder test; both in `Mathesis.slnx`; README package table row.
- **Done when:** a Release build has zero warnings, the placeholder test passes, `dotnet run eng/policy-check.cs` passes with no new package ID, `dotnet pack` produces seven `.nupkg` files, and a search of `docs/design/` finds no stale package or milestone count and every ADR-15 to ADR-18 reference resolves.

### Phase 1: Foundations

- **Read first:** `05-syntax-trees-and-notation.md` (Conventions the parser applies, Phase 4 implementation notes), `09-verification.md` (Property tests, Fuzzing), `02-architecture.md` (Results, AOT and trimming), and the contract below.
- `MathValidationAttribute` (abstract), `MathValidationResult` (sealed), `MathValidationCode`, `Messages.resx` with one default template per code read through `ResourceManager`, and `Messages.cs`.
- A test-only attribute derived from the base in the test project exercises every base rule before any real attribute exists.
- **Done when:** the contract tests pass, and 10,000 seeded inputs give identical verdicts through both `IsValid` overloads. Specifically:
  - standalone `IsValid(v)`, `Validate(v, name)` and `GetValidationResult` never throw `NullReferenceException`;
  - `Validator.TryValidateObject` and `TryValidateProperty` return `MathValidationResult` instances whose `MemberNames` equal the member, and a `[Required]` failure on the same member suppresses the other attributes' results;
  - null, empty and whitespace-only values are valid, and text longer than `MaxLength` returns `TooLong` without calling the verdict core (counted);
  - an invalid configuration throws `InvalidOperationException` on first use even for a null value, and `GetConfigurationError()` returns the same text without throwing;
  - a developer-set `ErrorMessage` or `ErrorMessageResourceType` replaces every default message; setting `ErrorMessage` and `ErrorMessageResourceName` together still throws the BCL's `InvalidOperationException`; a template naming an undefined placeholder throws `InvalidOperationException` that names the attribute;
  - every `MathValidationCode` has a template in the resx and the resx has no unused key (enum-driven test);
  - 8 threads × 5,000 validations on one shared instance return identical results.

### Phase 2: Numeric attributes

- **Read first:** `04-type-system.md` (Number tower), `05-syntax-trees-and-notation.md` (decimals are exact), `domains/d1-algebra.md` (Fractions, ratios and percents; Conventions).
- `RationalNumber`, `ExactRange` and `NonZero` on text and on typed values, via `BigRational.TryParse` only (no `Expr`).
- **Done when:**
  - 10,000 seeded `BigRational` values, each formatted as fraction, terminating decimal, repeating decimal and exponent form, are accepted by `RationalNumber`, and `ExactRange` agrees with the exact `BigRational` comparison on 200 seeded bound pairs in all four inclusive/exclusive combinations with 0 mismatches;
  - text and typed values (`BigRational`, `decimal`, `long`) of the same number get the same verdict on all 10,000;
  - a 20-case `double` table passes (`0.1` in `["0", "1/10"]` is valid, `0.1 + 0.2` against maximum `3/10` is invalid, NaN and ±∞ are invalid, `-0.0` fails `NonZero`);
  - a 40-case rejection table returns the expected code, including `""` (valid), `abc`, `1/0`, `--1`, `1e999999999` (`NotANumber`, never zero), `0,5` and `1,000` (suggestion names `.`), and U+2212 `−3` (accepted); `1e100000` and `1e-100000` are accepted and each call takes under 50 ms (measured about 8 ms);
  - verdicts are identical under `en-US`, `de-DE`, `fr-FR`, `ar-SA`, `tr-TR` and `ja-JP`, and only message formatting varies;
  - every configuration error row (unparsable bound, minimum above maximum, empty exclusive range, no bound, `MaxLength` outside 1–100,000) throws as specified; a 10 MB string returns `TooLong` in under 5 ms.

### Phase 3: Expression attributes

- **Read first:** `05-syntax-trees-and-notation.md` (all of it), `04-type-system.md` (The syntax tree, Sorts), `07-engines.md` (Evaluation and compilation, for what validation must not run).
- `MathExpression` (text, LaTeX, or an `Expr` value) and `MathEquation` (a sealed subclass fixing `Shape = Equation`): parse, then optional sort check, then shape, variables, required variables, disallowed operator families. The first failing check is reported; the attribute never simplifies, evaluates or solves. Parser errors keep the parser's span and suggestion unchanged.
- **Done when:**
  - a table of at least 60 cases covers every code the two attributes can return, with spans and suggestions taken from `Parser.Parse` for the same text;
  - 2,000 seeded `ExprGen` expressions are accepted as `ToString()` text, and as `ToLatex()` text with `Format = Latex` where the existing LaTeX round-trip tests accept them; with `Variables` set to exactly an expression's free symbols it is accepted, and with one symbol removed it is rejected with `UnknownVariable` naming exactly that symbol; `RequiredVariables` behaves symmetrically;
  - a 30-case shape table passes (`x^2 = 4` is an equation, `x < 3` and `x != 3` are inequalities, `[0, 1)` an interval, `x + 1` an expression), and an `Expr`-typed property gets the same semantic verdicts without parsing;
  - parser warnings: `1/2x` is accepted by default and returns `Ambiguous` only with `WarningsAreErrors`; `sqr(x)` is accepted by default (a user function call) and returns `UnknownFunction` only with `WarningsAreErrors`;
  - unknown multi-letter names behave as the parser reads them, pinned by test rows so nobody "fixes" them silently: `foo(x)` is the product f·∞·x (the parser reads `oo` as ∞), accepted by default and, with `Variables = ["x"]`, `UnknownVariable` naming `f`; with `SingleLetterVariables = false` it is a call to the function symbol `foo`, accepted even with `Variables = ["x"]`; `DisallowedFunction` is raised only for an operator of a family in `DisallowedFamilies` (for example `sin(x)` with `Trig`);
  - 20,000 seeded inputs (mutations of `tests/corpus/expressions.txt` plus random token streams, up to 1,200 characters) finish with no exception and no single call above 250 ms; adversarial inputs behave as probed: 400 nested parentheses give `Syntax` at column 151, and `2^999999999*x`, `(x+1)^999999999`, `9^9^9`, `x^x^x^x^x^x`, `1e100000`, `1e-100000` and `x + 1e100000 + 1e-100000` are valid and return in under 50 ms because nothing is evaluated (the last two need the open prerequisite); `1e999999999` gives `Syntax`.

### Phase 4: Polynomial and matrix attributes

- **Read first:** `domains/d1-algebra.md` (Polynomial division and roots), `04-type-system.md` (Representations), `domains/d7-linear-algebra.md` (Matrix algebra), `05-syntax-trees-and-notation.md` (Phase 4 notes on brackets).
- `PolynomialExpression("x", MaxDegree = n)`: parse, canonicalize, convert with `PolynomialConversion.TryToPolynomial`, check the degree. When conversion fails the attribute names the offending construct (`sin`, `1/x`, `x^(1/2)`, `x^-1`, `x^65`, another symbol); an exponent above `PolynomialConversion.MaxExponent` (64) on an x-dependent base is `DegreeTooHigh`, not `NotAPolynomial`.
- `MathMatrix` (`Rows`, `Columns`, `Square`, `NumericEntries`, `MaxDimension`, `Format`): parse and inspect the `Matrix` node. Numeric entries are checked structurally on the raw tree, never evaluated. Verified raw shapes: an integer or decimal literal is `Number` (`+1` is `1`, `2e3` is 2000), a negative one is `neg(Number)`, a fraction is `div(a, b)` and `-1/2` is `div(neg(1), 2)`; an entry is numeric when it is `Number`, `neg(Number)` or `div(a, b)` with `a` and `b` each `Number` or `neg(Number)`. Anything else, including a chained division such as `1/2/3` and any arithmetic such as `2^3`, is `NonNumericEntry`. `[1, 2]` parses as an interval, so the diagnostic suggests `[[1],[2]]` for a two-entry column.
- **Done when:**
  - a 30-case polynomial table passes (`x^2 - 5x + 6`, `x/2 + 1`, `(x+1)^2`, `7` and `0` are valid; `1/x`, `sqrt(x)`, `x^(1/2)`, `x^-1`, `sin(x)` give `NotAPolynomial` naming the construct; `x*y` gives `UnknownVariable`; `(x+1)^65` and `x^100` give `DegreeTooHigh`; `x^2 = 4` gives `WrongShape`);
  - 1,000 seeded polynomials printed with `PolynomialConversion.FromPolynomial` are accepted at their degree and rejected with `DegreeTooHigh` at `MaxDegree = degree − 1`;
  - a 40-case matrix table passes (2×2 and 3×1 valid; `[1, 2]` gives `NotAMatrix` with the suggestion; a 3×3 request on 2×3 reports both sizes; `[[1, x], [2, 3]]` gives `NonNumericEntry` at row 1, column 2 and is valid with `NumericEntries = false`; `[[2^3, 1]]` and `[[1/2/3, 1]]` give `NonNumericEntry`; `[[-1/2, 1/-2], [+1, 0.25]]` is valid; ragged rows keep the parser's message; 11×11 gives `DimensionTooLarge`; `\begin{pmatrix}1&2\\3&4\end{pmatrix}` with `Format = Latex` is valid; `\begin{vmatrix}…` gives `NotAMatrix`);
  - 500 seeded `DenseMatrix<BigRational>` values printed as matrix literals are accepted with the right dimensions;
  - the Phase 3 fuzz run repeated over both attributes meets the same bounds.

### Phase 5: Integration, AOT, packaging

- **Read first:** `09-verification.md` (Tooling and gates), `08-features-and-abilities.md` (Integration points), the Phase 0 ADRs.
- Model-level tests through the BCL entry points (`Validator.TryValidateObject` with `validateAllProperties: true`, `TryValidateProperty`, `TryValidateValue`) on three form models (calculator, polynomial roots, matrix), with `[Display]`, `[Required]` and an `IValidatableObject` cross-property rule that runs only when property validation passes.
- `samples/AotSmoke`: reference `Mathesis.Validation` and add checks for all seven attributes, built with the trim-safe `new ValidationContext(instance, displayName, serviceProvider: null, items: null)` and `GetValidationResult`; never `Validator.*` or `new ValidationContext(instance)` (both are `RequiresUnreferencedCode`). One check prints a message read from the embedded resx.
- README section "Validating input" with five examples executed by a test, following `tests/Mathesis.Tests/ReadmeTests.cs`; package row; RELEASE-NOTES "Unreleased" entry; XML docs on every public member.
- **Done when:** the model tests assert the exact message, member names and result types per property; the AOT publish of `samples/AotSmoke` has zero trim or AOT warnings and passes at least 8 new checks with `InvariantGlobalization` still on; the README examples match their tests; `dotnet pack` builds seven `.nupkg` files; build, tests, `dotnet run eng/policy-check.cs`, `eng/mlaw-lint.cs`, `eng/gen-knowledge.cs -- --check` and `eng/check-catalog-coverage.cs` are green with no catalog change.

## Attribute contract

Applies to every attribute; Phase 1 implements it and each later phase tests its attribute against it.

- **Usage.** `AttributeTargets.Property | Field | Parameter`, `AllowMultiple = false`, as the BCL attributes. Options are `init` properties (collection expressions work for `string[]`, verified on .NET 10.0.401), immutable after first use, so shared attribute instances are thread-safe. Constructor arguments are also exposed as read-only properties (`Minimum`, `Maximum`, `Variable`), as the BCL `RangeAttribute` does. The repo's `latest-recommended` analyzers raise nothing for these shapes (an unsealed attribute class, `string[]` properties, constructor arguments without properties), checked in a scratch build.
- **Presence.** Null, empty and whitespace-only strings are valid; presence is `[Required]`'s job.
- **Supported values.** Other types are API misuse and throw `InvalidOperationException` naming the attribute and the type:

| Attribute | Text | Typed values |
| --- | --- | --- |
| `RationalNumber` (`IntegerOnly`, `AllowFractions`, `AllowDecimals`) | yes | `BigRational`, `BigInteger`, `int`, `long`, `decimal`, finite `double` and `float` |
| `ExactRange(string? minimum, string? maximum)` (`MinimumIsExclusive`, `MaximumIsExclusive`; null or empty is unbounded) | yes | same as above |
| `NonZero` | yes | same as above |
| `MathExpression` (`Format`, `Shape`, `Variables`, `RequiredVariables`, `DisallowedFamilies`, `WarningsAreErrors`, `CheckSorts`, `SingleLetterVariables`, `LogMeansNatural`) | text or LaTeX | `Expr` (semantic checks only) |
| `MathEquation` | text or LaTeX | `Expr` |
| `PolynomialExpression(string variable)` (`MaxDegree` ≤ 64, default 20, `Format`) | text or LaTeX | `Expr` |
| `MathMatrix` (`Rows`, `Columns`, `Square`, `NumericEntries` default true, `MaxDimension` default 10, `Format`) | text or LaTeX | `Expr` |

- **Options.** `Variables` constrains non-function symbols only; constants (`e`, `pi`, `I`) and bound symbols never count, and function symbols such as `f` in `f(x)` are not constrained. `Shape` values: `Any`; `Expression` (not a relation or logical statement); `Equation` (`=`); `Inequality` (`<`, `<=`, `>`, `>=`, `!=`); `Interval`. `DisallowedFamilies` is the only function restriction (`DisallowedFunction`); unknown multi-letter names are not functions to the attribute (see the Phase 3 checks). `LogMeansNatural` and `SingleLetterVariables` map to the existing `ParserOptions` fields with their existing defaults, so bare `log` stays base 10. `MaxLength` defaults to 1,000 on every text attribute.
- **Verdict.** Computed once per attribute (`MathDiagnostic` internal to the base) and exposed through both `IsValid` overloads and a typed `Check(object? value, string displayName = "Value", string? memberName = null)` that returns `MathValidationResult?` (null when valid) without a `ValidationContext`.
- **Results.** `MathValidationResult` carries `Code`, `Span` (`TextSpan?`), `Suggestion`; `MemberNames` is `[context.MemberName]` when non-null (without it, form frameworks cannot attach the error to a field); `ErrorMessage` is never empty.
- **Messages.** Placeholder `{0}` is the display name; each attribute documents any extra ones (`ExactRange`: `{1}` the range in interval notation such as `[1/3, 5/2)`, `{2}` minimum text, `{3}` maximum text, as written). A developer-set message replaces all defaults. Implementation hint: pass a sentinel through the `ValidationAttribute(Func<string>)` constructor to tell "no custom message" from a set one, since the base class does not expose that.
- **Failure.** Bad input never throws. Bad configuration and unsupported types throw `InvalidOperationException` before the value is examined. An undefined placeholder in a developer template does the same.
- **Trim and AOT.** No reflection, no `Validator`, no `ValidationContext(object)` constructor in `src/`; only `DisplayName`, `MemberName` and `GetService` are read from the context supplied by the caller.
- **Illustrative use** (member names to be checked against the real API):

```csharp
public sealed class RootFinderForm
{
    [Required, PolynomialExpression("x", MaxDegree = 6)]
    [Display(Name = "Polynomial")]
    public string? Polynomial { get; set; }

    [ExactRange("-100", "100", MinimumIsExclusive = true)]
    public string? LowerBound { get; set; }

    [MathMatrix(Rows = 3, Columns = 3, Square = true)]
    public string? Coefficients { get; set; }
}
```

## Codes

| Code | Raised by | Extra placeholders |
| --- | --- | --- |
| `TooLong` | all text attributes | `{1}` the limit |
| `NotANumber`, `NotAnInteger`, `FractionNotAllowed`, `DecimalNotAllowed` | `RationalNumber`, `ExactRange`, `NonZero` | none |
| `OutOfRange` | `ExactRange` | `{1}`–`{3}` as above |
| `Zero` | `NonZero` | none |
| `Syntax` | expression, polynomial, matrix attributes | `{1}` the parser text |
| `Ambiguous`, `UnknownFunction`, `IllSorted` | `MathExpression`, `MathEquation` (the first two only with `WarningsAreErrors`, for the parser warnings of the same name; `IllSorted` with `CheckSorts`) | `{1}` the warning or sort-check text |
| `WrongShape` | `MathExpression`, `MathEquation`, `PolynomialExpression` | `{1}` expected, `{2}` found |
| `UnknownVariable`, `MissingVariable`, `DisallowedFunction` | `MathExpression`, `MathEquation` | `{1}` names |
| `NotAPolynomial`, `DegreeTooHigh` | `PolynomialExpression` | `{1}` construct or degree, `{2}` maximum |
| `NotAMatrix`, `WrongDimensions`, `NotSquare`, `NonNumericEntry`, `DimensionTooLarge` | `MathMatrix` | `{1}`, `{2}` sizes or the entry position |

## Risks

| Risk | Mitigation |
| --- | --- |
| Out-of-range exponent literal parses as 0 | Fixed in PR #5; Phase 3 keeps `1e999999999` as a regression check |
| `1e-100000` takes about 0.6 s to parse | Open prerequisite (Symbolics display-digit loop); Phase 3 timing checks include it and cannot pass without the fix |
| Unknown multi-letter names read as products (`foo(x)` is f·∞·x) | Documented behavior pinned by Phase 3 test rows; `DisallowedFunction` means a disallowed operator family only; `SingleLetterVariables = false` for apps that want words as names |
| Hostile or accidental huge input | `MaxLength` before parsing, parser depth limit 150, no evaluation, fuzz exit checks with numbers |
| A `ValidationResult` subclass is not preserved by some caller | Verified through `Validator` on .NET 10.0.401 in Phase 1; callers that rebuild results still get the message and member name |
| Trim or AOT warning from a BCL entry point | Phase 5 smoke app uses only trim-safe members; `IlcTreatWarningsAsErrors` is already on |
| Parser messages are English | Documented; localization stays in Milestone 8 |
