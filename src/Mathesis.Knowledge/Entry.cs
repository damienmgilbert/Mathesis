using System.Collections.Immutable;
using Mathesis.Symbolics;

namespace Mathesis.Knowledge;

/// <summary>The kinds of catalog entries (docs/design/06-knowledge-catalog.md, "Entry kinds").</summary>
public enum EntryKind : byte
{
    /// <summary>Meaning of a term or operator.</summary>
    Definition,

    /// <summary>An accepted starting point.</summary>
    Axiom,

    /// <summary>An identity or equivalence valid under stated conditions.</summary>
    Law,

    /// <summary>Hypotheses imply a conclusion.</summary>
    Theorem,

    /// <summary>A named relation among quantities.</summary>
    Formula,

    /// <summary>A named recognizable shape.</summary>
    Pattern,

    /// <summary>A named procedure.</summary>
    Method,

    /// <summary>A choice where mathematics allows several.</summary>
    Convention,
}

/// <summary>Curriculum levels in increasing order; an explanation at level L never cites an entry above L.</summary>
public enum CurriculumLevel : byte
{
    /// <summary>Arithmetic.</summary>
    Arithmetic,

    /// <summary>Pre-Algebra.</summary>
    PreAlgebra,

    /// <summary>Algebra 1.</summary>
    Algebra1,

    /// <summary>Geometry.</summary>
    Geometry,

    /// <summary>Algebra 2.</summary>
    Algebra2,

    /// <summary>Pre-Calculus.</summary>
    PreCalculus,

    /// <summary>Calculus 1.</summary>
    Calculus1,

    /// <summary>Calculus 2.</summary>
    Calculus2,

    /// <summary>Calculus 3.</summary>
    Calculus3,

    /// <summary>University courses.</summary>
    University,

    /// <summary>Advanced topics.</summary>
    Advanced,
}

/// <summary>Which directions of a law become rewrite rules.</summary>
public enum OrientDirection : byte
{
    /// <summary>Left to right.</summary>
    Ltr,

    /// <summary>Right to left.</summary>
    Rtl,

    /// <summary>Both directions (in different rule sets, chosen by the tags).</summary>
    Both,

    /// <summary>Never used for rewriting.</summary>
    None,
}

/// <summary>How an entry is verified.</summary>
public enum VerifyMode : byte
{
    /// <summary>Randomized numeric verification.</summary>
    Numeric,

    /// <summary>Random instances of a theorem.</summary>
    Instances,

    /// <summary>A stored proof replayed by the logic kernel (Milestone 3).</summary>
    Proof,

    /// <summary>Not verified; the entry must say why.</summary>
    None,
}

