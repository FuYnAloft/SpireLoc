using SpireLoc.Cli.Actions;
using SpireLoc.Cli.Pipeline;
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
    public void CatalogDiscoversBaseLibAndRitsuLibActions()
    {
        var catalog = new BuiltinActionCatalog(new ActionYamlLoader(), typeof(ActionExpander).Assembly);

        Assert.Contains(catalog.Entries,
            action => action.Name == "baselib" && action.Parameters.Count == 8);
        Assert.Contains(catalog.Entries,
            action => action.Name == "ritsulib" && action.Parameters.Count == 9);
    }

    [Fact]
    public void BaseLibDefaultsExpandToSourceToGamePipeline()
    {
        var invocations = ExpandAndCompile("baselib", ["MyMod", "./source", "./game"]);

        Assert.Collection(
            invocations,
            invocation =>
            {
                Assert.Equal(["input", "yaml"], invocation.FactoryPath);
                Assert.Equal("./source", Value(invocation, "path"));
            },
            invocation =>
            {
                Assert.Equal(["model-id", "baselib"], invocation.FactoryPath);
                Assert.Equal("MyMod", Value(invocation, "namespace-top"));
            },
            invocation =>
            {
                Assert.Equal(["output", "flat-json"], invocation.FactoryPath);
                Assert.Equal("./game", Value(invocation, "path"));
            });
    }

    [Fact]
    public void ReversedBaseLibWithMinionLibUsesGameInputAndNamespaceFallback()
    {
        var invocations = ExpandAndCompile("baselib", [
            "MyMod", "./source", "./game",
            "--source-format", "toml",
            "--game-format", "nested-json",
            "--minionlib-components",
            "--reversed",
        ]);

        Assert.Collection(
            invocations,
            invocation =>
            {
                Assert.Equal(["input", "nested-json"], invocation.FactoryPath);
                Assert.Equal("./game", Value(invocation, "path"));
            },
            invocation =>
            {
                Assert.Equal(["compat", "minionlib-component", "to-source"], invocation.FactoryPath);
                Assert.Equal("MyMod", Value(invocation, "namespace-top"));
            },
            invocation =>
            {
                Assert.Equal(["model-id", "baselib"], invocation.FactoryPath);
                Assert.Equal("true", Value(invocation, "reversed"));
            },
            invocation =>
            {
                Assert.Equal(["output", "toml"], invocation.FactoryPath);
                Assert.Equal("./source", Value(invocation, "path"));
            });
    }

    [Fact]
    public void RitsuLibDefaultsIncludeModelCapabilityMerge()
    {
        var invocations = ExpandAndCompile("ritsulib", ["MyMod", "./source", "./game"]);

        Assert.Equal(
            [
                "input yaml",
                "model-id ritsulib",
                "reshape ritsulib-model-capability merge",
                "output flat-json",
            ],
            invocations.Select(invocation => string.Join(' ', invocation.FactoryPath)));
        Assert.Equal("MyMod", Value(invocations[1], "mod-id"));
    }

    [Fact]
    public void RitsuLibMinionLibNamespaceDefaultsToModId()
    {
        var invocations = ExpandAndCompile("ritsulib", [
            "MyMod", "./source", "./game", "--minionlib-components",
        ]);

        var compatibility = Assert.Single(invocations,
            invocation => invocation.FactoryPath.SequenceEqual(
                ["compat", "minionlib-component", "to-game"]));
        Assert.Equal("MyMod", Value(compatibility, "namespace-top"));
    }

    [Fact]
    public void ReversedRitsuLibExpandsCompatibilityStepsInReverseOrder()
    {
        var invocations = ExpandAndCompile("ritsulib", [
            "MyMod", "./source", "./game",
            "--minionlib-components",
            "--minionlib-namespace-top", "ComponentsNs",
            "--reversed",
        ]);

        Assert.Equal(
            [
                "input flat-json",
                "compat minionlib-component to-source",
                "reshape ritsulib-model-capability split",
                "model-id ritsulib",
                "output yaml",
            ],
            invocations.Select(invocation => string.Join(' ', invocation.FactoryPath)));
        Assert.Equal("ComponentsNs", Value(invocations[1], "namespace-top"));
        Assert.Equal("MyMod", Value(invocations[2], "mod-id"));
        Assert.Equal("true", Value(invocations[3], "reversed"));
    }

    [Fact]
    public void RitsuLibCanDisableModelCapabilityHandling()
    {
        var invocations = ExpandAndCompile("ritsulib", [
            "MyMod", "./source", "./game", "--disable-model-capabilities",
        ]);

        Assert.Equal(
            ["input yaml", "model-id ritsulib", "output flat-json"],
            invocations.Select(invocation => string.Join(' ', invocation.FactoryPath)));
    }

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
                invocation.Arguments["path"].Values.Single().FormatInvariant()),
            invocation => Assert.Equal(
                "builtin-missing-action-dir/tail",
                invocation.Arguments["path"].Values.Single().FormatInvariant()));
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
            Directory.Delete(_root, true);
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

    private IReadOnlyList<OperationInvocationSpec> ExpandAndCompile(
        string action,
        IReadOnlyList<string> arguments)
    {
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);
        var loader = new ActionYamlLoader();
        var expander = new ActionExpander(
            loader,
            new BuiltinActionCatalog(loader, typeof(ActionExpander).Assembly),
            registry);
        var invocations = expander.ExpandAction(action, arguments, _root);
        _ = new OperationCompiler(registry).Compile(invocations);
        return invocations;
    }

    private static string Value(OperationInvocationSpec invocation, string name) =>
        invocation.Arguments[name].Values.Single().FormatInvariant();

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
