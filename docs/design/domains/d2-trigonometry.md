# D2 Trigonometry

Trigonometry in Mathesis is a large, fully verified identity catalog over six functions and their inverses, plus exact values, triangle solving, sinusoid analysis and general solutions of trigonometric equations. Hyperbolic functions live here too because their identities mirror the circular ones.

- **Prefix:** `trig` · **Course tag:** `Trigonometry` · **Completed in:** Milestone 2 (identities in Milestone 1)
- **Packages:** `Mathesis.Symbolics` (operators), `Mathesis.Knowledge` (identities), `Mathesis` (`Mathesis.Trigonometry`)

## Scope

Angle measure and conversions; right-triangle and unit-circle definitions; exact values; fundamental, sum and difference, multiple-angle, half-angle, power-reducing, product-to-sum and sum-to-product identities; linear combinations; Weierstrass substitution; exponential (Euler) forms; inverse functions with ranges and compositions; laws of sines, cosines and tangents; triangle area and the ambiguous case; sinusoid features; trigonometric equations; hyperbolic functions. Polar coordinates and complex polar form are in D4; derivatives and integrals in D5.

## Types and operators

| Type or operator | Notes |
| --- | --- |
| Operators `sin cos tan cot sec csc`, `arcsin … arccsc`, `atan2`, `sinh … csch`, `arsinh … arcsch` | Attributes: sin, tan, cot, csc odd; cos, sec even; periods 2π (sin, cos, sec, csc) and π (tan, cot) |
| `Angle` (record struct) | Value with unit (radians, degrees, DMS); exact when the value is an `Expr` |
| `Triangle` / `TriangleSolution` | Sides a, b, c opposite angles A, B, C; a solve returns 0, 1 or 2 triangles with steps |
| `Sinusoid` | y = A·sin(B(x − C)) + D: amplitude, period, phase shift, midline, max/min, key points |
| `ExactTrigTable` | Exact sin/cos/tan at kπ/n for n ∈ {1, 2, 3, 4, 5, 6, 8, 10, 12} |

## Definitions, angles and units

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.def.right-triangle | Right-triangle ratios | `sin(θ) = opp/hyp`, `cos(θ) = adj/hyp`, `tan(θ) = opp/adj` | 0 < θ < π/2 |
| trig.def.unit-circle | Unit-circle definition | The terminal point of angle θ on the unit circle is `(cos(θ), sin(θ))` | θ ∈ ℝ |
| trig.def.tan | Tangent | `tan(x) = sin(x)/cos(x)` | cos x ≠ 0 |
| trig.def.cot | Cotangent | `cot(x) = cos(x)/sin(x)` | sin x ≠ 0 |
| trig.def.sec | Secant | `sec(x) = 1/cos(x)` | cos x ≠ 0 |
| trig.def.csc | Cosecant | `csc(x) = 1/sin(x)` | sin x ≠ 0 |
| trig.def.radian | Radian measure | `θ = s/r` (arc length over radius) | r > 0 |
| trig.unit.deg-to-rad | Degrees to radians | `rad = deg*pi/180` | |
| trig.unit.dms | Degrees–minutes–seconds | `d°m′s″ = d + m/60 + s/3600` degrees | |
| trig.unit.arc-length | Arc length | `s = r*θ` | θ in radians |
| trig.unit.sector-area | Sector area | `A = r^2*θ/2` | θ in radians |
| trig.unit.linear-speed | Linear and angular speed | `v = r*ω`, `ω = θ/t` | |
| trig.def.coterminal | Coterminal angles | `θ` and `θ + 2πk` share a terminal side | k ∈ ℤ |
| trig.def.reference-angle | Reference angle | Acute angle between the terminal side and the x-axis; `f(θ) = ±f(θ_ref)` with the sign from the quadrant | |
| trig.def.quadrant-signs | Quadrant signs (ASTC) | QI all positive; QII sin, csc; QIII tan, cot; QIV cos, sec | |
| conv.arccot-range | Range of arccot | `arccot: ℝ → (0, π)` | Convention; some texts use (−π/2, π/2] |

## Exact values

Each row is a catalog entry `trig.exact.<angle>` giving sin, cos and tan; the other three follow by reciprocals. Values at other quadrants come from reference angles and signs.

