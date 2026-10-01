# D8 Finite Mathematics

Finite mathematics covers the discrete, applied mathematics of business, social and computer science: linear models, matrices in economics, linear programming, the mathematics of finance, counting, probability, statistics, Markov chains and game theory, together with the discrete foundations other domains lean on (number theory, graph theory, recurrences). Results are exact where the inputs are (rational probabilities, exact binomial coefficients) and money is computed in `decimal` and rounded only at display time.

- **Prefix:** `fin` (number theory under `fin.nt`) · **Course tag:** `FiniteMath` · **Completed in:** Milestone 6
- **Packages:** `Mathesis.Discrete` (all namespaces), `Mathesis.LinearAlgebra` (Leontief, Markov, LP tableaux), `Mathesis` (explained steps)

## Scope

Linear cost/revenue/profit models; Leontief input–output; linear programming (graphical and simplex methods, duality); simple, compound and continuous interest, annuities, sinking funds, amortization, NPV and IRR; sets and counting; probability, random variables and distributions; descriptive statistics and regression; Markov chains (regular and absorbing); matrix games; graph theory; elementary number theory; recurrence relations and generating functions.

## Types

| Type | Purpose |
| --- | --- |
| `SampleSpace<T>`, `Event<T>` | Finite sample spaces with exact (`BigRational`) probabilities |
| `IDiscreteDistribution`, `IContinuousDistribution` | PMF/PDF, CDF, quantile, mean, variance, sampling; implementations per distribution below |
| `DescriptiveStatistics` | Count, mean, median, mode(s), quartiles (selectable method), variance (population and sample), standard deviation, IQR, outliers |
| `LinearRegression` | Slope, intercept, r, r², residuals |
| `CashFlow`, `AnnuitySpec`, `AmortizationSchedule` | Finance inputs and outputs in `decimal` with explicit rounding policy |
| `LpModel`, `SimplexTableau`, `LpSolution` | Model, every tableau with pivot choices, and `Optimal`, `Unbounded` or `Infeasible` |
| `MarkovChain` | Transition matrix (row-stochastic), classification, steady state, absorption analysis |
| `MatrixGame` | Payoffs, saddle points, dominance reductions, optimal mixed strategies and value |
| `Graph<TVertex>`, `WeightedGraph<TVertex>` | Directed or undirected; algorithms below |

## Linear models and matrices in economics

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.lin.cost | formula | Linear cost | `C(x) = m*x + b` (b fixed cost, m marginal cost) | |
| fin.lin.revenue-profit | formula | Revenue and profit | `R(x) = p*x`, `P(x) = R(x) - C(x)` | |
| fin.lin.break-even | definition | Break-even point | x with `R(x) = C(x)` | |
| fin.lin.equilibrium | definition | Market equilibrium | Price and quantity where supply equals demand | |
| fin.lin.straight-line-depreciation | formula | Straight-line depreciation | `V(t) = C - (C - S)*t/n` | Cost C, salvage S, life n |
| fin.mat.leontief | theorem | Leontief input–output model | `X = A*X + D` gives `X = (I - A)^-1*D` | I − A invertible (e.g. column sums of A < 1) |

## Linear programming

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.lp.standard-max | definition | Standard maximum problem | Maximize `c·x` subject to `A*x <= b`, `x >= 0` | b ≥ 0 |
| fin.lp.fundamental | theorem | Fundamental theorem of linear programming | If an optimum exists it occurs at a corner point; a bounded, non-empty feasible region has both a maximum and a minimum | |
| fin.lp.corner-point | method | Corner-point method | Graph the region, find corners by solving pairs of boundary equations, evaluate the objective | Two variables |
| fin.lp.simplex | method | Simplex method | Pivot column: most negative indicator; pivot row: smallest non-negative ratio; stop when no indicator is negative | Standard maximum form |
| fin.lp.unbounded | theorem | Unboundedness test | A pivot column with no positive entries ⇒ the objective is unbounded | |
| fin.lp.bland | theorem | Bland's rule prevents cycling | Choose the lowest-index eligible entering and leaving variables | Degenerate problems |
| fin.lp.dual | definition | Dual problem | Max `c·x`, `A*x <= b`, `x >= 0` ↔ Min `b·y`, `A^T*y >= c`, `y >= 0` | |
| fin.lp.weak-duality | theorem | Weak duality | Any feasible x, y satisfy `c·x <= b·y` | |
| fin.lp.strong-duality | theorem | Strong duality (von Neumann) | If either problem has an optimum, both do and their optimal values are equal | |
| fin.lp.complementary-slackness | theorem | Complementary slackness | At optimality, `y_i*(b - A*x)_i = 0` and `x_j*(A^T*y - c)_j = 0` | |
| fin.lp.two-phase | method | Mixed constraints | Two-phase simplex (or big-M) for ≥ and = constraints | |
| fin.lp.shadow-price | definition | Shadow price | Optimal dual value y_i = rate of change of the optimum per unit of resource i | Non-degenerate optimum |

