using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using SpireLoc.Core.Execution;

namespace SpireLoc.Core.Models;

/// <summary>A set of localization tables keyed by language and table name.</summary>
public sealed class LocBundle : IReadOnlyDictionary<LocTablePath, LocTable>, ILocArtifact
{
    private readonly IReadOnlyDictionary<LocTablePath, LocTable> _tables;

    public LocBundle(IEnumerable<KeyValuePair<LocTablePath, LocTable>> tables)
    {
        _tables = new ReadOnlyDictionary<LocTablePath, LocTable>(
            new Dictionary<LocTablePath, LocTable>(tables));
    }

    public LocBundle(IEnumerable<KeyValuePair<LocTablePath, IReadOnlyList<LocEntry>>> tables)
        : this(tables.Select(static table =>
            KeyValuePair.Create(table.Key, new LocTable(table.Value))))
    {
    }

    public LocBundle(IEnumerable<KeyValuePair<LocTablePath, List<LocEntry>>> tables)
        : this(tables.Select(static table =>
            KeyValuePair.Create(table.Key, (IReadOnlyList<LocEntry>)table.Value)))
    {
    }

    public int Count => _tables.Count;
    public IEnumerable<LocTablePath> Keys => _tables.Keys;
    public IEnumerable<LocTable> Values => _tables.Values;
    public LocTable this[LocTablePath path] => _tables[path];
    public bool ContainsKey(LocTablePath path) => _tables.ContainsKey(path);
    public bool TryGetValue(LocTablePath path, [MaybeNullWhen(false)] out LocTable value) =>
        _tables.TryGetValue(path, out value);
    public IEnumerator<KeyValuePair<LocTablePath, LocTable>> GetEnumerator() => _tables.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Creates a mutable snapshot of this bundle. Modifying the returned dictionary or lists does not affect this bundle.
    /// </summary>
    public Dictionary<LocTablePath, List<LocEntry>> ToMutableTables() =>
        _tables.ToDictionary(
            static table => table.Key,
            static table => table.Value.ToList());

    /// <summary>
    /// Returns a bundle where tables and entries from <paramref name="overlay"/> replace matching values in this bundle.
    /// Existing entry positions are retained and new entry keys are appended in overlay order.
    /// </summary>
    public LocBundle Overlay(LocBundle overlay)
    {
        var tables = new Dictionary<LocTablePath, LocTable>(_tables);
        foreach (var (path, overlayTable) in overlay)
        {
            tables[path] = tables.TryGetValue(path, out var currentTable)
                ? OverlayTable(currentTable, overlayTable)
                : overlayTable;
        }

        return new LocBundle(tables);
    }

    private static LocTable OverlayTable(LocTable current, LocTable overlay)
    {
        var entries = current.ToList();
        var indexes = new Dictionary<IReadOnlyList<string>, int>(KeyComparer.Instance);
        for (var index = 0; index < entries.Count; index++)
            indexes[entries[index].Key] = index;

        foreach (var entry in overlay)
        {
            if (indexes.TryGetValue(entry.Key, out var index))
                entries[index] = entry;
            else
            {
                indexes[entry.Key] = entries.Count;
                entries.Add(entry);
            }
        }

        return new LocTable(entries);
    }

    private sealed class KeyComparer : IEqualityComparer<IReadOnlyList<string>>
    {
        public static KeyComparer Instance { get; } = new();

        public bool Equals(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
            ReferenceEquals(left, right) ||
            (left is not null && right is not null && left.SequenceEqual(right, StringComparer.Ordinal));

        public int GetHashCode(IReadOnlyList<string> key)
        {
            var hash = new HashCode();
            foreach (var segment in key)
                hash.Add(segment, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