| ID | Angle | sin | cos | tan |
| --- | --- | --- | --- | --- |
| trig.exact.zero | 0 | `0` | `1` | `0` |
| trig.exact.pi-12 | π/12 (15°) | `(sqrt(6) - sqrt(2))/4` | `(sqrt(6) + sqrt(2))/4` | `2 - sqrt(3)` |
| trig.exact.pi-10 | π/10 (18°) | `(sqrt(5) - 1)/4` | `sqrt(10 + 2sqrt(5))/4` | `sqrt(25 - 10sqrt(5))/5` |
| trig.exact.pi-8 | π/8 (22.5°) | `sqrt(2 - sqrt(2))/2` | `sqrt(2 + sqrt(2))/2` | `sqrt(2) - 1` |
| trig.exact.pi-6 | π/6 (30°) | `1/2` | `sqrt(3)/2` | `sqrt(3)/3` |
| trig.exact.pi-5 | π/5 (36°) | `sqrt(10 - 2sqrt(5))/4` | `(1 + sqrt(5))/4` | `sqrt(5 - 2sqrt(5))` |
| trig.exact.pi-4 | π/4 (45°) | `sqrt(2)/2` | `sqrt(2)/2` | `1` |
| trig.exact.3pi-10 | 3π/10 (54°) | `(1 + sqrt(5))/4` | `sqrt(10 - 2sqrt(5))/4` | `sqrt(25 + 10sqrt(5))/5` |
| trig.exact.pi-3 | π/3 (60°) | `sqrt(3)/2` | `1/2` | `sqrt(3)` |
| trig.exact.3pi-8 | 3π/8 (67.5°) | `sqrt(2 + sqrt(2))/2` | `sqrt(2 - sqrt(2))/2` | `sqrt(2) + 1` |
| trig.exact.2pi-5 | 2π/5 (72°) | `sqrt(10 + 2sqrt(5))/4` | `(sqrt(5) - 1)/4` | `sqrt(5 + 2sqrt(5))` |
| trig.exact.5pi-12 | 5π/12 (75°) | `(sqrt(6) + sqrt(2))/4` | `(sqrt(6) - sqrt(2))/4` | `2 + sqrt(3)` |
| trig.exact.pi-2 | π/2 (90°) | `1` | `0` | undefined |

## Fundamental identities

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.id.pythagorean | Pythagorean identity | `sin(x)^2 + cos(x)^2 = 1` | ℂ too |
| trig.id.pythagorean-tan | Pythagorean identity (tan) | `1 + tan(x)^2 = sec(x)^2` | cos x ≠ 0 |
| trig.id.pythagorean-cot | Pythagorean identity (cot) | `1 + cot(x)^2 = csc(x)^2` | sin x ≠ 0 |
| trig.id.sin-odd | Sine is odd | `sin(-x) = -sin(x)` | |
| trig.id.cos-even | Cosine is even | `cos(-x) = cos(x)` | |
| trig.id.tan-odd | Tangent is odd | `tan(-x) = -tan(x)` | |
| trig.id.cot-odd | Cotangent is odd | `cot(-x) = -cot(x)` | |
| trig.id.sec-even | Secant is even | `sec(-x) = sec(x)` | |
| trig.id.csc-odd | Cosecant is odd | `csc(-x) = -csc(x)` | |
| trig.id.period-sin | Period of sine | `sin(x + 2*pi*k) = sin(x)` | k ∈ ℤ (same for cos, sec, csc) |
| trig.id.period-tan | Period of tangent | `tan(x + pi*k) = tan(x)` | k ∈ ℤ (same for cot) |
| trig.id.cofunction-sin | Cofunction (sine) | `sin(pi/2 - x) = cos(x)` | |
| trig.id.cofunction-cos | Cofunction (cosine) | `cos(pi/2 - x) = sin(x)` | |
| trig.id.cofunction-tan | Cofunction (tangent) | `tan(pi/2 - x) = cot(x)` | sin x ≠ 0 |
| trig.id.cofunction-sec | Cofunction (secant) | `sec(pi/2 - x) = csc(x)` | sin x ≠ 0 |
| trig.id.supplement-sin | Supplementary angle (sine) | `sin(pi - x) = sin(x)` | |
| trig.id.supplement-cos | Supplementary angle (cosine) | `cos(pi - x) = -cos(x)` | |
| trig.id.shift-pi-sin | Half-turn (sine) | `sin(x + pi) = -sin(x)` | |
| trig.id.shift-pi-cos | Half-turn (cosine) | `cos(x + pi) = -cos(x)` | |
| trig.id.shift-half-pi-sin | Quarter-turn (sine) | `sin(x + pi/2) = cos(x)` | |
| trig.id.shift-half-pi-cos | Quarter-turn (cosine) | `cos(x + pi/2) = -sin(x)` | |