## Mathematics of finance

i = r/m is the rate per period and n = m·t the number of periods.

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.fin.simple-interest | formula | Simple interest | `I = P*r*t`, `A = P*(1 + r*t)` | |
| fin.fin.compound | formula | Compound interest | `A = P*(1 + r/m)^(m*t)` | |
| fin.fin.continuous | formula | Continuous compounding | `A = P*e^(r*t)` | |
| fin.fin.effective-rate | formula | Effective annual rate (APY) | `r_eff = (1 + r/m)^m - 1`; continuous `e^r - 1` | |
| fin.fin.present-value | formula | Present value of a lump sum | `PV = A*(1 + i)^(-n)` | |
| fin.fin.fv-annuity | formula | Future value of an ordinary annuity | `FV = PMT*((1 + i)^n - 1)/i` | i > 0 |
| fin.fin.fv-annuity-due | formula | Future value of an annuity due | `FV = PMT*((1 + i)^n - 1)/i*(1 + i)` | i > 0 |
| fin.fin.pv-annuity | formula | Present value of an ordinary annuity | `PV = PMT*(1 - (1 + i)^(-n))/i` | i > 0 |
| fin.fin.pv-annuity-due | formula | Present value of an annuity due | `PV = PMT*(1 - (1 + i)^(-n))/i*(1 + i)` | i > 0 |
| fin.fin.sinking-fund | formula | Sinking fund payment | `PMT = FV*i/((1 + i)^n - 1)` | i > 0 |
| fin.fin.amortization | formula | Amortized loan payment | `PMT = PV*i/(1 - (1 + i)^(-n))` | i > 0 |
| fin.fin.unpaid-balance | formula | Unpaid balance after k payments | `B(k) = PMT*(1 - (1 + i)^(-(n - k)))/i` | |
| fin.fin.schedule | method | Amortization schedule | Interest = balance·i (rounded to the cent); principal = PMT − interest; final payment adjusts for rounding | |
| fin.fin.perpetuity | formula | Perpetuity | `PV = PMT/i` | i > 0 |
| fin.fin.npv | formula | Net present value | `NPV = sum(CF_t/(1 + r)^t, t, 0, n)` | |
| fin.fin.irr | definition | Internal rate of return | r with NPV = 0 (found numerically; may be non-unique when cash flows change sign more than once) | |
| fin.fin.rule-of-72 | formula | Rule of 72 | Doubling time ≈ 72/(100·r) periods | Approximation for small r |
| conv.money-rounding | convention | Money rounding | Compute in `decimal`; round to cents half away from zero only when a value is displayed or posted to a schedule | |

