# D3 Proofs and Logic

This domain makes Mathesis's answers checkable: propositions, quantifiers, sets, relations and functions are ordinary syntax trees, and a small trusted kernel accepts a theorem only when every step is an instance of an inference rule. Derivations from every other engine convert into equational proofs the kernel can replay.

- **Prefix:** `logic` · **Course tag:** `Proofs` · **Completed in:** Milestone 3
- **Package:** `Mathesis.Logic` (namespaces `Mathesis.Logic`, `.Sat`, `.Sets`, `.Proofs`, `.Tactics`, `.Rendering`)

## Scope

Propositional logic (connectives, truth tables, equivalences, normal forms, inference rules, fallacies); predicate logic (quantifiers, negation, prenex form, instantiation and generalization); naive set theory; relations (equivalence relations, orders, closures); functions as relations (injective, surjective, bijective, images and preimages); cardinality; proof techniques from direct proof to structural induction; the proof kernel, tactics and decision procedures; proof rendering; counterexample search.

## Types

| Type | Purpose |
| --- | --- |
| `Expr` of sort `Boolean` | Propositions, built with `and`, `or`, `not`, `implies`, `iff`, `xor`, quantifier binders |
| `TruthTable` | Rows of assignments and values, with tautology/contradiction/contingency classification |
| `Cnf`, `Clause`, `Literal` | Integer-literal clauses for the SAT solver; Tseitin encoding from `Expr` |
| `SatResult` | `Satisfiable(model)` or `Unsatisfiable` (resolution proof or DRAT certificate in a later milestone) |
| `Judgment` | Γ ⊢ φ: a context of hypotheses and a proposition |
| `Theorem` | A judgment accepted by the kernel; its constructor is `internal` to `Kernel`, so the only way to obtain one is through kernel rules (LCF design) |
| `Proof` | A tree of rule applications, serializable and renderable |
| `Goal`, `ProofState`, `Tactic` | Interactive or scripted proof construction: a tactic maps a goal to subgoals and a function that assembles their theorems |
| `Relation<T>`, `FiniteFunction<TIn, TOut>` | Finite relations and functions with property checks and closures |
| `Counterexample` | An assignment that falsifies a claim, with the evaluated sides |

## The kernel

The kernel is the only trusted component besides the exact number types. It implements:

- **Structural rules:** `Assume` (φ ⊢ φ), weakening.
- **Natural deduction** for classical first-order logic with equality (rules in the table below).
- **Equality:** reflexivity and substitution (Leibniz); symmetry, transitivity and congruence are derived.
- **Arithmetic oracle:** `RingEq` accepts `a = b` when the commutative-ring normal forms of a and b are identical; `FieldEq` extends it to rational functions with the non-zero denominators added as hypotheses. The polynomial normalizer is therefore part of the trusted base and has its own exhaustive tests.
- **Induction over ℕ:** from P(0) and ∀n (P(n) → P(n + 1)), conclude ∀n P(n). Strong induction and the well-ordering principle are derived theorems.
- **Catalog axioms:** a catalog `axiom` can be instantiated directly. A catalog `law` can be instantiated only if it carries a kernel proof (`verify: proof`); otherwise the resulting theorem is marked *relative to the catalog*, and the renderer says so.

## Propositional logic

### Equivalences