## Sum and difference

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.sum.sin-of-sum | Sine of a sum | `sin(u + v) = sin(u)*cos(v) + cos(u)*sin(v)` | DLMF 4.21.2 |
| trig.sum.sin-of-diff | Sine of a difference | `sin(u - v) = sin(u)*cos(v) - cos(u)*sin(v)` | |
| trig.sum.cos-of-sum | Cosine of a sum | `cos(u + v) = cos(u)*cos(v) - sin(u)*sin(v)` | DLMF 4.21.3 |
| trig.sum.cos-of-diff | Cosine of a difference | `cos(u - v) = cos(u)*cos(v) + sin(u)*sin(v)` | |
| trig.sum.tan-of-sum | Tangent of a sum | `tan(u + v) = (tan(u) + tan(v))/(1 - tan(u)*tan(v))` | tan u, tan v, tan(u + v) defined |
| trig.sum.tan-of-diff | Tangent of a difference | `tan(u - v) = (tan(u) - tan(v))/(1 + tan(u)*tan(v))` | tan u, tan v, tan(u − v) defined |

## Multiple angles

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.mult.sin-double | Double angle (sine) | `sin(2x) = 2sin(x)*cos(x)` | |
| trig.mult.cos-double | Double angle (cosine) | `cos(2x) = cos(x)^2 - sin(x)^2` | |
| trig.mult.cos-double-cos | Double angle (cosine, cos form) | `cos(2x) = 2cos(x)^2 - 1` | |
| trig.mult.cos-double-sin | Double angle (cosine, sin form) | `cos(2x) = 1 - 2sin(x)^2` | |
| trig.mult.tan-double | Double angle (tangent) | `tan(2x) = 2tan(x)/(1 - tan(x)^2)` | tan x, tan 2x defined |
| trig.mult.sin-triple | Triple angle (sine) | `sin(3x) = 3sin(x) - 4sin(x)^3` | |
| trig.mult.cos-triple | Triple angle (cosine) | `cos(3x) = 4cos(x)^3 - 3cos(x)` | |
| trig.mult.tan-triple | Triple angle (tangent) | `tan(3x) = (3tan(x) - tan(x)^3)/(1 - 3tan(x)^2)` | Both sides defined |
| trig.mult.chebyshev | Cosine of a multiple | `cos(n*x) = chebyshevT(n, cos(x))` | n ∈ ℕ |
| trig.mult.de-moivre | Multiple angles from De Moivre | `cos(n*x) + I*sin(n*x) = (cos(x) + I*sin(x))^n` | n ∈ ℤ |

## Half angles and power reduction

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.half.sin | Half angle (sine) | `sin(x/2) = ± sqrt((1 - cos(x))/2)` | Sign from the quadrant of x/2 |
| trig.half.cos | Half angle (cosine) | `cos(x/2) = ± sqrt((1 + cos(x))/2)` | Sign from the quadrant of x/2 |
| trig.half.tan-sin | Half angle (tangent) | `tan(x/2) = sin(x)/(1 + cos(x))` | cos x ≠ −1 |
| trig.half.tan-cos | Half angle (tangent, alternate) | `tan(x/2) = (1 - cos(x))/sin(x)` | sin x ≠ 0 |
| trig.power.sin-sq | Power reduction (sin²) | `sin(x)^2 = (1 - cos(2x))/2` | |
| trig.power.cos-sq | Power reduction (cos²) | `cos(x)^2 = (1 + cos(2x))/2` | |
| trig.power.tan-sq | Power reduction (tan²) | `tan(x)^2 = (1 - cos(2x))/(1 + cos(2x))` | cos x ≠ 0 |
| trig.power.sin-cube | Power reduction (sin³) | `sin(x)^3 = (3sin(x) - sin(3x))/4` | |
| trig.power.cos-cube | Power reduction (cos³) | `cos(x)^3 = (3cos(x) + cos(3x))/4` | |
| trig.power.sin-fourth | Power reduction (sin⁴) | `sin(x)^4 = (3 - 4cos(2x) + cos(4x))/8` | |
| trig.power.cos-fourth | Power reduction (cos⁴) | `cos(x)^4 = (3 + 4cos(2x) + cos(4x))/8` | |
| trig.power.sin-sq-cos-sq | Product of squares | `sin(x)^2*cos(x)^2 = (1 - cos(4x))/8` | |

