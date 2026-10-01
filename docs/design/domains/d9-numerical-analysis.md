# D9 Numerical Analysis

Numerical analysis supplies every approximate answer Mathesis gives and, just as important, the error estimate attached to it. Algorithms are generic over `IFloatingPointIeee754<T>` (so `float`, `double`, `Half` and later `BigFloat` all work), return result records instead of throwing on non-convergence, and carry the theorems that say when they converge and how fast.

- **Prefix:** `num` · **Course tag:** `NumericalAnalysis` · **Completed in:** Milestone 7 (core in Milestone 1)
- **Packages:** `Mathesis.Numerics`, `Mathesis.LinearAlgebra`, `Mathesis.Core` (`Interval<T>`, `Dual<T>`, `BigFloat`)

## Scope

Floating-point arithmetic and error analysis; root finding; interpolation and approximation; numerical differentiation and automatic differentiation; numerical integration; ODE initial and boundary value problems; numerical linear algebra and eigenvalue algorithms; nonlinear systems and optimization; the FFT; evaluation of special functions; random numbers and Monte Carlo; finite-difference PDE methods; arbitrary precision and validated (interval) numerics.

## Types

| Type | Purpose |
| --- | --- |
| `RootResult<T>` | Root, iterations, function evaluations, estimated error, `Converged`, termination reason |
| `QuadratureResult<T>` | Value, error estimate, evaluations, subintervals, warnings (suspected singularity, round-off limited) |
| `OdeSolution<T>` | Accepted steps, dense-output interpolant, events located, step statistics, stiffness detected |
| `Interpolant<T>` | Evaluate, derivative, integral; Lebesgue-constant estimate |
| `ChebyshevSeries<T>` | Coefficients on [a, b], truncation error estimate, arithmetic, roots |
| `OptimizationResult<T>` | Minimizer, minimum, gradient norm, iterations, `Converged` |
| `ConditionReport` | Condition number estimate, backward error, digits likely lost |

## Floating-point arithmetic and error

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.fp.binary64 | definition | IEEE 754 binary64 | 1 sign bit, 11 exponent bits, 52 fraction bits; about 15–17 significant decimal digits | `double` |
| num.fp.epsilon | definition | Machine epsilon | `ε = 2^-52 ≈ 2.22e-16` (gap between 1 and the next double) | binary64 |
| num.fp.unit-roundoff | definition | Unit roundoff | `u = 2^-53 ≈ 1.11e-16` | Round to nearest |
| num.fp.standard-model | axiom | Standard model of arithmetic | `fl(x op y) = (x op y)*(1 + δ)` with `abs(δ) <= u` | No overflow or underflow |
| num.fp.errors | definition | Absolute and relative error | `abs(x̂ - x)` and `abs(x̂ - x)/abs(x)` | x ≠ 0 for relative |
| num.fp.cancellation | theorem | Catastrophic cancellation | Subtracting nearly equal approximations amplifies their relative errors by about `abs(x)/abs(x - y)` | |
| num.fp.sterbenz | theorem | Sterbenz lemma | `y/2 <= x <= 2y` ⇒ `x - y` is computed exactly | Same format, no underflow |
| num.fp.two-sum | theorem | Error-free addition (TwoSum) | `a + b = s + e` exactly, where `s = fl(a + b)` and e is computed with 6 flops | Round to nearest |
| num.fp.two-product | theorem | Error-free multiplication | `a*b = p + e` exactly with `p = fl(a*b)`, `e = fma(a, b, -p)` | FMA available |
| num.fp.condition-function | definition | Condition number of a function | `κ_f(x) = abs(x*f'(x)/f(x))` | |
| num.fp.forward-backward | theorem | Forward error ≲ condition × backward error | | First order |
| num.fp.stable-quadratic | method | Stable quadratic formula | `q = -(b + sign(b)*sqrt(b^2 - 4a*c))/2`, `x1 = q/a`, `x2 = c/q` | Real roots, b ≠ 0 |
| num.fp.kahan | theorem | Compensated (Kahan) summation | Error bound `(2u + O(n*u^2))*sum(abs(x_i))`, versus `(n - 1)*u*sum(abs(x_i))` for naive summation | |
| num.fp.pairwise | theorem | Pairwise summation | Error grows like `u*log2(n)` | |
| num.fp.horner | method | Horner's rule | Evaluate `p(x)` with n multiplications and n additions; backward stable | |
| num.fp.hypot | method | Overflow-safe hypotenuse | Scale by `max(abs(x), abs(y))` before squaring | |
| num.fp.expm1-log1p | method | Accurate exp(x) − 1 and ln(1 + x) | Dedicated algorithms for small x; the BCL's `ExpM1`/`LogP1` are tested for accuracy before use and replaced if they are the naive formulas | abs(x) ≪ 1 |

