# D6 Differential Equations

Mathesis classifies an ordinary differential equation, picks the textbook method for its type, solves it with steps, applies initial conditions, and verifies the result by substitution, falling back to a numeric integrator (and comparing) when no closed form exists. Laplace transforms, linear systems with phase-plane classification, series solutions, Fourier series and separation of variables for the classic PDEs complete the domain.

- **Prefix:** `ode` · **Course tag:** `DifferentialEquations` · **Completed in:** Milestone 4 (numeric solvers in Milestone 1)
- **Packages:** `Mathesis` (`Mathesis.DifferentialEquations`), `Mathesis.Numerics.Ode` and `.Pde` (numeric), `Mathesis.LinearAlgebra` (eigen, matrix exponential)

## Scope

Classification, existence and uniqueness; first-order methods (separable, linear, exact, integrating factors, Bernoulli, homogeneous, Riccati, Clairaut); autonomous equations and stability; standard models; second- and higher-order linear equations (constant coefficients, Cauchy–Euler, reduction of order, undetermined coefficients, variation of parameters); mechanical and electrical vibrations; linear systems and phase portraits; nonlinear systems, linearization and Lyapunov functions; Laplace transforms; power-series and Frobenius solutions; Fourier series; Sturm–Liouville problems; heat, wave and Laplace equations. Recurrence relations are in D8; numeric ODE and PDE algorithms are in D9.

## Types

| Type | Purpose |
| --- | --- |
| `Ode` | Equation, unknown function(s), independent variable, order |
| `OdeClassification` | Order, linear or not, homogeneous, autonomous, constant coefficients, and every matching type (`Separable`, `LinearFirstOrder`, `Exact`, `Bernoulli`, `HomogeneousDegreeZero`, `Riccati`, `Clairaut`, `ConstantCoefficient`, `CauchyEuler`, …) |
| `OdeSolution` | General or particular; explicit or implicit; constants; singular and equilibrium solutions; interval of validity; verification flag |
| `InitialValueProblem`, `BoundaryValueProblem` | Equation plus conditions |
| `LinearOdeSystem` | x′ = A(t)x + f(t) with eigen data and fundamental matrix |
| `PhasePortrait` | Equilibria with classification and stability, nullclines, a sampled vector field for plotting |
| `FourierSeries` | Period, coefficient formulas (lazy), partial sums |

## Foundations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.basic.order | definition | Order | Highest derivative that appears | |
| ode.basic.linear | definition | Linear ODE | `a_n(x)*y^(n) + … + a_1(x)*y' + a_0(x)*y = g(x)` | |
| ode.basic.homogeneous | definition | Homogeneous linear ODE | g(x) = 0 | |
| ode.basic.autonomous | definition | Autonomous ODE | `y' = f(y)` (no explicit x) | |
| ode.basic.ivp | definition | Initial value problem | ODE plus `y(x0) = y0` (and derivatives up to order n − 1) | |
| ode.basic.picard | theorem | Existence and uniqueness (Picard–Lindelöf) | f and ∂f/∂y continuous on a rectangle around (x0, y0) ⇒ `y' = f(x, y)`, `y(x0) = y0` has a unique solution on some interval around x0 | |
| ode.basic.linear-existence | theorem | Existence for linear equations | p, q continuous on I ∋ x0 ⇒ `y' + p*y = q` has a unique solution on all of I (likewise for n-th order linear with continuous coefficients and a_n ≠ 0) | |
| ode.basic.superposition | theorem | Superposition principle | Linear combinations of solutions of a homogeneous linear ODE are solutions | |
| ode.basic.general-solution | theorem | Structure of solutions | General solution of a linear ODE = `y_h + y_p` | |

