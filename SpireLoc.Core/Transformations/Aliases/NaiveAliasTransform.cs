using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations.Aliases;

/// <summary>Applies ordered literal source aliases to localization values.</summary>
public sealed class NaiveAliasTransform(params NaiveAliasTransform.Rule[] rules) : ReversibleLocEntryTransform
{
    public sealed record Rule(string Alias, string Game);

    protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context)
    {
        var value = entry.Value;
        foreach (var rule in rules)
            value = value.Replace(rule.Alias, rule.Game, StringComparison.Ordinal);
        return new LocEntry(entry.Key, value);
    }

    protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context)
    {
        var value = entry.Value;
        foreach (var rule in rules)
            value = value.Replace(rule.Game, rule.Alias, StringComparison.Ordinal);
        return new LocEntry(entry.Key, value);
    }
}
