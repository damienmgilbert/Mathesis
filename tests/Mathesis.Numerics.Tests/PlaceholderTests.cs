namespace Mathesis.Numerics.Tests;

[TestClass]
public class PlaceholderTests
{
    [TestMethod]
    public void BuildAndTestHarnessRuns() => Assert.AreEqual(4, 2 + int.Parse("2", System.Globalization.CultureInfo.InvariantCulture));
}
