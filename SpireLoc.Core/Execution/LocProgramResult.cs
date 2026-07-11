using SpireLoc.Core.Diagnostics;

namespace SpireLoc.Core.Execution;

public sealed class LocProgramResult(LocWorkspace workspace, IEnumerable<Diagnostic> diagnostics)
{
    public LocWorkspace Workspace { get; } = workspace ?? throw new ArgumentNullException(nameof(workspace));
    public IReadOnlyList<Diagnostic> Diagnostics { get; } = diagnostics?.ToArray()
        ?? throw new ArgumentNullException(nameof(diagnostics));
}
