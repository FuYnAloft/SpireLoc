using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Steps.IO;
using Xunit;

namespace SpireLoc.Core.Tests.Steps.IO;

public sealed class LocalizationDirectoryOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpireLocTests", Guid.NewGuid().ToString("N"));

    public static TheoryData<FormatOperations> Formats =>
    [
        new FormatOperations(
            ".yaml",
            root => new ReadYamlLocalizationDirectoryOperation(root),
            root => new WriteYamlLocalizationDirectoryOperation(root),
            IsFlat: false),
        new FormatOperations(
            ".toml",
            root => new ReadTomlLocalizationDirectoryOperation(root),
            root => new WriteTomlLocalizationDirectoryOperation(root),
            IsFlat: false),
        new FormatOperations(
            ".json",
            root => new ReadNestedJsonLocalizationDirectoryOperation(root),
            root => new WriteNestedJsonLocalizationDirectoryOperation(root),
            IsFlat: false),
        new FormatOperations(
            ".json",
            root => new ReadFlatJsonLocalizationDirectoryOperation(root),
            root => new WriteFlatJsonLocalizationDirectoryOperation(root),
            IsFlat: true)
    ];

    [Theory]
    [MemberData(nameof(Formats))]
    public void RoundTripsAllFormatsThroughDefaultMainSlot(FormatOperations format)
    {
        var source = format.IsFlat ? FlatBundle() : NestedBundle();
        var workspace = LocWorkspace.Empty.Set("main", source);
        var writeResult = format.CreateWriter(_root).Execute(workspace, LocExecutionContext.Default);
        var readResult = format.CreateReader(_root).Execute(LocWorkspace.Empty, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, writeResult.Status);
        Assert.Equal(LocOperationStatus.Succeeded, readResult.Status);
        AssertBundlesEqual(source, readResult.Workspace.Require<LocBundle>("main"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void WritersUseLfLineEndings(FormatOperations format)
    {
        var source = format.IsFlat ? FlatBundle() : NestedBundle();

        var result = format.CreateWriter(_root)
            .Execute(LocWorkspace.Empty.Set("main", source), LocExecutionContext.Default);
        var text = File.ReadAllText(Path.Combine(_root, "zhs", "cards" + format.Extension));

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void ReaderMergesNewTablesAndOverwritesMatchingEntryKeys()
    {
        var cards = new LocTablePath("zhs", "cards");
        var ui = new LocTablePath("zhs", "ui");
        var existing = new LocBundle(new Dictionary<LocTablePath, LocTable>
        {
            [cards] = new([
                new LocEntry(["existing"], "old"),
                new LocEntry(["keep"], "unchanged")
            ]),
            [ui] = new([new LocEntry(["title"], "ui")])
        });
        WriteFile("zhs/cards.json", """
        {
          "existing": "new",
          "added": "value"
        }
        """);

        var result = new ReadFlatJsonLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty.Set("main", existing), LocExecutionContext.Default);
        var merged = result.Workspace.Require<LocBundle>("main");

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.Equal(["new", "unchanged", "value"], merged[cards].Select(entry => entry.Value));
        Assert.Equal("ui", merged[ui].Single().Value);
    }

    [Fact]
    public void ReaderSkipsBadFileButMergesValidFilesAndReportsFailure()
    {
        WriteFile("zhs/good.json", "{ \"title\": \"valid\" }");
        WriteFile("zhs/bad.json", "{ \"count\": 1 }");

        var result = new ReadFlatJsonLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Equal("valid", result.Workspace.Require<LocBundle>("main")
            [new LocTablePath("zhs", "good")].Single().Value);
        Assert.False(result.Workspace.Require<LocBundle>("main").ContainsKey(new LocTablePath("zhs", "bad")));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LocalizationDirectory.ReadFile");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedReadersRejectNonStringLeaves(FormatOperations format)
    {
        if (format.IsFlat)
            return;

        var content = format.Extension switch
        {
            ".yaml" => "card:\n  count: 1\n",
            ".toml" => "[card]\ncount = 1\n",
            _ => "{ \"card\": { \"count\": 1 } }"
        };
        WriteFile("zhs/cards" + format.Extension, content);

        var result = format.CreateReader(_root).Execute(LocWorkspace.Empty, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Empty(result.Workspace.Require<LocBundle>("main"));
    }

    [Fact]
    public void WriterOverwritesMatchingFileAndPreservesUnrelatedFile()
    {
        WriteFile("zhs/cards.json", "{ \"title\": \"old\" }");
        WriteFile("zhs/unrelated.json", "{ \"keep\": \"yes\" }");
        var result = new WriteFlatJsonLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty.Set("main", FlatBundle()), LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        var readBack = new ReadFlatJsonLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty, LocExecutionContext.Default)
            .Workspace.Require<LocBundle>("main");
        Assert.Equal("Strike", readBack[new LocTablePath("zhs", "cards")].Single().Value);
        Assert.Equal("yes", readBack[new LocTablePath("zhs", "unrelated")].Single().Value);
    }

    [Fact]
    public void NestedWriterRejectsConflictingPathsBeforeWritingFiles()
    {
        var conflicting = new LocBundle(new Dictionary<LocTablePath, LocTable>
        {
            [new LocTablePath("zhs", "cards")] = new([
                new LocEntry(["card"], "leaf"),
                new LocEntry(["card", "title"], "nested")
            ])
        });

        var result = new WriteNestedJsonLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty.Set("main", conflicting), LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.False(Directory.Exists(_root));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LocalizationDirectory.WriteTable");
    }

    [Fact]
    public void WriterReportsMissingSourceSlot()
    {
        var result = new WriteYamlLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty, LocExecutionContext.Default);

        Assert.Equal(LocOperationStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LocalizationDirectory.WriteInput");
    }

    [Fact]
    public void TomlWriterUsesMultilineBasicStringForMultilineValues()
    {
        var result = new WriteTomlLocalizationDirectoryOperation(_root)
            .Execute(LocWorkspace.Empty.Set("main", NestedBundle()), LocExecutionContext.Default);
        var text = File.ReadAllText(Path.Combine(_root, "zhs", "cards.toml"));

        Assert.Equal(LocOperationStatus.Succeeded, result.Status);
        Assert.Contains("description = \"\"\"第一行\n第二行\"\"\"", text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void WriteFile(string relativePath, string text)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static LocBundle NestedBundle() => new(new Dictionary<LocTablePath, LocTable>
    {
        [new LocTablePath("zhs", "cards")] = new([
            new LocEntry(["Card", "title"], "打击"),
            new LocEntry(["Card", "description"], "第一行\n第二行")
        ])
    });

    private static LocBundle FlatBundle() => new(new Dictionary<LocTablePath, LocTable>
    {
        [new LocTablePath("zhs", "cards")] = new([new LocEntry(["MOD-CARD", "title"], "Strike")])
    });

    private static void AssertBundlesEqual(LocBundle expected, LocBundle actual)
    {
        Assert.Equal(expected.Keys.OrderBy(path => path.ToString()), actual.Keys.OrderBy(path => path.ToString()));
        foreach (var path in expected.Keys)
        {
            var expectedEntries = expected[path];
            var actualEntries = actual[path];
            Assert.Equal(expectedEntries.Count, actualEntries.Count);
            for (var index = 0; index < expectedEntries.Count; index++)
                Assert.Equal(expectedEntries[index], actualEntries[index]);
        }
    }

    public sealed record FormatOperations(
        string Extension,
        Func<string, ILocOperation> CreateReader,
        Func<string, ILocOperation> CreateWriter,
        bool IsFlat);
}
