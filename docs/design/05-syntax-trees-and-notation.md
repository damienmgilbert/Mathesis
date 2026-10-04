# 05 Syntax trees and notation

Every mathematical object Mathesis handles, from `2 + 3` to "for every ε > 0 there is a δ > 0 …", is an `Expr` tree built from twelve node kinds and an open registry of operators. This doc defines the nodes, the three canonical levels, the operator catalog, the input notation every other doc uses, and the printing rules.

## Node kinds

| Node | Children | Invariants |
| --- | --- | --- |
| `Number` | none | `BigRational` value, normalized; optional display hint (`Decimal(digits)`, `Fraction`, `MixedNumber`) so `0.25` typed by a user prints back as `0.25` while staying exact |
| `Float` | none | Approximate value with its precision in bits; never produced by parsing decimal literals (those are exact) |
| `Symbol` | none | Name (Unicode letters, digits, `_`, primes); declared sort; equality by name *and* sort |
| `Constant` | none | One of the built-in constants below |
| `Apply` | operator + ordered arguments | Arity matches the operator; at Structural level and above, associative operators are flattened |
| `Bind` | bound symbols, binder data, body | Bound symbols are local: α-equivalent binds are equal (`∑ k` and `∑ j` with the same body compare equal) |
| `MatrixLiteral` | m × n entries, row-major | m, n ≥ 1; a column vector is m × 1 |
| `SetLiteral` | elements | Deduplicated by structural equality and sorted at Canonical level |
| `IntervalLiteral` | lower, upper | Closed/open flags; infinite ends are always open |
| `TupleLiteral` | elements | Ordered; used for points, ordered pairs, argument lists |
| `Piecewise` | (value, condition) pairs | Conditions are evaluated in order; a final `True` condition is the "otherwise" branch |
| `Wild` | none | Appears only inside patterns; carries a name and an optional constraint |

All nodes are immutable. Each caches on first use: structural hash, leaf count, depth, free symbols, inferred sort, and a "contains" bitmask of operator families (trig, log, matrix …) used to skip irrelevant rules quickly.

An `ExprPath` (a list of child indices) addresses any subexpression. Steps record the path they rewrote, so a UI can highlight the part that changed.

## Canonical levels

| Level | Produced by | What it does | What it never does |
| --- | --- | --- | --- |
| **Raw** | Parser, `Sym` builders in explain mode | Keeps the input's shape: `Sub`, `Div`, `Neg` and `Sqrt` exist as operators; numbers keep their display hint | Reorder, fold or remove anything |
| **Structural** | `Normalizer.Structural` | Rewrites `a − b` → `a + (−1)·b`, `a / b` → `a · b^(−1)`, `−a` → `(−1)·a`, `√a` → `a^(1/2)`; flattens associative operators; keeps operand order | Fold numbers, remove identities, sort |
| **Canonical** | `Normalizer.Canonical` (automatic simplification) | Folds exact arithmetic (`2 + 3` → `5`, `2^10` → `1024`, `1/2 + 1/3` → `5/6`); removes identities (`x + 0`, `1·x`, `x^1`); `x^0` → `1` (ADR-10); collects like terms and like powers with *numeric* coefficients and exponents (`2x + 3x` → `5x`, `x·x^2` → `x^3`); evaluates operators at their identity points (`sin(0)` → `0`, `ln(1)` → `0`, `e^0` → `1`); sorts commutative operands by the total order | Distribute (`2(x + 1)` stays), expand powers, apply any law with a side condition |

The total order follows Joel S. Cohen's ordering rules for automatically simplified algebraic expressions (*Computer Algebra and Symbolic Computation: Elementary Algorithms*, 2002): numbers first, then symbols alphabetically, then applications by operator and arguments, with powers compared by base then exponent. Non-commutative factors (matrices, operators) keep their relative order inside `Mul`; only scalar factors move.

Explain mode starts from Raw and records every change, including the arithmetic Canonical would do silently, so the derivation can show "2 + 3 = 5" as its own step. Ordinary mode starts from Canonical.

