using SpireLoc.Cli.Actions;
using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Pipe;

internal sealed class PipeCommand(OperationRegistry registry, TextWriter output, TextWriter error)
{
    public int Run(IReadOnlyList<string> args)
    {
        var items = new PipeParser(registry).Parse(args);
        var expander = new ActionExpander(new ActionYamlLoader(), registry);
        var invocations = expander.ExpandPipeline(items, Directory.GetCurrentDirectory());
        return new PipelineExecutor(registry, output, error).Execute(invocations);
    }
}
