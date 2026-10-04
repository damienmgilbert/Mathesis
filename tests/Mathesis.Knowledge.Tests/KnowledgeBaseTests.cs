using Mathesis.Symbolics;

namespace Mathesis.Knowledge.Tests;

[TestClass]
public class KnowledgeBaseTests
{
    private static KnowledgeBase Load(string text) => KnowledgeBase.Load([("t.mlaw", text)]);

    private static string Errors(KnowledgeBase kb) => string.Join("\n", kb.Errors);

    private const string Header = "domain alg.t \"Test\"\n\n";

    [TestMethod]
    public void ReadsEveryFieldOfAnEntry()
    {
        var kb = Load(Header + """
            formula quad "Quadratic formula"
              vars:       a: complex, b: complex, c: complex, x: complex
              statement:  a*x^2 + b*x + c = 0 <=> (x = (-b + sqrt(b^2 - 4*a*c)) / (2*a) or x = (-b - sqrt(b^2 - 4*a*c)) / (2*a))
              where:      a != 0
              complex:    a != 0
              orient:     both
              solve-for:  x
              quantities: a "leading coefficient", b "linear coefficient"
              level:      Algebra1
              course:     Algebra, Trigonometry
              tags:       solve | quadratic
              explain:    "Roots of {a}x^2."
              aliases:    alg.t.quadratic-old
              refs:       dlmf:4.21.2, book:"Stewart, Calculus, 9e, §3.4", url:https://example.org/x
              verify:     numeric
              sample:     a in (0, 10), n in 1..12
              implemented-by: M:Mathesis.Solving.Quadratic.Solve
            """);
        Assert.AreEqual(0, kb.Errors.Count(), Errors(kb));
        var e = kb.Get("alg.t.quad");
        Assert.AreEqual(EntryKind.Formula, e.Kind);
        Assert.AreEqual(4, e.Vars.Length);
        Assert.AreEqual(Sort.Complex, e.Vars[0].Sort);
        Assert.IsNotNull(e.Statement);
        Assert.IsNotNull(e.Where);
        Assert.IsNotNull(e.Complex);
        Assert.AreEqual(OrientDirection.Both, e.Orient);
        CollectionAssert.AreEqual(new[] { "x" }, e.SolveFor.ToArray());
        Assert.AreEqual("leading coefficient", e.Quantities[0].Description);
        Assert.AreEqual(CurriculumLevel.Algebra1, e.Level);
        CollectionAssert.AreEqual(new[] { "Algebra", "Trigonometry" }, e.Courses.ToArray());
        CollectionAssert.AreEqual(new[] { "solve", "quadratic" }, e.Tags.ToArray());
        Assert.AreEqual("solve | quadratic", e.TagsText);
        Assert.AreEqual("Roots of {a}x^2.", e.Explain);
        Assert.AreEqual("alg.t.quadratic-old", e.Aliases[0].Value);
        Assert.AreEqual(3, e.Refs.Length);
        Assert.AreEqual("dlmf", e.Refs[0].Kind);
        Assert.AreEqual("4.21.2", e.Refs[0].Value);
        Assert.AreEqual("Stewart, Calculus, 9e, §3.4", e.Refs[1].Value);
        Assert.AreEqual(VerifyMode.Numeric, e.Verify);
        Assert.AreEqual(2, e.Sample.Length);
        Assert.IsTrue(e.Sample[0].LowOpen && e.Sample[0].HighOpen && !e.Sample[0].IsInteger);
        Assert.IsTrue(e.Sample[1].IsInteger && e.Sample[1].Low == 1 && e.Sample[1].High == 12);
        Assert.AreEqual("M:Mathesis.Solving.Quadratic.Solve", e.ImplementedBy);
        Assert.AreSame(e, kb.Get("alg.t.quadratic-old"), "an alias finds the entry");
        Assert.AreEqual(3, e.Expressions().Count(), "statement, where and complex are the expression fields");
    }

