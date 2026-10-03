using System.Collections.Immutable;
using Mathesis.Calculus;
using Mathesis.Explanation;
using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Solving;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Canonical;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Tests;

[TestClass]
public class CasTests
{
    private static readonly Symbol X = new("x");

    private static Expr P(string text) => Expr.Parse(text);

    private static Expr C(string text) => Normalizer.Canonical(Expr.Parse(text));

    private static Expr Value(Outcome<Expr> outcome) => ((Outcome<Expr>.Success)outcome).Value;

    private static DenseMatrix<BigRational> Matrix(int[,] entries) =>
        DenseMatrix.Create(entries.GetLength(0), entries.GetLength(1), (r, c) => new BigRational(entries[r, c]));

    [TestMethod]
    public void ParseAndCheckSorts()
    {
        Assert.AreEqual(C("x^2 + 1"), Normalizer.Canonical(((Outcome<Expr>.Success)Cas.Parse("x^2 + 1")).Value));
        Assert.IsInstanceOfType<Outcome<Expr>.Failed>(Cas.Parse("x +* 1"));
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(Cas.ParseLatex(@"\frac{x}{2}"));
        Assert.IsTrue(Cas.CheckSorts(P("x + 1")).IsValid);
    }

    [TestMethod]
    public void EvaluateAndNumericValue()
    {
        var bindings = new Dictionary<Symbol, Expr> { [X] = P("2") };
        Assert.AreEqual(C("5"), Value(Cas.Evaluate(P("x^2 + 1"), bindings)));
        var n = (Outcome<double>.Success)Cas.N(P("sqrt(2)"));
        Assert.AreEqual(Math.Sqrt(2), n.Value, 1e-15);
    }

    [TestMethod]
    public void AlgebraicTransforms()
    {
        Assert.AreEqual(C("x^2 + 2*x + 1"), Value(Cas.Expand(P("(x + 1)^2"))));
        Assert.AreEqual(C("(x + 2)*(x + 3)"), Value(Cas.Factor(P("x^2 + 5*x + 6"))));
        Assert.AreEqual(C("c + (a + b)*x"), Value(Cas.Collect(P("a*x + b*x + c"), X)));
        Assert.AreEqual(C("(1 + 2*x)/(x*(1 + x))"), Value(Cas.Together(P("1/x + 1/(x + 1)"))));
        Assert.AreEqual(C("1 + x"), Value(Cas.Cancel(P("(x^2 - 1)/(x - 1)"))));
        Assert.AreEqual(C("x + 1/(x - 1)"), Value(Cas.Apart(P("(x^3 + 1)/(x^2 - 1)"), X)));
        Assert.AreEqual(C("sqrt(2)/2"), Value(Cas.Rationalize(P("1/sqrt(2)"))));
        Assert.AreEqual(C("(x + 2)^2 + 3"), Value(Cas.CompleteSquare(P("x^2 + 4*x + 7"), X)));
        Assert.AreEqual(C("1"), Value(Cas.Simplify(P("sin(x)^2 + cos(x)^2"))));
    }

    [TestMethod]
    public void AFormThatAlreadyHoldsIsReturnedUnchanged()
    {
        var outcome = (Outcome<Expr>.Success)Cas.Expand(P("x + 1"));
        Assert.AreEqual(C("x + 1"), outcome.Value);
        Assert.AreEqual(0, ((Derivation)outcome.Steps!).Steps.Length);
    }

    [TestMethod]
    public void LogarithmAndTrigonometricForms()
    {
        var positive = new MathContext().Assume(P("x > 0")).Assume(P("y > 0"));
        Assert.AreEqual(C("ln(x*y)"), Value(Cas.LogCombine(P("ln(x) + ln(y)"), positive)));
        Assert.AreEqual(C("ln(x) + ln(y)"), Value(Cas.LogExpand(P("ln(x*y)"), positive)));
        Assert.AreEqual(C("sqrt(x*y)"), Value(Cas.PowerSimplify(P("sqrt(x)*sqrt(y)"), positive)));
        Assert.AreEqual(C("sec(x)^2"), Value(Cas.TrigSimplify(P("1 + tan(x)^2"))));
        Assert.AreEqual(C("2*sin(x)*cos(x)"), Value(Cas.TrigExpand(P("sin(2*x)"))));
        Assert.AreEqual(C("(1 - cos(2*x))/2"), Value(Cas.TrigReduce(P("sin(x)^2"))));
        var exponential = Value(Cas.TrigToExp(P("cos(x)")));
        Assert.AreEqual(C("cos(x)"), Value(Cas.Simplify(Value(Cas.ExpToTrig(exponential)))));
    }

