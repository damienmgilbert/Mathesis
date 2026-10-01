namespace Mathesis.Core.Tests;

[TestClass]
public class ResultTypesTests
{
    private sealed record Fact(string Name) : IMathObject;

    private sealed record FakeDerivation : IDerivation;

    private static readonly Truth[] AllTruths = [Truth.False, Truth.True, Truth.Unknown];

    [TestMethod]
    public void KleeneTablesAreCorrect()
    {
        // Rows and columns in the order False, True, Unknown.
        Truth[,] and = { { Truth.False, Truth.False, Truth.False }, { Truth.False, Truth.True, Truth.Unknown }, { Truth.False, Truth.Unknown, Truth.Unknown } };
        Truth[,] or = { { Truth.False, Truth.True, Truth.Unknown }, { Truth.True, Truth.True, Truth.True }, { Truth.Unknown, Truth.True, Truth.Unknown } };
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                Assert.AreEqual(and[i, j], AllTruths[i].And(AllTruths[j]), $"{AllTruths[i]} and {AllTruths[j]}");
                Assert.AreEqual(or[i, j], AllTruths[i].Or(AllTruths[j]), $"{AllTruths[i]} or {AllTruths[j]}");

                // De Morgan holds in Kleene logic.
                Assert.AreEqual(AllTruths[i].And(AllTruths[j]).Not(), AllTruths[i].Not().Or(AllTruths[j].Not()));
            }
        }
        Assert.AreEqual(Truth.True, Truth.False.Not());
        Assert.AreEqual(Truth.False, Truth.True.Not());
        Assert.AreEqual(Truth.Unknown, Truth.Unknown.Not());
        Assert.AreEqual(Truth.True, TruthExtensions.FromBool(true));
        Assert.AreEqual(Truth.False, TruthExtensions.FromBool(false));
    }

    [TestMethod]
    public void ProvisosDeduplicateAndKeepOrder()
    {
        var x = new Fact("x != 0");
        var y = new Fact("y > 0");
        var provisos = Provisos.Of(x, y, x);
        Assert.AreEqual(2, provisos.Count);
        Assert.AreSame(x, provisos[0]);
        Assert.AreSame(y, provisos[1]);
        Assert.AreSame(provisos, provisos.Add(new Fact("x != 0")));
        Assert.AreEqual(3, provisos.Add(new Fact("z")).Count);
        Assert.AreEqual(2, provisos.Count);

        Assert.AreEqual(Provisos.Of(x, y), provisos);
        Assert.AreEqual(Provisos.Of(x, y).GetHashCode(), provisos.GetHashCode());
        Assert.AreNotEqual(Provisos.Of(y, x), provisos);
        Assert.AreEqual(0, Provisos.None.Count);
        Assert.AreSame(Provisos.None, Provisos.None.Union(Provisos.None));
        Assert.AreEqual(Provisos.Of(x, y, new Fact("z")), provisos.Union(Provisos.Of(y, new Fact("z"))));
        Assert.AreEqual("(none)", Provisos.None.ToString());
        Assert.Throws<ArgumentNullException>(() => provisos.Add(null!));
    }

    [TestMethod]
    public void BudgetCountsStepsAndStops()
    {
        var budget = new Budget(maxSteps: 3);
        Assert.IsFalse(budget.IsExceeded);
        Assert.IsTrue(budget.TryCharge());
        Assert.IsTrue(budget.TryCharge(2));
        Assert.AreEqual(3, budget.StepsUsed);
        Assert.IsFalse(budget.IsExceeded);
        Assert.IsFalse(budget.TryCharge());
        Assert.IsTrue(budget.IsExceeded);
        StringAssert.Contains(budget.ExceededReason, "Step limit");
        Assert.AreEqual(MathErrorKind.BudgetExceeded, budget.ToError().Kind);
    }

    [TestMethod]
    public void BudgetHonorsSizeTimeAndCancellation()
    {
        var sized = new Budget(maxSize: 10);
        Assert.IsTrue(sized.AllowsSize(10));
        Assert.IsFalse(sized.AllowsSize(11));
        Assert.IsTrue(Budget.Unlimited.AllowsSize(int.MaxValue));

        var timed = new Budget(maxTime: TimeSpan.Zero);
        Thread.Sleep(5);
        Assert.IsTrue(timed.IsExceeded);
        StringAssert.Contains(timed.ExceededReason, "Time limit");

        using var cts = new CancellationTokenSource();
        var cancellable = new Budget(cancellationToken: cts.Token);
        Assert.IsFalse(cancellable.IsExceeded);
        cts.Cancel();
        Assert.IsTrue(cancellable.IsExceeded);
        Assert.AreEqual("Cancelled.", cancellable.ExceededReason);

        Assert.IsTrue(Budget.Unlimited.TryCharge(1_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Budget(maxSteps: -1));
    }

    [TestMethod]
    public void OutcomeCasesPatternMatch()
    {
        static string Describe(Outcome<int> outcome) => outcome switch
        {
            Outcome<int>.Success s => $"ok {s.Value} {s.Check}",
            Outcome<int>.Partial p => $"partial {p.Value}: {p.Reason}",
            Outcome<int>.Unevaluated u => $"unevaluated: {u.Reason}",
            Outcome<int>.Failed f => $"failed {f.Error.Kind}",
            _ => throw new InvalidOperationException(),
        };

        Assert.AreEqual("ok 4 NotChecked", Describe(Outcome.Ok(4)));
        Assert.AreEqual("ok 4 Verified", Describe(Outcome.Ok(4, new FakeDerivation(), Provisos.None, Verification.Verified)));
        Assert.AreEqual("partial 2: budget", Describe(new Outcome<int>.Partial(2, "budget", null, Provisos.None)));
        Assert.AreEqual("unevaluated: no rule", Describe(new Outcome<int>.Unevaluated(new Fact("f"), "no rule")));
        Assert.AreEqual("failed DivisionByZero", Describe(Outcome.Fail<int>(MathError.DivisionByZero())));

        Assert.IsTrue(Outcome.Ok(7).TryGetValue(out var seven) && seven == 7);
        Assert.IsTrue(new Outcome<int>.Partial(8, "x", null, Provisos.None).TryGetValue(out var eight) && eight == 8);
        Assert.IsFalse(Outcome.Fail<int>(MathError.Domain("d")).TryGetValue(out _));
        Assert.IsFalse(new Outcome<int>.Unevaluated(new Fact("f"), "r").TryGetValue(out _));
    }

    [TestMethod]
    public void MathErrorFactoriesSetTheKind()
    {
        var subject = new Fact("1/0");
        Assert.AreEqual(MathErrorKind.Domain, MathError.Domain("m").Kind);
        Assert.AreEqual(MathErrorKind.DivisionByZero, MathError.DivisionByZero().Kind);
        Assert.AreEqual(MathErrorKind.NonConvergence, MathError.NonConvergence("m").Kind);
        Assert.AreEqual(MathErrorKind.BudgetExceeded, MathError.BudgetExceeded().Kind);
        Assert.AreEqual(MathErrorKind.Unsupported, MathError.Unsupported("m").Kind);
        Assert.AreEqual(MathErrorKind.SortMismatch, MathError.SortMismatch("m").Kind);
        Assert.AreSame(subject, MathError.DivisionByZero(subject: subject).Subject);
        Assert.AreEqual("Domain: outside", MathError.Domain("outside").ToString());
    }
}
