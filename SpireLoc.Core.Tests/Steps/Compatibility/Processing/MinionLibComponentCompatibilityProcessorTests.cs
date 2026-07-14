    using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Compatibility.Processing;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Compatibility.Processing;

public sealed class MinionLibComponentCompatibilityProcessorTests
{
    [Fact]
    public void ToGameMovesNamespacedComponentsIntoCardsForEachLanguage()
    {
        var source = Bundle(
            ("zhs", "cards", [Entry(["MYMOD-CARD", "title"], "card")]),
            ("zhs", "components", [Entry(["StrikeComponent", "prefix"], "strike")]),
            ("eng", "components", [Entry(["DefendComponent", "prefix"], "defend")]),
            ("zhs", "settings_ui", [Entry(["setting"], "unchanged")]));

        var result = new MinionLibComponentToGameProcessor("myMod").Process(source);

        Assert.False(result.ContainsKey(Path("zhs", "components")));
        Assert.False(result.ContainsKey(Path("eng", "components")));
        Assert.Equal(["MYMOD-CARD", "title"], result[Path("zhs", "cards")][0].Key);
        Assert.Equal(["myMod", "StrikeComponent", "prefix"], result[Path("zhs", "cards")][1].Key);
        Assert.Equal(["myMod", "DefendComponent", "prefix"], result[Path("eng", "cards")][0].Key);
        Assert.Equal("unchanged", result[Path("zhs", "settings_ui")][0].Value);
    }

    [Fact]
    public void ToSourceExtractsLowercaseNamespaceEntriesAndReplacesComponentsTable()
    {
        var source = Bundle(
            ("zhs", "cards", [
                Entry(["MYMOD-CARD", "title"], "card"),
                Entry(["myMod", "StrikeComponent", "prefix"], "strike"),
                Entry(["UPPER_COMPONENT", "prefix"], "upper"),
            ]),
            ("zhs", "components", [Entry(["OldComponent", "prefix"], "old")]));

        var result = new MinionLibComponentToSourceProcessor("myMod").Process(source);

        Assert.Equal(2, result[Path("zhs", "cards")].Count);
        Assert.Equal(["MYMOD-CARD", "title"], result[Path("zhs", "cards")][0].Key);
        Assert.Equal(["UPPER_COMPONENT", "prefix"], result[Path("zhs", "cards")][1].Key);
        Assert.Collection(result[Path("zhs", "components")], component =>
        {
            Assert.Equal(["StrikeComponent", "prefix"], component.Key);
            Assert.Equal("strike", component.Value);
        });
    }

    [Fact]
    public void ToGameAndToSourceRoundTripSourceTables()
    {
        var source = Bundle(
            ("zhs", "cards", [Entry(["MYMOD-CARD", "title"], "card")]),
            ("zhs", "components", [
                Entry(["Invocation", "prefix"], "invoke"),
                Entry(["Mode", "selectionScreenPrompt"], "select"),
            ]));

        var game = new MinionLibComponentToGameProcessor("myMod").Process(source);
        var reverted = new MinionLibComponentToSourceProcessor("myMod").Process(game);

        Assert.Equal(source[Path("zhs", "cards")], reverted[Path("zhs", "cards")]);
        Assert.Equal(source[Path("zhs", "components")], reverted[Path("zhs", "components")]);
    }

    [Fact]
    public void ToSourceReportsUnexpectedComponentNamespaceAndPreservesEntry()
    {
        var diagnostics = new DiagnosticCollection();
        var source = Bundle(
            ("zhs", "cards", [Entry(["otherMod", "StrikeComponent", "prefix"], "strike")]));

        var result = new MinionLibComponentToSourceProcessor("myMod").Process(source, diagnostics);

        Assert.Empty(result[Path("zhs", "cards")]);
        Assert.Equal(
            ["otherMod", "StrikeComponent", "prefix"],
            result[Path("zhs", "components")][0].Key);
        Assert.Contains(diagnostics,
            diagnostic => diagnostic.Code == "MinionLibComponentIdTransform.UnexpectedNamespace");
    }

    private static LocBundle Bundle(params (string Language, string Table, LocEntry[] Entries)[] tables) =>
        new(tables.Select(table =>
            KeyValuePair.Create(Path(table.Language, table.Table), new LocTable(table.Entries))));

    private static LocEntry Entry(string[] key, string value) => new(key, value);

    private static LocTablePath Path(string language, string table) => new(language, table);
}
