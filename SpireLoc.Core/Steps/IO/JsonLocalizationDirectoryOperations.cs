using System.Text.Encodings.Web;
using System.Text.Json;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.IO;

public sealed class ReadFlatJsonLocalizationDirectoryOperation(
    string rootPath,
    string toSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Read(workspace, rootPath, toSlot, ".json", Parse);

    private static LocTable Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("A flat localization JSON file must contain an object.");

        var entries = new List<LocEntry>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"Flat localization key '{property.Name}' must have a string value.");
            entries.Add(new LocEntry(property.Name.Split('.'), property.Value.GetString()!));
        }

        return new LocTable(entries);
    }
}

public sealed class WriteFlatJsonLocalizationDirectoryOperation(
    string rootPath,
    string fromSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Write(workspace, rootPath, fromSlot, ".json", Serialize);

    private static string Serialize(LocTable table)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in table)
        {
            var key = string.Join('.', entry.Key);
            if (!values.TryAdd(key, entry.Value))
                throw new InvalidOperationException($"Flat JSON key '{key}' occurs more than once.");
        }

        return JsonSerializer.Serialize(values, Options);
    }
}

public sealed class ReadNestedJsonLocalizationDirectoryOperation(
    string rootPath,
    string toSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Read(workspace, rootPath, toSlot, ".json", Parse);

    private static LocTable Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        return NestedLocalizationMapping.Read(Convert(document.RootElement));
    }

    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => Convert(property.Value),
            StringComparer.Ordinal),
        JsonValueKind.String => element.GetString(),
        _ => null
    };
}

public sealed class WriteNestedJsonLocalizationDirectoryOperation(
    string rootPath,
    string fromSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Write(workspace, rootPath, fromSlot, ".json", Serialize);

    private static string Serialize(LocTable table) =>
        JsonSerializer.Serialize(NestedLocalizationMapping.Write(table), Options);
}
