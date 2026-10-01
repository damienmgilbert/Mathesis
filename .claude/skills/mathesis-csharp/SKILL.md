---
name: mathesis-csharp
description: Write, review or refactor C# code for the Mathesis math library so it follows the project's design — .NET 10 LTS, generic math over INumber of T and IFloatingPointIeee754 of T, Outcome results with Budget and provisos, immutable AOT-safe types, catalog-backed rules, and MSTest property/metamorphic tests. Use whenever the user asks for Mathesis code (BigRational, Complex, Dual, Interval, Expr, Sym, Cas, DenseMatrix, Polynomial, parser, simplifier, solver, numerics, tests), pastes Mathesis code for review, or asks how to implement a phase of the plan in C# — even if they don't say "Mathesis" but the code uses these types.
---

# Mathesis C#

Mathesis's design docs make specific choices that ordinary C# habits contradict (throwing on failure, `^` operator overloads, `System.Linq.Expressions`, reflection, helper-method sprawl). This skill keeps code consistent with those choices so Claude Code's phase exit checks pass and the code stays trustworthy.

Before writing code for a subsystem, read the design doc section for it (`project_read`): numbers → `04-type-system.md`; tree, parser, printers → `05-syntax-trees-and-notation.md`; engines → `07-engines.md`; namespaces and API style → `03-namespaces-and-packages.md`; tests → `09-verification.md`; phase scope → `Plan 1 Math library.md`. If the code you are asked for would differ from the design, say where and ask rather than silently diverging — the plan treats docs and code disagreeing as a stop-and-ask event.

## Platform and build

