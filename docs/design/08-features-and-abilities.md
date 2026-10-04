# 08 Features and abilities

This is the public surface: what a caller can ask Mathesis to do, how the call looks, and in which milestone it arrives. Domain docs list the mathematics behind each ability in detail.

## Ability matrix

Cells show the milestone (see `10-roadmap.md`) in which the ability is complete for that domain; "—" means it does not apply.

| Domain | Represent and parse | Evaluate (exact / numeric) | Transform and simplify | Solve | Calculus operations | Explain steps | Prove | Self-verify |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Algebra | 1 | 1 | 1 (factor over ℚ: 2) | 1 (higher degree, systems: 2) | — | 1 | 3 | 1 |
| Trigonometry | 1 | 1 (exact values: 2) | 1 | 2 | 1 | 1 | 3 | 1 |
| Proofs & Logic | 1 (syntax), 3 | 3 (truth tables) | 3 (normal forms) | 3 (SAT) | — | 3 | 3 | 3 |
| Pre-Calculus | 1 | 1 | 2 | 2 | — | 2 | 3 | 2 |
| Calculus | 1 | 1 | 1 | 1 | 1 (limits, series, multivariable: 4) | 1 | 4 | 1 |
| Differential Equations | 1 | 1 (numeric ODEs) | — | 4 | 4 | 4 | — | 4 |
| Linear Algebra | 1 | 1 | 1 | 1 (exact), 5 (full) | — | 1 (RREF), 5 | 5 | 1 |
| Finite Mathematics | 6 | 6 | 6 | 6 (LP, Markov, games) | — | 6 | 6 | 6 |
| Numerical Analysis | — | 1 (core), 7 (full) | — | 1 (roots), 7 | 1 (quadrature, ODEs), 7 | 7 (error analysis) | — | 1 |

## Façade

`Cas` is a static façade over a default `MathEngine`; every method has an overload taking a `MathContext`. All return `Outcome<T>` unless they are pure numeric routines.

### Parsing and output

| Ability | Call | Milestone |
| --- | --- | --- |
| Parse linear text, LaTeX, MathJSON | `Expr.Parse(text)`, `Expr.ParseLatex(tex)`, `Expr.FromMathJson(json)` | 1 (MathJSON: 8) |
| Build with C# | `Sym.Symbol("x")`, operators, `Sym.Sin(x)`, `Sym.Integral(f, x, 0, 1)` | 1 |
| Print | `ToString()`, `ToLatex()`, `ToMathML()`, `ToCSharp()`, `ToMathJson()`, `DumpTree()` | 1 (MathML, MathJSON: 8) |
| Sort-check | `Cas.CheckSorts(expr)` | 1 |
| Serialize | `JsonSerializer` with `MathesisJsonContext` | 1 |

### Evaluation

| Ability | Call | Milestone |
| --- | --- | --- |
| Exact evaluation and substitution | `Cas.Evaluate(expr, bindings)`, `expr.Substitute(x, 2)` | 1 |
| Numeric value | `Cas.N(expr, digits: 15)` | 1 (arbitrary digits: 7) |
| Compile to a delegate | `expr.Compile<double>(x)`, `expr.Compile<Complex<double>>(z)`, `expr.Compile<Interval<double>>(x)` | 1 |
| Value and derivative together | `expr.Compile<Dual<double>>(x)` | 1 |
| Tabulate and sample for plotting | `Cas.Sample(f, x, a, b, adaptive: true)` with discontinuity and asymptote detection | 2 |

### Algebraic transforms

