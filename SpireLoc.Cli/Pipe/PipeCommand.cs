using SpireLoc.Cli.Actions;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Pipe;

internal sealed class PipeCommand(OperationRegistry registry, TextWriter output, TextWriter error)
{
    public int Run(IReadOnlyList<string> args)
    {
        IReadOnlyList<PipelineItem> items;
        try
        {
            items = new PipeParser(registry).Parse(args);
        }
        catch (CliException exception) when (args.Count > 0 && args[^1] == "--help")
        {
            throw new CliException(
                $"{exception.Message}{Environment.NewLine}" +
                "hint: Use 'spireloc operation help <path...>' to inspect an operation.",
                exception);
        }

        var expander = new ActionExpander(new ActionYamlLoader(), registry);
        var invocations = expander.ExpandPipeline(items, Directory.GetCurrentDirectory());
        return new PipelineExecutor(registry, output, error).Execute(invocations);
    }
}
