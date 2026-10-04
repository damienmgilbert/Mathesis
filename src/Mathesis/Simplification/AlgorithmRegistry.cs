using Mathesis.Symbolics.Rewriting;

namespace Mathesis.Simplification;

/// <summary>
/// Every algorithm that can appear as an <c>algorithm:&lt;name&gt;</c> step, by name, so that <see cref="StepReplayer"/> can re-run a recorded
/// step. Algorithms are deterministic functions of the node they are applied to.
/// </summary>
public static class AlgorithmRegistry
{
    private static readonly Dictionary<string, AlgorithmFunction> All = new(Transforms.Algorithms, StringComparer.Ordinal);

    static AlgorithmRegistry()
    {
        foreach (var (name, function) in Calculus.CalculusAlgorithms.All) All[name] = function;
    }

    /// <summary>Finds the algorithm called <paramref name="name"/>.</summary>
    public static bool TryGet(string name, out AlgorithmFunction function) => All.TryGetValue(name, out function!);
}
