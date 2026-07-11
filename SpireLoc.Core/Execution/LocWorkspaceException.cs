namespace SpireLoc.Core.Execution;

public abstract class LocWorkspaceException(string message, string slotName) : Exception(message)
{
    public string SlotName { get; } = slotName;
}

public sealed class MissingSlotException(string slotName)
    : LocWorkspaceException($"Workspace slot '{slotName}' does not exist.", slotName);

public sealed class SlotTypeMismatchException(string slotName, Type expectedType, Type actualType)
    : LocWorkspaceException(
        $"Workspace slot '{slotName}' contains '{actualType.FullName}', not the required '{expectedType.FullName}'.",
        slotName)
{
    public Type ExpectedType { get; } = expectedType;
    public Type ActualType { get; } = actualType;
}
