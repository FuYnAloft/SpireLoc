using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ModelIds;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

/// <summary>Converts BaseLib's namespace-prefixed model IDs to or from source form.</summary>
public sealed class BaseLibModelIdProcessor : ModelIdBundleProcessor
{
    private readonly ModelIdTransform _modelId;
    private readonly ModelIdTransform _seaGlassCharacterId;
    private readonly AncientModelIdTransform _ancientId;

    public BaseLibModelIdProcessor(ModelIdDirection direction, string namespaceTop)
        : base(direction)
    {
        if (namespaceTop.Length == 0 || namespaceTop.All(char.IsWhiteSpace))
            throw new ArgumentException("Namespace cannot be empty or whitespace.", nameof(namespaceTop));

        NamespaceTop = namespaceTop;
        _modelId = ModelIdTransform.BaseLib(0, namespaceTop);
        _seaGlassCharacterId = ModelIdTransform.BaseLib(1, namespaceTop);
        _ancientId = AncientModelIdTransform.BaseLib(namespaceTop);
    }

    public string NamespaceTop { get; }

    protected override IEnumerable<ILocEntryTransform> GetTransforms(LocTablePath tablePath, LocEntry entry)
    {
        if (string.Equals(tablePath.TableName, "ancients", StringComparison.Ordinal))
            return [_ancientId];
        if (!IsStandardModelTable(tablePath.TableName))
            return [];

        return IsSeaGlassEntry(entry)
            ? [_modelId, _seaGlassCharacterId]
            : [_modelId];
    }
}
