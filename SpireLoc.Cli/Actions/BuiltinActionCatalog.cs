using System.Reflection;

namespace SpireLoc.Cli.Actions;

internal sealed class BuiltinActionCatalog
{
    private const string ResourcePrefix = "SpireLoc.Cli.Actions.Builtin.";
    private const string ResourceSuffix = ".yaml";

    private readonly IReadOnlyDictionary<string, ActionDocument> _documents;

    public BuiltinActionCatalog(ActionYamlLoader loader, Assembly assembly)
    {
        var documents = new Dictionary<string, ActionDocument>(StringComparer.Ordinal);
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                                           name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var name = resourceName[ResourcePrefix.Length..^ResourceSuffix.Length];
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var document = loader.LoadBuiltin(name, reader);
            if (!documents.TryAdd(name, document))
                throw new CliException($"Builtin action '{name}' is embedded more than once.");
        }

        _documents = documents;
        Entries = documents
            .Select(static pair => new BuiltinActionInfo(
                pair.Key,
                pair.Value.Description,
                pair.Value.Parameters))
            .ToArray();
    }

    public IReadOnlyList<BuiltinActionInfo> Entries { get; }

    public bool TryLoad(string name, out ActionDocument document) =>
        _documents.TryGetValue(name, out document!);
}
