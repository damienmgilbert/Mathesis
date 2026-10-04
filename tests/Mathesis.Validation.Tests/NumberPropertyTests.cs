using System.Globalization;
using System.Numerics;
using System.Text;
using Mathesis.Numbers;
using Mathesis.Testing;

namespace Mathesis.Validation.Tests;

/// <summary>Seeded property tests of the numeric attributes: forms of the same number, exact comparison, text versus typed values (PLAN-M9 Phase 2).</summary>
[TestClass]
public class NumberPropertyTests
{
    private const int Seed = 20261005;

    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _culture;

    /// <summary>A random rational with a denominator up to 400, so its repeating decimal has a period below 400 and fits the default length limit.</summary>
    private static BigRational SmallRational(Gen gen) => BigRational.Create(gen.WholeNumber(30), gen.Random.Next(1, 401));

    /// <summary>A random rational whose decimal expansion terminates: the denominator is 2^a·5^b.</summary>
    private static BigRational Terminating(Gen gen) =>
        BigRational.Create(gen.WholeNumber(40), BigInteger.Pow(2, gen.Random.Next(0, 12)) * BigInteger.Pow(5, gen.Random.Next(0, 8)));

    /// <summary>
    /// A random text form of <paramref name="value"/>: the fraction, or the decimal expansion when the denominator is at most 500, so the repeating block
    /// stays short (a longer one would exceed the default text length limit, which the attribute rightly reports as <c>TooLong</c>).
    /// </summary>
    private static string RandomText(BigRational value, Gen gen) => value.Denominator <= 500 && gen.Random.Next(2) == 0 ? value.ToDecimalString() : value.ToString();

    /// <summary><paramref name="value"/> in exponent form: X·10^k with X written as a (possibly repeating) decimal.</summary>
    private static string ExponentForm(BigRational value, int exponent) =>
        (exponent >= 0 ? value / new BigRational(BigInteger.Pow(10, exponent)) : value * new BigRational(BigInteger.Pow(10, -exponent))).ToDecimalString() + "e" + exponent.ToString(CultureInfo.InvariantCulture);

    [TestMethod]
    public void EveryFormOfTenThousandSeededRationalsIsAcceptedAndMeansTheSameNumber()
    {
        var gen = new Gen(Seed);
        var number = new RationalNumberAttribute();
        var integers = 0;
        var terminating = 0;
        var repeating = 0;

        for (var i = 0; i < 10_000; i++)
        {
            var value = i % 2 == 0 ? SmallRational(gen) : Terminating(gen);
            var decimalForm = value.ToDecimalString();
            if (value.IsInteger) integers++;
            else if (decimalForm.Contains('(', StringComparison.Ordinal)) repeating++;
            else terminating++;

            var forms = new[] { value.ToString(), decimalForm, ExponentForm(value, gen.Random.Next(-30, 31)), ExponentForm(value, 0), "+" + decimalForm.TrimStart('-') };
            var negated = value.Sign < 0;
            var exact = new ExactRangeAttribute(value.ToString(), value.ToString());
            var where = $"seed {Seed}, case {i}, value {value}";

            // forms[4] drops the sign of a negative value, so it is only the same number for value >= 0
            foreach (var text in negated ? forms[..4] : forms)
            {
                Assert.IsNull(number.Check(text), $"{where}, text '{text}' rejected by RationalNumber");
                Assert.IsNull(exact.Check(text), $"{where}, text '{text}' is not exactly {value}");
                Assert.AreEqual(value.Numerator.IsZero, new NonZeroAttribute().Check(text)?.Code == MathValidationCode.Zero, $"{where}, text '{text}' NonZero");
            }

            Assert.IsNull(exact.Check(value), where);
            Assert.AreEqual(MathValidationCode.OutOfRange, exact.Check(value + BigRational.Create(1, BigInteger.Pow(10, 40)))!.Code, where);
            Assert.AreEqual(MathValidationCode.OutOfRange, exact.Check(value - BigRational.Create(1, BigInteger.Pow(10, 40)))!.Code, where);
            Assert.AreEqual(value.IsInteger, new RationalNumberAttribute { IntegerOnly = true }.Check(decimalForm) is null, where);
        }

        Assert.IsTrue(integers > 500 && terminating > 2_500 && repeating > 2_500, $"the generator must cover integers, terminating and repeating decimals: {integers}, {terminating}, {repeating} (seed {Seed})");
    }

