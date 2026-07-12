using SpireLoc.Core.Diagnostics;

namespace SpireLoc.Core.Execution;

/// <summary>Executes operations in declaration order and never reorders or parallelizes them.</summary>
public sealed class LocOperationRunner
{
    public LocProgramResult Run(
        LocWorkspace initialWorkspace,
        IReadOnlyList<ILocOperation> operations,
        LocExecutionContext? context = null)
    {
        var workspace = initialWorkspace;
        var diagnostics = new DiagnosticCollection();
        context ??= LocExecutionContext.Default;

        for (var index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            try
            {
                var result = operation.Execute(workspace, context);
                workspace = result.Workspace;
                diagnostics.AddRange(result.Diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.AddError(
                    "Operation.InternalError",
                    $"Operation at index {index} failed unexpectedly: {exception.Message}");
            }
        }

        return new LocProgramResult(workspace, diagnostics);
    }
}