## Root finding

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.root.bisection | theorem | Bisection | Error after n steps ≤ `(b - a)/2^(n + 1)` using the midpoint; needs `n >= log2((b - a)/tol)` steps | f continuous, f(a)·f(b) < 0 |
| num.root.false-position | method | False position (Illinois variant) | Secant through the bracket ends, halving the stale end's value to avoid one-sided convergence | Bracket |
| num.root.secant | theorem | Secant method | Order of convergence `φ = (1 + sqrt(5))/2 ≈ 1.618` | Simple root, good start |
| num.root.newton | theorem | Newton's method | `x(n + 1) = x(n) - f(x(n))/f'(x(n))`; quadratic convergence `e(n + 1) ≈ f''(r)/(2f'(r))*e(n)^2` | Simple root, f ∈ C², start close enough |
| num.root.newton-multiple | theorem | Newton at a multiple root | Converges only linearly; `x - m*f/f'` restores quadratic convergence for multiplicity m | |
| num.root.halley | theorem | Halley's method | Cubic convergence using f″ | Simple root |
| num.root.brent | method | Brent's method | Inverse quadratic interpolation with bisection safeguards: guaranteed convergence and superlinear in practice | Bracket |
| num.root.ridders | method | Ridders' method | Exponential fitting in a bracket; quadratic convergence | Bracket |
| num.root.banach | theorem | Banach fixed-point theorem | g maps [a, b] into itself with `abs(g'(x)) <= L < 1` ⇒ unique fixed point; iteration converges with `abs(x(n) - x*) <= L^n/(1 - L)*abs(x(1) - x(0))` | |
| num.root.aitken | method | Aitken Δ² and Steffensen acceleration | `x - (Δx)^2/Δ²x` accelerates linearly convergent sequences | |
| num.root.muller | method | Muller's method | Quadratic through three points; finds complex roots; order ≈ 1.84 | |
| num.root.aberth | method | Aberth–Ehrlich method | Simultaneous iteration for all polynomial roots; cubic convergence | Polynomials |
| num.root.companion | theorem | Companion matrix | Polynomial roots are the eigenvalues of its companion matrix | Monic polynomial |
| num.root.wilkinson | theorem | Wilkinson's polynomial | Roots of `product(x - k, k, 1, 20)` are extremely sensitive to coefficient perturbations (example of ill-conditioning) | |
| num.root.stopping | method | Stopping criteria | `abs(x(n + 1) - x(n)) <= tol*(1 + abs(x(n + 1)))` or `abs(f(x)) <= ftol`, plus an iteration cap | |

## Interpolation and approximation

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.interp.unique | theorem | Existence and uniqueness | Exactly one polynomial of degree ≤ n passes through n + 1 points with distinct x_i | |
| num.interp.lagrange | formula | Lagrange form | `p(x) = sum(y_i*product((x - x_j)/(x_i - x_j), j ≠ i))` | |
| num.interp.barycentric | formula | Barycentric form | `p(x) = sum(w_i*y_i/(x - x_i))/sum(w_i/(x - x_i))` with `w_i = 1/product(x_i - x_j, j ≠ i)` | Stable, O(n) per evaluation |
| num.interp.newton | formula | Newton divided differences | `p(x) = f[x0] + f[x0, x1]*(x - x0) + …` | |
| num.interp.neville | method | Neville's algorithm | Recursive evaluation at one point | |
| num.interp.error | theorem | Interpolation error | `f(x) - p(x) = diff(f, x, n + 1)(ξ)/(n + 1)!*product(x - x_i)` | f ∈ Cⁿ⁺¹ |
| num.interp.runge | theorem | Runge phenomenon | High-degree interpolation at equispaced nodes can diverge (e.g. `1/(1 + 25x^2)` on [−1, 1]) | |
| num.interp.chebyshev-nodes | theorem | Chebyshev nodes | `x_k = cos((2k + 1)*pi/(2n + 2))` minimize `max(abs(product(x - x_k)))` on [−1, 1], with value `2^-n` | k = 0, …, n |
| num.interp.hermite | method | Hermite interpolation | Matches values and derivatives | |
| num.interp.cubic-spline | definition | Cubic splines | Piecewise cubic, C² at knots; natural (S″ = 0 at ends), clamped (S′ given) or not-a-knot | |
| num.interp.spline-error | theorem | Clamped spline error | `max abs(f - S) <= (5/384)*h^4*max abs(f'''')` | f ∈ C⁴ |
| num.approx.weierstrass | theorem | Weierstrass approximation theorem | Every continuous function on [a, b] is a uniform limit of polynomials | |
| num.approx.least-squares | method | Least-squares polynomial fit | Solve via QR (not the normal equations, which square the condition number) | |
| num.approx.equioscillation | theorem | Chebyshev equioscillation theorem | The best uniform polynomial approximation of degree n has an error that equioscillates at ≥ n + 2 points | f continuous |
| num.approx.remez | method | Remez algorithm | Iteratively builds the minimax polynomial or rational approximation | |
| num.approx.chebyshev-series | method | Chebyshev series | Coefficients by DCT at Chebyshev points; truncation gives near-minimax approximations | |
| num.approx.pade | method | Padé approximants | Rational function matching a Taylor series to order m + n | |

## Numerical differentiation

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.diff.forward | formula | Forward difference | `f'(x) ≈ (f(x + h) - f(x))/h`, error `-h*f''(ξ)/2` | |
| num.diff.central | formula | Central difference | `f'(x) ≈ (f(x + h) - f(x - h))/(2h)`, error `-h^2*f'''(ξ)/6` | |
| num.diff.second | formula | Second derivative | `f''(x) ≈ (f(x + h) - 2f(x) + f(x - h))/h^2`, error `-h^2*f''''(ξ)/12` | |
| num.diff.five-point | formula | Five-point stencil | `f'(x) ≈ (-f(x + 2h) + 8f(x + h) - 8f(x - h) + f(x - 2h))/(12h)`, error O(h⁴) | |
| num.diff.optimal-step | theorem | Optimal step size | Balancing truncation and rounding: `h ≈ sqrt(u)*max(1, abs(x))` for forward differences, `h ≈ root(u, 3)*max(1, abs(x))` for central | |
| num.diff.richardson | method | Richardson extrapolation | For an O(h²) method: `D ≈ (4D(h/2) - D(h))/3`, error O(h⁴) | |
| num.diff.complex-step | formula | Complex-step derivative | `f'(x) ≈ im(f(x + I*h))/h` with no subtractive cancellation (h ≈ 1e−20 works) | f analytic, real on ℝ |
| num.diff.forward-ad | theorem | Forward-mode automatic differentiation | `f(a + b*ε) = f(a) + b*f'(a)*ε` with ε² = 0 gives derivatives exact to rounding | `Dual<T>` |

## Numerical integration

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.quad.trapezoid | formula | Composite trapezoidal rule | Error `-(b - a)*h^2*f''(ξ)/12` | f ∈ C² |
| num.quad.midpoint | formula | Composite midpoint rule | Error `(b - a)*h^2*f''(ξ)/24` | f ∈ C² |
| num.quad.simpson | formula | Composite Simpson's rule | `h/3*(f0 + 4f1 + 2f2 + … + 4f(n-1) + fn)`, error `-(b - a)*h^4*f''''(ξ)/180` | n even, f ∈ C⁴ |
| num.quad.simpson-38 | formula | Simpson's 3/8 rule | `3h/8*(f0 + 3f1 + 3f2 + f3)` | |
| num.quad.degree-of-exactness | definition | Degree of exactness | Largest d such that the rule integrates all polynomials of degree ≤ d exactly | |
| num.quad.euler-maclaurin | theorem | Euler–Maclaurin formula | Trapezoid error expands in even powers of h with Bernoulli-number coefficients (basis of Romberg) | f smooth |
| num.quad.romberg | method | Romberg integration | Richardson extrapolation of trapezoid sums | |
| num.quad.periodic-trapezoid | theorem | Trapezoid rule for periodic functions | Converges exponentially for periodic analytic integrands over a full period | |
| num.quad.gauss | theorem | Gauss–Legendre quadrature | n nodes (roots of `legendreP(n, x)`) integrate polynomials of degree ≤ 2n − 1 exactly | |
| num.quad.gauss-families | method | Gauss–Laguerre, Gauss–Hermite, Gauss–Chebyshev | Weights e^(−x) on [0, ∞), e^(−x²) on ℝ, 1/√(1 − x²) on [−1, 1] | |
| num.quad.gauss-kronrod | method | Adaptive Gauss–Kronrod (G7/K15) | Nested rules give an error estimate; subdivide the worst interval | |
| num.quad.tanh-sinh | method | Tanh-sinh (double exponential) quadrature | Handles endpoint singularities with near-exponential convergence | Integrand analytic inside |
| num.quad.monte-carlo | theorem | Monte Carlo error | Standard error `σ/sqrt(N)` regardless of dimension | |
| num.quad.qmc | theorem | Quasi-Monte Carlo error | `O((log N)^d/N)` for low-discrepancy sequences (Halton, Sobol) | Bounded variation |

## Ordinary differential equations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.ode.euler | formula | Euler's method | `y(n + 1) = y(n) + h*f(t(n), y(n))`, global error O(h) | f Lipschitz in y |
| num.ode.rk4 | formula | Classical Runge–Kutta (RK4) | `k1 = f(t, y)`, `k2 = f(t + h/2, y + h*k1/2)`, `k3 = f(t + h/2, y + h*k2/2)`, `k4 = f(t + h, y + h*k3)`, `y(n + 1) = y(n) + h*(k1 + 2k2 + 2k3 + k4)/6`; global error O(h⁴) | |
| num.ode.local-global | theorem | Local and global error | A method with local truncation error O(h^(p+1)) has global error O(h^p) | Stable method |
| num.ode.step-control | formula | Adaptive step size | `h_new = h*safety*(tol/err)^(1/(p + 1))` from an embedded pair (RKF45, Dormand–Prince 5(4)) | |
| num.ode.dahlquist-equivalence | theorem | Dahlquist equivalence theorem | A linear multistep method is convergent ⇔ consistent and zero-stable | |
| num.ode.stiffness | definition | Stiffness | Widely separated time scales force explicit methods to tiny steps for stability, not accuracy | |
| num.ode.euler-stability | theorem | Stability region of Euler's method | Stable for y′ = λy iff `abs(1 + h*λ) <= 1` | |
| num.ode.a-stability | definition | A-stability | The stability region contains the left half-plane (backward Euler, trapezoidal rule) | |
| num.ode.dahlquist-barrier | theorem | Second Dahlquist barrier | An A-stable linear multistep method has order ≤ 2 | |
| num.ode.multistep | method | Adams–Bashforth–Moulton and BDF | Predictor–corrector for non-stiff problems; BDF for stiff ones | |
| num.ode.symplectic | theorem | Symplectic integrators | Velocity Verlet and leapfrog preserve phase-space volume and keep energy error bounded for Hamiltonian systems | |
| num.ode.shooting | method | Shooting method | Turn a boundary value problem into root finding on the unknown initial slope | |
| num.ode.fd-bvp | method | Finite differences for BVPs | Discretize derivatives on a grid and solve the (tri)diagonal system | |

## Numerical linear algebra and eigenvalues

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.la.gaussian-cost | theorem | Cost of Gaussian elimination | About `2n^3/3` flops (Cholesky about `n^3/3`) | Dense |
| num.la.gepp-stability | theorem | Gaussian elimination with partial pivoting | Backward stable in practice; worst-case growth factor 2^(n−1) | |
| num.la.householder | theorem | Householder QR | Backward stable | |
| num.la.perturbation | theorem | Perturbation bound | `norm(δx)/norm(x) <= κ(A)*norm(δb)/norm(b)`; expect to lose about log10 κ(A) digits | A invertible |
| num.la.residual | theorem | Small residual ≠ small error | Error ≤ κ(A) × relative residual | |
| num.la.refinement | method | Iterative refinement | Compute the residual (in higher precision when possible), solve for a correction, repeat | |
| num.la.jacobi-gs | theorem | Convergence of Jacobi and Gauss–Seidel | Both converge for strictly diagonally dominant A; generally iff the iteration matrix has spectral radius < 1 | |
| num.la.sor | method | Successive over-relaxation | Gauss–Seidel with relaxation 0 < ω < 2 | |
| num.la.cg | theorem | Conjugate gradient | For SPD A: exact in ≤ n steps in exact arithmetic; error contracts by about `(sqrt(κ) - 1)/(sqrt(κ) + 1)` per step | A symmetric positive definite |
| num.la.gmres | method | GMRES | Minimizes the residual over Krylov subspaces; for nonsymmetric A; restarted for memory | |
| num.la.hilbert | theorem | Hilbert matrices | `κ(H_n)` grows like e^(3.5n) (example of ill-conditioning) | |
| num.eig.power | theorem | Power iteration | Converges to the dominant eigenvector at rate `abs(λ2/λ1)` | abs(λ1) > abs(λ2) |
| num.eig.inverse | method | Inverse and shifted inverse iteration | Converges to the eigenvalue nearest the shift | |
| num.eig.rayleigh | theorem | Rayleigh quotient iteration | Cubic convergence for symmetric matrices | |
| num.eig.qr-algorithm | method | QR algorithm | Hessenberg reduction, Wilkinson shifts, deflation | |
| num.eig.jacobi | method | Jacobi eigenvalue algorithm | Rotations zero off-diagonal entries of a symmetric matrix | Symmetric |
| num.eig.krylov | method | Lanczos and Arnoldi | Krylov subspace methods for a few eigenvalues of large sparse matrices | |
| num.eig.bauer-fike | theorem | Bauer–Fike theorem | Each eigenvalue μ of A + E satisfies `abs(μ - λ) <= κ(V)*norm(E)` for some eigenvalue λ of A = VΛV⁻¹ | A diagonalizable |
| num.eig.weyl | theorem | Weyl's inequality | Eigenvalues of symmetric A + E move by at most `norm(E, 2)` | Symmetric |

## Nonlinear systems and optimization

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.opt.newton-system | method | Newton's method for systems | Solve `J(x)*Δ = -F(x)`, update `x + Δ`; quadratic near a regular root | J invertible at the root |
| num.opt.broyden | method | Broyden's method | Secant updates of the Jacobian; superlinear | |
| num.opt.golden-section | method | Golden-section search | Shrinks the bracket by `(sqrt(5) - 1)/2 ≈ 0.618` per step | Unimodal f |
| num.opt.gradient-descent | theorem | Gradient descent | With step 1/L, `f(x(k)) - f* <= L*norm(x(0) - x*)^2/(2k)` | f convex with L-Lipschitz gradient |
| num.opt.bfgs | formula | BFGS update | `H(k + 1) = (I - ρ*s*y^T)*H(k)*(I - ρ*y*s^T) + ρ*s*s^T` with `ρ = 1/(y^T*s)` | y^T s > 0 |
| num.opt.wolfe | definition | Wolfe conditions | Sufficient decrease and curvature conditions for line searches | |
| num.opt.nelder-mead | method | Nelder–Mead simplex | Derivative-free reflection, expansion, contraction, shrink | |
| num.opt.levenberg-marquardt | method | Levenberg–Marquardt | Damped Gauss–Newton for nonlinear least squares | |
| num.opt.kkt | theorem | Karush–Kuhn–Tucker conditions | Necessary conditions for constrained optima: stationarity, primal and dual feasibility, complementary slackness | Constraint qualification |
| num.opt.convex | theorem | Convexity | For a convex function every local minimum is global | |

## Fourier analysis

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.fft.dft | definition | Discrete Fourier transform | `X_k = sum(x_n*e^(-2*pi*I*k*n/N), n, 0, N - 1)`; inverse divides by N with the opposite sign | |
| num.fft.cooley-tukey | theorem | Fast Fourier transform | Computes the DFT in O(N log N) | N a power of 2 (Bluestein for any N) |
| num.fft.convolution | theorem | Convolution theorem (DFT) | Circular convolution ↔ pointwise product of DFTs; zero-pad to length ≥ n + m − 1 for linear convolution | |
| num.fft.parseval | theorem | Parseval's theorem (DFT) | `sum(abs(x_n)^2) = sum(abs(X_k)^2)/N` | |
| num.fft.real-symmetry | theorem | Real input symmetry | `X_(N - k) = conj(X_k)` | Real x |
| num.fft.nyquist | theorem | Nyquist–Shannon sampling theorem | A signal band-limited below f_s/2 is determined by samples at rate f_s; higher frequencies alias | |
| num.fft.windowing | method | Windowing | Hann or Hamming windows reduce spectral leakage | |

## Special functions, random numbers and Monte Carlo

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.sf.gamma-recurrence | law | Gamma recurrence | `gamma(z + 1) = z*gamma(z)`, `gamma(n + 1) = n!`, `gamma(1/2) = sqrt(pi)` | |
| num.sf.reflection | law | Reflection formula | `gamma(z)*gamma(1 - z) = pi/sin(pi*z)` | z ∉ ℤ |
| num.sf.duplication | law | Legendre duplication formula | `gamma(z)*gamma(z + 1/2) = 2^(1 - 2z)*sqrt(pi)*gamma(2z)` | |
| num.sf.lanczos | method | Lanczos approximation | Gamma to near double precision with a short rational series | Re z > 0, reflection otherwise |
| num.sf.miller | method | Miller's backward recurrence | Stable evaluation of Bessel J_n when forward recurrence is unstable (n > x) | |
| num.sf.lentz | method | Modified Lentz algorithm | Evaluates continued fractions (incomplete gamma, erfc, ratios of Bessel functions) | |
| num.sf.argument-reduction | method | Argument reduction | Reduce trig arguments modulo π/2 accurately (Cody–Waite; Payne–Hanek for huge arguments) | |
| num.mc.inverse-transform | theorem | Inverse transform sampling | `X = F^-1(U)` with U ~ Uniform(0, 1) has CDF F | |
| num.mc.box-muller | formula | Box–Muller transform | `Z = sqrt(-2ln(U1))*cos(2*pi*U2)` is standard normal | U1, U2 independent uniform on (0, 1] |
| num.mc.rejection | method | Acceptance–rejection sampling | Sample from g, accept with probability f/(c·g) | f ≤ c·g |
| num.mc.variance-reduction | method | Variance reduction | Antithetic variates, control variates, importance sampling | |
| num.mc.reproducibility | convention | Reproducible random streams | The algorithm behind `System.Random` is an implementation detail that has changed across .NET versions, so Mathesis ships its own documented seeded generator (xoshiro256**) for stable sequences | |

## PDE numerics

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.pde.ftcs | theorem | FTCS for the heat equation | Explicit scheme is stable iff `r = k*Δt/Δx^2 <= 1/2` | |
| num.pde.crank-nicolson | theorem | Crank–Nicolson | Unconditionally stable, error O(Δt² + Δx²) | |
| num.pde.cfl | theorem | CFL condition | Explicit wave schemes need `c*Δt/Δx <= 1` | |
| num.pde.lax-equivalence | theorem | Lax equivalence theorem | A consistent linear scheme for a well-posed linear problem converges ⇔ it is stable | |
| num.pde.von-neumann | method | Von Neumann stability analysis | Substitute Fourier modes; require amplification factor ≤ 1 in modulus | Constant coefficients, periodic |
| num.pde.five-point | formula | Five-point Laplacian | `(u(i+1,j) + u(i-1,j) + u(i,j+1) + u(i,j-1) - 4u(i,j))/h^2`, error O(h²) | |
| num.pde.upwind | method | Upwind scheme for advection | Difference in the direction information comes from | |

## Arbitrary precision and validated numerics

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| num.ap.correct-rounding | definition | Correct rounding | The result is the exact value rounded to the target precision | |
| num.ap.ziv | method | Ziv's strategy | Evaluate with extra precision; if the result cannot be rounded unambiguously, retry with more | Elementary functions in `BigFloat` |
| num.iv.inclusion | theorem | Fundamental theorem of interval arithmetic | The natural interval extension of f over X encloses the range of f on X | f built from operations with interval versions |
| num.iv.dependency | theorem | Dependency problem | Repeated variables widen enclosures: `X - X` ≠ [0, 0] in interval arithmetic | |
| num.iv.mean-value-form | method | Mean value form | `f(c) + F'(X)*(X - c)` gives tighter enclosures on narrow intervals | |
| num.iv.interval-newton | theorem | Interval Newton method | If `N(X) ⊆ X` then X contains exactly one root; if `N(X) ∩ X = ∅` it contains none | f ∈ C¹, 0 ∉ F′(X) |
| num.iv.outward-rounding | method | Outward rounding | Round lower bounds down and upper bounds up (`T.BitDecrement`, `T.BitIncrement`) | |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Root finding with bracketing guarantees and convergence diagnostics | `Roots.Brent(x => x*x - 2, 1, 2)` | 1 |
| Adaptive quadrature with error estimates, including singular and infinite ranges | `integrate(ln(x)/sqrt(x), x, 0, 1) = -4` | 1 (tanh-sinh, cubature: 7) |
| ODE integration with dense output, events and stiffness detection | Robertson chemical kinetics | 1 (stiff: 7) |
| Interpolation, splines, Chebyshev approximations | | 1 / 7 |
| Numerical derivatives (finite differences, Richardson, complex step) and automatic differentiation | | 1 |
| Dense and sparse linear solvers with condition reports | | 1 / 7 |
| Eigenvalues and SVD (numeric) | | 7 |
| Optimization: golden section, Brent, Nelder–Mead, BFGS, Levenberg–Marquardt | Curve fitting | 1 / 7 |
| FFT, convolution, spectra | Spectrum of a sampled tone | 7 |
| Special functions to near machine precision | `gamma(0.5) = sqrt(pi)` | 7 |
| Arbitrary precision and guaranteed enclosures | `N(pi, 100)`; prove `x^2 - 2` has exactly one root in [1.4, 1.5] | 7 |
| Error-analysis explanations ("lost about 8 digits because κ ≈ 10⁸") | | 7 |

### Example: Newton's method for √2

`f(x) = x^2 - 2`, `x(0) = 1` (`num.root.newton`); the error roughly squares each step:

| n | x(n) | abs(x(n) − √2) |
| --- | --- | --- |
| 0 | 1 | 4.1e−1 |
| 1 | 1.5 | 8.6e−2 |
| 2 | 1.4166666666666667 | 2.5e−3 |
| 3 | 1.4142156862745099 | 2.1e−6 |
| 4 | 1.4142135623746899 | 1.6e−12 |
| 5 | 1.4142135623730951 | 0 (to double precision) |
