# D7 Linear Algebra

Linear algebra runs on two tracks that share one set of laws: exact and symbolic (row reduction with recorded operations, determinants, eigen-analysis, Jordan form over `BigRational` or `Expr` entries) for explained answers, and floating-point (LU, QR, SVD, iterative solvers) for speed. The same catalog entries justify both, and each numeric result reports its residual.

- **Prefix:** `linalg` · **Course tag:** `LinearAlgebra` · **Completed in:** Milestone 5 (core in Milestone 1)
- **Packages:** `Mathesis.LinearAlgebra` (numeric and exact algorithms), `Mathesis` (`Mathesis.LinearAlgebra.Symbolic` for explained, symbolic work)

## Scope

Vectors in ℝⁿ and ℂⁿ, dot and cross products; linear systems, row reduction and solution structure; matrix algebra and special matrices; the Invertible Matrix Theorem; determinants; vector spaces, subspaces, bases, dimension and the four fundamental subspaces; linear transformations and change of basis; eigenvalues, diagonalization, Cayley–Hamilton, spectral theorem, Schur and Jordan forms; orthogonality, projections, Gram–Schmidt, QR and least squares; inner product spaces; quadratic forms; singular value decomposition and pseudoinverse; matrix factorizations; norms and conditioning; matrix exponential; Kronecker products.

## Types

| Type | Purpose |
| --- | --- |
| `DenseMatrix<T>`, `DenseVector<T>` | Row-major storage; any `T` from the number tower or `Expr` |
| `SparseMatrix<T>` | CSR/CSC storage for large sparse problems |
| `RowReduction<T>` | RREF, pivot columns, rank, and the list of `RowOperation`s (swap, scale, replace) for explanations |
| `LinearSystemSolution<T>` | `Unique(x)`, `None(reason)`, `Infinite(particular, nullspaceBasis)` |
| `Eigen<T>` | Eigenvalues with algebraic and geometric multiplicities, eigenspace bases, diagonalizability |
| `LuDecomposition`, `QrDecomposition`, `CholeskyDecomposition`, `SvdDecomposition`, `SchurDecomposition`, `JordanForm` | Factorization results with reconstruction and residual |
| `Subspace<T>` | Basis, dimension, membership test, projection |
| `LinearMap<T>` | Matrix with domain and codomain bases; kernel, range, composition |

## Vectors

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.vec.dot-def | definition | Dot product | `u·v = sum(u_i*v_i, i, 1, n)` (ℂ: `sum(conj(u_i)*v_i)`) | Same length |
| linalg.vec.dot-comm | law | Commutativity | `u·v = v·u` | Real vectors |
| linalg.vec.dot-dist | law | Distributivity | `u·(v + w) = u·v + u·w` | |
| linalg.vec.dot-scalar | law | Scalars | `(c*u)·v = c*(u·v)` | |
| linalg.vec.dot-self | law | Norm from the dot product | `u·u = norm(u)^2 >= 0`, with equality iff u = 0 | |
| linalg.vec.cauchy-schwarz | theorem | Cauchy–Schwarz inequality | `abs(u·v) <= norm(u)*norm(v)` | Equality iff dependent |
| linalg.vec.triangle | theorem | Triangle inequality | `norm(u + v) <= norm(u) + norm(v)` | |
| linalg.vec.pythagorean | theorem | Pythagorean theorem | `norm(u + v)^2 = norm(u)^2 + norm(v)^2 <=> u·v = 0` | Real vectors |
| linalg.vec.parallelogram | law | Parallelogram law | `norm(u + v)^2 + norm(u - v)^2 = 2norm(u)^2 + 2norm(v)^2` | |
| linalg.vec.cross-def | definition | Cross product | `cross(u, v) = (u2*v3 - u3*v2, u3*v1 - u1*v3, u1*v2 - u2*v1)` | ℝ³ |
| linalg.vec.cross-anticomm | law | Anticommutativity | `cross(u, v) = -cross(v, u)`; `cross(u, u) = 0` | |
| linalg.vec.cross-orthogonal | theorem | Orthogonality | `u·cross(u, v) = 0`, `v·cross(u, v) = 0` | |
| linalg.vec.cross-norm | law | Magnitude | `norm(cross(u, v)) = norm(u)*norm(v)*sin(θ)` = area of the parallelogram | |
| linalg.vec.lagrange-identity | law | Lagrange's identity | `norm(cross(u, v))^2 = norm(u)^2*norm(v)^2 - (u·v)^2` | |
| linalg.vec.triple-product | law | Scalar triple product | `u·cross(v, w) = det([u, v, w])` = signed volume | |
| linalg.vec.bac-cab | law | Vector triple product | `cross(a, cross(b, c)) = b*(a·c) - c*(a·b)` | |
| linalg.vec.jacobi | law | Jacobi identity | `cross(a, cross(b, c)) + cross(b, cross(c, a)) + cross(c, cross(a, b)) = 0` | |
| linalg.vec.plane | formula | Plane through a point | `n·(x - x0) = 0` | n ≠ 0 |
| linalg.vec.point-plane-distance | formula | Distance from a point to a plane | `abs(a*x0 + b*y0 + c*z0 + d)/sqrt(a^2 + b^2 + c^2)` | Plane ax + by + cz + d = 0 |

