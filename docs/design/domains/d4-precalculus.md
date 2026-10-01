# D4 Pre-Calculus

Pre-Calculus treats functions as objects: Mathesis can find a function's domain, range, symmetry, intercepts, asymptotes and transformations, analyze conics, parametric and polar curves, work with complex numbers in polar form and with vectors, and sum arithmetic and geometric sequences, all with explained steps.

- **Prefix:** `pre` · **Course tag:** `PreCalculus` · **Completed in:** Milestone 2
- **Packages:** `Mathesis` (`Mathesis.Functions`), using `Mathesis.Solving` for domains and ranges and `Mathesis.Calculus` for monotonicity and extrema when the level allows

## Scope

Functions and their algebra; parent functions; transformations; polynomial, rational, exponential and logarithmic functions and models; conic sections including rotated conics; parametric equations; polar coordinates and polar graphs; complex numbers in polar form, De Moivre's theorem and roots; two- and three-dimensional vectors (dot product, projections); sequences, series and sigma notation. Limits are introduced in D5; matrices in D7.

## Types

| Type | Purpose |
| --- | --- |
| `FunctionAnalysis` | Domain, range, intercepts, symmetry, asymptotes, intervals of increase/decrease, extrema, concavity, inflection points, end behavior, holes |
| `Asymptote` | `Vertical(x = a)`, `Horizontal(y = b)`, `Slant(y = m·x + b)`, `Polynomial(y = q(x))` |
| `Transformation` | Ordered list of shifts, stretches/compressions and reflections relative to a parent function |
| `Conic` | `Circle`, `Parabola`, `Ellipse`, `Hyperbola`, `Degenerate` with center/vertex, foci, vertices, directrix, axes, eccentricity, asymptotes, rotation angle |
| `ParametricCurve`, `PolarCurve` | Curves with parameter ranges, conversions and symmetry |
| `Sequence` | `Arithmetic`, `Geometric`, `Explicit(f)`, `Recursive(rule, initial)` with terms, partial sums and limits |

## Functions

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.fn.def | definition | Function | Each input in the domain has exactly one output | |
| pre.fn.vertical-line-test | theorem | Vertical line test | A graph is a function of x iff every vertical line meets it at most once | |
| pre.fn.natural-domain | definition | Natural domain | Largest set of real inputs where the formula is defined | Computed by the domain engine |
| pre.fn.range | definition | Range | `{f(x) \| x in dom(f)}` | |
| pre.fn.zero | definition | Zero (root) | x with f(x) = 0; the x-intercepts of the graph | |
| pre.fn.arithmetic | definition | Function arithmetic | `(f + g)(x) = f(x) + g(x)`, `(f*g)(x) = f(x)*g(x)`, `(f/g)(x) = f(x)/g(x)` | Domains intersect; g(x) ≠ 0 for f/g |
| pre.fn.composition | definition | Composition | `(f ∘ g)(x) = f(g(x))` | Domain `{x in dom(g) \| g(x) in dom(f)}` |
| pre.fn.composition-assoc | theorem | Composition is associative | `f ∘ (g ∘ h) = (f ∘ g) ∘ h` | Not commutative in general |
| pre.fn.inverse | definition | Inverse function | `f^-1(y) = x <=> f(x) = y` | f one-to-one |
| pre.fn.inverse-compose | theorem | Inverse compositions | `f(f^-1(x)) = x` on dom f⁻¹; `f^-1(f(x)) = x` on dom f | |
| pre.fn.horizontal-line-test | theorem | Horizontal line test | f is one-to-one iff every horizontal line meets the graph at most once | |
| pre.fn.inverse-graph | theorem | Graph of the inverse | Reflection of the graph of f across y = x; domain and range swap | |
| pre.fn.inverse-of-composition | theorem | Inverse of a composition | `(f ∘ g)^-1 = g^-1 ∘ f^-1` | f, g one-to-one |
| pre.fn.even | definition | Even function | `f(-x) = f(x)` for all x in the domain; symmetric about the y-axis | Domain symmetric about 0 |
| pre.fn.odd | definition | Odd function | `f(-x) = -f(x)`; symmetric about the origin | Domain symmetric about 0 |
| pre.fn.parity-algebra | theorem | Parity of combinations | even ± even = even; odd ± odd = odd; even·even = odd·odd = even; even·odd = odd; composition with an even inner function is even | |
| pre.fn.increasing | definition | Increasing on an interval | `x1 < x2 => f(x1) < f(x2)` for x1, x2 in I | Decreasing and non-decreasing similarly |
| pre.fn.local-extremum | definition | Local maximum and minimum | f(c) ≥ f(x) (≤ for minimum) for all x in some open interval around c | |
| pre.fn.absolute-extremum | definition | Absolute maximum and minimum | f(c) ≥ f(x) for all x in the domain | |
| pre.fn.avg-rate | formula | Average rate of change | `(f(b) - f(a))/(b - a)` | a ≠ b |
| pre.fn.difference-quotient | formula | Difference quotient | `(f(x + h) - f(x))/h` | h ≠ 0 |
| pre.fn.piecewise | definition | Piecewise function | Different formulas on disjoint parts of the domain | |
| pre.fn.one-to-one-monotone | theorem | Strictly monotonic functions are one-to-one | | On an interval |

