namespace Mathesis;

/// <summary>The number field an operation works in. Real mode is the default (docs/design/02-architecture.md, ADR-09).</summary>
public enum NumberField : byte
{
    /// <summary>Real numbers: odd roots of negatives are real, and results stay real or are reported as undefined.</summary>
    Real,

    /// <summary>Complex numbers with documented principal branches.</summary>
    Complex,
}
