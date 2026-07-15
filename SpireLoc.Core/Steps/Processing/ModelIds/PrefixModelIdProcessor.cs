using System.Globalization;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ModelIds;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

/// <summary>Converts selected key segments using a caller-provided model ID prefix.</summary>
public sealed class PrefixModelIdProcessor : ModelIdBundleProcessor
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModelIdTransform>> _tableTransforms;

    public PrefixModelIdProcessor(
        ModelIdDirection direction,
        string prefix,
        IReadOnlyList<string> tableSpecifications)
        : base(direction)
    {
        if (tableSpecifications.Count == 0)
            throw new ArgumentException("At least one table must be specified.", nameof(tableSpecifications));

        Prefix = prefix;
        var rules = new HashSet<(string TableName, int KeyIndex)>();
        var transforms = new Dictionary<string, List<ModelIdTransform>>(StringComparer.Ordinal);
        foreach (var specification in tableSpecifications)
        {
            var (tableName, keyIndex) = ParseTableSpecification(specification);
            if (!rules.Add((tableName, keyIndex)))
            {
                throw new ArgumentException(
                    $"Table specification '{specification}' duplicates '{tableName}:{keyIndex}'.",
                    nameof(tableSpecifications));
            }

            if (!transforms.TryGetValue(tableName, out var tableTransforms))
            {
                tableTransforms = [];
                transforms.Add(tableName, tableTransforms);
            }

            tableTransforms.Add(ModelIdTransform.Prefixed(keyIndex, prefix));
        }

        _tableTransforms = transforms.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<ModelIdTransform>)pair.Value,
            StringComparer.Ordinal);
    }

    public string Prefix { get; }

    protected override IEnumerable<ILocEntryTransform> GetTransforms(LocTablePath tablePath, LocEntry entry) =>
        _tableTransforms.TryGetValue(tablePath.TableName, out var transforms)
            ? transforms
            : [];

    private static (string TableName, int KeyIndex) ParseTableSpecification(string specification)
    {
        if (specification.Length == 0 || specification.All(char.IsWhiteSpace))
            throw InvalidTableSpecification(specification);

        var separator = specification.IndexOf(':');
        if (separator < 0)
            return (specification, 0);
        if (separator == 0 || separator != specification.LastIndexOf(':') || separator == specification.Length - 1)
            throw InvalidTableSpecification(specification);

        var tableName = specification[..separator];
        var indexText = specification[(separator + 1)..];
        if (tableName.All(char.IsWhiteSpace) ||
            !int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var keyIndex))
            throw InvalidTableSpecification(specification);

        return (tableName, keyIndex);
    }

    private static ArgumentException InvalidTableSpecification(string specification) =>
        new(
            $"Table specification '{specification}' must use 'table' or 'table:keyIndex' with a non-negative integer index.",
            nameof(specification));
}
