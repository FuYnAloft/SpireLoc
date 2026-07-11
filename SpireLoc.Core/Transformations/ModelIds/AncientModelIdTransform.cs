using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;

namespace SpireLoc.Core.Transformations.ModelIds;

/// <summary>
/// Transforms ancient localization keys according to the game's dialogue key layout.
/// Ancient IDs occupy segment 0. Character IDs occupy segment 2 only under the <c>talk</c> branch.
/// </summary>
public sealed class AncientModelIdTransform : ReversibleLocEntryTransform
{
    private const string TalkSegment = "talk";
    private const string AnyCharacterSegment = "ANY";
    private const string FirstVisitEverSegment = "firstVisitEver";

    public AncientModelIdTransform(string ancientPrefix, string characterPrefix)
    {
        ArgumentNullException.ThrowIfNull(ancientPrefix);
        ArgumentNullException.ThrowIfNull(characterPrefix);

        AncientPrefix = ancientPrefix;
        CharacterPrefix = characterPrefix;
    }

    public string AncientPrefix { get; }
    public string CharacterPrefix { get; }

    public static AncientModelIdTransform Vanilla() =>
        new(string.Empty, string.Empty);

    public static AncientModelIdTransform BaseLib(string namespaceTop) =>
        new(ModelIdTransform.BaseLibPrefix(namespaceTop), ModelIdTransform.BaseLibPrefix(namespaceTop));

    public static AncientModelIdTransform RitsuLib(string modId) =>
        new(
            ModelIdTransform.RitsuLibPrefix(modId, "AncientModel"),
            ModelIdTransform.RitsuLibPrefix(modId, "CharacterModel"));

    protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context)
    {
        var transformed = ModelIdTransform.TransformToGame(entry, 0, AncientPrefix, context);
        return IsCharacterDialogue(transformed)
            ? ModelIdTransform.TransformToGame(transformed, 2, CharacterPrefix, context)
            : transformed;
    }

    protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context)
    {
        var transformed = ModelIdTransform.TransformToSource(entry, 0, AncientPrefix, context);
        return IsCharacterDialogue(transformed)
            ? ModelIdTransform.TransformToSource(transformed, 2, CharacterPrefix, context)
            : transformed;
    }

    private static bool IsCharacterDialogue(LocEntry entry) =>
        entry.Key.Count > 2 &&
        string.Equals(entry.Key[1], TalkSegment, StringComparison.Ordinal) &&
        !string.Equals(entry.Key[2], AnyCharacterSegment, StringComparison.Ordinal) &&
        !string.Equals(entry.Key[2], FirstVisitEverSegment, StringComparison.Ordinal);
}
