using System.Globalization;
using Tommy;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Registration;

namespace SpireLoc.Core.Steps.IO;

[method: OperationFactory("input", "toml")]
public sealed class ReadTomlLocalizationDirectoryOperation(
    [OperationParameter("path", 0)] string rootPath,
    [OperationParameter("to")] string toSlot = LocalizationDirectoryOperationSupport.DefaultSlotName)
    : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Read(workspace, rootPath, toSlot, ".toml", Parse);

    private static LocTable Parse(string text)
    {
        using var reader = new StringReader(text);
        return TommyLocalizationMapping.Read(TOML.Parse(reader));
    }
}

[method: OperationFactory("output", "toml")]
public sealed class WriteTomlLocalizationDirectoryOperation(
    [OperationParameter("path", 0)] string rootPath,
    [OperationParameter("from")] string fromSlot = LocalizationDirectoryOperationSupport.DefaultSlotName)
    : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Write(workspace, rootPath, fromSlot, ".toml", Serialize);

    private static string Serialize(LocTable table)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        TommyLocalizationMapping.Write(table).WriteTo(writer);
        return TommyLocalizationMapping.NormalizeNewlines(writer.ToString());
    }
}

internal static class TommyLocalizationMapping
{
    public static LocTable Read(TomlTable root)
    {
        var entries = new List<LocEntry>();
        ReadTable(root, [], entries);
        return new LocTable(entries);
    }

    public static TomlTable Write(LocTable table)
    {
        var root = new TomlTable();
        foreach (var entry in table)
        {
            var current = root;
            for (var index = 0; index < entry.Key.Count - 1; index++)
            {
                var segment = entry.Key[index];
                if (!current.HasKey(segment))
                {
                    var child = new TomlTable();
                    current[segment] = child;
                    current = child;
                }
                else if (current[segment] is TomlTable child)
                {
                    current = child;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Key '{string.Join('.', entry.Key)}' conflicts with an existing string leaf.");
                }
            }

            var leaf = entry.Key[^1];
            if (current.HasKey(leaf))
            {
                throw new InvalidOperationException(
                    $"Key '{string.Join('.', entry.Key)}' occurs more than once or conflicts with a nested mapping.");
            }

            current[leaf] = new TomlString
            {
                Value = entry.Value,
                IsMultiline = entry.Value.Contains('\n') || entry.Value.Contains('\r')
            };
        }

        return root;
    }

    private static void ReadTable(TomlTable table, IReadOnlyList<string> path, ICollection<LocEntry> entries)
    {
        foreach (var (key, node) in table.RawTable)
        {
            var childPath = path.Append(key).ToArray();
            switch (node)
            {
                case TomlTable child:
                    ReadTable(child, childPath, entries);
                    break;
                case TomlString value:
                    entries.Add(new LocEntry(childPath, NormalizeNewlines(value.Value)));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Expected a string leaf at key '{string.Join('.', childPath)}'.");
            }
        }
    }

    public static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
