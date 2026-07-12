using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Support;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ModelIds;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

/// <summary>Shared execution logic for processors that rewrite model-ID key segments.</summary>
public abstract class ModelIdBundleProcessor : UnaryLocBundleProcessor
{
    private static readonly HashSet<string> StandardModelTables = new(StringComparer.Ordinal)
    {
        "achievements",
        "acts",
        "afflications",
        "afflictions",
        "card_keywords",
        "cards",
        "characters",
        "enchantments",
        "encounters",
        "events",
        "modifiers",
        "monsters",
        "orbs",
        "potions",
        "powers",
        "relics",
        "static_hover_tips",
    };

    protected ModelIdBundleProcessor(ModelIdDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown model ID direction.");

        Direction = direction;
    }

    public ModelIdDirection Direction { get; }

    public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null)
    {
        var tables = new Dictionary<LocTablePath, List<LocEntry>>(bundle.Count);
        foreach (var (tablePath, table) in bundle)
        {
            var entries = new List<LocEntry>(table.Count);
            for (var entryIndex = 0; entryIndex < table.Count; entryIndex++)
            {
                var entry = table[entryIndex];
                var context = new LocEntryTransformContext(
                    tablePath, entryIndex, LocExecutionContext.Default, diagnostics);
                foreach (var transform in GetTransforms(tablePath, entry))
                    entry = Apply(transform, entry, context);

                entries.Add(entry);
            }

            tables.Add(tablePath, entries);
        }

        return new LocBundle(tables);
    }

    /// <summary>Returns the transforms applicable to one source entry, in application order.</summary>
    protected abstract IEnumerable<ILocEntryTransform> GetTransforms(LocTablePath tablePath, LocEntry entry);

    protected static bool IsStandardModelTable(string tableName) => StandardModelTables.Contains(tableName);

    protected static bool IsSeaGlassEntry(LocEntry entry) =>
        entry.Key.Count > 1 && string.Equals(entry.Key[0], "SEA_GLASS", StringComparison.Ordinal);

    private LocEntry Apply(ILocEntryTransform transform, LocEntry entry, LocEntryTransformContext context) =>
        Direction == ModelIdDirection.ToGame
            ? transform.ToGame(entry, context)
            : transform.ToSource(entry, context);
}
