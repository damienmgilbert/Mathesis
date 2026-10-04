using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Mathesis.Validation;

/// <summary>
/// The base of the Mathesis validation attributes: a standard <see cref="ValidationAttribute"/> that checks mathematical input and
/// reports <see cref="MathValidationResult"/> failures with a stable code, a source span and a suggestion.
/// </summary>
/// <remarks>
/// <para>
/// <b>Presence.</b> <c>null</c>, empty and whitespace-only text are valid; presence is the job of <see cref="RequiredAttribute"/>.
/// <b>Supported values.</b> Text always, plus the typed values each attribute documents; any other type is API misuse and throws
/// <see cref="InvalidOperationException"/>, as does an invalid configuration (on first use, even for a <c>null</c> value; see
/// <see cref="GetConfigurationError"/>). Bad input never throws.
/// </para>
/// <para>
/// <b>One verdict, two entry points.</b> Each attribute computes its verdict once per value and exposes it through
/// <see cref="IsValid(object?)"/>, the validation-context overload used by <c>Validator</c> and UI frameworks, and the typed
/// <see cref="Check"/>. Both overloads work standalone, which the BCL base class does not guarantee for attributes that override only one.
/// </para>
/// <para>
/// <b>Messages.</b> The default message of each <see cref="MathValidationCode"/> is a template in the embedded <c>Messages.resx</c>;
/// <c>{0}</c> is the display name and <c>{1}</c> onwards are the details the code documents. A developer-set
/// <see cref="ValidationAttribute.ErrorMessage"/>, or <see cref="ValidationAttribute.ErrorMessageResourceType"/> with
/// <see cref="ValidationAttribute.ErrorMessageResourceName"/>, replaces every default; placeholders without a value for the failure at hand
/// format as empty text, and a placeholder beyond the attribute's last one is a configuration error. Set the message properties before
/// first use: the configuration is checked once. Values format with <see cref="CultureInfo.CurrentCulture"/>; the verdict never depends on the culture.
/// </para>
/// <para>
/// Options are <c>init</c>-only, so a shared attribute instance is immutable and thread-safe. This type has no public constructor: the
/// attributes of this package derive from it.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public abstract class MathValidationAttribute : ValidationAttribute
{
    /// <summary>The largest allowed <see cref="MaxLength"/>.</summary>
    internal const int MaxLengthLimit = 100_000;

    // Passed to the base class as the default message, so ErrorMessageString equals it (by reference) exactly when the developer set no message.
    private static readonly string DefaultMessage = new('~', 1);

    private readonly Lazy<string?> _configurationError;

    internal MathValidationAttribute()
        : base(static () => DefaultMessage)
    {
        _configurationError = new Lazy<string?>(FindConfigurationError);
    }

    /// <summary>
    /// The longest text, in characters, that is examined; longer text fails with <see cref="MathValidationCode.TooLong"/> before it is parsed.
    /// Defaults to 1,000; must be between 1 and 100,000. Typed values are not affected. Keep the default for form input: at 100,000 characters
    /// a numeric attribute measured up to a quarter of a second on hostile text, under a millisecond at 1,000.
    /// </summary>
    public int MaxLength { get; init; } = 1_000;

    /// <summary>How many message placeholders after <c>{0}</c> this attribute can fill (at least 1: the limit of <see cref="MathValidationCode.TooLong"/>).</summary>
    internal virtual int ExtraPlaceholderCount => 1;

    /// <summary>Whether <paramref name="value"/>, which is not text, is a typed value this attribute can check.</summary>
    internal virtual bool IsSupported(object value) => false;

    /// <summary>The problem with the attribute's own options, or <c>null</c> when they are valid.</summary>
    internal virtual string? ValidateOptions() => null;

    /// <summary>
    /// The verdict on a non-null value: text that is not blank and not longer than <see cref="MaxLength"/>, or a value for which
    /// <see cref="IsSupported"/> is true. <c>null</c> means valid.
    /// </summary>
    internal abstract MathDiagnostic? Evaluate(object value);

    /// <summary>
    /// Describes what is wrong with this attribute's configuration (an option out of range, a message template with an undefined
    /// placeholder, both <see cref="ValidationAttribute.ErrorMessage"/> and a resource name), or <c>null</c> when it is valid. Never throws.
    /// Use it at startup; the validation methods throw <see cref="InvalidOperationException"/> with this text instead.
    /// </summary>
    public string? GetConfigurationError() => _configurationError.Value;

    /// <summary>Validates <paramref name="value"/> without a <see cref="ValidationContext"/>.</summary>
    /// <param name="value">The text or typed value to check.</param>
    /// <param name="displayName">The name used for <c>{0}</c> in the message.</param>
    /// <param name="memberName">The member the failure belongs to, or <c>null</c>; it becomes <see cref="ValidationResult.MemberNames"/>.</param>
    /// <returns>The failure, or <c>null</c> when the value is valid.</returns>
    /// <exception cref="InvalidOperationException">The attribute is misconfigured or the value has an unsupported type.</exception>
    public MathValidationResult? Check(object? value, string displayName = "Value", string? memberName = null)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return Verdict(value) is { } diagnostic ? Result(diagnostic, displayName, memberName) : null;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The attribute is misconfigured or the value has an unsupported type.</exception>
    public sealed override bool IsValid(object? value) => Verdict(value) is null;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The attribute is misconfigured or the value has an unsupported type.</exception>
    protected sealed override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        return Verdict(value) is { } diagnostic ? Result(diagnostic, validationContext.DisplayName, validationContext.MemberName) : null;
    }

    /// <summary>
    /// The generic message used by <see cref="ValidationAttribute.Validate(object?, string)"/>, which has no verdict to describe: the developer's
    /// message if one is set, otherwise "{0} is not valid.". The code-specific messages come from <see cref="Check"/> and the context overload.
    /// </summary>
    public sealed override string FormatErrorMessage(string name)
    {
        if (_configurationError.Value is { } problem) throw new InvalidOperationException(problem);
        return Message(name, Messages.Get("Invalid"), []);
    }

    private MathDiagnostic? Verdict(object? value)
    {
        if (_configurationError.Value is { } problem) throw new InvalidOperationException(problem);

        if (value is null) return null;
        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (text.Length > MaxLength) return new MathDiagnostic(MathValidationCode.TooLong, [MaxLength]);
        }
        else if (!IsSupported(value))
        {
            throw new InvalidOperationException($"{GetType().Name} cannot validate a value of type {value.GetType().FullName}.");
        }

        return Evaluate(value);
    }

    private MathValidationResult Result(MathDiagnostic diagnostic, string displayName, string? memberName) =>
        new(diagnostic.Code, Message(displayName, Messages.Get(diagnostic.Code.ToString()), diagnostic.Details), string.IsNullOrEmpty(memberName) ? null : [memberName], diagnostic.Span, diagnostic.Suggestion);

    /// <summary>Formats the developer's template, or <paramref name="defaultTemplate"/> when there is none or it formats to nothing, so the message is never empty.</summary>
    private string Message(string displayName, string defaultTemplate, object?[] details)
    {
        var args = new object?[1 + Math.Max(ExtraPlaceholderCount, details.Length)];
        args[0] = displayName;
        for (var i = 1; i < args.Length; i++) args[i] = i <= details.Length ? details[i - 1] : string.Empty;

        string Format(string template)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"{GetType().Name}: the message template '{template}' cannot be formatted with the values of this failure.", ex);
            }
        }

        string? custom = ErrorMessageString;
        var text = custom is null || ReferenceEquals(custom, DefaultMessage) ? null : Format(custom);
        return string.IsNullOrWhiteSpace(text) ? Format(defaultTemplate) : text;
    }

    private string? FindConfigurationError()
    {
        var name = GetType().Name;
        if (MaxLength is < 1 or > MaxLengthLimit) return $"{name}: MaxLength must be between 1 and {MaxLengthLimit} but is {MaxLength}.";
        if (ValidateOptions() is { } problem) return $"{name}: {problem}";

        string? custom;
        try
        {
            // The base class validates the combination of ErrorMessage, ErrorMessageResourceName and ErrorMessageResourceType here.
            custom = ErrorMessageString;
        }
        catch (InvalidOperationException ex)
        {
            return $"{name}: {ex.Message}";
        }

        if (custom is not null && !ReferenceEquals(custom, DefaultMessage) && !string.IsNullOrWhiteSpace(custom))
        {
            var args = new object[1 + ExtraPlaceholderCount];
            Array.Fill(args, string.Empty);
            try
            {
                _ = string.Format(CultureInfo.InvariantCulture, custom, args);
            }
            catch (FormatException)
            {
                return $"{name}: the message template '{custom}' is not a valid format string or uses a placeholder beyond {{{ExtraPlaceholderCount}}}.";
            }
        }

        return null;
    }
}