- Target `net10.0` only (.NET 10 LTS, C# 14). Never add net9.0, net11.0 or preview targets, and don't use preview language features.
- Nullable on, implicit usings on, `TreatWarningsAsErrors`, `AnalysisLevel` latest-recommended, deterministic builds, `IsAotCompatible` on `src/` projects, central package management with transitive pinning.
- Shipped packages: BCL plus non-deprecated, non-vulnerable `System.*`, `Microsoft.*`, `CommunityToolkit.*` packages only, transitive included. Today only `System.Numerics.Tensors` is used (Numerics, LinearAlgebra). Suggesting any other package for `src/` is a design change — call it out. Test projects are exempt (MSTest).
- AOT/trim safe: no reflection-based dispatch, no `System.Linq.Expressions.Compile`, no `Reflection.Emit`, no `dynamic`; JSON via the System.Text.Json source generator; numeric compilation is a flat instruction array interpreted over spans (`CompiledExpr<T>`).

## Shape of the code

- **Results, not exceptions.** Engines return `Outcome<T>` (`Success(Value, Steps, Provisos, Check)`, `Partial`, `Unevaluated(Original, Reason)`, `Failed(MathError)`). Throw only for API misuse (null arguments, mismatched dimensions in strongly typed numeric code). Numeric routines return result records with value, error estimate, iterations and a converged flag instead of throwing on non-convergence.
- **Budgets.** Every symbolic operation takes a `MathContext` (or a `Budget`) and checks it inside loops: steps, expression size, time and the `CancellationToken`. Exceeding the budget returns `Partial` or `Unevaluated`, never hangs.
- **Immutability and thread safety.** Number types are `readonly struct`s; `Expr` nodes, operators, catalogs and contexts are immutable. Use `ImmutableArray<T>`, `FrozenDictionary`, `Lazy<T>` or interlocked publication for caches. No `AsyncLocal` or static mutable state.
- **Exact first.** Default to `BigRational`/exact `Expr`; produce floating point only when asked (`N`, `NSolve`, `Approximate`) and label it (`Float` nodes). `Sqrt(8)` is `2·√2`, not `2.828…`.
- **Conditions travel.** Any transformation that narrows the domain records a proviso (`x != 0`) on the step. Never drop one to make output prettier.
- **Mathematics comes from the catalog.** No rewrite, derivative or antiderivative rule in code without a `.mlaw` entry; steps cite `EntryId`s (via the generated handles, e.g. `Laws.Algebra.Exponents.ProductOfPowers`). If code needs a rule the catalog lacks, propose the entry too (see the mathesis-catalog skill if available).
- **Self-checks.** Antiderivatives are differentiated back and zero-tested; solutions are substituted back; decompositions check residuals. Set `Verification` honestly (`Verified`, `NumericallyConsistent`, `NotChecked`, `Failed`) and return `Failed` checks to the caller.

## Generic math

Pick the constraint the algorithm family uses (`04-type-system.md`):

| Family | Constraint |
| --- | --- |
| Floating-point numerics | `where T : IFloatingPointIeee754<T>` |
| Exact/generic linear algebra and polynomials | the operator interfaces (`IAdditionOperators<T,T,T>`, `ISubtractionOperators<T,T,T>`, `IMultiplyOperators<T,T,T>`, `IDivisionOperators<T,T,T>`, `IUnaryNegationOperators<T,T>`, `IAdditiveIdentity<T,T>`, `IMultiplicativeIdentity<T,T>`, `IEqualityOperators<T,T,bool>`) plus an optional `Func<T, bool>` zero test |
| Euclidean algorithms | `where T : IBinaryInteger<T>` or `Polynomial<TField>` |

Use `NumberTraits<T>.IsExact` to choose pivoting: largest-magnitude pivot for floating point, first non-zero pivot and fraction-free elimination for exact types. Use `T.Zero`, `T.One`, `T.CreateChecked`, `T.Abs`, `T.BitIncrement`/`BitDecrement` (outward rounding in `Interval<T>`); use `TensorPrimitives` for `float`/`double` hot loops over spans.

## Naming and API style

- Never name a type `Math`, `Vector`, `Matrix`, non-generic `Complex` or `Range`. Use `DenseVector<T>`, `DenseMatrix<T>`, `Complex<T>`, `Interval<T>`; the façade is `Cas`.
- Full mathematical names for engines (`Differentiate`, `Determinant`, `CharacteristicPolynomial`); short aliases (`Diff`, `Det`) only on `Sym`. Verbs for engines, nouns for results (`Factor` → `Factorization`). Exactness in names: `Solve` is exact, `NSolve`/`Approximate` say they are numeric.
- `Expr` overloads `+ - * /`, unary `-`, `< <= > >=` (relations), `& | !` (logic), and `==`/`!=` for structural equality. **Do not overload `^`** — it binds looser than `+` in C#; use `Pow`. Build equations with `Eq(a, b)`.
- Synchronous engines with `Budget`; `…Async` only in `Mathesis.Extensions`.
- XML docs on every public member (missing docs fail the build). State conditions and exactness in the summary.

## Code style the user prefers

- Inline small helpers into the calling method rather than extracting tiny one-use methods; extract only when logic is reused or a method becomes hard to follow.
- When editing an existing file, keep its formatting and change only what the task needs; show a focused diff or the changed members, not the whole file, unless asked.
- Latest C# features are welcome where they clarify (primary constructors, collection expressions, `field` keyword, extension members, pattern matching on `Outcome<T>` with `switch`).

## Tests (MSTest)

- One test project per `src` project; shared seeded generators live in `tests/Mathesis.Testing`. Seed every random test so failures reproduce, and print the seed and the failing case.
- New public API gets tests; a bug fix starts with a failing test.
- Prefer properties and metamorphic relations over hand-picked examples (`09-verification.md`): field axioms for number types; `Expand(Factor(p)) == p`; simplified and original agree at 20 random points inside the domain and provisos; derivatives match Richardson-extrapolated central differences; integrals pass differentiate-back; solutions substitute back to exactly 0 (exact) or ≤ 1e−10 (numeric); `Derivation.Replay` reproduces the result.
- Never loosen a tolerance or a condition to make a test pass; find out which side is wrong.

## Licensing

Implement from papers and textbooks and cite them in XML docs or comments (e.g. Brent 1973; Bareiss 1968; Cohen 2002 for ordering). Never transcribe Numerical Recipes or GPL CAS code (Maxima, etc.). BSD/MIT sources such as SymPy may be studied but not copied without attribution.

## Example: the expected shape

```csharp
namespace Mathesis.Algebra;

/// <summary>Cancels common polynomial factors of a rational expression over ℚ.</summary>
/// <remarks>Each cancelled factor c adds the proviso c ≠ 0 (catalog entry <c>alg.rat.cancel</c>).</remarks>
public static class Cancel
{
    /// <summary>Returns the expression with numerator and denominator in lowest terms.</summary>
    public static Outcome<Expr> Apply(Expr expr, MathContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expr);           // API misuse: throw
        var ctx = context ?? MathContext.Default;

        if (!RationalFunction<BigRational>.TryFrom(expr, out var rf))
            return new Outcome<Expr>.Unevaluated(expr, "Not a rational function over ℚ.");

        var gcd = PolynomialAlgorithms.Gcd(rf.Numerator, rf.Denominator, ctx.Budget);
        if (ctx.Budget.IsExceeded)
            return new Outcome<Expr>.Unevaluated(expr, "Budget exceeded while computing the GCD.");
        if (gcd.Degree == 0)
            return new Outcome<Expr>.Success(expr, null, Provisos.None, Verification.Verified);

        // ... build the reduced expression, a Step citing Laws.Algebra.Rational.Cancel,
        //     and Provisos.Of(Ne(gcd.ToExpr(), 0)) on that step ...
    }
}
```

Names such as `TryFrom`, `IsExceeded` and `Provisos.Of` are illustrative; use the real API from the code base or design docs, and say so when you are assuming a member that isn't specified yet.
