using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Validation.Tests;

/// <summary>Seeded equivalence and concurrency checks of the base rules: both <c>IsValid</c> overloads and <c>Check</c> give one verdict (PLAN-M9 Phase 1).</summary>
[TestClass]
public class MathValidationFuzzTests
{
    private const int Seed = 20261004;
    private const string Alphabet = "0123456789abcxyz+-*/^()[]{}., =<>!|_\\−·é中";

    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    private static string Preview(object? value) => value switch
    {
        null => "null",
        string s => $"string of length {s.Length} '{(s.Length > 40 ? s[..40] + "..." : s)}'",
        _ => $"{value.GetType().Name} {value}",
    };

    /// <summary>A random value: blank, short or long text (some past the default limit of 1,000 characters), or a typed value.</summary>
    private static object? RandomValue(Gen gen)
    {
        var random = gen.Random;
        var kind = random.Next(20);
        if (kind == 0) return null;
        if (kind == 1) return string.Empty;
        if (kind == 2) return new string(' ', random.Next(1, 30)) + (random.Next(2) == 0 ? "\t" : string.Empty);
        if (kind == 3) return random.Next(-100, 100);
        if (kind == 4) return random.NextInt64(-1_000_000, 1_000_000);
        if (kind == 5) return gen.Rational(8);

        var length = kind < 17 ? random.Next(1, 40) : random.Next(900, 1_200);
        var text = new StringBuilder(length);
        for (var i = 0; i < length; i++) text.Append(Alphabet[random.Next(Alphabet.Length)]);
        return text.ToString();
    }

    private static string Describe(MathValidationResult? result) =>
        result is null ? "valid" : $"{result.Code}|{result.Span}|{result.Suggestion}|{result.ErrorMessage}|{string.Join(",", result.MemberNames)}";

    [TestMethod]
    public void BothIsValidOverloadsAndCheckAgreeOnTenThousandSeededInputs()
    {
        var gen = new Gen(Seed);
        var probe = new ProbeAttribute();
        var context = MathValidationAttributeTests.Context("Member", "Formula");
        var failures = 0;

        for (var i = 0; i < 10_000; i++)
        {
            var value = RandomValue(gen);
            var where = $"seed {Seed}, case {i}, {Preview(value)}";

            var plain = probe.IsValid(value);
            var viaContext = probe.GetValidationResult(value, context);
            var viaCheck = probe.Check(value, "Formula", "Member");

            Assert.AreEqual(plain, viaContext is null, where);
            Assert.AreEqual(plain, viaCheck is null, where);

            var oracle = value is string { Length: > 1_000 } text && !string.IsNullOrWhiteSpace(text) ? MathValidationCode.TooLong : MathValidationAttributeTests.ExpectedCode(value);
            Assert.AreEqual(oracle is null, plain, where);

            if (!plain)
            {
                failures++;
                var math = (MathValidationResult)viaContext!;
                Assert.AreEqual(oracle, math.Code, where);
                Assert.AreEqual(Describe(viaCheck), Describe(math), where);
                Assert.IsFalse(string.IsNullOrWhiteSpace(math.ErrorMessage), where);
            }
        }

        Assert.IsTrue(failures > 2_000, $"the generator should produce many failures but produced {failures} (seed {Seed})");
        Assert.IsTrue(failures < 9_000, $"the generator should produce many valid values but produced {10_000 - failures} (seed {Seed})");
    }

    [TestMethod]
    public void OneSharedInstanceGivesIdenticalResultsOnEightThreads()
    {
        var gen = new Gen(Seed + 1);
        var inputs = Enumerable.Range(0, 200).Select(_ => RandomValue(gen)).ToArray();

        // A fresh instance per run, so all threads race on the first use (configuration check, message setup).
        var shared = new ProbeAttribute { ErrorMessage = "{0}|{1}|{2}|{3}" };
        var reference = new ProbeAttribute { ErrorMessage = "{0}|{1}|{2}|{3}" };
        var expected = inputs.Select(v => Describe(reference.Check(v, "F", "M"))).ToArray();

        var barrier = new Barrier(8);
        var failures = new ConcurrentBag<string>();
        var threads = Enumerable.Range(0, 8).Select(t => new Thread(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            barrier.SignalAndWait();
            for (var n = 0; n < 5_000; n++)
            {
                var index = (n * 7 + t * 13) % inputs.Length;
                string actual;
                switch (n % 3)
                {
                    case 0:
                        actual = Describe(shared.Check(inputs[index], "F", "M"));
                        break;
                    case 1:
                        actual = Describe((MathValidationResult?)shared.GetValidationResult(inputs[index], MathValidationAttributeTests.Context("M", "F")));
                        break;
                    default:
                        actual = shared.IsValid(inputs[index]) ? "valid" : "invalid";
                        if (actual != (expected[index] == "valid" ? "valid" : "invalid")) failures.Add($"thread {t}, step {n}, input {index} ({Preview(inputs[index])}): IsValid said {actual}");
                        continue;
                }

                if (actual != expected[index]) failures.Add($"thread {t}, step {n}, input {index} ({Preview(inputs[index])}): {actual} != {expected[index]}");
            }
        })).ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(5)));
        Assert.IsNull(shared.GetConfigurationError());
    }
}
