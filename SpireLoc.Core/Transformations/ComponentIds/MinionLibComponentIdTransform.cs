using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations.ComponentIds;

/// <summary>
/// Converts MinionLib component localization keys between source form and the namespaced form stored in cards.json.
/// </summary>
public sealed class MinionLibComponentIdTransform : ReversibleLocEntryTransform
{
    public MinionLibComponentIdTransform(string namespaceTop)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceTop);
        if (namespaceTop.Contains(".", StringComparison.Ordinal))
            throw new ArgumentException("The top-level namespace must be a single key segment.", nameof(namespaceTop));

        NamespaceTop = namespaceTop;
    }

    public string NamespaceTop { get; }

    protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context) =>
        new LocEntry([NamespaceTop, .. entry.Key], entry.Value);

    protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context)
    {
        if (entry.Key.Count < 2)
        {
            context.ReportError(
                "MinionLibComponentIdTransform.KeyTooShort",
                "A game-side component key must contain a namespace and at least one component key segment.");
            return entry;
        }

        if (!string.Equals(entry.Key[0], NamespaceTop, StringComparison.Ordinal))
        {
            context.ReportError(
                "MinionLibComponentIdTransform.UnexpectedNamespace",
                $"Component key namespace '{entry.Key[0]}' does not match expected namespace '{NamespaceTop}'.");
            return entry;
        }

        return new LocEntry(entry.Key.Skip(1), entry.Value);
    }
}
