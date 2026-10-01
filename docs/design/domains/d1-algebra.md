# D1 Algebra

Algebra is the foundation every other domain rewrites through: the field and order axioms, the laws of fractions, exponents, radicals, absolute values and logarithms, polynomial and rational-expression algebra, and the rules that keep equation solving honest about lost or extraneous solutions.

- **Prefix:** `alg` · **Course tag:** `Algebra` · **Completed in:** Milestone 2 (core in Milestone 1)
- **Packages:** `Mathesis.Core` (`BigRational`, `Complex<T>`, polynomials), `Mathesis.Symbolics` (representations), `Mathesis` (`Mathesis.Algebra`, `Mathesis.Solving`)

## Scope

Real-number properties; arithmetic of fractions, percents and ratios; exponents and radicals; absolute value; logarithms; complex-number arithmetic; polynomials (operations, special products, factoring, division, roots); quadratics; rational expressions and partial fractions; linear, quadratic, polynomial, rational, radical, absolute-value, exponential and logarithmic equations; inequalities; systems; lines and variation; conventions (order of operations, rounding). Functions as objects, sequences and conics are in D4 Pre-Calculus; number theory is in D8.

## Types and operators

| Type or operator | Where | Notes |
| --- | --- | --- |
| `BigRational`, `Complex<BigRational>` | Core | Exact arithmetic for ℚ and ℚ(i) |
| `Polynomial<T>`, `SparsePolynomial<T>`, `RationalFunction<T>` | Core | Fast representations; convert to and from `Expr` |
| `Factorization` | Mathesis.Algebra | Unit, list of (factor, multiplicity), field |
| `QuadraticForm` record | Mathesis.Algebra | Standard, vertex and factored forms of ax² + bx + c with discriminant |
| `PartialFractions` | Mathesis.Algebra | Polynomial part plus terms A/(x − a)^k and (Bx + C)/(x² + px + q)^k |
| Operators | Symbolics | add, mul, pow, sqrt, root, abs, sign, floor, ceil, gcd, lcm, ln, log, exp, re, im, conj (see `05-syntax-trees-and-notation.md`) |

## Axioms and properties of real numbers

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.ax.add-assoc | Associativity of addition | `(a + b) + c = a + (b + c)` | |
| alg.ax.add-comm | Commutativity of addition | `a + b = b + a` | |
| alg.ax.add-identity | Additive identity | `a + 0 = a` | |
| alg.ax.add-inverse | Additive inverse | `a + (-a) = 0` | |
| alg.ax.mul-assoc | Associativity of multiplication | `(a*b)*c = a*(b*c)` | |
| alg.ax.mul-comm | Commutativity of multiplication | `a*b = b*a` | Scalars only (matrices: D7) |
| alg.ax.mul-identity | Multiplicative identity | `a*1 = a` | |
| alg.ax.mul-inverse | Multiplicative inverse | `a*(1/a) = 1` | a ≠ 0 |
| alg.ax.distributive | Distributive property | `a*(b + c) = a*b + a*c` | |
| alg.ax.zero-ne-one | Non-triviality | `0 != 1` | |
| alg.prop.mul-zero | Multiplication by zero | `a*0 = 0` | |
| alg.prop.neg-one | Multiplying by −1 | `(-1)*a = -a` | |
| alg.prop.double-neg | Double negation | `-(-a) = a` | |
| alg.prop.neg-sum | Opposite of a sum | `-(a + b) = -a - b` | |
| alg.prop.neg-product | Signs of products | `(-a)*b = -(a*b)`, `(-a)*(-b) = a*b` | |
| alg.prop.sub-def | Subtraction as addition | `a - b = a + (-b)` | |
| alg.prop.div-def | Division as multiplication | `a/b = a*(1/b)` | b ≠ 0 |
| alg.prop.zero-product | Zero-product property | `a*b = 0 <=> a = 0 or b = 0` | |
| alg.prop.div-by-zero | Division by zero is undefined | `a/0 = undefined` | Convention entry; 0/0 also undefined |
| alg.eqprop.reflexive | Reflexive property of equality | `a = a` | |
| alg.eqprop.symmetric | Symmetric property | `a = b => b = a` | |
| alg.eqprop.transitive | Transitive property | `a = b and b = c => a = c` | |
| alg.eqprop.substitution | Substitution property | `a = b => f(a) = f(b)` | f any function defined at a |
| alg.eqprop.add | Addition property of equality | `a = b <=> a + c = b + c` | |
| alg.eqprop.mul | Multiplication property of equality | `a = b <=> a*c = b*c` | c ≠ 0 (⇒ holds for all c) |
| alg.eqprop.cancel-add | Additive cancellation | `a + c = b + c => a = b` | |
| alg.eqprop.cancel-mul | Multiplicative cancellation | `a*c = b*c => a = b` | c ≠ 0 |
| alg.order.trichotomy | Trichotomy | Exactly one of `a < b`, `a = b`, `a > b` | Reals |
| alg.order.transitive | Transitivity of < | `a < b and b < c => a < c` | |
| alg.order.add | Adding preserves order | `a < b <=> a + c < b + c` | |
| alg.order.mul-pos | Multiplying by a positive | `a < b <=> a*c < b*c` | c > 0 |
| alg.order.mul-neg | Multiplying by a negative reverses | `a < b <=> a*c > b*c` | c < 0 |
| alg.order.square-nonneg | Squares are non-negative | `a^2 >= 0` | Reals |
| alg.order.reciprocal | Reciprocals reverse order | `a < b => 1/a > 1/b` | a, b same sign, non-zero |
| alg.order.archimedean | Archimedean property | `exists n in N: n > a` | a ∈ ℝ |

