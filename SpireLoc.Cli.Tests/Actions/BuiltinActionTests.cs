using SpireLoc.Cli.Actions;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;
using Xunit;

namespace SpireLoc.Cli.Tests.Actions;

public sealed class BuiltinActionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SpireLocBuiltinActionTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void CatalogDiscoversDebugOnlyResourcesWithDescription()
    {
        var loader = new ActionYamlLoader();
        var catalog = new BuiltinActionCatalog(loader, typeof(ActionExpander).Assembly);

#if DEBUG
        var entry = Assert.Single(catalog.Entries, static item => item.Name == "debug-only-empty");
        Assert.Equal("Empty debug action used to verify builtin action discovery.", entry.Description);
        Assert.True(catalog.TryLoad("debug-only-empty", out var document));
        Assert.True(document.IsBuiltin);
        Assert.Equal("builtin:debug-only-empty", document.FilePath);
#else
        Assert.DoesNotContain(catalog.Entries, static item => item.Name == "debug-only-empty");
        Assert.False(catalog.TryLoad("debug-only-empty", out _));
#endif
    }

#if DEBUG
    [Fact]
    public void BuiltinNamesRequireAnExactBareNameMatch()
    {
        Write("debug-only-empty.yml", "steps:\n  - model-id:\n      kind: vanilla\n");
        var expander = Expander();

        Assert.Empty(expander.ExpandAction("debug-only-empty", [], _root));
        var fileInvocation = Assert.Single(expander.ExpandAction("debug-only-empty.yml", [], _root));
        Assert.Equal(["model-id", "vanilla"], fileInvocation.FactoryPath);

        Assert.Throws<CliException>(() => expander.ExpandAction("./debug-only-empty", [], _root));
        Assert.Throws<CliException>(() => expander.ExpandAction("DEBUG-ONLY-EMPTY", [], _root));
    }

    [Fact]
    public void UsesAppliesTheSameBuiltinNameResolutionRules()
    {
        Write("file-child.yml", "steps:\n  - model-id:\n      kind: vanilla\n");
        Write("builtin-parent.yml", "steps:\n  - uses: debug-only-empty\n");
        Write("file-parent.yml", "steps:\n  - uses: file-child.yml\n");
        var expander = Expander();

        Assert.Empty(expander.ExpandAction("builtin-parent.yml", [], _root));
        var invocation = Assert.Single(expander.ExpandAction("file-parent.yml", [], _root));
        Assert.Equal(["model-id", "vanilla"], invocation.FactoryPath);
    }

    [Fact]
    public void BuiltinTemplatePathsUseSentinelsAndCatalogExposesParameterDescriptions()
    {
        var loader = new ActionYamlLoader();
        var catalog = new BuiltinActionCatalog(loader, typeof(ActionExpander).Assembly);
        var entry = Assert.Single(catalog.Entries, static item => item.Name == "debug-only-context");
        var parameter = Assert.Single(entry.Parameters);
        Assert.Equal("Value appended to the sentinel directory.", parameter.Description);
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);
        var expander = new ActionExpander(loader, catalog, registry);

        var invocations = expander.ExpandAction("debug-only-context", ["tail"], _root);

        Assert.Collection(
            invocations,
            invocation => Assert.Equal(
                "builtin-missing-action-path",
                invocation.Arguments["path"].FormatInvariant()),
            invocation => Assert.Equal(
                "builtin-missing-action-dir/tail",
                invocation.Arguments["path"].FormatInvariant()));
    }
#endif

    [Fact]
    public void LoaderPreservesActionAndParameterDescriptions()
    {
        Write("described.yaml", """
            description: Convert localization for a mod.
            parameters:
              mod-id:
                description: The mod identifier used in model IDs.
                type: string
                position: 0
              reversed:
                description: Convert game localization back to source form.
                type: bool
                flag: true
                default: false
            steps: []
            """);

        var document = new ActionYamlLoader().Load(Path.Combine(_root, "described.yaml"));

        Assert.Equal("Convert localization for a mod.", document.Description);
        Assert.Collection(
            document.Parameters,
            parameter => Assert.Equal("The mod identifier used in model IDs.", parameter.Description),
            parameter => Assert.Equal("Convert game localization back to source form.", parameter.Description));
    }

    [Fact]
    public void ListHelpSortsBuiltinNamesAndShowsDescriptions()
    {
        var actions = new BuiltinActionInfo[]
        {
            new("z-last", null, []),
            new("a-first", "The first action.", []),
        };
        var output = new StringWriter();

        ActionHelpFormatter.WriteList(output, actions);

        Assert.Equal(
            """
            Built-in actions:
              a-first  The first action.
              z-last

            """.ReplaceLineEndings(),
            output.ToString());
    }

    [Fact]
    public void ActionHelpShowsDescriptionsPositionsFlagsAndDefaults()
    {
        Write("help.yaml", """
            description: Convert localization for a mod.
            parameters:
              mod-id:
                description: The mod identifier.
                type: string
                position: 0
              reversed:
                description: Reverse the conversion.
                type: bool
                flag: true
                default: false
            steps: []
            """);
        var document = new ActionYamlLoader().Load(Path.Combine(_root, "help.yaml"));
        var output = new StringWriter();

        ActionHelpFormatter.WriteAction(output, "convert", document);

        var help = output.ToString();
        Assert.Contains("Usage: spireloc action run convert <mod-id> [options]", help, StringComparison.Ordinal);
        Assert.Contains("Convert localization for a mod.", help, StringComparison.Ordinal);
        Assert.Contains("<mod-id>, --mod-id <string>", help, StringComparison.Ordinal);
        Assert.Contains("The mod identifier. (required)", help, StringComparison.Ordinal);
        Assert.Contains("--reversed", help, StringComparison.Ordinal);
        Assert.Contains("Reverse the conversion. (default: false)", help, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static ActionExpander Expander()
    {
        var loader = new ActionYamlLoader();
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);
        return new ActionExpander(
            loader,
            new BuiltinActionCatalog(loader, typeof(ActionExpander).Assembly),
            registry);
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
