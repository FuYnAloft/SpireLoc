using System.Collections;
using System.Text;
using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.IO;

internal static class LocalizationDirectoryOperationSupport
{
    public const string DefaultSlotName = "main";
    public static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static LocOperationResult Read(
        LocWorkspace workspace,
        string rootPath,
        string toSlot,
        string extension,
        Func<string, LocTable> parseTable)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (string.IsNullOrWhiteSpace(rootPath))
            return Failure(workspace, "LocalizationDirectory.RootPath", "Localization root path cannot be blank.");
        if (string.IsNullOrWhiteSpace(toSlot))
            return Failure(workspace, "LocalizationDirectory.ReadInput", "Target slot name cannot be blank.");

        LocBundle? existing = null;
        if (workspace.Contains(toSlot))
        {
            try
            {
                existing = workspace.Require<LocBundle>(toSlot);
            }
            catch (LocWorkspaceException exception)
            {
                return Failure(workspace, "LocalizationDirectory.ReadInput", exception.Message);
            }
        }

        var diagnostics = new DiagnosticCollection();
        if (!Directory.Exists(rootPath))
            return Failure(workspace, "LocalizationDirectory.RootNotFound", $"Localization root '{rootPath}' does not exist.");

        var tables = new Dictionary<LocTablePath, LocTable>();
        try
        {
            foreach (var languageDirectory in Directory.EnumerateDirectories(rootPath)
                         .OrderBy(static path => path, StringComparer.Ordinal))
            {
                var language = Path.GetFileName(languageDirectory);
                foreach (var file in Directory.EnumerateFiles(languageDirectory)
                             .Where(path => string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(static path => path, StringComparer.Ordinal))
                {
                    var tableName = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        var path = new LocTablePath(language, tableName);
                        var table = parseTable(File.ReadAllText(file, Utf8));
                        tables.Add(path, table);
                    }
                    catch (Exception exception)
                    {
                        diagnostics.AddError(
                            "LocalizationDirectory.ReadFile",
                            $"Could not read localization file '{file}': {exception.Message}");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            diagnostics.AddError(
                "LocalizationDirectory.Enumerate",
                $"Could not enumerate localization root '{rootPath}': {exception.Message}");
        }

        var loaded = new LocBundle(tables);
        var result = existing?.Overlay(loaded) ?? loaded;
        var nextWorkspace = workspace.Set(toSlot, result);
        return new LocOperationResult(
            nextWorkspace,
            diagnostics,
            diagnostics.HasErrors
                ? LocOperationStatus.Failed
                : LocOperationStatus.Succeeded);
    }

    public static LocOperationResult Write(
        LocWorkspace workspace,
        string rootPath,
        string fromSlot,
        string extension,
        Func<LocTable, string> serializeTable)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (string.IsNullOrWhiteSpace(rootPath))
            return Failure(workspace, "LocalizationDirectory.RootPath", "Localization root path cannot be blank.");
        if (string.IsNullOrWhiteSpace(fromSlot))
            return Failure(workspace, "LocalizationDirectory.WriteInput", "Source slot name cannot be blank.");

        LocBundle bundle;
        try
        {
            bundle = workspace.Require<LocBundle>(fromSlot);
        }
        catch (LocWorkspaceException exception)
        {
            return Failure(workspace, "LocalizationDirectory.WriteInput", exception.Message);
        }

        var diagnostics = new DiagnosticCollection();
        var files = new List<(string Path, string Text)>();
        foreach (var (tablePath, table) in bundle
                     .OrderBy(static pair => pair.Key.Language, StringComparer.Ordinal)
                     .ThenBy(static pair => pair.Key.TableName, StringComparer.Ordinal))
        {
            try
            {
                ValidateFileSegment(tablePath.Language, nameof(tablePath.Language));
                ValidateFileSegment(tablePath.TableName, nameof(tablePath.TableName));
                files.Add((
                    Path.Combine(rootPath, tablePath.Language, tablePath.TableName + extension),
                    serializeTable(table)));
            }
            catch (Exception exception)
            {
                diagnostics.AddError(
                    "LocalizationDirectory.WriteTable",
                    $"Could not serialize localization table '{tablePath}': {exception.Message}");
            }
        }

        if (diagnostics.Count > 0)
            return new LocOperationResult(workspace, diagnostics, LocOperationStatus.Failed);

        foreach (var (path, text) in files)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, NormalizeNewlines(text), Utf8);
            }
            catch (Exception exception)
            {
                diagnostics.AddError(
                    "LocalizationDirectory.WriteFile",
                    $"Could not write localization file '{path}': {exception.Message}");
            }
        }

        return new LocOperationResult(
            workspace,
            diagnostics,
            diagnostics.Count > 0 ? LocOperationStatus.Failed : LocOperationStatus.Succeeded);
    }

    private static LocOperationResult Failure(LocWorkspace workspace, string code, string message) =>
        new(workspace, CreateErrorDiagnostics(code, message), LocOperationStatus.Failed);

    private static DiagnosticCollection CreateErrorDiagnostics(string code, string message)
    {
        var diagnostics = new DiagnosticCollection();
        diagnostics.AddError(code, message);
        return diagnostics;
    }

    private static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    private static void ValidateFileSegment(string value, string parameterName)
    {
        if (value is "." or ".." || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException($"'{value}' is not a valid file name segment.", parameterName);
        }
    }
}

