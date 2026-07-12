using System.Text.RegularExpressions;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;

namespace SpireLoc.Core.Transformations.ModelIds;

/// <summary>Transforms configured model ID key segments while keeping the entry's structure and value intact.</summary>
public sealed partial class ModelIdTransform : ReversibleLocEntryTransform
{
    public ModelIdTransform(int keyIndex, string prefix)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(keyIndex);
        KeyIndex = keyIndex;
        Prefix = prefix;
    }

    public int KeyIndex { get; }
    public string Prefix { get; }

    public static ModelIdTransform Vanilla(int keyIndex) =>
        new(keyIndex, string.Empty);

    public static ModelIdTransform Prefixed(int keyIndex, string prefix) =>
        new(keyIndex, prefix);

    public static ModelIdTransform BaseLib(int keyIndex, string namespaceTop) =>
        Prefixed(keyIndex, BaseLibPrefix(namespaceTop));

    public static ModelIdTransform RitsuLib(int keyIndex, string modId, string category) =>
        Prefixed(keyIndex, RitsuLibPrefix(modId, category));

    public static string BaseLibPrefix(string namespaceTop)
    {
        ValidateNotBlank(namespaceTop, nameof(namespaceTop));
        return $"{namespaceTop.ToUpperInvariant()}-";
    }

    public static string RitsuLibPrefix(string modId, string category) =>
        $"{NormalizePublicStem(modId)}_{SlugifyCategory(category)}_";

    protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context) =>
        TransformToGame(entry, KeyIndex, Prefix, context);

    protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context) =>
        TransformToSource(entry, KeyIndex, Prefix, context);

    internal static LocEntry TransformToGame(
        LocEntry entry,
        int keyIndex,
        string prefix,
        LocEntryTransformContext context)
    {
        var key = entry.Key.ToArray();
        if (!TryGetSegment(key, keyIndex, context, out var segment))
            return entry;
        if (PreservedIdRegex().IsMatch(segment))
            return entry;

        key[keyIndex] = prefix + Slugify(segment);

        return new LocEntry(key, entry.Value);
    }

    internal static LocEntry TransformToSource(
        LocEntry entry,
        int keyIndex,
        string prefix,
        LocEntryTransformContext context)
    {
        var key = entry.Key.ToArray();
        if (!TryGetSegment(key, keyIndex, context, out var segment))
            return entry;

        if (segment.StartsWith(prefix, StringComparison.Ordinal))
        {
            key[keyIndex] = Unslugify(segment[prefix.Length..]);
        }
        else if (!PreservedIdRegex().IsMatch(segment))
        {
            context.ReportWarning(
                "ModelIdTransform.UnexpectedGameId",
                $"Key segment '{segment}' at index {keyIndex} does not start with expected prefix '{prefix}'.");
        }

        return new LocEntry(key, entry.Value);
    }

    public static string Slugify(string value)
    {
        var separated = CamelCaseRegex().Replace(value.Trim(), "$1_$2");
        var normalized = WhitespaceRegex().Replace(separated.ToUpperInvariant(), "_");
        return SpecialCharacterRegex().Replace(normalized, string.Empty);
    }

    public static string Unslugify(string value)
    {
        if (value.Length == 0)
            return string.Empty;

        var camelCase = SnakeCaseRegex().Replace(value.Trim().ToLowerInvariant(),
            match => match.Groups[1].Value + match.Groups[2].Value.ToUpperInvariant());
        return camelCase.Length == 0 ? string.Empty : char.ToUpperInvariant(camelCase[0]) + camelCase[1..];
    }

    private static bool TryGetSegment(
        IReadOnlyList<string> key,
        int keyIndex,
        LocEntryTransformContext context,
        out string segment)
    {
        if (keyIndex < key.Count)
        {
            segment = key[keyIndex];
            return true;
        }

        context.ReportError(
            "ModelIdTransform.KeyIndexOutOfRange",
            $"Key index {keyIndex} is outside the entry key with {key.Count} segments.");
        segment = string.Empty;
        return false;
    }

    private static string NormalizePublicStem(string value)
    {
        ValidateNotBlank(value, nameof(value));
        var normalized = NonAlphaNumericRegex().Replace(value.Trim(), "_");
        normalized = AcronymBoundaryRegex().Replace(normalized, "$1_$2");
        normalized = CamelBoundaryRegex().Replace(normalized, "$1_$2");
        normalized = RepeatedUnderscoreRegex().Replace(normalized, "_");
        return normalized.Trim('_').ToUpperInvariant();
    }

    private static string SlugifyCategory(string category)
    {
        ValidateNotBlank(category, nameof(category));
        if (category.All(char.IsUpper))
            return category;

        var slug = Slugify(category);
        return slug.EndsWith("_MODEL", StringComparison.Ordinal)
            ? slug[..^"_MODEL".Length]
            : slug;
    }

    private static void ValidateNotBlank(string value, string parameterName)
    {
        if (value.Length == 0 || value.All(char.IsWhiteSpace))
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
    }

    [GeneratedRegex("^[A-Z_-]+$")]
    private static partial Regex PreservedIdRegex();

    [GeneratedRegex(@"([A-Za-z0-9]|\G(?!^))([A-Z])")]
    private static partial Regex CamelCaseRegex();

    [GeneratedRegex(@"(.*?)_([a-zA-Z0-9])")]
    private static partial Regex SnakeCaseRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^A-Z0-9_]")]
    private static partial Regex SpecialCharacterRegex();

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundaryRegex();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundaryRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();

}
