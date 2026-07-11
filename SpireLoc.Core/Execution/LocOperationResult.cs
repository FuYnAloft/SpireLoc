using SpireLoc.Core.Diagnostics;

namespace SpireLoc.Core.Execution;

public enum LocOperationStatus
{
    Succeeded,
    Failed,
    Skipped
}

public sealed class LocOperationResult
{
    public LocOperationResult(
        LocWorkspace workspace,
        IEnumerable<Diagnostic>? diagnostics = null,
        LocOperationStatus status = LocOperationStatus.Succeeded)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        Workspace = workspace;
        Diagnostics = (diagnostics ?? []).ToArray();
        Status = status;
    }

    public LocWorkspace Workspace { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
    public LocOperationStatus Status { get; }
}