## Sets and counting

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.count.inclusion-exclusion-2 | formula | Inclusion–exclusion (two sets) | `card(A ∪ B) = card(A) + card(B) - card(A ∩ B)` | Finite sets |
| fin.count.inclusion-exclusion-3 | formula | Inclusion–exclusion (three sets) | `card(A ∪ B ∪ C) = card(A) + card(B) + card(C) - card(A ∩ B) - card(A ∩ C) - card(B ∩ C) + card(A ∩ B ∩ C)` | |
| fin.count.inclusion-exclusion | theorem | General inclusion–exclusion | `card(union of A_i) = sum over non-empty S of (-1)^(card(S) + 1)*card(intersection over S)` | |
| fin.count.multiplication | theorem | Multiplication principle | A task in k stages with n_i ways each can be done in `n1*n2*…*nk` ways | |
| fin.count.addition | theorem | Addition principle | Disjoint alternatives add | |
| fin.count.permutations | formula | Permutations | `perm(n, r) = n!/(n - r)!` | 0 ≤ r ≤ n |
| fin.count.combinations | formula | Combinations | `binomial(n, r) = n!/(r!*(n - r)!)` | 0 ≤ r ≤ n |
| fin.count.with-repetition | formula | Sequences with repetition | `n^r` | |
| fin.count.distinguishable | formula | Distinguishable permutations | `n!/(n1!*n2!*…*nk!)` | n1 + … + nk = n |
| fin.count.stars-and-bars | formula | Combinations with repetition | `binomial(n + r - 1, r)` | |
| fin.count.circular | formula | Circular permutations | `(n - 1)!` | n ≥ 1 |
| fin.count.symmetry | law | Symmetry | `binomial(n, r) = binomial(n, n - r)` | |
| fin.count.pascal | law | Pascal's identity | `binomial(n, r) = binomial(n - 1, r - 1) + binomial(n - 1, r)` | 1 ≤ r ≤ n − 1 |
| fin.count.vandermonde | law | Vandermonde's identity | `sum(binomial(m, k)*binomial(n, r - k), k, 0, r) = binomial(m + n, r)` | |
| fin.count.hockey-stick | law | Hockey-stick identity | `sum(binomial(i, r), i, r, n) = binomial(n + 1, r + 1)` | n ≥ r |
| fin.count.row-sum | law | Row sum | `sum(binomial(n, k), k, 0, n) = 2^n` | |
| fin.count.alternating-row | law | Alternating row sum | `sum((-1)^k*binomial(n, k), k, 0, n) = 0` | n ≥ 1 |
| fin.count.weighted-row | law | Weighted row sum | `sum(k*binomial(n, k), k, 0, n) = n*2^(n - 1)` | n ≥ 1 |
| fin.count.sum-squares | law | Sum of squares of a row | `sum(binomial(n, k)^2, k, 0, n) = binomial(2n, n)` | |
| fin.count.derangements | formula | Derangements | `D(n) = n!*sum((-1)^k/k!, k, 0, n)`, `D(n) = (n - 1)*(D(n - 1) + D(n - 2))`, `D(n) = round(n!/e)` | n ≥ 1 for the last |
| fin.count.stirling2 | formula | Stirling numbers of the second kind | `S(n, k) = k*S(n - 1, k) + S(n - 1, k - 1)` | Partitions of n items into k non-empty blocks |
| fin.count.stirling1 | formula | Unsigned Stirling numbers of the first kind | `c(n, k) = (n - 1)*c(n - 1, k) + c(n - 1, k - 1)` | Permutations with k cycles |
| fin.count.bell | formula | Bell numbers | `B(n) = sum(S(n, k), k, 0, n)`, `B(n + 1) = sum(binomial(n, k)*B(k), k, 0, n)` | |
| fin.count.catalan | formula | Catalan numbers | `catalan(n) = binomial(2n, n)/(n + 1)`, `catalan(n + 1) = sum(catalan(i)*catalan(n - i), i, 0, n)` | |
| fin.count.functions | formula | Counting functions | All functions n-set → k-set: `k^n`; injections: `k!/(k - n)!`; surjections: `k!*S(n, k)` | |
| fin.count.subsets | formula | Number of subsets | `2^n` | |
| fin.count.stirling-approximation | theorem | Stirling's approximation | `n! ~ sqrt(2*pi*n)*(n/e)^n` | n → ∞ |

