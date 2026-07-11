namespace SpireLoc.Core.Execution;

public interface ILocOperation
{
    LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context);
}