## Transformations

For y = a·f(b·(x − h)) + k, applied in the order: horizontal shift and scale, reflection, vertical stretch, vertical shift.

| ID | Name | Statement | Effect |
| --- | --- | --- | --- |
| pre.tf.vertical-shift | Vertical shift | `y = f(x) + k` | Up k (down if k < 0) |
| pre.tf.horizontal-shift | Horizontal shift | `y = f(x - h)` | Right h (left if h < 0) |
| pre.tf.reflect-x | Reflection across the x-axis | `y = -f(x)` | |
| pre.tf.reflect-y | Reflection across the y-axis | `y = f(-x)` | |
| pre.tf.vertical-stretch | Vertical stretch or compression | `y = a*f(x)` | Stretch by abs(a) if abs(a) > 1, compress if 0 < abs(a) < 1 |
| pre.tf.horizontal-stretch | Horizontal compression or stretch | `y = f(b*x)` | Compress by 1/abs(b) if abs(b) > 1, stretch if 0 < abs(b) < 1 |
| pre.tf.combined | Combined transformation | `y = a*f(b*(x - h)) + k` | Point (x, y) on f maps to (x/b + h, a·y + k) |

## Parent functions

| ID | Function | Domain | Range | Symmetry | Key features |
| --- | --- | --- | --- | --- | --- |
| pre.parent.constant | `f(x) = c` | ℝ | {c} | Even | Horizontal line |
| pre.parent.identity | `f(x) = x` | ℝ | ℝ | Odd | Slope 1 through the origin |
| pre.parent.square | `f(x) = x^2` | ℝ | [0, ∞) | Even | Vertex (0, 0) |
| pre.parent.cube | `f(x) = x^3` | ℝ | ℝ | Odd | Inflection at the origin |
| pre.parent.sqrt | `f(x) = sqrt(x)` | [0, ∞) | [0, ∞) | None | Starts at (0, 0) |
| pre.parent.cbrt | `f(x) = root(x, 3)` | ℝ | ℝ | Odd | Vertical tangent at 0 |
| pre.parent.reciprocal | `f(x) = 1/x` | ℝ ∖ {0} | ℝ ∖ {0} | Odd | Asymptotes x = 0, y = 0 |
| pre.parent.reciprocal-square | `f(x) = 1/x^2` | ℝ ∖ {0} | (0, ∞) | Even | Asymptotes x = 0, y = 0 |
| pre.parent.abs | `f(x) = abs(x)` | ℝ | [0, ∞) | Even | Corner at (0, 0) |
| pre.parent.exp | `f(x) = b^x` | ℝ | (0, ∞) | None | Asymptote y = 0; passes (0, 1) |
| pre.parent.log | `f(x) = log(x, b)` | (0, ∞) | ℝ | None | Asymptote x = 0; passes (1, 0) |
| pre.parent.floor | `f(x) = floor(x)` | ℝ | ℤ | None | Step function |
| pre.parent.sin | `f(x) = sin(x)` | ℝ | [−1, 1] | Odd | Period 2π |
| pre.parent.cos | `f(x) = cos(x)` | ℝ | [−1, 1] | Even | Period 2π |