| Ability | Call | Milestone |
| --- | --- | --- |
| Simplify | `Cas.Simplify(expr)` with optional `IComplexityMeasure` | 1 |
| Expand / factor | `Cas.Expand(expr)`, `Cas.Factor(expr, over: Field.Rationals)` | 1 / 2 |
| Collect, together, apart, cancel | `Cas.Collect(expr, x)`, `Cas.Together`, `Cas.Apart(expr, x)`, `Cas.Cancel` | 1 |
| Rationalize denominator, simplify radicals | `Cas.Rationalize(expr)`, `Cas.RadicalSimplify(expr)` | 1 / 2 |
| Complete the square, vertex form | `Cas.CompleteSquare(expr, x)` | 1 |
| Polynomial division (long or synthetic) | `Cas.Divide(p, q, x, style: DivisionStyle.Synthetic)` | 1 |
| Log and exponent forms | `Cas.LogCombine`, `Cas.LogExpand`, `Cas.PowerSimplify` | 1 |
| Trig forms | `Cas.TrigSimplify`, `Cas.TrigExpand`, `Cas.TrigReduce`, `Cas.TrigToExp`, `Cas.ExpToTrig` | 1 |
| Apply a specific law | `Cas.Apply(Laws.Trigonometry.Sum.SinOfSum, expr, at: path)` | 1 |

### Solving

| Ability | Call | Milestone |
| --- | --- | --- |
| Equations | `Cas.Solve(eq, x, method: SolveMethod.Auto)` → `SolutionSet` | 1 |
| Choose the method | `SolveMethod.Factoring`, `QuadraticFormula`, `CompletingTheSquare`, `Substitution`, `Elimination`, `Matrices`, `CramersRule`, `Graphing` | 1–2 |
| Inequalities | `Cas.Solve(Pow(x, 2) - 4 > 0, x)` → interval union | 2 |
| Systems | `Cas.Solve([eq1, eq2], [x, y])` | 1 (linear), 2 (polynomial) |
| Numeric roots | `Cas.NSolve(eq, x, interval)` | 1 |
| Formulas | `Formulas.Finance.CompoundInterest.SolveFor("t", values)` | 2 |
| Recurrences and Diophantine | `Cas.SolveRecurrence`, `Cas.SolveDiophantine` | 6 |

### Functions (Pre-Calculus)

| Ability | Call | Milestone |
| --- | --- | --- |
| Domain and range | `Cas.Domain(f, x)`, `Cas.Range(f, x)` | 2 |
| Full analysis | `Cas.Analyze(f, x)` → intercepts, symmetry, asymptotes, intervals of increase/decrease, extrema, concavity, inflection points, end behavior | 2 (calculus parts: 1) |
| Inverse and composition | `Cas.Inverse(f, x)`, `Cas.Compose(f, g)` | 2 |
| Transformations | `Cas.DescribeTransformation(parent, f)` → shifts, stretches, reflections | 2 |
| Conics | `Cas.ClassifyConic(eq)` → standard form, center, vertices, foci, directrix, eccentricity, asymptotes | 2 |
| Polar, parametric, complex forms | `Cas.ToPolar`, `Cas.EliminateParameter`, `Cas.ToPolarForm(z)`, `Cas.Roots(z, n)` | 2 |

### Calculus

| Ability | Call | Milestone |
| --- | --- | --- |
| Limits | `Cas.Limit(f, x, a, Direction.Both)` | 1 (complete: 4) |
| Derivatives | `Cas.Differentiate(f, x, order)`, `Cas.ImplicitDerivative(eq, y, x)` | 1 |
| Integrals | `Cas.Integrate(f, x)`, `Cas.Integrate(f, x, a, b)`, `method:` hint (substitution, parts, partial fractions, trig substitution) | 1 (rational complete: 2; Risch parts: 4) |
| Series | `Cas.Taylor(f, x, a, n)`, `Cas.Series(f, x, a, n)` | 1 |
| Sums | `Cas.Sum(f, k, a, b)`, convergence `Cas.Converges(series)` | 2 / 4 |
| Applications | tangent lines, linearization, related rates, optimization, area, volume, arc length, average value | 2–4 |
| Multivariable and vector calculus | partial derivatives, gradient, Hessian, Lagrange multipliers, multiple integrals, line and surface integrals, Green/Stokes/divergence | 4 |

### Differential equations