## Probability

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.prob.axioms | axiom | Kolmogorov axioms | `P(A) >= 0`; `P(S) = 1`; P of a countable disjoint union is the sum | |
| fin.prob.equally-likely | formula | Equally likely outcomes | `P(A) = card(A)/card(S)` | Finite S, equally likely |
| fin.prob.complement | law | Complement rule | `P(A^c) = 1 - P(A)` | |
| fin.prob.addition | law | Addition rule | `P(A ∪ B) = P(A) + P(B) - P(A ∩ B)` | |
| fin.prob.conditional | definition | Conditional probability | `P(A \| B) = P(A ∩ B)/P(B)` | P(B) > 0 |
| fin.prob.multiplication | law | Multiplication rule | `P(A ∩ B) = P(B)*P(A \| B)` | |
| fin.prob.independence | definition | Independent events | `P(A ∩ B) = P(A)*P(B)` | |
| fin.prob.total | theorem | Law of total probability | `P(A) = sum(P(A \| B_i)*P(B_i))` | B_i partition S |
| fin.prob.bayes | theorem | Bayes' theorem | `P(B_j \| A) = P(A \| B_j)*P(B_j)/sum(P(A \| B_i)*P(B_i))` | B_i partition S, P(A) > 0 |
| fin.prob.odds | formula | Odds | Odds in favor = `P(A)/(1 - P(A))` | |
| fin.prob.expectation | definition | Expected value | `E(X) = sum(x*p(x))` (continuous: `integrate(x*f(x), x, -oo, oo)`) | Converges absolutely |
| fin.prob.lotus | law | Law of the unconscious statistician | `E(g(X)) = sum(g(x)*p(x))` | |
| fin.prob.linearity | law | Linearity of expectation | `E(a*X + b*Y) = a*E(X) + b*E(Y)` | No independence needed |
| fin.prob.variance | definition | Variance | `Var(X) = E((X - μ)^2) = E(X^2) - μ^2` | |
| fin.prob.variance-affine | law | Variance of an affine map | `Var(a*X + b) = a^2*Var(X)` | |
| fin.prob.variance-sum | law | Variance of a sum | `Var(X + Y) = Var(X) + Var(Y) + 2Cov(X, Y)` | |
| fin.prob.covariance | definition | Covariance and correlation | `Cov(X, Y) = E(X*Y) - E(X)*E(Y)`, `ρ = Cov(X, Y)/(σ_X*σ_Y)`, with −1 ≤ ρ ≤ 1 | |
| fin.prob.independent-covariance | theorem | Independence implies zero covariance | Converse false | |
| fin.prob.markov-inequality | theorem | Markov's inequality | `P(X >= a) <= E(X)/a` | X ≥ 0, a > 0 |
| fin.prob.chebyshev | theorem | Chebyshev's inequality | `P(abs(X - μ) >= k*σ) <= 1/k^2` | k > 0 |
| fin.prob.lln | theorem | Law of large numbers | Sample means converge to μ | i.i.d., finite mean |
| fin.prob.clt | theorem | Central limit theorem | `(X̄ - μ)/(σ/sqrt(n))` converges in distribution to Normal(0, 1) | i.i.d., finite variance |
| fin.prob.normal-approx-binomial | method | Normal approximation to the binomial | Use Normal(np, np(1 − p)) with a continuity correction of ±0.5 | np ≥ 5 and n(1 − p) ≥ 5 (convention; some texts use 10) |
| fin.prob.fair-game | definition | Fair game | Expected net winnings are 0 | |

### Distributions

| ID | Distribution | PMF or PDF | Mean | Variance |
| --- | --- | --- | --- | --- |
| fin.dist.bernoulli | Bernoulli(p) | `p^x*(1 - p)^(1 - x)`, x ∈ {0, 1} | `p` | `p*(1 - p)` |
| fin.dist.binomial | Binomial(n, p) | `binomial(n, k)*p^k*(1 - p)^(n - k)` | `n*p` | `n*p*(1 - p)` |
| fin.dist.geometric | Geometric(p), trials to first success | `(1 - p)^(k - 1)*p`, k ≥ 1 | `1/p` | `(1 - p)/p^2` |
| fin.dist.hypergeometric | Hypergeometric(N, K, n) | `binomial(K, k)*binomial(N - K, n - k)/binomial(N, n)` | `n*K/N` | `n*(K/N)*(1 - K/N)*(N - n)/(N - 1)` |
| fin.dist.poisson | Poisson(λ) | `λ^k*e^(-λ)/k!` | `λ` | `λ` |
| fin.dist.discrete-uniform | Uniform{a, …, b} | `1/(b - a + 1)` | `(a + b)/2` | `((b - a + 1)^2 - 1)/12` |
| fin.dist.uniform | Uniform(a, b) | `1/(b - a)` on [a, b] | `(a + b)/2` | `(b - a)^2/12` |
| fin.dist.normal | Normal(μ, σ²) | `e^(-(x - μ)^2/(2σ^2))/(σ*sqrt(2*pi))` | `μ` | `σ^2` |
| fin.dist.exponential | Exponential(λ) | `λ*e^(-λ*x)`, x ≥ 0 | `1/λ` | `1/λ^2` |
| fin.dist.standardize | Standardization | `z = (x - μ)/σ` | | |
| fin.dist.empirical-rule | Empirical rule | About 68%, 95% and 99.7% of a normal distribution lie within 1, 2 and 3 standard deviations of the mean | | |
| fin.dist.memoryless | Memoryless property | `P(X > s + t \| X > s) = P(X > t)` | Exponential and geometric only | |

