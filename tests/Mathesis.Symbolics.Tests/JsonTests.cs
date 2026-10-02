using System.Text.Json;
using Mathesis.Numbers;
using Mathesis.Symbolics;
using Mathesis.Symbolics.Serialization;
using Mathesis.Testing;
using static Mathesis.Symbolics.Sym;

namespace Mathesis.Symbolics.Tests;

[TestClass]
public class JsonTests
{
    private const int Seed = 20261052;

    [TestMethod]
    public void TheCorpusRoundTripsThroughJson()
    {
        foreach (var line in Corpus.Lines())
        {
            var e = Expr.Parse(line);
            var json = ExprJson.Serialize(e);
            var back = ExprJson.Deserialize(json);
            Assert.AreEqual(e, back, $"'{line}' via {json}");
            Assert.AreEqual(json, ExprJson.Serialize(back), line);
        }
    }

    [TestMethod]
    public void RandomExpressionsRoundTripThroughJson()
    {
        var gen = new Gen(Seed);
        for (var i = 0; i < 2000; i++)
        {
            var e = gen.RandomExpr(4);
            Assert.AreEqual(e, ExprJson.Deserialize(ExprJson.Serialize(e)), e.ToString());
        }
    }

    [TestMethod]
    public void NumbersAreWrittenAsExactStrings()
    {
        var json = ExprJson.Serialize(Number(BigRational.Create(3, 4)));
        Assert.AreEqual("\"3/4\"", json);
        StringAssert.Contains(ExprJson.Serialize(Expr.Parse("x + 3/4")), "\"op\":\"add\"");
        var big = Expr.Parse("123456789012345678901234567890 / 7");
        Assert.AreEqual(big, ExprJson.Deserialize(ExprJson.Serialize(big)));
    }

    [TestMethod]
    public void TheConverterWorksWithPlainSerializer()
    {
        var e = Expr.Parse("sin(x)^2 + cos(x)^2");
        var json = JsonSerializer.Serialize(e);
        Assert.AreEqual(e, JsonSerializer.Deserialize<Expr>(json));
    }

    [TestMethod]
    public void MalformedJsonThrowsJsonException()
    {
        foreach (var bad in new[] { "", "{", "null", "[]", "{\"op\":\"nope\",\"args\":[]}", "{\"op\":\"add\"}", "{\"zzz\":1}", "\"not a number\"", "{\"op\":\"add\",\"args\":[\"1\"]}", "{\"op\":\"sin\",\"args\":[\"1\",\"2\"]}" })
        {
            Assert.ThrowsExactly<JsonException>(() => ExprJson.Deserialize(bad), $"'{bad}'");
        }
    }

    [TestMethod]
    public void NullArgumentsThrow()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ExprJson.Serialize(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ExprJson.Deserialize(null!));
    }
}
