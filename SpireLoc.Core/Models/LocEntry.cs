using System.Collections;
using System.Text;

namespace SpireLoc.Core.Models;

/// <summary>A localization value identified by an ordered sequence of key segments.</summary>
public sealed record LocEntry
{
    private readonly KeySegments _key;

    public LocEntry(IEnumerable<string> key, string value)
    {
        _key = new KeySegments(key);
        if (_key.Count == 0)
            throw new ArgumentException("A localization key must contain at least one segment.", nameof(key));

        Value = value;
    }

    /// <summary>The structural segments of the localization key.</summary>
    public IReadOnlyList<string> Key => _key;

    public string Value { get; }

    public bool Equals(LocEntry? other) =>
        other is not null && Value == other.Value && _key.Equals(other._key);

    public override int GetHashCode() => HashCode.Combine(_key, Value);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Key = [");
        builder.AppendJoin(", ", Key);
        builder.Append("], Value = ");
        builder.Append(Value);
        return true;
    }

    private sealed class KeySegments : IReadOnlyList<string>, IEquatable<KeySegments>
    {
        private readonly string[] _segments;

        public KeySegments(IEnumerable<string> segments)
        {
            _segments = segments.ToArray();
        }

        public int Count => _segments.Length;
        public string this[int index] => _segments[index];
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_segments).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(KeySegments? other) =>
            other is not null && _segments.SequenceEqual(other._segments, StringComparer.Ordinal);

        public override bool Equals(object? obj) => obj is KeySegments other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var segment in _segments)
                hash.Add(segment, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