| ID | Name | Statement |
| --- | --- | --- |
| logic.prop.identity | Identity laws | `p and true <=> p`, `p or false <=> p` |
| logic.prop.domination | Domination laws | `p or true <=> true`, `p and false <=> false` |
| logic.prop.idempotent | Idempotent laws | `p and p <=> p`, `p or p <=> p` |
| logic.prop.double-negation | Double negation | `not not p <=> p` |
| logic.prop.commutative | Commutative laws | `p and q <=> q and p`, `p or q <=> q or p` |
| logic.prop.associative | Associative laws | `(p and q) and r <=> p and (q and r)`, same for `or` |
| logic.prop.distributive-and | Distribute and over or | `p and (q or r) <=> (p and q) or (p and r)` |
| logic.prop.distributive-or | Distribute or over and | `p or (q and r) <=> (p or q) and (p or r)` |
| logic.prop.de-morgan-and | De Morgan (and) | `not (p and q) <=> not p or not q` |
| logic.prop.de-morgan-or | De Morgan (or) | `not (p or q) <=> not p and not q` |
| logic.prop.absorption | Absorption laws | `p or (p and q) <=> p`, `p and (p or q) <=> p` |
| logic.prop.excluded-middle | Excluded middle | `p or not p <=> true` |
| logic.prop.non-contradiction | Non-contradiction | `p and not p <=> false` |
| logic.prop.implication | Material implication | `(p => q) <=> (not p or q)` |
| logic.prop.contrapositive | Contrapositive | `(p => q) <=> (not q => not p)` |
| logic.prop.negated-implication | Negation of an implication | `not (p => q) <=> p and not q` |
| logic.prop.biconditional | Biconditional | `(p <=> q) <=> (p => q) and (q => p)` |
| logic.prop.biconditional-negation | Negated biconditional | `not (p <=> q) <=> (p <=> not q)` |
| logic.prop.exportation | Exportation | `((p and q) => r) <=> (p => (q => r))` |
| logic.prop.implication-and | Common antecedent | `(p => q) and (p => r) <=> (p => (q and r))` |
| logic.prop.implication-or | Common consequent | `(p => r) and (q => r) <=> ((p or q) => r)` |
| logic.prop.xor | Exclusive or | `(p xor q) <=> (p or q) and not (p and q)` |

### Rules of inference and fallacies

| ID | Kind | Name | Form |
| --- | --- | --- | --- |
| logic.rule.modus-ponens | theorem | Modus ponens | `p, p => q ⊢ q` |
| logic.rule.modus-tollens | theorem | Modus tollens | `not q, p => q ⊢ not p` |
| logic.rule.hypothetical-syllogism | theorem | Hypothetical syllogism | `p => q, q => r ⊢ p => r` |
| logic.rule.disjunctive-syllogism | theorem | Disjunctive syllogism | `p or q, not p ⊢ q` |
| logic.rule.addition | theorem | Addition | `p ⊢ p or q` |
| logic.rule.simplification | theorem | Simplification | `p and q ⊢ p` |
| logic.rule.conjunction | theorem | Conjunction | `p, q ⊢ p and q` |
| logic.rule.resolution | theorem | Resolution | `p or q, not p or r ⊢ q or r` |
| logic.rule.constructive-dilemma | theorem | Constructive dilemma | `p => q, r => s, p or r ⊢ q or s` |
| logic.rule.destructive-dilemma | theorem | Destructive dilemma | `p => q, r => s, not q or not s ⊢ not p or not r` |
| logic.fallacy.affirming-consequent | pattern | Affirming the consequent (invalid) | `p => q, q ⊬ p` |
| logic.fallacy.denying-antecedent | pattern | Denying the antecedent (invalid) | `p => q, not p ⊬ not q` |
| logic.fallacy.affirming-disjunct | pattern | Affirming a disjunct (invalid for inclusive or) | `p or q, p ⊬ not q` |
| logic.fallacy.converse | pattern | Confusing an implication with its converse | `p => q` does not give `q => p` |

### Natural deduction (kernel rules)

| ID | Rule | From | Conclude | Side condition |
| --- | --- | --- | --- | --- |
| logic.nd.and-intro | ∧I | Γ ⊢ p and Γ ⊢ q | Γ ⊢ p ∧ q | |
| logic.nd.and-elim | ∧E | Γ ⊢ p ∧ q | Γ ⊢ p (or q) | |
| logic.nd.or-intro | ∨I | Γ ⊢ p | Γ ⊢ p ∨ q | |
| logic.nd.or-elim | ∨E (proof by cases) | Γ ⊢ p ∨ q; Γ, p ⊢ r; Γ, q ⊢ r | Γ ⊢ r | |
| logic.nd.implies-intro | →I | Γ, p ⊢ q | Γ ⊢ p → q | Discharges p |
| logic.nd.implies-elim | →E | Γ ⊢ p → q and Γ ⊢ p | Γ ⊢ q | |
| logic.nd.not-intro | ¬I | Γ, p ⊢ ⊥ | Γ ⊢ ¬p | |
| logic.nd.not-elim | ¬E | Γ ⊢ p and Γ ⊢ ¬p | Γ ⊢ ⊥ | |
| logic.nd.false-elim | ⊥E (ex falso) | Γ ⊢ ⊥ | Γ ⊢ p | |
| logic.nd.raa | RAA (classical) | Γ, ¬p ⊢ ⊥ | Γ ⊢ p | |
| logic.nd.forall-intro | ∀I | Γ ⊢ P(a) | Γ ⊢ ∀x P(x) | a not free in Γ or ∀x P(x) |
| logic.nd.forall-elim | ∀E | Γ ⊢ ∀x P(x) | Γ ⊢ P(t) | t free for x |
| logic.nd.exists-intro | ∃I | Γ ⊢ P(t) | Γ ⊢ ∃x P(x) | |
| logic.nd.exists-elim | ∃E | Γ ⊢ ∃x P(x); Γ, P(a) ⊢ q | Γ ⊢ q | a not free in Γ, q or ∃x P(x) |
| logic.nd.eq-refl | =I | | Γ ⊢ t = t | |
| logic.nd.eq-subst | =E | Γ ⊢ s = t and Γ ⊢ P(s) | Γ ⊢ P(t) | |
| logic.nd.induction | ℕ-induction | Γ ⊢ P(0); Γ ⊢ ∀n (P(n) → P(n + 1)) | Γ ⊢ ∀n P(n) | n ∈ ℕ |

