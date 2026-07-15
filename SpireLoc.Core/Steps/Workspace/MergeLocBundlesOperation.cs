using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Registration;

namespace SpireLoc.Core.Steps.Workspace;

/// <summary>Merges localization bundles into one workspace slot using later bundles as overlays.</summary>
public sealed class MergeLocBundlesOperation : ILocOperation
{
    private readonly IReadOnlyList<string> _fromSlots;
    private readonly bool _useAllBundles;

    [OperationFactory("merge", Description = "Merge localization bundles, with later sources overriding earlier ones.")]
    public MergeLocBundlesOperation(
        [OperationParameter("from", 0,
            Description = "Source slots in merge order; omit to use every localization bundle slot.")]
        IReadOnlyList<string>? fromSlots = null,
        [OperationParameter("to", Description = "Destination localization bundle slot.")]
        string toSlot = "main")
    {
        _useAllBundles = fromSlots is null || fromSlots.Count == 0;
        _fromSlots = fromSlots?.ToArray() ?? [];
        if (!_useAllBundles)
        {
            foreach (var slot in _fromSlots)
                LocWorkspace.ValidateSlotName(slot);
            var duplicate = _fromSlots.GroupBy(static slot => slot, StringComparer.Ordinal)
                .FirstOrDefault(static group => group.Count() > 1);
            if (duplicate is not null)
                throw new ArgumentException($"Source slot '{duplicate.Key}' occurs more than once.", nameof(fromSlots));
        }

        LocWorkspace.ValidateSlotName(toSlot);
        ToSlot = toSlot;
    }

    public string ToSlot { get; }

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context)
    {
        IReadOnlyList<LocBundle> bundles;
        if (_useAllBundles)
        {
            bundles = workspace
                .Where(static pair => pair.Value is LocBundle)
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => (LocBundle)pair.Value)
                .ToArray();
        }
        else
        {
            var selected = new List<LocBundle>(_fromSlots.Count);
            try
            {
                foreach (var slot in _fromSlots)
                    selected.Add(workspace.Require<LocBundle>(slot));
            }
            catch (LocWorkspaceException exception)
            {
                return Failure(workspace, "LocBundleMerge.Input", exception.Message);
            }

            bundles = selected;
        }

        if (bundles.Count == 0)
            return Failure(workspace, "LocBundleMerge.NoSources", "No localization bundle source slots were found.");

        var merged = bundles[0];
        for (var index = 1; index < bundles.Count; index++)
            merged = merged.Overlay(bundles[index]);
        return new LocOperationResult(workspace.Set(ToSlot, merged));
    }

    private static LocOperationResult Failure(LocWorkspace workspace, string code, string message) =>
        new(workspace, [Diagnostic.Error(code, message)], LocOperationStatus.Failed);
}
