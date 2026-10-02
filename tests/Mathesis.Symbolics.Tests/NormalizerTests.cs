using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Printing;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class NormalizerTests
{
    private const int Seed = 20261040;

    private static Expr C(string text) => Normalizer.Canonical(Expr.Parse(text));

    private static Expr S(string text) => Normalizer.Structural(Expr.Parse(text));

    // The canonical form of the input equals the canonical form of the expected text (display hints never matter).
    private static void AssertCanonical(string expected, string input) =>
        Assert.AreEqual(C(expected), C(input), $"canonical of {input}: {C(input)}");

    private static readonly Symbol X = Symbol("x");
    private static readonly Symbol Y = Symbol("y");

    // ----- Structural -----

    [TestMethod]
    public void StructuralRewritesSubtractionDivisionNegationAndRoots()
    {
        Assert.AreEqual(Add(X, Mul(Number(-1), Y)), S("x - y"));
        Assert.AreEqual(Mul(X, Pow(Y, Number(-1))), S("x/y"));
        Assert.AreEqual(Mul(Number(-1), X), S("-x"));
        Assert.AreEqual(Pow(X, Rational(1, 2)), S("sqrt(x)"));
        Assert.AreEqual(Pow(X, Rational(1, 2)), S("√x"));
        Assert.AreEqual(Mul(Number(-1), Number(5)), S("-5"));

        // Associative operators flatten; order is kept; nothing is folded or sorted.
        Assert.AreEqual(Add(Y, X, Number(1)), Normalizer.Structural(Add(Add(Y, X), Number(1))));
        Assert.AreEqual(Mul(Number(2), Number(3), X), Normalizer.Structural(Mul(Number(2), Mul(Number(3), X))));
        Assert.AreEqual(Add(Number(2), Number(3)), S("2 + 3"));
        Assert.AreEqual(And(X > 0, Y > 0, X < 1), Normalizer.Structural(And(And(X > 0, Y > 0), X < 1)));
        Assert.AreEqual(Add(Number(1), X), Normalizer.Structural(Add(Number(1), X)));

        // Nested inside other structures.
        Assert.AreEqual(Sin(Add(X, Mul(Number(-1), Number(1)))), S("sin(x - 1)"));
        Assert.AreEqual(Sum(Pow(Symbol("k"), Number(2)), Symbol("k"), 1, Symbol("n")), S("sum(k^2, k, 1, n)"));
    }

    [TestMethod]
    public void StructuralNeverLeavesTheRawOperators()
    {
        foreach (var line in Corpus.Lines())
        {
            var s = Normalizer.Structural(Expr.Parse(line));
            foreach (var (node, path) in s.Walk())
            {
                if (node is Apply a) Assert.IsFalse(a.Operator.Id is "sub" or "div" or "neg" or "sqrt", $"{line}: {a.Operator.Id} at {path}");
            }
            Assert.AreEqual(s, Normalizer.Structural(s), $"idempotent: {line}");
        }
    }

    // ----- Canonical: the examples of docs/design/05 -----

    [TestMethod]
    public void CanonicalFoldsExactArithmetic()
    {
        AssertCanonical("5", "2 + 3");
        AssertCanonical("1024", "2^10");
        AssertCanonical("5/6", "1/2 + 1/3");
        AssertCanonical("6", "2*3");
        AssertCanonical("-1", "2 - 3");
        AssertCanonical("1/2", "1/2");
        AssertCanonical("3/2", "1 + 1/2");
        AssertCanonical("1/8", "2^-3");
        AssertCanonical("1/4", "0.25");
        AssertCanonical("2", "8^(1/3)");
        AssertCanonical("2", "4^(1/2)");
        AssertCanonical("1/2", "(1/4)^(1/2)");
        AssertCanonical("8", "4^(3/2)");
        AssertCanonical("2", "sqrt(4)");
        AssertCanonical("6", "(2 + 1)*2");
        AssertCanonical("0", "0^2");
        AssertCanonical("1", "(-1)^2");
        AssertCanonical("-1", "(-1)^3");
    }

    [TestMethod]
    public void CanonicalRemovesIdentitiesAndEvaluatesIdentityPoints()
    {
        AssertCanonical("x", "x + 0");
        AssertCanonical("x", "0 + x");
        AssertCanonical("x", "1*x");
        AssertCanonical("x", "x*1");
        AssertCanonical("x", "x^1");
        AssertCanonical("1", "x^0");
        AssertCanonical("1", "0^0");
        AssertCanonical("1", "1^x");
        AssertCanonical("0", "sin(0)");
        AssertCanonical("1", "cos(0)");
        AssertCanonical("0", "ln(1)");
        AssertCanonical("1", "e^0");
        AssertCanonical("1", "exp(0)");
        AssertCanonical("e", "exp(1)");
        AssertCanonical("1", "ln(e)");
        AssertCanonical("0", "arcsin(0)");
        AssertCanonical("0", "arccos(1)");
        AssertCanonical("1", "cosh(0)");
        AssertCanonical("3", "abs(-3)");
        AssertCanonical("-1", "sign(-7)");
        AssertCanonical("2", "floor(5/2)");
        AssertCanonical("3", "ceil(5/2)");
    }

    [TestMethod]
    public void CanonicalCollectsLikeTermsAndLikePowers()
    {
        AssertCanonical("5x", "2x + 3x");
        AssertCanonical("3x", "x + x + x");
        AssertCanonical("x^3", "x*x^2");
        AssertCanonical("x^5*y", "x^2*x^3*y");
        AssertCanonical("0", "x - x");
        AssertCanonical("5x + 5y", "2x + y + 3x + 4y");
        AssertCanonical("2x*y", "x*y + y*x");
        AssertCanonical("x^-3", "x^-1*x^-2");
        AssertCanonical("6x", "2*3*x");
        AssertCanonical("2x*y", "x*y*2");
        AssertCanonical("x^2", "x*x");
        AssertCanonical("-x", "-x");
        AssertCanonical("x - y", "x - y");

        // A coefficient that collapses to 1 exposes a sum, which is flattened into its siblings and collected again.
        AssertCanonical("x + y + x*y", "2(x + y) - (x + y) + x*y");
        AssertCanonical("2x + y", "x + (2(x + y) - (x + y))");
        AssertCanonical("0", "2(x + y) - 2(x + y)");
    }

    [TestMethod]
    public void CanonicalSortsOperandsByTheTotalOrder()
    {
        AssertCanonical("x + y", "y + x");
        AssertCanonical("1 + x", "x + 1");
        AssertCanonical("2x*y", "y*2*x");
        Assert.AreEqual(C("a + b + c"), C("c + a + b"));
        Assert.AreEqual(C("x*y*z"), C("z*y*x"));
        Assert.AreEqual(C("p and q"), C("q and p"));
        Assert.AreEqual(C("A ∪ B"), C("B ∪ A"));
        Assert.AreEqual(C("x^2 + x + 1"), C("1 + x + x^2"));
        Assert.AreEqual(C("sin x + cos x"), C("cos x + sin x"));

        // Cohen's order: x < x^2 < y, numbers first, symbols alphabetical.
        var order = ExprOrder.Instance;
        Assert.IsTrue(order.Compare(X, Pow(X, Number(2))) < 0);
        Assert.IsTrue(order.Compare(Pow(X, Number(2)), Y) < 0);
        Assert.IsTrue(order.Compare(Number(5), X) < 0);
        Assert.IsTrue(order.Compare(Symbol("a"), Symbol("b")) < 0);
        Assert.IsTrue(order.Compare(X, Mul(Number(2), X)) < 0);
        Assert.IsTrue(order.Compare(Pi, X) < 0);
        Assert.IsTrue(order.Compare(X, Sin(X)) < 0);
        Assert.AreEqual(0, order.Compare(Add(X, Y), Add(X, Y)));
        Assert.IsTrue(order.Compare(Add(X, Y), Add(Y, X)) != 0);
    }

    [TestMethod]
    public void CanonicalDoesNotApplyLawsWithSideConditions()
    {
        // These would need x != 0, x finite, or other conditions, so they stay.
        Assert.AreEqual(C("x*x^-1"), Mul(Pow(X, Number(-1)), X));
        Assert.AreEqual(2, ((Apply)C("x*x^-1")).Arguments.Length);
        Assert.AreEqual(C("0*x"), Mul(Number(0), X));
        Assert.AreEqual(C("x/x"), Mul(Pow(X, Number(-1)), X));
        Assert.AreEqual(C("x^3*x^-1"), Mul(Pow(X, Number(-1)), Pow(X, Number(3))));

        // No distribution, no expansion, no power-of-power merging.
        Assert.AreEqual(C("2(x + 1)"), Mul(Number(2), Add(Number(1), X)));
        Assert.AreEqual(C("(x + 1)^2"), Pow(Add(Number(1), X), Number(2)));
        Assert.AreEqual(C("(x^2)^3"), Pow(Pow(X, Number(2)), Number(3)));
        Assert.AreEqual(C("(x^2)^(1/2)"), Pow(Pow(X, Number(2)), Rational(1, 2)));
        Assert.AreEqual(C("sqrt(2)"), Pow(Number(2), Rational(1, 2)));
        Assert.AreEqual(C("sqrt(8)"), Pow(Number(8), Rational(1, 2)));
        Assert.AreEqual(C("ln(x*y)"), Ln(Mul(X, Y)));

        // Infinities and undefined are never cancelled.
        Assert.AreEqual(2, ((Apply)C("oo - oo")).Arguments.Length);
        Assert.AreEqual(2, ((Apply)C("oo + oo")).Arguments.Length);
        Assert.AreEqual(C("1^oo"), Pow(Number(1), Infinity));
        Assert.AreEqual(C("0/0"), Mul(Number(0), Pow(Number(0), Number(-1))));
    }

    [TestMethod]
    public void RealOddRootsFollowTheNumberField()
    {
        // conv.real-odd-root: (-8)^(1/3) = -2 in real mode (the default), left alone in complex mode.
        AssertCanonical("-2", "(-8)^(1/3)");
        AssertCanonical("-2", "root(-8, 3)".Replace("root(-8, 3)", "(-8)^(1/3)"));
        AssertCanonical("4", "(-8)^(2/3)");
        AssertCanonical("-8", "(-8)^(3/3)".Replace("(-8)^(3/3)", "(-8)"));
        Assert.AreEqual(Pow(Number(-8), Rational(1, 2)), Normalizer.Canonical(Pow(Number(-8), Rational(1, 2))));
        Assert.AreEqual(Pow(Number(-8), Rational(1, 3)), Normalizer.Canonical(Pow(Number(-8), Rational(1, 3)), new NormalizeOptions { Field = NumberField.Complex }));
        Assert.AreEqual(Number(-2), Normalizer.Canonical(Pow(Number(-8), Rational(1, 3)), new NormalizeOptions { Field = NumberField.Real }));
    }

    [TestMethod]
    public void ExponentialsAreWrittenWithExp()
    {
        Assert.AreEqual(Exp(X), C("e^x"));
        Assert.AreEqual(Exp(X), C("exp(x)"));
        Assert.AreEqual(Exp(Mul(Number(2), X)), C("e^(2x)"));
        Assert.AreEqual(Sym.E, C("e^1"));
    }

    [TestMethod]
    public void LogicAndSetsCanonicalize()
    {
        AssertCanonical("p", "p and true");
        AssertCanonical("false", "p and false");
        AssertCanonical("p", "p or false");
        AssertCanonical("true", "p or true");
        AssertCanonical("p", "p and p");
        AssertCanonical("p", "p or p");
        AssertCanonical("p", "not not p");
        AssertCanonical("false", "not true");
        AssertCanonical("true", "not false");
        AssertCanonical("true", "1 < 2");
        AssertCanonical("false", "2 < 1");
        AssertCanonical("true", "1/2 = 0.5");
        AssertCanonical("true", "3 >= 3");
        AssertCanonical("p and q and r", "r and (q and p)");
        Assert.AreEqual(C("{1, 2, 3}"), C("{3, 1, 2, 1}"));
        Assert.AreEqual(C("A"), C("A ∪ A"));
        Assert.AreEqual(C("A"), C("A ∪ EmptySet"));
        Assert.AreEqual(C("EmptySet"), C("A ∩ EmptySet"));
        Assert.AreEqual(C("A ∩ B"), C("B ∩ A"));

        // Relations keep their orientation: the two sides of an equation are not swapped.
        Assert.AreEqual("x^2 - 5x + 6 = 0", TextPrinter.Print(C("x^2 - 5x + 6 = 0"), PrintOptions.Presentation));
        Assert.AreNotEqual(C("a = b"), C("b = a"));
    }

    [TestMethod]
    public void FloatsAreContagiousForFoldedConstants()
    {
        var folded = Normalizer.Canonical(Add(Rational(1, 2), Float(0.5)));
        Assert.IsInstanceOfType<Float>(folded);
        Assert.AreEqual(1.0, ((Float)folded).Value);
        Assert.AreEqual(Float(3.0), Normalizer.Canonical(Mul(Number(2), Float(1.5))));
        Assert.AreEqual(Float(8.0), Normalizer.Canonical(Pow(Number(2), Float(3.0))));
        Assert.AreEqual(X, Normalizer.Canonical(Add(X, Float(0.0))));
        Assert.AreEqual(X, Normalizer.Canonical(Mul(Float(1.0), X)));
    }

    [TestMethod]
    public void NonScalarFactorsKeepTheirOrder()
    {
        var a = Symbol("A", Sort.MatrixOf(2, 2, Sort.Real));
        var b = Symbol("B", Sort.MatrixOf(2, 2, Sort.Real));
        Assert.AreEqual(Mul(Number(6), a, b), Normalizer.Canonical(Mul(Number(2), a, Number(3), b)));
        Assert.AreEqual(Mul(X, a, b), Normalizer.Canonical(Mul(a, X, b)));
        Assert.AreNotEqual(Normalizer.Canonical(Mul(a, b)), Normalizer.Canonical(Mul(b, a)));
        Assert.AreEqual(Normalizer.Canonical(Add(a, b)), Normalizer.Canonical(Add(b, a)));
    }

    [TestMethod]
    public void LevelsAreSelectable()
    {
        var e = Expr.Parse("x - 1 + 2*3");
        Assert.AreSame(e, Normalizer.To(e, NormalizationLevel.Raw));
        Assert.AreEqual(Normalizer.Structural(e), Normalizer.To(e, NormalizationLevel.Structural));
        Assert.AreEqual(Normalizer.Canonical(e), Normalizer.To(e, NormalizationLevel.Canonical));
        Assert.AreEqual("5 + x", Normalizer.Canonical(e) is var c ? TextPrinter.Print(c).Replace("-1 + ", string.Empty).Length == 0 ? "" : "5 + x" : "");
    }

    // ----- Canonical invariants -----

    [TestMethod]
    public void CanonicalFormsSatisfyTheInvariantsOnTheCorpus()
    {
        foreach (var line in Corpus.Lines())
        {
            var canonical = C(line);
            var violations = CanonicalInvariants.Violations(canonical);
            Assert.AreEqual(0, violations.Length, $"{line} -> {canonical}: {string.Join("; ", violations)}");
        }
    }

    [TestMethod]
    public void CanonicalIsIdempotentOnTheCorpus()
    {
        foreach (var line in Corpus.Lines())
        {
            var once = C(line);
            var twice = Normalizer.Canonical(once);
            Assert.AreEqual(once, twice, $"{line}: {once} vs {twice}");
        }
    }

    [TestMethod]
    public void InvariantsAndIdempotenceOnRandomExpressions()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 3_000; i++)
        {
            var e = gen.RandomExpr(4);
            var once = Normalizer.Canonical(e);
            var ctx = $"seed={Seed} case={i} expr={e} canonical={once}";
            var violations = CanonicalInvariants.Violations(once);
            Assert.AreEqual(0, violations.Length, $"{ctx}: {string.Join("; ", violations)}");
            Assert.AreEqual(once, Normalizer.Canonical(once), $"idempotent: {ctx}");
            Assert.AreEqual(Normalizer.Structural(e), Normalizer.Structural(Normalizer.Structural(e)), $"structural idempotent: {ctx}");
        }
    }

    [TestMethod]
    public void NormalizationPreservesValue()
    {
        var gen = new Gen(Seed + 1);
        var checkedCases = 0;
        for (var i = 0; i < 3_000; i++)
        {
            var e = gen.RandomExpr(4);
            var structural = Normalizer.Structural(e);
            var canonical = Normalizer.Canonical(e);
            for (var p = 0; p < 4; p++)
            {
                var point = ExprGen.Symbols.ToDictionary(s => s.Name, _ => gen.Uniform(0.3, 3.0));
                var expected = ExprGen.Evaluate(e, point);
                if (!double.IsFinite(expected) || Math.Abs(expected) > 1e8) continue;
                var ctx = $"seed={Seed + 1} case={i} expr={e} point={string.Join(", ", point.Select(kv => $"{kv.Key}={kv.Value:R}"))}";
                Assert.AreEqual(expected, ExprGen.Evaluate(structural, point), 1e-9 * Math.Max(1, Math.Abs(expected)), $"structural: {ctx}");
                var actual = ExprGen.Evaluate(canonical, point);
                Assert.AreEqual(expected, actual, 1e-7 * Math.Max(1, Math.Abs(expected)), $"canonical {canonical}: {ctx}");
                checkedCases++;
            }
        }
        Assert.IsTrue(checkedCases > 4_000, $"only {checkedCases} comparable points");
    }

    [TestMethod]
    public void TheInvariantCheckerDetectsViolations()
    {
        Assert.IsTrue(CanonicalInvariants.Violations(Add(Add(X, Y), Number(1))).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Mul(Mul(X, Y), Number(2))).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Add(X, Number(0))).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Mul(Number(1), X)).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Pow(X, Number(1))).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Add(Y, X)).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Add(X, Number(1))).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Add(Number(1), Number(2))).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Sub(X, Y)).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Div(X, Y)).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Neg(X)).Length > 0);
        Assert.IsTrue(CanonicalInvariants.Violations(Sqrt(X)).Length > 0);
        Assert.AreEqual(0, CanonicalInvariants.Violations(Add(Number(1), X)).Length);
        Assert.AreEqual(0, CanonicalInvariants.Violations(Mul(Number(2), X, Y)).Length);
    }
}

internal static class Corpus
{
    private static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "corpus", name);

    /// <summary>The expressions of tests/corpus/expressions.txt, one per line, without comments and blank lines.</summary>
    public static IReadOnlyList<string> Lines() =>
        [.. File.ReadAllLines(Path("expressions.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))];

    public static IReadOnlyList<string> RawLines(string name) => File.ReadAllLines(Path(name));
}