## Predicate logic

| ID | Name | Statement | Conditions |
| --- | --- | --- | --- |
| logic.pred.not-forall | Negating ∀ | `not (forall x: P(x)) <=> exists x: not P(x)` | |
| logic.pred.not-exists | Negating ∃ | `not (exists x: P(x)) <=> forall x: not P(x)` | |
| logic.pred.forall-and | ∀ distributes over ∧ | `(forall x: P(x) and Q(x)) <=> (forall x: P(x)) and (forall x: Q(x))` | |
| logic.pred.exists-or | ∃ distributes over ∨ | `(exists x: P(x) or Q(x)) <=> (exists x: P(x)) or (exists x: Q(x))` | |
| logic.pred.exists-and | ∃ over ∧ (one way) | `(exists x: P(x) and Q(x)) => (exists x: P(x)) and (exists x: Q(x))` | Converse false |
| logic.pred.forall-or | ∀ over ∨ (one way) | `(forall x: P(x)) or (forall x: Q(x)) => (forall x: P(x) or Q(x))` | Converse false |
| logic.pred.swap-same | Swap like quantifiers | `forall x: forall y: P <=> forall y: forall x: P` (same for ∃) | |
| logic.pred.exists-forall | ∃∀ implies ∀∃ | `(exists x: forall y: P(x, y)) => (forall y: exists x: P(x, y))` | Converse false |
| logic.pred.null-and | Null quantification | `(forall x: P(x) and Q) <=> (forall x: P(x)) and Q` | x not free in Q |
| logic.pred.null-implies | Null quantification (antecedent) | `(forall x: P(x) => Q) <=> ((exists x: P(x)) => Q)` | x not free in Q |
| logic.pred.bounded-forall | Bounded ∀ | `(forall x in S: P(x)) <=> forall x: (x in S => P(x))` | |
| logic.pred.bounded-exists | Bounded ∃ | `(exists x in S: P(x)) <=> exists x: (x in S and P(x))` | |
| logic.pred.unique-exists | Unique existence | `(exists! x: P(x)) <=> exists x: (P(x) and forall y: (P(y) => y = x))` | |
| logic.pred.prenex | Prenex normal form | Every formula is equivalent to one with all quantifiers in front | Rename bound variables first |

## Sets

