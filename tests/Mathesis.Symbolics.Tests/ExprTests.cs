using System.Collections.Immutable;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class ExprTests
{
    private static readonly Symbol X = Symbol("x");
    private static readonly Symbol Y = Symbol("y");
    private static readonly Symbol K = Symbol("k");
    private static readonly Symbol J = Symbol("j");

    [TestMethod]
    public void AllTwelveNodeKindsExistAndReportTheirKind()
    {
        var nodes = new (Expr Node, ExprKind Kind)[]
        {
            (Number(3), ExprKind.Number),
            (Float(2.5), ExprKind.Float),
            (X, ExprKind.Symbol),
            (Pi, ExprKind.Constant),
            (Add(X, Y), ExprKind.Apply),
            (Sum(X, K, 1, 3), ExprKind.Bind),
            (Matrix([X, Y], [Number(1), Number(2)]), ExprKind.Matrix),
            (Set(1, 2), ExprKind.Set),
            (Interval(0, 1), ExprKind.Interval),
            (Tuple(1, 2), ExprKind.Tuple),
            (Piecewise((X, X >= 0), (Neg(X), True)), ExprKind.Piecewise),
            (Wild("a"), ExprKind.Wild),
        };
        Assert.AreEqual(12, nodes.Select(n => n.Kind).Distinct().Count());
        foreach (var (node, kind) in nodes) Assert.AreEqual(kind, node.Kind);
    }

    [TestMethod]
    public void EqualityIsStructuralAndSymbolsCompareByNameAndSort()
    {
        Assert.AreEqual(Add(X, Number(1)), Add(Symbol("x"), Number(1)));
        Assert.AreEqual(Add(X, Number(1)).GetHashCode(), Add(Symbol("x"), Number(1)).GetHashCode());
        Assert.AreNotEqual(Add(X, Number(1)), Add(Number(1), X));
        Assert.AreNotEqual<Expr>(Symbol("x"), Symbol("x", Sort.Integer));
        Assert.IsTrue(Symbol("x") != Symbol("y"));
        Assert.IsFalse(Symbol("x") == Symbol("y"));

        // Display hints never affect identity: 0.25 and 1/4 are the same number.
        Assert.AreEqual<Expr>(Decimal("0.25"), Number(BigRational.Create(1, 4)));
        Assert.AreEqual(Decimal("0.25").StructuralHash, Number(BigRational.Create(1, 4)).StructuralHash);

        // Floats are not numbers: 0.5 (a double) differs from the exact 1/2.
        Assert.AreNotEqual<Expr>(Float(0.5), Number(BigRational.Create(1, 2)));
        Assert.AreEqual<Expr>(Float(0.5), Float(0.5));
        Assert.AreNotEqual<Expr>(Float(0.5), new Float(0.5, 24));

        // == on expressions is structural; Eq builds the equation.
        Assert.IsTrue(X == Symbol("x"));
        Assert.AreEqual("eq", ((Apply)Eq(X, Y)).Operator.Id);
    }

    [TestMethod]
    public void BoundSymbolsAreLocalSoAlphaEquivalentBindsAreEqual()
    {
        var sumK = Sum(K * K, K, 1, 10);
        var sumJ = Sum(J * J, J, 1, 10);
        Assert.AreEqual(sumK, sumJ);
        Assert.AreEqual(sumK.StructuralHash, sumJ.StructuralHash);

        // A free symbol with the same name as another binder's variable is not confused with it.
        Assert.AreNotEqual(Sum(K * Y, K, 1, 10), Sum(K * K, K, 1, 10));
        Assert.AreNotEqual(Sum(X * J, J, 1, 10), Sum(X * X, X, 1, 10));

        // Nested binders: the inner variable shadows the outer one consistently.
        Expr nestedA = Sum(Sum(K * J, J, 1, 3), K, 1, 3);
        Expr nestedB = Sum(Sum(J * K, K, 1, 3), J, 1, 3);
        Assert.AreEqual(nestedA, nestedB);
        Assert.AreNotEqual(nestedA, Sum(Sum(K * K, J, 1, 3), K, 1, 3));

        // Bounds are outside the binder's scope.
        Assert.AreEqual(Sum(K, K, 1, K), Sum(J, J, 1, K));
        Assert.AreNotEqual(Sum(K, K, 1, K), Sum(K, K, 1, J));

        // Quantifiers and lambdas are α-invariant too.
        Assert.AreEqual(ForAll(X, Reals(), X * X >= 0), ForAll(Y, Reals(), Y * Y >= 0));
        Assert.AreEqual(Lambda(X, X + 1), Lambda(Y, Y + 1));
    }

    private static Expr Reals() => Constant(ConstantId.Reals);

    [TestMethod]
    public void FreeSymbolsExcludeBoundOnes()
    {
        CollectionAssert.AreEquivalent(new[] { "x", "y" }, Add(X, Y).FreeSymbols.Select(s => s.Name).ToArray());
        CollectionAssert.AreEquivalent(new[] { "n" }, Sum(K * K, K, 1, Symbol("n")).FreeSymbols.Select(s => s.Name).ToArray());
        Assert.AreEqual(0, Lambda(X, X * X).FreeSymbols.Count);
        CollectionAssert.AreEquivalent(new[] { "y" }, Lambda(X, X * Y).FreeSymbols.Select(s => s.Name).ToArray());
        Assert.AreEqual(0, Number(5).FreeSymbols.Count);
    }

    [TestMethod]
    public void CachedMetricsAndFamilies()
    {
        var e = Sin(X) * Exp(Y) + Integrate(X, X);
        Assert.AreEqual(4, e.LeafCount);
        Assert.AreEqual(4, e.Depth);
        Assert.IsTrue(e.Families.HasFlag(OperatorFamily.Trig));
        Assert.IsTrue(e.Families.HasFlag(OperatorFamily.ExpLog));
        Assert.IsTrue(e.Families.HasFlag(OperatorFamily.Calculus));
        Assert.IsFalse(e.Families.HasFlag(OperatorFamily.Logic));
        Assert.IsTrue(Matrix([X]).Families.HasFlag(OperatorFamily.LinearAlgebra));
        Assert.IsTrue(ForAll(X, Reals(), X > 0).Families.HasFlag(OperatorFamily.Logic));
        Assert.AreEqual(OperatorFamily.None, Number(1).Families);
        Assert.AreEqual(1, X.LeafCount);
        Assert.AreEqual(1, X.Depth);
    }

    [TestMethod]
    public void PathsAddressAndReplaceSubexpressions()
    {
        Expr e = Add(Mul(Number(2), X), Sin(Y));
        Assert.AreEqual(Mul(Number(2), X), e.At(new ExprPath([0])));
        Assert.AreEqual(Y, e.At(new ExprPath([1, 0])));
        Assert.AreEqual(e, e.At(ExprPath.Root));
        var replaced = e.ReplaceAt(new ExprPath([1, 0]), Number(0));
        Assert.AreEqual(Add(Mul(Number(2), X), Sin(Number(0))), replaced);
        Assert.AreEqual("/0/1", ExprPath.Root.Child(0).Child(1).ToString());
        Assert.AreEqual("/", ExprPath.Root.ToString());
        Assert.AreEqual(new ExprPath([0]), new ExprPath([0, 1]).Parent);
        Assert.Throws<ArgumentException>(() => e.At(new ExprPath([5])));
        Assert.Throws<InvalidOperationException>(() => _ = ExprPath.Root.Parent);

        // Binder children: the data first, then the body; matrix entries row-major; piecewise value/condition pairs.
        var sum = Sum(K, K, 1, 5);
        Assert.AreEqual(Number(1), sum.At(new ExprPath([0])));
        Assert.AreEqual(K, sum.At(new ExprPath([2])));
        Assert.AreEqual(Y, Matrix([X, Y], [Number(1), Number(2)]).At(new ExprPath([1])));

        // Walk visits every node once with its path.
        foreach (var (node, path) in e.Walk()) Assert.AreEqual(node, e.At(path));
        Assert.AreEqual(6, e.Walk().Count());
    }

    [TestMethod]
    public void TransformSubstituteAndContains()
    {
        Expr e = Add(Mul(X, X), Sin(X));
        Assert.AreEqual(Add(Mul(Y, Y), Sin(Y)), e.Substitute(X, Y));
        Assert.AreEqual(Add(Mul(Number(2), Number(2)), Sin(Number(2))), e.Substitute(X, Number(2)));
        Assert.IsTrue(e.Contains(Sin(X)));
        Assert.IsFalse(e.Contains(Cos(X)));

        // Substitution respects binders: the bound x is untouched, the bounds are substituted.
        var sum = Sum(X * Y, X, 1, X);
        Assert.AreEqual(Sum(X * Y, X, 1, Number(7)), sum.Substitute(X, Number(7)));
        Assert.AreEqual(Sum(X * Number(3), X, 1, X), sum.Substitute(Y, Number(3)));

        // Transform is bottom-up: replace every symbol by its square, once.
        var squared = e.Transform(n => n is Symbol s ? s * s : null);
        Assert.AreEqual(Add(Mul(Mul(X, X), Mul(X, X)), Sin(Mul(X, X))), squared);
        Assert.AreSame(e, e.Transform(_ => null));
    }

    [TestMethod]
    public void OperatorOverloadsBuildRawTrees()
    {
        Assert.AreEqual(Add(X, Y), X + Y);
        Assert.AreEqual(Sub(X, Y), X - Y);
        Assert.AreEqual(Mul(X, Y), X * Y);
        Assert.AreEqual(Div(X, Y), X / Y);
        Assert.AreEqual(Neg(X), -X);
        Assert.AreEqual(Lt(X, Y), X < Y);
        Assert.AreEqual(Gt(X, Y), X > Y);
        Assert.AreEqual(Le(X, Y), X <= Y);
        Assert.AreEqual(Ge(X, Y), X >= Y);
        Assert.AreEqual(And(X > 0, Y > 0), (X > 0) & (Y > 0));
        Assert.AreEqual(Or(X > 0, Y > 0), (X > 0) | (Y > 0));
        Assert.AreEqual(Not(X > 0), !(X > 0));

        // Left-nested like the parser: x + y + z is add(add(x, y), z).
        Assert.AreEqual(Add(Add(X, Y), Symbol("z")), X + Y + Symbol("z"));

        // Implicit conversions: ints are exact, doubles are floats.
        Expr two = 2;
        Expr half = 0.5;
        Assert.AreEqual(Number(2), two);
        Assert.IsInstanceOfType<Float>(half);
        Assert.AreEqual(Add(Mul(Number(2), X), Number(1)), 2 * X + 1);
    }

    [TestMethod]
    public void NodeConstructorsValidateTheirInput()
    {
        Assert.Throws<ArgumentException>(() => new Symbol(""));
        Assert.Throws<ArgumentException>(() => new Symbol("1x"));
        Assert.Throws<ArgumentException>(() => new Symbol("x y"));
        Assert.Throws<ArgumentException>(() => new Symbol("x+y"));
        Assert.IsTrue(Symbol.IsValidName("θ"));
        Assert.IsTrue(Symbol.IsValidName("x_1"));
        Assert.IsTrue(Symbol.IsValidName("x'"));
        Assert.IsTrue(Symbol.IsValidName("x₁"));
        Assert.IsTrue(Symbol.IsValidName("_tmp"));
        Assert.IsFalse(Symbol.IsValidName("'x"));

        Assert.Throws<ArgumentException>(() => Apply(Operators.Sin));
        Assert.Throws<ArgumentException>(() => Apply(Operators.Sin, X, Y));
        Assert.Throws<ArgumentException>(() => Apply(Operators.Add, X));
        Assert.Throws<ArgumentException>(() => Fn("nosuchfunction", X));
        Assert.AreEqual(Arcsin(X), Fn("asin", X));

        Assert.Throws<ArgumentException>(() => new MatrixLiteral(2, 2, [X, Y, X]));
        Assert.Throws<ArgumentException>(() => Matrix([X, Y], [X]));
        Assert.Throws<ArgumentException>(() => new Bind(Binder.Sum, [], [], X));
        Assert.Throws<ArgumentException>(() => new Bind(Binder.Sum, [K, K], [], X));
        Assert.Throws<ArgumentException>(() => new Piecewise([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Constant((ConstantId)200));
        Assert.Throws<ArgumentException>(() => X.WithChildren([Y]));
        Assert.Throws<ArgumentException>(() => Add(X, Y).WithChildren([X]).WithChildren([X, Y, X]).WithChildren([]));
    }

    [TestMethod]
    public void SetsDeduplicateAndIntervalsKeepInfiniteEndsOpen()
    {
        Assert.AreEqual(2, ((SetLiteral)Set(1, 2, 1, 2, 1)).Elements.Length);
        Assert.AreEqual(Number(1), ((SetLiteral)Set(1, 2, 1)).Elements[0]);
        var interval = (IntervalLiteral)Interval(Number(0), Infinity, true, true);
        Assert.IsTrue(interval.LowerClosed);
        Assert.IsFalse(interval.UpperClosed);
        var both = (IntervalLiteral)Interval(NegativeInfinity, Infinity);
        Assert.IsFalse(both.LowerClosed || both.UpperClosed);
        Assert.AreNotEqual(Interval(0, 1), Interval(0, 1, true, false));
    }

    [TestMethod]
    public void OperatorRegistryIsConsistent()
    {
        Assert.IsTrue(Operators.All.Count > 170, $"{Operators.All.Count} operators");
        Assert.AreEqual(Operators.All.Count, Operators.All.Select(o => o.Id).Distinct().Count());
        foreach (var op in Operators.All)
        {
            Assert.IsTrue(Operators.TryGet(op.Id, out var found) && found == op, op.Id);
            Assert.IsTrue(Operators.TryGetByName(op.Id, out _), op.Id);
            Assert.IsTrue(op.Arity.Min >= 1, op.Id);
            Assert.IsTrue(op.Id.All(char.IsAsciiLetterOrDigit) && char.IsAsciiLetter(op.Id[0]), $"'{op.Id}' is not a plain identifier");
        }
        Assert.AreEqual(Operators.Arcsin, Operators.Get("arcsin"));
        Assert.IsTrue(Operators.TryGetByName("asin", out var asin) && asin == Operators.Arcsin);
        Assert.IsFalse(Operators.TryGet("asin", out _));
        Assert.Throws<KeyNotFoundException>(() => Operators.Get("nope"));

        // Attributes from docs/design/05: associativity, commutativity, identity and absorbing elements.
        Assert.IsTrue(Operators.Add.Has(OperatorAttributes.Associative | OperatorAttributes.Commutative));
        Assert.AreEqual(Number(0), Operators.Add.Identity);
        Assert.AreEqual(Number(1), Operators.Mul.Identity);
        Assert.AreEqual(Number(0), Operators.Mul.Absorbing);
        Assert.IsTrue(Operators.Sin.Has(OperatorAttributes.Odd));
        Assert.IsTrue(Operators.Cos.Has(OperatorAttributes.Even));
        Assert.IsTrue(Operators.Not.Has(OperatorAttributes.Involution));
        Assert.IsTrue(Operators.And.Has(OperatorAttributes.Idempotent));
        Assert.IsFalse(Operators.Pow.Has(OperatorAttributes.Commutative));
        Assert.AreEqual(Arity.Variadic(2), Operators.Add.Arity);
        Assert.IsTrue(Operators.Round.Arity.Accepts(1) && Operators.Round.Arity.Accepts(2) && !Operators.Round.Arity.Accepts(3));
    }

    [TestMethod]
    public void ConstantsHaveSortsAndNames()
    {
        Assert.AreEqual(Sort.Real, Pi.Sort);
        Assert.AreEqual(Sort.Complex, I.Sort);
        Assert.AreEqual(Sort.Boolean, True.Sort);
        Assert.AreEqual(Sort.ExtendedReal, Infinity.Sort);
        Assert.AreEqual("pi", Constants.Info(ConstantId.Pi).Text);
        Assert.AreEqual("π", Constants.Info(ConstantId.Pi).Unicode);
        Assert.IsTrue(Constants.IsSet(ConstantId.Reals));
        Assert.IsFalse(Constants.IsSet(ConstantId.Pi));
    }

    [TestMethod]
    public void WildAndMatrixNodesRebuildThemselves()
    {
        var constrained = new Wild("n", Constant(ConstantId.Integers));
        Assert.AreEqual(1, constrained.Children.Length);
        Assert.AreEqual(constrained, constrained.WithChildren(constrained.Children));
        Assert.AreEqual(Wild("n"), constrained.WithChildren([]));
        Assert.AreNotEqual(Wild("n"), constrained);

        var m = (MatrixLiteral)Matrix([X, Y], [Number(1), Number(2)]);
        Assert.AreEqual(2, m.Rows);
        Assert.AreEqual(2, m.Columns);
        Assert.AreEqual(Number(2), m[1, 1]);
        Assert.AreSame(m, m.WithChildren(m.Children));
    }
}
