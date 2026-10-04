namespace Mathesis.Knowledge.Tests;

[TestClass]
public class MlawParserTests
{
    private const string Sample = """
        # A comment before everything.
        domain alg.exp "Laws of exponents"
        uses alg.log

        law product-of-powers "Product of powers"
          vars:      a: real, m: real, n: real
          statement: a^m * a^n = a^(m + n)
          where:     a > 0       # trailing comment
          orient:    ltr
          level:     Algebra1
          course:    Algebra
          tags:      combine-powers
          explain:   "Same base # not a comment: add the exponents."

        theorem mean-value "Mean Value Theorem"
          vars:    f: function(R -> R), a: real, b: real
          given:   a < b
                   continuous(f, [a, b])
          then:    exists c in (a, b): f'(c) = (f(b) - f(a)) / (b - a)
          level:   Calculus1
        """;

    [TestMethod]
    public void ParsesDomainsEntriesAndFields()
    {
        var file = MlawParser.Parse(Sample, "sample.mlaw");
        Assert.AreEqual(0, file.Diagnostics.Length, string.Join("\n", file.Diagnostics));
        Assert.AreEqual(2, file.Entries.Length);

        var law = file.Entries[0];
        Assert.AreEqual(EntryKind.Law, law.Kind);
        Assert.AreEqual("product-of-powers", law.Name);
        Assert.AreEqual("Product of powers", law.Title);
        Assert.AreEqual("alg.exp", law.Domain);
        Assert.AreEqual("Laws of exponents", law.DomainTitle);
        Assert.AreEqual("alg.exp.product-of-powers", law.Id);
        CollectionAssert.AreEqual(new[] { "alg.log" }, law.Uses.ToArray());
        Assert.AreEqual(8, law.Fields.Length);
        Assert.AreEqual("a > 0", law.Fields.Single(f => f.Key == "where").Lines[0], "a trailing comment is not part of the value");
        StringAssert.Contains(law.Fields.Single(f => f.Key == "explain").Lines[0], "# not a comment");
        Assert.AreEqual(5, law.Line);

        var theorem = file.Entries[1];
        var given = theorem.Fields.Single(f => f.Key == "given");
        Assert.AreEqual(2, given.Lines.Length);
        Assert.AreEqual("continuous(f, [a, b])", given.Lines[1]);
    }

    [TestMethod]
    public void SeveralDomainGroupsShareAFile()
    {
        var text = "domain alg.a \"A\"\n\nlaw x \"X\"\n  level: Algebra1\n\ndomain alg.b \"B\"\n\nlaw y \"Y\"\n  level: Algebra1\n";
        var file = MlawParser.Parse(text, "t.mlaw");
        Assert.AreEqual(0, file.Diagnostics.Length);
        CollectionAssert.AreEqual(new[] { "alg.a.x", "alg.b.y" }, file.Entries.Select(e => e.Id).ToArray());
    }

    [TestMethod]
    [DataRow("law x \"X\"\n  level: Algebra1\n", "must follow a 'domain'")]
    [DataRow("domain a.b \"T\"\nlaw x \"X\"\n  nope: 1\n", "Unknown field 'nope'")]
    [DataRow("domain a.b \"T\"\nlaw x \"X\"\n  level: A\n  level: B\n", "Duplicate field 'level'")]
    [DataRow("domain a.b \"T\"\nlaw x \"X\"\n\tlevel: Algebra1\n", "Tabs are not allowed")]
    [DataRow("domain a.b \"T\"\n  level: Algebra1\n", "must belong to an entry")]
    [DataRow("domain a.b \"T\"\nbanana x \"X\"\n", "Unexpected 'banana'")]
    [DataRow("domain a.b \"T\"\nlaw x\n", "Expected: law NAME")]
    [DataRow("domain a.b\n", "Expected: domain PREFIX")]
    [DataRow("domain a.b \"T\"\nlaw x \"X\"\n  this is not a field\n", "Expected 'key: value'")]
    public void ReportsStructuralErrors(string text, string expected)
    {
        var file = MlawParser.Parse(text, "t.mlaw");
        Assert.IsTrue(file.Diagnostics.Any(d => d.Message.Contains(expected, StringComparison.Ordinal)), $"expected '{expected}' in: {string.Join(" | ", file.Diagnostics.Select(d => d.Message))}");
        Assert.IsTrue(file.Diagnostics.All(d => d.Severity == DiagnosticSeverity.Error && d.Line >= 1 && d.File == "t.mlaw"));
    }

    [TestMethod]
    public void BlankLinesAndCommentsInsideAnEntryAreIgnored()
    {
        var text = "domain a.b \"T\"\n\nlaw x \"X\"\n  vars: a\n\n  # note\n  statement: a = a\n  level: Algebra1\n";
        var file = MlawParser.Parse(text, "t.mlaw");
        Assert.AreEqual(0, file.Diagnostics.Length, string.Join("\n", file.Diagnostics));
        Assert.AreEqual(3, file.Entries[0].Fields.Length);
    }

    [TestMethod]
    public void KeysAreInCanonicalOrder()
    {
        Assert.AreEqual("vars", MlawParser.Keys[0]);
        Assert.AreEqual("rationale", MlawParser.Keys[^1]);
        Assert.AreEqual(MlawParser.Keys.Length, MlawParser.Keys.Distinct().Count());
        Assert.IsTrue(MlawParser.Keys.IndexOf("level") < MlawParser.Keys.IndexOf("explain"));
    }

    [TestMethod]
    public void NullArgumentsThrow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MlawParser.Parse(null!, "x"));
        Assert.ThrowsExactly<ArgumentNullException>(() => MlawParser.Parse("", null!));
    }

    [TestMethod]
    public void EntryIdsValidateTheirShape()
    {
        Assert.IsTrue(EntryId.IsValid("alg.exp.product-of-powers"));
        Assert.IsTrue(EntryId.IsValid("conv.zero-to-the-zero"));
        Assert.IsTrue(EntryId.IsValid("trig.exact.3pi-10"));
        Assert.IsFalse(EntryId.IsValid("alg"));
        Assert.IsFalse(EntryId.IsValid("alg.exp.a.b"));
        Assert.IsFalse(EntryId.IsValid("Alg.exp.x"));
        Assert.IsFalse(EntryId.IsValid("alg..x"));
        Assert.IsFalse(EntryId.IsValid("alg.exp.-x"));
        Assert.AreEqual("alg", new EntryId("alg.exp.x").Domain);
        Assert.AreEqual("alg.exp.x", new EntryId("alg.exp.x").ToString());
    }
}
