using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.Support;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ComponentIds;

namespace SpireLoc.Core.Steps.Compatibility.Processing;

/// <summary>Extracts MinionLib component entries from cards and removes their namespace segment.</summary>
[method: OperationFactory("compat", "minionlib-component", "to-source",
    Description = "Convert game-side MinionLib component localization back to source form.")]
public sealed class MinionLibComponentToSourceProcessor(
    [OperationParameter("namespace-top", 0, Description = "Top-level namespace used by MinionLib component IDs.")]
    string namespaceTop) : UnaryLocBundleProcessor
{
    private const string CardsTableName = "cards";
    private const string ComponentsTableName = "components";
    private readonly MinionLibComponentIdTransform _componentId = new(namespaceTop);

    public string NamespaceTop => _componentId.NamespaceTop;

    public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null)
    {
        var tables = bundle.ToMutableTables();
        var cardPaths = bundle.Keys
            .Where(static path => string.Equals(path.TableName, CardsTableName, StringComparison.Ordinal))
            .ToArray();

        foreach (var cardsPath in cardPaths)
        {
            var sourceEntries = tables[cardsPath];
            var cards = new List<LocEntry>(sourceEntries.Count);
            var components = new List<LocEntry>();

            for (var index = 0; index < sourceEntries.Count; index++)
            {
                var entry = sourceEntries[index];
                if (!IsComponentEntry(entry))
                {
                    cards.Add(entry);
                    continue;
                }

                var context = new LocEntryTransformContext(
                    cardsPath, index, LocExecutionContext.Default, diagnostics);
                components.Add(_componentId.ToSource(entry, context));
            }

            tables[cardsPath] = cards;
            tables[new LocTablePath(cardsPath.Language, ComponentsTableName)] = components;
        }

        return new LocBundle(tables);
    }

    private static bool IsComponentEntry(LocEntry entry) => entry.Key[0].Any(char.IsLower);
}
