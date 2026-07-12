using SpireLoc.Cli.Pipe;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.IO;
using SpireLoc.Core.Steps.Processing.ModelIds;
using SpireLoc.Core.Steps.Support;
using Xunit;

namespace SpireLoc.Cli.Tests.Registration;

public sealed class OperationRegistryTests
{
    [Fact]
    public void CoreFactoriesBindPipelinePositionalsOptionsAndInjectedSlots()
    {
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);

        var operations = ParseAndCompile(registry, [
            "--input", "yaml", "./source", "--to", "source",
            "--model-id", "ritsulib", "TestMod", "--reversed", "--from", "source", "--to", "game",
            "--output", "flat-json", "./output", "--from", "game",
        ]);

        Assert.Collection(
            operations,
            operation =>
            {
                Assert.IsType<ReadYamlLocalizationDirectoryOperation>(operation);
            },
            operation =>
            {
                var step = Assert.IsType<UnaryLocBundleProcessorStep>(operation);
                var processor = Assert.IsType<RitsuLibModelIdProcessor>(step.Processor);
                Assert.Equal("TestMod", processor.ModId);
                Assert.Equal(ModelIdDirection.ToSource, processor.Direction);
                Assert.Equal("source", step.FromSlot);
                Assert.Equal("game", step.ToSlot);
            },
            operation =>
            {
                Assert.IsType<WriteFlatJsonLocalizationDirectoryOperation>(operation);
            });
    }

    [Fact]
    public void PositionalParametersCanAlsoBeSuppliedByName()
    {
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);

        var operations = ParseAndCompile(registry, [
            "--model-id", "ritsulib", "--mod-id", "TestMod",
        ]);

        var step = Assert.IsType<UnaryLocBundleProcessorStep>(Assert.Single(operations));
        var processor = Assert.IsType<RitsuLibModelIdProcessor>(step.Processor);
        Assert.Equal("TestMod", processor.ModId);
        Assert.Equal("main", step.FromSlot);
        Assert.Equal("main", step.ToSlot);
    }

    [Fact]
    public void ScannerSupportsAllFourFactoryShapes()
    {
        var registry = OperationRegistry.Scan(typeof(OperationRegistryTests).Assembly);

        var operations = ParseAndCompile(registry, [
            "--fixture", "static-operation", "first",
            "--fixture", "constructor-operation", "second",
            "--fixture", "static-unary", "third", "--from", "a", "--to", "b",
            "--fixture", "constructor-unary", "fourth",
        ]);

        Assert.Equal("first", Assert.IsType<MarkerOperation>(operations[0]).ToString());
        Assert.Equal("second", Assert.IsType<ConstructorOperation>(operations[1]).ToString());

        var staticUnary = Assert.IsType<UnaryLocBundleProcessorStep>(operations[2]);
        Assert.Equal("third", Assert.IsType<MarkerProcessor>(staticUnary.Processor).ToString());
        Assert.Equal("a", staticUnary.FromSlot);
        Assert.Equal("b", staticUnary.ToSlot);

        var constructorUnary = Assert.IsType<UnaryLocBundleProcessorStep>(operations[3]);
        Assert.Equal("fourth", Assert.IsType<ConstructorProcessor>(constructorUnary.Processor).ToString());
        Assert.Equal("main", constructorUnary.FromSlot);
        Assert.Equal("main", constructorUnary.ToSlot);
    }

    [Fact]
    public void ScannerPreservesFactoryAndParameterDescriptions()
    {
        var registry = OperationRegistry.Scan(typeof(OperationRegistryTests).Assembly);
        var descriptor = registry.Resolve(
            ["fixture", "described"],
            new InvocationSource("test"));

        Assert.Equal("A described processor.", descriptor.Description);
        Assert.Equal("Value to process.", Assert.Single(descriptor.Parameters, parameter => parameter.Name == "value").Description);
        Assert.Equal("Source workspace slot.", Assert.Single(descriptor.Parameters, parameter => parameter.Name == "from").Description);
        Assert.Equal("Destination workspace slot.", Assert.Single(descriptor.Parameters, parameter => parameter.Name == "to").Description);
    }

    [Fact]
    public void DescriptorBindsStringAndIntegerLists()
    {
        var registry = OperationRegistry.Scan(typeof(OperationRegistryTests).Assembly);
        var descriptor = registry.Resolve(
            ["fixture", "lists"],
            new InvocationSource("test"));

        var operation = Assert.IsType<ListOperation>(descriptor.Create(new OperationInvocationSpec(
            descriptor.Path,
            new Dictionary<string, InvocationArgument>
            {
                ["names"] = InvocationArgument.List([
                    InvocationScalar.String("a"),
                    InvocationScalar.String("b"),
                ]),
                ["numbers"] = InvocationArgument.List([
                    InvocationScalar.Integer(1),
                    InvocationScalar.String("2"),
                ]),
            },
            new InvocationSource("test"))));

        Assert.Equal(["a", "b"], operation.Names);
        Assert.Equal([1, 2], operation.Numbers);
        Assert.Contains(
            "--numbers <value> [--numbers <value> ...]",
            descriptor.GetUsage(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorRejectsScalarForListParameter()
    {
        var registry = OperationRegistry.Scan(typeof(OperationRegistryTests).Assembly);
        var descriptor = registry.Resolve(
            ["fixture", "lists"],
            new InvocationSource("test"));

        var exception = Assert.Throws<CliException>(() => descriptor.Create(new OperationInvocationSpec(
            descriptor.Path,
            new Dictionary<string, InvocationArgument>
            {
                ["names"] = InvocationArgument.Scalar(InvocationScalar.String("a")),
                ["numbers"] = InvocationArgument.List([]),
            },
            new InvocationSource("test"))));

        Assert.Contains("Parameter 'names'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("requires a list value", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--input|yaml", "Missing required parameter 'path'")]
    [InlineData("--model-id|unknown", "Unknown subcommand")]
    [InlineData("--model-id|ritsulib|TestMod|--unknown", "Unknown option '--unknown'")]
    [InlineData("--model-id|ritsulib|TestMod|--mod-id|OtherMod", "supplied more than once")]
    public void ParserReportsInvalidPipelines(string command, string expectedMessage)
    {
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);

        var exception = Assert.Throws<CliException>(() =>
            ParseAndCompile(registry, command.Split('|')));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    public static class StaticFactories
    {
        [OperationFactory("fixture", "static-operation")]
        public static ILocOperation CreateOperation(
            [OperationParameter("value", 0)] string value) =>
            new MarkerOperation(value);

        [OperationFactory("fixture", "static-unary")]
        public static UnaryLocBundleProcessor CreateUnary(
            [OperationParameter("value", 0)] string value) =>
            new MarkerProcessor(value);

        [OperationFactory("fixture", "lists")]
        public static ILocOperation CreateLists(
            [OperationParameter("names", 0)] IReadOnlyList<string> names,
            [OperationParameter("numbers")] IReadOnlyList<int> numbers) =>
            new ListOperation(names, numbers);

        [OperationFactory("fixture", "described", Description = "A described processor.")]
        public static UnaryLocBundleProcessor CreateDescribed(
            [OperationParameter("value", 0, Description = "Value to process.")] string value) =>
            new MarkerProcessor(value);
    }

    [method: OperationFactory("fixture", "constructor-operation")]
    public sealed class ConstructorOperation(
        [OperationParameter("value", 0)] string value) : ILocOperation
    {
        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);

        public override string ToString() => value;
    }

    [method: OperationFactory("fixture", "constructor-unary")]
    public sealed class ConstructorProcessor(
        [OperationParameter("value", 0)] string value) : UnaryLocBundleProcessor
    {
        public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null) => bundle;

        public override string ToString() => value;
    }

    public sealed class MarkerOperation(string value) : ILocOperation
    {
        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);

        public override string ToString() => value;
    }

    public sealed class MarkerProcessor(string value) : UnaryLocBundleProcessor
    {
        public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null) => bundle;

        public override string ToString() => value;
    }

    public sealed class ListOperation(
        IReadOnlyList<string> names,
        IReadOnlyList<int> numbers) : ILocOperation
    {
        public IReadOnlyList<string> Names { get; } = names;
        public IReadOnlyList<int> Numbers { get; } = numbers;

        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);
    }

    private static IReadOnlyList<ILocOperation> ParseAndCompile(
        OperationRegistry registry,
        IReadOnlyList<string> tokens)
    {
        var invocations = new PipeParser(registry).Parse(tokens).Cast<OperationInvocationSpec>().ToArray();
        return new OperationCompiler(registry).Compile(invocations);
    }
}
