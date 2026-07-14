using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Core.Steps.Reshape.Processing;

/// <summary>Merges RitsuLib model capability source tables into each language's cards table.</summary>
public sealed class RitsuLibModelCapabilityMergeProcessor : UnaryLocBundleProcessor
{
    private const string CardsTableName = "cards";
    private const string UnderscoredTableName = "model_capabilities";
    private const string CompactTableName = "modelcapabilities";

    public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null)
    {
        var tables = bundle.ToMutableTables();
        var capabilityPaths = bundle.Keys.Where(static path => IsCapabilityTable(path.TableName)).ToArray();

        foreach (var capabilityPath in capabilityPaths)
        {
            var entries = tables[capabilityPath];
            var cardsPath = new LocTablePath(capabilityPath.Language, CardsTableName);
            if (tables.TryGetValue(cardsPath, out var cards))
                cards.AddRange(entries);
            else
                tables.Add(cardsPath, [.. entries]);

            tables.Remove(capabilityPath);
        }

        return new LocBundle(tables);
    }

    private static bool IsCapabilityTable(string tableName) =>
        string.Equals(tableName, UnderscoredTableName, StringComparison.Ordinal) ||
        string.Equals(tableName, CompactTableName, StringComparison.Ordinal);
}