## Fractions, ratios and percents

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.frac.equivalent | Equivalent fractions | `a/b = (a*c)/(b*c)` | b ≠ 0, c ≠ 0 |
| alg.frac.add-common | Add with a common denominator | `a/c + b/c = (a + b)/c` | c ≠ 0 |
| alg.frac.add | Add fractions | `a/b + c/d = (a*d + b*c)/(b*d)` | b, d ≠ 0 |
| alg.frac.mul | Multiply fractions | `(a/b)*(c/d) = (a*c)/(b*d)` | b, d ≠ 0 |
| alg.frac.div | Divide fractions | `(a/b)/(c/d) = (a*d)/(b*c)` | b, c, d ≠ 0 |
| alg.frac.reciprocal | Reciprocal of a fraction | `1/(a/b) = b/a` | a, b ≠ 0 |
| alg.frac.neg | Sign placement | `-(a/b) = (-a)/b = a/(-b)` | b ≠ 0 |
| alg.frac.cross-mult | Cross multiplication | `a/b = c/d <=> a*d = b*c` | b, d ≠ 0 |
| alg.frac.lowest-terms | Lowest terms | `a/b = (a/gcd(a, b))/(b/gcd(a, b))` | a, b ∈ ℤ, b ≠ 0 |
| alg.frac.mixed-number | Mixed number | `q + r/b = (q*b + r)/b` | b ≠ 0 |
| alg.pct.def | Percent | `p% = p/100` | |
| alg.pct.change | Percent change | `change = (new - old)/old * 100%` | old ≠ 0 |
| alg.ratio.proportion | Proportion | `a : b = c : d <=> a*d = b*c` | b, d ≠ 0 |
| alg.var.direct | Direct variation | `y = k*x` | k ≠ 0 constant |
| alg.var.inverse | Inverse variation | `y = k/x` | k ≠ 0, x ≠ 0 |
| alg.var.joint | Joint variation | `z = k*x*y` | k ≠ 0 |

## Exponents

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.exp.product-of-powers | Product of powers | `a^m * a^n = a^(m + n)` | a > 0; or a ≠ 0 and m, n ∈ ℤ; or a = 0 and m, n > 0. ℂ: a ≠ 0 or Re m, Re n > 0 |
| alg.exp.quotient-of-powers | Quotient of powers | `a^m / a^n = a^(m - n)` | a > 0; or a ≠ 0 and m, n ∈ ℤ |
| alg.exp.power-of-power | Power of a power | `(a^m)^n = a^(m*n)` | a ≥ 0 or n ∈ ℤ. Counterexample otherwise: ((−1)²)^(1/2) = 1 ≠ −1 |
| alg.exp.power-of-product | Power of a product | `(a*b)^n = a^n * b^n` | n ∈ ℤ, or a ≥ 0 and b ≥ 0 |
| alg.exp.power-of-quotient | Power of a quotient | `(a/b)^n = a^n / b^n` | b ≠ 0 and (n ∈ ℤ, or a ≥ 0 and b > 0) |
| alg.exp.zero | Zero exponent | `a^0 = 1` | All a (0⁰ by `conv.zero-to-the-zero`) |
| alg.exp.one | Exponent one | `a^1 = a` | |
| alg.exp.negative | Negative exponent | `a^(-n) = 1/a^n` | a ≠ 0 |
| alg.exp.one-base | Base one | `1^a = 1` | |
| alg.exp.zero-base | Base zero | `0^a = 0` | a > 0 |
| alg.exp.rational | Rational exponent | `a^(m/n) = root(a, n)^m = root(a^m, n)` | n ∈ ℤ, n ≥ 1, gcd(m, n) = 1; a ≥ 0 or n odd |
| alg.exp.even-power-neg | Even power of a negative | `(-a)^(2k) = a^(2k)` | k ∈ ℤ |
| alg.exp.odd-power-neg | Odd power of a negative | `(-a)^(2k + 1) = -(a^(2k + 1))` | k ∈ ℤ |
| alg.exp.one-to-one | Equal powers, equal exponents | `a^x = a^y <=> x = y` | a > 0, a ≠ 1 |
| alg.exp.equal-bases | Equal powers, equal bases | `x^n = y^n <=> x = y` | n odd; or n even, n ≠ 0 and x, y ≥ 0 |
| alg.exp.monotone | Monotonicity | `x < y <=> a^x < a^y` | a > 1 (reverses for 0 < a < 1) |
| alg.exp.via-e | Power as exponential | `a^x = e^(x*ln(a))` | a > 0 |
| alg.exp.sci-notation | Scientific notation | `x = m * 10^k` | 1 ≤ abs(m) < 10, k ∈ ℤ, x ≠ 0 |