| ID | Name | Statement |
| --- | --- | --- |
| logic.set.extensionality | Extensionality | `A = B <=> forall x: (x in A <=> x in B)` |
| logic.set.subset | Subset | `A ⊆ B <=> forall x: (x in A => x in B)` |
| logic.set.double-inclusion | Equality by double inclusion | `A = B <=> A ⊆ B and B ⊆ A` |
| logic.set.empty-subset | Empty set is a subset of every set | `EmptySet ⊆ A` |
| logic.set.union-def | Union | `x in A ∪ B <=> x in A or x in B` |
| logic.set.intersection-def | Intersection | `x in A ∩ B <=> x in A and x in B` |
| logic.set.difference-def | Difference | `x in A ∖ B <=> x in A and x ∉ B` |
| logic.set.complement-def | Complement | `x in A^c <=> x in U and x ∉ A` |
| logic.set.symdiff-def | Symmetric difference | `A △ B = (A ∖ B) ∪ (B ∖ A)` |
| logic.set.commutative | Commutative laws | `A ∪ B = B ∪ A`, `A ∩ B = B ∩ A` |
| logic.set.associative | Associative laws | `(A ∪ B) ∪ C = A ∪ (B ∪ C)`, same for ∩ |
| logic.set.distributive | Distributive laws | `A ∩ (B ∪ C) = (A ∩ B) ∪ (A ∩ C)`, `A ∪ (B ∩ C) = (A ∪ B) ∩ (A ∪ C)` |
| logic.set.de-morgan | De Morgan for sets | `(A ∪ B)^c = A^c ∩ B^c`, `(A ∩ B)^c = A^c ∪ B^c` |
| logic.set.identity | Identity laws | `A ∪ EmptySet = A`, `A ∩ U = A` |
| logic.set.domination | Domination laws | `A ∪ U = U`, `A ∩ EmptySet = EmptySet` |
| logic.set.idempotent | Idempotent laws | `A ∪ A = A`, `A ∩ A = A` |
| logic.set.absorption | Absorption laws | `A ∪ (A ∩ B) = A`, `A ∩ (A ∪ B) = A` |
| logic.set.complement-laws | Complement laws | `A ∪ A^c = U`, `A ∩ A^c = EmptySet`, `(A^c)^c = A` |
| logic.set.difference-as-intersection | Difference via complement | `A ∖ B = A ∩ B^c` |
| logic.set.powerset-card | Size of a power set | `card(P(A)) = 2^card(A)` for finite A |
| logic.set.product-card | Size of a Cartesian product | `card(A × B) = card(A)*card(B)` for finite A, B |

## Relations

| ID | Kind | Name | Statement |
| --- | --- | --- | --- |
| logic.rel.reflexive | definition | Reflexive | `forall a in A: a R a` |
| logic.rel.irreflexive | definition | Irreflexive | `forall a in A: not (a R a)` |
| logic.rel.symmetric | definition | Symmetric | `a R b => b R a` |
| logic.rel.antisymmetric | definition | Antisymmetric | `a R b and b R a => a = b` |
| logic.rel.asymmetric | definition | Asymmetric | `a R b => not (b R a)` |
| logic.rel.transitive | definition | Transitive | `a R b and b R c => a R c` |
| logic.rel.equivalence | definition | Equivalence relation | Reflexive, symmetric and transitive |
| logic.rel.partition | theorem | Equivalence classes partition the set | The classes of an equivalence relation on A are non-empty, pairwise disjoint and cover A; conversely every partition defines one |
| logic.rel.partial-order | definition | Partial order | Reflexive, antisymmetric and transitive |
| logic.rel.total-order | definition | Total order | Partial order in which every two elements are comparable |
| logic.rel.well-order | definition | Well-order | Total order in which every non-empty subset has a least element |
| logic.rel.inverse | definition | Inverse relation | `b R^-1 a <=> a R b` |
| logic.rel.composition | definition | Composition | `a (S ∘ R) c <=> exists b: a R b and b S c` |
| logic.rel.transitive-closure | method | Transitive closure (Warshall) | Smallest transitive relation containing R |

## Functions

| ID | Kind | Name | Statement |
| --- | --- | --- | --- |
| logic.fn.def | definition | Function | A relation f ⊆ A × B in which every a ∈ A is related to exactly one b ∈ B |
| logic.fn.injective | definition | Injective (one-to-one) | `f(a1) = f(a2) => a1 = a2` |
| logic.fn.surjective | definition | Surjective (onto) | `forall b in B: exists a in A: f(a) = b` |
| logic.fn.bijective | definition | Bijective | Injective and surjective |
| logic.fn.compose-injective | theorem | Composition preserves injectivity | f, g injective ⇒ g ∘ f injective (same for surjective, bijective) |
| logic.fn.inverse-exists | theorem | Inverse exists iff bijective | f has a two-sided inverse ⇔ f is bijective |
| logic.fn.left-inverse | theorem | Left inverse iff injective | For non-empty A |
| logic.fn.right-inverse | theorem | Right inverse iff surjective | Uses the axiom of choice for infinite sets |
| logic.fn.image-union | theorem | Image of a union | `f(A1 ∪ A2) = f(A1) ∪ f(A2)` |
| logic.fn.image-intersection | theorem | Image of an intersection | `f(A1 ∩ A2) ⊆ f(A1) ∩ f(A2)`, equality when f is injective |
| logic.fn.preimage-union | theorem | Preimage of a union | `f^-1(B1 ∪ B2) = f^-1(B1) ∪ f^-1(B2)` |
| logic.fn.preimage-intersection | theorem | Preimage of an intersection | `f^-1(B1 ∩ B2) = f^-1(B1) ∩ f^-1(B2)` |
| logic.fn.preimage-complement | theorem | Preimage of a complement | `f^-1(B^c) = f^-1(B)^c` |

