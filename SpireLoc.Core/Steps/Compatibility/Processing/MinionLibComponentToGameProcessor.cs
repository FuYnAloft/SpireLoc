using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.Support;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ComponentIds;

namespace SpireLoc.Core.Steps.Compatibility.Processing;

/// <summary>Moves each language's components table into cards and adds the MinionLib namespace segment.</summary>
[method: OperationFactory("compat", "minionlib-component", "to-game",
    Description = "Convert source-side MinionLib component localization for game output.")]
public sealed class MinionLibComponentToGameProcessor(
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
        var componentPaths = bundle.Keys
            .Where(static path => string.Equals(path.TableName, ComponentsTableName, StringComparison.Ordinal))
            .ToArray();

        foreach (var componentPath in componentPaths)
        {
            var components = tables[componentPath];
            var transformed = new List<LocEntry>(components.Count);
            for (var index = 0; index < components.Count; index++)
            {
                var context = new LocEntryTransformContext(
                    componentPath, index, LocExecutionContext.Default, diagnostics);
                transformed.Add(_componentId.ToGame(components[index], context));
            }

            var cardsPath = new LocTablePath(componentPath.Language, CardsTableName);
            if (tables.TryGetValue(cardsPath, out var cards))
                cards.AddRange(transformed);
            else
                tables.Add(cardsPath, transformed);

            tables.Remove(componentPath);
        }

        return new LocBundle(tables);
    }
}
