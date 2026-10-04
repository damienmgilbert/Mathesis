using Mathesis.Knowledge;
using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Explanation;

/// <summary>Rendering of the steps an outcome carries.</summary>
public static class ExplanationExtensions
{
    /// <summary>Renders a derivation as text, Markdown or LaTeX (docs/design/07-engines.md, "Explanation rendering").</summary>
    /// <param name="steps">The steps of an outcome; only <see cref="Derivation"/> values can be rendered.</param>
    /// <param name="format">The output format.</param>
    /// <param name="verbosity">How much to show.</param>
    /// <param name="level">The learner's curriculum level; steps above it are flagged.</param>
    /// <returns>The explanation, or an empty string when there are no steps.</returns>
    public static string Render(this IDerivation? steps, ExplanationFormat format = ExplanationFormat.Text, Verbosity verbosity = Verbosity.Standard, CurriculumLevel? level = null) =>
        steps is Derivation derivation ? ExplanationRenderer.Render(derivation, new ExplainOptions { Format = format, Verbosity = verbosity, Level = level }) : string.Empty;
}