| Ability | Call | Milestone |
| --- | --- | --- |
| Classify | `Ode.Classify(eq, y, x)` → order, linearity, type list | 4 |
| Solve symbolically | `Ode.Solve(eq, y, x, ics)` with steps | 4 |
| Laplace transforms | `Cas.Laplace(f, t, s)`, `Cas.InverseLaplace(F, s, t)` | 4 |
| Systems and phase portraits | `Ode.SolveSystem`, `Ode.Equilibria`, `Ode.Classify(A)` (node, saddle, spiral, center), `Ode.VectorField(...)` | 4 |
| Numeric | `OdeSolvers.DormandPrince(...)`, `OdeSolvers.Bdf(...)` | 1 / 7 |

### Linear algebra

| Ability | Call | Milestone |
| --- | --- | --- |
| Row reduce with steps | `Cas.RowReduce(A)` → RREF, pivots, row operations | 1 |
| Determinant, inverse, rank, nullspace, column space | `Cas.Determinant(A, method: Cofactor)`, `Cas.Inverse(A)`, … | 1 |
| Eigen and diagonalization | `Cas.Eigen(A)`, `Cas.Diagonalize(A)`, `Cas.JordanForm(A)` | 1 / 5 |
| Orthogonality | `Cas.GramSchmidt(vectors)`, `Cas.QR(A)`, `Cas.Project(u, subspace)`, `Cas.LeastSquares(A, b)` | 5 |
| Numeric decompositions | `Lu`, `Qr`, `Cholesky`, `SymmetricEigen`, `Svd` | 1 / 7 |

### Finite mathematics

| Ability | Call | Milestone |
| --- | --- | --- |
| Counting | `Combinatorics.Permutations(n, r)`, `Combinations`, explained formula choice | 6 |
| Probability | `Probability.Conditional(…)`, `Bayes(…)`, distributions with PMF/PDF/CDF/quantile | 6 |
| Statistics | `Describe(data)`, `LinearRegression(xs, ys)` | 6 |
| Finance | `Finance.FutureValue(…)`, `AmortizationSchedule(…)` | 6 |
| Linear programming | `Lp.Maximize(objective, constraints)` with tableau steps | 6 |
| Markov chains and games | `MarkovChain.SteadyState()`, `Game.Solve(payoffs)` | 6 |
| Number theory | `NumberTheory.IsPrime`, `Factor`, `ModInverse`, `Crt` | 6 |

### Proofs and logic

| Ability | Call | Milestone |
| --- | --- | --- |
| Truth tables, normal forms, satisfiability | `Logic.TruthTable(p)`, `Logic.ToCnf(p)`, `Sat.Solve(cnf)` | 3 |
| Check equivalences | `Logic.AreEquivalent(p, q)` with a derivation | 3 |
| Prove identities | `Cas.ProveIdentity(lhs, rhs)` → two-column proof | 3 |
| Induction | `Prover.ByInduction(statement, n)` for sums, divisibility, inequalities | 3 |
| Check a user's proof | `ProofChecker.Check(proof)` with the failing step and reason | 3 |
| Find a counterexample | `Cas.FindCounterexample(claim)` | 3 |

### Explanations

| Ability | Call | Milestone |
| --- | --- | --- |
| Steps for any result | `outcome.Steps.Render(Format.Markdown, Verbosity.Standard)` | 1 |
| Restrict methods to a level | `MathContext.Default with { Level = CurriculumLevel.PreCalculus }` | 1 |
| Cite the law at each step | `step.Entry` → catalog entry with name, statement, explanation | 1 |
| Look up the catalog | `Cas.Find("difference of squares")`, `Cas.Get(id)`, `Cas.ByDomain("trig.sum")` | 1 |

### Integration points

| Ability | Package | Milestone |
| --- | --- | --- |
| Dependency injection and options | `Mathesis.Extensions` (`services.AddMathesis()`) | 8 |
| AI tools for `IChatClient` | `Mathesis.Extensions.AI` | 8 |
| Plotting data for MAUI apps | `Cas.Sample`, `Ode.VectorField` (rendering stays in the app) | 2 / 4 |