    [TestMethod]
    public void PolynomialDivisionByLongAndSyntheticDivision()
    {
        var longDivision = (Outcome<DivisionResult>.Success)Cas.Divide(P("x^3 - 2*x^2 + x - 5"), P("x - 3"), X);
        Assert.AreEqual(C("x^2 + x + 4"), longDivision.Value.Quotient);
        Assert.AreEqual(C("7"), longDivision.Value.Remainder);
        var synthetic = (Outcome<DivisionResult>.Success)Cas.Divide(P("x^3 - 2*x^2 + x - 5"), P("x - 3"), X, DivisionStyle.Synthetic);
        Assert.AreEqual(longDivision.Value, synthetic.Value);
        Assert.AreEqual("alg.poly.synthetic-division", ((Derivation)synthetic.Steps!).Steps[0].Entry!.Value.Value);
        Assert.IsInstanceOfType<Outcome<DivisionResult>.Unevaluated>(Cas.Divide(P("x^3 + 1"), P("x^2 + 1"), X, DivisionStyle.Synthetic));
    }

    [TestMethod]
    public void ApplyAnEntryAtAPlace()
    {
        var outcome = (Outcome<Expr>.Success)Cas.Apply("trig.sum.sin-of-sum", P("sin(a + b)"));
        Assert.AreEqual(C("sin(a)*cos(b) + cos(a)*sin(b)"), outcome.Value);
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Cas.Apply("trig.sum.sin-of-sum", P("sin(a + b) + 1"), ExprPath.Root.Child(0)));
        Assert.AreEqual("trig.sum.sin-of-sum", ((Derivation)outcome.Steps!).Steps.Last().Entry!.Value.Value);
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Cas.Apply("trig.sum.sin-of-sum", P("cos(x)")));
        Assert.IsInstanceOfType<Outcome<Expr>.Unevaluated>(Cas.Apply("no.such.law", P("x")));
    }

    [TestMethod]
    public void SolvingEquationsInequalitiesAndSystems()
    {
        var set = ((Outcome<SolutionSet>.Success)Cas.Solve(P("x^2 - 5*x + 6 = 0"), X)).Value;
        Assert.AreEqual(2, set.Points.Length);
        Assert.AreEqual(SolutionKind.Intervals, ((Outcome<SolutionSet>.Success)Cas.Solve(P("x^2 - 4 > 0"), X)).Value.Kind);
        var system = ((Outcome<SolutionSet>.Success)Cas.Solve([P("x + y = 3"), P("x - y = 1")], [X, new Symbol("y")])).Value;
        Assert.AreEqual("(2, 1)", system.Points[0].ToString());
        var roots = ((Outcome<ImmutableArray<double>>.Success)Cas.NSolve(P("cos(x) = x"), X, -2, 2)).Value;
        Assert.AreEqual(0.7390851332151607, roots.Single(), 1e-12);
        var restricted = ((Outcome<ImmutableArray<double>>.Success)Cas.NSolve(P("sin(x) = 1/2"), X, 0, 2 * Math.PI)).Value;
        Assert.AreEqual(2, restricted.Length);
    }

    [TestMethod]
    public void CalculusAbilities()
    {
        Assert.AreEqual(C("2*x*cos(x^2)"), Value(Cas.Differentiate(P("sin(x^2)"), X)));
        Assert.AreEqual(C("60*x^2"), Value(Cas.Differentiate(P("x^5"), X, 3)));
        var slope = Value(Cas.ImplicitDerivative(P("x^2 + y^2 = 25"), new Symbol("y"), X));
        Assert.AreEqual(-0.75, CalculusHelpers.Eval(slope, new Dictionary<Symbol, double> { [X] = 3, [new Symbol("y")] = 4 })!.Value, 1e-12);
        Assert.IsInstanceOfType<Outcome<Expr>.Success>(Cas.Integrate(P("x*cos(x)"), X));
        var definite = Value(Cas.Integrate(P("x^2"), X, P("0"), P("3")));
        Assert.AreEqual(9, CalculusHelpers.Eval(definite, [])!.Value, 1e-12);
        var limit = (Outcome<LimitResult>.Success)Cas.Limit(P("sin(x)/x"), X, P("0"));
        Assert.AreEqual(new LimitResult.Finite(C("1")), limit.Value);
        Assert.AreEqual(C("1 + x + x^2/2 + x^3/6"), Value(Cas.Taylor(P("exp(x)"), X, P("0"), 3)));
    }

    [TestMethod]
    public void SeriesKeepsNegativePowersAtAPole()
    {
        var laurent = Value(Cas.Series(P("1/(x*(1 - x))"), X, P("0"), 2));
        Assert.AreEqual(C("1/x + 1 + x + x^2"), laurent);
    }

    [TestMethod]
    public void LinearAlgebra()
    {
        var a = Matrix(new[,] { { 2, 1 }, { 1, 3 } });
        Assert.AreEqual(new BigRational(5), Cas.Determinant(a));
        Assert.AreEqual(new BigRational(5), Cas.Determinant(a, DeterminantMethod.Cofactor));
        var big = Matrix(new[,] { { 1, 2, 3 }, { 0, 1, 4 }, { 5, 6, 0 } });
        Assert.AreEqual(Cas.Determinant(big), Cas.Determinant(big, DeterminantMethod.Cofactor));
        var inverse = ((Outcome<DenseMatrix<BigRational>>.Success)Cas.Inverse(a)).Value;
        Assert.AreEqual(new BigRational(3) / new BigRational(5), inverse[0, 0]);
        Assert.IsInstanceOfType<Outcome<DenseMatrix<BigRational>>.Failed>(Cas.Inverse(Matrix(new[,] { { 1, 2 }, { 2, 4 } })));
        Assert.AreEqual(1, Cas.Rank(Matrix(new[,] { { 1, 2 }, { 2, 4 } })));
        Assert.AreEqual(1, Cas.NullSpace(Matrix(new[,] { { 1, 2 }, { 2, 4 } })).Length);
        Assert.AreEqual(1, Cas.ColumnSpace(Matrix(new[,] { { 1, 2 }, { 2, 4 } })).Length);
        var reduction = Cas.RowReduce(Matrix(new[,] { { 1, 2, 3 }, { 4, 5, 6 } }));
        Assert.AreEqual(2, reduction.Rank);
        Assert.IsFalse(reduction.Operations.IsEmpty);
    }

    [TestMethod]
    public void EigenvaluesAndEigenvectors()
    {
        var pairs = ((Outcome<ImmutableArray<EigenPair>>.Success)Cas.Eigen(Matrix(new[,] { { 2, 0 }, { 0, 3 } }))).Value;
        Assert.AreEqual(2, pairs.Length);
        Assert.AreEqual(C("2"), pairs[0].Value);
        Assert.AreEqual(1, pairs[0].Eigenvectors.Length);
        var repeated = ((Outcome<ImmutableArray<EigenPair>>.Success)Cas.Eigen(Matrix(new[,] { { 2, 1 }, { 0, 2 } }))).Value;
        Assert.AreEqual(2, repeated.Single().Multiplicity);
        Assert.AreEqual(1, repeated.Single().Eigenvectors.Length);
        var irrational = ((Outcome<ImmutableArray<EigenPair>>.Success)Cas.Eigen(Matrix(new[,] { { 0, 1 }, { 1, 1 } }))).Value;
        Assert.AreEqual(2, irrational.Length);
        Assert.IsTrue(irrational.All(p => p.Eigenvectors.IsEmpty));
    }

    [TestMethod]
    public void StepsRenderAndCiteTheCatalog()
    {
        var outcome = (Outcome<Expr>.Success)Cas.Differentiate(P("x*exp(x)"), X);
        var markdown = outcome.Steps.Render(ExplanationFormat.Markdown, Verbosity.Standard);
        StringAssert.Contains(markdown, "Start:");
        Assert.IsTrue(((Derivation)outcome.Steps!).Steps.All(s => s.Entry is null || Cas.Get(s.Entry.Value.Value).Name.Length > 0));
        Assert.AreEqual(string.Empty, ((IDerivation?)null).Render());
    }

    [TestMethod]
    public void LookUpTheCatalog()
    {
        Assert.AreEqual("Difference of squares", Cas.Get("alg.factor.diff-squares").Name);
        Assert.IsTrue(Cas.Find("difference of squares").Any(e => e.Id.Value == "alg.factor.diff-squares"));
        Assert.IsTrue(Cas.ByDomain("trig.sum").Any());
        Assert.ThrowsExactly<KeyNotFoundException>(() => Cas.Get("no.such.entry"));
    }

    [TestMethod]
    public void TheLevelRestrictsTheMethods()
    {
        var low = new MathContext { Level = Mathesis.Knowledge.CurriculumLevel.PreAlgebra };
        var outcome = Cas.Differentiate(P("sin(x)"), X, 1, low);
        Assert.IsNotInstanceOfType<Outcome<Expr>.Success>(outcome);
    }
}
