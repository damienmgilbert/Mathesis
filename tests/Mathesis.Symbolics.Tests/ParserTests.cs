using System.Diagnostics;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class ParserTests
{
    private const int Seed = 20261050;

    private static readonly Symbol X = Symbol("x");
    private static readonly Symbol Y = Symbol("y");
    private static readonly Symbol Z = Symbol("z");

    private static Expr P(string text, ParserOptions? options = null) => Expr.Parse(text, options);

    // ----- The exit check: parse, print, parse is the identity -----

    [TestMethod]
    public void ParsePrintParseIsTheIdentityOnTheCorpus()
    {
        var lines = Corpus.Lines();
        Assert.IsTrue(lines.Count >= 300, $"the corpus has only {lines.Count} cases");
        foreach (var line in lines)
        {
            var parsed = Parser.Parse(line);
            Assert.IsTrue(parsed.Success, $"'{line}' failed to parse: {(parsed.Success ? string.Empty : parsed.Errors[0])}");
            var printed = parsed.Expr!.ToString();
            var reparsed = Parser.Parse(printed);
            Assert.IsTrue(reparsed.Success, $"'{line}' printed as '{printed}' which does not parse: {(reparsed.Success ? string.Empty : reparsed.Errors[0])}");
            Assert.AreEqual(parsed.Expr, reparsed.Expr, $"'{line}' printed as '{printed}' parses to {reparsed.Expr}");
        }
    }

    [TestMethod]
    public void TheCorpusSpansEveryOperatorFamilyAndNodeKind()
    {
        var families = OperatorFamily.None;
        var kinds = new HashSet<ExprKind>();
        var binders = new HashSet<Binder>();
        foreach (var line in Corpus.Lines())
        {
            var e = P(line);
            families |= e.Families;
            foreach (var (node, _) in e.Walk())
            {
                kinds.Add(node.Kind);
                if (node is Bind b) binders.Add(b.Binder);
            }
        }
        foreach (var family in Enum.GetValues<OperatorFamily>().Where(f => f != OperatorFamily.None)) Assert.IsTrue(families.HasFlag(family), $"no corpus line uses the {family} family");
        foreach (var kind in Enum.GetValues<ExprKind>().Where(k => k != ExprKind.Wild && k != ExprKind.Float)) Assert.IsTrue(kinds.Contains(kind), $"no corpus line produces a {kind} node");
        foreach (var binder in Enum.GetValues<Binder>()) Assert.IsTrue(binders.Contains(binder), $"no corpus line uses the {binder} binder");

        // Every built-in operator that has a notation the parser reads is exercised at least by name.
        var used = new HashSet<string>(Corpus.Lines().SelectMany(l => P(l).Walk()).Select(w => w.Expr).OfType<Apply>().Select(a => a.Operator.Id));
        var missing = Operators.All.Where(o => !used.Contains(o.Id)).Select(o => o.Id).ToList();
        Assert.IsTrue(missing.Count < 15, $"operators never parsed in the corpus: {string.Join(", ", missing)}");
    }

    // ----- Conventions of docs/design/05 -----

    [TestMethod]
    public void PrecedenceAndAssociativity()
    {
        Assert.AreEqual(Pow(Number(2), Pow(Number(3), Number(2))), P("2^3^2"));
        Assert.AreEqual(Neg(Pow(X, Number(2))), P("-x^2"));
        Assert.AreEqual(Sub(Sub(Symbol("a"), Symbol("b")), Symbol("c")), P("a - b - c"));
        Assert.AreEqual(Div(Div(Symbol("a"), Symbol("b")), Symbol("c")), P("a/b/c"));
        Assert.AreEqual(Add(Mul(Number(2), X), Number(1)), P("2x + 1"));
        Assert.AreEqual(Mul(Neg(Symbol("a")), Symbol("b")), P("-a*b"));
        Assert.AreEqual(Pow(Number(2), Neg(Number(3))), P("2^-3"));
        Assert.AreEqual(Add(Symbol("a"), Mul(Symbol("b"), Symbol("c"))), P("a + b*c"));
        Assert.AreEqual(Or(And(Symbol("p"), Symbol("q")), Symbol("r")), P("p and q or r"));
        Assert.AreEqual(Or(Symbol("p"), And(Symbol("q"), Symbol("r"))), P("p or q and r"));
        Assert.AreEqual(Implies(Symbol("p"), Implies(Symbol("q"), Symbol("r"))), P("p => q => r"));
        Assert.AreEqual(Iff(Iff(Symbol("p"), Symbol("q")), Symbol("r")), P("p <=> q <=> r"));
        Assert.AreEqual(And(Not(Symbol("p")), Symbol("q")), P("not p and q"));
        Assert.AreEqual(Not(Eq(X, Number(1))), P("not x = 1"));
        Assert.AreEqual(Union(Symbol("A"), Intersect(Symbol("B"), Symbol("D"))), P("A ∪ B ∩ D"));
    }

    [TestMethod]
    public void ChainedRelations()
    {
        Assert.AreEqual(And(Lt(Symbol("a"), Symbol("b")), Le(Symbol("b"), Symbol("c"))), P("a < b <= c"));
        Assert.AreEqual(And(Eq(Symbol("a"), Symbol("b")), Eq(Symbol("b"), Symbol("c"))), P("a = b = c"));
        Assert.AreEqual(And(Lt(Number(0), X), Lt(X, Number(1))), P("0 < x < 1"));
    }

    [TestMethod]
    public void DecimalsAreExact()
    {
        var tenth = (Number)P("0.1");
        Assert.AreEqual(BigRational.Create(1, 10), tenth.Value);
        Assert.AreEqual(NumberDisplayKind.Decimal, tenth.Display.Kind);
        Assert.AreEqual(1, tenth.Display.Digits);
        Assert.AreEqual(BigRational.Create(1, 400), ((Number)P("2.5e-3")).Value);
        Assert.AreEqual(BigRational.Parse("2000", null), ((Number)P("2e3")).Value);
        Assert.AreEqual(BigRational.Create(1, 2), ((Number)P(".5")).Value);
        Assert.IsInstanceOfType<Number>(P("0.1 + 0.2").Children[0]);
        Assert.AreEqual(Add(Decimal("0.1"), Decimal("0.2")), P("0.1 + 0.2"));
        Assert.AreEqual("0.25", P("0.25").ToString());
        Assert.AreEqual("0.250", P("0.250").ToString());
    }

    [TestMethod]
    public void ImplicitMultiplication()
    {
        Assert.AreEqual(Mul(Number(2), X), P("2x"));
        Assert.AreEqual(Mul(Number(3), Add(X, Number(1))), P("3(x + 1)"));
        Assert.AreEqual(Mul(Add(X, Number(1)), Sub(X, Number(1))), P("(x + 1)(x - 1)"));
        Assert.AreEqual(Mul(X, Y), P("x y"));
        Assert.AreEqual(Mul(Number(2), Sin(X)), P("2 sin x"));
        Assert.AreEqual(Mul(Mul(Number(3), Pow(X, Number(2))), Y), P("3x^2y"));
        Assert.AreEqual(Mul(Mul(X, Y), Z), P("xyz"));
        Assert.AreEqual(Mul(Mul(Number(2), X), Y), P("2 x y"));
        Assert.AreEqual(Mul(Number(2), Pi), P("2pi"));
        Assert.AreEqual(Mul(Pi, Pow(Symbol("r"), Number(2))), P("pi r^2"));
    }

    [TestMethod]
    public void OneOverTwoXWarnsAboutAmbiguousImplicitMultiplication()
    {
        var result = Parser.Parse("1/2x");
        Assert.AreEqual(Mul(Div(Number(1), Number(2)), X), result.Expr);
        Assert.AreEqual("AmbiguousImplicitMultiplication", result.Warnings.Single().Code);
        Assert.AreEqual(Div(Number(1), Mul(Number(2), X)), P("1/(2x)"));
        Assert.AreEqual(0, Parser.Parse("1/(2x)").Warnings.Length);
        Assert.AreEqual(0, Parser.Parse("2x/3").Warnings.Length);
    }

    [TestMethod]
    public void FunctionsWithoutParentheses()
    {
        Assert.AreEqual(Sin(X), P("sin x"));
        Assert.AreEqual(Sin(Mul(Number(2), X)), P("sin 2x"));
        Assert.AreEqual(Mul(Sin(X), Cos(X)), P("sin x cos x"));
        Assert.AreEqual(Add(Sin(X), Number(1)), P("sin x + 1"));
        Assert.AreEqual(Ln(Pow(X, Number(2))), P("ln x^2"));
        Assert.AreEqual(Mul(Sin(Mul(Number(2), X)), Cos(Mul(Number(3), X))), P("sin 2x cos 3x"));
        Assert.AreEqual(Div(Sin(X), Number(2)), P("sin x/2"));
        Assert.AreEqual(Sin(Neg(X)), P("sin -x"));
        Assert.AreEqual(Sqrt(X), P("sqrt x"));
        Assert.AreEqual(Mul(Sin(X), Cos(Y)), P("sinx cosy"));
    }

    [TestMethod]
    public void PowersOfFunctionsAndInverseNotation()
    {
        Assert.AreEqual(Pow(Sin(X), Number(2)), P("sin^2 x"));
        Assert.AreEqual(Pow(Sin(X), Number(2)), P("sin^2(x)"));
        Assert.AreEqual(Arcsin(X), P("sin^-1 x"));
        Assert.AreEqual(Arcsin(Mul(Number(2), X)), P("sin^-1(2x)"));
        Assert.AreEqual(Arctan(X), P("tan^-1 x"));
        Assert.AreEqual(Apply(Operators.Arsinh, X), P("sinh^-1 x"));
        Assert.AreEqual(Pow(Sin(X), Neg(Number(1))), P("(sin x)^-1"));
        Assert.AreEqual(Div(Number(1), Sin(X)), P("1/sin x"));
        Assert.AreEqual(Pow(Sin(X), Neg(Number(2))), P("sin^-2 x"));
        Assert.AreEqual(Add(Pow(Sin(X), Number(2)), Pow(Cos(X), Number(2))), P("sin^2 x + cos^2 x"));
    }

    [TestMethod]
    public void LogarithmConventions()
    {
        Assert.AreEqual(Ln(X), P("ln x"));
        Assert.AreEqual(Log(X, Number(2)), P("log(x, 2)"));
        Assert.AreEqual(Log(X, Number(10)), P("log x"));
        Assert.AreEqual(Log(X, Number(10)), P("log(x)"));
        Assert.AreEqual(Log(Number(8), Number(2)), P("log_2(8)"));
        Assert.AreEqual(Log(X, Symbol("b")), P("log_b(x)"));
        Assert.AreEqual(Ln(X), P("log x", new ParserOptions { LogMeansNatural = true }));
        Assert.AreEqual(Ln(X), P("log(x)", new ParserOptions { LogMeansNatural = true }));
        Assert.AreEqual(Log(X, Number(2)), P("log(x, 2)", new ParserOptions { LogMeansNatural = true }));
    }

    [TestMethod]
    public void PrimesAndLeibnizNotationBecomeDerivatives()
    {
        var f = Symbol("f", Sort.FunctionOf(Sort.Real, Sort.Real));
        Assert.AreEqual(Call(Apply(Operators.DerivativeOf, f, Number(1)), X), P("f'(x)"));
        Assert.AreEqual(Call(Apply(Operators.DerivativeOf, f, Number(2)), X), P("f''(x)"));
        Assert.AreEqual(Apply(Operators.DerivativeOf, Symbol("y", Sort.FunctionOf(Sort.Real, Sort.Real)), Number(2)), P("y''"));
        Assert.AreEqual(Diff(Y, X), P("dy/dx"));
        Assert.AreEqual(Diff(Pow(X, Number(2)), X), P("d/dx (x^2)"));
        Assert.AreEqual(Diff(Sin(X), X), P("d/dx sin x"));
        Assert.AreEqual(Diff(Y, X, Number(2)), P("d^2y/dx^2"));
        Assert.AreEqual(Diff(Call(f, X), X, Number(2)), P("d^2/dx^2 f(x)"));
        Assert.AreEqual(Diff(Symbol("f"), X), P("∂f/∂x"));
        Assert.AreEqual(Diff(Y, Symbol("t")), P("dy/dt"));
    }

    [TestMethod]
    public void UnicodeInputIsAccepted()
    {
        Assert.AreEqual(Pow(X, Number(2)), P("x²"));
        Assert.AreEqual(Pow(X, Number(10)), P("x¹⁰"));
        Assert.AreEqual(Pow(X, Neg(Number(1))), P("x⁻¹"));
        Assert.AreEqual(Sqrt(Number(2)), P("√2"));
        Assert.AreEqual(Mul(Sqrt(Number(2)), X), P("√2x"));
        Assert.AreEqual(Root(Number(8), Number(3)), P("∛8"));
        Assert.AreEqual(Pi, P("π"));
        Assert.AreEqual(Mul(Number(2), Pi), P("2π"));
        Assert.AreEqual(Mul(Symbol("a"), Symbol("b")), P("a·b"));
        Assert.AreEqual(Mul(Symbol("a"), Symbol("b")), P("a×b"));
        Assert.AreEqual(Div(Symbol("a"), Symbol("b")), P("a÷b"));
        Assert.AreEqual(Le(X, Y), P("x ≤ y"));
        Assert.AreEqual(Ge(X, Y), P("x ≥ y"));
        Assert.AreEqual(Ne(X, Y), P("x ≠ y"));
        Assert.AreEqual(Element(X, Constant(ConstantId.Reals)), P("x ∈ ℝ"));
        Assert.AreEqual(Infinity, P("∞"));
        Assert.AreEqual(Sub(Symbol("a"), Symbol("b")), P("a − b"));
        Assert.AreEqual(And(Symbol("p"), Symbol("q")), P("p ∧ q"));
        Assert.AreEqual(Implies(Symbol("p"), Symbol("q")), P("p → q"));
        Assert.AreEqual(Iff(Symbol("p"), Symbol("q")), P("p ↔ q"));
        Assert.AreEqual(Not(Symbol("p")), P("¬p"));
        Assert.AreEqual(Symbol("θ"), P("theta"));
        Assert.AreEqual(Mul(Symbol("α"), Symbol("β")), P("alpha beta"));
        Assert.AreEqual(Sum(Symbol("k"), Symbol("k"), 1, Symbol("n")), P("∑(k, k, 1, n)"));
        Assert.AreEqual(Abs(X), P("|x|"));
        Assert.AreEqual(Abs(Sub(Abs(X), Number(1))), P("||x| - 1|"));
        Assert.AreEqual(Floor(X), P("⌊x⌋"));
        Assert.AreEqual(Ceil(X), P("⌈x⌉"));
        Assert.AreEqual(Apply(Operators.Norm, Symbol("v")), P("‖v‖"));
    }

    [TestMethod]
    public void ConstantsAndNumberSets()
    {
        Assert.AreEqual(Sym.E, P("e"));
        Assert.AreEqual(Pow(Sym.E, X), P("e^x"));
        Assert.AreEqual(Sym.I, P("I"));
        Assert.AreEqual(Symbol("i"), P("i"));
        Assert.AreEqual(Sym.I, P("i", new ParserOptions { ImaginaryUnit = "i" }));
        Assert.AreEqual(Symbol("I"), P("I", new ParserOptions { ImaginaryUnit = "i" }));
        Assert.AreEqual(Symbol("e"), P("e", new ParserOptions { EIsSymbol = true }));
        Assert.AreEqual(Constant(ConstantId.GoldenRatio), P("GoldenRatio"));
        Assert.AreEqual(Symbol("φ"), P("phi"));
        Assert.AreEqual(Constant(ConstantId.ComplexInfinity), P("zoo"));
        Assert.AreEqual(Constant(ConstantId.Undefined), P("undefined"));
        Assert.AreEqual(Constant(ConstantId.Reals), P("R"));
        Assert.AreEqual(Constant(ConstantId.Naturals), P("N"));
        Assert.AreEqual(Symbol("R"), P("R", new ParserOptions { NumberSetLetters = false }));
        Assert.AreEqual(Constant(ConstantId.Reals), P("ℝ"));
        Assert.AreEqual(True, P("true"));
        Assert.AreEqual(Mul(Number(2), Sym.E), P("2e"));
    }

    [TestMethod]
    public void SingleLetterVariablesAndWords()
    {
        Assert.AreEqual(Mul(X, Y), P("xy"));
        Assert.AreEqual(Mul(X, Sin(Y)), P("xsin(y)"));
        Assert.AreEqual(Mul(Pi, X), P("pix"));
        Assert.AreEqual(Sin(Mul(X, Symbol("y"))), P("sinx y"));

        var words = new ParserOptions { SingleLetterVariables = false };
        Assert.AreEqual(Symbol("speed"), P("speed", words));
        Assert.AreEqual(Mul(Symbol("speed"), Symbol("time")), P("speed time", words));
        Assert.AreEqual(Sin(Symbol("speed")), P("sin(speed)", words));
        var declared = new ParserOptions { Declarations = new Dictionary<string, Sort> { ["speed"] = Sort.Natural, ["n"] = Sort.Integer } };
        Assert.AreEqual(Symbol("speed", Sort.Natural), P("speed", declared));
        Assert.AreEqual(Add(Symbol("n", Sort.Integer), Number(1)), P("n + 1", declared));
        Assert.AreEqual(Mul(Symbol("speed", Sort.Natural), X), P("speed x", declared));
    }

    [TestMethod]
    public void FunctionSymbolsAndApplications()
    {
        var f = Symbol("f", Sort.FunctionOf(Sort.Real, Sort.Real));
        var g2 = Symbol("g", Sort.FunctionOf(Sort.TupleOf(Sort.Real, Sort.Real), Sort.Real));
        Assert.AreEqual(Call(f, X), P("f(x)"));
        Assert.AreEqual(Call(f, Add(X, Number(1))), P("f(x + 1)"));
        Assert.AreEqual(Call(g2, X, Y), P("g(x, y)"));
        Assert.AreEqual(Mul(X, Add(X, Number(1))), P("x(x + 1)"));
        Assert.AreEqual(Mul(Symbol("a"), Add(Symbol("b"), Number(1))), P("a(b + 1)"));
        Assert.AreEqual(Call(Symbol("speed", Sort.FunctionOf(Sort.Real, Sort.Real)), X), P("speed(x)"));
        var declared = new ParserOptions { Declarations = new Dictionary<string, Sort> { ["k"] = Sort.FunctionOf(Sort.Real, Sort.Real) } };
        Assert.AreEqual(Call(Symbol("k", Sort.FunctionOf(Sort.Real, Sort.Real)), X), P("k(x)", declared));
        Assert.AreEqual(Call(Symbol("f_1", Sort.FunctionOf(Sort.Real, Sort.Real)), X), P("f_1(x)"));
        Assert.AreEqual(Prob(), P("P(A | B)"));
    }

    private static Expr Prob() => Apply(Operators.Prob, Symbol("A"), Symbol("B"));

    [TestMethod]
    public void OverloadedAbbreviationsDependOnTheArguments()
    {
        Assert.AreEqual(Apply(Operators.Perm, Symbol("n"), Symbol("k")), P("P(n, k)"));
        Assert.AreEqual(Apply(Operators.Prob, Symbol("A")), P("P(A)"));
        Assert.AreEqual(Apply(Operators.Binomial, Symbol("n"), Symbol("k")), P("C(n, k)"));
        Assert.AreEqual(Constant(ConstantId.Complexes), P("C"));
        Assert.AreEqual(Apply(Operators.Dirac, Symbol("t")), P("δ(t)"));
        Assert.AreEqual(Apply(Operators.Kronecker, Symbol("i"), Symbol("j")), P("δ(i, j)"));
        Assert.AreEqual(Apply(Operators.LeviCivita, Symbol("i"), Symbol("j"), Symbol("k")), P("ε(i, j, k)"));
        Assert.AreEqual(Apply(Operators.Identity, Number(3)), P("I(3)"));
        Assert.AreEqual(Apply(Operators.Expect, Symbol("X")), P("E(X)"));
        Assert.AreEqual(Symbol("E"), P("E"));
        Assert.AreEqual(Apply(Operators.Heaviside, X), P("u(x)"));
        Assert.AreEqual(Apply(Operators.N, Number(2)), P("N(2)"));
        Assert.AreEqual(Mul(Symbol("F"), X), P("F x"));
    }

    [TestMethod]
    public void IntervalsMatricesTuplesSetsAndPiecewise()
    {
        Assert.AreEqual(Interval(0, 1), P("[0, 1]"));
        Assert.AreEqual(Interval(Number(0), Number(1), true, false), P("[0, 1)"));
        Assert.AreEqual(Interval(Number(0), Number(1), false, true), P("(0, 1]"));
        Assert.AreEqual(Interval(Number(0), Number(1), false, false), P("]0, 1["));
        Assert.AreEqual(Interval(Number(0), Infinity), P("[0, oo)"));
        Assert.AreEqual(Tuple(1, 2), P("(1, 2)"));
        Assert.AreEqual(Set(1, 2, 3), P("{1, 2, 3}"));
        Assert.AreEqual(Set(), P("{}"));
        Assert.AreEqual(Matrix([Number(1), Number(2)], [Number(3), Number(4)]), P("[[1, 2], [3, 4]]"));
        Assert.AreEqual(Matrix([Number(1)], [Number(2)], [Number(3)]), P("[[1], [2], [3]]"));
        Assert.AreEqual(Matrix([Number(1)], [Number(2)], [Number(3)]), P("[1, 2, 3]"));
        Assert.AreEqual(Piecewise((X, X >= 0), (Neg(X), True)), P("Piecewise[(x, x >= 0), (-x, true)]"));
        Assert.AreEqual(new Bind(Binder.SetBuilder, [X], [Constant(ConstantId.Reals)], X > 0), P("{x in R | x > 0}"));
        Assert.AreEqual(new Bind(Binder.ImageSet, [Symbol("k")], [Constant(ConstantId.Integers)], Mul(Number(2), Symbol("k"))), P("{2k | k in Z}"));
    }

    [TestMethod]
    public void BindersAndQuantifiers()
    {
        var k = Symbol("k");
        var n = Symbol("n");
        Assert.AreEqual(Sum(Pow(k, Number(2)), k, 1, n), P("sum(k^2, k, 1, n)"));
        Assert.AreEqual(Product(k, k, 1, n), P("product(k, k, 1, n)"));
        Assert.AreEqual(Integrate(Pow(X, Number(2)), X, 0, 1), P("integrate(x^2, x, 0, 1)"));
        Assert.AreEqual(Integrate(Pow(X, Number(2)), X), P("integrate(x^2, x)"));
        Assert.AreEqual(Limit(Div(Sin(X), X), X, Number(0)), P("limit(sin(x)/x, x, 0)"));
        Assert.AreEqual(Limit(Div(Number(1), X), X, Number(0), 1), P("limit(1/x, x, 0, \"+\")"));
        Assert.AreEqual(Limit(Div(Number(1), X), X, Number(0), -1), P("limit(1/x, x, 0, \"-\")"));
        Assert.AreEqual(ForAll(X, Constant(ConstantId.Reals), Ge(Pow(X, Number(2)), Number(0))), P("forall x in R: x^2 >= 0"));
        Assert.AreEqual(Exists(X, Constant(ConstantId.Reals), Eq(Pow(X, Number(2)), Number(2))), P("exists x in R: x^2 = 2"));
        Assert.AreEqual(Lambda(X, Pow(X, Number(2))), P("x -> x^2"));
        Assert.AreEqual(new Bind(Binder.Lambda, [X, Y], [], X + Y), P("(x, y) -> x + y"));
        var nested = (Bind)P("forall x in R: exists y in R: y > x");
        Assert.AreEqual(Binder.Exists, ((Bind)nested.Body).Binder);
        Assert.AreEqual(Binder.ExistsUnique, ((Bind)P("exists! x in R: x^3 = 8")).Binder);
        var multiple = (Bind)P("integrate(f(x, y), (x, 0, 1), (y, 0, x))");
        Assert.AreEqual(2, multiple.Bound.Length);
        Assert.AreEqual(4, multiple.Data.Length);

        // Bound variables are local: renaming the variable gives an equal expression.
        Assert.AreEqual(P("sum(k, k, 1, n)"), P("sum(j, j, 1, n)"));
    }

    [TestMethod]
    public void CongruenceAndDivisibility()
    {
        Assert.AreEqual(Apply(Operators.Congruent, Symbol("a"), Symbol("b"), Symbol("n")), P("a ≡ b (mod n)"));
        Assert.AreEqual(Apply(Operators.Divides, Number(3), Symbol("n")), P("3 | n"));
        Assert.AreEqual(Apply(Operators.Mod, Symbol("a"), Symbol("n")), P("a mod n"));
        Assert.AreEqual(Or(Symbol("p"), Symbol("q")), P("p || q"));
        Assert.AreEqual(And(Symbol("p"), Symbol("q")), P("p && q"));
        Assert.AreEqual(Abs(Add(X, Number(1))), P("|x + 1|"));
    }

    [TestMethod]
    public void WildsAreRecognizedWhenEnabled()
    {
        var options = new ParserOptions { AllowWilds = true };
        Assert.AreEqual(Wild("a"), P("a_", options));
        Assert.AreEqual(Add(Wild("a"), Wild("b")), P("a_ + b_", options));
        Assert.AreEqual(Symbol("a_x"), P("a_x"));
    }

    // ----- Errors -----

    [TestMethod]
    [DataRow("", 1, "Expected an expression")]
    [DataRow("2 +", 4, "Expected an expression")]
    [DataRow("2 + * 3", 5, "Unexpected '*'")]
    [DataRow("(1 + 2", 7, "Expected ')'")]
    [DataRow("1 + 2)", 6, "Unexpected ')'")]
    [DataRow("2 3", 3, "Unexpected number '3'")]
    [DataRow("x y z )", 7, "Unexpected ')'")]
    [DataRow("sin", 4, "needs")]
    [DataRow("sin(", 5, "Expected an expression")]
    [DataRow("sin(1, 2)", 1, "takes")]
    [DataRow("[[1, 2], [3]]", 10, "equal length")]
    [DataRow("{x in R | }", 11, "Unexpected '}'")]
    [DataRow("a $ b", 3, "Unexpected character '$'")]
    [DataRow("sum(k, k, 1)", 1, "takes 4 arguments")]
    [DataRow("sum(k, 2, 1, n)", 1, "expects a variable")]
    [DataRow("|x", 3, "Expected '|'")]
    [DataRow("x ≡ y", 6, "Expected '(mod n)'")]
    [DataRow("\"abc\"", 1, "Unexpected string")]
    [DataRow("1 + \"a", 5, "Unterminated string")]
    [DataRow("f(x", 4, "Expected ')' to close")]
    [DataRow("forall x in R x > 0", 17, "Expected ':'")]
    [DataRow("(1, 2", 6, "Expected ')'")]
    [DataRow("x -> ", 6, "Expected an expression")]
    [DataRow("1 -> 2", 3, "must be a variable")]
    [DataRow("x^", 3, "Expected an expression")]
    [DataRow("()", 1, "Empty parentheses")]
    public void ErrorsPointToTheRightColumn(string text, int column, string message)
    {
        var result = Parser.Parse(text);
        Assert.IsFalse(result.Success, $"'{text}' should not parse");
        var error = result.Errors.Single();
        Assert.AreEqual(column, error.Column, $"{error}");
        StringAssert.Contains(error.Message, message);
        Assert.IsNull(result.Expr);
        Assert.IsTrue(error.Span.Start >= 0 && error.Span.End <= text.Length);
    }

    [TestMethod]
    [DataRow("1e999999999", 1, 11)]
    [DataRow("7e1000000", 1, 9)]
    [DataRow("1e-999999999", 1, 12)]
    [DataRow("1e100001", 1, 8)]
    [DataRow("1E-100001", 1, 9)]
    [DataRow("4e2147483648", 1, 12)]
    [DataRow("1e99999999999", 1, 13)]
    [DataRow("x + 1e999999999", 5, 11)]
    [DataRow("2 * 3.25e999999999 + y", 5, 14)]
    [DataRow("sin(x) + .5e999999999", 10, 12)]
    public void OutOfRangeExponentLiteralsAreRejectedNotParsedAsZero(string text, int column, int length)
    {
        var result = Parser.Parse(text);
        Assert.IsFalse(result.Success, $"'{text}' parsed as {result.Expr}");
        Assert.IsNull(result.Expr);
        var error = result.Errors.Single();
        Assert.AreEqual(column, error.Column, $"{error}");
        Assert.AreEqual(length, error.Span.Length, $"{error}");
        StringAssert.Contains(error.Message, "exponent");
        StringAssert.Contains(error.Message, "100000");
        Assert.IsInstanceOfType<ParseException>(Assert.Throws<ParseException>(() => Expr.Parse(text)));
    }

    [TestMethod]
    public void ExponentLiteralsWithinTheLimitKeepTheirExactValue()
    {
        Assert.AreEqual(BigRational.Parse("100000"), ((Number)P("1e5")).Value);
        Assert.AreEqual(BigRational.Parse("3/2000"), ((Number)P("1.5e-3")).Value);
        Assert.AreEqual(System.Numerics.BigInteger.Pow(10, 400), ((Number)P("1e400")).Value.Numerator);

        // The limit itself is accepted in both directions.
        var largest = (Number)P("1e100000");
        Assert.AreEqual(System.Numerics.BigInteger.Pow(10, 100_000), largest.Value.Numerator);
        Assert.AreEqual(System.Numerics.BigInteger.One, largest.Value.Denominator);
        var smallest = (Number)P("1e-100000");
        Assert.AreEqual(System.Numerics.BigInteger.One, smallest.Value.Numerator);
        Assert.AreEqual(System.Numerics.BigInteger.Pow(10, 100_000), smallest.Value.Denominator);

        // Zero stays zero whatever the exponent says only when it is in range.
        Assert.AreEqual(BigRational.Zero, ((Number)P("0e5")).Value);
        Assert.IsFalse(Parser.Parse("0e100001").Success);
    }

    [TestMethod]
    public void ErrorsCarryExpectedTokensAndSuggestions()
    {
        var closing = Parser.Parse("(1 + 2").Errors[0];
        CollectionAssert.Contains(closing.Expected.ToArray(), "')'");
        Assert.AreEqual("Add a closing ')'.", closing.Suggestion);
        Assert.AreEqual("Add a closing ']'.", Parser.Parse("[1, 2, 3").Errors[0].Suggestion);
        Assert.IsTrue(Parser.Parse("2 +").Errors[0].Expected.Length > 0);

        // A user-defined function that is one edit away from a built-in one gets a hint.
        var warning = Parser.Parse("sqr(x)").Warnings.Single();
        Assert.AreEqual("UnknownFunction", warning.Code);
        StringAssert.Contains(warning.Message, "sqrt");
        Assert.AreEqual(0, Parser.Parse("area(x)").Warnings.Length);

        var thrown = Assert.Throws<ParseException>(() => Expr.Parse("2 + * 3"));
        Assert.AreEqual(5, thrown.Error.Column);
        Assert.AreEqual("2 + * 3", thrown.Text);
        Assert.IsInstanceOfType<FormatException>(thrown);
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(Parser.Parse("2 +").ToOutcome());
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(Parser.Parse("2 + 2").ToOutcome());
        Assert.AreEqual(MathErrorKind.Syntax, ((Outcome<Expr>.Failed)Parser.Parse("(").ToOutcome()).Error.Kind);
    }

    [TestMethod]
    public void DeepNestingIsReportedNotCrashed()
    {
        var deep = new string('(', 5_000) + "x" + new string(')', 5_000);
        var result = Parser.Parse(deep);
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Errors[0].Message, "nested too deeply");

        var power = string.Join("^", Enumerable.Repeat("x", 3_000));
        Assert.IsFalse(Parser.Parse(power).Success);
        Assert.IsTrue(Parser.Parse(string.Join(" + ", Enumerable.Repeat("x", 2_000))).Success);
    }

    // ----- Fuzzing: a tree or a ParseError, never an exception or a hang -----

    [TestMethod]
    public void FuzzRandomTokenStreamsNeverThrowOrHang()
    {
        string[] pieces =
        [
            "x", "y", "2", "3.5", "0.1", "sin", "cos", "ln", "sqrt", "log_2", "e", "pi", "I", "oo", "(", ")", "[", "]", "{", "}", ",", ":", "|", "^", "*", "/", "+", "-",
            "=", "<", ">", "<=", "!=", "~=", "in", "and", "or", "not", "=>", "<=>", "->", "!", "'", "sum", "integrate", "limit", "forall", "exists", "d/dx", "dy/dx", "√", "∑",
            "²", "·", "≤", "∈", "∪", "∩", "mod", "...", "\"+\"", "f", "g'", "A", "B", "δ", "ε", " ", "  ", "R", "N", "C", "P", "‖", "⌊", "⌋", "∂", "_", "a_", "1e5", "2e", ".", ";",
        ];
        var gen = new Gen(Seed);
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 20_000; i++)
        {
            var text = string.Concat(Enumerable.Range(0, gen.Random.Next(1, 14)).Select(_ => pieces[gen.Random.Next(pieces.Length)] + (gen.Random.Next(3) == 0 ? " " : string.Empty)));
            AssertParsesOrReportsAnError(text, $"seed={Seed} case={i}");
        }
        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(60), $"token fuzz took {clock.Elapsed}");
    }

    [TestMethod]
    public void FuzzMutatedCorpusInputsNeverThrowOrHang()
    {
        var corpus = Corpus.Lines();
        var gen = new Gen(Seed + 1);
        const string noise = "()[]{}|^*/+-=<>!',:;_\"\\ 0123456789xyz√∑∫∂¬∧∨≤≥";
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 20_000; i++)
        {
            var chars = corpus[gen.Random.Next(corpus.Count)].ToList();
            for (var m = gen.Random.Next(1, 4); m > 0 && chars.Count > 0; m--)
            {
                var at = gen.Random.Next(chars.Count);
                switch (gen.Random.Next(4))
                {
                    case 0: chars.RemoveAt(at); break;
                    case 1: chars.Insert(at, noise[gen.Random.Next(noise.Length)]); break;
                    case 2: chars[at] = noise[gen.Random.Next(noise.Length)]; break;
                    default: chars.InsertRange(at, corpus[gen.Random.Next(corpus.Count)].Take(gen.Random.Next(1, 5))); break;
                }
            }
            AssertParsesOrReportsAnError(new string([.. chars]), $"seed={Seed + 1} case={i}");
        }
        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(60), $"mutation fuzz took {clock.Elapsed}");
    }

    private static void AssertParsesOrReportsAnError(string text, string context)
    {
        ParseResult result;
        try
        {
            result = Parser.Parse(text);
        }
        catch (Exception ex)
        {
            Assert.Fail($"{context}: parsing '{text}' threw {ex.GetType().Name}: {ex.Message}");
            return;
        }
        if (result.Success)
        {
            // Whatever parses must print and re-parse to the same tree, so the grammar and printer cannot disagree silently.
            var printed = result.Expr!.ToString();
            var again = Parser.Parse(printed);
            Assert.IsTrue(again.Success, $"{context}: '{text}' printed as '{printed}' which does not parse: {(again.Success ? string.Empty : again.Errors[0])}");
            Assert.AreEqual(result.Expr, again.Expr, $"{context}: '{text}' printed as '{printed}'");
        }
        else
        {
            var error = result.Errors.Single();
            Assert.IsTrue(error.Span.Start >= 0 && error.Span.Start <= text.Length && error.Span.End <= text.Length, $"{context}: bad span {error.Span} for '{text}'");
            Assert.IsFalse(string.IsNullOrEmpty(error.Message));
        }
    }

    [TestMethod]
    public void DeclaredFunctionSymbolsShadowOperatorNames()
    {
        // u(x) is the Heaviside step by default, but a declared function symbol u is a call of that symbol.
        Assert.AreEqual("heaviside", ((Apply)P("u(x)")).Operator.Id);
        var declared = new ParserOptions { Declarations = new Dictionary<string, Sort> { ["u"] = Sort.FunctionOf(Sort.Real, Sort.Real) } };
        var call = (Apply)P("u(x)", declared);
        Assert.AreEqual("call", call.Operator.Id);
        Assert.AreEqual("u", ((Symbol)call.Arguments[0]).Name);
    }
}
