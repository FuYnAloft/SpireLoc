using System.Text.Json;
using SpireLoc.Cli.Actions;
using SpireLoc.Cli.Pipe;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Steps.Processing.ModelIds;
using SpireLoc.Core.Steps.Support;
using Xunit;

namespace SpireLoc.Cli.Tests.Actions;

public sealed class ActionSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpireLocActionTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RunsActionWithNestedUsesTypedParametersAndActionDirectoryVariables()
    {
        Write("source/zhs/cards.yaml", "CustomCard:\n  title: Test\n");
        Write("model.yaml", """
            parameters:
              mod-id:
                type: string
                position: 0
              reversed:
                type: bool
                flag: true
                default: false
            steps:
              - model-id:
                  kind: [ritsulib]
                  mod-id: $(mod-id)
                  reversed: $(reversed)
            """);
        Write("forward.yaml", """
            version: 1
            parameters:
              mod-id:
                type: string
                position: 0
              write-output:
                type: bool
                default: "true"
            steps:
              - input:
                  kind: yaml
                  path: $(ActionDir)/source
              - uses: ./model.yaml
                with:
                  mod-id: $(mod-id)
                  reversed: false
              - if: $(write-output)
                output:
                  kind: flat-json
                  path: $(ActionDir)/game
            """);
        var registry = CoreRegistry();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = new ActionCommand(registry, output, error)
            .Run(Path.Combine(_root, "forward.yaml"), ["TestMod"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "game", "zhs", "cards.json")));
        Assert.Equal("Test", document.RootElement.GetProperty("TEST_MOD_CARD_CUSTOM_CARD.title").GetString());
    }

    [Fact]
    public void PipeActionExpandsInPlaceAndBindsFlagFromTargetSchema()
    {
        Write("game/zhs/cards.json", """
            {
              "TEST_MOD_CARD_CUSTOM_CARD.title": "Test"
            }
            """);
        Write("model.yaml", """
            parameters:
              mod-id:
                type: string
                position: 0
              reversed:
                type: bool
                flag: true
                default: false
            steps:
              - model-id:
                  kind: ritsulib
                  mod-id: $(mod-id)
                  reversed: $(reversed)
            """);
        var registry = CoreRegistry();
        var error = new StringWriter();

        var exitCode = new PipeCommand(registry, new StringWriter(), error).Run([
            "--input", "flat-json", Path.Combine(_root, "game"),
            "--action", Path.Combine(_root, "model.yaml"), "TestMod", "--reversed",
            "--output", "yaml", Path.Combine(_root, "source"),
        ]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("CustomCard:", File.ReadAllText(Path.Combine(_root, "source", "zhs", "cards.yaml")));
    }

    [Fact]
    public void QuotedBooleanAndExactBooleanTemplateRemainValidForOperationBinding()
    {
        Write("quoted.yaml", """
            parameters:
              reversed:
                type: bool
                default: "true"
            steps:
              - model-id:
                  kind: vanilla
                  reversed: $(reversed)
            """);
        var registry = CoreRegistry();
        var invocations = Expander(registry).ExpandAction(
            Path.Combine(_root, "quoted.yaml"), [], _root);

        var operation = Assert.Single(new OperationCompiler(registry).Compile(invocations));
        var step = Assert.IsType<UnaryLocBundleProcessorStep>(operation);
        var processor = Assert.IsType<VanillaModelIdProcessor>(step.Processor);
        Assert.Equal(ModelIdDirection.ToSource, processor.Direction);
    }

    [Fact]
    public void IntegerParametersSupportCliStringsAndInvariantEmbeddedExpansion()
    {
        Write("integer.yaml", """
            parameters:
              count:
                type: int
                position: 0
            steps:
              - input:
                  kind: yaml
                  path: value-$(count)
            """);
        var registry = CoreRegistry();

        var invocations = Expander(registry).ExpandAction(
            Path.Combine(_root, "integer.yaml"), ["42"], _root);

        Assert.Equal("value-42", invocations.Single().Arguments["path"].FormatInvariant());
    }

    [Fact]
    public void KindStringWithWhitespaceRemainsOnePathSegment()
    {
        Write("kind.yaml", """
            steps:
              - model-id:
                  kind: "ritsulib extra"
                  mod-id: TestMod
            """);
        var registry = CoreRegistry();

        var invocation = Assert.Single(Expander(registry).ExpandAction(
            Path.Combine(_root, "kind.yaml"), [], _root));

        Assert.Equal(["model-id", "ritsulib extra"], invocation.FactoryPath);
        Assert.Throws<CliException>(() => new OperationCompiler(registry).Compile([invocation]));
    }

    [Fact]
    public void FalseUsesIsRemovedWithoutLoadingItsTargetAndStructuredConditionsExpandEarly()
    {
        Write("conditions.yaml", """
            parameters:
              enabled:
                type: bool
                default: false
              format:
                type: string
                default: flat-json
            steps:
              - if: $(enabled)
                uses: ./does-not-exist.yaml
              - if:
                  equals: [$(format), flat-json]
                model-id:
                  kind: vanilla
              - if:
                  not: $(enabled)
                model-id:
                  kind: vanilla
              - if:
                  all:
                    - equals: [$(format), flat-json]
                    - not: $(enabled)
                model-id:
                  kind: vanilla
              - if:
                  any:
                    - $(enabled)
                    - equals: [$(format), flat-json]
                model-id:
                  kind: vanilla
            """);
        var registry = CoreRegistry();

        var invocations = Expander(registry).ExpandAction(
            Path.Combine(_root, "conditions.yaml"), [], _root);

        Assert.Equal(4, invocations.Count);
    }

    [Fact]
    public void UsesDoesNotImplicitlyInheritCallerParameters()
    {
        Write("child.yaml", """
            parameters:
              mod-id:
                type: string
            steps: []
            """);
        Write("parent.yaml", """
            parameters:
              mod-id:
                type: string
                position: 0
            steps:
              - uses: ./child.yaml
            """);
        var registry = CoreRegistry();

        var exception = Assert.Throws<CliException>(() => Expander(registry).ExpandAction(
            Path.Combine(_root, "parent.yaml"), ["TestMod"], _root));

        Assert.Contains("Missing required action parameter 'mod-id'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateEscapeIsLiteralAndUnknownVariablesFail()
    {
        Write("escaped.yaml", """
            steps:
              - model-id:
                  kind: ritsulib
                  mod-id: $$(literal)
            """);
        Write("unknown.yaml", """
            steps:
              - model-id:
                  kind: ritsulib
                  mod-id: $(missing)
            """);
        var registry = CoreRegistry();
        var escaped = Expander(registry).ExpandAction(Path.Combine(_root, "escaped.yaml"), [], _root);

        Assert.Equal("$(literal)", escaped.Single().Arguments["mod-id"].FormatInvariant());
        var exception = Assert.Throws<CliException>(() => Expander(registry).ExpandAction(
            Path.Combine(_root, "unknown.yaml"), [], _root));
        Assert.Contains("Unknown template variable 'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedOperationErrorsContainCompleteActionChain()
    {
        Write("child.yaml", """
            steps:
              - missing-operation: {}
            """);
        Write("parent.yaml", """
            steps:
              - uses: ./child.yaml
            """);
        var registry = CoreRegistry();
        var invocations = Expander(registry).ExpandAction(Path.Combine(_root, "parent.yaml"), [], _root);

        var exception = Assert.Throws<CliException>(() => new OperationCompiler(registry).Compile(invocations));

        Assert.Contains("parent.yaml -> child.yaml -> --missing-operation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectsActionCyclesWithCallChain()
    {
        Write("a.yaml", "steps:\n  - uses: ./b.yaml\n");
        Write("b.yaml", "steps:\n  - uses: ./a.yaml\n");
        var registry = CoreRegistry();

        var exception = Assert.Throws<CliException>(() => Expander(registry).ExpandAction(
            Path.Combine(_root, "a.yaml"), [], _root));

        Assert.Contains("a.yaml -> b.yaml -> a.yaml", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesMaximumActionDepth()
    {
        for (var index = 0; index <= ActionExpander.MaximumDepth; index++)
        {
            var steps = index == ActionExpander.MaximumDepth
                ? "steps: []\n"
                : $"steps:\n  - uses: ./{index + 1}.yaml\n";
            Write($"{index}.yaml", steps);
        }
        var registry = CoreRegistry();

        var exception = Assert.Throws<CliException>(() => Expander(registry).ExpandAction(
            Path.Combine(_root, "0.yaml"), [], _root));

        Assert.Contains("maximum of 64", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parameters:\n  value:\n    type: bool\n    default: maybe\nsteps: []\n", "not valid")]
    [InlineData("parameters:\n  ActionDir:\n    type: string\nsteps: []\n", "reserved")]
    [InlineData("steps:\n  - model-id:\n      kind: []\n", "non-empty")]
    [InlineData("unknown: true\nsteps: []\n", "Unknown action field")]
    [InlineData("version: 2\nsteps: []\n", "Unsupported action version")]
    public void LoaderRejectsInvalidSchemas(string yaml, string expectedMessage)
    {
        Write("invalid.yaml", yaml);

        var exception = Assert.Throws<CliException>(() =>
            new ActionYamlLoader().Load(Path.Combine(_root, "invalid.yaml")));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private ActionExpander Expander(OperationRegistry registry) => new(new ActionYamlLoader(), registry);

    private static OperationRegistry CoreRegistry() => OperationRegistry.Scan(typeof(ILocOperation).Assembly);

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