    [TestMethod]
    public void ExactRangeAgreesWithTheExactComparisonOnTwoHundredSeededBoundPairs()
    {
        var gen = new Gen(Seed + 1);
        var epsilon = BigRational.Create(1, BigInteger.Pow(10, 30));
        var checks = 0;
        var emptyRanges = 0;

        for (var pair = 0; pair < 200; pair++)
        {
            var low = SmallRational(gen);
            var high = pair % 10 == 0 ? low : low + BigRational.Create(gen.Random.Next(1, 1_000), gen.Random.Next(1, 401));
            var lowText = RandomText(low, gen);
            var highText = RandomText(high, gen);

            var samples = new List<BigRational> { low, high, (low + high) / 2, low - epsilon, low + epsilon, high - epsilon, high + epsilon };
            for (var k = 0; k < 5; k++) samples.Add(low + (high - low) * BigRational.Create(gen.Random.Next(-50, 150), 100) + SmallRational(gen) / 1_000);

            foreach (var (minimumExclusive, maximumExclusive) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                var where = $"seed {Seed + 1}, pair {pair}, [{lowText}, {highText}], exclusive {minimumExclusive}/{maximumExclusive}";
                var attribute = new ExactRangeAttribute(lowText, highText) { MinimumIsExclusive = minimumExclusive, MaximumIsExclusive = maximumExclusive };

                if (low == high && (minimumExclusive || maximumExclusive))
                {
                    emptyRanges++;
                    Assert.IsNotNull(attribute.GetConfigurationError(), where);
                    Assert.ThrowsExactly<InvalidOperationException>(() => attribute.IsValid("0"), where);
                    continue;
                }

                Assert.IsNull(attribute.GetConfigurationError(), where);
                foreach (var sample in samples)
                {
                    var expected = (sample > low || (sample == low && !minimumExclusive)) && (sample < high || (sample == high && !maximumExclusive));
                    var typed = attribute.Check(sample);
                    var text = attribute.Check(RandomText(sample, gen));
                    checks += 2;

                    Assert.AreEqual(expected, typed is null, $"{where}, typed {sample}");
                    Assert.AreEqual(expected, text is null, $"{where}, text of {sample}");
                    if (!expected) Assert.AreEqual(MathValidationCode.OutOfRange, typed!.Code, $"{where}, typed {sample}");
                }
            }
        }

        Assert.IsTrue(checks > 15_000 && emptyRanges >= 20, $"{checks} checks, {emptyRanges} empty ranges (seed {Seed + 1})");
    }

