namespace SpireLoc.Core.Execution;

public sealed class DelegateLocOperation(
    Func<LocWorkspace, LocExecutionContext, LocOperationResult> operation) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        operation(workspace, context);
}
