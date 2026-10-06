# 04 Type system

Mathesis has two type systems that meet in the syntax tree. **C# types** hold values: number types, the `Expr` tree, and the fast representations. **Sorts** are the *mathematical* types of expressions (ℤ, ℝ, a 3×3 real matrix, a function ℝ → ℝ, a proposition), checked and inferred by the library.

## Number tower (C# types)

All number types are immutable `readonly struct`s unless noted, and implement .NET generic math so generic algorithms accept them alongside `double`.

| Type | Meaning | Implements | Exact | Notes |
| --- | --- | --- | --- | --- |
| `BigInteger` (BCL) | ℤ | `IBinaryInteger<T>` | Yes | Used as is |
| `BigRational` | ℚ, always normalized (gcd 1, positive denominator) | `INumber<T>`, `ISignedNumber<T>`, `IExactNumber` | Yes | Parses `3/4`, `-2`, `0.125`, repeating decimals `0.1(6)`; best rational approximation of a `double` (Stern–Brocot) |
| `Complex<T>` where `T : INumber<T>` | ℚ(i) when `T = BigRational`; ℂ approximations when `T` is floating point | `INumberBase<T>`, `ISignedNumber<T>` | When `T` is exact | `System.Numerics.Complex` is `double`-only; transcendental members exist only when `T : IFloatingPointIeee754<T>` (C# 14 extension members: `Magnitude`, `Phase`, `FromPolar`, `Exp`, `Log`, `Sqrt` so far). `Abs` needs a square root, so it throws `NotSupportedException` for exact `T`; use `NormSquared`. Text form `(re, im)` |
| `Dual<T>` | a + bε with ε² = 0 | `INumberBase<T>`; elementary functions (`Sin`, `Exp`, `Log`, `Sqrt`, `Pow`, …) as C# 14 extension members when `T : IFloatingPointIeee754<T>` (ADR-14) | No | Forward-mode automatic differentiation. Text form `(value, derivative)` |
| `HyperDual<T>` | a + bε₁ + cε₂ + dε₁ε₂ | as `Dual<T>` | No | Exact first and second derivatives numerically |
| `Jet<T>` where `T : IFloatingPointIeee754<T>` | Value plus a gradient: multivariable first-order forward-mode AD; nested jets give Hessians | `IFloatingPointIeee754<Jet<T>>` (ADR-19) | No | Unlike `Dual<T>` it is accepted by every `IFloatingPointIeee754<T>` algorithm (`Roots`, `Quadrature`, `OdeSolver`; `Minimize` follows MW3), so one generic function runs on `double` and on `Jet<double>`. Value, active-lane count and 16 inline gradient lanes (`Jet<T>.Lanes`); allocation-free; `Constant` has no gradient. See "Jet semantics" below. Text form `(value; g₀, g₁, …)` |
| `Interval<T>` where `T : IFloatingPointIeee754<T>` | Closed interval [lo, hi] | `INumberBase<T>` | Enclosure | Outward rounding with `T.BitDecrement`/`T.BitIncrement`; guaranteed enclosures for validated numerics and assumption reasoning |
| `ModInt<TModulus>` where `TModulus : IModulus` | ℤ/nℤ with the modulus fixed at compile time | `INumberBase<T>`, `IExactNumber` | Yes | `UInt128` products; division only when `TModulus.IsPrime` (a field) |
| `ModInteger` (class) | ℤ/nℤ with a run-time `BigInteger` modulus | operators | Yes | For number-theory problems with large moduli |
| `BigFloat` | Arbitrary-precision binary floating point | `IFloatingPoint<T>` and function interfaces | Correctly rounded basic operations | Later milestone; elementary functions with Ziv's rounding test |
| `AlgebraicNumber` (class) | Root of an irreducible polynomial over ℚ with an isolating interval or box | operators | Yes | Arithmetic via resultants; later milestone |
| `ContinuedFraction` (class) | Finite or periodic continued fraction | conversions | Yes | Convergents, best approximations, √n expansions |

Components of `Complex<T>` and `Dual<T>` are formatted and parsed with the invariant culture; `BigRational` also ignores the culture and always uses `.` for decimals.

`NumberTraits<T>.IsExact` tells generic algorithms whether to pivot for stability (floating point) or for exactness (first non-zero pivot, fraction-free elimination). It is true for `BigInteger`, `BigRational`, `ModInt<T>`, `Complex<T>` of an exact `T`, and any type implementing the marker interface `IExactNumber`.

### Jet semantics

`Jet<T>` follows the rules of `Dual<T>` for gradients but differs from it where generic algorithms need `double`-like behaviour (ADR-19; evidence in the Technesis prototype report). Each row has a test in the conformance suite.

| Member group | Value | Gradient |
| --- | --- | --- |
| `+ − × ÷`, unary minus | as `T` | sum, product and quotient rules |
| `Sqrt`, `Cbrt`, `RootN`, `Pow`, `Exp*`, `Log*`, trigonometric and hyperbolic functions and inverses, `Atan2`, `Hypot`, `FusedMultiplyAdd`, `Lerp` | as `T` | chain rule; where the derivative is infinite or undefined the formula's result (±∞ or NaN) is returned, as IEEE arithmetic would (`Sqrt` of `Variable(0)` has gradient +∞) |
| `Abs` | as `T` | `g` at both +0 and −0, `−g` for negative values (`Dual<T>` differs at −0) |
| `Min`, `Max`, `MinMagnitude`, `MaxMagnitude`, `Clamp` | as `T` | gradient of the selected operand; ties select the first operand; `Clamp` takes the bound's gradient when it clamps |
| `Floor`, `Ceiling`, `Round`, `Truncate` | as `T` | zero |
| `CopySign` | as `T` | gradient of the magnitude operand, negated when the sign flips |
| `ScaleB`, `ILogB`, `BitIncrement`, `BitDecrement`, estimates, `Ieee754Remainder` | as `T` | `ScaleB` scales by `2ⁿ`; `BitIncrement`/`BitDecrement` pass the gradient through; estimates use the derivative of the function they estimate; remainder uses `gx − gy·round(x/y)` |
| `<`, `<=`, `>`, `>=`, `==`, `!=` | compare values only | none |
| `IsNaN`, `IsInfinity`, `IsFinite`, `IsNegative`, `IsZero`, … | of the value only; `IsGradientFinite` reports the gradient | none |
| `Equals`, `GetHashCode` | structural: value, dimension and gradient | |
| Constants | `Constant(c)` has dimension 0 and no gradient; a structural zero is never multiplied, so `Constant(2) × Variable(∞)` has gradient 2, not NaN | |
| Dimensions | operands of different non-zero dimension throw `ArgumentException`; a constant combines with any dimension; at most `Jet<T>.Lanes` (16) variables per jet | |
| Text form | `(value; g₀, g₁, …)`; a constant prints `(value)` | |

### Generic-math constraints by algorithm family
| Family | Constraint | Accepts |
| --- | --- | --- |
| Floating-point numerics (roots, quadrature, ODEs, FFT) | `T : IFloatingPointIeee754<T>` | `float`, `double`, `Half`, `NFloat`, later `BigFloat` |
| Exact and generic linear algebra, polynomial arithmetic | `T : IAdditionOperators<T,T,T>, ISubtractionOperators<T,T,T>, IMultiplyOperators<T,T,T>, IDivisionOperators<T,T,T>, IUnaryNegationOperators<T,T>, IAdditiveIdentity<T,T>, IMultiplicativeIdentity<T,T>, IEqualityOperators<T,T,bool>` plus an optional `Func<T, bool>` zero test | `double`, `BigRational`, `ModInt<T>`, `Complex<T>`, `Expr` |
| Euclidean algorithms (gcd, extended gcd) | `T : IBinaryInteger<T>` or `Polynomial<TField>` | `int`, `long`, `BigInteger`, polynomials over a field |
| Complex-capable numerics (Muller, Aberth, FFT) | `Complex<T>` with `T : IFloatingPointIeee754<T>` | |

### Runtime algebraic structures

Groups, rings and fields that are *parameterized values* (the dihedral group D₄, ℤ/12ℤ, GF(2⁸)) are objects implementing `IGroup<TElement>`, `IRing<TElement>`, `IField<TElement>`. They expose `Identity`, `Operate`, `Inverse`, `Elements` (when finite), `Order`, and property checks (`IsAbelian`, `IsCyclic`). This is a later milestone used by abstract algebra and number theory.

## The syntax tree (C# types)

`Expr` is an abstract class with a small closed set of node kinds. Full detail, invariants and notation are in `05-syntax-trees-and-notation.md`.

| Node | Fields | Example |
| --- | --- | --- |
| `Number` | `BigRational Value` | `3/4` |
| `Float` | `double` or `BigFloat` value, precision | `2.5` (approximate, labelled) |
| `Symbol` | `string Name`, `Sort DeclaredSort` | `x`, `θ`, `f` |
| `Constant` | `ConstantId` | π, e, i, φ, γ, ∞, −∞, complex ∞, undefined |
| `Apply` | `Operator Op`, `ImmutableArray<Expr> Args` | `sin(x)`, `a + b + c`, `x ≤ 3`, `A ∪ B` |
| `Bind` | `Binder Kind`, bound `Symbol`s, binder data (`ImmutableArray<Expr>`), `Expr Body` | ∑, ∏, ∫, lim, ∀, ∃, λ, {x ∣ P(x)} |
| `MatrixLiteral` | rows, columns, `ImmutableArray<Expr>` entries | `[[1, 2], [3, 4]]` |
| `SetLiteral` | `ImmutableArray<Expr>` elements (deduplicated, ordered) | `{1, 2, 3}` |
| `IntervalLiteral` | lower, upper, `bool` closed flags | `[0, 1)` |
| `Piecewise` | `ImmutableArray<(Expr Value, Expr Condition)>` | `\|x\|` as a piecewise definition |
| `TupleLiteral` | elements | `(1, 2)` as a point or an ordered pair |
| `Wild` (patterns only) | name, constraint | `a_`, `n_ ∈ ℤ` |

Every node caches its structural hash, leaf count, free symbols and inferred sort on first use.

## Sorts (mathematical types)

```text
Any
├── Boolean (propositions)
├── Number
│   └── Complex ℂ ⊃ Real ℝ ⊃ Rational ℚ ⊃ Integer ℤ ⊃ Natural ℕ (0 ∈ ℕ)
│       ├── Algebraic 𝔸 (ℚ ⊂ 𝔸 ⊂ ℂ)
│       ├── ExtendedReal ℝ ∪ {−∞, +∞}
│       └── Residue(n) ℤ/nℤ
├── Set(Sort)
├── Tuple(Sort₁, …, Sortₖ)
├── Vector(n, Sort)            n may be a symbol
├── Matrix(m, n, Sort)
├── Function(Domain → Codomain)
│   └── Sequence(Sort) = Function(ℕ → Sort)
├── Polynomial(Sort, vars)     used when a symbol stands for a polynomial
├── RandomVariable(Sort)
└── Structure (Group, Ring, Field, VectorSpace) — later milestone
```

- **Declaration.** `Symbol("n", Sort.Integer)`; undeclared symbols default to `Real` in real mode and `Complex` in complex mode.
- **Inference.** Operators declare signatures (`+ : Matrix(m,n,T) × Matrix(m,n,T) → Matrix(m,n,T)`); `SortChecker` infers each node's sort and reports mismatches (adding a 2×3 matrix to a scalar) as `MathError.SortMismatch` with the node path.
- **Refinement.** Assumptions can narrow sorts (`n ∈ ℤ`, `n > 0` makes n a positive integer).

## Truth and assumptions

```csharp
public enum Truth : byte { False, True, Unknown }   // Kleene three-valued logic

public sealed record AssumptionSet
{
    public AssumptionSet Add(Expr fact);                 // x > 0, n ∈ ℤ, a ≠ 0, x ∈ [0, π]
    public Truth Ask(Expr proposition);                  // Ask(x² + 1 > 0) == True
    public Interval<double>? Bounds(Symbol s);           // derived numeric bounds
    public SignInfo Sign(Expr e);                        // Negative, Zero, Positive, NonNegative …
}

public enum ZeroTestResult : byte { Zero, NonZero, ProbablyZero, Unknown }
```

Reasoning layers in order of cost: sort facts, sign propagation (sums of positives, squares, `exp`), interval arithmetic over derived bounds, small linear systems (Fourier–Motzkin), and parity facts for integers. `Ask` returns `Unknown` rather than guessing.

## Sets and solution sets

| Kind | Example | Produced by |
| --- | --- | --- |
| `EmptySet` | ∅ | `Solve(x² + 1 = 0, x)` in real mode |
| `FiniteSet` | {2, 3} | Polynomial equations |
| `Interval` and `Union` | (−∞, −2) ∪ [3, ∞) | Inequalities |
| Number sets | ℕ, ℤ, ℚ, ℝ, ℂ | Identities (solution set ℝ) |
| `ImageSet` | {π/6 + 2πk ∣ k ∈ ℤ} | Trigonometric equations |
| `ConditionSet` | {x ∈ ℝ ∣ x = cos x} | Equations without closed form, with a numeric approximation attached |
| `Complement`, `Intersection` | ℝ ∖ {0} | Domains |
| `ProductSet` | ℝ × [0, 2π) | Systems and parameterizations |
| `ParametricSolution` | {(1 − 2t, t) ∣ t ∈ ℝ} | Underdetermined linear systems |

## Representations

Fast, specialized forms that convert losslessly to and from `Expr` for a fixed variable order.

| Type | Used for | Key operations |
| --- | --- | --- |
| `Polynomial<T>` (dense univariate) | Root finding, division, GCD, interpolation | Horner evaluation, division with remainder, GCD, derivative, composition, Taylor shift |
| `SparsePolynomial<T>` (multivariate) | Expand, factor, Gröbner bases | Monomial orders, multiplication (Kronecker substitution for large inputs), division, content and primitive part |
| `RationalFunction<T>` | Together, Cancel, Apart, rational integration | Normalized numerator/denominator, partial fractions |
| `PowerSeries` | Taylor series, limits, asymptotics | Truncated arithmetic with order term O(xⁿ), composition, reversion |
| `DenseMatrix<Expr>` | Symbolic linear algebra | Reuses the exact algorithms in `Mathesis.LinearAlgebra.Exact` with a simplifying zero test |

## Derivations and steps

```csharp
public sealed record Step(
    EntryId? Entry,                 // catalog entry applied (law, theorem, method), if any
    string RuleName,
    Expr Before, Expr After,
    ExprPath Path,                  // where in the whole expression the rewrite happened
    Bindings Bindings,              // pattern variables → subexpressions
    Provisos Added,                 // conditions this step introduced, e.g. x ≠ 0
    ExplanationKey Explanation,     // template key + arguments, rendered later
    CurriculumLevel Level,
    Derivation? Substeps);          // nested detail, e.g. the inner derivative of a chain rule

public sealed record Derivation(Expr Start, Expr End, ImmutableArray<Step> Steps) : IDerivation
{
    public IEnumerable<Step> Flatten();
    public Outcome<Expr> Replay(MathContext ctx);   // re-applies each step; must reproduce End
    public Proof ToProof();                          // equational proof for the logic kernel
}
```

## Knowledge entries

Catalog entries are typed records (`Definition`, `Axiom`, `Law`, `Theorem`, `Formula`, `Pattern`, `Method`, `Convention`) sharing an `EntryId`, names, domain, formal statement, conditions, curriculum level, references and tags. See `06-knowledge-catalog.md`.

## Proof types

`Judgment` (context ⊢ proposition), `Theorem` (a judgment the kernel has accepted; its constructor is internal to the kernel), `Proof` (tree of rule applications), `Tactic` (a function from goals to subgoals plus a proof builder). See `domains/d3-proofs-and-logic.md`.