## First-order equations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.first.separable | method | Separable equations | `dy/dx = g(x)*h(y)` → `integrate(1/h(y), y) = integrate(g(x), x) + C`; also keep constant solutions with h(y) = 0, which division by h(y) loses | |
| ode.first.linear | method | Linear first-order (integrating factor) | `y' + P(x)*y = Q(x)`: `μ = e^integrate(P, x)`, `y = (integrate(μ*Q, x) + C)/μ` | P, Q continuous |
| ode.first.exact-test | theorem | Test for exactness | `M dx + N dy = 0` is exact iff `M_y = N_x` | M, N C¹ on a simply connected region |
| ode.first.exact | method | Exact equations | Find F with `F_x = M`, `F_y = N`; solution `F(x, y) = C` | Exact |
| ode.first.integrating-factor-x | method | Integrating factor μ(x) | If `(M_y - N_x)/N` depends only on x: `μ = e^integrate((M_y - N_x)/N, x)` | |
| ode.first.integrating-factor-y | method | Integrating factor μ(y) | If `(N_x - M_y)/M` depends only on y: `μ = e^integrate((N_x - M_y)/M, y)` | |
| ode.first.bernoulli | method | Bernoulli equations | `y' + P*y = Q*y^n`, n ≠ 0, 1: substitute `v = y^(1 - n)` to get `v' + (1 - n)*P*v = (1 - n)*Q`; y = 0 is also a solution when n > 0 | |
| ode.first.homogeneous | method | Homogeneous (degree zero) equations | `dy/dx = F(y/x)`: substitute `v = y/x`, giving `x*dv/dx = F(v) - v` (separable) | x ≠ 0 |
| ode.first.riccati | method | Riccati equations | `y' = q0(x) + q1(x)*y + q2(x)*y^2` with a known solution y1: `y = y1 + 1/v` gives a linear equation in v | |
| ode.first.clairaut | method | Clairaut's equation | `y = x*y' + f(y')`: general solution `y = C*x + f(C)`; singular solution parametrized by `x = -f'(p)`, `y = f(p) - p*f'(p)` | |
| ode.first.equilibrium | definition | Equilibrium solution | Constant y* with f(y*) = 0 for `y' = f(y)` | |
| ode.first.stability | theorem | Stability of equilibria (1D) | f′(y*) < 0 ⇒ asymptotically stable (sink); f′(y*) > 0 ⇒ unstable (source); f′(y*) = 0 inconclusive (check the phase line) | f C¹ |
| ode.first.phase-line | method | Phase line | Mark equilibria and the sign of f between them | Autonomous |

## Models

| ID | Name | Equation | Solution |
| --- | --- | --- | --- |
| ode.model.exponential | Exponential growth and decay | `y' = k*y` | `y = y0*e^(k*t)` |
| ode.model.cooling | Newton's law of cooling | `T' = -k*(T - Ta)` | `T = Ta + (T0 - Ta)*e^(-k*t)` |
| ode.model.logistic | Logistic growth | `P' = r*P*(1 - P/K)` | `P = K/(1 + ((K - P0)/P0)*e^(-r*t))` |
| ode.model.mixing | Mixing tank | `A' = r_in*c_in - r_out*A/V(t)` with `V(t) = V0 + (r_in - r_out)*t` | Linear first-order |
| ode.model.rc | RC circuit | `R*q' + q/C = E(t)` | Linear first-order |
| ode.model.rl | RL circuit | `L*i' + R*i = E(t)` | Linear first-order |
| ode.model.linear-drag | Falling body with linear drag | `m*v' = m*g - k*v` | `v = m*g/k + (v0 - m*g/k)*e^(-k*t/m)`; terminal velocity m·g/k |
| ode.model.torricelli | Draining tank (Torricelli) | `h' = -(a/A)*sqrt(2g*h)` | Separable |
| ode.model.orthogonal-trajectories | Orthogonal trajectories | Replace y′ = f(x, y) by y′ = −1/f(x, y) | |

## Second-order and higher linear equations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.second.wronskian | definition | Wronskian | `W(y1, y2) = y1*y2' - y1'*y2` (n×n determinant for order n) | |
| ode.second.independence | theorem | Wronskian test | Solutions y1, y2 of a homogeneous linear ODE on I are independent ⇔ W ≠ 0 at some (then every) point of I | Continuous coefficients |
| ode.second.abel | theorem | Abel's identity | `W(x) = C*e^(-integrate(p, x))` for `y'' + p*y' + q*y = 0` | |
| ode.second.reduction-of-order | formula | Reduction of order | `y2 = y1*integrate(e^(-integrate(p, x))/y1^2, x)` | y1 a known non-zero solution |
| ode.second.characteristic | definition | Characteristic equation | `a*r^2 + b*r + c = 0` for `a*y'' + b*y' + c*y = 0` | Constant coefficients, a ≠ 0 |
| ode.second.distinct-real | theorem | Distinct real roots | `y = C1*e^(r1*x) + C2*e^(r2*x)` | r1 ≠ r2 real |
| ode.second.repeated | theorem | Repeated root | `y = (C1 + C2*x)*e^(r*x)` | |
| ode.second.complex | theorem | Complex roots α ± βi | `y = e^(α*x)*(C1*cos(β*x) + C2*sin(β*x))` | β ≠ 0 |
| ode.higher.constant-coefficients | theorem | Higher-order constant coefficients | Each root r of multiplicity m contributes `x^k*e^(r*x)`, k = 0, …, m − 1 (complex pairs as cos/sin) | |
| ode.second.cauchy-euler | method | Cauchy–Euler equations | `a*x^2*y'' + b*x*y' + c*y = 0`: solve `a*m*(m - 1) + b*m + c = 0`; distinct: `x^m1`, `x^m2`; repeated: `x^m`, `x^m*ln(x)`; complex α ± βi: `x^α*cos(β*ln(x))`, `x^α*sin(β*ln(x))` | x > 0 |
| ode.second.undetermined | method | Undetermined coefficients | Trial forms: polynomial → polynomial of the same degree; `e^(a*x)` → `A*e^(a*x)`; `cos(b*x)` or `sin(b*x)` → `A*cos(b*x) + B*sin(b*x)`; products → products of trial forms | Constant coefficients; g of these forms |
| ode.second.modification-rule | theorem | Modification rule | Multiply the trial form by x^s, s the smallest non-negative integer so that no term duplicates a homogeneous solution | |
| ode.second.variation-of-parameters | formula | Variation of parameters | `y_p = -y1*integrate(y2*g/W, x) + y2*integrate(y1*g/W, x)` | Standard form `y'' + p*y' + q*y = g` |
| ode.higher.to-system | method | Reduce to a first-order system | `x1 = y, x2 = y', …, xn = y^(n-1)` | |

## Vibrations and circuits

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| ode.vib.spring-mass | Spring–mass system | `m*x'' + c*x' + k*x = F(t)` | m, k > 0, c ≥ 0 |
| ode.vib.natural-frequency | Natural frequency | `ω0 = sqrt(k/m)` | |
| ode.vib.damping | Damping regimes | `c^2 - 4m*k > 0` overdamped; `= 0` critically damped; `< 0` underdamped with quasi-frequency `μ = sqrt(4m*k - c^2)/(2m)` | |
| ode.vib.resonance | Pure resonance | `m*x'' + k*x = F0*cos(ω0*t)` has particular solution `x_p = F0*t*sin(ω0*t)/(2m*ω0)` | c = 0, forcing at ω0 |
| ode.vib.beats | Beats | Undamped forcing at ω ≠ ω0 gives `x = 2F0/(m*(ω0^2 - ω^2))*sin((ω0 - ω)*t/2)*sin((ω0 + ω)*t/2)` from rest | x(0) = x′(0) = 0 |
| ode.vib.rlc | RLC circuit | `L*q'' + R*q' + q/C = E(t)` (analog of the spring–mass system) | |

## Linear systems and phase portraits

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.sys.eigen-real | theorem | Distinct real eigenvalues | `x = sum(c_i*e^(λ_i*t)*v_i)` for x′ = Ax | A diagonalizable over ℝ |
| ode.sys.eigen-complex | theorem | Complex eigenvalues | For λ = α + βi with eigenvector a + b·i: real solutions `e^(α*t)*(a*cos(β*t) - b*sin(β*t))` and `e^(α*t)*(a*sin(β*t) + b*cos(β*t))` | |
| ode.sys.eigen-defective | theorem | Defective eigenvalue | `e^(λ*t)*v` and `e^(λ*t)*(t*v + w)` with `(A - λ*I)*w = v` | Geometric multiplicity 1, algebraic 2 |
| ode.sys.matrix-exponential | theorem | Matrix-exponential solution | `x(t) = e^(A*t)*x0` | Constant A |
| ode.sys.variation | formula | Nonhomogeneous systems | `x_p = Φ(t)*integrate(Φ(t)^-1*f(t), t)` with a fundamental matrix Φ | |
| ode.sys.classify-2x2 | theorem | Phase portrait classification | With τ = tr A, Δ = det A: Δ < 0 saddle; Δ > 0 and τ² > 4Δ node; τ² < 4Δ spiral (τ ≠ 0) or center (τ = 0); τ² = 4Δ degenerate or star node; stable when τ < 0, unstable when τ > 0; Δ = 0 a line of equilibria | 2×2 real A |
| ode.nonlin.linearization | method | Linearization | Equilibria from `f(x*) = 0`; classify with the Jacobian at x* | |
| ode.nonlin.hartman-grobman | theorem | Hartman–Grobman theorem | Near a hyperbolic equilibrium (no eigenvalue with zero real part) the phase portrait matches the linearization | f C¹ |
| ode.nonlin.lyapunov | theorem | Lyapunov's stability theorem | V positive definite near x* with V′ ≤ 0 along solutions ⇒ stable; V′ < 0 (except at x*) ⇒ asymptotically stable | |
| ode.nonlin.poincare-bendixson | theorem | Poincaré–Bendixson theorem | A bounded planar trajectory that stays away from equilibria approaches a periodic orbit | Planar C¹ systems |
| ode.nonlin.lotka-volterra | formula | Predator–prey model | `x' = a*x - b*x*y`, `y' = -c*y + d*x*y` with conserved `d*x - c*ln(x) + b*y - a*ln(y)` | a, b, c, d > 0 |
| ode.nonlin.pendulum | formula | Damped pendulum | `θ'' + γ*θ' + (g/L)*sin(θ) = 0` | |

## Laplace transforms

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.lap.def | definition | Laplace transform | `laplace(f, t, s) = integrate(e^(-s*t)*f(t), t, 0, oo)` | f piecewise continuous of exponential order; s large enough |
| ode.lap.linearity | law | Linearity | `laplace(a*f + b*g) = a*laplace(f) + b*laplace(g)` | |
| ode.lap.one | law | Transform of 1 | `laplace(1) = 1/s` | s > 0 |
| ode.lap.power | law | Transform of tⁿ | `laplace(t^n) = n!/s^(n + 1)` | n ∈ ℕ, s > 0 |
| ode.lap.exp | law | Transform of e^(at) | `laplace(e^(a*t)) = 1/(s - a)` | s > a |
| ode.lap.sin | law | Transform of sin | `laplace(sin(b*t)) = b/(s^2 + b^2)` | s > 0 |
| ode.lap.cos | law | Transform of cos | `laplace(cos(b*t)) = s/(s^2 + b^2)` | s > 0 |
| ode.lap.sinh | law | Transform of sinh | `laplace(sinh(b*t)) = b/(s^2 - b^2)` | s > abs(b) |
| ode.lap.cosh | law | Transform of cosh | `laplace(cosh(b*t)) = s/(s^2 - b^2)` | s > abs(b) |
| ode.lap.t-n-exp | law | Transform of tⁿe^(at) | `laplace(t^n*e^(a*t)) = n!/(s - a)^(n + 1)` | s > a |
| ode.lap.exp-sin | law | Damped sine | `laplace(e^(a*t)*sin(b*t)) = b/((s - a)^2 + b^2)` | s > a |
| ode.lap.exp-cos | law | Damped cosine | `laplace(e^(a*t)*cos(b*t)) = (s - a)/((s - a)^2 + b^2)` | s > a |
| ode.lap.t-sin | law | t·sin | `laplace(t*sin(b*t)) = 2b*s/(s^2 + b^2)^2` | s > 0 |
| ode.lap.t-cos | law | t·cos | `laplace(t*cos(b*t)) = (s^2 - b^2)/(s^2 + b^2)^2` | s > 0 |
| ode.lap.step | law | Unit step | `laplace(heaviside(t - a)) = e^(-a*s)/s` | a ≥ 0 |
| ode.lap.delta | law | Dirac delta | `laplace(dirac(t - a)) = e^(-a*s)` | a ≥ 0 |
| ode.lap.first-shift | law | First shifting theorem | `laplace(e^(a*t)*f(t)) = F(s - a)` | |
| ode.lap.second-shift | law | Second shifting theorem | `laplace(heaviside(t - a)*f(t - a)) = e^(-a*s)*F(s)` | a ≥ 0 |
| ode.lap.derivative | law | Transform of a derivative | `laplace(f') = s*F(s) - f(0)`; `laplace(f'') = s^2*F(s) - s*f(0) - f'(0)` | f, f′ continuous, exponential order |
| ode.lap.nth-derivative | law | Transform of the n-th derivative | `laplace(f^(n)) = s^n*F(s) - sum(s^(n - 1 - k)*f^(k)(0), k, 0, n - 1)` | |
| ode.lap.t-times | law | Multiplication by t | `laplace(t*f(t)) = -F'(s)` | |
| ode.lap.integral | law | Transform of an integral | `laplace(integrate(f(τ), τ, 0, t)) = F(s)/s` | |
| ode.lap.convolution | theorem | Convolution theorem | `laplace(integrate(f(τ)*g(t - τ), τ, 0, t)) = F(s)*G(s)` | |
| ode.lap.periodic | law | Periodic functions | `laplace(f) = integrate(e^(-s*t)*f(t), t, 0, T)/(1 - e^(-s*T))` | f has period T |
| ode.lap.initial-value | theorem | Initial value theorem | `limit(f(t), t, 0, "+") = limit(s*F(s), s, oo)` | Limits exist |
| ode.lap.final-value | theorem | Final value theorem | `limit(f(t), t, oo) = limit(s*F(s), s, 0)` | All poles of sF(s) in the open left half-plane |
| ode.lap.method | method | Solving IVPs with Laplace transforms | Transform, solve for Y(s), partial fractions, invert with the table and shifting theorems | |
| ode.lap.transfer | definition | Transfer function | `H(s) = Y(s)/X(s)` with zero initial conditions | Linear time-invariant system |

## Series solutions

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.ser.ordinary-point | theorem | Solutions at an ordinary point | `y = sum(a_n*(x - x0)^n, n, 0, oo)` with radius at least the distance to the nearest singular point | p, q analytic at x0 |
| ode.ser.recurrence | method | Recurrence for coefficients | Substitute the series, shift indices, equate coefficients | |
| ode.ser.regular-singular | definition | Regular singular point | `(x - x0)*p` and `(x - x0)^2*q` analytic at x0 | |
| ode.ser.frobenius | method | Frobenius method | `y = sum(a_n*x^(n + r), n, 0, oo)`; indicial equation `r*(r - 1) + p0*r + q0 = 0` | Regular singular point at 0 |
| ode.ser.frobenius-cases | theorem | Frobenius cases | Roots differing by a non-integer: two Frobenius series; equal roots: the second solution includes `y1*ln(x)`; roots differing by an integer: the second may include `ln(x)` | |
| ode.ser.bessel | definition | Bessel's equation | `x^2*y'' + x*y' + (x^2 - ν^2)*y = 0`, solutions `besselj(ν, x)`, `bessely(ν, x)` | |
| ode.ser.legendre | definition | Legendre's equation | `(1 - x^2)*y'' - 2x*y' + n*(n + 1)*y = 0`, polynomial solutions `legendreP(n, x)` | n ∈ ℕ |
| ode.ser.airy | definition | Airy's equation | `y'' - x*y = 0`, solutions `airyai(x)`, `airybi(x)` | |
| ode.ser.hermite | definition | Hermite's equation | `y'' - 2x*y' + 2n*y = 0`, polynomial solutions `hermiteH(n, x)` | n ∈ ℕ |

## Fourier series and boundary value problems

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.fourier.series | definition | Fourier series (period 2L) | `f ~ a0/2 + sum(a_n*cos(n*pi*x/L) + b_n*sin(n*pi*x/L), n, 1, oo)`, `a_n = integrate(f*cos(n*pi*x/L), x, -L, L)/L`, `b_n = integrate(f*sin(n*pi*x/L), x, -L, L)/L` | f integrable on [−L, L] |
| ode.fourier.orthogonality | law | Orthogonality relations | `integrate(sin(m*pi*x/L)*sin(n*pi*x/L), x, -L, L) = L*δ(m, n)` (likewise for cos; sin·cos integrates to 0) | m, n ≥ 1 |
| ode.fourier.dirichlet | theorem | Dirichlet convergence theorem | For piecewise smooth f the series converges to (f(x⁻) + f(x⁺))/2 | |
| ode.fourier.even-odd | theorem | Cosine and sine series | Even extension → cosine series; odd extension → sine series | |
| ode.fourier.parseval | theorem | Parseval's identity | `integrate(f^2, x, -L, L)/L = a0^2/2 + sum(a_n^2 + b_n^2, n, 1, oo)` | f square-integrable |
| ode.fourier.gibbs | theorem | Gibbs phenomenon | Partial sums overshoot a jump by about 9% of the jump size | |
| ode.bvp.sturm-liouville | definition | Sturm–Liouville problem | `(p*y')' + q*y + λ*w*y = 0` with separated boundary conditions | p, w > 0 |
| ode.bvp.sl-properties | theorem | Sturm–Liouville properties | Eigenvalues are real and can be ordered λ1 < λ2 < … → ∞; eigenfunctions for distinct eigenvalues are orthogonal with weight w | Regular problem |
| ode.bvp.dirichlet-example | theorem | `y'' + λ*y = 0`, `y(0) = y(L) = 0` | `λ_n = (n*pi/L)^2`, `y_n = sin(n*pi*x/L)` | n ≥ 1 |

## Partial differential equations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| ode.pde.classify | theorem | Classifying second-order linear PDEs | For `A*u_xx + B*u_xy + C*u_yy + … = 0`: `B^2 - 4A*C < 0` elliptic, `= 0` parabolic, `> 0` hyperbolic | |
| ode.pde.separation | method | Separation of variables | Assume `u = X(x)*T(t)`, separate into ODEs with a constant, solve the eigenvalue problem, superpose | Linear homogeneous PDE and BCs |
| ode.pde.heat | theorem | Heat equation on [0, L] | `u_t = k*u_xx`, `u(0, t) = u(L, t) = 0`: `u = sum(b_n*e^(-k*(n*pi/L)^2*t)*sin(n*pi*x/L))` with b_n the sine coefficients of u(x, 0) | |
| ode.pde.wave | theorem | Wave equation on [0, L] | `u_tt = c^2*u_xx`, fixed ends: `u = sum((A_n*cos(c*n*pi*t/L) + B_n*sin(c*n*pi*t/L))*sin(n*pi*x/L))` | |
| ode.pde.dalembert | formula | d'Alembert's formula | `u = (f(x - c*t) + f(x + c*t))/2 + integrate(g(s), s, x - c*t, x + c*t)/(2c)` | Infinite string, u(x, 0) = f, u_t(x, 0) = g |
| ode.pde.laplace | method | Laplace's equation on a rectangle | Separate variables; one boundary side non-zero at a time; superpose | |
| ode.pde.max-principle | theorem | Maximum principle | A harmonic function on a bounded domain attains its max and min on the boundary | u continuous on the closure |
| ode.pde.mean-value | theorem | Mean value property | A harmonic function equals its average over any circle (sphere) in its domain | |

## Methods

| ID | Name | Description |
| --- | --- | --- |
| ode.method.classify | Classification pipeline | Normalize; read order and linearity; test each type's pattern (separable factorization, linear form, exactness, Bernoulli exponent, degree-zero homogeneity, constant coefficients, Cauchy–Euler shape); list all matches in teaching order |
| ode.method.solve | Solve by type | Apply the first matching method at the context's curriculum level; apply initial conditions; verify by substitution; numeric fallback compared on the interval |
| ode.method.verify | Verify a solution | Substitute into the ODE and zero-test; check initial conditions; compare with a Dormand–Prince solution |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Classify an ODE and list the applicable methods | `x*y' + y = x^2` → linear first-order (also exact after rearranging) | 4 |
| Solve first-order equations of every listed type with steps | `y' = x*y^2` → `y = -2/(x^2 + C)` and y = 0 | 4 |
| Solve higher-order linear equations; IVPs | See the example below | 4 |
| Undetermined coefficients and variation of parameters | `y'' - y = e^x` → `y_p = x*e^x/2` (modification rule) | 4 |
| Laplace transforms and inverses with steps; solve IVPs with discontinuous forcing | `laplace(t^2*e^(3t)) = 2/(s - 3)^3` | 4 |
| Linear systems with eigenvalues; phase-portrait classification and plot data | | 4 |
| Linearize nonlinear systems and classify equilibria | Lotka–Volterra coexistence point is a center of the linearization | 4 |
| Power-series and Frobenius solutions to n terms | Airy's equation | 4 |
| Fourier series of piecewise functions; heat and wave equations on an interval | Square wave → `(4/pi)*sum(sin((2k - 1)x)/(2k - 1), k, 1, oo)` | 4 |
| Numeric solutions with error control (always available) | Van der Pol, stiff Robertson problem | 1 / 7 |

### Example derivation

`y'' + 4y = 0`, `y(0) = 1`, `y'(0) = 2`:

| Step | Result | Cited entry |
| --- | --- | --- |
| 1 | Characteristic equation `r^2 + 4 = 0` | `ode.second.characteristic` |
| 2 | `r = ±2I` (α = 0, β = 2) | `alg.quad.quadratic-formula` |
| 3 | `y = C1*cos(2x) + C2*sin(2x)` | `ode.second.complex` |
| 4 | `y(0) = C1 = 1` | Initial condition |
| 5 | `y'(x) = -2C1*sin(2x) + 2C2*cos(2x)`, `y'(0) = 2C2 = 2`, so `C2 = 1` | `calc.dtab.cos`, `calc.dtab.sin` |
| 6 | `y = cos(2x) + sin(2x)`; substitution gives `-4cos(2x) - 4sin(2x) + 4cos(2x) + 4sin(2x) = 0` ✓ | `ode.method.verify` |
