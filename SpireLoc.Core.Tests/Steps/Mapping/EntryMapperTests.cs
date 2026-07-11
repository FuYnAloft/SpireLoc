using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Models;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Steps.Mapping;
using SpireLoc.Core.Transformations;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Mapping;

public sealed class EntryMapperTests
{
    [Fact]
    public void FirstMatchModeWritesOutputSlotAndOnlyAppliesFirstRule()
    {
        var mapper = new EntryMapper("source", "mapped", false,
            new EntryMapper.Rule(Append(" first")),
            new EntryMapper.Rule(Append(" second")));

        var result = mapper.Execute(WorkspaceWith("value"), LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.Equal("value first", SingleEntry(result.Workspace.Require<LocBundle>("mapped")).Value);
    }

    [Fact]
    public void AllMatchesModeChainsRulesInOrder()
    {
        var mapper = new EntryMapper("source", "mapped", true,
            new EntryMapper.Rule((_, _, entry) => entry.Value == "value", Append(" first")),
            new EntryMapper.Rule((_, _, entry) => entry.Value.EndsWith("first"), Append(" second")));

        var result = mapper.Execute(WorkspaceWith("value"), LocExecutionContext.Default);

        Assert.Equal("value first second", SingleEntry(result.Workspace.Require<LocBundle>("mapped")).Value);
    }

    [Fact]
    public void OutputSlotIsOverwrittenWhenItAlreadyExists()
    {
        var workspace = WorkspaceWith("value").Set("mapped", BundleWith("old"));
        var mapper = new EntryMapper("source", "mapped", false,
            new EntryMapper.Rule(Append(" new")));

        var result = mapper.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal("value new", SingleEntry(result.Workspace.Require<LocBundle>("mapped")).Value);
    }

    [Fact]
    public void InputTypeMismatchReturnsDiagnosticAndPreservesWorkspace()
    {
        var workspace = LocWorkspace.Empty.Set("source", new TestArtifact());
        var mapper = new EntryMapper("source", "mapped", false,
            new EntryMapper.Rule(Append(" ignored")));

        var result = mapper.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Collection(result.Diagnostics, diagnostic => Assert.Equal("EntryMapper.Input", diagnostic.Code));
    }

    [Fact]
    public void UnmatchedEntryReturnsErrorAndDoesNotCreateOutputSlot()
    {
        var mapper = new EntryMapper("source", "mapped", false,
            new EntryMapper.Rule((_, _, _) => false, Append(" ignored")));

        var result = mapper.Execute(WorkspaceWith("value"), LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.False(result.Workspace.ContainsKey("mapped"));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Code == "EntryMapper.UnmatchedEntry");
    }

    private static DelegateLocEntryConverter Append(string suffix) =>
        new(entry => new LocEntry(entry.Key, entry.Value + suffix));

    private static LocEntry SingleEntry(LocBundle bundle) =>
        bundle[new LocTablePath("zhs", "cards")].Single();

    private static LocWorkspace WorkspaceWith(string value) => LocWorkspace.Empty.Set("source", BundleWith(value));

    private static LocBundle BundleWith(string value) => new(
        new Dictionary<LocTablePath, LocTable>
        {
            [new LocTablePath("zhs", "cards")] = new([new LocEntry(["Card", "title"], value)])
        });

    private sealed class TestArtifact : ILocArtifact;
}
