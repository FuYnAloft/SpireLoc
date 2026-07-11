using System.Text.RegularExpressions;

namespace SpireLoc.Core.Transformations.ModelIds;

/// <summary>Creates model ID rules compatible with the previously supported localization conventions.</summary>
public static partial class ModelIdRules
{
    public static ModelIdRule Vanilla(int keyIndex) =>
        Prefixed(keyIndex, string.Empty);

    public static ModelIdRule Prefixed(int keyIndex, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return new ModelIdRule(keyIndex, prefix);
    }

    public static ModelIdRule BaseLib(int keyIndex, string namespaceTop)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceTop);
        return Prefixed(keyIndex, $"{namespaceTop.ToUpperInvariant()}-");
    }

    public static ModelIdRule RitsuLib(int keyIndex, string modId, string category) =>
        Prefixed(keyIndex, $"{NormalizePublicStem(modId)}_{SlugifyCategory(category)}_");

    private static string NormalizePublicStem(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = NonAlphaNumericRegex().Replace(value.Trim(), "_");
        normalized = AcronymBoundaryRegex().Replace(normalized, "$1_$2");
        normalized = CamelBoundaryRegex().Replace(normalized, "$1_$2");
        normalized = RepeatedUnderscoreRegex().Replace(normalized, "_");
        return normalized.Trim('_').ToUpperInvariant();
    }

    private static string SlugifyCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (category.All(char.IsUpper))
            return category;

        var slug = ModelIdTransform.Slugify(category);
        return slug.EndsWith("_MODEL", StringComparison.Ordinal)
            ? slug[..^"_MODEL".Length]
            : slug;
    }

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundaryRegex();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundaryRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();
}