## Statistics

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.stat.mean | formula | Mean | `x̄ = sum(x_i)/n` | |
| fin.stat.median | definition | Median | Middle value of the sorted data (average of the two middle values when n is even) | |
| fin.stat.mode | definition | Mode | Most frequent value(s) | |
| fin.stat.pop-variance | formula | Population variance | `σ^2 = sum((x_i - μ)^2)/N` | |
| fin.stat.sample-variance | formula | Sample variance | `s^2 = sum((x_i - x̄)^2)/(n - 1)` | n ≥ 2 |
| fin.stat.z-score | formula | z-score | `z = (x - x̄)/s` (or μ, σ for populations) | |
| fin.stat.quartiles | definition | Quartiles and IQR | Q1, Q2, Q3; `IQR = Q3 - Q1` | Method per `conv.quantile-method` |
| conv.quantile-method | convention | Quantile method | Default: linear interpolation between order statistics (Hyndman–Fan type 7, as in Excel `PERCENTILE.INC`); the textbook median-of-halves method is selectable | |
| fin.stat.outlier | method | Outlier rule | Values below Q1 − 1.5·IQR or above Q3 + 1.5·IQR | |
| fin.stat.five-number | definition | Five-number summary | min, Q1, median, Q3, max | |
| fin.stat.cv | formula | Coefficient of variation | `CV = s/x̄` | x̄ ≠ 0 |
| fin.stat.weighted-mean | formula | Weighted mean | `sum(w_i*x_i)/sum(w_i)` | |
| fin.stat.correlation | formula | Correlation coefficient | `r = sum((x_i - x̄)*(y_i - ȳ))/sqrt(sum((x_i - x̄)^2)*sum((y_i - ȳ)^2))` | Non-constant data |
| fin.stat.regression | formula | Least-squares line | `ŷ = a + b*x`, `b = r*s_y/s_x = S_xy/S_xx`, `a = ȳ - b*x̄` | |
| fin.stat.r-squared | formula | Coefficient of determination | `r^2` = fraction of variance in y explained by the line | |

## Markov chains

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.markov.transition | definition | Transition matrix | `P_(i,j) = P(next state j \| current state i)`; rows sum to 1 | |
| fin.markov.evolution | formula | State evolution | `x(k + 1) = x(k)*P`, `x(k) = x(0)*P^k` (row vectors) | |
| fin.markov.chapman-kolmogorov | theorem | Chapman–Kolmogorov | `P^(m + n) = P^m*P^n` | |
| fin.markov.stationary | definition | Stationary distribution | `π*P = π`, `sum(π_i) = 1` | |
| fin.markov.regular | theorem | Regular chains | If some power of P has all positive entries, there is a unique stationary π and `x(k) → π` from any start | |
| fin.markov.absorbing | definition | Absorbing chain | Has an absorbing state (P_(i,i) = 1) reachable from every state | |
| fin.markov.canonical | definition | Canonical form | Order states absorbing first: `P = [[I, 0], [R, Q]]` | |
| fin.markov.fundamental | formula | Fundamental matrix | `N = (I - Q)^-1`; N_(i,j) = expected visits to j starting from i | |
| fin.markov.time-to-absorption | formula | Expected steps to absorption | `t = N*1` | |
| fin.markov.absorption-probabilities | formula | Absorption probabilities | `B = N*R` | |

