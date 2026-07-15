using SpireLoc.Core.Execution;
using SpireLoc.Core.Steps.Workspace;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Workspace;

public sealed class CopyArtifactOperationTests
{
    [Fact]
    public void CopiesAnyArtifactAndOverwritesTheDestination()
    {
        var source = new TestArtifact("source");
        var workspace = LocWorkspace.Empty
            .Set("source", source)
            .Set("target", new TestArtifact("old"));
        var operation = new CopyArtifactOperation("source", "target");

        var result = operation.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.Same(source, result.Workspace.Require<TestArtifact>("source"));
        Assert.Same(source, result.Workspace.Require<TestArtifact>("target"));
    }

    [Fact]
    public void MissingSourceFailsWithoutChangingTheWorkspace()
    {
        var workspace = LocWorkspace.Empty.Set("existing", new TestArtifact("value"));
        var operation = new CopyArtifactOperation("missing", "target");

        var result = operation.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Equal("ArtifactCopy.Input", Assert.Single(result.Diagnostics).Code);
    }

    private sealed record TestArtifact(string Value) : ILocArtifact;
}