## Polynomial and rational functions

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.poly.end-behavior | theorem | End behavior | Determined by the leading term a·xⁿ: n even, a > 0 → up/up; n even, a < 0 → down/down; n odd, a > 0 → down/up; n odd, a < 0 → up/down | n ≥ 1 |
| pre.poly.max-zeros | theorem | Number of real zeros | At most n | Degree n ≥ 1 |
| pre.poly.turning-points | theorem | Turning points | At most n − 1 | Degree n ≥ 1 |
| pre.poly.multiplicity-behavior | theorem | Behavior at a zero | Odd multiplicity: the graph crosses; even: it touches and turns; multiplicity ≥ 2 flattens the graph there | |
| pre.poly.sketch | method | Sketch a polynomial | Zeros with multiplicity, y-intercept, end behavior, sign between zeros | |
| pre.rat.vertical-asymptote | theorem | Vertical asymptotes | After cancelling common factors, x = a is a vertical asymptote where the denominator is zero | |
| pre.rat.hole | theorem | Removable discontinuity (hole) | A factor (x − a) cancelled from numerator and denominator gives a hole at (a, reduced(a)) | |
| pre.rat.horizontal-asymptote | theorem | Horizontal asymptote | deg N < deg D: y = 0; deg N = deg D: y = ratio of leading coefficients; deg N > deg D: none | |
| pre.rat.slant-asymptote | theorem | Slant (oblique) asymptote | deg N = deg D + 1: y = quotient of N ÷ D | |
| pre.rat.polynomial-asymptote | theorem | Polynomial asymptote | deg N > deg D + 1: the graph approaches y = quotient of N ÷ D | |
| pre.rat.crossing | theorem | Crossing asymptotes | A graph never crosses a vertical asymptote; it may cross a horizontal or slant asymptote, at solutions of f(x) = asymptote | |
| pre.rat.va-behavior | theorem | Behavior near a vertical asymptote | Odd multiplicity: opposite infinite signs on each side; even: same sign | |
| pre.rat.sketch | method | Sketch a rational function | Domain, intercepts, asymptotes, holes, sign chart, symmetry | |

## Exponential and logarithmic functions and models

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.explog.growth-decay | theorem | Growth or decay | `a*b^x` with a > 0 grows if b > 1, decays if 0 < b < 1 | b > 0, b ≠ 1 |
| pre.explog.inverse-pair | theorem | Exponential and logarithm are inverses | `log(b^x, b) = x`, `b^log(x, b) = x` | x > 0 for the second |
| pre.explog.continuous-growth | formula | Continuous exponential model | `A(t) = A0*e^(k*t)` | k > 0 growth, k < 0 decay |
| pre.explog.doubling-time | formula | Doubling time | `T = ln(2)/k` | k > 0 |
| pre.explog.half-life | formula | Half-life | `A(t) = A0*(1/2)^(t/T)`, `T = ln(2)/k` for `A0*e^(-k*t)` | k > 0 |
| pre.explog.logistic | formula | Logistic model | `P(t) = K/(1 + A*e^(-r*t))` with `A = (K - P0)/P0` | K, r > 0 |
| pre.explog.newton-cooling | formula | Newton's law of cooling (solution) | `T(t) = Ta + (T0 - Ta)*e^(-k*t)` | k > 0 |
| pre.explog.ph | formula | pH | `pH = -log(H, 10)` where H is the hydrogen-ion concentration in mol/L | H > 0 |
| pre.explog.sound-intensity | formula | Sound intensity level | `L = 10*log(I/I0, 10)` dB with `I0 = 10^-12` W/m² | I > 0 |
| pre.explog.sound-pressure | formula | Sound pressure level | `Lp = 20*log(p/p0, 10)` dB with `p0 = 20` µPa | p > 0 |
| pre.explog.richter | formula | Richter magnitude | `M = log(A/A0, 10)` | A > 0 |

