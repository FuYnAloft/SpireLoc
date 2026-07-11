using System.Text.RegularExpressions;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;

namespace SpireLoc.Core.Transformations.ModelIds;

/// <summary>Transforms configured model ID key segments while keeping the entry's structure and value intact.</summary>
public sealed partial class ModelIdTransform : ReversibleLocEntryTransform
{
    private readonly ModelIdRule[] _rules;

    public ModelIdTransform(params ModelIdRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Any(static rule => rule is null))
            throw new ArgumentException("Model ID transform rules cannot be null.", nameof(rules));
        if (rules.Any(static rule => rule.KeyIndex < 0))
            throw new ArgumentOutOfRangeException(nameof(rules), "Model ID key indexes cannot be negative.");
        if (rules.Select(static rule => rule.KeyIndex).Distinct().Count() != rules.Length)
            throw new ArgumentException("Only one model ID rule may target each key index.", nameof(rules));
        if (rules.Any(static rule => rule.Prefix is null))
            throw new ArgumentException("Model ID prefixes cannot be null.", nameof(rules));

        _rules = rules.ToArray();
    }

    protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context)
    {
        var key = entry.Key.ToArray();
        foreach (var rule in _rules)
        {
            if (!TryGetSegment(key, rule, context, out var segment))
                continue;
            if (PreservedIdRegex().IsMatch(segment))
                continue;

            key[rule.KeyIndex] = rule.Prefix + Slugify(segment);
        }

        return new LocEntry(key, entry.Value);
    }

    protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context)
    {
        var key = entry.Key.ToArray();
        foreach (var rule in _rules)
        {
            if (!TryGetSegment(key, rule, context, out var segment))
                continue;

            if (segment.StartsWith(rule.Prefix, StringComparison.Ordinal))
            {
                key[rule.KeyIndex] = Unslugify(segment[rule.Prefix.Length..]);
            }
            else if (!PreservedIdRegex().IsMatch(segment))
            {
                context.ReportWarning(
                    "ModelIdTransform.UnexpectedGameId",
                    $"Key segment '{segment}' at index {rule.KeyIndex} does not start with expected prefix '{rule.Prefix}'.");
            }
        }

        return new LocEntry(key, entry.Value);
    }

    public static string Slugify(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var separated = CamelCaseRegex().Replace(value.Trim(), "$1_$2");
        var normalized = WhitespaceRegex().Replace(separated.ToUpperInvariant(), "_");
        return SpecialCharacterRegex().Replace(normalized, string.Empty);
    }

    public static string Unslugify(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
            return string.Empty;

        var camelCase = SnakeCaseRegex().Replace(value.Trim().ToLowerInvariant(),
            match => match.Groups[1].Value + match.Groups[2].Value.ToUpperInvariant());
        return camelCase.Length == 0 ? string.Empty : char.ToUpperInvariant(camelCase[0]) + camelCase[1..];
    }

    private static bool TryGetSegment(
        IReadOnlyList<string> key,
        ModelIdRule rule,
        LocEntryTransformContext context,
        out string segment)
    {
        if (rule.KeyIndex < key.Count)
        {
            segment = key[rule.KeyIndex];
            return true;
        }

        context.ReportError(
            "ModelIdTransform.KeyIndexOutOfRange",
            $"Key index {rule.KeyIndex} is outside the entry key with {key.Count} segments.");
        segment = string.Empty;
        return false;
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

}
