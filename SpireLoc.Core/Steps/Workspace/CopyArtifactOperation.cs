using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Registration;

namespace SpireLoc.Core.Steps.Workspace;

/// <summary>Copies an immutable artifact reference from one workspace slot to another.</summary>
[method: OperationFactory("copy", Description = "Copy an artifact to another workspace slot.")]
public sealed class CopyArtifactOperation(
    [OperationParameter("from", 0, Description = "Source artifact slot.")]
    string fromSlot,
    [OperationParameter("to", 1, Description = "Destination workspace slot.")]
    string toSlot) : ILocOperation
{
    public string FromSlot { get; } = ValidateSlotName(fromSlot);
    public string ToSlot { get; } = ValidateSlotName(toSlot);

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context)
    {
        if (!workspace.TryGetValue(FromSlot, out var artifact))
        {
            return new LocOperationResult(
                workspace,
                [Diagnostic.Error("ArtifactCopy.Input", $"Workspace slot '{FromSlot}' does not exist.")],
                LocOperationStatus.Failed);
        }

        return new LocOperationResult(workspace.Set(ToSlot, artifact));
    }

    private static string ValidateSlotName(string slotName)
    {
        LocWorkspace.ValidateSlotName(slotName);
        return slotName;
    }
}
