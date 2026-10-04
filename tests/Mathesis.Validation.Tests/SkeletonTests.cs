using System.Reflection;

namespace Mathesis.Validation.Tests;

/// <summary>Package skeleton checks (ADR-15): the package references Symbolics and nothing outside the framework and Mathesis.</summary>
[TestClass]
public class SkeletonTests
{
    [TestMethod]
    public void ValidationAssemblyReferencesSymbolicsAndOnlyFrameworkAndMathesisAssemblies()
    {
        var names = typeof(MathValidationAttribute).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

        CollectionAssert.Contains(names, "Mathesis.Symbolics");
        foreach (var name in names)
        {
            var allowed = name.StartsWith("System", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.", StringComparison.Ordinal)
                || name.StartsWith("Mathesis.", StringComparison.Ordinal);
            Assert.IsTrue(allowed, $"Mathesis.Validation references '{name}', which is outside System.*, Microsoft.* and Mathesis.*.");
        }

        Assert.IsFalse(names.Any(n => n.Contains("Windows", StringComparison.Ordinal) || n.Contains("Maui", StringComparison.Ordinal) || n.Contains("Blazor", StringComparison.Ordinal)), "no UI-framework reference");
    }
}