## Radicals

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.rad.sqrt-def | Principal square root | `sqrt(a) = b <=> b >= 0 and b^2 = a` | a ≥ 0 |
| alg.rad.sqrt-of-square | Square root of a square | `sqrt(a^2) = abs(a)` | a ∈ ℝ |
| alg.rad.square-of-sqrt | Square of a square root | `sqrt(a)^2 = a` | a ≥ 0 (ℂ: all a) |
| alg.rad.product | Product rule | `sqrt(a*b) = sqrt(a)*sqrt(b)` | a ≥ 0 and b ≥ 0. ℂ: a ≥ 0 or b ≥ 0. Counterexample: √((−4)(−9)) = 6 ≠ (2i)(3i) = −6 |
| alg.rad.quotient | Quotient rule | `sqrt(a/b) = sqrt(a)/sqrt(b)` | a ≥ 0, b > 0 |
| alg.rad.nth-def | Real n-th root | `root(a, n) = b <=> b^n = a` | n odd: all a; n even: a ≥ 0, b ≥ 0 |
| alg.rad.nth-product | n-th root of a product | `root(a*b, n) = root(a, n)*root(b, n)` | n odd, or a, b ≥ 0 |
| alg.rad.nth-quotient | n-th root of a quotient | `root(a/b, n) = root(a, n)/root(b, n)` | b ≠ 0; n odd, or a ≥ 0 and b > 0 |
| alg.rad.root-of-power-even | Even root of an even power | `root(a^n, n) = abs(a)` | n even |
| alg.rad.root-of-power-odd | Odd root of an odd power | `root(a^n, n) = a` | n odd |
| alg.rad.power-of-root | Power of a root | `root(a, n)^n = a` | n odd, or a ≥ 0 |
| alg.rad.root-of-root | Root of a root | `root(root(a, m), n) = root(a, m*n)` | a ≥ 0, or m and n odd |
| alg.rad.extract-square | Extract a square factor | `sqrt(k^2 * m) = k*sqrt(m)` | k ≥ 0, m ≥ 0 |
| alg.rad.like-radicals | Combine like radicals | `p*root(a, n) + q*root(a, n) = (p + q)*root(a, n)` | Both defined |
| alg.rad.rationalize-monomial | Rationalize a monomial denominator | `a/sqrt(b) = a*sqrt(b)/b` | b > 0 |
| alg.rad.rationalize-conjugate | Rationalize with a conjugate | `a/(sqrt(b) + sqrt(c)) = a*(sqrt(b) - sqrt(c))/(b - c)` | b, c ≥ 0, b ≠ c |
| alg.rad.rationalize-cube | Rationalize a cube-root binomial | `1/(root(a, 3) - root(b, 3)) = (root(a, 3)^2 + root(a, 3)*root(b, 3) + root(b, 3)^2)/(a - b)` | a ≠ b |
| alg.rad.denest | Denesting square roots | `sqrt(a + sqrt(b)) = sqrt((a + sqrt(a^2 - b))/2) + sqrt((a - sqrt(a^2 - b))/2)` (and the `−` version with `−` between the terms) | a > 0, b ≥ 0, a² ≥ b |

## Absolute value

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.abs.def | Definition | `abs(a) = Piecewise[(a, a >= 0), (-a, true)]` | a ∈ ℝ |
| alg.abs.nonneg | Non-negativity | `abs(a) >= 0` | |
| alg.abs.zero | Zero | `abs(a) = 0 <=> a = 0` | |
| alg.abs.even | Symmetry | `abs(-a) = abs(a)` | |
| alg.abs.product | Product | `abs(a*b) = abs(a)*abs(b)` | ℂ too |
| alg.abs.quotient | Quotient | `abs(a/b) = abs(a)/abs(b)` | b ≠ 0 |
| alg.abs.power | Power | `abs(a^n) = abs(a)^n` | n ∈ ℤ (a ≠ 0 if n < 0) |
| alg.abs.square | Square | `abs(a)^2 = a^2` | a ∈ ℝ (ℂ: `abs(z)^2 = z*conj(z)`) |
| alg.abs.max | As a maximum | `abs(a) = max(a, -a)` | a ∈ ℝ |
| alg.abs.triangle | Triangle inequality | `abs(a + b) <= abs(a) + abs(b)` | ℂ too |
| alg.abs.reverse-triangle | Reverse triangle inequality | `abs(abs(a) - abs(b)) <= abs(a - b)` | |
| alg.abs.eq | Absolute-value equation | `abs(a) = b <=> b >= 0 and (a = b or a = -b)` | |
| alg.abs.lt | Less-than inequality | `abs(a) < b <=> -b < a < b` | |
| alg.abs.gt | Greater-than inequality | `abs(a) > b <=> a < -b or a > b` | |
| alg.abs.eq-abs | Equal absolute values | `abs(a) = abs(b) <=> a = b or a = -b` | |

