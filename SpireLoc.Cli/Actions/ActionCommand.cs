using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Actions;

internal sealed class ActionCommand(OperationRegistry registry, TextWriter output, TextWriter error)
{
    public int Run(string actionPath, IReadOnlyList<string> args)
    {
        var expander = new ActionExpander(new ActionYamlLoader(), registry);
        var invocations = expander.ExpandAction(actionPath, args, Directory.GetCurrentDirectory());
        return new PipelineExecutor(registry, output, error).Execute(invocations);
    }
}