## Linear systems

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.sys.row-ops | theorem | Elementary row operations preserve solutions | Swapping rows, scaling a row by c ≠ 0, adding a multiple of one row to another | |
| linalg.sys.ref | definition | Row echelon form | Zero rows at the bottom; each leading entry right of the one above; zeros below each leading entry | |
| linalg.sys.rref | definition | Reduced row echelon form | REF with leading entries 1 and zeros above and below each | |
| linalg.sys.rref-unique | theorem | Uniqueness of RREF | Each matrix is row equivalent to exactly one RREF matrix | |
| linalg.sys.rouche-capelli | theorem | Consistency (Rouché–Capelli) | `A*x = b` is consistent ⇔ `rank(A) = rank([A, b])` | |
| linalg.sys.solution-count | theorem | Number of solutions | Consistent and rank = n: unique; consistent and rank < n: infinitely many (n − rank free variables) | n unknowns |
| linalg.sys.homogeneous | theorem | Homogeneous systems | Always consistent; a non-trivial solution exists iff rank < n, in particular when there are more unknowns than equations | |
| linalg.sys.structure | theorem | Structure of solutions | Every solution of `A*x = b` is `x_p + x_h` with x_h ∈ Nul A | Consistent |
| linalg.sys.gaussian | method | Gaussian elimination | Forward elimination to REF, then back substitution | |
| linalg.sys.gauss-jordan | method | Gauss–Jordan elimination | Reduce to RREF and read off the solution | |
| linalg.sys.cramer | theorem | Cramer's rule | `x_i = det(A_i)/det(A)` with A_i = A with column i replaced by b | A square, det A ≠ 0 |