## Cardinality

| ID | Kind | Name | Statement |
| --- | --- | --- | --- |
| logic.card.equinumerous | definition | Same cardinality | card(A) = card(B) iff there is a bijection A → B |
| logic.card.pigeonhole | theorem | Pigeonhole principle | n + 1 objects in n boxes put at least two objects in one box; generally some box holds at least ⌈N/k⌉ |
| logic.card.countable | definition | Countable | Finite or equinumerous with ℕ |
| logic.card.integers-countable | theorem | ℤ and ℚ are countable | |
| logic.card.reals-uncountable | theorem | ℝ is uncountable | Cantor's diagonal argument |
| logic.card.cantor | theorem | Cantor's theorem | `card(A) < card(P(A))` |
| logic.card.schroeder-bernstein | theorem | Schröder–Bernstein | Injections A → B and B → A give a bijection |

## Proof techniques

Each technique is a `method` entry with a proof template; the tactic of the same name builds the skeleton and the renderer labels it.

| ID | Technique | Template |
| --- | --- | --- |
| logic.tech.direct | Direct proof | Assume p; derive q through definitions and known results; conclude p → q |
| logic.tech.contrapositive | Proof by contrapositive | Assume ¬q; derive ¬p |
| logic.tech.contradiction | Proof by contradiction | Assume ¬p; derive a contradiction; conclude p |
| logic.tech.cases | Proof by cases (exhaustion) | Split into cases that cover all possibilities; prove each |
| logic.tech.biconditional | Biconditional proof | Prove p → q and q → p |
| logic.tech.existence-constructive | Constructive existence | Exhibit a witness and verify it |
| logic.tech.existence-nonconstructive | Non-constructive existence | Show non-existence leads to a contradiction, or argue by cases without naming the witness |
| logic.tech.uniqueness | Uniqueness | Assume two objects satisfy P; show they are equal |
| logic.tech.counterexample | Disproof by counterexample | Exhibit one instance that falsifies a universal claim |
| logic.tech.weak-induction | Mathematical induction | Base case P(n₀); inductive step P(k) → P(k + 1) |
| logic.tech.strong-induction | Strong induction | Base cases; P(n₀) ∧ … ∧ P(k) → P(k + 1) |
| logic.tech.structural-induction | Structural induction | Prove for base constructors and for each constructor assuming its parts |
| logic.tech.well-ordering | Well-ordering principle | Assume a counterexample exists; take the least one; contradict minimality |
| logic.tech.infinite-descent | Infinite descent | From any solution build a strictly smaller one in ℕ |
| logic.tech.double-counting | Combinatorial proof (double counting) | Count one set in two ways |
| logic.tech.bijective | Bijective proof | Build a bijection between two sets |
| logic.tech.invariant | Invariant or monovariant | Find a quantity preserved (or strictly changing) by every move |
| logic.tech.epsilon-delta | ε–δ proof | Given ε > 0, choose δ (scratch work on the side), verify the implication |
| logic.tech.two-column | Two-column proof | Statements on the left, each justified by a cited law, definition or previous line |
| logic.tech.wlog | Without loss of generality | Reduce symmetric cases to one, citing the symmetry |

### Worked theorems with stored proofs

| ID | Statement | Technique |
| --- | --- | --- |
| logic.example.sqrt2-irrational | `sqrt(2) ∉ Q` | Contradiction |
| logic.example.infinitely-many-primes | There are infinitely many primes (Euclid) | Contradiction |
| logic.example.gauss-sum | `sum(k, k, 1, n) = n*(n + 1)/2` | Weak induction |
| logic.example.n-cubed-minus-n | `divides(6, n^3 - n)` for n ∈ ℤ | Factor as (n − 1)n(n + 1) and cases |
| logic.example.prime-factor | Every integer n ≥ 2 has a prime factor | Strong induction |
| logic.example.irrational-power | There exist irrational a, b with a^b rational | Non-constructive, using √2^√2 |
| logic.example.odd-square | n odd ⇒ n² odd | Direct |
| logic.example.square-even | n² even ⇒ n even | Contrapositive |

