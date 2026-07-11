using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;
using SpireLoc.Core.Transformations.Aliases;
using SpireLoc.Core.Transformations.ModelIds;
using Xunit;

namespace SpireLoc.Core.Tests.Transformations;

public sealed class LocEntryTransformTests
{
    [Fact]
    public void ContextReportsPositionAndPreservesOperationContext()
    {
        var diagnostics = new List<Diagnostic>();
        var operationContext = new LocExecutionContext();
        var context = new LocEntryTransformContext(
            new LocTablePath("zhs", "cards"), 4, operationContext, diagnostics.Add);

        context.ReportWarning("Test.Warning", "details");

        Assert.Same(operationContext, context.OperationContext);
        Assert.Collection(diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal("Test.Warning", diagnostic.Code);
            Assert.Equal("[zhs/cards#4] details", diagnostic.Message);
        });
    }

    [Fact]
    public void ContextWithoutReporterDoesNotThrow()
    {
        var context = new LocEntryTransformContext(
            new LocTablePath("zhs", "cards"), 0, LocExecutionContext.Default);

        context.ReportError("Test.Error", "details");
    }

    [Fact]
    public void ReversibleBaseReportsNonReversibleTransform()
    {
        var diagnostics = new List<Diagnostic>();
        var transform = new NonReversibleTransform();

        var result = transform.ToGame(Entry("key", "value"), Context(diagnostics));

        Assert.Equal("game", result.Value);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "EntryTransform.NonReversible");
    }

    [Fact]
    public void VanillaModelIdTransformMatchesEmptyPrefixBehavior()
    {
        var transform = new ModelIdTransform(ModelIdRules.Vanilla(0));
        var source = Entry("DoroStrike", "value");

        var game = transform.ToGame(source, Context());
        var reverted = transform.ToSource(game, Context());

        Assert.Equal("DORO_STRIKE", game.Key[0]);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void ModelIdTransformHandlesMultipleSegmentsWithIndependentPrefixes()
    {
        var transform = new ModelIdTransform(
            new ModelIdRule(0, "FIRST-"),
            new ModelIdRule(2, "SECOND-"));

        var game = transform.ToGame(new LocEntry(["FirstModel", "unchanged", "SecondModel"], "value"), Context());

        Assert.Equal(["FIRST-FIRST_MODEL", "unchanged", "SECOND-SECOND_MODEL"], game.Key);
    }

    [Fact]
    public void ModelIdTransformPreservesUppercaseSourceIdAndUnknownGameId()
    {
        var diagnostics = new List<Diagnostic>();
        var transform = new ModelIdTransform(ModelIdRules.Prefixed(0, "MOD-"));

        var source = transform.ToGame(Entry("EXTERNAL-ID", "value"), Context(diagnostics));
        var game = transform.ToSource(Entry("OtherId", "value"), Context(diagnostics));

        Assert.Equal("EXTERNAL-ID", source.Key[0]);
        Assert.Equal("OtherId", game.Key[0]);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ModelIdTransform.UnexpectedGameId");
    }

    [Fact]
    public void ModelIdTransformReportsOutOfRangeRuleAndLeavesEntryUnchanged()
    {
        var diagnostics = new List<Diagnostic>();
        var transform = new ModelIdTransform(ModelIdRules.Prefixed(2, "MOD-"));
        var entry = Entry("OnlySegment", "value");

        var result = transform.ToGame(entry, Context(diagnostics));

        Assert.Equal(entry, result);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ModelIdTransform.KeyIndexOutOfRange");
    }

    [Fact]
    public void BaseLibAndRitsuLibFactoriesPreserveLegacyPrefixes()
    {
        var baseLib = new ModelIdTransform(ModelIdRules.BaseLib(0, "myMod"));
        var ritsuLib = new ModelIdTransform(ModelIdRules.RitsuLib(0, "myMod", "CardModel"));

        Assert.Equal("MYMOD-CARD_ID", baseLib.ToGame(Entry("CardId", "value"), Context()).Key[0]);
        Assert.Equal("MY_MOD_CARD_CARD_ID", ritsuLib.ToGame(Entry("CardId", "value"), Context()).Key[0]);
    }

    [Fact]
    public void NaiveAliasTransformUsesSourceAndGameDirections()
    {
        var transform = new NaiveAliasTransform(new NaiveAliasTransform.Rule("{D}", "{Damage:diff()}"));
        var source = Entry("key", "造成{D}点伤害\n第二行");

        var game = transform.ToGame(source, Context());
        var reverted = transform.ToSource(game, Context());

        Assert.Equal("造成{Damage:diff()}点伤害\n第二行", game.Value);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void BbCodeAliasTransformConvertsPairedTagsAndMetadata()
    {
        var transform = new BbCodeAliasTransform(
            new BbCodeAliasTransform.Rule("g", "gold"),
            new BbCodeAliasTransform.Rule("highlight", "color", "light blue"));
        var game = Entry("key", "[gold]金色[/gold]\n[color=\"light blue\"]蓝色[/color]");

        var source = transform.ToSource(game, Context());
        var reverted = transform.ToGame(source, Context());

        Assert.Equal("[g]金色[/g]\n[highlight]蓝色[/highlight]", source.Value);
        Assert.Equal(game, reverted);
    }

    private static LocEntry Entry(string key, string value) => new([key], value);

    private static LocEntryTransformContext Context(List<Diagnostic>? diagnostics = null) =>
        new(new LocTablePath("zhs", "cards"), 0, LocExecutionContext.Default,
            diagnostics is null ? null : diagnostics.Add);

    private sealed class NonReversibleTransform : ReversibleLocEntryTransform
    {
        protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context) =>
            new(entry.Key, "game");

        protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context) =>
            new(entry.Key, "source");
    }
}