## Game theory

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.game.payoff | definition | Payoff matrix | Entry a_(i,j) is the row player's gain when row i meets column j | Zero-sum |
| fin.game.saddle | theorem | Strictly determined games | An entry that is the minimum of its row and the maximum of its column is a saddle point; its value is the game value | |
| fin.game.dominance | method | Dominance | Delete rows dominated by another row (≤ entrywise) and columns dominating another column (≥) | |
| fin.game.expected | formula | Expected payoff | `E = p*A*q^T` | Mixed strategies p, q |
| fin.game.2x2 | formula | Optimal mixed strategies (2×2) | For `[[a, b], [c, d]]` without a saddle: `p1 = (d - c)/(a + d - b - c)`, `q1 = (d - b)/(a + d - b - c)`, `v = (a*d - b*c)/(a + d - b - c)` | Not strictly determined |
| fin.game.minimax | theorem | Minimax theorem (von Neumann) | Every finite zero-sum game has a value and optimal mixed strategies | |
| fin.game.lp | method | Solving games by linear programming | Add a constant to make payoffs positive, solve the LP pair, convert back | |

## Graph theory

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.graph.handshake | theorem | Handshake lemma | `sum of degrees = 2*card(E)`; the number of odd-degree vertices is even | Undirected |
| fin.graph.complete | formula | Edges of a complete graph | `card(E(K_n)) = n*(n - 1)/2` | |
| fin.graph.euler-circuit | theorem | Euler circuits | A connected graph has an Euler circuit iff every vertex has even degree | |
| fin.graph.euler-path | theorem | Euler paths | A connected graph has an Euler path iff it has exactly 0 or 2 odd-degree vertices | |
| fin.graph.dirac | theorem | Dirac's theorem | Simple graph, n ≥ 3, every degree ≥ n/2 ⇒ Hamiltonian | |
| fin.graph.ore | theorem | Ore's theorem | deg u + deg v ≥ n for every non-adjacent pair ⇒ Hamiltonian | n ≥ 3 |
| fin.graph.tree | theorem | Trees | A connected acyclic graph has `card(E) = card(V) - 1` and a unique path between any two vertices | |
| fin.graph.cayley | formula | Cayley's formula | `n^(n - 2)` labeled trees on n vertices | n ≥ 2 |
| fin.graph.euler-formula | theorem | Euler's formula for planar graphs | `V - E + F = 2` | Connected planar |
| fin.graph.planar-bound | theorem | Edge bound for planar graphs | `E <= 3V - 6` | Simple planar, V ≥ 3 |
| fin.graph.kuratowski | theorem | Kuratowski's theorem | Planar iff no subdivision of K5 or K3,3 | |
| fin.graph.four-color | theorem | Four color theorem | Every planar graph is 4-colorable | Cited, computer-assisted proof |
| fin.graph.greedy-coloring | theorem | Greedy coloring bound | `χ(G) <= Δ(G) + 1` | |
| fin.graph.bipartite | theorem | Bipartite characterization | Bipartite iff no odd cycle | |
| fin.graph.hall | theorem | Hall's marriage theorem | A bipartite graph has a matching saturating X iff `card(N(S)) >= card(S)` for all S ⊆ X | |
| fin.graph.walks | theorem | Counting walks | `(A^k)_(i,j)` = number of walks of length k from i to j | A the adjacency matrix |
| fin.graph.dijkstra | method | Dijkstra's algorithm | Shortest paths from a source | Non-negative weights |
| fin.graph.bellman-ford | method | Bellman–Ford algorithm | Shortest paths; detects negative cycles | |
| fin.graph.floyd-warshall | method | Floyd–Warshall algorithm | All-pairs shortest paths | No negative cycles |
| fin.graph.kruskal-prim | method | Minimum spanning trees | Kruskal (sorted edges with union–find) and Prim (grow from a vertex), both justified by the cut property | Connected, weighted |
| fin.graph.max-flow-min-cut | theorem | Max-flow min-cut theorem | Maximum flow value = minimum cut capacity (Edmonds–Karp to compute) | |
| fin.graph.topological | theorem | Topological order | Exists iff the directed graph is acyclic | |

