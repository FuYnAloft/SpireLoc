using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations;

/// <summary>Describes an entry's bundle position and provides an optional diagnostic sink for transforms.</summary>
public sealed class LocEntryTransformContext
{
    public LocEntryTransformContext(
        LocTablePath tablePath,
        int entryIndex,
        LocExecutionContext operationContext,
        DiagnosticCollection? diagnostics = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(entryIndex);

        TablePath = tablePath;
        EntryIndex = entryIndex;
        OperationContext = operationContext;
        Diagnostics = diagnostics;
    }

    public LocTablePath TablePath { get; }
    public int EntryIndex { get; }
    public LocExecutionContext OperationContext { get; }
    public DiagnosticCollection? Diagnostics { get; }

    public void Report(Diagnostic diagnostic)
    {
        Diagnostics?.Add(diagnostic with { Message = $"[{TablePath}#{EntryIndex}] {diagnostic.Message}" });
    }

    public void ReportWarning(string code, string message) =>
        Report(new Diagnostic(DiagnosticSeverity.Warning, code, message));

    public void ReportError(string code, string message) =>
        Report(new Diagnostic(DiagnosticSeverity.Error, code, message));

    public void ReportException(string code, Exception exception) =>
        ReportError(code, exception.Message);
}
