# 03 Namespaces and packages

Namespaces follow mathematical domains; packages follow layers. A domain can span packages (linear algebra has numeric kernels in `Mathesis.LinearAlgebra` and symbolic, explained versions in `Mathesis`), but each namespace lives in exactly one package.

## Namespace tree

### `Mathesis.Core` package

| Namespace | Contents |
| --- | --- |
| `Mathesis` | `Outcome<T>`, `Truth`, `MathError`, `Verification`, `Budget`, `Tolerance`, `Provisos`, `IMathObject`, `IDerivation`, `NumberField`, `CurriculumLevel` |
| `Mathesis.Numbers` | `BigRational`, `Complex<T>`, `Dual<T>`, `HyperDual<T>`, `Jet<T>`, `Interval<T>`, `ModInt<TModulus>`, `IModulus`, `ModInteger` (runtime modulus), `BigFloat`, `ContinuedFraction`, `NumberTraits<T>`, `IExactNumber` |
| `Mathesis.Polynomials` | `Polynomial<T>` (dense univariate), `SparsePolynomial<T>` (multivariate), `Monomial`, `MonomialOrder` (lex, grlex, grevlex), `RationalFunction<T>`, `PolynomialAlgorithms` (division, GCD, extended GCD, square-free, resultant, discriminant, Sturm sequence, root bounds, Taylor shift) |
| `Mathesis.Structures` | Runtime algebraic structures: `IMagma<T>`, `IMonoid<T>`, `IGroup<T>`, `IRing<T>`, `IField<T>`, `PermutationGroup`, `CyclicGroup`, `DihedralGroup`, `IntegersModN`, `GaloisField` (later milestone) |

### `Mathesis.Numerics` package

| Namespace | Contents |
| --- | --- |
| `Mathesis.Numerics` | Shared result records (`RootResult<T>`, `QuadratureResult<T>`, `DerivativeResult<T>`, `OdeSolution<T>` …), `Convergence`, `StoppingCriteria`, and the static class `Roots` (see the next row) |
| `Mathesis.Numerics.FloatingPoint` | Ulp distance, machine constants, compensated (Kahan/Neumaier) and pairwise summation, two-sum/two-product, accurate `Hypot`, `Expm1`, `Log1p` |
| `Roots` (static class in `Mathesis.Numerics`; a `Mathesis.Numerics.Roots` namespace would shadow it and break `Roots.Brent(...)`) | Bisection, false position (Illinois), secant, Newton, Halley, Brent, Ridders, fixed-point iteration, Muller, Aberth–Ehrlich, Jenkins–Traub |
| `Mathesis.Numerics.Differentiation` | Finite differences of any order and accuracy, Richardson extrapolation, complex-step derivative, forward-mode automatic differentiation over `Dual<T>` and `Jet<T>` (`JetDifferentiation`: `Gradient`, `Jacobian`, `Hessian`, `Derivative`) |
| `Mathesis.Numerics.Integration` | Newton–Cotes, Romberg, Gauss–Legendre/Laguerre/Hermite/Chebyshev, adaptive Gauss–Kronrod, tanh-sinh, Monte Carlo and quasi-Monte Carlo (Halton, Sobol), multidimensional cubature |
| `Mathesis.Numerics.Interpolation` (factory class `Interpolate`, for the same shadowing reason) | Lagrange, barycentric, Newton divided differences, Neville, Hermite, cubic splines (natural, clamped, not-a-knot), B-splines, Akima, rational (Floater–Hormann) |
| `Mathesis.Numerics.Approximation` | Least-squares polynomial fit, Chebyshev series, Padé, minimax (Remez) |
| `Mathesis.Numerics.Ode` | Euler, Heun, midpoint, RK4, RKF45, Cash–Karp, Dormand–Prince 5(4), Adams–Bashforth–Moulton, BDF, Rosenbrock, velocity Verlet, event detection, boundary-value shooting and finite differences |
| `Mathesis.Numerics.Pde` | Finite-difference heat (FTCS, Crank–Nicolson), wave, Poisson/Laplace (Jacobi, SOR) on rectangular grids |
| `Mathesis.Numerics.Optimization` | Golden-section, Brent minimization, gradient descent, Newton, BFGS, L-BFGS, Nelder–Mead, Levenberg–Marquardt, penalty and augmented-Lagrangian methods |
| `Mathesis.Numerics.NonlinearSystems` | Newton's method for systems, Broyden, homotopy continuation (later) |
| `Mathesis.Numerics.Fourier` | FFT (radix-2 Cooley–Tukey, Bluestein for any length), real FFT, convolution, DCT |
| `Mathesis.Numerics.SpecialFunctions` | Gamma, log-gamma, digamma, beta, incomplete gamma/beta, erf/erfc/inverse, Bessel J/Y/I/K, Airy, elliptic K/E, zeta, polylogarithm, Lambert W, exponential/sine/cosine integrals, Fresnel |
| `Mathesis.Numerics.Random` | Seeded generators wrapping `System.Random`, sampling from distributions, Sobol/Halton sequences |