## Logarithms

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.log.def | Definition | `log(x, b) = y <=> b^y = x` | b > 0, b ≠ 1, x > 0 |
| alg.log.natural | Natural logarithm | `ln(x) = log(x, e)` | x > 0 |
| alg.log.of-one | Log of one | `log(1, b) = 0` | b > 0, b ≠ 1 |
| alg.log.of-base | Log of the base | `log(b, b) = 1` | b > 0, b ≠ 1 |
| alg.log.inverse-exp | Exponential undoes log | `b^log(x, b) = x` | x > 0 |
| alg.log.inverse-log | Log undoes exponential | `log(b^x, b) = x` | x ∈ ℝ (ℂ: −π < Im(x ln b) ≤ π) |
| alg.log.product | Product rule | `log(x*y, b) = log(x, b) + log(y, b)` | x, y > 0 |
| alg.log.quotient | Quotient rule | `log(x/y, b) = log(x, b) - log(y, b)` | x, y > 0 |
| alg.log.power | Power rule | `log(x^r, b) = r*log(x, b)` | x > 0, r ∈ ℝ |
| alg.log.even-power | Power rule for even powers | `ln(x^(2k)) = 2k*ln(abs(x))` | x ≠ 0, k ∈ ℤ |
| alg.log.reciprocal | Log of a reciprocal | `ln(1/x) = -ln(x)` | x > 0 |
| alg.log.root | Log of a root | `ln(root(x, n)) = ln(x)/n` | x > 0 |
| alg.log.change-of-base | Change of base | `log(x, b) = ln(x)/ln(b) = log(x, c)/log(b, c)` | x > 0; b, c > 0, ≠ 1 |
| alg.log.swap | Reciprocal bases | `log(a, b) = 1/log(b, a)` | a, b > 0, ≠ 1 |
| alg.log.power-base | Power of the base | `log(x, b^k) = log(x, b)/k` | k ≠ 0 |
| alg.log.one-to-one | One-to-one | `log(x, b) = log(y, b) <=> x = y` | x, y > 0 |
| alg.log.monotone | Monotonicity | `x < y <=> log(x, b) < log(y, b)` | x, y > 0, b > 1 (reverses for 0 < b < 1) |
| alg.log.ln-e | ln e | `ln(e) = 1` | |
| alg.log.complex-product | Complex logarithm of a product | `ln(z*w) = ln(z) + ln(w) + 2*pi*I*k` for some k ∈ {−1, 0, 1} | ℂ, principal branch |

## Complex numbers (arithmetic)

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.cplx.i-squared | Imaginary unit | `I^2 = -1` | |
| alg.cplx.powers-of-i | Powers of i | `I^n = I^(n mod 4)` | n ∈ ℤ |
| alg.cplx.standard-form | Standard form | `z = a + b*I`, `re(z) = a`, `im(z) = b` | a, b ∈ ℝ |
| alg.cplx.equality | Equality | `a + b*I = c + d*I <=> a = c and b = d` | a, b, c, d ∈ ℝ |
| alg.cplx.mul | Multiplication | `(a + b*I)*(c + d*I) = (a*c - b*d) + (a*d + b*c)*I` | |
| alg.cplx.conj-product | Conjugate product | `z*conj(z) = re(z)^2 + im(z)^2` | |
| alg.cplx.div | Division by the conjugate | `w/z = w*conj(z)/(z*conj(z))` | z ≠ 0 |
| alg.cplx.conj-sum | Conjugate of a sum and product | `conj(z + w) = conj(z) + conj(w)`, `conj(z*w) = conj(z)*conj(w)` | |
| alg.cplx.sqrt-neg | Square root of a negative | `sqrt(-a) = I*sqrt(a)` | a ≥ 0 |
| alg.cplx.modulus | Modulus | `abs(a + b*I) = sqrt(a^2 + b^2)` | a, b ∈ ℝ |

Polar and exponential forms, De Moivre and n-th roots are in D4.

