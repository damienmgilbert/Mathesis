using System.Globalization;
using System.Resources;

namespace Mathesis.Validation;

/// <summary>The default message templates, embedded as <c>Messages.resx</c>: one per <see cref="MathValidationCode"/> plus <c>Invalid</c> for the context-free path.</summary>
internal static class Messages
{
    internal static ResourceManager Resources { get; } = new("Mathesis.Validation.Messages", typeof(Messages).Assembly);

    /// <summary>The template for the resource <paramref name="key"/> (a code name or <c>Invalid</c>) in the current UI culture, falling back to the neutral English text.</summary>
    internal static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? throw new InvalidOperationException($"The message '{key}' is missing from Mathesis.Validation.Messages.resx.");
}