## Product-to-sum and sum-to-product

| ID | Name | Statement |
| --- | --- | --- |
| trig.prod.sin-sin | Product of sines | `sin(u)*sin(v) = (cos(u - v) - cos(u + v))/2` |
| trig.prod.cos-cos | Product of cosines | `cos(u)*cos(v) = (cos(u - v) + cos(u + v))/2` |
| trig.prod.sin-cos | Sine times cosine | `sin(u)*cos(v) = (sin(u + v) + sin(u - v))/2` |
| trig.prod.cos-sin | Cosine times sine | `cos(u)*sin(v) = (sin(u + v) - sin(u - v))/2` |
| trig.s2p.sin-plus-sin | Sum of sines | `sin(u) + sin(v) = 2sin((u + v)/2)*cos((u - v)/2)` |
| trig.s2p.sin-minus-sin | Difference of sines | `sin(u) - sin(v) = 2cos((u + v)/2)*sin((u - v)/2)` |
| trig.s2p.cos-plus-cos | Sum of cosines | `cos(u) + cos(v) = 2cos((u + v)/2)*cos((u - v)/2)` |
| trig.s2p.cos-minus-cos | Difference of cosines | `cos(u) - cos(v) = -2sin((u + v)/2)*sin((u - v)/2)` |

## Linear combinations, substitutions and exponential forms

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.lin.sin-form | Harmonic addition (sine form) | `a*sin(x) + b*cos(x) = R*sin(x + φ)` with `R = sqrt(a^2 + b^2)`, `φ = atan2(b, a)` | a, b ∈ ℝ, not both 0 |
| trig.lin.cos-form | Harmonic addition (cosine form) | `a*cos(x) + b*sin(x) = R*cos(x - φ)` with `R = sqrt(a^2 + b^2)`, `φ = atan2(b, a)` | a, b ∈ ℝ, not both 0 |
| trig.weier.sin | Weierstrass substitution (sine) | `sin(x) = 2t/(1 + t^2)` with `t = tan(x/2)` | cos(x/2) ≠ 0 |
| trig.weier.cos | Weierstrass substitution (cosine) | `cos(x) = (1 - t^2)/(1 + t^2)` | cos(x/2) ≠ 0 |
| trig.weier.tan | Weierstrass substitution (tangent) | `tan(x) = 2t/(1 - t^2)` | t² ≠ 1 |
| trig.exp.euler | Euler's formula | `e^(I*x) = cos(x) + I*sin(x)` | ℂ |
| trig.exp.euler-identity | Euler's identity | `e^(I*pi) + 1 = 0` | |
| trig.exp.sin | Sine as exponentials | `sin(x) = (e^(I*x) - e^(-I*x))/(2I)` | ℂ |
| trig.exp.cos | Cosine as exponentials | `cos(x) = (e^(I*x) + e^(-I*x))/2` | ℂ |
| trig.exp.tan | Tangent as exponentials | `tan(x) = -I*(e^(I*x) - e^(-I*x))/(e^(I*x) + e^(-I*x))` | cos x ≠ 0 |