## Polynomials: products and expansion

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| alg.poly.like-terms | Combine like terms | `a*x^n + b*x^n = (a + b)*x^n` | |
| alg.poly.foil | Product of binomials (FOIL) | `(a + b)*(c + d) = a*c + a*d + b*c + b*d` | |
| alg.poly.square-sum | Square of a sum | `(a + b)^2 = a^2 + 2a*b + b^2` | |
| alg.poly.square-diff | Square of a difference | `(a - b)^2 = a^2 - 2a*b + b^2` | |
| alg.poly.sum-times-diff | Sum times difference | `(a + b)*(a - b) = a^2 - b^2` | |
| alg.poly.cube-sum | Cube of a sum | `(a + b)^3 = a^3 + 3a^2*b + 3a*b^2 + b^3` | |
| alg.poly.cube-diff | Cube of a difference | `(a - b)^3 = a^3 - 3a^2*b + 3a*b^2 - b^3` | |
| alg.poly.square-trinomial | Square of a trinomial | `(a + b + c)^2 = a^2 + b^2 + c^2 + 2a*b + 2a*c + 2b*c` | |
| alg.poly.binomial-theorem | Binomial theorem | `(a + b)^n = sum(binomial(n, k)*a^(n - k)*b^k, k, 0, n)` | n ∈ ℕ |
| alg.poly.multinomial-theorem | Multinomial theorem | `(x1 + … + xm)^n = sum over k1 + … + km = n of multinomial(n; k1, …, km)*x1^k1*…*xm^km` | n ∈ ℕ |
| alg.poly.degree-product | Degree of a product | `deg(p*q) = deg(p) + deg(q)` | p, q ≠ 0, over an integral domain |
| alg.poly.degree-sum | Degree of a sum | `deg(p + q) <= max(deg(p), deg(q))` | |

## Factoring

| ID | Kind | Name | Match → yields | Conditions |
| --- | --- | --- | --- | --- |
| alg.factor.gcf | pattern | Greatest common factor | `a*b + a*c → a*(b + c)` | a = gcd of terms (content and common variable powers) |
| alg.factor.grouping | method | Factor by grouping | `a*x + a*y + b*x + b*y → (a + b)*(x + y)` | |
| alg.factor.diff-squares | pattern | Difference of squares | `a^2 - b^2 → (a - b)*(a + b)` | |
| alg.factor.perfect-square-plus | pattern | Perfect-square trinomial | `a^2 + 2a*b + b^2 → (a + b)^2` | |
| alg.factor.perfect-square-minus | pattern | Perfect-square trinomial | `a^2 - 2a*b + b^2 → (a - b)^2` | |
| alg.factor.sum-cubes | pattern | Sum of cubes | `a^3 + b^3 → (a + b)*(a^2 - a*b + b^2)` | |
| alg.factor.diff-cubes | pattern | Difference of cubes | `a^3 - b^3 → (a - b)*(a^2 + a*b + b^2)` | |
| alg.factor.diff-powers | pattern | Difference of n-th powers | `a^n - b^n → (a - b)*sum(a^(n - 1 - k)*b^k, k, 0, n - 1)` | n ∈ ℤ, n ≥ 1 |
| alg.factor.sum-odd-powers | pattern | Sum of odd powers | `a^n + b^n → (a + b)*sum((-1)^k*a^(n - 1 - k)*b^k, k, 0, n - 1)` | n odd |
| alg.factor.simple-trinomial | pattern | Monic trinomial | `x^2 + (p + q)*x + p*q → (x + p)*(x + q)` | |
| alg.factor.ac-method | method | AC method | `a*x^2 + b*x + c`: find m·n = a·c, m + n = b, split and group | a, b, c ∈ ℤ |
| alg.factor.by-roots | theorem | Factor from roots | `a*x^2 + b*x + c = a*(x - r1)*(x - r2)` | r1, r2 roots |
| alg.factor.quadratic-in-form | pattern | Quadratic in form | `a*u^2 + b*u + c` with u = x^k (or another subexpression) | Substitute, factor, substitute back |
| alg.factor.sophie-germain | pattern | Sophie Germain identity | `a^4 + 4b^4 → (a^2 + 2a*b + 2b^2)*(a^2 - 2a*b + 2b^2)` | |
| alg.factor.irreducible-quadratic | definition | Irreducible over ℝ | `a*x^2 + b*x + c` with `b^2 - 4a*c < 0` | Factors only over ℂ |
| alg.factor.content-primitive | definition | Content and primitive part | `p = content(p)*primitive(p)` | Over ℤ |
| alg.factor.square-free | method | Square-free factorization (Yun) | `p = product(q_i^i)` with each q_i square-free and pairwise coprime | Characteristic 0 |
| alg.factor.over-q | method | Factorization over ℚ (Berlekamp–Zassenhaus with Hensel lifting) | `p → product of irreducibles in ℚ[x]` | Milestone 2 |