## Number theory

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.nt.divides | definition | Divisibility | `divides(a, b) <=> exists k in Z: b = a*k` | |
| fin.nt.division-algorithm | theorem | Division algorithm | `a = b*q + r` with `0 <= r < abs(b)`, q and r unique | b ≠ 0 |
| fin.nt.gcd-euclid | theorem | Euclid's GCD step | `gcd(a, b) = gcd(b, a mod b)` | b ≠ 0 |
| fin.nt.bezout | theorem | Bézout's identity | `exists x, y in Z: a*x + b*y = gcd(a, b)` (found by the extended Euclidean algorithm) | a, b not both 0 |
| fin.nt.gcd-lcm | law | GCD and LCM | `gcd(a, b)*lcm(a, b) = abs(a*b)` | |
| fin.nt.euclid-lemma | theorem | Euclid's lemma | `p` prime, `divides(p, a*b)` ⇒ `divides(p, a)` or `divides(p, b)` | |
| fin.nt.fta | theorem | Fundamental theorem of arithmetic | Every integer n ≥ 2 is a product of primes, unique up to order | |
| fin.nt.infinitely-many-primes | theorem | Infinitely many primes | See `logic.example.infinitely-many-primes` | |
| fin.nt.pnt | theorem | Prime number theorem | `primePi(x) ~ x/ln(x)` | x → ∞ |
| fin.nt.divisibility-rules | pattern | Divisibility rules | 2: last digit even; 3 and 9: digit sum; 4: last two digits; 5: last digit 0 or 5; 8: last three digits; 11: alternating digit sum | Base 10 |
| fin.nt.congruence | definition | Congruence | `a ≡ b (mod n) <=> divides(n, a - b)` | n ≥ 1 |
| fin.nt.congruence-arithmetic | theorem | Congruences respect + and × | a ≡ b and c ≡ d (mod n) ⇒ a + c ≡ b + d and a·c ≡ b·d; aᵏ ≡ bᵏ | |
| fin.nt.cancellation | theorem | Cancellation | `a*c ≡ b*c (mod n)` and `gcd(c, n) = 1` ⇒ `a ≡ b (mod n)` | |
| fin.nt.inverse | theorem | Modular inverse | a has an inverse mod n ⇔ `gcd(a, n) = 1` | |
| fin.nt.linear-congruence | theorem | Linear congruences | `a*x ≡ b (mod n)` is solvable ⇔ `divides(gcd(a, n), b)`; then it has gcd(a, n) solutions mod n | |
| fin.nt.crt | theorem | Chinese remainder theorem | Pairwise coprime moduli: the system x ≡ a_i (mod n_i) has a unique solution mod ∏n_i | |
| fin.nt.fermat | theorem | Fermat's little theorem | `a^(p - 1) ≡ 1 (mod p)` | p prime, p ∤ a |
| fin.nt.euler | theorem | Euler's theorem | `a^totient(n) ≡ 1 (mod n)` | gcd(a, n) = 1 |
| fin.nt.totient | formula | Euler's totient | `totient(n) = n*product(1 - 1/p)` over primes p dividing n; multiplicative for coprime arguments; `sum(totient(d)) over d dividing n = n` | n ≥ 1 |
| fin.nt.wilson | theorem | Wilson's theorem | `(p - 1)! ≡ -1 (mod p)` ⇔ p is prime | p ≥ 2 |
| fin.nt.divisor-count | formula | Number and sum of divisors | For `n = product(p_i^e_i)`: `τ(n) = product(e_i + 1)`, `σ(n) = product((p_i^(e_i + 1) - 1)/(p_i - 1))` | |
| fin.nt.mobius-inversion | theorem | Möbius inversion | `g(n) = sum(f(d)) over d dividing n` ⇔ `f(n) = sum(mobius(d)*g(n/d))` | |
| fin.nt.euler-criterion | theorem | Euler's criterion | `a^((p - 1)/2) ≡ legendre(a, p) (mod p)` | p odd prime, p ∤ a |
| fin.nt.quadratic-reciprocity | theorem | Quadratic reciprocity | `legendre(p, q)*legendre(q, p) = (-1)^((p - 1)/2*(q - 1)/2)` | p, q distinct odd primes |
| fin.nt.linear-diophantine | theorem | Linear Diophantine equations | `a*x + b*y = c` is solvable ⇔ `divides(gcd(a, b), c)`; all solutions `x = x0 + (b/d)*t`, `y = y0 - (a/d)*t` | d = gcd(a, b) |
| fin.nt.pythagorean-triples | formula | Pythagorean triples | `(k*(m^2 - n^2), 2k*m*n, k*(m^2 + n^2))` gives all triples | m > n > 0, gcd(m, n) = 1, m − n odd |
| fin.nt.modpow | method | Modular exponentiation | Square-and-multiply in O(log e) multiplications | |
| fin.nt.miller-rabin | method | Deterministic Miller–Rabin | Bases {2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37} decide primality for all n < 3.3·10²⁴ (hence all 64-bit n) | |
| fin.nt.rsa | theorem | RSA correctness | `(m^e)^d ≡ m (mod n)` | n = pq, e·d ≡ 1 (mod φ(n)) |