## Inverse functions

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.inv.arcsin-def | Arcsine | `arcsin(x) = y <=> sin(y) = x and -pi/2 <= y <= pi/2` | −1 ≤ x ≤ 1 |
| trig.inv.arccos-def | Arccosine | `arccos(x) = y <=> cos(y) = x and 0 <= y <= pi` | −1 ≤ x ≤ 1 |
| trig.inv.arctan-def | Arctangent | `arctan(x) = y <=> tan(y) = x and -pi/2 < y < pi/2` | x ∈ ℝ |
| trig.inv.arccot-def | Arccotangent | `arccot(x) = y <=> cot(y) = x and 0 < y < pi` | x ∈ ℝ (`conv.arccot-range`) |
| trig.inv.arcsec-def | Arcsecant | `arcsec(x) = arccos(1/x)` | abs(x) ≥ 1 |
| trig.inv.arccsc-def | Arccosecant | `arccsc(x) = arcsin(1/x)` | abs(x) ≥ 1 |
| trig.inv.atan2-def | Two-argument arctangent | `atan2(y, x)` is the angle in (−π, π] of the point (x, y) | (x, y) ≠ (0, 0) |
| trig.inv.sin-arcsin | Sine of arcsine | `sin(arcsin(x)) = x` | −1 ≤ x ≤ 1 |
| trig.inv.arcsin-sin | Arcsine of sine | `arcsin(sin(x)) = x` | −π/2 ≤ x ≤ π/2 |
| trig.inv.cos-arccos | Cosine of arccosine | `cos(arccos(x)) = x` | −1 ≤ x ≤ 1 |
| trig.inv.arccos-cos | Arccosine of cosine | `arccos(cos(x)) = x` | 0 ≤ x ≤ π |
| trig.inv.tan-arctan | Tangent of arctangent | `tan(arctan(x)) = x` | x ∈ ℝ |
| trig.inv.arctan-tan | Arctangent of tangent | `arctan(tan(x)) = x` | −π/2 < x < π/2 |
| trig.inv.arcsin-plus-arccos | Complementary inverses | `arcsin(x) + arccos(x) = pi/2` | −1 ≤ x ≤ 1 |
| trig.inv.arctan-plus-arccot | Complementary inverses | `arctan(x) + arccot(x) = pi/2` | x ∈ ℝ |
| trig.inv.arctan-reciprocal | Arctangent of a reciprocal | `arctan(x) + arctan(1/x) = pi/2*sign(x)` | x ≠ 0 |
| trig.inv.arcsin-odd | Arcsine is odd | `arcsin(-x) = -arcsin(x)` | −1 ≤ x ≤ 1 |
| trig.inv.arccos-neg | Arccosine of a negative | `arccos(-x) = pi - arccos(x)` | −1 ≤ x ≤ 1 |
| trig.inv.arctan-odd | Arctangent is odd | `arctan(-x) = -arctan(x)` | |
| trig.inv.sin-arccos | Sine of arccosine | `sin(arccos(x)) = sqrt(1 - x^2)` | −1 ≤ x ≤ 1 |
| trig.inv.cos-arcsin | Cosine of arcsine | `cos(arcsin(x)) = sqrt(1 - x^2)` | −1 ≤ x ≤ 1 |
| trig.inv.tan-arcsin | Tangent of arcsine | `tan(arcsin(x)) = x/sqrt(1 - x^2)` | −1 < x < 1 |
| trig.inv.tan-arccos | Tangent of arccosine | `tan(arccos(x)) = sqrt(1 - x^2)/x` | −1 ≤ x ≤ 1, x ≠ 0 |
| trig.inv.sin-arctan | Sine of arctangent | `sin(arctan(x)) = x/sqrt(1 + x^2)` | |
| trig.inv.cos-arctan | Cosine of arctangent | `cos(arctan(x)) = 1/sqrt(1 + x^2)` | |
| trig.inv.arctan-sum | Arctangent addition | `arctan(x) + arctan(y) = arctan((x + y)/(1 - x*y))` | x·y < 1 (add π if x·y > 1 and x > 0; subtract π if x·y > 1 and x < 0) |

## Triangles

Sides a, b, c are opposite angles A, B, C; R is the circumradius, r the inradius, s = (a + b + c)/2.

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| trig.tri.angle-sum | theorem | Angle sum | `A + B + C = pi` | Euclidean triangle |
| trig.tri.pythagorean | theorem | Pythagorean theorem | `a^2 + b^2 = c^2` | C = π/2 |
| trig.tri.law-of-sines | theorem | Law of Sines | `a/sin(A) = b/sin(B) = c/sin(C) = 2R` | |
| trig.tri.law-of-cosines | theorem | Law of Cosines | `c^2 = a^2 + b^2 - 2a*b*cos(C)` | |
| trig.tri.law-of-tangents | theorem | Law of Tangents | `(a - b)/(a + b) = tan((A - B)/2)/tan((A + B)/2)` | |
| trig.tri.mollweide-plus | theorem | Mollweide's formula | `(a + b)/c = cos((A - B)/2)/sin(C/2)` | |
| trig.tri.mollweide-minus | theorem | Mollweide's formula | `(a - b)/c = sin((A - B)/2)/cos(C/2)` | |
| trig.tri.projection | theorem | Projection formula | `a = b*cos(C) + c*cos(B)` | |
| trig.tri.area-sas | formula | Area from two sides and the included angle | `Area = a*b*sin(C)/2` | |
| trig.tri.heron | formula | Heron's formula | `Area = sqrt(s*(s - a)*(s - b)*(s - c))` | |
| trig.tri.inradius | formula | Inradius | `r = Area/s` | |
| trig.tri.circumradius | formula | Circumradius | `R = a*b*c/(4*Area)` | |
| trig.tri.inequality | theorem | Triangle inequality | `a < b + c`, `b < a + c`, `c < a + b` | Non-degenerate |
| trig.tri.ssa | theorem | Ambiguous case (SSA) | Given a, b, A with A acute and h = b·sin A: a < h none; a = h one (right); h < a < b two; a ≥ b one. A not acute: a ≤ b none; a > b one | |
| trig.tri.solve-sss | method | Solve SSS | Law of Cosines for two angles, angle sum for the third | Triangle inequality holds |
| trig.tri.solve-sas | method | Solve SAS | Law of Cosines for the third side, then Law of Sines or Cosines | |
| trig.tri.solve-asa-aas | method | Solve ASA/AAS | Angle sum, then Law of Sines | |
| trig.tri.solve-ssa | method | Solve SSA | Law of Sines with the ambiguous-case analysis | |