**Canonical invariants (property-tested):** no nested `Add` in `Add` or `Mul` in `Mul`; at most one `Number` per `Add`/`Mul` (first operand); no single-operand `Add`/`Mul`; no `x^1`; no `0` term or `1` factor; operands sorted; `Sub`, `Div`, `Neg`, `Sqrt` absent.

## Operators

An `Operator` is data. The built-in registry (`Operators`) and the catalog can add more; nothing in the tree is hard-wired to a specific function.

```csharp
public sealed record Operator(
    string Id,                       // stable: "sin", "add", "det"
    Arity Arity,                     // Fixed(n) or Variadic(min)
    OperatorAttributes Attributes,   // flags below
    Signature Signature,             // sorts in → sort out (with overloads, e.g. mul on matrices)
    Notation Notation,               // fixity, precedence, text symbol, LaTeX template, MathML
    Expr? Identity, Expr? Absorbing, // 0 for add; 1 and 0 for mul
    string? InverseOf,               // "ln" for "exp"; restricted-domain inverses noted in the catalog
    NaturalDomain Domain,            // ln: x > 0 (real mode); sqrt: x ≥ 0 (real mode)
    BranchCut? Cut,                  // complex mode: documented principal branch
    NumericKernels Kernels,          // double, Complex<double>, Interval<double>, BigFloat, Dual<T>
    EntryId? DerivativeRule, EntryId? AntiderivativeRule, EntryId? SeriesRule);
```

| Attribute | Meaning | Examples |
| --- | --- | --- |
| `Associative` | Flatten nested applications | add, mul, and, or, union, intersection, gcd, compose |
| `Commutative` | Operands may be sorted (scalar sorts only for mul) | add, mul, and, or, union, gcd, eq, ne |
| `Idempotent` | `f(a, a) = f(a)` | and, or, union, intersection, gcd, max, min |
| `Involution` | `f(f(a)) = a` | not, conj, transpose, neg |
| `Listable` | Threads elementwise over matrices, tuples and sets of values | sin, exp, abs, floor |
| `Linear` | Linear in the first argument | diff, integrate, sum, expectation, laplace, trace |
| `Odd` / `Even` | `f(−x) = −f(x)` / `f(−x) = f(x)` | sin, tan, sinh, arcsin / cos, cosh, abs, sec |
| `Periodic(p)` | `f(x + p) = f(x)` | sin, cos (2π), tan (π), frac (1) |
| `Monotonic(…)` | Increasing/decreasing on stated intervals | exp, ln, arctan (increasing on ℝ) |
| `Injective` | One-to-one on its natural domain | exp, ln, cube root, arctan |
| `HoldArguments` | Do not normalize arguments | quote, unevaluated forms |
| `NumericFunction` | Evaluates to a number when all arguments are numbers | all elementary and special functions |

### Built-in operator catalog

Notation column shows the linear input syntax (see below). Unicode alternatives are accepted where listed.