/// <summary>A stable catalog identifier <c>domain.topic.name</c> in lowercase kebab-case (or <c>conv.name</c>).</summary>
/// <param name="Value">The identifier text.</param>
public readonly record struct EntryId(string Value)
{
    /// <summary>Whether <paramref name="text"/> has the form of an identifier: two or three dot-separated segments of lowercase letters, digits and hyphens.</summary>
    public static bool IsValid(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('.');
        if (parts.Length is < 2 or > 3) return false;
        foreach (var part in parts)
        {
            if (part.Length == 0 || part[0] == '-' || part[^1] == '-') return false;
            foreach (var c in part)
            {
                if (!(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')) return false;
            }
        }
        return true;
    }

    /// <summary>The domain prefix (the first segment).</summary>
    public string Domain => Value[..Value.IndexOf('.', StringComparison.Ordinal)];

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>A declared pattern variable.</summary>
/// <param name="Name">The symbol name.</param>
/// <param name="SortText">The sort as written (<c>real</c>, <c>function(R -&gt; R)</c>, …).</param>
/// <param name="Sort">The parsed sort.</param>
public sealed record VarDecl(string Name, string SortText, Sort Sort);

/// <summary>A named step of a method with its intermediate form.</summary>
/// <param name="Description">What the step does.</param>
/// <param name="Form">The expression after the step.</param>
public sealed record MethodStep(string Description, Expr Form);

/// <summary>A hint for the verifier's sampler: the variable ranges over the given interval (integers when <see cref="IsInteger"/>).</summary>
/// <param name="Variable">The variable name.</param>
/// <param name="Low">The lower end.</param>
/// <param name="High">The upper end.</param>
/// <param name="IsInteger">Whether only integers are drawn (<c>n in 1..12</c>).</param>
/// <param name="LowOpen">Whether the lower end is excluded.</param>
/// <param name="HighOpen">Whether the upper end is excluded.</param>
public sealed record SampleHint(string Variable, double Low, double High, bool IsInteger, bool LowOpen, bool HighOpen);

/// <summary>A source: <c>dlmf:4.21.2</c>, <c>book:"…"</c> or <c>url:…</c>.</summary>
/// <param name="Kind">The scheme.</param>
/// <param name="Value">The rest.</param>
public sealed record Reference(string Kind, string Value);

/// <summary>A plain-language name for a formula variable.</summary>
/// <param name="Variable">The variable.</param>
/// <param name="Description">Its meaning.</param>
public sealed record Quantity(string Variable, string Description);

/// <summary>
/// One catalog entry: a definition, axiom, law, theorem, formula, pattern, method or convention with its formal statement, exact
/// conditions and explanation (docs/design/06-knowledge-catalog.md). Expression fields are parsed with the Mathesis parser using
/// the declared variables.
/// </summary>
public sealed class Entry
{
    /// <summary>The identifier.</summary>
    public required EntryId Id { get; init; }

    /// <summary>The kind.</summary>
    public required EntryKind Kind { get; init; }

    /// <summary>The display name.</summary>
    public required string Name { get; init; }

    /// <summary>The domain group (<c>alg.exp</c>) the entry was declared in.</summary>
    public required string Domain { get; init; }

    /// <summary>The title of the domain group.</summary>
    public string DomainTitle { get; init; } = string.Empty;

    /// <summary>The source file name.</summary>
    public string File { get; init; } = string.Empty;

    /// <summary>The line of the entry header in <see cref="File"/>.</summary>
    public int Line { get; init; }

    /// <summary>Declared variables.</summary>
    public ImmutableArray<VarDecl> Vars { get; init; } = [];

    /// <summary>The law or formula.</summary>
    public Expr? Statement { get; init; }

    /// <summary>Theorem hypotheses.</summary>
    public ImmutableArray<Expr> Given { get; init; } = [];

    /// <summary>Theorem conclusion.</summary>
    public Expr? Then { get; init; }

    /// <summary>The defined predicate or operator.</summary>
    public Expr? Defines { get; init; }

    /// <summary>The meaning of the defined term.</summary>
    public Expr? Iff { get; init; }

    /// <summary>Conditions in real mode; <c>null</c> means "wherever both sides are defined".</summary>
    public Expr? Where { get; init; }

    /// <summary>Conditions in complex mode when they differ.</summary>
    public Expr? Complex { get; init; }

    /// <summary>Which directions become rewrite rules.</summary>
    public OrientDirection? Orient { get; init; }

    /// <summary>A pattern's recognizer.</summary>
    public Expr? Match { get; init; }

    /// <summary>What a pattern produces.</summary>
    public Expr? Yields { get; init; }

    /// <summary>The shape a method applies to.</summary>
    public Expr? AppliesTo { get; init; }

    /// <summary>A method's named steps.</summary>
    public ImmutableArray<MethodStep> Steps { get; init; } = [];

    /// <summary>A method's final form.</summary>
    public Expr? Result { get; init; }

    /// <summary>Variables a formula can be solved for (empty: all).</summary>
    public ImmutableArray<string> SolveFor { get; init; } = [];

    /// <summary>Plain-language names of formula variables.</summary>
    public ImmutableArray<Quantity> Quantities { get; init; } = [];

    /// <summary>The curriculum level.</summary>
    public CurriculumLevel? Level { get; init; }

    /// <summary>Course tags for browsing.</summary>
    public ImmutableArray<string> Courses { get; init; } = [];

    /// <summary>Rule-set names and search keywords (alternatives written with <c>|</c> are flattened).</summary>
    public ImmutableArray<string> Tags { get; init; } = [];

    /// <summary>The tags as written, for <see cref="OrientDirection.Both"/> entries whose alternatives name different rule sets.</summary>
    public string TagsText { get; init; } = string.Empty;

    /// <summary>The explanation template with <c>{var}</c> placeholders.</summary>
    public string? Explain { get; init; }

    /// <summary>Former identifiers.</summary>
    public ImmutableArray<EntryId> Aliases { get; init; } = [];

    /// <summary>Sources.</summary>
    public ImmutableArray<Reference> Refs { get; init; } = [];

    /// <summary>Related entries.</summary>
    public ImmutableArray<EntryId> See { get; init; } = [];

    /// <summary>The verification mode; when omitted the default for the kind applies (see <see cref="EffectiveVerify"/>).</summary>
    public VerifyMode? Verify { get; init; }

    /// <summary>Sampler hints.</summary>
    public ImmutableArray<SampleHint> Sample { get; init; } = [];

    /// <summary>The documentation ID of the implementing engine method.</summary>
    public string? ImplementedBy { get; init; }

    /// <summary>Why an entry is not verified, or why a convention holds.</summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// The verification mode that applies: the declared one, else numeric for laws, formulas, patterns and methods that give an equivalence
    /// (steps, or an <c>applies-to</c> form with a <c>result</c>), else none.
    /// </summary>
    public VerifyMode EffectiveVerify => Verify ?? (Kind is EntryKind.Law or EntryKind.Formula or EntryKind.Pattern || (Kind == EntryKind.Method && (Steps.Length > 0 || (AppliesTo is not null && Result is not null))) ? VerifyMode.Numeric : VerifyMode.None);

    /// <summary>The variable declarations as a map from name to sort.</summary>
    public IReadOnlyDictionary<string, Sort> Declarations => Vars.ToDictionary(v => v.Name, v => v.Sort);

    /// <summary>Every expression field that is present, with the name of its field.</summary>
    public IEnumerable<(string Field, Expr Expr)> Expressions()
    {
        if (Statement is not null) yield return ("statement", Statement);
        foreach (var g in Given) yield return ("given", g);
        if (Then is not null) yield return ("then", Then);
        if (Defines is not null) yield return ("defines", Defines);
        if (Iff is not null) yield return ("iff", Iff);
        if (Where is not null) yield return ("where", Where);
        if (Complex is not null) yield return ("complex", Complex);
        if (Match is not null) yield return ("match", Match);
        if (Yields is not null) yield return ("yields", Yields);
        if (AppliesTo is not null) yield return ("applies-to", AppliesTo);
        foreach (var s in Steps) yield return ("steps", s.Form);
        if (Result is not null) yield return ("result", Result);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()} {Id}";
}

/// <summary>How serious a <see cref="KnowledgeDiagnostic"/> is.</summary>
public enum DiagnosticSeverity : byte
{
    /// <summary>Worth fixing; does not fail the catalog.</summary>
    Warning,

    /// <summary>The catalog is wrong.</summary>
    Error,
}

/// <summary>A problem found while reading or validating the catalog.</summary>
/// <param name="Severity">How serious it is.</param>
/// <param name="EntryId">The entry it concerns, if any.</param>
/// <param name="File">The file.</param>
/// <param name="Line">The 1-based line (0 when unknown).</param>
/// <param name="Message">What is wrong.</param>
public sealed record KnowledgeDiagnostic(DiagnosticSeverity Severity, string? EntryId, string File, int Line, string Message)
{
    /// <inheritdoc />
    public override string ToString() => $"{File}({Line}): {Severity.ToString().ToLowerInvariant()}: {(EntryId is null ? string.Empty : EntryId + ": ")}{Message}";
}