## Polynomial division and roots

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| alg.poly.division-algorithm | theorem | Division algorithm | `p = d*q + r` with `deg(r) < deg(d)`, q and r unique | d ≠ 0, coefficients in a field |
| alg.poly.long-division | method | Polynomial long division | Produces q and r step by step | |
| alg.poly.synthetic-division | method | Synthetic division | Division by `x - c` with a coefficient table | Divisor monic linear |
| alg.poly.remainder-theorem | theorem | Remainder theorem | Remainder of `p ÷ (x - c)` equals `p(c)` | |
| alg.poly.factor-theorem | theorem | Factor theorem | `divides(x - c, p) <=> p(c) = 0` | |
| alg.poly.rational-root | theorem | Rational Root Theorem | If `p/q` in lowest terms is a root of `a_n*x^n + … + a_0`, then `divides(p, a_0)` and `divides(q, a_n)` | Integer coefficients, a_n ≠ 0 |
| alg.poly.fta | theorem | Fundamental Theorem of Algebra | A degree-n polynomial has exactly n complex roots counted with multiplicity | n ≥ 1, complex coefficients |
| alg.poly.conjugate-roots | theorem | Complex conjugate roots | `p(z) = 0 => p(conj(z)) = 0` | Real coefficients |
| alg.poly.irrational-conjugates | theorem | Irrational conjugate roots | `p(a + sqrt(b)) = 0 => p(a - sqrt(b)) = 0` | Rational coefficients; a, b ∈ ℚ, √b irrational |
| alg.poly.descartes | theorem | Descartes' rule of signs | Positive real roots = sign changes of p(x) minus an even number ≥ 0; negative roots from p(−x) | Real coefficients |
| alg.poly.bound-theorem | theorem | Upper and lower bound theorem | Synthetic division by x − c with c > 0 giving no negative entries ⇒ no root > c (alternating signs with c < 0 ⇒ no root < c) | Real coefficients, positive leading coefficient |
| alg.poly.cauchy-bound | theorem | Cauchy root bound | Every root satisfies `abs(r) <= 1 + max(abs(a_i/a_n))` | a_n ≠ 0 |
| alg.poly.vieta | theorem | Vieta's formulas | `sum of roots = -a_(n-1)/a_n`, `product of roots = (-1)^n*a_0/a_n`; in general the elementary symmetric polynomials of the roots are `(-1)^k*a_(n-k)/a_n` | a_n ≠ 0 |
| alg.poly.ivt | theorem | Sign change implies a root | `p(a)*p(b) < 0 => exists c in (a, b): p(c) = 0` | Real coefficients |
| alg.poly.multiplicity | definition | Multiplicity | r has multiplicity m if `(x - r)^m` divides p and `(x - r)^(m+1)` does not | |
| alg.poly.gcd | method | Euclidean algorithm for polynomials | `gcd(p, q) = gcd(q, p mod q)`, `gcd(p, 0) = monic(p)` | Field coefficients |
| alg.poly.lcm | theorem | LCM via GCD | `lcm(p, q) = p*q/gcd(p, q)` | p, q ≠ 0, up to a unit |

## Quadratics

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| alg.quad.standard-form | definition | Standard form | `a*x^2 + b*x + c` | a ≠ 0 |
| alg.quad.vertex-form | formula | Vertex form | `a*x^2 + b*x + c = a*(x - h)^2 + k` with `h = -b/(2a)`, `k = c - b^2/(4a)` | a ≠ 0 |
| alg.quad.discriminant | definition | Discriminant | `D = b^2 - 4a*c` | |
| alg.quad.root-nature | theorem | Nature of roots | D > 0: two distinct real roots; D = 0: one repeated real root; D < 0: two complex-conjugate roots | Real coefficients |
| alg.quad.quadratic-formula | formula | Quadratic formula | `x = (-b + sqrt(D))/(2a)` or `x = (-b - sqrt(D))/(2a)` | a ≠ 0 |
| alg.quad.completing-the-square | method | Completing the square | See `06-knowledge-catalog.md` example | a ≠ 0 |
| alg.quad.axis | formula | Axis of symmetry | `x = -b/(2a)` | a ≠ 0 |
| alg.quad.extremum | theorem | Vertex is the extremum | Minimum value k when a > 0, maximum when a < 0 | |
| alg.quad.vieta | theorem | Sum and product of roots | `r1 + r2 = -b/a`, `r1*r2 = c/a` | a ≠ 0 |
| alg.quad.from-roots | formula | Quadratic from its roots | `x^2 - (r1 + r2)*x + r1*r2 = 0` | |