internal static class NestedLocalizationMapping
{
    public static LocTable Read(object? root)
    {
        var entries = new List<LocEntry>();
        Visit(root, [], entries, isRoot: true);
        return new LocTable(entries);
    }

    public static IDictionary<string, object> Write(LocTable table)
    {
        var root = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in table)
        {
            IDictionary<string, object> current = root;
            for (var index = 0; index < entry.Key.Count - 1; index++)
            {
                var segment = entry.Key[index];
                if (!current.TryGetValue(segment, out var next))
                {
                    var child = new Dictionary<string, object>(StringComparer.Ordinal);
                    current[segment] = child;
                    current = child;
                }
                else if (next is IDictionary<string, object> mapping)
                {
                    current = mapping;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Key '{string.Join('.', entry.Key)}' conflicts with an existing string leaf.");
                }
            }

            var leaf = entry.Key[^1];
            if (current.ContainsKey(leaf))
            {
                throw new InvalidOperationException(
                    $"Key '{string.Join('.', entry.Key)}' occurs more than once or conflicts with a nested mapping.");
            }

            current[leaf] = entry.Value;
        }

        return root;
    }

    private static void Visit(object? node, IReadOnlyList<string> path, ICollection<LocEntry> entries, bool isRoot)
    {
        if (!TryGetMapping(node, out var mapping))
        {
            var location = path.Count == 0 ? "root" : $"key '{string.Join('.', path)}'";
            throw new InvalidOperationException($"Expected a mapping at {location}.");
        }

        foreach (var (key, value) in mapping)
        {
            var childPath = path.Append(key).ToArray();
            if (TryGetMapping(value, out _))
            {
                Visit(value, childPath, entries, isRoot: false);
            }
            else if (value is string text)
            {
                entries.Add(new LocEntry(childPath, text));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Expected a string leaf at key '{string.Join('.', childPath)}'.");
            }
        }
    }

    private static bool TryGetMapping(object? value, out IEnumerable<KeyValuePair<string, object?>> mapping)
    {
        if (value is IDictionary<string, object> genericDictionary)
        {
            mapping = genericDictionary.Select(static entry =>
                KeyValuePair.Create(entry.Key, (object?)entry.Value));
            return true;
        }

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
        {
            mapping = readOnlyDictionary.Select(static entry =>
                KeyValuePair.Create(entry.Key, (object?)entry.Value));
            return true;
        }

        if (value is IDictionary dictionary)
        {
            var entries = new List<KeyValuePair<string, object?>>(dictionary.Count);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key)
                    throw new InvalidOperationException("Localization mapping keys must be strings.");
                entries.Add(KeyValuePair.Create(key, entry.Value));
            }

            mapping = entries;
            return true;
        }

        mapping = [];
        return false;
    }
}