    [TestMethod]
    public void TextAndTypedValuesOfTheSameNumberGetTheSameVerdictOnTenThousandSeededCases()
    {
        var gen = new Gen(Seed + 2);
        var random = gen.Random;

        for (var i = 0; i < 10_000; i++)
        {
            var integerOnly = random.Next(2) == 0;
            var rational = new RationalNumberAttribute { IntegerOnly = integerOnly };
            var nonZero = new NonZeroAttribute();

            var low = random.Next(-50, 50);
            var high = low + random.Next(0, 60);
            var minimum = random.Next(8) == 0 ? null : (low + random.Next(2) * 0.5m).ToString(CultureInfo.InvariantCulture);
            var maximum = random.Next(8) == 0 && minimum is not null ? null : high.ToString(CultureInfo.InvariantCulture);
            var exclusiveMinimum = minimum is not null && random.Next(2) == 0;
            var exclusiveMaximum = maximum is not null && random.Next(2) == 0;
            var range = new ExactRangeAttribute(minimum, maximum) { MinimumIsExclusive = exclusiveMinimum, MaximumIsExclusive = exclusiveMaximum };
            if (range.GetConfigurationError() is not null)
            {
                range = new ExactRangeAttribute("-100", "100");   // equal or crossed bounds: any valid range will do
            }

            // The same number in every representation: whole numbers in all integer types, and decimals in the types that hold them.
            var representations = new List<object>();
            if (i % 2 == 0)
            {
                var n = random.NextInt64(-80, 80);
                representations.AddRange([n.ToString(CultureInfo.InvariantCulture), (BigRational)n, new BigInteger(n), (decimal)n, n, (int)n, (double)n, (float)n, BigRational.FromDecimal(n)]);
            }
            else
            {
                var d = random.NextInt64(-100_000, 100_000) / 1000m;
                representations.AddRange([d.ToString(CultureInfo.InvariantCulture), BigRational.FromDecimal(d), d, (double)d]);
            }

            foreach (var attribute in new MathValidationAttribute[] { rational, nonZero, range })
            {
                var expected = attribute.Check(representations[0])?.Code;
                foreach (var representation in representations.Skip(1))
                {
                    var actual = attribute.Check(representation)?.Code;
                    Assert.AreEqual(expected, actual, $"seed {Seed + 2}, case {i}, {attribute.GetType().Name} (min {minimum}, max {maximum}, integerOnly {integerOnly}): text '{representations[0]}' vs {representation.GetType().Name} {representation}");
                }
            }
        }
    }

    [TestMethod]
    public void RandomTextNeverThrowsAndAgreesWithTheParser()
    {
        var gen = new Gen(Seed + 3);
        const string alphabet = "0123456789+-−./(),eE _'x ٣１/";
        var attributes = new MathValidationAttribute[]
        {
            new RationalNumberAttribute(),
            new RationalNumberAttribute { IntegerOnly = true, AllowFractions = false },
            new RationalNumberAttribute { AllowDecimals = false },
            new NonZeroAttribute(),
            new ExactRangeAttribute("-1/2", "3") { MinimumIsExclusive = true },
        };

        for (var i = 0; i < 20_000; i++)
        {
            string text;
            if (i % 3 == 0)
            {
                // a valid number with one character replaced, inserted or removed
                var chars = new StringBuilder(SmallRational(gen).ToDecimalString());
                if (chars.Length == 0) chars.Append('1');
                var at = gen.Random.Next(chars.Length);
                switch (gen.Random.Next(3))
                {
                    case 0: chars[at] = alphabet[gen.Random.Next(alphabet.Length)]; break;
                    case 1: chars.Insert(at, alphabet[gen.Random.Next(alphabet.Length)]); break;
                    default: chars.Remove(at, 1); break;
                }

                text = chars.ToString();
            }
            else
            {
                var chars = new StringBuilder();
                for (var k = gen.Random.Next(0, 24); k > 0; k--) chars.Append(alphabet[gen.Random.Next(alphabet.Length)]);
                text = chars.ToString();
            }

            var isNumber = BigRational.TryParse(text.Replace('−', '-'), CultureInfo.InvariantCulture, out _);
            var blank = string.IsNullOrWhiteSpace(text);
            foreach (var attribute in attributes)
            {
                MathValidationResult? result = null;
                try
                {
                    result = attribute.Check(text);
                    _ = attribute.IsValid(text);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"seed {Seed + 3}, case {i}, {attribute.GetType().Name} threw {ex.GetType().Name} on '{text}': {ex.Message}");
                }

                Assert.IsFalse(string.IsNullOrWhiteSpace(result?.ErrorMessage ?? "x"), $"seed {Seed + 3}, case {i}");
                if (!blank) Assert.AreEqual(!isNumber, result?.Code == MathValidationCode.NotANumber, $"seed {Seed + 3}, case {i}, {attribute.GetType().Name} on '{text}'");
                if (blank) Assert.IsNull(result, $"seed {Seed + 3}, case {i}");
            }
        }
    }
}
