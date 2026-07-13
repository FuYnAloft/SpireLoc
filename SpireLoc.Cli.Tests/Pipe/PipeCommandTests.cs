using System.Text.Json;
using SpireLoc.Cli.Pipe;
using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;
using Xunit;

namespace SpireLoc.Cli.Tests.Pipe;

public sealed class PipeCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpireLocCliTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RunsForwardAndReverseLocalizationPipelines()
    {
        var source = Path.Combine(_root, "source");
        var game = Path.Combine(_root, "game");
        var reverted = Path.Combine(_root, "reverted");
        Directory.CreateDirectory(Path.Combine(source, "zhs"));
        File.WriteAllText(Path.Combine(source, "zhs", "cards.yaml"), "CustomCard:\n  title: Test\n");
        var registry = OperationRegistry.Scan(typeof(ILocOperation).Assembly);
        var output = new StringWriter();
        var error = new StringWriter();
        var command = new PipeCommand(registry, output, error);

        var forwardExitCode = command.Run([
            "--input", "yaml", source, "--to", "source",
            "--model-id", "ritsulib", "TestMod", "--from", "source", "--to", "game",
            "--output", "flat-json", game, "--from", "game",
        ]);
        var reverseExitCode = command.Run([
            "--input", "flat-json", game,
            "--model-id", "ritsulib", "TestMod", "--reversed",
            "--output", "yaml", reverted,
        ]);

        Assert.Equal(0, forwardExitCode);
        Assert.Equal(0, reverseExitCode);
        Assert.Equal(string.Empty, error.ToString());

        using var gameDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(game, "zhs", "cards.json")));
        Assert.Equal(
            "Test",
            gameDocument.RootElement.GetProperty("TEST_MOD_CARD_CUSTOM_CARD.title").GetString());
        Assert.Contains("CustomCard:", File.ReadAllText(Path.Combine(reverted, "zhs", "cards.yaml")));
    }

    [Fact]
    public void ParseFailureEndingInHelpPointsToOperationHelp()
    {
        var error = new StringWriter();
        var command = new PipeCommand(
            OperationRegistry.Scan(typeof(ILocOperation).Assembly),
            new StringWriter(),
            error);

        var exception = Assert.Throws<CliException>(() => command.Run(["--input", "yaml", "--help"]));

        Assert.Contains("Unknown option '--help'", exception.Message, StringComparison.Ordinal);
        Assert.EndsWith(
            "hint: Use 'spireloc operation help <path...>' to inspect an operation.",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