## Matrix algebra

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.mat.product-def | definition | Matrix product | `(A*B)_(i,j) = sum(A_(i,k)*B_(k,j), k, 1, n)` | A is m×n, B is n×p |
| linalg.mat.assoc | law | Associativity | `(A*B)*C = A*(B*C)` | Compatible sizes |
| linalg.mat.dist-left | law | Left distributivity | `A*(B + C) = A*B + A*C` | |
| linalg.mat.dist-right | law | Right distributivity | `(A + B)*C = A*C + B*C` | |
| linalg.mat.scalar | law | Scalars commute with products | `c*(A*B) = (c*A)*B = A*(c*B)` | |
| linalg.mat.identity | law | Identity | `A*I = I*A = A` | A square (or matching sizes) |
| linalg.mat.non-commutative | theorem | Products need not commute | There are A, B with `A*B != B*A` | n ≥ 2 |
| linalg.mat.zero-divisors | theorem | Zero divisors | `A*B = 0` does not imply A = 0 or B = 0 | n ≥ 2 |
| linalg.mat.transpose-involution | law | Double transpose | `(A^T)^T = A` | |
| linalg.mat.transpose-sum | law | Transpose of a sum | `(A + B)^T = A^T + B^T` | |
| linalg.mat.transpose-product | law | Transpose of a product | `(A*B)^T = B^T*A^T` | |
| linalg.mat.inverse-def | definition | Inverse | `A*B = B*A = I` | A square |
| linalg.mat.inverse-unique | theorem | Uniqueness of the inverse | | |
| linalg.mat.inverse-inverse | law | Inverse of the inverse | `(A^-1)^-1 = A` | A invertible |
| linalg.mat.inverse-product | law | Inverse of a product | `(A*B)^-1 = B^-1*A^-1` | A, B invertible |
| linalg.mat.inverse-transpose | law | Inverse of the transpose | `(A^T)^-1 = (A^-1)^T` | A invertible |
| linalg.mat.inverse-scalar | law | Inverse of a scalar multiple | `(c*A)^-1 = A^-1/c` | c ≠ 0 |
| linalg.mat.inverse-2x2 | formula | 2×2 inverse | `[[a, b], [c, d]]^-1 = [[d, -b], [-c, a]]/(a*d - b*c)` | ad − bc ≠ 0 |
| linalg.mat.inverse-gauss-jordan | method | Inverse by Gauss–Jordan | Row reduce `[A, I]` to `[I, A^-1]`; failure to reach I means A is singular | |
| linalg.mat.elementary | theorem | Elementary matrices | Each row operation is left multiplication by an invertible elementary matrix | |
| linalg.mat.trace-linear | law | Trace is linear | `tr(A + B) = tr(A) + tr(B)`, `tr(c*A) = c*tr(A)` | |
| linalg.mat.trace-transpose | law | Trace of the transpose | `tr(A^T) = tr(A)` | |
| linalg.mat.trace-cyclic | law | Cyclic property | `tr(A*B) = tr(B*A)`, `tr(A*B*C) = tr(B*C*A)` | Products defined |
| linalg.mat.block | law | Block multiplication | Partitioned matrices multiply blockwise with compatible partitions | |
| linalg.mat.gram | theorem | Gram matrix | `A^T*A` is symmetric positive semidefinite; positive definite iff A has independent columns | |
| linalg.mat.special | definition | Special matrices | Diagonal, triangular, symmetric (`A^T = A`), skew-symmetric (`A^T = -A`), orthogonal (`Q^T*Q = I`), Hermitian (`A^H = A`), unitary (`U^H*U = I`), normal (`A*A^H = A^H*A`), idempotent (`A^2 = A`), nilpotent (`A^k = 0`), involutory (`A^2 = I`), permutation, positive definite (`x^T*A*x > 0` for x ≠ 0) | |
| linalg.mat.invertible-matrix-theorem | theorem | Invertible Matrix Theorem | For square A (n×n) these are equivalent: A invertible; RREF(A) = I; A has n pivots; `A*x = 0` has only the trivial solution; columns independent; columns span ℝⁿ; `A*x = b` has a unique solution for every b; x ↦ Ax is one-to-one and onto; A^T invertible; rank A = n; Nul A = {0}; det A ≠ 0; 0 is not an eigenvalue; A is a product of elementary matrices | |