## Graphs of trigonometric functions

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.graph.sinusoid | Sinusoid features | For `y = A*sin(B*(x - C)) + D`: amplitude abs(A), period 2π/abs(B), phase shift C, midline y = D, maximum D + abs(A), minimum D − abs(A) | A, B ≠ 0 (same for cos) |
| trig.graph.frequency | Frequency | `f = abs(B)/(2*pi)` | |
| trig.graph.tangent | Tangent graph | `y = A*tan(B*(x - C)) + D` has period π/abs(B) and asymptotes at `x = C + pi/(2B) + pi*k/B` | k ∈ ℤ |
| trig.graph.key-points | Five key points | A sinusoid period splits into four equal quarters: start, max/min, midline, min/max, end | |

## Trigonometric equations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| trig.eqn.sin | theorem | Basic sine equation | `sin(x) = a <=> x = arcsin(a) + 2*pi*k or x = pi - arcsin(a) + 2*pi*k` | abs(a) ≤ 1, k ∈ ℤ (no solution otherwise) |
| trig.eqn.cos | theorem | Basic cosine equation | `cos(x) = a <=> x = arccos(a) + 2*pi*k or x = -arccos(a) + 2*pi*k` | abs(a) ≤ 1, k ∈ ℤ |
| trig.eqn.tan | theorem | Basic tangent equation | `tan(x) = a <=> x = arctan(a) + pi*k` | k ∈ ℤ |
| trig.eqn.sin-sin | theorem | Equal sines | `sin(x) = sin(y) <=> x = y + 2*pi*k or x = pi - y + 2*pi*k` | k ∈ ℤ |
| trig.eqn.cos-cos | theorem | Equal cosines | `cos(x) = cos(y) <=> x = y + 2*pi*k or x = -y + 2*pi*k` | k ∈ ℤ |
| trig.eqn.tan-tan | theorem | Equal tangents | `tan(x) = tan(y) <=> x = y + pi*k` | Both defined, k ∈ ℤ |
| trig.eqn.linear-combination | method | `a sin x + b cos x = c` | Rewrite with `trig.lin.sin-form`; solvable iff abs(c) ≤ √(a² + b²) | |
| trig.eqn.quadratic | method | Quadratic in a trig function | Substitute u = sin x (or cos, tan), solve, keep u in range | |
| trig.eqn.factor | method | Factor and use the zero-product property | e.g. `sin(2x) = sin(x)` → `sin(x)*(2cos(x) - 1) = 0` | |
| trig.eqn.interval | method | Solutions in an interval | Enumerate k for which the general solution lies in the interval | |

