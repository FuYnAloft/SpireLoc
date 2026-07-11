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
        ArgumentNullException.ThrowIfNull(tables);
        _tables = new ReadOnlyDictionary<LocTablePath, LocTable>(
            new Dictionary<LocTablePath, LocTable>(tables));
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
}
