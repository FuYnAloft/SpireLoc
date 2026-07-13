using SpireLoc.Core.Diagnostics;

namespace SpireLoc.Core.Execution;

public enum LocOperationStatus
{
    Succeeded,
    Failed,
    Skipped,
}

public sealed class LocOperationResult
{
    public LocOperationResult(
        LocWorkspace workspace,
        IEnumerable<Diagnostic>? diagnostics = null,
        LocOperationStatus status = LocOperationStatus.Succeeded)
    {
        Workspace = workspace;
        Diagnostics = diagnostics switch
        {
            null => new DiagnosticCollection(),
            DiagnosticCollection collection => collection,
            _ => new DiagnosticCollection(diagnostics),
        };
        Status = status;
    }

    public LocWorkspace Workspace { get; }
    public DiagnosticCollection Diagnostics { get; }
    public LocOperationStatus Status { get; }
}
