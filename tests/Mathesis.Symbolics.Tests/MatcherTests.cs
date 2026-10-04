using System.Collections.Immutable;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;
using Mathesis.Symbolics.Patterns;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class MatcherTests
{
    private static readonly ParserOptions Wilds = new() { AllowWilds = true };

    // The matcher works on flattened (Structural) trees, as the rewrite engine's states are.
    private static Expr P(string text) => Canonical.Normalizer.Structural(Expr.Parse(text, Wilds));

    private static Pattern Pat(string text, params (string Name, WildOptions Options)[] options) =>
        new(P(text), options.ToDictionary(o => o.Name, o => o.Options));

    private static Bindings? First(Pattern pattern, string subject) => Matcher.TryMatch(pattern, S(subject), out var b) ? b : null;

    private static Expr S(string text) => Canonical.Normalizer.Structural(Expr.Parse(text));

    private static string Show(Bindings? b, string name) => b![name].ToString();

    [TestMethod]
    public void SyntacticMatching()
    {
        var b = First(Pat("sin(a_)"), "sin(2*x)");
        Assert.IsNotNull(b);
        Assert.AreEqual("2x", Show(b, "a"));
        Assert.IsNull(First(Pat("sin(a_)"), "cos(2*x)"));
        Assert.IsNull(First(Pat("sin(a_)"), "x"));
        Assert.IsNotNull(First(Pat("x + 1"), "x + 1"));
        Assert.IsNull(First(Pat("x + 1"), "x + 2"));
        Assert.AreEqual("y", Show(First(Pat("a_^2"), "y^2"), "a"));
        Assert.IsNull(First(Pat("a_^2"), "y^3"));
    }

    [TestMethod]
    public void NonLinearPatternsRequireEqualOccurrences()
    {
        Assert.IsNotNull(First(Pat("a_ - a_"), "x - x"));
        Assert.IsNotNull(First(Pat("max(a_, a_)"), "max(y + 1, y + 1)"));
        Assert.IsNull(First(Pat("a_ - a_"), "x - y"));
        Assert.IsNotNull(First(Pat("max(a_, b_, a_)"), "max(1, 2, 1)"));
        Assert.IsNull(First(Pat("max(a_, b_, a_)"), "max(1, 2, 3)"));
    }

    [TestMethod]
    public void SortConstrainedWildcards()
    {
        var integer = Pat("x^n_", ("n", new WildOptions(Sort.Integer)));
        Assert.IsNotNull(First(integer, "x^3"));
        Assert.IsNull(First(integer, "x^(1/2)"));
        Assert.IsNull(First(integer, "x^y"));
        var typed = Matcher.TryMatch(integer, Pow(Symbol("x"), Symbol("k", Sort.Integer)), out _);
        Assert.IsTrue(typed, "an integer-sorted symbol satisfies the constraint");
        Assert.IsFalse(Matcher.TryMatch(Pat("a_", ("a", new WildOptions(Sort.Natural))), Number(-3), out _));
        Assert.IsTrue(Matcher.TryMatch(Pat("a_", ("a", new WildOptions(Sort.Real))), Number(-3), out _));
        Assert.IsTrue(Matcher.TryMatch(Pat("a_", ("a", new WildOptions(Sort.Complex))), Number(-3), out _));
    }

    [TestMethod]
    public void FreeOfConstraints()
    {
        var x = Symbol("x");
        var pattern = Pat("c_*x", ("c", new WildOptions(FreeOf: [x])));
        Assert.IsTrue(Matcher.TryMatch(pattern, Expr.Parse("3*x"), out _));
        Assert.IsFalse(Matcher.TryMatch(pattern, Expr.Parse("x^2*x"), out _));
        Assert.IsTrue(Matcher.TryMatch(pattern, Expr.Parse("a*x"), out _));
    }

    [TestMethod]
    public void OptionalWildcardsUseTheirDefaults()
    {
        // a_.*x^n_. covers x, 3x, x^4 and 3x^4.
        var one = Number(1);
        var pattern = Pat("a_*x^n_", ("a", new WildOptions(Default: one)), ("n", new WildOptions(Default: one)));
        foreach (var (subject, a, n) in new[] { ("x", "1", "1"), ("3*x", "3", "1"), ("x^4", "1", "4"), ("3*x^4", "3", "4") })
        {
            var b = First(pattern, subject);
            Assert.IsNotNull(b, subject);
            Assert.AreEqual(a, Show(b, "a"), subject);
            Assert.AreEqual(n, Show(b, "n"), subject);
        }
        Assert.IsNull(First(pattern, "y"));
        Assert.IsNull(First(pattern, "3*y^4"));
    }

    [TestMethod]
    public void AssociativeCommutativeMatchingIgnoresOrder()
    {
        var pattern = Pat("a_^2 + 2*a_*b_ + b_^2");
        foreach (var subject in new[] { "x^2 + 2*x*y + y^2", "y^2 + x^2 + 2*y*x", "2*x*y + y^2 + x^2" })
        {
            var b = First(pattern, subject);
            Assert.IsNotNull(b, subject);
            Assert.AreEqual(2, b.Count);
        }
        Assert.IsNull(First(pattern, "x^2 + 3*x*y + y^2"));
        Assert.IsNull(First(pattern, "x^2 + 2*x*y"));
    }

    [TestMethod]
    public void WildsAbsorbOperandsOfAnAssociativeOperator()
    {
        var all = Matcher.Match(Pat("a_ + b_"), S("x + y + z")).ToList();
        Assert.IsTrue(all.Count >= 3);
        foreach (var b in all)
        {
            var rebuilt = Add(b["a"], b["b"]);
            Assert.AreEqual(Normalize("x + y + z"), Normalize(rebuilt.ToString()));
        }
        Assert.IsTrue(all.Any(b => b["b"].ToString() is "y + z" or "z + y"));

        // a_*(b_ + c_) against 2*x*(y + z): the product operands that are not the sum go to a.
        var distributive = Matcher.Match(Pat("a_*(b_ + c_)"), S("2*x*(y + z)")).ToList();
        Assert.IsTrue(distributive.Any(b => b["a"].ToString() is "2x" or "x*2" or "2*x"));
    }

    private static string Normalize(string text) => Canonical.Normalizer.Canonical(Expr.Parse(text)).ToString();

    [TestMethod]
    public void SequenceWildcardsBindTheRemainingOperands()
    {
        var pattern = Pat("max(x^2, rest__)", ("rest", new WildOptions(IsSequence: true)));
        var b = First(pattern, "max(x^2, y, z, 3)");
        Assert.IsNotNull(b);
        Assert.AreEqual(3, b.Sequences["rest"].Length);
        Assert.AreEqual(1, First(pattern, "max(x^2, 0)")!.Sequences["rest"].Length);
        Assert.AreEqual("y", First(pattern, "max(y, x^2)")!.Sequences["rest"][0].ToString(), "max is commutative, so the pattern operand may be anywhere");

        // In the middle: f(first_, mid__, last_).
        var middle = Pat("diff(first_, mid__, last_)", ("mid", new WildOptions(IsSequence: true)));
        var m = First(middle, "diff(1, 2, 3, 4)");
        Assert.AreEqual("1", Show(m, "first"));
        Assert.AreEqual("4", Show(m, "last"));
        Assert.AreEqual(2, m!.Sequences["mid"].Length);

        // Sequence wilds in an associative-commutative operator take everything else.
        var add = Pat("x^2 + rest__", ("rest", new WildOptions(IsSequence: true)));
        var s = First(add, "y + x^2 + z");
        Assert.IsNotNull(s);
        Assert.AreEqual(2, s.Sequences["rest"].Length);
    }

    [TestMethod]
    public void RootPatternsMayLeaveOperandsOver()
    {
        var pattern = Pat("sin(t_)^2 + cos(t_)^2");
        var b = First(pattern, "sin(u)^2 + cos(u)^2 + 3");
        Assert.IsNotNull(b);
        Assert.AreEqual(1, b.Leftover.Length);
        Assert.AreEqual("3", b.Leftover[0].ToString());
        Assert.IsNull(First(pattern, "sin(u)^2 + cos(v)^2"));

        // A nested pattern must account for every operand: sqrt(sin^2 + cos^2) does not match sqrt(sin^2 + cos^2 + 3).
        var nested = Pat("sqrt(sin(t_)^2 + cos(t_)^2)");
        Assert.IsNotNull(First(nested, "sqrt(sin(u)^2 + cos(u)^2)"));
        Assert.IsNull(First(nested, "sqrt(sin(u)^2 + cos(u)^2 + 3)"));
    }

    [TestMethod]
    public void CommutativeOperatorsMatchEitherOrder()
    {
        Assert.IsNotNull(First(Pat("a_ = 0"), "0 = x"));
        Assert.AreEqual("x", Show(First(Pat("a_ = 0"), "0 = x"), "a"));
        Assert.IsNull(First(Pat("a_ < 0"), "0 < x"), "less-than is not commutative");
    }

    [TestMethod]
    public void MatchingUnderBinders()
    {
        var pattern = Pat("sum(k^2, k, 1, n_)");
        var b = First(pattern, "sum(j^2, j, 1, 10)");
        Assert.IsNotNull(b, "bound variables are renamed before matching");
        Assert.AreEqual("10", Show(b, "n"));
    }

    [TestMethod]
    public void TheAttemptBudgetBoundsAcMatching()
    {
        var pattern = Pat("a_*b_ + c_*d_ + e_*f_");
        var subject = S("p*q + r*s + t*u + v*w + x*y");
        var limited = Matcher.Match(pattern, subject, new MatchOptions(MaxAttempts: 3)).ToList();
        var full = Matcher.Match(pattern, subject).Take(5).ToList();
        Assert.IsTrue(limited.Count < full.Count || full.Count == 0);

        var budget = new Budget(maxSteps: 2);
        _ = Matcher.Match(pattern, subject, new MatchOptions(Budget: budget)).ToList();
        Assert.IsTrue(budget.IsExceeded);
    }

    [TestMethod]
    public void InstantiateReplacesWildsSplicesSequencesAndAppliesDefaults()
    {
        var pattern = Pat("max(x^2, rest__)", ("rest", new WildOptions(IsSequence: true)));
        var b = First(pattern, "max(x^2, y, z)")!;
        var rebuilt = Matcher.Instantiate(Expr.Parse("min(rest__, 1)", Wilds), b);
        Assert.AreEqual("min(y, z, 1)", rebuilt.ToString());

        var optional = Pat("a_*x", ("a", new WildOptions(Default: Number(1))));
        var m = First(optional, "x")!;
        Assert.AreEqual(Number(1), m["a"]);
        Assert.AreEqual("1x", Matcher.Instantiate(P("a_*x"), m, optional).ToString());
        Assert.ThrowsExactly<InvalidOperationException>(() => Matcher.Instantiate(P("a_ + b_"), Bindings.Empty.With("a", Number(1))));
        Assert.AreEqual("5", Matcher.Instantiate(P("a_"), Bindings.Empty.With("a", Number(5))).ToString());
    }

    [TestMethod]
    public void PatternsCanBeBuiltFromDeclaredVariables()
    {
        var statement = Expr.Parse("a^m * a^n");
        var pattern = Pattern.FromVariables(statement, new Dictionary<string, Sort> { ["a"] = Sort.Real, ["m"] = Sort.Real, ["n"] = Sort.Real });
        CollectionAssert.AreEquivalent(new[] { "a", "m", "n" }, pattern.Names.ToArray());
        Assert.IsTrue(Matcher.TryMatch(pattern, Expr.Parse("x^2 * x^3"), out var b));
        Assert.AreEqual("x", b["a"].ToString());
        Assert.IsFalse(Matcher.TryMatch(pattern, Expr.Parse("x^2 * y^3"), out _));
    }

    [TestMethod]
    public void NumbersAndConstantsMatchThemselves()
    {
        Assert.IsNotNull(First(Pat("2*a_"), "2*x"));
        Assert.IsNull(First(Pat("2*a_"), "3*x"));
        Assert.IsNotNull(First(Pat("pi*a_"), "pi*x"));
        Assert.IsNull(First(Pat("e*a_"), "pi*x"));
        Assert.IsNotNull(First(Pat("1/2"), "1/2"));
    }

    [TestMethod]
    public void NullArgumentsThrow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Matcher.Match(null!, Number(1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => Matcher.Match(Pat("a_"), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Matcher.Instantiate(null!, Bindings.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pattern(null!));
    }

    [TestMethod]
    public void BindingsBehaveLikeADictionary()
    {
        var b = Bindings.Empty.With("a", Number(1)).With("b", Number(2));
        Assert.AreEqual(2, b.Count);
        Assert.IsTrue(b.ContainsKey("a"));
        Assert.IsFalse(b.TryGetValue("c", out _));
        CollectionAssert.AreEquivalent(new[] { "a", "b" }, b.Keys.ToArray());
        Assert.AreEqual(2, b.Values.Count());
        Assert.AreEqual(2, b.AsEnumerable().Count());
        Assert.AreEqual("{a → 1, b → 2}", b.ToString());
    }
}