## Recurrences and generating functions

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| fin.rec.characteristic | method | Linear homogeneous recurrences | For `a(n) = c1*a(n - 1) + … + ck*a(n - k)` solve `r^k = c1*r^(k - 1) + … + ck`; distinct roots give `sum(α_i*r_i^n)`; a root of multiplicity m contributes `(β0 + β1*n + … + β(m-1)*n^(m-1))*r^n` | Constant coefficients |
| fin.rec.nonhomogeneous | method | Non-homogeneous recurrences | General solution = homogeneous + particular, with trial forms like undetermined coefficients | |
| fin.rec.binet | formula | Binet's formula | `F(n) = (φ^n - ψ^n)/sqrt(5)` with `φ = (1 + sqrt(5))/2`, `ψ = (1 - sqrt(5))/2` | |
| fin.rec.ogf | definition | Ordinary generating function | `G(x) = sum(a(n)*x^n, n, 0, oo)` (formal power series) | |
| fin.rec.ogf-geometric | law | Geometric generating function | `1/(1 - x) = sum(x^n, n, 0, oo)` | Formal |
| fin.rec.ogf-negative-binomial | law | Negative binomial series | `1/(1 - x)^k = sum(binomial(n + k - 1, k - 1)*x^n, n, 0, oo)` | k ≥ 1 |
| fin.rec.egf | definition | Exponential generating function | `sum(a(n)*x^n/n!, n, 0, oo)` | |
| fin.rec.master | theorem | Master theorem | `T(n) = a*T(n/b) + Θ(n^d)`: Θ(n^d) if d > log_b a; Θ(n^d·log n) if d = log_b a; Θ(n^(log_b a)) if d < log_b a | a ≥ 1, b > 1 |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Counting problems with the formula choice explained (order matters? repetition allowed?) | Committees of 3 from 10: `binomial(10, 3) = 120` | 6 |
| Exact probability in finite sample spaces; conditional probability and Bayes with tree diagrams | Test with 1% prevalence, 99% sensitivity, 5% false positives: P(disease ∣ positive) = 1/6 | 6 |
| Distributions: PMF/PDF, CDF, quantiles, mean, variance; normal approximations | | 6 |
| Descriptive statistics, box-plot data, regression line with r and r² | | 6 |
| Finance: every formula solvable for any variable; amortization schedules | $200,000 for 30 years at 6% compounded monthly: payment $1,199.10 | 6 |
| Linear programming with corner points (two variables) or simplex tableaux; duality | | 6 |
| Markov chains: k-step distributions, steady state, absorbing-chain analysis | | 6 |
| Matrix games: saddle points, dominance, optimal mixed strategies | | 6 |
| Graph algorithms with step traces | Dijkstra on a weighted graph | 6 |
| Number theory: gcd with Bézout coefficients, modular inverses, CRT, primality, factorization | `17^-1 mod 3120 = 2753` | 6 |
| Solve linear recurrences in closed form | `a(n) = 5a(n - 1) - 6a(n - 2)` → `α*2^n + β*3^n` | 6 |