## Decision procedures and tactics

| ID | Kind | Name | Decides or proves | Notes |
| --- | --- | --- | --- | --- |
| logic.dp.truth-table | method | Truth tables | Propositional validity and equivalence | Exponential; used for ≤ 12 variables and for teaching |
| logic.dp.cdcl | method | CDCL SAT solving | Propositional satisfiability | Tseitin encoding, two-watched literals, clause learning, restarts |
| logic.dp.congruence-closure | method | Congruence closure | Ground equalities with uninterpreted functions | Used inside `Simp` |
| logic.dp.ring | method | Ring normalization | Identities in commutative rings; field identities with non-zero hypotheses | Kernel `RingEq`/`FieldEq` |
| logic.dp.linear-arith | method | Linear arithmetic over ℚ | Conjunctions of linear inequalities (Fourier–Motzkin; simplex later) | `LinearArith` tactic |
| logic.dp.induction-sums | method | Automatic induction for sum identities | `sum(f(k), k, 1, n) = g(n)` | Base case plus `g(n) + f(n + 1) - g(n + 1)` normalizes to 0 |
| logic.dp.induction-divisibility | method | Automatic induction for divisibility | `divides(m, f(n))` | Show `f(n + 1) - c*f(n)` is a multiple of m |
| logic.dp.counterexample | method | Counterexample search | Refutes universal claims | Random and boundary values, then small exhaustive domains |

Tactics: `Intro`, `Apply`, `Exact`, `Cases`, `Induction`, `StrongInduction`, `Rewrite(law, direction)`, `Ring`, `Field`, `Simp`, `LinearArith`, `Contradiction`, `Contrapositive`, `Witness(term)`, `Unfold(definition)`. Each tactic returns subgoals and a kernel-checked assembly function.

## Rendering

- **Two-column:** statements and reasons; the default for algebra and trigonometry identities.
- **Fitch style:** nested boxes for assumptions, for natural-deduction courses.
- **Paragraph:** prose generated from the proof tree with technique-specific openings ("Suppose, for contradiction, that …").
- **LaTeX:** `align*` for equational chains and `bussproofs` trees for natural deduction.

## Abilities

| Ability | Example | Milestone |
| --- | --- | --- |
| Truth tables and classification | `(p => q) <=> (not q => not p)` is a tautology | 3 |
| Simplify propositions; NNF, CNF, DNF, prenex form | `not (p and (q or not r))` → `not p or (not q and r)` | 3 |
| Check validity of an argument; name the fallacy if invalid | "If it rains the street is wet; the street is wet; so it rained" → affirming the consequent | 3 |
| Satisfiability with a model | 3-coloring of a small graph encoded as CNF | 3 |
| Prove algebraic and trigonometric identities | From any derivation, kernel-checked | 3 |
| Induction proofs for sums, products, divisibility | `sum(k^3, k, 1, n) = (n*(n + 1)/2)^2` | 3 |
| Set identities by element chasing or algebra of sets | `A ∖ (B ∪ C) = (A ∖ B) ∩ (A ∖ C)` | 3 |
| Relation and function property checks with witnesses | "Is ≡ (mod 5) an equivalence relation on ℤ?" | 3 |
| Check a user-written proof and report the first invalid step | | 3 |
| Find counterexamples | "n² + n + 41 is prime for all n ∈ ℕ" fails at n = 40 | 3 |

### Example: induction proof, two-column rendering

Claim `logic.example.gauss-sum`: for all n ≥ 1, `sum(k, k, 1, n) = n*(n + 1)/2`.

| # | Statement | Reason |
| --- | --- | --- |
| 1 | `sum(k, k, 1, 1) = 1 = 1*(1 + 1)/2` | Base case, arithmetic |
| 2 | Assume `sum(k, k, 1, m) = m*(m + 1)/2` for some m ≥ 1 | Inductive hypothesis |
| 3 | `sum(k, k, 1, m + 1) = sum(k, k, 1, m) + (m + 1)` | Splitting the last term (`calc.sum.split-last`) |
| 4 | `= m*(m + 1)/2 + (m + 1)` | Line 2, substitution (`logic.nd.eq-subst`) |
| 5 | `= (m + 1)*(m + 2)/2` | Ring normalization (`logic.dp.ring`) |
| 6 | For all n ≥ 1 the formula holds ∎ | Lines 1–5, induction (`logic.nd.induction`) |