## Conic sections

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.conic.general | definition | General second-degree equation | `A*x^2 + B*x*y + C*y^2 + D*x + E*y + F = 0` | A, B, C not all 0 |
| pre.conic.classify | theorem | Classification by discriminant | `B^2 - 4A*C < 0` ellipse (circle if B = 0 and A = C); `= 0` parabola; `> 0` hyperbola | Non-degenerate cases |
| pre.conic.rotation-angle | formula | Rotation of axes | `cot(2θ) = (A - C)/B` removes the xy term | B ≠ 0 |
| pre.conic.rotation | formula | Rotation formulas | `x = x'*cos(θ) - y'*sin(θ)`, `y = x'*sin(θ) + y'*cos(θ)` | |
| pre.conic.circle | formula | Circle | `(x - h)^2 + (y - k)^2 = r^2` | r > 0 |
| pre.conic.parabola-vertical | formula | Parabola (vertical axis) | `(x - h)^2 = 4p*(y - k)`: vertex (h, k), focus (h, k + p), directrix y = k − p | p ≠ 0 |
| pre.conic.parabola-horizontal | formula | Parabola (horizontal axis) | `(y - k)^2 = 4p*(x - h)`: focus (h + p, k), directrix x = h − p | p ≠ 0 |
| pre.conic.ellipse | formula | Ellipse (horizontal major axis) | `(x - h)^2/a^2 + (y - k)^2/b^2 = 1`, `c^2 = a^2 - b^2`, vertices (h ± a, k), foci (h ± c, k) | a > b > 0 |
| pre.conic.ellipse-vertical | formula | Ellipse (vertical major axis) | `(x - h)^2/b^2 + (y - k)^2/a^2 = 1`, foci (h, k ± c) | a > b > 0 |
| pre.conic.hyperbola | formula | Hyperbola (horizontal transverse axis) | `(x - h)^2/a^2 - (y - k)^2/b^2 = 1`, `c^2 = a^2 + b^2`, foci (h ± c, k), asymptotes `y - k = ±(b/a)*(x - h)` | a, b > 0 |
| pre.conic.hyperbola-vertical | formula | Hyperbola (vertical transverse axis) | `(y - k)^2/a^2 - (x - h)^2/b^2 = 1`, foci (h, k ± c), asymptotes `y - k = ±(a/b)*(x - h)` | a, b > 0 |
| pre.conic.eccentricity | formula | Eccentricity | `e = c/a`: 0 ≤ e < 1 ellipse (0 circle), e = 1 parabola, e > 1 hyperbola | |
| pre.conic.focus-directrix | theorem | Focus–directrix property | A conic is the set of points whose distance to the focus is e times the distance to the directrix | e > 0 |
| pre.conic.polar | formula | Polar form with a focus at the pole | `r = e*p/(1 ± e*cos(θ))` or `r = e*p/(1 ± e*sin(θ))` | e > 0, p > 0 |
| pre.conic.latus-rectum | formula | Latus rectum | Parabola 4·abs(p); ellipse and hyperbola 2b²/a | |
| pre.conic.ellipse-area | formula | Area of an ellipse | `Area = pi*a*b` | |
| pre.conic.reflective | theorem | Reflective properties | Rays from one focus of an ellipse reflect through the other; rays parallel to a parabola's axis reflect through its focus | |
| pre.conic.to-standard | method | Standard form by completing the square | Group x and y terms, complete each square, divide to get 1 on the right | B = 0 |

## Parametric equations and polar coordinates

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.param.def | definition | Parametric curve | `(x, y) = (f(t), g(t))` for t in an interval | |
| pre.param.eliminate | method | Eliminate the parameter | Solve one equation for t and substitute, or use an identity such as cos²t + sin²t = 1 | Track the restricted range |
| pre.param.line | formula | Line through a point with a direction | `(x, y) = (x0, y0) + t*(a, b)` | (a, b) ≠ (0, 0) |
| pre.param.circle | formula | Circle | `(x, y) = (h + r*cos(t), k + r*sin(t))` | 0 ≤ t < 2π |
| pre.param.ellipse | formula | Ellipse | `(x, y) = (h + a*cos(t), k + b*sin(t))` | |
| pre.param.projectile | formula | Projectile | `x = v0*cos(θ)*t`, `y = h0 + v0*sin(θ)*t - g*t^2/2` | No air resistance |
| pre.param.cycloid | formula | Cycloid | `x = r*(t - sin(t))`, `y = r*(1 - cos(t))` | |
| pre.polar.to-rect | formula | Polar to rectangular | `x = r*cos(θ)`, `y = r*sin(θ)` | |
| pre.polar.to-polar | formula | Rectangular to polar | `r^2 = x^2 + y^2`, `θ = atan2(y, x)` | (x, y) ≠ (0, 0) |
| pre.polar.equivalent | theorem | Equivalent polar coordinates | `(r, θ)`, `(r, θ + 2*pi*k)` and `(-r, θ + pi)` name the same point | k ∈ ℤ |
| pre.polar.symmetry | theorem | Symmetry tests | Replacing θ by −θ (polar axis), θ by π − θ (line θ = π/2), r by −r (pole) leaves an equivalent equation | Sufficient, not necessary |
| pre.polar.circles | formula | Polar circles | `r = a` (centered at the pole), `r = 2a*cos(θ)`, `r = 2a*sin(θ)` | |
| pre.polar.limacon | formula | Limaçon | `r = a + b*cos(θ)`: inner loop if a < b, cardioid if a = b, dimpled if b < a < 2b, convex if a ≥ 2b | a, b > 0 |
| pre.polar.rose | formula | Rose | `r = a*cos(n*θ)`: n petals if n is odd, 2n if even | n ∈ ℤ, n ≥ 2 |
| pre.polar.lemniscate | formula | Lemniscate | `r^2 = a^2*cos(2θ)` | |
| pre.polar.spiral | formula | Archimedean spiral | `r = a*θ` | |