## Determinants

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.det.2x2 | formula | 2×2 determinant | `det([[a, b], [c, d]]) = a*d - b*c` | |
| linalg.det.sarrus | method | Rule of Sarrus | 3×3 diagonal products | 3×3 only |
| linalg.det.cofactor | theorem | Cofactor (Laplace) expansion | `det(A) = sum((-1)^(i + j)*A_(i,j)*M_(i,j), j, 1, n)` along any row i (or column) | |
| linalg.det.leibniz | definition | Leibniz formula | `det(A) = sum over permutations σ of sign(σ)*product(A_(i,σ(i)))` | |
| linalg.det.triangular | theorem | Triangular matrices | det = product of the diagonal entries | |
| linalg.det.row-swap | theorem | Swapping rows | Negates the determinant | |
| linalg.det.row-scale | theorem | Scaling a row | Scales the determinant by the same factor | |
| linalg.det.row-replace | theorem | Row replacement | Adding a multiple of one row to another leaves det unchanged | |
| linalg.det.transpose | law | Transpose | `det(A^T) = det(A)` | |
| linalg.det.product | law | Multiplicativity | `det(A*B) = det(A)*det(B)` | Square, same size |
| linalg.det.inverse | law | Inverse | `det(A^-1) = 1/det(A)` | A invertible |
| linalg.det.scalar | law | Scalar multiple | `det(c*A) = c^n*det(A)` | A is n×n |
| linalg.det.invertible | theorem | Invertibility | A invertible ⇔ det A ≠ 0 | |
| linalg.det.repeated-row | theorem | Repeated or zero rows | Two equal rows or a zero row ⇒ det = 0 | |
| linalg.det.adjugate | theorem | Adjugate formula | `A^-1 = adj(A)/det(A)`; `A*adj(A) = det(A)*I` | Second holds for all A |
| linalg.det.volume | theorem | Geometric meaning | abs(det A) is the factor by which x ↦ Ax scales area or volume | |
| linalg.det.vandermonde | formula | Vandermonde determinant | `det(V) = product(x_j - x_i)` over i < j | |
| linalg.det.block-triangular | law | Block triangular | `det([[A, B], [0, D]]) = det(A)*det(D)` | A, D square |
| linalg.det.eigen | theorem | Determinant and eigenvalues | `det(A) = product of eigenvalues` (with multiplicity, over ℂ) | |
| linalg.det.orthogonal | theorem | Orthogonal matrices | `det(Q) = ±1` | |
| linalg.det.matrix-determinant-lemma | law | Matrix determinant lemma | `det(A + u*v^T) = (1 + v^T*A^-1*u)*det(A)` | A invertible |
| linalg.det.sylvester | law | Sylvester's determinant identity | `det(I + A*B) = det(I + B*A)` | A m×n, B n×m |
| linalg.det.bareiss | method | Fraction-free elimination (Bareiss) | Exact integer determinant without fractions | Integer or polynomial entries |

## Vector spaces and subspaces

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.vs.axioms | axiom | Vector space axioms | Closure, associativity and commutativity of addition, zero vector, additive inverses, scalar distributivity over vector and scalar sums, compatibility `a*(b*v) = (a*b)*v`, `1*v = v` | Over a field |
| linalg.vs.subspace-test | theorem | Subspace test | W is a subspace iff 0 ∈ W and W is closed under addition and scalar multiplication | |
| linalg.vs.span-subspace | theorem | Spans are subspaces | span(S) is the smallest subspace containing S | |
| linalg.vs.independence | definition | Linear independence | `sum(c_i*v_i) = 0 => all c_i = 0` | |
| linalg.vs.basis | definition | Basis | An independent spanning set | |
| linalg.vs.dimension | theorem | Dimension is well defined | All bases of a finite-dimensional space have the same size | |
| linalg.vs.too-many | theorem | Too many vectors | More than n vectors in an n-dimensional space are dependent | |
| linalg.vs.n-vectors | theorem | n vectors in n dimensions | Independent ⇔ spanning ⇔ basis | |
| linalg.vs.extension | theorem | Basis extension | Any independent set extends to a basis; any spanning set contains a basis | Finite-dimensional |
| linalg.vs.sum-dimension | theorem | Dimension of a sum | `dim(U + W) = dim(U) + dim(W) - dim(U ∩ W)` | |
| linalg.vs.coordinates | definition | Coordinates | `[x]_B` is the unique c with `x = sum(c_i*b_i)` | B a basis |
| linalg.vs.change-of-basis | theorem | Change of basis | `[x]_C = P_(C<-B)*[x]_B` where the columns of P are `[b_i]_C` | |
| linalg.vs.null-space | definition | Null space | `Nul(A) = {x \| A*x = 0}` | |
| linalg.vs.column-space | definition | Column space | `Col(A) = span(columns of A)` | |
| linalg.vs.rank | theorem | Row rank equals column rank | `dim(Col(A)) = dim(Row(A)) = rank(A)` | |
| linalg.vs.rank-nullity | theorem | Rank–Nullity theorem | `rank(A) + dim(Nul(A)) = n` | A is m×n |
| linalg.vs.fundamental-subspaces | theorem | Fundamental subspaces | `Row(A)^⊥ = Nul(A)` and `Col(A)^⊥ = Nul(A^T)` | |
| linalg.vs.pivot-basis | method | Bases from RREF | Pivot columns of A form a basis of Col A; non-zero rows of RREF form a basis of Row A; free-variable vectors form a basis of Nul A | |