## Hyperbolic functions

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| trig.hyp.sinh-def | Hyperbolic sine | `sinh(x) = (e^x - e^(-x))/2` | |
| trig.hyp.cosh-def | Hyperbolic cosine | `cosh(x) = (e^x + e^(-x))/2` | |
| trig.hyp.tanh-def | Hyperbolic tangent | `tanh(x) = sinh(x)/cosh(x)` | |
| trig.hyp.pythagorean | Fundamental identity | `cosh(x)^2 - sinh(x)^2 = 1` | |
| trig.hyp.sech | Identity with sech | `1 - tanh(x)^2 = sech(x)^2` | |
| trig.hyp.csch | Identity with csch | `coth(x)^2 - 1 = csch(x)^2` | x ≠ 0 |
| trig.hyp.sinh-odd | Parity | `sinh(-x) = -sinh(x)`, `cosh(-x) = cosh(x)` | |
| trig.hyp.exp | Sum is the exponential | `cosh(x) + sinh(x) = e^x` | |
| trig.hyp.sinh-sum | Addition (sinh) | `sinh(x + y) = sinh(x)*cosh(y) + cosh(x)*sinh(y)` | |
| trig.hyp.cosh-sum | Addition (cosh) | `cosh(x + y) = cosh(x)*cosh(y) + sinh(x)*sinh(y)` | |
| trig.hyp.sinh-double | Double argument | `sinh(2x) = 2sinh(x)*cosh(x)` | |
| trig.hyp.cosh-double | Double argument | `cosh(2x) = cosh(x)^2 + sinh(x)^2 = 2cosh(x)^2 - 1` | |
| trig.hyp.arsinh | Inverse hyperbolic sine | `arsinh(x) = ln(x + sqrt(x^2 + 1))` | |
| trig.hyp.arcosh | Inverse hyperbolic cosine | `arcosh(x) = ln(x + sqrt(x^2 - 1))` | x ≥ 1 |
| trig.hyp.artanh | Inverse hyperbolic tangent | `artanh(x) = ln((1 + x)/(1 - x))/2` | −1 < x < 1 |
| trig.hyp.circular | Link to circular functions | `sinh(I*x) = I*sin(x)`, `cosh(I*x) = cos(x)` | ℂ |

## Patterns and methods

| ID | Kind | Name | Description |
| --- | --- | --- | --- |
| trig.pattern.pythagorean-scaled | pattern | Scaled Pythagorean sum | `a*sin(u)^2 + a*cos(u)^2 → a` with the same u |
| trig.pattern.one-minus-sin-sq | pattern | 1 − sin² | `1 - sin(u)^2 → cos(u)^2` (and the cos and tan/sec variants) |
| trig.pattern.conjugate | pattern | Conjugate product | `(1 - sin(u))*(1 + sin(u)) → cos(u)^2` |
| trig.pattern.sum-sin-cos | pattern | a sin + b cos | Same argument, constant coefficients → harmonic form |
| trig.method.prove-identity | method | Prove an identity | Transform the more complex side; or reduce both sides to a common form; convert to sin and cos; combine fractions; use conjugates; never move terms across the equals sign |
| trig.method.simplify | method | Simplify a trigonometric expression | Rewrite in sin and cos, apply Pythagorean identities, cancel, then return to the shortest form by the complexity measure |
| trig.method.reduce-powers | method | Reduce powers for integration | Apply `trig.power.*` until linear in cos(kx) and sin(kx) |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Convert degrees, radians and DMS exactly | `135° = 3π/4` | 1 |
| Evaluate exactly at rational multiples of π | `cos(7π/12) = (√2 − √6)/4` | 2 |
| Simplify, expand and reduce with steps | `sin(x)^4 - cos(x)^4 = -cos(2x)` | 1 |
| Prove identities as two-column proofs | `(1 - cos(2x))/sin(2x) = tan(x)` | 1 (kernel-checked: 3) |
| Solve trigonometric equations: general solution and solutions in an interval | `2sin(x)^2 - sin(x) - 1 = 0` | 2 |
| Solve triangles (SSS, SAS, ASA, AAS, SSA with the ambiguous case) | a = 7, b = 10, A = 30° → two triangles | 2 |
| Analyze sinusoids | `y = 3sin(2x - π/2) + 1` → amplitude 3, period π, phase shift π/4 | 2 |
| Inverse-function compositions with range checks | `arcsin(sin(5π/6)) = π/6` | 2 |

### Example derivation

Proving `(1 - cos(2x))/sin(2x) = tan(x)`:

| Step | Left side | Cited entry |
| --- | --- | --- |
| 1 | `(1 - (1 - 2sin(x)^2))/sin(2x)` | `trig.mult.cos-double-sin` |
| 2 | `2sin(x)^2/sin(2x)` | `alg.prop.sub-def`, arithmetic |
| 3 | `2sin(x)^2/(2sin(x)*cos(x))` | `trig.mult.sin-double` |
| 4 | `sin(x)/cos(x)` (proviso sin x ≠ 0) | `alg.rat.cancel` |
| 5 | `tan(x)` = right side ∎ | `trig.def.tan` |
