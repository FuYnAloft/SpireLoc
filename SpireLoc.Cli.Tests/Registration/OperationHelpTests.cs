using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Registration;
using Xunit;

namespace SpireLoc.Cli.Tests.Registration;

public sealed class OperationHelpTests
{
    [Fact]
    public void ListShowsRegisteredPathsAndDescriptions()
    {
        var output = new StringWriter();
        new OperationCommand(CreateRegistry(), output).List();

        Assert.Contains("Operations:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("help-fixture convert", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Convert localization.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrefixHelpListsImmediateSubcommands()
    {
        var output = new StringWriter();
        new OperationCommand(CreateRegistry(), output).PrintHelp(["help-fixture"]);

        var help = output.ToString();
        Assert.Contains("Operation: help-fixture", help, StringComparison.Ordinal);
        Assert.Contains("Subcommands:", help, StringComparison.Ordinal);
        Assert.Contains("convert", help, StringComparison.Ordinal);
        Assert.Contains("nested", help, StringComparison.Ordinal);
        Assert.DoesNotContain("nested leaf", help, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactHelpShowsUsageParametersAndChildren()
    {
        var output = new StringWriter();
        new OperationCommand(CreateRegistry(), output).PrintHelp(["help-fixture", "convert"]);

        var help = output.ToString();
        Assert.Contains("Usage: spireloc pipe --help-fixture convert <source>", help, StringComparison.Ordinal);
        Assert.Contains("Convert localization.", help, StringComparison.Ordinal);
        Assert.Contains("<source>, --source <string>", help, StringComparison.Ordinal);
        Assert.Contains("Input directory. (required)", help, StringComparison.Ordinal);
        Assert.Contains("--count <int>", help, StringComparison.Ordinal);
        Assert.Contains("Number of passes. (default: 1)", help, StringComparison.Ordinal);
        Assert.Contains("Subcommands:", help, StringComparison.Ordinal);
        Assert.Contains("advanced", help, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownPathIsRejected()
    {
        var exception = Assert.Throws<CliException>(() =>
            new OperationCommand(CreateRegistry(), TextWriter.Null).PrintHelp(["missing"]));

        Assert.Contains("Unknown operation path 'missing'", exception.Message, StringComparison.Ordinal);
    }

    private static OperationRegistry CreateRegistry() =>
        OperationRegistry.Scan(typeof(OperationHelpTests).Assembly);

    public static class Fixtures
    {
        [OperationFactory("help-fixture", "convert", Description = "Convert localization.")]
        public static ILocOperation Convert(
            [OperationParameter("source", 0, Description = "Input directory.")] string source,
            [OperationParameter("count", Description = "Number of passes.")] int count = 1) =>
            new MarkerOperation();

        [OperationFactory("help-fixture", "convert", "advanced", Description = "Advanced conversion.")]
        public static ILocOperation Advanced() => new MarkerOperation();

        [OperationFactory("help-fixture", "nested", "leaf", Description = "Nested leaf.")]
        public static ILocOperation Nested() => new MarkerOperation();
    }

    private sealed class MarkerOperation : ILocOperation
    {
        public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) => new(workspace);
    }
}
