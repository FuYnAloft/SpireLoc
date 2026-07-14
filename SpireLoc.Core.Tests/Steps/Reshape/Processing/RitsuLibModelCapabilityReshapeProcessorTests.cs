using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Processing.ModelIds;
using SpireLoc.Core.Steps.Reshape.Processing;
using SpireLoc.Core.Steps.Support;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Reshape.Processing;

public sealed class RitsuLibModelCapabilityReshapeProcessorTests
{
    [Fact]
    public void MergeMovesBothCapabilityTableSpellingsIntoCardsInBundleOrder()
    {
        var source = Bundle(
            ("zhs", "cards", [Entry(["MY_MOD_CARD_CARD"], "card")]),
            ("zhs", "model_capabilities", [Entry(["MY_MOD_MODEL_CAPABILITY_FIRST"], "first")]),
            ("zhs", "modelcapabilities", [Entry(["MY_MOD_MODELCAPABILITY_SECOND"], "second")]),
            ("eng", "modelcapabilities", [Entry(["MY_MOD_MODELCAPABILITY_ENGLISH"], "english")]));

        var result = new RitsuLibModelCapabilityMergeProcessor().Process(source);

        Assert.False(result.ContainsKey(Path("zhs", "model_capabilities")));
        Assert.False(result.ContainsKey(Path("zhs", "modelcapabilities")));
        Assert.Equal(
            ["MY_MOD_CARD_CARD", "MY_MOD_MODEL_CAPABILITY_FIRST", "MY_MOD_MODELCAPABILITY_SECOND"],
            result[Path("zhs", "cards")].Select(entry => entry.Key[0]));
        Assert.Equal("MY_MOD_MODELCAPABILITY_ENGLISH", result[Path("eng", "cards")][0].Key[0]);
    }

    [Fact]
    public void SplitWithModIdExtractsOnlyThatModsExactPrefixes()
    {
        var source = Bundle(("zhs", "cards", [
            Entry(["MY_MOD_CARD_CARD"], "card"),
            Entry(["MY_MOD_MODEL_CAPABILITY_FIRST"], "first"),
            Entry(["MY_MOD_MODELCAPABILITY_SECOND"], "second"),
            Entry(["OTHER_MOD_MODEL_CAPABILITY_OTHER"], "other"),
        ]));

        var result = new RitsuLibModelCapabilitySplitProcessor("my-mod").Process(source);

        Assert.Equal(
            ["MY_MOD_CARD_CARD", "OTHER_MOD_MODEL_CAPABILITY_OTHER"],
            result[Path("zhs", "cards")].Select(entry => entry.Key[0]));
        Assert.Equal("MY_MOD_MODEL_CAPABILITY_FIRST", result[Path("zhs", "model_capabilities")][0].Key[0]);
        Assert.Equal("MY_MOD_MODELCAPABILITY_SECOND", result[Path("zhs", "modelcapabilities")][0].Key[0]);
    }

    [Fact]
    public void SplitWithoutModIdUsesCategoryTokensAndReportsWarningOnce()
    {
        var diagnostics = new DiagnosticCollection();
        var source = Bundle(("zhs", "cards", [
            Entry(["FIRST_MOD_MODEL_CAPABILITY_FIRST"], "first"),
            Entry(["SECOND_MOD_MODELCAPABILITY_SECOND"], "second"),
            Entry(["FIRST_MOD_CARD_CARD"], "card"),
        ]));

        var result = new RitsuLibModelCapabilitySplitProcessor().Process(source, diagnostics);

        Assert.Equal("FIRST_MOD_MODEL_CAPABILITY_FIRST", result[Path("zhs", "model_capabilities")][0].Key[0]);
        Assert.Equal("SECOND_MOD_MODELCAPABILITY_SECOND", result[Path("zhs", "modelcapabilities")][0].Key[0]);
        Assert.Equal("FIRST_MOD_CARD_CARD", result[Path("zhs", "cards")][0].Key[0]);
        Assert.Single(diagnostics,
            diagnostic => diagnostic.Code == "RitsuLibModelCapabilitySplit.HeuristicDetection" &&
                          diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void HeuristicWarningFlowsThroughOperationResultWithoutFailingStep()
    {
        var source = Bundle(("zhs", "cards", [
            Entry(["MY_MOD_MODELCAPABILITY_FIRST"], "first"),
        ]));
        var workspace = LocWorkspace.Empty.Set("main", source);
        var step = new UnaryLocBundleProcessorStep(new RitsuLibModelCapabilitySplitProcessor());

        var result = step.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == "RitsuLibModelCapabilitySplit.HeuristicDetection" &&
                          diagnostic.Severity == DiagnosticSeverity.Warning);
        Assert.True(result.Workspace.Require<LocBundle>("main")
            .ContainsKey(Path("zhs", "modelcapabilities")));
    }

    [Fact]
    public void ReshapeAndModelIdProcessorsRoundTripSourceLayout()
    {
        var source = Bundle(
            ("zhs", "cards", [Entry(["CustomCard", "title"], "card")]),
            ("zhs", "model_capabilities", [Entry(["FirstCapability", "title"], "first")]),
            ("zhs", "modelcapabilities", [Entry(["SecondCapability", "title"], "second")]));

        var gameIds = new RitsuLibModelIdProcessor(ModelIdDirection.ToGame, "my-mod").Process(source);
        var game = new RitsuLibModelCapabilityMergeProcessor().Process(gameIds);
        var sourceTables = new RitsuLibModelCapabilitySplitProcessor("my-mod").Process(game);
        var reverted = new RitsuLibModelIdProcessor(ModelIdDirection.ToSource, "my-mod").Process(sourceTables);

        Assert.False(game.ContainsKey(Path("zhs", "model_capabilities")));
        Assert.False(game.ContainsKey(Path("zhs", "modelcapabilities")));
        Assert.Equal(source[Path("zhs", "cards")], reverted[Path("zhs", "cards")]);
        Assert.Equal(source[Path("zhs", "model_capabilities")], reverted[Path("zhs", "model_capabilities")]);
        Assert.Equal(source[Path("zhs", "modelcapabilities")], reverted[Path("zhs", "modelcapabilities")]);
    }

    private static LocBundle Bundle(params (string Language, string Table, LocEntry[] Entries)[] tables) =>
        new(tables.Select(table =>
            KeyValuePair.Create(Path(table.Language, table.Table), new LocTable(table.Entries))));

    private static LocEntry Entry(string[] key, string value) => new(key, value);

    private static LocTablePath Path(string language, string table) => new(language, table);
}
