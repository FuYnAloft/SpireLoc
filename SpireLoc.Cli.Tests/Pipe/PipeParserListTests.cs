using SpireLoc.Cli.Pipe;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Registration;
using Xunit;

namespace SpireLoc.Cli.Tests.Pipe;

public sealed class PipeParserListTests
{
    [Fact]
    public void RepeatedNamedListOptionsAppendInCommandLineOrder()
    {
        var operations = ParseAndCompile([
            "--list-fixture", "named",
            "--value", "a",
            "--value", "b",
            "--value", "c",
        ]);

        var operation = Assert.IsType<StringListOperation>(Assert.Single(operations));
        Assert.Equal(["a", "b", "c"], operation.Values);
    }

    [Fact]
    public void FinalPositionalListConsumesRemainingValuesAndStopsAtNextStep()
    {
        var operations = ParseAndCompile([
            "--list-fixture", "positional", "prefix", "a",
            "--values", "b",
            "c",
            "--list-fixture", "after", "next",
        ]);

        var list = Assert.IsType<PositionalListOperation>(operations[0]);
        Assert.Equal("prefix", list.Prefix);
        Assert.Equal(["a", "b", "c"], list.Values);

        var after = Assert.IsType<AfterOperation>(operations[1]);
        Assert.Equal("next", after.Value);
    }

    [Fact]
    public void IntegerListOptionsAreConvertedElementByElement()
    {
        var operations = ParseAndCompile([
            "--list-fixture", "integers",
            "--value", "10",
            "--value", "-20",
        ]);

        var operation = Assert.IsType<IntegerListOperation>(Assert.Single(operations));
        Assert.Equal([10, -20], operation.Values);
    }

    [method: OperationFactory("list-fixture", "named")]
    public sealed class StringListOperation(
        [OperationParameter("value")] IReadOnlyList<string> values) : ILocOperation
    {
        public IReadOnlyList<string> Values { get; } = values;

        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);
    }

    [method: OperationFactory("list-fixture", "positional")]
    public sealed class PositionalListOperation(
        [OperationParameter("prefix", 0)] string prefix,
        [OperationParameter("values", 1)] IReadOnlyList<string> values) : ILocOperation
    {
        public string Prefix { get; } = prefix;
        public IReadOnlyList<string> Values { get; } = values;

        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);
    }

    [method: OperationFactory("list-fixture", "integers")]
    public sealed class IntegerListOperation(
        [OperationParameter("value")] IReadOnlyList<int> values) : ILocOperation
    {
        public IReadOnlyList<int> Values { get; } = values;

        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);
    }

    [method: OperationFactory("list-fixture", "after")]
    public sealed class AfterOperation(
        [OperationParameter("value", 0)] string value) : ILocOperation
    {
        public string Value { get; } = value;

        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);
    }

    private static IReadOnlyList<ILocOperation> ParseAndCompile(IReadOnlyList<string> tokens)
    {
        var registry = OperationRegistry.Scan(typeof(PipeParserListTests).Assembly);
        var invocations = new PipeParser(registry).Parse(tokens).Cast<OperationInvocationSpec>().ToArray();
        return new OperationCompiler(registry).Compile(invocations);
    }
}