## Complex numbers in polar form

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.cplx.polar | formula | Polar (trigonometric) form | `z = r*(cos(θ) + I*sin(θ)) = r*cis(θ) = r*e^(I*θ)` with `r = abs(z)`, `θ = arg(z)` | z ≠ 0 |
| pre.cplx.principal-arg | convention | Principal argument | `arg(z)` ∈ (−π, π] | |
| pre.cplx.mul | theorem | Multiplication | `r1*cis(θ1)*r2*cis(θ2) = r1*r2*cis(θ1 + θ2)` | |
| pre.cplx.div | theorem | Division | `r1*cis(θ1)/(r2*cis(θ2)) = (r1/r2)*cis(θ1 - θ2)` | r2 ≠ 0 |
| pre.cplx.de-moivre | theorem | De Moivre's theorem | `(r*cis(θ))^n = r^n*cis(n*θ)` | n ∈ ℤ |
| pre.cplx.nth-roots | theorem | n-th roots | The n-th roots of `r*cis(θ)` are `root(r, n)*cis((θ + 2*pi*k)/n)`, k = 0, …, n − 1 | r > 0, n ≥ 1 |
| pre.cplx.roots-of-unity | definition | Roots of unity | `ω_k = e^(2*pi*I*k/n)`, k = 0, …, n − 1 | |
| pre.cplx.roots-of-unity-sum | theorem | Sum of the roots of unity | `sum(e^(2*pi*I*k/n), k, 0, n - 1) = 0` | n ≥ 2 |
| pre.cplx.arg-product | theorem | Argument of a product | `arg(z*w) ≡ arg(z) + arg(w) (mod 2π)` | z, w ≠ 0 |

## Vectors

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.vec.magnitude | formula | Magnitude | `norm((a, b)) = sqrt(a^2 + b^2)` (3D adds c²) | |
| pre.vec.unit | formula | Unit vector | `u = v/norm(v)` | v ≠ 0 |
| pre.vec.direction | formula | Direction form | `v = norm(v)*(cos(θ), sin(θ))` | 2D |
| pre.vec.add-scale | definition | Addition and scalar multiplication | Componentwise | |
| pre.vec.dot | formula | Dot product | `u·v = u1*v1 + u2*v2 (+ u3*v3) = norm(u)*norm(v)*cos(θ)` | |
| pre.vec.angle | formula | Angle between vectors | `cos(θ) = (u·v)/(norm(u)*norm(v))` | u, v ≠ 0 |
| pre.vec.orthogonal | theorem | Orthogonality | `u ⊥ v <=> u·v = 0` | |
| pre.vec.projection | formula | Vector projection | `proj(u, v) = ((u·v)/norm(v)^2)*v` | v ≠ 0 |
| pre.vec.scalar-projection | formula | Scalar projection | `comp(u, v) = (u·v)/norm(v)` | v ≠ 0 |
| pre.vec.work | formula | Work | `W = F·d` | Constant force |

