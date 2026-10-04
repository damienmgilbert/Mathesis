using Mathesis.Symbolics;
using Mathesis.Symbolics.Parsing;

namespace Mathesis.Knowledge.Tests;

[TestClass]
public class CatalogTests
{
    private static KnowledgeBase Catalog => KnowledgeBase.Default;

    [TestMethod]
    public void TheCatalogLoadsWithoutErrors()
    {
        var errors = Catalog.Errors.Select(e => e.ToString()).ToList();
        Assert.AreEqual(0, errors.Count, string.Join("\n", errors.Take(40)));
    }

    [TestMethod]
    public void TheCatalogHasAtLeast450Entries()
    {
        Assert.IsTrue(Catalog.Count >= 450, $"only {Catalog.Count} entries");
    }

    [TestMethod]
    public void IdsAreUniqueAndWellFormed()
    {
        Assert.AreEqual(Catalog.Count, Catalog.Entries.Select(e => e.Id.Value).Distinct().Count());
        foreach (var e in Catalog.Entries)
        {
            Assert.IsTrue(EntryId.IsValid(e.Id.Value), e.Id.Value);
            Assert.AreEqual(e.Domain + "." + e.Id.Value[(e.Domain.Length + 1)..], e.Id.Value);
        }
    }

    [TestMethod]
    public void EveryStatementParsesPrintsAndParsesBack()
    {
        var failures = new List<string>();
        foreach (var e in Catalog.Entries)
        {
            var options = new ParserOptions { Declarations = e.Declarations };
            foreach (var (field, expr) in e.Expressions())
            {
                var printed = expr.ToString();
                var again = Parser.Parse(printed, options);
                if (!again.Success) failures.Add($"{e.Id} {field}: `{printed}` does not parse: {again.Errors[0]}");
                else if (!again.Expr!.Equals(expr)) failures.Add($"{e.Id} {field}: `{printed}` parses to a different tree");
            }
        }
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(40)));
    }

    [TestMethod]
    public void EveryEntryHasLevelCourseAndTheFieldsItsKindNeeds()
    {
        foreach (var e in Catalog.Entries)
        {
            Assert.IsNotNull(e.Level, e.Id.Value);
            Assert.IsFalse(e.Courses.IsEmpty, e.Id.Value);
            switch (e.Kind)
            {
                case EntryKind.Law or EntryKind.Formula:
                    Assert.IsNotNull(e.Statement, e.Id.Value);
                    break;
                case EntryKind.Pattern:
                    Assert.IsTrue(e.Match is not null && e.Yields is not null, e.Id.Value);
                    break;
                case EntryKind.Axiom:
                    Assert.IsFalse(e.Refs.IsEmpty, e.Id.Value);
                    break;
            }
        }
    }

    [TestMethod]
    public void LinksResolve()
    {
        foreach (var e in Catalog.Entries)
        {
            foreach (var target in e.See) Assert.IsTrue(Catalog.Contains(target.Value), $"{e.Id} sees unknown {target}");
        }
    }

    [TestMethod]
    public void LookupByIdDomainKindTagCourseAndText()
    {
        var e = Catalog.Get("alg.ax.add-comm");
        Assert.AreEqual(EntryKind.Axiom, e.Kind);
        Assert.AreEqual("Commutativity of addition", e.Name);
        Assert.AreSame(e, Catalog.Get(new EntryId("alg.ax.add-comm")));
        Assert.IsTrue(Catalog.TryGet("alg.ax.add-comm", out _));
        Assert.IsFalse(Catalog.TryGet("alg.ax.nope", out _));
        Assert.ThrowsExactly<KeyNotFoundException>(() => Catalog.Get("alg.ax.nope"));

        Assert.IsTrue(Catalog.ByDomain("alg").Count() > 100);
        Assert.IsTrue(Catalog.ByDomain("alg.ax").All(x => x.Domain == "alg.ax"));
        Assert.IsFalse(Catalog.ByDomain("alg.a").Any(), "a domain prefix matches whole groups only");
        Assert.IsTrue(Catalog.ByKind(EntryKind.Law).Any());
        Assert.IsTrue(Catalog.ByCourse("Algebra").Any());
        Assert.IsTrue(Catalog.ByTag("solve-by-factoring").Any(x => x.Id.Value == "alg.prop.zero-product"));
        Assert.IsTrue(Catalog.Search("pythagorean identity").Any(x => x.Id.Value == "trig.id.pythagorean"));
        Assert.IsFalse(Catalog.Search("").Any());
        Assert.IsTrue(Catalog.UpToLevel(CurriculumLevel.Algebra1).All(x => x.Level <= CurriculumLevel.Algebra1));
    }

    [TestMethod]
    public void ConventionsExist()
    {
        foreach (var id in new[] { "conv.zero-to-the-zero", "conv.real-odd-root", "conv.bare-log-base-10", "conv.rounding", "conv.decimal-exact", "conv.order-of-operations", "conv.arccot-range" })
        {
            Assert.AreEqual(EntryKind.Convention, Catalog.Get(id).Kind, id);
        }
    }
}
