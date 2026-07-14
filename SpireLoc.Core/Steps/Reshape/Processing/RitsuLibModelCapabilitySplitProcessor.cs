using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Support;
using SpireLoc.Core.Transformations.ModelIds;

namespace SpireLoc.Core.Steps.Reshape.Processing;

/// <summary>Extracts RitsuLib model capability entries from each language's cards table.</summary>
public sealed class RitsuLibModelCapabilitySplitProcessor : UnaryLocBundleProcessor
{
    private const string CardsTableName = "cards";
    private const string UnderscoredTableName = "model_capabilities";
    private const string CompactTableName = "modelcapabilities";
    private const string UnderscoredCategoryToken = "_MODEL_CAPABILITY_";
    private const string CompactCategoryToken = "_MODELCAPABILITY_";

    private readonly string? _underscoredPrefix;
    private readonly string? _compactPrefix;

    public RitsuLibModelCapabilitySplitProcessor(string? modId = null)
    {
        if (modId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modId);
            _underscoredPrefix = ModelIdTransform.RitsuLibPrefix(modId, "ModelCapability");
            _compactPrefix = ModelIdTransform.RitsuLibPrefix(modId, "MODELCAPABILITY");
        }

        ModId = modId;
    }

    public string? ModId { get; }

    public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null)
    {
        if (ModId is null)
        {
            diagnostics?.AddWarning(
                "RitsuLibModelCapabilitySplit.HeuristicDetection",
                "No mod ID was provided; model capability entries will be detected by category substring.");
        }

        var tables = bundle.ToMutableTables();
        var cardPaths = bundle.Keys
            .Where(static path => string.Equals(path.TableName, CardsTableName, StringComparison.Ordinal))
            .ToArray();

        foreach (var cardsPath in cardPaths)
        {
            var cards = new List<LocEntry>();
            var underscored = new List<LocEntry>();
            var compact = new List<LocEntry>();

            foreach (var entry in tables[cardsPath])
            {
                switch (GetDestinationTable(entry.Key[0]))
                {
                    case UnderscoredTableName:
                        underscored.Add(entry);
                        break;
                    case CompactTableName:
                        compact.Add(entry);
                        break;
                    default:
                        cards.Add(entry);
                        break;
                }
            }

            tables[cardsPath] = cards;
            if (underscored.Count > 0)
                tables[new LocTablePath(cardsPath.Language, UnderscoredTableName)] = underscored;
            if (compact.Count > 0)
                tables[new LocTablePath(cardsPath.Language, CompactTableName)] = compact;
        }

        return new LocBundle(tables);
    }

    private string? GetDestinationTable(string id)
    {
        if (_underscoredPrefix is not null)
        {
            if (id.StartsWith(_underscoredPrefix, StringComparison.Ordinal))
                return UnderscoredTableName;
            if (id.StartsWith(_compactPrefix!, StringComparison.Ordinal))
                return CompactTableName;
            return null;
        }

        if (id.Contains(UnderscoredCategoryToken, StringComparison.Ordinal))
            return UnderscoredTableName;
        if (id.Contains(CompactCategoryToken, StringComparison.Ordinal))
            return CompactTableName;
        return null;
    }
}