## Rational expressions and partial fractions

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| alg.rat.domain | definition | Domain of a rational expression | All x with denominator ≠ 0 | |
| alg.rat.cancel | law | Cancel common factors | `(a*c)/(b*c) = a/b` | b ≠ 0, c ≠ 0 (proviso c ≠ 0 recorded) |
| alg.rat.lcd | method | Least common denominator | Add/subtract by rewriting over `lcm` of denominators | |
| alg.rat.complex-fraction | method | Simplify a complex fraction | Multiply numerator and denominator by the LCD of the inner fractions | Inner denominators ≠ 0 |
| alg.rat.proper | definition | Proper rational function | `deg(P) < deg(Q)`; otherwise divide first | |
| alg.pf.distinct-linear | formula | Distinct linear factors | `P/((x - a)*(x - b)) = A/(x - a) + B/(x - b)` | a ≠ b, deg P < 2 |
| alg.pf.repeated-linear | formula | Repeated linear factor | `P/(x - a)^k = sum(A_j/(x - a)^j, j, 1, k)` | deg P < k |
| alg.pf.quadratic | formula | Irreducible quadratic factor | `P/((x^2 + p*x + q)^k) = sum((B_j*x + C_j)/(x^2 + p*x + q)^j, j, 1, k)` | p² − 4q < 0 |
| alg.pf.cover-up | method | Heaviside cover-up | `A = P(a)/Q'(a)` for a simple root a of Q | |
| alg.pf.uniqueness | theorem | Uniqueness of partial fractions | The decomposition of a proper rational function over ℝ is unique | |

## Equations

The `alg.eq.*` entries are the principles the solver uses to stay honest about solution sets.

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| alg.eq.equiv-add | theorem | Adding preserves solutions | `A = B <=> A + C = B + C` | C defined wherever A and B are |
| alg.eq.equiv-mul | theorem | Multiplying by a non-zero quantity preserves solutions | `A = B <=> A*C = B*C` | C ≠ 0 on the domain |
| alg.eq.equiv-injective | theorem | Applying a one-to-one function preserves solutions | `A = B <=> f(A) = f(B)` | f injective on the range of A and B |
| alg.eq.square-both-sides | theorem | Squaring may add solutions | `A = B => A^2 = B^2` (converse false) | Candidates must be checked |
| alg.eq.mul-by-variable | theorem | Multiplying by an expression that can vanish may add solutions | `A = B => A*C = B*C` | Check candidates where C = 0 |
| alg.eq.divide-by-variable | theorem | Dividing by an expression that can vanish may lose solutions | From `A*C = B*C`, also solve `C = 0` | e.g. x² = x loses x = 0 if divided by x |
| alg.eq.linear | formula | Linear equation | `a*x + b = 0 <=> x = -b/a` | a ≠ 0 |
| alg.eq.literal | method | Solve a literal equation for a variable | Isolate the variable with equivalence steps | |
| alg.eq.rational | method | Rational equations | Multiply by the LCD, solve, discard excluded values | |
| alg.eq.radical | method | Radical equations | Isolate the radical, raise to a power, solve, check candidates | |
| alg.eq.abs | method | Absolute-value equations | Use `alg.abs.eq` to split into cases | |
| alg.eq.exponential | method | Exponential equations | Common base and `alg.exp.one-to-one`, or logarithms of both sides | |
| alg.eq.logarithmic | method | Logarithmic equations | Combine logs, use `alg.log.def` or one-to-one, check domain | |
| alg.eq.quadratic-in-form | method | Equations quadratic in form | Substitute u, solve, back-substitute | |
| alg.eq.zero-product-solve | method | Solve by factoring | Factor to a product = 0, then `alg.prop.zero-product` | |
| alg.eq.extraneous | definition | Extraneous solution | A candidate produced by a non-equivalence step that fails the original equation | |

## Inequalities

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| alg.ineq.square-monotone | theorem | Squaring non-negatives | `a < b <=> a^2 < b^2` | a, b ≥ 0 |
| alg.ineq.sqrt-monotone | theorem | Square roots preserve order | `a < b <=> sqrt(a) < sqrt(b)` | a, b ≥ 0 |
| alg.ineq.compound | definition | Compound inequality | `a < x < b <=> a < x and x < b` | |
| alg.ineq.sign-chart | method | Sign chart | Find zeros and undefined points, test each interval, assemble the union | Polynomial and rational inequalities |
| alg.ineq.am-gm | theorem | AM–GM inequality | `(a + b)/2 >= sqrt(a*b)`, equality iff a = b | a, b ≥ 0 (n-term version for n non-negatives) |
| alg.ineq.cauchy-schwarz | theorem | Cauchy–Schwarz (sums) | `sum(a_k*b_k)^2 <= sum(a_k^2)*sum(b_k^2)` | Reals |
| alg.ineq.bernoulli | theorem | Bernoulli's inequality | `(1 + x)^n >= 1 + n*x` | x ≥ −1, n ∈ ℕ |
| alg.ineq.rearrangement | theorem | Rearrangement inequality | Similarly ordered sequences maximize `sum(a_k*b_k)` | Reals |