## Linear transformations

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.lt.def | definition | Linear transformation | `T(u + v) = T(u) + T(v)`, `T(c*u) = c*T(u)` | |
| linalg.lt.standard-matrix | theorem | Standard matrix | `T(x) = A*x` with `A = [T(e1), …, T(en)]` | T: ℝⁿ → ℝᵐ linear |
| linalg.lt.one-to-one | theorem | One-to-one | T is one-to-one ⇔ ker T = {0} ⇔ columns of A independent | |
| linalg.lt.onto | theorem | Onto | T is onto ℝᵐ ⇔ columns of A span ℝᵐ ⇔ rank A = m | |
| linalg.lt.composition | theorem | Composition is multiplication | The matrix of S ∘ T is `B*A` | |
| linalg.lt.similarity | definition | Similar matrices | `B = P^-1*A*P` for some invertible P (same map, different basis) | |
| linalg.lt.similarity-invariants | theorem | Similarity invariants | Similar matrices share determinant, trace, rank, characteristic polynomial and eigenvalues | |
| linalg.lt.isomorphism | theorem | Isomorphism theorem | Finite-dimensional spaces over the same field are isomorphic iff they have the same dimension | |
| linalg.lt.rotation | formula | Rotation by θ | `[[cos(θ), -sin(θ)], [sin(θ), cos(θ)]]` | ℝ² |
| linalg.lt.reflection | formula | Reflection across the line at angle θ | `[[cos(2θ), sin(2θ)], [sin(2θ), -cos(2θ)]]` | ℝ² |
| linalg.lt.projection-line | formula | Projection onto span(u) | `u*u^T/(u^T*u)` | u ≠ 0 |
| linalg.lt.shear-scale | formula | Shears and scalings | `[[1, k], [0, 1]]`, `[[a, 0], [0, d]]` | |

