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
        ArgumentNullException.ThrowIfNull(initialWorkspace);
        ArgumentNullException.ThrowIfNull(operations);

        var workspace = initialWorkspace;
        var diagnostics = new List<Diagnostic>();
        context ??= LocExecutionContext.Default;

        for (var index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            if (operation is null)
            {
                diagnostics.Add(Diagnostic.Error(
                    "Operation.Null",
                    $"Operation at index {index} is null and was skipped."));
                continue;
            }

            try
            {
                var result = operation.Execute(workspace, context)
                    ?? throw new InvalidOperationException("An operation returned null.");
                workspace = result.Workspace;
                diagnostics.AddRange(result.Diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostic.Error(
                    "Operation.InternalError",
                    $"Operation at index {index} failed unexpectedly: {exception.Message}"));
            }
        }

        return new LocProgramResult(workspace, diagnostics);
    }
}
