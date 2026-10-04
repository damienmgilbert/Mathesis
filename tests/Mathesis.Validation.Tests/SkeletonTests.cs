using System.Reflection;

namespace Mathesis.Validation.Tests;

/// <summary>Placeholder checks for the package skeleton (ADR-15); the attribute tests arrive with Phases 1-5 of PLAN-M9.md.</summary>
[TestClass]
public class SkeletonTests
{
    [TestMethod]
    public void ValidationAssemblyLoadsAndReferencesOnlyFrameworkAndMathesisAssemblies()
    {
        var assembly = Assembly.Load("Mathesis.Validation");

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            var allowed = name.StartsWith("System", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.", StringComparison.Ordinal)
                || name.StartsWith("Mathesis.", StringComparison.Ordinal);
            Assert.IsTrue(allowed, $"Mathesis.Validation references '{name}', which is outside System.*, Microsoft.* and Mathesis.*.");
        }
    }
}
