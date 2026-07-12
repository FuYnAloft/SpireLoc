using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using Xunit;

namespace SpireLoc.Core.Tests.Diagnostics;

public sealed class DiagnosticCollectionTests
{
    [Fact]
    public void AddsConvenienceDiagnosticsInInsertionOrder()
    {
        var diagnostics = new DiagnosticCollection();

        diagnostics.AddInfo("Test.Info", "info");
        diagnostics.AddWarning("Test.Warning", "warning");
        diagnostics.AddException("Test.Exception", new InvalidOperationException("boom"));

        Assert.True(diagnostics.HasErrors);
        Assert.Equal(
            ["Test.Info", "Test.Warning", "Test.Exception"],
            diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[2].Severity);
        Assert.Equal("boom", diagnostics[2].Message);
    }

    [Fact]
    public void OperationResultPreservesPassedCollection()
    {
        var diagnostics = new DiagnosticCollection();
        var result = new LocOperationResult(LocWorkspace.Empty, diagnostics);

        Assert.Same(diagnostics, result.Diagnostics);
    }
}
