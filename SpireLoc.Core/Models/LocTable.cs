using System.Collections;

namespace SpireLoc.Core.Models;

/// <summary>An ordered, immutable snapshot of entries in one localization table.</summary>
public sealed class LocTable : IReadOnlyList<LocEntry>
{
    private readonly LocEntry[] _entries;

    public LocTable(IEnumerable<LocEntry> entries)
    {
        _entries = entries.ToArray();
    }

    public int Count => _entries.Length;
    public LocEntry this[int index] => _entries[index];
    public IEnumerator<LocEntry> GetEnumerator() => ((IEnumerable<LocEntry>)_entries).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
