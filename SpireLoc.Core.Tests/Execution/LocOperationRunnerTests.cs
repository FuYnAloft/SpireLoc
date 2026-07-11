using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using Xunit;

namespace SpireLoc.Core.Tests.Execution;

public sealed class LocOperationRunnerTests
{
    [Fact]
    public void RunsInDeclarationOrderAndSharesResultingWorkspace()
    {
        var observed = new List<string>();
        var operations = new ILocOperation[]
        {
            new DelegateLocOperation((workspace, _) =>
            {
                observed.Add("first");
                return new LocOperationResult(workspace.Set("value", new TestArtifact("created")));
            }),
            new DelegateLocOperation((workspace, _) =>
            {
                observed.Add(workspace.Require<TestArtifact>("value").Value);
                return new LocOperationResult(workspace);
            })
        };

        var result = new LocOperationRunner().Run(LocWorkspace.Empty, operations);

        Assert.Equal(["first", "created"], observed);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ContinuesAfterOperationReportsError()
    {
        var ranAfterFailure = false;
        var operations = new ILocOperation[]
        {
            new DelegateLocOperation((workspace, _) => new LocOperationResult(
                workspace,
                [Diagnostic.Error("Test.First", "first failure")],
                LocOperationStatus.Failed)),
            new DelegateLocOperation((workspace, _) =>
            {
                ranAfterFailure = true;
                return new LocOperationResult(workspace);
            })
        };

        var result = new LocOperationRunner().Run(LocWorkspace.Empty, operations);

        Assert.True(ranAfterFailure);
        Assert.Collection(result.Diagnostics, diagnostic => Assert.Equal("Test.First", diagnostic.Code));
    }

    [Fact]
    public void AggregatesDiagnosticsFromLaterOperationsAfterFailureInStableOrder()
    {
        var operations = new ILocOperation[]
        {
            new DelegateLocOperation((workspace, _) => new LocOperationResult(
                workspace,
                [Diagnostic.Error("Test.First", "first failure")],
                LocOperationStatus.Failed)),
            new DelegateLocOperation((workspace, _) =>
            {
                var diagnostic = workspace.TryGet<TestArtifact>("missing", out var _)
                    ? throw new InvalidOperationException("The test workspace unexpectedly contains the slot.")
                    : Diagnostic.Error("Test.MissingSlot", "required slot is missing");
                return new LocOperationResult(workspace, [diagnostic], LocOperationStatus.Failed);
            })
        };

        var result = new LocOperationRunner().Run(LocWorkspace.Empty, operations);

        Assert.Equal(["Test.First", "Test.MissingSlot"], result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void ConvertsUnexpectedExceptionToDiagnosticAndContinues()
    {
        var operationAfterExceptionRan = false;
        var operations = new ILocOperation[]
        {
            new DelegateLocOperation((_, _) => throw new InvalidOperationException("boom")),
            new DelegateLocOperation((workspace, _) =>
            {
                operationAfterExceptionRan = true;
                return new LocOperationResult(workspace);
            })
        };

        var result = new LocOperationRunner().Run(LocWorkspace.Empty, operations);

        Assert.True(operationAfterExceptionRan);
        Assert.Collection(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("Operation.InternalError", diagnostic.Code);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        });
    }

    private sealed record TestArtifact(string Value) : ILocArtifact;
}
