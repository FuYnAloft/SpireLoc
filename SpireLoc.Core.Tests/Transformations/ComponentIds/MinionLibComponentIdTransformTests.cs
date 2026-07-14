using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.ComponentIds;
using Xunit;

namespace SpireLoc.Core.Tests.Transformations.ComponentIds;

public sealed class MinionLibComponentIdTransformTests
{
    [Fact]
    public void AddsAndRemovesNamespaceSegment()
    {
        var transform = new MinionLibComponentIdTransform("myMod");
        var source = new LocEntry(["StrikeComponent", "prefix"], "value");

        var game = transform.ToGame(source, Context());
        var reverted = transform.ToSource(game, Context());

        Assert.Equal(["myMod", "StrikeComponent", "prefix"], game.Key);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void UnexpectedNamespaceReportsErrorWithoutDiscardingKeySegments()
    {
        var diagnostics = new DiagnosticCollection();
        var transform = new MinionLibComponentIdTransform("myMod");
        var entry = new LocEntry(["otherMod", "StrikeComponent", "prefix"], "value");

        var result = transform.ToSource(entry, Context(diagnostics));

        Assert.Equal(entry, result);
        Assert.Contains(diagnostics,
            diagnostic => diagnostic.Code == "MinionLibComponentIdTransform.UnexpectedNamespace" &&
                          diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void NamespaceOnlyKeyReportsErrorAndRemainsUnchanged()
    {
        var diagnostics = new DiagnosticCollection();
        var transform = new MinionLibComponentIdTransform("myMod");
        var entry = new LocEntry(["myMod"], "value");

        var result = transform.ToSource(entry, Context(diagnostics));

        Assert.Equal(entry, result);
        Assert.Contains(diagnostics,
            diagnostic => diagnostic.Code == "MinionLibComponentIdTransform.KeyTooShort");
    }

    private static LocEntryTransformContext Context(DiagnosticCollection? diagnostics = null) =>
        new(new LocTablePath("zhs", "components"), 0, LocExecutionContext.Default, diagnostics);
}