## Eigenvalues and eigenvectors

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.eig.def | definition | Eigenvalue and eigenvector | `A*v = λ*v` with v ≠ 0 | A square |
| linalg.eig.characteristic | theorem | Characteristic equation | λ is an eigenvalue ⇔ `det(A - λ*I) = 0` | |
| linalg.eig.eigenspace | definition | Eigenspace | `Nul(A - λ*I)` | |
| linalg.eig.multiplicities | theorem | Multiplicities | 1 ≤ geometric multiplicity ≤ algebraic multiplicity | |
| linalg.eig.trace | theorem | Trace and eigenvalues | `tr(A) = sum of eigenvalues` | Over ℂ, with multiplicity |
| linalg.eig.triangular | theorem | Triangular matrices | Eigenvalues are the diagonal entries | |
| linalg.eig.functions | theorem | Eigenvalues of related matrices | A^k has λ^k; A⁻¹ has 1/λ; A + c·I has λ + c; A^T has the same eigenvalues | Same eigenvectors for the first three |
| linalg.eig.distinct-independent | theorem | Distinct eigenvalues give independent eigenvectors | | |
| linalg.eig.conjugate-pairs | theorem | Complex eigenvalues of real matrices | Occur in conjugate pairs with conjugate eigenvectors | A real |
| linalg.eig.diagonalizable | theorem | Diagonalization theorem | A = P·D·P⁻¹ ⇔ A has n independent eigenvectors ⇔ geometric = algebraic multiplicity for every eigenvalue | |
| linalg.eig.powers | law | Powers via diagonalization | `A^k = P*D^k*P^-1` | A = PDP⁻¹ |
| linalg.eig.cayley-hamilton | theorem | Cayley–Hamilton theorem | `p_A(A) = 0` where p_A is the characteristic polynomial | |
| linalg.eig.spectral | theorem | Spectral theorem | A real symmetric matrix has real eigenvalues and is orthogonally diagonalizable, `A = Q*Λ*Q^T` | |
| linalg.eig.normal | theorem | Normal matrices | A is unitarily diagonalizable ⇔ A is normal | Complex |
| linalg.eig.schur | theorem | Schur decomposition | Every complex square matrix is `Q*T*Q^H` with Q unitary and T upper triangular | |
| linalg.eig.jordan | theorem | Jordan canonical form | Every complex square matrix is similar to a block diagonal matrix of Jordan blocks, unique up to block order | |
| linalg.eig.gershgorin | theorem | Gershgorin circle theorem | Every eigenvalue lies in some disc centered at A_(i,i) with radius `sum(abs(A_(i,j)), j ≠ i)` | |
| linalg.eig.rayleigh | theorem | Rayleigh quotient bounds | `λ_min <= x^T*A*x/(x^T*x) <= λ_max` | A symmetric, x ≠ 0 |
| linalg.eig.perron-frobenius | theorem | Perron–Frobenius theorem | A positive matrix has a simple positive eigenvalue of largest modulus with a positive eigenvector | Entries > 0 |
| linalg.eig.stochastic | theorem | Stochastic matrices | A column- or row-stochastic matrix has eigenvalue 1, and every eigenvalue has modulus ≤ 1 | |

## Orthogonality, projections and least squares

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.orth.independent | theorem | Orthogonal sets are independent | | Non-zero vectors |
| linalg.orth.coordinates | theorem | Coordinates in an orthogonal basis | `c_i = (y·u_i)/(u_i·u_i)` | Orthogonal basis |
| linalg.orth.complement | definition | Orthogonal complement | `W^⊥ = {z \| z·w = 0 for all w in W}` | |
| linalg.orth.decomposition | theorem | Orthogonal decomposition | Each y is uniquely `ŷ + z` with ŷ ∈ W and z ∈ W⊥ | W finite-dimensional |
| linalg.orth.projection | formula | Projection onto a subspace | `ŷ = sum((y·u_i)*u_i)` for an orthonormal basis u_i of W | |
| linalg.orth.projection-matrix | formula | Projection matrix | `P = A*(A^T*A)^-1*A^T`, with `P^2 = P = P^T` | A has independent columns |
| linalg.orth.best-approximation | theorem | Best approximation theorem | ŷ is the closest point of W to y | |
| linalg.orth.gram-schmidt | method | Gram–Schmidt process | `v_k = x_k - sum((x_k·v_j)/(v_j·v_j)*v_j, j, 1, k - 1)`, then normalize | Independent x_k |
| linalg.orth.qr | theorem | QR factorization | `A = Q*R`, Q with orthonormal columns, R upper triangular invertible | A has independent columns |
| linalg.orth.normal-equations | theorem | Normal equations | Least-squares solutions of `A*x = b` satisfy `A^T*A*x̂ = A^T*b` | |
| linalg.orth.least-squares-unique | theorem | Unique least-squares solution | Unique iff the columns of A are independent; then `x̂ = R^-1*Q^T*b` | |
| linalg.orth.orthogonal-matrix | theorem | Orthogonal matrices preserve geometry | `norm(Q*x) = norm(x)`, `(Q*x)·(Q*y) = x·y`, `Q^-1 = Q^T` | Q square orthogonal |
| linalg.orth.inner-product | axiom | Inner product axioms | Symmetry (conjugate symmetry over ℂ), linearity, positivity `⟨v, v⟩ > 0` for v ≠ 0 | |
| linalg.orth.function-inner-product | definition | Function inner product | `⟨f, g⟩ = integrate(f*g, x, a, b)` | Continuous functions on [a, b] |
| linalg.orth.bessel | theorem | Bessel's inequality | `sum(abs(⟨v, e_k⟩)^2) <= norm(v)^2` | Orthonormal e_k |