### `Mathesis.LinearAlgebra` package

| Namespace | Contents |
| --- | --- |
| `Mathesis.LinearAlgebra` | `DenseMatrix<T>`, `DenseVector<T>` (with companion factory classes `DenseMatrix` and `DenseVector`), `MatrixView<T>`, `Shape`, norms (`DenseMatrixNorms`, `DenseVectorNorms`), products (Hadamard, Kronecker), block operations, and `MatrixSolvers` (C# 14 extension members on `DenseMatrix<T>` for floating-point `T`: `Lu`, `Qr`, `Cholesky`, `SymmetricEigen`, `Solve`, `Determinant`, `Inverse`, `LeastSquares`, `ConditionEstimate`) |
| `Mathesis.LinearAlgebra.Exact` | Fraction-free Gaussian elimination (Bareiss), RREF with recorded row operations, exact inverse, nullspace, column space, rank, characteristic polynomial (Faddeev–LeVerrier, Berkowitz), Smith and Hermite normal forms |
| `Mathesis.LinearAlgebra.Decompositions` | LU/PLU, Cholesky, LDLᵀ, QR (Householder, Givens, Gram–Schmidt), eigen (symmetric Jacobi/QR, general Hessenberg-QR), Schur, SVD (Golub–Kahan), polar |
| `Mathesis.LinearAlgebra.Sparse` | `SparseMatrix<T>` (CSR/CSC), sparse products, conjugate gradient, GMRES, BiCGSTAB, Jacobi, Gauss–Seidel, SOR, incomplete LU/Cholesky preconditioners |
| `Mathesis.LinearAlgebra.Functions` | Matrix exponential (scaling and squaring with Padé), logarithm, square root, powers |

### `Mathesis.Discrete` package

| Namespace | Contents |
| --- | --- |
| `Mathesis.Discrete.Combinatorics` | Factorials, binomials, multinomials, permutations and combinations (with and without repetition), derangements, Stirling numbers (both kinds), Bell, Catalan, integer partitions, enumeration iterators |
| `Mathesis.Discrete.Probability` | Finite sample spaces, events, conditional probability, Bayes, discrete and continuous distributions (PMF/PDF/CDF/quantile, mean, variance) |
| `Mathesis.Discrete.Statistics` | Descriptive statistics (Welford online), quantiles, five-number summary, z-scores, correlation, least-squares regression; inferential tests in a later milestone |
| `Mathesis.Discrete.Finance` | Simple, compound and continuous interest, effective rates, annuities (ordinary and due), sinking funds, amortization schedules, perpetuities, NPV, IRR |
| `Mathesis.Discrete.LinearProgramming` | Standard-form models, graphical (2-variable) corner-point solver, simplex with tableau trace, two-phase method, duality |
| `Mathesis.Discrete.Markov` | Transition matrices, state evolution, steady state, regular and absorbing chains (fundamental matrix) |
| `Mathesis.Discrete.GameTheory` | Payoff matrices, saddle points, dominance, mixed strategies (2×2 closed form, general via LP) |
| `Mathesis.Discrete.Graphs` | Graph types, traversal, Euler/Hamilton checks, shortest paths (Dijkstra, Bellman–Ford, Floyd–Warshall), minimum spanning trees (Kruskal, Prim), topological sort, coloring heuristics, max flow |
| `Mathesis.Discrete.NumberTheory` | Divisibility, extended Euclid, Bézout, primality (deterministic Miller–Rabin for 64-bit), sieves, factorization (trial division, Pollard rho), totient, Möbius, CRT, modular exponentiation and inverses, Legendre/Jacobi symbols, continued fractions |
| `Mathesis.Discrete.Recurrences` | Linear recurrences with constant coefficients (closed form via characteristic roots), generating functions, numeric evaluation |

### `Mathesis.Symbolics` package

| Namespace | Contents |
| --- | --- |
| `Mathesis.Symbolics` | `Expr` and node types, `Symbol`, `Constant`, `Operator`, `Operators` (built-in registry), `Sort`, `Sym` (static builders), `ExprPath` |
| `Mathesis.Symbolics.Canonical` | `Normalizer` for the Raw → Structural → Canonical levels, ordering, automatic simplification |
| `Mathesis.Symbolics.Assumptions` | `AssumptionSet`, `Fact`, `TruthQuery`, sign and interval reasoning, natural domain computation |
| `Mathesis.Symbolics.Sets` | Set expressions: finite sets, intervals, unions, `ImageSet`, `ConditionSet`, number sets (ℕ, ℤ, ℚ, ℝ, ℂ), `SolutionSet` |
| `Mathesis.Symbolics.Patterns` | `Wild`, `SequenceWild`, `Pattern`, `Matcher` (syntactic, associative-commutative), `Bindings` |
| `Mathesis.Symbolics.Rewriting` | `Rule`, `RuleSet`, `RuleIndex` (discrimination tree), `Strategy` combinators, `RewriteEngine`, `Step`, `Derivation`, `IKnowledgeBase` |
| `Mathesis.Symbolics.Representations` | Conversions between `Expr` and `Polynomial<Expr>`/`SparsePolynomial<BigRational>`, `RationalFunction`, `PowerSeries`, `DenseMatrix<Expr>` |
| `Mathesis.Symbolics.Evaluation` | Exact evaluation, `Evaluate<T>`, `CompiledExpr<T>`, zero testing (`ZeroTest`) |
| `Mathesis.Symbolics.Parsing` | Linear-text parser (Pratt), LaTeX-subset parser, MathJSON reader, `ParseError` with spans |
| `Mathesis.Symbolics.Printing` | Text, LaTeX, Presentation and Content MathML, C# source, tree dump, MathJSON writer |
| `Mathesis.Symbolics.Serialization` | JSON contracts (System.Text.Json source generator) |

### `Mathesis.Knowledge` package

| Namespace | Contents |
| --- | --- |
| `Mathesis.Knowledge` | `KnowledgeBase`, `Entry` and kinds (`Definition`, `Axiom`, `Law`, `Theorem`, `Formula`, `Pattern`, `Method`, `Convention`), `EntryId`, `Domain`, `Reference` |
| `Mathesis.Knowledge.Loading` | `.mlaw` lexer/parser, validation, embedded-resource loader |
| `Mathesis.Knowledge.Catalog` | Generated typed handles: `Laws.Algebra.Exponents.ProductOfPowers`, `Theorems.Calculus.MeanValue` … |

### `Mathesis.Logic` package

| Namespace | Contents |
| --- | --- |
| `Mathesis.Logic` | Propositional and first-order helpers, truth tables, normal forms (NNF, CNF, DNF, prenex) |
| `Mathesis.Logic.Sat` | DPLL and CDCL SAT solver, Tseitin encoding |
| `Mathesis.Logic.Sets` | Set identities, relations (reflexive … equivalence, partial order), functions (injective/surjective/bijective) |
| `Mathesis.Logic.Proofs` | `Judgment`, `Theorem` (kernel-only constructor), `Kernel` inference rules, `Proof` trees, `ProofChecker` |
| `Mathesis.Logic.Tactics` | `Intro`, `Cases`, `Induction`, `Rewrite`, `Ring`, `Simp`, `LinearArith`, `Contradiction`, `Exact` |
| `Mathesis.Logic.Rendering` | Fitch-style, two-column, paragraph and LaTeX proof output |

### `Mathesis` package (engines and façade)

| Namespace | Contents |
| --- | --- |
| `Mathesis` | Façade static class `Cas` and the instance `MathEngine` (one per context) |
| `Mathesis.Simplification` | `Simplify` orchestrator, `IComplexityMeasure`, transform registry |
| `Mathesis.Algebra` | Expand, Factor (over ℤ, ℚ, ℚ(i), finite fields), Collect, Together, Apart (partial fractions), Cancel, Rationalize, CompleteSquare, polynomial long and synthetic division, radical simplification and denesting |
| `Mathesis.Trigonometry` | Exact values, trig simplify/expand/reduce, identity proving, trig equations, triangle solver, degree/radian/DMS conversion |
| `Mathesis.Functions` | Function analysis (domain, range, intercepts, symmetry, asymptotes, monotonicity, extrema, concavity), transformations, composition, inverse, piecewise, conics, parametric and polar curves, sequences |
| `Mathesis.Calculus` | Limits, derivatives, integrals, series, summation, multivariable calculus, vector calculus |
| `Mathesis.DifferentialEquations` | ODE classification and solvers, Laplace transforms, systems, series solutions, Fourier series, PDE templates |
| `Mathesis.Solving` | Equations, inequalities, systems, `Solve`/`SolveSet`, method selection |
| `Mathesis.LinearAlgebra.Symbolic` | Symbolic matrices with explained row reduction, determinants, eigen, diagonalization, Gram–Schmidt |
| `Mathesis.Explanation` | `ExplanationRenderer`, verbosity, curriculum filtering, templates and localization keys |

### `Mathesis.Extensions` package (optional)

| Namespace | Contents |
| --- | --- |
| `Mathesis.Extensions.DependencyInjection` | `services.AddMathesis(options => …)`, `IMathEngineFactory` |
| `Mathesis.Extensions.Logging` | `ILogger` sinks for derivations and budget events |
| `Mathesis.Extensions.AI` | `AIFunction` tools (simplify, solve, differentiate, integrate, evaluate, explain) for any `IChatClient` |

### `Mathesis.Validation` package (optional)

| Namespace | Contents |
| --- | --- |
| `Mathesis.Validation` | `System.ComponentModel.DataAnnotations` attributes that check mathematical input: the base `MathValidationAttribute`, `MathValidationResult` (code, span, suggestion), `MathValidationCode`, and `RationalNumberAttribute`, `ExactRangeAttribute`, `NonZeroAttribute`, `MathExpressionAttribute`, `MathEquationAttribute`, `PolynomialExpressionAttribute`, `MathMatrixAttribute` with the small enums their options use |

## Naming conventions

- **Avoid BCL collisions.** Never name a type `Math`, `Vector`, `Matrix`, `Vector<T>`, `Complex` (non-generic) or `Range`. Use `DenseVector<T>`, `DenseMatrix<T>`, `Complex<T>`, `Interval<T>`. The façade is `Cas`, not `Math`.
- **Mathematical names in full.** `Differentiate`, `Integrate`, `Determinant`, `CharacteristicPolynomial`; short aliases (`Diff`, `Det`) only on the `Sym` builder.
- **Verbs for engines, nouns for data.** `Factor(expr)` is an engine; `Factorization` is its result.
- **Catalog IDs are stable kebab-case dotted paths** (`alg.exp.product-of-powers`). Generated C# handles use PascalCase (`Laws.Algebra.Exponents.ProductOfPowers`). IDs never change after release; renamed entries keep their old ID as an alias.
- **Exactness in names.** `Solve` returns exact solutions; `NSolve` (numeric) and `Approximate` say so in the name.
- **Async only where I/O or long work exists.** Engines are synchronous with a `Budget`; `…Async` overloads exist only in `Mathesis.Extensions`.
- **Attributes end in `Attribute`** and never reuse a `System.ComponentModel.DataAnnotations` name (`Range`, `Required`, `Compare` …): `ExactRange`, not `Range`.

## Public API style

```csharp
using Mathesis;
using Mathesis.Symbolics;
using static Mathesis.Symbolics.Sym;

var x = Symbol("x");
Expr f = Sin(x) * Exp(2 * x);

var df = Cas.Differentiate(f, x);                 // Outcome<Expr>
var roots = Cas.Solve(Pow(x, 2) - 5 * x + 6, x);  // Outcome<SolutionSet>: {2, 3}
var id = Cas.Simplify(Pow(Sin(x), 2) + Pow(Cos(x), 2));
Console.WriteLine(id.Value);                      // 1
foreach (var step in id.Steps!.Flatten())
    Console.WriteLine(step.Explain());             // "Pythagorean identity: sin²x + cos²x = 1"

var ctx = MathContext.Default.Assume(x > 0);
var r = Cas.Simplify(Sqrt(Pow(x, 2)), ctx);       // x   (without the assumption: |x|)

var g = Expr.Parse("x^3 - 2x").Compile<double>(x);
double y = g(1.5);
```

Operator overloads on `Expr`: `+ - * /` and unary `-` build arithmetic; `< <= > >=` build relations; `& | !` build logical connectives; `==` and `!=` are structural equality (use `Eq(a, b)` to build an equation). `^` is **not** overloaded because C#'s `^` binds more loosely than `+`, so `x^2 + 1` would mean x^(2+1); use `Pow`.
