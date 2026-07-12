using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ModelIds;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

/// <summary>Converts RitsuLib's category-qualified model IDs to or from source form.</summary>
public sealed class RitsuLibModelIdProcessor : ModelIdBundleProcessor
{
    private readonly IReadOnlyDictionary<string, ModelIdTransform> _tableTransforms;
    private readonly ModelIdTransform _seaGlassCharacterId;
    private readonly AncientModelIdTransform _ancientId;

    public RitsuLibModelIdProcessor(ModelIdDirection direction, string modId)
        : base(direction)
    {
        if (modId.Length == 0 || modId.All(char.IsWhiteSpace))
            throw new ArgumentException("Mod ID cannot be empty or whitespace.", nameof(modId));

        ModId = modId;
        _tableTransforms = new Dictionary<string, ModelIdTransform>(StringComparer.Ordinal)
        {
            ["achievements"] = ModelIdTransform.RitsuLib(0, modId, "AchievementModel"),
            ["acts"] = ModelIdTransform.RitsuLib(0, modId, "ActModel"),
            ["afflications"] = ModelIdTransform.RitsuLib(0, modId, "AfflictionModel"),
            ["afflictions"] = ModelIdTransform.RitsuLib(0, modId, "AfflictionModel"),
            ["card_keywords"] = ModelIdTransform.RitsuLib(0, modId, "Keyword"),
            ["cards"] = ModelIdTransform.RitsuLib(0, modId, "CardModel"),
            ["characters"] = ModelIdTransform.RitsuLib(0, modId, "CharacterModel"),
            ["enchantments"] = ModelIdTransform.RitsuLib(0, modId, "EnchantmentModel"),
            ["encounters"] = ModelIdTransform.RitsuLib(0, modId, "EncounterModel"),
            ["events"] = ModelIdTransform.RitsuLib(0, modId, "EventModel"),
            ["modifiers"] = ModelIdTransform.RitsuLib(0, modId, "ModifierModel"),
            ["monsters"] = ModelIdTransform.RitsuLib(0, modId, "MonsterModel"),
            ["orbs"] = ModelIdTransform.RitsuLib(0, modId, "OrbModel"),
            ["potions"] = ModelIdTransform.RitsuLib(0, modId, "PotionModel"),
            ["powers"] = ModelIdTransform.RitsuLib(0, modId, "PowerModel"),
            ["relics"] = ModelIdTransform.RitsuLib(0, modId, "RelicModel"),
        };
        _seaGlassCharacterId = ModelIdTransform.RitsuLib(1, modId, "CharacterModel");
        _ancientId = AncientModelIdTransform.RitsuLib(modId);
    }

    public string ModId { get; }

    protected override IEnumerable<ILocEntryTransform> GetTransforms(LocTablePath tablePath, LocEntry entry)
    {
        if (string.Equals(tablePath.TableName, "ancients", StringComparison.Ordinal))
            return [_ancientId];
        if (!_tableTransforms.TryGetValue(tablePath.TableName, out var transform))
            return [];

        return IsSeaGlassEntry(entry)
            ? [transform, _seaGlassCharacterId]
            : [transform];
    }
}