## Quadratic forms

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.qf.def | definition | Quadratic form | `Q(x) = x^T*A*x` | A symmetric |
| linalg.qf.principal-axes | theorem | Principal axes theorem | With `x = P*y` (P orthogonal, columns eigenvectors), `Q = sum(λ_i*y_i^2)` | |
| linalg.qf.classification | theorem | Classification by eigenvalues | All λ > 0 positive definite; all λ < 0 negative definite; mixed signs indefinite; ≥ 0 positive semidefinite | |
| linalg.qf.sylvester | theorem | Sylvester's criterion | A symmetric matrix is positive definite ⇔ all leading principal minors are positive | |
| linalg.qf.constrained | theorem | Constrained extrema | `max(x^T*A*x)` on norm(x) = 1 is λ_max, attained at its unit eigenvector (min is λ_min) | |

## Singular value decomposition

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.svd.existence | theorem | SVD | Every m×n matrix is `U*Σ*V^T` with U, V orthogonal and Σ diagonal with σ1 ≥ σ2 ≥ … ≥ 0 | Real (unitary for ℂ) |
| linalg.svd.singular-values | theorem | Singular values | `σ_i = sqrt(λ_i(A^T*A))` | |
| linalg.svd.rank | theorem | Rank from singular values | rank A = number of non-zero singular values | |
| linalg.svd.norm | theorem | Spectral norm | `norm(A, 2) = σ_max` | |
| linalg.svd.pseudoinverse | formula | Moore–Penrose pseudoinverse | `A^+ = V*Σ^+*U^T` | |
| linalg.svd.min-norm-ls | theorem | Minimum-norm least squares | `x = A^+*b` is the least-squares solution of smallest norm | |
| linalg.svd.eckart-young | theorem | Eckart–Young theorem | The best rank-k approximation in the 2-norm and Frobenius norm is the truncated SVD; the 2-norm error is σ_(k+1) | |
| linalg.svd.polar | theorem | Polar decomposition | `A = U*P` with U orthogonal and P symmetric positive semidefinite | A square |

## Factorizations, norms and matrix functions

