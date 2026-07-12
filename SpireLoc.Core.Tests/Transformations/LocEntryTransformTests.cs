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
        var diagnostics = new DiagnosticCollection();
        var operationContext = new LocExecutionContext();
        var context = new LocEntryTransformContext(
            new LocTablePath("zhs", "cards"), 4, operationContext, diagnostics);

        context.ReportWarning("Test.Warning", "details");

        Assert.Same(operationContext, context.OperationContext);
        Assert.Same(diagnostics, context.Diagnostics);
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
        var diagnostics = new DiagnosticCollection();
        var transform = new NonReversibleTransform();

        var result = transform.ToGame(Entry("key", "value"), Context(diagnostics));

        Assert.Equal("game", result.Value);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "EntryTransform.NonReversible");
    }

    [Fact]
    public void VanillaModelIdTransformMatchesEmptyPrefixBehavior()
    {
        var transform = ModelIdTransform.Vanilla(0);
        var source = Entry("DoroStrike", "value");

        var game = transform.ToGame(source, Context());
        var reverted = transform.ToSource(game, Context());

        Assert.Equal("DORO_STRIKE", game.Key[0]);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void ModelIdTransformChangesOnlyItsConfiguredSegment()
    {
        var transform = ModelIdTransform.Prefixed(0, "FIRST-");

        var game = transform.ToGame(new LocEntry(["FirstModel", "unchanged", "SecondModel"], "value"), Context());

        Assert.Equal(["FIRST-FIRST_MODEL", "unchanged", "SecondModel"], game.Key);
    }

    [Fact]
    public void ModelIdTransformPreservesUppercaseSourceIdAndUnknownGameId()
    {
        var diagnostics = new DiagnosticCollection();
        var transform = ModelIdTransform.Prefixed(0, "MOD-");

        var source = transform.ToGame(Entry("EXTERNAL-ID", "value"), Context(diagnostics));
        var game = transform.ToSource(Entry("OtherId", "value"), Context(diagnostics));

        Assert.Equal("EXTERNAL-ID", source.Key[0]);
        Assert.Equal("OtherId", game.Key[0]);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ModelIdTransform.UnexpectedGameId");
    }

    [Fact]
    public void ModelIdTransformReportsOutOfRangeRuleAndLeavesEntryUnchanged()
    {
        var diagnostics = new DiagnosticCollection();
        var transform = ModelIdTransform.Prefixed(2, "MOD-");
        var entry = Entry("OnlySegment", "value");

        var result = transform.ToGame(entry, Context(diagnostics));

        Assert.Equal(entry, result);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "ModelIdTransform.KeyIndexOutOfRange");
    }

    [Fact]
    public void BaseLibAndRitsuLibFactoriesPreserveLegacyPrefixes()
    {
        var baseLib = ModelIdTransform.BaseLib(0, "myMod");
        var ritsuLib = ModelIdTransform.RitsuLib(0, "myMod", "CardModel");

        Assert.Equal("MYMOD-", ModelIdTransform.BaseLibPrefix("myMod"));
        Assert.Equal("MY_MOD_CARD_", ModelIdTransform.RitsuLibPrefix("myMod", "CardModel"));
        Assert.Equal("MYMOD-CARD_ID", baseLib.ToGame(Entry("CardId", "value"), Context()).Key[0]);
        Assert.Equal("MY_MOD_CARD_CARD_ID", ritsuLib.ToGame(Entry("CardId", "value"), Context()).Key[0]);
    }

    [Fact]
    public void AncientModelIdTransformHandlesAncientAndCharacterIdsByDialogueShape()
    {
        var transform = new AncientModelIdTransform("ANCIENT-", "CHARACTER-");
        var source = new LocEntry(["CustomAncient", "talk", "CustomCharacter", "0-0", "ancient"], "value");

        var game = transform.ToGame(source, Context());
        var reverted = transform.ToSource(game, Context());

        Assert.Equal(
            ["ANCIENT-CUSTOM_ANCIENT", "talk", "CHARACTER-CUSTOM_CHARACTER", "0-0", "ancient"],
            game.Key);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void AncientModelIdTransformReportsNonReversibilityOnlyOnce()
    {
        var diagnostics = new DiagnosticCollection();
        var transform = new AncientModelIdTransform("", "");
        var source = new LocEntry(["Custom.Ancient", "pages", "INITIAL", "description"], "value");

        transform.ToGame(source, Context(diagnostics));

        Assert.Single(diagnostics, diagnostic => diagnostic.Code == "EntryTransform.NonReversible");
    }

    [Theory]
    [InlineData("ANY")]
    [InlineData("firstVisitEver")]
    public void AncientModelIdTransformPreservesDialogueSelectors(string selector)
    {
        var transform = AncientModelIdTransform.Vanilla();
        var source = new LocEntry(["Neow", "talk", selector, "0-0", "ancient"], "value");

        var game = transform.ToGame(source, Context());
        var reverted = transform.ToSource(game, Context());

        Assert.Equal("NEOW", game.Key[0]);
        Assert.Equal(selector, game.Key[2]);
        Assert.Equal(source, reverted);
    }

    [Fact]
    public void AncientModelIdTransformSkipsCharacterTransformOutsideTalkBranch()
    {
        var transform = AncientModelIdTransform.BaseLib("myMod");
        var source = new LocEntry(["CustomAncient", "pages", "INITIAL", "description"], "value");

        var game = transform.ToGame(source, Context());

        Assert.Equal(["MYMOD-CUSTOM_ANCIENT", "pages", "INITIAL", "description"], game.Key);
    }

    [Fact]
    public void AncientModelIdTransformRitsuLibUsesAncientAndCharacterCategories()
    {
        var transform = AncientModelIdTransform.RitsuLib("myMod");
        var source = new LocEntry(["CustomAncient", "talk", "CustomCharacter", "0-0", "ancient"], "value");

        var game = transform.ToGame(source, Context());

        Assert.Equal("MY_MOD_ANCIENT_CUSTOM_ANCIENT", game.Key[0]);
        Assert.Equal("MY_MOD_CHARACTER_CUSTOM_CHARACTER", game.Key[2]);
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

    private static LocEntryTransformContext Context(DiagnosticCollection? diagnostics = null) =>
        new(new LocTablePath("zhs", "cards"), 0, LocExecutionContext.Default,
            diagnostics);

    private sealed class NonReversibleTransform : ReversibleLocEntryTransform
    {
        protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context) =>
            new(entry.Key, "game");

        protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context) =>
            new(entry.Key, "source");
    }
}
