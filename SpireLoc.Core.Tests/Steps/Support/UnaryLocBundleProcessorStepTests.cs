using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Support;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Support;

public sealed class UnaryLocBundleProcessorStepTests
{
    [Fact]
    public void ReadsSourceSlotAndWritesProcessorResultToTargetSlot()
    {
        var source = BundleWith("source");
        var replacement = BundleWith("processed");
        var workspace = LocWorkspace.Empty
            .Set("from", source)
            .Set("to", source);
        var step = new UnaryLocBundleProcessorStep(new ReturningProcessor(replacement), "from", "to");

        var result = step.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.Same(replacement, result.Workspace.Require<LocBundle>("to"));
        Assert.Same(source, workspace.Require<LocBundle>("to"));
    }

    [Fact]
    public void MissingSourceSlotBecomesDiagnosticAndPreservesWorkspace()
    {
        var workspace = LocWorkspace.Empty;
        var step = new UnaryLocBundleProcessorStep(new ReturningProcessor(BundleWith("processed")), "missing", "to");

        var result = step.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Collection(result.Diagnostics, diagnostic =>
            Assert.Equal("UnaryLocBundleProcessorStep.Input", diagnostic.Code));
    }

    private static LocBundle BundleWith(string value) => new(
        new Dictionary<LocTablePath, LocTable>
        {
            [new LocTablePath("zhs", "cards")] = new([new LocEntry(["card"], value)])
        });

    private sealed class ReturningProcessor(LocBundle output) : UnaryLocBundleProcessor
    {
        public override LocBundle Process(LocBundle bundle) => output;
    }
}
