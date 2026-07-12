using System.Collections;

namespace SpireLoc.Core.Diagnostics;

/// <summary>An ordered, mutable collection of diagnostics with helpers for common report shapes.</summary>
public sealed class DiagnosticCollection : IReadOnlyList<Diagnostic>
{
    private readonly List<Diagnostic> _diagnostics;

    public DiagnosticCollection()
    {
        _diagnostics = [];
    }

    public DiagnosticCollection(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = [.. diagnostics];
    }

    public int Count => _diagnostics.Count;
    public Diagnostic this[int index] => _diagnostics[index];
    public bool HasErrors => _diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    public void AddRange(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics.AddRange(diagnostics);
    }

    public void AddInfo(string code, string message) =>
        Add(new Diagnostic(DiagnosticSeverity.Info, code, message));

    public void AddWarning(string code, string message) =>
        Add(new Diagnostic(DiagnosticSeverity.Warning, code, message));

    public void AddError(string code, string message) =>
        Add(new Diagnostic(DiagnosticSeverity.Error, code, message));

    public void AddException(string code, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        AddError(code, exception.Message);
    }

    public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