## Sequences and series

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| pre.seq.def | definition | Sequence | A function ℕ → ℝ (or from {n₀, n₀ + 1, …}) | |
| pre.seq.arith-term | formula | Arithmetic sequence | `a(n) = a1 + (n - 1)*d` | |
| pre.seq.arith-sum | formula | Arithmetic series | `S(n) = n*(a1 + a(n))/2 = n*(2a1 + (n - 1)*d)/2` | |
| pre.seq.geom-term | formula | Geometric sequence | `a(n) = a1*r^(n - 1)` | |
| pre.seq.geom-sum | formula | Geometric series | `S(n) = a1*(1 - r^n)/(1 - r)` | r ≠ 1 (S = n·a1 if r = 1) |
| pre.seq.geom-infinite | theorem | Infinite geometric series | `sum(a1*r^(k - 1), k, 1, oo) = a1/(1 - r)` | abs(r) < 1; diverges if abs(r) ≥ 1 and a1 ≠ 0 |
| pre.sigma.constant-multiple | law | Constant multiple | `sum(c*a(k), k, m, n) = c*sum(a(k), k, m, n)` | |
| pre.sigma.additivity | law | Sum of sums | `sum(a(k) + b(k), k, m, n) = sum(a(k), k, m, n) + sum(b(k), k, m, n)` | |
| pre.sigma.constant | formula | Sum of a constant | `sum(c, k, 1, n) = n*c` | |
| pre.sigma.integers | formula | Sum of integers | `sum(k, k, 1, n) = n*(n + 1)/2` | |
| pre.sigma.squares | formula | Sum of squares | `sum(k^2, k, 1, n) = n*(n + 1)*(2n + 1)/6` | |
| pre.sigma.cubes | formula | Sum of cubes | `sum(k^3, k, 1, n) = (n*(n + 1)/2)^2` | |
| pre.sigma.shift | law | Index shift | `sum(a(k), k, m, n) = sum(a(k + j), k, m - j, n - j)` | |
| pre.sigma.telescoping | law | Telescoping sum | `sum(b(k + 1) - b(k), k, m, n) = b(n + 1) - b(m)` | |
| pre.seq.fibonacci | definition | Fibonacci sequence | `F(0) = 0`, `F(1) = 1`, `F(n) = F(n - 1) + F(n - 2)` | |
| pre.seq.pascal | theorem | Pascal's rule | `binomial(n, k) = binomial(n - 1, k - 1) + binomial(n - 1, k)` | 1 ≤ k ≤ n − 1 |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Domain, range and full function analysis | See the example below | 2 |
| Describe transformations from a parent function | `y = -2sqrt(x + 3) + 1`: left 3, reflect across the x-axis, stretch by 2, up 1 | 2 |
| Find and verify inverses, with domain restrictions | `f(x) = (2x + 1)/(x - 3)` → `f^-1(x) = (3x + 1)/(x - 2)` | 2 |
| Compose functions with domains | `f(x) = sqrt(x)`, `g(x) = x - 2`: dom(f ∘ g) = [2, ∞) | 2 |
| Classify and analyze conics, including rotated ones | `x^2 + 4y^2 - 6x + 16y + 21 = 0` → ellipse centered (3, −2), a = 2, b = 1 | 2 |
| Eliminate parameters; convert polar ↔ rectangular | `r = 4cos(θ)` → `(x - 2)^2 + y^2 = 4` | 2 |
| Complex numbers in polar form, powers and roots | Cube roots of `8I` | 2 |
| Vector operations, angles and projections | | 2 |
| Arithmetic and geometric sequences and sums; sigma notation | `sum(3*(1/2)^k, k, 0, oo) = 6` | 2 |
| Fit exponential and logistic models to two or more data points | | 2 |

### Example: analyze `f(x) = (x^2 - 1)/(x^2 - 4)`

| Feature | Result | Cited entries |
| --- | --- | --- |
| Domain | ℝ ∖ {−2, 2} | `pre.fn.natural-domain` |
| Symmetry | Even (y-axis) | `pre.fn.even` |
| Intercepts | x = ±1; y = 1/4 | `pre.fn.zero` |
| Vertical asymptotes | x = −2, x = 2 (no common factors) | `pre.rat.vertical-asymptote` |
| Horizontal asymptote | y = 1 (equal degrees, leading coefficients 1/1) | `pre.rat.horizontal-asymptote` |
| Range | (−∞, 1/4] ∪ (1, ∞) | Solve y = f(x) for x |
| Increasing / decreasing | Increasing on (−∞, −2) and (−2, 0); decreasing on (0, 2) and (2, ∞) | `f'(x) = -6x/(x^2 - 4)^2` (Calculus1 level) |
| Local maximum | (0, 1/4) | `calc.app.first-derivative-test` |
