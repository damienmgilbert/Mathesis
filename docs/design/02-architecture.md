# 02 Architecture

Mathesis is ten packages in five layers. Lower layers never reference higher ones, and the mathematics itself (laws, theorems, formulas) lives in a data catalog that the generic engines read at run time.

## Layers

```text
Layer 4  Mathesis (engines + façade)     Mathesis.Logic        Mathesis.Extensions (optional)    Mathesis.Validation (optional)
           Algebra, Trigonometry,          propositions, sets,    DI registration, options,         DataAnnotations attributes
           Functions, Calculus,            proof kernel,          ILogger tracing,                  for math input
           DifferentialEquations,          tactics, SAT           Microsoft.Extensions.AI tools
           Solving, Simplification,
           Explanation
              │                               │
Layer 3  Mathesis.Knowledge  ── catalog: definitions, axioms, laws, theorems,
              │                  formulas, patterns, methods (.mlaw files)
Layer 2  Mathesis.Symbolics  ── Expr tree, operators, sorts, parser, printers,
              │                  assumptions, sets, patterns, rewrite engine,
              │                  derivations, evaluation/compilation
Layer 1  Mathesis.Numerics       Mathesis.LinearAlgebra       Mathesis.Discrete
           floating point,         dense/sparse matrices,       combinatorics, probability,
           roots, quadrature,      decompositions, exact        statistics, finance, LP,
           ODEs, optimization,     and numeric solvers,         Markov chains, games,
           FFT, special functions  iterative methods            graphs, number theory
              │                        │                             │
Layer 0  Mathesis.Core  ── number tower (BigRational, Complex<T>, Dual<T>, Interval<T>,
                           ModInt<TM>, BigFloat), polynomials, Outcome, Truth, Budget
```

### Package dependencies

| Package | References | External packages |
| --- | --- | --- |
| `Mathesis.Core` | none | none |
| `Mathesis.Numerics` | Core | `System.Numerics.Tensors` |
| `Mathesis.LinearAlgebra` | Core | `System.Numerics.Tensors` |
| `Mathesis.Discrete` | Core, Numerics, LinearAlgebra | none |
| `Mathesis.Symbolics` | Core, Numerics, LinearAlgebra | none |
| `Mathesis.Knowledge` | Symbolics | none |
| `Mathesis.Logic` | Symbolics, Knowledge | none |
| `Mathesis` | all of the above except Extensions | none |
| `Mathesis.Extensions` | Mathesis | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.AI.Abstractions` |
| `Mathesis.Validation` | Symbolics | none (`System.ComponentModel.DataAnnotations` is part of the shared framework) |

`Mathesis.Symbolics` defines the rewrite *engine* but contains no mathematical laws beyond operator definitions and automatic simplification. Laws come from `Mathesis.Knowledge` through the `IKnowledgeBase` interface (defined in Symbolics, implemented in Knowledge). This breaks the cycle "the catalog needs the parser, the simplifier needs the catalog."

## Data flow

```text
 text / LaTeX / MathJSON / C# builder
            │  Parse (Raw level: exactly as written)
            ▼
        Expr (Raw) ──────────────► Explain mode keeps Raw so every step is visible
            │  Normalize (Structural → Canonical)
            ▼
        Expr (Canonical) ◄────────► Representations: Polynomial<T>, RationalFunction<T>,
            │                         PowerSeries, DenseMatrix<Expr> (fast algorithms)
            │  Engine (Simplify, Factor, Differentiate, Solve, Prove …)
            │     ├── reads rules from IKnowledgeBase (catalog entries → oriented rules)
            │     ├── queries AssumptionSet for side conditions (Truth: True/False/Unknown)
            │     └── records Steps into a Derivation (tree of steps with provisos)
            ▼
        Outcome<T> { Value, Derivation, Provisos, Verification }
            │
            ├── Render: text, LaTeX, MathML, two-column proof, JSON
            ├── Check: numeric cross-validation, proof kernel replay
            └── Evaluate/Compile: exact, double, Complex<double>, Interval<double>, BigFloat