    [TestMethod]
    public void ParsesSortsAndStatementsOfFunctionsAndMatrices()
    {
        var kb = Load(Header + """
            law chain "Chain rule"
              vars:      f: function(R -> R), g: function(R -> R), x: real
              statement: diff(f(g(x)), x) = f'(g(x))*diff(g(x), x)
              level:     Calculus1
              course:    Calculus

            law det "Transpose"
              vars:      n: natural, A: matrix(n, n), v: vector(n), S: set(real)
              statement: det(A^T) = det(A)
              level:     University
              course:    LinearAlgebra
            """);
        Assert.AreEqual(0, kb.Errors.Count(), Errors(kb));
        Assert.IsInstanceOfType<FunctionSort>(kb.Get("alg.t.chain").Vars[0].Sort);
        Assert.IsInstanceOfType<MatrixSort>(kb.Get("alg.t.det").Vars[1].Sort);
        Assert.IsInstanceOfType<VectorSort>(kb.Get("alg.t.det").Vars[2].Sort);
        Assert.IsInstanceOfType<SetSort>(kb.Get("alg.t.det").Vars[3].Sort);
    }

    [TestMethod]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x + y = y + x\n  level: Algebra1\n  course: Algebra\n", "undeclared variable 'y'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x + = 3\n  level: Algebra1\n  course: Algebra\n", "In 'statement'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  course: Algebra\n", "Missing 'level'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n", "Missing 'course'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Nowhere\n  course: Algebra\n", "Unknown level 'Nowhere'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Knitting\n", "Unknown course 'Knitting'")]
    [DataRow("law a \"A\"\n  vars: x: banana\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n", "Unknown sort 'banana'")]
    [DataRow("law a \"A\"\n  vars: x, x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n", "declared twice")]
    [DataRow("law a \"A\"\n  vars: x\n  level: Algebra1\n  course: Algebra\n  explain: \"hi\"\n", "needs a 'statement'")]
    [DataRow("pattern a \"A\"\n  vars: x\n  match: x\n  level: Algebra1\n  course: Algebra\n", "needs 'match' and 'yields'")]
    [DataRow("axiom a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n", "must cite a source")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  verify: none\n", "requires a 'rationale'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  see: alg.t.nope\n", "unknown entry 'alg.t.nope'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  orient: sideways\n", "Unknown orientation")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  verify: maybe\n", "Unknown verify mode")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  sample: x everywhere\n", "sample hint")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  refs: nowhere\n", "reference looks like")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  solve-for: y\n", "undeclared variable 'y'")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  aliases: Not.An.Id\n", "not a valid entry ID")]
    [DataRow("law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n\nlaw a \"A again\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n", "Duplicate ID")]
    public void ValidationReportsProblems(string entries, string expected)
    {
        var kb = Load(Header + entries);
        Assert.IsTrue(kb.Errors.Any(e => e.Message.Contains(expected, StringComparison.Ordinal)), $"expected '{expected}' in:\n{Errors(kb)}");
        foreach (var d in kb.Errors) Assert.IsTrue(d.File == "t.mlaw" && d.Line >= 1, d.ToString());
    }

    [TestMethod]
    public void UnknownDomainPrefixAndUsesAreChecked()
    {
        var kb = Load("domain zzz.t \"Z\"\nuses nope.group\n\nlaw a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n");
        Assert.IsTrue(kb.Errors.Any(e => e.Message.Contains("Unknown domain prefix 'zzz'", StringComparison.Ordinal)));
        Assert.IsTrue(kb.Errors.Any(e => e.Message.Contains("'uses nope.group'", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AliasMayNotShadowAnotherEntry()
    {
        var kb = Load(Header + "law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n\nlaw b \"B\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  aliases: alg.t.a\n");
        Assert.IsTrue(kb.Errors.Any(e => e.Message.Contains("alias", StringComparison.Ordinal)), Errors(kb));
    }

    [TestMethod]
    public void TagAlternativesWithoutBothOrientationWarn()
    {
        var kb = Load(Header + "law a \"A\"\n  vars: x\n  statement: x = x\n  level: Algebra1\n  course: Algebra\n  orient: ltr\n  tags: one | two\n");
        Assert.AreEqual(0, kb.Errors.Count());
        Assert.IsTrue(kb.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning));
    }

    [TestMethod]
    public void LookupSearchAndFilters()
    {
        var kb = Load(Header + """
            law add-zero "Adding zero"
              vars:    a
              statement: a + 0 = a
              level:   PreAlgebra
              course:  Algebra
              tags:    identity-simplify
              explain: "Zero does not change a sum."

            theorem big "A theorem about primes"
              level:   University
              course:  Proofs
              explain: "There are infinitely many primes."
            """);
        Assert.AreEqual(2, kb.Count);
        Assert.IsTrue(kb.Contains("alg.t.add-zero"));
        Assert.IsFalse(kb.Contains("alg.t.nope"));
        CollectionAssert.AreEqual(new[] { "alg.t.add-zero" }, kb.ByKind(EntryKind.Law).Select(e => e.Id.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "alg.t.add-zero" }, kb.ByTag("identity-simplify").Select(e => e.Id.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "alg.t.big" }, kb.ByCourse("Proofs").Select(e => e.Id.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "alg.t.add-zero" }, kb.UpToLevel(CurriculumLevel.Algebra1).Select(e => e.Id.Value).ToArray());
        Assert.AreEqual(2, kb.ByDomain("alg").Count());
        Assert.AreEqual(2, kb.ByDomain("alg.t").Count());
        Assert.AreEqual("alg.t.big", kb.Search("primes").Single().Id.Value);
        Assert.AreEqual("alg.t.add-zero", kb.Search("adding").First().Id.Value);
        Assert.AreEqual(0, kb.Search("primes zero").Count(), "every word must match");
        Assert.AreEqual(EntryKind.Theorem, kb.Get("alg.t.big").Kind);
        Assert.ThrowsExactly<ArgumentNullException>(() => kb.Search(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => kb.TryGet(null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => KnowledgeBase.Load(null!));
    }

    [TestMethod]
    public void EffectiveVerifyFollowsTheKind()
    {
        var kb = Load(Header + """
            law l "L"
              vars: x
              statement: x = x
              level: Algebra1
              course: Algebra

            theorem t "T"
              level: Algebra1
              course: Algebra
              explain: "A theorem."

            method m "M"
              vars: x
              applies-to: x + x
              result: 2*x
              level: Algebra1
              course: Algebra

            definition d "D"
              level: Algebra1
              course: Algebra
              explain: "A definition."
            """);
        Assert.AreEqual(0, kb.Errors.Count(), Errors(kb));
        Assert.AreEqual(VerifyMode.Numeric, kb.Get("alg.t.l").EffectiveVerify);
        Assert.AreEqual(VerifyMode.None, kb.Get("alg.t.t").EffectiveVerify);
        Assert.AreEqual(VerifyMode.Numeric, kb.Get("alg.t.m").EffectiveVerify);
        Assert.AreEqual(VerifyMode.None, kb.Get("alg.t.d").EffectiveVerify);
    }

    [TestMethod]
    public void StepsAreReadWithTheirForms()
    {
        var kb = Load(Header + """
            method completing "Completing the square"
              vars:       a, b, c, x
              applies-to: a*x^2 + b*x + c
              where:      a != 0
              steps:      "Factor a out of the x terms"      a*(x^2 + (b/a)*x) + c
                          "Write the perfect square"         a*(x + b/(2*a))^2 + c - b^2/(4*a)
              result:     a*(x + b/(2*a))^2 + (4*a*c - b^2)/(4*a)
              level:      Algebra1
              course:     Algebra
            """);
        Assert.AreEqual(0, kb.Errors.Count(), Errors(kb));
        var steps = kb.Get("alg.t.completing").Steps;
        Assert.AreEqual(2, steps.Length);
        Assert.AreEqual("Factor a out of the x terms", steps[0].Description);
        Assert.IsNotNull(steps[1].Form);
    }
}