| Family | Operators (id: notation) |
| --- | --- |
| Arithmetic | `add: a + b`, `mul: a*b` (also `a·b`, `a×b` for scalars, implicit `2x`), `pow: a^b`, Raw-only `sub: a - b`, `div: a/b` (also `÷`), `neg: -a`, `sqrt: sqrt(a)` (also `√a`), `root: root(a, n)` (real n-th root), `abs: abs(a)` (also `\|a\|`), `sign: sign(a)`, `floor: floor(a)` (also `⌊a⌋`), `ceil: ceil(a)` (also `⌈a⌉`), `round`, `frac: frac(a)`, `mod: a mod n`, `quo: quo(a, n)`, `gcd`, `lcm`, `max`, `min`, `factorial: n!`, `factorial2: n!!`, `subfactorial: !n`, `binomial: binomial(n, k)` (also `C(n, k)`, `nCr`), `perm: perm(n, k)` (also `P(n, k)`, `nPr`) |
| Complex | `re`, `im`, `conj: conj(z)` (also `z̄`), `arg`, `cis: cis(θ)` |
| Exponential and logarithmic | `exp: exp(x)` (also `e^x`), `ln: ln(x)`, `log: log(x, b)` (also `log_b(x)`), `lambertw: W(x)` |
| Trigonometric | `sin cos tan cot sec csc`, inverses `arcsin arccos arctan arccot arcsec arccsc` (also `asin`, `sin^-1(x)`), `atan2(y, x)` |
| Hyperbolic | `sinh cosh tanh coth sech csch`, inverses `arsinh arcosh artanh arcoth arsech arcsch` (also `asinh`) |
| Special functions | `gamma`, `beta`, `digamma`, `erf`, `erfc`, `erfi`, `zeta`, `polylog`, `besselj`, `bessely`, `besseli`, `besselk`, `airyai`, `airybi`, `ellipk`, `ellipe`, `si`, `ci`, `ei`, `li`, `fresnels`, `fresnelc`, `heaviside` (also `u(t)`), `dirac` (also `δ(t)`), `kronecker: δ(i, j)`, `leviCivita: ε(i, j, k)` |
| Orthogonal polynomials | `legendreP`, `hermiteH`, `laguerreL`, `chebyshevT`, `chebyshevU`, `jacobiP`, `gegenbauerC` |
| Number theory and combinatorics | `totient: φ(n)`, `mobius: μ(n)`, `divisorSigma: σ(k, n)`, `primePi`, `prime(n)`, `fibonacci: F(n)`, `lucas`, `catalan`, `stirling1`, `stirling2`, `bell`, `partitions`, `harmonic: H(n)`, `multinomial` |
| Relations | `eq: a = b`, `ne: a != b` (also `≠`), `lt <`, `le <=` (also `≤`), `gt >`, `ge >=` (also `≥`), `approx: a ~= b` (also `≈`), `divides: a \| b` (also `∣`), `congruent: a ≡ b (mod n)`, `element: x in S` (also `∈`), `notElement` (`∉`), `subset ⊂`, `subsetEq ⊆`, `similar` (matrices), `proportional ∝`, `perpendicular ⊥`, `parallel ∥`, `distributed: X ~ Normal(0, 1)` |
| Logic | `and: p and q` (also `∧`, `&&`), `or` (`∨`, `\|\|`), `not` (`¬`, `!`), `implies: p => q` (also `→`, `⇒`), `iff: p <=> q` (also `↔`, `⇔`), `xor` (`⊕`), `nand`, `nor` |
| Sets | `union ∪`, `intersect ∩`, `setminus ∖` (also `\`), `complement: A^c`, `symdiff △`, `cartesian ×` (resolved by sort), `powerset: P(A)` (also `𝒫`), `card: \|A\|` (resolved by sort) |
| Functions | `call: f(x)` for function symbols, `compose: f ∘ g`, `inverseFunction: f^-1` (on function symbols), `derivativeOf: f'` (`f''`, `f^(n)`) |
| Calculus | `diff: diff(f, x)`, `diff(f, x, n)`, `diff(f, x, y)` (also `d/dx f`, `dy/dx`, `∂f/∂x`), `integrate: integrate(f, x)` (indefinite), `bigO: O(x^n)`, `grad: grad(f)` (also `∇f`), `divergence: divergence(F)` (also `∇·F`), `curl: curl(F)` (also `∇×F`), `laplacian: laplacian(f)` (also `∇²f`), `jacobian`, `hessian` |
| Linear algebra | `mul` (non-commutative on matrices), `transpose: A^T` (also `Aᵀ`), `conjTranspose: A^H` (also `A*`, `Aᴴ`), `inverse: A^-1`, `det: det(A)` (also `\|A\|` by sort), `trace: tr(A)`, `rank`, `adj`, `dot: dot(u, v)` (also `u·v` on vectors), `cross: cross(u, v)` (also `u×v`), `outer`, `kron ⊗`, `norm: norm(v)` (also `‖v‖`, `norm(v, p)`), `rref`, `nullspace`, `colspace`, `rowspace`, `eigenvalues`, `eigenvectors`, `charpoly`, `identity: I(n)`, `zeros(m, n)`, `diag(…)`, `proj: proj(u, v)` |
| Probability | `prob: P(A)`, `P(A \| B)` (conditional), `expect: E(X)` (also `E[X]`), `var: Var(X)`, `cov: Cov(X, Y)`, `corr`, distribution constructors (`Bernoulli`, `Binomial`, `Geometric`, `Poisson`, `Hypergeometric`, `Uniform`, `Normal`, `Exponential`, `StudentT`, `ChiSquared`) |

### Constants

| Constant | Input | Notes |
| --- | --- | --- |
| π | `pi`, `π` | |
| e | `e` | Euler's number; `e` is never a symbol unless the parser option `EIsSymbol` is set |
| imaginary unit | `I`, `ⅈ` | Default keeps `i` free for indices (Plan 1 decision); option `ImaginaryUnit = "i"` for school complex-number problems |
| golden ratio | `GoldenRatio` | `phi`/`φ` stays a symbol (angles) |
| Euler–Mascheroni γ | `EulerGamma` | |
| Catalan's constant | `CatalanG` | |
| ∞, −∞ | `oo`, `inf`, `∞` | Extended reals |
| complex infinity | `zoo` | Complex mode |
| undefined | `undefined` | Result of `0/0` and similar; propagates |
| true, false | `true`, `false` | Booleans |
| ∅, ℕ, ℤ, ℚ, ℝ, ℂ | `EmptySet`, `N`, `Z`, `Q`, `R`, `C` (also `∅ ℕ ℤ ℚ ℝ ℂ`) | `N` contains 0 |

### Binders

| Binder | Input | Bound | Binder data |
| --- | --- | --- | --- |
| Sum | `sum(f, k, a, b)`, `∑` | k | a, b |
| Product | `product(f, k, a, b)`, `∏` | k | a, b |
| Definite integral | `integrate(f, x, a, b)`, `∫` | x | a, b |
| Multiple integral | `integrate(f, (x, a, b), (y, c, d))` | x, y | ranges (inner first) |
| Region integral | `integrate(f, (x, y) in R)`, `integrate(f, dA over D)`, `integrate(f, dV over E)` | coordinates | region as a set expression |
| Line and surface integrals | `integrate(f, s on C)`, `integrate(F·dr on C)`, `integrate(P*dx + Q*dy on C)`, `integrate(F·n, dS over S)` | curve or surface parameter | parameterization or set expression; `∂D` is the positively oriented boundary |
| Limit | `limit(f, x, a)`, `limit(f, x, a, "+")` | x | a, direction |
| Universal / existential | `forall x in S: P`, `exists x in S: P`, `exists! x: P` (also `∀ ∃ ∃!`) | x | domain |
| Lambda | `x -> f` (also `x ↦ f`) | x | none |
| Set builder | `{x in S \| P}` | x | domain |
| Image set | `{f \| k in Z}` | k | domain |
| Indexed union / intersection | `Union(A(k), k, 1, n)` (also `⋃ ⋂`) | k | range |
| Laplace / Fourier transform | `laplace(f, t, s)`, `fourier(f, t, ω)` | t | s or ω |
| Argmin / argmax, min/max over a set | `argmin(f, x in S)` | x | domain |

## Input notation

The same linear notation is used in the parser, in `.mlaw` catalog files, and in every table in these docs.

### Precedence (highest first)

| Level | Operators | Associativity | Notes |
| --- | --- | --- | --- |
| 1 | atoms, `( )`, `[ ]`, `{ }`, `\|x\|`, function call `f(x)`, postfix `!`, `'`, `^T` | | |
| 2 | `^` | right | `2^3^2 = 2^9`; `-x^2 = -(x^2)` |
| 3 | prefix `-`, `+` | | |
| 4 | `*`, `/`, `·`, `×`, `÷`, `mod`, implicit multiplication | left | `1/2x` = `(1/2)·x`; the parser warns `AmbiguousImplicitMultiplication` |
| 5 | `+`, `-` | left | |
| 6 | `∪`, `∩`, `∖` | left | `∩` binds tighter than `∪` |
| 7 | `=`, `!=`, `<`, `<=`, `>`, `>=`, `~=`, `in`, `\|`, `≡`, `⊂`, `⊆` | chained | `a < b <= c` means `a < b ∧ b <= c` |
| 8 | `not` | prefix | |
| 9 | `and` | left | |
| 10 | `or`, `xor` | left | |
| 11 | `=>` | right | |
| 12 | `<=>` | left | |
| 13 | `forall`, `exists`, `->` (lambda) | | Extend as far right as possible |

### Conventions the parser applies

- **Decimals are exact.** `0.1` is the rational 1/10 with a decimal display hint. Floats come only from numeric evaluation, `N(…)`, or C# `double` values.
- **Implicit multiplication.** `2x`, `3(x + 1)`, `(x + 1)(x − 1)`, `x y`, `2 sin x`. With the default option `SingleLetterVariables`, `xy` means `x·y`, except for known function and constant names and Greek letter names (`theta`, `alpha`).
- **Function application without parentheses.** `sin x` means `sin(x)`; `sin 2x` means `sin(2x)`; `sin x cos x` means `sin(x)·cos(x)`.
- **Powers of functions.** `sin^2 x` means `(sin x)^2`. `sin^-1 x` means `arcsin x` (textbook convention); write `(sin x)^-1` or `1/sin x` for the reciprocal.
- **Logarithms.** `ln x` is natural, `log(x, b)` and `log_b(x)` take a base, and bare `log x` is base 10 (school convention). Option `LogMeansNatural` switches bare `log` to base e for university and programming use.
- **Real odd roots.** In real mode `(-8)^(1/3)` and `root(-8, 3)` are both −2 (textbook convention, catalog entry `conv.real-odd-root`). In complex mode `(-8)^(1/3)` is the principal value 1 + i√3 and `root(-8, 3)` stays −2.
- **Primes and Leibniz notation.** `f'(x)`, `y''`, `dy/dx`, `d/dx (x^2)`, `d^2y/dx^2`, `∂f/∂x` parse to `diff`/`derivativeOf`.
- **Unicode.** Superscript digits (`x²`), `√`, `∛`, `π`, `θ`, `≤`, `≥`, `≠`, `∈`, `∞`, `·`, `×`, `÷`, `∑`, `∏`, `∫`, `∀`, `∃`, `¬`, `∧`, `∨`, `→`, `↔` are accepted.
- **Errors.** `ParseError` carries the source span, an expected-token list and a suggestion (`Did you mean sqrt(x)?`).

### LaTeX input subset

`\frac`, `\dfrac`, `\sqrt[n]{}`, `^{}`, `_{}`, `\cdot`, `\times`, `\div`, `\pm` (produces a two-element set), `\left( \right)`, `\sin` and the other function macros, `\ln`, `\log_{b}`, `\int_{a}^{b} … \,dx`, `\sum_{k=1}^{n}`, `\prod`, `\lim_{x \to a^{+}}`, `\infty`, Greek letters, `\mathbb{R}` and the other number sets, `\in`, `\le`, `\ge`, `\ne`, `\land`, `\lor`, `\neg`, `\implies`, `\iff`, `\forall`, `\exists`, `\begin{pmatrix}`/`bmatrix`/`vmatrix` (determinant), `\begin{cases}`, `\{ \mid \}`, `\binom`, `\overline{z}`, `\vec{v}`, `\|v\|`, `\lfloor \rfloor`, `\lceil \rceil`, `\operatorname{name}`.

### MathJSON

[MathJSON](https://mathlive.io/math-json/) is the interchange format used by the MathLive math-input editor, which a MAUI app can host in a WebView for math entry. Mathesis reads and writes it (for example `["Divide", "n", ["Add", 1, "n"]]`), mapping its operator names to Mathesis operator IDs through a table in `Mathesis.Symbolics.Serialization`. Mathesis's own JSON form is `{"op": "add", "args": [...]}` with numbers as `"3/4"` strings to stay exact.

## Example trees

`x^2 - 5x + 6 = 0`, Raw and Canonical:

```text
Raw (exactly as typed)                Canonical
eq                                    eq
├── add                               ├── add
│   ├── sub                           │   ├── 6
│   │   ├── pow                       │   ├── mul
│   │   │   ├── x                     │   │   ├── -5
│   │   │   └── 2                     │   │   └── x
│   │   └── mul                       │   └── pow
│   │       ├── 5                     │       ├── x
│   │       └── x                     │       └── 2
│   └── 6                             └── 0
└── 0
```

Raw keeps the left-associative chain `add(sub(pow(x, 2), mul(5, x)), 6)`; Canonical flattens it into one sorted `add` with the subtraction turned into the coefficient −5. Printing the Canonical tree gives back `x^2 - 5x + 6 = 0`.

`∫₀¹ x² dx`:

```text
Bind(Integral, vars: [x], data: [0, 1])
└── pow(x, 2)
```

"f is continuous at a" (ε–δ definition):

```text
Bind(ForAll, [ε], data: [ℝ])
└── implies
    ├── gt(ε, 0)
    └── Bind(Exists, [δ], data: [ℝ])
        └── and
            ├── gt(δ, 0)
            └── Bind(ForAll, [x], data: [ℝ])
                └── implies
                    ├── lt(abs(add(x, mul(-1, a))), δ)
                    └── lt(abs(add(call(f, x), mul(-1, call(f, a)))), ε)
```

Solutions of `sin x = 1/2`:

```text
union
├── Bind(ImageSet, [k], data: [ℤ])  body: add(mul(1/6, π), mul(2, k, π))
└── Bind(ImageSet, [k], data: [ℤ])  body: add(mul(5/6, π), mul(2, k, π))
```

`|x|` as a piecewise definition: `Piecewise[(x, x >= 0), (-x, true)]`.

## Printing

| Rule | Example |
| --- | --- |
| Terms with negative coefficients print as subtraction | `add(x, mul(-1, y))` → `x - y` |
| Factors with negative exponents print as a fraction | `mul(a, pow(b, -1))` → `a/b` |
| `pow(x, 1/2)` → `√x`; `pow(x, 1/n)` → `x^(1/n)` (real-root semantics print as `root(x, n)`) | |
| Polynomials print in descending degree of the main variable; Structural trees keep the user's order | `6 - 5x + x^2` → `x^2 - 5x + 6` (Canonical only) |
| Minimal parentheses from precedence and associativity | `a - (b - c)` keeps its parentheses; `(a·b)·c` → `a·b·c` |
| `mul(-1, x)` → `-x`; `mul(2, pow(3, 1/2))` → `2√3` | |
| Exact numbers keep their display hint | `1/4` typed as `0.25` prints `0.25` |
| LaTeX uses `\frac`, `\sqrt`, `\left(\right)` only where needed, `\,dx`, `\operatorname{}` for unknown functions | |
| MathML presentation and content forms, C# (`double.Sin(x)` or generic `T.Sin(x)`), tree dump for debugging | |

### Phase 4 implementation notes

Behaviors the design left open, as implemented (change them here first):

- `ToString()` is the faithful printer: ASCII, compact spacing around `*`, `/` and `^`, trees print in the order they have so the output parses back to the same Raw tree. `PrintOptions.Presentation` adds Unicode symbols and descending polynomials; `PrintOptions.DecimalDigits` rounds exact numbers half away from zero.
- A lone `x^-1` in a Canonical tree prints as `x^-1`; only products split into numerator and denominator.
- `!` after an operand is factorial and before one is logical not; `|` between operands is divides and `|x|` is absolute value; `(a, b)` is a tuple, an interval needs a bracket (`[a, b)`); `[a, b, c]` is a column vector; `f(x)` is a call when `f` is declared, listed in `ParserOptions.FunctionSymbols` (default `f`, `g`, `h`) or not a single letter.
- Relation chains `a < b < c` parse to left-nested binary `and`.
- Variable names that are constants (`e`, `I`, `pi`) are rejected in quantifiers and in `d/dx`.
- LaTeX: the imaginary unit prints as `\mathrm{i}` and parses back to `I`; `\bmod`, `A^{\top}`, `\frac{d}{dx} f` and `\frac{d^{n}}{dx^{n}} f` round trip. Region and line integrals are unsupported.
- A number literal with an exponent (`1.5e-3`) is exact; an exponent whose magnitude exceeds `BigRational.MaxExponentMagnitude` (100,000) is a `ParseError` spanning the literal, never the number 0. LaTeX has no exponent literal.
- Mathesis JSON writes numbers as exact strings (`"3/4"`), operators as `{"op": id, "args": [...]}` and symbols as `{"sym": name}`; it never reads or writes display hints.