```

## Key subsystems

| Subsystem | Lives in | Responsibility | Detail doc |
| --- | --- | --- | --- |
| Number tower | Core | Exact and approximate number types implementing .NET generic math | `04-type-system.md` |
| Polynomials | Core | Dense/sparse univariate and multivariate polynomials, GCD, square-free, resultants | `domains/d1-algebra.md` |
| Syntax tree | Symbolics | `Expr` nodes, operators, binders, canonical levels | `05-syntax-trees-and-notation.md` |
| Sorts and assumptions | Symbolics | Mathematical types (ℕ ⊂ ℤ ⊂ ℚ ⊂ ℝ ⊂ ℂ, sets, matrices, functions) and facts about symbols | `04-type-system.md` |
| Parser and printers | Symbolics | Linear text, LaTeX subset, MathJSON in; text, LaTeX, MathML, C#, JSON out | `05-syntax-trees-and-notation.md` |
| Pattern matching and rewriting | Symbolics | Wildcards, associative-commutative matching, rule indexing, strategies, derivations | `07-engines.md` |
| Knowledge catalog | Knowledge | Entries authored in `.mlaw` files, loaded lazily, verified in CI | `06-knowledge-catalog.md` |
| Domain engines | Mathesis | Simplify, Expand, Factor, Calculus, Solve, ODEs, function analysis, explanations | `07-engines.md`, `domains/` |
| Proof kernel | Logic | LCF-style trusted kernel, natural deduction, equational proofs, tactics, SAT | `domains/d3-proofs-and-logic.md` |
| Numerics | Numerics, LinearAlgebra | Floating-point algorithms generic over `IFloatingPointIeee754<T>` | `domains/d9-numerical-analysis.md` |
| Finite math | Discrete | Counting, probability, statistics, finance, LP, Markov chains, game theory, graphs, number theory | `domains/d8-finite-mathematics.md` |

## Cross-cutting concerns

### Context

Every engine call takes an optional immutable `MathContext`:

```csharp
public sealed record MathContext
{
    public static MathContext Default { get; }
    public NumberField Field { get; init; }            // Real (default) or Complex
    public AssumptionSet Assumptions { get; init; }    // facts about symbols
    public IKnowledgeBase Knowledge { get; init; }     // catalog in use
    public CurriculumLevel Level { get; init; }        // limits methods used in explanations
    public Budget Budget { get; init; }                // steps, size, time
    public TraceMode Trace { get; init; }              // None, Steps, Detailed
    public MathContext Assume(Expr fact);              // returns a new context
}
```

There is no ambient (`AsyncLocal`) state. `MathContext.Default` is used when none is passed.

### Results

Engines never throw for mathematical failure. They return `Outcome<T>`:

```csharp
public abstract record Outcome<T>
{
    public sealed record Success(T Value, IDerivation? Steps, Provisos Provisos, Verification Check) : Outcome<T>;
    public sealed record Partial(T Value, string Reason, IDerivation? Steps, Provisos Provisos) : Outcome<T>;
    public sealed record Unevaluated(IMathObject Original, string Reason) : Outcome<T>;
    public sealed record Failed(MathError Error) : Outcome<T>;
}
```

`Outcome<T>` lives in `Mathesis.Core`, which cannot reference `Expr` or `Derivation` (Symbolics). Core therefore defines the marker interfaces `IMathObject` and `IDerivation : IMathObject`; `Expr` implements `IMathObject`, `Derivation` implements `IDerivation`, and `Provisos` holds `IMathObject` conditions. Callers cast back to the concrete type (ADR-13).

Numeric routines return result records (value, error estimate, iterations, converged flag), as in Plan 1 Phase 2.

### Budgets and cancellation

`Budget` caps rewrite steps, intermediate expression size and wall time, and carries a `CancellationToken`. Exceeding it yields `Partial` or `Unevaluated`, never a hang.

### Thread safety

Expressions, operators, catalogs, contexts and number types are immutable. Caches (structural hashes, compiled rule indexes, lazily loaded catalog domains) are initialized with `Lazy<T>` or interlocked publication. Catalog indexes use `FrozenDictionary`.

### AOT and trimming

- No reflection-based dispatch; operators carry delegates and implementation tables built in code.
- No `System.Linq.Expressions` compilation (it interprets under NativeAOT). Numeric compilation emits a flat instruction array run by a span-based interpreter (`CompiledExpr<T>`).
- JSON uses the `System.Text.Json` source generator.
- Catalog `.mlaw` files are embedded resources parsed by Mathesis's own parser; typed handles are generated ahead of time by `eng/gen-knowledge.cs` and checked in.

### Error model

- `MathError` (domain error, division by zero, non-convergence, budget exceeded, unsupported) is data inside `Outcome.Failed`.
- `ArgumentException` and friends are thrown only for API misuse (null arguments, mismatched matrix dimensions in strongly typed numeric code).

### Logging and diagnostics

Core packages take no logging dependency. Derivations are the trace. `Mathesis.Extensions` adapts derivations and budgets to `ILogger` and `IOptions<T>` for hosted apps.

## Architecture decisions

| ID | Decision | Chosen | Main alternative | Why |
| --- | --- | --- | --- | --- |
| ADR-01 | Tree shape | Small closed set of node kinds plus an open operator registry (`Apply(Operator, args)`) | One C# class per function (`Sin`, `Cos` …) | Thousands of functions and relations can be added as data; pattern matching stays uniform |
| ADR-02 | Equality and sharing | Cached structural hash plus structural equality; optional per-context interning later | Global hash-consing table | No global mutable state; simpler thread safety; profile before interning |
| ADR-03 | Canonical levels | Raw → Structural → Canonical, with explanation mode starting from Raw | Always auto-simplify on construction (SymPy style) | Step-by-step output must show "combine like terms", which auto-simplification would hide |
| ADR-04 | Laws as data | `.mlaw` catalog parsed by the library's own parser, verified numerically in CI | Rules hard-coded in C# | Reviewable, countable, testable, traceable to docs |
| ADR-05 | Generic arithmetic | Number types implement .NET generic math (`INumberBase<T>` family); exact algorithms constrain on operator interfaces and take a pluggable zero test; `Expr` implements the operator interfaces | Separate "witness" type-class interfaces for every algorithm | Reuses the BCL's model, works for `double` and our types, and lets `DenseMatrix<Expr>` reuse exact algorithms |
| ADR-06 | Abstract structures | Groups, rings and fields as runtime objects (`IGroup<T>` instances such as `DihedralGroup(4)`) | Static-abstract structure interfaces | Structures are parameterized values in mathematics |
| ADR-07 | Simplification | Strategy-driven term rewriting with a complexity measure; equality saturation (e-graphs, Willsey et al., POPL 2021) as a later research spike | E-graphs from the start | Directed rewriting yields readable step-by-step derivations |
| ADR-08 | Proofs | Small LCF-style kernel: `Theorem` values only constructible by kernel rules | Trusting engine output | Keeps the trusted base tiny |
| ADR-09 | Real vs. complex | Real mode by default (school mathematics); complex mode opt-in, principal branches documented per function | Complex by default | Matches the curriculum; avoids surprising complex answers |
| ADR-10 | 0⁰ | Exactly 1 in arithmetic (consistent with the binomial theorem, power series, IEEE 754 `pow` and `Math.Pow(0, 0)`); indeterminate as a limit form | Undefined everywhere | Keeps polynomial and series algebra uniform |
| ADR-11 | Numeric compilation | Flat instruction array with a span interpreter | `System.Linq.Expressions.Compile` | AOT-safe and fast enough; avoids reflection emit |
| ADR-12 | Logging | No logging dependency in core packages | `Microsoft.Extensions.Logging.Abstractions` everywhere | Smallest dependency graph; derivations already trace |
| ADR-13 | Core result types vs. Expr | `Outcome<T>` and `Provisos` in Core carry `IMathObject` / `IDerivation` marker interfaces that Symbolics implements (2026-10-01) | Move `Outcome<T>` to Symbolics, or an `Outcome<T>` without expressions | Numerics and LinearAlgebra, which sit below Symbolics, can still return `Outcome<T>`; Core stays dependency-free |
| ADR-14 | Elementary functions on `Dual<T>` and `Complex<T>` | C# 14 extension members constrained on `IFloatingPointIeee754<T>` (2026-10-02) | Implementing `IExponentialFunctions<T>`, `ITrigonometricFunctions<T>`… on the struct | A struct cannot implement an interface only for some `T`; generic code that needs `Dual<T>` takes the operator interfaces and calls `Dual<T>.Sin(x)` directly |
| ADR-15 | Validation package | `Mathesis.Validation`: layer 4, optional, references Symbolics only, `net10.0` only, no UI-framework reference (2026-10-04) | Put it in `Mathesis.Extensions`, or one package per UI framework | The attributes are plain BCL types every UI stack already consumes; no workloads or `-windows` TFMs; avoids the `Xamarin.*` transitive packages the policy check rejects |
| ADR-16 | One verdict, two entry points | Each attribute computes its verdict once and exposes it through `IsValid(object?)` and `IsValid(object?, ValidationContext)`; failures are `MathValidationResult` with code, span and suggestion (2026-10-04) | Override only the context overload | Verified on .NET 10.0.401: a context-only attribute throws `NullReferenceException` from `IsValid(value)` and `Validate(value, name)`; a `ValidationResult` subclass survives `Validator.TryValidateObject` and `TryValidateProperty` |
| ADR-17 | Cross-property rules | `IValidatableObject` (and each attribute's typed `Check`) (2026-10-04) | A `CompareAttribute`-style attribute naming another property | Resolving a property by name needs reflection (`RequiresUnreferencedCode`), which G8 forbids |
| ADR-18 | Sync only, .NET 10 surface | Synchronous validation; never use `AsyncValidationAttribute`, `IAsyncValidatableObject` or `Validator.*Async` (2026-10-04) | Adopt the async types | They ship in .NET 11 only; validation here is parse-and-inspect and does no I/O |

Record new decisions in this table as they are made, with the date in the commit message.
