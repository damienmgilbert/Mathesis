using Mathesis.Symbolics;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class SortTests
{
    private static readonly Symbol X = Symbol("x");
    private static readonly Symbol N = Symbol("n", Sort.Natural);
    private static readonly Symbol A = Symbol("A", Sort.MatrixOf(2, 3, Sort.Real));
    private static readonly Symbol B = Symbol("B", Sort.MatrixOf(3, 2, Sort.Real));
    private static readonly Symbol V = Symbol("v", Sort.VectorOf(Number(3), Sort.Real));

    [TestMethod]
    public void NumberSortsFormTheDocumentedChain()
    {
        Assert.IsTrue(Sort.Natural.IsSubsortOf(Sort.Integer));
        Assert.IsTrue(Sort.Integer.IsSubsortOf(Sort.Rational));
        Assert.IsTrue(Sort.Rational.IsSubsortOf(Sort.Real));
        Assert.IsTrue(Sort.Real.IsSubsortOf(Sort.Complex));
        Assert.IsTrue(Sort.Rational.IsSubsortOf(Sort.Algebraic));
        Assert.IsTrue(Sort.Algebraic.IsSubsortOf(Sort.Complex));
        Assert.IsTrue(Sort.Real.IsSubsortOf(Sort.ExtendedReal));
        Assert.IsTrue(Sort.Natural.IsSubsortOf(Sort.Number));
        Assert.IsTrue(Sort.Boolean.IsSubsortOf(Sort.Any));
        Assert.IsFalse(Sort.Real.IsSubsortOf(Sort.Rational));
        Assert.IsFalse(Sort.Complex.IsSubsortOf(Sort.Real));
        Assert.IsFalse(Sort.Boolean.IsSubsortOf(Sort.Real));
        Assert.IsFalse(Sort.Algebraic.IsSubsortOf(Sort.Real));
        Assert.IsTrue(Sort.Natural.IsNumeric && Sort.Complex.IsNumeric && Sort.ExtendedReal.IsNumeric);
        Assert.IsFalse(Sort.Boolean.IsNumeric);
        Assert.IsTrue(Sort.Integer.IsRealValued && !Sort.Complex.IsRealValued);
    }

    [TestMethod]
    public void JoinFindsTheSmallestCommonSort()
    {
        Assert.AreEqual(Sort.Real, Sort.Join(Sort.Natural, Sort.Real));
        Assert.AreEqual(Sort.Rational, Sort.Join(Sort.Integer, Sort.Rational));
        Assert.AreEqual(Sort.Complex, Sort.Join(Sort.Algebraic, Sort.Real));
        Assert.AreEqual(Sort.Number, Sort.Join(Sort.ExtendedReal, Sort.Complex));
        Assert.AreEqual(Sort.Any, Sort.Join(Sort.Boolean, Sort.Real));
        Assert.AreEqual(Sort.SetOf(Sort.Real), Sort.Join(Sort.SetOf(Sort.Integer), Sort.SetOf(Sort.Real)));
        Assert.AreEqual(Sort.MatrixOf(2, 2, Sort.Real), Sort.Join(Sort.MatrixOf(2, 2, Sort.Integer), Sort.MatrixOf(2, 2, Sort.Real)));
        Assert.AreEqual(Sort.Any, Sort.Join(Sort.MatrixOf(2, 2, Sort.Integer), Sort.MatrixOf(2, 3, Sort.Integer)));
        Assert.AreEqual(Sort.Number, Sort.Join(Sort.ResidueOf(12), Sort.Real));
        Assert.AreEqual(Sort.TupleOf(Sort.Real, Sort.Integer), Sort.Join(Sort.TupleOf(Sort.Integer, Sort.Integer), Sort.TupleOf(Sort.Real, Sort.Natural)));
    }

    [TestMethod]
    public void SortsCompareAndPrintByValue()
    {
        Assert.AreEqual(Sort.MatrixOf(2, 3, Sort.Real), Sort.MatrixOf(Number(2), Number(3), Sort.Real));
        Assert.AreNotEqual(Sort.MatrixOf(2, 3, Sort.Real), Sort.MatrixOf(3, 2, Sort.Real));
        Assert.AreEqual(Sort.TupleOf(Sort.Real, Sort.Integer), Sort.TupleOf(Sort.Real, Sort.Integer));
        Assert.AreEqual(Sort.TupleOf(Sort.Real, Sort.Integer).GetHashCode(), Sort.TupleOf(Sort.Real, Sort.Integer).GetHashCode());
        Assert.AreEqual("Matrix(2, 3, Real)", Sort.MatrixOf(2, 3, Sort.Real).ToString());
        Assert.AreEqual("Function(Real -> Boolean)", Sort.FunctionOf(Sort.Real, Sort.Boolean).ToString());
        Assert.AreEqual("Residue(12)", Sort.ResidueOf(12).ToString());
        Assert.AreEqual("Set(Integer)", Sort.SetOf(Sort.Integer).ToString());
        Assert.AreEqual(Sort.FunctionOf(Sort.Natural, Sort.Real), Sort.SequenceOf(Sort.Real));
        Assert.AreEqual(Sort.MatrixOf(Symbol("m"), Symbol("n"), Sort.Real), Sort.MatrixOf(Symbol("m"), Symbol("n"), Sort.Real));
    }

    [TestMethod]
    public void LiteralsAndSymbolsHaveTheirSorts()
    {
        Assert.AreEqual(Sort.Natural, Number(3).Sort);
        Assert.AreEqual(Sort.Natural, Number(0).Sort);
        Assert.AreEqual(Sort.Integer, Number(-3).Sort);
        Assert.AreEqual(Sort.Rational, Rational(1, 2).Sort);
        Assert.AreEqual(Sort.Real, Float(2.5).Sort);
        Assert.AreEqual(Sort.Real, X.Sort);
        Assert.AreEqual(Sort.Natural, N.Sort);
        Assert.AreEqual(Sort.MatrixOf(2, 3, Sort.Real), A.Sort);
        Assert.AreEqual(Sort.Boolean, (X > 0).Sort);
        Assert.AreEqual(Sort.SetOf(Sort.Natural), Set(1, 2, 3).Sort);
        Assert.AreEqual(Sort.SetOf(Sort.Real), Interval(0, 1).Sort);
        Assert.AreEqual(Sort.TupleOf(Sort.Natural, Sort.Rational), Tuple(1, Rational(1, 2)).Sort);
        Assert.AreEqual(Sort.MatrixOf(1, 2, Sort.Natural), Matrix([Number(1), Number(2)]).Sort);
    }

    [TestMethod]
    public void ArithmeticJoinsNumberSorts()
    {
        Assert.AreEqual(Sort.Natural, (N + 1).Sort);
        Assert.AreEqual(Sort.Rational, (N + Rational(1, 2)).Sort);
        Assert.AreEqual(Sort.Real, (N * X).Sort);
        Assert.AreEqual(Sort.Integer, (N - 1).Sort);
        Assert.AreEqual(Sort.Integer, (-N).Sort);
        Assert.AreEqual(Sort.Rational, (N / 2).Sort);
        Assert.AreEqual(Sort.Natural, Pow(N, 2).Sort);
        Assert.AreEqual(Sort.Rational, Pow(N, -1).Sort);
        Assert.AreEqual(Sort.Real, Pow(X, Rational(1, 2)).Sort);
        Assert.AreEqual(Sort.Real, Sin(N).Sort);
        Assert.AreEqual(Sort.Complex, Sin(I).Sort);
        Assert.AreEqual(Sort.Natural, Abs(Number(-3)).Sort);
        Assert.AreEqual(Sort.Integer, Floor(X).Sort);
        Assert.AreEqual(Sort.Real, Max(N, X).Sort);
    }

    [TestMethod]
    public void MatrixAndVectorShapesAreChecked()
    {
        Assert.AreEqual(Sort.MatrixOf(2, 2, Sort.Real), (A * B).Sort);
        Assert.AreEqual(Sort.MatrixOf(3, 3, Sort.Real), (B * A).Sort);
        Assert.AreEqual(Sort.MatrixOf(2, 3, Sort.Real), (A + A).Sort);
        Assert.AreEqual(Sort.MatrixOf(2, 3, Sort.Real), (2 * A).Sort);
        Assert.AreEqual(Sort.MatrixOf(3, 2, Sort.Real), Transpose(A).Sort);
        Assert.AreEqual(Sort.VectorOf(Number(2), Sort.Real), (A * V).Sort);
        Assert.AreEqual(Sort.Real, Det(A * B).Sort);

        var mismatch = SortChecker.Check(A + 1);
        Assert.IsFalse(mismatch.IsValid);
        Assert.AreEqual(MathErrorKind.SortMismatch, mismatch.Issues[0].Error.Kind);
        Assert.AreEqual(ExprPath.Root, mismatch.Issues[0].Path);
        StringAssert.Contains(mismatch.Issues[0].Error.Message, "add");
        Assert.AreEqual(Sort.Any, mismatch.Sort);

        Assert.IsFalse(SortChecker.Check(A * A).IsValid);
        Assert.IsFalse(SortChecker.Check(A + B).IsValid);
        Assert.IsFalse(SortChecker.Check(Det(A)).IsValid);
        Assert.IsFalse(SortChecker.Check(Pow(A, 2)).IsValid);
    }

    [TestMethod]
    public void MismatchesReportThePathOfTheOffendingNode()
    {
        // The bad sum sits at child 1 of the outer multiplication.
        Expr e = X * (A + 1);
        var result = SortChecker.Check(e);
        Assert.AreEqual(1, result.Issues.Length);
        Assert.AreEqual(new ExprPath([1]), result.Issues[0].Path);
        Assert.AreEqual(A + 1, e.At(result.Issues[0].Path));
    }

    [TestMethod]
    public void LogicRelationsAndPiecewiseNeedBooleans()
    {
        Assert.IsTrue(SortChecker.Check(And(X > 0, X < 1)).IsValid);
        Assert.IsFalse(SortChecker.Check(And(X, X > 0)).IsValid);
        Assert.IsFalse(SortChecker.Check(Not(X)).IsValid);
        Assert.IsTrue(SortChecker.Check(Piecewise((X, X >= 0), (-X, True))).IsValid);
        var bad = SortChecker.Check(Piecewise((X, X + 1)));
        Assert.IsFalse(bad.IsValid);
        Assert.AreEqual(new ExprPath([1]), bad.Issues[0].Path);
    }

    [TestMethod]
    public void FunctionSortsApplyAndBindersInferTheirSorts()
    {
        var f = Symbol("f", Sort.FunctionOf(Sort.Real, Sort.Boolean));
        Assert.AreEqual(Sort.Boolean, Call(f, X).Sort);
        Assert.IsFalse(SortChecker.Check(Call(X, X)).IsValid);
        Assert.AreEqual(Sort.Boolean, ForAll(X, Constant(ConstantId.Reals), X > 0).Sort);
        Assert.AreEqual(Sort.FunctionOf(Sort.Real, Sort.Real), Lambda(X, X * X).Sort);
        Assert.AreEqual(Sort.Natural, Sum(N, N, 1, 5).Sort);
        Assert.AreEqual(Sort.SetOf(Sort.Real), new Bind(Binder.SetBuilder, [X], [Constant(ConstantId.Reals)], X > 0).Sort);
        Assert.AreEqual(Sort.SetOf(Sort.Integer), Constants.Info(ConstantId.Integers).Sort);
        Assert.AreEqual(Sort.Any, Wild("a").Sort);
    }

    [TestMethod]
    public void SortsAreCachedAndConsistent()
    {
        Expr e = A * B;
        var first = e.Sort;
        Assert.AreSame(first, e.Sort);
        Assert.AreEqual(SortChecker.Check(e).Sort, e.Sort);
    }
}