| ID | Kind | Name | Statement | Conditions |
| --- | --- | --- | --- | --- |
| linalg.dec.lu | theorem | LU factorization | `A = L*U` without pivoting exists iff the leading principal minors are non-zero | A invertible |
| linalg.dec.plu | theorem | PLU factorization | `P*A = L*U` exists for every square A | |
| linalg.dec.cholesky | theorem | Cholesky factorization | `A = L*L^T` with positive diagonal ⇔ A symmetric positive definite | |
| linalg.dec.ldl | theorem | LDLᵀ factorization | `A = L*D*L^T` | A symmetric with non-zero leading minors |
| linalg.norm.axioms | axiom | Norm axioms | `norm(x) >= 0` with equality iff x = 0; `norm(c*x) = abs(c)*norm(x)`; triangle inequality | |
| linalg.norm.p-norms | definition | Vector p-norms | `norm(x, 1) = sum(abs(x_i))`, `norm(x, 2) = sqrt(sum(abs(x_i)^2))`, `norm(x, oo) = max(abs(x_i))` | |
| linalg.norm.induced | theorem | Induced matrix norms | `norm(A, 1)` = max column sum; `norm(A, oo)` = max row sum; `norm(A, 2) = σ_max` | |
| linalg.norm.frobenius | formula | Frobenius norm | `norm(A, "F") = sqrt(sum(abs(A_(i,j))^2)) = sqrt(tr(A^H*A))` | |
| linalg.norm.submultiplicative | theorem | Submultiplicativity | `norm(A*B) <= norm(A)*norm(B)` | Induced and Frobenius norms |
| linalg.norm.condition | definition | Condition number | `κ(A) = norm(A)*norm(A^-1)` | A invertible |
| linalg.norm.equivalence | theorem | Norm equivalence | All norms on a finite-dimensional space are equivalent | |
| linalg.fn.exp-def | definition | Matrix exponential | `e^A = sum(A^k/k!, k, 0, oo)` | |
| linalg.fn.exp-commuting | law | Exponential of a sum | `e^(A + B) = e^A*e^B` | A·B = B·A |
| linalg.fn.exp-det | law | Determinant of the exponential | `det(e^A) = e^tr(A)` | |
| linalg.fn.exp-inverse | law | Inverse of the exponential | `(e^A)^-1 = e^(-A)` | |
| linalg.fn.exp-derivative | law | Derivative | `diff(e^(A*t), t) = A*e^(A*t)` | |
| linalg.kron.mixed-product | law | Mixed-product property | `kron(A, B)*kron(C, D) = kron(A*C, B*D)` | Products defined |
| linalg.kron.transpose | law | Transpose | `kron(A, B)^T = kron(A^T, B^T)` | |
| linalg.kron.inverse | law | Inverse | `kron(A, B)^-1 = kron(A^-1, B^-1)` | Both invertible |
| linalg.kron.det | law | Determinant | `det(kron(A, B)) = det(A)^m*det(B)^n` | A n×n, B m×m |
| linalg.kron.vec | law | vec identity | `vec(A*X*B) = kron(B^T, A)*vec(X)` | |

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Row reduce with every operation recorded and explained | See the example below | 1 |
| Solve systems and describe the solution set (unique, none, parametric) | | 1 |
| Determinants by cofactors, row reduction or Bareiss, with steps | | 1 |
| Inverses by Gauss–Jordan or adjugate | | 1 |
| Rank, null space, column space, row space bases | | 1 |
| Eigenvalues and eigenvectors with characteristic polynomial factoring | `[[2, 1], [1, 2]]` → λ = 1, 3 | 1 |
| Diagonalize, Jordan form, matrix powers and exponentials | | 5 |
| Gram–Schmidt, QR, projections, least-squares fits | Best-fit line through data | 5 |
| Change of basis, matrices of linear maps, similarity | | 5 |
| Classify quadratic forms; principal axes | | 5 |
| SVD, pseudoinverse, low-rank approximation (numeric) | | 5 / 7 |
| Symbolic matrices (entries in `Expr`) | `det([[a, b], [c, d]]) = a*d - b*c` | 1 |
| Property checks with witnesses (is A orthogonal, positive definite, diagonalizable?) | | 5 |

### Example derivation

Solve `x + 2y + z = 4`, `2x + 5y + 3z = 9`, `x + 3y + 3z = 6`:

| Step | Augmented matrix | Operation (cited `linalg.sys.row-ops`) |
| --- | --- | --- |
| 0 | `[[1, 2, 1, 4], [2, 5, 3, 9], [1, 3, 3, 6]]` | |
| 1 | `[[1, 2, 1, 4], [0, 1, 1, 1], [1, 3, 3, 6]]` | R2 ← R2 − 2R1 |
| 2 | `[[1, 2, 1, 4], [0, 1, 1, 1], [0, 1, 2, 2]]` | R3 ← R3 − R1 |
| 3 | `[[1, 2, 1, 4], [0, 1, 1, 1], [0, 0, 1, 1]]` | R3 ← R3 − R2 (REF) |
| 4 | `[[1, 2, 0, 3], [0, 1, 0, 0], [0, 0, 1, 1]]` | R2 ← R2 − R3, R1 ← R1 − R3 |
| 5 | `[[1, 0, 0, 3], [0, 1, 0, 0], [0, 0, 1, 1]]` | R1 ← R1 − 2R2 (RREF) |
| | **Unique solution (x, y, z) = (3, 0, 1)**, rank 3 (`linalg.sys.solution-count`); substitution check passes | |
