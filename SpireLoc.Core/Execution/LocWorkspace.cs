using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace SpireLoc.Core.Execution;

/// <summary>
/// The immutable state passed through an ordered localization operation program.
/// Each slot belongs to one shared string namespace, regardless of artifact type.
/// </summary>
public sealed class LocWorkspace : IReadOnlyDictionary<string, ILocArtifact>
{
    private readonly ImmutableDictionary<string, ILocArtifact> _slots;

    private LocWorkspace(ImmutableDictionary<string, ILocArtifact> slots)
    {
        _slots = slots;
    }

    public static LocWorkspace Empty { get; } = new(
        ImmutableDictionary.Create<string, ILocArtifact>(StringComparer.Ordinal));

    public int Count => _slots.Count;
    public IEnumerable<string> Keys => _slots.Keys;
    public IEnumerable<ILocArtifact> Values => _slots.Values;
    public ILocArtifact this[string slotName] => _slots[slotName];

    public bool Contains(string slotName) => _slots.ContainsKey(slotName);

    public bool ContainsKey(string slotName) => _slots.ContainsKey(slotName);

    public bool TryGetValue(string slotName, [MaybeNullWhen(false)] out ILocArtifact artifact) =>
        _slots.TryGetValue(slotName, out artifact);

    public bool TryGet<TArtifact>(string slotName, [MaybeNullWhen(false)] out TArtifact artifact)
        where TArtifact : class, ILocArtifact
    {
        if (_slots.TryGetValue(slotName, out var value) && value is TArtifact typed)
        {
            artifact = typed;
            return true;
        }

        artifact = null;
        return false;
    }

    public TArtifact Require<TArtifact>(string slotName)
        where TArtifact : class, ILocArtifact
    {
        ValidateSlotName(slotName);

        if (!_slots.TryGetValue(slotName, out var artifact))
            throw new MissingSlotException(slotName);

        if (artifact is TArtifact typed)
            return typed;

        throw new SlotTypeMismatchException(slotName, typeof(TArtifact), artifact.GetType());
    }

    /// <summary>Creates or overwrites a slot and returns the resulting workspace.</summary>
    public LocWorkspace Set(string slotName, ILocArtifact artifact)
    {
        ValidateSlotName(slotName);
        ArgumentNullException.ThrowIfNull(artifact);

        return new LocWorkspace(_slots.SetItem(slotName, artifact));
    }

    public LocWorkspace Remove(string slotName)
    {
        ValidateSlotName(slotName);

        if (!_slots.ContainsKey(slotName))
            throw new MissingSlotException(slotName);

        return new LocWorkspace(_slots.Remove(slotName));
    }

    public IEnumerator<KeyValuePair<string, ILocArtifact>> GetEnumerator() => _slots.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static void ValidateSlotName(string slotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);
    }
}
