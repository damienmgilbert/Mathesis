using System.Text.RegularExpressions;

namespace Mathesis.Knowledge.Tests;

[TestClass]
public partial class HandleTests
{
    [GeneratedRegex("new\\(\"([a-z0-9.-]+)\"\\)")]
    private static partial Regex HandlePattern();

    private static string RepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Mathesis.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Mathesis.slnx not found above the test binaries.");
    }

    [TestMethod]
    public void TheCheckedInHandlesMatchTheCatalog()
    {
        var generated = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Mathesis.Knowledge", "Catalog.g.cs"));
        var handles = HandlePattern().Matches(generated).Select(m => m.Groups[1].Value).ToList();
        var ids = KnowledgeBase.Default.Entries.Select(e => e.Id.Value).ToList();
        CollectionAssert.AreEquivalent(ids, handles, "Catalog.g.cs is out of date: run dotnet run eng/gen-knowledge.cs");
    }

    [TestMethod]
    public void HandlesResolveToEntriesOfTheRightKind()
    {
        Assert.AreEqual("alg.exp.product-of-powers", Laws.Algebra.Exp.ProductOfPowers.Value);
        Assert.AreEqual(EntryKind.Law, KnowledgeBase.Default.Get(Laws.Algebra.Exp.ProductOfPowers).Kind);
        Assert.AreEqual(EntryKind.Axiom, KnowledgeBase.Default.Get(Axioms.Algebra.Ax.AddComm).Kind);
        Assert.AreEqual(EntryKind.Formula, KnowledgeBase.Default.Get(Formulas.Algebra.Quad.QuadraticFormula).Kind);
        Assert.AreEqual(EntryKind.Theorem, KnowledgeBase.Default.Get(Theorems.Algebra.Poly.RationalRoot).Kind);
        Assert.AreEqual(EntryKind.Pattern, KnowledgeBase.Default.Get(Patterns.Algebra.Factor.DiffSquares).Kind);
        Assert.AreEqual(EntryKind.Method, KnowledgeBase.Default.Get(Methods.Algebra.Quad.CompletingTheSquare).Kind);
        Assert.AreEqual(EntryKind.Definition, KnowledgeBase.Default.Get(Definitions.Algebra.Quad.Discriminant).Kind);
        Assert.AreEqual(EntryKind.Convention, KnowledgeBase.Default.Get(Conventions.ZeroToTheZero).Kind);
        Assert.AreEqual("trig.exact.3pi-10", Laws.Trigonometry.Exact._3pi10.Value);
        Assert.AreEqual("linalg.det.2x2", Formulas.LinearAlgebra.Det._2x2.Value);
    }
}
