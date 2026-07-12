using SpireLoc.Cli.Pipeline;
using SpireLoc.Cli.Registration;

namespace SpireLoc.Cli.Actions;

internal sealed class ActionCommand
{
    private readonly OperationRegistry _registry;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly ActionYamlLoader _loader;
    private readonly BuiltinActionCatalog _builtins;

    public ActionCommand(OperationRegistry registry, TextWriter output, TextWriter error)
    {
        _registry = registry;
        _output = output;
        _error = error;
        _loader = new ActionYamlLoader();
        _builtins = new BuiltinActionCatalog(_loader, typeof(ActionCommand).Assembly);
    }

    public int Run(string actionPath, IReadOnlyList<string> args)
    {
        var expander = new ActionExpander(_loader, _builtins, _registry);
        var invocations = expander.ExpandAction(actionPath, args, Directory.GetCurrentDirectory());
        return new PipelineExecutor(_registry, _output, _error).Execute(invocations);
    }

    public void List()
    {
        ActionHelpFormatter.WriteList(_output, _builtins.Entries);
    }

    public void PrintHelp(string actionReference)
    {
        var document = _builtins.TryLoad(actionReference, out var builtin)
            ? builtin
            : _loader.Load(Path.Combine(Directory.GetCurrentDirectory(), actionReference));
        ActionHelpFormatter.WriteAction(_output, actionReference, document);
    }
}
