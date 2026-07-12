using SpireLoc.Core.Diagnostics;

namespace SpireLoc.Core.Execution;

public sealed class LocProgramResult
{
    public LocProgramResult(LocWorkspace workspace, IEnumerable<Diagnostic> diagnostics)
    {
        Workspace = workspace;
        Diagnostics = diagnostics is DiagnosticCollection collection
            ? collection
            : new DiagnosticCollection(diagnostics);
    }

    public LocWorkspace Workspace { get; }
    public DiagnosticCollection Diagnostics { get; }
}
