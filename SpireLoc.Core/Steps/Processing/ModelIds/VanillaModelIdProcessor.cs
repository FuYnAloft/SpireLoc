using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ModelIds;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

/// <summary>Converts game model IDs between their vanilla and source spellings.</summary>
public sealed class VanillaModelIdProcessor : ModelIdBundleProcessor
{
    private static readonly ModelIdTransform ModelId = ModelIdTransform.Vanilla(0);
    private static readonly ModelIdTransform SeaGlassCharacterId = ModelIdTransform.Vanilla(1);
    private static readonly AncientModelIdTransform AncientId = AncientModelIdTransform.Vanilla();

    public VanillaModelIdProcessor(ModelIdDirection direction)
        : base(direction) { }

    protected override IEnumerable<ILocEntryTransform> GetTransforms(LocTablePath tablePath, LocEntry entry)
    {
        if (string.Equals(tablePath.TableName, "ancients", StringComparison.Ordinal))
            return [AncientId];
        if (!IsStandardModelTable(tablePath.TableName))
            return [];

        return IsSeaGlassEntry(entry)
            ? [ModelId, SeaGlassCharacterId]
            : [ModelId];
    }
}
