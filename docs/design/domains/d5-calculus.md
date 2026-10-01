# D5 Calculus

Calculus is the largest catalog domain: limits, continuity, derivatives, integrals, series, and multivariable and vector calculus. Every derivative rule and antiderivative is a catalog entry the engines apply and cite; every antiderivative is verified by differentiating it back, and every definite integral by quadrature.

- **Prefix:** `calc` · **Course tag:** `Calculus` · **Completed in:** Milestone 4 (single-variable core in Milestone 1)
- **Packages:** `Mathesis` (`Mathesis.Calculus`), `Mathesis.Symbolics` (`PowerSeries`), `Mathesis.Numerics` (cross-checks)

## Scope

Limits (laws, special limits, indeterminate forms, L'Hôpital); continuity (IVT, EVT); derivatives (definition, rules, full function table, implicit, logarithmic, inverse-function, higher-order, parametric and polar); differential-calculus theorems (Rolle, MVT, Cauchy MVT, Fermat); applications (tangent lines, linearization, related rates, extrema, concavity, optimization, motion, curve sketching); integrals (antiderivative table, substitution, parts, reduction formulas, trigonometric integrals and substitutions, partial fractions, Riemann sums, FTC, improper integrals); applications of integration (area, volume, arc length, surface area, work, centroids); sequences and series (convergence tests, power and Taylor series, remainder bounds); multivariable calculus; vector calculus.

## Types

| Type | Purpose |
| --- | --- |
| `LimitResult` | `Finite(value)`, `PositiveInfinity`, `NegativeInfinity`, `DoesNotExist(reason)` with one-sided values |
| `Antiderivative` | Expression + C, method used, verification flag |
| `DefiniteIntegral` | Exact value (when found), numeric value with error estimate, discontinuities handled |
| `PowerSeries` | Center, coefficients (lazy), order term, radius of convergence |
| `Convergence` | `Converges`, `ConvergesAbsolutely`, `ConvergesConditionally`, `Diverges`, `Unknown` + the test applied |
| `CriticalPoint` | Location, value, classification (local min/max, saddle, inconclusive), test used |
| `RiemannSum` | Rule (left, right, midpoint, trapezoid), n, value, rectangles for plotting |
| `VectorField` | Components as `Expr`, divergence, curl, conservativeness and potential |

## Limits

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.lim.def | definition | ε–δ limit | `limit(f(x), x, a) = L <=> forall ε > 0: exists δ > 0: forall x: 0 < abs(x - a) < δ => abs(f(x) - L) < ε` | |
| calc.lim.one-sided | theorem | Two-sided limit from one-sided limits | `limit(f, x, a) = L <=> limit(f, x, a, "-") = L and limit(f, x, a, "+") = L` | |
| calc.lim.infinite | definition | Infinite limit | `limit(f, x, a) = oo <=> forall M: exists δ > 0: 0 < abs(x - a) < δ => f(x) > M` | |
| calc.lim.at-infinity | definition | Limit at infinity | `limit(f, x, oo) = L <=> forall ε > 0: exists N: x > N => abs(f(x) - L) < ε` | |
| calc.lim.sum | law | Sum law | `limit(f + g, x, a) = limit(f, x, a) + limit(g, x, a)` | Both limits finite |
| calc.lim.constant-multiple | law | Constant multiple law | `limit(c*f, x, a) = c*limit(f, x, a)` | Limit finite |
| calc.lim.product | law | Product law | `limit(f*g, x, a) = limit(f, x, a)*limit(g, x, a)` | Both finite |
| calc.lim.quotient | law | Quotient law | `limit(f/g, x, a) = limit(f, x, a)/limit(g, x, a)` | Both finite, limit of g ≠ 0 |
| calc.lim.power | law | Power law | `limit(f^n, x, a) = limit(f, x, a)^n` | n ∈ ℕ, limit finite |
| calc.lim.root | law | Root law | `limit(root(f, n), x, a) = root(limit(f, x, a), n)` | n odd, or limit > 0 |
| calc.lim.composition | theorem | Limit of a composition | `limit(g(f(x)), x, a) = g(limit(f(x), x, a))` | g continuous at the inner limit |
| calc.lim.direct-substitution | theorem | Direct substitution | `limit(f(x), x, a) = f(a)` | f continuous at a |
| calc.lim.squeeze | theorem | Squeeze theorem | `g <= f <= h` near a and `limit(g) = limit(h) = L` ⇒ `limit(f) = L` | |
| calc.lim.sin-over-x | law | Fundamental trigonometric limit | `limit(sin(x)/x, x, 0) = 1` | |
| calc.lim.one-minus-cos-over-x | law | Cosine limit | `limit((1 - cos(x))/x, x, 0) = 0` | |
| calc.lim.one-minus-cos-over-x2 | law | Cosine limit (second order) | `limit((1 - cos(x))/x^2, x, 0) = 1/2` | |
| calc.lim.tan-over-x | law | Tangent limit | `limit(tan(x)/x, x, 0) = 1` | |
| calc.lim.exp-minus-one | law | Exponential limit | `limit((e^x - 1)/x, x, 0) = 1` | |
| calc.lim.power-minus-one | law | General exponential limit | `limit((a^x - 1)/x, x, 0) = ln(a)` | a > 0 |
| calc.lim.log-one-plus | law | Logarithm limit | `limit(ln(1 + x)/x, x, 0) = 1` | |
| calc.lim.e-sequence | law | Definition of e as a limit | `limit((1 + 1/n)^n, n, oo) = e` | |
| calc.lim.e-general | law | Compound growth limit | `limit((1 + a/n)^n, n, oo) = e^a` | |
| calc.lim.e-zero | law | e as a limit at 0 | `limit((1 + x)^(1/x), x, 0) = e` | |
| calc.lim.x-ln-x | law | x ln x at 0 | `limit(x*ln(x), x, 0, "+") = 0` | |
| calc.lim.x-to-x | law | xˣ at 0 | `limit(x^x, x, 0, "+") = 1` | |
| calc.lim.nth-root-n | law | n-th root of n | `limit(n^(1/n), n, oo) = 1` | |
| calc.lim.geometric | law | Powers of a fraction | `limit(r^n, n, oo) = 0` | abs(r) < 1 |
| calc.lim.arctan-infinity | law | Arctangent at infinity | `limit(arctan(x), x, oo) = pi/2` | |
| calc.lim.growth-hierarchy | theorem | Growth hierarchy | As x → ∞: `ln(x)^q ≪ x^p ≪ b^x ≪ x! ≪ x^x` (each ratio → 0) | p, q > 0, b > 1 |
| calc.lim.rational-at-infinity | theorem | Rational functions at infinity | Limit is 0, the ratio of leading coefficients, or ±∞ by comparing degrees | |
| calc.lim.indeterminate-forms | definition | Indeterminate forms | `0/0, oo/oo, 0*oo, oo - oo, 0^0, 1^oo, oo^0` | |
| calc.lim.lhopital | theorem | L'Hôpital's rule | `limit(f/g, x, a) = limit(f'/g', x, a)` | f/g of form 0/0 or ±∞/±∞; f, g differentiable near a; g' ≠ 0 near a; right side exists or is ±∞ |
| calc.lim.log-trick | method | Exponential indeterminate forms | For `0^0, 1^oo, oo^0`: take ln, find the limit L of the log, answer e^L | |

## Continuity

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.cont.def | definition | Continuity at a point | `limit(f(x), x, a) = f(a)` | a in the domain |
| calc.cont.removable | definition | Removable discontinuity | Limit exists but differs from f(a) or f(a) undefined | |
| calc.cont.jump | definition | Jump discontinuity | One-sided limits exist and differ | |
| calc.cont.infinite | definition | Infinite discontinuity | A one-sided limit is infinite | |
| calc.cont.oscillating | definition | Oscillating discontinuity | No one-sided limit, e.g. sin(1/x) at 0 | |
| calc.cont.algebra | theorem | Combinations of continuous functions | Sums, products, quotients (denominator ≠ 0) and compositions of continuous functions are continuous | |
| calc.cont.elementary | theorem | Elementary functions are continuous on their domains | | |
| calc.cont.ivt | theorem | Intermediate Value Theorem | f continuous on [a, b], N between f(a) and f(b) ⇒ `exists c in [a, b]: f(c) = N` | |
| calc.cont.evt | theorem | Extreme Value Theorem | f continuous on [a, b] attains an absolute maximum and minimum on [a, b] | Closed, bounded interval |
| calc.cont.differentiable | theorem | Differentiable implies continuous | f differentiable at a ⇒ f continuous at a | Converse false: abs(x) at 0 |

## Derivatives: definition and rules

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.deriv.def | definition | Derivative | `f'(x) = limit((f(x + h) - f(x))/h, h, 0)` | Limit exists |
| calc.deriv.def-alt | definition | Derivative at a point (alternative) | `f'(a) = limit((f(x) - f(a))/(x - a), x, a)` | |
| calc.deriv.constant | law | Constant rule | `diff(c, x) = 0` | c free of x |
| calc.deriv.power | law | Power rule | `diff(x^n, x) = n*x^(n - 1)` | n ∈ ℝ, where xⁿ is defined (x ≠ 0 if n < 1) |
| calc.deriv.constant-multiple | law | Constant multiple rule | `diff(c*f, x) = c*diff(f, x)` | c free of x |
| calc.deriv.sum | law | Sum rule | `diff(f + g, x) = diff(f, x) + diff(g, x)` | |
| calc.deriv.product | law | Product rule | `diff(f*g, x) = diff(f, x)*g + f*diff(g, x)` | |
| calc.deriv.quotient | law | Quotient rule | `diff(f/g, x) = (diff(f, x)*g - f*diff(g, x))/g^2` | g ≠ 0 |
| calc.deriv.chain | law | Chain rule | `diff(f(g(x)), x) = f'(g(x))*diff(g(x), x)` | |
| calc.deriv.reciprocal | law | Reciprocal rule | `diff(1/g, x) = -diff(g, x)/g^2` | g ≠ 0 |
| calc.deriv.general-power | law | General power rule | `diff(u^n, x) = n*u^(n - 1)*diff(u, x)` | |
| calc.deriv.inverse-function | theorem | Derivative of an inverse | `(f^-1)'(y) = 1/f'(f^-1(y))` | f′ ≠ 0 at f⁻¹(y) |
| calc.deriv.implicit | method | Implicit differentiation | Differentiate both sides of F(x, y) = 0 treating y as y(x); solve for dy/dx; equivalently `dy/dx = -F_x/F_y` | F_y ≠ 0 |
| calc.deriv.logarithmic | method | Logarithmic differentiation | Take ln of both sides, differentiate, multiply by y | y > 0 (or use ln abs(y)) |
| calc.deriv.var-power | law | Variable base and exponent | `diff(u^v, x) = u^v*(diff(v, x)*ln(u) + v*diff(u, x)/u)` | u > 0 |
| calc.deriv.leibniz | law | General Leibniz rule | `diff(f*g, x, n) = sum(binomial(n, k)*diff(f, x, k)*diff(g, x, n - k), k, 0, n)` | n ∈ ℕ |
| calc.deriv.parametric | formula | Parametric derivative | `dy/dx = (dy/dt)/(dx/dt)`; `d^2y/dx^2 = (d/dt (dy/dx))/(dx/dt)` | dx/dt ≠ 0 |
| calc.deriv.polar | formula | Slope of a polar curve | `dy/dx = (r'*sin(θ) + r*cos(θ))/(r'*cos(θ) - r*sin(θ))` | Denominator ≠ 0 |

## Derivative table

| ID | Function | Derivative | Conditions |
| --- | --- | --- | --- |
| calc.dtab.exp | `e^x` | `e^x` | |
| calc.dtab.exp-base | `a^x` | `a^x*ln(a)` | a > 0 |
| calc.dtab.ln | `ln(x)` | `1/x` | x > 0 |
| calc.dtab.ln-abs | `ln(abs(x))` | `1/x` | x ≠ 0 |
| calc.dtab.log-base | `log(x, a)` | `1/(x*ln(a))` | x > 0, a > 0, a ≠ 1 |
| calc.dtab.sqrt | `sqrt(x)` | `1/(2sqrt(x))` | x > 0 |
| calc.dtab.abs | `abs(x)` | `sign(x)` | x ≠ 0 |
| calc.dtab.sin | `sin(x)` | `cos(x)` | |
| calc.dtab.cos | `cos(x)` | `-sin(x)` | |
| calc.dtab.tan | `tan(x)` | `sec(x)^2` | cos x ≠ 0 |
| calc.dtab.cot | `cot(x)` | `-csc(x)^2` | sin x ≠ 0 |
| calc.dtab.sec | `sec(x)` | `sec(x)*tan(x)` | cos x ≠ 0 |
| calc.dtab.csc | `csc(x)` | `-csc(x)*cot(x)` | sin x ≠ 0 |
| calc.dtab.arcsin | `arcsin(x)` | `1/sqrt(1 - x^2)` | abs(x) < 1 |
| calc.dtab.arccos | `arccos(x)` | `-1/sqrt(1 - x^2)` | abs(x) < 1 |
| calc.dtab.arctan | `arctan(x)` | `1/(1 + x^2)` | |
| calc.dtab.arccot | `arccot(x)` | `-1/(1 + x^2)` | |
| calc.dtab.arcsec | `arcsec(x)` | `1/(abs(x)*sqrt(x^2 - 1))` | abs(x) > 1 |
| calc.dtab.arccsc | `arccsc(x)` | `-1/(abs(x)*sqrt(x^2 - 1))` | abs(x) > 1 |
| calc.dtab.sinh | `sinh(x)` | `cosh(x)` | |
| calc.dtab.cosh | `cosh(x)` | `sinh(x)` | |
| calc.dtab.tanh | `tanh(x)` | `sech(x)^2` | |
| calc.dtab.coth | `coth(x)` | `-csch(x)^2` | x ≠ 0 |
| calc.dtab.sech | `sech(x)` | `-sech(x)*tanh(x)` | |
| calc.dtab.csch | `csch(x)` | `-csch(x)*coth(x)` | x ≠ 0 |
| calc.dtab.arsinh | `arsinh(x)` | `1/sqrt(x^2 + 1)` | |
| calc.dtab.arcosh | `arcosh(x)` | `1/sqrt(x^2 - 1)` | x > 1 |
| calc.dtab.artanh | `artanh(x)` | `1/(1 - x^2)` | abs(x) < 1 |
| calc.dtab.erf | `erf(x)` | `2/sqrt(pi)*e^(-x^2)` | |
| calc.dtab.gamma | `gamma(x)` | `gamma(x)*digamma(x)` | x not a non-positive integer |
| calc.dtab.lambertw | `lambertw(x)` | `lambertw(x)/(x*(1 + lambertw(x)))` | x ≠ 0, x > −1/e |

## Theorems of differential calculus

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| calc.thm.fermat | Fermat's theorem | f has a local extremum at c and f′(c) exists ⇒ f′(c) = 0 | c interior |
| calc.thm.rolle | Rolle's theorem | f(a) = f(b) ⇒ `exists c in (a, b): f'(c) = 0` | f continuous on [a, b], differentiable on (a, b) |
| calc.thm.mean-value | Mean Value Theorem | `exists c in (a, b): f'(c) = (f(b) - f(a))/(b - a)` | Same as Rolle |
| calc.thm.cauchy-mean-value | Cauchy Mean Value Theorem | `exists c in (a, b): (f(b) - f(a))*g'(c) = (g(b) - g(a))*f'(c)` | f, g continuous on [a, b], differentiable on (a, b) |
| calc.thm.monotonicity | Monotonicity test | f′ > 0 on I ⇒ f increasing on I; f′ < 0 ⇒ decreasing | I an interval |
| calc.thm.zero-derivative | Zero derivative | f′ = 0 on an interval ⇒ f is constant there | |
| calc.thm.same-derivative | Same derivative | f′ = g′ on an interval ⇒ f − g is constant there | |
| calc.thm.darboux | Darboux's theorem | Derivatives have the intermediate value property | f differentiable on [a, b] |

## Applications of derivatives

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.app.tangent-line | formula | Tangent line | `y = f(a) + f'(a)*(x - a)` | |
| calc.app.normal-line | formula | Normal line | `y = f(a) - (x - a)/f'(a)` | f′(a) ≠ 0 |
| calc.app.linearization | formula | Linear approximation | `L(x) = f(a) + f'(a)*(x - a)` | |
| calc.app.differential | formula | Differential | `dy = f'(x)*dx` | |
| calc.app.related-rates | method | Related rates | Relate the quantities, differentiate with respect to t, substitute known values | |
| calc.app.critical-point | definition | Critical point | c in the domain with f′(c) = 0 or f′(c) undefined | |
| calc.app.first-derivative-test | theorem | First derivative test | f′ changes + to − at c ⇒ local maximum; − to + ⇒ local minimum; no change ⇒ neither | c critical, f continuous at c |
| calc.app.second-derivative-test | theorem | Second derivative test | f′(c) = 0 and f″(c) > 0 ⇒ local minimum; f″(c) < 0 ⇒ local maximum; f″(c) = 0 inconclusive | |
| calc.app.concavity | theorem | Concavity test | f″ > 0 on I ⇒ concave up; f″ < 0 ⇒ concave down | |
| calc.app.inflection | definition | Inflection point | Point where f is continuous and concavity changes | |
| calc.app.closed-interval | method | Closed interval method | Absolute extrema on [a, b] are among f at critical points and at a, b | f continuous on [a, b] |
| calc.app.optimization | method | Optimization | Model, constrain to one variable, find critical points, justify the extremum | |
| calc.app.motion | formula | Motion | `v = s'`, `a = v' = s''`, speed = abs(v) | |
| calc.app.marginal | formula | Marginal analysis | Marginal cost C′(x), revenue R′(x), profit P′(x) = R′(x) − C′(x) | |
| calc.app.curve-sketch | method | Curve sketching | Domain, intercepts, symmetry, asymptotes, f′ (monotonicity, extrema), f″ (concavity, inflection) | |
| calc.app.newton | formula | Newton's method | `x(n + 1) = x(n) - f(x(n))/f'(x(n))` | See D9 for convergence |

## Antiderivatives

Each entry also implies its constant of integration C. All are tagged `antiderivative` for the integration engine.

| ID | Integral | Result | Conditions |
| --- | --- | --- | --- |
| calc.itab.power | `integrate(x^n, x)` | `x^(n + 1)/(n + 1)` | n ≠ −1 |
| calc.itab.reciprocal | `integrate(1/x, x)` | `ln(abs(x))` | x ≠ 0 |
| calc.itab.exp | `integrate(e^x, x)` | `e^x` | |
| calc.itab.exp-linear | `integrate(e^(a*x), x)` | `e^(a*x)/a` | a ≠ 0 |
| calc.itab.exp-base | `integrate(a^x, x)` | `a^x/ln(a)` | a > 0, a ≠ 1 |
| calc.itab.ln | `integrate(ln(x), x)` | `x*ln(x) - x` | x > 0 |
| calc.itab.sin | `integrate(sin(x), x)` | `-cos(x)` | |
| calc.itab.cos | `integrate(cos(x), x)` | `sin(x)` | |
| calc.itab.tan | `integrate(tan(x), x)` | `-ln(abs(cos(x)))` | cos x ≠ 0 |
| calc.itab.cot | `integrate(cot(x), x)` | `ln(abs(sin(x)))` | sin x ≠ 0 |
| calc.itab.sec | `integrate(sec(x), x)` | `ln(abs(sec(x) + tan(x)))` | cos x ≠ 0 |
| calc.itab.csc | `integrate(csc(x), x)` | `ln(abs(csc(x) - cot(x)))` | sin x ≠ 0 |
| calc.itab.sec-sq | `integrate(sec(x)^2, x)` | `tan(x)` | |
| calc.itab.csc-sq | `integrate(csc(x)^2, x)` | `-cot(x)` | |
| calc.itab.sec-tan | `integrate(sec(x)*tan(x), x)` | `sec(x)` | |
| calc.itab.csc-cot | `integrate(csc(x)*cot(x), x)` | `-csc(x)` | |
| calc.itab.sin-sq | `integrate(sin(x)^2, x)` | `x/2 - sin(2x)/4` | |
| calc.itab.cos-sq | `integrate(cos(x)^2, x)` | `x/2 + sin(2x)/4` | |
| calc.itab.sinh | `integrate(sinh(x), x)` | `cosh(x)` | |
| calc.itab.cosh | `integrate(cosh(x), x)` | `sinh(x)` | |
| calc.itab.arcsin-form | `integrate(1/sqrt(a^2 - x^2), x)` | `arcsin(x/a)` | a > 0, abs(x) < a |
| calc.itab.arctan-form | `integrate(1/(a^2 + x^2), x)` | `arctan(x/a)/a` | a ≠ 0 |
| calc.itab.arcsec-form | `integrate(1/(x*sqrt(x^2 - a^2)), x)` | `arcsec(abs(x)/a)/a` | a > 0, abs(x) > a |
| calc.itab.log-diff-squares | `integrate(1/(x^2 - a^2), x)` | `ln(abs((x - a)/(x + a)))/(2a)` | a ≠ 0, x ≠ ±a |
| calc.itab.arsinh-form | `integrate(1/sqrt(x^2 + a^2), x)` | `ln(x + sqrt(x^2 + a^2))` | a ≠ 0 |
| calc.itab.arcosh-form | `integrate(1/sqrt(x^2 - a^2), x)` | `ln(abs(x + sqrt(x^2 - a^2)))` | abs(x) > abs(a) |
| calc.itab.semicircle | `integrate(sqrt(a^2 - x^2), x)` | `x*sqrt(a^2 - x^2)/2 + a^2*arcsin(x/a)/2` | a > 0, abs(x) ≤ a |
| calc.itab.exp-sin | `integrate(e^(a*x)*sin(b*x), x)` | `e^(a*x)*(a*sin(b*x) - b*cos(b*x))/(a^2 + b^2)` | a² + b² ≠ 0 |
| calc.itab.exp-cos | `integrate(e^(a*x)*cos(b*x), x)` | `e^(a*x)*(a*cos(b*x) + b*sin(b*x))/(a^2 + b^2)` | a² + b² ≠ 0 |
| calc.itab.x-exp | `integrate(x*e^x, x)` | `(x - 1)*e^x` | |
| calc.itab.gaussian | `integrate(e^(-x^2), x)` | `sqrt(pi)*erf(x)/2` | Non-elementary (Liouville) |
| calc.itab.sinc | `integrate(sin(x)/x, x)` | `si(x)` | Non-elementary |
| calc.itab.inv-log | `integrate(1/ln(x), x)` | `li(x)` | x > 0, x ≠ 1; non-elementary |

## Integration techniques

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.int.antiderivative | definition | Antiderivative | F is an antiderivative of f on I if F′ = f on I; all antiderivatives are F + C | I an interval |
| calc.int.linearity | law | Linearity | `integrate(a*f + b*g, x) = a*integrate(f, x) + b*integrate(g, x)` | a, b free of x |
| calc.int.substitution | law | Substitution | `integrate(f(g(x))*g'(x), x) = integrate(f(u), u)` with u = g(x) | |
| calc.int.substitution-definite | law | Definite substitution | `integrate(f(g(x))*g'(x), x, a, b) = integrate(f(u), u, g(a), g(b))` | g′ continuous on [a, b] |
| calc.int.parts | law | Integration by parts | `integrate(u*diff(v, x), x) = u*v - integrate(v*diff(u, x), x)` | |
| calc.int.parts-definite | law | Definite integration by parts | `integrate(u*v', x, a, b) = (u*v)(b) - (u*v)(a) - integrate(v*u', x, a, b)` | |
| calc.int.liate | method | LIATE heuristic | Choose u in the order logarithmic, inverse trig, algebraic, trig, exponential | |
| calc.int.tabular | method | Tabular integration | Repeated parts for polynomial × exp/sin/cos | |
| calc.int.reduce-sin | formula | Reduction formula (sin) | `integrate(sin(x)^n, x) = -sin(x)^(n - 1)*cos(x)/n + (n - 1)/n*integrate(sin(x)^(n - 2), x)` | n ≥ 2 |
| calc.int.reduce-cos | formula | Reduction formula (cos) | `integrate(cos(x)^n, x) = cos(x)^(n - 1)*sin(x)/n + (n - 1)/n*integrate(cos(x)^(n - 2), x)` | n ≥ 2 |
| calc.int.reduce-tan | formula | Reduction formula (tan) | `integrate(tan(x)^n, x) = tan(x)^(n - 1)/(n - 1) - integrate(tan(x)^(n - 2), x)` | n ≥ 2 |
| calc.int.reduce-sec | formula | Reduction formula (sec) | `integrate(sec(x)^n, x) = sec(x)^(n - 2)*tan(x)/(n - 1) + (n - 2)/(n - 1)*integrate(sec(x)^(n - 2), x)` | n ≥ 2 |
| calc.int.reduce-x-exp | formula | Reduction formula (xⁿeˣ) | `integrate(x^n*e^x, x) = x^n*e^x - n*integrate(x^(n - 1)*e^x, x)` | n ≥ 1 |
| calc.int.reduce-ln | formula | Reduction formula ((ln x)ⁿ) | `integrate(ln(x)^n, x) = x*ln(x)^n - n*integrate(ln(x)^(n - 1), x)` | n ≥ 1 |
| calc.int.trig-odd-sin | method | Odd power of sine | Save one sin, convert the rest with sin² = 1 − cos², substitute u = cos x | |
| calc.int.trig-odd-cos | method | Odd power of cosine | Save one cos, convert with cos² = 1 − sin², substitute u = sin x | |
| calc.int.trig-even | method | Even powers of sine and cosine | Power-reduce (`trig.power.*`) | |
| calc.int.trig-tan-sec | method | Tangent and secant powers | Even sec power: save sec², u = tan x; odd tan power: save sec·tan, u = sec x | |
| calc.int.trig-sub-sin | method | Trig substitution for √(a² − x²) | x = a·sin θ, −π/2 ≤ θ ≤ π/2 | a > 0 |
| calc.int.trig-sub-tan | method | Trig substitution for √(a² + x²) | x = a·tan θ, −π/2 < θ < π/2 | a > 0 |
| calc.int.trig-sub-sec | method | Trig substitution for √(x² − a²) | x = a·sec θ, θ ∈ [0, π/2) or [π, 3π/2) | a > 0 |
| calc.int.partial-fractions | method | Partial fractions | Divide if improper; decompose (`alg.pf.*`); integrate term by term | |
| calc.int.weierstrass | method | Weierstrass substitution | t = tan(x/2) for rational functions of sin and cos (`trig.weier.*`, `dx = 2/(1 + t^2) dt`) | |
| calc.int.rational-complete | theorem | Rational functions have elementary antiderivatives | Every rational function integrates to a rational function plus a sum of constant multiples of logarithms (and arctangents over ℝ) | Basis of the complete algorithm |
| calc.int.liouville | theorem | Liouville's theorem (elementary integrability) | If ∫f is elementary, it has the form `v0 + sum(c_i*ln(v_i))` with v_i in the field of f | Explains why e^(−x²) has no elementary antiderivative |

## Definite integrals

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.def.riemann-sum | definition | Riemann sum | `sum(f(x_k*)*Δx, k, 1, n)` with Δx = (b − a)/n; left, right, midpoint tags | |
| calc.def.integral | definition | Definite integral | Limit of Riemann sums as the mesh → 0 | f integrable |
| calc.def.trapezoid | formula | Trapezoidal sum | `T(n) = Δx/2*(f(x0) + 2f(x1) + … + 2f(x(n-1)) + f(xn))` | |
| calc.def.zero-width | law | Zero-width interval | `integrate(f, x, a, a) = 0` | |
| calc.def.reverse | law | Reversing limits | `integrate(f, x, b, a) = -integrate(f, x, a, b)` | |
| calc.def.additivity | law | Additivity over intervals | `integrate(f, x, a, b) + integrate(f, x, b, c) = integrate(f, x, a, c)` | f integrable on the hull |
| calc.def.comparison | theorem | Comparison | f ≤ g on [a, b] ⇒ `integrate(f, x, a, b) <= integrate(g, x, a, b)` | a ≤ b |
| calc.def.bounds | theorem | Bounds | `m*(b - a) <= integrate(f, x, a, b) <= M*(b - a)` | m ≤ f ≤ M on [a, b] |
| calc.def.abs-bound | theorem | Absolute value bound | `abs(integrate(f, x, a, b)) <= integrate(abs(f), x, a, b)` | a ≤ b |
| calc.def.even | law | Even integrand | `integrate(f, x, -a, a) = 2*integrate(f, x, 0, a)` | f even |
| calc.def.odd | law | Odd integrand | `integrate(f, x, -a, a) = 0` | f odd, integrable |
| calc.def.periodic | law | Periodic integrand | `integrate(f, x, a, a + p) = integrate(f, x, 0, p)` | f has period p |
| calc.ftc.part1 | theorem | Fundamental Theorem of Calculus, part 1 | `diff(integrate(f(t), t, a, x), x) = f(x)` | f continuous on an interval containing a and x |
| calc.ftc.part1-chain | theorem | FTC part 1 with a variable limit | `diff(integrate(f(t), t, a, g(x)), x) = f(g(x))*g'(x)` | f continuous, g differentiable |
| calc.ftc.part2 | theorem | Fundamental Theorem of Calculus, part 2 | `integrate(f, x, a, b) = F(b) - F(a)` | F′ = f and f continuous on [a, b] (F continuous on [a, b]) |
| calc.ftc.net-change | theorem | Net change theorem | `integrate(F'(x), x, a, b) = F(b) - F(a)` | F′ continuous |
| calc.def.mvt | theorem | Mean Value Theorem for integrals | `exists c in [a, b]: f(c) = integrate(f, x, a, b)/(b - a)` | f continuous on [a, b] |
| calc.def.average | formula | Average value | `f_avg = integrate(f, x, a, b)/(b - a)` | a < b |
| calc.def.leibniz-rule | theorem | Differentiation under the integral sign | `diff(integrate(f(x, t), t, a, b), x) = integrate(diff(f(x, t), x), t, a, b)` | f and ∂f/∂x continuous |

## Improper integrals

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.improper.type1 | definition | Infinite interval | `integrate(f, x, a, oo) = limit(integrate(f, x, a, t), t, oo)` | |
| calc.improper.type2 | definition | Discontinuous integrand | `integrate(f, x, a, b) = limit(integrate(f, x, a, t), t, b, "-")` | f unbounded near b |
| calc.improper.p-infinity | theorem | p-integral at infinity | `integrate(1/x^p, x, 1, oo)` converges iff p > 1, to 1/(p − 1) | |
| calc.improper.p-zero | theorem | p-integral at zero | `integrate(1/x^p, x, 0, 1)` converges iff p < 1, to 1/(1 − p) | |
| calc.improper.comparison | theorem | Comparison test | 0 ≤ f ≤ g: ∫g converges ⇒ ∫f converges; ∫f diverges ⇒ ∫g diverges | |
| calc.improper.limit-comparison | theorem | Limit comparison test | f, g > 0, f/g → c ∈ (0, ∞) ⇒ both converge or both diverge | |
| calc.improper.gaussian | law | Gaussian integral | `integrate(e^(-x^2), x, -oo, oo) = sqrt(pi)` | |
| calc.improper.gamma | law | Gamma integral | `integrate(x^n*e^(-x), x, 0, oo) = n!` (generally `gamma(s) = integrate(x^(s - 1)*e^(-x), x, 0, oo)`) | n ∈ ℕ; Re s > 0 |
| calc.improper.dirichlet | law | Dirichlet integral | `integrate(sin(x)/x, x, 0, oo) = pi/2` | Conditionally convergent |

## Applications of integration

| ID | Name | Formula | Conditions |
| --- | --- | --- | --- |
| calc.appint.area-between | Area between curves | `A = integrate(abs(f - g), x, a, b)` | |
| calc.appint.area-y | Area with respect to y | `A = integrate(abs(f(y) - g(y)), y, c, d)` | |
| calc.appint.disk | Disk method | `V = pi*integrate(R(x)^2, x, a, b)` | |
| calc.appint.washer | Washer method | `V = pi*integrate(R(x)^2 - r(x)^2, x, a, b)` | R ≥ r ≥ 0 |
| calc.appint.shell | Shell method | `V = 2*pi*integrate(radius(x)*height(x), x, a, b)` | |
| calc.appint.cross-section | Known cross-sections | `V = integrate(A(x), x, a, b)` | |
| calc.appint.arc-length | Arc length | `L = integrate(sqrt(1 + f'(x)^2), x, a, b)` | f′ continuous |
| calc.appint.arc-length-param | Parametric arc length | `L = integrate(sqrt(x'(t)^2 + y'(t)^2), t, α, β)` | |
| calc.appint.arc-length-polar | Polar arc length | `L = integrate(sqrt(r^2 + r'^2), θ, α, β)` | |
| calc.appint.polar-area | Polar area | `A = integrate(r(θ)^2, θ, α, β)/2` | |
| calc.appint.param-area | Area under a parametric curve | `A = integrate(y(t)*x'(t), t, α, β)` | |
| calc.appint.surface | Surface area of revolution (x-axis) | `S = 2*pi*integrate(f(x)*sqrt(1 + f'(x)^2), x, a, b)` | f ≥ 0 |
| calc.appint.work | Work | `W = integrate(F(x), x, a, b)` | |
| calc.appint.hooke | Hooke's law | `F = k*x` | Spring within elastic limit |
| calc.appint.hydrostatic | Hydrostatic force | `F = integrate(ρ*g*depth(y)*width(y), y, c, d)` | |
| calc.appint.centroid | Centroid of a region | `x̄ = integrate(x*(f - g), x, a, b)/A`, `ȳ = integrate((f^2 - g^2)/2, x, a, b)/A` | f ≥ g |
| calc.appint.pappus | Pappus's theorem | `V = 2*pi*r̄*A` (r̄ distance from the centroid to the axis) | Region on one side of the axis |
| calc.appint.displacement | Displacement and distance | Displacement `integrate(v, t, a, b)`, distance `integrate(abs(v), t, a, b)` | |
| calc.appint.pdf | Probability density | `integrate(f, x, -oo, oo) = 1`, mean `integrate(x*f, x, -oo, oo)` | f ≥ 0 |

## Sequences and series

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.series.sequence-limit | definition | Limit of a sequence | `limit(a(n), n, oo) = L <=> forall ε > 0: exists N: n > N => abs(a(n) - L) < ε` | |
| calc.series.monotone-convergence | theorem | Monotone convergence theorem | A bounded monotonic sequence converges | |
| calc.series.sum-def | definition | Sum of a series | `sum(a(k), k, 1, oo) = limit(S(n), n, oo)` with partial sums S(n) | |
| calc.sum.split-last | law | Split off the last term | `sum(a(k), k, m, n + 1) = sum(a(k), k, m, n) + a(n + 1)` | n ≥ m − 1 |
| calc.series.divergence-test | theorem | n-th term test | `limit(a(n), n, oo) != 0 => series diverges` | Converse false |
| calc.series.harmonic | theorem | Harmonic series diverges | `sum(1/k, k, 1, oo) = oo` | |
| calc.series.p-series | theorem | p-series | `sum(1/k^p, k, 1, oo)` converges iff p > 1 | |
| calc.series.integral-test | theorem | Integral test | f positive, continuous, decreasing with f(k) = a(k): the series and `integrate(f, x, 1, oo)` converge or diverge together | |
| calc.series.comparison | theorem | Direct comparison test | 0 ≤ a(k) ≤ b(k): ∑b converges ⇒ ∑a converges; ∑a diverges ⇒ ∑b diverges | |
| calc.series.limit-comparison | theorem | Limit comparison test | a(k), b(k) > 0 and a(k)/b(k) → c ∈ (0, ∞) ⇒ same behavior | |
| calc.series.alternating | theorem | Alternating series test | b(k) decreasing to 0 ⇒ `sum((-1)^k*b(k))` converges, with `abs(S - S(n)) <= b(n + 1)` | b(k) > 0 |
| calc.series.ratio | theorem | Ratio test | `L = limit(abs(a(k + 1)/a(k)), k, oo)`: L < 1 converges absolutely, L > 1 diverges, L = 1 inconclusive | |
| calc.series.root | theorem | Root test | `L = limit(abs(a(k))^(1/k), k, oo)`: same conclusions as the ratio test | |
| calc.series.absolute | theorem | Absolute convergence implies convergence | | |
| calc.series.rearrangement | theorem | Riemann rearrangement theorem | A conditionally convergent series can be rearranged to sum to any real number or diverge | |
| calc.series.telescoping | law | Telescoping series | `sum(b(k) - b(k + 1), k, 1, oo) = b(1) - limit(b(n), n, oo)` | Limit exists |
| calc.series.radius | theorem | Radius of convergence | A power series `sum(c(n)*(x - a)^n)` converges absolutely for abs(x − a) < R and diverges for abs(x − a) > R; `1/R = limit(abs(c(n + 1)/c(n)), n, oo)` when it exists | Endpoints checked separately |
| calc.series.termwise | theorem | Termwise differentiation and integration | Allowed inside the radius of convergence; the radius is unchanged | |
| calc.series.taylor | definition | Taylor series | `sum(diff(f, x, n)(a)/n!*(x - a)^n, n, 0, oo)` | f infinitely differentiable at a |
| calc.series.taylor-remainder | theorem | Taylor's theorem (Lagrange remainder) | `R(n, x) = diff(f, x, n + 1)(ξ)*(x - a)^(n + 1)/(n + 1)!` for some ξ between a and x | f ∈ Cⁿ⁺¹ |
| calc.series.taylor-inequality | theorem | Taylor's inequality | `abs(R(n, x)) <= M*abs(x - a)^(n + 1)/(n + 1)!` | abs(f⁽ⁿ⁺¹⁾) ≤ M between a and x |
| calc.mac.exp | law | Maclaurin series of eˣ | `e^x = sum(x^n/n!, n, 0, oo)` | All x |
| calc.mac.sin | law | Maclaurin series of sin | `sin(x) = sum((-1)^n*x^(2n + 1)/(2n + 1)!, n, 0, oo)` | All x |
| calc.mac.cos | law | Maclaurin series of cos | `cos(x) = sum((-1)^n*x^(2n)/(2n)!, n, 0, oo)` | All x |
| calc.mac.geometric | law | Geometric series | `1/(1 - x) = sum(x^n, n, 0, oo)` | abs(x) < 1 |
| calc.mac.ln | law | Maclaurin series of ln(1 + x) | `ln(1 + x) = sum((-1)^(n + 1)*x^n/n, n, 1, oo)` | −1 < x ≤ 1 |
| calc.mac.arctan | law | Maclaurin series of arctan | `arctan(x) = sum((-1)^n*x^(2n + 1)/(2n + 1), n, 0, oo)` | abs(x) ≤ 1 |
| calc.mac.binomial | law | Binomial series | `(1 + x)^k = sum(binomial(k, n)*x^n, n, 0, oo)` | abs(x) < 1, k ∈ ℝ |
| calc.mac.sinh | law | Maclaurin series of sinh | `sinh(x) = sum(x^(2n + 1)/(2n + 1)!, n, 0, oo)` | All x |
| calc.mac.cosh | law | Maclaurin series of cosh | `cosh(x) = sum(x^(2n)/(2n)!, n, 0, oo)` | All x |
| calc.known.basel | law | Basel problem | `sum(1/k^2, k, 1, oo) = pi^2/6` | |
| calc.known.alternating-harmonic | law | Alternating harmonic series | `sum((-1)^(k + 1)/k, k, 1, oo) = ln(2)` | |
| calc.known.leibniz-pi | law | Leibniz series for π | `sum((-1)^k/(2k + 1), k, 0, oo) = pi/4` | |
| calc.known.e | law | Series for e | `sum(1/k!, k, 0, oo) = e` | |

## Multivariable calculus

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.multi.limit-paths | theorem | Two-path test | Different limits along two paths ⇒ the limit does not exist | |
| calc.multi.partial | definition | Partial derivative | `diff(f, x) = limit((f(x + h, y) - f(x, y))/h, h, 0)` | |
| calc.multi.clairaut | theorem | Clairaut–Schwarz theorem | `diff(f, x, y) = diff(f, y, x)` | Second partials continuous near the point |
| calc.multi.gradient | definition | Gradient | `grad(f) = (diff(f, x), diff(f, y), diff(f, z))` | |
| calc.multi.directional | formula | Directional derivative | `D_u f = grad(f)·u` | u a unit vector, f differentiable |
| calc.multi.max-rate | theorem | Maximum rate of change | Maximum of D_u f is norm(grad f), in the direction of grad f | |
| calc.multi.gradient-normal | theorem | Gradient is normal to level sets | grad f(P) is orthogonal to the level curve or surface through P | grad f(P) ≠ 0 |
| calc.multi.tangent-plane | formula | Tangent plane | `z = f(a, b) + f_x(a, b)*(x - a) + f_y(a, b)*(y - b)` | f differentiable |
| calc.multi.total-differential | formula | Total differential | `dz = f_x*dx + f_y*dy` | |
| calc.multi.chain | law | Multivariable chain rule | `diff(f(x(t), y(t)), t) = f_x*x'(t) + f_y*y'(t)` | |
| calc.multi.implicit | formula | Implicit derivative | `dy/dx = -F_x/F_y` | F_y ≠ 0 |
| calc.multi.second-derivative-test | theorem | Second derivative test | At a critical point with `D = f_xx*f_yy - f_xy^2`: D > 0 and f_xx > 0 minimum; D > 0 and f_xx < 0 maximum; D < 0 saddle; D = 0 inconclusive | Continuous second partials |
| calc.multi.hessian | definition | Hessian | Matrix of second partial derivatives; definiteness classifies critical points in n variables | |
| calc.multi.lagrange | theorem | Lagrange multipliers | Extrema of f subject to g = c occur where `grad(f) = λ*grad(g)` | grad g ≠ 0 on the constraint |
| calc.multi.fubini | theorem | Fubini's theorem | Iterated integrals over a rectangle may be taken in either order | f continuous (or absolutely integrable) |
| calc.multi.polar-double | formula | Double integral in polar coordinates | `dA = r*dr*dθ` | |
| calc.multi.cylindrical | formula | Cylindrical coordinates | `dV = r*dz*dr*dθ` | |
| calc.multi.spherical | formula | Spherical coordinates | `x = ρ*sin(φ)*cos(θ)`, `y = ρ*sin(φ)*sin(θ)`, `z = ρ*cos(φ)`, `dV = ρ^2*sin(φ)*dρ*dφ*dθ` | 0 ≤ φ ≤ π |
| calc.multi.change-of-variables | theorem | Change of variables | `integrate(f, (x, y) in R) = integrate(f(x(u, v), y(u, v))*abs(J), (u, v) in S)` with Jacobian J = ∂(x, y)/∂(u, v) | Transformation one-to-one, C¹ |
| calc.multi.mass-center | formula | Mass and center of mass | `m = integrate(ρ, dA)`, `x̄ = integrate(x*ρ, dA)/m`, `ȳ = integrate(y*ρ, dA)/m` | |

## Vector calculus

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| calc.vec.divergence | definition | Divergence | `divergence(F) = diff(P, x) + diff(Q, y) + diff(R, z)` | F = (P, Q, R) |
| calc.vec.curl | definition | Curl | `curl(F) = (R_y - Q_z, P_z - R_x, Q_x - P_y)` | |
| calc.vec.laplacian | definition | Laplacian | `laplacian(f) = f_xx + f_yy + f_zz` | |
| calc.vec.line-scalar | formula | Line integral of a scalar field | `integrate(f, s on C) = integrate(f(r(t))*norm(r'(t)), t, a, b)` | |
| calc.vec.line-vector | formula | Line integral of a vector field | `integrate(F·dr on C) = integrate(F(r(t))·r'(t), t, a, b)` | |
| calc.vec.ftc-line | theorem | Fundamental theorem for line integrals | `integrate(grad(f)·dr on C) = f(r(b)) - f(r(a))` | f C¹ |
| calc.vec.conservative | theorem | Conservative fields | On a simply connected domain, F is conservative ⇔ curl F = 0 (in 2D: P_y = Q_x) ⇔ line integrals are path-independent | F C¹ |
| calc.vec.green | theorem | Green's theorem | `integrate(P*dx + Q*dy on ∂D) = integrate(Q_x - P_y, dA over D)` | ∂D positively oriented, piecewise smooth, simple |
| calc.vec.green-area | formula | Area by Green's theorem | `A = integrate(x*dy - y*dx on ∂D)/2` | |
| calc.vec.surface-integral | formula | Surface integral and flux | `integrate(f, dS over S)`, flux `integrate(F·n, dS over S)` | |
| calc.vec.stokes | theorem | Stokes' theorem | `integrate(F·dr on ∂S) = integrate(curl(F)·n, dS over S)` | S oriented, boundary positively oriented |
| calc.vec.divergence-theorem | theorem | Divergence theorem | `integrate(F·n, dS over ∂E) = integrate(divergence(F), dV over E)` | E solid with closed outward-oriented boundary |
| calc.vec.curl-grad | law | Curl of a gradient | `curl(grad(f)) = 0` | f C² |
| calc.vec.div-curl | law | Divergence of a curl | `divergence(curl(F)) = 0` | F C² |
| calc.vec.div-product | law | Product rule for divergence | `divergence(f*F) = f*divergence(F) + F·grad(f)` | |
| calc.vec.curl-product | law | Product rule for curl | `curl(f*F) = f*curl(F) + cross(grad(f), F)` | |
| calc.vec.div-cross | law | Divergence of a cross product | `divergence(cross(F, G)) = G·curl(F) - F·curl(G)` | |
| calc.vec.curl-curl | law | Curl of a curl | `curl(curl(F)) = grad(divergence(F)) - laplacian(F)` | Cartesian components |
| calc.vec.tangent | formula | Unit tangent, normal, binormal | `T = r'/norm(r')`, `N = T'/norm(T')`, `B = cross(T, N)` | |
| calc.vec.curvature | formula | Curvature | `κ = norm(cross(r', r''))/norm(r')^3` | |
| calc.vec.acceleration-components | formula | Tangential and normal acceleration | `a_T = (v·a)/norm(v)`, `a_N = norm(cross(v, a))/norm(v)` | v ≠ 0 |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Limits with method choice (algebraic, special limits, L'Hôpital, series) | `limit((x^2 - 9)/(x - 3), x, 3) = 6` | 1 (complete for exp-log: 4) |
| Derivatives of any elementary expression with nested chain-rule steps | See below | 1 |
| Implicit and logarithmic differentiation | `x^2 + y^2 = 25` → `dy/dx = -x/y` | 1 |
| Taylor and Maclaurin series to order n; radius of convergence | `taylor(ln(x), x, 1, 4)` | 1 |
| Indefinite integrals with method selection and verification | `integrate(x*e^x, x) = (x - 1)*e^x + C` | 1 |
| Complete integration of rational functions | `integrate(1/(x^3 + 1), x)` | 2 |
| Definite and improper integrals with discontinuity handling | `integrate(1/x^2, x, -1, 1)` diverges (not −2) | 1 / 4 |
| Applications: tangent lines, extrema, optimization, area, volume, arc length | | 2–4 |
| Series convergence with the test named | `sum(n!/n^n, n, 1, oo)` converges by the ratio test | 4 |
| Multivariable: partials, gradients, critical points, Lagrange multipliers, multiple integrals | | 4 |
| Vector calculus: divergence, curl, potentials, line and surface integrals, the three big theorems | | 4 |

### Example derivation

`diff(x^2*sin(3x), x)` (Standard verbosity; the chain rule shows its inner derivative as a nested step):

| Step | Expression | Cited entry |
| --- | --- | --- |
| 1 | `diff(x^2, x)*sin(3x) + x^2*diff(sin(3x), x)` | `calc.deriv.product` |
| 2 | `2x*sin(3x) + x^2*diff(sin(3x), x)` | `calc.deriv.power` |
| 3 | `2x*sin(3x) + x^2*cos(3x)*diff(3x, x)` | `calc.deriv.chain`, `calc.dtab.sin` |
| 3.1 | `diff(3x, x) = 3` | `calc.deriv.constant-multiple`, `calc.deriv.power` |
| 4 | `2x*sin(3x) + 3x^2*cos(3x)` | arithmetic |