## Systems, lines and coordinates

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| alg.sys.substitution | method | Substitution method | Solve one equation for a variable, substitute into the others | |
| alg.sys.elimination | method | Elimination method | Add multiples of equations to eliminate a variable | |
| alg.sys.classification | theorem | Two linear equations in two unknowns | One solution (independent), none (inconsistent, parallel lines) or infinitely many (dependent) | |
| alg.sys.cramer-2x2 | formula | 2×2 Cramer's rule | `x = (c1*b2 - c2*b1)/(a1*b2 - a2*b1)`, `y = (a1*c2 - a2*c1)/(a1*b2 - a2*b1)` | a1·b2 − a2·b1 ≠ 0 |
| alg.line.slope | formula | Slope | `m = (y2 - y1)/(x2 - x1)` | x1 ≠ x2 |
| alg.line.slope-intercept | formula | Slope-intercept form | `y = m*x + b` | |
| alg.line.point-slope | formula | Point-slope form | `y - y1 = m*(x - x1)` | |
| alg.line.standard | formula | Standard form | `A*x + B*y = C` | A, B not both 0 |
| alg.line.parallel | theorem | Parallel lines | Non-vertical lines are parallel iff `m1 = m2` | Distinct lines |
| alg.line.perpendicular | theorem | Perpendicular lines | Non-vertical lines are perpendicular iff `m1*m2 = -1` | |
| alg.coord.distance | formula | Distance formula | `d = sqrt((x2 - x1)^2 + (y2 - y1)^2)` | |
| alg.coord.midpoint | formula | Midpoint formula | `M = ((x1 + x2)/2, (y1 + y2)/2)` | |

## Conventions

| ID | Name | Statement |
| --- | --- | --- |
| conv.order-of-operations | Order of operations | Parentheses, exponents (right to left), multiplication and division (left to right), addition and subtraction (left to right); implicit multiplication has the same precedence as `*` |
| conv.zero-to-the-zero | 0⁰ | `0^0 = 1` in arithmetic; indeterminate as a limit form |
| conv.real-odd-root | Real odd roots | `(-8)^(1/3) = -2` in real mode |
| conv.bare-log-base-10 | Bare log | `log x` is base 10 unless `LogMeansNatural` |
| conv.rounding | Rounding for display | Round half away from zero (school convention). Note: .NET's `Math.Round` defaults to `MidpointRounding.ToEven`, so printers pass `MidpointRounding.AwayFromZero` explicitly |
| conv.decimal-exact | Decimal literals are exact | `0.1` parses as 1/10 |

## Abstract algebra (later milestone)

Group, ring, field, integral domain, ideal, homomorphism definitions; Lagrange's theorem (the order of a subgroup divides the group order); cyclic groups; ℤ/nℤ is a field iff n is prime; first isomorphism theorem. IDs reserved under `alg.struct.*`.

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Exact arithmetic with explained steps | `3/4 + 5/6 = 19/12` via LCD 12 | 1 |
| Simplify exponent and radical expressions with provisos | `sqrt(x^2) = abs(x)`; with `x > 0`, `= x` | 1 |
| Expand and multiply polynomials | `(2x - 3)^3` | 1 |
| Factor: GCF, grouping, special patterns, trinomials, quadratic in form | `x^4 - 16 = (x - 2)(x + 2)(x^2 + 4)` | 1 |
| Factor completely over ℚ, ℝ (with radicals for quadratics) or ℂ | `x^4 + 4 = (x^2 - 2x + 2)(x^2 + 2x + 2)` | 2 |
| Divide polynomials (long, synthetic) | `(x^3 - 6x^2 + 11x - 6) ÷ (x - 1)` | 1 |
| Find all roots with the Rational Root Theorem, Descartes and bounds explained | `2x^3 - 3x^2 - 11x + 6` | 1 |
| Complete the square, vertex form, discriminant analysis | `2x^2 + 8x + 5 = 2(x + 2)^2 - 3` | 1 |
| Rational expressions: simplify, combine, complex fractions, partial fractions | `(3x + 5)/((x + 1)(x + 2))` | 1 |
| Solve equations of every listed type with extraneous-solution checks | `sqrt(x + 7) = x - 5` → {9} (2 rejected) | 1–2 |
| Solve inequalities with sign charts and interval notation | `(x - 1)/(x + 2) >= 0` → `(-∞, -2) ∪ [1, ∞)` | 2 |
| Solve systems by substitution, elimination, Cramer or matrices | | 1 |
| Logarithm and exponent forms | "Write as a single logarithm" | 1 |
| Complex arithmetic in standard form | `(2 + 3I)/(1 - I)` | 1 |

### Example derivation

Solving `x^2 - 5x + 6 = 0` by factoring (explain mode, Standard verbosity):

| Step | Expression | Cited entry |
| --- | --- | --- |
| 1 | Find two numbers with product 6 and sum −5: −2 and −3 | `alg.factor.simple-trinomial` |
| 2 | `(x - 2)(x - 3) = 0` | `alg.factor.simple-trinomial` |
| 3 | `x - 2 = 0 or x - 3 = 0` | `alg.prop.zero-product` |
| 4 | `x = 2 or x = 3` | `alg.eq.equiv-add` |
| 5 | Check: `2^2 - 5·2 + 6 = 0`, `3^2 - 5·3 + 6 = 0` | self-check |
| | **Solution set {2, 3}** | |
