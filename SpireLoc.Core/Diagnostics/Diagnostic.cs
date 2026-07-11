namespace SpireLoc.Core.Diagnostics;

/// <summary>A non-fatal issue reported while executing a localization operation.</summary>
public sealed record Diagnostic(DiagnosticSeverity Severity, string Code, string Message)
{
    public static Diagnostic Error(string code, string message) =>
        new(DiagnosticSeverity.Error, code, message);
}
