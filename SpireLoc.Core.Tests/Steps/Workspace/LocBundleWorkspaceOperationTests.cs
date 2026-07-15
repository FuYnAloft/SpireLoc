using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.Workspace;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.Workspace;

public sealed class LocBundleWorkspaceOperationTests
{
    [Fact]
    public void TablePartitionMatchesWholeTablesAcrossLanguages()
    {
        var source = Bundle(
            ("zhs", "cards", [Entry("Card", "title")]),
            ("eng", "cards", [Entry("Card", "title")]),
            ("zhs", "relics", [Entry("Relic", "title")]));
        var workspace = LocWorkspace.Empty.Set("source", source);
        var operation = PartitionLocBundleOperation.ByTable(
            ["cards"], "source", "selected", "remaining");

        var result = operation.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        var selected = result.Workspace.Require<LocBundle>("selected");
        var remaining = result.Workspace.Require<LocBundle>("remaining");
        Assert.Equal(2, selected.Count);
        Assert.Contains(new LocTablePath("zhs", "cards"), selected.Keys);
        Assert.Contains(new LocTablePath("eng", "cards"), selected.Keys);
        Assert.Single(remaining);
        Assert.Contains(new LocTablePath("zhs", "relics"), remaining.Keys);
    }

    [Fact]
    public void LanguagePartitionMatchesAnyConfiguredLanguage()
    {
        var source = Bundle(
            ("zhs", "cards", [Entry("Card", "title")]),
            ("jpn", "relics", [Entry("Relic", "title")]),
            ("eng", "cards", [Entry("Card", "title")]));
        var operation = PartitionLocBundleOperation.ByLanguage(
            ["zhs", "jpn"], "main", "matched", "unmatched");

        var result = operation.Execute(
            LocWorkspace.Empty.Set("main", source),
            LocExecutionContext.Default);

        Assert.Equal(2, result.Workspace.Require<LocBundle>("matched").Count);
        Assert.Single(result.Workspace.Require<LocBundle>("unmatched"));
    }

    [Fact]
    public void RegexPartitionMatchesEntryPathsWithOrSemanticsAndKeepsEmptyTablesUnmatched()
    {
        var source = Bundle(
            ("zhs", "cards", [
                Entry("CustomCard", "title"),
                Entry("OtherCard", "description"),
                Entry("OtherCard", "title"),
            ]),
            ("eng", "cards", [Entry("CustomCard", "title")]),
            ("zhs", "empty", []));
        var operation = PartitionLocBundleOperation.ByRegex(
            [@"^zhs/cards/CustomCard\.", @"description$"],
            "main",
            "matched",
            "unmatched");

        var result = operation.Execute(
            LocWorkspace.Empty.Set("main", source),
            LocExecutionContext.Default);

        var matched = result.Workspace.Require<LocBundle>("matched");
        var unmatched = result.Workspace.Require<LocBundle>("unmatched");
        Assert.Equal(2, matched[new LocTablePath("zhs", "cards")].Count);
        Assert.Single(unmatched[new LocTablePath("zhs", "cards")]);
        Assert.Single(unmatched[new LocTablePath("eng", "cards")]);
        Assert.Empty(unmatched[new LocTablePath("zhs", "empty")]);
    }

    [Fact]
    public void PartitionRejectsInvalidConfigurationAndPreservesWorkspaceOnMissingInput()
    {
        Assert.Throws<ArgumentException>(() =>
            PartitionLocBundleOperation.ByTable([], "main", "matched", "unmatched"));
        Assert.ThrowsAny<ArgumentException>(() =>
            PartitionLocBundleOperation.ByRegex(["["], "main", "matched", "unmatched"));
        Assert.Throws<ArgumentException>(() =>
            PartitionLocBundleOperation.ByLanguage(["zhs"], "main", "same", "same"));

        var workspace = LocWorkspace.Empty.Set("existing", Bundle(("zhs", "cards", [Entry("Card")])));
        var operation = PartitionLocBundleOperation.ByTable(
            ["cards"], "missing", "matched", "unmatched");

        var result = operation.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Equal("LocBundlePartition.Input", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void MergeUsesExplicitSourceOrderAndLaterEntriesOverrideEarlierOnes()
    {
        var first = Bundle(
            ("zhs", "cards", [EntryWithValue("Card", "old"), EntryWithValue("FirstOnly", "first")]),
            ("zhs", "relics", [EntryWithValue("Relic", "relic")]));
        var second = Bundle(
            ("zhs", "cards", [EntryWithValue("Card", "new"), EntryWithValue("SecondOnly", "second")]));
        var workspace = LocWorkspace.Empty.Set("first", first).Set("second", second);
        var operation = new MergeLocBundlesOperation(["first", "second"], "result");

        var result = operation.Execute(workspace, LocExecutionContext.Default);

        var merged = result.Workspace.Require<LocBundle>("result");
        Assert.Equal(["new", "first", "second"],
            merged[new LocTablePath("zhs", "cards")].Select(static entry => entry.Value));
        Assert.Equal("relic", merged[new LocTablePath("zhs", "relics")].Single().Value);
    }

    [Fact]
    public void MergeWithoutSourcesUsesAllBundlesInOrdinalSlotOrderAndIgnoresOtherArtifacts()
    {
        var workspace = LocWorkspace.Empty
            .Set("z-last", Bundle(("zhs", "cards", [EntryWithValue("Card", "z")])))
            .Set("other", new TestArtifact())
            .Set("a-first", Bundle(("zhs", "cards", [EntryWithValue("Card", "a")])));
        var operation = new MergeLocBundlesOperation(null, "main");

        var result = operation.Execute(workspace, LocExecutionContext.Default);

        Assert.Equal(
            "z",
            result.Workspace.Require<LocBundle>("main")[new LocTablePath("zhs", "cards")].Single().Value);
    }

    [Fact]
    public void MergeFailsWithoutBundlesOrWhenAnExplicitSourceIsInvalid()
    {
        var noBundles = LocWorkspace.Empty.Set("other", new TestArtifact());
        var noSourcesResult = new MergeLocBundlesOperation(null, "main")
            .Execute(noBundles, LocExecutionContext.Default);
        Assert.Equal(LocOperationStatus.Failed, noSourcesResult.Status);
        Assert.Same(noBundles, noSourcesResult.Workspace);
        Assert.Equal("LocBundleMerge.NoSources", Assert.Single(noSourcesResult.Diagnostics).Code);

        var wrongType = new MergeLocBundlesOperation(["other"], "main")
            .Execute(noBundles, LocExecutionContext.Default);
        Assert.Equal(LocOperationStatus.Failed, wrongType.Status);
        Assert.Same(noBundles, wrongType.Workspace);
        Assert.Equal("LocBundleMerge.Input", Assert.Single(wrongType.Diagnostics).Code);

        Assert.Throws<ArgumentException>(() =>
            new MergeLocBundlesOperation(["source", "source"], "main"));
    }

    private static LocBundle Bundle(params (string Language, string Table, LocEntry[] Entries)[] tables) =>
        new(tables.Select(static table => KeyValuePair.Create(
            new LocTablePath(table.Language, table.Table),
            new LocTable(table.Entries))));

    private static LocEntry Entry(params string[] key) => new(key, "value");

    private static LocEntry EntryWithValue(string key, string value) => new([key], value);

    private sealed class TestArtifact : ILocArtifact;
}
